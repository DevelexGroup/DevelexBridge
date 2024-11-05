using System.Text.Json.Serialization;

namespace Bridge.Models;

public class WsResponseOutput(string type) : WsBaseResponseMessage(type)
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
    public string? FixationId { get; set; }
    
    [JsonPropertyName("fixationDuration")]
    public float? FixationDuration { get; set; }
}