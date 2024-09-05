using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Bridge.Models;
using Bridge.Output;
using Bridge.WebSockets;

namespace Bridge;

public partial class BridgeWindow : Form
{
    private WebSocketServer? Server { get; set; } = null;
    private EyeTracker? EyeTracker { get; set; } = null;
    
    public BridgeWindow()
    {
        InitializeComponent();

        Console.SetOut(new TextBoxConsole(tbConsole));
    }

    private void startStopButton_Click(object sender, EventArgs e)
    {
        if (Server == null)
        {
            var ipPort = tbIpPort.Text;

            if (string.IsNullOrEmpty(ipPort))
            {
                ConsoleOutput.InputWrongIpOrPort();
                return;
            }

            Server = new WebSocketServer(ipPort);
            Server.MessageRecieved += OnMessageRecieved;

            try
            {
                Server.Start();
                buttonStartStop.Text = "Vypnout";
                ConsoleOutput.WsStarted(ipPort);
            }
            catch (Exception ex)
            {
                Server = null;
                buttonStartStop.Text = "Zapnout";
                ConsoleOutput.WsUnableToStart(ex.Message);
            }
        }
        else
        {
            Server.Stop();
            Server = null;
            buttonStartStop.Text = "Zapnout";
            ConsoleOutput.WsStopped();
        }
    }

    private void OnMessageRecieved(WebSocket webSocket, string message)
    {
        ConsoleOutput.WsMessageRecieved(message);

        if (!TryParseWebsocketMessage(message, out var parsedMessage))
        {
            ConsoleOutput.WsUnableToParseMessage("neznámý typ zprávy");
            return;
        }

        switch (parsedMessage)
        {
            case WsConnectMessage connectMessage:
                OnConnectMessage(webSocket, connectMessage);
                break;
            case WsStartMessage startMessage:
                OnStartMessage(webSocket, startMessage);
                break;
            case WsStopMessage stopMessage:
                OnStopMessage(webSocket, stopMessage);
                break;
            case WsCalibrateMessage calibrateMessage:
                OnCalibrateMessage(webSocket, calibrateMessage);
                break;
            case WsDisconnectMessage disconnectMessage:
                OnDisconnectMessage(webSocket, disconnectMessage);
                break;
        }
    }
    
    private bool TryParseWebsocketMessage(string message, [NotNullWhen(true)] out WsBaseMessage? parsedMessage)
    {
        parsedMessage = null;

        try
        {
            var baseMessage = JsonSerializer.Deserialize<WsBaseMessage>(message);

            if (baseMessage == null)
            {
                return false;
            }

            parsedMessage = baseMessage.Type switch
            {
                "connect" => JsonSerializer.Deserialize<WsConnectMessage>(message),
                "start" => JsonSerializer.Deserialize<WsStartMessage>(message),
                "stop" => JsonSerializer.Deserialize<WsStopMessage>(message),
                "calibrate" => JsonSerializer.Deserialize<WsCalibrateMessage>(message),
                "disconnect" => JsonSerializer.Deserialize<WsDisconnectMessage>(message),
                _ => null,
            };

            return parsedMessage != null;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WsUnableToParseMessage(ex.Message);
            return false;
        }
    }
}