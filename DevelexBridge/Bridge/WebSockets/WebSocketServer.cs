using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using Bridge.Output;

namespace Bridge.WebSockets;

public class WebSocketServer(string ipPort, Func<WebSocket, string, Task> messageHandler)
{
    private HttpListener? _httpListener;
    private CancellationTokenSource? _cancellationTokenSource;
    public string IpPort { get; } = ipPort;
    public Func<WebSocket, string, Task> MessageHandler { get; } = messageHandler;
    private readonly ConcurrentDictionary<WebSocket, string> _clients = new();

    public void Start()
    {
        _httpListener = new HttpListener();
        _httpListener.Prefixes.Add($"http://{IpPort}/");
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            _httpListener.Start();
            
            Task.Run(async () => await AcceptWebSocketClientsAsync(_cancellationTokenSource));
        }
        catch (Exception e)
        {
            _httpListener = null;
            throw;
        }
    }

    public void Stop()
    {
        foreach (var client in _clients.Keys)
        {
            try
            {
                if (client.State == WebSocketState.Open || client.State == WebSocketState.Connecting)
                {
                    client.Abort();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while aborting WebSocket: {ex.Message}");
            }
        }
        
        _cancellationTokenSource?.Cancel();
        _httpListener?.Stop();
        _httpListener?.Close();
        _httpListener = null;
    }

    [MemberNotNullWhen(true, nameof(_cancellationTokenSource))]
    [MemberNotNullWhen(true, nameof(_httpListener))]
    public bool IsRunning()
    {
        return _cancellationTokenSource != null && _httpListener != null;
    }
    
    private async Task AcceptWebSocketClientsAsync(CancellationTokenSource cancellationTokenSource)
    {
        if (!IsRunning())
        {
            return;
        }
        
        while (_httpListener.IsListening)
        {
            try 
            {
                var context = await _httpListener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(5));
                
                if (context.Request.IsWebSocketRequest)
                {
                    var webSocketContext = await context.AcceptWebSocketAsync(null);
                    _clients.TryAdd(webSocketContext.WebSocket,
                        context.Request.RemoteEndPoint?.ToString() ?? "Unknown");
                    await HandleWebSocketAsync(webSocketContext.WebSocket, cancellationTokenSource);
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
            catch (HttpListenerException)
            {
                ConsoleOutput.WsListenerStopped();
                break;
            }
            catch (Exception ex)
            {
                ConsoleOutput.WsListenerError(ex.Message);
            }
        }
    }
    
    private async Task HandleWebSocketAsync(WebSocket webSocket, CancellationTokenSource cancellationTokenSource)
    {
        var buffer = new byte[1024 * 4];

        while (webSocket.State == WebSocketState.Open && !cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationTokenSource.Token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationTokenSource.Token);
                    ConsoleOutput.WsRecievedClose();
                }
                else
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await MessageHandler(webSocket, message);
                }
            }
            catch (Exception ex)
            {
                ConsoleOutput.WsRecievingMessageError(ex.Message);
                break;
            }
        }

        if (_clients.TryRemove(webSocket, out var endpoint))
        {
            Console.WriteLine($"successfully closed with endpoint {endpoint}");
        }
        
        if (webSocket.State != WebSocketState.Closed)
        {
            webSocket.Abort();
        }

        webSocket.Dispose();
    }

    public IReadOnlyCollection<string> GetClients() => _clients.Values.ToList().AsReadOnly();

    public async Task SendToAll(string message)
    {
        var buffer = Encoding.UTF8.GetBytes(message);
        var tasks = _clients.Keys
            .Where(ws => ws.State == WebSocketState.Open)
            .Select(async ws =>
            {
                try
                {
                    await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            });

        await Task.WhenAll(tasks);
    }
}