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
            this.FileListBox = new System.Windows.Forms.ListBox();
            this.RemoveFileButton = new System.Windows.Forms.Button();
            this.StartButton = new System.Windows.Forms.Button();
            this.FilterGB = new System.Windows.Forms.GroupBox();
            this.DemultiplexPanel = new System.Windows.Forms.Panel();
            this.DemuxMassErrorTypeBox = new System.Windows.Forms.ComboBox();
            this.DemuxMassErrorValue = new System.Windows.Forms.TextBox();
            this.DemuxMassErrorLabel = new System.Windows.Forms.Label();
            this.DemuxTypeBox = new System.Windows.Forms.ComboBox();
            this.LockmassRefinerPanel = new System.Windows.Forms.Panel();
            this.LockmassTolerance = new System.Windows.Forms.TextBox();
            this.lockmassToleranceLabel = new System.Windows.Forms.Label();
            this.lockmassMzLabel = new System.Windows.Forms.Label();
            this.LockmassMz = new System.Windows.Forms.TextBox();
            this.FilterBox = new System.Windows.Forms.ComboBox();
            this.MSLevelPanel = new System.Windows.Forms.Panel();
            this.MSLevelLabel = new System.Windows.Forms.Label();
            this.MSLabelSeperator = new System.Windows.Forms.Label();
            this.MSLevelHigh = new System.Windows.Forms.TextBox();
            this.MSLevelLow = new System.Windows.Forms.TextBox();
            this.PeakPickingPanel = new System.Windows.Forms.Panel();
            this.PeakMinSpacingLabel = new System.Windows.Forms.Label();
            this.PeakMinSpacing = new System.Windows.Forms.TextBox();
            this.PeakMinSnrLabel = new System.Windows.Forms.Label();
            this.PeakMinSnr = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.PeakPickingAlgorithmComboBox = new System.Windows.Forms.ComboBox();
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
            this.label2 = new System.Windows.Forms.Label();
            this.ScanEventHigh = new System.Windows.Forms.TextBox();
            this.ScanEventLow = new System.Windows.Forms.TextBox();
            this.ScanEventLabel = new System.Windows.Forms.Label();
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
            this.SrmSpectraBox = new System.Windows.Forms.CheckBox();
            this.SimSpectraBox = new System.Windows.Forms.CheckBox();
            this.CombineIonMobilitySpectraBox = new System.Windows.Forms.CheckBox();
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
            this.PresetSaveButton = new CustomDataSourceDialog.SplitButton();
            this.presetSaveButtonMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.presetSaveAsButton = new System.Windows.Forms.ToolStripMenuItem();
            this.presetSetDefaultButton = new System.Windows.Forms.ToolStripMenuItem();
            this.AboutButton = new System.Windows.Forms.Button();
            this.dataGridViewTextBoxColumn1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.networkResourceComboBox = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.presetComboBox = new System.Windows.Forms.ComboBox();
            this.ScanSummingPanel = new System.Windows.Forms.Panel();
            this.ScanSummingScanTimeToleranceTextBox = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.ScanSummingPrecursorToleranceTextBox = new System.Windows.Forms.TextBox();
            this.ScanSummingIonMobilityToleranceTextBox = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.OptionTab = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ValueTab = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.FilterDGV)).BeginInit();
            this.FilterGB.SuspendLayout();
            this.DemultiplexPanel.SuspendLayout();
            this.LockmassRefinerPanel.SuspendLayout();
            this.MSLevelPanel.SuspendLayout();
            this.PeakPickingPanel.SuspendLayout();
            this.ZeroSamplesPanel.SuspendLayout();
            this.ETDFilterPanel.SuspendLayout();
            this.ThresholdFilterPanel.SuspendLayout();
            this.ChargeStatePredictorPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.ChaSingleBox)).BeginInit();
            this.ActivationPanel.SuspendLayout();
            this.SubsetPanel.SuspendLayout();
            this.OptionsGB.SuspendLayout();
            this.SlidingPanel.SuspendLayout();
            this.presetSaveButtonMenu.SuspendLayout();
            this.ScanSummingPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // FileBox
            // 
            this.FileBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.FileBox.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystem;
            this.FileBox.Location = new System.Drawing.Point(43, 35);
            this.FileBox.Name = "FileBox";
            this.FileBox.Size = new System.Drawing.Size(182, 20);
            this.FileBox.TabIndex = 3;
            this.FileBox.TextChanged += new System.EventHandler(this.FileBox_TextChanged);
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
            this.FilterDGV.Location = new System.Drawing.Point(322, 261);
            this.FilterDGV.MultiSelect = false;
            this.FilterDGV.Name = "FilterDGV";
            this.FilterDGV.RowHeadersVisible = false;
            this.FilterDGV.RowTemplate.Height = 24;
            this.FilterDGV.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.FilterDGV.Size = new System.Drawing.Size(528, 267);
            this.FilterDGV.TabIndex = 12;
            // 
            // FileListBox
            // 
            this.FileListBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.FileListBox.FormattingEnabled = true;
            this.FileListBox.HorizontalScrollbar = true;
            this.FileListBox.Location = new System.Drawing.Point(17, 90);
            this.FileListBox.Name = "FileListBox";
            this.FileListBox.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.FileListBox.Size = new System.Drawing.Size(269, 147);
            this.FileListBox.TabIndex = 7;
            this.FileListBox.KeyUp += new System.Windows.Forms.KeyEventHandler(this.FileListBox_KeyUp);
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
            // 
            // StartButton
            // 
            this.StartButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.StartButton.Location = new System.Drawing.Point(775, 537);
            this.StartButton.Name = "StartButton";
            this.StartButton.Size = new System.Drawing.Size(75, 23);
            this.StartButton.TabIndex = 13;
            this.StartButton.Text = "Start";
            this.StartButton.UseVisualStyleBackColor = true;
            this.StartButton.Click += new System.EventHandler(this.StartButton_Click);
            // 
            // FilterGB
            // 
            this.FilterGB.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.FilterGB.Controls.Add(this.ScanSummingPanel);
            this.FilterGB.Controls.Add(this.LockmassRefinerPanel);
            this.FilterGB.Controls.Add(this.DemultiplexPanel);
            this.FilterGB.Controls.Add(this.FilterBox);
            this.FilterGB.Controls.Add(this.MSLevelPanel);
            this.FilterGB.Controls.Add(this.PeakPickingPanel);
            this.FilterGB.Controls.Add(this.ZeroSamplesPanel);
            this.FilterGB.Controls.Add(this.ETDFilterPanel);
            this.FilterGB.Controls.Add(this.ThresholdFilterPanel);
            this.FilterGB.Controls.Add(this.ChargeStatePredictorPanel);
            this.FilterGB.Controls.Add(this.ActivationPanel);
            this.FilterGB.Controls.Add(this.SubsetPanel);
            this.FilterGB.Location = new System.Drawing.Point(322, 83);
            this.FilterGB.Name = "FilterGB";
            this.FilterGB.Size = new System.Drawing.Size(528, 143);
            this.FilterGB.TabIndex = 9;
            this.FilterGB.TabStop = false;
            this.FilterGB.Text = "Filters";
            // 
            // DemultiplexPanel
            // 
            this.DemultiplexPanel.Controls.Add(this.DemuxMassErrorTypeBox);
            this.DemultiplexPanel.Controls.Add(this.DemuxMassErrorValue);
            this.DemultiplexPanel.Controls.Add(this.DemuxMassErrorLabel);
            this.DemultiplexPanel.Controls.Add(this.DemuxTypeBox);
            this.DemultiplexPanel.Location = new System.Drawing.Point(22, 46);
            this.DemultiplexPanel.Name = "DemultiplexPanel";
            this.DemultiplexPanel.Size = new System.Drawing.Size(283, 91);
            this.DemultiplexPanel.TabIndex = 18;
            this.DemultiplexPanel.Visible = false;
            // 
            // DemuxMassErrorTypeBox
            // 
            this.DemuxMassErrorTypeBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.DemuxMassErrorTypeBox.FormattingEnabled = true;
            this.DemuxMassErrorTypeBox.Items.AddRange(new object[] {
            "ppm",
            "Da"});
            this.DemuxMassErrorTypeBox.Location = new System.Drawing.Point(151, 50);
            this.DemuxMassErrorTypeBox.Name = "DemuxMassErrorTypeBox";
            this.DemuxMassErrorTypeBox.Size = new System.Drawing.Size(58, 21);
            this.DemuxMassErrorTypeBox.TabIndex = 18;
            // 
            // DemuxMassErrorValue
            // 
            this.DemuxMassErrorValue.Location = new System.Drawing.Point(87, 51);
            this.DemuxMassErrorValue.Name = "DemuxMassErrorValue";
            this.DemuxMassErrorValue.Size = new System.Drawing.Size(48, 20);
            this.DemuxMassErrorValue.TabIndex = 17;
            this.DemuxMassErrorValue.Text = "10.0";
            // 
            // DemuxMassErrorLabel
            // 
            this.DemuxMassErrorLabel.AutoSize = true;
            this.DemuxMassErrorLabel.Location = new System.Drawing.Point(17, 53);
            this.DemuxMassErrorLabel.Name = "DemuxMassErrorLabel";
            this.DemuxMassErrorLabel.Size = new System.Drawing.Size(60, 13);
            this.DemuxMassErrorLabel.TabIndex = 16;
            this.DemuxMassErrorLabel.Text = "Mass Error:";
            // 
            // DemuxTypeBox
            // 
            this.DemuxTypeBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.DemuxTypeBox.FormattingEnabled = true;
            this.DemuxTypeBox.Items.AddRange(new object[] {
            "Overlap Only",
            "MSX (Overlap and Non-Overlap)"});
            this.DemuxTypeBox.Location = new System.Drawing.Point(20, 16);
            this.DemuxTypeBox.Name = "DemuxTypeBox";
            this.DemuxTypeBox.Size = new System.Drawing.Size(189, 21);
            this.DemuxTypeBox.TabIndex = 15;
            // 
            // LockmassRefinerPanel
            // 
            this.LockmassRefinerPanel.Controls.Add(this.LockmassTolerance);
            this.LockmassRefinerPanel.Controls.Add(this.lockmassToleranceLabel);
            this.LockmassRefinerPanel.Controls.Add(this.lockmassMzLabel);
            this.LockmassRefinerPanel.Controls.Add(this.LockmassMz);
            this.LockmassRefinerPanel.Location = new System.Drawing.Point(22, 46);
            this.LockmassRefinerPanel.Name = "LockmassRefinerPanel";
            this.LockmassRefinerPanel.Size = new System.Drawing.Size(283, 91);
            this.LockmassRefinerPanel.TabIndex = 17;
            this.LockmassRefinerPanel.Visible = false;
            // 
            // LockmassTolerance
            // 
            this.LockmassTolerance.Location = new System.Drawing.Point(135, 36);
            this.LockmassTolerance.Name = "LockmassTolerance";
            this.LockmassTolerance.Size = new System.Drawing.Size(62, 20);
            this.LockmassTolerance.TabIndex = 1;
            this.LockmassTolerance.Text = "0.1";
            // 
            // lockmassToleranceLabel
            // 
            this.lockmassToleranceLabel.AutoSize = true;
            this.lockmassToleranceLabel.Location = new System.Drawing.Point(48, 39);
            this.lockmassToleranceLabel.Name = "lockmassToleranceLabel";
            this.lockmassToleranceLabel.Size = new System.Drawing.Size(79, 13);
            this.lockmassToleranceLabel.TabIndex = 3;
            this.lockmassToleranceLabel.Text = "m/z Tolerance:";
            // 
            // lockmassMzLabel
            // 
            this.lockmassMzLabel.AutoSize = true;
            this.lockmassMzLabel.Location = new System.Drawing.Point(47, 12);
            this.lockmassMzLabel.Name = "lockmassMzLabel";
            this.lockmassMzLabel.Size = new System.Drawing.Size(81, 13);
            this.lockmassMzLabel.TabIndex = 13;
            this.lockmassMzLabel.Text = "Reference m/z:";
            // 
            // LockmassMz
            // 
            this.LockmassMz.Location = new System.Drawing.Point(135, 10);
            this.LockmassMz.Name = "LockmassMz";
            this.LockmassMz.Size = new System.Drawing.Size(62, 20);
            this.LockmassMz.TabIndex = 0;
            // 
            // FilterBox
            // 
            this.FilterBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.FilterBox.FormattingEnabled = true;
            this.FilterBox.Items.AddRange(new object[] {
            "Activation",
            "Charge State Predictor",
            "Demultiplex",
            "ETD Peak Filter",
            "Lockmass Refiner",
            "MS Level",
            "Peak Picking",
            "Threshold Peak Filter",
            "Scan Summing",
            "Subset",
            "Zero Samples"});
            this.FilterBox.Location = new System.Drawing.Point(97, 19);
            this.FilterBox.Name = "FilterBox";
            this.FilterBox.Size = new System.Drawing.Size(132, 21);
            this.FilterBox.TabIndex = 0;
            this.FilterBox.SelectedIndexChanged += new System.EventHandler(this.FilterBox_SelectedIndexChanged);
            // 
            // MSLevelPanel
            // 
            this.MSLevelPanel.Controls.Add(this.MSLevelLabel);
            this.MSLevelPanel.Controls.Add(this.MSLabelSeperator);
            this.MSLevelPanel.Controls.Add(this.MSLevelHigh);
            this.MSLevelPanel.Controls.Add(this.MSLevelLow);
            this.MSLevelPanel.Location = new System.Drawing.Point(22, 46);
            this.MSLevelPanel.Name = "MSLevelPanel";
            this.MSLevelPanel.Size = new System.Drawing.Size(283, 91);
            this.MSLevelPanel.TabIndex = 1;
            this.MSLevelPanel.Visible = false;
            // 
            // MSLevelLabel
            // 
            this.MSLevelLabel.AutoSize = true;
            this.MSLevelLabel.Location = new System.Drawing.Point(121, 19);
            this.MSLevelLabel.Name = "MSLevelLabel";
            this.MSLevelLabel.Size = new System.Drawing.Size(41, 13);
            this.MSLevelLabel.TabIndex = 3;
            this.MSLevelLabel.Text = "Levels:";
            // 
            // MSLabelSeperator
            // 
            this.MSLabelSeperator.AutoSize = true;
            this.MSLabelSeperator.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MSLabelSeperator.Location = new System.Drawing.Point(134, 34);
            this.MSLabelSeperator.Name = "MSLabelSeperator";
            this.MSLabelSeperator.Size = new System.Drawing.Size(15, 20);
            this.MSLabelSeperator.TabIndex = 2;
            this.MSLabelSeperator.Text = "-";
            // 
            // MSLevelHigh
            // 
            this.MSLevelHigh.Location = new System.Drawing.Point(151, 36);
            this.MSLevelHigh.Name = "MSLevelHigh";
            this.MSLevelHigh.Size = new System.Drawing.Size(37, 20);
            this.MSLevelHigh.TabIndex = 1;
            this.MSLevelHigh.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumTextBox_KeyPress);
            // 
            // MSLevelLow
            // 
            this.MSLevelLow.Location = new System.Drawing.Point(95, 36);
            this.MSLevelLow.Name = "MSLevelLow";
            this.MSLevelLow.Size = new System.Drawing.Size(37, 20);
            this.MSLevelLow.TabIndex = 0;
            this.MSLevelLow.Text = "1";
            this.MSLevelLow.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumTextBox_KeyPress);
            // 
            // PeakPickingPanel
            // 
            this.PeakPickingPanel.Controls.Add(this.PeakMinSpacingLabel);
            this.PeakPickingPanel.Controls.Add(this.PeakMinSpacing);
            this.PeakPickingPanel.Controls.Add(this.PeakMinSnrLabel);
            this.PeakPickingPanel.Controls.Add(this.PeakMinSnr);
            this.PeakPickingPanel.Controls.Add(this.label1);
            this.PeakPickingPanel.Controls.Add(this.PeakPickingAlgorithmComboBox);
            this.PeakPickingPanel.Controls.Add(this.PeakMSLevelLabel);
            this.PeakPickingPanel.Controls.Add(this.PeakMSLevelSeperator);
            this.PeakPickingPanel.Controls.Add(this.PeakMSLevelHigh);
            this.PeakPickingPanel.Controls.Add(this.PeakMSLevelLow);
            this.PeakPickingPanel.Location = new System.Drawing.Point(6, 46);
            this.PeakPickingPanel.Name = "PeakPickingPanel";
            this.PeakPickingPanel.Size = new System.Drawing.Size(331, 91);
            this.PeakPickingPanel.TabIndex = 2;
            this.PeakPickingPanel.Visible = false;
            // 
            // PeakMinSpacingLabel
            // 
            this.PeakMinSpacingLabel.AutoSize = true;
            this.PeakMinSpacingLabel.Location = new System.Drawing.Point(228, 51);
            this.PeakMinSpacingLabel.Name = "PeakMinSpacingLabel";
            this.PeakMinSpacingLabel.Size = new System.Drawing.Size(94, 13);
            this.PeakMinSpacingLabel.TabIndex = 26;
            this.PeakMinSpacingLabel.Text = "Min peak spacing:";
            // 
            // PeakMinSpacing
            // 
            this.PeakMinSpacing.Location = new System.Drawing.Point(256, 67);
            this.PeakMinSpacing.Name = "PeakMinSpacing";
            this.PeakMinSpacing.Size = new System.Drawing.Size(37, 20);
            this.PeakMinSpacing.TabIndex = 25;
            this.PeakMinSpacing.Text = "0.1";
            // 
            // PeakMinSnrLabel
            // 
            this.PeakMinSnrLabel.AutoSize = true;
            this.PeakMinSnrLabel.Location = new System.Drawing.Point(145, 51);
            this.PeakMinSnrLabel.Name = "PeakMinSnrLabel";
            this.PeakMinSnrLabel.Size = new System.Drawing.Size(53, 13);
            this.PeakMinSnrLabel.TabIndex = 24;
            this.PeakMinSnrLabel.Text = "Min SNR:";
            // 
            // PeakMinSnr
            // 
            this.PeakMinSnr.Location = new System.Drawing.Point(152, 67);
            this.PeakMinSnr.Name = "PeakMinSnr";
            this.PeakMinSnr.Size = new System.Drawing.Size(37, 20);
            this.PeakMinSnr.TabIndex = 23;
            this.PeakMinSnr.Text = "0.1";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(136, 6);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(53, 13);
            this.label1.TabIndex = 22;
            this.label1.Text = "Algorithm:";
            // 
            // PeakPickingAlgorithmComboBox
            // 
            this.PeakPickingAlgorithmComboBox.DisplayMember = "Text";
            this.PeakPickingAlgorithmComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.PeakPickingAlgorithmComboBox.FormattingEnabled = true;
            this.PeakPickingAlgorithmComboBox.Location = new System.Drawing.Point(3, 23);
            this.PeakPickingAlgorithmComboBox.Name = "PeakPickingAlgorithmComboBox";
            this.PeakPickingAlgorithmComboBox.Size = new System.Drawing.Size(325, 21);
            this.PeakPickingAlgorithmComboBox.TabIndex = 21;
            this.PeakPickingAlgorithmComboBox.ValueMember = "Tag";
            this.PeakPickingAlgorithmComboBox.SelectedIndexChanged += new System.EventHandler(this.PeakPickingAlgorithmComboBox_SelectedIndexChanged);
            // 
            // PeakMSLevelLabel
            // 
            this.PeakMSLevelLabel.AutoSize = true;
            this.PeakMSLevelLabel.Location = new System.Drawing.Point(33, 51);
            this.PeakMSLevelLabel.Name = "PeakMSLevelLabel";
            this.PeakMSLevelLabel.Size = new System.Drawing.Size(60, 13);
            this.PeakMSLevelLabel.TabIndex = 20;
            this.PeakMSLevelLabel.Text = "MS Levels:";
            // 
            // PeakMSLevelSeperator
            // 
            this.PeakMSLevelSeperator.AutoSize = true;
            this.PeakMSLevelSeperator.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.PeakMSLevelSeperator.Location = new System.Drawing.Point(54, 65);
            this.PeakMSLevelSeperator.Name = "PeakMSLevelSeperator";
            this.PeakMSLevelSeperator.Size = new System.Drawing.Size(15, 20);
            this.PeakMSLevelSeperator.TabIndex = 19;
            this.PeakMSLevelSeperator.Text = "-";
            // 
            // PeakMSLevelHigh
            // 
            this.PeakMSLevelHigh.Location = new System.Drawing.Point(71, 67);
            this.PeakMSLevelHigh.Name = "PeakMSLevelHigh";
            this.PeakMSLevelHigh.Size = new System.Drawing.Size(37, 20);
            this.PeakMSLevelHigh.TabIndex = 18;
            this.PeakMSLevelHigh.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumTextBox_KeyPress);
            // 
            // PeakMSLevelLow
            // 
            this.PeakMSLevelLow.Location = new System.Drawing.Point(15, 67);
            this.PeakMSLevelLow.Name = "PeakMSLevelLow";
            this.PeakMSLevelLow.Size = new System.Drawing.Size(37, 20);
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
            this.ZeroSamplesPanel.Location = new System.Drawing.Point(22, 46);
            this.ZeroSamplesPanel.Name = "ZeroSamplesPanel";
            this.ZeroSamplesPanel.Size = new System.Drawing.Size(283, 91);
            this.ZeroSamplesPanel.TabIndex = 24;
            this.ZeroSamplesPanel.Visible = false;
            // 
            // ZeroSamplesAddMissing
            // 
            this.ZeroSamplesAddMissing.AutoSize = true;
            this.ZeroSamplesAddMissing.Location = new System.Drawing.Point(93, 12);
            this.ZeroSamplesAddMissing.Name = "ZeroSamplesAddMissing";
            this.ZeroSamplesAddMissing.Size = new System.Drawing.Size(127, 17);
            this.ZeroSamplesAddMissing.TabIndex = 30;
            this.ZeroSamplesAddMissing.TabStop = true;
            this.ZeroSamplesAddMissing.Text = "Add missing, flank by:";
            this.ZeroSamplesAddMissing.UseVisualStyleBackColor = true;
            this.ZeroSamplesAddMissing.Click += new System.EventHandler(this.ZeroSamples_ModeChanged);
            // 
            // ZeroSamplesAddMissingFlankCountBox
            // 
            this.ZeroSamplesAddMissingFlankCountBox.Enabled = this.ZeroSamplesAddMissing.Checked;
            this.ZeroSamplesAddMissingFlankCountBox.Location = new System.Drawing.Point(221, 12);
            this.ZeroSamplesAddMissingFlankCountBox.Name = "ZeroSamplesAddMissingFlankCountBox";
            this.ZeroSamplesAddMissingFlankCountBox.Size = new System.Drawing.Size(37, 20);
            this.ZeroSamplesAddMissingFlankCountBox.TabIndex = 31;
            this.ZeroSamplesAddMissingFlankCountBox.Text = "5";
            this.ZeroSamplesAddMissingFlankCountBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumTextBox_KeyPress);
            // 
            // ZeroSamplesRemove
            // 
            this.ZeroSamplesRemove.AutoSize = true;
            this.ZeroSamplesRemove.Checked = true;
            this.ZeroSamplesRemove.Location = new System.Drawing.Point(20, 12);
            this.ZeroSamplesRemove.Name = "ZeroSamplesRemove";
            this.ZeroSamplesRemove.Size = new System.Drawing.Size(65, 17);
            this.ZeroSamplesRemove.TabIndex = 29;
            this.ZeroSamplesRemove.TabStop = true;
            this.ZeroSamplesRemove.Text = "Remove";
            this.ZeroSamplesRemove.UseVisualStyleBackColor = true;
            this.ZeroSamplesRemove.Click += new System.EventHandler(this.ZeroSamples_ModeChanged);
            // 
            // ZeroSamplesMSLevelLabel
            // 
            this.ZeroSamplesMSLevelLabel.AutoSize = true;
            this.ZeroSamplesMSLevelLabel.Location = new System.Drawing.Point(113, 41);
            this.ZeroSamplesMSLevelLabel.Name = "ZeroSamplesMSLevelLabel";
            this.ZeroSamplesMSLevelLabel.Size = new System.Drawing.Size(60, 13);
            this.ZeroSamplesMSLevelLabel.TabIndex = 25;
            this.ZeroSamplesMSLevelLabel.Text = "MS Levels:";
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
            // ZeroSamplesMSLevelHigh
            // 
            this.ZeroSamplesMSLevelHigh.Location = new System.Drawing.Point(151, 57);
            this.ZeroSamplesMSLevelHigh.Name = "ZeroSamplesMSLevelHigh";
            this.ZeroSamplesMSLevelHigh.Size = new System.Drawing.Size(37, 20);
            this.ZeroSamplesMSLevelHigh.TabIndex = 28;
            this.ZeroSamplesMSLevelHigh.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumTextBox_KeyPress);
            // 
            // ZeroSamplesMSLevelLow
            // 
            this.ZeroSamplesMSLevelLow.Location = new System.Drawing.Point(95, 57);
            this.ZeroSamplesMSLevelLow.Name = "ZeroSamplesMSLevelLow";
            this.ZeroSamplesMSLevelLow.Size = new System.Drawing.Size(37, 20);
            this.ZeroSamplesMSLevelLow.TabIndex = 27;
            this.ZeroSamplesMSLevelLow.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumTextBox_KeyPress);
            // 
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
            // 
            // ThresholdFilterPanel
            // 
            this.ThresholdFilterPanel.Controls.Add(this.thresholdValueLabel);
            this.ThresholdFilterPanel.Controls.Add(this.thresholdOrientationLabel);
            this.ThresholdFilterPanel.Controls.Add(this.thresholdTypeLabel);
            this.ThresholdFilterPanel.Controls.Add(this.thresholdOrientationComboBox);
            this.ThresholdFilterPanel.Controls.Add(this.thresholdValueTextBox);
            this.ThresholdFilterPanel.Controls.Add(this.thresholdTypeComboBox);
            this.ThresholdFilterPanel.Location = new System.Drawing.Point(22, 46);
            this.ThresholdFilterPanel.Name = "ThresholdFilterPanel";
            this.ThresholdFilterPanel.Size = new System.Drawing.Size(283, 91);
            this.ThresholdFilterPanel.TabIndex = 20;
            this.ThresholdFilterPanel.Visible = false;
            // 
            // thresholdValueLabel
            // 
            this.thresholdValueLabel.AutoSize = true;
            this.thresholdValueLabel.Location = new System.Drawing.Point(69, 64);
            this.thresholdValueLabel.Name = "thresholdValueLabel";
            this.thresholdValueLabel.Size = new System.Drawing.Size(37, 13);
            this.thresholdValueLabel.TabIndex = 16;
            this.thresholdValueLabel.Text = "Value:";
            // 
            // thresholdOrientationLabel
            // 
            this.thresholdOrientationLabel.AutoSize = true;
            this.thresholdOrientationLabel.Location = new System.Drawing.Point(45, 37);
            this.thresholdOrientationLabel.Name = "thresholdOrientationLabel";
            this.thresholdOrientationLabel.Size = new System.Drawing.Size(61, 13);
            this.thresholdOrientationLabel.TabIndex = 15;
            this.thresholdOrientationLabel.Text = "Orientation:";
            // 
            // thresholdTypeLabel
            // 
            this.thresholdTypeLabel.AutoSize = true;
            this.thresholdTypeLabel.Location = new System.Drawing.Point(26, 10);
            this.thresholdTypeLabel.Name = "thresholdTypeLabel";
            this.thresholdTypeLabel.Size = new System.Drawing.Size(80, 13);
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
            this.thresholdOrientationComboBox.Location = new System.Drawing.Point(113, 34);
            this.thresholdOrientationComboBox.Name = "thresholdOrientationComboBox";
            this.thresholdOrientationComboBox.Size = new System.Drawing.Size(121, 21);
            this.thresholdOrientationComboBox.TabIndex = 2;
            // 
            // thresholdValueTextBox
            // 
            this.thresholdValueTextBox.Location = new System.Drawing.Point(114, 61);
            this.thresholdValueTextBox.Name = "thresholdValueTextBox";
            this.thresholdValueTextBox.Size = new System.Drawing.Size(120, 20);
            this.thresholdValueTextBox.TabIndex = 1;
            // 
            // thresholdTypeComboBox
            // 
            this.thresholdTypeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.thresholdTypeComboBox.FormattingEnabled = true;
            this.thresholdTypeComboBox.Location = new System.Drawing.Point(113, 7);
            this.thresholdTypeComboBox.Name = "thresholdTypeComboBox";
            this.thresholdTypeComboBox.Size = new System.Drawing.Size(121, 21);
            this.thresholdTypeComboBox.TabIndex = 0;
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
            // 
            // ChaMCMaxLabel
            // 
            this.ChaMCMaxLabel.AutoSize = true;
            this.ChaMCMaxLabel.Location = new System.Drawing.Point(182, 66);
            this.ChaMCMaxLabel.Name = "ChaMCMaxLabel";
            this.ChaMCMaxLabel.Size = new System.Drawing.Size(30, 13);
            this.ChaMCMaxLabel.TabIndex = 19;
            this.ChaMCMaxLabel.Text = "Max:";
            // 
            // ChaMCMaxBox
            // 
            this.ChaMCMaxBox.Location = new System.Drawing.Point(215, 62);
            this.ChaMCMaxBox.Name = "ChaMCMaxBox";
            this.ChaMCMaxBox.Size = new System.Drawing.Size(37, 20);
            this.ChaMCMaxBox.TabIndex = 18;
            this.ChaMCMaxBox.Text = "3";
            this.ChaMCMaxBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumTextBox_KeyPress);
            // 
            // ChaMCMinBox
            // 
            this.ChaMCMinBox.Location = new System.Drawing.Point(133, 62);
            this.ChaMCMinBox.Name = "ChaMCMinBox";
            this.ChaMCMinBox.Size = new System.Drawing.Size(37, 20);
            this.ChaMCMinBox.TabIndex = 17;
            this.ChaMCMinBox.Text = "2";
            this.ChaMCMinBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumTextBox_KeyPress);
            // 
            // ChaMCMinLabel
            // 
            this.ChaMCMinLabel.AutoSize = true;
            this.ChaMCMinLabel.Location = new System.Drawing.Point(30, 66);
            this.ChaMCMinLabel.Name = "ChaMCMinLabel";
            this.ChaMCMinLabel.Size = new System.Drawing.Size(103, 13);
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
            // 
            // ChaSingleLabel
            // 
            this.ChaSingleLabel.AutoSize = true;
            this.ChaSingleLabel.Location = new System.Drawing.Point(67, 38);
            this.ChaSingleLabel.Name = "ChaSingleLabel";
            this.ChaSingleLabel.Size = new System.Drawing.Size(107, 13);
            this.ChaSingleLabel.TabIndex = 7;
            this.ChaSingleLabel.Text = "Single Charge % TIC:";
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
            // 
            // ActivationTypeLabel
            // 
            this.ActivationTypeLabel.AutoSize = true;
            this.ActivationTypeLabel.Location = new System.Drawing.Point(81, 37);
            this.ActivationTypeLabel.Name = "ActivationTypeLabel";
            this.ActivationTypeLabel.Size = new System.Drawing.Size(34, 13);
            this.ActivationTypeLabel.TabIndex = 15;
            this.ActivationTypeLabel.Text = "Type:";
            // 
            // SubsetPanel
            // 
            this.SubsetPanel.Controls.Add(this.label2);
            this.SubsetPanel.Controls.Add(this.ScanEventHigh);
            this.SubsetPanel.Controls.Add(this.ScanEventLow);
            this.SubsetPanel.Controls.Add(this.ScanEventLabel);
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
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(174, 60);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(15, 20);
            this.label2.TabIndex = 20;
            this.label2.Text = "-";
            // 
            // ScanEventHigh
            // 
            this.ScanEventHigh.Location = new System.Drawing.Point(191, 62);
            this.ScanEventHigh.Name = "ScanEventHigh";
            this.ScanEventHigh.Size = new System.Drawing.Size(37, 20);
            this.ScanEventHigh.TabIndex = 19;
            // 
            // ScanEventLow
            // 
            this.ScanEventLow.Location = new System.Drawing.Point(135, 62);
            this.ScanEventLow.Name = "ScanEventLow";
            this.ScanEventLow.Size = new System.Drawing.Size(37, 20);
            this.ScanEventLow.TabIndex = 18;
            // 
            // ScanEventLabel
            // 
            this.ScanEventLabel.AutoSize = true;
            this.ScanEventLabel.Location = new System.Drawing.Point(64, 67);
            this.ScanEventLabel.Name = "ScanEventLabel";
            this.ScanEventLabel.Size = new System.Drawing.Size(66, 13);
            this.ScanEventLabel.TabIndex = 17;
            this.ScanEventLabel.Text = "Scan Event:";
            // 
            // mzWinLabel2
            // 
            this.mzWinLabel2.AutoSize = true;
            this.mzWinLabel2.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
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
            // 
            // ScanTimeLow
            // 
            this.ScanTimeLow.Location = new System.Drawing.Point(135, 36);
            this.ScanTimeLow.Name = "ScanTimeLow";
            this.ScanTimeLow.Size = new System.Drawing.Size(37, 20);
            this.ScanTimeLow.TabIndex = 0;
            // 
            // mzWinHigh
            // 
            this.mzWinHigh.Location = new System.Drawing.Point(191, 62);
            this.mzWinHigh.Name = "mzWinHigh";
            this.mzWinHigh.Size = new System.Drawing.Size(37, 20);
            this.mzWinHigh.TabIndex = 15;
            // 
            // ScanTimeHigh
            // 
            this.ScanTimeHigh.Location = new System.Drawing.Point(191, 36);
            this.ScanTimeHigh.Name = "ScanTimeHigh";
            this.ScanTimeHigh.Size = new System.Drawing.Size(37, 20);
            this.ScanTimeHigh.TabIndex = 1;
            // 
            // ScanTimeLabel2
            // 
            this.ScanTimeLabel2.AutoSize = true;
            this.ScanTimeLabel2.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
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
            // 
            // ScanTimeLabel
            // 
            this.ScanTimeLabel.AutoSize = true;
            this.ScanTimeLabel.Location = new System.Drawing.Point(68, 41);
            this.ScanTimeLabel.Name = "ScanTimeLabel";
            this.ScanTimeLabel.Size = new System.Drawing.Size(61, 13);
            this.ScanTimeLabel.TabIndex = 3;
            this.ScanTimeLabel.Text = "Scan Time:";
            // 
            // mzWinLabel
            // 
            this.mzWinLabel.AutoSize = true;
            this.mzWinLabel.Location = new System.Drawing.Point(64, 67);
            this.mzWinLabel.Name = "mzWinLabel";
            this.mzWinLabel.Size = new System.Drawing.Size(65, 13);
            this.mzWinLabel.TabIndex = 6;
            this.mzWinLabel.Text = "mz Window:";
            // 
            // ScanNumberLabel
            // 
            this.ScanNumberLabel.AutoSize = true;
            this.ScanNumberLabel.Location = new System.Drawing.Point(54, 12);
            this.ScanNumberLabel.Name = "ScanNumberLabel";
            this.ScanNumberLabel.Size = new System.Drawing.Size(75, 13);
            this.ScanNumberLabel.TabIndex = 13;
            this.ScanNumberLabel.Text = "Scan Number:";
            // 
            // ScanNumberLow
            // 
            this.ScanNumberLow.Location = new System.Drawing.Point(135, 10);
            this.ScanNumberLow.Name = "ScanNumberLow";
            this.ScanNumberLow.Size = new System.Drawing.Size(37, 20);
            this.ScanNumberLow.TabIndex = 10;
            // 
            // ScanNumberLabel2
            // 
            this.ScanNumberLabel2.AutoSize = true;
            this.ScanNumberLabel2.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ScanNumberLabel2.Location = new System.Drawing.Point(174, 8);
            this.ScanNumberLabel2.Name = "ScanNumberLabel2";
            this.ScanNumberLabel2.Size = new System.Drawing.Size(15, 20);
            this.ScanNumberLabel2.TabIndex = 12;
            this.ScanNumberLabel2.Text = "-";
            // 
            // RemoveFilterButton
            // 
            this.RemoveFilterButton.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.RemoveFilterButton.Location = new System.Drawing.Point(578, 232);
            this.RemoveFilterButton.Name = "RemoveFilterButton";
            this.RemoveFilterButton.Size = new System.Drawing.Size(58, 23);
            this.RemoveFilterButton.TabIndex = 11;
            this.RemoveFilterButton.Text = "Remove";
            this.RemoveFilterButton.UseVisualStyleBackColor = true;
            this.RemoveFilterButton.Click += new System.EventHandler(this.RemoveFilterButton_Click);
            // 
            // AddFilterButton
            // 
            this.AddFilterButton.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.AddFilterButton.Location = new System.Drawing.Point(530, 232);
            this.AddFilterButton.Name = "AddFilterButton";
            this.AddFilterButton.Size = new System.Drawing.Size(42, 23);
            this.AddFilterButton.TabIndex = 10;
            this.AddFilterButton.Text = "Add";
            this.AddFilterButton.UseVisualStyleBackColor = true;
            this.AddFilterButton.Click += new System.EventHandler(this.AddFilterButton_Click);
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
            this.OutputFormatBox.Location = new System.Drawing.Point(89, 16);
            this.OutputFormatBox.Name = "OutputFormatBox";
            this.OutputFormatBox.Size = new System.Drawing.Size(60, 21);
            this.OutputFormatBox.TabIndex = 1;
            this.OutputFormatBox.SelectedIndexChanged += new System.EventHandler(this.OutputFormatBox_SelectedIndexChanged);
            // 
            // FormatLabel
            // 
            this.FormatLabel.AutoSize = true;
            this.FormatLabel.Location = new System.Drawing.Point(14, 19);
            this.FormatLabel.Name = "FormatLabel";
            this.FormatLabel.Size = new System.Drawing.Size(74, 13);
            this.FormatLabel.TabIndex = 13;
            this.FormatLabel.Text = "Output format:";
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
            // 
            // OutputBox
            // 
            this.OutputBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Append;
            this.OutputBox.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystemDirectories;
            this.OutputBox.Location = new System.Drawing.Point(5, 18);
            this.OutputBox.Name = "OutputBox";
            this.OutputBox.Size = new System.Drawing.Size(210, 20);
            this.OutputBox.TabIndex = 1;
            // 
            // PrecisionLabel
            // 
            this.PrecisionLabel.AutoSize = true;
            this.PrecisionLabel.Location = new System.Drawing.Point(13, 45);
            this.PrecisionLabel.Name = "PrecisionLabel";
            this.PrecisionLabel.Size = new System.Drawing.Size(131, 13);
            this.PrecisionLabel.TabIndex = 18;
            this.PrecisionLabel.Text = "Binary encoding precision:";
            // 
            // OptionsGB
            // 
            this.OptionsGB.Controls.Add(this.SrmSpectraBox);
            this.OptionsGB.Controls.Add(this.SimSpectraBox);
            this.OptionsGB.Controls.Add(this.CombineIonMobilitySpectraBox);
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
            this.OptionsGB.Location = new System.Drawing.Point(2, 44);
            this.OptionsGB.Name = "OptionsGB";
            this.OptionsGB.Size = new System.Drawing.Size(269, 226);
            this.OptionsGB.TabIndex = 3;
            this.OptionsGB.TabStop = false;
            this.OptionsGB.Text = "Options";
            // 
            // SrmSpectraBox
            // 
            this.SrmSpectraBox.AutoSize = true;
            this.SrmSpectraBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.SrmSpectraBox.Location = new System.Drawing.Point(148, 207);
            this.SrmSpectraBox.Name = "SrmSpectraBox";
            this.SrmSpectraBox.Size = new System.Drawing.Size(105, 17);
            this.SrmSpectraBox.TabIndex = 29;
            this.SrmSpectraBox.Text = "SRM as spectra:";
            this.SrmSpectraBox.UseVisualStyleBackColor = true;
            // 
            // SimSpectraBox
            // 
            this.SimSpectraBox.AutoSize = true;
            this.SimSpectraBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.SimSpectraBox.Location = new System.Drawing.Point(44, 207);
            this.SimSpectraBox.Name = "SimSpectraBox";
            this.SimSpectraBox.Size = new System.Drawing.Size(100, 17);
            this.SimSpectraBox.TabIndex = 28;
            this.SimSpectraBox.Text = "SIM as spectra:";
            this.SimSpectraBox.UseVisualStyleBackColor = true;
            // 
            // CombineIonMobilitySpectraBox
            // 
            this.CombineIonMobilitySpectraBox.AutoSize = true;
            this.CombineIonMobilitySpectraBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.CombineIonMobilitySpectraBox.Location = new System.Drawing.Point(98, 184);
            this.CombineIonMobilitySpectraBox.Name = "CombineIonMobilitySpectraBox";
            this.CombineIonMobilitySpectraBox.Size = new System.Drawing.Size(155, 17);
            this.CombineIonMobilitySpectraBox.TabIndex = 27;
            this.CombineIonMobilitySpectraBox.Text = "Combine ion mobility scans:";
            this.CombineIonMobilitySpectraBox.UseVisualStyleBackColor = true;
            // 
            // NumpressSlofBox
            // 
            this.NumpressSlofBox.AutoSize = true;
            this.NumpressSlofBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.NumpressSlofBox.Location = new System.Drawing.Point(11, 137);
            this.NumpressSlofBox.Name = "NumpressSlofBox";
            this.NumpressSlofBox.Size = new System.Drawing.Size(242, 17);
            this.NumpressSlofBox.TabIndex = 25;
            this.NumpressSlofBox.Text = "Use numpress short logged float compression:";
            this.NumpressSlofBox.UseVisualStyleBackColor = true;
            this.NumpressSlofBox.CheckedChanged += new System.EventHandler(this.NumpressSlofBox_CheckedChanged);
            // 
            // NumpressLinearBox
            // 
            this.NumpressLinearBox.AutoSize = true;
            this.NumpressLinearBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.NumpressLinearBox.Location = new System.Drawing.Point(67, 114);
            this.NumpressLinearBox.Name = "NumpressLinearBox";
            this.NumpressLinearBox.Size = new System.Drawing.Size(186, 17);
            this.NumpressLinearBox.TabIndex = 24;
            this.NumpressLinearBox.Text = "Use numpress linear compression:";
            this.NumpressLinearBox.UseVisualStyleBackColor = true;
            // 
            // NumpressPicBox
            // 
            this.NumpressPicBox.AutoSize = true;
            this.NumpressPicBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.NumpressPicBox.Location = new System.Drawing.Point(21, 160);
            this.NumpressPicBox.Name = "NumpressPicBox";
            this.NumpressPicBox.Size = new System.Drawing.Size(232, 17);
            this.NumpressPicBox.TabIndex = 26;
            this.NumpressPicBox.Text = "Use numpress positive integer compression:";
            this.NumpressPicBox.UseVisualStyleBackColor = true;
            this.NumpressPicBox.CheckedChanged += new System.EventHandler(this.NumpressPicBox_CheckedChanged);
            // 
            // MakeTPPCompatibleOutputButton
            // 
            this.MakeTPPCompatibleOutputButton.AutoSize = true;
            this.MakeTPPCompatibleOutputButton.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.MakeTPPCompatibleOutputButton.Checked = true;
            this.MakeTPPCompatibleOutputButton.CheckState = System.Windows.Forms.CheckState.Checked;
            this.MakeTPPCompatibleOutputButton.Location = new System.Drawing.Point(2, 92);
            this.MakeTPPCompatibleOutputButton.Name = "MakeTPPCompatibleOutputButton";
            this.MakeTPPCompatibleOutputButton.Size = new System.Drawing.Size(110, 17);
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
            this.UseZlibBox.Location = new System.Drawing.Point(125, 69);
            this.UseZlibBox.Name = "UseZlibBox";
            this.UseZlibBox.Size = new System.Drawing.Size(128, 17);
            this.UseZlibBox.TabIndex = 6;
            this.UseZlibBox.Text = "Use zlib compression:";
            this.UseZlibBox.UseVisualStyleBackColor = true;
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
            // 
            // OutputExtensionBox
            // 
            this.OutputExtensionBox.Location = new System.Drawing.Point(212, 16);
            this.OutputExtensionBox.Name = "OutputExtensionBox";
            this.OutputExtensionBox.Size = new System.Drawing.Size(43, 20);
            this.OutputExtensionBox.TabIndex = 2;
            // 
            // OutputExtensionLabel
            // 
            this.OutputExtensionLabel.AutoSize = true;
            this.OutputExtensionLabel.Location = new System.Drawing.Point(155, 19);
            this.OutputExtensionLabel.Name = "OutputExtensionLabel";
            this.OutputExtensionLabel.Size = new System.Drawing.Size(56, 13);
            this.OutputExtensionLabel.TabIndex = 23;
            this.OutputExtensionLabel.Text = "Extension:";
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
            // 
            // SlidingPanel
            // 
            this.SlidingPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.SlidingPanel.Controls.Add(this.OutputBrowse);
            this.SlidingPanel.Controls.Add(this.OutputLabel);
            this.SlidingPanel.Controls.Add(this.OutputBox);
            this.SlidingPanel.Controls.Add(this.OptionsGB);
            this.SlidingPanel.Location = new System.Drawing.Point(15, 255);
            this.SlidingPanel.Name = "SlidingPanel";
            this.SlidingPanel.Size = new System.Drawing.Size(275, 273);
            this.SlidingPanel.TabIndex = 8;
            // 
            // PresetSaveButton
            // 
            this.PresetSaveButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.PresetSaveButton.Location = new System.Drawing.Point(322, 537);
            this.PresetSaveButton.Menu = this.presetSaveButtonMenu;
            this.PresetSaveButton.Name = "PresetSaveButton";
            this.PresetSaveButton.Size = new System.Drawing.Size(90, 23);
            this.PresetSaveButton.TabIndex = 32;
            this.PresetSaveButton.Text = "Save Preset";
            this.PresetSaveButton.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.PresetSaveButton.UseVisualStyleBackColor = true;
            this.PresetSaveButton.Click += new System.EventHandler(this.presetSaveButton_Click);
            // 
            // presetSaveButtonMenu
            // 
            this.presetSaveButtonMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.presetSaveAsButton,
            this.presetSetDefaultButton});
            this.presetSaveButtonMenu.Name = "presetSaveButtonMenu";
            this.presetSaveButtonMenu.Size = new System.Drawing.Size(159, 48);
            // 
            // presetSaveAsButton
            // 
            this.presetSaveAsButton.Name = "presetSaveAsButton";
            this.presetSaveAsButton.Size = new System.Drawing.Size(158, 22);
            this.presetSaveAsButton.Text = "Save Preset As...";
            this.presetSaveAsButton.Click += new System.EventHandler(this.presetSaveAsButton_Click);
            // 
            // presetSetDefaultButton
            // 
            this.presetSetDefaultButton.Name = "presetSetDefaultButton";
            this.presetSetDefaultButton.Size = new System.Drawing.Size(145, 22);
            this.presetSetDefaultButton.Text = "Set as Default";
            this.presetSetDefaultButton.Click += new System.EventHandler(this.presetSetDefaultButton_Click);
            // 
            // AboutButton
            // 
            this.AboutButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.AboutButton.Location = new System.Drawing.Point(719, 28);
            this.AboutButton.Name = "AboutButton";
            this.AboutButton.Size = new System.Drawing.Size(131, 23);
            this.AboutButton.TabIndex = 33;
            this.AboutButton.Text = "About MSConvert";
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
            // networkResourceComboBox
            // 
            this.networkResourceComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.networkResourceComboBox.FormattingEnabled = true;
            this.networkResourceComboBox.Items.AddRange(new object[] {
            "UNIFI"});
            this.networkResourceComboBox.Location = new System.Drawing.Point(326, 35);
            this.networkResourceComboBox.Name = "networkResourceComboBox";
            this.networkResourceComboBox.Size = new System.Drawing.Size(167, 21);
            this.networkResourceComboBox.TabIndex = 34;
            this.networkResourceComboBox.DropDown += new System.EventHandler(this.networkResourceComboBox_DropDown);
            this.networkResourceComboBox.SelectedIndexChanged += new System.EventHandler(this.networkResourceComboBox_SelectedIndexChanged);
            this.networkResourceComboBox.Leave += new System.EventHandler(this.networkResourceComboBox_Leave);
            // 
            // label3
            // 
            this.label3.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(12, 542);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(45, 13);
            this.label3.TabIndex = 27;
            this.label3.Text = "Presets:";
            // 
            // presetComboBox
            // 
            this.presetComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.presetComboBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.presetComboBox.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.presetComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.presetComboBox.FormattingEnabled = true;
            this.presetComboBox.Location = new System.Drawing.Point(58, 537);
            this.presetComboBox.Name = "presetComboBox";
            this.presetComboBox.Size = new System.Drawing.Size(258, 21);
            this.presetComboBox.TabIndex = 35;
            this.presetComboBox.SelectedIndexChanged += new System.EventHandler(this.presetComboBox_SelectedIndexChanged);
            // 
            // ScanSummingPanel
            // 
            this.ScanSummingPanel.Controls.Add(this.label7);
            this.ScanSummingPanel.Controls.Add(this.label8);
            this.ScanSummingPanel.Controls.Add(this.label9);
            this.ScanSummingPanel.Controls.Add(this.ScanSummingIonMobilityToleranceTextBox);
            this.ScanSummingPanel.Controls.Add(this.label6);
            this.ScanSummingPanel.Controls.Add(this.ScanSummingScanTimeToleranceTextBox);
            this.ScanSummingPanel.Controls.Add(this.label4);
            this.ScanSummingPanel.Controls.Add(this.label5);
            this.ScanSummingPanel.Controls.Add(this.ScanSummingPrecursorToleranceTextBox);
            this.ScanSummingPanel.Location = new System.Drawing.Point(24, 47);
            this.ScanSummingPanel.Name = "ScanSummingPanel";
            this.ScanSummingPanel.Size = new System.Drawing.Size(283, 91);
            this.ScanSummingPanel.TabIndex = 18;
            this.ScanSummingPanel.Visible = false;
            // 
            // ScanSummingScanTimeToleranceTextBox
            // 
            this.ScanSummingScanTimeToleranceTextBox.Location = new System.Drawing.Point(136, 36);
            this.ScanSummingScanTimeToleranceTextBox.Name = "ScanSummingScanTimeToleranceTextBox";
            this.ScanSummingScanTimeToleranceTextBox.Size = new System.Drawing.Size(48, 20);
            this.ScanSummingScanTimeToleranceTextBox.TabIndex = 1;
            this.ScanSummingScanTimeToleranceTextBox.Text = "5";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(20, 39);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(113, 13);
            this.label4.TabIndex = 3;
            this.label4.Text = "Scan time tolerance: ±";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(22, 14);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(111, 13);
            this.label5.TabIndex = 13;
            this.label5.Text = "Precursor tolerance: ±";
            // 
            // ScanSummingPrecursorToleranceTextBox
            // 
            this.ScanSummingPrecursorToleranceTextBox.Location = new System.Drawing.Point(136, 10);
            this.ScanSummingPrecursorToleranceTextBox.Name = "ScanSummingPrecursorToleranceTextBox";
            this.ScanSummingPrecursorToleranceTextBox.Size = new System.Drawing.Size(48, 20);
            this.ScanSummingPrecursorToleranceTextBox.TabIndex = 0;
            this.ScanSummingPrecursorToleranceTextBox.Text = "0.05";
            // 
            // ScanSummingIonMobilityToleranceTextBox
            // 
            this.ScanSummingIonMobilityToleranceTextBox.Location = new System.Drawing.Point(136, 62);
            this.ScanSummingIonMobilityToleranceTextBox.Name = "ScanSummingIonMobilityToleranceTextBox";
            this.ScanSummingIonMobilityToleranceTextBox.Size = new System.Drawing.Size(48, 20);
            this.ScanSummingIonMobilityToleranceTextBox.TabIndex = 14;
            this.ScanSummingIonMobilityToleranceTextBox.Text = "5";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(15, 64);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(118, 13);
            this.label6.TabIndex = 15;
            this.label6.Text = "Ion mobility tolerance: ±";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(192, 65);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(77, 13);
            this.label7.TabIndex = 18;
            this.label7.Text = "ms or vs/cm^2";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(192, 40);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(47, 13);
            this.label8.TabIndex = 16;
            this.label8.Text = "seconds";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(193, 14);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(25, 13);
            this.label9.TabIndex = 17;
            this.label9.Text = "m/z";
            // 
            // OptionTab
            // 
            this.OptionTab.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.OptionTab.FillWeight = 33F;
            this.OptionTab.HeaderText = "Filter";
            this.OptionTab.Name = "OptionTab";
            // 
            // ValueTab
            // 
            this.ValueTab.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.ValueTab.HeaderText = "Parameters";
            this.ValueTab.Name = "ValueTab";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(862, 568);
            this.Controls.Add(this.presetComboBox);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.networkResourceComboBox);
            this.Controls.Add(this.AboutButton);
            this.Controls.Add(this.BrowseFileButton);
            this.Controls.Add(this.FileListRadio);
            this.Controls.Add(this.TextFileRadio);
            this.Controls.Add(this.RemoveFilterButton);
            this.Controls.Add(this.AddFilterButton);
            this.Controls.Add(this.FilterGB);
            this.Controls.Add(this.StartButton);
            this.Controls.Add(this.PresetSaveButton);
            this.Controls.Add(this.RemoveFileButton);
            this.Controls.Add(this.FileListBox);
            this.Controls.Add(this.FilterDGV);
            this.Controls.Add(this.AddFileButton);
            this.Controls.Add(this.FileLabel);
            this.Controls.Add(this.FileBox);
            this.Controls.Add(this.SlidingPanel);
            this.Name = "MainForm";
            this.Text = "MSConvert";
            this.Load += new System.EventHandler(this.MainForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.FilterDGV)).EndInit();
            this.FilterGB.ResumeLayout(false);
            this.DemultiplexPanel.ResumeLayout(false);
            this.DemultiplexPanel.PerformLayout();
            this.LockmassRefinerPanel.ResumeLayout(false);
            this.LockmassRefinerPanel.PerformLayout();
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
            this.ChargeStatePredictorPanel.ResumeLayout(false);
            this.ChargeStatePredictorPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.ChaSingleBox)).EndInit();
            this.ActivationPanel.ResumeLayout(false);
            this.ActivationPanel.PerformLayout();
            this.SubsetPanel.ResumeLayout(false);
            this.SubsetPanel.PerformLayout();
            this.OptionsGB.ResumeLayout(false);
            this.OptionsGB.PerformLayout();
            this.SlidingPanel.ResumeLayout(false);
            this.SlidingPanel.PerformLayout();
            this.presetSaveButtonMenu.ResumeLayout(false);
            this.ScanSummingPanel.ResumeLayout(false);
            this.ScanSummingPanel.PerformLayout();
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
        private CustomDataSourceDialog.SplitButton PresetSaveButton;
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
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox PeakPickingAlgorithmComboBox;
        private System.Windows.Forms.Label PeakMinSpacingLabel;
        private System.Windows.Forms.TextBox PeakMinSpacing;
        private System.Windows.Forms.Label PeakMinSnrLabel;
        private System.Windows.Forms.TextBox PeakMinSnr;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox ScanEventHigh;
        private System.Windows.Forms.TextBox ScanEventLow;
        private System.Windows.Forms.Label ScanEventLabel;
        private System.Windows.Forms.Panel LockmassRefinerPanel;
        private System.Windows.Forms.TextBox LockmassTolerance;
        private System.Windows.Forms.Label lockmassToleranceLabel;
        private System.Windows.Forms.Label lockmassMzLabel;
        private System.Windows.Forms.TextBox LockmassMz;
        private System.Windows.Forms.Panel DemultiplexPanel;
        private System.Windows.Forms.ComboBox DemuxMassErrorTypeBox;
        private System.Windows.Forms.TextBox DemuxMassErrorValue;
        private System.Windows.Forms.Label DemuxMassErrorLabel;
        private System.Windows.Forms.ComboBox DemuxTypeBox;
        private System.Windows.Forms.ComboBox networkResourceComboBox;
        private System.Windows.Forms.CheckBox SrmSpectraBox;
        private System.Windows.Forms.CheckBox SimSpectraBox;
        private System.Windows.Forms.CheckBox CombineIonMobilitySpectraBox;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox presetComboBox;
        private System.Windows.Forms.ContextMenuStrip presetSaveButtonMenu;
        private System.Windows.Forms.ToolStripMenuItem presetSaveAsButton;
        private System.Windows.Forms.ToolStripMenuItem presetSetDefaultButton;
        private System.Windows.Forms.Panel ScanSummingPanel;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox ScanSummingIonMobilityToleranceTextBox;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox ScanSummingScanTimeToleranceTextBox;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox ScanSummingPrecursorToleranceTextBox;
        private System.Windows.Forms.DataGridViewTextBoxColumn OptionTab;
        private System.Windows.Forms.DataGridViewTextBoxColumn ValueTab;
    }
}

