using System.Net.WebSockets;
using Bridge.Enums;
using Bridge.Models;
using Bridge.WebSockets;
using SuperSocket.WebSocket.Server;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnBridgeStateMessage(WebSocketSession session, WsIncomingBridgeStateMessage message)
    {
        if (EyeTracker == null)
        {
            await WsErrorDeviceNotConnected("status", message.Identifiers, ResponseStatus.Resolved);
            return;
        }

        await WsBroadcaster.SendToAll(new WsOutgoingResponseMessage("status", EyeTracker, message.Identifiers, ResponseStatus.Resolved));
    }
}