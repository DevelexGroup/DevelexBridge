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

        var tasks = WsSessionManager.GetAll()
            .Where(s => s.State == SessionState.Connected)
            .Select(async s =>
            {
                try
                {
                    await s.SendAsync(serializedMessage).AsTask();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Unable to send message: {e.Message}");
                }
            });

        return Task.WhenAll(tasks);
    }
}