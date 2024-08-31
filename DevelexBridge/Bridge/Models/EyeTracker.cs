using System.Windows.Forms.VisualStyles;
using Bridge.Enums;

namespace Bridge.Models;

public abstract class EyeTracker
{
    public abstract EyeTrackerState State { get; set; }
    
    public abstract void Connect();
    public abstract void Start();
    public abstract void Stop();
    public abstract void Calibrate();
    public abstract void Disconnect();
}