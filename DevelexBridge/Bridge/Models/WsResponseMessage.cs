using System.Text.Json.Serialization;
using Bridge.Enums;

namespace Bridge.Models;

public class WsBaseResponseMessage(string type)
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = type;
}

public class WsErrorResponseMessage(string message) : WsBaseResponseMessage("error")
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = message;
}

public class WsStatusResponseMessage(EyeTrackerState state) : WsBaseResponseMessage("status")
{
    [JsonPropertyName("state")]
    public EyeTrackerState State { get; set; } = state;
}