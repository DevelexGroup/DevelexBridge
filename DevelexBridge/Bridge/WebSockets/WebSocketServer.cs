using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using Bridge.Models;
using Bridge.Output;

namespace Bridge.WebSockets;

/**
 * This WebSocket Server is improved version for Develex purposees from
 * WatsonWebSocket Server (https://github.com/jchristn/WatsonWebsocket/blob/master/src/WatsonWebsocket/WatsonWsServer.cs)
 */
public class WebSocketServer : IDisposable
{
    private HttpListener _httpListener;
    private CancellationTokenSource _cancellationTokenSource = new();
    private CancellationToken _cancellationToken;
    public string IpPort { get; }
    private Func<WsMessageRecievedArgs, Task> MessageHandler { get; }
    private readonly ConcurrentDictionary<Guid, WsClientMetadata> _clients = new();

    public WebSocketServer(string ipPort, Func<WsMessageRecievedArgs, Task> messageHandler)
    {
        IpPort = ipPort;
        MessageHandler = messageHandler;

        _httpListener = new HttpListener();
        _httpListener.Prefixes.Add($"http://{IpPort}/");
        
        _cancellationToken = _cancellationTokenSource.Token;
    }

    public void Dispose()
    {
        Dispose(true);
    }

    public void Start()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _cancellationToken = _cancellationTokenSource.Token;
        _httpListener.Start();
        
        Task.Run(() => AcceptWebSocketClientsAsync(_cancellationToken), _cancellationToken);
    }

    public void Stop()
    {
        _httpListener.Stop();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var client in _clients)
            {
                client.Value.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", client.Value.TokenSource.Token);
                client.Value.TokenSource.Cancel();
            }
        
            if (_httpListener.IsListening)
                _httpListener.Stop();
        
            _httpListener.Close();
        
            _cancellationTokenSource.Cancel();
        }
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
                }, _cancellationToken).ConfigureAwait(false);
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
                
                _ = Task.Run(() => MessageHandler(message), clientMetadata.TokenSource.Token);
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
        using var stream = new MemoryStream(buffer);
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
                var resultMessage = Encoding.UTF8.GetString(stream.ToArray(), 0, result.Count);
                    
                return new WsMessageRecievedArgs(clientMetadata, resultMessage, result.MessageType);
            }
        }
    }

    public IReadOnlyCollection<WsClientMetadata> GetClients() => _clients.Values.ToList().AsReadOnly();

    public async Task SendToAll(string message)
    {
        var tasks = _clients.Values
            .Where(metadata => metadata.WebSocket.State == WebSocketState.Open)
            .Select(async metadata => await SendAsync(metadata.Id, message))
            .ToList();

        ConsoleOutput.WsSendingToClients(message, tasks.Count);
        
        await Task.WhenAll(tasks);
    }

    public Task<bool> SendAsync(Guid guid, string message, CancellationToken token = default)
    {
        if (!_clients.TryGetValue(guid, out var clientMetadata))
        {
            return Task.FromResult(false);
        }
        
        var messageWriteTask = MessageWriteAsync(clientMetadata, Encoding.UTF8.GetBytes(message), token);
        
        return messageWriteTask;
    }

    private async Task<bool> MessageWriteAsync(WsClientMetadata clientMetadata, byte[] buffer, CancellationToken token)
    {
        var tokens = new CancellationToken[3];
        tokens[0] = _cancellationToken;
        tokens[1] = token;
        tokens[2] = clientMetadata.TokenSource.Token;

        using var linkedTokens = CancellationTokenSource.CreateLinkedTokenSource(tokens);

        try
        {
            await clientMetadata.SendLock.WaitAsync(clientMetadata.TokenSource.Token).ConfigureAwait(false);

            try
            {
                await clientMetadata.WebSocket
                    .SendAsync(buffer, WebSocketMessageType.Text, true, linkedTokens.Token).ConfigureAwait(false);
            }
            finally
            {
                clientMetadata.SendLock.Release();
            }

            return true;
        }
        catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException)
        {
            if (_cancellationTokenSource.Token.IsCancellationRequested)
                ConsoleOutput.WsMessageWriteError("server canceled");
            else if (token.IsCancellationRequested)
                ConsoleOutput.WsMessageWriteError("message sender canceled");
            else if (clientMetadata.TokenSource.Token.IsCancellationRequested)
                ConsoleOutput.WsMessageWriteError("client canceled");
        }
        catch (Exception ex) when (ex is WebSocketException || ex is SocketException)
        {
            ConsoleOutput.WsMessageWriteError("websocket disconnected");
        }
        catch (Exception ex)
        {
            ConsoleOutput.WsMessageWriteError(ex.Message);
        }

        return false;
    }

    public void DisconnectClient(Guid guid)
    {
        if (_clients.TryGetValue(guid, out var clientMetadata))
        {
            lock (clientMetadata)
            {
                clientMetadata.WebSocket
                    .CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", clientMetadata.TokenSource.Token)
                    .Wait();
                clientMetadata.TokenSource.Cancel();
                clientMetadata.WebSocket.Dispose();
            }
        }
    }
}