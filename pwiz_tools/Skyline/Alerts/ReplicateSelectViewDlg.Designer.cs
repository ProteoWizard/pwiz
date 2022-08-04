namespace pwiz.Skyline.Alerts
{
    partial class ShareResultsFilesDlg
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
            this.label1 = new System.Windows.Forms.Label();
            this.Btn_Accept = new System.Windows.Forms.Button();
            this.Btn_Cancel = new System.Windows.Forms.Button();
            this.checkedListBox = new System.Windows.Forms.CheckedListBox();
            this.checkboxSelectAll = new System.Windows.Forms.CheckBox();
            this.checkedStatus = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(80, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Files to include:";
            // 
            // Btn_Accept
            // 
            this.Btn_Accept.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.Btn_Accept.Location = new System.Drawing.Point(563, 433);
            this.Btn_Accept.Name = "Btn_Accept";
            this.Btn_Accept.Size = new System.Drawing.Size(75, 23);
            this.Btn_Accept.TabIndex = 2;
            this.Btn_Accept.Text = "Ok";
            this.Btn_Accept.UseVisualStyleBackColor = true;
            this.Btn_Accept.Click += new System.EventHandler(this.Btn_Accept_Click);
            // 
            // Btn_Cancel
            // 
            this.Btn_Cancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.Btn_Cancel.Location = new System.Drawing.Point(644, 433);
            this.Btn_Cancel.Name = "Btn_Cancel";
            this.Btn_Cancel.Size = new System.Drawing.Size(75, 23);
            this.Btn_Cancel.TabIndex = 3;
            this.Btn_Cancel.Text = "Cancel";
            this.Btn_Cancel.UseVisualStyleBackColor = true;
            this.Btn_Cancel.Click += new System.EventHandler(this.Btn_Cancel_Click);
            // 
            // checkedListBox
            // 
            this.checkedListBox.CheckOnClick = true;
            this.checkedListBox.FormattingEnabled = true;
            this.checkedListBox.Location = new System.Drawing.Point(15, 25);
            this.checkedListBox.Name = "checkedListBox";
            this.checkedListBox.Size = new System.Drawing.Size(704, 394);
            this.checkedListBox.TabIndex = 4;
            this.checkedListBox.SelectedIndexChanged += new System.EventHandler(this.checkedListBoxResults_SelectIndexChanged);
            // 
            // checkboxSelectAll
            // 
            this.checkboxSelectAll.AutoSize = true;
            this.checkboxSelectAll.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.checkboxSelectAll.Location = new System.Drawing.Point(15, 425);
            this.checkboxSelectAll.Name = "checkboxSelectAll";
            this.checkboxSelectAll.Size = new System.Drawing.Size(120, 17);
            this.checkboxSelectAll.TabIndex = 15;
            this.checkboxSelectAll.Text = "Select / deselect &all";
            this.checkboxSelectAll.UseVisualStyleBackColor = true;
            this.checkboxSelectAll.CheckedChanged += new System.EventHandler(this.checkboxSelectAll_CheckedChanged);
            // 
            // checkedStatus
            // 
            this.checkedStatus.AutoSize = true;
            this.checkedStatus.Location = new System.Drawing.Point(141, 426);
            this.checkedStatus.Name = "checkedStatus";
            this.checkedStatus.Size = new System.Drawing.Size(0, 13);
            this.checkedStatus.TabIndex = 16;
            // 
            // ShareResultsFilesDlg
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(731, 468);
            this.Controls.Add(this.checkedStatus);
            this.Controls.Add(this.checkboxSelectAll);
            this.Controls.Add(this.checkedListBox);
            this.Controls.Add(this.Btn_Cancel);
            this.Controls.Add(this.Btn_Accept);
            this.Controls.Add(this.label1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ShareResultsFilesDlg";
            this.ShowInTaskbar = false;
            this.Text = "Share Results Files";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button Btn_Accept;
        private System.Windows.Forms.Button Btn_Cancel;
        private System.Windows.Forms.CheckedListBox checkedListBox;
        private System.Windows.Forms.CheckBox checkboxSelectAll;
        private System.Windows.Forms.Label checkedStatus;
    }
}