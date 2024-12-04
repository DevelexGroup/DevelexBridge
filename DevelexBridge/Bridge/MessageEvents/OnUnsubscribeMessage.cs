using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnUnsubscribeMessage(WsClientMetadata clientMetadata, WsIncomingUnsubscribeMessage message)
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
        await SendToAll(new WsOutgoingResponseMessage("unsubscribe", EyeTracker, message.Identifiers));
    }
}