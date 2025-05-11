using Bridge.Enums;
using Bridge.Models;
using Bridge.Output;
using Bridge.WebSockets;
using SuperSocket.WebSocket.Server;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnUnsubscribeMessage(WebSocketSession session, WsIncomingUnsubscribeMessage message)
    {
        if (Server == null)
        {
            ConsoleOutput.WsServerIsNotRunning();
            return;
        }

        await WsBroadcaster.SendToAll(new WsOutgoingResponseMessage("unsubscribe", EyeTracker, message.Identifiers, ResponseStatus.Resolved));
    }
}