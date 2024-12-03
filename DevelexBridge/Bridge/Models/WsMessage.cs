using System.Text.Json.Serialization;

namespace Bridge.Models;

public class WsMessageIdentifiers
{
    [JsonPropertyName("correlationId")]
    public required int CorrelationId { get; init; }
    
    [JsonPropertyName("initiatorId")]
    public required string InitiatorId { get; init; }
}

public class WsBaseMessage : WsMessageIdentifiers
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    public WsMessageIdentifiers Identifiers => new()
        { CorrelationId = CorrelationId, InitiatorId = InitiatorId };
}

public class WsConnectMessage : WsBaseMessage
{
    [JsonPropertyName("config")]
    public required WsConnectEyeTrackerConfig Config { get; set; }
}

public class WsConnectEyeTrackerConfig
{
    [JsonPropertyName("trackerType")]
    public required string TrackerType { get; set; }
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

public class WsBridgeMessage : WsBaseMessage
{
    [JsonPropertyName("content")]
    public required string Content { get; set; }
}

public class WsSubscribeMessage : WsBaseMessage
{
    
}

public class WsUnsubscribeMessage : WsBaseMessage
{
    
}