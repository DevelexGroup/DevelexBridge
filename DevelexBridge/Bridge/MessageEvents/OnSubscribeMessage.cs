using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnSubscribeMessage(WsClientMetadata clientMetadata, WsIncomingSubscribeMessage message)
    {
        await SendToAll(new WsOutgoingResponseMessage("subscribe", EyeTracker, message.Identifiers));
    }
}