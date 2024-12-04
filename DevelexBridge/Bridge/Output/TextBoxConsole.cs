using System.Text;
using System.Text.RegularExpressions;

namespace Bridge.Output;

internal partial class RichTextBoxConsole(RichTextBox richTextBox) : TextWriter
{
    private readonly Dictionary<string, Color> _colorMap = new()
    {
        { "{Red}", Color.Red },
        { "{Blue}", Color.Blue },
        { "{Green}", Color.Green },
        { "{Yellow}", Color.Goldenrod },
        { "{Default}", Color.Black }
    };
    
    private static readonly Regex ColorTagRegex = ColorTagReplacementRegex();

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(string? value)
    {
        if (richTextBox.InvokeRequired)
        {
            richTextBox.Invoke(new Action<string>(Write), value);
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
        richTextBox.SuspendLayout();
        
        try 
        {
            var matches = ColorTagRegex.Matches(value);
            var lastIndex = 0;
            var currentColor = _colorMap["{Default}"];

            foreach (Match match in matches)
            {
                if (match.Index > lastIndex)
                {
                    AppendColoredText(value.Substring(lastIndex, match.Index - lastIndex), currentColor);
                }

                if (_colorMap.TryGetValue(match.Value, out var newColor))
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
            richTextBox.ResumeLayout();
        }
    }

    private void AppendColoredText(string text, Color color)
    {
        if (string.IsNullOrEmpty(text)) return;

        var start = richTextBox.TextLength;
        
        richTextBox.AppendText(text);
        richTextBox.Select(start, text.Length);
        richTextBox.SelectionColor = color;
        richTextBox.SelectionLength = 0;
    }

    [GeneratedRegex(@"\{[^{}]+\}", RegexOptions.Compiled)]
    private static partial Regex ColorTagReplacementRegex();
}
