using System.Text.Json.Serialization;
using Bridge.Enums;
using Bridge.Extensions;

namespace Bridge.Models;

public abstract class WsOutgoingMessage(string type)
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = type;
}

public abstract class WsOutgoingMessageWithIdentifiers(string type, WsMessageIdentifiers identifiers) : WsOutgoingMessage(type)
{
    [JsonPropertyName("correlationId")]
    public int CorrelationId { get; init; } = identifiers.CorrelationId;

    [JsonPropertyName("initiatorId")]
    public string InitiatorId { get; init; } = identifiers.InitiatorId;
}

public class WsOutgoingResponseMessage(string responseTo, EyeTracker eyeTracker, WsMessageIdentifiers identifiers) : WsOutgoingMessageWithIdentifiers("response", identifiers)
{ 
    [JsonPropertyName("responseTo")] 
    public string ResponseTo { get; set; } = responseTo; 
    
    [JsonPropertyName("status")] 
    public string Status { get; set; } = eyeTracker.State.GetDisplayName() ?? "trackerDisconnected";
    
    [JsonPropertyName("trackerCalibration")] 
    public DateTime? TrackerCalibration { get; set; } = eyeTracker.LastCalibration;
}

public class WsOutgoingErrorMessage(string content) : WsOutgoingMessage("error")
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = content;
    
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class WsOutgoingTunnelMessage(string content, WsMessageIdentifiers identifiers) : WsOutgoingMessageWithIdentifiers("message", identifiers)
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = content;
    
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class WsOutgoingGazeMessage() : WsOutgoingMessage("gaze")
{
    [JsonPropertyName("xL")]
    public double LeftX { get; set; }
    
    [JsonPropertyName("yL")]
    public double LeftY { get; set; }
    
    [JsonPropertyName("xR")]
    public double RightX { get; set; }
    
    [JsonPropertyName("yR")]
    public double RightY { get; set; }
    
    [JsonPropertyName("validityL")]
    public bool LeftValidity { get; set; }
    
    [JsonPropertyName("validityR")]
    public bool RightValidity { get; set; }
    
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
    
    [JsonPropertyName("fixationId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FixationId { get; set; }
    
    [JsonPropertyName("fixationDuration")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? FixationDuration { get; set; }
    
    [JsonPropertyName("pupilDiameterL")]
    public double LeftPupil { get; set; }
    
    [JsonPropertyName("pupilDiameterR")]
    public double RightPupil { get; set; }
}