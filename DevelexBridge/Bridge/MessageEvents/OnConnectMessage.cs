using Bridge.Enums;
using Bridge.EyeTrackers.GazePoint;
using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnConnectMessage(WsClientMetadata clientMetadata, WsIncomingConnectMessage message)
    {
        if (EyeTracker != null)
        {
            if (EyeTracker.State == EyeTrackerState.Connecting)
            {
                await WsErrorDeviceConnecting();
                return;
            }
            
            if (EyeTracker.State == EyeTrackerState.Connected)
            {
                await SendToAll(new WsOutgoingErrorMessage("device already connected"));
                return;
            }
        }
        
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
                    await SendToAll(new WsOutgoingResponseMessage("connect", EyeTracker, message.Identifiers));
                }
            }
            catch (Exception ex)
            {
                await SendToAll(new WsOutgoingErrorMessage(ex.Message));
            }
        }
    }
}