using System.Net.WebSockets;
using Bridge.Enums;
using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnCalibrateMessage(WsClientMetadata clientMetadata, WsIncomingCalibrateMessage message)
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

        if (EyeTracker.State == EyeTrackerState.Started)
        {
            if (!await TryStop(EyeTracker))
            {
                return;
            }
        }
        
        try
        {
            var result = await EyeTracker.Calibrate();

            if (result)
            {
                await SendToAll(new WsOutgoingResponseMessage("connect", EyeTracker, message.Identifiers));
            }
        }
        catch (Exception ex)
        {
            await SendToAll(new WsOutgoingErrorMessage(ex.Message));
        }
    }
}