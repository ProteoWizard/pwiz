using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using IDPicker;

namespace Forms
{

    partial class PTMDigger
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="dispossing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        // Runtime config
        public Workspace.RunTimeConfig rtConfig;
        // Input final assembly file.
        public string inputAssembly;
        public string secondaryResultsAssembly;
        // Workspace
        public Workspace ws;
        public Workspace secondaryMatchesWorkspace;

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PTMDigger));
            this.toolstrip1 = new System.Windows.Forms.ToolStrip();
            this.openToolStripButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.addRawDataPath = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.loadUnimod = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            this.attest = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.RowTotalFilterLabel = new System.Windows.Forms.ToolStripLabel();
            this.RowTotalFilterThresholdComboBox = new System.Windows.Forms.ToolStripComboBox();
            this.tabControl = new System.Windows.Forms.TabControl();
            this.programBindingSource = new System.Windows.Forms.BindingSource(this.components);
            this.tabPageImages = new System.Windows.Forms.ImageList(this.components);
            this.toolstrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.programBindingSource)).BeginInit();
            this.SuspendLayout();
            // 
            // toolstrip1
            // 
            this.toolstrip1.Font = new System.Drawing.Font("Tahoma", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.toolstrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openToolStripButton,
            this.toolStripSeparator1,
            this.addRawDataPath,
            this.toolStripSeparator3,
            this.loadUnimod,
            this.toolStripSeparator4,
            this.attest,
            this.toolStripSeparator2,
            this.RowTotalFilterLabel,
            this.RowTotalFilterThresholdComboBox});
            this.toolstrip1.Location = new System.Drawing.Point(0, 0);
            this.toolstrip1.Name = "toolstrip1";
            this.toolstrip1.Size = new System.Drawing.Size(1160, 26);
            this.toolstrip1.TabIndex = 2;
            // 
            // openToolStripButton
            // 
            this.openToolStripButton.Font = new System.Drawing.Font("Tahoma", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.openToolStripButton.Image = ((System.Drawing.Image)(resources.GetObject("openToolStripButton.Image")));
            this.openToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.openToolStripButton.Name = "openToolStripButton";
            this.openToolStripButton.Size = new System.Drawing.Size(116, 23);
            this.openToolStripButton.Text = "Load Results";
            this.openToolStripButton.TextImageRelation = System.Windows.Forms.TextImageRelation.TextBeforeImage;
            this.openToolStripButton.Click += new System.EventHandler(this.openToolStripButton_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 26);
            // 
            // addRawDataPath
            // 
            this.addRawDataPath.Font = new System.Drawing.Font("Tahoma", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.addRawDataPath.Image = ((System.Drawing.Image)(resources.GetObject("addRawDataPath.Image")));
            this.addRawDataPath.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.addRawDataPath.Name = "addRawDataPath";
            this.addRawDataPath.Size = new System.Drawing.Size(130, 23);
            this.addRawDataPath.Text = "Raw Data Path";
            this.addRawDataPath.TextImageRelation = System.Windows.Forms.TextImageRelation.TextBeforeImage;
            this.addRawDataPath.Click += new System.EventHandler(this.addRawDataPath_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(6, 26);
            // 
            // loadUnimod
            // 
            this.loadUnimod.Font = new System.Drawing.Font("Tahoma", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.loadUnimod.Image = ((System.Drawing.Image)(resources.GetObject("loadUnimod.Image")));
            this.loadUnimod.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.loadUnimod.Name = "loadUnimod";
            this.loadUnimod.Size = new System.Drawing.Size(119, 23);
            this.loadUnimod.Text = "Load Unimod";
            this.loadUnimod.TextImageRelation = System.Windows.Forms.TextImageRelation.TextBeforeImage;
            this.loadUnimod.Click += new System.EventHandler(this.loadUnimod_Click);
            // 
            // toolStripSeparator4
            // 
            this.toolStripSeparator4.Name = "toolStripSeparator4";
            this.toolStripSeparator4.Size = new System.Drawing.Size(6, 26);
            // 
            // attest
            // 
            this.attest.Font = new System.Drawing.Font("Tahoma", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.attest.Image = global::Forms.Properties.Resources.green_check;
            this.attest.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.attest.Name = "attest";
            this.attest.Size = new System.Drawing.Size(132, 23);
            this.attest.Text = "Attest Matches";
            this.attest.TextImageRelation = System.Windows.Forms.TextImageRelation.TextBeforeImage;
            this.attest.Click += new System.EventHandler(this.openSecondaryResults_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(6, 26);
            // 
            // RowTotalFilterLabel
            // 
            this.RowTotalFilterLabel.Enabled = false;
            this.RowTotalFilterLabel.Font = new System.Drawing.Font("Tahoma", 8.400001F, System.Drawing.FontStyle.Bold);
            this.RowTotalFilterLabel.Name = "RowTotalFilterLabel";
            this.RowTotalFilterLabel.Size = new System.Drawing.Size(133, 23);
            this.RowTotalFilterLabel.Text = "Row Total Filter:";
            // 
            // RowTotalFilterThresholdComboBox
            // 
            this.RowTotalFilterThresholdComboBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.RowTotalFilterThresholdComboBox.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.CustomSource;
            this.RowTotalFilterThresholdComboBox.AutoToolTip = true;
            this.RowTotalFilterThresholdComboBox.DropDownWidth = 40;
            this.RowTotalFilterThresholdComboBox.Enabled = false;
            this.RowTotalFilterThresholdComboBox.Font = new System.Drawing.Font("Tahoma", 8.400001F, System.Drawing.FontStyle.Bold);
            this.RowTotalFilterThresholdComboBox.Items.AddRange(new object[] {
            "1",
            "2",
            "5",
            "10",
            "25",
            "50",
            "100",
            "(user)"});
            this.RowTotalFilterThresholdComboBox.Name = "RowTotalFilterThresholdComboBox";
            this.RowTotalFilterThresholdComboBox.Size = new System.Drawing.Size(75, 26);
            this.RowTotalFilterThresholdComboBox.Text = "2";
            this.RowTotalFilterThresholdComboBox.ToolTipText = "Filter rows by total counts";
            this.RowTotalFilterThresholdComboBox.TextChanged += new System.EventHandler(this.RowTotalFilterThresholdComboBox_TextChanged);
            // 
            // tabControl
            // 
            this.tabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl.ImageList = this.tabPageImages;
            this.tabControl.Location = new System.Drawing.Point(0, 26);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(1160, 604);
            this.tabControl.TabIndex = 3;
            // 
            // tabPageImages
            // 
            this.tabPageImages.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("tabPageImages.ImageStream")));
            this.tabPageImages.TransparentColor = System.Drawing.Color.Transparent;
            this.tabPageImages.Images.SetKeyName(0, "green_check.jpg");
            this.tabPageImages.Images.SetKeyName(1, "question_mark.jpg");
            // 
            // PTMDigger
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1160, 630);
            this.Controls.Add(this.tabControl);
            this.Controls.Add(this.toolstrip1);
            this.Name = "PTMDigger";
            this.Text = "PTM Digger";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.toolstrip1.ResumeLayout(false);
            this.toolstrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.programBindingSource)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private BindingSource programBindingSource;
        private ToolStripButton openToolStripButton;
        private TabControl tabControl;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripLabel RowTotalFilterLabel;
        private ToolStripComboBox RowTotalFilterThresholdComboBox;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripButton attest;
        private ToolStripSeparator toolStripSeparator3;
        public ToolStrip toolstrip1;
        private ToolStripSeparator toolStripSeparator4;
        private ToolStripButton loadUnimod;
        private ToolStripButton addRawDataPath;
        private ImageList tabPageImages;
    }
}

