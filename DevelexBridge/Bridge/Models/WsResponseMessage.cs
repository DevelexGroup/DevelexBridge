using System.Text.Json.Serialization;
using Bridge.Enums;

namespace Bridge.Models;

public class WsResponseIdentifiers(WsMessageIdentifiers identifiers)
{
    [JsonPropertyName("correlationId")]
    public int CorrelationId { get; init; } = identifiers.CorrelationId;

    [JsonPropertyName("initiatorId")]
    public string InitiatorId { get; init; } = identifiers.InitiatorId;
}

public class WsResponseMessage(string responseTo, EyeTracker eyeTracker, WsMessageIdentifiers identifiers)
    : WsResponseIdentifiers(identifiers)
{
    [JsonPropertyName("responseTo")]
    public string ResponseTo { get; set; } = responseTo;

    [JsonPropertyName("status")]
    public EyeTrackerState Status { get; set; } = eyeTracker.State;

    [JsonPropertyName("trackerCalibration")]
    public DateTime? TrackerCalibration { get; set; } = eyeTracker.LastCalibration;
}

public class WsTypeResponseMessage(string type)
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = type;
}

public class WsTypeResponseMessageWithIdentifiers(string type, WsMessageIdentifiers identifiers)
    : WsResponseIdentifiers(identifiers)
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = type;
}

public class WsErrorResponseMessage(string message) : WsTypeResponseMessage("error")
{
    [JsonPropertyName("content")]
    public string Message { get; set; } = message;
    
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class WsBridgeResponseMessage(string message, WsMessageIdentifiers identifiers)
    : WsTypeResponseMessageWithIdentifiers("message", identifiers)
{
    [JsonPropertyName("content")]
    public string Message { get; set; } = message;
    
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}