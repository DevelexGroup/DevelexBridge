using System.Net;
using System.Net.WebSockets;
using System.Text;
using Bridge.Output;
using Bridge.WebSockets;

namespace Bridge;

public partial class BridgeWindow : Form
{
    public WebSocketServer? Server { get; set; } = null;
    
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
    }

    private void Form1_Load(object sender, EventArgs e)
    {

    }
}