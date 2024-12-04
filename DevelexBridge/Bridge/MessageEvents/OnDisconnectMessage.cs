using System.Net.WebSockets;
using Bridge.Enums;
using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnDisconnectMessage(WsClientMetadata clientMetadata, WsIncomingDisconnectMessage message)
    {
        if (EyeTracker == null || EyeTracker.State == EyeTrackerState.Disconnected)
        {
            await WsErrorDeviceNotConnected();
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
            var result = await EyeTracker.Disconnect();

            if (result)
            {
                await SendToAll(new WsOutgoingResponseMessage("disconnect", EyeTracker, message.Identifiers));
            }
        }
        catch (Exception ex)
        {
            await SendToAll(new WsOutgoingErrorMessage(ex.Message));
        }
        finally
        {
            EyeTracker = null;
        }
    }
}