using Bridge.Enums;
using Bridge.EyeTrackers.GazePoint;
using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnConnectMessage(WsClientMetadata clientMetadata, WsIncomingConnectMessage message)
    {
        var responseTo = "connect";
        
        if (EyeTracker != null)
        {
            if (EyeTracker.State == EyeTrackerState.Connecting)
            {
                await WsErrorDeviceConnecting(responseTo, message.Identifiers);
                return;
            }
            
            if (EyeTracker.State == EyeTrackerState.Connected)
            {
                await SendToAll(new WsOutgoingResponseMessage(responseTo, EyeTracker, message.Identifiers,
                    ResponseStatus.Rejected, "device already connected"));
                return;
            }
        }
        
        await SendToAll(new WsOutgoingResponseMessage(responseTo, EyeTracker, message.Identifiers, ResponseStatus.Processing));
        
        switch (message.Config.TrackerType)
        {
            case "gazepoint":
                var gp = new GazePoint(SendToAll);
                EyeTracker = gp;
                break;
        }

        if (EyeTracker != null)
        {
            try
            {
                var result = await EyeTracker.Connect();

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
}