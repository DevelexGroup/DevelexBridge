using System.Net.WebSockets;
using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnBridgeStateMessage(WsClientMetadata clientMetadata, WsBridgeStatusMessage message)
    {
        if (EyeTracker == null)
        {
            await WsErrorDeviceNotConnected();
            return;
        }

        await SendToAll(new WsResponseMessage("status", EyeTracker, message.Identifiers));
    }
}