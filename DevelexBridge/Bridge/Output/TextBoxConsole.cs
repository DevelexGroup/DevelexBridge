using System.Text;

namespace Bridge.Output;

internal class TextBoxConsole : TextWriter
{
    private readonly TextBox _textBox;

    public TextBoxConsole(TextBox textBox)
    {
        _textBox = textBox;
    }

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        if (_textBox.InvokeRequired)
        {
            _textBox.Invoke(new Action<char>(Write), value);
        }
        else
        {
            _textBox.AppendText(value.ToString());
        }
    }

    public override void Write(string value)
    {
        if (_textBox.InvokeRequired)
        {
            _textBox.Invoke(new Action<string>(Write), value);
        }
        else
        {
            _textBox.AppendText(value);
        }
    }
}
