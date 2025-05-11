using Bridge.Enums;
using Bridge.Models;
using Bridge.WebSockets;
using SuperSocket.WebSocket.Server;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnSubscribeMessage(WebSocketSession session, WsIncomingSubscribeMessage message)
    {
        await WsBroadcaster.SendToAll(new WsOutgoingResponseMessage("subscribe", EyeTracker, message.Identifiers, ResponseStatus.Resolved));
    }
}