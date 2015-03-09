namespace pwiz.Topograph.ui.Forms.Dashboard
{
    partial class RawFilesStep
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.lblDescription = new System.Windows.Forms.Label();
            this.lblStatus = new System.Windows.Forms.Label();
            this.panelButton = new System.Windows.Forms.Panel();
            this.btnChooseDirectory = new System.Windows.Forms.Button();
            this.panelButton.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblDescription
            // 
            this.lblDescription.AutoSize = true;
            this.lblDescription.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblDescription.Location = new System.Drawing.Point(0, 0);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.Size = new System.Drawing.Size(731, 13);
            this.lblDescription.TabIndex = 1;
            this.lblDescription.Text = "In order to generate chromatograms or allow you to view spectra, Topograph needs " +
                "to know where to find your MS data files (.RAW, .mzXML, or .mzML).  ";
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStatus.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblStatus.Location = new System.Drawing.Point(0, 13);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(199, 13);
            this.lblStatus.TabIndex = 2;
            this.lblStatus.Text = "The data files directory has been set to...";
            // 
            // panelButton
            // 
            this.panelButton.AutoSize = true;
            this.panelButton.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panelButton.Controls.Add(this.btnChooseDirectory);
            this.panelButton.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelButton.Location = new System.Drawing.Point(0, 26);
            this.panelButton.Name = "panelButton";
            this.panelButton.Size = new System.Drawing.Size(786, 26);
            this.panelButton.TabIndex = 3;
            // 
            // btnChooseDirectory
            // 
            this.btnChooseDirectory.AutoSize = true;
            this.btnChooseDirectory.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnChooseDirectory.Location = new System.Drawing.Point(0, 0);
            this.btnChooseDirectory.Name = "btnChooseDirectory";
            this.btnChooseDirectory.Size = new System.Drawing.Size(164, 23);
            this.btnChooseDirectory.TabIndex = 0;
            this.btnChooseDirectory.Text = "Browse for data files directory...";
            this.btnChooseDirectory.UseVisualStyleBackColor = true;
            this.btnChooseDirectory.Click += new System.EventHandler(this.BtnChooseDirectoryOnClick);
            // 
            // RawFilesStep
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.Controls.Add(this.panelButton);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.lblDescription);
            this.Name = "RawFilesStep";
            this.Size = new System.Drawing.Size(786, 150);
            this.panelButton.ResumeLayout(false);
            this.panelButton.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblDescription;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Panel panelButton;
        private System.Windows.Forms.Button btnChooseDirectory;
    }
}
