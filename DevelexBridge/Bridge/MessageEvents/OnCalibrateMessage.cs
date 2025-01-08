using System.Net.WebSockets;
using Bridge.Enums;
using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnCalibrateMessage(WsClientMetadata clientMetadata, WsIncomingCalibrateMessage message)
    {
        var responseTo = "calibrate";
        
        if (EyeTracker == null || EyeTracker.State == EyeTrackerState.Disconnected)
        {
            await WsErrorDeviceNotConnected(responseTo, message.Identifiers);
            return;
        }

        if (EyeTracker.State == EyeTrackerState.Connecting)
        {
            await WsErrorDeviceConnecting(responseTo, message.Identifiers);
            return;
        }
        
        await SendToAll(new WsOutgoingResponseMessage(responseTo, EyeTracker, message.Identifiers, ResponseStatus.Processing));

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
                await SendToAll(new WsOutgoingResponseMessage(responseTo, EyeTracker, message.Identifiers, ResponseStatus.Resolved));
            }
        }
        catch (Exception ex)
        {
            await SendToAll(new WsOutgoingResponseMessage(responseTo, EyeTracker, message.Identifiers, ResponseStatus.Rejected, ex.Message));
        }
    }
}