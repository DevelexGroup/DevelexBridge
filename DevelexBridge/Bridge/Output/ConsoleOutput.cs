namespace Bridge.Output;

public static class ConsoleOutput
{
    public static void WsRecievingMessageError(string errorMessage)
    {
        Console.WriteLine($"[WS] Nastala chyba při přijímaní zprávy: {errorMessage}");
    }

    public static void WsRecievedClose()
    {
        Console.WriteLine($"[WS] Klient poslal 'close', ukončuji s ním spojení.");
    }

    public static void WsListenerError(string errorMessage)
    {
        Console.WriteLine($"[WS] Nastala chyba při listeneru: {errorMessage}");
    }

    public static void WsListenerStopped()
    {
        Console.WriteLine($"[WS] Listener byl násilně ukončen.");
    }

    public static void WsStarted(string ipPort)
    {
        Console.WriteLine($"[WS] Websocket byl úspěšně zapnut na {ipPort}.");
    }

    public static void WsUnableToStart(string errorMessage)
    {
        Console.WriteLine($"[WS] Nebylo možné spustit Websocket server: {errorMessage}");
    }

    public static void WsStopped()
    {
        Console.WriteLine($"[WS] Websocket byl úspěšně vypnut.");
    }

    public static void WsMessageRecieved(string message)
    {
        Console.WriteLine($"[WS] Nová zpráva: {message}");
    }

    public static void InputWrongIpOrPort()
    {
        Console.WriteLine($"Špatně zadaná IP nebo port.");
    }
}