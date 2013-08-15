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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditStaticModDlg));
            this.textName = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.labelChemicalFormula = new System.Windows.Forms.Label();
            this.textFormula = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.textMonoMass = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.textAverageMass = new System.Windows.Forms.TextBox();
            this.labelAA = new System.Windows.Forms.Label();
            this.comboAA = new System.Windows.Forms.ComboBox();
            this.comboTerm = new System.Windows.Forms.ComboBox();
            this.label6 = new System.Windows.Forms.Label();
            this.btnFormulaPopup = new System.Windows.Forms.Button();
            this.contextFormula = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.hContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.h2ContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.cContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.c13ContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.nContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.n15ContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.oContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.o18ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.sContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.panelFormula = new System.Windows.Forms.Panel();
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
            this.contextFormula.SuspendLayout();
            this.panelFormula.SuspendLayout();
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
            // labelChemicalFormula
            // 
            resources.ApplyResources(this.labelChemicalFormula, "labelChemicalFormula");
            this.labelChemicalFormula.Name = "labelChemicalFormula";
            // 
            // textFormula
            // 
            resources.ApplyResources(this.textFormula, "textFormula");
            this.textFormula.Name = "textFormula";
            this.textFormula.TextChanged += new System.EventHandler(this.textFormula_TextChanged);
            this.textFormula.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.textFormula_KeyPress);
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // textMonoMass
            // 
            resources.ApplyResources(this.textMonoMass, "textMonoMass");
            this.textMonoMass.Name = "textMonoMass";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // textAverageMass
            // 
            resources.ApplyResources(this.textAverageMass, "textAverageMass");
            this.textAverageMass.Name = "textAverageMass";
            // 
            // labelAA
            // 
            resources.ApplyResources(this.labelAA, "labelAA");
            this.labelAA.Name = "labelAA";
            // 
            // comboAA
            // 
            this.comboAA.FormattingEnabled = true;
            this.comboAA.Items.AddRange(new object[] {
            resources.GetString("comboAA.Items"),
            resources.GetString("comboAA.Items1"),
            resources.GetString("comboAA.Items2"),
            resources.GetString("comboAA.Items3"),
            resources.GetString("comboAA.Items4"),
            resources.GetString("comboAA.Items5"),
            resources.GetString("comboAA.Items6"),
            resources.GetString("comboAA.Items7"),
            resources.GetString("comboAA.Items8"),
            resources.GetString("comboAA.Items9"),
            resources.GetString("comboAA.Items10"),
            resources.GetString("comboAA.Items11"),
            resources.GetString("comboAA.Items12"),
            resources.GetString("comboAA.Items13"),
            resources.GetString("comboAA.Items14"),
            resources.GetString("comboAA.Items15"),
            resources.GetString("comboAA.Items16"),
            resources.GetString("comboAA.Items17"),
            resources.GetString("comboAA.Items18"),
            resources.GetString("comboAA.Items19"),
            resources.GetString("comboAA.Items20"),
            resources.GetString("comboAA.Items21")});
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
            this.comboTerm.Items.AddRange(new object[] {
            resources.GetString("comboTerm.Items"),
            resources.GetString("comboTerm.Items1"),
            resources.GetString("comboTerm.Items2")});
            this.comboTerm.Name = "comboTerm";
            // 
            // label6
            // 
            resources.ApplyResources(this.label6, "label6");
            this.label6.Name = "label6";
            // 
            // btnFormulaPopup
            // 
            resources.ApplyResources(this.btnFormulaPopup, "btnFormulaPopup");
            this.btnFormulaPopup.Name = "btnFormulaPopup";
            this.btnFormulaPopup.UseVisualStyleBackColor = true;
            this.btnFormulaPopup.Click += new System.EventHandler(this.btnFormulaPopup_Click);
            // 
            // contextFormula
            // 
            this.contextFormula.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.hContextMenuItem,
            this.h2ContextMenuItem,
            this.cContextMenuItem,
            this.c13ContextMenuItem,
            this.nContextMenuItem,
            this.n15ContextMenuItem,
            this.oContextMenuItem,
            this.o18ToolStripMenuItem,
            this.pContextMenuItem,
            this.sContextMenuItem});
            this.contextFormula.Name = "contextMenuStrip1";
            resources.ApplyResources(this.contextFormula, "contextFormula");
            // 
            // hContextMenuItem
            // 
            this.hContextMenuItem.Name = "hContextMenuItem";
            resources.ApplyResources(this.hContextMenuItem, "hContextMenuItem");
            this.hContextMenuItem.Click += new System.EventHandler(this.hContextMenuItem_Click);
            // 
            // h2ContextMenuItem
            // 
            this.h2ContextMenuItem.Name = "h2ContextMenuItem";
            resources.ApplyResources(this.h2ContextMenuItem, "h2ContextMenuItem");
            this.h2ContextMenuItem.Click += new System.EventHandler(this.h2ContextMenuItem_Click);
            // 
            // cContextMenuItem
            // 
            this.cContextMenuItem.Name = "cContextMenuItem";
            resources.ApplyResources(this.cContextMenuItem, "cContextMenuItem");
            this.cContextMenuItem.Click += new System.EventHandler(this.cContextMenuItem_Click);
            // 
            // c13ContextMenuItem
            // 
            this.c13ContextMenuItem.Name = "c13ContextMenuItem";
            resources.ApplyResources(this.c13ContextMenuItem, "c13ContextMenuItem");
            this.c13ContextMenuItem.Click += new System.EventHandler(this.c13ContextMenuItem_Click);
            // 
            // nContextMenuItem
            // 
            this.nContextMenuItem.Name = "nContextMenuItem";
            resources.ApplyResources(this.nContextMenuItem, "nContextMenuItem");
            this.nContextMenuItem.Click += new System.EventHandler(this.nContextMenuItem_Click);
            // 
            // n15ContextMenuItem
            // 
            this.n15ContextMenuItem.Name = "n15ContextMenuItem";
            resources.ApplyResources(this.n15ContextMenuItem, "n15ContextMenuItem");
            this.n15ContextMenuItem.Click += new System.EventHandler(this.n15ContextMenuItem_Click);
            // 
            // oContextMenuItem
            // 
            this.oContextMenuItem.Name = "oContextMenuItem";
            resources.ApplyResources(this.oContextMenuItem, "oContextMenuItem");
            this.oContextMenuItem.Click += new System.EventHandler(this.oContextMenuItem_Click);
            // 
            // o18ToolStripMenuItem
            // 
            this.o18ToolStripMenuItem.Name = "o18ToolStripMenuItem";
            resources.ApplyResources(this.o18ToolStripMenuItem, "o18ToolStripMenuItem");
            this.o18ToolStripMenuItem.Click += new System.EventHandler(this.o18ToolStripMenuItem_Click);
            // 
            // pContextMenuItem
            // 
            this.pContextMenuItem.Name = "pContextMenuItem";
            resources.ApplyResources(this.pContextMenuItem, "pContextMenuItem");
            this.pContextMenuItem.Click += new System.EventHandler(this.pContextMenuItem_Click);
            // 
            // sContextMenuItem
            // 
            this.sContextMenuItem.Name = "sContextMenuItem";
            resources.ApplyResources(this.sContextMenuItem, "sContextMenuItem");
            this.sContextMenuItem.Click += new System.EventHandler(this.sContextMenuItem_Click);
            // 
            // panelFormula
            // 
            this.panelFormula.Controls.Add(this.btnFormulaPopup);
            this.panelFormula.Controls.Add(this.textFormula);
            resources.ApplyResources(this.panelFormula, "panelFormula");
            this.panelFormula.Name = "panelFormula";
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
            this.Controls.Add(this.panelFormula);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.btnLoss);
            this.Controls.Add(this.comboTerm);
            this.Controls.Add(this.labelChemicalFormula);
            this.Controls.Add(this.comboAA);
            this.Controls.Add(this.labelAA);
            this.Controls.Add(this.textAverageMass);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.textMonoMass);
            this.Controls.Add(this.label2);
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
            this.contextFormula.ResumeLayout(false);
            this.panelFormula.ResumeLayout(false);
            this.panelFormula.PerformLayout();
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
        private System.Windows.Forms.Label labelChemicalFormula;
        private System.Windows.Forms.TextBox textFormula;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textMonoMass;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textAverageMass;
        private System.Windows.Forms.Label labelAA;
        private System.Windows.Forms.ComboBox comboAA;
        private System.Windows.Forms.ComboBox comboTerm;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Button btnFormulaPopup;
        private System.Windows.Forms.ContextMenuStrip contextFormula;
        private System.Windows.Forms.ToolStripMenuItem hContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem cContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem c13ContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem nContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem n15ContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem oContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem o18ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem sContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem h2ContextMenuItem;
        private System.Windows.Forms.Panel panelFormula;
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