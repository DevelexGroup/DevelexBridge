namespace Bridge;

partial class BridgeWindow
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        buttonStartStop = new Button();
        tbIpPort = new TextBox();
        tbConsole = new TextBox();
        SuspendLayout();
        // 
        // buttonStartStop
        // 
        buttonStartStop.Location = new Point(450, 11);
        buttonStartStop.Name = "buttonStartStop";
        buttonStartStop.Size = new Size(135, 24);
        buttonStartStop.TabIndex = 0;
        buttonStartStop.Text = "Zapnout";
        buttonStartStop.UseVisualStyleBackColor = true;
        buttonStartStop.Click += startStopButton_Click;
        // 
        // tbIpPort
        // 
        tbIpPort.BorderStyle = BorderStyle.FixedSingle;
        tbIpPort.Location = new Point(12, 12);
        tbIpPort.Name = "tbIpPort";
        tbIpPort.Text = "localhost:13892";
        tbIpPort.Size = new Size(432, 23);
        tbIpPort.TabIndex = 1;
        // 
        // tbConsole
        // 
        tbConsole.BackColor = Color.FromArgb(212, 212, 216);
        tbConsole.BorderStyle = BorderStyle.FixedSingle;
        tbConsole.Location = new Point(12, 41);
        tbConsole.Multiline = true;
        tbConsole.Name = "tbConsole";
        tbConsole.ReadOnly = true;
        tbConsole.Size = new Size(573, 340);
        tbConsole.TabIndex = 2;
        // 
        // Form1
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(226, 232, 240);
        ClientSize = new Size(597, 393);
        Controls.Add(tbConsole);
        Controls.Add(tbIpPort);
        Controls.Add(buttonStartStop);
        Name = "BridgeWindow";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Form1";
        Load += Form1_Load;
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Button buttonStartStop;
    private TextBox tbIpPort;
    private TextBox tbConsole;
}