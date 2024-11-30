namespace Bridge;

using System.Drawing;
using System.Windows.Forms;

public class CustomTextBoxContainer : UserControl
{
    private readonly TextBox _innerTextBox;

    public Color BorderColor { get; set; } = Color.FromArgb(55, 65, 81);
    public int BorderSize { get; set; } = 1;
    public int CornerRadius { get; set; } = 5;

    public TextBox InnerTextBox => _innerTextBox;

    public CustomTextBoxContainer()
    {
        _innerTextBox = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(31, 41, 55),
            Font = new Font("Segoe UI", 10F),
            Location = new Point(8, 5),
            Multiline = false
        };

        Controls.Add(_innerTextBox);
        Padding = new Padding(8, 5, 8, 5);
        BackColor = Color.White;
        Size = new Size(200, 30);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var borderRect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var pen = new Pen(BorderColor, BorderSize);
        
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        if (CornerRadius > 0)
        {
            var path = CreateRoundedRectangle(borderRect, CornerRadius);
            e.Graphics.DrawPath(pen, path);
        }
        else
        {
            e.Graphics.DrawRectangle(pen, borderRect);
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        AdjustInnerTextBoxSize();
    }

    private void AdjustInnerTextBoxSize()
    {
        _innerTextBox.Size = new Size(Width - Padding.Left - Padding.Right, Height - Padding.Top - Padding.Bottom);
    }

    private System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        
        path.AddArc(bounds.X, bounds.Y, radius, radius, 180, 90);
        path.AddArc(bounds.Right - radius, bounds.Y, radius, radius, 270, 90);
        path.AddArc(bounds.Right - radius, bounds.Bottom - radius, radius, radius, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - radius, radius, radius, 90, 90);
        path.CloseFigure();
        
        return path;
    }
}
