namespace pwiz.Skyline.Controls.Startup
{
    partial class RecentFileControl
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
            this.labelFileName = new System.Windows.Forms.Label();
            this.labelFilePath = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // labelFileName
            // 
            this.labelFileName.AutoEllipsis = true;
            this.labelFileName.Font = new System.Drawing.Font("Arial", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelFileName.ForeColor = System.Drawing.Color.White;
            this.labelFileName.Location = new System.Drawing.Point(0, 5);
            this.labelFileName.Name = "labelFileName";
            this.labelFileName.Size = new System.Drawing.Size(225, 20);
            this.labelFileName.TabIndex = 0;
            this.labelFileName.Click += new System.EventHandler(this.ControlClick);
            this.labelFileName.MouseEnter += new System.EventHandler(this.ControlMouseEnter);
            this.labelFileName.MouseLeave += new System.EventHandler(this.ControlMouseLeave);
            // 
            // labelFilePath
            // 
            this.labelFilePath.AutoEllipsis = true;
            this.labelFilePath.Font = new System.Drawing.Font("Arial", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelFilePath.ForeColor = System.Drawing.Color.White;
            this.labelFilePath.Location = new System.Drawing.Point(0, 25);
            this.labelFilePath.Name = "labelFilePath";
            this.labelFilePath.Size = new System.Drawing.Size(225, 20);
            this.labelFilePath.TabIndex = 1;
            this.labelFilePath.Click += new System.EventHandler(this.ControlClick);
            this.labelFilePath.MouseEnter += new System.EventHandler(this.ControlMouseEnter);
            this.labelFilePath.MouseLeave += new System.EventHandler(this.ControlMouseLeave);
            // 
            // RecentFileControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Controls.Add(this.labelFilePath);
            this.Controls.Add(this.labelFileName);
            this.Font = new System.Drawing.Font("Arial", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "RecentFileControl";
            this.Size = new System.Drawing.Size(230, 45);
            this.Click += new System.EventHandler(this.ControlClick);
            this.MouseEnter += new System.EventHandler(this.ControlMouseEnter);
            this.MouseLeave += new System.EventHandler(this.ControlMouseLeave);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label labelFileName;
        private System.Windows.Forms.Label labelFilePath;

    }
}
