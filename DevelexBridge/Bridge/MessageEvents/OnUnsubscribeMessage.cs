using Bridge.Models;
using Bridge.Output;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnUnsubscribeMessage(WsClientMetadata clientMetadata, WsIncomingUnsubscribeMessage message)
    {
        if (Server == null)
        {
            ConsoleOutput.WsServerIsNotRunning();
            return;
        }

        Server.DisconnectClient(clientMetadata.Id);
        await SendToAll(new WsOutgoingResponseMessage("unsubscribe", EyeTracker, message.Identifiers));
    }
}