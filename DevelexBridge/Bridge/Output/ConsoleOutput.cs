namespace Bridge.Output;

public static class ConsoleOutput
{
    public static readonly string Version = "1.2.5";
    public static readonly string Start = "Start";
    public static readonly string Stop = "Stop";
    
    public static void WsRecievingMessageError(string errorMessage)
    {
        Console.WriteLine($"[WS] An error happened when recieving message: {errorMessage}");
    }

    public static void WsListenerError(string errorMessage)
    {
        Console.WriteLine($"[WS] An error happened on http listener: {errorMessage}");
    }

    public static void WsListenerStopped()
    {
        Console.WriteLine($"[WS] Http listener was stopped.");
    }

    public static void WsStarted(string ipPort)
    {
        Console.WriteLine($"[WS] Websocket {{Green}}successfully{{Default}} started, running on \"{ipPort}\" with version \"{Version}\".");
    }

    public static void WsUnableToStart(string errorMessage)
    {
        Console.WriteLine($"[WS] An error happened while starting websocket server: {errorMessage}");
    }

    public static void WsStopped()
    {
        Console.WriteLine($"[WS] Websocket server was successfully stopped.");
    }

    public static void WsMessageRecieved(string message)
    {
        Console.WriteLine($"[WS] New message recieved: {message}");
    }

    public static void WsUnableToParseMessage(string errorMessage)
    {
        Console.WriteLine($"[WS] Unable to parse recieved message: {errorMessage}");
    }

    public static void InputWrongIpOrPort()
    {
        Console.WriteLine($"[WS] IP or port are invalid.");
    }

    public static void WsMessageWriteError(string message)
    {
        Console.WriteLine($"[WS] An error happened while sending message: {message}");
    }

    public static void WsSendingToClients(string message, int clients)
    {
        Console.WriteLine($"[WS] Sending message to {clients} clients: {message}");
    }

    public static void WsServerIsNotRunning()
    {
        Console.WriteLine($"[WS] Websocket server {{Red}}is not{{Default}} running.");
    }

    public static void EtStoppedRecording()
    {
        Console.WriteLine($"[ET] Eye tracker stopped recording.");
    }

    public static void EtStartedRecording()
    {
        Console.WriteLine($"[ET] Eye tracker started recording.");
    }
}