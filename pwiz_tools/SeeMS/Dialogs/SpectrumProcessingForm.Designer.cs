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
    partial class SpectrumProcessingForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SpectrumProcessingForm));
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.processingListView = new System.Windows.Forms.ListView();
            this.EnabledHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ProcessingHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.listViewContextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.addToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addContextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.chargeStateCalculatorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peakPickerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.smootherToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.thresholderToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.lockmassRefinerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addProcessingDropDownButton = new System.Windows.Forms.ToolStripDropDownButton();
            this.removeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.removeProcessingButton = new System.Windows.Forms.ToolStripButton();
            this.moveUpProcessingButton = new System.Windows.Forms.ToolStripButton();
            this.moveDownProcessingButton = new System.Windows.Forms.ToolStripButton();
            this.globalOverrideToolStripButton = new System.Windows.Forms.ToolStripDropDownButton();
            this.global_withAllListedProcessorsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.global_withCurrentlySelectedProcessorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.clearGlobalOverrideToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.overrideModeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.beforeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.afterToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replaceToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.runOverrideToolStripButton = new System.Windows.Forms.ToolStripDropDownButton();
            this.run_withAllListedProcessorsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.run_withCurrentlySelectedProcessorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.clearRunOverrideToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.imageList1 = new System.Windows.Forms.ImageList(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.listViewContextMenuStrip.SuspendLayout();
            this.addContextMenuStrip.SuspendLayout();
            this.toolStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer
            // 
            this.splitContainer.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.splitContainer.Location = new System.Drawing.Point(0, 23);
            this.splitContainer.Name = "splitContainer";
            // 
            // splitContainer.Panel1
            // 
            this.splitContainer.Panel1.Controls.Add(this.processingListView);
            this.splitContainer.Size = new System.Drawing.Size(792, 552);
            this.splitContainer.SplitterDistance = 400;
            this.splitContainer.TabIndex = 1;
            // 
            // processingListView
            // 
            this.processingListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.processingListView.CheckBoxes = true;
            this.processingListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.EnabledHeader,
            this.ProcessingHeader});
            this.processingListView.ContextMenuStrip = this.listViewContextMenuStrip;
            this.processingListView.FullRowSelect = true;
            this.processingListView.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.processingListView.HideSelection = false;
            this.processingListView.LabelWrap = false;
            this.processingListView.Location = new System.Drawing.Point(10, 13);
            this.processingListView.Name = "processingListView";
            this.processingListView.Size = new System.Drawing.Size(374, 521);
            this.processingListView.TabIndex = 4;
            this.processingListView.UseCompatibleStateImageBehavior = false;
            this.processingListView.View = System.Windows.Forms.View.Details;
            this.processingListView.VirtualMode = true;
            this.processingListView.RetrieveVirtualItem += new System.Windows.Forms.RetrieveVirtualItemEventHandler(this.processingListView_RetrieveVirtualItem);
            this.processingListView.SelectedIndexChanged += new System.EventHandler(this.processingListView_SelectedIndexChanged);
            this.processingListView.VirtualItemsSelectionRangeChanged += new System.Windows.Forms.ListViewVirtualItemsSelectionRangeChangedEventHandler(this.processingListView_VirtualItemsSelectionRangeChanged);
            this.processingListView.KeyDown += new System.Windows.Forms.KeyEventHandler(this.processingListView_KeyDown);
            this.processingListView.Layout += new System.Windows.Forms.LayoutEventHandler(this.processingListView_Layout);
            this.processingListView.MouseClick += new System.Windows.Forms.MouseEventHandler(this.processingListView_MouseClick);
            this.processingListView.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.processingListView_MouseDoubleClick);
            // 
            // EnabledHeader
            // 
            this.EnabledHeader.Text = "On";
            this.EnabledHeader.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.EnabledHeader.Width = 32;
            // 
            // ProcessingHeader
            // 
            this.ProcessingHeader.Text = "Processor";
            this.ProcessingHeader.Width = 225;
            // 
            // listViewContextMenuStrip
            // 
            this.listViewContextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addToolStripMenuItem,
            this.removeToolStripMenuItem});
            this.listViewContextMenuStrip.Name = "listViewContextMenuStrip";
            this.listViewContextMenuStrip.Size = new System.Drawing.Size(118, 48);
            this.listViewContextMenuStrip.Opening += new System.ComponentModel.CancelEventHandler(this.ContextMenuStrip_Opening);
            // 
            // addToolStripMenuItem
            // 
            this.addToolStripMenuItem.DropDown = this.addContextMenuStrip;
            this.addToolStripMenuItem.Name = "addToolStripMenuItem";
            this.addToolStripMenuItem.Size = new System.Drawing.Size(117, 22);
            this.addToolStripMenuItem.Text = "Add";
            // 
            // addContextMenuStrip
            // 
            this.addContextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.chargeStateCalculatorToolStripMenuItem,
            this.peakPickerToolStripMenuItem,
            this.smootherToolStripMenuItem,
            this.thresholderToolStripMenuItem,
            this.lockmassRefinerToolStripMenuItem});
            this.addContextMenuStrip.Name = "addContextMenuStrip";
            this.addContextMenuStrip.OwnerItem = this.addToolStripMenuItem;
            this.addContextMenuStrip.Size = new System.Drawing.Size(199, 114);
            // 
            // chargeStateCalculatorToolStripMenuItem
            // 
            this.chargeStateCalculatorToolStripMenuItem.Name = "chargeStateCalculatorToolStripMenuItem";
            this.chargeStateCalculatorToolStripMenuItem.Size = new System.Drawing.Size(198, 22);
            this.chargeStateCalculatorToolStripMenuItem.Text = "Charge State Calculator";
            this.chargeStateCalculatorToolStripMenuItem.Click += new System.EventHandler(this.chargeStateCalculatorToolStripMenuItem_Click);
            // 
            // peakPickerToolStripMenuItem
            // 
            this.peakPickerToolStripMenuItem.Name = "peakPickerToolStripMenuItem";
            this.peakPickerToolStripMenuItem.Size = new System.Drawing.Size(198, 22);
            this.peakPickerToolStripMenuItem.Text = "Peak Picker";
            this.peakPickerToolStripMenuItem.Click += new System.EventHandler(this.centroiderToolStripMenuItem_Click);
            // 
            // smootherToolStripMenuItem
            // 
            this.smootherToolStripMenuItem.Name = "smootherToolStripMenuItem";
            this.smootherToolStripMenuItem.Size = new System.Drawing.Size(198, 22);
            this.smootherToolStripMenuItem.Text = "Smoother";
            this.smootherToolStripMenuItem.Click += new System.EventHandler(this.smootherToolStripMenuItem_Click);
            // 
            // thresholderToolStripMenuItem
            // 
            this.thresholderToolStripMenuItem.Name = "thresholderToolStripMenuItem";
            this.thresholderToolStripMenuItem.Size = new System.Drawing.Size(198, 22);
            this.thresholderToolStripMenuItem.Text = "Thresholder";
            this.thresholderToolStripMenuItem.Click += new System.EventHandler(this.thresholderToolStripMenuItem_Click);
            // 
            // lockmassRefinerToolStripMenuItem
            // 
            this.lockmassRefinerToolStripMenuItem.Name = "lockmassRefinerToolStripMenuItem";
            this.lockmassRefinerToolStripMenuItem.Size = new System.Drawing.Size(198, 22);
            this.lockmassRefinerToolStripMenuItem.Text = "Lockmass Refiner";
            this.lockmassRefinerToolStripMenuItem.Click += new System.EventHandler(this.lockmassRefinerToolStripMenuItem_Click);
            // 
            // addProcessingDropDownButton
            // 
            this.addProcessingDropDownButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.addProcessingDropDownButton.DropDown = this.addContextMenuStrip;
            this.addProcessingDropDownButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.addProcessingDropDownButton.Image = ((System.Drawing.Image)(resources.GetObject("addProcessingDropDownButton.Image")));
            this.addProcessingDropDownButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.addProcessingDropDownButton.Name = "addProcessingDropDownButton";
            this.addProcessingDropDownButton.Size = new System.Drawing.Size(32, 24);
            this.addProcessingDropDownButton.Text = "+";
            this.addProcessingDropDownButton.ToolTipText = "Add";
            // 
            // removeToolStripMenuItem
            // 
            this.removeToolStripMenuItem.Name = "removeToolStripMenuItem";
            this.removeToolStripMenuItem.Size = new System.Drawing.Size(117, 22);
            this.removeToolStripMenuItem.Text = "Remove";
            // 
            // toolStrip
            // 
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addProcessingDropDownButton,
            this.removeProcessingButton,
            this.moveUpProcessingButton,
            this.moveDownProcessingButton,
            this.globalOverrideToolStripButton,
            this.runOverrideToolStripButton});
            this.toolStrip.Location = new System.Drawing.Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new System.Drawing.Size(792, 27);
            this.toolStrip.TabIndex = 1;
            this.toolStrip.Text = "toolStrip1";
            // 
            // removeProcessingButton
            // 
            this.removeProcessingButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.removeProcessingButton.Enabled = false;
            this.removeProcessingButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.removeProcessingButton.Image = ((System.Drawing.Image)(resources.GetObject("removeProcessingButton.Image")));
            this.removeProcessingButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.removeProcessingButton.Name = "removeProcessingButton";
            this.removeProcessingButton.Size = new System.Drawing.Size(23, 24);
            this.removeProcessingButton.Text = "�";
            this.removeProcessingButton.ToolTipText = "Remove";
            this.removeProcessingButton.Click += new System.EventHandler(this.removeProcessingButton_Click);
            // 
            // moveUpProcessingButton
            // 
            this.moveUpProcessingButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.moveUpProcessingButton.Enabled = false;
            this.moveUpProcessingButton.Font = new System.Drawing.Font("Wingdings", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(2)));
            this.moveUpProcessingButton.Image = ((System.Drawing.Image)(resources.GetObject("moveUpProcessingButton.Image")));
            this.moveUpProcessingButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.moveUpProcessingButton.Name = "moveUpProcessingButton";
            this.moveUpProcessingButton.Size = new System.Drawing.Size(26, 24);
            this.moveUpProcessingButton.Text = "�";
            this.moveUpProcessingButton.Click += new System.EventHandler(this.moveUpProcessingButton_Click);
            // 
            // moveDownProcessingButton
            // 
            this.moveDownProcessingButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.moveDownProcessingButton.Enabled = false;
            this.moveDownProcessingButton.Font = new System.Drawing.Font("Wingdings", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(2)));
            this.moveDownProcessingButton.Image = ((System.Drawing.Image)(resources.GetObject("moveDownProcessingButton.Image")));
            this.moveDownProcessingButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.moveDownProcessingButton.Name = "moveDownProcessingButton";
            this.moveDownProcessingButton.Size = new System.Drawing.Size(26, 24);
            this.moveDownProcessingButton.Text = "�";
            this.moveDownProcessingButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.moveDownProcessingButton.Click += new System.EventHandler(this.moveDownProcessingButton_Click);
            // 
            // globalOverrideToolStripButton
            // 
            this.globalOverrideToolStripButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.global_withAllListedProcessorsToolStripMenuItem,
            this.global_withCurrentlySelectedProcessorToolStripMenuItem,
            this.toolStripSeparator1,
            this.overrideModeToolStripMenuItem,
            this.toolStripSeparator2,
            this.clearGlobalOverrideToolStripMenuItem});
            this.globalOverrideToolStripButton.Enabled = false;
            this.globalOverrideToolStripButton.Image = ((System.Drawing.Image)(resources.GetObject("globalOverrideToolStripButton.Image")));
            this.globalOverrideToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.globalOverrideToolStripButton.Name = "globalOverrideToolStripButton";
            this.globalOverrideToolStripButton.Size = new System.Drawing.Size(178, 24);
            this.globalOverrideToolStripButton.Text = "Override Global Processing";
            // 
            // global_withAllListedProcessorsToolStripMenuItem
            // 
            this.global_withAllListedProcessorsToolStripMenuItem.Name = "global_withAllListedProcessorsToolStripMenuItem";
            this.global_withAllListedProcessorsToolStripMenuItem.Size = new System.Drawing.Size(249, 22);
            this.global_withAllListedProcessorsToolStripMenuItem.Text = "With all listed processors";
            this.global_withAllListedProcessorsToolStripMenuItem.Click += new System.EventHandler(this.global_withAllListedProcessorsToolStripMenuItem_Click);
            // 
            // global_withCurrentlySelectedProcessorToolStripMenuItem
            // 
            this.global_withCurrentlySelectedProcessorToolStripMenuItem.Name = "global_withCurrentlySelectedProcessorToolStripMenuItem";
            this.global_withCurrentlySelectedProcessorToolStripMenuItem.Size = new System.Drawing.Size(249, 22);
            this.global_withCurrentlySelectedProcessorToolStripMenuItem.Text = "With currently selected processor";
            this.global_withCurrentlySelectedProcessorToolStripMenuItem.Click += new System.EventHandler(this.global_withCurrentlySelectedProcessorToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(246, 6);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(246, 6);
            // 
            // clearGlobalOverrideToolStripMenuItem
            // 
            this.clearGlobalOverrideToolStripMenuItem.Enabled = false;
            this.clearGlobalOverrideToolStripMenuItem.Name = "clearGlobalOverrideToolStripMenuItem";
            this.clearGlobalOverrideToolStripMenuItem.Size = new System.Drawing.Size(249, 22);
            this.clearGlobalOverrideToolStripMenuItem.Text = "Clear global override";
            this.clearGlobalOverrideToolStripMenuItem.Click += new System.EventHandler(this.clearGlobalOverrideToolStripMenuItem_Click);
            // 
            // overrideModeToolStripMenuItem
            // 
            this.overrideModeToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.beforeToolStripMenuItem,
            this.afterToolStripMenuItem,
            this.replaceToolStripMenuItem});
            this.overrideModeToolStripMenuItem.Name = "overrideModeToolStripMenuItem";
            this.overrideModeToolStripMenuItem.Size = new System.Drawing.Size(249, 22);
            this.overrideModeToolStripMenuItem.Text = "Override mode";
            // 
            // beforeToolStripMenuItem
            // 
            this.beforeToolStripMenuItem.CheckOnClick = true;
            this.beforeToolStripMenuItem.Name = "beforeToolStripMenuItem";
            this.beforeToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.beforeToolStripMenuItem.Text = "Before";
            this.beforeToolStripMenuItem.Click += new System.EventHandler(this.global_overrideModeToolStripMenuItem_Click);
            // 
            // afterToolStripMenuItem
            // 
            this.afterToolStripMenuItem.CheckOnClick = true;
            this.afterToolStripMenuItem.Name = "afterToolStripMenuItem";
            this.afterToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.afterToolStripMenuItem.Text = "After";
            this.afterToolStripMenuItem.Click += new System.EventHandler(this.global_overrideModeToolStripMenuItem_Click);
            // 
            // replaceToolStripMenuItem
            // 
            this.replaceToolStripMenuItem.Checked = true;
            this.replaceToolStripMenuItem.CheckOnClick = true;
            this.replaceToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.replaceToolStripMenuItem.Name = "replaceToolStripMenuItem";
            this.replaceToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.replaceToolStripMenuItem.Text = "Replace";
            this.replaceToolStripMenuItem.Click += new System.EventHandler(this.global_overrideModeToolStripMenuItem_Click);
            // 
            // runOverrideToolStripButton
            // 
            this.runOverrideToolStripButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.run_withAllListedProcessorsToolStripMenuItem,
            this.run_withCurrentlySelectedProcessorToolStripMenuItem,
            this.clearRunOverrideToolStripMenuItem});
            this.runOverrideToolStripButton.Enabled = false;
            this.runOverrideToolStripButton.Image = ((System.Drawing.Image)(resources.GetObject("runOverrideToolStripButton.Image")));
            this.runOverrideToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.runOverrideToolStripButton.Name = "runOverrideToolStripButton";
            this.runOverrideToolStripButton.Size = new System.Drawing.Size(165, 24);
            this.runOverrideToolStripButton.Text = "Override Run Processing";
            this.runOverrideToolStripButton.DropDownOpening += new System.EventHandler(this.runOverrideToolStripButton_DropDownOpening);
            // 
            // run_withAllListedProcessorsToolStripMenuItem
            // 
            this.run_withAllListedProcessorsToolStripMenuItem.Name = "run_withAllListedProcessorsToolStripMenuItem";
            this.run_withAllListedProcessorsToolStripMenuItem.Size = new System.Drawing.Size(249, 22);
            this.run_withAllListedProcessorsToolStripMenuItem.Text = "With all listed processors";
            this.run_withAllListedProcessorsToolStripMenuItem.Click += new System.EventHandler(this.run_withAllListedProcessorsToolStripMenuItem_Click);
            // 
            // run_withCurrentlySelectedProcessorToolStripMenuItem
            // 
            this.run_withCurrentlySelectedProcessorToolStripMenuItem.Name = "run_withCurrentlySelectedProcessorToolStripMenuItem";
            this.run_withCurrentlySelectedProcessorToolStripMenuItem.Size = new System.Drawing.Size(249, 22);
            this.run_withCurrentlySelectedProcessorToolStripMenuItem.Text = "With currently selected processor";
            this.run_withCurrentlySelectedProcessorToolStripMenuItem.Click += new System.EventHandler(this.run_withCurrentlySelectedProcessorToolStripMenuItem_Click);
            // 
            // clearRunOverrideToolStripMenuItem
            // 
            this.clearRunOverrideToolStripMenuItem.Enabled = false;
            this.clearRunOverrideToolStripMenuItem.Name = "clearRunOverrideToolStripMenuItem";
            this.clearRunOverrideToolStripMenuItem.Size = new System.Drawing.Size(249, 22);
            this.clearRunOverrideToolStripMenuItem.Text = "Clear run override";
            this.clearRunOverrideToolStripMenuItem.Click += new System.EventHandler(this.clearRunOverrideToolStripMenuItem_Click);
            // 
            // imageList1
            // 
            this.imageList1.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList1.ImageStream")));
            this.imageList1.TransparentColor = System.Drawing.Color.Transparent;
            this.imageList1.Images.SetKeyName(0, "Thresholder.png");
            this.imageList1.Images.SetKeyName(1, "Centroider.png");
            this.imageList1.Images.SetKeyName(2, "DataProcessing.png");
            this.imageList1.Images.SetKeyName(3, "Smoother.png");
            // 
            // SpectrumProcessingForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(792, 573);
            this.Controls.Add(this.toolStrip);
            this.Controls.Add(this.splitContainer);
            this.Name = "SpectrumProcessingForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.TabText = "Spectrum Data Processing Manager";
            this.Text = "Spectrum Data Processing Manager";
            this.splitContainer.Panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.listViewContextMenuStrip.ResumeLayout(false);
            this.addContextMenuStrip.ResumeLayout(false);
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripDropDownButton runOverrideToolStripButton;
        private System.Windows.Forms.ToolStripDropDownButton globalOverrideToolStripButton;
        private System.Windows.Forms.ImageList imageList1;
        private System.Windows.Forms.ListView processingListView;
        private System.Windows.Forms.ColumnHeader ProcessingHeader;
        private System.Windows.Forms.ToolStripDropDownButton addProcessingDropDownButton;
        private System.Windows.Forms.ContextMenuStrip addContextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem thresholderToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addToolStripMenuItem;
        private System.Windows.Forms.ContextMenuStrip listViewContextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem removeToolStripMenuItem;
        private System.Windows.Forms.ToolStripButton removeProcessingButton;
        private System.Windows.Forms.ToolStripMenuItem chargeStateCalculatorToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peakPickerToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem smootherToolStripMenuItem;
        private System.Windows.Forms.ColumnHeader EnabledHeader;
        private System.Windows.Forms.ToolStripButton moveUpProcessingButton;
        private System.Windows.Forms.ToolStripButton moveDownProcessingButton;
        private System.Windows.Forms.ToolStripMenuItem lockmassRefinerToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem global_withAllListedProcessorsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem global_withCurrentlySelectedProcessorToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem clearGlobalOverrideToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem run_withAllListedProcessorsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem run_withCurrentlySelectedProcessorToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem clearRunOverrideToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem overrideModeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem beforeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem afterToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem replaceToolStripMenuItem;


    }
}