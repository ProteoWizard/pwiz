//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
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
            this.gbProteinDetails = new System.Windows.Forms.GroupBox();
            this.minSpectraTextBox = new System.Windows.Forms.TextBox();
            this.lblMinSpectraPerProtein = new System.Windows.Forms.Label();
            this.lblParsimonyVariable = new System.Windows.Forms.Label();
            this.minAdditionalPeptidesTextBox = new System.Windows.Forms.TextBox();
            this.minDistinctPeptidesTextBox = new System.Windows.Forms.TextBox();
            this.gbPeptideDetails = new System.Windows.Forms.GroupBox();
            this.lblPercentSign = new System.Windows.Forms.Label();
            this.minPeptideLengthTextBox = new System.Windows.Forms.TextBox();
            this.maxAmbiguousIdsTextBox = new System.Windows.Forms.TextBox();
            this.lblMinPeptideLength = new System.Windows.Forms.Label();
            this.lblMaxAmbigIds = new System.Windows.Forms.Label();
            this.maxQValueComboBox = new System.Windows.Forms.ComboBox();
            this.lblMaxFdr = new System.Windows.Forms.Label();
            this.CloseLabel = new System.Windows.Forms.LinkLabel();
            this.panel1 = new System.Windows.Forms.Panel();
            this.QonverterLabel = new System.Windows.Forms.LinkLabel();
            lblMinDistinctPeptides = new System.Windows.Forms.Label();
            this.gbProteinDetails.SuspendLayout();
            this.gbPeptideDetails.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblMinDistinctPeptides
            // 
            lblMinDistinctPeptides.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblMinDistinctPeptides.AutoSize = true;
            lblMinDistinctPeptides.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            lblMinDistinctPeptides.Location = new System.Drawing.Point(15, 24);
            lblMinDistinctPeptides.Name = "lblMinDistinctPeptides";
            lblMinDistinctPeptides.Size = new System.Drawing.Size(132, 13);
            lblMinDistinctPeptides.TabIndex = 127;
            lblMinDistinctPeptides.Text = "Minimum distinct peptides:";
            // 
            // gbProteinDetails
            // 
            this.gbProteinDetails.Controls.Add(this.minSpectraTextBox);
            this.gbProteinDetails.Controls.Add(this.lblMinSpectraPerProtein);
            this.gbProteinDetails.Controls.Add(this.lblParsimonyVariable);
            this.gbProteinDetails.Controls.Add(this.minAdditionalPeptidesTextBox);
            this.gbProteinDetails.Controls.Add(this.minDistinctPeptidesTextBox);
            this.gbProteinDetails.Controls.Add(lblMinDistinctPeptides);
            this.gbProteinDetails.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.gbProteinDetails.Location = new System.Drawing.Point(3, 117);
            this.gbProteinDetails.Name = "gbProteinDetails";
            this.gbProteinDetails.Size = new System.Drawing.Size(233, 108);
            this.gbProteinDetails.TabIndex = 125;
            this.gbProteinDetails.TabStop = false;
            this.gbProteinDetails.Text = "Protein Level Filters";
            // 
            // minSpectraTextBox
            // 
            this.minSpectraTextBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.minSpectraTextBox.Location = new System.Drawing.Point(167, 74);
            this.minSpectraTextBox.Name = "minSpectraTextBox";
            this.minSpectraTextBox.Size = new System.Drawing.Size(46, 21);
            this.minSpectraTextBox.TabIndex = 11;
            this.minSpectraTextBox.TextChanged += new System.EventHandler(this.filterControl_TextChanged);
            this.minSpectraTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
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
            this.lblParsimonyVariable.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblParsimonyVariable.Location = new System.Drawing.Point(15, 51);
            this.lblParsimonyVariable.Name = "lblParsimonyVariable";
            this.lblParsimonyVariable.Size = new System.Drawing.Size(144, 13);
            this.lblParsimonyVariable.TabIndex = 132;
            this.lblParsimonyVariable.Text = "Minimum additional peptides:";
            // 
            // minAdditionalPeptidesTextBox
            // 
            this.minAdditionalPeptidesTextBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.minAdditionalPeptidesTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
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
            this.minDistinctPeptidesTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.minDistinctPeptidesTextBox.Location = new System.Drawing.Point(167, 19);
            this.minDistinctPeptidesTextBox.Name = "minDistinctPeptidesTextBox";
            this.minDistinctPeptidesTextBox.Size = new System.Drawing.Size(46, 21);
            this.minDistinctPeptidesTextBox.TabIndex = 7;
            this.minDistinctPeptidesTextBox.TextChanged += new System.EventHandler(this.filterControl_TextChanged);
            this.minDistinctPeptidesTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
            // 
            // gbPeptideDetails
            // 
            this.gbPeptideDetails.Controls.Add(this.lblPercentSign);
            this.gbPeptideDetails.Controls.Add(this.minPeptideLengthTextBox);
            this.gbPeptideDetails.Controls.Add(this.maxAmbiguousIdsTextBox);
            this.gbPeptideDetails.Controls.Add(this.lblMinPeptideLength);
            this.gbPeptideDetails.Controls.Add(this.lblMaxAmbigIds);
            this.gbPeptideDetails.Controls.Add(this.maxQValueComboBox);
            this.gbPeptideDetails.Controls.Add(this.lblMaxFdr);
            this.gbPeptideDetails.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.gbPeptideDetails.Location = new System.Drawing.Point(3, 3);
            this.gbPeptideDetails.Name = "gbPeptideDetails";
            this.gbPeptideDetails.Size = new System.Drawing.Size(233, 108);
            this.gbPeptideDetails.TabIndex = 126;
            this.gbPeptideDetails.TabStop = false;
            this.gbPeptideDetails.Text = "Peptide-Spectrum-Match Filters";
            // 
            // lblPercentSign
            // 
            this.lblPercentSign.AutoSize = true;
            this.lblPercentSign.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblPercentSign.Location = new System.Drawing.Point(214, 24);
            this.lblPercentSign.Margin = new System.Windows.Forms.Padding(0);
            this.lblPercentSign.Name = "lblPercentSign";
            this.lblPercentSign.Size = new System.Drawing.Size(18, 13);
            this.lblPercentSign.TabIndex = 129;
            this.lblPercentSign.Text = "%";
            // 
            // minPeptideLengthTextBox
            // 
            this.minPeptideLengthTextBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.minPeptideLengthTextBox.Enabled = false;
            this.minPeptideLengthTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.minPeptideLengthTextBox.Location = new System.Drawing.Point(167, 75);
            this.minPeptideLengthTextBox.Name = "minPeptideLengthTextBox";
            this.minPeptideLengthTextBox.Size = new System.Drawing.Size(45, 21);
            this.minPeptideLengthTextBox.TabIndex = 6;
            this.minPeptideLengthTextBox.TextChanged += new System.EventHandler(this.filterControl_TextChanged);
            this.minPeptideLengthTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
            // 
            // maxAmbiguousIdsTextBox
            // 
            this.maxAmbiguousIdsTextBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.maxAmbiguousIdsTextBox.Enabled = false;
            this.maxAmbiguousIdsTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.maxAmbiguousIdsTextBox.Location = new System.Drawing.Point(167, 48);
            this.maxAmbiguousIdsTextBox.Name = "maxAmbiguousIdsTextBox";
            this.maxAmbiguousIdsTextBox.Size = new System.Drawing.Size(45, 21);
            this.maxAmbiguousIdsTextBox.TabIndex = 5;
            this.maxAmbiguousIdsTextBox.TextChanged += new System.EventHandler(this.filterControl_TextChanged);
            this.maxAmbiguousIdsTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.integerTextBox_KeyDown);
            // 
            // lblMinPeptideLength
            // 
            this.lblMinPeptideLength.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblMinPeptideLength.AutoSize = true;
            this.lblMinPeptideLength.Enabled = false;
            this.lblMinPeptideLength.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblMinPeptideLength.Location = new System.Drawing.Point(14, 78);
            this.lblMinPeptideLength.Name = "lblMinPeptideLength";
            this.lblMinPeptideLength.Size = new System.Drawing.Size(123, 13);
            this.lblMinPeptideLength.TabIndex = 127;
            this.lblMinPeptideLength.Text = "Minimum peptide length:";
            // 
            // lblMaxAmbigIds
            // 
            this.lblMaxAmbigIds.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblMaxAmbigIds.AutoSize = true;
            this.lblMaxAmbigIds.Enabled = false;
            this.lblMaxAmbigIds.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblMaxAmbigIds.Location = new System.Drawing.Point(14, 51);
            this.lblMaxAmbigIds.Name = "lblMaxAmbigIds";
            this.lblMaxAmbigIds.Size = new System.Drawing.Size(125, 13);
            this.lblMaxAmbigIds.TabIndex = 122;
            this.lblMaxAmbigIds.Text = "Maximum ambiguous ids:";
            // 
            // maxQValueComboBox
            // 
            this.maxQValueComboBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.maxQValueComboBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
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
            // lblMaxFdr
            // 
            this.lblMaxFdr.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblMaxFdr.AutoSize = true;
            this.lblMaxFdr.BackColor = System.Drawing.Color.Transparent;
            this.lblMaxFdr.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
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
            this.panel1.Location = new System.Drawing.Point(3, 223);
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
            this.Controls.Add(this.gbPeptideDetails);
            this.Controls.Add(this.gbProteinDetails);
            this.Controls.Add(this.panel1);
            this.Name = "BasicFilterControl";
            this.Size = new System.Drawing.Size(239, 247);
            this.gbProteinDetails.ResumeLayout(false);
            this.gbProteinDetails.PerformLayout();
            this.gbPeptideDetails.ResumeLayout(false);
            this.gbPeptideDetails.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox gbProteinDetails;
        private System.Windows.Forms.TextBox minSpectraTextBox;
        private System.Windows.Forms.Label lblMinSpectraPerProtein;
        private System.Windows.Forms.Label lblParsimonyVariable;
        private System.Windows.Forms.TextBox minAdditionalPeptidesTextBox;
        private System.Windows.Forms.TextBox minDistinctPeptidesTextBox;
        private System.Windows.Forms.GroupBox gbPeptideDetails;
        private System.Windows.Forms.Label lblPercentSign;
        private System.Windows.Forms.TextBox minPeptideLengthTextBox;
        private System.Windows.Forms.TextBox maxAmbiguousIdsTextBox;
        private System.Windows.Forms.Label lblMinPeptideLength;
        private System.Windows.Forms.Label lblMaxAmbigIds;
        private System.Windows.Forms.ComboBox maxQValueComboBox;
        private System.Windows.Forms.Label lblMaxFdr;
        private System.Windows.Forms.LinkLabel CloseLabel;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.LinkLabel QonverterLabel;
    }
}
