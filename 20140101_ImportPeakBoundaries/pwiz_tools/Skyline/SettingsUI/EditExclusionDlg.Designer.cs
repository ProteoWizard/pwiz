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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditExclusionDlg));
            this.textName = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.textExclusionRegex = new System.Windows.Forms.TextBox();
            this.labelRegex = new System.Windows.Forms.Label();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.radioSequence = new System.Windows.Forms.RadioButton();
            this.radioModSequence = new System.Windows.Forms.RadioButton();
            this.linkRegex = new System.Windows.Forms.LinkLabel();
            this.label2 = new System.Windows.Forms.Label();
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
            resources.ApplyResources(this.textName, "textName");
            this.textName.Name = "textName";
            this.helpTip.SetToolTip(this.textName, resources.GetString("textName.ToolTip"));
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // textExclusionRegex
            // 
            resources.ApplyResources(this.textExclusionRegex, "textExclusionRegex");
            this.textExclusionRegex.Name = "textExclusionRegex";
            this.helpTip.SetToolTip(this.textExclusionRegex, resources.GetString("textExclusionRegex.ToolTip"));
            // 
            // labelRegex
            // 
            resources.ApplyResources(this.labelRegex, "labelRegex");
            this.labelRegex.Name = "labelRegex";
            // 
            // radioSequence
            // 
            resources.ApplyResources(this.radioSequence, "radioSequence");
            this.radioSequence.Name = "radioSequence";
            this.radioSequence.TabStop = true;
            this.helpTip.SetToolTip(this.radioSequence, resources.GetString("radioSequence.ToolTip"));
            this.radioSequence.UseVisualStyleBackColor = true;
            // 
            // radioModSequence
            // 
            resources.ApplyResources(this.radioModSequence, "radioModSequence");
            this.radioModSequence.Name = "radioModSequence";
            this.radioModSequence.TabStop = true;
            this.helpTip.SetToolTip(this.radioModSequence, resources.GetString("radioModSequence.ToolTip"));
            this.radioModSequence.UseVisualStyleBackColor = true;
            // 
            // linkRegex
            // 
            resources.ApplyResources(this.linkRegex, "linkRegex");
            this.linkRegex.Name = "linkRegex";
            this.linkRegex.TabStop = true;
            this.linkRegex.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkRegex_LinkClicked);
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // radioMatching
            // 
            resources.ApplyResources(this.radioMatching, "radioMatching");
            this.radioMatching.Name = "radioMatching";
            this.radioMatching.TabStop = true;
            this.radioMatching.UseVisualStyleBackColor = true;
            // 
            // radioNotMatching
            // 
            resources.ApplyResources(this.radioNotMatching, "radioNotMatching");
            this.radioNotMatching.Name = "radioNotMatching";
            this.radioNotMatching.TabStop = true;
            this.radioNotMatching.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.radioModSequence);
            this.panel1.Controls.Add(this.radioSequence);
            this.panel1.Controls.Add(this.label1);
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Name = "panel1";
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.radioNotMatching);
            this.panel2.Controls.Add(this.radioMatching);
            this.panel2.Controls.Add(this.label2);
            resources.ApplyResources(this.panel2, "panel2");
            this.panel2.Name = "panel2";
            // 
            // EditExclusionDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
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