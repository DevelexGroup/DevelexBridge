using System.Net.WebSockets;
using Bridge.Enums;
using Bridge.Models;
using Bridge.WebSockets;
using SuperSocket.WebSocket.Server;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnDisconnectMessage(WebSocketSession session, WsIncomingDisconnectMessage message)
    {
        var responseTo = "disconnect";
        
        if (EyeTracker == null || EyeTracker.State == EyeTrackerState.Disconnected)
        {
            await WsErrorDeviceNotConnected(responseTo, message.Identifiers);
            return;
        }
        
        await WsBroadcaster.SendToAll(new WsOutgoingResponseMessage(responseTo, EyeTracker, message.Identifiers, ResponseStatus.Processing));

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
                await WsBroadcaster.SendToAll(new WsOutgoingResponseMessage(responseTo, EyeTracker, message.Identifiers, ResponseStatus.Resolved));
            }
        }
        catch (Exception ex)
        {
            await WsBroadcaster.SendToAll(new WsOutgoingResponseMessage(responseTo, EyeTracker, message.Identifiers, ResponseStatus.Rejected, ex.Message));
        }
        finally
        {
            EyeTracker = null;
        }
    }
}