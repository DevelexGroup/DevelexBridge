using System.Net.WebSockets;
using Bridge.Enums;
using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnDisconnectMessage(WsClientMetadata clientMetadata, WsDisconnectMessage message)
    {
        if (EyeTracker == null || EyeTracker.State == EyeTrackerState.Disconnected)
        {
            await WsErrorDeviceNotConnected();
            return;
        }
        
        await EyeTracker.Disconnect();
        EyeTracker = null;
    }
}