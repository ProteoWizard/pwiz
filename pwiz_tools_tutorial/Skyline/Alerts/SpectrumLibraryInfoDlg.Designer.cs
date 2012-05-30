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
            this.labelLibInfo.AutoSize = true;
            this.labelLibInfo.Location = new System.Drawing.Point(3, 0);
            this.labelLibInfo.Name = "labelLibInfo";
            this.labelLibInfo.Size = new System.Drawing.Size(115, 13);
            this.labelLibInfo.TabIndex = 0;
            this.labelLibInfo.Text = "Spectrum library details";
            // 
            // linkSpecLibLinks
            // 
            this.linkSpecLibLinks.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.linkSpecLibLinks.AutoSize = true;
            this.linkSpecLibLinks.Location = new System.Drawing.Point(3, 71);
            this.linkSpecLibLinks.Name = "linkSpecLibLinks";
            this.linkSpecLibLinks.Size = new System.Drawing.Size(100, 13);
            this.linkSpecLibLinks.TabIndex = 1;
            this.linkSpecLibLinks.TabStop = true;
            this.linkSpecLibLinks.Text = "Spectral library links";
            this.linkSpecLibLinks.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel1.AutoSize = true;
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel1.Controls.Add(this.labelLibInfo, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.textBoxDataFiles, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.linkSpecLibLinks, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.btnOk, 0, 3);
            this.tableLayoutPanel1.Location = new System.Drawing.Point(21, 12);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 4;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 15F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(285, 119);
            this.tableLayoutPanel1.TabIndex = 5;
            // 
            // textBoxDataFiles
            // 
            this.textBoxDataFiles.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxDataFiles.BackColor = System.Drawing.SystemColors.Window;
            this.textBoxDataFiles.Location = new System.Drawing.Point(3, 16);
            this.textBoxDataFiles.Multiline = true;
            this.textBoxDataFiles.Name = "textBoxDataFiles";
            this.textBoxDataFiles.ReadOnly = true;
            this.textBoxDataFiles.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textBoxDataFiles.Size = new System.Drawing.Size(279, 50);
            this.textBoxDataFiles.TabIndex = 5;
            this.textBoxDataFiles.Visible = false;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnOk.Location = new System.Drawing.Point(105, 93);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 3;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            // 
            // SpectrumLibraryInfoDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnOk;
            this.ClientSize = new System.Drawing.Size(328, 137);
            this.Controls.Add(this.tableLayoutPanel1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SpectrumLibraryInfoDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Library Details";
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