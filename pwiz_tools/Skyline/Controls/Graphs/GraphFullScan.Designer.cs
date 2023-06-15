﻿namespace pwiz.Skyline.Controls.Graphs
{
    partial class GraphFullScan
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GraphFullScan));
            this.toolStripLabel1 = new System.Windows.Forms.ToolStripLabel();
            this.comboSpectrum = new System.Windows.Forms.ToolStripComboBox();
            this.GraphPanel = new System.Windows.Forms.Panel();
            this.graphControlExtension = new MsGraphExtension();
            this.toolBar = new System.Windows.Forms.ToolStrip();
            this.toolStripLabel2 = new System.Windows.Forms.ToolStripLabel();
            this.comboBoxScanType = new System.Windows.Forms.ToolStripComboBox();
            this.rightButton = new System.Windows.Forms.ToolStripButton();
            this.leftButton = new System.Windows.Forms.ToolStripButton();
            this.magnifyBtn = new System.Windows.Forms.ToolStripButton();
            this.spectrumBtn = new System.Windows.Forms.ToolStripButton();
            this.filterBtn = new System.Windows.Forms.ToolStripButton();
            this.propertiesBtn = new System.Windows.Forms.ToolStripButton();
            this.lblScanId = new System.Windows.Forms.ToolStripLabel();
            this.btnIsolationWindow = new System.Windows.Forms.ToolStripButton();
            this.toolStripButtonShowAnnotations = new System.Windows.Forms.ToolStripButton();
            this.toolStripLabelPeakType = new System.Windows.Forms.ToolStripLabel();
            this.comboBoxPeakType = new System.Windows.Forms.ToolStripComboBox();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.showScanNumberContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showPeakAnnotationsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.showCollisionEnergyContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.GraphPanel.SuspendLayout();
            this.toolBar.SuspendLayout();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolStripLabel1
            // 
            this.toolStripLabel1.Name = "toolStripLabel1";
            resources.ApplyResources(this.toolStripLabel1, "toolStripLabel1");
            // 
            // comboSpectrum
            // 
            this.comboSpectrum.Name = "comboSpectrum";
            resources.ApplyResources(this.comboSpectrum, "comboSpectrum");
            // 
            // GraphPanel
            // 
            this.GraphPanel.Controls.Add(this.graphControlExtension);
            this.GraphPanel.Controls.Add(this.toolBar);
            resources.ApplyResources(this.GraphPanel, "GraphPanel");
            this.GraphPanel.Name = "GraphPanel";
            // 
            // graphControl
            // 
            resources.ApplyResources(this.graphControlExtension, "graphControlExtension");
            this.graphControlExtension.Name = "graphControlExtension";
            this.graphControlExtension.PropertySheetVisibilityPropName = "FullScanPropertySheetVisible";
            // 
            // toolBar
            // 
            this.toolBar.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripLabel2,
            this.comboBoxScanType,
            this.propertiesBtn,
            this.rightButton,
            this.leftButton,
            this.magnifyBtn,
            this.spectrumBtn,
            this.filterBtn,
            this.lblScanId,
            this.btnIsolationWindow,
            this.toolStripButtonShowAnnotations,
            this.toolStripLabelPeakType,
            this.comboBoxPeakType});
            resources.ApplyResources(this.toolBar, "toolBar");
            this.toolBar.Name = "toolBar";
            // 
            // toolStripLabel2
            // 
            this.toolStripLabel2.Name = "toolStripLabel2";
            resources.ApplyResources(this.toolStripLabel2, "toolStripLabel2");
            // 
            // comboBoxScanType
            // 
            this.comboBoxScanType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxScanType.Items.AddRange(new object[] {
            resources.GetString("comboBoxScanType.Items"),
            resources.GetString("comboBoxScanType.Items1"),
            resources.GetString("comboBoxScanType.Items2")});
            resources.ApplyResources(this.comboBoxScanType, "comboBoxScanType");
            this.comboBoxScanType.Name = "comboBoxScanType";
            this.comboBoxScanType.Overflow = System.Windows.Forms.ToolStripItemOverflow.Never;
            this.comboBoxScanType.SelectedIndexChanged += new System.EventHandler(this.comboBoxScanType_SelectedIndexChanged);
            // 
            // rightButton
            // 
            this.rightButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.rightButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.rightButton.Image = global::pwiz.Skyline.Properties.Resources.Icojam_Blueberry_Basic_Arrow_right;
            resources.ApplyResources(this.rightButton, "rightButton");
            this.rightButton.Name = "rightButton";
            this.rightButton.Click += new System.EventHandler(this.rightButton_Click);
            // 
            // leftButton
            // 
            this.leftButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.leftButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.leftButton.Image = global::pwiz.Skyline.Properties.Resources.Icojam_Blueberry_Basic_Arrow_left;
            resources.ApplyResources(this.leftButton, "leftButton");
            this.leftButton.Name = "leftButton";
            this.leftButton.Click += new System.EventHandler(this.leftButton_Click);
            // 
            // magnifyBtn
            // 
            this.magnifyBtn.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.magnifyBtn.Checked = true;
            this.magnifyBtn.CheckOnClick = true;
            this.magnifyBtn.CheckState = System.Windows.Forms.CheckState.Checked;
            this.magnifyBtn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.magnifyBtn.Image = global::pwiz.Skyline.Properties.Resources.magnifier_zoom_in;
            resources.ApplyResources(this.magnifyBtn, "magnifyBtn");
            this.magnifyBtn.Margin = new System.Windows.Forms.Padding(0, 1, 10, 2);
            this.magnifyBtn.Name = "magnifyBtn";
            // 
            // spectrumBtn
            // 
            this.spectrumBtn.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.spectrumBtn.Checked = true;
            this.spectrumBtn.CheckOnClick = true;
            this.spectrumBtn.CheckState = System.Windows.Forms.CheckState.Checked;
            this.spectrumBtn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.spectrumBtn.Image = global::pwiz.Skyline.Properties.Resources.DataProcessing;
            resources.ApplyResources(this.spectrumBtn, "spectrumBtn");
            this.spectrumBtn.Name = "spectrumBtn";
            // 
            // filterBtn
            // 
            this.filterBtn.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.filterBtn.CheckOnClick = true;
            this.filterBtn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.filterBtn.Image = global::pwiz.Skyline.Properties.Resources.Filter;
            resources.ApplyResources(this.filterBtn, "filterBtn");
            this.filterBtn.Margin = new System.Windows.Forms.Padding(10, 1, 0, 2);
            this.filterBtn.Name = "filterBtn";
            // 
            // propertiesBtn
            // 
            resources.ApplyResources(this.propertiesBtn, "propertiesBtn");
            this.propertiesBtn.Name = "propertiesBtn";
            this.propertiesBtn.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.propertiesBtn.CheckOnClick = true;
            this.propertiesBtn.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.propertiesBtn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.propertiesBtn.Image = global::pwiz.Skyline.Properties.Resources.Properties_Button;
            this.propertiesBtn.Margin = new System.Windows.Forms.Padding(10, 1, 0, 2);
            this.propertiesBtn.Click += new System.EventHandler(this.propertiesBtn_Click);

            // 
            // lblScanId
            // 
            this.lblScanId.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.lblScanId.Name = "lblScanId";
            resources.ApplyResources(this.lblScanId, "lblScanId");
            // 
            // btnIsolationWindow
            // 
            this.btnIsolationWindow.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.btnIsolationWindow.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            resources.ApplyResources(this.btnIsolationWindow, "btnIsolationWindow");
            this.btnIsolationWindow.Margin = new System.Windows.Forms.Padding(0, 1, 10, 2);
            this.btnIsolationWindow.Name = "btnIsolationWindow";
            this.btnIsolationWindow.Click += new System.EventHandler(this.btnIsolationWindow_Click);
            // 
            // toolStripButtonShowAnnotations
            // 
            this.toolStripButtonShowAnnotations.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.toolStripButtonShowAnnotations.CheckOnClick = true;
            this.toolStripButtonShowAnnotations.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripButtonShowAnnotations.Image = global::pwiz.Skyline.Properties.Resources.AnnotatedSpectum;
            resources.ApplyResources(this.toolStripButtonShowAnnotations, "toolStripButtonShowAnnotations");
            this.toolStripButtonShowAnnotations.Name = "toolStripButtonShowAnnotations";
            // 
            // toolStripLabelPeakType
            // 
            this.toolStripLabelPeakType.Name = "toolStripLabelPeakType";
            resources.ApplyResources(this.toolStripLabelPeakType, "toolStripLabelPeakType");
            // 
            // comboBoxPeakType
            // 
            this.comboBoxPeakType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxPeakType.Items.AddRange(new object[] {
            resources.GetString("comboBoxPeakType.Items"),
            resources.GetString("comboBoxPeakType.Items1")});
            this.comboBoxPeakType.Name = "comboBoxPeakType";
            resources.ApplyResources(this.comboBoxPeakType, "comboBoxPeakType");
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.showScanNumberContextMenuItem,
            this.showCollisionEnergyContextMenuItem,
            this.showPeakAnnotationsContextMenuItem,
            this.toolStripSeparator1});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            resources.ApplyResources(this.contextMenuStrip1, "contextMenuStrip1");
            // 
            // showScanNumberContextMenuItem
            // 
            this.showScanNumberContextMenuItem.Name = "showScanNumberContextMenuItem";
            resources.ApplyResources(this.showScanNumberContextMenuItem, "showScanNumberContextMenuItem");
            this.showScanNumberContextMenuItem.Click += new System.EventHandler(this.showScanNumberToolStripMenuItem_Click);
            // 
            // showIonTypesRanksToolStripMenuItem
            // 
            this.showPeakAnnotationsContextMenuItem.CheckOnClick = true;
            this.showPeakAnnotationsContextMenuItem.Name = "showIonTypesRanksToolStripMenuItem";
            resources.ApplyResources(this.showPeakAnnotationsContextMenuItem, "showIonTypesRanksToolStripMenuItem");
            this.showPeakAnnotationsContextMenuItem.CheckedChanged += new System.EventHandler(this.showIonTypesRanksToolStripMenuItem_CheckedChanged);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            resources.ApplyResources(this.toolStripSeparator1, "toolStripSeparator1");
            // 
            // showCollisionEnergyContextMenuItem
            // 
            this.showCollisionEnergyContextMenuItem.Name = "showCollisionEnergyContextMenuItem";
            resources.ApplyResources(this.showCollisionEnergyContextMenuItem, "showCollisionEnergyContextMenuItem");
            this.showCollisionEnergyContextMenuItem.Click += new System.EventHandler(this.showCollisionEnergyToolStripMenuItem_Click);
            // 
            // GraphFullScan
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.GraphPanel);
            this.HideOnClose = true;
            this.KeyPreview = true;
            this.Name = "GraphFullScan";
            this.VisibleChanged += new System.EventHandler(this.GraphFullScan_VisibleChanged);
            this.GraphPanel.ResumeLayout(false);
            this.GraphPanel.PerformLayout();
            this.toolBar.ResumeLayout(false);
            this.toolBar.PerformLayout();
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ToolStripLabel toolStripLabel1;
        private System.Windows.Forms.ToolStripComboBox comboSpectrum;
        private System.Windows.Forms.Panel GraphPanel;
        private MsGraphExtension graphControlExtension;
        private System.Windows.Forms.ToolStrip toolBar;
        private System.Windows.Forms.ToolStripLabel toolStripLabel2;
        private System.Windows.Forms.ToolStripComboBox comboBoxScanType;
        private System.Windows.Forms.ToolStripButton rightButton;
        private System.Windows.Forms.ToolStripButton leftButton;
        private System.Windows.Forms.ToolStripLabel lblScanId;
        private System.Windows.Forms.ToolStripButton magnifyBtn;
        private System.Windows.Forms.ToolStripButton btnIsolationWindow;
        private System.Windows.Forms.ToolStripButton spectrumBtn;
        private System.Windows.Forms.ToolStripButton filterBtn;
        private System.Windows.Forms.ToolStripButton propertiesBtn;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem showScanNumberContextMenuItem;
        private System.Windows.Forms.ToolStripButton toolStripButtonShowAnnotations;
        private System.Windows.Forms.ToolStripMenuItem showPeakAnnotationsContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripLabel toolStripLabelPeakType;
        private System.Windows.Forms.ToolStripComboBox comboBoxPeakType;
        private System.Windows.Forms.ToolStripMenuItem showCollisionEnergyContextMenuItem;
    }
}