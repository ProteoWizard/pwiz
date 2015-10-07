namespace MultiLoad
{
    partial class MultiLoad
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
            this.label1 = new System.Windows.Forms.Label();
            this.btnStart = new System.Windows.Forms.Button();
            this.lblTime = new System.Windows.Forms.Label();
            this.numericMaxProcesses = new System.Windows.Forms.NumericUpDown();
            this.comboModel = new System.Windows.Forms.ComboBox();
            this.comboUI = new System.Windows.Forms.ComboBox();
            ((System.ComponentModel.ISupportInitialize)(this.numericMaxProcesses)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(9, 14);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(78, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Max processes";
            // 
            // btnStart
            // 
            this.btnStart.Location = new System.Drawing.Point(12, 97);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(65, 23);
            this.btnStart.TabIndex = 2;
            this.btnStart.Text = "Start";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // lblTime
            // 
            this.lblTime.AutoSize = true;
            this.lblTime.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTime.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.lblTime.Location = new System.Drawing.Point(109, 102);
            this.lblTime.Name = "lblTime";
            this.lblTime.Size = new System.Drawing.Size(39, 13);
            this.lblTime.TabIndex = 3;
            this.lblTime.Text = "00:00";
            // 
            // numericMaxProcesses
            // 
            this.numericMaxProcesses.Location = new System.Drawing.Point(93, 12);
            this.numericMaxProcesses.Maximum = new decimal(new int[] {
            6,
            0,
            0,
            0});
            this.numericMaxProcesses.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericMaxProcesses.Name = "numericMaxProcesses";
            this.numericMaxProcesses.Size = new System.Drawing.Size(30, 20);
            this.numericMaxProcesses.TabIndex = 5;
            this.numericMaxProcesses.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // comboModel
            // 
            this.comboModel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboModel.FormattingEnabled = true;
            this.comboModel.Items.AddRange(new object[] {
            "mz5-centroid",
            "wiff"});
            this.comboModel.Location = new System.Drawing.Point(12, 63);
            this.comboModel.Name = "comboModel";
            this.comboModel.Size = new System.Drawing.Size(136, 21);
            this.comboModel.TabIndex = 6;
            // 
            // comboUI
            // 
            this.comboUI.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboUI.FormattingEnabled = true;
            this.comboUI.Items.AddRange(new object[] {
            "No UI",
            "Show import progress",
            "Hide import progress",
            "Disable import progress"});
            this.comboUI.Location = new System.Drawing.Point(12, 38);
            this.comboUI.Name = "comboUI";
            this.comboUI.Size = new System.Drawing.Size(136, 21);
            this.comboUI.TabIndex = 7;
            // 
            // MultiLoad
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(160, 129);
            this.Controls.Add(this.comboUI);
            this.Controls.Add(this.comboModel);
            this.Controls.Add(this.numericMaxProcesses);
            this.Controls.Add(this.lblTime);
            this.Controls.Add(this.btnStart);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "MultiLoad";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "Skyline MultiLoad";
            ((System.ComponentModel.ISupportInitialize)(this.numericMaxProcesses)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Label lblTime;
        private System.Windows.Forms.NumericUpDown numericMaxProcesses;
        private System.Windows.Forms.ComboBox comboModel;
        private System.Windows.Forms.ComboBox comboUI;
    }
}

