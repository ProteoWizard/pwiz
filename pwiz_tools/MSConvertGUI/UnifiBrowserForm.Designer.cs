//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
//
// Copyright 2018 Matt Chambers - Nashville, TN 37221
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

namespace MSConvertGUI
{
    partial class UnifiBrowserForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UnifiBrowserForm));
            this.VertSplit = new System.Windows.Forms.SplitContainer();
            this.FileTree = new System.Windows.Forms.TreeView();
            this.FolderViewList = new System.Windows.Forms.ListView();
            this.SourceType = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.Analysis = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.WellPosition = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.Replicate = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.SourceName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.AcquisitionStartTime = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.SourceSize = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.DateModified = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.labelSourcePath = new System.Windows.Forms.Label();
            this.serverLocationTextBox = new System.Windows.Forms.TextBox();
            this.cancelButton = new System.Windows.Forms.Button();
            this.openButton = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.sampleResultTextBox = new System.Windows.Forms.TextBox();
            this.connectButton = new System.Windows.Forms.Button();
            this.treeViewImageList = new System.Windows.Forms.ImageList(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.VertSplit)).BeginInit();
            this.VertSplit.Panel1.SuspendLayout();
            this.VertSplit.Panel2.SuspendLayout();
            this.VertSplit.SuspendLayout();
            this.SuspendLayout();
            // 
            // VertSplit
            // 
            this.VertSplit.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.VertSplit.Location = new System.Drawing.Point(12, 31);
            this.VertSplit.Name = "VertSplit";
            // 
            // VertSplit.Panel1
            // 
            this.VertSplit.Panel1.Controls.Add(this.FileTree);
            // 
            // VertSplit.Panel2
            // 
            this.VertSplit.Panel2.Controls.Add(this.FolderViewList);
            this.VertSplit.Size = new System.Drawing.Size(1462, 549);
            this.VertSplit.SplitterDistance = 333;
            this.VertSplit.TabIndex = 44;
            this.VertSplit.TabStop = false;
            // 
            // FileTree
            // 
            this.FileTree.Dock = System.Windows.Forms.DockStyle.Fill;
            this.FileTree.Location = new System.Drawing.Point(0, 0);
            this.FileTree.Name = "FileTree";
            this.FileTree.Size = new System.Drawing.Size(333, 549);
            this.FileTree.TabIndex = 33;
            this.FileTree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.FileTree_AfterSelect);
            // 
            // FolderViewList
            // 
            this.FolderViewList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.SourceType,
            this.Analysis,
            this.WellPosition,
            this.Replicate,
            this.SourceName,
            this.AcquisitionStartTime,
            this.SourceSize,
            this.DateModified});
            this.FolderViewList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.FolderViewList.FullRowSelect = true;
            this.FolderViewList.Location = new System.Drawing.Point(0, 0);
            this.FolderViewList.Name = "FolderViewList";
            this.FolderViewList.Size = new System.Drawing.Size(1125, 549);
            this.FolderViewList.Sorting = System.Windows.Forms.SortOrder.Ascending;
            this.FolderViewList.TabIndex = 24;
            this.FolderViewList.UseCompatibleStateImageBehavior = false;
            this.FolderViewList.View = System.Windows.Forms.View.Details;
            this.FolderViewList.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.FolderViewList_ColumnClick);
            this.FolderViewList.SelectedIndexChanged += new System.EventHandler(this.FolderViewList_SelectedIndexChanged);
            this.FolderViewList.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.FolderViewList_MouseDoubleClick);
            // 
            // SourceType
            // 
            this.SourceType.Text = "Type";
            this.SourceType.Width = 80;
            // 
            // Analysis
            // 
            this.Analysis.Text = "Analysis";
            this.Analysis.Width = 200;
            // 
            // WellPosition
            // 
            this.WellPosition.Text = "Well Position";
            this.WellPosition.Width = 95;
            // 
            // Replicate
            // 
            this.Replicate.Text = "Replicate";
            // 
            // SourceName
            // 
            this.SourceName.Text = "Name";
            this.SourceName.Width = 200;
            // 
            // AcquisitionStartTime
            // 
            this.AcquisitionStartTime.Text = "Acquisition Start Time";
            this.AcquisitionStartTime.Width = 130;
            // 
            // SourceSize
            // 
            this.SourceSize.Text = "Size";
            // 
            // DateModified
            // 
            this.DateModified.Text = "Date Modified";
            this.DateModified.Width = 140;
            // 
            // labelSourcePath
            // 
            this.labelSourcePath.AutoSize = true;
            this.labelSourcePath.Location = new System.Drawing.Point(12, 9);
            this.labelSourcePath.Name = "labelSourcePath";
            this.labelSourcePath.Size = new System.Drawing.Size(81, 13);
            this.labelSourcePath.TabIndex = 46;
            this.labelSourcePath.Text = "&Server location:";
            this.labelSourcePath.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // serverLocationTextBox
            // 
            this.serverLocationTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.serverLocationTextBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.serverLocationTextBox.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystem;
            this.serverLocationTextBox.Location = new System.Drawing.Point(102, 5);
            this.serverLocationTextBox.Name = "serverLocationTextBox";
            this.serverLocationTextBox.Size = new System.Drawing.Size(1275, 20);
            this.serverLocationTextBox.TabIndex = 45;
            // 
            // cancelButton
            // 
            this.cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(1408, 586);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(66, 21);
            this.cancelButton.TabIndex = 50;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            // 
            // openButton
            // 
            this.openButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.openButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.openButton.Location = new System.Drawing.Point(1336, 586);
            this.openButton.Name = "openButton";
            this.openButton.Size = new System.Drawing.Size(66, 21);
            this.openButton.TabIndex = 49;
            this.openButton.Text = "&Open";
            this.openButton.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(7, 590);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(92, 13);
            this.label1.TabIndex = 48;
            this.label1.Text = "Sample &Result ID:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // sampleResultTextBox
            // 
            this.sampleResultTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.sampleResultTextBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.sampleResultTextBox.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystem;
            this.sampleResultTextBox.Location = new System.Drawing.Point(102, 586);
            this.sampleResultTextBox.Name = "sampleResultTextBox";
            this.sampleResultTextBox.Size = new System.Drawing.Size(1228, 20);
            this.sampleResultTextBox.TabIndex = 47;
            // 
            // connectButton
            // 
            this.connectButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.connectButton.Location = new System.Drawing.Point(1383, 5);
            this.connectButton.Name = "connectButton";
            this.connectButton.Size = new System.Drawing.Size(91, 21);
            this.connectButton.TabIndex = 51;
            this.connectButton.Text = "&Connect";
            this.connectButton.UseVisualStyleBackColor = true;
            // 
            // treeViewImageList
            // 
            this.treeViewImageList.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("treeViewImageList.ImageStream")));
            this.treeViewImageList.TransparentColor = System.Drawing.Color.Transparent;
            this.treeViewImageList.Images.SetKeyName(0, "MyNetworkPlaces.png");
            this.treeViewImageList.Images.SetKeyName(1, "folder.png");
            this.treeViewImageList.Images.SetKeyName(2, "DataProcessing.png");
            // 
            // UnifiBrowserForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1486, 618);
            this.Controls.Add(this.connectButton);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.openButton);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.sampleResultTextBox);
            this.Controls.Add(this.labelSourcePath);
            this.Controls.Add(this.serverLocationTextBox);
            this.Controls.Add(this.VertSplit);
            this.Name = "UnifiBrowserForm";
            this.Text = "UNIFI Browser";
            this.VertSplit.Panel1.ResumeLayout(false);
            this.VertSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.VertSplit)).EndInit();
            this.VertSplit.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.SplitContainer VertSplit;
        private System.Windows.Forms.TreeView FileTree;
        private System.Windows.Forms.ListView FolderViewList;
        private System.Windows.Forms.ColumnHeader SourceSize;
        private System.Windows.Forms.Label labelSourcePath;
        private System.Windows.Forms.TextBox serverLocationTextBox;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Button openButton;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox sampleResultTextBox;
        private System.Windows.Forms.Button connectButton;
        private System.Windows.Forms.ImageList treeViewImageList;
        internal System.Windows.Forms.ColumnHeader SourceName;
        internal System.Windows.Forms.ColumnHeader SourceType;
        internal System.Windows.Forms.ColumnHeader DateModified;
        internal System.Windows.Forms.ColumnHeader WellPosition;
        internal System.Windows.Forms.ColumnHeader Replicate;
        internal System.Windows.Forms.ColumnHeader AcquisitionStartTime;
        internal System.Windows.Forms.ColumnHeader Analysis;

    }
}