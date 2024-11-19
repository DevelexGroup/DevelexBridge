using System.Net.WebSockets;

namespace Bridge.Models;

public class WsMessageRecievedArgs(WsClientMetadata clientMetadata, ArraySegment<byte> data, WebSocketMessageType messageType)
{
    public WsClientMetadata ClientMetadata { get; set; } = clientMetadata;
    public ArraySegment<byte> Data { get; set; } = data;
    public WebSocketMessageType MessageType { get; set; } = messageType;
}