namespace pwiz.Skyline.FileUI.PeptideSearch
{
    partial class DDASearchControl
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
            this.txtSearchProgress = new System.Windows.Forms.TextBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.lblProgress = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // txtSearchProgress
            // 
            this.txtSearchProgress.AcceptsReturn = true;
            this.txtSearchProgress.Location = new System.Drawing.Point(12, 38);
            this.txtSearchProgress.Multiline = true;
            this.txtSearchProgress.Name = "txtSearchProgress";
            this.txtSearchProgress.ReadOnly = true;
            this.txtSearchProgress.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtSearchProgress.Size = new System.Drawing.Size(354, 373);
            this.txtSearchProgress.TabIndex = 0;
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(13, 418);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(233, 23);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "Cancel search";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // lblProgress
            // 
            this.lblProgress.AutoSize = true;
            this.lblProgress.Location = new System.Drawing.Point(12, 13);
            this.lblProgress.Name = "lblProgress";
            this.lblProgress.Size = new System.Drawing.Size(87, 13);
            this.lblProgress.TabIndex = 3;
            this.lblProgress.Text = "Search progress:";
            // 
            // DDASearchControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.lblProgress);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.txtSearchProgress);
            this.Name = "DDASearchControl";
            this.Size = new System.Drawing.Size(381, 450);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtSearchProgress;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label lblProgress;
    }
}