using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private Task WsErrorDeviceNotConnected()
    {
        return SendToAll(new WsErrorResponseMessage("device not connected"));
    }

    private Task WsErrorDeviceConnecting()
    {
        return SendToAll(new WsErrorResponseMessage("device is connecting"));
    }

    private async Task<bool> TryStop(EyeTracker eyeTracker)
    {
        try
        {
            await eyeTracker.Stop();
            return true;
        }
        catch (Exception e)
        {
            await SendToAll(new WsErrorResponseMessage(e.Message));
            return false;
        }
    }
}