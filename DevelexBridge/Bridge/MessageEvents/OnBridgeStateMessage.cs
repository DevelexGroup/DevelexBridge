using System.Net.WebSockets;
using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private void OnBridgeStateMessage(WebSocket websocket, WsBridgeStatusMessage message)
    {
        if (EyeTracker == null)
        {
            WsErrorDeviceNotConnected();
            return;
        }

        SendToAll(new WsStatusResponseMessage(EyeTracker.State));
    }
}