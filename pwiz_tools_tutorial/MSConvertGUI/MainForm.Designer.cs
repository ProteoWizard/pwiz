//
// $Id$
//
//
// Original author: Jay Holman <jay.holman .@. vanderbilt.edu>
//
// Copyright 2011 Vanderbilt University - Nashville, TN 37232
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

using System;

namespace MSConvertGUI
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

        void setToolTip(System.Windows.Forms.Control ctl, string text)
        {
            ctlToolTip.UseFading = true;
            ctlToolTip.UseAnimation = true;
            ctlToolTip.IsBalloon = true;

            ctlToolTip.ShowAlways = true;

            ctlToolTip.AutoPopDelay = 5000;
            ctlToolTip.InitialDelay = 1000;
            ctlToolTip.ReshowDelay = 500;

            ctlToolTip.SetToolTip(ctl, text);

        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.ctlToolTip = new System.Windows.Forms.ToolTip();
            this.FileBox = new System.Windows.Forms.TextBox();
            this.FileLabel = new System.Windows.Forms.Label();
            this.AddFileButton = new System.Windows.Forms.Button();
            this.FilterDGV = new System.Windows.Forms.DataGridView();
            this.OptionTab = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ValueTab = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.FileListBox = new System.Windows.Forms.ListBox();
            this.RemoveFileButton = new System.Windows.Forms.Button();
            this.StartButton = new System.Windows.Forms.Button();
            this.FilterGB = new System.Windows.Forms.GroupBox();
            this.FilterBox = new System.Windows.Forms.ComboBox();
            this.ActivationPanel = new System.Windows.Forms.Panel();
            this.ActivationTypeBox = new System.Windows.Forms.ComboBox();
            this.ActivationTypeLabel = new System.Windows.Forms.Label();
            this.SubsetPanel = new System.Windows.Forms.Panel();
            this.mzWinLabel2 = new System.Windows.Forms.Label();
            this.ScanNumberHigh = new System.Windows.Forms.TextBox();
            this.ScanTimeLow = new System.Windows.Forms.TextBox();
            this.mzWinHigh = new System.Windows.Forms.TextBox();
            this.ScanTimeHigh = new System.Windows.Forms.TextBox();
            this.ScanTimeLabel2 = new System.Windows.Forms.Label();
            this.mzWinLow = new System.Windows.Forms.TextBox();
            this.ScanTimeLabel = new System.Windows.Forms.Label();
            this.mzWinLabel = new System.Windows.Forms.Label();
            this.ScanNumberLabel = new System.Windows.Forms.Label();
            this.ScanNumberLow = new System.Windows.Forms.TextBox();
            this.ScanNumberLabel2 = new System.Windows.Forms.Label();
            this.MSLevelPanel = new System.Windows.Forms.Panel();
            this.MSLevelLabel = new System.Windows.Forms.Label();
            this.MSLabelSeperator = new System.Windows.Forms.Label();
            this.MSLevelBox2 = new System.Windows.Forms.TextBox();
            this.MSLevelBox1 = new System.Windows.Forms.TextBox();
            this.PeakPickingPanel = new System.Windows.Forms.Panel();
            this.PeakPreferVendorBox = new System.Windows.Forms.CheckBox();
            this.PeakMSLevelLabel = new System.Windows.Forms.Label();
            this.PeakMSLevelSeperator = new System.Windows.Forms.Label();
            this.PeakMSLevelBox2 = new System.Windows.Forms.TextBox();
            this.PeakMSLevelBox1 = new System.Windows.Forms.TextBox();
            this.ZeroSamplesPanel = new System.Windows.Forms.Panel();
            this.ZeroSamplesRemove = new System.Windows.Forms.RadioButton();
            this.ZeroSamplesAddMissing = new System.Windows.Forms.RadioButton();
            this.ZeroSamplesAddMissingFlankCountBox = new System.Windows.Forms.TextBox();
            this.ZeroSamplesMSLevelLabel = new System.Windows.Forms.Label();
            this.ZeroSamplesMSLevelSeperator = new System.Windows.Forms.Label();
            this.ZeroSamplesMSLevelBox2 = new System.Windows.Forms.TextBox();
            this.ZeroSamplesMSLevelBox1 = new System.Windows.Forms.TextBox();
            this.ETDFilterPanel = new System.Windows.Forms.Panel();
            this.ETDBlanketRemovalBox = new System.Windows.Forms.CheckBox();
            this.ETDRemoveChargeReducedBox = new System.Windows.Forms.CheckBox();
            this.ETDRemoveNeutralLossBox = new System.Windows.Forms.CheckBox();
            this.ETDRemovePrecursorBox = new System.Windows.Forms.CheckBox();
            this.ChargeStatePredictorPanel = new System.Windows.Forms.Panel();
            this.ChaMCMaxLabel = new System.Windows.Forms.Label();
            this.ChaMCMaxBox = new System.Windows.Forms.TextBox();
            this.ChaMCMinBox = new System.Windows.Forms.TextBox();
            this.ChaMCMinLabel = new System.Windows.Forms.Label();
            this.ChaSingleBox = new System.Windows.Forms.NumericUpDown();
            this.ChaSingleLabel = new System.Windows.Forms.Label();
            this.ChaOverwriteCharge = new System.Windows.Forms.CheckBox();
            this.RemoveFilterButton = new System.Windows.Forms.Button();
            this.AddFilterButton = new System.Windows.Forms.Button();
            this.TextFileRadio = new System.Windows.Forms.RadioButton();
            this.FileListRadio = new System.Windows.Forms.RadioButton();
            this.OutputFormatBox = new System.Windows.Forms.ComboBox();
            this.FormatLabel = new System.Windows.Forms.Label();
            this.BrowseFileButton = new System.Windows.Forms.Button();
            this.OutputBrowse = new System.Windows.Forms.Button();
            this.OutputLabel = new System.Windows.Forms.Label();
            this.OutputBox = new System.Windows.Forms.TextBox();
            this.PrecisionLabel = new System.Windows.Forms.Label();
            this.OptionsGB = new System.Windows.Forms.GroupBox();
            this.ConfigurationFileGB = new System.Windows.Forms.GroupBox();
            this.BrowseConfigButton = new System.Windows.Forms.Button();
            this.ConfigBox = new System.Windows.Forms.TextBox();
            this.UseCFGButton = new System.Windows.Forms.CheckBox();
            this.UseZlibBox = new System.Windows.Forms.CheckBox();
            this.GzipBox = new System.Windows.Forms.CheckBox();
            this.OutputExtensionBox = new System.Windows.Forms.TextBox();
            this.OutputExtensionLabel = new System.Windows.Forms.Label();
            this.WriteIndexBox = new System.Windows.Forms.CheckBox();
            this.Precision32 = new System.Windows.Forms.RadioButton();
            this.Precision64 = new System.Windows.Forms.RadioButton();
            this.SlidingPanel = new System.Windows.Forms.Panel();
            this.SetDefaultsButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize) (this.FilterDGV)).BeginInit();
            this.FilterGB.SuspendLayout();
            this.ActivationPanel.SuspendLayout();
            this.SubsetPanel.SuspendLayout();
            this.MSLevelPanel.SuspendLayout();
            this.PeakPickingPanel.SuspendLayout();
            this.ZeroSamplesPanel.SuspendLayout();
            this.ZeroSamplesRemove = new System.Windows.Forms.RadioButton();
            this.ZeroSamplesAddMissing = new System.Windows.Forms.RadioButton();
            this.ETDFilterPanel.SuspendLayout();
            this.ChargeStatePredictorPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize) (this.ChaSingleBox)).BeginInit();
            this.OptionsGB.SuspendLayout();
            this.ConfigurationFileGB.SuspendLayout();
            this.SlidingPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // FileBox
            // 
            this.FileBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Append;
            this.FileBox.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystem;
            this.FileBox.Location = new System.Drawing.Point(43, 35);
            this.FileBox.Name = "FileBox";
            this.FileBox.Size = new System.Drawing.Size(182, 20);
            this.FileBox.TabIndex = 3;
            this.FileBox.TextChanged += new System.EventHandler(this.FileBox_TextChanged);
            setToolTip(this.FileBox,"Use the Browse button or type a filename here, then click Add to add it to the list of files to be converted.");
            // 
            // FileLabel
            // 
            this.FileLabel.AutoSize = true;
            this.FileLabel.Location = new System.Drawing.Point(12, 38);
            this.FileLabel.Name = "FileLabel";
            this.FileLabel.Size = new System.Drawing.Size(26, 13);
            this.FileLabel.TabIndex = 1;
            this.FileLabel.Text = "File:";
            // 
            // AddFileButton
            // 
            this.AddFileButton.Enabled = false;
            this.AddFileButton.Location = new System.Drawing.Point(92, 61);
            this.AddFileButton.Name = "AddFileButton";
            this.AddFileButton.Size = new System.Drawing.Size(42, 23);
            this.AddFileButton.TabIndex = 5;
            this.AddFileButton.Text = "Add";
            this.AddFileButton.UseVisualStyleBackColor = true;
            this.AddFileButton.Click += new System.EventHandler(this.AddFileButton_Click);
            setToolTip(this.AddFileButton, "Adds the current file to the conversion list.");
            // 
            // FilterDGV
            // 
            this.FilterDGV.AllowUserToAddRows = false;
            this.FilterDGV.AllowUserToDeleteRows = false;
            this.FilterDGV.AllowUserToResizeRows = false;
            this.FilterDGV.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.FilterDGV.BackgroundColor = System.Drawing.SystemColors.Window;
            this.FilterDGV.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.FilterDGV.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.FilterDGV.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.OptionTab,
            this.ValueTab});
            for (int col = 0; col < this.FilterDGV.Columns.Count; col++)
            {
                this.FilterDGV.Columns[col].SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            }
            this.FilterDGV.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.FilterDGV.Location = new System.Drawing.Point(322, 261);
            this.FilterDGV.MultiSelect = false;
            this.FilterDGV.Name = "FilterDGV";
            this.FilterDGV.RowHeadersVisible = false;
            this.FilterDGV.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.FilterDGV.Size = new System.Drawing.Size(327, 163);
            this.FilterDGV.TabIndex = 12;
            setToolTip(this.FilterDGV, "Use the controls above to add conversion filters.  The order can be significant.");
            // 
            // OptionTab
            // 
            this.OptionTab.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.OptionTab.FillWeight = 50F;
            this.OptionTab.HeaderText = "Filter";
            this.OptionTab.Name = "OptionTab";
            // 
            // ValueTab
            // 
            this.ValueTab.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.ValueTab.HeaderText = "Parameters";
            this.ValueTab.Name = "ValueTab";
            // 
            // FileListBox
            // 
            this.FileListBox.Anchor = ((System.Windows.Forms.AnchorStyles) (((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)));
            this.FileListBox.FormattingEnabled = true;
            this.FileListBox.HorizontalScrollbar = true;
            this.FileListBox.Location = new System.Drawing.Point(15, 90);
            this.FileListBox.Name = "FileListBox";
            this.FileListBox.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.FileListBox.Size = new System.Drawing.Size(266, 160);
            this.FileListBox.TabIndex = 7;
            this.FileListBox.KeyUp += new System.Windows.Forms.KeyEventHandler(this.FileListBox_KeyUp);
            setToolTip(this.FileListBox, "Add files to this conversion list by using the Browse button to select a file, then clicking the Add button.");
            // 
            // RemoveFileButton
            // 
            this.RemoveFileButton.Enabled = false;
            this.RemoveFileButton.Location = new System.Drawing.Point(140, 61);
            this.RemoveFileButton.Name = "RemoveFileButton";
            this.RemoveFileButton.Size = new System.Drawing.Size(58, 23);
            this.RemoveFileButton.TabIndex = 6;
            this.RemoveFileButton.Text = "Remove";
            this.RemoveFileButton.UseVisualStyleBackColor = true;
            this.RemoveFileButton.Click += new System.EventHandler(this.RemoveFileButton_Click);
            setToolTip(this.RemoveFileButton, "Select a file to be removed from the conversion list, then click here.");
            // 
            // StartButton
            // 
            this.StartButton.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.StartButton.Location = new System.Drawing.Point(574, 430);
            this.StartButton.Name = "StartButton";
            this.StartButton.Size = new System.Drawing.Size(75, 23);
            this.StartButton.TabIndex = 13;
            this.StartButton.Text = "Start";
            this.StartButton.UseVisualStyleBackColor = true;
            this.StartButton.Click += new System.EventHandler(this.StartButton_Click);
            setToolTip(this.StartButton, "Click here to begin the conversion process.");
            // 
            // FilterGB
            // 
            this.FilterGB.Anchor = ((System.Windows.Forms.AnchorStyles) (((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.FilterGB.Controls.Add(this.FilterBox);
            this.FilterGB.Controls.Add(this.ActivationPanel);
            this.FilterGB.Controls.Add(this.SubsetPanel);
            this.FilterGB.Controls.Add(this.MSLevelPanel);
            this.FilterGB.Controls.Add(this.PeakPickingPanel);
            this.FilterGB.Controls.Add(this.ZeroSamplesPanel);
            this.FilterGB.Controls.Add(this.ETDFilterPanel);
            this.FilterGB.Controls.Add(this.ChargeStatePredictorPanel);
            this.FilterGB.Location = new System.Drawing.Point(322, 83);
            this.FilterGB.Name = "FilterGB";
            this.FilterGB.Size = new System.Drawing.Size(327, 143);
            this.FilterGB.TabIndex = 9;
            this.FilterGB.TabStop = false;
            this.FilterGB.Text = "Filters";
            setToolTip(this.FilterGB, "Use these controls to add to the conversion filter list.");
            // 
            // FilterBox
            // 
            this.FilterBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.FilterBox.FormattingEnabled = true;
            this.FilterBox.Items.AddRange(new object[] {
            "MS Level",
            "Peak Picking",
            "Zero Samples",
            "ETD Peak Filter",
            "Charge State Predictor",
            "Activation",
            "Subset"});
            this.FilterBox.Location = new System.Drawing.Point(97, 19);
            this.FilterBox.Name = "FilterBox";
            this.FilterBox.Size = new System.Drawing.Size(132, 21);
            this.FilterBox.TabIndex = 0;
            this.FilterBox.SelectedIndexChanged += new System.EventHandler(this.FilterBox_SelectedIndexChanged);
            setToolTip(this.FilterBox, "This chooses the type of filter that you want to add next.");
            // 
            // ActivationPanel
            // 
            this.ActivationPanel.Controls.Add(this.ActivationTypeBox);
            this.ActivationPanel.Controls.Add(this.ActivationTypeLabel);
            this.ActivationPanel.Location = new System.Drawing.Point(22, 46);
            this.ActivationPanel.Name = "ActivationPanel";
            this.ActivationPanel.Size = new System.Drawing.Size(283, 91);
            this.ActivationPanel.TabIndex = 5;
            this.ActivationPanel.Visible = false;
            // 
            // ActivationTypeBox
            // 
            this.ActivationTypeBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ActivationTypeBox.FormattingEnabled = true;
            this.ActivationTypeBox.Items.AddRange(new object[] {
            "BIRD",
            "CID",
            "ECD",
            "ETD",
            "ETD+SA",
            "HCD",
            "IRMPD",
            "PD",
            "PQD",
            "PSD",
            "SID",
            "SORI"});
            this.ActivationTypeBox.Location = new System.Drawing.Point(121, 35);
            this.ActivationTypeBox.MaxDropDownItems = 16;
            this.ActivationTypeBox.Name = "ActivationTypeBox";
            this.ActivationTypeBox.Size = new System.Drawing.Size(68, 21);
            this.ActivationTypeBox.Sorted = true;
            this.ActivationTypeBox.TabIndex = 14;
            string ActivationTypeHelp = "Include only scans with this precursor activation type.";
            setToolTip(this.ActivationTypeBox, ActivationTypeHelp);
            // 
            // ActivationTypeLabel
            // 
            this.ActivationTypeLabel.AutoSize = true;
            this.ActivationTypeLabel.Location = new System.Drawing.Point(81, 37);
            this.ActivationTypeLabel.Name = "ActivationTypeLabel";
            this.ActivationTypeLabel.Size = new System.Drawing.Size(34, 13);
            this.ActivationTypeLabel.TabIndex = 15;
            this.ActivationTypeLabel.Text = "Type:";
            setToolTip(this.ActivationTypeLabel, ActivationTypeHelp);
            // 
            // SubsetPanel
            // 
            this.SubsetPanel.Controls.Add(this.mzWinLabel2);
            this.SubsetPanel.Controls.Add(this.ScanNumberHigh);
            this.SubsetPanel.Controls.Add(this.ScanTimeLow);
            this.SubsetPanel.Controls.Add(this.mzWinHigh);
            this.SubsetPanel.Controls.Add(this.ScanTimeHigh);
            this.SubsetPanel.Controls.Add(this.ScanTimeLabel2);
            this.SubsetPanel.Controls.Add(this.mzWinLow);
            this.SubsetPanel.Controls.Add(this.ScanTimeLabel);
            this.SubsetPanel.Controls.Add(this.mzWinLabel);
            this.SubsetPanel.Controls.Add(this.ScanNumberLabel);
            this.SubsetPanel.Controls.Add(this.ScanNumberLow);
            this.SubsetPanel.Controls.Add(this.ScanNumberLabel2);
            this.SubsetPanel.Location = new System.Drawing.Point(22, 46);
            this.SubsetPanel.Name = "SubsetPanel";
            this.SubsetPanel.Size = new System.Drawing.Size(283, 91);
            this.SubsetPanel.TabIndex = 6;
            this.SubsetPanel.Visible = false;
            setToolTip(this.SubsetPanel, "Set values for one or more subset filters, then click Add.");

            // 
            // mzWinLabel2
            // 
            this.mzWinLabel2.AutoSize = true;
            this.mzWinLabel2.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.mzWinLabel2.Location = new System.Drawing.Point(174, 60);
            this.mzWinLabel2.Name = "mzWinLabel2";
            this.mzWinLabel2.Size = new System.Drawing.Size(15, 20);
            this.mzWinLabel2.TabIndex = 16;
            this.mzWinLabel2.Text = "-";
            // 
            // ScanNumberHigh
            // 
            this.ScanNumberHigh.Location = new System.Drawing.Point(191, 10);
            this.ScanNumberHigh.Name = "ScanNumberHigh";
            this.ScanNumberHigh.Size = new System.Drawing.Size(37, 20);
            this.ScanNumberHigh.TabIndex = 11;
            setToolTip(this.ScanNumberHigh, "Highest scan number to include in the conversion (may be left blank).");
            // 
            // ScanTimeLow
            // 
            this.ScanTimeLow.Location = new System.Drawing.Point(135, 36);
            this.ScanTimeLow.Name = "ScanTimeLow";
            this.ScanTimeLow.Size = new System.Drawing.Size(37, 20);
            this.ScanTimeLow.TabIndex = 0;
            setToolTip(this.ScanTimeLow,"Lowest scan time to include in the conversion.");
            // 
            // mzWinHigh
            // 
            this.mzWinHigh.Location = new System.Drawing.Point(191, 62);
            this.mzWinHigh.Name = "mzWinHigh";
            this.mzWinHigh.Size = new System.Drawing.Size(37, 20);
            this.mzWinHigh.TabIndex = 15;
            setToolTip(this.mzWinHigh,"Highest m/z value to include in the conversion.");
            // 
            // ScanTimeHigh
            // 
            this.ScanTimeHigh.Location = new System.Drawing.Point(191, 36);
            this.ScanTimeHigh.Name = "ScanTimeHigh";
            this.ScanTimeHigh.Size = new System.Drawing.Size(37, 20);
            this.ScanTimeHigh.TabIndex = 1;
            setToolTip(this.ScanTimeHigh,"Highest scan time to include in the conversion.");
            // 
            // ScanTimeLabel2
            // 
            this.ScanTimeLabel2.AutoSize = true;
            this.ScanTimeLabel2.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.ScanTimeLabel2.Location = new System.Drawing.Point(174, 34);
            this.ScanTimeLabel2.Name = "ScanTimeLabel2";
            this.ScanTimeLabel2.Size = new System.Drawing.Size(15, 20);
            this.ScanTimeLabel2.TabIndex = 2;
            this.ScanTimeLabel2.Text = "-";
            // 
            // mzWinLow
            // 
            this.mzWinLow.Location = new System.Drawing.Point(135, 62);
            this.mzWinLow.Name = "mzWinLow";
            this.mzWinLow.Size = new System.Drawing.Size(37, 20);
            this.mzWinLow.TabIndex = 14;
            setToolTip(this.mzWinLow, "Lowest m/z value to include in the conversion");
            // 
            // ScanTimeLabel
            // 
            this.ScanTimeLabel.AutoSize = true;
            this.ScanTimeLabel.Location = new System.Drawing.Point(68, 41);
            this.ScanTimeLabel.Name = "ScanTimeLabel";
            this.ScanTimeLabel.Size = new System.Drawing.Size(61, 13);
            this.ScanTimeLabel.TabIndex = 3;
            this.ScanTimeLabel.Text = "Scan Time:";
            setToolTip(this.ScanTimeLabel, "Use this filter to include only scans with a limited range of scan times.");
            // 
            // mzWinLabel
            // 
            this.mzWinLabel.AutoSize = true;
            this.mzWinLabel.Location = new System.Drawing.Point(64, 67);
            this.mzWinLabel.Name = "mzWinLabel";
            this.mzWinLabel.Size = new System.Drawing.Size(65, 13);
            this.mzWinLabel.TabIndex = 6;
            this.mzWinLabel.Text = "mz Window:";
            setToolTip(this.mzWinLabel, "Use this filter to include only scans with a limited range of m/z values.");
            // 
            // ScanNumberLabel
            // 
            this.ScanNumberLabel.AutoSize = true;
            this.ScanNumberLabel.Location = new System.Drawing.Point(54, 12);
            this.ScanNumberLabel.Name = "ScanNumberLabel";
            this.ScanNumberLabel.Size = new System.Drawing.Size(75, 13);
            this.ScanNumberLabel.TabIndex = 13;
            this.ScanNumberLabel.Text = "Scan Number:";
            setToolTip(this.ScanNumberLabel, "Use this filter to include only scans with a limited range of scan numbers.");
            // 
            // ScanNumberLow
            // 
            this.ScanNumberLow.Location = new System.Drawing.Point(135, 10);
            this.ScanNumberLow.Name = "ScanNumberLow";
            this.ScanNumberLow.Size = new System.Drawing.Size(37, 20);
            this.ScanNumberLow.TabIndex = 10;
            setToolTip(this.ScanNumberLow, "Lowest scan number to include in the conversion.");
            // 
            // ScanNumberLabel2
            // 
            this.ScanNumberLabel2.AutoSize = true;
            this.ScanNumberLabel2.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.ScanNumberLabel2.Location = new System.Drawing.Point(174, 8);
            this.ScanNumberLabel2.Name = "ScanNumberLabel2";
            this.ScanNumberLabel2.Size = new System.Drawing.Size(15, 20);
            this.ScanNumberLabel2.TabIndex = 12;
            this.ScanNumberLabel2.Text = "-";
            // 
            // MSLevelPanel
            // 
            this.MSLevelPanel.Controls.Add(this.MSLevelLabel);
            this.MSLevelPanel.Controls.Add(this.MSLabelSeperator);
            this.MSLevelPanel.Controls.Add(this.MSLevelBox2);
            this.MSLevelPanel.Controls.Add(this.MSLevelBox1);
            this.MSLevelPanel.Location = new System.Drawing.Point(22, 46);
            this.MSLevelPanel.Name = "MSLevelPanel";
            this.MSLevelPanel.Size = new System.Drawing.Size(283, 91);
            this.MSLevelPanel.TabIndex = 1;
            this.MSLevelPanel.Visible = false;
            string msLevelHelp = "Use this filter to include only scans with certain MS levels.";
            setToolTip(this.MSLevelPanel, msLevelHelp);
            // 
            // MSLevelLabel
            // 
            this.MSLevelLabel.AutoSize = true;
            this.MSLevelLabel.Location = new System.Drawing.Point(121, 19);
            this.MSLevelLabel.Name = "MSLevelLabel";
            this.MSLevelLabel.Size = new System.Drawing.Size(41, 13);
            this.MSLevelLabel.TabIndex = 3;
            this.MSLevelLabel.Text = "Levels:";
            setToolTip(this.MSLevelLabel, msLevelHelp);
            // 
            // MSLabelSeperator
            // 
            this.MSLabelSeperator.AutoSize = true;
            this.MSLabelSeperator.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.MSLabelSeperator.Location = new System.Drawing.Point(134, 34);
            this.MSLabelSeperator.Name = "MSLabelSeperator";
            this.MSLabelSeperator.Size = new System.Drawing.Size(15, 20);
            this.MSLabelSeperator.TabIndex = 2;
            this.MSLabelSeperator.Text = "-";
            // 
            // MSLevelBox2
            // 
            this.MSLevelBox2.Location = new System.Drawing.Point(151, 36);
            this.MSLevelBox2.Name = "MSLevelBox2";
            this.MSLevelBox2.Size = new System.Drawing.Size(37, 20);
            this.MSLevelBox2.TabIndex = 1;
            this.MSLevelBox2.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumTextBox_KeyPress);
            setToolTip(this.MSLevelBox2, "Highest MS level to include in the conversion (may be left blank).");
            // 
            // MSLevelBox1
            // 
            this.MSLevelBox1.Location = new System.Drawing.Point(95, 36);
            this.MSLevelBox1.Name = "MSLevelBox1";
            this.MSLevelBox1.Size = new System.Drawing.Size(37, 20);
            this.MSLevelBox1.TabIndex = 0;
            this.MSLevelBox1.Text = "1";
            this.MSLevelBox1.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumTextBox_KeyPress);
            setToolTip(this.MSLevelBox1, "Lowest MS level for scans to include in the conversion.");
            // 
            // PeakPickingPanel
            // 
            this.PeakPickingPanel.Controls.Add(this.PeakPreferVendorBox);
            this.PeakPickingPanel.Controls.Add(this.PeakMSLevelLabel);
            this.PeakPickingPanel.Controls.Add(this.PeakMSLevelSeperator);
            this.PeakPickingPanel.Controls.Add(this.PeakMSLevelBox2);
            this.PeakPickingPanel.Controls.Add(this.PeakMSLevelBox1);
            this.PeakPickingPanel.Location = new System.Drawing.Point(22, 46);
            this.PeakPickingPanel.Name = "PeakPickingPanel";
            this.PeakPickingPanel.Size = new System.Drawing.Size(283, 91);
            this.PeakPickingPanel.TabIndex = 2;
            this.PeakPickingPanel.Visible = false;
            string peakPickingHelp = "Use this filter to perform peak picking (centroiding) on the input data.";
            setToolTip(this.PeakPickingPanel, peakPickingHelp);
            // 
            // PeakPreferVendorBox
            // 
            this.PeakPreferVendorBox.AutoSize = true;
            this.PeakPreferVendorBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.PeakPreferVendorBox.Checked = true;
            this.PeakPreferVendorBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.PeakPreferVendorBox.Location = new System.Drawing.Point(94, 12);
            this.PeakPreferVendorBox.Name = "PeakPreferVendorBox";
            this.PeakPreferVendorBox.Size = new System.Drawing.Size(94, 17);
            this.PeakPreferVendorBox.TabIndex = 21;
            this.PeakPreferVendorBox.Text = "Prefer Vendor:";
            this.PeakPreferVendorBox.UseVisualStyleBackColor = true;
            string preferVendorHelp = "Uncheck this box if you prefer ProteoWizard's peak picking algorithm to that provided by the vendor (normally the vendor code works better).  Not all input formats have vendor peakpicking, but it's OK to leave this checked.";
            setToolTip(this.PeakPreferVendorBox, preferVendorHelp);
            // 
            // PeakMSLevelLabel
            // 
            this.PeakMSLevelLabel.AutoSize = true;
            this.PeakMSLevelLabel.Location = new System.Drawing.Point(113, 41);
            this.PeakMSLevelLabel.Name = "PeakMSLevelLabel";
            this.PeakMSLevelLabel.Size = new System.Drawing.Size(60, 13);
            this.PeakMSLevelLabel.TabIndex = 20;
            this.PeakMSLevelLabel.Text = "MS Levels:";
            setToolTip(this.PeakMSLevelLabel, "Selects the MS levels for scans on which to perform peak picking.");
            // 
            // PeakMSLevelSeperator
            // 
            this.PeakMSLevelSeperator.AutoSize = true;
            this.PeakMSLevelSeperator.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.PeakMSLevelSeperator.Location = new System.Drawing.Point(134, 55);
            this.PeakMSLevelSeperator.Name = "PeakMSLevelSeperator";
            this.PeakMSLevelSeperator.Size = new System.Drawing.Size(15, 20);
            this.PeakMSLevelSeperator.TabIndex = 19;
            this.PeakMSLevelSeperator.Text = "-";
            // 
            // PeakMSLevelBox2
            // 
            this.PeakMSLevelBox2.Location = new System.Drawing.Point(151, 57);
            this.PeakMSLevelBox2.Name = "PeakMSLevelBox2";
            this.PeakMSLevelBox2.Size = new System.Drawing.Size(37, 20);
            this.PeakMSLevelBox2.TabIndex = 18;
            this.PeakMSLevelBox2.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumTextBox_KeyPress);
            setToolTip(this.PeakMSLevelBox2, "Highest MS level on which to perform peak picking (may be left blank).");
            // 
            // PeakMSLevelBox1
            // 
            this.PeakMSLevelBox1.Location = new System.Drawing.Point(95, 57);
            this.PeakMSLevelBox1.Name = "PeakMSLevelBox1";
            this.PeakMSLevelBox1.Size = new System.Drawing.Size(37, 20);
            this.PeakMSLevelBox1.TabIndex = 17;
            this.PeakMSLevelBox1.Text = "1";
            this.PeakMSLevelBox1.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumTextBox_KeyPress);
            setToolTip(this.PeakMSLevelBox1, "Lowest MS level on which to perform peak picking.");
            // 
            // ZeroSamplesPanel
            // 
            this.ZeroSamplesPanel.Controls.Add(this.ZeroSamplesAddMissing);
            this.ZeroSamplesPanel.Controls.Add(this.ZeroSamplesAddMissingFlankCountBox);
            this.ZeroSamplesPanel.Controls.Add(this.ZeroSamplesRemove);
            this.ZeroSamplesPanel.Controls.Add(this.ZeroSamplesMSLevelLabel);
            this.ZeroSamplesPanel.Controls.Add(this.ZeroSamplesMSLevelSeperator);
            this.ZeroSamplesPanel.Controls.Add(this.ZeroSamplesMSLevelBox2);
            this.ZeroSamplesPanel.Controls.Add(this.ZeroSamplesMSLevelBox1);
            this.ZeroSamplesPanel.Location = new System.Drawing.Point(22, 46);
            this.ZeroSamplesPanel.Name = "ZeroSamplesPanel";
            this.ZeroSamplesPanel.Size = new System.Drawing.Size(283, 91);
            this.ZeroSamplesPanel.TabIndex = 24;
            this.ZeroSamplesPanel.Visible = false;
            setToolTip(this.ZeroSamplesPanel, "These filters help with missing or unwanted zero value samples.");

            // 
            // ZeroSamplesRemove
            // 
            this.ZeroSamplesRemove.AutoSize = true;
            this.ZeroSamplesRemove.Checked = true;
            this.ZeroSamplesRemove.Location = new System.Drawing.Point(20, 12);
            this.ZeroSamplesRemove.Name = "ZeroSamplesRemove";
            this.ZeroSamplesRemove.Size = new System.Drawing.Size(51, 17);
            this.ZeroSamplesRemove.TabIndex = 29;
            this.ZeroSamplesRemove.Text = "Remove";
            this.ZeroSamplesRemove.UseVisualStyleBackColor = true;
            this.ZeroSamplesRemove.Click += new System.EventHandler(this.ZeroSamples_ModeChanged);
            setToolTip(this.ZeroSamplesRemove, "Reduces output file sizes by removing zero values which are not adjacent to nonzero values.");

            // 
            // ZeroSamplesAddMissing
            // 
            this.ZeroSamplesAddMissing.AutoSize = true;
            this.ZeroSamplesAddMissing.Checked = false;
            this.ZeroSamplesAddMissing.Location = new System.Drawing.Point(93, 12);
            this.ZeroSamplesAddMissing.Name = "ZeroSamplesAddMissing";
            this.ZeroSamplesAddMissing.Size = new System.Drawing.Size(51, 17);
            this.ZeroSamplesAddMissing.TabIndex = 30;
            this.ZeroSamplesAddMissing.TabStop = true;
            this.ZeroSamplesAddMissing.Text = "Add missing, flank by:";
            this.ZeroSamplesAddMissing.UseVisualStyleBackColor = true;
            this.ZeroSamplesAddMissing.Click += new System.EventHandler(this.ZeroSamples_ModeChanged);
            string addZerosHelp = "Adds flanking zero values next to nonzero values where needed, to help with things like smoothing.";
            setToolTip(this.ZeroSamplesAddMissing, addZerosHelp);

            // 
            // AddMissingFlankCountBox
            // 
            this.ZeroSamplesAddMissingFlankCountBox.Location = new System.Drawing.Point(221, 12);
            this.ZeroSamplesAddMissingFlankCountBox.Enabled = this.ZeroSamplesAddMissing.Checked;
            this.ZeroSamplesAddMissingFlankCountBox.Name = "ZeroSamplesAddMissingFlankCountBox";
            this.ZeroSamplesAddMissingFlankCountBox.Size = new System.Drawing.Size(37, 20);
            this.ZeroSamplesAddMissingFlankCountBox.Text = "5";
            this.ZeroSamplesAddMissingFlankCountBox.TabIndex = 31;
            this.ZeroSamplesAddMissingFlankCountBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumTextBox_KeyPress);
            setToolTip(this.ZeroSamplesAddMissingFlankCountBox, addZerosHelp);

            // 
            // ZeroSamplesMSLevelLabel
            // 
            this.ZeroSamplesMSLevelLabel.AutoSize = true;
            this.ZeroSamplesMSLevelLabel.Location = new System.Drawing.Point(113, 41);
            this.ZeroSamplesMSLevelLabel.Name = "ZeroSamplesMSLevelLabel";
            this.ZeroSamplesMSLevelLabel.Size = new System.Drawing.Size(60, 13);
            this.ZeroSamplesMSLevelLabel.TabIndex = 25;
            this.ZeroSamplesMSLevelLabel.Text = "MS Levels:";
            setToolTip(this.ZeroSamplesMSLevelLabel, "Perform this filter only on scans with these MS Levels.");
            
            // 
            // ZeroSamplesMSLevelSeperator
            // 
            this.ZeroSamplesMSLevelSeperator.AutoSize = true;
            this.ZeroSamplesMSLevelSeperator.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ZeroSamplesMSLevelSeperator.Location = new System.Drawing.Point(134, 55);
            this.ZeroSamplesMSLevelSeperator.Name = "ZeroSamplesMSLevelSeperator";
            this.ZeroSamplesMSLevelSeperator.Size = new System.Drawing.Size(15, 20);
            this.ZeroSamplesMSLevelSeperator.TabIndex = 26;
            this.ZeroSamplesMSLevelSeperator.Text = "-";
            // 
            // ZeroSamplesMSLevelBox2
            // 
            this.ZeroSamplesMSLevelBox2.Location = new System.Drawing.Point(151, 57);
            this.ZeroSamplesMSLevelBox2.Name = "ZeroSamplesMSLevelBox2";
            this.ZeroSamplesMSLevelBox2.Size = new System.Drawing.Size(37, 20);
            this.ZeroSamplesMSLevelBox2.TabIndex = 28;
            this.ZeroSamplesMSLevelBox2.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumTextBox_KeyPress);
            setToolTip(this.ZeroSamplesMSLevelBox2, "Highest MS level for scans to be treated with this filter (may be left blank).");
            // 
            // ZeroSamplesMSLevelBox1
            // 
            this.ZeroSamplesMSLevelBox1.Location = new System.Drawing.Point(95, 57);
            this.ZeroSamplesMSLevelBox1.Name = "ZeroSamplesMSLevelBox1";
            this.ZeroSamplesMSLevelBox1.Size = new System.Drawing.Size(37, 20);
            this.ZeroSamplesMSLevelBox1.TabIndex = 27;
            this.ZeroSamplesMSLevelBox1.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumTextBox_KeyPress);            // 
            setToolTip(this.ZeroSamplesMSLevelBox2, "Lowest MS level for scans to be treated with this filter.");
            // ETDFilterPanel
            // 
            this.ETDFilterPanel.Controls.Add(this.ETDBlanketRemovalBox);
            this.ETDFilterPanel.Controls.Add(this.ETDRemoveChargeReducedBox);
            this.ETDFilterPanel.Controls.Add(this.ETDRemoveNeutralLossBox);
            this.ETDFilterPanel.Controls.Add(this.ETDRemovePrecursorBox);
            this.ETDFilterPanel.Location = new System.Drawing.Point(22, 46);
            this.ETDFilterPanel.Name = "ETDFilterPanel";
            this.ETDFilterPanel.Size = new System.Drawing.Size(283, 91);
            this.ETDFilterPanel.TabIndex = 3;
            this.ETDFilterPanel.Visible = false;
            // TODO setToolTip(this.ETDFilterPanel, "whatisthis");

            // 
            // ETDBlanketRemovalBox
            // 
            this.ETDBlanketRemovalBox.AutoSize = true;
            this.ETDBlanketRemovalBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.ETDBlanketRemovalBox.Location = new System.Drawing.Point(108, 71);
            this.ETDBlanketRemovalBox.Name = "ETDBlanketRemovalBox";
            this.ETDBlanketRemovalBox.Size = new System.Drawing.Size(110, 17);
            this.ETDBlanketRemovalBox.TabIndex = 9;
            this.ETDBlanketRemovalBox.Text = "Blanket Removal:";
            this.ETDBlanketRemovalBox.UseVisualStyleBackColor = true;
            // TODO setToolTip(this.ETDBlanketRemovalBox, "whatisthis");
            // 
            // ETDRemoveChargeReducedBox
            // 
            this.ETDRemoveChargeReducedBox.AutoSize = true;
            this.ETDRemoveChargeReducedBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.ETDRemoveChargeReducedBox.Checked = true;
            this.ETDRemoveChargeReducedBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ETDRemoveChargeReducedBox.Location = new System.Drawing.Point(65, 25);
            this.ETDRemoveChargeReducedBox.Name = "ETDRemoveChargeReducedBox";
            this.ETDRemoveChargeReducedBox.Size = new System.Drawing.Size(153, 17);
            this.ETDRemoveChargeReducedBox.TabIndex = 8;
            this.ETDRemoveChargeReducedBox.Text = "Remove Charge Reduced:";
            this.ETDRemoveChargeReducedBox.UseVisualStyleBackColor = true;
            // TODO setToolTip(ETDRemoveChargeReducedBox, "whatisthis");
            // 
            // ETDRemoveNeutralLossBox
            // 
            this.ETDRemoveNeutralLossBox.AutoSize = true;
            this.ETDRemoveNeutralLossBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.ETDRemoveNeutralLossBox.Checked = true;
            this.ETDRemoveNeutralLossBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ETDRemoveNeutralLossBox.Location = new System.Drawing.Point(87, 48);
            this.ETDRemoveNeutralLossBox.Name = "ETDRemoveNeutralLossBox";
            this.ETDRemoveNeutralLossBox.Size = new System.Drawing.Size(131, 17);
            this.ETDRemoveNeutralLossBox.TabIndex = 7;
            this.ETDRemoveNeutralLossBox.Text = "Remove Neutral Loss:";
            this.ETDRemoveNeutralLossBox.UseVisualStyleBackColor = true;
            // TODO setToolTip(ETDRemoveNeutralLossBox, "whatisthis");
            // 
            // ETDRemovePrecursorBox
            // 
            this.ETDRemovePrecursorBox.AutoSize = true;
            this.ETDRemovePrecursorBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.ETDRemovePrecursorBox.Checked = true;
            this.ETDRemovePrecursorBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ETDRemovePrecursorBox.Location = new System.Drawing.Point(101, 2);
            this.ETDRemovePrecursorBox.Name = "ETDRemovePrecursorBox";
            this.ETDRemovePrecursorBox.Size = new System.Drawing.Size(117, 17);
            this.ETDRemovePrecursorBox.TabIndex = 6;
            this.ETDRemovePrecursorBox.Text = "Remove Precursor:";
            this.ETDRemovePrecursorBox.UseVisualStyleBackColor = true;
            // TODO setToolTip(ETDRemovePrecursorBox, "whatisthis");
            // 
            // ChargeStatePredictorPanel
            // 
            this.ChargeStatePredictorPanel.Controls.Add(this.ChaMCMaxLabel);
            this.ChargeStatePredictorPanel.Controls.Add(this.ChaMCMaxBox);
            this.ChargeStatePredictorPanel.Controls.Add(this.ChaMCMinBox);
            this.ChargeStatePredictorPanel.Controls.Add(this.ChaMCMinLabel);
            this.ChargeStatePredictorPanel.Controls.Add(this.ChaSingleBox);
            this.ChargeStatePredictorPanel.Controls.Add(this.ChaSingleLabel);
            this.ChargeStatePredictorPanel.Controls.Add(this.ChaOverwriteCharge);
            this.ChargeStatePredictorPanel.Location = new System.Drawing.Point(22, 46);
            this.ChargeStatePredictorPanel.Name = "ChargeStatePredictorPanel";
            this.ChargeStatePredictorPanel.Size = new System.Drawing.Size(283, 91);
            this.ChargeStatePredictorPanel.TabIndex = 4;
            this.ChargeStatePredictorPanel.Visible = false;
            setToolTip(ChargeStatePredictorPanel, "Use this filter to add missing (and optionally overwrite existing) charge state information.");

            // 
            // ChaMCMaxLabel
            // 
            this.ChaMCMaxLabel.AutoSize = true;
            this.ChaMCMaxLabel.Location = new System.Drawing.Point(182, 66);
            this.ChaMCMaxLabel.Name = "ChaMCMaxLabel";
            this.ChaMCMaxLabel.Size = new System.Drawing.Size(30, 13);
            this.ChaMCMaxLabel.TabIndex = 19;
            this.ChaMCMaxLabel.Text = "Max:";
            string maxChargeHelp = "Maximum charge state to be used.";
            string minChargeHelp = "Minimum charge state to be used.";
            setToolTip(this.ChaMCMaxLabel, maxChargeHelp);
            // 
            // ChaMCMaxBox
            // 
            this.ChaMCMaxBox.Location = new System.Drawing.Point(215, 62);
            this.ChaMCMaxBox.Name = "ChaMCMaxBox";
            this.ChaMCMaxBox.Size = new System.Drawing.Size(37, 20);
            this.ChaMCMaxBox.TabIndex = 18;
            this.ChaMCMaxBox.Text = "3";
            this.ChaMCMaxBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumTextBox_KeyPress);
            setToolTip(this.ChaMCMaxBox, maxChargeHelp);
            // 
            // ChaMCMinBox
            // 
            this.ChaMCMinBox.Location = new System.Drawing.Point(133, 62);
            this.ChaMCMinBox.Name = "ChaMCMinBox";
            this.ChaMCMinBox.Size = new System.Drawing.Size(37, 20);
            this.ChaMCMinBox.TabIndex = 17;
            this.ChaMCMinBox.Text = "2";
            this.ChaMCMinBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumTextBox_KeyPress);
            setToolTip(this.ChaMCMinBox, minChargeHelp);
            // 
            // ChaMCMinLabel
            // 
            this.ChaMCMinLabel.AutoSize = true;
            this.ChaMCMinLabel.Location = new System.Drawing.Point(30, 66);
            this.ChaMCMinLabel.Name = "ChaMCMinLabel";
            this.ChaMCMinLabel.Size = new System.Drawing.Size(103, 13);
            this.ChaMCMinLabel.TabIndex = 9;
            this.ChaMCMinLabel.Text = "Multiple Charge Min:";
            setToolTip(this.ChaMCMinLabel, minChargeHelp);
            // 
            // ChaSingleBox
            // 
            this.ChaSingleBox.DecimalPlaces = 2;
            this.ChaSingleBox.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.ChaSingleBox.Location = new System.Drawing.Point(174, 36);
            this.ChaSingleBox.Maximum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.ChaSingleBox.Name = "ChaSingleBox";
            this.ChaSingleBox.Size = new System.Drawing.Size(41, 20);
            this.ChaSingleBox.TabIndex = 8;
            this.ChaSingleBox.Value = new decimal(new int[] {
            9,
            0,
            0,
            65536});
            // TODO string chaSingleHelp = "what is this";
            // TODO setToolTip(this.ChaSingleBox,chaSingleHelp);
            // 
            // ChaSingleLabel
            // 
            this.ChaSingleLabel.AutoSize = true;
            this.ChaSingleLabel.Location = new System.Drawing.Point(67, 38);
            this.ChaSingleLabel.Name = "ChaSingleLabel";
            this.ChaSingleLabel.Size = new System.Drawing.Size(107, 13);
            this.ChaSingleLabel.TabIndex = 7;
            this.ChaSingleLabel.Text = "Single Charge % TIC:";
            // TODO setToolTip(this.ChaSingleLabel,chaSingleHelp);
            // 
            // ChaOverwriteCharge
            // 
            this.ChaOverwriteCharge.AutoSize = true;
            this.ChaOverwriteCharge.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.ChaOverwriteCharge.Location = new System.Drawing.Point(86, 12);
            this.ChaOverwriteCharge.Name = "ChaOverwriteCharge";
            this.ChaOverwriteCharge.Size = new System.Drawing.Size(111, 17);
            this.ChaOverwriteCharge.TabIndex = 6;
            this.ChaOverwriteCharge.Text = "Overwrite Charge:";
            this.ChaOverwriteCharge.UseVisualStyleBackColor = true;
            // TODO setToolTip(this.ChaOverwriteCharge,"whatisthis");
            // 
            // RemoveFilterButton
            // 
            this.RemoveFilterButton.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.RemoveFilterButton.Location = new System.Drawing.Point(477, 232);
            this.RemoveFilterButton.Name = "RemoveFilterButton";
            this.RemoveFilterButton.Size = new System.Drawing.Size(58, 23);
            this.RemoveFilterButton.TabIndex = 11;
            this.RemoveFilterButton.Text = "Remove";
            this.RemoveFilterButton.UseVisualStyleBackColor = true;
            this.RemoveFilterButton.Click += new System.EventHandler(this.RemoveFilterButton_Click);
            setToolTip(this.RemoveFilterButton, "Select a filter in the list below then click here to remove it.");
            // 
            // AddFilterButton
            // 
            this.AddFilterButton.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.AddFilterButton.Location = new System.Drawing.Point(429, 232);
            this.AddFilterButton.Name = "AddFilterButton";
            this.AddFilterButton.Size = new System.Drawing.Size(42, 23);
            this.AddFilterButton.TabIndex = 10;
            this.AddFilterButton.Text = "Add";
            this.AddFilterButton.UseVisualStyleBackColor = true;
            this.AddFilterButton.Click += new System.EventHandler(this.AddFilterButton_Click);
            setToolTip(this.AddFilterButton, "Add the filter specifed above to the list below.");
            // 
            // TextFileRadio
            // 
            this.TextFileRadio.AutoSize = true;
            this.TextFileRadio.Location = new System.Drawing.Point(147, 12);
            this.TextFileRadio.Name = "TextFileRadio";
            this.TextFileRadio.Size = new System.Drawing.Size(103, 17);
            this.TextFileRadio.TabIndex = 2;
            this.TextFileRadio.Text = "File of file names";
            this.TextFileRadio.UseVisualStyleBackColor = true;
            setToolTip(this.TextFileRadio, "Click this if your input file actually contains a list of files to be converted.");
            // 
            // FileListRadio
            // 
            this.FileListRadio.AutoSize = true;
            this.FileListRadio.Checked = true;
            this.FileListRadio.Location = new System.Drawing.Point(64, 12);
            this.FileListRadio.Name = "FileListRadio";
            this.FileListRadio.Size = new System.Drawing.Size(77, 17);
            this.FileListRadio.TabIndex = 1;
            this.FileListRadio.TabStop = true;
            this.FileListRadio.Text = "List of Files";
            this.FileListRadio.UseVisualStyleBackColor = true;
            this.FileListRadio.CheckedChanged += new System.EventHandler(this.FileListRadio_CheckedChanged);
            setToolTip(this.TextFileRadio, "Click this for normal operation.");
            // 
            // OutputFormatBox
            // 
            this.OutputFormatBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.OutputFormatBox.FormattingEnabled = true;
            this.OutputFormatBox.Items.AddRange(new object[] {
            "mzML",
            "mzXML",
            "mz5",
            "mgf",
            "text",
            "ms2",
            "cms2"});
            this.OutputFormatBox.Location = new System.Drawing.Point(89, 16);
            this.OutputFormatBox.Name = "OutputFormatBox";
            this.OutputFormatBox.Size = new System.Drawing.Size(60, 21);
            this.OutputFormatBox.TabIndex = 1;
            this.OutputFormatBox.SelectedIndexChanged += new System.EventHandler(this.OutputFormatBox_SelectedIndexChanged);
            string outputFormatHelp = "Selects the output format for the conversion";
            setToolTip(this.OutputFormatBox, outputFormatHelp);
            // 
            // FormatLabel
            // 
            this.FormatLabel.AutoSize = true;
            this.FormatLabel.Location = new System.Drawing.Point(14, 19);
            this.FormatLabel.Name = "FormatLabel";
            this.FormatLabel.Size = new System.Drawing.Size(74, 13);
            this.FormatLabel.TabIndex = 13;
            this.FormatLabel.Text = "Output format:";
            setToolTip(this.FormatLabel, outputFormatHelp);
            // 
            // BrowseFileButton
            // 
            this.BrowseFileButton.Location = new System.Drawing.Point(231, 33);
            this.BrowseFileButton.Name = "BrowseFileButton";
            this.BrowseFileButton.Size = new System.Drawing.Size(50, 23);
            this.BrowseFileButton.TabIndex = 4;
            this.BrowseFileButton.Text = "Browse";
            this.BrowseFileButton.UseVisualStyleBackColor = true;
            this.BrowseFileButton.Click += new System.EventHandler(this.BrowseFileButton_Click);
            // 
            // OutputBrowse
            // 
            this.OutputBrowse.Location = new System.Drawing.Point(221, 16);
            this.OutputBrowse.Name = "OutputBrowse";
            this.OutputBrowse.Size = new System.Drawing.Size(50, 23);
            this.OutputBrowse.TabIndex = 2;
            this.OutputBrowse.Text = "Browse";
            this.OutputBrowse.UseVisualStyleBackColor = true;
            this.OutputBrowse.Click += new System.EventHandler(this.OutputBrowse_Click);
            // 
            // OutputLabel
            // 
            this.OutputLabel.AutoSize = true;
            this.OutputLabel.Location = new System.Drawing.Point(2, 2);
            this.OutputLabel.Name = "OutputLabel";
            this.OutputLabel.Size = new System.Drawing.Size(87, 13);
            this.OutputLabel.TabIndex = 16;
            this.OutputLabel.Text = "Output Directory:";
            string outputHelp = "Choose the directory for writing the converted file(s).";
            setToolTip(this.OutputLabel, outputHelp);
            // 
            // OutputBox
            // 
            this.OutputBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Append;
            this.OutputBox.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystemDirectories;
            this.OutputBox.Location = new System.Drawing.Point(5, 18);
            this.OutputBox.Name = "OutputBox";
            this.OutputBox.Size = new System.Drawing.Size(210, 20);
            this.OutputBox.TabIndex = 1;
            setToolTip(this.OutputBox, outputHelp);
            // 
            // PrecisionLabel
            // 
            this.PrecisionLabel.AutoSize = true;
            this.PrecisionLabel.Location = new System.Drawing.Point(13, 45);
            this.PrecisionLabel.Name = "PrecisionLabel";
            this.PrecisionLabel.Size = new System.Drawing.Size(131, 13);
            this.PrecisionLabel.TabIndex = 18;
            this.PrecisionLabel.Text = "Binary encoding precision:";
            string precisionHelp = "Sets output precision for writing binary m/z and intensity information.  Most input formats store m/z and intensity as 32 bit values, so choosing 64 bit output precision usually just creates unnecessarily large output files.";
            setToolTip(this.PrecisionLabel, precisionHelp);
            // 
            // OptionsGB
            // 
            this.OptionsGB.Controls.Add(this.ConfigurationFileGB);
            this.OptionsGB.Controls.Add(this.UseCFGButton);
            this.OptionsGB.Controls.Add(this.UseZlibBox);
            this.OptionsGB.Controls.Add(this.GzipBox);
            this.OptionsGB.Controls.Add(this.OutputExtensionBox);
            this.OptionsGB.Controls.Add(this.OutputExtensionLabel);
            this.OptionsGB.Controls.Add(this.WriteIndexBox);
            this.OptionsGB.Controls.Add(this.Precision32);
            this.OptionsGB.Controls.Add(this.Precision64);
            this.OptionsGB.Controls.Add(this.PrecisionLabel);
            this.OptionsGB.Controls.Add(this.OutputFormatBox);
            this.OptionsGB.Controls.Add(this.FormatLabel);
            this.OptionsGB.Location = new System.Drawing.Point(2, 44);
            this.OptionsGB.Name = "OptionsGB";
            this.OptionsGB.Size = new System.Drawing.Size(269, 119);
            this.OptionsGB.TabIndex = 3;
            this.OptionsGB.TabStop = false;
            this.OptionsGB.Text = "Options";
            setToolTip(this.OptionsGB, "Useful options for controlling output format and file size.");
            // 
            // ConfigurationFileGB
            // 
            this.ConfigurationFileGB.Controls.Add(this.BrowseConfigButton);
            this.ConfigurationFileGB.Controls.Add(this.ConfigBox);
            this.ConfigurationFileGB.Location = new System.Drawing.Point(3, 115);
            this.ConfigurationFileGB.Name = "ConfigurationFileGB";
            this.ConfigurationFileGB.Size = new System.Drawing.Size(260, 42);
            this.ConfigurationFileGB.TabIndex = 9;
            this.ConfigurationFileGB.TabStop = false;
            this.ConfigurationFileGB.Text = "Configuration File";
            this.ConfigurationFileGB.Visible = false;
            string configFileHelp = "Specify a msconvert configuration file to include in this conversion.";
            setToolTip(this.ConfigurationFileGB, configFileHelp);
            // 
            // BrowseConfigButton
            // 
            this.BrowseConfigButton.Location = new System.Drawing.Point(204, 13);
            this.BrowseConfigButton.Name = "BrowseConfigButton";
            this.BrowseConfigButton.Size = new System.Drawing.Size(50, 23);
            this.BrowseConfigButton.TabIndex = 21;
            this.BrowseConfigButton.Text = "Browse";
            this.BrowseConfigButton.UseVisualStyleBackColor = true;
            // 
            // ConfigBox
            // 
            this.ConfigBox.Location = new System.Drawing.Point(13, 15);
            this.ConfigBox.Name = "ConfigBox";
            this.ConfigBox.Size = new System.Drawing.Size(185, 20);
            this.ConfigBox.TabIndex = 20;
            setToolTip(this.ConfigBox, configFileHelp);
            // 
            // UseCFGButton
            // 
            this.UseCFGButton.AutoSize = true;
            this.UseCFGButton.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.UseCFGButton.Location = new System.Drawing.Point(16, 92);
            this.UseCFGButton.Name = "UseCFGButton";
            this.UseCFGButton.Size = new System.Drawing.Size(96, 17);
            this.UseCFGButton.TabIndex = 7;
            this.UseCFGButton.Text = "Use config file:";
            this.UseCFGButton.UseVisualStyleBackColor = true;
            this.UseCFGButton.CheckedChanged += new System.EventHandler(this.UseCFGButton_CheckedChanged);
            setToolTip(this.UseCFGButton, configFileHelp);
            // 
            // UseZlibBox
            // 
            this.UseZlibBox.AutoSize = true;
            this.UseZlibBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.UseZlibBox.Location = new System.Drawing.Point(125, 69);
            this.UseZlibBox.Name = "UseZlibBox";
            this.UseZlibBox.Size = new System.Drawing.Size(128, 17);
            this.UseZlibBox.TabIndex = 6;
            this.UseZlibBox.Checked = true;
            this.UseZlibBox.Text = "Use zlib compression:";
            this.UseZlibBox.UseVisualStyleBackColor = true;
            setToolTip(this.UseZlibBox, "Using zlib to compress peak lists results in much smaller mzML and mzXML output files.");
            // 
            // GzipBox
            // 
            this.GzipBox.AutoSize = true;
            this.GzipBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.GzipBox.Location = new System.Drawing.Point(148, 92);
            this.GzipBox.Name = "GzipBox";
            this.GzipBox.Size = new System.Drawing.Size(105, 17);
            this.GzipBox.TabIndex = 8;
            this.GzipBox.Text = "Package in gzip:";
            this.GzipBox.UseVisualStyleBackColor = true;
            setToolTip(this.GzipBox, "This compresses the entire output file using gzip, and adds \".gz\" to the end of the filename.");
            // 
            // OutputExtensionBox
            // 
            this.OutputExtensionBox.Location = new System.Drawing.Point(212, 16);
            this.OutputExtensionBox.Name = "OutputExtensionBox";
            this.OutputExtensionBox.Size = new System.Drawing.Size(43, 20);
            this.OutputExtensionBox.TabIndex = 2;
            string OutputExtensionHelp = "Sets the filename extension for the output file(s)";
            setToolTip(this.OutputExtensionBox, OutputExtensionHelp);
            // 
            // OutputExtensionLabel
            // 
            this.OutputExtensionLabel.AutoSize = true;
            this.OutputExtensionLabel.Location = new System.Drawing.Point(155, 19);
            this.OutputExtensionLabel.Name = "OutputExtensionLabel";
            this.OutputExtensionLabel.Size = new System.Drawing.Size(56, 13);
            this.OutputExtensionLabel.TabIndex = 23;
            this.OutputExtensionLabel.Text = "Extension:";
            setToolTip(this.OutputExtensionLabel, OutputExtensionHelp);
            // 
            // WriteIndexBox
            // 
            this.WriteIndexBox.AutoSize = true;
            this.WriteIndexBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.WriteIndexBox.Checked = true;
            this.WriteIndexBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.WriteIndexBox.Location = new System.Drawing.Point(30, 69);
            this.WriteIndexBox.Name = "WriteIndexBox";
            this.WriteIndexBox.Size = new System.Drawing.Size(82, 17);
            this.WriteIndexBox.TabIndex = 5;
            this.WriteIndexBox.Text = "Write index:";
            this.WriteIndexBox.UseVisualStyleBackColor = true;
            setToolTip(this.WriteIndexBox, "Include an index in mzML and mzXML output files.");
            // 
            // Precision32
            // 
            this.Precision32.AutoSize = true;
            this.Precision32.Location = new System.Drawing.Point(204, 43);
            this.Precision32.Name = "Precision32";
            this.Precision32.Size = new System.Drawing.Size(51, 17);
            this.Precision32.TabIndex = 4;
            this.Precision32.Text = "32-bit";
            this.Precision32.UseVisualStyleBackColor = true;
            setToolTip(this.Precision32, precisionHelp);
            // 
            // Precision64
            // 
            this.Precision64.AutoSize = true;
            this.Precision64.Checked = true;
            this.Precision64.Location = new System.Drawing.Point(147, 43);
            this.Precision64.Name = "Precision64";
            this.Precision64.Size = new System.Drawing.Size(51, 17);
            this.Precision64.TabIndex = 3;
            this.Precision64.TabStop = true;
            this.Precision64.Text = "64-bit";
            this.Precision64.UseVisualStyleBackColor = true;
            setToolTip(this.Precision64, precisionHelp);
            // 
            // SlidingPanel
            // 
            this.SlidingPanel.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.SlidingPanel.Controls.Add(this.OutputBrowse);
            this.SlidingPanel.Controls.Add(this.OptionsGB);
            this.SlidingPanel.Controls.Add(this.OutputLabel);
            this.SlidingPanel.Controls.Add(this.OutputBox);
            this.SlidingPanel.Location = new System.Drawing.Point(13, 261);
            this.SlidingPanel.Name = "SlidingPanel";
            this.SlidingPanel.Size = new System.Drawing.Size(275, 211);
            this.SlidingPanel.TabIndex = 8;
            // 
            // SetDefaultsButton
            // 
            this.SetDefaultsButton.Location = new System.Drawing.Point(21, 430);
            this.SetDefaultsButton.Name = "SetDefaultsButton";
            this.SetDefaultsButton.Size = new System.Drawing.Size(360, 23);
            this.SetDefaultsButton.TabIndex = 32;
            this.SetDefaultsButton.Text = "Use these settings next time I start MSConvertGUI";
            this.SetDefaultsButton.UseVisualStyleBackColor = true;
            this.SetDefaultsButton.Click += new System.EventHandler(this.SetDefaultsButton_Click);
            setToolTip(this.SetDefaultsButton,"Saves the current settings and uses them as the defaults next time you use MSConvertGUI without a recognized input file type.");
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(661, 464);
            this.Controls.Add(this.BrowseFileButton);
            this.Controls.Add(this.FileListRadio);
            this.Controls.Add(this.TextFileRadio);
            this.Controls.Add(this.RemoveFilterButton);
            this.Controls.Add(this.AddFilterButton);
            this.Controls.Add(this.FilterGB);
            this.Controls.Add(this.StartButton);
            this.Controls.Add(this.SetDefaultsButton);
            this.Controls.Add(this.RemoveFileButton);
            this.Controls.Add(this.FileListBox);
            this.Controls.Add(this.FilterDGV);
            this.Controls.Add(this.AddFileButton);
            this.Controls.Add(this.FileLabel);
            this.Controls.Add(this.FileBox);
            this.Controls.Add(this.SlidingPanel);
            this.Name = "MainForm";
            this.Text = "MSConvertGUI" + (Environment.Is64BitProcess ? " (64-bit)" : "");
            this.Load += new System.EventHandler(this.MainForm_Load);
            ((System.ComponentModel.ISupportInitialize) (this.FilterDGV)).EndInit();
            this.FilterGB.ResumeLayout(false);
            this.ActivationPanel.ResumeLayout(false);
            this.ActivationPanel.PerformLayout();
            this.SubsetPanel.ResumeLayout(false);
            this.SubsetPanel.PerformLayout();
            this.MSLevelPanel.ResumeLayout(false);
            this.MSLevelPanel.PerformLayout();
            this.PeakPickingPanel.ResumeLayout(false);
            this.PeakPickingPanel.PerformLayout();
            this.ZeroSamplesPanel.ResumeLayout(false);
            this.ZeroSamplesPanel.PerformLayout();
            this.ETDFilterPanel.ResumeLayout(false);
            this.ETDFilterPanel.PerformLayout();
            this.ChargeStatePredictorPanel.ResumeLayout(false);
            this.ChargeStatePredictorPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize) (this.ChaSingleBox)).EndInit();
            this.OptionsGB.ResumeLayout(false);
            this.OptionsGB.PerformLayout();
            this.ConfigurationFileGB.ResumeLayout(false);
            this.ConfigurationFileGB.PerformLayout();
            this.SlidingPanel.ResumeLayout(false);
            this.SlidingPanel.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolTip ctlToolTip;
        private System.Windows.Forms.TextBox FileBox;
        private System.Windows.Forms.Label FileLabel;
        private System.Windows.Forms.Button AddFileButton;
        private System.Windows.Forms.DataGridView FilterDGV;
        private System.Windows.Forms.ListBox FileListBox;
        private System.Windows.Forms.Button RemoveFileButton;
        private System.Windows.Forms.Button StartButton;
        private System.Windows.Forms.Button SetDefaultsButton;
        private System.Windows.Forms.GroupBox FilterGB;
        private System.Windows.Forms.Button RemoveFilterButton;
        private System.Windows.Forms.Button AddFilterButton;
        private System.Windows.Forms.RadioButton TextFileRadio;
        private System.Windows.Forms.RadioButton FileListRadio;
        private System.Windows.Forms.ComboBox OutputFormatBox;
        private System.Windows.Forms.Label FormatLabel;
        private System.Windows.Forms.Button BrowseFileButton;
        private System.Windows.Forms.Button OutputBrowse;
        private System.Windows.Forms.Label OutputLabel;
        private System.Windows.Forms.TextBox OutputBox;
        private System.Windows.Forms.Label PrecisionLabel;
        private System.Windows.Forms.GroupBox OptionsGB;
        private System.Windows.Forms.RadioButton Precision32;
        private System.Windows.Forms.RadioButton Precision64;
        private System.Windows.Forms.CheckBox WriteIndexBox;
        private System.Windows.Forms.TextBox OutputExtensionBox;
        private System.Windows.Forms.Label OutputExtensionLabel;
        private System.Windows.Forms.GroupBox ConfigurationFileGB;
        private System.Windows.Forms.CheckBox UseCFGButton;
        private System.Windows.Forms.CheckBox UseZlibBox;
        private System.Windows.Forms.CheckBox GzipBox;
        private System.Windows.Forms.Button BrowseConfigButton;
        private System.Windows.Forms.TextBox ConfigBox;
        private System.Windows.Forms.Panel SlidingPanel;
        private System.Windows.Forms.Panel MSLevelPanel;
        private System.Windows.Forms.ComboBox FilterBox;
        private System.Windows.Forms.Panel ChargeStatePredictorPanel;
        private System.Windows.Forms.Panel PeakPickingPanel;
        private System.Windows.Forms.Panel ZeroSamplesPanel;
        private System.Windows.Forms.Panel ETDFilterPanel;
        private System.Windows.Forms.Panel ActivationPanel;
        private System.Windows.Forms.Label MSLabelSeperator;
        private System.Windows.Forms.TextBox MSLevelBox2;
        private System.Windows.Forms.TextBox MSLevelBox1;
        private System.Windows.Forms.CheckBox PeakPreferVendorBox;
        private System.Windows.Forms.Label PeakMSLevelLabel;
        private System.Windows.Forms.Label PeakMSLevelSeperator;
        private System.Windows.Forms.TextBox PeakMSLevelBox2;
        private System.Windows.Forms.TextBox PeakMSLevelBox1;
        private System.Windows.Forms.RadioButton ZeroSamplesRemove;
        private System.Windows.Forms.RadioButton ZeroSamplesAddMissing;
        private System.Windows.Forms.TextBox ZeroSamplesAddMissingFlankCountBox;
        private System.Windows.Forms.Label ZeroSamplesMSLevelLabel;
        private System.Windows.Forms.Label ZeroSamplesMSLevelSeperator;
        private System.Windows.Forms.TextBox ZeroSamplesMSLevelBox2;
        private System.Windows.Forms.TextBox ZeroSamplesMSLevelBox1;
        private System.Windows.Forms.CheckBox ETDBlanketRemovalBox;
        private System.Windows.Forms.CheckBox ETDRemoveChargeReducedBox;
        private System.Windows.Forms.CheckBox ETDRemoveNeutralLossBox;
        private System.Windows.Forms.CheckBox ETDRemovePrecursorBox;
        private System.Windows.Forms.Label ChaSingleLabel;
        private System.Windows.Forms.CheckBox ChaOverwriteCharge;
        private System.Windows.Forms.Label ChaMCMaxLabel;
        private System.Windows.Forms.TextBox ChaMCMaxBox;
        private System.Windows.Forms.TextBox ChaMCMinBox;
        private System.Windows.Forms.Label ChaMCMinLabel;
        private System.Windows.Forms.NumericUpDown ChaSingleBox;
        private System.Windows.Forms.ComboBox ActivationTypeBox;
        private System.Windows.Forms.Label ActivationTypeLabel;
        private System.Windows.Forms.Label MSLevelLabel;
        private System.Windows.Forms.Panel SubsetPanel;
        private System.Windows.Forms.Label mzWinLabel;
        private System.Windows.Forms.Label ScanTimeLabel;
        private System.Windows.Forms.Label ScanTimeLabel2;
        private System.Windows.Forms.TextBox ScanTimeHigh;
        private System.Windows.Forms.TextBox ScanTimeLow;
        private System.Windows.Forms.Label ScanNumberLabel;
        private System.Windows.Forms.Label ScanNumberLabel2;
        private System.Windows.Forms.TextBox ScanNumberHigh;
        private System.Windows.Forms.TextBox ScanNumberLow;
        private System.Windows.Forms.Label mzWinLabel2;
        private System.Windows.Forms.TextBox mzWinHigh;
        private System.Windows.Forms.TextBox mzWinLow;
        private System.Windows.Forms.DataGridViewTextBoxColumn OptionTab;
        private System.Windows.Forms.DataGridViewTextBoxColumn ValueTab;
    }
}

