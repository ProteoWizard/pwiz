namespace pwiz.Skyline.SettingsUI
{
    partial class EditMeasuredIonDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditMeasuredIonDlg));
            this.label1 = new System.Windows.Forms.Label();
            this.textFragment = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.textRestrict = new System.Windows.Forms.TextBox();
            this.comboDirection = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.textName = new System.Windows.Forms.TextBox();
            this.labelName = new System.Windows.Forms.Label();
            this.radioFragment = new System.Windows.Forms.RadioButton();
            this.radioReporter = new System.Windows.Forms.RadioButton();
            this.labelFormula = new System.Windows.Forms.Label();
            this.panelLossFormula = new System.Windows.Forms.Panel();
            this.btnFormulaPopup = new System.Windows.Forms.Button();
            this.textFormula = new System.Windows.Forms.TextBox();
            this.textMonoMass = new System.Windows.Forms.TextBox();
            this.textAverageMass = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.textMinAas = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
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
            this.panelLossFormula.SuspendLayout();
            this.contextFormula.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // textFragment
            // 
            resources.ApplyResources(this.textFragment, "textFragment");
            this.textFragment.Name = "textFragment";
            this.textFragment.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.textAa_KeyPress);
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // textRestrict
            // 
            resources.ApplyResources(this.textRestrict, "textRestrict");
            this.textRestrict.Name = "textRestrict";
            this.textRestrict.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.textAa_KeyPress);
            // 
            // comboDirection
            // 
            this.comboDirection.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboDirection.FormattingEnabled = true;
            this.comboDirection.Items.AddRange(new object[] {
            resources.GetString("comboDirection.Items"),
            resources.GetString("comboDirection.Items1")});
            resources.ApplyResources(this.comboDirection, "comboDirection");
            this.comboDirection.Name = "comboDirection";
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
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // textName
            // 
            resources.ApplyResources(this.textName, "textName");
            this.textName.Name = "textName";
            // 
            // labelName
            // 
            resources.ApplyResources(this.labelName, "labelName");
            this.labelName.Name = "labelName";
            // 
            // radioFragment
            // 
            resources.ApplyResources(this.radioFragment, "radioFragment");
            this.radioFragment.Checked = true;
            this.radioFragment.Name = "radioFragment";
            this.radioFragment.TabStop = true;
            this.radioFragment.UseVisualStyleBackColor = true;
            this.radioFragment.CheckedChanged += new System.EventHandler(this.radioFragment_CheckedChanged);
            // 
            // radioReporter
            // 
            resources.ApplyResources(this.radioReporter, "radioReporter");
            this.radioReporter.Name = "radioReporter";
            this.radioReporter.UseVisualStyleBackColor = true;
            // 
            // labelFormula
            // 
            resources.ApplyResources(this.labelFormula, "labelFormula");
            this.labelFormula.Name = "labelFormula";
            // 
            // panelLossFormula
            // 
            this.panelLossFormula.Controls.Add(this.btnFormulaPopup);
            this.panelLossFormula.Controls.Add(this.textFormula);
            resources.ApplyResources(this.panelLossFormula, "panelLossFormula");
            this.panelLossFormula.Name = "panelLossFormula";
            // 
            // btnFormulaPopup
            // 
            resources.ApplyResources(this.btnFormulaPopup, "btnFormulaPopup");
            this.btnFormulaPopup.Name = "btnFormulaPopup";
            this.btnFormulaPopup.UseVisualStyleBackColor = true;
            this.btnFormulaPopup.Click += new System.EventHandler(this.btnFormulaPopup_Click);
            // 
            // textFormula
            // 
            resources.ApplyResources(this.textFormula, "textFormula");
            this.textFormula.Name = "textFormula";
            this.textFormula.TextChanged += new System.EventHandler(this.textFormula_TextChanged);
            this.textFormula.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.textFormula_KeyPress);
            // 
            // textMonoMass
            // 
            resources.ApplyResources(this.textMonoMass, "textMonoMass");
            this.textMonoMass.Name = "textMonoMass";
            // 
            // textAverageMass
            // 
            resources.ApplyResources(this.textAverageMass, "textAverageMass");
            this.textAverageMass.Name = "textAverageMass";
            // 
            // label7
            // 
            resources.ApplyResources(this.label7, "label7");
            this.label7.Name = "label7";
            // 
            // label8
            // 
            resources.ApplyResources(this.label8, "label8");
            this.label8.Name = "label8";
            // 
            // textMinAas
            // 
            resources.ApplyResources(this.textMinAas, "textMinAas");
            this.textMinAas.Name = "textMinAas";
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
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
            // EditMeasuredIonDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.textMinAas);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.labelFormula);
            this.Controls.Add(this.panelLossFormula);
            this.Controls.Add(this.textMonoMass);
            this.Controls.Add(this.textAverageMass);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.radioReporter);
            this.Controls.Add(this.radioFragment);
            this.Controls.Add(this.textName);
            this.Controls.Add(this.labelName);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.comboDirection);
            this.Controls.Add(this.textRestrict);
            this.Controls.Add(this.textFragment);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditMeasuredIonDlg";
            this.ShowInTaskbar = false;
            this.panelLossFormula.ResumeLayout(false);
            this.panelLossFormula.PerformLayout();
            this.contextFormula.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textFragment;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textRestrict;
        private System.Windows.Forms.ComboBox comboDirection;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label labelName;
        private System.Windows.Forms.RadioButton radioFragment;
        private System.Windows.Forms.RadioButton radioReporter;
        private System.Windows.Forms.Label labelFormula;
        private System.Windows.Forms.Panel panelLossFormula;
        private System.Windows.Forms.Button btnFormulaPopup;
        private System.Windows.Forms.TextBox textFormula;
        private System.Windows.Forms.TextBox textMonoMass;
        private System.Windows.Forms.TextBox textAverageMass;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox textMinAas;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ContextMenuStrip contextFormula;
        private System.Windows.Forms.ToolStripMenuItem hContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem h2ContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem cContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem c13ContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem nContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem n15ContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem oContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem o18ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem sContextMenuItem;
    }
}