using Bridge.Enums;
using Bridge.EyeTrackers.aSee;
using Bridge.EyeTrackers.EyeLogic;
using Bridge.EyeTrackers.GazePoint;
using Bridge.Models;
using Bridge.WebSockets;
using SuperSocket.WebSocket.Server;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnConnectMessage(WebSocketSession session, WsIncomingConnectMessage message)
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
                await WsBroadcaster.SendToAll(new WsOutgoingResponseMessage(responseTo, EyeTracker, message.Identifiers,
                    ResponseStatus.Rejected, "device already connected"));
                return;
            }
        }
        
        await WsBroadcaster.SendToAll(new WsOutgoingResponseMessage(responseTo, EyeTracker, message.Identifiers, ResponseStatus.Processing));
        
        switch (message.Config.TrackerType)
        {
            case "gazepoint":
                EyeTracker = new GazePoint(WsBroadcaster.SendToAll);
                break;
            case "eyelogic":
                EyeTracker = new EyeLogic(WsBroadcaster.SendToAll);
                break;
            case "asee":
                EyeTracker = new ASee(WsBroadcaster.SendToAll);
                break;
            default:
                await WsBroadcaster.SendToAll(new WsOutgoingResponseMessage(responseTo, EyeTracker, message.Identifiers, ResponseStatus.Rejected, "unknown tracker type"));
                return;
        }

        if (EyeTracker != null)
        {
            try
            {
                var result = await EyeTracker.Connect();

                if (result)
                {
                    await WsBroadcaster.SendToAll(new WsOutgoingResponseMessage(responseTo, EyeTracker, message.Identifiers, ResponseStatus.Resolved));
                }
            }
            catch (Exception ex)
            {
                await WsBroadcaster.SendToAll(new WsOutgoingResponseMessage(responseTo, EyeTracker, message.Identifiers, ResponseStatus.Rejected, ex.Message));
            }
        }
    }
}