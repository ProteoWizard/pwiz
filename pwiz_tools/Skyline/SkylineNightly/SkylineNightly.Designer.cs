namespace SkylineNightly
{
    partial class SkylineNightly
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
            this.enabled = new System.Windows.Forms.CheckBox();
            this.startTime = new System.Windows.Forms.DateTimePicker();
            this.label1 = new System.Windows.Forms.Label();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.buttonOK = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.endTime = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.textBoxFolder = new System.Windows.Forms.TextBox();
            this.buttonFolder = new System.Windows.Forms.Button();
            this.buttonNow = new System.Windows.Forms.Button();
            this.comboBoxBranch1 = new System.Windows.Forms.ComboBox();
            this.labelOptions = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.labelRuns = new System.Windows.Forms.Label();
            this.radioButtonOneRun = new System.Windows.Forms.RadioButton();
            this.radioButtonTwoRuns = new System.Windows.Forms.RadioButton();
            this.comboBoxBranch2 = new System.Windows.Forms.ComboBox();
            this.labelType1 = new System.Windows.Forms.Label();
            this.comboBoxType1 = new System.Windows.Forms.ComboBox();
            this.labelType2 = new System.Windows.Forms.Label();
            this.comboBoxType2 = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // enabled
            // 
            this.enabled.AutoSize = true;
            this.enabled.Location = new System.Drawing.Point(12, 12);
            this.enabled.Name = "enabled";
            this.enabled.Size = new System.Drawing.Size(117, 17);
            this.enabled.TabIndex = 0;
            this.enabled.Text = "Enable nightly build";
            this.enabled.UseVisualStyleBackColor = true;
            // 
            // startTime
            // 
            this.startTime.CustomFormat = "h:mm tt";
            this.startTime.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.startTime.Location = new System.Drawing.Point(66, 36);
            this.startTime.Name = "startTime";
            this.startTime.ShowUpDown = true;
            this.startTime.Size = new System.Drawing.Size(75, 20);
            this.startTime.TabIndex = 2;
            this.startTime.ValueChanged += new System.EventHandler(this.StartTimeChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(9, 40);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(54, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Start time:";
            // 
            // buttonCancel
            // 
            this.buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.Location = new System.Drawing.Point(344, 205);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(75, 23);
            this.buttonCancel.TabIndex = 21;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            this.buttonCancel.Click += new System.EventHandler(this.Cancel);
            // 
            // buttonOK
            // 
            this.buttonOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.buttonOK.Location = new System.Drawing.Point(263, 205);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.Size = new System.Drawing.Size(75, 23);
            this.buttonOK.TabIndex = 20;
            this.buttonOK.Text = "OK";
            this.buttonOK.UseVisualStyleBackColor = true;
            this.buttonOK.Click += new System.EventHandler(this.OK);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(9, 63);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(51, 13);
            this.label2.TabIndex = 4;
            this.label2.Text = "End time:";
            // 
            // endTime
            // 
            this.endTime.Location = new System.Drawing.Point(65, 63);
            this.endTime.Name = "endTime";
            this.endTime.Size = new System.Drawing.Size(53, 13);
            this.endTime.TabIndex = 5;
            this.endTime.Text = "11:11 PM";
            this.endTime.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(9, 85);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(39, 13);
            this.label3.TabIndex = 6;
            this.label3.Text = "Folder:";
            // 
            // textBoxFolder
            // 
            this.textBoxFolder.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxFolder.Location = new System.Drawing.Point(68, 81);
            this.textBoxFolder.Name = "textBoxFolder";
            this.textBoxFolder.Size = new System.Drawing.Size(319, 20);
            this.textBoxFolder.TabIndex = 7;
            // 
            // buttonFolder
            // 
            this.buttonFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonFolder.Location = new System.Drawing.Point(393, 81);
            this.buttonFolder.Name = "buttonFolder";
            this.buttonFolder.Size = new System.Drawing.Size(26, 20);
            this.buttonFolder.TabIndex = 8;
            this.buttonFolder.Text = "...";
            this.buttonFolder.UseVisualStyleBackColor = true;
            this.buttonFolder.Click += new System.EventHandler(this.buttonFolder_Click);
            // 
            // buttonNow
            // 
            this.buttonNow.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.buttonNow.Location = new System.Drawing.Point(148, 35);
            this.buttonNow.Name = "buttonNow";
            this.buttonNow.Size = new System.Drawing.Size(55, 23);
            this.buttonNow.TabIndex = 3;
            this.buttonNow.Text = "Now";
            this.buttonNow.UseVisualStyleBackColor = true;
            this.buttonNow.Click += new System.EventHandler(this.Now_Click);
            // 
            // comboBoxBranch1
            // 
            this.comboBoxBranch1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxBranch1.FormattingEnabled = true;
            this.comboBoxBranch1.Items.AddRange(new object[] {
            "Master",
            "Release Branch",
            "Integration"});
            this.comboBoxBranch1.Location = new System.Drawing.Point(68, 136);
            this.comboBoxBranch1.Name = "comboBoxBranch1";
            this.comboBoxBranch1.Size = new System.Drawing.Size(115, 21);
            this.comboBoxBranch1.TabIndex = 13;
            this.comboBoxBranch1.SelectedIndexChanged += new System.EventHandler(this.comboBoxBranch_SelectedIndexChanged);
            // 
            // labelOptions
            // 
            this.labelOptions.AutoSize = true;
            this.labelOptions.Location = new System.Drawing.Point(9, 140);
            this.labelOptions.Name = "labelOptions";
            this.labelOptions.Size = new System.Drawing.Size(36, 13);
            this.labelOptions.TabIndex = 12;
            this.labelOptions.Text = "Tests:";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(9, 165);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(35, 13);
            this.label4.TabIndex = 16;
            this.label4.Text = "Then:";
            // 
            // labelRuns
            // 
            this.labelRuns.AutoSize = true;
            this.labelRuns.Location = new System.Drawing.Point(9, 116);
            this.labelRuns.Name = "labelRuns";
            this.labelRuns.Size = new System.Drawing.Size(35, 13);
            this.labelRuns.TabIndex = 9;
            this.labelRuns.Text = "Runs:";
            // 
            // radioButtonOneRun
            // 
            this.radioButtonOneRun.AutoSize = true;
            this.radioButtonOneRun.Checked = true;
            this.radioButtonOneRun.Location = new System.Drawing.Point(68, 114);
            this.radioButtonOneRun.Name = "radioButtonOneRun";
            this.radioButtonOneRun.Size = new System.Drawing.Size(31, 17);
            this.radioButtonOneRun.TabIndex = 10;
            this.radioButtonOneRun.TabStop = true;
            this.radioButtonOneRun.Text = "1";
            this.radioButtonOneRun.UseVisualStyleBackColor = true;
            // 
            // radioButtonTwoRuns
            // 
            this.radioButtonTwoRuns.AutoSize = true;
            this.radioButtonTwoRuns.Location = new System.Drawing.Point(107, 114);
            this.radioButtonTwoRuns.Name = "radioButtonTwoRuns";
            this.radioButtonTwoRuns.Size = new System.Drawing.Size(31, 17);
            this.radioButtonTwoRuns.TabIndex = 11;
            this.radioButtonTwoRuns.Text = "2";
            this.radioButtonTwoRuns.UseVisualStyleBackColor = true;
            this.radioButtonTwoRuns.CheckedChanged += new System.EventHandler(this.radioButtonTwoRuns_CheckedChanged);
            // 
            // comboBoxBranch2
            // 
            this.comboBoxBranch2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxBranch2.FormattingEnabled = true;
            this.comboBoxBranch2.Items.AddRange(new object[] {
            "Master",
            "Release Branch",
            "Integration"});
            this.comboBoxBranch2.Location = new System.Drawing.Point(68, 161);
            this.comboBoxBranch2.Name = "comboBoxBranch2";
            this.comboBoxBranch2.Size = new System.Drawing.Size(115, 21);
            this.comboBoxBranch2.TabIndex = 17;
            this.comboBoxBranch2.SelectedIndexChanged += new System.EventHandler(this.comboBoxBranch_SelectedIndexChanged);
            // 
            // labelType1
            // 
            this.labelType1.AutoSize = true;
            this.labelType1.Location = new System.Drawing.Point(193, 140);
            this.labelType1.Name = "labelType1";
            this.labelType1.Size = new System.Drawing.Size(34, 13);
            this.labelType1.TabIndex = 14;
            this.labelType1.Text = "Type:";
            // 
            // comboBoxType1
            // 
            this.comboBoxType1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxType1.FormattingEnabled = true;
            this.comboBoxType1.Items.AddRange(new object[] {
            "Standard",
            "Leak Checking",
            "Perf"});
            this.comboBoxType1.Location = new System.Drawing.Point(230, 136);
            this.comboBoxType1.Name = "comboBoxType1";
            this.comboBoxType1.Size = new System.Drawing.Size(135, 21);
            this.comboBoxType1.TabIndex = 15;
            this.comboBoxType1.SelectedIndexChanged += new System.EventHandler(this.comboBoxType_SelectedIndexChanged);
            // 
            // labelType2
            // 
            this.labelType2.AutoSize = true;
            this.labelType2.Location = new System.Drawing.Point(193, 165);
            this.labelType2.Name = "labelType2";
            this.labelType2.Size = new System.Drawing.Size(34, 13);
            this.labelType2.TabIndex = 18;
            this.labelType2.Text = "Type:";
            // 
            // comboBoxType2
            // 
            this.comboBoxType2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxType2.FormattingEnabled = true;
            this.comboBoxType2.Items.AddRange(new object[] {
            "Standard",
            "Leak Checking",
            "Perf"});
            this.comboBoxType2.Location = new System.Drawing.Point(230, 161);
            this.comboBoxType2.Name = "comboBoxType2";
            this.comboBoxType2.Size = new System.Drawing.Size(135, 21);
            this.comboBoxType2.TabIndex = 19;
            this.comboBoxType2.SelectedIndexChanged += new System.EventHandler(this.comboBoxType_SelectedIndexChanged);
            // 
            // SkylineNightly
            // 
            this.AcceptButton = this.buttonOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.buttonCancel;
            this.ClientSize = new System.Drawing.Size(431, 240);
            this.ControlBox = false;
            this.Controls.Add(this.comboBoxType2);
            this.Controls.Add(this.labelType2);
            this.Controls.Add(this.comboBoxType1);
            this.Controls.Add(this.labelType1);
            this.Controls.Add(this.radioButtonTwoRuns);
            this.Controls.Add(this.radioButtonOneRun);
            this.Controls.Add(this.labelRuns);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.comboBoxBranch2);
            this.Controls.Add(this.labelOptions);
            this.Controls.Add(this.comboBoxBranch1);
            this.Controls.Add(this.buttonNow);
            this.Controls.Add(this.buttonFolder);
            this.Controls.Add(this.textBoxFolder);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.endTime);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.buttonOK);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.startTime);
            this.Controls.Add(this.enabled);
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(1192, 290);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(415, 212);
            this.Name = "SkylineNightly";
            this.Text = "Skyline nightly build";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox enabled;
        private System.Windows.Forms.DateTimePicker startTime;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.Button buttonOK;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label endTime;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textBoxFolder;
        private System.Windows.Forms.Button buttonFolder;
        private System.Windows.Forms.Button buttonNow;
        private System.Windows.Forms.ComboBox comboBoxBranch1;
        private System.Windows.Forms.Label labelOptions;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label labelRuns;
        private System.Windows.Forms.RadioButton radioButtonOneRun;
        private System.Windows.Forms.RadioButton radioButtonTwoRuns;
        private System.Windows.Forms.ComboBox comboBoxBranch2;
        private System.Windows.Forms.Label labelType1;
        private System.Windows.Forms.ComboBox comboBoxType1;
        private System.Windows.Forms.Label labelType2;
        private System.Windows.Forms.ComboBox comboBoxType2;
    }
}

