//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s):
//

namespace IDPicker.Controls
{
    partial class BasicFilterControl
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose (bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent ()
        {
            System.Windows.Forms.Label lblMinDistinctPeptides;
            System.Windows.Forms.Label label3;
            this.proteinLevelFilterGroupBox = new System.Windows.Forms.GroupBox();
            this.minSpectraPerProteinTextBox = new System.Windows.Forms.TextBox();
            this.lblMinSpectraPerProtein = new System.Windows.Forms.Label();
            this.lblParsimonyVariable = new System.Windows.Forms.Label();
            this.minAdditionalPeptidesTextBox = new System.Windows.Forms.TextBox();
            this.minDistinctPeptidesTextBox = new System.Windows.Forms.TextBox();
            this.psmLevelFilterGroupBox = new System.Windows.Forms.GroupBox();
            this.label1 = new System.Windows.Forms.Label();
            this.maxProteinGroupsTextBox = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.lblPercentSign = new System.Windows.Forms.Label();
            this.minSpectraPerMatchTextBox = new System.Windows.Forms.TextBox();
            this.maxQValueComboBox = new System.Windows.Forms.ComboBox();
            this.minSpectraPerPeptideTextBox = new System.Windows.Forms.TextBox();
            this.lblMaxFdr = new System.Windows.Forms.Label();
            this.CloseLabel = new System.Windows.Forms.LinkLabel();
            this.panel1 = new System.Windows.Forms.Panel();
            this.QonverterLabel = new System.Windows.Forms.LinkLabel();
            lblMinDistinctPeptides = new System.Windows.Forms.Label();
            label3 = new System.Windows.Forms.Label();
            this.proteinLevelFilterGroupBox.SuspendLayout();
            this.psmLevelFilterGroupBox.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblMinDistinctPeptides
            // 
            lblMinDistinctPeptides.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblMinDistinctPeptides.AutoSize = true;
            lblMinDistinctPeptides.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            lblMinDistinctPeptides.Location = new System.Drawing.Point(15, 24);
            lblMinDistinctPeptides.Name = "lblMinDistinctPeptides";
            lblMinDistinctPeptides.Size = new System.Drawing.Size(132, 13);
            lblMinDistinctPeptides.TabIndex = 127;
            lblMinDistinctPeptides.Text = "Minimum distinct peptides:";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            label3.Location = new System.Drawing.Point(15, 51);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(148, 13);
            label3.TabIndex = 127;
            label3.Text = "Minimum spectra per peptide:";
            // 
            // proteinLevelFilterGroupBox
            // 
            this.proteinLevelFilterGroupBox.Controls.Add(this.minSpectraPerProteinTextBox);
            this.proteinLevelFilterGroupBox.Controls.Add(this.lblMinSpectraPerProtein);
            this.proteinLevelFilterGroupBox.Controls.Add(this.lblParsimonyVariable);
            this.proteinLevelFilterGroupBox.Controls.Add(this.minAdditionalPeptidesTextBox);
            this.proteinLevelFilterGroupBox.Controls.Add(this.minDistinctPeptidesTextBox);
            this.proteinLevelFilterGroupBox.Controls.Add(lblMinDistinctPeptides);
            this.proteinLevelFilterGroupBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.proteinLevelFilterGroupBox.Location = new System.Drawing.Point(3, 148);
            this.proteinLevelFilterGroupBox.Name = "proteinLevelFilterGroupBox";
            this.proteinLevelFilterGroupBox.Size = new System.Drawing.Size(233, 108);
            this.proteinLevelFilterGroupBox.TabIndex = 125;
            this.proteinLevelFilterGroupBox.TabStop = false;
            this.proteinLevelFilterGroupBox.Text = "Protein Level Filters";
            // 
            // minSpectraPerProteinTextBox
            // 
            this.minSpectraPerProteinTextBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.minSpectraPerProteinTextBox.Location = new System.Drawing.Point(167, 74);
            this.minSpectraPerProteinTextBox.Name = "minSpectraPerProteinTextBox";
            this.minSpectraPerProteinTextBox.Size = new System.Drawing.Size(46, 21);
            this.minSpectraPerProteinTextBox.TabIndex = 11;
            this.minSpectraPerProteinTextBox.TextChanged += new System.EventHandler(this.filterControl_TextChanged);
            this.minSpectraPerProteinTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
            // 
            // lblMinSpectraPerProtein
            // 
            this.lblMinSpectraPerProtein.AutoSize = true;
            this.lblMinSpectraPerProtein.Location = new System.Drawing.Point(15, 77);
            this.lblMinSpectraPerProtein.Name = "lblMinSpectraPerProtein";
            this.lblMinSpectraPerProtein.Size = new System.Drawing.Size(146, 13);
            this.lblMinSpectraPerProtein.TabIndex = 133;
            this.lblMinSpectraPerProtein.Text = "Minimum spectra per protein:";
            // 
            // lblParsimonyVariable
            // 
            this.lblParsimonyVariable.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblParsimonyVariable.AutoSize = true;
            this.lblParsimonyVariable.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.lblParsimonyVariable.Location = new System.Drawing.Point(15, 51);
            this.lblParsimonyVariable.Name = "lblParsimonyVariable";
            this.lblParsimonyVariable.Size = new System.Drawing.Size(144, 13);
            this.lblParsimonyVariable.TabIndex = 132;
            this.lblParsimonyVariable.Text = "Minimum additional peptides:";
            // 
            // minAdditionalPeptidesTextBox
            // 
            this.minAdditionalPeptidesTextBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.minAdditionalPeptidesTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.minAdditionalPeptidesTextBox.Location = new System.Drawing.Point(167, 46);
            this.minAdditionalPeptidesTextBox.Name = "minAdditionalPeptidesTextBox";
            this.minAdditionalPeptidesTextBox.Size = new System.Drawing.Size(46, 21);
            this.minAdditionalPeptidesTextBox.TabIndex = 9;
            this.minAdditionalPeptidesTextBox.TextChanged += new System.EventHandler(this.filterControl_TextChanged);
            this.minAdditionalPeptidesTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
            // 
            // minDistinctPeptidesTextBox
            // 
            this.minDistinctPeptidesTextBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.minDistinctPeptidesTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.minDistinctPeptidesTextBox.Location = new System.Drawing.Point(167, 19);
            this.minDistinctPeptidesTextBox.Name = "minDistinctPeptidesTextBox";
            this.minDistinctPeptidesTextBox.Size = new System.Drawing.Size(46, 21);
            this.minDistinctPeptidesTextBox.TabIndex = 7;
            this.minDistinctPeptidesTextBox.TextChanged += new System.EventHandler(this.filterControl_TextChanged);
            this.minDistinctPeptidesTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
            // 
            // psmLevelFilterGroupBox
            // 
            this.psmLevelFilterGroupBox.Controls.Add(this.label1);
            this.psmLevelFilterGroupBox.Controls.Add(this.maxProteinGroupsTextBox);
            this.psmLevelFilterGroupBox.Controls.Add(this.label2);
            this.psmLevelFilterGroupBox.Controls.Add(this.lblPercentSign);
            this.psmLevelFilterGroupBox.Controls.Add(this.minSpectraPerMatchTextBox);
            this.psmLevelFilterGroupBox.Controls.Add(this.maxQValueComboBox);
            this.psmLevelFilterGroupBox.Controls.Add(this.minSpectraPerPeptideTextBox);
            this.psmLevelFilterGroupBox.Controls.Add(label3);
            this.psmLevelFilterGroupBox.Controls.Add(this.lblMaxFdr);
            this.psmLevelFilterGroupBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.psmLevelFilterGroupBox.Location = new System.Drawing.Point(3, 3);
            this.psmLevelFilterGroupBox.Name = "psmLevelFilterGroupBox";
            this.psmLevelFilterGroupBox.Size = new System.Drawing.Size(233, 139);
            this.psmLevelFilterGroupBox.TabIndex = 126;
            this.psmLevelFilterGroupBox.TabStop = false;
            this.psmLevelFilterGroupBox.Text = "Peptide-Spectrum-Match Filters";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.label1.Location = new System.Drawing.Point(15, 105);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(128, 13);
            this.label1.TabIndex = 134;
            this.label1.Text = "Maximum protein groups:";
            // 
            // maxProteinGroupsTextBox
            // 
            this.maxProteinGroupsTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.maxProteinGroupsTextBox.Location = new System.Drawing.Point(167, 102);
            this.maxProteinGroupsTextBox.Name = "maxProteinGroupsTextBox";
            this.maxProteinGroupsTextBox.Size = new System.Drawing.Size(46, 21);
            this.maxProteinGroupsTextBox.TabIndex = 133;
            this.maxProteinGroupsTextBox.TextChanged += new System.EventHandler(this.filterControl_TextChanged);
            this.maxProteinGroupsTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.label2.Location = new System.Drawing.Point(15, 78);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(141, 13);
            this.label2.TabIndex = 132;
            this.label2.Text = "Minimum spectra per match:";
            // 
            // lblPercentSign
            // 
            this.lblPercentSign.AutoSize = true;
            this.lblPercentSign.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.lblPercentSign.Location = new System.Drawing.Point(214, 24);
            this.lblPercentSign.Margin = new System.Windows.Forms.Padding(0);
            this.lblPercentSign.Name = "lblPercentSign";
            this.lblPercentSign.Size = new System.Drawing.Size(18, 13);
            this.lblPercentSign.TabIndex = 129;
            this.lblPercentSign.Text = "%";
            // 
            // minSpectraPerMatchTextBox
            // 
            this.minSpectraPerMatchTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.minSpectraPerMatchTextBox.Location = new System.Drawing.Point(167, 75);
            this.minSpectraPerMatchTextBox.Name = "minSpectraPerMatchTextBox";
            this.minSpectraPerMatchTextBox.Size = new System.Drawing.Size(46, 21);
            this.minSpectraPerMatchTextBox.TabIndex = 9;
            this.minSpectraPerMatchTextBox.TextChanged += new System.EventHandler(this.filterControl_TextChanged);
            this.minSpectraPerMatchTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
            // 
            // maxQValueComboBox
            // 
            this.maxQValueComboBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.maxQValueComboBox.FormattingEnabled = true;
            this.maxQValueComboBox.Items.AddRange(new object[] {
            "0",
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9",
            "10"});
            this.maxQValueComboBox.Location = new System.Drawing.Point(167, 21);
            this.maxQValueComboBox.Name = "maxQValueComboBox";
            this.maxQValueComboBox.Size = new System.Drawing.Size(45, 21);
            this.maxQValueComboBox.TabIndex = 4;
            this.maxQValueComboBox.Text = "5";
            this.maxQValueComboBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.doubleTextBox_KeyDown);
            this.maxQValueComboBox.TextChanged += new System.EventHandler(this.filterControl_TextChanged);
            // 
            // minSpectraPerPeptideTextBox
            // 
            this.minSpectraPerPeptideTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.minSpectraPerPeptideTextBox.Location = new System.Drawing.Point(167, 48);
            this.minSpectraPerPeptideTextBox.Name = "minSpectraPerPeptideTextBox";
            this.minSpectraPerPeptideTextBox.Size = new System.Drawing.Size(46, 21);
            this.minSpectraPerPeptideTextBox.TabIndex = 7;
            this.minSpectraPerPeptideTextBox.TextChanged += new System.EventHandler(this.filterControl_TextChanged);
            this.minSpectraPerPeptideTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
            // 
            // lblMaxFdr
            // 
            this.lblMaxFdr.AutoSize = true;
            this.lblMaxFdr.BackColor = System.Drawing.Color.Transparent;
            this.lblMaxFdr.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.lblMaxFdr.Location = new System.Drawing.Point(14, 24);
            this.lblMaxFdr.Name = "lblMaxFdr";
            this.lblMaxFdr.Size = new System.Drawing.Size(95, 13);
            this.lblMaxFdr.TabIndex = 125;
            this.lblMaxFdr.Text = "Maximum Q Value:";
            // 
            // CloseLabel
            // 
            this.CloseLabel.AutoSize = true;
            this.CloseLabel.Location = new System.Drawing.Point(151, 5);
            this.CloseLabel.Name = "CloseLabel";
            this.CloseLabel.Size = new System.Drawing.Size(82, 13);
            this.CloseLabel.TabIndex = 127;
            this.CloseLabel.TabStop = true;
            this.CloseLabel.Text = "Save and Close";
            this.CloseLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.CloseLabel_LinkClicked);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.QonverterLabel);
            this.panel1.Controls.Add(this.CloseLabel);
            this.panel1.Location = new System.Drawing.Point(3, 262);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(233, 21);
            this.panel1.TabIndex = 128;
            // 
            // QonverterLabel
            // 
            this.QonverterLabel.AutoSize = true;
            this.QonverterLabel.Location = new System.Drawing.Point(3, 5);
            this.QonverterLabel.Name = "QonverterLabel";
            this.QonverterLabel.Size = new System.Drawing.Size(95, 13);
            this.QonverterLabel.TabIndex = 128;
            this.QonverterLabel.TabStop = true;
            this.QonverterLabel.Text = "Qonverter Settings";
            this.QonverterLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.QonverterLabel_LinkClicked);
            // 
            // BasicFilterControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.BackColor = System.Drawing.SystemColors.Menu;
            this.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.Controls.Add(this.psmLevelFilterGroupBox);
            this.Controls.Add(this.proteinLevelFilterGroupBox);
            this.Controls.Add(this.panel1);
            this.Name = "BasicFilterControl";
            this.Size = new System.Drawing.Size(239, 286);
            this.proteinLevelFilterGroupBox.ResumeLayout(false);
            this.proteinLevelFilterGroupBox.PerformLayout();
            this.psmLevelFilterGroupBox.ResumeLayout(false);
            this.psmLevelFilterGroupBox.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox proteinLevelFilterGroupBox;
        private System.Windows.Forms.TextBox minSpectraPerProteinTextBox;
        private System.Windows.Forms.Label lblMinSpectraPerProtein;
        private System.Windows.Forms.Label lblParsimonyVariable;
        private System.Windows.Forms.TextBox minAdditionalPeptidesTextBox;
        private System.Windows.Forms.TextBox minDistinctPeptidesTextBox;
        private System.Windows.Forms.GroupBox psmLevelFilterGroupBox;
        private System.Windows.Forms.Label lblPercentSign;
        private System.Windows.Forms.ComboBox maxQValueComboBox;
        private System.Windows.Forms.Label lblMaxFdr;
        private System.Windows.Forms.LinkLabel CloseLabel;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.LinkLabel QonverterLabel;
        private System.Windows.Forms.TextBox minSpectraPerPeptideTextBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox maxProteinGroupsTextBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox minSpectraPerMatchTextBox;
    }
}
