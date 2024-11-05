using System.Net.WebSockets;
using System.Text.Json;
using Bridge.Enums;
using Bridge.EyeTrackers.OpenGaze;
using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnConnectMessage(WebSocket webSocket, WsConnectMessage message)
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

        switch (message.Tracker)
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
                await EyeTracker.Connect();
            }
            catch (Exception ex)
            {
                await SendToAll(new WsErrorResponseMessage(ex.Message));
            }
        }
    }
}