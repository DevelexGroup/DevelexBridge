using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using Bridge.Output;

namespace Bridge.WebSockets;

public class WebSocketServer(string ipPort)
{
    private HttpListener? _httpListener;
    private CancellationTokenSource? _cancellationTokenSource;
    public string IpPort { get; } = ipPort;
    public event Action<WebSocket, string>? MessageRecieved;

    public void Start()
    {
        _httpListener = new HttpListener();
        _httpListener.Prefixes.Add($"http://{IpPort}/");
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            _httpListener.Start();
            
            Task.Run(() => AcceptWebSocketClientsAsync(_cancellationTokenSource.Token));
        }
        catch (Exception e)
        {
            _httpListener = null;
            throw;
        }
    }

    public void Stop()
    {
        _cancellationTokenSource?.Cancel();
        _httpListener?.Stop();
        _httpListener = null;
    }

    [MemberNotNullWhen(true, nameof(_cancellationTokenSource))]
    [MemberNotNullWhen(true, nameof(_httpListener))]
    public bool IsRunning()
    {
        return _cancellationTokenSource != null && _httpListener != null;
    }
    
    private async Task AcceptWebSocketClientsAsync(CancellationToken token)
    {
        if (!IsRunning())
        {
            return;
        }
        
        while (_httpListener.IsListening)
        {
            try
            {
                var context = await _httpListener.GetContextAsync();

                if (context.Request.IsWebSocketRequest)
                {
                    var webSocketContext = await context.AcceptWebSocketAsync(null);
                    await HandleWebSocketAsync(webSocketContext.WebSocket, token);
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
    
    private async Task HandleWebSocketAsync(WebSocket webSocket, CancellationToken token)
    {
        var buffer = new byte[1024 * 4];

        while (webSocket.State == WebSocketState.Open && !token.IsCancellationRequested)
        {
            try
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", token);
                    ConsoleOutput.WsRecievedClose();
                }
                else
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    MessageRecieved?.Invoke(webSocket, message);
                }
            }
            catch (Exception ex)
            {
                ConsoleOutput.WsRecievingMessageError(ex.Message);
                break;
            }
        }

        if (webSocket.State != WebSocketState.Closed)
        {
            webSocket.Abort();
            webSocket.Dispose();
        }
    }
}