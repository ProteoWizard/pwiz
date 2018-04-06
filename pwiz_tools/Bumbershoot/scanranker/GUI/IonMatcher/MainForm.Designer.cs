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

namespace IonMatcher
{
    partial class MainForm
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.btnRun = new System.Windows.Forms.Button();
            this.btnSpecFileBrowse = new System.Windows.Forms.Button();
            this.btnMetricsFileBrowser = new System.Windows.Forms.Button();
            this.tbSpecFile = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.tbMetricsFile = new System.Windows.Forms.TextBox();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.pepGridView = new System.Windows.Forms.DataGridView();
            this.splitContainer3 = new System.Windows.Forms.SplitContainer();
            this.splitContainer4 = new System.Windows.Forms.SplitContainer();
            this.splitContainer5 = new System.Windows.Forms.SplitContainer();
            this.gbDenovo = new System.Windows.Forms.GroupBox();
            this.tbPepNovoResult = new System.Windows.Forms.TextBox();
            this.btnRunPepNovo = new System.Windows.Forms.Button();
            this.cbUseSpectrumMZ = new System.Windows.Forms.CheckBox();
            this.cbUseSpectrumCharge = new System.Windows.Forms.CheckBox();
            this.tbPTMs = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.tbFragmentTolerance = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.tbPrecursorTolerance = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.bgRunPepNovo = new System.ComponentModel.BackgroundWorker();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pepGridView)).BeginInit();
            this.splitContainer3.Panel1.SuspendLayout();
            this.splitContainer3.Panel2.SuspendLayout();
            this.splitContainer3.SuspendLayout();
            this.splitContainer4.SuspendLayout();
            this.splitContainer5.Panel2.SuspendLayout();
            this.splitContainer5.SuspendLayout();
            this.gbDenovo.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.btnRun);
            this.splitContainer1.Panel1.Controls.Add(this.btnSpecFileBrowse);
            this.splitContainer1.Panel1.Controls.Add(this.btnMetricsFileBrowser);
            this.splitContainer1.Panel1.Controls.Add(this.tbSpecFile);
            this.splitContainer1.Panel1.Controls.Add(this.label2);
            this.splitContainer1.Panel1.Controls.Add(this.label1);
            this.splitContainer1.Panel1.Controls.Add(this.tbMetricsFile);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.splitContainer2);
            this.splitContainer1.Size = new System.Drawing.Size(897, 605);
            this.splitContainer1.SplitterDistance = 56;
            this.splitContainer1.TabIndex = 0;
            // 
            // btnRun
            // 
            this.btnRun.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRun.Location = new System.Drawing.Point(811, 9);
            this.btnRun.Name = "btnRun";
            this.btnRun.Size = new System.Drawing.Size(73, 39);
            this.btnRun.TabIndex = 7;
            this.btnRun.Text = "Run";
            this.btnRun.UseVisualStyleBackColor = true;
            this.btnRun.Click += new System.EventHandler(this.btnRun_Click);
            // 
            // btnSpecFileBrowse
            // 
            this.btnSpecFileBrowse.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSpecFileBrowse.Location = new System.Drawing.Point(733, 5);
            this.btnSpecFileBrowse.Name = "btnSpecFileBrowse";
            this.btnSpecFileBrowse.Size = new System.Drawing.Size(73, 21);
            this.btnSpecFileBrowse.TabIndex = 4;
            this.btnSpecFileBrowse.Text = "Browse";
            this.btnSpecFileBrowse.UseVisualStyleBackColor = true;
            this.btnSpecFileBrowse.Click += new System.EventHandler(this.btnSpecFileBrowse_Click);
            // 
            // btnMetricsFileBrowser
            // 
            this.btnMetricsFileBrowser.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnMetricsFileBrowser.Location = new System.Drawing.Point(733, 31);
            this.btnMetricsFileBrowser.Name = "btnMetricsFileBrowser";
            this.btnMetricsFileBrowser.Size = new System.Drawing.Size(73, 21);
            this.btnMetricsFileBrowser.TabIndex = 6;
            this.btnMetricsFileBrowser.Text = "Browse";
            this.btnMetricsFileBrowser.UseVisualStyleBackColor = true;
            this.btnMetricsFileBrowser.Click += new System.EventHandler(this.btnMetricsFileBrowser_Click);
            // 
            // tbSpecFile
            // 
            this.tbSpecFile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.tbSpecFile.Location = new System.Drawing.Point(92, 6);
            this.tbSpecFile.Name = "tbSpecFile";
            this.tbSpecFile.Size = new System.Drawing.Size(635, 20);
            this.tbSpecFile.TabIndex = 9;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 9);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(74, 13);
            this.label2.TabIndex = 1;
            this.label2.Text = "Spectrum File:";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 35);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(63, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Metrics File:";
            // 
            // tbMetricsFile
            // 
            this.tbMetricsFile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.tbMetricsFile.Location = new System.Drawing.Point(92, 32);
            this.tbMetricsFile.Name = "tbMetricsFile";
            this.tbMetricsFile.Size = new System.Drawing.Size(635, 20);
            this.tbMetricsFile.TabIndex = 11;
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.pepGridView);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.splitContainer3);
            this.splitContainer2.Size = new System.Drawing.Size(897, 545);
            this.splitContainer2.SplitterDistance = 279;
            this.splitContainer2.TabIndex = 0;
            // 
            // pepGridView
            // 
            this.pepGridView.BackgroundColor = System.Drawing.SystemColors.Control;
            this.pepGridView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.pepGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle1.NullValue = null;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.pepGridView.DefaultCellStyle = dataGridViewCellStyle1;
            this.pepGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pepGridView.Location = new System.Drawing.Point(0, 0);
            this.pepGridView.Name = "pepGridView";
            this.pepGridView.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.pepGridView.Size = new System.Drawing.Size(279, 545);
            this.pepGridView.TabIndex = 0;
            // 
            // splitContainer3
            // 
            this.splitContainer3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer3.Location = new System.Drawing.Point(0, 0);
            this.splitContainer3.Name = "splitContainer3";
            this.splitContainer3.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer3.Panel1
            // 
            this.splitContainer3.Panel1.Controls.Add(this.splitContainer4);
            // 
            // splitContainer3.Panel2
            // 
            this.splitContainer3.Panel2.Controls.Add(this.splitContainer5);
            this.splitContainer3.Size = new System.Drawing.Size(614, 545);
            this.splitContainer3.SplitterDistance = 167;
            this.splitContainer3.TabIndex = 0;
            // 
            // splitContainer4
            // 
            this.splitContainer4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer4.Location = new System.Drawing.Point(0, 0);
            this.splitContainer4.Name = "splitContainer4";
            this.splitContainer4.Size = new System.Drawing.Size(614, 167);
            this.splitContainer4.SplitterDistance = 408;
            this.splitContainer4.TabIndex = 0;
            // 
            // splitContainer5
            // 
            this.splitContainer5.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer5.Location = new System.Drawing.Point(0, 0);
            this.splitContainer5.Name = "splitContainer5";
            this.splitContainer5.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer5.Panel2
            // 
            this.splitContainer5.Panel2.Controls.Add(this.gbDenovo);
            this.splitContainer5.Size = new System.Drawing.Size(614, 374);
            this.splitContainer5.SplitterDistance = 286;
            this.splitContainer5.TabIndex = 0;
            // 
            // gbDenovo
            // 
            this.gbDenovo.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.gbDenovo.Controls.Add(this.tbPepNovoResult);
            this.gbDenovo.Controls.Add(this.btnRunPepNovo);
            this.gbDenovo.Controls.Add(this.cbUseSpectrumMZ);
            this.gbDenovo.Controls.Add(this.cbUseSpectrumCharge);
            this.gbDenovo.Controls.Add(this.tbPTMs);
            this.gbDenovo.Controls.Add(this.label5);
            this.gbDenovo.Controls.Add(this.tbFragmentTolerance);
            this.gbDenovo.Controls.Add(this.label4);
            this.gbDenovo.Controls.Add(this.tbPrecursorTolerance);
            this.gbDenovo.Controls.Add(this.label3);
            this.gbDenovo.Location = new System.Drawing.Point(0, 3);
            this.gbDenovo.Name = "gbDenovo";
            this.gbDenovo.Size = new System.Drawing.Size(611, 81);
            this.gbDenovo.TabIndex = 0;
            this.gbDenovo.TabStop = false;
            this.gbDenovo.Text = "de novo sequencing";
            // 
            // tbPepNovoResult
            // 
            this.tbPepNovoResult.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.tbPepNovoResult.Location = new System.Drawing.Point(304, 10);
            this.tbPepNovoResult.Multiline = true;
            this.tbPepNovoResult.Name = "tbPepNovoResult";
            this.tbPepNovoResult.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.tbPepNovoResult.Size = new System.Drawing.Size(307, 70);
            this.tbPepNovoResult.TabIndex = 1;
            // 
            // btnRunPepNovo
            // 
            this.btnRunPepNovo.Location = new System.Drawing.Point(168, 58);
            this.btnRunPepNovo.Name = "btnRunPepNovo";
            this.btnRunPepNovo.Size = new System.Drawing.Size(130, 23);
            this.btnRunPepNovo.TabIndex = 8;
            this.btnRunPepNovo.Text = "Run PepNovo";
            this.btnRunPepNovo.UseVisualStyleBackColor = true;
            this.btnRunPepNovo.Click += new System.EventHandler(this.btnRunPepNovo_Click);
            // 
            // cbUseSpectrumMZ
            // 
            this.cbUseSpectrumMZ.AutoSize = true;
            this.cbUseSpectrumMZ.Location = new System.Drawing.Point(168, 41);
            this.cbUseSpectrumMZ.Name = "cbUseSpectrumMZ";
            this.cbUseSpectrumMZ.Size = new System.Drawing.Size(112, 17);
            this.cbUseSpectrumMZ.TabIndex = 7;
            this.cbUseSpectrumMZ.Text = "Use Spectrum MZ";
            this.cbUseSpectrumMZ.UseVisualStyleBackColor = true;
            // 
            // cbUseSpectrumCharge
            // 
            this.cbUseSpectrumCharge.AutoSize = true;
            this.cbUseSpectrumCharge.Location = new System.Drawing.Point(168, 20);
            this.cbUseSpectrumCharge.Name = "cbUseSpectrumCharge";
            this.cbUseSpectrumCharge.Size = new System.Drawing.Size(130, 17);
            this.cbUseSpectrumCharge.TabIndex = 6;
            this.cbUseSpectrumCharge.Text = "Use Spectrum Charge";
            this.cbUseSpectrumCharge.UseVisualStyleBackColor = true;
            // 
            // tbPTMs
            // 
            this.tbPTMs.Location = new System.Drawing.Point(50, 60);
            this.tbPTMs.Name = "tbPTMs";
            this.tbPTMs.Size = new System.Drawing.Size(100, 20);
            this.tbPTMs.TabIndex = 5;
            this.tbPTMs.Text = "C+57:M+16";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(6, 63);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(38, 13);
            this.label5.TabIndex = 4;
            this.label5.Text = "PTMs:";
            // 
            // tbFragmentTolerance
            // 
            this.tbFragmentTolerance.Location = new System.Drawing.Point(113, 39);
            this.tbFragmentTolerance.Name = "tbFragmentTolerance";
            this.tbFragmentTolerance.Size = new System.Drawing.Size(37, 20);
            this.tbFragmentTolerance.TabIndex = 3;
            this.tbFragmentTolerance.Text = "0.4";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(6, 42);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(105, 13);
            this.label4.TabIndex = 2;
            this.label4.Text = "Fragment Tolerance:";
            // 
            // tbPrecursorTolerance
            // 
            this.tbPrecursorTolerance.Location = new System.Drawing.Point(113, 18);
            this.tbPrecursorTolerance.Name = "tbPrecursorTolerance";
            this.tbPrecursorTolerance.Size = new System.Drawing.Size(37, 20);
            this.tbPrecursorTolerance.TabIndex = 1;
            this.tbPrecursorTolerance.Text = "0.02";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(6, 21);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(106, 13);
            this.label3.TabIndex = 0;
            this.label3.Text = "Precursor Tolerance:";
            // 
            // bgRunPepNovo
            // 
            this.bgRunPepNovo.DoWork += new System.ComponentModel.DoWorkEventHandler(this.bgRunPepNovo_DoWork);
            this.bgRunPepNovo.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.bgRunPepNovo_RunWorkerCompleted);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(897, 605);
            this.Controls.Add(this.splitContainer1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "MainForm";
            this.Text = "IonMatcher";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.ResumeLayout(false);
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            this.splitContainer2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pepGridView)).EndInit();
            this.splitContainer3.Panel1.ResumeLayout(false);
            this.splitContainer3.Panel2.ResumeLayout(false);
            this.splitContainer3.ResumeLayout(false);
            this.splitContainer4.ResumeLayout(false);
            this.splitContainer5.Panel2.ResumeLayout(false);
            this.splitContainer5.ResumeLayout(false);
            this.gbDenovo.ResumeLayout(false);
            this.gbDenovo.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.SplitContainer splitContainer3;
        private System.Windows.Forms.DataGridView pepGridView;
        private System.Windows.Forms.SplitContainer splitContainer4;
        private System.Windows.Forms.Button btnRun;
        private System.Windows.Forms.Button btnSpecFileBrowse;
        private System.Windows.Forms.Button btnMetricsFileBrowser;
        private System.Windows.Forms.TextBox tbSpecFile;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbMetricsFile;
        private System.Windows.Forms.SplitContainer splitContainer5;
        private System.Windows.Forms.GroupBox gbDenovo;
        private System.Windows.Forms.CheckBox cbUseSpectrumMZ;
        private System.Windows.Forms.CheckBox cbUseSpectrumCharge;
        private System.Windows.Forms.TextBox tbPTMs;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox tbFragmentTolerance;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox tbPrecursorTolerance;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btnRunPepNovo;
        private System.Windows.Forms.TextBox tbPepNovoResult;
        public System.ComponentModel.BackgroundWorker bgRunPepNovo;
       


    }
}

