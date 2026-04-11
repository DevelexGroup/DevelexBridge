using System.Collections.Concurrent;
using Bridge.Enums;
using Bridge.Exceptions.EyeTracker;
using Bridge.Extensions;
using Bridge.Models;
using Bridge.Output;

namespace Bridge.EyeTrackers.Mock;

public class MockEyeTracker(Func<object, bool, Task> wsResponse) : EyeTracker
{
    public override EyeTrackerState State { get; set; } = EyeTrackerState.Disconnected;
    public override Func<object, bool, Task> WsResponse { get; init; } = wsResponse;
    public override DateTime? LastCalibration { get; set; } = null;

    private MockDevice? Device { get; set; } = null;
    private BlockingCollection<MockGazeSample> _gazeSamples = new(new ConcurrentQueue<MockGazeSample>());
    private ConcurrentQueue<MockFixationStartSample> _fixationStartSamples = new();
    private volatile bool _processingFixationStart = false;
    private ConcurrentQueue<MockFixationEndSample> _fixationEndSamples = new();
    private volatile bool _processingFixationEnd = false;
    private volatile MockDevice.ScreenConfig? _screenConfig = null;
    private ConcurrentDictionary<int, int> _fixationIndexCache = new();
    private int _fixationCount = 1;
    private Thread? _sampleThread = null;
    private CancellationTokenSource _threadCancel = new();

    private const int MaxFixationCacheSize = 10_000;

    public override async Task<bool> Connect()
    {
        State = EyeTrackerState.Connecting;

        try
        {
            Device = new MockDevice("Develex Bridge client");

            Device.OnDeviceEvent += OnDeviceEvent;
            Device.OnGazeSample += OnGazeSample;
            Device.OnFixationStartSample += OnFixationStartSample;
            Device.OnFixationEndSample += OnFixationEndSample;

            Device.connect();

            var deviceConfig = Device.getDeviceConfig();

            if (deviceConfig == null)
            {
                throw new EyeTrackerUnableToConnect("mock device config not found");
            }

            _screenConfig = Device.getActiveScreen();

            if (_screenConfig == null)
            {
                throw new EyeTrackerUnableToConnect("mock screen config not found");
            }

            StartSampleThread();
        }
        catch (Exception)
        {
            if (Device != null)
            {
                try
                {
                    Device.disconnect();
                    Device.destroy();
                }
                catch
                {
                    // Ignore cleanup errors during failed connect
                }
                Device = null;
            }

            StopSampleThread();
            State = EyeTrackerState.Disconnected;
            throw;
        }

        State = EyeTrackerState.Connected;

        ConsoleOutput.MockEvent("Connected to Mock eye tracker (120 Hz)");

        await Task.Delay(1);
        return true;
    }

    public override async Task<bool> Start()
    {
        if (Device == null || State != EyeTrackerState.Connected)
        {
            throw new EyeTrackerNotConnected("device not connected");
        }

        Device.requestTracking(0);

        State = EyeTrackerState.Started;

        ConsoleOutput.EtStartedRecording();

        await Task.Delay(1);
        return true;
    }

    public override async Task<bool> Stop()
    {
        if (Device == null || State != EyeTrackerState.Started)
        {
            throw new EyeTrackerNotConnected("device not started");
        }

        Device.unrequestTracking();

        State = EyeTrackerState.Connected;

        ConsoleOutput.EtStoppedRecording();

        await Task.Delay(1);
        return true;
    }

    public override async Task<bool> Calibrate()
    {
        if (Device == null || State == EyeTrackerState.Disconnected)
        {
            throw new EyeTrackerNotConnected("device not connected");
        }

        var wasStarted = State == EyeTrackerState.Started;

        State = EyeTrackerState.Calibrating;

        if (!wasStarted)
        {
            Device.requestTracking(0);
        }

        try
        {
            Device.calibrate(0);
        }
        catch (Exception)
        {
            if (!wasStarted)
            {
                try { Device.unrequestTracking(); } catch { /* ignore cleanup error */ }
            }
            State = wasStarted ? EyeTrackerState.Started : EyeTrackerState.Connected;
            throw;
        }

        if (!wasStarted)
        {
            Device.unrequestTracking();
            State = EyeTrackerState.Connected;
        }
        else
        {
            State = EyeTrackerState.Started;
        }

        LastCalibration = DateTime.UtcNow;

        ConsoleOutput.MockEvent("Mock calibration completed");

        await Task.Delay(1);
        return true;
    }

    public override async Task<bool> Disconnect()
    {
        if (Device == null || State == EyeTrackerState.Disconnected)
        {
            throw new EyeTrackerNotConnected("device not connected");
        }

        StopSampleThread();

        try
        {
            Device.disconnect();
            Device.destroy();
        }
        catch (Exception ex)
        {
            ConsoleOutput.MockEvent($"Warning during disconnect cleanup: {ex.Message}");
        }

        Device = null;
        _screenConfig = null;
        ClearFixationState();

        State = EyeTrackerState.Disconnected;

        ConsoleOutput.MockEvent("Disconnected from Mock eye tracker");

        await Task.Delay(1);
        return true;
    }

    // ─── Thread management ─────────────────────────────────────────

    private void StartSampleThread()
    {
        _threadCancel = new CancellationTokenSource();
        _sampleThread = new Thread(SampleThread)
        {
            IsBackground = true,
            Name = "Mock-GazeSampleThread"
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

    // ─── Sample processing (mirrors EyeLogic pattern) ──────────────

    /// <summary>
    /// Main gaze sample processing thread. Dequeues samples and sends them via WebSocket.
    /// Uses BlockingCollection.Take to sleep efficiently when no data is available.
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
                    LeftX = gazeSample.porLeft.x / resX,
                    LeftY = gazeSample.porLeft.y / resY,
                    RightX = gazeSample.porRight.x / resX,
                    RightY = gazeSample.porRight.y / resY,
                    LeftValidity = true,
                    RightValidity = true,
                    LeftPupil = gazeSample.pupilRadiusLeft,
                    RightPupil = gazeSample.pupilRadiusRight,
                    Timestamp = DateTimeExtensions.IsoNow,
                    DeviceTimestamp = MicrosecToIso(gazeSample.timestampMicroSec)
                };

                WsResponse(gazeOutput, false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when _threadCancel is cancelled during shutdown
        }
    }

    // ─── Device event handlers ─────────────────────────────────────

    private void OnDeviceEvent(MockDeviceEventType eventType)
    {
        ConsoleOutput.MockEvent($"Device event: {eventType}");
    }

    private void OnGazeSample(MockGazeSample gazeSample)
    {
        _gazeSamples.Add(gazeSample);
    }

    private void OnFixationStartSample(MockFixationStartSample fixationStartSample)
    {
        _fixationStartSamples.Enqueue(fixationStartSample);

        if (!_processingFixationStart)
        {
            var screenConfig = _screenConfig;
            if (screenConfig == null) return;

            _ = ProcessFixationStartSample(screenConfig.resolutionX, screenConfig.resolutionY);
        }
    }

    private async Task ProcessFixationStartSample(int resX, int resY)
    {
        _processingFixationStart = true;

        try
        {
            while (_fixationStartSamples.TryDequeue(out var sample))
            {
                var fixationId = Interlocked.Increment(ref _fixationCount);
                _fixationIndexCache[sample.index] = fixationId;

                PruneFixationCacheIfNeeded();

                var output = new WsOutgoingFixationStartMessage()
                {
                    FixationId = fixationId,
                    GazeDeviceId = sample.index,
                    X = sample.por.x / resX,
                    Y = sample.por.y / resY,
                    Duration = 0,
                    Timestamp = DateTimeExtensions.IsoNow,
                    DeviceTimestamp = MicrosecToIso(sample.timestampMicroSec)
                };

                await WsResponse(output, false);
            }
        }
        catch (Exception ex)
        {
            ConsoleOutput.MockEvent($"Error processing fixation start: {ex.Message}");
        }
        finally
        {
            _processingFixationStart = false;
        }
    }

    private void OnFixationEndSample(MockFixationEndSample fixationEndSample)
    {
        _fixationEndSamples.Enqueue(fixationEndSample);

        if (!_processingFixationEnd)
        {
            var screenConfig = _screenConfig;
            if (screenConfig == null) return;

            _ = ProcessFixationEndSample(screenConfig.resolutionX, screenConfig.resolutionY);
        }
    }

    private async Task ProcessFixationEndSample(int resX, int resY)
    {
        _processingFixationEnd = true;

        try
        {
            while (_fixationEndSamples.TryDequeue(out var sample))
            {
                var output = new WsOutgoingFixationEndMessage()
                {
                    FixationId = _fixationIndexCache.GetValueOrDefault(sample.indexStart, -1),
                    GazeDeviceId = sample.index,
                    X = sample.por.x / resX,
                    Y = sample.por.y / resY,
                    Duration = (sample.timestampMicroSec - sample.timestampStartMicroSec) / 1000f,
                    Timestamp = DateTimeExtensions.IsoNow,
                    DeviceTimestamp = MicrosecToIso(sample.timestampMicroSec)
                };

                await WsResponse(output, false);
            }
        }
        catch (Exception ex)
        {
            ConsoleOutput.MockEvent($"Error processing fixation end: {ex.Message}");
        }
        finally
        {
            _processingFixationEnd = false;
        }
    }

    // ─── Helpers ───────────────────────────────────────────────────

    private static string MicrosecToIso(long microseconds)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(microseconds / 1000)
            .UtcDateTime.ToIso();
    }

    private void PruneFixationCacheIfNeeded()
    {
        if (_fixationIndexCache.Count > MaxFixationCacheSize)
        {
            var keysToRemove = _fixationIndexCache
                .OrderBy(kv => kv.Value)
                .Take(_fixationIndexCache.Count / 2)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _fixationIndexCache.TryRemove(key, out _);
            }
        }
    }

    private void ClearFixationState()
    {
        _fixationIndexCache.Clear();
        _fixationCount = 1;

        while (_gazeSamples.TryTake(out _)) { }
        while (_fixationStartSamples.TryDequeue(out _)) { }
        while (_fixationEndSamples.TryDequeue(out _)) { }
    }
}