using Timer = System.Windows.Forms.Timer;

namespace Bridge
{
    partial class BridgeWindow
    {
        private System.ComponentModel.IContainer components = null;
        private readonly Color normalColor = Color.FromArgb(255, 39, 39, 42);
        private readonly Color hoverColor = Color.FromArgb(255, 63, 63, 70);

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();

            buttonStartStop = new Button();
            tbIpPortContainer = new CustomTextBoxContainer();
            tbConsoleContainer = new CustomTextBoxContainer();

            SuspendLayout();
            // 
            // buttonStartStop
            // 
            buttonStartStop.FlatStyle = FlatStyle.Flat;
            buttonStartStop.BackColor = normalColor;
            buttonStartStop.ForeColor = Color.White;
            buttonStartStop.FlatAppearance.BorderSize = 0;
            buttonStartStop.Cursor = Cursors.Hand;
            buttonStartStop.Location = new Point(470, 20);
            buttonStartStop.Name = "buttonStartStop";
            buttonStartStop.Size = new Size(135, 30);
            buttonStartStop.TabIndex = 0;
            buttonStartStop.Text = "Zapnout";
            buttonStartStop.UseVisualStyleBackColor = false;
            buttonStartStop.FlatAppearance.BorderSize = 0;
            buttonStartStop.Paint += buttonStartStop_Paint;

            buttonStartStop.MouseEnter += (_, _) => buttonStartStop.BackColor = hoverColor;
            buttonStartStop.MouseLeave += (_, _) => buttonStartStop.BackColor = normalColor;
            buttonStartStop.Click += startStopButton_Click;

            // 
            // tbIpPortContainer
            // 
            tbIpPortContainer.Location = new Point(20, 20);
            tbIpPortContainer.Name = "tbIpPortContainer";
            tbIpPortContainer.Size = new Size(440, 30);
            tbIpPortContainer.TabIndex = 1;
            tbIpPortContainer.BorderColor = Color.FromArgb(255, 75, 85, 99);
            tbIpPortContainer.CornerRadius = 5;
            tbIpPortContainer.InnerTextBox.Text = "localhost:13892";

            // 
            // tbConsoleContainer
            // 
            tbConsoleContainer.Location = new Point(20, 70);
            tbConsoleContainer.Name = "tbConsoleContainer";
            tbConsoleContainer.Size = new Size(585, 360);
            tbConsoleContainer.TabIndex = 2;
            tbConsoleContainer.BorderColor = Color.FromArgb(255, 75, 85, 99);
            tbConsoleContainer.CornerRadius = 5;
            tbConsoleContainer.InnerTextBox.Multiline = true;
            tbConsoleContainer.InnerTextBox.ReadOnly = true;
            tbConsoleContainer.InnerTextBox.ScrollBars = ScrollBars.Vertical;

            // 
            // BridgeWindow
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(255, 248, 250, 252);
            ClientSize = new Size(625, 450);
            Controls.Add(tbConsoleContainer);
            Controls.Add(tbIpPortContainer);
            Controls.Add(buttonStartStop);
            Name = "BridgeWindow";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Develex Bridge";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button buttonStartStop;
        private CustomTextBoxContainer tbIpPortContainer;
        private CustomTextBoxContainer tbConsoleContainer;

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

        private void buttonStartStop_Paint(object sender, PaintEventArgs e)
        {
            Button button = sender as Button;
            int radius = 10;
            using (var path = CreateRoundedRectangle(button.ClientRectangle, radius))
            {
                button.Region = new Region(path);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.FillPath(new SolidBrush(button.BackColor), path);
                e.Graphics.DrawPath(new Pen(button.ForeColor), path);

                TextRenderer.DrawText(e.Graphics, button.Text, button.Font, button.ClientRectangle, button.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }
}
