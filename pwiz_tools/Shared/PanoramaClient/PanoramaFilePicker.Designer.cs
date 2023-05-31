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
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(7, 36);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(81, 13);
            this.label3.TabIndex = 6;
            this.label3.Text = "Remote folders:";
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
            this.showSkyCheckBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.showSkyCheckBox.AutoSize = true;
            this.showSkyCheckBox.Location = new System.Drawing.Point(250, 25);
            this.showSkyCheckBox.Name = "showSkyCheckBox";
            this.showSkyCheckBox.Size = new System.Drawing.Size(163, 17);
            this.showSkyCheckBox.TabIndex = 8;
            this.showSkyCheckBox.Text = "View folders with Skyline files";
            this.showSkyCheckBox.UseVisualStyleBackColor = true;
            this.showSkyCheckBox.CheckedChanged += new System.EventHandler(this.ShowSkyCheckBox_CheckedChanged);
            // 
            // versionOptions
            // 
            this.versionOptions.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.versionOptions.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.versionOptions.FormattingEnabled = true;
            this.versionOptions.Items.AddRange(new object[] {
            "All",
            "Most recent"});
            this.versionOptions.Location = new System.Drawing.Point(573, 26);
            this.versionOptions.Name = "versionOptions";
            this.versionOptions.Size = new System.Drawing.Size(138, 21);
            this.versionOptions.TabIndex = 10;
            this.versionOptions.Visible = false;
            this.versionOptions.SelectedIndexChanged += new System.EventHandler(this.VersionOptions_SelectedIndexChanged);
            // 
            // open
            // 
            this.open.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.open.Location = new System.Drawing.Point(577, 540);
            this.open.Name = "open";
            this.open.Size = new System.Drawing.Size(64, 20);
            this.open.TabIndex = 11;
            this.open.Text = "Open";
            this.open.UseVisualStyleBackColor = true;
            this.open.Click += new System.EventHandler(this.Open_Click);
            // 
            // cancel
            // 
            this.cancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancel.Location = new System.Drawing.Point(647, 540);
            this.cancel.Name = "cancel";
            this.cancel.Size = new System.Drawing.Size(64, 20);
            this.cancel.TabIndex = 12;
            this.cancel.Text = "Cancel";
            this.cancel.UseVisualStyleBackColor = true;
            this.cancel.Click += new System.EventHandler(this.Cancel_Click);
            // 
            // versionLabel
            // 
            this.versionLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.versionLabel.AutoSize = true;
            this.versionLabel.Location = new System.Drawing.Point(570, 10);
            this.versionLabel.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.versionLabel.Name = "versionLabel";
            this.versionLabel.Size = new System.Drawing.Size(50, 13);
            this.versionLabel.TabIndex = 13;
            this.versionLabel.Text = "Versions:";
            this.versionLabel.Visible = false;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Margin = new System.Windows.Forms.Padding(2);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.noFiles);
            this.splitContainer1.Panel2.Controls.Add(this.listView);
            this.splitContainer1.Size = new System.Drawing.Size(701, 483);
            this.splitContainer1.SplitterDistance = 237;
            this.splitContainer1.SplitterWidth = 3;
            this.splitContainer1.TabIndex = 7;
            // 
            // noFiles
            // 
            this.noFiles.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.noFiles.AutoSize = true;
            this.noFiles.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.noFiles.Location = new System.Drawing.Point(115, 209);
            this.noFiles.Name = "noFiles";
            this.noFiles.Size = new System.Drawing.Size(231, 16);
            this.noFiles.TabIndex = 9;
            this.noFiles.Text = "There are no Skyline files in this folder";
            this.noFiles.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // listView
            // 
            this.listView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colName,
            this.colSize,
            this.colVersions,
            this.colReplacedBy,
            this.colCreated});
            this.listView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listView.FullRowSelect = true;
            this.listView.HideSelection = false;
            this.listView.Location = new System.Drawing.Point(0, 0);
            this.listView.Margin = new System.Windows.Forms.Padding(2);
            this.listView.Name = "listView";
            this.listView.ShowItemToolTips = true;
            this.listView.Size = new System.Drawing.Size(461, 483);
            this.listView.SmallImageList = this.imageList2;
            this.listView.TabIndex = 8;
            this.listView.UseCompatibleStateImageBehavior = false;
            this.listView.View = System.Windows.Forms.View.Details;
            this.listView.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.listView_ColumnClick);
            this.listView.SizeChanged += new System.EventHandler(this.listView_SizeChanged);
            // 
            // colName
            // 
            this.colName.Text = "Name";
            this.colName.Width = 175;
            // 
            // colSize
            // 
            this.colSize.Text = "Size";
            this.colSize.Width = 75;
            // 
            // colVersions
            // 
            this.colVersions.Text = "Versions";
            this.colVersions.Width = 52;
            // 
            // colReplacedBy
            // 
            this.colReplacedBy.Text = "Replaced By";
            this.colReplacedBy.Width = 100;
            // 
            // colCreated
            // 
            this.colCreated.Text = "Created";
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.Controls.Add(this.splitContainer1);
            this.panel1.Location = new System.Drawing.Point(10, 52);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(701, 483);
            this.panel1.TabIndex = 14;
            // 
            // toolStrip
            // 
            this.toolStrip.BackColor = System.Drawing.Color.Transparent;
            this.toolStrip.Dock = System.Windows.Forms.DockStyle.None;
            this.toolStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.back,
            this.forward,
            this.up});
            this.toolStrip.Location = new System.Drawing.Point(9, 2);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new System.Drawing.Size(96, 31);
            this.toolStrip.TabIndex = 18;
            this.toolStrip.Text = "toolStrip1";
            // 
            // back
            // 
            this.back.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.back.Image = ((System.Drawing.Image)(resources.GetObject("back.Image")));
            this.back.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.back.Name = "back";
            this.back.Size = new System.Drawing.Size(28, 28);
            this.back.Text = "Back";
            this.back.Click += new System.EventHandler(this.Back_Click);
            // 
            // forward
            // 
            this.forward.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.forward.Image = ((System.Drawing.Image)(resources.GetObject("forward.Image")));
            this.forward.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.forward.Name = "forward";
            this.forward.Size = new System.Drawing.Size(28, 28);
            this.forward.Text = "Forward";
            this.forward.Click += new System.EventHandler(this.Forward_Click);
            // 
            // up
            // 
            this.up.Image = ((System.Drawing.Image)(resources.GetObject("up.Image")));
            this.up.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.up.Name = "up";
            this.up.Size = new System.Drawing.Size(28, 28);
            this.up.Click += new System.EventHandler(this.UpButton_Click);
            // 
            // PanoramaFilePicker
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(723, 564);
            this.Controls.Add(this.toolStrip);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.versionLabel);
            this.Controls.Add(this.cancel);
            this.Controls.Add(this.open);
            this.Controls.Add(this.versionOptions);
            this.Controls.Add(this.showSkyCheckBox);
            this.Controls.Add(this.label3);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PanoramaFilePicker";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Panorama Folders";
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