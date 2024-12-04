using System.Text.Json.Serialization;

namespace Bridge.Models;

public class WsMessageIdentifiers
{
    [JsonPropertyName("correlationId")]
    public required int CorrelationId { get; init; }
    
    [JsonPropertyName("initiatorId")]
    public required string InitiatorId { get; init; }
}

public class WsIncomingMessage : WsMessageIdentifiers
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    public WsMessageIdentifiers Identifiers => new()
        { CorrelationId = CorrelationId, InitiatorId = InitiatorId };
}

public class WsIncomingConnectMessage : WsIncomingMessage
{
    [JsonPropertyName("config")]
    public required ConnectEyeTrackerConfig Config { get; set; }
}

public class ConnectEyeTrackerConfig
{
    [JsonPropertyName("trackerType")]
    public required string TrackerType { get; set; }
}

public class WsIncomingStartMessage : WsIncomingMessage
{
    
}

public class WsIncomingStopMessage : WsIncomingMessage
{
    
}

public class WsIncomingCalibrateMessage : WsIncomingMessage
{
    
}

public class WsIncomingDisconnectMessage : WsIncomingMessage
{
    
}

public class WsIncomingBridgeStateMessage : WsIncomingMessage
{
    
}

public class WsIncomingBridgeMessage : WsIncomingMessage
{
    [JsonPropertyName("content")]
    public required string Content { get; set; }
}

public class WsIncomingSubscribeMessage : WsIncomingMessage
{
    
}

public class WsIncomingUnsubscribeMessage : WsIncomingMessage
{
    
}