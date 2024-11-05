using System.Net.WebSockets;
using System.Text.Json;
using Bridge.Enums;
using Bridge.EyeTrackers.OpenGaze;
using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private void OnConnectMessage(WebSocket webSocket, WsConnectMessage message)
    {
        if (EyeTracker != null)
        {
            if (EyeTracker.State == EyeTrackerState.Connecting)
            {
                WsErrorDeviceConnecting();
                return;
            }
            
            if (EyeTracker.State == EyeTrackerState.Connected)
            {
                SendToAll(new WsErrorResponseMessage("device already connected"));
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
                EyeTracker.Connect();
                SendToAll(new WsBaseResponseMessage("connected"));
            }
            catch (Exception ex)
            {
                SendToAll(new WsErrorResponseMessage(ex.Message));
            }
        }
    }
}