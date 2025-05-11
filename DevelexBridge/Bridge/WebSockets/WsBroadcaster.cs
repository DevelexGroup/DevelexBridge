using System.Text.Json;
using SuperSocket.Server.Abstractions;

namespace Bridge.WebSockets;

public static class WsBroadcaster
{
    public static Task SendToAll<T>(T message, bool log = true)
    {
        var serializedMessage = JsonSerializer.Serialize(message);

        if (log)
        {
            Task.Run(() => Console.WriteLine($"Broadcasting message: {serializedMessage}"));
        }

        return Task.WhenAll(
            WsSessionManager.GetAll()
                .Where(s => s.State == SessionState.Connected)
                .Select(s => s.SendAsync(serializedMessage).AsTask())
        );
    }
}