using System.ComponentModel.DataAnnotations;

namespace Bridge.Enums;

public enum EyeTrackerState
{
    [Display(Name = "trackerDisconnected")]
    Disconnected,
    
    [Display(Name = "trackerConnecting")]
    Connecting,
    
    [Display(Name = "trackerConnected")]
    Connected,
    
    [Display(Name = "trackerEmitting")]
    Started,
    
    [Display(Name = "trackerCalibrating")]
    Calibrating,
}