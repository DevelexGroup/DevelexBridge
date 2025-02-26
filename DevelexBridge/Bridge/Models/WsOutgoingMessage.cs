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

public class WsOutgoingResponseMessage(string responseTo, EyeTracker? eyeTracker, WsMessageIdentifiers identifiers, ResponseStatus responseStatus, string responseMessage = "") 
    : WsOutgoingMessageWithIdentifiers("response", identifiers)
{
    [JsonPropertyName("tracker")]
    public TrackerInfo Tracker { get; set; } = new(eyeTracker);
    
    [JsonPropertyName("response")]
    public ResponseInfo Response { get; set; } = new (responseTo, responseStatus, responseMessage);

    public class ResponseInfo(string responseTo, ResponseStatus responseStatus, string responseMessage = "")
    {
        [JsonPropertyName("to")]
        public string To { get; set; } = responseTo;

        [JsonPropertyName("status")]
        public string Status { get; set; } = responseStatus.GetDisplayName() ?? "rejected";

        [JsonPropertyName("message")]
        public string Message { get; set; } = responseMessage;
    }

    public class TrackerInfo(EyeTracker? eyeTracker)
    {
        [JsonPropertyName("status")] 
        public string Status { get; set; } = eyeTracker?.State.GetDisplayName() ?? "trackerDisconnected";
    
        [JsonPropertyName("calibration")] 
        public string? Calibration { get; set; } = eyeTracker?.LastCalibration?.ToIso();
    }
}

public class WsOutgoingErrorMessage(string content) : WsOutgoingMessage("error")
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = content;
    
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = DateTimeExtensions.IsoNow;
}

public class WsOutgoingTunnelMessage(string content, WsMessageIdentifiers identifiers) : WsOutgoingMessageWithIdentifiers("message", identifiers)
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = content;
    
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = DateTimeExtensions.IsoNow;
}

public class WsOutgoingGazeMessage() : WsOutgoingMessage("gaze")
{
    [JsonPropertyName("xL")]
    public required double LeftX { get; set; }
    
    [JsonPropertyName("yL")]
    public required double LeftY { get; set; }
    
    [JsonPropertyName("xR")]
    public required double RightX { get; set; }
    
    [JsonPropertyName("yR")]
    public required double RightY { get; set; }
    
    [JsonPropertyName("validityL")]
    public bool LeftValidity { get; set; }
    
    [JsonPropertyName("validityR")]
    public bool RightValidity { get; set; }
    
    [JsonPropertyName("timestamp")]
    public required string Timestamp { get; set; }
    
    [JsonPropertyName("deviceTimestamp")]
    public required string DeviceTimestamp { get; set; }
    
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