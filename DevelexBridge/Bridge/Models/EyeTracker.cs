using System.Windows.Forms.VisualStyles;
using Bridge.Enums;

namespace Bridge.Models;

public abstract class EyeTracker
{
    public abstract EyeTrackerState State { get; set; }

    public abstract Func<object, bool, Task> WsResponse { get; init; }
    public abstract DateTime? LastCalibration { get; set; }
    
    public abstract Task<bool> Connect();
    public abstract Task<bool> Start();
    public abstract Task<bool> Stop();
    public abstract Task<bool> Calibrate();
    public abstract Task<bool> Disconnect();
}