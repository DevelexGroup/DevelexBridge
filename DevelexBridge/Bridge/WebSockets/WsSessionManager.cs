using System.Collections.Concurrent;
using SuperSocket.WebSocket.Server;

namespace Bridge.WebSockets;

public static class WsSessionManager
{
    private static readonly ConcurrentDictionary<string, WebSocketSession> Sessions = new();
    
    public static void Add(WebSocketSession session) => Sessions[session.SessionID] = session;
    public static void Remove(string sessionId) => Sessions.TryRemove(sessionId, out _);
    public static void Remove(WebSocketSession session) => Remove(session.SessionID);
    public static IEnumerable<WebSocketSession> GetAll() => Sessions.Values;
}