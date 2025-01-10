using System.Diagnostics.CodeAnalysis;
using Bridge.Enums;
using Bridge.Exceptions.EyeTracker;
using Bridge.Models;
using eyelogic;

namespace Bridge.EyeTrackers.EyeLogic;

public class EyeLogic(Func<object, Task> wsResponse) : EyeTracker
{
    public override EyeTrackerState State { get; set; } = EyeTrackerState.Disconnected;
    public override Func<object, Task> WsResponse { get; init; } = wsResponse;
    public override DateTime? LastCalibration  { get; set; } = null;

    private ELCsApi? Api { get; set; } = null;

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

            var screenConfig = Api.getActiveScreen();

            if (screenConfig == null)
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
        
        Api.calibrate(0);

        State = EyeTrackerState.Connected;

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
        // Console.WriteLine($"EL gaze sample ${gazeSample.porLeft} - ${gazeSample.porRight}");
        
        var outputData = new WsOutgoingGazeMessage
        {
            LeftX = gazeSample.porLeft.x,
            LeftY = gazeSample.porLeft.y,
            RightX = gazeSample.porRight.x,
            RightY = gazeSample.porRight.y,
            LeftValidity = true,
            RightValidity = true,
            LeftPupil = gazeSample.pupilRadiusLeft,
            RightPupil = gazeSample.pupilRadiusRight,
            Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds()
        };
        
        WsResponse(outputData);
    }
}