using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnSubscribeMessage(WsClientMetadata clientMetadata, WsSubscribeMessage message)
    {
        await SendToAll(new WsResponseMessage("subscribe", EyeTracker, message.Identifiers));
    }
}