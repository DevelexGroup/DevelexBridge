using System.Net.WebSockets;
using Bridge.Enums;
using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private void OnStartMessage(WebSocket webSocket, WsStartMessage message)
    {
        if (EyeTracker == null || EyeTracker.State == EyeTrackerState.Disconnected)
        {
            // not connected
            return;
        }

        if (EyeTracker.State == EyeTrackerState.Connecting)
        {
            // connecting
            return;
        }
        
        EyeTracker.Start();
    }
}