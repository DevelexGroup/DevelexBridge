using System.Text;

namespace Bridge.Output;

internal class TextBoxConsole(TextBox textBox) : TextWriter
{
    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        if (textBox.InvokeRequired)
        {
            textBox.Invoke(new Action<char>(Write), value);
        }
        else
        {
            textBox.AppendText(value.ToString());
        }
    }

    public override void Write(string? value)
    {
        if (textBox.InvokeRequired)
        {
            textBox.Invoke(new Action<string>(Write), value);
        }
        else
        {
            textBox.AppendText(value);
        }
    }
}
