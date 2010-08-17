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
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(11, 80);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(76, 13);
            this.label1.TabIndex = 3;
            this.label1.Text = "&Fragment AAs:";
            // 
            // textFragment
            // 
            this.textFragment.Location = new System.Drawing.Point(14, 97);
            this.textFragment.Name = "textFragment";
            this.textFragment.Size = new System.Drawing.Size(87, 20);
            this.textFragment.TabIndex = 4;
            this.textFragment.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.textAa_KeyPress);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(129, 80);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(87, 13);
            this.label2.TabIndex = 5;
            this.label2.Text = "&Unless next AAs:";
            // 
            // textRestrict
            // 
            this.textRestrict.Location = new System.Drawing.Point(129, 97);
            this.textRestrict.Name = "textRestrict";
            this.textRestrict.Size = new System.Drawing.Size(87, 20);
            this.textRestrict.TabIndex = 6;
            this.textRestrict.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.textAa_KeyPress);
            // 
            // comboDirection
            // 
            this.comboDirection.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboDirection.FormattingEnabled = true;
            this.comboDirection.Items.AddRange(new object[] {
            "C-terminus",
            "N-terminus"});
            this.comboDirection.Location = new System.Drawing.Point(14, 152);
            this.comboDirection.Name = "comboDirection";
            this.comboDirection.Size = new System.Drawing.Size(87, 21);
            this.comboDirection.TabIndex = 8;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(11, 133);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(34, 13);
            this.label3.TabIndex = 7;
            this.label3.Text = "&Type:";
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(238, 14);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 18;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(238, 44);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 19;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // textName
            // 
            this.textName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.textName.Location = new System.Drawing.Point(13, 30);
            this.textName.Name = "textName";
            this.textName.Size = new System.Drawing.Size(205, 20);
            this.textName.TabIndex = 1;
            // 
            // labelName
            // 
            this.labelName.AutoSize = true;
            this.labelName.Location = new System.Drawing.Point(10, 14);
            this.labelName.Name = "labelName";
            this.labelName.Size = new System.Drawing.Size(38, 13);
            this.labelName.TabIndex = 0;
            this.labelName.Text = "&Name:";
            // 
            // radioFragment
            // 
            this.radioFragment.AutoSize = true;
            this.radioFragment.Checked = true;
            this.radioFragment.Location = new System.Drawing.Point(-5, 51);
            this.radioFragment.Name = "radioFragment";
            this.radioFragment.Size = new System.Drawing.Size(104, 17);
            this.radioFragment.TabIndex = 2;
            this.radioFragment.TabStop = true;
            this.radioFragment.Text = "Inten&se fragment";
            this.radioFragment.UseVisualStyleBackColor = true;
            this.radioFragment.Visible = false;
            this.radioFragment.CheckedChanged += new System.EventHandler(this.radioFragment_CheckedChanged);
            // 
            // radioReporter
            // 
            this.radioReporter.AutoSize = true;
            this.radioReporter.Location = new System.Drawing.Point(-5, 205);
            this.radioReporter.Name = "radioReporter";
            this.radioReporter.Size = new System.Drawing.Size(83, 17);
            this.radioReporter.TabIndex = 11;
            this.radioReporter.Text = "&Reporter ion";
            this.radioReporter.UseVisualStyleBackColor = true;
            this.radioReporter.Visible = false;
            // 
            // labelFormula
            // 
            this.labelFormula.AutoSize = true;
            this.labelFormula.Enabled = false;
            this.labelFormula.Location = new System.Drawing.Point(11, 234);
            this.labelFormula.Name = "labelFormula";
            this.labelFormula.Size = new System.Drawing.Size(135, 13);
            this.labelFormula.TabIndex = 12;
            this.labelFormula.Text = "Molecule &chemical formula:";
            this.labelFormula.Visible = false;
            // 
            // panelLossFormula
            // 
            this.panelLossFormula.Controls.Add(this.btnFormulaPopup);
            this.panelLossFormula.Controls.Add(this.textFormula);
            this.panelLossFormula.Location = new System.Drawing.Point(8, 247);
            this.panelLossFormula.Name = "panelLossFormula";
            this.panelLossFormula.Size = new System.Drawing.Size(230, 31);
            this.panelLossFormula.TabIndex = 13;
            this.panelLossFormula.Visible = false;
            // 
            // btnFormulaPopup
            // 
            this.btnFormulaPopup.Enabled = false;
            this.btnFormulaPopup.Location = new System.Drawing.Point(170, 3);
            this.btnFormulaPopup.Name = "btnFormulaPopup";
            this.btnFormulaPopup.Size = new System.Drawing.Size(24, 23);
            this.btnFormulaPopup.TabIndex = 1;
            this.btnFormulaPopup.UseVisualStyleBackColor = true;
            this.btnFormulaPopup.Click += new System.EventHandler(this.btnFormulaPopup_Click);
            // 
            // textFormula
            // 
            this.textFormula.Enabled = false;
            this.textFormula.Location = new System.Drawing.Point(5, 5);
            this.textFormula.Name = "textFormula";
            this.textFormula.Size = new System.Drawing.Size(160, 20);
            this.textFormula.TabIndex = 0;
            this.textFormula.TextChanged += new System.EventHandler(this.textFormula_TextChanged);
            this.textFormula.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.textFormula_KeyPress);
            // 
            // textMonoMass
            // 
            this.textMonoMass.Enabled = false;
            this.textMonoMass.Location = new System.Drawing.Point(14, 306);
            this.textMonoMass.Name = "textMonoMass";
            this.textMonoMass.Size = new System.Drawing.Size(87, 20);
            this.textMonoMass.TabIndex = 15;
            this.textMonoMass.Visible = false;
            // 
            // textAverageMass
            // 
            this.textAverageMass.Enabled = false;
            this.textAverageMass.Location = new System.Drawing.Point(129, 306);
            this.textAverageMass.Name = "textAverageMass";
            this.textAverageMass.Size = new System.Drawing.Size(87, 20);
            this.textAverageMass.TabIndex = 17;
            this.textAverageMass.Visible = false;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Enabled = false;
            this.label7.Location = new System.Drawing.Point(129, 290);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(77, 13);
            this.label7.TabIndex = 16;
            this.label7.Text = "A&verage mass:";
            this.label7.Visible = false;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Enabled = false;
            this.label8.Location = new System.Drawing.Point(11, 290);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(100, 13);
            this.label8.TabIndex = 14;
            this.label8.Text = "&Monoisotopic mass:";
            this.label8.Visible = false;
            // 
            // textMinAas
            // 
            this.textMinAas.Location = new System.Drawing.Point(129, 152);
            this.textMinAas.Name = "textMinAas";
            this.textMinAas.Size = new System.Drawing.Size(87, 20);
            this.textMinAas.TabIndex = 10;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(129, 133);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(49, 13);
            this.label4.TabIndex = 9;
            this.label4.Text = "Min &AAs:";
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
            // EditMeasuredIonDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(325, 368);
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
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit Special Ion";
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