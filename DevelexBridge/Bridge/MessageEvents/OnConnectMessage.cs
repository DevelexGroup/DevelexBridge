using System.Net.WebSockets;
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
                // connecting
                return;
            }
            
            if (EyeTracker.State == EyeTrackerState.Connected)
            {
                // connected
                return;
            }
        }

        switch (message.Tracker)
        {
            case "opengaze":
                EyeTracker = new OpenGaze();
                EyeTracker.Connect();
                break;
        }
    }
}