using System.Text.Json.Serialization;

namespace Bridge.Models;

public class WsBaseMessage
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }
}

public class WsConnectMessage : WsBaseMessage
{
    [JsonPropertyName("tracker")]
    public required string Tracker { get; set; }
    
    [JsonPropertyName("keepFixations")]
    public bool? KeepFixations { get; set; }
}

public class WsStartMessage : WsBaseMessage
{
    
}

public class WsStopMessage : WsBaseMessage
{
    
}

public class WsCalibrateMessage : WsBaseMessage
{
    
}

public class WsDisconnectMessage : WsBaseMessage
{
    
}

public class WsBridgeStatusMessage : WsBaseMessage
{
    
}