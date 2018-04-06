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
// The Initial Developer of the DirecTag peptide sequence tagger is Matt Chambers.
// Contributor(s): Surendra Dasaris
//
// The Initial Developer of the ScanRanker GUI is Zeqiang Ma.
// Contributor(s): 
//
// Copyright 2009 Vanderbilt University
//

namespace ScanRanker
{
    partial class IDPickerConfigForm
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
            this.lblPepXMLFile = new System.Windows.Forms.Label();
            this.tbPepXMLFile = new System.Windows.Forms.TextBox();
            this.btnBrowsePepXML = new System.Windows.Forms.Button();
            this.lblDatabaseFile = new System.Windows.Forms.Label();
            this.tbDBfile = new System.Windows.Forms.TextBox();
            this.btnBrowseDB = new System.Windows.Forms.Button();
            this.lblMaxFDR = new System.Windows.Forms.Label();
            this.lblSearchScoreWeights = new System.Windows.Forms.Label();
            this.cbNormSearchScores = new System.Windows.Forms.CheckBox();
            this.cbOptimizeScoreWeights = new System.Windows.Forms.CheckBox();
            this.tbMaxFDR = new System.Windows.Forms.TextBox();
            this.cmbScoreWeights = new System.Windows.Forms.ComboBox();
            this.lblDecoyPrefix = new System.Windows.Forms.Label();
            this.tbDecoyPrefix = new System.Windows.Forms.TextBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lblPepXMLFile
            // 
            this.lblPepXMLFile.AutoSize = true;
            this.lblPepXMLFile.Location = new System.Drawing.Point(12, 30);
            this.lblPepXMLFile.Name = "lblPepXMLFile";
            this.lblPepXMLFile.Size = new System.Drawing.Size(69, 13);
            this.lblPepXMLFile.TabIndex = 0;
            this.lblPepXMLFile.Text = "pepXML File:";
            // 
            // tbPepXMLFile
            // 
            this.tbPepXMLFile.Location = new System.Drawing.Point(100, 27);
            this.tbPepXMLFile.Name = "tbPepXMLFile";
            this.tbPepXMLFile.Size = new System.Drawing.Size(333, 20);
            this.tbPepXMLFile.TabIndex = 1;
            // 
            // btnBrowsePepXML
            // 
            this.btnBrowsePepXML.Location = new System.Drawing.Point(450, 27);
            this.btnBrowsePepXML.Name = "btnBrowsePepXML";
            this.btnBrowsePepXML.Size = new System.Drawing.Size(64, 21);
            this.btnBrowsePepXML.TabIndex = 2;
            this.btnBrowsePepXML.Text = "Browse";
            this.btnBrowsePepXML.UseVisualStyleBackColor = true;
            this.btnBrowsePepXML.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // lblDatabaseFile
            // 
            this.lblDatabaseFile.AutoSize = true;
            this.lblDatabaseFile.Location = new System.Drawing.Point(12, 75);
            this.lblDatabaseFile.Name = "lblDatabaseFile";
            this.lblDatabaseFile.Size = new System.Drawing.Size(75, 13);
            this.lblDatabaseFile.TabIndex = 3;
            this.lblDatabaseFile.Text = "Database File:";
            // 
            // tbDBfile
            // 
            this.tbDBfile.Location = new System.Drawing.Point(100, 72);
            this.tbDBfile.Name = "tbDBfile";
            this.tbDBfile.Size = new System.Drawing.Size(333, 20);
            this.tbDBfile.TabIndex = 4;
            // 
            // btnBrowseDB
            // 
            this.btnBrowseDB.Location = new System.Drawing.Point(450, 72);
            this.btnBrowseDB.Name = "btnBrowseDB";
            this.btnBrowseDB.Size = new System.Drawing.Size(64, 21);
            this.btnBrowseDB.TabIndex = 5;
            this.btnBrowseDB.Text = "Browse";
            this.btnBrowseDB.UseVisualStyleBackColor = true;
            this.btnBrowseDB.Click += new System.EventHandler(this.btnBrowseDB_Click);
            // 
            // lblMaxFDR
            // 
            this.lblMaxFDR.AutoSize = true;
            this.lblMaxFDR.Location = new System.Drawing.Point(314, 120);
            this.lblMaxFDR.Name = "lblMaxFDR";
            this.lblMaxFDR.Size = new System.Drawing.Size(55, 13);
            this.lblMaxFDR.TabIndex = 6;
            this.lblMaxFDR.Text = "Max FDR:";
            // 
            // lblSearchScoreWeights
            // 
            this.lblSearchScoreWeights.AutoSize = true;
            this.lblSearchScoreWeights.Location = new System.Drawing.Point(12, 164);
            this.lblSearchScoreWeights.Name = "lblSearchScoreWeights";
            this.lblSearchScoreWeights.Size = new System.Drawing.Size(169, 13);
            this.lblSearchScoreWeights.TabIndex = 8;
            this.lblSearchScoreWeights.Text = "Database Search Score Weights: ";
            // 
            // cbNormSearchScores
            // 
            this.cbNormSearchScores.AutoSize = true;
            this.cbNormSearchScores.Checked = true;
            this.cbNormSearchScores.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbNormSearchScores.Location = new System.Drawing.Point(317, 160);
            this.cbNormSearchScores.Name = "cbNormSearchScores";
            this.cbNormSearchScores.Size = new System.Drawing.Size(148, 17);
            this.cbNormSearchScores.TabIndex = 9;
            this.cbNormSearchScores.Text = "Normalize Search Scores ";
            this.cbNormSearchScores.UseVisualStyleBackColor = true;
            // 
            // cbOptimizeScoreWeights
            // 
            this.cbOptimizeScoreWeights.AutoSize = true;
            this.cbOptimizeScoreWeights.Checked = true;
            this.cbOptimizeScoreWeights.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbOptimizeScoreWeights.Location = new System.Drawing.Point(317, 182);
            this.cbOptimizeScoreWeights.Name = "cbOptimizeScoreWeights";
            this.cbOptimizeScoreWeights.Size = new System.Drawing.Size(142, 17);
            this.cbOptimizeScoreWeights.TabIndex = 10;
            this.cbOptimizeScoreWeights.Text = "Optimize Score Weights ";
            this.cbOptimizeScoreWeights.UseVisualStyleBackColor = true;
            // 
            // tbMaxFDR
            // 
            this.tbMaxFDR.Location = new System.Drawing.Point(387, 117);
            this.tbMaxFDR.Name = "tbMaxFDR";
            this.tbMaxFDR.Size = new System.Drawing.Size(55, 20);
            this.tbMaxFDR.TabIndex = 11;
            this.tbMaxFDR.Text = "0.02";
            // 
            // cmbScoreWeights
            // 
            this.cmbScoreWeights.FormattingEnabled = true;
            this.cmbScoreWeights.Items.AddRange(new object[] {
            "mvh 1 mzFidelity 1",
            "xcorr 1 deltacn 1",
            "hyperscore 1 expect -1",
            "ionscore 1 identityscore -1"});
            this.cmbScoreWeights.Location = new System.Drawing.Point(15, 180);
            this.cmbScoreWeights.Name = "cmbScoreWeights";
            this.cmbScoreWeights.Size = new System.Drawing.Size(244, 21);
            this.cmbScoreWeights.TabIndex = 12;
            // 
            // lblDecoyPrefix
            // 
            this.lblDecoyPrefix.AutoSize = true;
            this.lblDecoyPrefix.Location = new System.Drawing.Point(12, 120);
            this.lblDecoyPrefix.Name = "lblDecoyPrefix";
            this.lblDecoyPrefix.Size = new System.Drawing.Size(70, 13);
            this.lblDecoyPrefix.TabIndex = 14;
            this.lblDecoyPrefix.Text = "Decoy Prefix:";
            // 
            // tbDecoyPrefix
            // 
            this.tbDecoyPrefix.Location = new System.Drawing.Point(100, 117);
            this.tbDecoyPrefix.Name = "tbDecoyPrefix";
            this.tbDecoyPrefix.Size = new System.Drawing.Size(55, 20);
            this.tbDecoyPrefix.TabIndex = 15;
            this.tbDecoyPrefix.Text = "rev_";
            // 
            // btnOK
            // 
            this.btnOK.Location = new System.Drawing.Point(340, 222);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(80, 23);
            this.btnOK.TabIndex = 16;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(434, 222);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(80, 23);
            this.btnCancel.TabIndex = 17;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // IDPickerConfigForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(534, 260);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.tbDecoyPrefix);
            this.Controls.Add(this.lblDecoyPrefix);
            this.Controls.Add(this.cmbScoreWeights);
            this.Controls.Add(this.tbMaxFDR);
            this.Controls.Add(this.cbOptimizeScoreWeights);
            this.Controls.Add(this.cbNormSearchScores);
            this.Controls.Add(this.lblSearchScoreWeights);
            this.Controls.Add(this.lblMaxFDR);
            this.Controls.Add(this.btnBrowseDB);
            this.Controls.Add(this.tbDBfile);
            this.Controls.Add(this.lblDatabaseFile);
            this.Controls.Add(this.btnBrowsePepXML);
            this.Controls.Add(this.tbPepXMLFile);
            this.Controls.Add(this.lblPepXMLFile);
            this.Name = "IDPickerConfigForm";
            this.Text = "IDPicker Configuration";
            this.Load += new System.EventHandler(this.IDPickerConfigForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblPepXMLFile;
        private System.Windows.Forms.TextBox tbPepXMLFile;
        private System.Windows.Forms.Button btnBrowsePepXML;
        private System.Windows.Forms.Label lblDatabaseFile;
        private System.Windows.Forms.TextBox tbDBfile;
        private System.Windows.Forms.Button btnBrowseDB;
        private System.Windows.Forms.Label lblMaxFDR;
        private System.Windows.Forms.Label lblSearchScoreWeights;
        private System.Windows.Forms.CheckBox cbNormSearchScores;
        private System.Windows.Forms.CheckBox cbOptimizeScoreWeights;
        private System.Windows.Forms.TextBox tbMaxFDR;
        private System.Windows.Forms.ComboBox cmbScoreWeights;
        private System.Windows.Forms.Label lblDecoyPrefix;
        private System.Windows.Forms.TextBox tbDecoyPrefix;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
    }
}