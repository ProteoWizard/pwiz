namespace pwiz.Skyline.SettingsUI
{
    partial class EditExclusionDlg
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
            this.components = new System.ComponentModel.Container();
            this.textName = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.textExclusionRegex = new System.Windows.Forms.TextBox();
            this.labelRegex = new System.Windows.Forms.Label();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.linkRegex = new System.Windows.Forms.LinkLabel();
            this.label2 = new System.Windows.Forms.Label();
            this.radioSequence = new System.Windows.Forms.RadioButton();
            this.radioModSequence = new System.Windows.Forms.RadioButton();
            this.radioMatching = new System.Windows.Forms.RadioButton();
            this.radioNotMatching = new System.Windows.Forms.RadioButton();
            this.label1 = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel2 = new System.Windows.Forms.Panel();
            this.panel1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // textName
            // 
            this.textName.Location = new System.Drawing.Point(9, 31);
            this.textName.Name = "textName";
            this.textName.Size = new System.Drawing.Size(160, 20);
            this.textName.TabIndex = 1;
            this.helpTip.SetToolTip(this.textName, "Name of the exclusion as it will appear in the exclusion list");
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(6, 14);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(38, 13);
            this.label4.TabIndex = 0;
            this.label4.Text = "&Name:";
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(190, 44);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 8;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Location = new System.Drawing.Point(190, 14);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 7;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // textExclusionRegex
            // 
            this.textExclusionRegex.Location = new System.Drawing.Point(9, 89);
            this.textExclusionRegex.Name = "textExclusionRegex";
            this.textExclusionRegex.Size = new System.Drawing.Size(160, 20);
            this.textExclusionRegex.TabIndex = 4;
            this.helpTip.SetToolTip(this.textExclusionRegex, "Exclude peptides matching this regular expression");
            // 
            // labelRegex
            // 
            this.labelRegex.AutoSize = true;
            this.labelRegex.Location = new System.Drawing.Point(6, 72);
            this.labelRegex.Name = "labelRegex";
            this.labelRegex.Size = new System.Drawing.Size(52, 13);
            this.labelRegex.TabIndex = 2;
            this.labelRegex.Text = "&Exclusion";
            // 
            // linkRegex
            // 
            this.linkRegex.AutoSize = true;
            this.linkRegex.Location = new System.Drawing.Point(54, 72);
            this.linkRegex.Name = "linkRegex";
            this.linkRegex.Size = new System.Drawing.Size(95, 13);
            this.linkRegex.TabIndex = 3;
            this.linkRegex.TabStop = true;
            this.linkRegex.Text = "regular expression:";
            this.linkRegex.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkRegex_LinkClicked);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(3, 3);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(91, 13);
            this.label2.TabIndex = 8;
            this.label2.Text = "E&xclude peptides:";
            // 
            // radioSequence
            // 
            this.radioSequence.AutoSize = true;
            this.radioSequence.Location = new System.Drawing.Point(11, 23);
            this.radioSequence.Name = "radioSequence";
            this.radioSequence.Size = new System.Drawing.Size(127, 17);
            this.radioSequence.TabIndex = 6;
            this.radioSequence.TabStop = true;
            this.radioSequence.Text = "&Amino acid sequence";
            this.helpTip.SetToolTip(this.radioSequence, "Match a plain amino acid sequence like PEPMICER\r\nregardless of modifications.");
            this.radioSequence.UseVisualStyleBackColor = true;
            // 
            // radioModSequence
            // 
            this.radioModSequence.AutoSize = true;
            this.radioModSequence.Location = new System.Drawing.Point(11, 44);
            this.radioModSequence.Name = "radioModSequence";
            this.radioModSequence.Size = new System.Drawing.Size(140, 17);
            this.radioModSequence.TabIndex = 7;
            this.radioModSequence.TabStop = true;
            this.radioModSequence.Text = "&Light modified sequence";
            this.helpTip.SetToolTip(this.radioModSequence, "Match a light modified peptide sequence like\r\nPEPM[+12]IC[+58]ER");
            this.radioModSequence.UseVisualStyleBackColor = true;
            // 
            // radioMatching
            // 
            this.radioMatching.AutoSize = true;
            this.radioMatching.Location = new System.Drawing.Point(11, 20);
            this.radioMatching.Name = "radioMatching";
            this.radioMatching.Size = new System.Drawing.Size(122, 17);
            this.radioMatching.TabIndex = 9;
            this.radioMatching.TabStop = true;
            this.radioMatching.Text = "&Matching expression";
            this.radioMatching.UseVisualStyleBackColor = true;
            // 
            // radioNotMatching
            // 
            this.radioNotMatching.AutoSize = true;
            this.radioNotMatching.Location = new System.Drawing.Point(11, 43);
            this.radioNotMatching.Name = "radioNotMatching";
            this.radioNotMatching.Size = new System.Drawing.Size(141, 17);
            this.radioNotMatching.TabIndex = 10;
            this.radioNotMatching.TabStop = true;
            this.radioNotMatching.Text = "N&ot matching expression";
            this.radioNotMatching.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(4, 6);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(80, 13);
            this.label1.TabIndex = 5;
            this.label1.Text = "&Apply match to:";
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.radioModSequence);
            this.panel1.Controls.Add(this.radioSequence);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Location = new System.Drawing.Point(2, 124);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(273, 67);
            this.panel1.TabIndex = 5;
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.radioNotMatching);
            this.panel2.Controls.Add(this.radioMatching);
            this.panel2.Controls.Add(this.label2);
            this.panel2.Location = new System.Drawing.Point(3, 202);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(271, 67);
            this.panel2.TabIndex = 6;
            // 
            // EditExclusionDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(277, 281);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.linkRegex);
            this.Controls.Add(this.textName);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.textExclusionRegex);
            this.Controls.Add(this.labelRegex);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditExclusionDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit Exclusion";
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.TextBox textExclusionRegex;
        private System.Windows.Forms.Label labelRegex;
        private System.Windows.Forms.ToolTip helpTip;
        private System.Windows.Forms.LinkLabel linkRegex;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.RadioButton radioSequence;
        private System.Windows.Forms.RadioButton radioModSequence;
        private System.Windows.Forms.RadioButton radioMatching;
        private System.Windows.Forms.RadioButton radioNotMatching;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Panel panel2;
    }
}