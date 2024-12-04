using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private Task WsErrorDeviceNotConnected()
    {
        return SendToAll(new WsOutgoingErrorMessage("device is not connected"));
    }

    private Task WsErrorDeviceConnecting()
    {
        return SendToAll(new WsOutgoingErrorMessage("device is connecting"));
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
            await SendToAll(new WsOutgoingErrorMessage(e.Message));
            return false;
        }
    }
}