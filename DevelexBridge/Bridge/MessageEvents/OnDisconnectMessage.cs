using System.Net.WebSockets;
using Bridge.Enums;
using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private void OnDisconnectMessage(WebSocket webSocket, WsDisconnectMessage message)
    {
        if (EyeTracker == null || EyeTracker.State == EyeTrackerState.Disconnected)
        {
            WsErrorDeviceNotConnected();
            return;
        }
        
        EyeTracker.Disconnect();
        EyeTracker = null;
    }
}