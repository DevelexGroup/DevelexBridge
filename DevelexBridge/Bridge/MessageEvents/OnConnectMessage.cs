using System.Net.WebSockets;
using System.Text.Json;
using Bridge.Enums;
using Bridge.EyeTrackers.OpenGaze;
using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnConnectMessage(WsClientMetadata clientMetadata, WsConnectMessage message)
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
                await SendToAll(new WsErrorResponseMessage("device already connected"));
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
                    await SendToAll(new WsResponseMessage("connect", EyeTracker, message.Identifiers));
                }
            }
            catch (Exception ex)
            {
                await SendToAll(new WsErrorResponseMessage(ex.Message));
            }
        }
    }
}