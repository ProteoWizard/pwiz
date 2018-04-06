namespace AutoQC
{
    partial class MainForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.btnEdit = new System.Windows.Forms.Button();
            this.btnDelete = new System.Windows.Forms.Button();
            this.btnCopy = new System.Windows.Forms.Button();
            this.lblNoConfigs = new System.Windows.Forms.Label();
            this.btnNewConfig = new System.Windows.Forms.Button();
            this.listViewConfigs = new System.Windows.Forms.ListView();
            this.listViewConfigName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.listViewUser = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.listViewCreated = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.listViewStatus = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.labelSavedConfigurations = new System.Windows.Forms.Label();
            this.btnViewLog1 = new System.Windows.Forms.Button();
            this.tabMain = new System.Windows.Forms.TabControl();
            this.tabFront = new System.Windows.Forms.TabPage();
            this.btnImportConfigs = new System.Windows.Forms.Button();
            this.btnExportConfigs = new System.Windows.Forms.Button();
            this.tabLog = new System.Windows.Forms.TabPage();
            this.btnOpenFolder = new System.Windows.Forms.Button();
            this.btnViewLog2 = new System.Windows.Forms.Button();
            this.lblConfigSelect = new System.Windows.Forms.Label();
            this.textBoxLog = new System.Windows.Forms.RichTextBox();
            this.comboConfigs = new System.Windows.Forms.ComboBox();
            this.toolTip_MainForm = new System.Windows.Forms.ToolTip(this.components);
            this.tabMain.SuspendLayout();
            this.tabFront.SuspendLayout();
            this.tabLog.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnEdit
            // 
            this.btnEdit.Location = new System.Drawing.Point(603, 111);
            this.btnEdit.Name = "btnEdit";
            this.btnEdit.Size = new System.Drawing.Size(75, 23);
            this.btnEdit.TabIndex = 2;
            this.btnEdit.Text = "Edit";
            this.btnEdit.UseVisualStyleBackColor = true;
            this.btnEdit.Click += new System.EventHandler(this.btnEdit_Click);
            // 
            // btnDelete
            // 
            this.btnDelete.Location = new System.Drawing.Point(603, 193);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(75, 23);
            this.btnDelete.TabIndex = 3;
            this.btnDelete.Text = "Delete";
            this.btnDelete.UseVisualStyleBackColor = true;
            this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
            // 
            // btnCopy
            // 
            this.btnCopy.Location = new System.Drawing.Point(603, 153);
            this.btnCopy.Name = "btnCopy";
            this.btnCopy.Size = new System.Drawing.Size(75, 23);
            this.btnCopy.TabIndex = 4;
            this.btnCopy.Text = "Copy";
            this.btnCopy.UseVisualStyleBackColor = true;
            this.btnCopy.Click += new System.EventHandler(this.btnCopy_Click);
            // 
            // lblNoConfigs
            // 
            this.lblNoConfigs.AutoSize = true;
            this.lblNoConfigs.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblNoConfigs.ForeColor = System.Drawing.Color.Blue;
            this.lblNoConfigs.Location = new System.Drawing.Point(81, 440);
            this.lblNoConfigs.Name = "lblNoConfigs";
            this.lblNoConfigs.Size = new System.Drawing.Size(539, 16);
            this.lblNoConfigs.TabIndex = 5;
            this.lblNoConfigs.Text = "There are no saved configurations. Click on the button below to create a new conf" +
    "iguration.";
            // 
            // btnNewConfig
            // 
            this.btnNewConfig.Location = new System.Drawing.Point(260, 468);
            this.btnNewConfig.Name = "btnNewConfig";
            this.btnNewConfig.Size = new System.Drawing.Size(202, 23);
            this.btnNewConfig.TabIndex = 6;
            this.btnNewConfig.Text = "Create a new configuration";
            this.btnNewConfig.UseVisualStyleBackColor = true;
            this.btnNewConfig.Click += new System.EventHandler(this.btnNewConfig_Click);
            // 
            // listViewConfigs
            // 
            this.listViewConfigs.CheckBoxes = true;
            this.listViewConfigs.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.listViewConfigName,
            this.listViewUser,
            this.listViewCreated,
            this.listViewStatus});
            this.listViewConfigs.FullRowSelect = true;
            this.listViewConfigs.Location = new System.Drawing.Point(59, 54);
            this.listViewConfigs.MultiSelect = false;
            this.listViewConfigs.Name = "listViewConfigs";
            this.listViewConfigs.Size = new System.Drawing.Size(527, 373);
            this.listViewConfigs.TabIndex = 7;
            this.listViewConfigs.UseCompatibleStateImageBehavior = false;
            this.listViewConfigs.View = System.Windows.Forms.View.Details;
            this.listViewConfigs.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.listViewConfigs_ColumnClick);
            this.listViewConfigs.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.listViewConfigs_ItemCheck);
            this.listViewConfigs.ItemChecked += new System.Windows.Forms.ItemCheckedEventHandler(this.listViewConfigs_ItemChecked);
            this.listViewConfigs.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.listViewConfigs_ItemSelectionChanged);
            // 
            // listViewConfigName
            // 
            this.listViewConfigName.Text = "Configuration";
            this.listViewConfigName.Width = 257;
            // 
            // listViewUser
            // 
            this.listViewUser.Text = "User";
            this.listViewUser.Width = 117;
            // 
            // listViewCreated
            // 
            this.listViewCreated.Text = "Created";
            this.listViewCreated.Width = 80;
            // 
            // listViewStatus
            // 
            this.listViewStatus.Text = "Status";
            this.listViewStatus.Width = 69;
            // 
            // labelSavedConfigurations
            // 
            this.labelSavedConfigurations.AutoSize = true;
            this.labelSavedConfigurations.Location = new System.Drawing.Point(56, 35);
            this.labelSavedConfigurations.Name = "labelSavedConfigurations";
            this.labelSavedConfigurations.Size = new System.Drawing.Size(110, 13);
            this.labelSavedConfigurations.TabIndex = 8;
            this.labelSavedConfigurations.Text = "Saved configurations:";
            // 
            // btnViewLog1
            // 
            this.btnViewLog1.Location = new System.Drawing.Point(603, 286);
            this.btnViewLog1.Name = "btnViewLog1";
            this.btnViewLog1.Size = new System.Drawing.Size(75, 23);
            this.btnViewLog1.TabIndex = 9;
            this.btnViewLog1.Text = "View log";
            this.btnViewLog1.UseVisualStyleBackColor = true;
            this.btnViewLog1.Click += new System.EventHandler(this.btnViewLog1_Click);
            // 
            // tabMain
            // 
            this.tabMain.Controls.Add(this.tabFront);
            this.tabMain.Controls.Add(this.tabLog);
            this.tabMain.Location = new System.Drawing.Point(12, 12);
            this.tabMain.Name = "tabMain";
            this.tabMain.SelectedIndex = 0;
            this.tabMain.Size = new System.Drawing.Size(739, 551);
            this.tabMain.TabIndex = 10;
            // 
            // tabFront
            // 
            this.tabFront.BackColor = System.Drawing.Color.Transparent;
            this.tabFront.Controls.Add(this.btnImportConfigs);
            this.tabFront.Controls.Add(this.btnExportConfigs);
            this.tabFront.Controls.Add(this.listViewConfigs);
            this.tabFront.Controls.Add(this.labelSavedConfigurations);
            this.tabFront.Controls.Add(this.btnViewLog1);
            this.tabFront.Controls.Add(this.lblNoConfigs);
            this.tabFront.Controls.Add(this.btnNewConfig);
            this.tabFront.Controls.Add(this.btnEdit);
            this.tabFront.Controls.Add(this.btnCopy);
            this.tabFront.Controls.Add(this.btnDelete);
            this.tabFront.Location = new System.Drawing.Point(4, 22);
            this.tabFront.Name = "tabFront";
            this.tabFront.Padding = new System.Windows.Forms.Padding(3);
            this.tabFront.Size = new System.Drawing.Size(731, 525);
            this.tabFront.TabIndex = 0;
            this.tabFront.Text = "Configurations";
            // 
            // btnImportConfigs
            // 
            this.btnImportConfigs.Location = new System.Drawing.Point(603, 380);
            this.btnImportConfigs.Name = "btnImportConfigs";
            this.btnImportConfigs.Size = new System.Drawing.Size(75, 23);
            this.btnImportConfigs.TabIndex = 11;
            this.btnImportConfigs.Text = "Import...";
            this.toolTip_MainForm.SetToolTip(this.btnImportConfigs, "Import saved configurations...");
            this.btnImportConfigs.UseVisualStyleBackColor = true;
            this.btnImportConfigs.Click += new System.EventHandler(this.btnImport_Click);
            // 
            // btnExportConfigs
            // 
            this.btnExportConfigs.Location = new System.Drawing.Point(603, 340);
            this.btnExportConfigs.Name = "btnExportConfigs";
            this.btnExportConfigs.Size = new System.Drawing.Size(75, 23);
            this.btnExportConfigs.TabIndex = 10;
            this.btnExportConfigs.Text = "Export...";
            this.toolTip_MainForm.SetToolTip(this.btnExportConfigs, "Export saved configurations...");
            this.btnExportConfigs.UseVisualStyleBackColor = true;
            this.btnExportConfigs.Click += new System.EventHandler(this.btnExport_Click);
            // 
            // tabLog
            // 
            this.tabLog.BackColor = System.Drawing.Color.Transparent;
            this.tabLog.Controls.Add(this.btnOpenFolder);
            this.tabLog.Controls.Add(this.btnViewLog2);
            this.tabLog.Controls.Add(this.lblConfigSelect);
            this.tabLog.Controls.Add(this.textBoxLog);
            this.tabLog.Controls.Add(this.comboConfigs);
            this.tabLog.Location = new System.Drawing.Point(4, 22);
            this.tabLog.Name = "tabLog";
            this.tabLog.Padding = new System.Windows.Forms.Padding(3);
            this.tabLog.Size = new System.Drawing.Size(731, 525);
            this.tabLog.TabIndex = 1;
            this.tabLog.Text = "Log";
            // 
            // btnOpenFolder
            // 
            this.btnOpenFolder.Location = new System.Drawing.Point(627, 19);
            this.btnOpenFolder.Name = "btnOpenFolder";
            this.btnOpenFolder.Size = new System.Drawing.Size(81, 23);
            this.btnOpenFolder.TabIndex = 5;
            this.btnOpenFolder.Text = "Open folder";
            this.btnOpenFolder.UseVisualStyleBackColor = true;
            this.btnOpenFolder.Click += new System.EventHandler(this.btnOpenFolder_Click);
            // 
            // btnViewLog2
            // 
            this.btnViewLog2.Location = new System.Drawing.Point(546, 19);
            this.btnViewLog2.Name = "btnViewLog2";
            this.btnViewLog2.Size = new System.Drawing.Size(75, 23);
            this.btnViewLog2.TabIndex = 4;
            this.btnViewLog2.Text = "View log";
            this.btnViewLog2.UseVisualStyleBackColor = true;
            this.btnViewLog2.Click += new System.EventHandler(this.btnViewLog2_Click);
            // 
            // lblConfigSelect
            // 
            this.lblConfigSelect.AutoSize = true;
            this.lblConfigSelect.Location = new System.Drawing.Point(21, 19);
            this.lblConfigSelect.Name = "lblConfigSelect";
            this.lblConfigSelect.Size = new System.Drawing.Size(72, 13);
            this.lblConfigSelect.TabIndex = 2;
            this.lblConfigSelect.Text = "Configuration:";
            // 
            // textBoxLog
            // 
            this.textBoxLog.Location = new System.Drawing.Point(24, 59);
            this.textBoxLog.Name = "textBoxLog";
            this.textBoxLog.ReadOnly = true;
            this.textBoxLog.Size = new System.Drawing.Size(684, 446);
            this.textBoxLog.TabIndex = 1;
            this.textBoxLog.Text = "";
            // 
            // comboConfigs
            // 
            this.comboConfigs.FormattingEnabled = true;
            this.comboConfigs.Location = new System.Drawing.Point(114, 19);
            this.comboConfigs.Name = "comboConfigs";
            this.comboConfigs.Size = new System.Drawing.Size(414, 21);
            this.comboConfigs.TabIndex = 0;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(763, 575);
            this.Controls.Add(this.tabMain);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.Text = " AutoQC";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.tabMain.ResumeLayout(false);
            this.tabFront.ResumeLayout(false);
            this.tabFront.PerformLayout();
            this.tabLog.ResumeLayout(false);
            this.tabLog.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnEdit;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.Button btnCopy;
        private System.Windows.Forms.Label lblNoConfigs;
        private System.Windows.Forms.Button btnNewConfig;
        private System.Windows.Forms.ListView listViewConfigs;
        private System.Windows.Forms.ColumnHeader listViewConfigName;
        private System.Windows.Forms.ColumnHeader listViewStatus;
        private System.Windows.Forms.ColumnHeader listViewUser;
        private System.Windows.Forms.ColumnHeader listViewCreated;
        private System.Windows.Forms.Label labelSavedConfigurations;
        private System.Windows.Forms.Button btnViewLog1;
        private System.Windows.Forms.TabControl tabMain;
        private System.Windows.Forms.TabPage tabFront;
        private System.Windows.Forms.TabPage tabLog;
        private System.Windows.Forms.Label lblConfigSelect;
        private System.Windows.Forms.RichTextBox textBoxLog;
        private System.Windows.Forms.ComboBox comboConfigs;
        private System.Windows.Forms.Button btnViewLog2;
        private System.Windows.Forms.Button btnOpenFolder;
        private System.Windows.Forms.Button btnImportConfigs;
        private System.Windows.Forms.Button btnExportConfigs;
        private System.Windows.Forms.ToolTip toolTip_MainForm;

    }
}