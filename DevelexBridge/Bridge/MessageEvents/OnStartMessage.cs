using System.Net.WebSockets;
using Bridge.Enums;
using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private async Task OnStartMessage(WebSocket webSocket, WsStartMessage message)
    {
        if (EyeTracker == null || EyeTracker.State == EyeTrackerState.Disconnected)
        {
            await WsErrorDeviceNotConnected();
            return;
        }

        if (EyeTracker.State == EyeTrackerState.Connecting)
        {
            await WsErrorDeviceConnecting();
            return;
        }
        
        await EyeTracker.Start();
    }
}