using System.Net.WebSockets;
using Bridge.Enums;
using Bridge.Models;
using Bridge.WebSockets;
using SuperSocket.WebSocket.Server;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnStopMessage(WebSocketSession session, WsIncomingStopMessage message)
    {
        var responseTo = "stop";
        
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

        if (EyeTracker.State != EyeTrackerState.Started)
        {
            await WsBroadcaster.SendToAll(new WsOutgoingResponseMessage(responseTo, EyeTracker, message.Identifiers, ResponseStatus.Rejected, "device not started"));
            return;
        }
        
        try
        {
            var result = await EyeTracker.Stop();

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