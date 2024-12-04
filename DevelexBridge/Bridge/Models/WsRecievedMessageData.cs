using System.Net.WebSockets;

namespace Bridge.Models;

public class WsRecievedMessageData(WsClientMetadata clientMetadata, string data, WebSocketMessageType messageType)
{
    public WsClientMetadata ClientMetadata { get; set; } = clientMetadata;
    public string Data { get; set; } = data;
    public WebSocketMessageType MessageType { get; set; } = messageType;
}