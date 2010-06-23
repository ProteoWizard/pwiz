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
// The Original Code is the IDPicker suite.
//
// The Initial Developer of the Original Code is Mike Litton.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Matt Chambers
//

namespace IdPickerGui
{
    partial class ToolsOptionsForm
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
            System.Windows.Forms.Button btnBrowseDestDir;
            this.lblDecoyPrefix = new System.Windows.Forms.Label();
            this.cmWhatsThis = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.whatsThisToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tbDecoyPrefix = new System.Windows.Forms.TextBox();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.gbDefaultSettings = new System.Windows.Forms.GroupBox();
            this.lblSourceExtensions = new System.Windows.Forms.Label();
            this.btnResetResultsDir = new System.Windows.Forms.Button();
            this.tbSourceExtensions = new System.Windows.Forms.TextBox();
            this.lblDestDir = new System.Windows.Forms.Label();
            this.tbResultsDir = new System.Windows.Forms.TextBox();
            this.btnAddRelative = new System.Windows.Forms.Button();
            this.tabSearchPaths = new System.Windows.Forms.TabControl();
            this.tabFastaSearchPaths = new System.Windows.Forms.TabPage();
            this.label3 = new System.Windows.Forms.Label();
            this.lbFastaPaths = new System.Windows.Forms.ListBox();
            this.tabSourceSearchPaths = new System.Windows.Forms.TabPage();
            this.label1 = new System.Windows.Forms.Label();
            this.lbSourcePaths = new System.Windows.Forms.ListBox();
            this.tabSearchFilesPaths = new System.Windows.Forms.TabPage();
            this.label2 = new System.Windows.Forms.Label();
            this.lbSearchPaths = new System.Windows.Forms.ListBox();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.btnRemove = new System.Windows.Forms.Button();
            this.btnClear = new System.Windows.Forms.Button();
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.gbSearchPaths = new System.Windows.Forms.GroupBox();
            btnBrowseDestDir = new System.Windows.Forms.Button();
            this.cmWhatsThis.SuspendLayout();
            this.gbDefaultSettings.SuspendLayout();
            this.tabSearchPaths.SuspendLayout();
            this.tabFastaSearchPaths.SuspendLayout();
            this.tabSourceSearchPaths.SuspendLayout();
            this.tabSearchFilesPaths.SuspendLayout();
            this.gbSearchPaths.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnBrowseDestDir
            // 
            btnBrowseDestDir.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            btnBrowseDestDir.Location = new System.Drawing.Point(324, 42);
            btnBrowseDestDir.Name = "btnBrowseDestDir";
            btnBrowseDestDir.Size = new System.Drawing.Size(28, 20);
            btnBrowseDestDir.TabIndex = 104;
            btnBrowseDestDir.Text = "...";
            btnBrowseDestDir.UseVisualStyleBackColor = true;
            btnBrowseDestDir.Click += new System.EventHandler(this.btnBrowseDestDir_Click);
            // 
            // lblDecoyPrefix
            // 
            this.lblDecoyPrefix.AutoSize = true;
            this.lblDecoyPrefix.ContextMenuStrip = this.cmWhatsThis;
            this.lblDecoyPrefix.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblDecoyPrefix.Location = new System.Drawing.Point(14, 74);
            this.lblDecoyPrefix.Name = "lblDecoyPrefix";
            this.lblDecoyPrefix.Size = new System.Drawing.Size(72, 13);
            this.lblDecoyPrefix.TabIndex = 44;
            this.lblDecoyPrefix.Text = "Decoy prefix:";
            // 
            // cmWhatsThis
            // 
            this.cmWhatsThis.DropShadowEnabled = false;
            this.cmWhatsThis.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cmWhatsThis.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.whatsThisToolStripMenuItem});
            this.cmWhatsThis.Name = "cmWhatsThis";
            this.cmWhatsThis.Size = new System.Drawing.Size(144, 26);
            this.cmWhatsThis.Click += new System.EventHandler(this.cmWhatsThis_Click);
            // 
            // whatsThisToolStripMenuItem
            // 
            this.whatsThisToolStripMenuItem.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.whatsThisToolStripMenuItem.Name = "whatsThisToolStripMenuItem";
            this.whatsThisToolStripMenuItem.Size = new System.Drawing.Size(143, 22);
            this.whatsThisToolStripMenuItem.Text = "What\'s this?";
            // 
            // tbDecoyPrefix
            // 
            this.tbDecoyPrefix.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tbDecoyPrefix.Location = new System.Drawing.Point(17, 90);
            this.tbDecoyPrefix.Name = "tbDecoyPrefix";
            this.tbDecoyPrefix.Size = new System.Drawing.Size(301, 21);
            this.tbDecoyPrefix.TabIndex = 45;
            // 
            // btnOk
            // 
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOk.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnOk.Location = new System.Drawing.Point(358, 455);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 54;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnCancel.Location = new System.Drawing.Point(277, 455);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 55;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // gbDefaultSettings
            // 
            this.gbDefaultSettings.BackColor = System.Drawing.SystemColors.Control;
            this.gbDefaultSettings.ContextMenuStrip = this.cmWhatsThis;
            this.gbDefaultSettings.Controls.Add(this.lblSourceExtensions);
            this.gbDefaultSettings.Controls.Add(this.btnResetResultsDir);
            this.gbDefaultSettings.Controls.Add(this.tbSourceExtensions);
            this.gbDefaultSettings.Controls.Add(this.lblDestDir);
            this.gbDefaultSettings.Controls.Add(btnBrowseDestDir);
            this.gbDefaultSettings.Controls.Add(this.tbResultsDir);
            this.gbDefaultSettings.Controls.Add(this.tbDecoyPrefix);
            this.gbDefaultSettings.Controls.Add(this.lblDecoyPrefix);
            this.gbDefaultSettings.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.gbDefaultSettings.Location = new System.Drawing.Point(12, 12);
            this.gbDefaultSettings.Name = "gbDefaultSettings";
            this.gbDefaultSettings.Size = new System.Drawing.Size(421, 182);
            this.gbDefaultSettings.TabIndex = 56;
            this.gbDefaultSettings.TabStop = false;
            this.gbDefaultSettings.Text = "Default Settings";
            // 
            // lblSourceExtensions
            // 
            this.lblSourceExtensions.AutoSize = true;
            this.lblSourceExtensions.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblSourceExtensions.Location = new System.Drawing.Point(14, 126);
            this.lblSourceExtensions.Name = "lblSourceExtensions";
            this.lblSourceExtensions.Size = new System.Drawing.Size(99, 13);
            this.lblSourceExtensions.TabIndex = 109;
            this.lblSourceExtensions.Text = "Source extensions:";
            // 
            // btnResetResultsDir
            // 
            this.btnResetResultsDir.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnResetResultsDir.Location = new System.Drawing.Point(358, 42);
            this.btnResetResultsDir.Name = "btnResetResultsDir";
            this.btnResetResultsDir.Size = new System.Drawing.Size(45, 20);
            this.btnResetResultsDir.TabIndex = 106;
            this.btnResetResultsDir.Text = "Reset";
            this.btnResetResultsDir.UseVisualStyleBackColor = true;
            this.btnResetResultsDir.Click += new System.EventHandler(this.btnResetResultsDir_Click);
            // 
            // tbSourceExtensions
            // 
            this.tbSourceExtensions.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tbSourceExtensions.Location = new System.Drawing.Point(17, 142);
            this.tbSourceExtensions.Name = "tbSourceExtensions";
            this.tbSourceExtensions.Size = new System.Drawing.Size(301, 21);
            this.tbSourceExtensions.TabIndex = 108;
            // 
            // lblDestDir
            // 
            this.lblDestDir.AutoSize = true;
            this.lblDestDir.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblDestDir.Location = new System.Drawing.Point(14, 26);
            this.lblDestDir.Name = "lblDestDir";
            this.lblDestDir.Size = new System.Drawing.Size(90, 13);
            this.lblDestDir.TabIndex = 105;
            this.lblDestDir.Text = "Report directory:";
            // 
            // tbResultsDir
            // 
            this.tbResultsDir.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.tbResultsDir.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystemDirectories;
            this.tbResultsDir.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tbResultsDir.Location = new System.Drawing.Point(17, 42);
            this.tbResultsDir.Name = "tbResultsDir";
            this.tbResultsDir.Size = new System.Drawing.Size(301, 21);
            this.tbResultsDir.TabIndex = 103;
            this.tbResultsDir.Leave += new System.EventHandler(this.tbResultsDir_Leave);
            // 
            // btnAddRelative
            // 
            this.btnAddRelative.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnAddRelative.Location = new System.Drawing.Point(324, 67);
            this.btnAddRelative.Name = "btnAddRelative";
            this.btnAddRelative.Size = new System.Drawing.Size(79, 23);
            this.btnAddRelative.TabIndex = 110;
            this.btnAddRelative.Text = "Add Relative";
            this.btnAddRelative.UseVisualStyleBackColor = true;
            this.btnAddRelative.Click += new System.EventHandler(this.btnAddRelative_Click);
            // 
            // tabSearchPaths
            // 
            this.tabSearchPaths.Controls.Add(this.tabFastaSearchPaths);
            this.tabSearchPaths.Controls.Add(this.tabSourceSearchPaths);
            this.tabSearchPaths.Controls.Add(this.tabSearchFilesPaths);
            this.tabSearchPaths.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tabSearchPaths.Location = new System.Drawing.Point(17, 20);
            this.tabSearchPaths.Name = "tabSearchPaths";
            this.tabSearchPaths.SelectedIndex = 0;
            this.tabSearchPaths.Size = new System.Drawing.Size(301, 217);
            this.tabSearchPaths.TabIndex = 107;
            this.tabSearchPaths.SelectedIndexChanged += new System.EventHandler(this.lbSearchPaths_SelectedIndexChanged);
            // 
            // tabFastaSearchPaths
            // 
            this.tabFastaSearchPaths.BackColor = System.Drawing.Color.White;
            this.tabFastaSearchPaths.Controls.Add(this.label3);
            this.tabFastaSearchPaths.Controls.Add(this.lbFastaPaths);
            this.tabFastaSearchPaths.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tabFastaSearchPaths.Location = new System.Drawing.Point(4, 22);
            this.tabFastaSearchPaths.Name = "tabFastaSearchPaths";
            this.tabFastaSearchPaths.Padding = new System.Windows.Forms.Padding(3);
            this.tabFastaSearchPaths.Size = new System.Drawing.Size(293, 191);
            this.tabFastaSearchPaths.TabIndex = 0;
            this.tabFastaSearchPaths.Text = "Database";
            this.tabFastaSearchPaths.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(3, 6);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(271, 13);
            this.label3.TabIndex = 50;
            this.label3.Text = "Protein databases contain sequences in FASTA format.";
            // 
            // lbFastaPaths
            // 
            this.lbFastaPaths.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.lbFastaPaths.BackColor = System.Drawing.Color.White;
            this.lbFastaPaths.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lbFastaPaths.FormattingEnabled = true;
            this.lbFastaPaths.HorizontalScrollbar = true;
            this.lbFastaPaths.Location = new System.Drawing.Point(3, 25);
            this.lbFastaPaths.Name = "lbFastaPaths";
            this.lbFastaPaths.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.lbFastaPaths.Size = new System.Drawing.Size(286, 160);
            this.lbFastaPaths.TabIndex = 49;
            // 
            // tabSourceSearchPaths
            // 
            this.tabSourceSearchPaths.BackColor = System.Drawing.Color.White;
            this.tabSourceSearchPaths.Controls.Add(this.label1);
            this.tabSourceSearchPaths.Controls.Add(this.lbSourcePaths);
            this.tabSourceSearchPaths.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tabSourceSearchPaths.Location = new System.Drawing.Point(4, 22);
            this.tabSourceSearchPaths.Name = "tabSourceSearchPaths";
            this.tabSourceSearchPaths.Padding = new System.Windows.Forms.Padding(3);
            this.tabSourceSearchPaths.Size = new System.Drawing.Size(293, 191);
            this.tabSourceSearchPaths.TabIndex = 1;
            this.tabSourceSearchPaths.Text = "Source";
            this.tabSourceSearchPaths.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(3, 6);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(273, 13);
            this.label1.TabIndex = 50;
            this.label1.Text = "Source files contain profile or centroid data for spectra.";
            // 
            // lbSourcePaths
            // 
            this.lbSourcePaths.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.lbSourcePaths.BackColor = System.Drawing.Color.White;
            this.lbSourcePaths.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lbSourcePaths.FormattingEnabled = true;
            this.lbSourcePaths.HorizontalScrollbar = true;
            this.lbSourcePaths.Location = new System.Drawing.Point(3, 25);
            this.lbSourcePaths.Name = "lbSourcePaths";
            this.lbSourcePaths.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.lbSourcePaths.Size = new System.Drawing.Size(286, 160);
            this.lbSourcePaths.TabIndex = 49;
            // 
            // tabSearchFilesPaths
            // 
            this.tabSearchFilesPaths.BackColor = System.Drawing.Color.White;
            this.tabSearchFilesPaths.Controls.Add(this.label2);
            this.tabSearchFilesPaths.Controls.Add(this.lbSearchPaths);
            this.tabSearchFilesPaths.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tabSearchFilesPaths.Location = new System.Drawing.Point(4, 22);
            this.tabSearchFilesPaths.Name = "tabSearchFilesPaths";
            this.tabSearchFilesPaths.Padding = new System.Windows.Forms.Padding(3);
            this.tabSearchFilesPaths.Size = new System.Drawing.Size(293, 191);
            this.tabSearchFilesPaths.TabIndex = 2;
            this.tabSearchFilesPaths.Text = "Search";
            this.tabSearchFilesPaths.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(3, 6);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(260, 13);
            this.label2.TabIndex = 50;
            this.label2.Text = "Search files contain search results in pepXML format.";
            // 
            // lbSearchPaths
            // 
            this.lbSearchPaths.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.lbSearchPaths.BackColor = System.Drawing.Color.White;
            this.lbSearchPaths.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lbSearchPaths.FormattingEnabled = true;
            this.lbSearchPaths.HorizontalScrollbar = true;
            this.lbSearchPaths.Location = new System.Drawing.Point(3, 25);
            this.lbSearchPaths.Name = "lbSearchPaths";
            this.lbSearchPaths.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.lbSearchPaths.Size = new System.Drawing.Size(286, 160);
            this.lbSearchPaths.TabIndex = 49;
            // 
            // btnBrowse
            // 
            this.btnBrowse.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnBrowse.Location = new System.Drawing.Point(324, 38);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(79, 23);
            this.btnBrowse.TabIndex = 51;
            this.btnBrowse.Text = "Add Path";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // btnRemove
            // 
            this.btnRemove.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnRemove.Location = new System.Drawing.Point(324, 95);
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Size = new System.Drawing.Size(79, 23);
            this.btnRemove.TabIndex = 52;
            this.btnRemove.Text = "Remove";
            this.btnRemove.UseVisualStyleBackColor = true;
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            // 
            // btnClear
            // 
            this.btnClear.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnClear.Location = new System.Drawing.Point(324, 124);
            this.btnClear.Name = "btnClear";
            this.btnClear.Size = new System.Drawing.Size(79, 23);
            this.btnClear.TabIndex = 53;
            this.btnClear.Text = "Clear";
            this.btnClear.UseVisualStyleBackColor = true;
            this.btnClear.Click += new System.EventHandler(this.btnClear_Click);
            // 
            // listBox1
            // 
            this.listBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.listBox1.BackColor = System.Drawing.Color.White;
            this.listBox1.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.listBox1.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listBox1.FormattingEnabled = true;
            this.listBox1.Location = new System.Drawing.Point(1, 1);
            this.listBox1.Name = "listBox1";
            this.listBox1.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.listBox1.Size = new System.Drawing.Size(285, 182);
            this.listBox1.TabIndex = 49;
            // 
            // gbSearchPaths
            // 
            this.gbSearchPaths.ContextMenuStrip = this.cmWhatsThis;
            this.gbSearchPaths.Controls.Add(this.tabSearchPaths);
            this.gbSearchPaths.Controls.Add(this.btnRemove);
            this.gbSearchPaths.Controls.Add(this.btnAddRelative);
            this.gbSearchPaths.Controls.Add(this.btnClear);
            this.gbSearchPaths.Controls.Add(this.btnBrowse);
            this.gbSearchPaths.Location = new System.Drawing.Point(12, 200);
            this.gbSearchPaths.Name = "gbSearchPaths";
            this.gbSearchPaths.Size = new System.Drawing.Size(421, 249);
            this.gbSearchPaths.TabIndex = 111;
            this.gbSearchPaths.TabStop = false;
            this.gbSearchPaths.Text = "Search Paths";
            // 
            // ToolsOptionsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(444, 486);
            this.Controls.Add(this.gbSearchPaths);
            this.Controls.Add(this.gbDefaultSettings);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ToolsOptionsForm";
            this.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Options";
            this.Load += new System.EventHandler(this.ToolsOptionsForm_Load);
            this.cmWhatsThis.ResumeLayout(false);
            this.gbDefaultSettings.ResumeLayout(false);
            this.gbDefaultSettings.PerformLayout();
            this.tabSearchPaths.ResumeLayout(false);
            this.tabFastaSearchPaths.ResumeLayout(false);
            this.tabFastaSearchPaths.PerformLayout();
            this.tabSourceSearchPaths.ResumeLayout(false);
            this.tabSourceSearchPaths.PerformLayout();
            this.tabSearchFilesPaths.ResumeLayout(false);
            this.tabSearchFilesPaths.PerformLayout();
            this.gbSearchPaths.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label lblDecoyPrefix;
        private System.Windows.Forms.TextBox tbDecoyPrefix;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.GroupBox gbDefaultSettings;
        private System.Windows.Forms.Label lblDestDir;
        private System.Windows.Forms.TextBox tbResultsDir;
        private System.Windows.Forms.Button btnResetResultsDir;
        private System.Windows.Forms.TabPage tabFastaSearchPaths;
        private System.Windows.Forms.ListBox lbFastaPaths;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Button btnRemove;
        private System.Windows.Forms.Button btnClear;
        private System.Windows.Forms.TabPage tabSourceSearchPaths;
        private System.Windows.Forms.TabPage tabSearchFilesPaths;
        private System.Windows.Forms.TabControl tabSearchPaths;
        private System.Windows.Forms.ListBox lbSourcePaths;
        private System.Windows.Forms.ListBox lbSearchPaths;
        private System.Windows.Forms.ListBox listBox1;
        private System.Windows.Forms.Label lblSourceExtensions;
        private System.Windows.Forms.TextBox tbSourceExtensions;
        public System.Windows.Forms.ContextMenuStrip cmWhatsThis;
        private System.Windows.Forms.ToolStripMenuItem whatsThisToolStripMenuItem;
        private System.Windows.Forms.Button btnAddRelative;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.GroupBox gbSearchPaths;
    }
}
