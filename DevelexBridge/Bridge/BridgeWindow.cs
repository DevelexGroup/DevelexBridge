using System.Text.Json;
using Bridge.Exceptions.Parser;
using Bridge.Models;
using Bridge.Output;
using Bridge.WebSockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SuperSocket.ProtoBase;
using SuperSocket.Server.Abstractions;
using SuperSocket.Server.Host;
using SuperSocket.WebSocket;
using SuperSocket.WebSocket.Server;

namespace Bridge;

public partial class BridgeWindow : Form
{
    private IHost? Server { get; set; }
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

            var ip = ipPort.Split(":");
            
            // The communication over this connection is being closed, send is not allowed.
            // Writing is not allowed after writer was completed.
            
            var server = WebSocketHostBuilder.Create()
                .UseWebSocketMessageHandler(OnWebSocketMessageHandle)
                .UseSessionHandler(
                    onConnected: session =>
                    {
                        WsSessionManager.Add((WebSocketSession)session);
                        return ValueTask.CompletedTask;
                    },
                    onClosed: (session, reason) =>
                    {
                        Console.WriteLine($"ALERT: disconnecting client because of {reason.Reason}");
                        WsSessionManager.Remove(session.SessionID);
                        return ValueTask.CompletedTask;
                    })
                .ConfigureAppConfiguration((hostCtx, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "serverOptions:name", "Develex Bridge Server" },
                        { "serverOptions:listeners:0:ip", ip[0] },
                        { "serverOptions:listeners:0:port", ip[1] },
                        { "serverOptions:clearIdleSession", "false" },
                        { "serverOptions:idleSessionTimeOut", "0" },
                        { "serverOptions:clearIdleSessionInterval", "0" },
                        { "serverOptions:receiveTimeout", "0" },
                        { "serverOptions:keepAliveTime", "15000" },
                        { "serverOptions:keepAliveInterval", "15000" },
                    }!);
                })
                .ConfigureLogging((hostCtx, loggingBuilder) =>
                {
                    loggingBuilder.ClearProviders();
                })
                .Build();

            Server = server;
            
            try
            {
                Task.Run(() => Server.RunAsync());
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
            Task.Run(async () =>
            {
                if (EyeTracker != null)
                {
                    await EyeTracker.Stop();
                    await EyeTracker.Disconnect();
                    EyeTracker = null;
                }
                
                await Server.StopAsync();
                Server.Dispose();
                Server = null;
                
                buttonStartStop.Text = ConsoleOutput.Start;
                buttonStartStop.Enabled = true;
                ConsoleOutput.WsStopped();
            });
        }
    }

    private async ValueTask OnWebSocketMessageHandle(WebSocketSession session, WebSocketPackage package)
    {
        var message = package.Message;
        
        ConsoleOutput.WsMessageRecieved(message);

        try
        {
            var parsedMessage = ParseWebsocketMessage(message);
            
            switch (parsedMessage)
            {
                case WsIncomingConnectMessage connectMessage:
                    await OnConnectMessage(session, connectMessage);
                    break;
                case WsIncomingStartMessage startMessage:
                    await OnStartMessage(session, startMessage);
                    break;
                case WsIncomingStopMessage stopMessage:
                    await OnStopMessage(session, stopMessage);
                    break;
                case WsIncomingCalibrateMessage calibrateMessage:
                    await OnCalibrateMessage(session, calibrateMessage);
                    break;
                case WsIncomingDisconnectMessage disconnectMessage:
                    await OnDisconnectMessage(session, disconnectMessage);
                    break;
                case WsIncomingBridgeStateMessage bridgeStateMessage:
                    await OnBridgeStateMessage(session, bridgeStateMessage);
                    break;
                case WsIncomingSubscribeMessage subscribeMessage:
                    await OnSubscribeMessage(session, subscribeMessage);
                    break;
                case WsIncomingUnsubscribeMessage unsubscribeMessage:
                    await OnUnsubscribeMessage(session, unsubscribeMessage);
                    break;
                case WsIncomingBridgeMessage bridgeMessage:
                    await OnBridgeMessage(session, bridgeMessage);
                    break;
            }
        }
        catch (Exception ex)
        {
            ConsoleOutput.WsUnableToParseMessage(ex.Message);
            await WsBroadcaster.SendToAll(new WsOutgoingErrorMessage(ex.Message));
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
}