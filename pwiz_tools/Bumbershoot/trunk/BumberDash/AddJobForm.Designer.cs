namespace BumberDash
{
    partial class AddJobForm
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
            this.IntermediateBox = new System.Windows.Forms.CheckBox();
            this.CPUsAutoLabel = new System.Windows.Forms.Label();
            this.CPUsBox = new System.Windows.Forms.NumericUpDown();
            this.label3 = new System.Windows.Forms.Label();
            this.NameBox = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.DatabaseLocBox = new System.Windows.Forms.ComboBox();
            this.TagRadio = new System.Windows.Forms.RadioButton();
            this.OutputDirectoryBox = new System.Windows.Forms.ComboBox();
            this.DatabaseRadio = new System.Windows.Forms.RadioButton();
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
            this.FolderPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.CPUsBox)).BeginInit();
            this.ConfigGB.SuspendLayout();
            this.ConfigDatabasePanel.SuspendLayout();
            this.ConfigTagPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // FolderPanel
            // 
            this.FolderPanel.Controls.Add(this.IntermediateBox);
            this.FolderPanel.Controls.Add(this.CPUsAutoLabel);
            this.FolderPanel.Controls.Add(this.CPUsBox);
            this.FolderPanel.Controls.Add(this.label3);
            this.FolderPanel.Controls.Add(this.NameBox);
            this.FolderPanel.Controls.Add(this.label1);
            this.FolderPanel.Controls.Add(this.DatabaseLocBox);
            this.FolderPanel.Controls.Add(this.TagRadio);
            this.FolderPanel.Controls.Add(this.OutputDirectoryBox);
            this.FolderPanel.Controls.Add(this.DatabaseRadio);
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
            this.FolderPanel.Controls.Add(this.ConfigGB);
            this.FolderPanel.Location = new System.Drawing.Point(0, 0);
            this.FolderPanel.Name = "FolderPanel";
            this.FolderPanel.Size = new System.Drawing.Size(425, 406);
            this.FolderPanel.TabIndex = 3;
            // 
            // IntermediateBox
            // 
            this.IntermediateBox.AutoSize = true;
            this.IntermediateBox.Location = new System.Drawing.Point(12, 377);
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
            this.CPUsAutoLabel.Location = new System.Drawing.Point(362, 378);
            this.CPUsAutoLabel.Name = "CPUsAutoLabel";
            this.CPUsAutoLabel.Size = new System.Drawing.Size(31, 15);
            this.CPUsAutoLabel.TabIndex = 31;
            this.CPUsAutoLabel.Text = "Auto";
            // 
            // CPUsBox
            // 
            this.CPUsBox.Location = new System.Drawing.Point(361, 376);
            this.CPUsBox.Name = "CPUsBox";
            this.CPUsBox.Size = new System.Drawing.Size(49, 20);
            this.CPUsBox.TabIndex = 30;
            this.CPUsBox.ValueChanged += new System.EventHandler(this.CPUsBox_ValueChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(321, 378);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(34, 13);
            this.label3.TabIndex = 29;
            this.label3.Text = "CPUs";
            // 
            // NameBox
            // 
            this.NameBox.Location = new System.Drawing.Point(28, 103);
            this.NameBox.Name = "NameBox";
            this.NameBox.Size = new System.Drawing.Size(300, 20);
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
            this.DatabaseLocBox.Size = new System.Drawing.Size(300, 21);
            this.DatabaseLocBox.TabIndex = 25;
            // 
            // TagRadio
            // 
            this.TagRadio.AutoSize = true;
            this.TagRadio.Font = new System.Drawing.Font("Book Antiqua", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TagRadio.Location = new System.Drawing.Point(203, 43);
            this.TagRadio.Name = "TagRadio";
            this.TagRadio.Size = new System.Drawing.Size(125, 22);
            this.TagRadio.TabIndex = 3;
            this.TagRadio.TabStop = true;
            this.TagRadio.Text = "Tag Sequencing";
            this.TagRadio.UseVisualStyleBackColor = true;
            this.TagRadio.CheckedChanged += new System.EventHandler(this.DestinationRadio_CheckedChanged);
            // 
            // OutputDirectoryBox
            // 
            this.OutputDirectoryBox.FormattingEnabled = true;
            this.OutputDirectoryBox.Location = new System.Drawing.Point(28, 191);
            this.OutputDirectoryBox.Name = "OutputDirectoryBox";
            this.OutputDirectoryBox.Size = new System.Drawing.Size(300, 21);
            this.OutputDirectoryBox.TabIndex = 24;
            this.OutputDirectoryBox.TextChanged += new System.EventHandler(this.OutputDirectoryBox_TextChanged);
            // 
            // DatabaseRadio
            // 
            this.DatabaseRadio.AutoSize = true;
            this.DatabaseRadio.Font = new System.Drawing.Font("Book Antiqua", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.DatabaseRadio.Location = new System.Drawing.Point(203, 15);
            this.DatabaseRadio.Name = "DatabaseRadio";
            this.DatabaseRadio.Size = new System.Drawing.Size(129, 22);
            this.DatabaseRadio.TabIndex = 2;
            this.DatabaseRadio.TabStop = true;
            this.DatabaseRadio.Text = "Database Search";
            this.DatabaseRadio.UseVisualStyleBackColor = true;
            this.DatabaseRadio.CheckedChanged += new System.EventHandler(this.DestinationRadio_CheckedChanged);
            // 
            // InputFilesBox
            // 
            this.InputFilesBox.FormattingEnabled = true;
            this.InputFilesBox.Location = new System.Drawing.Point(28, 147);
            this.InputFilesBox.Name = "InputFilesBox";
            this.InputFilesBox.Size = new System.Drawing.Size(300, 21);
            this.InputFilesBox.TabIndex = 23;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Book Antiqua", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(92, 18);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(105, 18);
            this.label2.TabIndex = 1;
            this.label2.Text = "Type of search:";
            // 
            // DataFilesButton
            // 
            this.DataFilesButton.Location = new System.Drawing.Point(338, 146);
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
            this.AddJobRunButton.Location = new System.Drawing.Point(215, 373);
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
            this.AddJobCancelButton.Location = new System.Drawing.Point(134, 373);
            this.AddJobCancelButton.Name = "AddJobCancelButton";
            this.AddJobCancelButton.Size = new System.Drawing.Size(75, 23);
            this.AddJobCancelButton.TabIndex = 22;
            this.AddJobCancelButton.Text = "Cancel";
            this.AddJobCancelButton.UseVisualStyleBackColor = true;
            this.AddJobCancelButton.Click += new System.EventHandler(this.AddJobCancelButton_Click);
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
            this.DatabaseLocButton.Location = new System.Drawing.Point(338, 234);
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
            this.label5.Size = new System.Drawing.Size(195, 18);
            this.label5.TabIndex = 6;
            this.label5.Text = "Which database will be used?";
            // 
            // InitialDirectoryButton
            // 
            this.InitialDirectoryButton.Location = new System.Drawing.Point(338, 190);
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
            // ConfigGB
            // 
            this.ConfigGB.Controls.Add(this.ConfigTagPanel);
            this.ConfigGB.Controls.Add(this.ConfigDatabasePanel);
            this.ConfigGB.Location = new System.Drawing.Point(12, 262);
            this.ConfigGB.Name = "ConfigGB";
            this.ConfigGB.Size = new System.Drawing.Size(398, 105);
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
            this.MyriConfigBox.Location = new System.Drawing.Point(6, 26);
            this.MyriConfigBox.Name = "MyriConfigBox";
            this.MyriConfigBox.Size = new System.Drawing.Size(245, 21);
            this.MyriConfigBox.TabIndex = 26;
            // 
            // MyriEditButton
            // 
            this.MyriEditButton.Location = new System.Drawing.Point(318, 25);
            this.MyriEditButton.Name = "MyriEditButton";
            this.MyriEditButton.Size = new System.Drawing.Size(55, 21);
            this.MyriEditButton.TabIndex = 16;
            this.MyriEditButton.Text = "Edit";
            this.MyriEditButton.UseVisualStyleBackColor = true;
            this.MyriEditButton.Visible = false;
            this.MyriEditButton.Click += new System.EventHandler(this.MyriEditButton_Click);
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
            this.ConfigTagPanel.Size = new System.Drawing.Size(379, 90);
            this.ConfigTagPanel.TabIndex = 11;
            this.ConfigTagPanel.Visible = false;
            // 
            // DTConfigBox
            // 
            this.DTConfigBox.FormattingEnabled = true;
            this.DTConfigBox.Location = new System.Drawing.Point(6, 21);
            this.DTConfigBox.Name = "DTConfigBox";
            this.DTConfigBox.Size = new System.Drawing.Size(245, 21);
            this.DTConfigBox.TabIndex = 27;
            // 
            // TRConfigBox
            // 
            this.TRConfigBox.FormattingEnabled = true;
            this.TRConfigBox.Location = new System.Drawing.Point(6, 65);
            this.TRConfigBox.Name = "TRConfigBox";
            this.TRConfigBox.Size = new System.Drawing.Size(245, 21);
            this.TRConfigBox.TabIndex = 28;
            // 
            // TREditButton
            // 
            this.TREditButton.Location = new System.Drawing.Point(318, 64);
            this.TREditButton.Name = "TREditButton";
            this.TREditButton.Size = new System.Drawing.Size(55, 21);
            this.TREditButton.TabIndex = 16;
            this.TREditButton.Text = "Edit";
            this.TREditButton.UseVisualStyleBackColor = true;
            this.TREditButton.Visible = false;
            this.TREditButton.Click += new System.EventHandler(this.TREditButton_Click);
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
            this.DTEditButton.Text = "Edit";
            this.DTEditButton.UseVisualStyleBackColor = true;
            this.DTEditButton.Visible = false;
            this.DTEditButton.Click += new System.EventHandler(this.DTEditButton_Click);
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
            // AddJobForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(422, 405);
            this.Controls.Add(this.FolderPanel);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "AddJobForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Add Job";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.AddJobForm_FormClosed);
            this.FolderPanel.ResumeLayout(false);
            this.FolderPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.CPUsBox)).EndInit();
            this.ConfigGB.ResumeLayout(false);
            this.ConfigDatabasePanel.ResumeLayout(false);
            this.ConfigDatabasePanel.PerformLayout();
            this.ConfigTagPanel.ResumeLayout(false);
            this.ConfigTagPanel.PerformLayout();
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
        internal System.Windows.Forms.ComboBox DatabaseLocBox;
        internal System.Windows.Forms.RadioButton TagRadio;
        internal System.Windows.Forms.ComboBox OutputDirectoryBox;
        internal System.Windows.Forms.RadioButton DatabaseRadio;
        internal System.Windows.Forms.ComboBox InputFilesBox;
        internal System.Windows.Forms.Label label2;
        internal System.Windows.Forms.Button DataFilesButton;
        internal System.Windows.Forms.Button AddJobRunButton;
        internal System.Windows.Forms.Button AddJobCancelButton;
        internal System.Windows.Forms.Label label7;
        internal System.Windows.Forms.Button DatabaseLocButton;
        internal System.Windows.Forms.Label label5;
        internal System.Windows.Forms.Button InitialDirectoryButton;
        internal System.Windows.Forms.Label label4;
        internal System.Windows.Forms.GroupBox ConfigGB;
        internal System.Windows.Forms.TextBox NameBox;
        internal System.Windows.Forms.Label label1;
        internal System.Windows.Forms.ComboBox DTConfigBox;
        internal System.Windows.Forms.ComboBox TRConfigBox;
        internal System.Windows.Forms.ComboBox MyriConfigBox;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label CPUsAutoLabel;
        internal System.Windows.Forms.NumericUpDown CPUsBox;
        private System.Windows.Forms.CheckBox IntermediateBox;

    }
}

