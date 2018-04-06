namespace pwiz.Skyline.FileUI
{
    partial class ChooseIrtStandardPeptidesDlg
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
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.comboProteins = new System.Windows.Forms.ComboBox();
            this.radioProtein = new System.Windows.Forms.RadioButton();
            this.radioTransitionList = new System.Windows.Forms.RadioButton();
            this.txtTransitionList = new System.Windows.Forms.TextBox();
            this.btnBrowseTransitionList = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(116, 111);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 5;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(197, 111);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 6;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // comboProteins
            // 
            this.comboProteins.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.comboProteins.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboProteins.FormattingEnabled = true;
            this.comboProteins.Location = new System.Drawing.Point(12, 35);
            this.comboProteins.Name = "comboProteins";
            this.comboProteins.Size = new System.Drawing.Size(260, 21);
            this.comboProteins.TabIndex = 1;
            // 
            // radioProtein
            // 
            this.radioProtein.AutoSize = true;
            this.radioProtein.Checked = true;
            this.radioProtein.Location = new System.Drawing.Point(12, 12);
            this.radioProtein.Name = "radioProtein";
            this.radioProtein.Size = new System.Drawing.Size(90, 17);
            this.radioProtein.TabIndex = 0;
            this.radioProtein.TabStop = true;
            this.radioProtein.Text = "&Protein name:";
            this.radioProtein.UseVisualStyleBackColor = true;
            // 
            // radioTransitionList
            // 
            this.radioTransitionList.AutoSize = true;
            this.radioTransitionList.Location = new System.Drawing.Point(12, 62);
            this.radioTransitionList.Name = "radioTransitionList";
            this.radioTransitionList.Size = new System.Drawing.Size(131, 17);
            this.radioTransitionList.TabIndex = 2;
            this.radioTransitionList.Text = "&Separate transition list:";
            this.radioTransitionList.UseVisualStyleBackColor = true;
            this.radioTransitionList.CheckedChanged += new System.EventHandler(this.UpdateSelection);
            // 
            // txtTransitionList
            // 
            this.txtTransitionList.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtTransitionList.Location = new System.Drawing.Point(12, 85);
            this.txtTransitionList.Name = "txtTransitionList";
            this.txtTransitionList.Size = new System.Drawing.Size(179, 20);
            this.txtTransitionList.TabIndex = 3;
            // 
            // btnBrowseTransitionList
            // 
            this.btnBrowseTransitionList.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowseTransitionList.Location = new System.Drawing.Point(197, 83);
            this.btnBrowseTransitionList.Name = "btnBrowseTransitionList";
            this.btnBrowseTransitionList.Size = new System.Drawing.Size(75, 23);
            this.btnBrowseTransitionList.TabIndex = 4;
            this.btnBrowseTransitionList.Text = "&Browse...";
            this.btnBrowseTransitionList.UseVisualStyleBackColor = true;
            this.btnBrowseTransitionList.Click += new System.EventHandler(this.btnBrowseTransitionList_Click);
            // 
            // ChooseIrtStandardPeptides
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(284, 146);
            this.Controls.Add(this.btnBrowseTransitionList);
            this.Controls.Add(this.txtTransitionList);
            this.Controls.Add(this.radioTransitionList);
            this.Controls.Add(this.radioProtein);
            this.Controls.Add(this.comboProteins);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ChooseIrtStandardPeptides";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Choose iRT Standard Peptides";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.ComboBox comboProteins;
        private System.Windows.Forms.RadioButton radioProtein;
        private System.Windows.Forms.RadioButton radioTransitionList;
        private System.Windows.Forms.TextBox txtTransitionList;
        private System.Windows.Forms.Button btnBrowseTransitionList;
    }
}