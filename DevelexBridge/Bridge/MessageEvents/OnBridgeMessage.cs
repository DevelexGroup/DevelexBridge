using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnTunnelMessage(WsClientMetadata clientMetadata, WsBridgeMessage message)
    {
        await SendToAll(new WsBridgeResponseMessage(message.Content, message.Identifiers));
    }
}