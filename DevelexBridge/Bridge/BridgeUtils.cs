using Bridge.Enums;
using Bridge.Models;

namespace Bridge;

public partial class BridgeWindow
{
    private Task WsErrorDeviceNotConnected(string responseTo, WsMessageIdentifiers identifiers)
    {
        return SendToAll(new WsOutgoingResponseMessage(responseTo, EyeTracker, identifiers, ResponseStatus.Rejected, "device is not connected"));
    }

    private Task WsErrorDeviceConnecting(string responseTo, WsMessageIdentifiers identifiers)
    {
        return SendToAll(new WsOutgoingResponseMessage(responseTo, EyeTracker, identifiers, ResponseStatus.Rejected, "device is connecting"));
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