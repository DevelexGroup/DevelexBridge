using System.Net.WebSockets;
using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnBridgeStateMessage(WsClientMetadata clientMetadata, WsIncomingBridgeStateMessage message)
    {
        if (EyeTracker == null)
        {
            await WsErrorDeviceNotConnected();
            return;
        }

        await SendToAll(new WsOutgoingResponseMessage("status", EyeTracker, message.Identifiers));
    }
}