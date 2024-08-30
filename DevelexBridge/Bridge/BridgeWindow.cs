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
                Console.WriteLine("Zadejete ip nebo port");
                return;
            }

            Server = new WebSocketServer(ipPort);
            Server.MessageRecieved += OnMessageRecieved;

            try
            {
                Server.Start();
                buttonStartStop.Text = "Vypnout";
                Console.WriteLine("Websocket server byl zapnut.");
            }
            catch (Exception ex)
            {
                Server = null;
                buttonStartStop.Text = "Zapnout";
                Console.WriteLine($"Nebylo možné zapnout websocket server: {ex.Message}");
            }
        }
        else
        {
            Server.Stop();
            Server = null;
            buttonStartStop.Text = "Zapnout";
            Console.WriteLine("Websocket server byl vypnut.");
        }
    }

    private void OnMessageRecieved(WebSocket webSocket, string message)
    {
        Console.WriteLine($"From reciver: {message}");
    }

    private void Form1_Load(object sender, EventArgs e)
    {

    }
}