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
}