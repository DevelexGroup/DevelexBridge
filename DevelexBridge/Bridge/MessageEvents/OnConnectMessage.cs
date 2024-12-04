using System.Net.WebSockets;
using System.Text.Json;
using Bridge.Enums;
using Bridge.EyeTrackers.OpenGaze;
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
            case "opengaze":
                var og = new OpenGaze(SendToAll);
                EyeTracker = og;
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