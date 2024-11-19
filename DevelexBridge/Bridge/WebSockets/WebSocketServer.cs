using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using Bridge.Models;
using Bridge.Output;

namespace Bridge.WebSockets;

// Stolen from https://github.com/jchristn/WatsonWebsocket/blob/master/src/WatsonWebsocket/WatsonWsServer.cs

public class WebSocketServer(string ipPort, Func<WsMessageRecievedArgs, Task> messageHandler)
{
    private HttpListener? _httpListener;
    private CancellationTokenSource? _cancellationTokenSource;
    public string IpPort { get; } = ipPort;
    public Func<WsMessageRecievedArgs, Task> MessageHandler { get; } = messageHandler;
    private readonly ConcurrentDictionary<Guid, WsClientMetadata> _clients = new();

    public void Start()
    {
        _httpListener = new HttpListener();
        _httpListener.Prefixes.Add($"http://{IpPort}/");
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            _httpListener.Start();
            
            Task.Run(() => AcceptWebSocketClientsAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
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
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (_httpListener == null || !_httpListener.IsListening)
                {
                    Task.Delay(100).Wait();
                    continue;
                }

                var context = await _httpListener.GetContextAsync().ConfigureAwait(false);

                if (!context.Request.IsWebSocketRequest)
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();

                    continue;
                }
                
                await Task.Run(() =>
                {
                    var newTokenSource = new CancellationTokenSource();
                    var newToken = newTokenSource.Token;

                    Task.Run(async () =>
                    {
                        var ctxGuid = context.Request.Headers.Get("x-guid");
                        var guid = string.IsNullOrEmpty(ctxGuid) ? Guid.NewGuid() : Guid.Parse(ctxGuid);
                        
                        var webSocketContext = await context.AcceptWebSocketAsync(null);
                        var clientMetadata = new WsClientMetadata(context, webSocketContext.WebSocket, webSocketContext, newTokenSource, guid);

                        _clients.TryAdd(guid, clientMetadata);

                        await Task.Run(() => HandleDataReciever(clientMetadata), newToken);
                    }, newToken);
                }, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException ||
                                       ex is ObjectDisposedException || ex is HttpListenerException)
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

    private async Task HandleDataReciever(WsClientMetadata clientMetadata)
    {
        var buffer = new byte[65536];

        try
        {
            while (true)
            {
                var message = await MessageReadAsync(clientMetadata, buffer).ConfigureAwait(false);

                if (message.Data != null)
                {
                    _ = Task.Run(() => MessageHandler(message), clientMetadata.TokenSource.Token);
                }
                else
                {
                    await Task.Delay(10).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleOutput.WsRecievingMessageError(ex.Message);
        }
        finally
        {
            _clients.TryRemove(clientMetadata.Id, out _);

            if (clientMetadata.WebSocket.State != WebSocketState.Closed)
            {
                clientMetadata.WebSocket.Abort();
            }
            
            clientMetadata.WebSocket.Dispose();
        }
    }

    private async Task<WsMessageRecievedArgs> MessageReadAsync(WsClientMetadata clientMetadata, byte[] buffer)
    {
        using (var stream = new MemoryStream(buffer))
        {
            var segment = new ArraySegment<byte>(buffer);

            while (true)
            {
                var result = await clientMetadata.WebSocket.ReceiveAsync(segment, clientMetadata.TokenSource.Token)
                    .ConfigureAwait(false);

                if (result.CloseStatus != null)
                {
                    await clientMetadata.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "",
                        CancellationToken.None);

                    throw new WebSocketException("Websocket closed");
                }

                if (clientMetadata.WebSocket.State != WebSocketState.Open)
                {
                    throw new WebSocketException("Websocket state is not open");
                }

                if (result.Count > 0)
                {
                    stream.Write(buffer, 0, result.Count);
                }

                if (result.EndOfMessage)
                {
                    return new WsMessageRecievedArgs(clientMetadata,
                        new ArraySegment<byte>(stream.GetBuffer(), 0, (int)stream.Length), result.MessageType);
                }
            }
        }
    }

    public IReadOnlyCollection<WsClientMetadata> GetClients() => _clients.Values.ToList().AsReadOnly();

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