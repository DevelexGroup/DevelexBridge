using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Bridge.Enums;
using Bridge.Exceptions.EyeTracker;
using Bridge.Extensions;
using Bridge.Models;
using eyelogic;

namespace Bridge.EyeTrackers.aSee;

public class ASee(Func<object, Task> wsResponse) : EyeTracker
{
    public override EyeTrackerState State { get; set; } = EyeTrackerState.Disconnected;
    public override Func<object, Task> WsResponse { get; init; } = wsResponse;
    public override DateTime? LastCalibration { get; set; }

    private ASeeApi.gazeCallback _gazeCallback = GazeCallback;
    private static ConcurrentQueue<_7i_eye_data_ex_t> _gazeSamples = new();
    private static bool _processingQueue = false;
    private GCHandle _contextHandle;
    
    public override async Task<bool> Connect()
    {
        State = EyeTrackerState.Connecting;
        
        _contextHandle = GCHandle.Alloc(this);
        ASeeApi._7i_set_gaze_callback(Marshal.GetFunctionPointerForDelegate(_gazeCallback), GCHandle.ToIntPtr(_contextHandle));

        try
        {
            var ret = ASeeApi._7i_start("./");

            if (ret != 0)
            { 
                throw new EyeTrackerUnableToConnect($"asee unable to connect - code {ret}");
            }
        }
        catch (Exception e)
        {
            State = EyeTrackerState.Disconnected;
            throw;
        }

        State = EyeTrackerState.Connected;

        await Task.Delay(1);

        return true;
    }

    public override async Task<bool> Start()
    {
        var trackingCoefficient = new _7i_coefficient_t();
        var ret = ASeeApi._7i_start_tracking(ref trackingCoefficient);

        if (ret != 0)
        {
            throw new EyeTrackerUnableToConnect($"asee unable to start tracking - code {ret}");
        }
        
        State = EyeTrackerState.Started;

        await Task.Delay(1);

        return true;
    }

    public override async Task<bool> Stop()
    {
        var ret = ASeeApi._7i_stop_tracking();

        if (ret != 0)
        {
            throw new EyeTrackerUnableToConnect($"asee unable to stop tracking - code {ret}");
        }

        State = EyeTrackerState.Connected;

        await Task.Delay(1);

        return true;
    }

    public override async Task<bool> Calibrate()
    {
        // var ret = ASeeApi._7i_start_calibration(5);

        //TODO: pro kalibraci je nutné mít vlastní okno s body!
        
        await Task.Delay(1);
        return true;
    }

    public override async Task<bool> Disconnect()
    {
        if (_contextHandle.IsAllocated)
        {
            _contextHandle.Free();
        }
        
        var ret = ASeeApi._7i_stop();

        if (ret != 0)
        {
            throw new EyeTrackerUnableToProceed($"asee unable to proceed - code {ret}");
        }

        State = EyeTrackerState.Disconnected;

        await Task.Delay(1);

        return true;
    }

    private static void GazeCallback(ref _7i_eye_data_ex_t eyes, IntPtr context)
    {
        var handle = GCHandle.FromIntPtr(context);
        var instance = handle.Target as ASee;

        if (instance == null || instance.State != EyeTrackerState.Started)
        {
            return;
        }
        
        _gazeSamples.Enqueue(eyes);

        if (!_processingQueue)
        {
            _ = ProcessGazeSampleQueue(instance);
        }
    }
    
    private static async Task ProcessGazeSampleQueue(ASee instance)
    {
        _processingQueue = true;

        while (_gazeSamples.TryDequeue(out var gazeSample))
        {
            var outputData = new WsOutgoingGazeMessage
            {
                LeftX = gazeSample.left_gaze.gaze_point.x,
                LeftY = gazeSample.left_gaze.gaze_point.y,
                RightX = gazeSample.right_gaze.gaze_point.x,
                RightY = gazeSample.right_gaze.gaze_point.y,
                LeftValidity = (int)GetValidValue((byte)_7I_EYE_GAZE_VALIDITY.ID_EYE_GAZE_POINT, gazeSample.left_gaze.ex_data_bit_mask) == 1,
                RightValidity = (int)GetValidValue((byte)_7I_EYE_GAZE_VALIDITY.ID_EYE_GAZE_POINT, gazeSample.right_gaze.ex_data_bit_mask) == 1,
                LeftPupil = gazeSample.left_pupil.pupil_diameter,
                RightPupil = gazeSample.right_pupil.pupil_diameter,
                Timestamp = DateTimeExtensions.IsoNow,
            };
            
            await instance.WsResponse(outputData);
        }

        _processingQueue = false;
    }

    private static uint GetValidValue(byte position, uint bits)
    {
        var mask = (uint)1 << position;
        
        return (mask &= bits) >> position;
    }
}