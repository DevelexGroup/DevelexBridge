using Bridge.Enums;
using Bridge.EyeTrackers.aSee;
using Bridge.EyeTrackers.EyeLogic;
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
                EyeTracker = new GazePoint(SendToAll);
                break;
            case "eyelogic":
                EyeTracker = new EyeLogic(SendToAll);
                break;
            case "asee":
                EyeTracker = new ASee(SendToAll);
                break;
            default:
                await SendToAll(new WsOutgoingResponseMessage(responseTo, EyeTracker, message.Identifiers, ResponseStatus.Rejected, "unknown tracker type"));
                return;
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