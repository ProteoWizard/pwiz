namespace pwiz.Skyline.Alerts
{
    partial class DiagnosticsWindow
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
            this.tbxOutput = new System.Windows.Forms.TextBox();
            this.btnMemoryUsage = new System.Windows.Forms.Button();
            this.btnGC = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // tbxOutput
            // 
            this.tbxOutput.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbxOutput.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.tbxOutput.Location = new System.Drawing.Point(20, 151);
            this.tbxOutput.Multiline = true;
            this.tbxOutput.Name = "tbxOutput";
            this.tbxOutput.ReadOnly = true;
            this.tbxOutput.Size = new System.Drawing.Size(768, 287);
            this.tbxOutput.TabIndex = 0;
            // 
            // btnMemoryUsage
            // 
            this.btnMemoryUsage.Location = new System.Drawing.Point(20, 12);
            this.btnMemoryUsage.Name = "btnMemoryUsage";
            this.btnMemoryUsage.Size = new System.Drawing.Size(115, 23);
            this.btnMemoryUsage.TabIndex = 1;
            this.btnMemoryUsage.Text = "Memory Usage";
            this.btnMemoryUsage.UseVisualStyleBackColor = true;
            this.btnMemoryUsage.Click += new System.EventHandler(this.btnMemoryUsage_Click);
            // 
            // btnGC
            // 
            this.btnGC.Location = new System.Drawing.Point(25, 46);
            this.btnGC.Name = "btnGC";
            this.btnGC.Size = new System.Drawing.Size(110, 23);
            this.btnGC.TabIndex = 2;
            this.btnGC.Text = "Garbage Collect";
            this.btnGC.UseVisualStyleBackColor = true;
            this.btnGC.Click += new System.EventHandler(this.btnGC_Click);
            // 
            // DiagnosticsWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.btnGC);
            this.Controls.Add(this.btnMemoryUsage);
            this.Controls.Add(this.tbxOutput);
            this.Name = "DiagnosticsWindow";
            this.Text = "DiagnosticsWindow";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox tbxOutput;
        private System.Windows.Forms.Button btnMemoryUsage;
        private System.Windows.Forms.Button btnGC;
    }
}