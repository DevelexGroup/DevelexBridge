using System.Windows.Forms.VisualStyles;
using Bridge.Enums;

namespace Bridge.Models;

public abstract class EyeTracker
{
    public abstract EyeTrackerState State { get; set; }

    public abstract Func<WsBaseResponseMessage, Task> WsResponse { get; init; }
    
    public abstract Task Connect();
    public abstract Task Start();
    public abstract Task Stop();
    public abstract Task Calibrate();
    public abstract Task Disconnect();
}