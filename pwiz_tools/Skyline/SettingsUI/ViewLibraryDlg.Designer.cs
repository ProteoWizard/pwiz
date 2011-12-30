namespace pwiz.Skyline.SettingsUI
{
    partial class ViewLibraryDlg
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
            this.splitMain = new System.Windows.Forms.SplitContainer();
            this.splitPeptideList = new System.Windows.Forms.SplitContainer();
            this.PeptideListPanel = new System.Windows.Forms.Panel();
            this.listPeptide = new System.Windows.Forms.ListBox();
            this.cbShowModMasses = new System.Windows.Forms.CheckBox();
            this.PageCount = new System.Windows.Forms.Label();
            this.PeptideCount = new System.Windows.Forms.Label();
            this.NextLink = new System.Windows.Forms.LinkLabel();
            this.PreviousLink = new System.Windows.Forms.LinkLabel();
            this.PeptideEditPanel = new System.Windows.Forms.Panel();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.comboLibrary = new System.Windows.Forms.ComboBox();
            this.btnLibDetails = new System.Windows.Forms.Button();
            this.PeptideLabel = new System.Windows.Forms.Label();
            this.textPeptide = new System.Windows.Forms.TextBox();
            this.GraphPanel = new System.Windows.Forms.Panel();
            this.graphControl = new pwiz.MSGraph.MSGraphControl();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.btnAIons = new System.Windows.Forms.ToolStripButton();
            this.btnBIons = new System.Windows.Forms.ToolStripButton();
            this.btnCIons = new System.Windows.Forms.ToolStripButton();
            this.btnXIons = new System.Windows.Forms.ToolStripButton();
            this.btnYIons = new System.Windows.Forms.ToolStripButton();
            this.btnZIons = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.charge1Button = new System.Windows.Forms.ToolStripButton();
            this.charge2Button = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.copyMetafileButton = new System.Windows.Forms.ToolStripButton();
            this.btnCopy = new System.Windows.Forms.ToolStripButton();
            this.btnSave = new System.Windows.Forms.ToolStripButton();
            this.btnPrint = new System.Windows.Forms.ToolStripButton();
            this.panel2 = new System.Windows.Forms.Panel();
            this.cbAssociateProteins = new System.Windows.Forms.CheckBox();
            this.labelFilename = new System.Windows.Forms.Label();
            this.btnAdd = new System.Windows.Forms.Button();
            this.btnAddAll = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.labelRT = new System.Windows.Forms.Label();
            this.ViewLibraryPanel = new System.Windows.Forms.Panel();
            this.LibraryLabel = new System.Windows.Forms.Label();
            this.contextMenuSpectrum = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.aionsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bionsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.cionsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.xionsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.yionsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.zionsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.precursorIonContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator11 = new System.Windows.Forms.ToolStripSeparator();
            this.chargesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.charge1ContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.charge2ContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.charge3ContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.charge4ContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator12 = new System.Windows.Forms.ToolStripSeparator();
            this.ranksContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ionMzValuesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.observedMzValuesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.duplicatesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator13 = new System.Windows.Forms.ToolStripSeparator();
            this.lockYaxisContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator14 = new System.Windows.Forms.ToolStripSeparator();
            this.spectrumPropsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator15 = new System.Windows.Forms.ToolStripSeparator();
            this.zoomSpectrumContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator27 = new System.Windows.Forms.ToolStripSeparator();
            this.splitMain.Panel1.SuspendLayout();
            this.splitMain.Panel2.SuspendLayout();
            this.splitMain.SuspendLayout();
            this.splitPeptideList.Panel1.SuspendLayout();
            this.splitPeptideList.Panel2.SuspendLayout();
            this.splitPeptideList.SuspendLayout();
            this.PeptideListPanel.SuspendLayout();
            this.PeptideEditPanel.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.GraphPanel.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.ViewLibraryPanel.SuspendLayout();
            this.contextMenuSpectrum.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitMain
            // 
            this.splitMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitMain.Location = new System.Drawing.Point(0, 0);
            this.splitMain.Name = "splitMain";
            // 
            // splitMain.Panel1
            // 
            this.splitMain.Panel1.Controls.Add(this.splitPeptideList);
            this.splitMain.Panel1.Controls.Add(this.PeptideEditPanel);
            // 
            // splitMain.Panel2
            // 
            this.splitMain.Panel2.Controls.Add(this.GraphPanel);
            this.splitMain.Panel2.Controls.Add(this.panel2);
            this.splitMain.Size = new System.Drawing.Size(739, 389);
            this.splitMain.SplitterDistance = 258;
            this.splitMain.TabIndex = 0;
            this.splitMain.TabStop = false;
            this.splitMain.MouseDown += new System.Windows.Forms.MouseEventHandler(this.splitMain_MouseDown);
            this.splitMain.MouseUp += new System.Windows.Forms.MouseEventHandler(this.splitMain_MouseUp);
            // 
            // splitPeptideList
            // 
            this.splitPeptideList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitPeptideList.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitPeptideList.IsSplitterFixed = true;
            this.splitPeptideList.Location = new System.Drawing.Point(0, 63);
            this.splitPeptideList.Name = "splitPeptideList";
            this.splitPeptideList.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitPeptideList.Panel1
            // 
            this.splitPeptideList.Panel1.Controls.Add(this.PeptideListPanel);
            this.splitPeptideList.Panel1.Padding = new System.Windows.Forms.Padding(0, 3, 0, 0);
            // 
            // splitPeptideList.Panel2
            // 
            this.splitPeptideList.Panel2.Controls.Add(this.PageCount);
            this.splitPeptideList.Panel2.Controls.Add(this.PeptideCount);
            this.splitPeptideList.Panel2.Controls.Add(this.NextLink);
            this.splitPeptideList.Panel2.Controls.Add(this.PreviousLink);
            this.splitPeptideList.Panel2MinSize = 50;
            this.splitPeptideList.Size = new System.Drawing.Size(258, 326);
            this.splitPeptideList.SplitterDistance = 251;
            this.splitPeptideList.TabIndex = 2;
            // 
            // PeptideListPanel
            // 
            this.PeptideListPanel.Controls.Add(this.listPeptide);
            this.PeptideListPanel.Controls.Add(this.cbShowModMasses);
            this.PeptideListPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PeptideListPanel.Location = new System.Drawing.Point(0, 3);
            this.PeptideListPanel.Name = "PeptideListPanel";
            this.PeptideListPanel.Size = new System.Drawing.Size(258, 248);
            this.PeptideListPanel.TabIndex = 3;
            // 
            // listPeptide
            // 
            this.listPeptide.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listPeptide.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            this.listPeptide.FormattingEnabled = true;
            this.listPeptide.ItemHeight = 16;
            this.listPeptide.Location = new System.Drawing.Point(0, 0);
            this.listPeptide.Name = "listPeptide";
            this.listPeptide.Size = new System.Drawing.Size(258, 228);
            this.listPeptide.TabIndex = 0;
            this.listPeptide.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.listPeptide_DrawItem);
            this.listPeptide.SelectedIndexChanged += new System.EventHandler(this.listPeptide_SelectedIndexChanged);
            this.listPeptide.MouseMove += new System.Windows.Forms.MouseEventHandler(this.listPeptide_MouseMove);
            this.listPeptide.MouseLeave += new System.EventHandler(this.listPeptide_MouseLeave);
            // 
            // cbShowModMasses
            // 
            this.cbShowModMasses.AutoSize = true;
            this.cbShowModMasses.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.cbShowModMasses.Location = new System.Drawing.Point(0, 231);
            this.cbShowModMasses.Name = "cbShowModMasses";
            this.cbShowModMasses.Size = new System.Drawing.Size(258, 17);
            this.cbShowModMasses.TabIndex = 1;
            this.cbShowModMasses.Text = "&Show modification masses";
            this.cbShowModMasses.UseVisualStyleBackColor = true;
            this.cbShowModMasses.Visible = false;
            this.cbShowModMasses.CheckedChanged += new System.EventHandler(this.cbShowModMasses_CheckedChanged);
            // 
            // PageCount
            // 
            this.PageCount.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.PageCount.AutoSize = true;
            this.PageCount.Location = new System.Drawing.Point(108, 0);
            this.PageCount.Margin = new System.Windows.Forms.Padding(3, 0, 0, 0);
            this.PageCount.Name = "PageCount";
            this.PageCount.Size = new System.Drawing.Size(35, 13);
            this.PageCount.TabIndex = 2;
            this.PageCount.Text = "label1";
            // 
            // PeptideCount
            // 
            this.PeptideCount.AutoSize = true;
            this.PeptideCount.Location = new System.Drawing.Point(0, 23);
            this.PeptideCount.Margin = new System.Windows.Forms.Padding(0);
            this.PeptideCount.Name = "PeptideCount";
            this.PeptideCount.Size = new System.Drawing.Size(71, 13);
            this.PeptideCount.TabIndex = 0;
            this.PeptideCount.Text = "PeptideCount";
            // 
            // NextLink
            // 
            this.NextLink.AutoSize = true;
            this.NextLink.Dock = System.Windows.Forms.DockStyle.Right;
            this.NextLink.Location = new System.Drawing.Point(214, 0);
            this.NextLink.Name = "NextLink";
            this.NextLink.Size = new System.Drawing.Size(44, 13);
            this.NextLink.TabIndex = 1;
            this.NextLink.TabStop = true;
            this.NextLink.Text = "Next >>";
            this.NextLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.NextLink_LinkClicked);
            // 
            // PreviousLink
            // 
            this.PreviousLink.AutoSize = true;
            this.PreviousLink.Dock = System.Windows.Forms.DockStyle.Left;
            this.PreviousLink.Location = new System.Drawing.Point(0, 0);
            this.PreviousLink.Name = "PreviousLink";
            this.PreviousLink.Size = new System.Drawing.Size(63, 13);
            this.PreviousLink.TabIndex = 0;
            this.PreviousLink.TabStop = true;
            this.PreviousLink.Text = "<< Previous";
            this.PreviousLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.PreviousLink_LinkClicked);
            // 
            // PeptideEditPanel
            // 
            this.PeptideEditPanel.Controls.Add(this.tableLayoutPanel1);
            this.PeptideEditPanel.Controls.Add(this.PeptideLabel);
            this.PeptideEditPanel.Controls.Add(this.textPeptide);
            this.PeptideEditPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.PeptideEditPanel.Location = new System.Drawing.Point(0, 0);
            this.PeptideEditPanel.Name = "PeptideEditPanel";
            this.PeptideEditPanel.Size = new System.Drawing.Size(258, 63);
            this.PeptideEditPanel.TabIndex = 0;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 90.31007F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 9.689922F));
            this.tableLayoutPanel1.Controls.Add(this.comboLibrary, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.btnLibDetails, 1, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(258, 24);
            this.tableLayoutPanel1.TabIndex = 3;
            // 
            // comboLibrary
            // 
            this.comboLibrary.Dock = System.Windows.Forms.DockStyle.Top;
            this.comboLibrary.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboLibrary.FormattingEnabled = true;
            this.comboLibrary.Location = new System.Drawing.Point(0, 0);
            this.comboLibrary.Margin = new System.Windows.Forms.Padding(0);
            this.comboLibrary.Name = "comboLibrary";
            this.comboLibrary.Size = new System.Drawing.Size(233, 21);
            this.comboLibrary.TabIndex = 0;
            this.comboLibrary.SelectedIndexChanged += new System.EventHandler(this.LibraryComboBox_SelectedIndexChanged);
            // 
            // btnLibDetails
            // 
            this.btnLibDetails.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnLibDetails.Location = new System.Drawing.Point(233, 0);
            this.btnLibDetails.Margin = new System.Windows.Forms.Padding(0);
            this.btnLibDetails.Name = "btnLibDetails";
            this.btnLibDetails.Size = new System.Drawing.Size(24, 21);
            this.btnLibDetails.TabIndex = 5;
            this.btnLibDetails.Text = "...";
            this.btnLibDetails.UseVisualStyleBackColor = true;
            this.btnLibDetails.Click += new System.EventHandler(this.btnLibDetails_Click);
            // 
            // PeptideLabel
            // 
            this.PeptideLabel.AutoSize = true;
            this.PeptideLabel.Location = new System.Drawing.Point(0, 27);
            this.PeptideLabel.Name = "PeptideLabel";
            this.PeptideLabel.Size = new System.Drawing.Size(46, 13);
            this.PeptideLabel.TabIndex = 1;
            this.PeptideLabel.Text = "&Peptide:";
            // 
            // textPeptide
            // 
            this.textPeptide.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textPeptide.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.textPeptide.Location = new System.Drawing.Point(0, 43);
            this.textPeptide.Name = "textPeptide";
            this.textPeptide.Size = new System.Drawing.Size(258, 20);
            this.textPeptide.TabIndex = 2;
            this.textPeptide.TextChanged += new System.EventHandler(this.textPeptide_TextChanged);
            this.textPeptide.KeyDown += new System.Windows.Forms.KeyEventHandler(this.PeptideTextBox_KeyDown);
            // 
            // GraphPanel
            // 
            this.GraphPanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.GraphPanel.Controls.Add(this.graphControl);
            this.GraphPanel.Controls.Add(this.toolStrip1);
            this.GraphPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.GraphPanel.Location = new System.Drawing.Point(0, 0);
            this.GraphPanel.Name = "GraphPanel";
            this.GraphPanel.Size = new System.Drawing.Size(477, 333);
            this.GraphPanel.TabIndex = 0;
            // 
            // graphControl
            // 
            this.graphControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.graphControl.EditButtons = System.Windows.Forms.MouseButtons.Left;
            this.graphControl.EditModifierKeys = System.Windows.Forms.Keys.None;
            this.graphControl.IsEnableVPan = false;
            this.graphControl.IsEnableVZoom = false;
            this.graphControl.IsShowCopyMessage = false;
            this.graphControl.Location = new System.Drawing.Point(0, 0);
            this.graphControl.Name = "graphControl";
            this.graphControl.ScrollGrace = 0;
            this.graphControl.ScrollMaxX = 0;
            this.graphControl.ScrollMaxY = 0;
            this.graphControl.ScrollMaxY2 = 0;
            this.graphControl.ScrollMinX = 0;
            this.graphControl.ScrollMinY = 0;
            this.graphControl.ScrollMinY2 = 0;
            this.graphControl.Size = new System.Drawing.Size(449, 329);
            this.graphControl.TabIndex = 3;
            this.graphControl.ContextMenuBuilder += new ZedGraph.ZedGraphControl.ContextMenuBuilderEventHandler(this.graphControl_ContextMenuBuilder);
            // 
            // toolStrip1
            // 
            this.toolStrip1.Dock = System.Windows.Forms.DockStyle.Right;
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnAIons,
            this.btnBIons,
            this.btnCIons,
            this.btnXIons,
            this.btnYIons,
            this.btnZIons,
            this.toolStripSeparator1,
            this.charge1Button,
            this.charge2Button,
            this.toolStripSeparator2,
            this.copyMetafileButton,
            this.btnCopy,
            this.btnSave,
            this.btnPrint});
            this.toolStrip1.Location = new System.Drawing.Point(449, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(24, 329);
            this.toolStrip1.TabIndex = 4;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // btnAIons
            // 
            this.btnAIons.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnAIons.Image = global::pwiz.Skyline.Properties.Resources.Ions_A;
            this.btnAIons.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnAIons.Name = "btnAIons";
            this.btnAIons.Size = new System.Drawing.Size(21, 20);
            this.btnAIons.Text = "toolStripButton1";
            this.btnAIons.ToolTipText = "A-ions";
            this.btnAIons.Click += new System.EventHandler(this.aionsContextMenuItem_Click);
            // 
            // btnBIons
            // 
            this.btnBIons.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnBIons.Image = global::pwiz.Skyline.Properties.Resources.Ions_B;
            this.btnBIons.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnBIons.Name = "btnBIons";
            this.btnBIons.Size = new System.Drawing.Size(21, 20);
            this.btnBIons.Text = "toolStripButton2";
            this.btnBIons.ToolTipText = "B-ions";
            this.btnBIons.Click += new System.EventHandler(this.bionsContextMenuItem_Click);
            // 
            // btnCIons
            // 
            this.btnCIons.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnCIons.Image = global::pwiz.Skyline.Properties.Resources.Ions_C;
            this.btnCIons.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnCIons.Name = "btnCIons";
            this.btnCIons.Size = new System.Drawing.Size(21, 20);
            this.btnCIons.Text = "toolStripButton3";
            this.btnCIons.ToolTipText = "C-ions";
            this.btnCIons.Click += new System.EventHandler(this.cionsContextMenuItem_Click);
            // 
            // btnXIons
            // 
            this.btnXIons.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnXIons.Image = global::pwiz.Skyline.Properties.Resources.Ions_X;
            this.btnXIons.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnXIons.Name = "btnXIons";
            this.btnXIons.Size = new System.Drawing.Size(21, 20);
            this.btnXIons.Text = "toolStripButton4";
            this.btnXIons.ToolTipText = "X-ions";
            this.btnXIons.Click += new System.EventHandler(this.xionsContextMenuItem_Click);
            // 
            // btnYIons
            // 
            this.btnYIons.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnYIons.Image = global::pwiz.Skyline.Properties.Resources.Ions_Y;
            this.btnYIons.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnYIons.Name = "btnYIons";
            this.btnYIons.Size = new System.Drawing.Size(21, 20);
            this.btnYIons.Text = "toolStripButton5";
            this.btnYIons.ToolTipText = "Y-ions";
            this.btnYIons.Click += new System.EventHandler(this.yionsContextMenuItem_Click);
            // 
            // btnZIons
            // 
            this.btnZIons.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnZIons.Image = global::pwiz.Skyline.Properties.Resources.Ions_Z;
            this.btnZIons.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnZIons.Name = "btnZIons";
            this.btnZIons.Size = new System.Drawing.Size(21, 20);
            this.btnZIons.Text = "toolStripButton6";
            this.btnZIons.ToolTipText = "Z-ions";
            this.btnZIons.Click += new System.EventHandler(this.zionsContextMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(21, 6);
            // 
            // charge1Button
            // 
            this.charge1Button.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.charge1Button.Image = global::pwiz.Skyline.Properties.Resources.Ions_1;
            this.charge1Button.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.charge1Button.Name = "charge1Button";
            this.charge1Button.Size = new System.Drawing.Size(21, 20);
            this.charge1Button.Text = "toolStripButton7";
            this.charge1Button.ToolTipText = "Charge 1 ions";
            this.charge1Button.Click += new System.EventHandler(this.charge1ContextMenuItem_Click);
            // 
            // charge2Button
            // 
            this.charge2Button.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.charge2Button.Image = global::pwiz.Skyline.Properties.Resources.Ions_2;
            this.charge2Button.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.charge2Button.Name = "charge2Button";
            this.charge2Button.Size = new System.Drawing.Size(21, 20);
            this.charge2Button.Text = "toolStripButton8";
            this.charge2Button.ToolTipText = "Charge 2 ions";
            this.charge2Button.Click += new System.EventHandler(this.charge2ContextMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(21, 6);
            // 
            // copyMetafileButton
            // 
            this.copyMetafileButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.copyMetafileButton.Image = global::pwiz.Skyline.Properties.Resources.Copy;
            this.copyMetafileButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.copyMetafileButton.Name = "copyMetafileButton";
            this.copyMetafileButton.Size = new System.Drawing.Size(21, 20);
            this.copyMetafileButton.Text = "toolStripButton9";
            this.copyMetafileButton.ToolTipText = "Copy Metafile";
            this.copyMetafileButton.Click += new System.EventHandler(this.copyMetafileButton_Click);
            // 
            // btnCopy
            // 
            this.btnCopy.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnCopy.Image = global::pwiz.Skyline.Properties.Resources.Copy_Bitmap;
            this.btnCopy.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnCopy.Name = "btnCopy";
            this.btnCopy.Size = new System.Drawing.Size(21, 20);
            this.btnCopy.Text = "toolStripButton10";
            this.btnCopy.ToolTipText = "Copy Bitmap";
            this.btnCopy.Click += new System.EventHandler(this.btnCopy_Click);
            // 
            // btnSave
            // 
            this.btnSave.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnSave.Image = global::pwiz.Skyline.Properties.Resources.Save;
            this.btnSave.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(21, 20);
            this.btnSave.Text = "toolStripButton11";
            this.btnSave.ToolTipText = "Save";
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // btnPrint
            // 
            this.btnPrint.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnPrint.Image = global::pwiz.Skyline.Properties.Resources.Print;
            this.btnPrint.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnPrint.Name = "btnPrint";
            this.btnPrint.Size = new System.Drawing.Size(21, 20);
            this.btnPrint.Text = "toolStripButton12";
            this.btnPrint.ToolTipText = "Print";
            this.btnPrint.Click += new System.EventHandler(this.btnPrint_Click);
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.cbAssociateProteins);
            this.panel2.Controls.Add(this.labelFilename);
            this.panel2.Controls.Add(this.btnAdd);
            this.panel2.Controls.Add(this.btnAddAll);
            this.panel2.Controls.Add(this.btnCancel);
            this.panel2.Controls.Add(this.labelRT);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel2.Location = new System.Drawing.Point(0, 333);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(477, 56);
            this.panel2.TabIndex = 1;
            // 
            // cbAssociateProteins
            // 
            this.cbAssociateProteins.AutoSize = true;
            this.cbAssociateProteins.Location = new System.Drawing.Point(163, 36);
            this.cbAssociateProteins.Name = "cbAssociateProteins";
            this.cbAssociateProteins.Size = new System.Drawing.Size(112, 17);
            this.cbAssociateProteins.TabIndex = 3;
            this.cbAssociateProteins.Text = "Asso&ciate proteins";
            this.cbAssociateProteins.UseVisualStyleBackColor = true;
            // 
            // labelFilename
            // 
            this.labelFilename.AutoSize = true;
            this.labelFilename.Location = new System.Drawing.Point(3, 6);
            this.labelFilename.Name = "labelFilename";
            this.labelFilename.Size = new System.Drawing.Size(23, 13);
            this.labelFilename.TabIndex = 3;
            this.labelFilename.Text = "File";
            // 
            // btnAdd
            // 
            this.btnAdd.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnAdd.Location = new System.Drawing.Point(0, 33);
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(75, 23);
            this.btnAdd.TabIndex = 2;
            this.btnAdd.Text = "&Add";
            this.btnAdd.UseVisualStyleBackColor = true;
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            // 
            // btnAddAll
            // 
            this.btnAddAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnAddAll.Location = new System.Drawing.Point(81, 33);
            this.btnAddAll.Name = "btnAddAll";
            this.btnAddAll.Size = new System.Drawing.Size(75, 23);
            this.btnAddAll.TabIndex = 1;
            this.btnAddAll.Text = "A&dd All...";
            this.btnAddAll.UseVisualStyleBackColor = true;
            this.btnAddAll.Click += new System.EventHandler(this.btnAddAll_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(402, 33);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 0;
            this.btnCancel.Text = "Close";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // labelRT
            // 
            this.labelRT.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.labelRT.Location = new System.Drawing.Point(72, 3);
            this.labelRT.Name = "labelRT";
            this.labelRT.Size = new System.Drawing.Size(403, 16);
            this.labelRT.TabIndex = 4;
            this.labelRT.Text = "RT";
            this.labelRT.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // ViewLibraryPanel
            // 
            this.ViewLibraryPanel.Controls.Add(this.splitMain);
            this.ViewLibraryPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ViewLibraryPanel.Location = new System.Drawing.Point(10, 25);
            this.ViewLibraryPanel.Name = "ViewLibraryPanel";
            this.ViewLibraryPanel.Size = new System.Drawing.Size(739, 389);
            this.ViewLibraryPanel.TabIndex = 2;
            // 
            // LibraryLabel
            // 
            this.LibraryLabel.AutoSize = true;
            this.LibraryLabel.Location = new System.Drawing.Point(10, 9);
            this.LibraryLabel.Name = "LibraryLabel";
            this.LibraryLabel.Size = new System.Drawing.Size(41, 13);
            this.LibraryLabel.TabIndex = 0;
            this.LibraryLabel.Text = "&Library:";
            // 
            // contextMenuSpectrum
            // 
            this.contextMenuSpectrum.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.aionsContextMenuItem,
            this.bionsContextMenuItem,
            this.cionsContextMenuItem,
            this.xionsContextMenuItem,
            this.yionsContextMenuItem,
            this.zionsContextMenuItem,
            this.precursorIonContextMenuItem,
            this.toolStripSeparator11,
            this.chargesContextMenuItem,
            this.toolStripSeparator12,
            this.ranksContextMenuItem,
            this.ionMzValuesContextMenuItem,
            this.observedMzValuesContextMenuItem,
            this.duplicatesContextMenuItem,
            this.toolStripSeparator13,
            this.lockYaxisContextMenuItem,
            this.toolStripSeparator14,
            this.spectrumPropsContextMenuItem,
            this.toolStripSeparator15,
            this.zoomSpectrumContextMenuItem,
            this.toolStripSeparator27});
            this.contextMenuSpectrum.Name = "contextMenuSpectrum";
            this.contextMenuSpectrum.Size = new System.Drawing.Size(186, 370);
            // 
            // aionsContextMenuItem
            // 
            this.aionsContextMenuItem.CheckOnClick = true;
            this.aionsContextMenuItem.Name = "aionsContextMenuItem";
            this.aionsContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.aionsContextMenuItem.Text = "A-ions";
            this.aionsContextMenuItem.Click += new System.EventHandler(this.aionsContextMenuItem_Click);
            // 
            // bionsContextMenuItem
            // 
            this.bionsContextMenuItem.CheckOnClick = true;
            this.bionsContextMenuItem.Name = "bionsContextMenuItem";
            this.bionsContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.bionsContextMenuItem.Text = "B-ions";
            this.bionsContextMenuItem.Click += new System.EventHandler(this.bionsContextMenuItem_Click);
            // 
            // cionsContextMenuItem
            // 
            this.cionsContextMenuItem.CheckOnClick = true;
            this.cionsContextMenuItem.Name = "cionsContextMenuItem";
            this.cionsContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.cionsContextMenuItem.Text = "C-ions";
            this.cionsContextMenuItem.Click += new System.EventHandler(this.cionsContextMenuItem_Click);
            // 
            // xionsContextMenuItem
            // 
            this.xionsContextMenuItem.CheckOnClick = true;
            this.xionsContextMenuItem.Name = "xionsContextMenuItem";
            this.xionsContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.xionsContextMenuItem.Text = "X-ions";
            this.xionsContextMenuItem.Click += new System.EventHandler(this.xionsContextMenuItem_Click);
            // 
            // yionsContextMenuItem
            // 
            this.yionsContextMenuItem.CheckOnClick = true;
            this.yionsContextMenuItem.Name = "yionsContextMenuItem";
            this.yionsContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.yionsContextMenuItem.Text = "Y-ions";
            this.yionsContextMenuItem.Click += new System.EventHandler(this.yionsContextMenuItem_Click);
            // 
            // zionsContextMenuItem
            // 
            this.zionsContextMenuItem.CheckOnClick = true;
            this.zionsContextMenuItem.Name = "zionsContextMenuItem";
            this.zionsContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.zionsContextMenuItem.Text = "Z-ions";
            this.zionsContextMenuItem.Click += new System.EventHandler(this.zionsContextMenuItem_Click);
            // 
            // precursorIonContextMenuItem
            // 
            this.precursorIonContextMenuItem.Name = "precursorIonContextMenuItem";
            this.precursorIonContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.precursorIonContextMenuItem.Text = "Precursor";
            this.precursorIonContextMenuItem.Click += new System.EventHandler(this.precursorIonContextMenuItem_Click);
            // 
            // toolStripSeparator11
            // 
            this.toolStripSeparator11.Name = "toolStripSeparator11";
            this.toolStripSeparator11.Size = new System.Drawing.Size(182, 6);
            // 
            // chargesContextMenuItem
            // 
            this.chargesContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.charge1ContextMenuItem,
            this.charge2ContextMenuItem,
            this.charge3ContextMenuItem,
            this.charge4ContextMenuItem});
            this.chargesContextMenuItem.Name = "chargesContextMenuItem";
            this.chargesContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.chargesContextMenuItem.Text = "Charges";
            this.chargesContextMenuItem.DropDownOpening += new System.EventHandler(this.chargesMenuItem_DropDownOpening);
            // 
            // charge1ContextMenuItem
            // 
            this.charge1ContextMenuItem.Name = "charge1ContextMenuItem";
            this.charge1ContextMenuItem.Size = new System.Drawing.Size(80, 22);
            this.charge1ContextMenuItem.Text = "1";
            this.charge1ContextMenuItem.Click += new System.EventHandler(this.charge1ContextMenuItem_Click);
            // 
            // charge2ContextMenuItem
            // 
            this.charge2ContextMenuItem.Name = "charge2ContextMenuItem";
            this.charge2ContextMenuItem.Size = new System.Drawing.Size(80, 22);
            this.charge2ContextMenuItem.Text = "2";
            this.charge2ContextMenuItem.Click += new System.EventHandler(this.charge2ContextMenuItem_Click);
            // 
            // charge3ContextMenuItem
            // 
            this.charge3ContextMenuItem.Name = "charge3ContextMenuItem";
            this.charge3ContextMenuItem.Size = new System.Drawing.Size(80, 22);
            this.charge3ContextMenuItem.Text = "3";
            this.charge3ContextMenuItem.Click += new System.EventHandler(this.charge3ContextMenuItem_Click);
            // 
            // charge4ContextMenuItem
            // 
            this.charge4ContextMenuItem.Name = "charge4ContextMenuItem";
            this.charge4ContextMenuItem.Size = new System.Drawing.Size(80, 22);
            this.charge4ContextMenuItem.Text = "4";
            this.charge4ContextMenuItem.Click += new System.EventHandler(this.charge4ContextMenuItem_Click);
            // 
            // toolStripSeparator12
            // 
            this.toolStripSeparator12.Name = "toolStripSeparator12";
            this.toolStripSeparator12.Size = new System.Drawing.Size(182, 6);
            // 
            // ranksContextMenuItem
            // 
            this.ranksContextMenuItem.CheckOnClick = true;
            this.ranksContextMenuItem.Name = "ranksContextMenuItem";
            this.ranksContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.ranksContextMenuItem.Text = "Ranks";
            this.ranksContextMenuItem.Click += new System.EventHandler(this.ranksContextMenuItem_Click);
            // 
            // ionMzValuesContextMenuItem
            // 
            this.ionMzValuesContextMenuItem.CheckOnClick = true;
            this.ionMzValuesContextMenuItem.Name = "ionMzValuesContextMenuItem";
            this.ionMzValuesContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.ionMzValuesContextMenuItem.Text = "Ion m/z Values";
            this.ionMzValuesContextMenuItem.Click += new System.EventHandler(this.ionMzValuesContextMenuItem_Click);
            // 
            // observedMzValuesContextMenuItem
            // 
            this.observedMzValuesContextMenuItem.CheckOnClick = true;
            this.observedMzValuesContextMenuItem.Name = "observedMzValuesContextMenuItem";
            this.observedMzValuesContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.observedMzValuesContextMenuItem.Text = "Observed m/z Values";
            this.observedMzValuesContextMenuItem.Click += new System.EventHandler(this.observedMzValuesContextMenuItem_Click);
            // 
            // duplicatesContextMenuItem
            // 
            this.duplicatesContextMenuItem.CheckOnClick = true;
            this.duplicatesContextMenuItem.Name = "duplicatesContextMenuItem";
            this.duplicatesContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.duplicatesContextMenuItem.Text = "Duplicate Ions";
            this.duplicatesContextMenuItem.Click += new System.EventHandler(this.duplicatesContextMenuItem_Click);
            // 
            // toolStripSeparator13
            // 
            this.toolStripSeparator13.Name = "toolStripSeparator13";
            this.toolStripSeparator13.Size = new System.Drawing.Size(182, 6);
            // 
            // lockYaxisContextMenuItem
            // 
            this.lockYaxisContextMenuItem.CheckOnClick = true;
            this.lockYaxisContextMenuItem.Name = "lockYaxisContextMenuItem";
            this.lockYaxisContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.lockYaxisContextMenuItem.Text = "Auto-scale Y-axis";
            this.lockYaxisContextMenuItem.Click += new System.EventHandler(this.lockYaxisContextMenuItem_Click);
            // 
            // toolStripSeparator14
            // 
            this.toolStripSeparator14.Name = "toolStripSeparator14";
            this.toolStripSeparator14.Size = new System.Drawing.Size(182, 6);
            // 
            // spectrumPropsContextMenuItem
            // 
            this.spectrumPropsContextMenuItem.Name = "spectrumPropsContextMenuItem";
            this.spectrumPropsContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.spectrumPropsContextMenuItem.Text = "Properties...";
            this.spectrumPropsContextMenuItem.Click += new System.EventHandler(this.spectrumPropsContextMenuItem_Click);
            // 
            // toolStripSeparator15
            // 
            this.toolStripSeparator15.Name = "toolStripSeparator15";
            this.toolStripSeparator15.Size = new System.Drawing.Size(182, 6);
            // 
            // zoomSpectrumContextMenuItem
            // 
            this.zoomSpectrumContextMenuItem.Name = "zoomSpectrumContextMenuItem";
            this.zoomSpectrumContextMenuItem.Size = new System.Drawing.Size(185, 22);
            this.zoomSpectrumContextMenuItem.Text = "Zoom Out";
            this.zoomSpectrumContextMenuItem.Click += new System.EventHandler(this.zoomSpectrumContextMenuItem_Click);
            // 
            // toolStripSeparator27
            // 
            this.toolStripSeparator27.Name = "toolStripSeparator27";
            this.toolStripSeparator27.Size = new System.Drawing.Size(182, 6);
            // 
            // ViewLibraryDlg
            // 
            this.AcceptButton = this.btnCancel;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(759, 424);
            this.Controls.Add(this.LibraryLabel);
            this.Controls.Add(this.ViewLibraryPanel);
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ViewLibraryDlg";
            this.Padding = new System.Windows.Forms.Padding(10, 25, 10, 10);
            this.ShowInTaskbar = false;
            this.Text = "Spectral Library Explorer";
            this.Deactivate += new System.EventHandler(this.ViewLibraryDlg_Deactivate);
            this.Load += new System.EventHandler(this.ViewLibraryDlg_Load);
            this.Shown += new System.EventHandler(this.ViewLibraryDlg_Shown);
            this.Activated += new System.EventHandler(this.ViewLibraryDlg_Activated);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ViewLibraryDlg_KeyDown);
            this.splitMain.Panel1.ResumeLayout(false);
            this.splitMain.Panel2.ResumeLayout(false);
            this.splitMain.ResumeLayout(false);
            this.splitPeptideList.Panel1.ResumeLayout(false);
            this.splitPeptideList.Panel2.ResumeLayout(false);
            this.splitPeptideList.Panel2.PerformLayout();
            this.splitPeptideList.ResumeLayout(false);
            this.PeptideListPanel.ResumeLayout(false);
            this.PeptideListPanel.PerformLayout();
            this.PeptideEditPanel.ResumeLayout(false);
            this.PeptideEditPanel.PerformLayout();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.GraphPanel.ResumeLayout(false);
            this.GraphPanel.PerformLayout();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.ViewLibraryPanel.ResumeLayout(false);
            this.contextMenuSpectrum.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitMain;
        private System.Windows.Forms.SplitContainer splitPeptideList;
        private System.Windows.Forms.Panel PeptideListPanel;
        private System.Windows.Forms.ListBox listPeptide;
        private System.Windows.Forms.Label PageCount;
        private System.Windows.Forms.Label PeptideCount;
        private System.Windows.Forms.LinkLabel NextLink;
        private System.Windows.Forms.LinkLabel PreviousLink;
        private System.Windows.Forms.Panel PeptideEditPanel;
        private System.Windows.Forms.TextBox textPeptide;
        private System.Windows.Forms.Panel ViewLibraryPanel;
        private System.Windows.Forms.Label PeptideLabel;
        private System.Windows.Forms.ComboBox comboLibrary;
        private System.Windows.Forms.Label LibraryLabel;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Panel GraphPanel;
        private pwiz.MSGraph.MSGraphControl graphControl;
        private System.Windows.Forms.ContextMenuStrip contextMenuSpectrum;
        private System.Windows.Forms.ToolStripMenuItem aionsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem bionsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem cionsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem xionsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem yionsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem zionsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem precursorIonContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator11;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator12;
        private System.Windows.Forms.ToolStripMenuItem ranksContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem duplicatesContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator13;
        private System.Windows.Forms.ToolStripMenuItem lockYaxisContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator14;
        private System.Windows.Forms.ToolStripMenuItem spectrumPropsContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator15;
        private System.Windows.Forms.ToolStripMenuItem zoomSpectrumContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator27;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton btnAIons;
        private System.Windows.Forms.ToolStripButton btnBIons;
        private System.Windows.Forms.ToolStripButton btnCIons;
        private System.Windows.Forms.ToolStripButton btnXIons;
        private System.Windows.Forms.ToolStripButton btnYIons;
        private System.Windows.Forms.ToolStripButton btnZIons;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripButton charge1Button;
        private System.Windows.Forms.ToolStripButton charge2Button;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripButton copyMetafileButton;
        private System.Windows.Forms.ToolStripButton btnCopy;
        private System.Windows.Forms.ToolStripButton btnSave;
        private System.Windows.Forms.ToolStripButton btnPrint;
        private System.Windows.Forms.CheckBox cbShowModMasses;
        private System.Windows.Forms.ToolStripMenuItem ionMzValuesContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem observedMzValuesContextMenuItem;
        private System.Windows.Forms.Button btnAddAll;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.CheckBox cbAssociateProteins;
        private System.Windows.Forms.ToolStripMenuItem chargesContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem charge1ContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem charge2ContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem charge3ContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem charge4ContextMenuItem;
        private System.Windows.Forms.Label labelFilename;
        private System.Windows.Forms.Label labelRT;
        private System.Windows.Forms.Button btnLibDetails;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;




    }
}
