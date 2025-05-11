using Bridge.Models;
using Bridge.WebSockets;
using SuperSocket.WebSocket.Server;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnBridgeMessage(WebSocketSession session, WsIncomingBridgeMessage message)
    {
        await WsBroadcaster.SendToAll(new WsOutgoingTunnelMessage(message.Content, message.Identifiers));
    }
}