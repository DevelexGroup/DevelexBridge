using System.Net.WebSockets;
using Bridge.Enums;
using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnStopMessage(WsClientMetadata clientMetadata, WsStopMessage message)
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

        if (EyeTracker.State != EyeTrackerState.Started)
        {
            // not started
            return;
        }
        
        try
        {
            var result = await EyeTracker.Stop();

            if (result)
            {
                await SendToAll(new WsResponseMessage("stop", EyeTracker, message.Identifiers));
            }
        }
        catch (Exception ex)
        {
            await SendToAll(new WsErrorResponseMessage(ex.Message));
        }
    }
}