using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnSubscribeMessage(WsClientMetadata clientMetadata, WsIncomingSubscribeMessage message)
    {
        if (EyeTracker == null)
        {
            await WsErrorDeviceNotConnected();
            return;
        }
        
        await SendToAll(new WsOutgoingResponseMessage("subscribe", EyeTracker, message.Identifiers));
    }
}