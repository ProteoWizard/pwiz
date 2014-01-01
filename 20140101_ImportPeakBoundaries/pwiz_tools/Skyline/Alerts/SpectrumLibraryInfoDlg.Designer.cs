namespace pwiz.Skyline.Alerts
{
    partial class SpectrumLibraryInfoDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SpectrumLibraryInfoDlg));
            this.labelLibInfo = new System.Windows.Forms.Label();
            this.linkSpecLibLinks = new System.Windows.Forms.LinkLabel();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.textBoxDataFiles = new System.Windows.Forms.TextBox();
            this.btnOk = new System.Windows.Forms.Button();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // labelLibInfo
            // 
            resources.ApplyResources(this.labelLibInfo, "labelLibInfo");
            this.labelLibInfo.Name = "labelLibInfo";
            // 
            // linkSpecLibLinks
            // 
            resources.ApplyResources(this.linkSpecLibLinks, "linkSpecLibLinks");
            this.linkSpecLibLinks.Name = "linkSpecLibLinks";
            this.linkSpecLibLinks.TabStop = true;
            this.linkSpecLibLinks.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(this.tableLayoutPanel1, "tableLayoutPanel1");
            this.tableLayoutPanel1.Controls.Add(this.labelLibInfo, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.textBoxDataFiles, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.linkSpecLibLinks, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.btnOk, 0, 3);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // textBoxDataFiles
            // 
            resources.ApplyResources(this.textBoxDataFiles, "textBoxDataFiles");
            this.textBoxDataFiles.BackColor = System.Drawing.SystemColors.Window;
            this.textBoxDataFiles.Name = "textBoxDataFiles";
            this.textBoxDataFiles.ReadOnly = true;
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            // 
            // SpectrumLibraryInfoDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnOk;
            this.Controls.Add(this.tableLayoutPanel1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SpectrumLibraryInfoDlg";
            this.ShowInTaskbar = false;
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelLibInfo;
        private System.Windows.Forms.LinkLabel linkSpecLibLinks;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TextBox textBoxDataFiles;
        private System.Windows.Forms.Button btnOk;
    }
}