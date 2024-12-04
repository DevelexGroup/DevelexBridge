using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnBridgeMessage(WsClientMetadata clientMetadata, WsIncomingBridgeMessage message)
    {
        await SendToAll(new WsOutgoingTunnelMessage(message.Content, message.Identifiers));
    }
}