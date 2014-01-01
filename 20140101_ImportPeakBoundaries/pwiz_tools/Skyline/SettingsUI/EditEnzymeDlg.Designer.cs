namespace pwiz.Skyline.SettingsUI
{
    partial class EditEnzymeDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditEnzymeDlg));
            this.labelCleavage = new System.Windows.Forms.Label();
            this.textCleavage = new System.Windows.Forms.TextBox();
            this.labelRestrict = new System.Windows.Forms.Label();
            this.textRestrict = new System.Windows.Forms.TextBox();
            this.comboDirection = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.textName = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.textRestrictN = new System.Windows.Forms.TextBox();
            this.textCleavageN = new System.Windows.Forms.TextBox();
            this.labelRestrictN = new System.Windows.Forms.Label();
            this.labelCleavageN = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // labelCleavage
            // 
            resources.ApplyResources(this.labelCleavage, "labelCleavage");
            this.labelCleavage.Name = "labelCleavage";
            // 
            // textCleavage
            // 
            resources.ApplyResources(this.textCleavage, "textCleavage");
            this.textCleavage.Name = "textCleavage";
            this.helpTip.SetToolTip(this.textCleavage, resources.GetString("textCleavage.ToolTip"));
            // 
            // labelRestrict
            // 
            resources.ApplyResources(this.labelRestrict, "labelRestrict");
            this.labelRestrict.Name = "labelRestrict";
            // 
            // textRestrict
            // 
            resources.ApplyResources(this.textRestrict, "textRestrict");
            this.textRestrict.Name = "textRestrict";
            this.helpTip.SetToolTip(this.textRestrict, resources.GetString("textRestrict.ToolTip"));
            // 
            // comboDirection
            // 
            this.comboDirection.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboDirection.FormattingEnabled = true;
            this.comboDirection.Items.AddRange(new object[] {
            resources.GetString("comboDirection.Items"),
            resources.GetString("comboDirection.Items1"),
            resources.GetString("comboDirection.Items2")});
            resources.ApplyResources(this.comboDirection, "comboDirection");
            this.comboDirection.Name = "comboDirection";
            this.helpTip.SetToolTip(this.comboDirection, resources.GetString("comboDirection.ToolTip"));
            this.comboDirection.SelectedIndexChanged += new System.EventHandler(this.comboDirection_SelectedIndexChanged);
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
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
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
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
            // textRestrictN
            // 
            resources.ApplyResources(this.textRestrictN, "textRestrictN");
            this.textRestrictN.Name = "textRestrictN";
            this.helpTip.SetToolTip(this.textRestrictN, resources.GetString("textRestrictN.ToolTip"));
            // 
            // textCleavageN
            // 
            resources.ApplyResources(this.textCleavageN, "textCleavageN");
            this.textCleavageN.Name = "textCleavageN";
            this.helpTip.SetToolTip(this.textCleavageN, resources.GetString("textCleavageN.ToolTip"));
            // 
            // labelRestrictN
            // 
            resources.ApplyResources(this.labelRestrictN, "labelRestrictN");
            this.labelRestrictN.Name = "labelRestrictN";
            // 
            // labelCleavageN
            // 
            resources.ApplyResources(this.labelCleavageN, "labelCleavageN");
            this.labelCleavageN.Name = "labelCleavageN";
            // 
            // EditEnzymeDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.textRestrictN);
            this.Controls.Add(this.labelRestrictN);
            this.Controls.Add(this.textCleavageN);
            this.Controls.Add(this.labelCleavageN);
            this.Controls.Add(this.textName);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.comboDirection);
            this.Controls.Add(this.textRestrict);
            this.Controls.Add(this.labelRestrict);
            this.Controls.Add(this.textCleavage);
            this.Controls.Add(this.labelCleavage);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditEnzymeDlg";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelCleavage;
        private System.Windows.Forms.TextBox textCleavage;
        private System.Windows.Forms.Label labelRestrict;
        private System.Windows.Forms.TextBox textRestrict;
        private System.Windows.Forms.ComboBox comboDirection;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ToolTip helpTip;
        private System.Windows.Forms.TextBox textRestrictN;
        private System.Windows.Forms.Label labelRestrictN;
        private System.Windows.Forms.TextBox textCleavageN;
        private System.Windows.Forms.Label labelCleavageN;
    }
}