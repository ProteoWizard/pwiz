namespace BumberDash
{
    partial class QueueForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(QueueForm));
            this.JobQueueDGV = new System.Windows.Forms.DataGridView();
            this.JQRowMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.lockToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.cloneToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.openOutputFolderToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.runToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.iDPickerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.confiToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.setIDPickerLocationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.newJobToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.documentationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rowStatusLabel = new System.Windows.Forms.Label();
            this.pauseButton = new System.Windows.Forms.Button();
            this.LogButton = new System.Windows.Forms.Button();
            this.TrayIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.TrayMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.showToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.dataGridViewProgressColumn1 = new MSDNSample.DataGridViewProgressColumn();
            this.dataGridViewProgressColumn2 = new MSDNSample.DataGridViewProgressColumn();
            this.dataGridViewProgressColumn3 = new MSDNSample.DataGridViewProgressColumn();
            this.dataGridViewImageColumn1 = new System.Windows.Forms.DataGridViewImageColumn();
            this.JQName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.JQOutputDirectory = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.JQDatabaseFile = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.JQConfigFile = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.JQScanType = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.JQProgress = new MSDNSample.DataGridViewProgressColumn();
            this.Kill = new System.Windows.Forms.DataGridViewButtonColumn();
            ((System.ComponentModel.ISupportInitialize)(this.JobQueueDGV)).BeginInit();
            this.JQRowMenu.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            this.TrayMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // JobQueueDGV
            // 
            this.JobQueueDGV.AllowDrop = true;
            this.JobQueueDGV.AllowUserToAddRows = false;
            this.JobQueueDGV.AllowUserToDeleteRows = false;
            this.JobQueueDGV.AllowUserToResizeRows = false;
            this.JobQueueDGV.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.JobQueueDGV.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.JobQueueDGV.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.JQName,
            this.JQOutputDirectory,
            this.JQDatabaseFile,
            this.JQConfigFile,
            this.JQScanType,
            this.JQProgress,
            this.Kill});
            this.JobQueueDGV.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.JobQueueDGV.Location = new System.Drawing.Point(12, 27);
            this.JobQueueDGV.MultiSelect = false;
            this.JobQueueDGV.Name = "JobQueueDGV";
            this.JobQueueDGV.RowHeadersVisible = false;
            this.JobQueueDGV.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.JobQueueDGV.Size = new System.Drawing.Size(718, 288);
            this.JobQueueDGV.TabIndex = 0;
            this.JobQueueDGV.MouseDown += new System.Windows.Forms.MouseEventHandler(this.JobQueueDGV_MouseDown);
            this.JobQueueDGV.CellBeginEdit += new System.Windows.Forms.DataGridViewCellCancelEventHandler(this.JobQueueDGV_CellBeginEdit);
            this.JobQueueDGV.MouseMove += new System.Windows.Forms.MouseEventHandler(this.JobQueueDGV_MouseMove);
            this.JobQueueDGV.DoubleClick += new System.EventHandler(this.JobQueueDGV_DoubleClick);
            this.JobQueueDGV.DragOver += new System.Windows.Forms.DragEventHandler(this.JobQueueDGV_DragOver);
            this.JobQueueDGV.MouseUp += new System.Windows.Forms.MouseEventHandler(this.JobQueueDGV_MouseUp);
            this.JobQueueDGV.CellPainting += new System.Windows.Forms.DataGridViewCellPaintingEventHandler(this.JobQueueDGV_CellPainting);
            this.JobQueueDGV.SelectionChanged += new System.EventHandler(this.JobQueueDGV_SelectionChanged);
            this.JobQueueDGV.Click += new System.EventHandler(this.JobQueueDGV_Click);
            this.JobQueueDGV.DragDrop += new System.Windows.Forms.DragEventHandler(this.JobQueueDGV_DragDrop);
            this.JobQueueDGV.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.JobQueueDGV_CellContentClick);
            // 
            // JQRowMenu
            // 
            this.JQRowMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lockToolStripMenuItem,
            this.editToolStripMenuItem,
            this.cloneToolStripMenuItem,
            this.deleteToolStripMenuItem,
            this.toolStripSeparator2,
            this.openOutputFolderToolStripMenuItem});
            this.JQRowMenu.Name = "contextMenuStrip1";
            this.JQRowMenu.Size = new System.Drawing.Size(182, 120);
            this.JQRowMenu.Closed += new System.Windows.Forms.ToolStripDropDownClosedEventHandler(this.JQRowMenu_Closed);
            // 
            // lockToolStripMenuItem
            // 
            this.lockToolStripMenuItem.Name = "lockToolStripMenuItem";
            this.lockToolStripMenuItem.Size = new System.Drawing.Size(181, 22);
            this.lockToolStripMenuItem.Text = "Lock";
            this.lockToolStripMenuItem.Click += new System.EventHandler(this.lockToolStripMenuItem_Click);
            // 
            // editToolStripMenuItem
            // 
            this.editToolStripMenuItem.Name = "editToolStripMenuItem";
            this.editToolStripMenuItem.Size = new System.Drawing.Size(181, 22);
            this.editToolStripMenuItem.Text = "Edit...";
            this.editToolStripMenuItem.Click += new System.EventHandler(this.editToolStripMenuItem_Click);
            // 
            // cloneToolStripMenuItem
            // 
            this.cloneToolStripMenuItem.Name = "cloneToolStripMenuItem";
            this.cloneToolStripMenuItem.Size = new System.Drawing.Size(181, 22);
            this.cloneToolStripMenuItem.Text = "Clone...";
            this.cloneToolStripMenuItem.Click += new System.EventHandler(this.cloneToolStripMenuItem_Click);
            // 
            // deleteToolStripMenuItem
            // 
            this.deleteToolStripMenuItem.Name = "deleteToolStripMenuItem";
            this.deleteToolStripMenuItem.Size = new System.Drawing.Size(181, 22);
            this.deleteToolStripMenuItem.Text = "Delete";
            this.deleteToolStripMenuItem.Click += new System.EventHandler(this.deleteToolStripMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(178, 6);
            // 
            // openOutputFolderToolStripMenuItem
            // 
            this.openOutputFolderToolStripMenuItem.Name = "openOutputFolderToolStripMenuItem";
            this.openOutputFolderToolStripMenuItem.Size = new System.Drawing.Size(181, 22);
            this.openOutputFolderToolStripMenuItem.Text = "Open Output Folder";
            this.openOutputFolderToolStripMenuItem.Click += new System.EventHandler(this.openOutputFolderToolStripMenuItem_Click);
            // 
            // menuStrip1
            // 
            this.menuStrip1.BackColor = System.Drawing.SystemColors.Control;
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.helpToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(742, 24);
            this.menuStrip1.TabIndex = 1;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.runToolStripMenuItem,
            this.newJobToolStripMenuItem,
            this.toolStripSeparator1,
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(35, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // runToolStripMenuItem
            // 
            this.runToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.iDPickerToolStripMenuItem,
            this.confiToolStripMenuItem,
            this.toolStripSeparator3,
            this.setIDPickerLocationToolStripMenuItem});
            this.runToolStripMenuItem.Name = "runToolStripMenuItem";
            this.runToolStripMenuItem.Size = new System.Drawing.Size(126, 22);
            this.runToolStripMenuItem.Text = "Run";
            // 
            // iDPickerToolStripMenuItem
            // 
            this.iDPickerToolStripMenuItem.Name = "iDPickerToolStripMenuItem";
            this.iDPickerToolStripMenuItem.Size = new System.Drawing.Size(189, 22);
            this.iDPickerToolStripMenuItem.Text = "ID Picker";
            this.iDPickerToolStripMenuItem.Click += new System.EventHandler(this.iDPickerToolStripMenuItem_Click);
            // 
            // confiToolStripMenuItem
            // 
            this.confiToolStripMenuItem.Name = "confiToolStripMenuItem";
            this.confiToolStripMenuItem.Size = new System.Drawing.Size(189, 22);
            this.confiToolStripMenuItem.Text = "Config Editor - Full";
            this.confiToolStripMenuItem.Click += new System.EventHandler(this.confiToolStripMenuItem_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(186, 6);
            // 
            // setIDPickerLocationToolStripMenuItem
            // 
            this.setIDPickerLocationToolStripMenuItem.Name = "setIDPickerLocationToolStripMenuItem";
            this.setIDPickerLocationToolStripMenuItem.Size = new System.Drawing.Size(189, 22);
            this.setIDPickerLocationToolStripMenuItem.Text = "Set ID Picker Location";
            this.setIDPickerLocationToolStripMenuItem.Click += new System.EventHandler(this.setIDPickerLocationToolStripMenuItem_Click);
            // 
            // newJobToolStripMenuItem
            // 
            this.newJobToolStripMenuItem.Name = "newJobToolStripMenuItem";
            this.newJobToolStripMenuItem.Size = new System.Drawing.Size(126, 22);
            this.newJobToolStripMenuItem.Text = "New Job";
            this.newJobToolStripMenuItem.Click += new System.EventHandler(this.newJobToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(123, 6);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(126, 22);
            this.exitToolStripMenuItem.Text = "Exit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.documentationToolStripMenuItem,
            this.toolStripSeparator4,
            this.aboutToolStripMenuItem});
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            this.helpToolStripMenuItem.Size = new System.Drawing.Size(40, 20);
            this.helpToolStripMenuItem.Text = "Help";
            // 
            // documentationToolStripMenuItem
            // 
            this.documentationToolStripMenuItem.Name = "documentationToolStripMenuItem";
            this.documentationToolStripMenuItem.Size = new System.Drawing.Size(157, 22);
            this.documentationToolStripMenuItem.Text = "Documentation";
            this.documentationToolStripMenuItem.Click += new System.EventHandler(this.documentationToolStripMenuItem_Click);
            // 
            // toolStripSeparator4
            // 
            this.toolStripSeparator4.Name = "toolStripSeparator4";
            this.toolStripSeparator4.Size = new System.Drawing.Size(154, 6);
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(157, 22);
            this.aboutToolStripMenuItem.Text = "About";
            this.aboutToolStripMenuItem.Click += new System.EventHandler(this.aboutToolStripMenuItem_Click);
            // 
            // rowStatusLabel
            // 
            this.rowStatusLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.rowStatusLabel.BackColor = System.Drawing.SystemColors.Control;
            this.rowStatusLabel.Location = new System.Drawing.Point(436, 4);
            this.rowStatusLabel.Name = "rowStatusLabel";
            this.rowStatusLabel.Size = new System.Drawing.Size(294, 20);
            this.rowStatusLabel.TabIndex = 2;
            this.rowStatusLabel.Text = "label1";
            this.rowStatusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // pauseButton
            // 
            this.pauseButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.pauseButton.Location = new System.Drawing.Point(655, 321);
            this.pauseButton.Name = "pauseButton";
            this.pauseButton.Size = new System.Drawing.Size(75, 23);
            this.pauseButton.TabIndex = 3;
            this.pauseButton.Text = "Pause";
            this.pauseButton.UseVisualStyleBackColor = true;
            this.pauseButton.Click += new System.EventHandler(this.pauseButton_Click);
            // 
            // LogButton
            // 
            this.LogButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.LogButton.Location = new System.Drawing.Point(12, 321);
            this.LogButton.Name = "LogButton";
            this.LogButton.Size = new System.Drawing.Size(75, 23);
            this.LogButton.TabIndex = 4;
            this.LogButton.Text = "Log";
            this.LogButton.UseVisualStyleBackColor = true;
            this.LogButton.Click += new System.EventHandler(this.LogButton_Click);
            // 
            // TrayIcon
            // 
            this.TrayIcon.BalloonTipText = "Double-Click this icon to show BumberDash again";
            this.TrayIcon.ContextMenuStrip = this.TrayMenu;
            this.TrayIcon.Icon = ((System.Drawing.Icon)(resources.GetObject("TrayIcon.Icon")));
            this.TrayIcon.Text = "BumberDash";
            this.TrayIcon.DoubleClick += new System.EventHandler(this.TrayIcon_DoubleClick);
            // 
            // TrayMenu
            // 
            this.TrayMenu.BackColor = System.Drawing.SystemColors.ButtonHighlight;
            this.TrayMenu.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.TrayMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.showToolStripMenuItem});
            this.TrayMenu.Name = "TrayMenu";
            this.TrayMenu.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.TrayMenu.Size = new System.Drawing.Size(116, 26);
            // 
            // showToolStripMenuItem
            // 
            this.showToolStripMenuItem.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Bold);
            this.showToolStripMenuItem.Name = "showToolStripMenuItem";
            this.showToolStripMenuItem.Size = new System.Drawing.Size(115, 22);
            this.showToolStripMenuItem.Text = "Show";
            this.showToolStripMenuItem.Click += new System.EventHandler(this.showToolStripMenuItem_Click);
            // 
            // dataGridViewProgressColumn1
            // 
            this.dataGridViewProgressColumn1.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.dataGridViewProgressColumn1.HeaderText = "Progress";
            this.dataGridViewProgressColumn1.MinimumWidth = 50;
            this.dataGridViewProgressColumn1.Name = "dataGridViewProgressColumn1";
            this.dataGridViewProgressColumn1.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            // 
            // dataGridViewProgressColumn2
            // 
            this.dataGridViewProgressColumn2.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.dataGridViewProgressColumn2.HeaderText = "Progress";
            this.dataGridViewProgressColumn2.MinimumWidth = 50;
            this.dataGridViewProgressColumn2.Name = "dataGridViewProgressColumn2";
            this.dataGridViewProgressColumn2.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            // 
            // dataGridViewProgressColumn3
            // 
            this.dataGridViewProgressColumn3.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.dataGridViewProgressColumn3.HeaderText = "Progress";
            this.dataGridViewProgressColumn3.MinimumWidth = 50;
            this.dataGridViewProgressColumn3.Name = "dataGridViewProgressColumn3";
            this.dataGridViewProgressColumn3.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            // 
            // dataGridViewImageColumn1
            // 
            this.dataGridViewImageColumn1.HeaderText = "Progress";
            this.dataGridViewImageColumn1.Name = "dataGridViewImageColumn1";
            this.dataGridViewImageColumn1.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridViewImageColumn1.Width = 110;
            // 
            // JQName
            // 
            this.JQName.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.JQName.HeaderText = "Name";
            this.JQName.MinimumWidth = 45;
            this.JQName.Name = "JQName";
            this.JQName.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            // 
            // JQOutputDirectory
            // 
            this.JQOutputDirectory.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.JQOutputDirectory.HeaderText = "Output Directory";
            this.JQOutputDirectory.MinimumWidth = 110;
            this.JQOutputDirectory.Name = "JQOutputDirectory";
            this.JQOutputDirectory.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            // 
            // JQDatabaseFile
            // 
            this.JQDatabaseFile.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.JQDatabaseFile.HeaderText = "Database File";
            this.JQDatabaseFile.MinimumWidth = 95;
            this.JQDatabaseFile.Name = "JQDatabaseFile";
            this.JQDatabaseFile.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            // 
            // JQConfigFile
            // 
            this.JQConfigFile.HeaderText = "Config File";
            this.JQConfigFile.MinimumWidth = 80;
            this.JQConfigFile.Name = "JQConfigFile";
            this.JQConfigFile.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            this.JQConfigFile.Width = 115;
            // 
            // JQScanType
            // 
            this.JQScanType.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.JQScanType.HeaderText = "Search Type";
            this.JQScanType.Items.AddRange(new object[] {
            "Database Search",
            "Sequence Tagging"});
            this.JQScanType.MinimumWidth = 75;
            this.JQScanType.Name = "JQScanType";
            // 
            // JQProgress
            // 
            this.JQProgress.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.JQProgress.HeaderText = "Progress";
            this.JQProgress.MinimumWidth = 50;
            this.JQProgress.Name = "JQProgress";
            this.JQProgress.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            // 
            // Kill
            // 
            this.Kill.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.Kill.HeaderText = "";
            this.Kill.MinimumWidth = 15;
            this.Kill.Name = "Kill";
            this.Kill.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.Kill.Width = 25;
            // 
            // QueueForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.ControlLight;
            this.ClientSize = new System.Drawing.Size(742, 356);
            this.Controls.Add(this.LogButton);
            this.Controls.Add(this.pauseButton);
            this.Controls.Add(this.JobQueueDGV);
            this.Controls.Add(this.rowStatusLabel);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "QueueForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "BumberDash";
            this.Load += new System.EventHandler(this.QueueForm_Load);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.QueueForm_FormClosing);
            this.Resize += new System.EventHandler(this.QueueForm_Resize);
            ((System.ComponentModel.ISupportInitialize)(this.JobQueueDGV)).EndInit();
            this.JQRowMenu.ResumeLayout(false);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.TrayMenu.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.DataGridViewImageColumn dataGridViewImageColumn1;
        private MSDNSample.DataGridViewProgressColumn dataGridViewProgressColumn1;
        private System.Windows.Forms.ContextMenuStrip JQRowMenu;
        private System.Windows.Forms.ToolStripMenuItem cloneToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem lockToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem deleteToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openOutputFolderToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem newJobToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.Label rowStatusLabel;
        private System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        internal System.Windows.Forms.DataGridView JobQueueDGV;
        private System.Windows.Forms.Button pauseButton;
        private System.Windows.Forms.ToolStripMenuItem runToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem confiToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem iDPickerToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripMenuItem setIDPickerLocationToolStripMenuItem;
        private System.Windows.Forms.Button LogButton;
        internal System.Windows.Forms.NotifyIcon TrayIcon;
        private System.Windows.Forms.ContextMenuStrip TrayMenu;
        private System.Windows.Forms.ToolStripMenuItem showToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem documentationToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
        private MSDNSample.DataGridViewProgressColumn dataGridViewProgressColumn2;
        private MSDNSample.DataGridViewProgressColumn dataGridViewProgressColumn3;
        private System.Windows.Forms.DataGridViewTextBoxColumn JQName;
        private System.Windows.Forms.DataGridViewTextBoxColumn JQOutputDirectory;
        private System.Windows.Forms.DataGridViewTextBoxColumn JQDatabaseFile;
        private System.Windows.Forms.DataGridViewTextBoxColumn JQConfigFile;
        private System.Windows.Forms.DataGridViewComboBoxColumn JQScanType;
        private MSDNSample.DataGridViewProgressColumn JQProgress;
        private System.Windows.Forms.DataGridViewButtonColumn Kill;
    }
}