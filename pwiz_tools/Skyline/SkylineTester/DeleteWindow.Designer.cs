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
            this.buttonCancel.Location = new System.Drawing.Point(455, 22);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(75, 23);
            this.buttonCancel.TabIndex = 4;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            this.buttonCancel.Click += new System.EventHandler(this.buttonCancel_Click);
            // 
            // progressBarDelete
            // 
            this.progressBarDelete.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBarDelete.Location = new System.Drawing.Point(10, 11);
            this.progressBarDelete.Margin = new System.Windows.Forms.Padding(2);
            this.progressBarDelete.Name = "progressBarDelete";
            this.progressBarDelete.Size = new System.Drawing.Size(520, 20);
            this.progressBarDelete.TabIndex = 5;
            // 
            // labelDeletingFile
            // 
            this.labelDeletingFile.AutoEllipsis = true;
            this.labelDeletingFile.Location = new System.Drawing.Point(10, 44);
            this.labelDeletingFile.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelDeletingFile.Name = "labelDeletingFile";
            this.labelDeletingFile.Size = new System.Drawing.Size(440, 23);
            this.labelDeletingFile.TabIndex = 6;
            this.labelDeletingFile.Text = "Deleting filename";
            this.labelDeletingFile.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // DeleteWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.ClientSize = new System.Drawing.Size(539, 91);
            this.ControlBox = false;
            this.Controls.Add(this.labelDeletingFile);
            this.Controls.Add(this.progressBarDelete);
            this.Controls.Add(this.buttonCancel);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(2);
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(754, 129);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(192, 129);
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