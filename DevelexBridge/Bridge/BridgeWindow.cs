using System.Diagnostics.CodeAnalysis;
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
        
        Console.SetOut(new TextBoxConsole(tbConsoleContainer.InnerTextBox));
    }

    private void startStopButton_Click(object sender, EventArgs e)
    {
        if (Server == null)
        {
            var ipPort = tbIpPortContainer.InnerTextBox.Text;

            if (string.IsNullOrEmpty(ipPort))
            {
                ConsoleOutput.InputWrongIpOrPort();
                return;
            }

            Server = new WebSocketServer(ipPort, OnMessageRecieved);

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
            Task.Run(() =>
            {
                Server.Stop();
                Server.Dispose();
                Server = null;
                
                buttonStartStop.Text = "Zapnout";
                buttonStartStop.Enabled = true;
                ConsoleOutput.WsStopped();
            });
        }
    }

    private async Task OnMessageRecieved(WsMessageRecievedArgs messageArgs)
    {
        var message = messageArgs.Data;
        var clientMetadata = messageArgs.ClientMetadata;
        
        ConsoleOutput.WsMessageRecieved(message);

        if (!TryParseWebsocketMessage(message, out var parsedMessage))
        {
            ConsoleOutput.WsUnableToParseMessage("neznámý typ zprávy");
            return;
        }

        switch (parsedMessage)
        {
            case WsConnectMessage connectMessage:
                await OnConnectMessage(clientMetadata, connectMessage);
                break;
            case WsStartMessage startMessage:
                await OnStartMessage(clientMetadata, startMessage);
                break;
            case WsStopMessage stopMessage:
                await OnStopMessage(clientMetadata, stopMessage);
                break;
            case WsCalibrateMessage calibrateMessage:
                await OnCalibrateMessage(clientMetadata, calibrateMessage);
                break;
            case WsDisconnectMessage disconnectMessage:
                await OnDisconnectMessage(clientMetadata, disconnectMessage);
                break;
            case WsBridgeStatusMessage bridgeStatusMessage:
                await OnBridgeStateMessage(clientMetadata, bridgeStatusMessage);
                break;
            case WsSubscribeMessage subscribeMessage:
                await OnSubscribeMessage(clientMetadata, subscribeMessage);
                break;
            case WsUnsubscribeMessage unsubscribeMessage:
                await OnUnsubscribeMessage(clientMetadata, unsubscribeMessage);
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
                "status" => JsonSerializer.Deserialize<WsBridgeStatusMessage>(message),
                "subscribe" => JsonSerializer.Deserialize<WsSubscribeMessage>(message),
                "unsubscribe" => JsonSerializer.Deserialize<WsUnsubscribeMessage>(message),
                _ => null,
            };

            return parsedMessage != null;
        }
        catch (Exception ex)
        {
            return false;
        }
    }
    
    private Task SendToAll<T>(T responseMessage)
    {
        return Server?.SendToAll(JsonSerializer.Serialize(responseMessage)) ?? Task.FromResult(false);
    }
}