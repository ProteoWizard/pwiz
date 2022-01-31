namespace TestHarness
{
    partial class MainForm
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
            this.lblCsvFile = new System.Windows.Forms.Label();
            this.tbxCsvFile = new System.Windows.Forms.TextBox();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.btnGroupComparison = new System.Windows.Forms.Button();
            this.tbxOutput = new System.Windows.Forms.TextBox();
            this.lblOutput = new System.Windows.Forms.Label();
            this.btnQualityControl = new System.Windows.Forms.Button();
            this.btnDesignSampleSize = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lblCsvFile
            // 
            this.lblCsvFile.AutoSize = true;
            this.lblCsvFile.Location = new System.Drawing.Point(9, 9);
            this.lblCsvFile.Name = "lblCsvFile";
            this.lblCsvFile.Size = new System.Drawing.Size(50, 13);
            this.lblCsvFile.TabIndex = 0;
            this.lblCsvFile.Text = "CSV File:";
            // 
            // tbxCsvFile
            // 
            this.tbxCsvFile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbxCsvFile.Location = new System.Drawing.Point(12, 25);
            this.tbxCsvFile.Name = "tbxCsvFile";
            this.tbxCsvFile.Size = new System.Drawing.Size(461, 20);
            this.tbxCsvFile.TabIndex = 1;
            // 
            // btnBrowse
            // 
            this.btnBrowse.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowse.Location = new System.Drawing.Point(479, 25);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(75, 23);
            this.btnBrowse.TabIndex = 2;
            this.btnBrowse.Text = "Browse...";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // btnGroupComparison
            // 
            this.btnGroupComparison.Location = new System.Drawing.Point(12, 68);
            this.btnGroupComparison.Name = "btnGroupComparison";
            this.btnGroupComparison.Size = new System.Drawing.Size(118, 23);
            this.btnGroupComparison.TabIndex = 3;
            this.btnGroupComparison.Text = "Group Comparison";
            this.btnGroupComparison.UseVisualStyleBackColor = true;
            this.btnGroupComparison.Click += new System.EventHandler(this.btnGroupComparison_Click);
            // 
            // tbxOutput
            // 
            this.tbxOutput.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbxOutput.Location = new System.Drawing.Point(12, 125);
            this.tbxOutput.Multiline = true;
            this.tbxOutput.Name = "tbxOutput";
            this.tbxOutput.Size = new System.Drawing.Size(542, 313);
            this.tbxOutput.TabIndex = 4;
            // 
            // lblOutput
            // 
            this.lblOutput.AutoSize = true;
            this.lblOutput.Location = new System.Drawing.Point(12, 109);
            this.lblOutput.Name = "lblOutput";
            this.lblOutput.Size = new System.Drawing.Size(42, 13);
            this.lblOutput.TabIndex = 5;
            this.lblOutput.Text = "Output:";
            // 
            // btnQualityControl
            // 
            this.btnQualityControl.Location = new System.Drawing.Point(148, 68);
            this.btnQualityControl.Name = "btnQualityControl";
            this.btnQualityControl.Size = new System.Drawing.Size(104, 23);
            this.btnQualityControl.TabIndex = 6;
            this.btnQualityControl.Text = "Quality Control";
            this.btnQualityControl.UseVisualStyleBackColor = true;
            this.btnQualityControl.Click += new System.EventHandler(this.btnQualityControl_Click);
            // 
            // btnDesignSampleSize
            // 
            this.btnDesignSampleSize.Location = new System.Drawing.Point(278, 68);
            this.btnDesignSampleSize.Name = "btnDesignSampleSize";
            this.btnDesignSampleSize.Size = new System.Drawing.Size(115, 23);
            this.btnDesignSampleSize.TabIndex = 7;
            this.btnDesignSampleSize.Text = "Design Sample Size";
            this.btnDesignSampleSize.UseVisualStyleBackColor = true;
            this.btnDesignSampleSize.Click += new System.EventHandler(this.btnDesignSampleSize_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(566, 450);
            this.Controls.Add(this.btnDesignSampleSize);
            this.Controls.Add(this.btnQualityControl);
            this.Controls.Add(this.lblOutput);
            this.Controls.Add(this.tbxOutput);
            this.Controls.Add(this.btnGroupComparison);
            this.Controls.Add(this.btnBrowse);
            this.Controls.Add(this.tbxCsvFile);
            this.Controls.Add(this.lblCsvFile);
            this.Name = "MainForm";
            this.Text = "MainForm";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblCsvFile;
        private System.Windows.Forms.TextBox tbxCsvFile;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Button btnGroupComparison;
        private System.Windows.Forms.TextBox tbxOutput;
        private System.Windows.Forms.Label lblOutput;
        private System.Windows.Forms.Button btnQualityControl;
        private System.Windows.Forms.Button btnDesignSampleSize;
    }
}