using System.Net;
using System.Net.WebSockets;

namespace Bridge.Models;

public class WsClientMetadata(HttpListenerContext httpContext, WebSocket webSocket, WebSocketContext webSocketContext, CancellationTokenSource tokenSource, Guid guid)
{
    public Guid Id { get; set; } = guid;
    public string Ip { get; set; } = httpContext.Request.RemoteEndPoint.Address.ToString();
    public int Port { get; set; } = httpContext.Request.RemoteEndPoint.Port;
    
    public HttpListenerContext HttpContext = httpContext;
    public WebSocket WebSocket = webSocket;
    public WebSocketContext WebSocketContext = webSocketContext;
    public readonly CancellationTokenSource TokenSource = tokenSource;
    public readonly SemaphoreSlim SendLock = new(1);
}