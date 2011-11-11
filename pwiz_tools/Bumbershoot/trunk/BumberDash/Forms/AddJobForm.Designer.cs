namespace BumberDash.Forms
{
    sealed partial class AddJobForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AddJobForm));
            this.FolderPanel = new System.Windows.Forms.Panel();
            this.SearchTypeBox = new System.Windows.Forms.ComboBox();
            this.newFolderBox = new System.Windows.Forms.CheckBox();
            this.IntermediateBox = new System.Windows.Forms.CheckBox();
            this.CPUsAutoLabel = new System.Windows.Forms.Label();
            this.CPUsBox = new System.Windows.Forms.NumericUpDown();
            this.label3 = new System.Windows.Forms.Label();
            this.NameBox = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.DatabaseLocBox = new System.Windows.Forms.ComboBox();
            this.OutputDirectoryBox = new System.Windows.Forms.ComboBox();
            this.InputFilesBox = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.DataFilesButton = new System.Windows.Forms.Button();
            this.AddJobRunButton = new System.Windows.Forms.Button();
            this.AddJobCancelButton = new System.Windows.Forms.Button();
            this.label7 = new System.Windows.Forms.Label();
            this.DatabaseLocButton = new System.Windows.Forms.Button();
            this.label5 = new System.Windows.Forms.Label();
            this.InitialDirectoryButton = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.PepPanel = new System.Windows.Forms.Panel();
            this.SpecLibBox = new System.Windows.Forms.ComboBox();
            this.SpecLibBrowse = new System.Windows.Forms.Button();
            this.label6 = new System.Windows.Forms.Label();
            this.PepConfigGB = new System.Windows.Forms.GroupBox();
            this.PepConfigBox = new System.Windows.Forms.ComboBox();
            this.PepEditButton = new System.Windows.Forms.Button();
            this.PepConfigBrowse = new System.Windows.Forms.Button();
            this.label8 = new System.Windows.Forms.Label();
            this.ConfigGB = new System.Windows.Forms.GroupBox();
            this.ConfigDatabasePanel = new System.Windows.Forms.Panel();
            this.MyriConfigBox = new System.Windows.Forms.ComboBox();
            this.MyriEditButton = new System.Windows.Forms.Button();
            this.MyriConfigButton = new System.Windows.Forms.Button();
            this.label13 = new System.Windows.Forms.Label();
            this.ConfigTagPanel = new System.Windows.Forms.Panel();
            this.DTConfigBox = new System.Windows.Forms.ComboBox();
            this.TRConfigBox = new System.Windows.Forms.ComboBox();
            this.TREditButton = new System.Windows.Forms.Button();
            this.TRConfigButton = new System.Windows.Forms.Button();
            this.label12 = new System.Windows.Forms.Label();
            this.DTEditButton = new System.Windows.Forms.Button();
            this.DTConfigButton = new System.Windows.Forms.Button();
            this.label11 = new System.Windows.Forms.Label();
            this.TagReconInfoBox = new System.Windows.Forms.TextBox();
            this.TagReconInfoLabel = new System.Windows.Forms.Label();
            this.DirecTagInfoLabel = new System.Windows.Forms.Label();
            this.DirecTagInfoBox = new System.Windows.Forms.TextBox();
            this.TagConfigInfoPanel = new System.Windows.Forms.Panel();
            this.DatabaseConfigInfoPanel = new System.Windows.Forms.Panel();
            this.MyriMatchInfoLabel = new System.Windows.Forms.Label();
            this.MyriMatchInfoBox = new System.Windows.Forms.TextBox();
            this.PepConfigInfoPanel = new System.Windows.Forms.Panel();
            this.PepInfoLabel = new System.Windows.Forms.Label();
            this.PepitomeInfoBox = new System.Windows.Forms.TextBox();
            this.FolderPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.CPUsBox)).BeginInit();
            this.PepPanel.SuspendLayout();
            this.PepConfigGB.SuspendLayout();
            this.ConfigGB.SuspendLayout();
            this.ConfigDatabasePanel.SuspendLayout();
            this.ConfigTagPanel.SuspendLayout();
            this.TagConfigInfoPanel.SuspendLayout();
            this.DatabaseConfigInfoPanel.SuspendLayout();
            this.PepConfigInfoPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // FolderPanel
            // 
            this.FolderPanel.Controls.Add(this.SearchTypeBox);
            this.FolderPanel.Controls.Add(this.newFolderBox);
            this.FolderPanel.Controls.Add(this.IntermediateBox);
            this.FolderPanel.Controls.Add(this.CPUsAutoLabel);
            this.FolderPanel.Controls.Add(this.CPUsBox);
            this.FolderPanel.Controls.Add(this.label3);
            this.FolderPanel.Controls.Add(this.NameBox);
            this.FolderPanel.Controls.Add(this.label1);
            this.FolderPanel.Controls.Add(this.DatabaseLocBox);
            this.FolderPanel.Controls.Add(this.OutputDirectoryBox);
            this.FolderPanel.Controls.Add(this.InputFilesBox);
            this.FolderPanel.Controls.Add(this.label2);
            this.FolderPanel.Controls.Add(this.DataFilesButton);
            this.FolderPanel.Controls.Add(this.AddJobRunButton);
            this.FolderPanel.Controls.Add(this.AddJobCancelButton);
            this.FolderPanel.Controls.Add(this.label7);
            this.FolderPanel.Controls.Add(this.DatabaseLocButton);
            this.FolderPanel.Controls.Add(this.label5);
            this.FolderPanel.Controls.Add(this.InitialDirectoryButton);
            this.FolderPanel.Controls.Add(this.label4);
            this.FolderPanel.Controls.Add(this.PepPanel);
            this.FolderPanel.Controls.Add(this.ConfigGB);
            this.FolderPanel.Location = new System.Drawing.Point(0, 0);
            this.FolderPanel.Name = "FolderPanel";
            this.FolderPanel.Size = new System.Drawing.Size(435, 415);
            this.FolderPanel.TabIndex = 3;
            // 
            // SearchTypeBox
            // 
            this.SearchTypeBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.SearchTypeBox.FormattingEnabled = true;
            this.SearchTypeBox.Items.AddRange(new object[] {
            "Myrimatch- Database Searching",
            "DirecTag / TagRecon- Sequence Tagging",
            "Pepitome- Spectral Library"});
            this.SearchTypeBox.Location = new System.Drawing.Point(167, 45);
            this.SearchTypeBox.Name = "SearchTypeBox";
            this.SearchTypeBox.Size = new System.Drawing.Size(211, 21);
            this.SearchTypeBox.TabIndex = 35;
            this.SearchTypeBox.SelectedIndexChanged += new System.EventHandler(this.SearchTypeBox_SelectedIndexChanged);
            // 
            // newFolderBox
            // 
            this.newFolderBox.AutoSize = true;
            this.newFolderBox.Location = new System.Drawing.Point(199, 105);
            this.newFolderBox.Name = "newFolderBox";
            this.newFolderBox.Size = new System.Drawing.Size(196, 17);
            this.newFolderBox.TabIndex = 34;
            this.newFolderBox.Text = "Create new folder in output directory";
            this.newFolderBox.UseVisualStyleBackColor = true;
            // 
            // IntermediateBox
            // 
            this.IntermediateBox.AutoSize = true;
            this.IntermediateBox.Location = new System.Drawing.Point(12, 380);
            this.IntermediateBox.Name = "IntermediateBox";
            this.IntermediateBox.Size = new System.Drawing.Size(107, 17);
            this.IntermediateBox.TabIndex = 32;
            this.IntermediateBox.Text = "Create mzML File";
            this.IntermediateBox.UseVisualStyleBackColor = true;
            this.IntermediateBox.Visible = false;
            // 
            // CPUsAutoLabel
            // 
            this.CPUsAutoLabel.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.CPUsAutoLabel.Location = new System.Drawing.Point(362, 381);
            this.CPUsAutoLabel.Name = "CPUsAutoLabel";
            this.CPUsAutoLabel.Size = new System.Drawing.Size(31, 15);
            this.CPUsAutoLabel.TabIndex = 31;
            this.CPUsAutoLabel.Text = "Auto";
            // 
            // CPUsBox
            // 
            this.CPUsBox.Location = new System.Drawing.Point(361, 379);
            this.CPUsBox.Name = "CPUsBox";
            this.CPUsBox.Size = new System.Drawing.Size(49, 20);
            this.CPUsBox.TabIndex = 30;
            this.CPUsBox.ValueChanged += new System.EventHandler(this.CPUsBox_ValueChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(321, 381);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(34, 13);
            this.label3.TabIndex = 29;
            this.label3.Text = "CPUs";
            // 
            // NameBox
            // 
            this.NameBox.Location = new System.Drawing.Point(28, 103);
            this.NameBox.Name = "NameBox";
            this.NameBox.Size = new System.Drawing.Size(165, 20);
            this.NameBox.TabIndex = 28;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Book Antiqua", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(25, 82);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(50, 18);
            this.label1.TabIndex = 27;
            this.label1.Text = "Name:";
            // 
            // DatabaseLocBox
            // 
            this.DatabaseLocBox.FormattingEnabled = true;
            this.DatabaseLocBox.Location = new System.Drawing.Point(28, 235);
            this.DatabaseLocBox.Name = "DatabaseLocBox";
            this.DatabaseLocBox.Size = new System.Drawing.Size(306, 21);
            this.DatabaseLocBox.TabIndex = 25;
            // 
            // OutputDirectoryBox
            // 
            this.OutputDirectoryBox.FormattingEnabled = true;
            this.OutputDirectoryBox.Location = new System.Drawing.Point(28, 191);
            this.OutputDirectoryBox.Name = "OutputDirectoryBox";
            this.OutputDirectoryBox.Size = new System.Drawing.Size(306, 21);
            this.OutputDirectoryBox.TabIndex = 24;
            // 
            // InputFilesBox
            // 
            this.InputFilesBox.FormattingEnabled = true;
            this.InputFilesBox.Location = new System.Drawing.Point(28, 147);
            this.InputFilesBox.Name = "InputFilesBox";
            this.InputFilesBox.Size = new System.Drawing.Size(306, 21);
            this.InputFilesBox.TabIndex = 23;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Book Antiqua", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(56, 46);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(105, 18);
            this.label2.TabIndex = 1;
            this.label2.Text = "Type of search:";
            // 
            // DataFilesButton
            // 
            this.DataFilesButton.Location = new System.Drawing.Point(340, 146);
            this.DataFilesButton.Name = "DataFilesButton";
            this.DataFilesButton.Size = new System.Drawing.Size(55, 21);
            this.DataFilesButton.TabIndex = 7;
            this.DataFilesButton.Text = "Browse";
            this.DataFilesButton.UseVisualStyleBackColor = true;
            this.DataFilesButton.Click += new System.EventHandler(this.DataFilesButton_Click);
            // 
            // AddJobRunButton
            // 
            this.AddJobRunButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.AddJobRunButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.AddJobRunButton.Location = new System.Drawing.Point(215, 385);
            this.AddJobRunButton.Name = "AddJobRunButton";
            this.AddJobRunButton.Size = new System.Drawing.Size(75, 23);
            this.AddJobRunButton.TabIndex = 18;
            this.AddJobRunButton.Text = "Add";
            this.AddJobRunButton.UseVisualStyleBackColor = true;
            this.AddJobRunButton.Click += new System.EventHandler(this.AddJobRunButton_Click);
            // 
            // AddJobCancelButton
            // 
            this.AddJobCancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.AddJobCancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.AddJobCancelButton.Location = new System.Drawing.Point(134, 385);
            this.AddJobCancelButton.Name = "AddJobCancelButton";
            this.AddJobCancelButton.Size = new System.Drawing.Size(75, 23);
            this.AddJobCancelButton.TabIndex = 22;
            this.AddJobCancelButton.Text = "Cancel";
            this.AddJobCancelButton.UseVisualStyleBackColor = true;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Font = new System.Drawing.Font("Book Antiqua", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label7.Location = new System.Drawing.Point(25, 126);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(79, 18);
            this.label7.TabIndex = 5;
            this.label7.Text = "Input Files:";
            // 
            // DatabaseLocButton
            // 
            this.DatabaseLocButton.Location = new System.Drawing.Point(340, 234);
            this.DatabaseLocButton.Name = "DatabaseLocButton";
            this.DatabaseLocButton.Size = new System.Drawing.Size(55, 21);
            this.DatabaseLocButton.TabIndex = 8;
            this.DatabaseLocButton.Text = "Browse";
            this.DatabaseLocButton.UseVisualStyleBackColor = true;
            this.DatabaseLocButton.Click += new System.EventHandler(this.DatabaseLocButton_Click);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Book Antiqua", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(25, 215);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(120, 18);
            this.label5.TabIndex = 6;
            this.label5.Text = "FASTA Database:";
            // 
            // InitialDirectoryButton
            // 
            this.InitialDirectoryButton.Location = new System.Drawing.Point(340, 191);
            this.InitialDirectoryButton.Name = "InitialDirectoryButton";
            this.InitialDirectoryButton.Size = new System.Drawing.Size(55, 21);
            this.InitialDirectoryButton.TabIndex = 4;
            this.InitialDirectoryButton.Text = "Browse";
            this.InitialDirectoryButton.UseVisualStyleBackColor = true;
            this.InitialDirectoryButton.Click += new System.EventHandler(this.InitialDirectoryButton_Click);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Book Antiqua", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(25, 171);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(122, 18);
            this.label4.TabIndex = 2;
            this.label4.Text = "Output Directory:";
            // 
            // PepPanel
            // 
            this.PepPanel.Controls.Add(this.SpecLibBox);
            this.PepPanel.Controls.Add(this.SpecLibBrowse);
            this.PepPanel.Controls.Add(this.label6);
            this.PepPanel.Controls.Add(this.PepConfigGB);
            this.PepPanel.Location = new System.Drawing.Point(9, 261);
            this.PepPanel.Name = "PepPanel";
            this.PepPanel.Size = new System.Drawing.Size(405, 118);
            this.PepPanel.TabIndex = 36;
            this.PepPanel.Visible = false;
            // 
            // SpecLibBox
            // 
            this.SpecLibBox.FormattingEnabled = true;
            this.SpecLibBox.Location = new System.Drawing.Point(19, 21);
            this.SpecLibBox.Name = "SpecLibBox";
            this.SpecLibBox.Size = new System.Drawing.Size(306, 21);
            this.SpecLibBox.TabIndex = 30;
            // 
            // SpecLibBrowse
            // 
            this.SpecLibBrowse.Location = new System.Drawing.Point(331, 20);
            this.SpecLibBrowse.Name = "SpecLibBrowse";
            this.SpecLibBrowse.Size = new System.Drawing.Size(55, 21);
            this.SpecLibBrowse.TabIndex = 29;
            this.SpecLibBrowse.Text = "Browse";
            this.SpecLibBrowse.UseVisualStyleBackColor = true;
            this.SpecLibBrowse.Click += new System.EventHandler(this.SpecLibBrowse_Click);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Book Antiqua", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.Location = new System.Drawing.Point(16, 1);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(114, 18);
            this.label6.TabIndex = 28;
            this.label6.Text = "Spectral Library:";
            // 
            // PepConfigGB
            // 
            this.PepConfigGB.Controls.Add(this.PepConfigBox);
            this.PepConfigGB.Controls.Add(this.PepEditButton);
            this.PepConfigGB.Controls.Add(this.PepConfigBrowse);
            this.PepConfigGB.Controls.Add(this.label8);
            this.PepConfigGB.Location = new System.Drawing.Point(3, 43);
            this.PepConfigGB.Name = "PepConfigGB";
            this.PepConfigGB.Size = new System.Drawing.Size(398, 70);
            this.PepConfigGB.TabIndex = 27;
            this.PepConfigGB.TabStop = false;
            this.PepConfigGB.Text = "Configuration";
            // 
            // PepConfigBox
            // 
            this.PepConfigBox.FormattingEnabled = true;
            this.PepConfigBox.Items.AddRange(new object[] {
            ""});
            this.PepConfigBox.Location = new System.Drawing.Point(9, 37);
            this.PepConfigBox.Name = "PepConfigBox";
            this.PepConfigBox.Size = new System.Drawing.Size(245, 21);
            this.PepConfigBox.TabIndex = 32;
            this.PepConfigBox.SelectedIndexChanged += new System.EventHandler(this.PepConfigBox_SelectedIndexChanged);
            this.PepConfigBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.ConfigBox_KeyPress);
            this.PepConfigBox.TextChanged += new System.EventHandler(this.ConfigBox_TextChanged);
            // 
            // PepEditButton
            // 
            this.PepEditButton.Location = new System.Drawing.Point(321, 36);
            this.PepEditButton.Name = "PepEditButton";
            this.PepEditButton.Size = new System.Drawing.Size(55, 21);
            this.PepEditButton.TabIndex = 31;
            this.PepEditButton.Text = "New";
            this.PepEditButton.UseVisualStyleBackColor = true;
            this.PepEditButton.Click += new System.EventHandler(this.ConfigEditButton_Click);
            // 
            // PepConfigBrowse
            // 
            this.PepConfigBrowse.Location = new System.Drawing.Point(260, 36);
            this.PepConfigBrowse.Name = "PepConfigBrowse";
            this.PepConfigBrowse.Size = new System.Drawing.Size(55, 21);
            this.PepConfigBrowse.TabIndex = 30;
            this.PepConfigBrowse.Text = "Browse";
            this.PepConfigBrowse.UseVisualStyleBackColor = true;
            this.PepConfigBrowse.Click += new System.EventHandler(this.PepConfigBrowse_Click);
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Font = new System.Drawing.Font("Book Antiqua", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label8.Location = new System.Drawing.Point(6, 16);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(118, 18);
            this.label8.TabIndex = 29;
            this.label8.Text = "Pepitome Config:";
            // 
            // ConfigGB
            // 
            this.ConfigGB.Controls.Add(this.ConfigDatabasePanel);
            this.ConfigGB.Controls.Add(this.ConfigTagPanel);
            this.ConfigGB.Location = new System.Drawing.Point(12, 262);
            this.ConfigGB.Name = "ConfigGB";
            this.ConfigGB.Size = new System.Drawing.Size(398, 112);
            this.ConfigGB.TabIndex = 26;
            this.ConfigGB.TabStop = false;
            this.ConfigGB.Text = "Configuration";
            this.ConfigGB.Visible = false;
            // 
            // ConfigDatabasePanel
            // 
            this.ConfigDatabasePanel.Controls.Add(this.MyriConfigBox);
            this.ConfigDatabasePanel.Controls.Add(this.MyriEditButton);
            this.ConfigDatabasePanel.Controls.Add(this.MyriConfigButton);
            this.ConfigDatabasePanel.Controls.Add(this.label13);
            this.ConfigDatabasePanel.Location = new System.Drawing.Point(10, 14);
            this.ConfigDatabasePanel.Name = "ConfigDatabasePanel";
            this.ConfigDatabasePanel.Size = new System.Drawing.Size(379, 61);
            this.ConfigDatabasePanel.TabIndex = 17;
            this.ConfigDatabasePanel.Visible = false;
            // 
            // MyriConfigBox
            // 
            this.MyriConfigBox.FormattingEnabled = true;
            this.MyriConfigBox.Items.AddRange(new object[] {
            ""});
            this.MyriConfigBox.Location = new System.Drawing.Point(6, 26);
            this.MyriConfigBox.Name = "MyriConfigBox";
            this.MyriConfigBox.Size = new System.Drawing.Size(245, 21);
            this.MyriConfigBox.TabIndex = 26;
            this.MyriConfigBox.SelectedIndexChanged += new System.EventHandler(this.MyriConfigBox_SelectedIndexChanged);
            this.MyriConfigBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.ConfigBox_KeyPress);
            this.MyriConfigBox.TextChanged += new System.EventHandler(this.ConfigBox_TextChanged);
            // 
            // MyriEditButton
            // 
            this.MyriEditButton.Location = new System.Drawing.Point(318, 25);
            this.MyriEditButton.Name = "MyriEditButton";
            this.MyriEditButton.Size = new System.Drawing.Size(55, 21);
            this.MyriEditButton.TabIndex = 16;
            this.MyriEditButton.Text = "New";
            this.MyriEditButton.UseVisualStyleBackColor = true;
            this.MyriEditButton.Click += new System.EventHandler(this.ConfigEditButton_Click);
            // 
            // MyriConfigButton
            // 
            this.MyriConfigButton.Location = new System.Drawing.Point(257, 25);
            this.MyriConfigButton.Name = "MyriConfigButton";
            this.MyriConfigButton.Size = new System.Drawing.Size(55, 21);
            this.MyriConfigButton.TabIndex = 15;
            this.MyriConfigButton.Text = "Browse";
            this.MyriConfigButton.UseVisualStyleBackColor = true;
            this.MyriConfigButton.Click += new System.EventHandler(this.MyriConfigButton_Click);
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Font = new System.Drawing.Font("Book Antiqua", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label13.Location = new System.Drawing.Point(3, 5);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(129, 18);
            this.label13.TabIndex = 13;
            this.label13.Text = "MyriMatch Config:";
            // 
            // ConfigTagPanel
            // 
            this.ConfigTagPanel.Controls.Add(this.DTConfigBox);
            this.ConfigTagPanel.Controls.Add(this.TRConfigBox);
            this.ConfigTagPanel.Controls.Add(this.TREditButton);
            this.ConfigTagPanel.Controls.Add(this.TRConfigButton);
            this.ConfigTagPanel.Controls.Add(this.label12);
            this.ConfigTagPanel.Controls.Add(this.DTEditButton);
            this.ConfigTagPanel.Controls.Add(this.DTConfigButton);
            this.ConfigTagPanel.Controls.Add(this.label11);
            this.ConfigTagPanel.Location = new System.Drawing.Point(10, 14);
            this.ConfigTagPanel.Name = "ConfigTagPanel";
            this.ConfigTagPanel.Size = new System.Drawing.Size(379, 94);
            this.ConfigTagPanel.TabIndex = 11;
            this.ConfigTagPanel.Visible = false;
            // 
            // DTConfigBox
            // 
            this.DTConfigBox.FormattingEnabled = true;
            this.DTConfigBox.Items.AddRange(new object[] {
            ""});
            this.DTConfigBox.Location = new System.Drawing.Point(6, 20);
            this.DTConfigBox.Name = "DTConfigBox";
            this.DTConfigBox.Size = new System.Drawing.Size(245, 21);
            this.DTConfigBox.TabIndex = 27;
            this.DTConfigBox.SelectedIndexChanged += new System.EventHandler(this.DTConfigBox_SelectedIndexChanged);
            this.DTConfigBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.ConfigBox_KeyPress);
            this.DTConfigBox.TextChanged += new System.EventHandler(this.ConfigBox_TextChanged);
            // 
            // TRConfigBox
            // 
            this.TRConfigBox.FormattingEnabled = true;
            this.TRConfigBox.Items.AddRange(new object[] {
            ""});
            this.TRConfigBox.Location = new System.Drawing.Point(6, 65);
            this.TRConfigBox.Name = "TRConfigBox";
            this.TRConfigBox.Size = new System.Drawing.Size(245, 21);
            this.TRConfigBox.TabIndex = 28;
            this.TRConfigBox.SelectedIndexChanged += new System.EventHandler(this.TRConfigBox_SelectedIndexChanged);
            this.TRConfigBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.ConfigBox_KeyPress);
            this.TRConfigBox.TextChanged += new System.EventHandler(this.ConfigBox_TextChanged);
            // 
            // TREditButton
            // 
            this.TREditButton.Location = new System.Drawing.Point(318, 64);
            this.TREditButton.Name = "TREditButton";
            this.TREditButton.Size = new System.Drawing.Size(55, 21);
            this.TREditButton.TabIndex = 16;
            this.TREditButton.Text = "New";
            this.TREditButton.UseVisualStyleBackColor = true;
            this.TREditButton.Click += new System.EventHandler(this.ConfigEditButton_Click);
            // 
            // TRConfigButton
            // 
            this.TRConfigButton.Location = new System.Drawing.Point(257, 64);
            this.TRConfigButton.Name = "TRConfigButton";
            this.TRConfigButton.Size = new System.Drawing.Size(55, 21);
            this.TRConfigButton.TabIndex = 15;
            this.TRConfigButton.Text = "Browse";
            this.TRConfigButton.UseVisualStyleBackColor = true;
            this.TRConfigButton.Click += new System.EventHandler(this.TRConfigButton_Click);
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Font = new System.Drawing.Font("Book Antiqua", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label12.Location = new System.Drawing.Point(3, 44);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(121, 18);
            this.label12.TabIndex = 13;
            this.label12.Text = "TagRecon Config:";
            // 
            // DTEditButton
            // 
            this.DTEditButton.Location = new System.Drawing.Point(318, 20);
            this.DTEditButton.Name = "DTEditButton";
            this.DTEditButton.Size = new System.Drawing.Size(55, 21);
            this.DTEditButton.TabIndex = 12;
            this.DTEditButton.Text = "New";
            this.DTEditButton.UseVisualStyleBackColor = true;
            this.DTEditButton.Click += new System.EventHandler(this.ConfigEditButton_Click);
            // 
            // DTConfigButton
            // 
            this.DTConfigButton.Location = new System.Drawing.Point(257, 20);
            this.DTConfigButton.Name = "DTConfigButton";
            this.DTConfigButton.Size = new System.Drawing.Size(55, 21);
            this.DTConfigButton.TabIndex = 11;
            this.DTConfigButton.Text = "Browse";
            this.DTConfigButton.UseVisualStyleBackColor = true;
            this.DTConfigButton.Click += new System.EventHandler(this.DTConfigButton_Click);
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Font = new System.Drawing.Font("Book Antiqua", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label11.Location = new System.Drawing.Point(3, 0);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(117, 18);
            this.label11.TabIndex = 9;
            this.label11.Text = "DirecTag Config:";
            // 
            // TagReconInfoBox
            // 
            this.TagReconInfoBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.TagReconInfoBox.Location = new System.Drawing.Point(6, 223);
            this.TagReconInfoBox.Multiline = true;
            this.TagReconInfoBox.Name = "TagReconInfoBox";
            this.TagReconInfoBox.ReadOnly = true;
            this.TagReconInfoBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.TagReconInfoBox.Size = new System.Drawing.Size(245, 175);
            this.TagReconInfoBox.TabIndex = 5;
            this.TagReconInfoBox.WordWrap = false;
            // 
            // TagReconInfoLabel
            // 
            this.TagReconInfoLabel.AutoSize = true;
            this.TagReconInfoLabel.Location = new System.Drawing.Point(6, 207);
            this.TagReconInfoLabel.Name = "TagReconInfoLabel";
            this.TagReconInfoLabel.Size = new System.Drawing.Size(123, 13);
            this.TagReconInfoLabel.TabIndex = 6;
            this.TagReconInfoLabel.Text = "TagRecon Configuration";
            // 
            // DirecTagInfoLabel
            // 
            this.DirecTagInfoLabel.AutoSize = true;
            this.DirecTagInfoLabel.Location = new System.Drawing.Point(6, 8);
            this.DirecTagInfoLabel.Name = "DirecTagInfoLabel";
            this.DirecTagInfoLabel.Size = new System.Drawing.Size(116, 13);
            this.DirecTagInfoLabel.TabIndex = 8;
            this.DirecTagInfoLabel.Text = "DirecTag Configuration";
            // 
            // DirecTagInfoBox
            // 
            this.DirecTagInfoBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.DirecTagInfoBox.Location = new System.Drawing.Point(6, 24);
            this.DirecTagInfoBox.Multiline = true;
            this.DirecTagInfoBox.Name = "DirecTagInfoBox";
            this.DirecTagInfoBox.ReadOnly = true;
            this.DirecTagInfoBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.DirecTagInfoBox.Size = new System.Drawing.Size(245, 175);
            this.DirecTagInfoBox.TabIndex = 7;
            this.DirecTagInfoBox.WordWrap = false;
            // 
            // TagConfigInfoPanel
            // 
            this.TagConfigInfoPanel.Controls.Add(this.DirecTagInfoLabel);
            this.TagConfigInfoPanel.Controls.Add(this.DirecTagInfoBox);
            this.TagConfigInfoPanel.Controls.Add(this.TagReconInfoBox);
            this.TagConfigInfoPanel.Controls.Add(this.TagReconInfoLabel);
            this.TagConfigInfoPanel.Location = new System.Drawing.Point(436, 1);
            this.TagConfigInfoPanel.Name = "TagConfigInfoPanel";
            this.TagConfigInfoPanel.Size = new System.Drawing.Size(255, 409);
            this.TagConfigInfoPanel.TabIndex = 9;
            this.TagConfigInfoPanel.Visible = false;
            // 
            // DatabaseConfigInfoPanel
            // 
            this.DatabaseConfigInfoPanel.Controls.Add(this.MyriMatchInfoLabel);
            this.DatabaseConfigInfoPanel.Controls.Add(this.MyriMatchInfoBox);
            this.DatabaseConfigInfoPanel.Location = new System.Drawing.Point(436, 1);
            this.DatabaseConfigInfoPanel.Name = "DatabaseConfigInfoPanel";
            this.DatabaseConfigInfoPanel.Size = new System.Drawing.Size(255, 409);
            this.DatabaseConfigInfoPanel.TabIndex = 33;
            this.DatabaseConfigInfoPanel.Visible = false;
            // 
            // MyriMatchInfoLabel
            // 
            this.MyriMatchInfoLabel.AutoSize = true;
            this.MyriMatchInfoLabel.Location = new System.Drawing.Point(6, 8);
            this.MyriMatchInfoLabel.Name = "MyriMatchInfoLabel";
            this.MyriMatchInfoLabel.Size = new System.Drawing.Size(121, 13);
            this.MyriMatchInfoLabel.TabIndex = 10;
            this.MyriMatchInfoLabel.Text = "MyriMatch Configuration";
            // 
            // MyriMatchInfoBox
            // 
            this.MyriMatchInfoBox.Location = new System.Drawing.Point(6, 24);
            this.MyriMatchInfoBox.Multiline = true;
            this.MyriMatchInfoBox.Name = "MyriMatchInfoBox";
            this.MyriMatchInfoBox.ReadOnly = true;
            this.MyriMatchInfoBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.MyriMatchInfoBox.Size = new System.Drawing.Size(245, 374);
            this.MyriMatchInfoBox.TabIndex = 9;
            this.MyriMatchInfoBox.WordWrap = false;
            // 
            // PepConfigInfoPanel
            // 
            this.PepConfigInfoPanel.Controls.Add(this.PepInfoLabel);
            this.PepConfigInfoPanel.Controls.Add(this.PepitomeInfoBox);
            this.PepConfigInfoPanel.Location = new System.Drawing.Point(436, 1);
            this.PepConfigInfoPanel.Name = "PepConfigInfoPanel";
            this.PepConfigInfoPanel.Size = new System.Drawing.Size(255, 409);
            this.PepConfigInfoPanel.TabIndex = 34;
            this.PepConfigInfoPanel.Visible = false;
            // 
            // PepInfoLabel
            // 
            this.PepInfoLabel.AutoSize = true;
            this.PepInfoLabel.Location = new System.Drawing.Point(6, 8);
            this.PepInfoLabel.Name = "PepInfoLabel";
            this.PepInfoLabel.Size = new System.Drawing.Size(116, 13);
            this.PepInfoLabel.TabIndex = 12;
            this.PepInfoLabel.Text = "Pepitome Configuration";
            // 
            // PepitomeInfoBox
            // 
            this.PepitomeInfoBox.Location = new System.Drawing.Point(6, 24);
            this.PepitomeInfoBox.Multiline = true;
            this.PepitomeInfoBox.Name = "PepitomeInfoBox";
            this.PepitomeInfoBox.ReadOnly = true;
            this.PepitomeInfoBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.PepitomeInfoBox.Size = new System.Drawing.Size(245, 374);
            this.PepitomeInfoBox.TabIndex = 11;
            this.PepitomeInfoBox.WordWrap = false;
            // 
            // AddJobForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(690, 411);
            this.Controls.Add(this.FolderPanel);
            this.Controls.Add(this.PepConfigInfoPanel);
            this.Controls.Add(this.DatabaseConfigInfoPanel);
            this.Controls.Add(this.TagConfigInfoPanel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Fixed3D;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "AddJobForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Add Job";
            this.FolderPanel.ResumeLayout(false);
            this.FolderPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.CPUsBox)).EndInit();
            this.PepPanel.ResumeLayout(false);
            this.PepPanel.PerformLayout();
            this.PepConfigGB.ResumeLayout(false);
            this.PepConfigGB.PerformLayout();
            this.ConfigGB.ResumeLayout(false);
            this.ConfigDatabasePanel.ResumeLayout(false);
            this.ConfigDatabasePanel.PerformLayout();
            this.ConfigTagPanel.ResumeLayout(false);
            this.ConfigTagPanel.PerformLayout();
            this.TagConfigInfoPanel.ResumeLayout(false);
            this.TagConfigInfoPanel.PerformLayout();
            this.DatabaseConfigInfoPanel.ResumeLayout(false);
            this.DatabaseConfigInfoPanel.PerformLayout();
            this.PepConfigInfoPanel.ResumeLayout(false);
            this.PepConfigInfoPanel.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel FolderPanel;
        private System.Windows.Forms.Panel ConfigTagPanel;
        private System.Windows.Forms.Button TREditButton;
        private System.Windows.Forms.Button TRConfigButton;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Button DTEditButton;
        private System.Windows.Forms.Button DTConfigButton;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Panel ConfigDatabasePanel;
        private System.Windows.Forms.Button MyriEditButton;
        private System.Windows.Forms.Button MyriConfigButton;
        private System.Windows.Forms.Label label13;
        internal System.Windows.Forms.ComboBox MyriConfigBox;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label CPUsAutoLabel;
        internal System.Windows.Forms.NumericUpDown CPUsBox;
        private System.Windows.Forms.CheckBox IntermediateBox;
        private System.Windows.Forms.Label TagReconInfoLabel;
        private System.Windows.Forms.Label DirecTagInfoLabel;
        private System.Windows.Forms.Panel TagConfigInfoPanel;
        private System.Windows.Forms.Panel DatabaseConfigInfoPanel;
        private System.Windows.Forms.Label MyriMatchInfoLabel;
        private System.Windows.Forms.ComboBox DatabaseLocBox;
        private System.Windows.Forms.ComboBox InputFilesBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button DataFilesButton;
        private System.Windows.Forms.Button AddJobRunButton;
        private System.Windows.Forms.Button AddJobCancelButton;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Button DatabaseLocButton;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Button InitialDirectoryButton;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.GroupBox ConfigGB;
        private System.Windows.Forms.TextBox NameBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox DTConfigBox;
        private System.Windows.Forms.ComboBox TRConfigBox;
        private System.Windows.Forms.CheckBox newFolderBox;
        internal System.Windows.Forms.TextBox MyriMatchInfoBox;
        internal System.Windows.Forms.TextBox TagReconInfoBox;
        internal System.Windows.Forms.TextBox DirecTagInfoBox;
        internal System.Windows.Forms.ComboBox OutputDirectoryBox;
        private System.Windows.Forms.ComboBox SearchTypeBox;
        private System.Windows.Forms.Panel PepPanel;
        private System.Windows.Forms.GroupBox PepConfigGB;
        private System.Windows.Forms.ComboBox SpecLibBox;
        private System.Windows.Forms.Button SpecLibBrowse;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.ComboBox PepConfigBox;
        private System.Windows.Forms.Button PepEditButton;
        private System.Windows.Forms.Button PepConfigBrowse;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Panel PepConfigInfoPanel;
        private System.Windows.Forms.Label PepInfoLabel;
        internal System.Windows.Forms.TextBox PepitomeInfoBox;

    }
}

