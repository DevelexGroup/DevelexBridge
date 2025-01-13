using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Bridge.Enums;
using Bridge.Exceptions.EyeTracker;
using Bridge.Extensions;
using Bridge.Models;
using eyelogic;

namespace Bridge.EyeTrackers.EyeLogic;

public class EyeLogic(Func<object, Task> wsResponse) : EyeTracker
{
    public override EyeTrackerState State { get; set; } = EyeTrackerState.Disconnected;
    public override Func<object, Task> WsResponse { get; init; } = wsResponse;
    public override DateTime? LastCalibration  { get; set; } = null;
    private ELCsApi? Api { get; set; } = null;
    private ConcurrentQueue<GazeSample> _gazeSamples = new();
    private bool _processingQueue = false;
    private ELCsApi.ScreenConfig? _screenConfig = null;

    public override async Task<bool> Connect()
    {
        State = EyeTrackerState.Connecting;

        try
        {
            Api = new ELCsApi("Develex Bridge client");

            Api.OnEvent += OnEyeLogicApiEvent;
            Api.OnGazeSample += OnEyeLogicGazeSample;
            
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
        }
        catch (Exception e)
        {
            if (Api != null)
            {
                Api.disconnect();
                Api.destroy();
                Api = null;
            }
            
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

    private void OnEyeLogicApiEvent(EventType eventType)
    {
        
    }

    private void OnEyeLogicGazeSample(GazeSample gazeSample)
    {
        _gazeSamples.Enqueue(gazeSample);

        if (!_processingQueue)
        {
            if (_screenConfig == null)
            {
                WsResponse(new WsOutgoingErrorMessage("unable to get screen config, disconnect and connect again!"));
                
                return;
            }
            
            _ = ProcessGazeSampleQueue(_screenConfig.resolutionX, _screenConfig.resolutionY);
        }
    }

    private async Task ProcessGazeSampleQueue(int resX, int resY)
    {
        _processingQueue = true;

        while (_gazeSamples.TryDequeue(out var gazeSample))
        {
            var outputData = new WsOutgoingGazeMessage
            {
                LeftX = gazeSample.porLeft.x / resX,
                LeftY = gazeSample.porLeft.y / resY,
                RightX = gazeSample.porRight.x / resX,
                RightY = gazeSample.porRight.y / resY,
                LeftValidity = gazeSample.porLeft.x > double.MinValue && gazeSample.porLeft.y > double.MinValue,
                RightValidity = gazeSample.porRight.x > double.MinValue && gazeSample.porRight.y > double.MinValue,
                LeftPupil = gazeSample.pupilRadiusLeft,
                RightPupil = gazeSample.pupilRadiusRight,
                Timestamp = DateTimeExtensions.IsoNow,
            };

            await WsResponse(outputData);
        }

        _processingQueue = false;
    }
}