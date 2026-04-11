using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Bridge.Enums;
using Bridge.Exceptions.EyeTracker;
using Bridge.Extensions;
using Bridge.Models;
using Bridge.Output;
using eyelogic;

namespace Bridge.EyeTrackers.EyeLogic;

public class EyeLogic(Func<object, bool, Task> wsResponse) : EyeTracker
{
    public override EyeTrackerState State { get; set; } = EyeTrackerState.Disconnected;
    public override Func<object, bool, Task> WsResponse { get; init; } = wsResponse;
    public override DateTime? LastCalibration { get; set; } = null;
    private DELCsApi? Api { get; set; } = null;
    private BlockingCollection<GazeSample> _gazeSamples = new(new ConcurrentQueue<GazeSample>());
    private ConcurrentQueue<FixationStartSample> _fixationStartSamples = new();
    private volatile bool _processingFixationStart = false;
    private ConcurrentQueue<FixationEndSample> _fixationEndSamples = new();
    private volatile bool _processingFixationEnd = false;
    private volatile DELCsApi.ScreenConfig? _screenConfig = null;
    private ConcurrentDictionary<int, int> _fixationIndexCache = new();
    private int _fixationCount = 1;
    private Thread? _sampleThread = null;
    private CancellationTokenSource _threadCancel = new();
    private System.Threading.Timer? _healthCheckTimer = null;

    /// <summary>
    /// Maximum number of entries kept in the fixation index cache before pruning.
    /// Prevents unbounded memory growth during long sessions.
    /// </summary>
    private const int MaxFixationCacheSize = 10_000;
    
    /// <summary>
    /// Interval in milliseconds between connection health checks.
    /// </summary>
    private const int HealthCheckIntervalMs = 5_000;
    
    public override async Task<bool> Connect()
    {
        State = EyeTrackerState.Connecting;

        try
        {
            Api = new DELCsApi("Develex Bridge client");

            Api.OnDeviceEvent += OnEyeLogicApiEvent;
            Api.OnGazeSample += OnEyeLogicGazeSample;
            Api.OnFixationStartSample += OnFixationStartSample;
            Api.OnFixationEndSample += OnFixationEndSample;

            Api.connect();

            var deviceConfig = Api.getDeviceConfig();

            if (deviceConfig == null)
            {
                throw new EyeTrackerUnableToConnect("eyelogic config not found - device not connected");
            }

            _screenConfig = Api.getActiveScreen();

            if (_screenConfig == null)
            {
                throw new EyeTrackerUnableToConnect("eyelogic screen config not found - device not connected");
            }
            
            StartSampleThread();
            StartHealthCheckTimer();
        }
        catch (Exception e)
        {
            if (Api != null)
            {
                try
                {
                    Api.disconnect();
                    Api.destroy();
                }
                catch
                {
                    // Ignore cleanup errors during failed connect
                }
                Api = null;
            }
            
            StopHealthCheckTimer();
            StopSampleThread();
            
            State = EyeTrackerState.Disconnected;
            throw;
        }

        State = EyeTrackerState.Connected;
        
        ConsoleOutput.EyeLogicEvent("Connected to EyeLogic device");

        await Task.Delay(1);

        return true;
    }

    public override async Task<bool> Start()
    {
        if (!IsConnected())
        {
            throw new EyeTrackerNotConnected("device not connected");
        }

        Api.requestTracking(0);

        State = EyeTrackerState.Started;
        
        ConsoleOutput.EtStartedRecording();

        await Task.Delay(1);

        return true;
    }

    public override async Task<bool> Stop()
    {
        if (!IsConnected())
        {
            throw new EyeTrackerNotConnected("device not connected");
        }

        Api.unrequestTracking();
        
        State = EyeTrackerState.Connected;
        
        ConsoleOutput.EtStoppedRecording();

        await Task.Delay(1);

        return true;
    }

    public override async Task<bool> Calibrate()
    {
        if (!IsConnected())
        {
            throw new EyeTrackerNotConnected("device not connected");
        }

        // Remember whether we were tracking before calibration so we know
        // whether to leave tracking on afterward.
        var wasStarted = State == EyeTrackerState.Started;

        State = EyeTrackerState.Calibrating;

        // EyeLogic requires tracking to be active during calibration.
        // Only request tracking if we weren't already tracking.
        if (!wasStarted)
        {
            Api.requestTracking(0);
        }

        try
        {
            Api.calibrate(0);
        }
        catch (Exception)
        {
            // On calibration failure, restore to previous tracking state
            if (!wasStarted)
            {
                try { Api.unrequestTracking(); } catch { /* ignore cleanup error */ }
            }
            State = wasStarted ? EyeTrackerState.Started : EyeTrackerState.Connected;
            throw;
        }

        // Calibration succeeded — unrequest tracking only if we weren't tracking before.
        if (!wasStarted)
        {
            Api.unrequestTracking();
            State = EyeTrackerState.Connected;
        }
        else
        {
            // We were tracking before, leave tracking on so calibration
            // stays applied to the active tracking session.
            State = EyeTrackerState.Started;
        }
        
        // Refresh screen config after calibration in case it changed
        try
        {
            var newScreenConfig = Api.getActiveScreen();
            if (newScreenConfig != null)
            {
                _screenConfig = newScreenConfig;
            }
        }
        catch (Exception ex)
        {
            ConsoleOutput.EyeLogicEvent($"Warning: could not refresh screen config after calibration: {ex.Message}");
        }

        LastCalibration = DateTime.UtcNow;
        
        ConsoleOutput.EyeLogicEvent("Calibration completed successfully");

        await Task.Delay(1);

        return true;
    }

    public override async Task<bool> Disconnect()
    {
        if (!IsConnected())
        {
            throw new EyeTrackerNotConnected("device not connected");
        }
        
        StopHealthCheckTimer();
        StopSampleThread();
        
        try
        {
            Api.disconnect();
            Api.destroy();
        }
        catch (Exception ex)
        {
            ConsoleOutput.EyeLogicEvent($"Warning during disconnect cleanup: {ex.Message}");
        }
        
        Api = null;
        _screenConfig = null;
        ClearFixationState();

        State = EyeTrackerState.Disconnected;

        await Task.Delay(1);

        return true;
    }

    [MemberNotNullWhen(true, nameof(Api))]
    private bool IsConnected()
    {
        return Api != null;
    }

    /// <summary>
    /// Handles device events from the EyeLogic API.
    /// This is CRITICAL for detecting connection loss, device disconnects,
    /// tracking stops, and screen changes that would invalidate calibration.
    /// </summary>
    private void OnEyeLogicApiEvent(DeviceEventType eventType)
    {
        ConsoleOutput.EyeLogicEvent($"Device event received: {eventType}");
        
        switch (eventType)
        {
            case DeviceEventType.CONNECTION_CLOSED:
                HandleConnectionLost("EyeLogic server connection was closed");
                break;
                
            case DeviceEventType.DEVICE_DISCONNECTED:
                HandleConnectionLost("EyeLogic device was disconnected");
                break;
                
            case DeviceEventType.TRACKING_STOPPED:
                HandleTrackingStopped();
                break;
                
            case DeviceEventType.SCREEN_CHANGED:
                HandleScreenChanged();
                break;
                
            case DeviceEventType.DEVICE_CONNECTED:
                ConsoleOutput.EyeLogicEvent("EyeLogic device reconnected");
                break;
        }
    }
    
    /// <summary>
    /// Handles connection loss or device disconnection.
    /// Resets the bridge state and notifies WebSocket clients.
    /// </summary>
    private void HandleConnectionLost(string reason)
    {
        ConsoleOutput.EyeLogicEvent($"CONNECTION LOST: {reason}");
        
        var previousState = State;
        State = EyeTrackerState.Disconnected;
        LastCalibration = null;
        _screenConfig = null;
        
        StopHealthCheckTimer();
        StopSampleThread();
        
        // Clean up API reference - it's no longer usable
        if (Api != null)
        {
            try
            {
                Api.destroy();
            }
            catch
            {
                // API may already be in a bad state
            }
            Api = null;
        }
        
        ClearFixationState();
        
        // Notify clients about the connection loss
        _ = WsResponse(new WsOutgoingErrorMessage(
            $"EyeLogic connection lost: {reason}. Calibration has been invalidated. Please reconnect and recalibrate."), true);
    }
    
    /// <summary>
    /// Handles the case when tracking is stopped externally (e.g., by EyeLogic server).
    /// This can happen when another application takes over or the server decides to stop.
    /// </summary>
    private void HandleTrackingStopped()
    {
        ConsoleOutput.EyeLogicEvent("Tracking was stopped externally by EyeLogic server");
        
        if (State == EyeTrackerState.Started)
        {
            State = EyeTrackerState.Connected;
            
            // Notify clients that tracking was stopped externally
            _ = WsResponse(new WsOutgoingErrorMessage(
                "EyeLogic tracking was stopped externally. You may need to recalibrate and restart."), true);
        }
        else if (State == EyeTrackerState.Calibrating)
        {
            // Tracking stopped during calibration — calibration likely failed
            ConsoleOutput.EyeLogicEvent("WARNING: Tracking stopped during calibration — calibration may have failed");
        }
    }
    
    /// <summary>
    /// Handles screen configuration changes. Refreshes the screen config
    /// and warns that calibration may be invalid for the new screen.
    /// </summary>
    private void HandleScreenChanged()
    {
        ConsoleOutput.EyeLogicEvent("Screen configuration changed — refreshing and invalidating calibration");
        
        if (Api != null)
        {
            try
            {
                var newConfig = Api.getActiveScreen();
                if (newConfig != null)
                {
                    _screenConfig = newConfig;
                    ConsoleOutput.EyeLogicEvent(
                        $"Screen config updated: {newConfig.resolutionX}x{newConfig.resolutionY} ({newConfig.name})");
                }
            }
            catch (Exception ex)
            {
                ConsoleOutput.EyeLogicEvent($"Failed to refresh screen config: {ex.Message}");
            }
        }
        
        // Screen change invalidates calibration
        LastCalibration = null;
        
        _ = WsResponse(new WsOutgoingErrorMessage(
            "Screen configuration changed. Calibration has been invalidated. Please recalibrate."), true);
    }

    private void StartSampleThread()
    {
        _threadCancel = new CancellationTokenSource();
        _sampleThread = new Thread(SampleThread)
        {
            IsBackground = true,
            Name = "EyeLogic-GazeSampleThread"
        };
        _sampleThread.Start();
    }

    private void StopSampleThread()
    { 
        _threadCancel.Cancel();
        
        if (_sampleThread != null)
        {
            _sampleThread.Join(timeout: TimeSpan.FromSeconds(3));
            _sampleThread = null;
            _threadCancel.Dispose();
        }
    }
    
    /// <summary>
    /// Starts a periodic health check timer that verifies the EyeLogic connection is still alive.
    /// </summary>
    private void StartHealthCheckTimer()
    {
        _healthCheckTimer = new System.Threading.Timer(HealthCheckCallback, null, HealthCheckIntervalMs, HealthCheckIntervalMs);
    }
    
    private void StopHealthCheckTimer()
    {
        if (_healthCheckTimer != null)
        {
            _healthCheckTimer.Dispose();
            _healthCheckTimer = null;
        }
    }
    
    /// <summary>
    /// Periodic health check that verifies the EyeLogic API connection is still active.
    /// If the connection is lost silently (without a device event), this will catch it.
    /// </summary>
    private void HealthCheckCallback(object? state)
    {
        try
        {
            if (Api == null || State == EyeTrackerState.Disconnected)
                return;
            
            if (!Api.isConnected())
            {
                ConsoleOutput.EyeLogicEvent("Health check: EyeLogic connection lost (detected by polling)");
                HandleConnectionLost("Connection lost (detected by health check)");
            }
        }
        catch (Exception ex)
        {
            ConsoleOutput.EyeLogicEvent($"Health check error: {ex.Message}");
        }
    }

    /// <summary>
    /// Main gaze sample processing thread. Dequeues samples and sends them via WebSocket.
    /// Uses BlockingCollection.Take to sleep efficiently when no data is available,
    /// instead of burning CPU with a tight spin loop.
    /// </summary>
    private async void SampleThread()
    {
        try
        {
            foreach (var gazeSample in _gazeSamples.GetConsumingEnumerable(_threadCancel.Token))
            {
                if (State != EyeTrackerState.Started && State != EyeTrackerState.Calibrating)
                    continue;

                var screenConfig = _screenConfig;
                if (screenConfig == null) continue;

                var resX = screenConfig.resolutionX;
                var resY = screenConfig.resolutionY;

                var gazeOutput = new WsOutgoingGazeMessage
                {
                    DeviceId = gazeSample.index,
                    LeftX = gazeSample.porLeft.x <= double.MinValue ? 0 : gazeSample.porLeft.x / resX,
                    LeftY = gazeSample.porLeft.y <= double.MinValue ? 0 : gazeSample.porLeft.y / resY,
                    RightX = gazeSample.porRight.x <= double.MinValue ? 0 : gazeSample.porRight.x / resX,
                    RightY = gazeSample.porRight.y <= double.MinValue ? 0 : gazeSample.porRight.y / resY,
                    LeftValidity = gazeSample.porLeft.x > double.MinValue && gazeSample.porLeft.y > double.MinValue,
                    RightValidity = gazeSample.porRight.x > double.MinValue && gazeSample.porRight.y > double.MinValue,
                    LeftPupil = gazeSample.pupilRadiusLeft,
                    RightPupil = gazeSample.pupilRadiusRight,
                    Timestamp = DateTimeExtensions.IsoNow,
                    DeviceTimestamp = MicrosecToIso(gazeSample.timestampMicroSec)
                };

                await WsResponse(gazeOutput, false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when _threadCancel is cancelled during shutdown
        }
    }

    private void OnEyeLogicGazeSample(GazeSample gazeSample)
    {
        _gazeSamples.Add(gazeSample);
    }

    private void OnFixationStartSample(FixationStartSample fixationStartSample)
    {
        _fixationStartSamples.Enqueue(fixationStartSample);

        if (!_processingFixationStart)
        {
            var screenConfig = _screenConfig;
            if (screenConfig == null)
            {
                _ = WsResponse(new WsOutgoingErrorMessage("unable to get screen config, disconnect and connect again!"), true);
                return;
            }

            _ = ProcessFixationStartSample(screenConfig.resolutionX, screenConfig.resolutionY);
        }
    }

    private async Task ProcessFixationStartSample(int resX, int resY)
    {
        _processingFixationStart = true;

        try
        {
            while (_fixationStartSamples.TryDequeue(out var fixationStartSample))
            {
                var fixationId = Interlocked.Increment(ref _fixationCount);
                _fixationIndexCache[fixationStartSample.index] = fixationId;
                
                // Prune cache if it grows too large to prevent memory leaks
                PruneFixationCacheIfNeeded();

                var gazeOutput = new WsOutgoingFixationStartMessage()
                {
                    FixationId = fixationId,
                    GazeDeviceId = fixationStartSample.index,
                    X = fixationStartSample.por.x / resX,
                    Y = fixationStartSample.por.y / resY,
                    Duration = 0,
                    Timestamp = DateTimeExtensions.IsoNow,
                    DeviceTimestamp = MicrosecToIso(fixationStartSample.timestampMicroSec)
                };

                await WsResponse(gazeOutput, false);
            }
        }
        catch (Exception ex)
        {
            ConsoleOutput.EyeLogicEvent($"Error processing fixation start: {ex.Message}");
        }
        finally
        {
            _processingFixationStart = false;
        }
    }

    private void OnFixationEndSample(FixationEndSample fixationEndSample)
    {
        _fixationEndSamples.Enqueue(fixationEndSample);

        if (!_processingFixationEnd)
        {
            var screenConfig = _screenConfig;
            if (screenConfig == null)
            {
                _ = WsResponse(new WsOutgoingErrorMessage("unable to get screen config, disconnect and connect again!"), true);
                return;
            }

            _ = ProcessFixationEndSample(screenConfig.resolutionX, screenConfig.resolutionY);
        }
    }

    private async Task ProcessFixationEndSample(int resX, int resY)
    {
        _processingFixationEnd = true;

        try
        {
            while (_fixationEndSamples.TryDequeue(out var fixationEndSample))
            {
                var gazeOutput = new WsOutgoingFixationEndMessage()
                {
                    FixationId = _fixationIndexCache.GetValueOrDefault(fixationEndSample.indexStart, -1),
                    GazeDeviceId = fixationEndSample.index,
                    X = fixationEndSample.por.x / resX,
                    Y = fixationEndSample.por.y / resY,
                    Duration = (fixationEndSample.timestampMicroSec - fixationEndSample.timestampStartMicroSec) / 1000f,
                    Timestamp = DateTimeExtensions.IsoNow,
                    DeviceTimestamp = MicrosecToIso(fixationEndSample.timestampMicroSec)
                };

                await WsResponse(gazeOutput, false);
            }
        }
        catch (Exception ex)
        {
            ConsoleOutput.EyeLogicEvent($"Error processing fixation end: {ex.Message}");
        }
        finally
        {
            _processingFixationEnd = false;
        }
    }
    
    /// <summary>
    /// Prunes the fixation index cache when it exceeds the maximum size.
    /// Prevents unbounded memory growth during long recording sessions.
    /// </summary>
    private void PruneFixationCacheIfNeeded()
    {
        if (_fixationIndexCache.Count > MaxFixationCacheSize)
        {
            // Keep only the most recent half of entries
            var keysToRemove = _fixationIndexCache
                .OrderBy(kv => kv.Value)
                .Take(_fixationIndexCache.Count / 2)
                .Select(kv => kv.Key)
                .ToList();
            
            foreach (var key in keysToRemove)
            {
                _fixationIndexCache.TryRemove(key, out _);
            }
            
            ConsoleOutput.EyeLogicEvent($"Pruned fixation cache from {MaxFixationCacheSize}+ to {_fixationIndexCache.Count} entries");
        }
    }
    
    /// <summary>
    /// Converts a device timestamp in microseconds to an ISO 8601 string,
    /// preserving full microsecond precision (no integer-division truncation).
    /// </summary>
    private static string MicrosecToIso(long microseconds)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(microseconds / 1000)
            .UtcDateTime.ToIso();
    }
    
    /// <summary>
    /// Clears all fixation-related state. Called on disconnect or connection loss.
    /// </summary>
    private void ClearFixationState()
    {
        _fixationIndexCache.Clear();
        _fixationCount = 1;
        
        // Drain queues
        while (_gazeSamples.TryTake(out _)) { }
        while (_fixationStartSamples.TryDequeue(out _)) { }
        while (_fixationEndSamples.TryDequeue(out _)) { }
    }
}
