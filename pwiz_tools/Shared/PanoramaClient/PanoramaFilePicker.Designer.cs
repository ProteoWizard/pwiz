using System.Windows.Forms;

namespace pwiz.PanoramaClient
{
    partial class PanoramaFilePicker
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
       /*
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }*/

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PanoramaFilePicker));
            this.label3 = new System.Windows.Forms.Label();
            this.imageList1 = new System.Windows.Forms.ImageList(this.components);
            this.imageList2 = new System.Windows.Forms.ImageList(this.components);
            this.showSkyCheckBox = new System.Windows.Forms.CheckBox();
            this.versionOptions = new System.Windows.Forms.ComboBox();
            this.open = new System.Windows.Forms.Button();
            this.cancel = new System.Windows.Forms.Button();
            this.versionLabel = new System.Windows.Forms.Label();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.noFiles = new System.Windows.Forms.Label();
            this.listView = new System.Windows.Forms.ListView();
            this.colName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colSize = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colVersions = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colReplacedBy = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colCreated = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.panel1 = new System.Windows.Forms.Panel();
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.back = new System.Windows.Forms.ToolStripButton();
            this.forward = new System.Windows.Forms.ToolStripButton();
            this.up = new System.Windows.Forms.ToolStripButton();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.panel1.SuspendLayout();
            this.toolStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // imageList1
            // 
            this.imageList1.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList1.ImageStream")));
            this.imageList1.TransparentColor = System.Drawing.Color.Transparent;
            this.imageList1.Images.SetKeyName(0, "Panorama.bmp");
            this.imageList1.Images.SetKeyName(1, "LabKey.bmp");
            this.imageList1.Images.SetKeyName(2, "ChromLib.bmp");
            this.imageList1.Images.SetKeyName(3, "Folder.png");
            // 
            // imageList2
            // 
            this.imageList2.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList2.ImageStream")));
            this.imageList2.TransparentColor = System.Drawing.Color.Transparent;
            this.imageList2.Images.SetKeyName(0, "File.png");
            this.imageList2.Images.SetKeyName(1, "SkylineDoc.ico");
            // 
            // showSkyCheckBox
            // 
            resources.ApplyResources(this.showSkyCheckBox, "showSkyCheckBox");
            this.showSkyCheckBox.Name = "showSkyCheckBox";
            this.showSkyCheckBox.UseVisualStyleBackColor = true;
            this.showSkyCheckBox.CheckedChanged += new System.EventHandler(this.ShowSkyCheckBox_CheckedChanged);
            // 
            // versionOptions
            // 
            resources.ApplyResources(this.versionOptions, "versionOptions");
            this.versionOptions.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.versionOptions.FormattingEnabled = true;
            this.versionOptions.Items.AddRange(new object[] {
            resources.GetString("versionOptions.Items"),
            resources.GetString("versionOptions.Items1")});
            this.versionOptions.Name = "versionOptions";
            this.versionOptions.SelectedIndexChanged += new System.EventHandler(this.VersionOptions_SelectedIndexChanged);
            // 
            // open
            // 
            resources.ApplyResources(this.open, "open");
            this.open.Name = "open";
            this.open.UseVisualStyleBackColor = true;
            this.open.Click += new System.EventHandler(this.Open_Click);
            // 
            // cancel
            // 
            resources.ApplyResources(this.cancel, "cancel");
            this.cancel.Name = "cancel";
            this.cancel.UseVisualStyleBackColor = true;
            this.cancel.Click += new System.EventHandler(this.Cancel_Click);
            // 
            // versionLabel
            // 
            resources.ApplyResources(this.versionLabel, "versionLabel");
            this.versionLabel.Name = "versionLabel";
            // 
            // splitContainer1
            // 
            resources.ApplyResources(this.splitContainer1, "splitContainer1");
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.noFiles);
            this.splitContainer1.Panel2.Controls.Add(this.listView);
            // 
            // noFiles
            // 
            resources.ApplyResources(this.noFiles, "noFiles");
            this.noFiles.Name = "noFiles";
            // 
            // listView
            // 
            this.listView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colName,
            this.colSize,
            this.colVersions,
            this.colReplacedBy,
            this.colCreated});
            resources.ApplyResources(this.listView, "listView");
            this.listView.FullRowSelect = true;
            this.listView.HideSelection = false;
            this.listView.Name = "listView";
            this.listView.ShowItemToolTips = true;
            this.listView.SmallImageList = this.imageList2;
            this.listView.UseCompatibleStateImageBehavior = false;
            this.listView.View = System.Windows.Forms.View.Details;
            this.listView.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.listView_ColumnClick);
            this.listView.SizeChanged += new System.EventHandler(this.listView_SizeChanged);
            // 
            // colName
            // 
            resources.ApplyResources(this.colName, "colName");
            // 
            // colSize
            // 
            resources.ApplyResources(this.colSize, "colSize");
            // 
            // colVersions
            // 
            resources.ApplyResources(this.colVersions, "colVersions");
            // 
            // colReplacedBy
            // 
            resources.ApplyResources(this.colReplacedBy, "colReplacedBy");
            // 
            // colCreated
            // 
            resources.ApplyResources(this.colCreated, "colCreated");
            // 
            // panel1
            // 
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Controls.Add(this.splitContainer1);
            this.panel1.Name = "panel1";
            // 
            // toolStrip
            // 
            this.toolStrip.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.toolStrip, "toolStrip");
            this.toolStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.back,
            this.forward,
            this.up});
            this.toolStrip.Name = "toolStrip";
            // 
            // back
            // 
            this.back.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.back, "back");
            this.back.Name = "back";
            this.back.Click += new System.EventHandler(this.Back_Click);
            // 
            // forward
            // 
            this.forward.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.forward, "forward");
            this.forward.Name = "forward";
            this.forward.Click += new System.EventHandler(this.Forward_Click);
            // 
            // up
            // 
            resources.ApplyResources(this.up, "up");
            this.up.Name = "up";
            this.up.Click += new System.EventHandler(this.UpButton_Click);
            // 
            // PanoramaFilePicker
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.toolStrip);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.versionLabel);
            this.Controls.Add(this.cancel);
            this.Controls.Add(this.open);
            this.Controls.Add(this.versionOptions);
            this.Controls.Add(this.showSkyCheckBox);
            this.Controls.Add(this.label3);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PanoramaFilePicker";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.Load += new System.EventHandler(this.FilePicker_Load);
            this.SizeChanged += new System.EventHandler(this.PanoramaFilePicker_SizeChanged);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private Label label3;
        private ImageList imageList1;
        private ImageList imageList2;
        private CheckBox showSkyCheckBox;
        private ComboBox versionOptions;
        private Button open;
        private Button cancel;
        private Label versionLabel;
        private SplitContainer splitContainer1;
        private ListView listView;
        private Panel panel1;
        private ToolStrip toolStrip;
        private ToolStripButton back;
        private ToolStripButton forward;
        private ToolStripButton up;
        private ColumnHeader colName;
        private ColumnHeader colSize;
        private ColumnHeader colVersions;
        private ColumnHeader colReplacedBy;
        private ColumnHeader colCreated;
        private Label noFiles;
    }
}