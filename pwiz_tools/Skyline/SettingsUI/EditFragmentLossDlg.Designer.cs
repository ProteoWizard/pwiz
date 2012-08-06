namespace pwiz.Skyline.SettingsUI
{
    partial class EditFragmentLossDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditFragmentLossDlg));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.labelLossFormula = new System.Windows.Forms.Label();
            this.panelLossFormula = new System.Windows.Forms.Panel();
            this.btnLossFormulaPopup = new System.Windows.Forms.Button();
            this.textLossFormula = new System.Windows.Forms.TextBox();
            this.textLossMonoMass = new System.Windows.Forms.TextBox();
            this.textLossAverageMass = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
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
            // labelLossFormula
            // 
            resources.ApplyResources(this.labelLossFormula, "labelLossFormula");
            this.labelLossFormula.Name = "labelLossFormula";
            // 
            // panelLossFormula
            // 
            this.panelLossFormula.Controls.Add(this.btnLossFormulaPopup);
            this.panelLossFormula.Controls.Add(this.textLossFormula);
            resources.ApplyResources(this.panelLossFormula, "panelLossFormula");
            this.panelLossFormula.Name = "panelLossFormula";
            // 
            // btnLossFormulaPopup
            // 
            resources.ApplyResources(this.btnLossFormulaPopup, "btnLossFormulaPopup");
            this.btnLossFormulaPopup.Name = "btnLossFormulaPopup";
            this.btnLossFormulaPopup.UseVisualStyleBackColor = true;
            this.btnLossFormulaPopup.Click += new System.EventHandler(this.btnLossFormulaPopup_Click);
            // 
            // textLossFormula
            // 
            resources.ApplyResources(this.textLossFormula, "textLossFormula");
            this.textLossFormula.Name = "textLossFormula";
            this.textLossFormula.TextChanged += new System.EventHandler(this.textLossFormula_TextChanged);
            this.textLossFormula.KeyPress += new System.Windows.Forms.KeyPressEventHandler(textLossFormula_KeyPress);
            // 
            // textLossMonoMass
            // 
            resources.ApplyResources(this.textLossMonoMass, "textLossMonoMass");
            this.textLossMonoMass.Name = "textLossMonoMass";
            // 
            // textLossAverageMass
            // 
            resources.ApplyResources(this.textLossAverageMass, "textLossAverageMass");
            this.textLossAverageMass.Name = "textLossAverageMass";
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
            // EditFragmentLossDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.labelLossFormula);
            this.Controls.Add(this.panelLossFormula);
            this.Controls.Add(this.textLossMonoMass);
            this.Controls.Add(this.textLossAverageMass);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditFragmentLossDlg";
            this.ShowInTaskbar = false;
            this.panelLossFormula.ResumeLayout(false);
            this.panelLossFormula.PerformLayout();
            this.contextFormula.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Label labelLossFormula;
        private System.Windows.Forms.Panel panelLossFormula;
        private System.Windows.Forms.Button btnLossFormulaPopup;
        private System.Windows.Forms.TextBox textLossFormula;
        private System.Windows.Forms.TextBox textLossMonoMass;
        private System.Windows.Forms.TextBox textLossAverageMass;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label8;
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