namespace SkylineTester
{
    partial class DeleteWindow
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DeleteWindow));
            this.buttonCancel = new System.Windows.Forms.Button();
            this.progressBarDelete = new System.Windows.Forms.ProgressBar();
            this.labelDeletingFile = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // buttonCancel
            // 
            this.buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonCancel.Location = new System.Drawing.Point(606, 27);
            this.buttonCancel.Margin = new System.Windows.Forms.Padding(4);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(100, 28);
            this.buttonCancel.TabIndex = 4;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            this.buttonCancel.Click += new System.EventHandler(this.buttonCancel_Click);
            // 
            // progressBarDelete
            // 
            this.progressBarDelete.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBarDelete.Location = new System.Drawing.Point(13, 13);
            this.progressBarDelete.Name = "progressBarDelete";
            this.progressBarDelete.Size = new System.Drawing.Size(693, 25);
            this.progressBarDelete.TabIndex = 5;
            // 
            // labelDeletingFile
            // 
            this.labelDeletingFile.AutoEllipsis = true;
            this.labelDeletingFile.Location = new System.Drawing.Point(13, 54);
            this.labelDeletingFile.Name = "labelDeletingFile";
            this.labelDeletingFile.Size = new System.Drawing.Size(586, 28);
            this.labelDeletingFile.TabIndex = 6;
            this.labelDeletingFile.Text = "Deleting filename";
            this.labelDeletingFile.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // DeleteWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.ClientSize = new System.Drawing.Size(719, 105);
            this.ControlBox = false;
            this.Controls.Add(this.labelDeletingFile);
            this.Controls.Add(this.progressBarDelete);
            this.Controls.Add(this.buttonCancel);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(1000, 150);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(250, 150);
            this.Name = "DeleteWindow";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Deleting...";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.ProgressBar progressBarDelete;
        private System.Windows.Forms.Label labelDeletingFile;

    }
}