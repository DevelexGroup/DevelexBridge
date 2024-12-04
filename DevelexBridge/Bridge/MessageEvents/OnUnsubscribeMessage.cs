using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnUnsubscribeMessage(WsClientMetadata clientMetadata, WsUnsubscribeMessage message)
    {
        if (Server == null)
        {
            return;
        }
        
        if (EyeTracker == null)
        {
            await WsErrorDeviceNotConnected();
            return;
        }

        Server.DisconnectClient(clientMetadata.Id);
        await SendToAll(new WsResponseMessage("unsubscribe", EyeTracker, message.Identifiers));
    }
}