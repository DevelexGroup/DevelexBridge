using Bridge.Output;

namespace Bridge;

public partial class BridgeWindow : Form
{
    public BridgeWindow()
    {
        InitializeComponent();

        Console.SetOut(new TextBoxConsole(tbConsole));
    }

    private void button1_Click(object sender, EventArgs e)
    {
        Console.WriteLine("testing");
    }

    private void Form1_Load(object sender, EventArgs e)
    {

    }
}