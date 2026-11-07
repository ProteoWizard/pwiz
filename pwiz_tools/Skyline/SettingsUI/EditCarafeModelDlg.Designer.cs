namespace pwiz.Skyline.SettingsUI
{
    partial class EditCarafeModelDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditCarafeModelDlg));
            this.labelName = new System.Windows.Forms.Label();
            this.textName = new System.Windows.Forms.TextBox();
            this.labelPath = new System.Windows.Forms.Label();
            this.textPath = new System.Windows.Forms.TextBox();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.labelDetails = new System.Windows.Forms.Label();
            this.textDetails = new System.Windows.Forms.TextBox();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // labelName
            //
            resources.ApplyResources(this.labelName, "labelName");
            this.labelName.Name = "labelName";
            //
            // textName
            //
            resources.ApplyResources(this.textName, "textName");
            this.textName.Name = "textName";
            //
            // labelPath
            //
            resources.ApplyResources(this.labelPath, "labelPath");
            this.labelPath.Name = "labelPath";
            //
            // textPath
            //
            resources.ApplyResources(this.textPath, "textPath");
            this.textPath.Name = "textPath";
            this.textPath.TextChanged += new System.EventHandler(this.textPath_TextChanged);
            //
            // btnBrowse
            //
            resources.ApplyResources(this.btnBrowse, "btnBrowse");
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            //
            // labelDetails
            //
            resources.ApplyResources(this.labelDetails, "labelDetails");
            this.labelDetails.Name = "labelDetails";
            //
            // textDetails
            //
            resources.ApplyResources(this.textDetails, "textDetails");
            this.textDetails.BackColor = System.Drawing.SystemColors.Window;
            this.textDetails.Name = "textDetails";
            this.textDetails.ReadOnly = true;
            //
            // btnOk
            //
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            //
            // btnCancel
            //
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            //
            // EditCarafeModelDlg
            //
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.textDetails);
            this.Controls.Add(this.labelDetails);
            this.Controls.Add(this.btnBrowse);
            this.Controls.Add(this.textPath);
            this.Controls.Add(this.labelPath);
            this.Controls.Add(this.textName);
            this.Controls.Add(this.labelName);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditCarafeModelDlg";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelName;
        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label labelPath;
        private System.Windows.Forms.TextBox textPath;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Label labelDetails;
        private System.Windows.Forms.TextBox textDetails;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
    }
}
