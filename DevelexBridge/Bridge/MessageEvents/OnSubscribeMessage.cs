using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnSubscribeMessage(WsClientMetadata clientMetadata, WsSubscribeMessage message)
    {
        if (EyeTracker == null)
        {
            await WsErrorDeviceNotConnected();
            return;
        }
        
        await SendToAll(new WsResponseMessage("subscribe", EyeTracker, message.Identifiers));
    }
}