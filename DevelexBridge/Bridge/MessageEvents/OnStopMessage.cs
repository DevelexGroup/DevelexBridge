using System.Net.WebSockets;
using Bridge.Enums;
using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private void OnStopMessage(WebSocket webSocket, WsStopMessage message)
    {
        if (EyeTracker == null || EyeTracker.State == EyeTrackerState.Disconnected)
        {
            WsErrorDeviceNotConnected();
            return;
        }

        if (EyeTracker.State == EyeTrackerState.Connecting)
        {
            WsErrorDeviceConnecting();
            return;
        }

        if (EyeTracker.State != EyeTrackerState.Started)
        {
            // not started
            return;
        }
        
        EyeTracker.Stop();
    }
}