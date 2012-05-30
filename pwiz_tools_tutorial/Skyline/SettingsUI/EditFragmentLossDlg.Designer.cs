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
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(267, 42);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 7;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(267, 12);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 6;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // labelLossFormula
            // 
            this.labelLossFormula.AutoSize = true;
            this.labelLossFormula.Location = new System.Drawing.Point(12, 9);
            this.labelLossFormula.Name = "labelLossFormula";
            this.labelLossFormula.Size = new System.Drawing.Size(147, 13);
            this.labelLossFormula.TabIndex = 0;
            this.labelLossFormula.Text = "Neutral loss &chemical formula:";
            // 
            // panelLossFormula
            // 
            this.panelLossFormula.Controls.Add(this.btnLossFormulaPopup);
            this.panelLossFormula.Controls.Add(this.textLossFormula);
            this.panelLossFormula.Location = new System.Drawing.Point(10, 22);
            this.panelLossFormula.Name = "panelLossFormula";
            this.panelLossFormula.Size = new System.Drawing.Size(230, 31);
            this.panelLossFormula.TabIndex = 1;
            // 
            // btnLossFormulaPopup
            // 
            this.btnLossFormulaPopup.Location = new System.Drawing.Point(170, 3);
            this.btnLossFormulaPopup.Name = "btnLossFormulaPopup";
            this.btnLossFormulaPopup.Size = new System.Drawing.Size(24, 23);
            this.btnLossFormulaPopup.TabIndex = 1;
            this.btnLossFormulaPopup.UseVisualStyleBackColor = true;
            this.btnLossFormulaPopup.Click += new System.EventHandler(this.btnLossFormulaPopup_Click);
            // 
            // textLossFormula
            // 
            this.textLossFormula.Location = new System.Drawing.Point(5, 5);
            this.textLossFormula.Name = "textLossFormula";
            this.textLossFormula.Size = new System.Drawing.Size(160, 20);
            this.textLossFormula.TabIndex = 0;
            this.textLossFormula.TextChanged += new System.EventHandler(this.textLossFormula_TextChanged);
            this.textLossFormula.KeyPress += new System.Windows.Forms.KeyPressEventHandler(textLossFormula_KeyPress);
            // 
            // textLossMonoMass
            // 
            this.textLossMonoMass.Location = new System.Drawing.Point(15, 81);
            this.textLossMonoMass.Name = "textLossMonoMass";
            this.textLossMonoMass.Size = new System.Drawing.Size(98, 20);
            this.textLossMonoMass.TabIndex = 3;
            // 
            // textLossAverageMass
            // 
            this.textLossAverageMass.Location = new System.Drawing.Point(142, 81);
            this.textLossAverageMass.Name = "textLossAverageMass";
            this.textLossAverageMass.Size = new System.Drawing.Size(98, 20);
            this.textLossAverageMass.TabIndex = 5;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(139, 65);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(71, 13);
            this.label7.TabIndex = 4;
            this.label7.Text = "A&verage loss:";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(12, 65);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(94, 13);
            this.label8.TabIndex = 2;
            this.label8.Text = "&Monoisotopic loss:";
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
            this.contextFormula.Size = new System.Drawing.Size(96, 224);
            // 
            // hContextMenuItem
            // 
            this.hContextMenuItem.Name = "hContextMenuItem";
            this.hContextMenuItem.Size = new System.Drawing.Size(95, 22);
            this.hContextMenuItem.Text = "H";
            this.hContextMenuItem.Click += new System.EventHandler(this.hContextMenuItem_Click);
            // 
            // h2ContextMenuItem
            // 
            this.h2ContextMenuItem.Name = "h2ContextMenuItem";
            this.h2ContextMenuItem.Size = new System.Drawing.Size(95, 22);
            this.h2ContextMenuItem.Text = "2H";
            this.h2ContextMenuItem.Click += new System.EventHandler(this.h2ContextMenuItem_Click);
            // 
            // cContextMenuItem
            // 
            this.cContextMenuItem.Name = "cContextMenuItem";
            this.cContextMenuItem.Size = new System.Drawing.Size(95, 22);
            this.cContextMenuItem.Text = "C";
            this.cContextMenuItem.Click += new System.EventHandler(this.cContextMenuItem_Click);
            // 
            // c13ContextMenuItem
            // 
            this.c13ContextMenuItem.Name = "c13ContextMenuItem";
            this.c13ContextMenuItem.Size = new System.Drawing.Size(95, 22);
            this.c13ContextMenuItem.Text = "13C";
            this.c13ContextMenuItem.Click += new System.EventHandler(this.c13ContextMenuItem_Click);
            // 
            // nContextMenuItem
            // 
            this.nContextMenuItem.Name = "nContextMenuItem";
            this.nContextMenuItem.Size = new System.Drawing.Size(95, 22);
            this.nContextMenuItem.Text = "N";
            this.nContextMenuItem.Click += new System.EventHandler(this.nContextMenuItem_Click);
            // 
            // n15ContextMenuItem
            // 
            this.n15ContextMenuItem.Name = "n15ContextMenuItem";
            this.n15ContextMenuItem.Size = new System.Drawing.Size(95, 22);
            this.n15ContextMenuItem.Text = "15N";
            this.n15ContextMenuItem.Click += new System.EventHandler(this.n15ContextMenuItem_Click);
            // 
            // oContextMenuItem
            // 
            this.oContextMenuItem.Name = "oContextMenuItem";
            this.oContextMenuItem.Size = new System.Drawing.Size(95, 22);
            this.oContextMenuItem.Text = "O";
            this.oContextMenuItem.Click += new System.EventHandler(this.oContextMenuItem_Click);
            // 
            // o18ToolStripMenuItem
            // 
            this.o18ToolStripMenuItem.Name = "o18ToolStripMenuItem";
            this.o18ToolStripMenuItem.Size = new System.Drawing.Size(95, 22);
            this.o18ToolStripMenuItem.Text = "18O";
            this.o18ToolStripMenuItem.Click += new System.EventHandler(this.o18ToolStripMenuItem_Click);
            // 
            // pContextMenuItem
            // 
            this.pContextMenuItem.Name = "pContextMenuItem";
            this.pContextMenuItem.Size = new System.Drawing.Size(95, 22);
            this.pContextMenuItem.Text = "P";
            this.pContextMenuItem.Click += new System.EventHandler(this.pContextMenuItem_Click);
            // 
            // sContextMenuItem
            // 
            this.sContextMenuItem.Name = "sContextMenuItem";
            this.sContextMenuItem.Size = new System.Drawing.Size(95, 22);
            this.sContextMenuItem.Text = "S";
            this.sContextMenuItem.Click += new System.EventHandler(this.sContextMenuItem_Click);
            // 
            // EditFragmentLossDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(354, 120);
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
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit Neutral Loss";
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