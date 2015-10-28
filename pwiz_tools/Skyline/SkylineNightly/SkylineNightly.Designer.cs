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
            this.includePerfTests = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // enabled
            // 
            this.enabled.AutoSize = true;
            this.enabled.Location = new System.Drawing.Point(16, 15);
            this.enabled.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.enabled.Name = "enabled";
            this.enabled.Size = new System.Drawing.Size(153, 21);
            this.enabled.TabIndex = 0;
            this.enabled.Text = "Enable nightly build";
            this.enabled.UseVisualStyleBackColor = true;
            // 
            // startTime
            // 
            this.startTime.CustomFormat = "h:mm tt";
            this.startTime.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.startTime.Location = new System.Drawing.Point(88, 43);
            this.startTime.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.startTime.Name = "startTime";
            this.startTime.ShowUpDown = true;
            this.startTime.Size = new System.Drawing.Size(99, 22);
            this.startTime.TabIndex = 2;
            this.startTime.ValueChanged += new System.EventHandler(this.StartTimeChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 49);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(68, 17);
            this.label1.TabIndex = 1;
            this.label1.Text = "Start time";
            // 
            // button1
            // 
            this.button1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.button1.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.button1.Location = new System.Drawing.Point(459, 165);
            this.button1.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(100, 28);
            this.button1.TabIndex = 11;
            this.button1.Text = "Cancel";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.Cancel);
            // 
            // button2
            // 
            this.button2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.button2.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.button2.Location = new System.Drawing.Point(351, 165);
            this.button2.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(100, 28);
            this.button2.TabIndex = 10;
            this.button2.Text = "OK";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.OK);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 78);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(63, 17);
            this.label2.TabIndex = 4;
            this.label2.Text = "End time";
            // 
            // endTime
            // 
            this.endTime.Location = new System.Drawing.Point(87, 78);
            this.endTime.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.endTime.Name = "endTime";
            this.endTime.Size = new System.Drawing.Size(71, 16);
            this.endTime.TabIndex = 5;
            this.endTime.Text = "11:11 PM";
            this.endTime.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(12, 105);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(48, 17);
            this.label3.TabIndex = 6;
            this.label3.Text = "Folder";
            // 
            // textBoxFolder
            // 
            this.textBoxFolder.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxFolder.Location = new System.Drawing.Point(91, 101);
            this.textBoxFolder.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.textBoxFolder.Name = "textBoxFolder";
            this.textBoxFolder.Size = new System.Drawing.Size(424, 22);
            this.textBoxFolder.TabIndex = 7;
            // 
            // buttonFolder
            // 
            this.buttonFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonFolder.Location = new System.Drawing.Point(524, 101);
            this.buttonFolder.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.buttonFolder.Name = "buttonFolder";
            this.buttonFolder.Size = new System.Drawing.Size(35, 25);
            this.buttonFolder.TabIndex = 8;
            this.buttonFolder.Text = "...";
            this.buttonFolder.UseVisualStyleBackColor = true;
            this.buttonFolder.Click += new System.EventHandler(this.buttonFolder_Click);
            // 
            // button3
            // 
            this.button3.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.button3.Location = new System.Drawing.Point(197, 43);
            this.button3.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(73, 28);
            this.button3.TabIndex = 3;
            this.button3.Text = "Now";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.Now_Click);
            // 
            // includePerfTests
            // 
            this.includePerfTests.AutoSize = true;
            this.includePerfTests.Location = new System.Drawing.Point(16, 135);
            this.includePerfTests.Margin = new System.Windows.Forms.Padding(5, 5, 5, 5);
            this.includePerfTests.Name = "includePerfTests";
            this.includePerfTests.Size = new System.Drawing.Size(144, 21);
            this.includePerfTests.TabIndex = 9;
            this.includePerfTests.Text = "Include Perf Tests";
            this.includePerfTests.UseVisualStyleBackColor = true;
            // 
            // SkylineNightly
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(575, 215);
            this.ControlBox = false;
            this.Controls.Add(this.includePerfTests);
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
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(1586, 355);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(551, 260);
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
        private System.Windows.Forms.CheckBox includePerfTests;
    }
}

