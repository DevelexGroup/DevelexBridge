using System.Text;
using System.Text.RegularExpressions;

namespace Bridge.Output;

internal partial class RichTextBoxConsole : TextWriter
{
    private readonly RichTextBox _richTextBox;

    public RichTextBoxConsole(RichTextBox richTextBox)
    {
        _richTextBox = richTextBox;
    }

    private static readonly Dictionary<string, Color> ColorMap = new()
    {
        { "{Red}", Color.Red },
        { "{Blue}", Color.Blue },
        { "{Green}", Color.Green },
        { "{Yellow}", Color.Goldenrod },
        { "{Default}", Color.Black }
    };

    private static readonly Regex ColorTagRegex = GenerateColorTagRegex();

    public override Encoding Encoding => Encoding.UTF8;

    private static Regex GenerateColorTagRegex()
    {
        var tags = string.Join("|", ColorMap.Keys);

        return new Regex(@"(" + tags + @")+", RegexOptions.Compiled);
    }

    public override void Write(string? value)
    {
        if (_richTextBox.InvokeRequired)
        {
            _richTextBox.Invoke(new Action<string>(Write), value);
        }
        else
        {
            ProcessText(value ?? string.Empty);
        }
    }

    public override void Write(char value)
    {
        Write(value.ToString());
    }

    private void ProcessText(string value)
    {
        _richTextBox.SuspendLayout();

        try
        {
            var matches = ColorTagRegex.Matches(value);
            var lastIndex = 0;
            var currentColor = ColorMap["{Default}"];

            foreach (Match match in matches)
            {
                if (match.Index > lastIndex)
                {
                    AppendColoredText(value.Substring(lastIndex, match.Index - lastIndex), currentColor);
                }

                if (ColorMap.TryGetValue(match.Value, out var newColor))
                {
                    currentColor = newColor;
                }

                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < value.Length)
            {
                AppendColoredText(value[lastIndex..], currentColor);
            }
        }
        finally
        {
            _richTextBox.ResumeLayout();
            ScrollToEnd();
        }
    }

    private void AppendColoredText(string text, Color color)
    {
        if (string.IsNullOrEmpty(text)) return;

        var start = _richTextBox.TextLength;

        _richTextBox.AppendText(text);
        _richTextBox.Select(start, text.Length);
        _richTextBox.SelectionColor = color;
        _richTextBox.SelectionLength = 0;
    }

    private void ScrollToEnd()
    {
        _richTextBox.SelectionStart = _richTextBox.Text.Length;
        _richTextBox.ScrollToCaret();
    }
}
