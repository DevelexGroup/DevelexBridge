using System.Text.Json.Serialization;

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