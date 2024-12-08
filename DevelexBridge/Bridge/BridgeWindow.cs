using System.Text.Json;
using Bridge.Exceptions.Parser;
using Bridge.Models;
using Bridge.Output;
using Bridge.WebSockets;

namespace Bridge;

public partial class BridgeWindow : Form
{
    private WebSocketServer? Server { get; set; }
    private EyeTracker? EyeTracker { get; set; }
    
    public BridgeWindow()
    {
        InitializeComponent();
        
        Console.SetOut(new RichTextBoxConsole(tbConsoleContainer.InnerTextBox));
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
                buttonStartStop.Text = ConsoleOutput.Stop;
                ConsoleOutput.WsStarted(ipPort);
            }
            catch (Exception ex)
            {
                Server = null;
                buttonStartStop.Text = ConsoleOutput.Start;
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
                
                buttonStartStop.Text = ConsoleOutput.Start;
                buttonStartStop.Enabled = true;
                ConsoleOutput.WsStopped();
            });
        }
    }

    private async Task OnMessageRecieved(WsRecievedMessageData recievedMessageArgs)
    {
        var message = recievedMessageArgs.Data;
        var clientMetadata = recievedMessageArgs.ClientMetadata;
        
        ConsoleOutput.WsMessageRecieved(message);

        try
        {
            var parsedMessage = ParseWebsocketMessage(message);
            
            switch (parsedMessage)
            {
                case WsIncomingConnectMessage connectMessage:
                    await OnConnectMessage(clientMetadata, connectMessage);
                    break;
                case WsIncomingStartMessage startMessage:
                    await OnStartMessage(clientMetadata, startMessage);
                    break;
                case WsIncomingStopMessage stopMessage:
                    await OnStopMessage(clientMetadata, stopMessage);
                    break;
                case WsIncomingCalibrateMessage calibrateMessage:
                    await OnCalibrateMessage(clientMetadata, calibrateMessage);
                    break;
                case WsIncomingDisconnectMessage disconnectMessage:
                    await OnDisconnectMessage(clientMetadata, disconnectMessage);
                    break;
                case WsIncomingBridgeStateMessage bridgeStateMessage:
                    await OnBridgeStateMessage(clientMetadata, bridgeStateMessage);
                    break;
                case WsIncomingSubscribeMessage subscribeMessage:
                    await OnSubscribeMessage(clientMetadata, subscribeMessage);
                    break;
                case WsIncomingUnsubscribeMessage unsubscribeMessage:
                    await OnUnsubscribeMessage(clientMetadata, unsubscribeMessage);
                    break;
                case WsIncomingBridgeMessage bridgeMessage:
                    await OnBridgeMessage(clientMetadata, bridgeMessage);
                    break;
            }
        }
        catch (Exception ex)
        {
            ConsoleOutput.WsUnableToParseMessage(ex.Message);
            await SendToAll(new WsOutgoingErrorMessage(ex.Message));
        }
    }
    
    private WsIncomingMessage ParseWebsocketMessage(string message)
    {
        var baseMessage = JsonSerializer.Deserialize<WsIncomingMessage>(message);

        if (baseMessage == null)
        {
            throw new ArgumentNullException("type or identifiers are invalid or missing");
        }
        
        WsIncomingMessage? parsedMessage = baseMessage.Type switch
        {
            "connect" => JsonSerializer.Deserialize<WsIncomingConnectMessage>(message),
            "start" => JsonSerializer.Deserialize<WsIncomingStartMessage>(message),
            "stop" => JsonSerializer.Deserialize<WsIncomingStopMessage>(message),
            "calibrate" => JsonSerializer.Deserialize<WsIncomingCalibrateMessage>(message),
            "disconnect" => JsonSerializer.Deserialize<WsIncomingDisconnectMessage>(message),
            "status" => JsonSerializer.Deserialize<WsIncomingBridgeStateMessage>(message),
            "subscribe" => JsonSerializer.Deserialize<WsIncomingSubscribeMessage>(message),
            "unsubscribe" => JsonSerializer.Deserialize<WsIncomingUnsubscribeMessage>(message),
            "message" => JsonSerializer.Deserialize<WsIncomingBridgeMessage>(message),
            _ => null,
        };

        if (parsedMessage == null)
        {
            throw new UnknownType($"unknown type \"{baseMessage.Type}\"");
        }

        return parsedMessage;
    }
    
    private Task SendToAll<T>(T responseMessage)
    {
        return Server?.SendToAll(JsonSerializer.Serialize(responseMessage)) ?? Task.FromResult(false);
    }
}