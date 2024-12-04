using System.Net.WebSockets;
using Bridge.Enums;
using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnStartMessage(WsClientMetadata clientMetadata, WsIncomingStartMessage message)
    {
        if (EyeTracker == null || EyeTracker.State == EyeTrackerState.Disconnected)
        {
            await WsErrorDeviceNotConnected();
            return;
        }

        if (EyeTracker.State == EyeTrackerState.Connecting)
        {
            await WsErrorDeviceConnecting();
            return;
        }
        
        try
        {
            var result = await EyeTracker.Start();

            if (result)
            {
                await SendToAll(new WsOutgoingResponseMessage("start", EyeTracker, message.Identifiers));
            }
        }
        catch (Exception ex)
        {
            await SendToAll(new WsOutgoingErrorMessage(ex.Message));
        }
    }
}