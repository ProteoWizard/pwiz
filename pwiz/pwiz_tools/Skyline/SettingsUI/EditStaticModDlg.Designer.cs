namespace pwiz.Skyline.SettingsUI
{
    partial class EditStaticModDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditStaticModDlg));
            this.textName = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.labelAA = new System.Windows.Forms.Label();
            this.comboAA = new System.Windows.Forms.ComboBox();
            this.comboTerm = new System.Windows.Forms.ComboBox();
            this.label6 = new System.Windows.Forms.Label();
            this.panelAtoms = new System.Windows.Forms.Panel();
            this.cb2H = new System.Windows.Forms.CheckBox();
            this.cb18O = new System.Windows.Forms.CheckBox();
            this.cb15N = new System.Windows.Forms.CheckBox();
            this.cb13C = new System.Windows.Forms.CheckBox();
            this.cbChemicalFormula = new System.Windows.Forms.CheckBox();
            this.btnLoss = new System.Windows.Forms.Button();
            this.comboRelativeRT = new System.Windows.Forms.ComboBox();
            this.labelRelativeRT = new System.Windows.Forms.Label();
            this.cbVariableMod = new System.Windows.Forms.CheckBox();
            this.listNeutralLosses = new System.Windows.Forms.ListBox();
            this.labelLoss = new System.Windows.Forms.Label();
            this.panelLoss = new System.Windows.Forms.Panel();
            this.toolBarLosses = new System.Windows.Forms.ToolStrip();
            this.tbbAddLoss = new System.Windows.Forms.ToolStripButton();
            this.tbbEditLoss = new System.Windows.Forms.ToolStripButton();
            this.tbbDeleteLoss = new System.Windows.Forms.ToolStripButton();
            this.comboMod = new System.Windows.Forms.ComboBox();
            this.panelAtoms.SuspendLayout();
            this.panelLoss.SuspendLayout();
            this.toolBarLosses.SuspendLayout();
            this.SuspendLayout();
            // 
            // textName
            // 
            resources.ApplyResources(this.textName, "textName");
            this.textName.Name = "textName";
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
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
            // labelAA
            // 
            resources.ApplyResources(this.labelAA, "labelAA");
            this.labelAA.Name = "labelAA";
            // 
            // comboAA
            // 
            this.comboAA.FormattingEnabled = true;
            resources.ApplyResources(this.comboAA, "comboAA");
            this.comboAA.Name = "comboAA";
            this.comboAA.SelectedIndexChanged += new System.EventHandler(this.comboAA_SelectedIndexChanged);
            this.comboAA.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.comboAA_KeyPress);
            this.comboAA.Leave += new System.EventHandler(this.comboAA_Leave);
            // 
            // comboTerm
            // 
            resources.ApplyResources(this.comboTerm, "comboTerm");
            this.comboTerm.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboTerm.FormattingEnabled = true;
            this.comboTerm.Name = "comboTerm";
            // 
            // label6
            // 
            resources.ApplyResources(this.label6, "label6");
            this.label6.Name = "label6";
            // 
            // panelAtoms
            // 
            this.panelAtoms.Controls.Add(this.cb2H);
            this.panelAtoms.Controls.Add(this.cb18O);
            this.panelAtoms.Controls.Add(this.cb15N);
            this.panelAtoms.Controls.Add(this.cb13C);
            resources.ApplyResources(this.panelAtoms, "panelAtoms");
            this.panelAtoms.Name = "panelAtoms";
            // 
            // cb2H
            // 
            resources.ApplyResources(this.cb2H, "cb2H");
            this.cb2H.Name = "cb2H";
            this.cb2H.UseVisualStyleBackColor = true;
            this.cb2H.CheckedChanged += new System.EventHandler(this.cb2H_CheckedChanged);
            // 
            // cb18O
            // 
            resources.ApplyResources(this.cb18O, "cb18O");
            this.cb18O.Name = "cb18O";
            this.cb18O.UseVisualStyleBackColor = true;
            this.cb18O.CheckedChanged += new System.EventHandler(this.cb18O_CheckedChanged);
            // 
            // cb15N
            // 
            resources.ApplyResources(this.cb15N, "cb15N");
            this.cb15N.Name = "cb15N";
            this.cb15N.UseVisualStyleBackColor = true;
            this.cb15N.CheckedChanged += new System.EventHandler(this.cb15N_CheckedChanged);
            // 
            // cb13C
            // 
            resources.ApplyResources(this.cb13C, "cb13C");
            this.cb13C.Name = "cb13C";
            this.cb13C.UseVisualStyleBackColor = true;
            this.cb13C.CheckedChanged += new System.EventHandler(this.cb13C_CheckedChanged);
            // 
            // cbChemicalFormula
            // 
            resources.ApplyResources(this.cbChemicalFormula, "cbChemicalFormula");
            this.cbChemicalFormula.Name = "cbChemicalFormula";
            this.cbChemicalFormula.UseVisualStyleBackColor = true;
            this.cbChemicalFormula.CheckedChanged += new System.EventHandler(this.cbChemicalFormula_CheckedChanged);
            // 
            // btnLoss
            // 
            resources.ApplyResources(this.btnLoss, "btnLoss");
            this.btnLoss.Name = "btnLoss";
            this.btnLoss.UseVisualStyleBackColor = true;
            this.btnLoss.Click += new System.EventHandler(this.btnLoss_Click);
            // 
            // comboRelativeRT
            // 
            this.comboRelativeRT.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRelativeRT.FormattingEnabled = true;
            resources.ApplyResources(this.comboRelativeRT, "comboRelativeRT");
            this.comboRelativeRT.Name = "comboRelativeRT";
            // 
            // labelRelativeRT
            // 
            resources.ApplyResources(this.labelRelativeRT, "labelRelativeRT");
            this.labelRelativeRT.Name = "labelRelativeRT";
            // 
            // cbVariableMod
            // 
            resources.ApplyResources(this.cbVariableMod, "cbVariableMod");
            this.cbVariableMod.Name = "cbVariableMod";
            this.cbVariableMod.UseVisualStyleBackColor = true;
            // 
            // listNeutralLosses
            // 
            resources.ApplyResources(this.listNeutralLosses, "listNeutralLosses");
            this.listNeutralLosses.FormattingEnabled = true;
            this.listNeutralLosses.Name = "listNeutralLosses";
            this.listNeutralLosses.SelectedIndexChanged += new System.EventHandler(this.listNeutralLosses_SelectedIndexChanged);
            // 
            // labelLoss
            // 
            resources.ApplyResources(this.labelLoss, "labelLoss");
            this.labelLoss.Name = "labelLoss";
            // 
            // panelLoss
            // 
            this.panelLoss.Controls.Add(this.listNeutralLosses);
            this.panelLoss.Controls.Add(this.toolBarLosses);
            resources.ApplyResources(this.panelLoss, "panelLoss");
            this.panelLoss.Name = "panelLoss";
            // 
            // toolBarLosses
            // 
            resources.ApplyResources(this.toolBarLosses, "toolBarLosses");
            this.toolBarLosses.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolBarLosses.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tbbAddLoss,
            this.tbbEditLoss,
            this.tbbDeleteLoss});
            this.toolBarLosses.Name = "toolBarLosses";
            // 
            // tbbAddLoss
            // 
            this.tbbAddLoss.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.tbbAddLoss.Image = global::pwiz.Skyline.Properties.Resources.add_pro32;
            resources.ApplyResources(this.tbbAddLoss, "tbbAddLoss");
            this.tbbAddLoss.Name = "tbbAddLoss";
            this.tbbAddLoss.Click += new System.EventHandler(this.tbbAddLoss_Click);
            // 
            // tbbEditLoss
            // 
            this.tbbEditLoss.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.tbbEditLoss, "tbbEditLoss");
            this.tbbEditLoss.Image = global::pwiz.Skyline.Properties.Resources.Comment;
            this.tbbEditLoss.Name = "tbbEditLoss";
            this.tbbEditLoss.Click += new System.EventHandler(this.tbbEditLoss_Click);
            // 
            // tbbDeleteLoss
            // 
            this.tbbDeleteLoss.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.tbbDeleteLoss, "tbbDeleteLoss");
            this.tbbDeleteLoss.Image = global::pwiz.Skyline.Properties.Resources.Delete;
            this.tbbDeleteLoss.Name = "tbbDeleteLoss";
            this.tbbDeleteLoss.Click += new System.EventHandler(this.tbbDeleteLoss_Click);
            // 
            // comboMod
            // 
            this.comboMod.FormattingEnabled = true;
            resources.ApplyResources(this.comboMod, "comboMod");
            this.comboMod.Name = "comboMod";
            this.comboMod.SelectedIndexChanged += new System.EventHandler(this.comboMod_SelectedIndexChanged);
            this.comboMod.DropDownClosed += new System.EventHandler(this.comboMod_DropDownClosed);
            // 
            // EditStaticModDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.panelLoss);
            this.Controls.Add(this.comboMod);
            this.Controls.Add(this.labelLoss);
            this.Controls.Add(this.cbVariableMod);
            this.Controls.Add(this.labelRelativeRT);
            this.Controls.Add(this.comboRelativeRT);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.btnLoss);
            this.Controls.Add(this.comboTerm);
            this.Controls.Add(this.comboAA);
            this.Controls.Add(this.labelAA);
            this.Controls.Add(this.textName);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.cbChemicalFormula);
            this.Controls.Add(this.panelAtoms);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditStaticModDlg";
            this.ShowInTaskbar = false;
            this.panelAtoms.ResumeLayout(false);
            this.panelAtoms.PerformLayout();
            this.panelLoss.ResumeLayout(false);
            this.panelLoss.PerformLayout();
            this.toolBarLosses.ResumeLayout(false);
            this.toolBarLosses.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Label labelAA;
        private System.Windows.Forms.ComboBox comboAA;
        private System.Windows.Forms.ComboBox comboTerm;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Panel panelAtoms;
        private System.Windows.Forms.CheckBox cb13C;
        private System.Windows.Forms.CheckBox cb2H;
        private System.Windows.Forms.CheckBox cb18O;
        private System.Windows.Forms.CheckBox cb15N;
        private System.Windows.Forms.CheckBox cbChemicalFormula;
        private System.Windows.Forms.Button btnLoss;
        private System.Windows.Forms.ComboBox comboRelativeRT;
        private System.Windows.Forms.Label labelRelativeRT;
        private System.Windows.Forms.CheckBox cbVariableMod;
        private System.Windows.Forms.ListBox listNeutralLosses;
        private System.Windows.Forms.Label labelLoss;
        private System.Windows.Forms.Panel panelLoss;
        private System.Windows.Forms.ToolStrip toolBarLosses;
        private System.Windows.Forms.ToolStripButton tbbAddLoss;
        private System.Windows.Forms.ToolStripButton tbbEditLoss;
        private System.Windows.Forms.ToolStripButton tbbDeleteLoss;
        private System.Windows.Forms.ComboBox comboMod;
    }
}