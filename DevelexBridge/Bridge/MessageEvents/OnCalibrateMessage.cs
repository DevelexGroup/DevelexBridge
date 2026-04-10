using System.Net.WebSockets;
using Bridge.Enums;
using Bridge.Models;
using Bridge.WebSockets;
using SuperSocket.WebSocket.Server;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnCalibrateMessage(WebSocketSession session, WsIncomingCalibrateMessage message)
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
        
        await WsBroadcaster.SendToAll(new WsOutgoingResponseMessage(responseTo, EyeTracker, message.Identifiers, ResponseStatus.Processing));

        // Remember if we were tracking so we can preserve that state.
        // The EyeLogic Calibrate() method now handles tracking state internally,
        // so we no longer need to stop/restart manually — which previously could
        // cause calibration to be lost when tracking was unrequested and re-requested.
        var wasStarted = EyeTracker.State == EyeTrackerState.Started;
        
        if (wasStarted)
        {
            // For non-EyeLogic trackers, we still need to stop first.
            // EyeLogic handles this internally now, but other trackers may not.
            if (EyeTracker is not EyeTrackers.EyeLogic.EyeLogic)
            {
                if (!await TryStop(EyeTracker))
                {
                    return;
                }
            }
        }
        
        try
        {
            var result = await EyeTracker.Calibrate();

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