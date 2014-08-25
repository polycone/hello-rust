namespace HelloRust
{
    partial class Console
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.textBox = new System.Windows.Forms.TextBox();
            this.messagePump = new System.ComponentModel.BackgroundWorker();
            this.controlsPanel = new System.Windows.Forms.Panel();
            this.splitter1 = new System.Windows.Forms.Splitter();
            this.consolePanel = new System.Windows.Forms.Panel();
            this.consolePanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // textBox
            // 
            this.textBox.BackColor = System.Drawing.Color.White;
            this.textBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBox.Location = new System.Drawing.Point(3, 3);
            this.textBox.Multiline = true;
            this.textBox.Name = "textBox";
            this.textBox.ReadOnly = true;
            this.textBox.Size = new System.Drawing.Size(373, 338);
            this.textBox.TabIndex = 4;
            // 
            // messagePump
            // 
            this.messagePump.DoWork += new System.ComponentModel.DoWorkEventHandler(this.messagePump_DoWork);
            // 
            // controlsPanel
            // 
            this.controlsPanel.Dock = System.Windows.Forms.DockStyle.Left;
            this.controlsPanel.Location = new System.Drawing.Point(0, 0);
            this.controlsPanel.Name = "controlsPanel";
            this.controlsPanel.Size = new System.Drawing.Size(202, 344);
            this.controlsPanel.TabIndex = 5;
            // 
            // splitter1
            // 
            this.splitter1.BackColor = System.Drawing.Color.LightCoral;
            this.splitter1.Location = new System.Drawing.Point(202, 0);
            this.splitter1.Name = "splitter1";
            this.splitter1.Size = new System.Drawing.Size(3, 344);
            this.splitter1.TabIndex = 6;
            this.splitter1.TabStop = false;
            // 
            // consolePanel
            // 
            this.consolePanel.BackColor = System.Drawing.Color.White;
            this.consolePanel.Controls.Add(this.textBox);
            this.consolePanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.consolePanel.Location = new System.Drawing.Point(205, 0);
            this.consolePanel.Name = "consolePanel";
            this.consolePanel.Padding = new System.Windows.Forms.Padding(3);
            this.consolePanel.Size = new System.Drawing.Size(379, 344);
            this.consolePanel.TabIndex = 7;
            // 
            // Console
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 344);
            this.Controls.Add(this.consolePanel);
            this.Controls.Add(this.splitter1);
            this.Controls.Add(this.controlsPanel);
            this.Name = "Console";
            this.Text = "Console";
            this.consolePanel.ResumeLayout(false);
            this.consolePanel.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TextBox textBox;
        private System.ComponentModel.BackgroundWorker messagePump;
        private System.Windows.Forms.Panel controlsPanel;
        private System.Windows.Forms.Splitter splitter1;
        private System.Windows.Forms.Panel consolePanel;
    }
}