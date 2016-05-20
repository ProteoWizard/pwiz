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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.ctlToolTip = new System.Windows.Forms.ToolTip(this.components);
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
            this.ChargeStatePredictorPanel = new System.Windows.Forms.Panel();
            this.ChaMCMaxLabel = new System.Windows.Forms.Label();
            this.ChaMCMaxBox = new System.Windows.Forms.TextBox();
            this.ChaMCMinBox = new System.Windows.Forms.TextBox();
            this.ChaMCMinLabel = new System.Windows.Forms.Label();
            this.ChaSingleBox = new System.Windows.Forms.NumericUpDown();
            this.ChaSingleLabel = new System.Windows.Forms.Label();
            this.ChaOverwriteCharge = new System.Windows.Forms.CheckBox();
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
            this.MSLevelHigh = new System.Windows.Forms.TextBox();
            this.MSLevelLow = new System.Windows.Forms.TextBox();
            this.PeakPickingPanel = new System.Windows.Forms.Panel();
            this.PeakPreferVendorBox = new System.Windows.Forms.CheckBox();
            this.PeakMSLevelLabel = new System.Windows.Forms.Label();
            this.PeakMSLevelSeperator = new System.Windows.Forms.Label();
            this.PeakMSLevelHigh = new System.Windows.Forms.TextBox();
            this.PeakMSLevelLow = new System.Windows.Forms.TextBox();
            this.ZeroSamplesPanel = new System.Windows.Forms.Panel();
            this.ZeroSamplesAddMissing = new System.Windows.Forms.RadioButton();
            this.ZeroSamplesAddMissingFlankCountBox = new System.Windows.Forms.TextBox();
            this.ZeroSamplesRemove = new System.Windows.Forms.RadioButton();
            this.ZeroSamplesMSLevelLabel = new System.Windows.Forms.Label();
            this.ZeroSamplesMSLevelSeperator = new System.Windows.Forms.Label();
            this.ZeroSamplesMSLevelHigh = new System.Windows.Forms.TextBox();
            this.ZeroSamplesMSLevelLow = new System.Windows.Forms.TextBox();
            this.ETDFilterPanel = new System.Windows.Forms.Panel();
            this.ETDBlanketRemovalBox = new System.Windows.Forms.CheckBox();
            this.ETDRemoveChargeReducedBox = new System.Windows.Forms.CheckBox();
            this.ETDRemoveNeutralLossBox = new System.Windows.Forms.CheckBox();
            this.ETDRemovePrecursorBox = new System.Windows.Forms.CheckBox();
            this.ThresholdFilterPanel = new System.Windows.Forms.Panel();
            this.thresholdValueLabel = new System.Windows.Forms.Label();
            this.thresholdOrientationLabel = new System.Windows.Forms.Label();
            this.thresholdTypeLabel = new System.Windows.Forms.Label();
            this.thresholdOrientationComboBox = new System.Windows.Forms.ComboBox();
            this.thresholdValueTextBox = new System.Windows.Forms.TextBox();
            this.thresholdTypeComboBox = new System.Windows.Forms.ComboBox();
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
            this.NumpressSlofBox = new System.Windows.Forms.CheckBox();
            this.NumpressLinearBox = new System.Windows.Forms.CheckBox();
            this.NumpressPicBox = new System.Windows.Forms.CheckBox();
            this.MakeTPPCompatibleOutputButton = new System.Windows.Forms.CheckBox();
            this.UseZlibBox = new System.Windows.Forms.CheckBox();
            this.GzipBox = new System.Windows.Forms.CheckBox();
            this.OutputExtensionBox = new System.Windows.Forms.TextBox();
            this.OutputExtensionLabel = new System.Windows.Forms.Label();
            this.WriteIndexBox = new System.Windows.Forms.CheckBox();
            this.Precision32 = new System.Windows.Forms.RadioButton();
            this.Precision64 = new System.Windows.Forms.RadioButton();
            this.SlidingPanel = new System.Windows.Forms.Panel();
            this.SetDefaultsButton = new System.Windows.Forms.Button();
            this.AboutButton = new System.Windows.Forms.Button();
            this.dataGridViewTextBoxColumn1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.FilterDGV)).BeginInit();
            this.FilterGB.SuspendLayout();
            this.ChargeStatePredictorPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.ChaSingleBox)).BeginInit();
            this.ActivationPanel.SuspendLayout();
            this.SubsetPanel.SuspendLayout();
            this.MSLevelPanel.SuspendLayout();
            this.PeakPickingPanel.SuspendLayout();
            this.ZeroSamplesPanel.SuspendLayout();
            this.ETDFilterPanel.SuspendLayout();
            this.ThresholdFilterPanel.SuspendLayout();
            this.OptionsGB.SuspendLayout();
            this.SlidingPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // FileBox
            // 
            this.FileBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.FileBox.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystem;
            this.FileBox.Location = new System.Drawing.Point(57, 43);
            this.FileBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.FileBox.Name = "FileBox";
            this.FileBox.Size = new System.Drawing.Size(241, 22);
            this.FileBox.TabIndex = 3;
            this.FileBox.TextChanged += new System.EventHandler(this.FileBox_TextChanged);
            // 
            // FileLabel
            // 
            this.FileLabel.AutoSize = true;
            this.FileLabel.Location = new System.Drawing.Point(16, 47);
            this.FileLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.FileLabel.Name = "FileLabel";
            this.FileLabel.Size = new System.Drawing.Size(34, 17);
            this.FileLabel.TabIndex = 1;
            this.FileLabel.Text = "File:";
            // 
            // AddFileButton
            // 
            this.AddFileButton.Enabled = false;
            this.AddFileButton.Location = new System.Drawing.Point(123, 75);
            this.AddFileButton.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.AddFileButton.Name = "AddFileButton";
            this.AddFileButton.Size = new System.Drawing.Size(56, 28);
            this.AddFileButton.TabIndex = 5;
            this.AddFileButton.Text = "Add";
            this.AddFileButton.UseVisualStyleBackColor = true;
            this.AddFileButton.Click += new System.EventHandler(this.AddFileButton_Click);
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
            this.FilterDGV.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.FilterDGV.Location = new System.Drawing.Point(429, 321);
            this.FilterDGV.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.FilterDGV.MultiSelect = false;
            this.FilterDGV.Name = "FilterDGV";
            this.FilterDGV.RowHeadersVisible = false;
            this.FilterDGV.RowTemplate.Height = 24;
            this.FilterDGV.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.FilterDGV.Size = new System.Drawing.Size(436, 308);
            this.FilterDGV.TabIndex = 12;
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
            this.FileListBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.FileListBox.FormattingEnabled = true;
            this.FileListBox.HorizontalScrollbar = true;
            this.FileListBox.ItemHeight = 16;
            this.FileListBox.Location = new System.Drawing.Point(23, 111);
            this.FileListBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.FileListBox.Name = "FileListBox";
            this.FileListBox.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.FileListBox.Size = new System.Drawing.Size(357, 228);
            this.FileListBox.TabIndex = 7;
            this.FileListBox.KeyUp += new System.Windows.Forms.KeyEventHandler(this.FileListBox_KeyUp);
            // 
            // RemoveFileButton
            // 
            this.RemoveFileButton.Enabled = false;
            this.RemoveFileButton.Location = new System.Drawing.Point(187, 75);
            this.RemoveFileButton.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.RemoveFileButton.Name = "RemoveFileButton";
            this.RemoveFileButton.Size = new System.Drawing.Size(77, 28);
            this.RemoveFileButton.TabIndex = 6;
            this.RemoveFileButton.Text = "Remove";
            this.RemoveFileButton.UseVisualStyleBackColor = true;
            this.RemoveFileButton.Click += new System.EventHandler(this.RemoveFileButton_Click);
            // 
            // StartButton
            // 
            this.StartButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.StartButton.Location = new System.Drawing.Point(765, 640);
            this.StartButton.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.StartButton.Name = "StartButton";
            this.StartButton.Size = new System.Drawing.Size(100, 28);
            this.StartButton.TabIndex = 13;
            this.StartButton.Text = "Start";
            this.StartButton.UseVisualStyleBackColor = true;
            this.StartButton.Click += new System.EventHandler(this.StartButton_Click);
            // 
            // FilterGB
            // 
            this.FilterGB.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.FilterGB.Controls.Add(this.FilterBox);
            this.FilterGB.Controls.Add(this.ChargeStatePredictorPanel);
            this.FilterGB.Controls.Add(this.ActivationPanel);
            this.FilterGB.Controls.Add(this.SubsetPanel);
            this.FilterGB.Controls.Add(this.MSLevelPanel);
            this.FilterGB.Controls.Add(this.PeakPickingPanel);
            this.FilterGB.Controls.Add(this.ZeroSamplesPanel);
            this.FilterGB.Controls.Add(this.ETDFilterPanel);
            this.FilterGB.Controls.Add(this.ThresholdFilterPanel);
            this.FilterGB.Location = new System.Drawing.Point(429, 102);
            this.FilterGB.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.FilterGB.Name = "FilterGB";
            this.FilterGB.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.FilterGB.Size = new System.Drawing.Size(436, 176);
            this.FilterGB.TabIndex = 9;
            this.FilterGB.TabStop = false;
            this.FilterGB.Text = "Filters";
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
            "Threshold Peak Filter",
            "Charge State Predictor",
            "Activation",
            "Subset"});
            this.FilterBox.Location = new System.Drawing.Point(129, 23);
            this.FilterBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.FilterBox.Name = "FilterBox";
            this.FilterBox.Size = new System.Drawing.Size(175, 24);
            this.FilterBox.TabIndex = 0;
            this.FilterBox.SelectedIndexChanged += new System.EventHandler(this.FilterBox_SelectedIndexChanged);
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
            this.ChargeStatePredictorPanel.Location = new System.Drawing.Point(29, 57);
            this.ChargeStatePredictorPanel.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.ChargeStatePredictorPanel.Name = "ChargeStatePredictorPanel";
            this.ChargeStatePredictorPanel.Size = new System.Drawing.Size(377, 112);
            this.ChargeStatePredictorPanel.TabIndex = 4;
            this.ChargeStatePredictorPanel.Visible = false;
            // 
            // ChaMCMaxLabel
            // 
            this.ChaMCMaxLabel.AutoSize = true;
            this.ChaMCMaxLabel.Location = new System.Drawing.Point(243, 81);
            this.ChaMCMaxLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.ChaMCMaxLabel.Name = "ChaMCMaxLabel";
            this.ChaMCMaxLabel.Size = new System.Drawing.Size(37, 17);
            this.ChaMCMaxLabel.TabIndex = 19;
            this.ChaMCMaxLabel.Text = "Max:";
            // 
            // ChaMCMaxBox
            // 
            this.ChaMCMaxBox.Location = new System.Drawing.Point(287, 76);
            this.ChaMCMaxBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.ChaMCMaxBox.Name = "ChaMCMaxBox";
            this.ChaMCMaxBox.Size = new System.Drawing.Size(48, 22);
            this.ChaMCMaxBox.TabIndex = 18;
            this.ChaMCMaxBox.Text = "3";
            this.ChaMCMaxBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumTextBox_KeyPress);
            // 
            // ChaMCMinBox
            // 
            this.ChaMCMinBox.Location = new System.Drawing.Point(177, 76);
            this.ChaMCMinBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.ChaMCMinBox.Name = "ChaMCMinBox";
            this.ChaMCMinBox.Size = new System.Drawing.Size(48, 22);
            this.ChaMCMinBox.TabIndex = 17;
            this.ChaMCMinBox.Text = "2";
            this.ChaMCMinBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumTextBox_KeyPress);
            // 
            // ChaMCMinLabel
            // 
            this.ChaMCMinLabel.AutoSize = true;
            this.ChaMCMinLabel.Location = new System.Drawing.Point(40, 81);
            this.ChaMCMinLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.ChaMCMinLabel.Name = "ChaMCMinLabel";
            this.ChaMCMinLabel.Size = new System.Drawing.Size(136, 17);
            this.ChaMCMinLabel.TabIndex = 9;
            this.ChaMCMinLabel.Text = "Multiple Charge Min:";
            // 
            // ChaSingleBox
            // 
            this.ChaSingleBox.DecimalPlaces = 2;
            this.ChaSingleBox.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.ChaSingleBox.Location = new System.Drawing.Point(232, 44);
            this.ChaSingleBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.ChaSingleBox.Maximum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.ChaSingleBox.Name = "ChaSingleBox";
            this.ChaSingleBox.Size = new System.Drawing.Size(55, 22);
            this.ChaSingleBox.TabIndex = 8;
            this.ChaSingleBox.Value = new decimal(new int[] {
            9,
            0,
            0,
            65536});
            // 
            // ChaSingleLabel
            // 
            this.ChaSingleLabel.AutoSize = true;
            this.ChaSingleLabel.Location = new System.Drawing.Point(89, 47);
            this.ChaSingleLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.ChaSingleLabel.Name = "ChaSingleLabel";
            this.ChaSingleLabel.Size = new System.Drawing.Size(142, 17);
            this.ChaSingleLabel.TabIndex = 7;
            this.ChaSingleLabel.Text = "Single Charge % TIC:";
            // 
            // ChaOverwriteCharge
            // 
            this.ChaOverwriteCharge.AutoSize = true;
            this.ChaOverwriteCharge.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.ChaOverwriteCharge.Location = new System.Drawing.Point(115, 15);
            this.ChaOverwriteCharge.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.ChaOverwriteCharge.Name = "ChaOverwriteCharge";
            this.ChaOverwriteCharge.Size = new System.Drawing.Size(144, 21);
            this.ChaOverwriteCharge.TabIndex = 6;
            this.ChaOverwriteCharge.Text = "Overwrite Charge:";
            this.ChaOverwriteCharge.UseVisualStyleBackColor = true;
            // 
            // ActivationPanel
            // 
            this.ActivationPanel.Controls.Add(this.ActivationTypeBox);
            this.ActivationPanel.Controls.Add(this.ActivationTypeLabel);
            this.ActivationPanel.Location = new System.Drawing.Point(29, 57);
            this.ActivationPanel.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.ActivationPanel.Name = "ActivationPanel";
            this.ActivationPanel.Size = new System.Drawing.Size(377, 112);
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
            this.ActivationTypeBox.Location = new System.Drawing.Point(161, 43);
            this.ActivationTypeBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.ActivationTypeBox.MaxDropDownItems = 16;
            this.ActivationTypeBox.Name = "ActivationTypeBox";
            this.ActivationTypeBox.Size = new System.Drawing.Size(89, 24);
            this.ActivationTypeBox.Sorted = true;
            this.ActivationTypeBox.TabIndex = 14;
            // 
            // ActivationTypeLabel
            // 
            this.ActivationTypeLabel.AutoSize = true;
            this.ActivationTypeLabel.Location = new System.Drawing.Point(108, 46);
            this.ActivationTypeLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.ActivationTypeLabel.Name = "ActivationTypeLabel";
            this.ActivationTypeLabel.Size = new System.Drawing.Size(44, 17);
            this.ActivationTypeLabel.TabIndex = 15;
            this.ActivationTypeLabel.Text = "Type:";
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
            this.SubsetPanel.Location = new System.Drawing.Point(29, 57);
            this.SubsetPanel.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.SubsetPanel.Name = "SubsetPanel";
            this.SubsetPanel.Size = new System.Drawing.Size(377, 112);
            this.SubsetPanel.TabIndex = 6;
            this.SubsetPanel.Visible = false;
            // 
            // mzWinLabel2
            // 
            this.mzWinLabel2.AutoSize = true;
            this.mzWinLabel2.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.mzWinLabel2.Location = new System.Drawing.Point(232, 74);
            this.mzWinLabel2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.mzWinLabel2.Name = "mzWinLabel2";
            this.mzWinLabel2.Size = new System.Drawing.Size(20, 25);
            this.mzWinLabel2.TabIndex = 16;
            this.mzWinLabel2.Text = "-";
            // 
            // ScanNumberHigh
            // 
            this.ScanNumberHigh.Location = new System.Drawing.Point(255, 12);
            this.ScanNumberHigh.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.ScanNumberHigh.Name = "ScanNumberHigh";
            this.ScanNumberHigh.Size = new System.Drawing.Size(48, 22);
            this.ScanNumberHigh.TabIndex = 11;
            // 
            // ScanTimeLow
            // 
            this.ScanTimeLow.Location = new System.Drawing.Point(180, 44);
            this.ScanTimeLow.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.ScanTimeLow.Name = "ScanTimeLow";
            this.ScanTimeLow.Size = new System.Drawing.Size(48, 22);
            this.ScanTimeLow.TabIndex = 0;
            // 
            // mzWinHigh
            // 
            this.mzWinHigh.Location = new System.Drawing.Point(255, 76);
            this.mzWinHigh.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.mzWinHigh.Name = "mzWinHigh";
            this.mzWinHigh.Size = new System.Drawing.Size(48, 22);
            this.mzWinHigh.TabIndex = 15;
            // 
            // ScanTimeHigh
            // 
            this.ScanTimeHigh.Location = new System.Drawing.Point(255, 44);
            this.ScanTimeHigh.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.ScanTimeHigh.Name = "ScanTimeHigh";
            this.ScanTimeHigh.Size = new System.Drawing.Size(48, 22);
            this.ScanTimeHigh.TabIndex = 1;
            // 
            // ScanTimeLabel2
            // 
            this.ScanTimeLabel2.AutoSize = true;
            this.ScanTimeLabel2.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ScanTimeLabel2.Location = new System.Drawing.Point(232, 42);
            this.ScanTimeLabel2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.ScanTimeLabel2.Name = "ScanTimeLabel2";
            this.ScanTimeLabel2.Size = new System.Drawing.Size(20, 25);
            this.ScanTimeLabel2.TabIndex = 2;
            this.ScanTimeLabel2.Text = "-";
            // 
            // mzWinLow
            // 
            this.mzWinLow.Location = new System.Drawing.Point(180, 76);
            this.mzWinLow.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.mzWinLow.Name = "mzWinLow";
            this.mzWinLow.Size = new System.Drawing.Size(48, 22);
            this.mzWinLow.TabIndex = 14;
            // 
            // ScanTimeLabel
            // 
            this.ScanTimeLabel.AutoSize = true;
            this.ScanTimeLabel.Location = new System.Drawing.Point(91, 50);
            this.ScanTimeLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.ScanTimeLabel.Name = "ScanTimeLabel";
            this.ScanTimeLabel.Size = new System.Drawing.Size(79, 17);
            this.ScanTimeLabel.TabIndex = 3;
            this.ScanTimeLabel.Text = "Scan Time:";
            // 
            // mzWinLabel
            // 
            this.mzWinLabel.AutoSize = true;
            this.mzWinLabel.Location = new System.Drawing.Point(85, 82);
            this.mzWinLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.mzWinLabel.Name = "mzWinLabel";
            this.mzWinLabel.Size = new System.Drawing.Size(83, 17);
            this.mzWinLabel.TabIndex = 6;
            this.mzWinLabel.Text = "mz Window:";
            // 
            // ScanNumberLabel
            // 
            this.ScanNumberLabel.AutoSize = true;
            this.ScanNumberLabel.Location = new System.Drawing.Point(72, 15);
            this.ScanNumberLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.ScanNumberLabel.Name = "ScanNumberLabel";
            this.ScanNumberLabel.Size = new System.Drawing.Size(98, 17);
            this.ScanNumberLabel.TabIndex = 13;
            this.ScanNumberLabel.Text = "Scan Number:";
            // 
            // ScanNumberLow
            // 
            this.ScanNumberLow.Location = new System.Drawing.Point(180, 12);
            this.ScanNumberLow.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.ScanNumberLow.Name = "ScanNumberLow";
            this.ScanNumberLow.Size = new System.Drawing.Size(48, 22);
            this.ScanNumberLow.TabIndex = 10;
            // 
            // ScanNumberLabel2
            // 
            this.ScanNumberLabel2.AutoSize = true;
            this.ScanNumberLabel2.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ScanNumberLabel2.Location = new System.Drawing.Point(232, 10);
            this.ScanNumberLabel2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.ScanNumberLabel2.Name = "ScanNumberLabel2";
            this.ScanNumberLabel2.Size = new System.Drawing.Size(20, 25);
            this.ScanNumberLabel2.TabIndex = 12;
            this.ScanNumberLabel2.Text = "-";
            // 
            // MSLevelPanel
            // 
            this.MSLevelPanel.Controls.Add(this.MSLevelLabel);
            this.MSLevelPanel.Controls.Add(this.MSLabelSeperator);
            this.MSLevelPanel.Controls.Add(this.MSLevelHigh);
            this.MSLevelPanel.Controls.Add(this.MSLevelLow);
            this.MSLevelPanel.Location = new System.Drawing.Point(29, 57);
            this.MSLevelPanel.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.MSLevelPanel.Name = "MSLevelPanel";
            this.MSLevelPanel.Size = new System.Drawing.Size(377, 112);
            this.MSLevelPanel.TabIndex = 1;
            this.MSLevelPanel.Visible = false;
            // 
            // MSLevelLabel
            // 
            this.MSLevelLabel.AutoSize = true;
            this.MSLevelLabel.Location = new System.Drawing.Point(161, 23);
            this.MSLevelLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.MSLevelLabel.Name = "MSLevelLabel";
            this.MSLevelLabel.Size = new System.Drawing.Size(53, 17);
            this.MSLevelLabel.TabIndex = 3;
            this.MSLevelLabel.Text = "Levels:";
            // 
            // MSLabelSeperator
            // 
            this.MSLabelSeperator.AutoSize = true;
            this.MSLabelSeperator.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MSLabelSeperator.Location = new System.Drawing.Point(179, 42);
            this.MSLabelSeperator.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.MSLabelSeperator.Name = "MSLabelSeperator";
            this.MSLabelSeperator.Size = new System.Drawing.Size(20, 25);
            this.MSLabelSeperator.TabIndex = 2;
            this.MSLabelSeperator.Text = "-";
            // 
            // MSLevelHigh
            // 
            this.MSLevelHigh.Location = new System.Drawing.Point(201, 44);
            this.MSLevelHigh.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.MSLevelHigh.Name = "MSLevelHigh";
            this.MSLevelHigh.Size = new System.Drawing.Size(48, 22);
            this.MSLevelHigh.TabIndex = 1;
            this.MSLevelHigh.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumTextBox_KeyPress);
            // 
            // MSLevelLow
            // 
            this.MSLevelLow.Location = new System.Drawing.Point(127, 44);
            this.MSLevelLow.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.MSLevelLow.Name = "MSLevelLow";
            this.MSLevelLow.Size = new System.Drawing.Size(48, 22);
            this.MSLevelLow.TabIndex = 0;
            this.MSLevelLow.Text = "1";
            this.MSLevelLow.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumTextBox_KeyPress);
            // 
            // PeakPickingPanel
            // 
            this.PeakPickingPanel.Controls.Add(this.PeakPreferVendorBox);
            this.PeakPickingPanel.Controls.Add(this.PeakMSLevelLabel);
            this.PeakPickingPanel.Controls.Add(this.PeakMSLevelSeperator);
            this.PeakPickingPanel.Controls.Add(this.PeakMSLevelHigh);
            this.PeakPickingPanel.Controls.Add(this.PeakMSLevelLow);
            this.PeakPickingPanel.Location = new System.Drawing.Point(29, 57);
            this.PeakPickingPanel.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.PeakPickingPanel.Name = "PeakPickingPanel";
            this.PeakPickingPanel.Size = new System.Drawing.Size(377, 112);
            this.PeakPickingPanel.TabIndex = 2;
            this.PeakPickingPanel.Visible = false;
            // 
            // PeakPreferVendorBox
            // 
            this.PeakPreferVendorBox.AutoSize = true;
            this.PeakPreferVendorBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.PeakPreferVendorBox.Checked = true;
            this.PeakPreferVendorBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.PeakPreferVendorBox.Location = new System.Drawing.Point(125, 15);
            this.PeakPreferVendorBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.PeakPreferVendorBox.Name = "PeakPreferVendorBox";
            this.PeakPreferVendorBox.Size = new System.Drawing.Size(123, 21);
            this.PeakPreferVendorBox.TabIndex = 21;
            this.PeakPreferVendorBox.Text = "Prefer Vendor:";
            this.PeakPreferVendorBox.UseVisualStyleBackColor = true;
            // 
            // PeakMSLevelLabel
            // 
            this.PeakMSLevelLabel.AutoSize = true;
            this.PeakMSLevelLabel.Location = new System.Drawing.Point(151, 50);
            this.PeakMSLevelLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.PeakMSLevelLabel.Name = "PeakMSLevelLabel";
            this.PeakMSLevelLabel.Size = new System.Drawing.Size(77, 17);
            this.PeakMSLevelLabel.TabIndex = 20;
            this.PeakMSLevelLabel.Text = "MS Levels:";
            // 
            // PeakMSLevelSeperator
            // 
            this.PeakMSLevelSeperator.AutoSize = true;
            this.PeakMSLevelSeperator.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.PeakMSLevelSeperator.Location = new System.Drawing.Point(179, 68);
            this.PeakMSLevelSeperator.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.PeakMSLevelSeperator.Name = "PeakMSLevelSeperator";
            this.PeakMSLevelSeperator.Size = new System.Drawing.Size(20, 25);
            this.PeakMSLevelSeperator.TabIndex = 19;
            this.PeakMSLevelSeperator.Text = "-";
            // 
            // PeakMSLevelHigh
            // 
            this.PeakMSLevelHigh.Location = new System.Drawing.Point(201, 70);
            this.PeakMSLevelHigh.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.PeakMSLevelHigh.Name = "PeakMSLevelHigh";
            this.PeakMSLevelHigh.Size = new System.Drawing.Size(48, 22);
            this.PeakMSLevelHigh.TabIndex = 18;
            this.PeakMSLevelHigh.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumTextBox_KeyPress);
            // 
            // PeakMSLevelLow
            // 
            this.PeakMSLevelLow.Location = new System.Drawing.Point(127, 70);
            this.PeakMSLevelLow.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.PeakMSLevelLow.Name = "PeakMSLevelLow";
            this.PeakMSLevelLow.Size = new System.Drawing.Size(48, 22);
            this.PeakMSLevelLow.TabIndex = 17;
            this.PeakMSLevelLow.Text = "1";
            this.PeakMSLevelLow.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumTextBox_KeyPress);
            // 
            // ZeroSamplesPanel
            // 
            this.ZeroSamplesPanel.Controls.Add(this.ZeroSamplesAddMissing);
            this.ZeroSamplesPanel.Controls.Add(this.ZeroSamplesAddMissingFlankCountBox);
            this.ZeroSamplesPanel.Controls.Add(this.ZeroSamplesRemove);
            this.ZeroSamplesPanel.Controls.Add(this.ZeroSamplesMSLevelLabel);
            this.ZeroSamplesPanel.Controls.Add(this.ZeroSamplesMSLevelSeperator);
            this.ZeroSamplesPanel.Controls.Add(this.ZeroSamplesMSLevelHigh);
            this.ZeroSamplesPanel.Controls.Add(this.ZeroSamplesMSLevelLow);
            this.ZeroSamplesPanel.Location = new System.Drawing.Point(29, 57);
            this.ZeroSamplesPanel.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.ZeroSamplesPanel.Name = "ZeroSamplesPanel";
            this.ZeroSamplesPanel.Size = new System.Drawing.Size(377, 112);
            this.ZeroSamplesPanel.TabIndex = 24;
            this.ZeroSamplesPanel.Visible = false;
            // 
            // ZeroSamplesAddMissing
            // 
            this.ZeroSamplesAddMissing.AutoSize = true;
            this.ZeroSamplesAddMissing.Location = new System.Drawing.Point(124, 15);
            this.ZeroSamplesAddMissing.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.ZeroSamplesAddMissing.Name = "ZeroSamplesAddMissing";
            this.ZeroSamplesAddMissing.Size = new System.Drawing.Size(166, 21);
            this.ZeroSamplesAddMissing.TabIndex = 30;
            this.ZeroSamplesAddMissing.TabStop = true;
            this.ZeroSamplesAddMissing.Text = "Add missing, flank by:";
            this.ZeroSamplesAddMissing.UseVisualStyleBackColor = true;
            this.ZeroSamplesAddMissing.Click += new System.EventHandler(this.ZeroSamples_ModeChanged);
            // 
            // ZeroSamplesAddMissingFlankCountBox
            // 
            this.ZeroSamplesAddMissingFlankCountBox.Enabled = this.ZeroSamplesAddMissing.Checked;
            this.ZeroSamplesAddMissingFlankCountBox.Location = new System.Drawing.Point(295, 15);
            this.ZeroSamplesAddMissingFlankCountBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.ZeroSamplesAddMissingFlankCountBox.Name = "ZeroSamplesAddMissingFlankCountBox";
            this.ZeroSamplesAddMissingFlankCountBox.Size = new System.Drawing.Size(48, 22);
            this.ZeroSamplesAddMissingFlankCountBox.TabIndex = 31;
            this.ZeroSamplesAddMissingFlankCountBox.Text = "5";
            this.ZeroSamplesAddMissingFlankCountBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumTextBox_KeyPress);
            // 
            // ZeroSamplesRemove
            // 
            this.ZeroSamplesRemove.AutoSize = true;
            this.ZeroSamplesRemove.Checked = true;
            this.ZeroSamplesRemove.Location = new System.Drawing.Point(27, 15);
            this.ZeroSamplesRemove.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.ZeroSamplesRemove.Name = "ZeroSamplesRemove";
            this.ZeroSamplesRemove.Size = new System.Drawing.Size(81, 21);
            this.ZeroSamplesRemove.TabIndex = 29;
            this.ZeroSamplesRemove.TabStop = true;
            this.ZeroSamplesRemove.Text = "Remove";
            this.ZeroSamplesRemove.UseVisualStyleBackColor = true;
            this.ZeroSamplesRemove.Click += new System.EventHandler(this.ZeroSamples_ModeChanged);
            // 
            // ZeroSamplesMSLevelLabel
            // 
            this.ZeroSamplesMSLevelLabel.AutoSize = true;
            this.ZeroSamplesMSLevelLabel.Location = new System.Drawing.Point(151, 50);
            this.ZeroSamplesMSLevelLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.ZeroSamplesMSLevelLabel.Name = "ZeroSamplesMSLevelLabel";
            this.ZeroSamplesMSLevelLabel.Size = new System.Drawing.Size(77, 17);
            this.ZeroSamplesMSLevelLabel.TabIndex = 25;
            this.ZeroSamplesMSLevelLabel.Text = "MS Levels:";
            // 
            // ZeroSamplesMSLevelSeperator
            // 
            this.ZeroSamplesMSLevelSeperator.AutoSize = true;
            this.ZeroSamplesMSLevelSeperator.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ZeroSamplesMSLevelSeperator.Location = new System.Drawing.Point(179, 68);
            this.ZeroSamplesMSLevelSeperator.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.ZeroSamplesMSLevelSeperator.Name = "ZeroSamplesMSLevelSeperator";
            this.ZeroSamplesMSLevelSeperator.Size = new System.Drawing.Size(20, 25);
            this.ZeroSamplesMSLevelSeperator.TabIndex = 26;
            this.ZeroSamplesMSLevelSeperator.Text = "-";
            // 
            // ZeroSamplesMSLevelHigh
            // 
            this.ZeroSamplesMSLevelHigh.Location = new System.Drawing.Point(201, 70);
            this.ZeroSamplesMSLevelHigh.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.ZeroSamplesMSLevelHigh.Name = "ZeroSamplesMSLevelHigh";
            this.ZeroSamplesMSLevelHigh.Size = new System.Drawing.Size(48, 22);
            this.ZeroSamplesMSLevelHigh.TabIndex = 28;
            this.ZeroSamplesMSLevelHigh.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumTextBox_KeyPress);
            // 
            // ZeroSamplesMSLevelLow
            // 
            this.ZeroSamplesMSLevelLow.Location = new System.Drawing.Point(127, 70);
            this.ZeroSamplesMSLevelLow.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.ZeroSamplesMSLevelLow.Name = "ZeroSamplesMSLevelLow";
            this.ZeroSamplesMSLevelLow.Size = new System.Drawing.Size(48, 22);
            this.ZeroSamplesMSLevelLow.TabIndex = 27;
            this.ZeroSamplesMSLevelLow.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumTextBox_KeyPress);
            // 
            // ETDFilterPanel
            // 
            this.ETDFilterPanel.Controls.Add(this.ETDBlanketRemovalBox);
            this.ETDFilterPanel.Controls.Add(this.ETDRemoveChargeReducedBox);
            this.ETDFilterPanel.Controls.Add(this.ETDRemoveNeutralLossBox);
            this.ETDFilterPanel.Controls.Add(this.ETDRemovePrecursorBox);
            this.ETDFilterPanel.Location = new System.Drawing.Point(29, 57);
            this.ETDFilterPanel.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.ETDFilterPanel.Name = "ETDFilterPanel";
            this.ETDFilterPanel.Size = new System.Drawing.Size(377, 112);
            this.ETDFilterPanel.TabIndex = 3;
            this.ETDFilterPanel.Visible = false;
            // 
            // ETDBlanketRemovalBox
            // 
            this.ETDBlanketRemovalBox.AutoSize = true;
            this.ETDBlanketRemovalBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.ETDBlanketRemovalBox.Location = new System.Drawing.Point(144, 87);
            this.ETDBlanketRemovalBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.ETDBlanketRemovalBox.Name = "ETDBlanketRemovalBox";
            this.ETDBlanketRemovalBox.Size = new System.Drawing.Size(140, 21);
            this.ETDBlanketRemovalBox.TabIndex = 9;
            this.ETDBlanketRemovalBox.Text = "Blanket Removal:";
            this.ETDBlanketRemovalBox.UseVisualStyleBackColor = true;
            // 
            // ETDRemoveChargeReducedBox
            // 
            this.ETDRemoveChargeReducedBox.AutoSize = true;
            this.ETDRemoveChargeReducedBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.ETDRemoveChargeReducedBox.Checked = true;
            this.ETDRemoveChargeReducedBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ETDRemoveChargeReducedBox.Location = new System.Drawing.Point(87, 31);
            this.ETDRemoveChargeReducedBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.ETDRemoveChargeReducedBox.Name = "ETDRemoveChargeReducedBox";
            this.ETDRemoveChargeReducedBox.Size = new System.Drawing.Size(197, 21);
            this.ETDRemoveChargeReducedBox.TabIndex = 8;
            this.ETDRemoveChargeReducedBox.Text = "Remove Charge Reduced:";
            this.ETDRemoveChargeReducedBox.UseVisualStyleBackColor = true;
            // 
            // ETDRemoveNeutralLossBox
            // 
            this.ETDRemoveNeutralLossBox.AutoSize = true;
            this.ETDRemoveNeutralLossBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.ETDRemoveNeutralLossBox.Checked = true;
            this.ETDRemoveNeutralLossBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ETDRemoveNeutralLossBox.Location = new System.Drawing.Point(116, 59);
            this.ETDRemoveNeutralLossBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.ETDRemoveNeutralLossBox.Name = "ETDRemoveNeutralLossBox";
            this.ETDRemoveNeutralLossBox.Size = new System.Drawing.Size(170, 21);
            this.ETDRemoveNeutralLossBox.TabIndex = 7;
            this.ETDRemoveNeutralLossBox.Text = "Remove Neutral Loss:";
            this.ETDRemoveNeutralLossBox.UseVisualStyleBackColor = true;
            // 
            // ETDRemovePrecursorBox
            // 
            this.ETDRemovePrecursorBox.AutoSize = true;
            this.ETDRemovePrecursorBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.ETDRemovePrecursorBox.Checked = true;
            this.ETDRemovePrecursorBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ETDRemovePrecursorBox.Location = new System.Drawing.Point(135, 2);
            this.ETDRemovePrecursorBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.ETDRemovePrecursorBox.Name = "ETDRemovePrecursorBox";
            this.ETDRemovePrecursorBox.Size = new System.Drawing.Size(152, 21);
            this.ETDRemovePrecursorBox.TabIndex = 6;
            this.ETDRemovePrecursorBox.Text = "Remove Precursor:";
            this.ETDRemovePrecursorBox.UseVisualStyleBackColor = true;
            // 
            // ThresholdFilterPanel
            // 
            this.ThresholdFilterPanel.Controls.Add(this.thresholdValueLabel);
            this.ThresholdFilterPanel.Controls.Add(this.thresholdOrientationLabel);
            this.ThresholdFilterPanel.Controls.Add(this.thresholdTypeLabel);
            this.ThresholdFilterPanel.Controls.Add(this.thresholdOrientationComboBox);
            this.ThresholdFilterPanel.Controls.Add(this.thresholdValueTextBox);
            this.ThresholdFilterPanel.Controls.Add(this.thresholdTypeComboBox);
            this.ThresholdFilterPanel.Location = new System.Drawing.Point(29, 57);
            this.ThresholdFilterPanel.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.ThresholdFilterPanel.Name = "ThresholdFilterPanel";
            this.ThresholdFilterPanel.Size = new System.Drawing.Size(377, 112);
            this.ThresholdFilterPanel.TabIndex = 20;
            this.ThresholdFilterPanel.Visible = false;
            // 
            // thresholdValueLabel
            // 
            this.thresholdValueLabel.AutoSize = true;
            this.thresholdValueLabel.Location = new System.Drawing.Point(92, 79);
            this.thresholdValueLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.thresholdValueLabel.Name = "thresholdValueLabel";
            this.thresholdValueLabel.Size = new System.Drawing.Size(48, 17);
            this.thresholdValueLabel.TabIndex = 16;
            this.thresholdValueLabel.Text = "Value:";
            // 
            // thresholdOrientationLabel
            // 
            this.thresholdOrientationLabel.AutoSize = true;
            this.thresholdOrientationLabel.Location = new System.Drawing.Point(60, 46);
            this.thresholdOrientationLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.thresholdOrientationLabel.Name = "thresholdOrientationLabel";
            this.thresholdOrientationLabel.Size = new System.Drawing.Size(82, 17);
            this.thresholdOrientationLabel.TabIndex = 15;
            this.thresholdOrientationLabel.Text = "Orientation:";
            // 
            // thresholdTypeLabel
            // 
            this.thresholdTypeLabel.AutoSize = true;
            this.thresholdTypeLabel.Location = new System.Drawing.Point(35, 12);
            this.thresholdTypeLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.thresholdTypeLabel.Name = "thresholdTypeLabel";
            this.thresholdTypeLabel.Size = new System.Drawing.Size(107, 17);
            this.thresholdTypeLabel.TabIndex = 14;
            this.thresholdTypeLabel.Text = "Threshold type:";
            // 
            // thresholdOrientationComboBox
            // 
            this.thresholdOrientationComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.thresholdOrientationComboBox.FormattingEnabled = true;
            this.thresholdOrientationComboBox.Items.AddRange(new object[] {
            "Most intense",
            "Least intense"});
            this.thresholdOrientationComboBox.Location = new System.Drawing.Point(151, 42);
            this.thresholdOrientationComboBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.thresholdOrientationComboBox.Name = "thresholdOrientationComboBox";
            this.thresholdOrientationComboBox.Size = new System.Drawing.Size(160, 24);
            this.thresholdOrientationComboBox.TabIndex = 2;
            // 
            // thresholdValueTextBox
            // 
            this.thresholdValueTextBox.Location = new System.Drawing.Point(152, 75);
            this.thresholdValueTextBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.thresholdValueTextBox.Name = "thresholdValueTextBox";
            this.thresholdValueTextBox.Size = new System.Drawing.Size(159, 22);
            this.thresholdValueTextBox.TabIndex = 1;
            // 
            // thresholdTypeComboBox
            // 
            this.thresholdTypeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.thresholdTypeComboBox.FormattingEnabled = true;
            this.thresholdTypeComboBox.Location = new System.Drawing.Point(151, 9);
            this.thresholdTypeComboBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.thresholdTypeComboBox.Name = "thresholdTypeComboBox";
            this.thresholdTypeComboBox.Size = new System.Drawing.Size(160, 24);
            this.thresholdTypeComboBox.TabIndex = 0;
            // 
            // RemoveFilterButton
            // 
            this.RemoveFilterButton.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.RemoveFilterButton.Location = new System.Drawing.Point(636, 286);
            this.RemoveFilterButton.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.RemoveFilterButton.Name = "RemoveFilterButton";
            this.RemoveFilterButton.Size = new System.Drawing.Size(77, 28);
            this.RemoveFilterButton.TabIndex = 11;
            this.RemoveFilterButton.Text = "Remove";
            this.RemoveFilterButton.UseVisualStyleBackColor = true;
            this.RemoveFilterButton.Click += new System.EventHandler(this.RemoveFilterButton_Click);
            // 
            // AddFilterButton
            // 
            this.AddFilterButton.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.AddFilterButton.Location = new System.Drawing.Point(572, 286);
            this.AddFilterButton.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.AddFilterButton.Name = "AddFilterButton";
            this.AddFilterButton.Size = new System.Drawing.Size(56, 28);
            this.AddFilterButton.TabIndex = 10;
            this.AddFilterButton.Text = "Add";
            this.AddFilterButton.UseVisualStyleBackColor = true;
            this.AddFilterButton.Click += new System.EventHandler(this.AddFilterButton_Click);
            // 
            // TextFileRadio
            // 
            this.TextFileRadio.AutoSize = true;
            this.TextFileRadio.Location = new System.Drawing.Point(196, 15);
            this.TextFileRadio.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.TextFileRadio.Name = "TextFileRadio";
            this.TextFileRadio.Size = new System.Drawing.Size(135, 21);
            this.TextFileRadio.TabIndex = 2;
            this.TextFileRadio.Text = "File of file names";
            this.TextFileRadio.UseVisualStyleBackColor = true;
            // 
            // FileListRadio
            // 
            this.FileListRadio.AutoSize = true;
            this.FileListRadio.Checked = true;
            this.FileListRadio.Location = new System.Drawing.Point(85, 15);
            this.FileListRadio.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.FileListRadio.Name = "FileListRadio";
            this.FileListRadio.Size = new System.Drawing.Size(100, 21);
            this.FileListRadio.TabIndex = 1;
            this.FileListRadio.TabStop = true;
            this.FileListRadio.Text = "List of Files";
            this.FileListRadio.UseVisualStyleBackColor = true;
            this.FileListRadio.CheckedChanged += new System.EventHandler(this.FileListRadio_CheckedChanged);
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
            "ms1",
            "cms1",
            "ms2",
            "cms2"});
            this.OutputFormatBox.Location = new System.Drawing.Point(119, 20);
            this.OutputFormatBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.OutputFormatBox.Name = "OutputFormatBox";
            this.OutputFormatBox.Size = new System.Drawing.Size(79, 24);
            this.OutputFormatBox.TabIndex = 1;
            this.OutputFormatBox.SelectedIndexChanged += new System.EventHandler(this.OutputFormatBox_SelectedIndexChanged);
            // 
            // FormatLabel
            // 
            this.FormatLabel.AutoSize = true;
            this.FormatLabel.Location = new System.Drawing.Point(19, 23);
            this.FormatLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.FormatLabel.Name = "FormatLabel";
            this.FormatLabel.Size = new System.Drawing.Size(99, 17);
            this.FormatLabel.TabIndex = 13;
            this.FormatLabel.Text = "Output format:";
            // 
            // BrowseFileButton
            // 
            this.BrowseFileButton.Location = new System.Drawing.Point(308, 41);
            this.BrowseFileButton.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.BrowseFileButton.Name = "BrowseFileButton";
            this.BrowseFileButton.Size = new System.Drawing.Size(67, 28);
            this.BrowseFileButton.TabIndex = 4;
            this.BrowseFileButton.Text = "Browse";
            this.BrowseFileButton.UseVisualStyleBackColor = true;
            this.BrowseFileButton.Click += new System.EventHandler(this.BrowseFileButton_Click);
            // 
            // OutputBrowse
            // 
            this.OutputBrowse.Location = new System.Drawing.Point(295, 20);
            this.OutputBrowse.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.OutputBrowse.Name = "OutputBrowse";
            this.OutputBrowse.Size = new System.Drawing.Size(67, 28);
            this.OutputBrowse.TabIndex = 2;
            this.OutputBrowse.Text = "Browse";
            this.OutputBrowse.UseVisualStyleBackColor = true;
            this.OutputBrowse.Click += new System.EventHandler(this.OutputBrowse_Click);
            // 
            // OutputLabel
            // 
            this.OutputLabel.AutoSize = true;
            this.OutputLabel.Location = new System.Drawing.Point(3, 2);
            this.OutputLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.OutputLabel.Name = "OutputLabel";
            this.OutputLabel.Size = new System.Drawing.Size(116, 17);
            this.OutputLabel.TabIndex = 16;
            this.OutputLabel.Text = "Output Directory:";
            // 
            // OutputBox
            // 
            this.OutputBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Append;
            this.OutputBox.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystemDirectories;
            this.OutputBox.Location = new System.Drawing.Point(7, 22);
            this.OutputBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.OutputBox.Name = "OutputBox";
            this.OutputBox.Size = new System.Drawing.Size(279, 22);
            this.OutputBox.TabIndex = 1;
            // 
            // PrecisionLabel
            // 
            this.PrecisionLabel.AutoSize = true;
            this.PrecisionLabel.Location = new System.Drawing.Point(17, 55);
            this.PrecisionLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.PrecisionLabel.Name = "PrecisionLabel";
            this.PrecisionLabel.Size = new System.Drawing.Size(175, 17);
            this.PrecisionLabel.TabIndex = 18;
            this.PrecisionLabel.Text = "Binary encoding precision:";
            // 
            // OptionsGB
            // 
            this.OptionsGB.Controls.Add(this.NumpressSlofBox);
            this.OptionsGB.Controls.Add(this.NumpressLinearBox);
            this.OptionsGB.Controls.Add(this.NumpressPicBox);
            this.OptionsGB.Controls.Add(this.MakeTPPCompatibleOutputButton);
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
            this.OptionsGB.Location = new System.Drawing.Point(3, 54);
            this.OptionsGB.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.OptionsGB.Name = "OptionsGB";
            this.OptionsGB.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.OptionsGB.Size = new System.Drawing.Size(359, 226);
            this.OptionsGB.TabIndex = 3;
            this.OptionsGB.TabStop = false;
            this.OptionsGB.Text = "Options";
            // 
            // NumpressSlofBox
            // 
            this.NumpressSlofBox.AutoSize = true;
            this.NumpressSlofBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.NumpressSlofBox.Location = new System.Drawing.Point(4, 169);
            this.NumpressSlofBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.NumpressSlofBox.Name = "NumpressSlofBox";
            this.NumpressSlofBox.Size = new System.Drawing.Size(431, 26);
            this.NumpressSlofBox.TabIndex = 25;
            this.NumpressSlofBox.Text = "Use numpress short logged float compression:";
            this.NumpressSlofBox.UseVisualStyleBackColor = true;
            this.NumpressSlofBox.CheckedChanged += new System.EventHandler(this.NumpressSlofBox_CheckedChanged);
            // 
            // NumpressLinearBox
            // 
            this.NumpressLinearBox.AutoSize = true;
            this.NumpressLinearBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.NumpressLinearBox.Location = new System.Drawing.Point(4, 142);
            this.NumpressLinearBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.NumpressLinearBox.Name = "NumpressLinearBox";
            this.NumpressLinearBox.Size = new System.Drawing.Size(331, 26);
            this.NumpressLinearBox.TabIndex = 24;
            this.NumpressLinearBox.Text = "Use numpress linear compression:";
            this.NumpressLinearBox.UseVisualStyleBackColor = true;
            // 
            // NumpressPicBox
            // 
            this.NumpressPicBox.AutoSize = true;
            this.NumpressPicBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.NumpressPicBox.Location = new System.Drawing.Point(5, 198);
            this.NumpressPicBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.NumpressPicBox.Name = "NumpressPicBox";
            this.NumpressPicBox.Size = new System.Drawing.Size(460, 26);
            this.NumpressPicBox.TabIndex = 26;
            this.NumpressPicBox.Text = "Use numpress short positive integer compression:";
            this.NumpressPicBox.UseVisualStyleBackColor = true;
            this.NumpressPicBox.CheckedChanged += new System.EventHandler(this.NumpressPicBox_CheckedChanged);
            // 
            // MakeTPPCompatibleOutputButton
            // 
            this.MakeTPPCompatibleOutputButton.AutoSize = true;
            this.MakeTPPCompatibleOutputButton.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.MakeTPPCompatibleOutputButton.Checked = true;
            this.MakeTPPCompatibleOutputButton.CheckState = System.Windows.Forms.CheckState.Checked;
            this.MakeTPPCompatibleOutputButton.Location = new System.Drawing.Point(3, 113);
            this.MakeTPPCompatibleOutputButton.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.MakeTPPCompatibleOutputButton.Name = "MakeTPPCompatibleOutputButton";
            this.MakeTPPCompatibleOutputButton.Size = new System.Drawing.Size(189, 26);
            this.MakeTPPCompatibleOutputButton.TabIndex = 7;
            this.MakeTPPCompatibleOutputButton.Text = "TPP compatibility:";
            this.MakeTPPCompatibleOutputButton.UseVisualStyleBackColor = true;
            // 
            // UseZlibBox
            // 
            this.UseZlibBox.AutoSize = true;
            this.UseZlibBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.UseZlibBox.Checked = true;
            this.UseZlibBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.UseZlibBox.Location = new System.Drawing.Point(167, 85);
            this.UseZlibBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.UseZlibBox.Name = "UseZlibBox";
            this.UseZlibBox.Size = new System.Drawing.Size(224, 26);
            this.UseZlibBox.TabIndex = 6;
            this.UseZlibBox.Text = "Use zlib compression:";
            this.UseZlibBox.UseVisualStyleBackColor = true;
            // 
            // GzipBox
            // 
            this.GzipBox.AutoSize = true;
            this.GzipBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.GzipBox.Location = new System.Drawing.Point(197, 113);
            this.GzipBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.GzipBox.Name = "GzipBox";
            this.GzipBox.Size = new System.Drawing.Size(179, 26);
            this.GzipBox.TabIndex = 8;
            this.GzipBox.Text = "Package in gzip:";
            this.GzipBox.UseVisualStyleBackColor = true;
            // 
            // OutputExtensionBox
            // 
            this.OutputExtensionBox.Location = new System.Drawing.Point(283, 20);
            this.OutputExtensionBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.OutputExtensionBox.Name = "OutputExtensionBox";
            this.OutputExtensionBox.Size = new System.Drawing.Size(56, 22);
            this.OutputExtensionBox.TabIndex = 2;
            // 
            // OutputExtensionLabel
            // 
            this.OutputExtensionLabel.AutoSize = true;
            this.OutputExtensionLabel.Location = new System.Drawing.Point(207, 23);
            this.OutputExtensionLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.OutputExtensionLabel.Name = "OutputExtensionLabel";
            this.OutputExtensionLabel.Size = new System.Drawing.Size(73, 17);
            this.OutputExtensionLabel.TabIndex = 23;
            this.OutputExtensionLabel.Text = "Extension:";
            // 
            // WriteIndexBox
            // 
            this.WriteIndexBox.AutoSize = true;
            this.WriteIndexBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.WriteIndexBox.Checked = true;
            this.WriteIndexBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.WriteIndexBox.Location = new System.Drawing.Point(40, 85);
            this.WriteIndexBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.WriteIndexBox.Name = "WriteIndexBox";
            this.WriteIndexBox.Size = new System.Drawing.Size(139, 26);
            this.WriteIndexBox.TabIndex = 5;
            this.WriteIndexBox.Text = "Write index:";
            this.WriteIndexBox.UseVisualStyleBackColor = true;
            // 
            // Precision32
            // 
            this.Precision32.AutoSize = true;
            this.Precision32.Location = new System.Drawing.Point(272, 53);
            this.Precision32.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Precision32.Name = "Precision32";
            this.Precision32.Size = new System.Drawing.Size(87, 26);
            this.Precision32.TabIndex = 4;
            this.Precision32.Text = "32-bit";
            this.Precision32.UseVisualStyleBackColor = true;
            // 
            // Precision64
            // 
            this.Precision64.AutoSize = true;
            this.Precision64.Checked = true;
            this.Precision64.Location = new System.Drawing.Point(196, 53);
            this.Precision64.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Precision64.Name = "Precision64";
            this.Precision64.Size = new System.Drawing.Size(87, 26);
            this.Precision64.TabIndex = 3;
            this.Precision64.TabStop = true;
            this.Precision64.Text = "64-bit";
            this.Precision64.UseVisualStyleBackColor = true;
            // 
            // SlidingPanel
            // 
            this.SlidingPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.SlidingPanel.Controls.Add(this.OutputBrowse);
            this.SlidingPanel.Controls.Add(this.OutputLabel);
            this.SlidingPanel.Controls.Add(this.OutputBox);
            this.SlidingPanel.Controls.Add(this.OptionsGB);
            this.SlidingPanel.Location = new System.Drawing.Point(20, 347);
            this.SlidingPanel.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.SlidingPanel.Name = "SlidingPanel";
            this.SlidingPanel.Size = new System.Drawing.Size(367, 282);
            this.SlidingPanel.TabIndex = 8;
            // 
            // SetDefaultsButton
            // 
            this.SetDefaultsButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.SetDefaultsButton.Location = new System.Drawing.Point(20, 640);
            this.SetDefaultsButton.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.SetDefaultsButton.Name = "SetDefaultsButton";
            this.SetDefaultsButton.Size = new System.Drawing.Size(407, 28);
            this.SetDefaultsButton.TabIndex = 32;
            this.SetDefaultsButton.Text = "Use these settings next time I start MSConvertGUI";
            this.SetDefaultsButton.UseVisualStyleBackColor = true;
            this.SetDefaultsButton.Click += new System.EventHandler(this.SetDefaultsButton_Click);
            // 
            // AboutButton
            // 
            this.AboutButton.Location = new System.Drawing.Point(660, 41);
            this.AboutButton.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.AboutButton.Name = "AboutButton";
            this.AboutButton.Size = new System.Drawing.Size(175, 28);
            this.AboutButton.TabIndex = 33;
            this.AboutButton.Text = "About MSConvertGUI";
            this.AboutButton.UseVisualStyleBackColor = true;
            this.AboutButton.Click += new System.EventHandler(this.AboutButtonClick);
            // 
            // dataGridViewTextBoxColumn1
            // 
            this.dataGridViewTextBoxColumn1.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.dataGridViewTextBoxColumn1.FillWeight = 50F;
            this.dataGridViewTextBoxColumn1.HeaderText = "Filter";
            this.dataGridViewTextBoxColumn1.Name = "dataGridViewTextBoxColumn1";
            // 
            // dataGridViewTextBoxColumn2
            // 
            this.dataGridViewTextBoxColumn2.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.dataGridViewTextBoxColumn2.HeaderText = "Parameters";
            this.dataGridViewTextBoxColumn2.Name = "dataGridViewTextBoxColumn2";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(881, 678);
            this.Controls.Add(this.AboutButton);
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
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Name = "MainForm";
            this.Text = "MSConvertGUI";
            this.Load += new System.EventHandler(this.MainForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.FilterDGV)).EndInit();
            this.FilterGB.ResumeLayout(false);
            this.ChargeStatePredictorPanel.ResumeLayout(false);
            this.ChargeStatePredictorPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.ChaSingleBox)).EndInit();
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
            this.ThresholdFilterPanel.ResumeLayout(false);
            this.ThresholdFilterPanel.PerformLayout();
            this.OptionsGB.ResumeLayout(false);
            this.OptionsGB.PerformLayout();
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
        private System.Windows.Forms.CheckBox MakeTPPCompatibleOutputButton;
        private System.Windows.Forms.CheckBox UseZlibBox;
        private System.Windows.Forms.CheckBox GzipBox;
        private System.Windows.Forms.Panel SlidingPanel;
        private System.Windows.Forms.Panel MSLevelPanel;
        private System.Windows.Forms.ComboBox FilterBox;
        private System.Windows.Forms.Panel ChargeStatePredictorPanel;
        private System.Windows.Forms.Panel PeakPickingPanel;
        private System.Windows.Forms.Panel ZeroSamplesPanel;
        private System.Windows.Forms.Panel ETDFilterPanel;
        private System.Windows.Forms.Panel ActivationPanel;
        private System.Windows.Forms.Label MSLabelSeperator;
        private System.Windows.Forms.TextBox MSLevelHigh;
        private System.Windows.Forms.TextBox MSLevelLow;
        private System.Windows.Forms.CheckBox PeakPreferVendorBox;
        private System.Windows.Forms.Label PeakMSLevelLabel;
        private System.Windows.Forms.Label PeakMSLevelSeperator;
        private System.Windows.Forms.TextBox PeakMSLevelHigh;
        private System.Windows.Forms.TextBox PeakMSLevelLow;
        private System.Windows.Forms.RadioButton ZeroSamplesRemove;
        private System.Windows.Forms.RadioButton ZeroSamplesAddMissing;
        private System.Windows.Forms.TextBox ZeroSamplesAddMissingFlankCountBox;
        private System.Windows.Forms.Label ZeroSamplesMSLevelLabel;
        private System.Windows.Forms.Label ZeroSamplesMSLevelSeperator;
        private System.Windows.Forms.TextBox ZeroSamplesMSLevelHigh;
        private System.Windows.Forms.TextBox ZeroSamplesMSLevelLow;
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
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn1;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn2;
        private System.Windows.Forms.Panel ThresholdFilterPanel;
        private System.Windows.Forms.Label thresholdValueLabel;
        private System.Windows.Forms.Label thresholdOrientationLabel;
        private System.Windows.Forms.Label thresholdTypeLabel;
        private System.Windows.Forms.ComboBox thresholdOrientationComboBox;
        private System.Windows.Forms.TextBox thresholdValueTextBox;
        private System.Windows.Forms.ComboBox thresholdTypeComboBox;
        private string lastFileboxText;
        private System.Windows.Forms.Button AboutButton;
        private System.Windows.Forms.CheckBox NumpressLinearBox;
        private System.Windows.Forms.CheckBox NumpressSlofBox;
        private System.Windows.Forms.CheckBox NumpressPicBox;
    }
}

