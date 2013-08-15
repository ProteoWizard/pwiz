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
    partial class OpenDataSourceDialog
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager( typeof( OpenDataSourceDialog ) );
            this.specialPlacesImageList = new System.Windows.Forms.ImageList( this.components );
            this.lookInImageList = new System.Windows.Forms.ImageList( this.components );
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.recentDocumentsButton = new System.Windows.Forms.Button();
            this.desktopButton = new System.Windows.Forms.Button();
            this.myDocumentsButton = new System.Windows.Forms.Button();
            this.myComputerButton = new System.Windows.Forms.Button();
            this.myNetworkPlacesButton = new System.Windows.Forms.Button();
            this.navToolStrip = new System.Windows.Forms.ToolStrip();
            this.backButton = new System.Windows.Forms.ToolStripButton();
            this.upOneLevelButton = new System.Windows.Forms.ToolStripButton();
            this.viewsDropDownButton = new System.Windows.Forms.ToolStripDropDownButton();
            this.smallIconsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.largeIconsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tilesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.listToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.detailsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.cancelButton = new System.Windows.Forms.Button();
            this.openButton = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.sourceTypeComboBox = new System.Windows.Forms.ComboBox();
            this.labelSourcePath = new System.Windows.Forms.Label();
            this.sourcePathTextBox = new System.Windows.Forms.TextBox();
            this.labelLookIn = new System.Windows.Forms.Label();
            this.lookInComboBox = new System.Windows.Forms.ComboBox();
            this.listView = new System.Windows.Forms.ListView();
            this.SourceName = new System.Windows.Forms.ColumnHeader();
            this.SourceType = new System.Windows.Forms.ColumnHeader();
            this.Spectra = new System.Windows.Forms.ColumnHeader();
            this.SourceSize = new System.Windows.Forms.ColumnHeader();
            this.DateModified = new System.Windows.Forms.ColumnHeader();
            this.IonSourceType = new System.Windows.Forms.ColumnHeader();
            this.AnalyzerType = new System.Windows.Forms.ColumnHeader();
            this.DetectorType = new System.Windows.Forms.ColumnHeader();
            this.ContentType = new System.Windows.Forms.ColumnHeader();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.flowLayoutPanel1.SuspendLayout();
            this.navToolStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // specialPlacesImageList
            // 
            this.specialPlacesImageList.ImageStream = ( (System.Windows.Forms.ImageListStreamer) ( resources.GetObject( "specialPlacesImageList.ImageStream" ) ) );
            this.specialPlacesImageList.TransparentColor = System.Drawing.Color.Transparent;
            this.specialPlacesImageList.Images.SetKeyName( 0, "RecentDocuments.png" );
            this.specialPlacesImageList.Images.SetKeyName( 1, "Desktop.png" );
            this.specialPlacesImageList.Images.SetKeyName( 2, "MyDocuments.png" );
            this.specialPlacesImageList.Images.SetKeyName( 3, "MyComputer.png" );
            this.specialPlacesImageList.Images.SetKeyName( 4, "MyNetworkPlaces.png" );
            // 
            // lookInImageList
            // 
            this.lookInImageList.ImageStream = ( (System.Windows.Forms.ImageListStreamer) ( resources.GetObject( "lookInImageList.ImageStream" ) ) );
            this.lookInImageList.TransparentColor = System.Drawing.Color.Transparent;
            this.lookInImageList.Images.SetKeyName( 0, "RecentDocuments.png" );
            this.lookInImageList.Images.SetKeyName( 1, "Desktop.png" );
            this.lookInImageList.Images.SetKeyName( 2, "MyDocuments.png" );
            this.lookInImageList.Images.SetKeyName( 3, "MyComputer.png" );
            this.lookInImageList.Images.SetKeyName( 4, "MyNetworkPlaces.png" );
            this.lookInImageList.Images.SetKeyName( 5, "LocalDrive.png" );
            this.lookInImageList.Images.SetKeyName( 6, "OpticalDrive.png" );
            this.lookInImageList.Images.SetKeyName( 7, "NetworkDrive.png" );
            this.lookInImageList.Images.SetKeyName( 8, "folder.png" );
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainer1.IsSplitterFixed = true;
            this.splitContainer1.Location = new System.Drawing.Point( 0, 0 );
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add( this.flowLayoutPanel1 );
            this.splitContainer1.Panel1.Controls.Add( this.navToolStrip );
            this.splitContainer1.Panel1.Controls.Add( this.cancelButton );
            this.splitContainer1.Panel1.Controls.Add( this.openButton );
            this.splitContainer1.Panel1.Controls.Add( this.label1 );
            this.splitContainer1.Panel1.Controls.Add( this.sourceTypeComboBox );
            this.splitContainer1.Panel1.Controls.Add( this.labelSourcePath );
            this.splitContainer1.Panel1.Controls.Add( this.sourcePathTextBox );
            this.splitContainer1.Panel1.Controls.Add( this.labelLookIn );
            this.splitContainer1.Panel1.Controls.Add( this.lookInComboBox );
            this.splitContainer1.Panel1.Controls.Add( this.listView );
            this.splitContainer1.Size = new System.Drawing.Size( 914, 642 );
            this.splitContainer1.SplitterDistance = 473;
            this.splitContainer1.TabIndex = 15;
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom )
                        | System.Windows.Forms.AnchorStyles.Left ) ) );
            this.flowLayoutPanel1.BackColor = System.Drawing.SystemColors.Window;
            this.flowLayoutPanel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.flowLayoutPanel1.Controls.Add( this.recentDocumentsButton );
            this.flowLayoutPanel1.Controls.Add( this.desktopButton );
            this.flowLayoutPanel1.Controls.Add( this.myDocumentsButton );
            this.flowLayoutPanel1.Controls.Add( this.myComputerButton );
            this.flowLayoutPanel1.Controls.Add( this.myNetworkPlacesButton );
            this.flowLayoutPanel1.Location = new System.Drawing.Point( 9, 44 );
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size( 100, 413 );
            this.flowLayoutPanel1.TabIndex = 25;
            // 
            // recentDocumentsButton
            // 
            this.recentDocumentsButton.FlatAppearance.BorderSize = 0;
            this.recentDocumentsButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.recentDocumentsButton.ImageIndex = 0;
            this.recentDocumentsButton.ImageList = this.specialPlacesImageList;
            this.recentDocumentsButton.Location = new System.Drawing.Point( 3, 7 );
            this.recentDocumentsButton.Margin = new System.Windows.Forms.Padding( 3, 7, 3, 3 );
            this.recentDocumentsButton.Name = "recentDocumentsButton";
            this.recentDocumentsButton.Padding = new System.Windows.Forms.Padding( 1 );
            this.recentDocumentsButton.Size = new System.Drawing.Size( 92, 73 );
            this.recentDocumentsButton.TabIndex = 0;
            this.recentDocumentsButton.Text = "My Recent Documents";
            this.recentDocumentsButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.recentDocumentsButton.UseVisualStyleBackColor = false;
            this.recentDocumentsButton.Click += new System.EventHandler( this.recentDocumentsButton_Click );
            // 
            // desktopButton
            // 
            this.desktopButton.FlatAppearance.BorderSize = 0;
            this.desktopButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.desktopButton.ImageIndex = 1;
            this.desktopButton.ImageList = this.specialPlacesImageList;
            this.desktopButton.Location = new System.Drawing.Point( 3, 90 );
            this.desktopButton.Margin = new System.Windows.Forms.Padding( 3, 7, 3, 3 );
            this.desktopButton.Name = "desktopButton";
            this.desktopButton.Padding = new System.Windows.Forms.Padding( 1 );
            this.desktopButton.Size = new System.Drawing.Size( 92, 73 );
            this.desktopButton.TabIndex = 1;
            this.desktopButton.Text = "Desktop";
            this.desktopButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.desktopButton.UseVisualStyleBackColor = false;
            this.desktopButton.Click += new System.EventHandler( this.desktopButton_Click );
            // 
            // myDocumentsButton
            // 
            this.myDocumentsButton.FlatAppearance.BorderSize = 0;
            this.myDocumentsButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.myDocumentsButton.ImageIndex = 2;
            this.myDocumentsButton.ImageList = this.specialPlacesImageList;
            this.myDocumentsButton.Location = new System.Drawing.Point( 3, 173 );
            this.myDocumentsButton.Margin = new System.Windows.Forms.Padding( 3, 7, 3, 3 );
            this.myDocumentsButton.Name = "myDocumentsButton";
            this.myDocumentsButton.Padding = new System.Windows.Forms.Padding( 1 );
            this.myDocumentsButton.Size = new System.Drawing.Size( 92, 73 );
            this.myDocumentsButton.TabIndex = 2;
            this.myDocumentsButton.Text = "My Documents";
            this.myDocumentsButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.myDocumentsButton.UseVisualStyleBackColor = false;
            this.myDocumentsButton.Click += new System.EventHandler( this.myDocumentsButton_Click );
            // 
            // myComputerButton
            // 
            this.myComputerButton.FlatAppearance.BorderSize = 0;
            this.myComputerButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.myComputerButton.ImageIndex = 3;
            this.myComputerButton.ImageList = this.specialPlacesImageList;
            this.myComputerButton.Location = new System.Drawing.Point( 3, 256 );
            this.myComputerButton.Margin = new System.Windows.Forms.Padding( 3, 7, 3, 3 );
            this.myComputerButton.Name = "myComputerButton";
            this.myComputerButton.Padding = new System.Windows.Forms.Padding( 1 );
            this.myComputerButton.Size = new System.Drawing.Size( 92, 73 );
            this.myComputerButton.TabIndex = 3;
            this.myComputerButton.Text = "My Computer";
            this.myComputerButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.myComputerButton.UseVisualStyleBackColor = false;
            this.myComputerButton.Click += new System.EventHandler( this.myComputerButton_Click );
            // 
            // myNetworkPlacesButton
            // 
            this.myNetworkPlacesButton.FlatAppearance.BorderSize = 0;
            this.myNetworkPlacesButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.myNetworkPlacesButton.ImageIndex = 4;
            this.myNetworkPlacesButton.ImageList = this.specialPlacesImageList;
            this.myNetworkPlacesButton.Location = new System.Drawing.Point( 3, 339 );
            this.myNetworkPlacesButton.Margin = new System.Windows.Forms.Padding( 3, 7, 3, 3 );
            this.myNetworkPlacesButton.Name = "myNetworkPlacesButton";
            this.myNetworkPlacesButton.Padding = new System.Windows.Forms.Padding( 1 );
            this.myNetworkPlacesButton.Size = new System.Drawing.Size( 92, 73 );
            this.myNetworkPlacesButton.TabIndex = 4;
            this.myNetworkPlacesButton.Text = "My Network Places";
            this.myNetworkPlacesButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.myNetworkPlacesButton.UseVisualStyleBackColor = false;
            this.myNetworkPlacesButton.Click += new System.EventHandler( this.myNetworkPlacesButton_Click );
            // 
            // navToolStrip
            // 
            this.navToolStrip.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.navToolStrip.BackColor = System.Drawing.SystemColors.Control;
            this.navToolStrip.Dock = System.Windows.Forms.DockStyle.None;
            this.navToolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.navToolStrip.Items.AddRange( new System.Windows.Forms.ToolStripItem[] {
            this.backButton,
            this.upOneLevelButton,
            this.viewsDropDownButton} );
            this.navToolStrip.Location = new System.Drawing.Point( 752, 8 );
            this.navToolStrip.Name = "navToolStrip";
            this.navToolStrip.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.navToolStrip.Size = new System.Drawing.Size( 78, 25 );
            this.navToolStrip.TabIndex = 24;
            this.navToolStrip.Text = "toolStrip1";
            // 
            // backButton
            // 
            this.backButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.backButton.Image = ( (System.Drawing.Image) ( resources.GetObject( "backButton.Image" ) ) );
            this.backButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.backButton.Name = "backButton";
            this.backButton.Size = new System.Drawing.Size( 23, 22 );
            this.backButton.Text = "Back";
            this.backButton.ToolTipText = "Go Back to Previous Directory";
            this.backButton.Click += new System.EventHandler( this.backButton_Click );
            // 
            // upOneLevelButton
            // 
            this.upOneLevelButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.upOneLevelButton.Image = ( (System.Drawing.Image) ( resources.GetObject( "upOneLevelButton.Image" ) ) );
            this.upOneLevelButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.upOneLevelButton.Name = "upOneLevelButton";
            this.upOneLevelButton.Size = new System.Drawing.Size( 23, 22 );
            this.upOneLevelButton.Text = "Up";
            this.upOneLevelButton.ToolTipText = "Go Up One Level";
            this.upOneLevelButton.Click += new System.EventHandler( this.upOneLevelButton_Click );
            // 
            // viewsDropDownButton
            // 
            this.viewsDropDownButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.viewsDropDownButton.DropDownItems.AddRange( new System.Windows.Forms.ToolStripItem[] {
            this.smallIconsToolStripMenuItem,
            this.largeIconsToolStripMenuItem,
            this.tilesToolStripMenuItem,
            this.listToolStripMenuItem,
            this.detailsToolStripMenuItem} );
            this.viewsDropDownButton.Image = ( (System.Drawing.Image) ( resources.GetObject( "viewsDropDownButton.Image" ) ) );
            this.viewsDropDownButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.viewsDropDownButton.Name = "viewsDropDownButton";
            this.viewsDropDownButton.Size = new System.Drawing.Size( 29, 22 );
            this.viewsDropDownButton.Text = "Views";
            this.viewsDropDownButton.ToolTipText = "Views";
            // 
            // smallIconsToolStripMenuItem
            // 
            this.smallIconsToolStripMenuItem.Name = "smallIconsToolStripMenuItem";
            this.smallIconsToolStripMenuItem.Size = new System.Drawing.Size( 152, 22 );
            this.smallIconsToolStripMenuItem.Text = "Small Icons";
            this.smallIconsToolStripMenuItem.Click += new System.EventHandler( this.smallIconsToolStripMenuItem_Click );
            // 
            // largeIconsToolStripMenuItem
            // 
            this.largeIconsToolStripMenuItem.Name = "largeIconsToolStripMenuItem";
            this.largeIconsToolStripMenuItem.Size = new System.Drawing.Size( 152, 22 );
            this.largeIconsToolStripMenuItem.Text = "Large Icons";
            this.largeIconsToolStripMenuItem.Click += new System.EventHandler( this.largeIconsToolStripMenuItem_Click );
            // 
            // tilesToolStripMenuItem
            // 
            this.tilesToolStripMenuItem.Name = "tilesToolStripMenuItem";
            this.tilesToolStripMenuItem.Size = new System.Drawing.Size( 152, 22 );
            this.tilesToolStripMenuItem.Text = "Tiles";
            this.tilesToolStripMenuItem.Click += new System.EventHandler( this.tilesToolStripMenuItem_Click );
            // 
            // listToolStripMenuItem
            // 
            this.listToolStripMenuItem.Checked = true;
            this.listToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.listToolStripMenuItem.Name = "listToolStripMenuItem";
            this.listToolStripMenuItem.Size = new System.Drawing.Size( 152, 22 );
            this.listToolStripMenuItem.Text = "List";
            this.listToolStripMenuItem.Click += new System.EventHandler( this.listToolStripMenuItem_Click );
            // 
            // detailsToolStripMenuItem
            // 
            this.detailsToolStripMenuItem.Name = "detailsToolStripMenuItem";
            this.detailsToolStripMenuItem.Size = new System.Drawing.Size( 152, 22 );
            this.detailsToolStripMenuItem.Text = "Details";
            this.detailsToolStripMenuItem.Click += new System.EventHandler( this.detailsToolStripMenuItem_Click );
            // 
            // cancelButton
            // 
            this.cancelButton.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.cancelButton.Location = new System.Drawing.Point( 836, 440 );
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size( 66, 21 );
            this.cancelButton.TabIndex = 23;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            this.cancelButton.Click += new System.EventHandler( this.cancelButton_Click );
            // 
            // openButton
            // 
            this.openButton.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.openButton.Location = new System.Drawing.Point( 836, 414 );
            this.openButton.Name = "openButton";
            this.openButton.Size = new System.Drawing.Size( 66, 20 );
            this.openButton.TabIndex = 22;
            this.openButton.Text = "&Open";
            this.openButton.UseVisualStyleBackColor = true;
            this.openButton.Click += new System.EventHandler( this.openButton_Click );
            // 
            // label1
            // 
            this.label1.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left ) ) );
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point( 118, 444 );
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size( 84, 13 );
            this.label1.TabIndex = 21;
            this.label1.Text = "Sources of &type:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // sourceTypeComboBox
            // 
            this.sourceTypeComboBox.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.sourceTypeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.sourceTypeComboBox.FormattingEnabled = true;
            this.sourceTypeComboBox.Location = new System.Drawing.Point( 208, 440 );
            this.sourceTypeComboBox.Name = "sourceTypeComboBox";
            this.sourceTypeComboBox.Size = new System.Drawing.Size( 624, 21 );
            this.sourceTypeComboBox.TabIndex = 20;
            this.sourceTypeComboBox.SelectionChangeCommitted += new System.EventHandler( this.sourceTypeComboBox_SelectionChangeCommitted );
            // 
            // labelSourcePath
            // 
            this.labelSourcePath.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left ) ) );
            this.labelSourcePath.AutoSize = true;
            this.labelSourcePath.Location = new System.Drawing.Point( 118, 418 );
            this.labelSourcePath.Name = "labelSourcePath";
            this.labelSourcePath.Size = new System.Drawing.Size( 73, 13 );
            this.labelSourcePath.TabIndex = 19;
            this.labelSourcePath.Text = "Source &name:";
            this.labelSourcePath.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // sourcePathTextBox
            // 
            this.sourcePathTextBox.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.sourcePathTextBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.sourcePathTextBox.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystem;
            this.sourcePathTextBox.Location = new System.Drawing.Point( 208, 414 );
            this.sourcePathTextBox.Name = "sourcePathTextBox";
            this.sourcePathTextBox.Size = new System.Drawing.Size( 624, 20 );
            this.sourcePathTextBox.TabIndex = 18;
            this.sourcePathTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler( this.sourcePathTextBox_KeyUp );
            // 
            // labelLookIn
            // 
            this.labelLookIn.AutoSize = true;
            this.labelLookIn.Location = new System.Drawing.Point( 67, 14 );
            this.labelLookIn.Name = "labelLookIn";
            this.labelLookIn.Size = new System.Drawing.Size( 45, 13 );
            this.labelLookIn.TabIndex = 17;
            this.labelLookIn.Text = "Look &in:";
            // 
            // lookInComboBox
            // 
            this.lookInComboBox.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.lookInComboBox.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawVariable;
            this.lookInComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.lookInComboBox.FormattingEnabled = true;
            this.lookInComboBox.Location = new System.Drawing.Point( 118, 11 );
            this.lookInComboBox.Name = "lookInComboBox";
            this.lookInComboBox.Size = new System.Drawing.Size( 585, 21 );
            this.lookInComboBox.TabIndex = 16;
            this.lookInComboBox.DrawItem += new System.Windows.Forms.DrawItemEventHandler( this.lookInComboBox_DrawItem );
            this.lookInComboBox.MeasureItem += new System.Windows.Forms.MeasureItemEventHandler( this.lookInComboBox_MeasureItem );
            this.lookInComboBox.SelectionChangeCommitted += new System.EventHandler( this.lookInComboBox_SelectionChangeCommitted );
            this.lookInComboBox.DropDown += new System.EventHandler( this.lookInComboBox_DropDown );
            // 
            // listView
            // 
            this.listView.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom )
                        | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.listView.Columns.AddRange( new System.Windows.Forms.ColumnHeader[] {
            this.SourceName,
            this.SourceType,
            this.Spectra,
            this.SourceSize,
            this.DateModified,
            this.IonSourceType,
            this.AnalyzerType,
            this.DetectorType,
            this.ContentType} );
            this.listView.Location = new System.Drawing.Point( 118, 44 );
            this.listView.Name = "listView";
            this.listView.Size = new System.Drawing.Size( 784, 364 );
            this.listView.TabIndex = 15;
            this.listView.UseCompatibleStateImageBehavior = false;
            this.listView.View = System.Windows.Forms.View.List;
            this.listView.ItemActivate += new System.EventHandler( this.listView_ItemActivate );
            this.listView.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler( this.listView_ColumnClick );
            this.listView.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler( this.listView_ItemSelectionChanged );
            this.listView.KeyDown += new System.Windows.Forms.KeyEventHandler( this.listView_KeyDown );
            // 
            // SourceName
            // 
            this.SourceName.Text = "Name";
            this.SourceName.Width = 200;
            // 
            // SourceType
            // 
            this.SourceType.Text = "Type";
            this.SourceType.Width = 80;
            // 
            // Spectra
            // 
            this.Spectra.Text = "Spectra";
            this.Spectra.Width = 65;
            // 
            // SourceSize
            // 
            this.SourceSize.Text = "Size";
            // 
            // DateModified
            // 
            this.DateModified.Text = "Date Modified";
            this.DateModified.Width = 120;
            // 
            // IonSourceType
            // 
            this.IonSourceType.Text = "Source";
            // 
            // AnalyzerType
            // 
            this.AnalyzerType.Text = "Analyzer";
            // 
            // DetectorType
            // 
            this.DetectorType.Text = "Detector";
            // 
            // ContentType
            // 
            this.ContentType.Text = "Content Type";
            // 
            // OpenDataSourceDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF( 6F, 13F );
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size( 914, 642 );
            this.Controls.Add( this.splitContainer1 );
            this.DoubleBuffered = true;
            this.HelpButton = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "OpenDataSourceDialog";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Open Data Source";
            this.splitContainer1.Panel1.ResumeLayout( false );
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.ResumeLayout( false );
            this.flowLayoutPanel1.ResumeLayout( false );
            this.navToolStrip.ResumeLayout( false );
            this.navToolStrip.PerformLayout();
            this.ResumeLayout( false );

        }

        #endregion

        private System.Windows.Forms.ImageList specialPlacesImageList;
        private System.Windows.Forms.ImageList lookInImageList;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.Button recentDocumentsButton;
        private System.Windows.Forms.Button desktopButton;
        private System.Windows.Forms.Button myDocumentsButton;
        private System.Windows.Forms.Button myComputerButton;
        private System.Windows.Forms.Button myNetworkPlacesButton;
        private System.Windows.Forms.ToolStrip navToolStrip;
        private System.Windows.Forms.ToolStripButton backButton;
        private System.Windows.Forms.ToolStripButton upOneLevelButton;
        private System.Windows.Forms.ToolStripDropDownButton viewsDropDownButton;
        private System.Windows.Forms.ToolStripMenuItem smallIconsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem largeIconsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem tilesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem listToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem detailsToolStripMenuItem;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Button openButton;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox sourceTypeComboBox;
        private System.Windows.Forms.Label labelSourcePath;
        private System.Windows.Forms.TextBox sourcePathTextBox;
        private System.Windows.Forms.Label labelLookIn;
        private System.Windows.Forms.ComboBox lookInComboBox;
        private System.Windows.Forms.ListView listView;
        private System.Windows.Forms.ColumnHeader SourceName;
        private System.Windows.Forms.ColumnHeader SourceType;
        private System.Windows.Forms.ColumnHeader Spectra;
        private System.Windows.Forms.ColumnHeader SourceSize;
        private System.Windows.Forms.ColumnHeader DateModified;
        private System.Windows.Forms.ColumnHeader IonSourceType;
        private System.Windows.Forms.ColumnHeader AnalyzerType;
        private System.Windows.Forms.ColumnHeader DetectorType;
        private System.Windows.Forms.ColumnHeader ContentType;
    }
}