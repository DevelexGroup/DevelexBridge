using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
    private ConcurrentQueue<GazeSample> _gazeSamples = new();
    private ConcurrentQueue<FixationStartSample> _fixationStartSamples = new();
    private bool _processingFixationStart = false;
    private ConcurrentQueue<FixationEndSample> _fixationEndSamples = new();
    private bool _processingFixationEnd = false;
    private DELCsApi.ScreenConfig? _screenConfig = null;
    private Dictionary<int, int> _fixationIndexCache = new();
    private int _fixationCount = 1;
    private Thread? _sampleThread = null;
    private CancellationTokenSource _threadCancel = new();
    
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
        }
        catch (Exception e)
        {
            if (Api != null)
            {
                Api.disconnect();
                Api.destroy();
                Api = null;
            }
            
            StopSampleThread();
            
            State = EyeTrackerState.Disconnected;
            throw;
        }

        State = EyeTrackerState.Connected;

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

        State = EyeTrackerState.Calibrating;

        Api.requestTracking(0);

        try
        {
            Api.calibrate(0);
        }
        finally
        {
            Api.unrequestTracking();
            State = EyeTrackerState.Connected;
        }

        LastCalibration = DateTime.UtcNow;

        await Task.Delay(1);

        return true;
    }

    public override async Task<bool> Disconnect()
    {
        if (!IsConnected())
        {
            throw new EyeTrackerNotConnected("device not connected");
        }
        
        StopSampleThread();
        Api.disconnect();
        Api.destroy();
        Api = null;
        _screenConfig = null;

        State = EyeTrackerState.Disconnected;

        await Task.Delay(1);

        return true;
    }

    [MemberNotNullWhen(true, nameof(Api))]
    private bool IsConnected()
    {
        return Api != null;
    }

    private void OnEyeLogicApiEvent(DeviceEventType eventType)
    {
    }

    private void StartSampleThread()
    {
        _threadCancel = new CancellationTokenSource();
        _sampleThread = new Thread(SampleThread)
        {
            IsBackground = true
        };
        _sampleThread.Start();
    }

    private void StopSampleThread()
    { 
        _threadCancel.Cancel();
        if (_sampleThread != null)
        {
            _sampleThread.Join();
            _sampleThread = null;
            _threadCancel.Dispose();
        }
    }

    private async void SampleThread()
    {
        while (!_threadCancel.IsCancellationRequested)
        {
            if ((State != EyeTrackerState.Started && State != EyeTrackerState.Calibrating) || _screenConfig == null ||
                !_gazeSamples.TryDequeue(out var gazeSample))
            {
                continue;
            }
            
            var resX = _screenConfig.resolutionX;
            var resY = _screenConfig.resolutionY;
            
            var gazeOutput = new WsOutgoingGazeMessage
            {
                DeviceId = gazeSample.index,
                LeftX = gazeSample.porLeft.x / resX,
                LeftY = gazeSample.porLeft.y / resY,
                RightX = gazeSample.porRight.x / resX,
                RightY = gazeSample.porRight.y / resY,
                LeftValidity = gazeSample.porLeft.x > double.MinValue && gazeSample.porLeft.y > double.MinValue,
                RightValidity = gazeSample.porRight.x > double.MinValue && gazeSample.porRight.y > double.MinValue,
                LeftPupil = gazeSample.pupilRadiusLeft,
                RightPupil = gazeSample.pupilRadiusRight,
                Timestamp = DateTimeExtensions.IsoNow,
                DeviceTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(gazeSample.timestampMicroSec / 1000)
                    .UtcDateTime.ToIso()
            };
            
            await WsResponse(gazeOutput, false);
        }
    }

    private void OnEyeLogicGazeSample(GazeSample gazeSample)
    {
        _gazeSamples.Enqueue(gazeSample);
    }

    private void OnFixationStartSample(FixationStartSample fixationStartSample)
    {
        _fixationStartSamples.Enqueue(fixationStartSample);

        if (!_processingFixationStart)
        {
            if (_screenConfig == null)
            {
                WsResponse(new WsOutgoingErrorMessage("unable to get screen config, disconnect and connect again!"), true);

                return;
            }

            _ = ProcessFixationStartSample(_screenConfig.resolutionX, _screenConfig.resolutionY);
        }
    }

    private async Task ProcessFixationStartSample(int resX, int resY)
    {
        _processingFixationStart = true;

        while (_fixationStartSamples.TryDequeue(out var fixationStartSample))
        {
            var fixationId = _fixationCount++;
            _fixationIndexCache[fixationStartSample.index] = fixationId;

            var gazeOutput = new WsOutgoingFixationStartMessage()
            {
                FixationId = fixationId,
                GazeDeviceId = fixationStartSample.index,
                X = fixationStartSample.por.x / resX,
                Y = fixationStartSample.por.y / resY,
                Duration = 0,
                Timestamp = DateTimeExtensions.IsoNow,
                DeviceTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(fixationStartSample.timestampMicroSec / 1000)
                    .UtcDateTime.ToIso()
            };

            await WsResponse(gazeOutput, false);
        }

        _processingFixationStart = false;
    }

    private void OnFixationEndSample(FixationEndSample fixationEndSample)
    {
        _fixationEndSamples.Enqueue(fixationEndSample);

        if (!_processingFixationEnd)
        {
            if (_screenConfig == null)
            {
                WsResponse(new WsOutgoingErrorMessage("unable to get screen config, disconnect and connect again!"), true);

                return;
            }

            _ = ProcessFixationEndSample(_screenConfig.resolutionX, _screenConfig.resolutionY);
        }
    }

    private async Task ProcessFixationEndSample(int resX, int resY)
    {
        _processingFixationEnd = true;

        while (_fixationEndSamples.TryDequeue(out var fixationEndSample))
        {
            var gazeOutput = new WsOutgoingFixationStartMessage()
            {
                FixationId = _fixationIndexCache.GetValueOrDefault(fixationEndSample.indexStart, -1),
                GazeDeviceId = fixationEndSample.index,
                X = fixationEndSample.por.x / resX,
                Y = fixationEndSample.por.y / resY,
                Duration = (fixationEndSample.timestampMicroSec - fixationEndSample.timestampStartMicroSec) / 1000f,
                Timestamp = DateTimeExtensions.IsoNow,
                DeviceTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(fixationEndSample.timestampMicroSec / 1000)
                    .UtcDateTime.ToIso()
            };

            await WsResponse(gazeOutput, false);
        }

        _processingFixationEnd = false;
    }
}