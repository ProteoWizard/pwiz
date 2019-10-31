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
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.endTime = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.textBoxFolder = new System.Windows.Forms.TextBox();
            this.buttonFolder = new System.Windows.Forms.Button();
            this.button3 = new System.Windows.Forms.Button();
            this.comboBoxOptions = new System.Windows.Forms.ComboBox();
            this.labelOptions = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.comboBoxOptions2 = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // enabled
            // 
            this.enabled.AutoSize = true;
            this.enabled.Location = new System.Drawing.Point(18, 18);
            this.enabled.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.enabled.Name = "enabled";
            this.enabled.Size = new System.Drawing.Size(171, 24);
            this.enabled.TabIndex = 0;
            this.enabled.Text = "Enable nightly build";
            this.enabled.UseVisualStyleBackColor = true;
            // 
            // startTime
            // 
            this.startTime.CustomFormat = "h:mm tt";
            this.startTime.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.startTime.Location = new System.Drawing.Point(99, 54);
            this.startTime.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.startTime.Name = "startTime";
            this.startTime.ShowUpDown = true;
            this.startTime.Size = new System.Drawing.Size(110, 26);
            this.startTime.TabIndex = 2;
            this.startTime.ValueChanged += new System.EventHandler(this.StartTimeChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(14, 62);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(78, 20);
            this.label1.TabIndex = 1;
            this.label1.Text = "Start time";
            // 
            // button1
            // 
            this.button1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.button1.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.button1.Location = new System.Drawing.Point(516, 293);
            this.button1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(112, 35);
            this.button1.TabIndex = 11;
            this.button1.Text = "Cancel";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.Cancel);
            // 
            // button2
            // 
            this.button2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.button2.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.button2.Location = new System.Drawing.Point(394, 293);
            this.button2.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(112, 35);
            this.button2.TabIndex = 10;
            this.button2.Text = "OK";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.OK);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(14, 97);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(72, 20);
            this.label2.TabIndex = 4;
            this.label2.Text = "End time";
            // 
            // endTime
            // 
            this.endTime.Location = new System.Drawing.Point(98, 97);
            this.endTime.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.endTime.Name = "endTime";
            this.endTime.Size = new System.Drawing.Size(80, 20);
            this.endTime.TabIndex = 5;
            this.endTime.Text = "11:11 PM";
            this.endTime.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(14, 131);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(54, 20);
            this.label3.TabIndex = 6;
            this.label3.Text = "Folder";
            // 
            // textBoxFolder
            // 
            this.textBoxFolder.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxFolder.Location = new System.Drawing.Point(102, 126);
            this.textBoxFolder.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.textBoxFolder.Name = "textBoxFolder";
            this.textBoxFolder.Size = new System.Drawing.Size(476, 26);
            this.textBoxFolder.TabIndex = 7;
            // 
            // buttonFolder
            // 
            this.buttonFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonFolder.Location = new System.Drawing.Point(590, 126);
            this.buttonFolder.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.buttonFolder.Name = "buttonFolder";
            this.buttonFolder.Size = new System.Drawing.Size(39, 31);
            this.buttonFolder.TabIndex = 8;
            this.buttonFolder.Text = "...";
            this.buttonFolder.UseVisualStyleBackColor = true;
            this.buttonFolder.Click += new System.EventHandler(this.buttonFolder_Click);
            // 
            // button3
            // 
            this.button3.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.button3.Location = new System.Drawing.Point(222, 54);
            this.button3.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(82, 35);
            this.button3.TabIndex = 3;
            this.button3.Text = "Now";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.Now_Click);
            // 
            // comboBoxOptions
            // 
            this.comboBoxOptions.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxOptions.FormattingEnabled = true;
            this.comboBoxOptions.Items.AddRange(new object[] {
            "Trunk",
            "Trunk with Perf Tests",
            "Release Branch",
            "Trunk with Stress Tests",
            "Integration",
            "Release Branch with Perf Tests",
            "Integration with Perf Tests"
            });
            this.comboBoxOptions.Location = new System.Drawing.Point(102, 174);
            this.comboBoxOptions.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.comboBoxOptions.Name = "comboBoxOptions";
            this.comboBoxOptions.Size = new System.Drawing.Size(373, 28);
            this.comboBoxOptions.TabIndex = 12;
            this.comboBoxOptions.SelectedIndexChanged += new System.EventHandler(this.comboBoxOptions_SelectedIndexChanged);
            // 
            // labelOptions
            // 
            this.labelOptions.AutoSize = true;
            this.labelOptions.Location = new System.Drawing.Point(14, 178);
            this.labelOptions.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelOptions.Name = "labelOptions";
            this.labelOptions.Size = new System.Drawing.Size(48, 20);
            this.labelOptions.TabIndex = 13;
            this.labelOptions.Text = "Tests";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(14, 216);
            this.label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(45, 20);
            this.label4.TabIndex = 15;
            this.label4.Text = "Then";
            // 
            // comboBoxOptions2
            // 
            this.comboBoxOptions2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxOptions2.FormattingEnabled = true;
            this.comboBoxOptions2.Items.AddRange(new object[] {
            "Trunk",
            "Trunk with Perf Tests",
            "Release Branch",
            "Trunk with Stress Tests",
            "Integration",
            "Release Branch with Perf Tests",
            "Integration with Perf Tests",
            "None"});
            this.comboBoxOptions2.Location = new System.Drawing.Point(102, 212);
            this.comboBoxOptions2.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.comboBoxOptions2.Name = "comboBoxOptions2";
            this.comboBoxOptions2.Size = new System.Drawing.Size(373, 28);
            this.comboBoxOptions2.TabIndex = 14;
            this.comboBoxOptions2.SelectedIndexChanged += new System.EventHandler(this.comboBoxOptions2_SelectedIndexChanged);
            // 
            // SkylineNightly
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(646, 369);
            this.ControlBox = false;
            this.Controls.Add(this.label4);
            this.Controls.Add(this.comboBoxOptions2);
            this.Controls.Add(this.labelOptions);
            this.Controls.Add(this.comboBoxOptions);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.buttonFolder);
            this.Controls.Add(this.textBoxFolder);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.endTime);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.startTime);
            this.Controls.Add(this.enabled);
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(1780, 425);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(614, 305);
            this.Name = "SkylineNightly";
            this.Text = "Skyline nightly build";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox enabled;
        private System.Windows.Forms.DateTimePicker startTime;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label endTime;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textBoxFolder;
        private System.Windows.Forms.Button buttonFolder;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.ComboBox comboBoxOptions;
        private System.Windows.Forms.Label labelOptions;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox comboBoxOptions2;
    }
}

