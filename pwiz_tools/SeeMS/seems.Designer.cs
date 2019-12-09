//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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

namespace seems
{
	partial class seemsForm
	{

		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose( bool disposing )
		{
			if( disposing && ( components != null ) )
			{
				components.Dispose();
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(seemsForm));
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripProgressBar1 = new System.Windows.Forms.ToolStripProgressBar();
            this.toolStripPanel1 = new System.Windows.Forms.ToolStripPanel();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openFileMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.recentFilesFileMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.exitFileMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timeToMzHeatmapsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.previewAsMzMLToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.eventLogToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.decimalPlacesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.decimalPlaces0 = new System.Windows.Forms.ToolStripMenuItem();
            this.decimalPlaces1 = new System.Windows.Forms.ToolStripMenuItem();
            this.decimalPlaces2 = new System.Windows.Forms.ToolStripMenuItem();
            this.decimalPlaces3 = new System.Windows.Forms.ToolStripMenuItem();
            this.decimalPlaces4 = new System.Windows.Forms.ToolStripMenuItem();
            this.decimalPlaces5 = new System.Windows.Forms.ToolStripMenuItem();
            this.combineIonMobilitySpectraToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ignoreZeroIntensityPointsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.acceptZeroLengthSpectraToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.windowToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.cascadeWindowMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tileVerticalWindowMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tileHorizontalWindowMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.arrangeIconsWindowMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.closeAllWindowMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.helpToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.openFileToolStripButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            this.dataProcessingButton = new System.Windows.Forms.ToolStripButton();
            this.annotationButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripPanel2 = new System.Windows.Forms.ToolStripPanel();
            this.dockPanel = new DigitalRune.Windows.Docking.DockPanel();
            this.timeInMinutesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStrip1.SuspendLayout();
            this.toolStripPanel1.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.toolStripPanel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // statusStrip1
            // 
            this.statusStrip1.Dock = System.Windows.Forms.DockStyle.None;
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabel1,
            this.toolStripProgressBar1});
            this.statusStrip1.Location = new System.Drawing.Point(0, 0);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(792, 22);
            this.statusStrip1.TabIndex = 1;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // toolStripStatusLabel1
            // 
            this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            this.toolStripStatusLabel1.Size = new System.Drawing.Size(777, 17);
            this.toolStripStatusLabel1.Spring = true;
            this.toolStripStatusLabel1.Text = "No source loaded.";
            this.toolStripStatusLabel1.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // toolStripProgressBar1
            // 
            this.toolStripProgressBar1.Name = "toolStripProgressBar1";
            this.toolStripProgressBar1.Size = new System.Drawing.Size(100, 16);
            this.toolStripProgressBar1.Visible = false;
            // 
            // toolStripPanel1
            // 
            this.toolStripPanel1.Controls.Add(this.menuStrip1);
            this.toolStripPanel1.Controls.Add(this.toolStrip1);
            this.toolStripPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.toolStripPanel1.Location = new System.Drawing.Point(0, 0);
            this.toolStripPanel1.Name = "toolStripPanel1";
            this.toolStripPanel1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.toolStripPanel1.RowMargin = new System.Windows.Forms.Padding(3, 0, 0, 0);
            this.toolStripPanel1.Size = new System.Drawing.Size(792, 49);
            this.toolStripPanel1.Layout += new System.Windows.Forms.LayoutEventHandler(this.toolStripPanel1_Layout);
            // 
            // menuStrip1
            // 
            this.menuStrip1.Dock = System.Windows.Forms.DockStyle.None;
            this.menuStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Visible;
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.editToolStripMenuItem,
            this.viewToolStripMenuItem,
            this.windowToolStripMenuItem,
            this.helpToolStripMenuItem1});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.MdiWindowListItem = this.windowToolStripMenuItem;
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(792, 24);
            this.menuStrip1.TabIndex = 6;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openFileMenuItem,
            this.toolStripSeparator2,
            this.recentFilesFileMenuItem,
            this.toolStripSeparator3,
            this.exitFileMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // openFileMenuItem
            // 
            this.openFileMenuItem.Name = "openFileMenuItem";
            this.openFileMenuItem.Size = new System.Drawing.Size(136, 22);
            this.openFileMenuItem.Text = "&Open";
            this.openFileMenuItem.Click += new System.EventHandler(this.openFile_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(133, 6);
            // 
            // recentFilesFileMenuItem
            // 
            this.recentFilesFileMenuItem.Name = "recentFilesFileMenuItem";
            this.recentFilesFileMenuItem.Size = new System.Drawing.Size(136, 22);
            this.recentFilesFileMenuItem.Text = "Recent Files";
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(133, 6);
            // 
            // exitFileMenuItem
            // 
            this.exitFileMenuItem.Name = "exitFileMenuItem";
            this.exitFileMenuItem.Size = new System.Drawing.Size(136, 22);
            this.exitFileMenuItem.Text = "E&xit";
            this.exitFileMenuItem.Click += new System.EventHandler(this.exitFileMenuItem_Click);
            // 
            // editToolStripMenuItem
            // 
            this.editToolStripMenuItem.Name = "editToolStripMenuItem";
            this.editToolStripMenuItem.Size = new System.Drawing.Size(39, 20);
            this.editToolStripMenuItem.Text = "Edit";
            this.editToolStripMenuItem.Visible = false;
            // 
            // viewToolStripMenuItem
            // 
            this.viewToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.timeToMzHeatmapsToolStripMenuItem,
            this.previewAsMzMLToolStripMenuItem,
            this.eventLogToolStripMenuItem,
            this.decimalPlacesToolStripMenuItem,
            this.combineIonMobilitySpectraToolStripMenuItem,
            this.ignoreZeroIntensityPointsToolStripMenuItem,
            this.acceptZeroLengthSpectraToolStripMenuItem,
            this.timeInMinutesToolStripMenuItem});
            this.viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            this.viewToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.viewToolStripMenuItem.Text = "View";
            // 
            // timeToMzHeatmapsToolStripMenuItem
            // 
            this.timeToMzHeatmapsToolStripMenuItem.Name = "timeToMzHeatmapsToolStripMenuItem";
            this.timeToMzHeatmapsToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.timeToMzHeatmapsToolStripMenuItem.Text = "Time to m/z Heatmaps";
            this.timeToMzHeatmapsToolStripMenuItem.Click += new System.EventHandler(this.timeToMzHeatmapsToolStripMenuItem_Click);
            // 
            // previewAsMzMLToolStripMenuItem
            // 
            this.previewAsMzMLToolStripMenuItem.Name = "previewAsMzMLToolStripMenuItem";
            this.previewAsMzMLToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.previewAsMzMLToolStripMenuItem.Text = "Preview as mzML";
            this.previewAsMzMLToolStripMenuItem.Visible = false;
            this.previewAsMzMLToolStripMenuItem.Click += new System.EventHandler(this.previewAsMzMLToolStripMenuItem_Click);
            // 
            // eventLogToolStripMenuItem
            // 
            this.eventLogToolStripMenuItem.Name = "eventLogToolStripMenuItem";
            this.eventLogToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.eventLogToolStripMenuItem.Text = "Event Log";
            this.eventLogToolStripMenuItem.Click += new System.EventHandler(this.eventLogToolStripMenuItem_Click);
            // 
            // decimalPlacesToolStripMenuItem
            // 
            this.decimalPlacesToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.decimalPlaces0,
            this.decimalPlaces1,
            this.decimalPlaces2,
            this.decimalPlaces3,
            this.decimalPlaces4,
            this.decimalPlaces5});
            this.decimalPlacesToolStripMenuItem.Name = "decimalPlacesToolStripMenuItem";
            this.decimalPlacesToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.decimalPlacesToolStripMenuItem.Text = "Decimal places";
            // 
            // decimalPlaces0
            // 
            this.decimalPlaces0.CheckOnClick = true;
            this.decimalPlaces0.Name = "decimalPlaces0";
            this.decimalPlaces0.Size = new System.Drawing.Size(80, 22);
            this.decimalPlaces0.Text = "0";
            this.decimalPlaces0.Click += new System.EventHandler(this.decimalPlaces_Click);
            // 
            // decimalPlaces1
            // 
            this.decimalPlaces1.CheckOnClick = true;
            this.decimalPlaces1.Name = "decimalPlaces1";
            this.decimalPlaces1.Size = new System.Drawing.Size(80, 22);
            this.decimalPlaces1.Text = "1";
            this.decimalPlaces1.Click += new System.EventHandler(this.decimalPlaces_Click);
            // 
            // decimalPlaces2
            // 
            this.decimalPlaces2.CheckOnClick = true;
            this.decimalPlaces2.Name = "decimalPlaces2";
            this.decimalPlaces2.Size = new System.Drawing.Size(80, 22);
            this.decimalPlaces2.Text = "2";
            this.decimalPlaces2.Click += new System.EventHandler(this.decimalPlaces_Click);
            // 
            // decimalPlaces3
            // 
            this.decimalPlaces3.CheckOnClick = true;
            this.decimalPlaces3.Name = "decimalPlaces3";
            this.decimalPlaces3.Size = new System.Drawing.Size(80, 22);
            this.decimalPlaces3.Text = "3";
            this.decimalPlaces3.Click += new System.EventHandler(this.decimalPlaces_Click);
            // 
            // decimalPlaces4
            // 
            this.decimalPlaces4.CheckOnClick = true;
            this.decimalPlaces4.Name = "decimalPlaces4";
            this.decimalPlaces4.Size = new System.Drawing.Size(80, 22);
            this.decimalPlaces4.Text = "4";
            this.decimalPlaces4.Click += new System.EventHandler(this.decimalPlaces_Click);
            // 
            // decimalPlaces5
            // 
            this.decimalPlaces5.CheckOnClick = true;
            this.decimalPlaces5.Name = "decimalPlaces5";
            this.decimalPlaces5.Size = new System.Drawing.Size(80, 22);
            this.decimalPlaces5.Text = "5";
            this.decimalPlaces5.Click += new System.EventHandler(this.decimalPlaces_Click);
            // 
            // combineIonMobilitySpectraToolStripMenuItem
            // 
            this.combineIonMobilitySpectraToolStripMenuItem.CheckOnClick = true;
            this.combineIonMobilitySpectraToolStripMenuItem.Name = "combineIonMobilitySpectraToolStripMenuItem";
            this.combineIonMobilitySpectraToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.combineIonMobilitySpectraToolStripMenuItem.Text = "Combine ion mobility spectra";
            this.combineIonMobilitySpectraToolStripMenuItem.Click += new System.EventHandler(this.combineIonMobilitySpectraToolStripMenuItem_Click);
            // 
            // ignoreZeroIntensityPointsToolStripMenuItem
            // 
            this.ignoreZeroIntensityPointsToolStripMenuItem.CheckOnClick = true;
            this.ignoreZeroIntensityPointsToolStripMenuItem.Name = "ignoreZeroIntensityPointsToolStripMenuItem";
            this.ignoreZeroIntensityPointsToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.ignoreZeroIntensityPointsToolStripMenuItem.Text = "Ignore zero intensity points";
            this.ignoreZeroIntensityPointsToolStripMenuItem.Click += new System.EventHandler(this.ignoreZeroIntensityPointsToolStripMenuItem_Click);
            // 
            // acceptZeroLengthSpectraToolStripMenuItem
            // 
            this.acceptZeroLengthSpectraToolStripMenuItem.CheckOnClick = true;
            this.acceptZeroLengthSpectraToolStripMenuItem.Name = "acceptZeroLengthSpectraToolStripMenuItem";
            this.acceptZeroLengthSpectraToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.acceptZeroLengthSpectraToolStripMenuItem.Text = "Accept zero length spectra";
            this.acceptZeroLengthSpectraToolStripMenuItem.Click += new System.EventHandler(this.acceptZeroLengthSpectraToolStripMenuItem_Click);
            // 
            // windowToolStripMenuItem
            // 
            this.windowToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.cascadeWindowMenuItem,
            this.tileVerticalWindowMenuItem,
            this.tileHorizontalWindowMenuItem,
            this.arrangeIconsWindowMenuItem,
            this.closeAllWindowMenuItem,
            this.toolStripSeparator1});
            this.windowToolStripMenuItem.Name = "windowToolStripMenuItem";
            this.windowToolStripMenuItem.Size = new System.Drawing.Size(63, 20);
            this.windowToolStripMenuItem.Text = "Window";
            this.windowToolStripMenuItem.DropDownOpening += new System.EventHandler(this.windowToolStripMenuItem_DropDownOpening);
            // 
            // cascadeWindowMenuItem
            // 
            this.cascadeWindowMenuItem.Name = "cascadeWindowMenuItem";
            this.cascadeWindowMenuItem.Size = new System.Drawing.Size(151, 22);
            this.cascadeWindowMenuItem.Text = "&Cascade";
            this.cascadeWindowMenuItem.Click += new System.EventHandler(this.cascadeWindowMenuItem_Click);
            // 
            // tileVerticalWindowMenuItem
            // 
            this.tileVerticalWindowMenuItem.Name = "tileVerticalWindowMenuItem";
            this.tileVerticalWindowMenuItem.Size = new System.Drawing.Size(151, 22);
            this.tileVerticalWindowMenuItem.Text = "Tile &Vertical";
            this.tileVerticalWindowMenuItem.Click += new System.EventHandler(this.tileVerticalWindowMenuItem_Click);
            // 
            // tileHorizontalWindowMenuItem
            // 
            this.tileHorizontalWindowMenuItem.Name = "tileHorizontalWindowMenuItem";
            this.tileHorizontalWindowMenuItem.Size = new System.Drawing.Size(151, 22);
            this.tileHorizontalWindowMenuItem.Text = "Tile &Horizontal";
            this.tileHorizontalWindowMenuItem.Click += new System.EventHandler(this.tileHorizontalWindowMenuItem_Click);
            // 
            // arrangeIconsWindowMenuItem
            // 
            this.arrangeIconsWindowMenuItem.Name = "arrangeIconsWindowMenuItem";
            this.arrangeIconsWindowMenuItem.Size = new System.Drawing.Size(151, 22);
            this.arrangeIconsWindowMenuItem.Text = "&Arrange Icons";
            this.arrangeIconsWindowMenuItem.Click += new System.EventHandler(this.arrangeIconsWindowMenuItem_Click);
            // 
            // closeAllWindowMenuItem
            // 
            this.closeAllWindowMenuItem.Name = "closeAllWindowMenuItem";
            this.closeAllWindowMenuItem.Size = new System.Drawing.Size(151, 22);
            this.closeAllWindowMenuItem.Text = "Close All";
            this.closeAllWindowMenuItem.Click += new System.EventHandler(this.closeAllWindowMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(148, 6);
            // 
            // helpToolStripMenuItem1
            // 
            this.helpToolStripMenuItem1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.aboutToolStripMenuItem});
            this.helpToolStripMenuItem1.Name = "helpToolStripMenuItem1";
            this.helpToolStripMenuItem1.Size = new System.Drawing.Size(44, 20);
            this.helpToolStripMenuItem1.Text = "Help";
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(107, 22);
            this.aboutToolStripMenuItem.Text = "&About";
            this.aboutToolStripMenuItem.Click += new System.EventHandler(this.aboutHelpMenuItem_Click);
            // 
            // toolStrip1
            // 
            this.toolStrip1.Dock = System.Windows.Forms.DockStyle.None;
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openFileToolStripButton,
            this.toolStripSeparator4,
            this.dataProcessingButton,
            this.annotationButton});
            this.toolStrip1.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.HorizontalStackWithOverflow;
            this.toolStrip1.Location = new System.Drawing.Point(0, 24);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.toolStrip1.Size = new System.Drawing.Size(792, 25);
            this.toolStrip1.Stretch = true;
            this.toolStrip1.TabIndex = 2;
            // 
            // openFileToolStripButton
            // 
            this.openFileToolStripButton.AutoSize = false;
            this.openFileToolStripButton.BackColor = System.Drawing.SystemColors.Control;
            this.openFileToolStripButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.openFileToolStripButton.Image = ((System.Drawing.Image)(resources.GetObject("openFileToolStripButton.Image")));
            this.openFileToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.openFileToolStripButton.Name = "openFileToolStripButton";
            this.openFileToolStripButton.Overflow = System.Windows.Forms.ToolStripItemOverflow.Never;
            this.openFileToolStripButton.Size = new System.Drawing.Size(23, 22);
            this.openFileToolStripButton.Text = "&Open";
            this.openFileToolStripButton.ToolTipText = "Open specified source file";
            this.openFileToolStripButton.Click += new System.EventHandler(this.openFile_Click);
            // 
            // toolStripSeparator4
            // 
            this.toolStripSeparator4.Name = "toolStripSeparator4";
            this.toolStripSeparator4.Size = new System.Drawing.Size(6, 25);
            // 
            // dataProcessingButton
            // 
            this.dataProcessingButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.dataProcessingButton.Enabled = false;
            this.dataProcessingButton.Image = global::seems.Properties.Resources.DataProcessing;
            this.dataProcessingButton.ImageTransparentColor = System.Drawing.Color.White;
            this.dataProcessingButton.Name = "dataProcessingButton";
            this.dataProcessingButton.Size = new System.Drawing.Size(23, 22);
            this.dataProcessingButton.Text = "Manage Data Processing";
            this.dataProcessingButton.Click += new System.EventHandler(this.dataProcessingButton_Click);
            // 
            // annotationButton
            // 
            this.annotationButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.annotationButton.Enabled = false;
            this.annotationButton.Image = global::seems.Properties.Resources.Annotation;
            this.annotationButton.ImageTransparentColor = System.Drawing.Color.White;
            this.annotationButton.Name = "annotationButton";
            this.annotationButton.Size = new System.Drawing.Size(23, 22);
            this.annotationButton.Text = "Manage Annotations";
            this.annotationButton.Click += new System.EventHandler(this.annotationButton_Click);
            // 
            // toolStripPanel2
            // 
            this.toolStripPanel2.Controls.Add(this.statusStrip1);
            this.toolStripPanel2.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.toolStripPanel2.Location = new System.Drawing.Point(0, 544);
            this.toolStripPanel2.Name = "toolStripPanel2";
            this.toolStripPanel2.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.toolStripPanel2.RowMargin = new System.Windows.Forms.Padding(3, 0, 0, 0);
            this.toolStripPanel2.Size = new System.Drawing.Size(792, 22);
            this.toolStripPanel2.Layout += new System.Windows.Forms.LayoutEventHandler(this.toolStripPanel2_Layout);
            // 
            // dockPanel
            // 
            this.dockPanel.ActiveAutoHideContent = null;
            this.dockPanel.BackColor = System.Drawing.SystemColors.AppWorkspace;
            this.dockPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dockPanel.Location = new System.Drawing.Point(0, 49);
            this.dockPanel.Name = "dockPanel";
            this.dockPanel.Size = new System.Drawing.Size(792, 495);
            this.dockPanel.TabIndex = 7;
            // 
            // timeInMinutesToolStripMenuItem
            // 
            this.timeInMinutesToolStripMenuItem.CheckOnClick = true;
            this.timeInMinutesToolStripMenuItem.Name = "timeInMinutesToolStripMenuItem";
            this.timeInMinutesToolStripMenuItem.Size = new System.Drawing.Size(231, 22);
            this.timeInMinutesToolStripMenuItem.Text = "Time in minutes";
            this.timeInMinutesToolStripMenuItem.Click += new System.EventHandler(this.timeInMinutesToolStripMenuItem_Click);
            // 
            // seemsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(792, 566);
            this.Controls.Add(this.dockPanel);
            this.Controls.Add(this.toolStripPanel2);
            this.Controls.Add(this.toolStripPanel1);
            this.DoubleBuffered = true;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.IsMdiContainer = true;
            this.MainMenuStrip = this.menuStrip1;
            this.MinimumSize = new System.Drawing.Size(400, 150);
            this.Name = "seemsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "SeeMS";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.seems_FormClosing);
            this.ResizeBegin += new System.EventHandler(this.seems_ResizeBegin);
            this.ResizeEnd += new System.EventHandler(this.seems_ResizeEnd);
            this.Resize += new System.EventHandler(this.seems_Resize);
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.toolStripPanel1.ResumeLayout(false);
            this.toolStripPanel1.PerformLayout();
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.toolStripPanel2.ResumeLayout(false);
            this.toolStripPanel2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.StatusStrip statusStrip1;
		private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
		private System.Windows.Forms.ToolStripProgressBar toolStripProgressBar1;
		private System.Windows.Forms.ToolStripPanel toolStripPanel1;
		private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton openFileToolStripButton;
		private System.Windows.Forms.ToolStripPanel toolStripPanel2;
		private System.Windows.Forms.MenuStrip menuStrip1;
		private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem viewToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem windowToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem1;
		private System.Windows.Forms.ToolStripMenuItem tileHorizontalWindowMenuItem;
		private System.Windows.Forms.ToolStripMenuItem cascadeWindowMenuItem;
		private System.Windows.Forms.ToolStripMenuItem tileVerticalWindowMenuItem;
		private System.Windows.Forms.ToolStripMenuItem closeAllWindowMenuItem;
		private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
		private System.Windows.Forms.ToolStripMenuItem arrangeIconsWindowMenuItem;
		private System.Windows.Forms.ToolStripMenuItem openFileMenuItem;
		private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
		private System.Windows.Forms.ToolStripMenuItem recentFilesFileMenuItem;
		private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
		private System.Windows.Forms.ToolStripMenuItem exitFileMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
        private System.Windows.Forms.ToolStripButton dataProcessingButton;
        private System.Windows.Forms.ToolStripMenuItem previewAsMzMLToolStripMenuItem;
        private System.Windows.Forms.ToolStripButton annotationButton;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
        private System.Windows.Forms.ToolStripMenuItem eventLogToolStripMenuItem;
        private DigitalRune.Windows.Docking.DockPanel dockPanel;
        private System.Windows.Forms.ToolStripMenuItem timeToMzHeatmapsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem decimalPlacesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem decimalPlaces0;
        private System.Windows.Forms.ToolStripMenuItem decimalPlaces1;
        private System.Windows.Forms.ToolStripMenuItem decimalPlaces2;
        private System.Windows.Forms.ToolStripMenuItem decimalPlaces3;
        private System.Windows.Forms.ToolStripMenuItem decimalPlaces4;
        private System.Windows.Forms.ToolStripMenuItem decimalPlaces5;
        private System.Windows.Forms.ToolStripMenuItem combineIonMobilitySpectraToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem ignoreZeroIntensityPointsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem acceptZeroLengthSpectraToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timeInMinutesToolStripMenuItem;
    }
}