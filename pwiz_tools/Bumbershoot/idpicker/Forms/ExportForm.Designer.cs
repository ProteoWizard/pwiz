namespace IDPicker.Forms
{
    partial class ExportForm
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
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.LibraryOptionsPanel = new System.Windows.Forms.Panel();
            this.StartLibraryButton = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.FragmentNumBox = new System.Windows.Forms.NumericUpDown();
            this.SpectrumNumBox = new System.Windows.Forms.NumericUpDown();
            this.label2 = new System.Windows.Forms.Label();
            this.LibraryOptionsPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.FragmentNumBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.SpectrumNumBox)).BeginInit();
            this.SuspendLayout();
            // 
            // progressBar
            // 
            this.progressBar.Dock = System.Windows.Forms.DockStyle.Fill;
            this.progressBar.Location = new System.Drawing.Point(0, 0);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(510, 22);
            this.progressBar.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.progressBar.TabIndex = 0;
            // 
            // LibraryOptionsPanel
            // 
            this.LibraryOptionsPanel.Controls.Add(this.SpectrumNumBox);
            this.LibraryOptionsPanel.Controls.Add(this.label2);
            this.LibraryOptionsPanel.Controls.Add(this.FragmentNumBox);
            this.LibraryOptionsPanel.Controls.Add(this.label1);
            this.LibraryOptionsPanel.Controls.Add(this.StartLibraryButton);
            this.LibraryOptionsPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.LibraryOptionsPanel.Location = new System.Drawing.Point(0, 0);
            this.LibraryOptionsPanel.Name = "LibraryOptionsPanel";
            this.LibraryOptionsPanel.Size = new System.Drawing.Size(510, 22);
            this.LibraryOptionsPanel.TabIndex = 1;
            this.LibraryOptionsPanel.Visible = false;
            // 
            // StartLibraryButton
            // 
            this.StartLibraryButton.Location = new System.Drawing.Point(103, 67);
            this.StartLibraryButton.Name = "StartLibraryButton";
            this.StartLibraryButton.Size = new System.Drawing.Size(75, 23);
            this.StartLibraryButton.TabIndex = 0;
            this.StartLibraryButton.Text = "Begin";
            this.StartLibraryButton.UseVisualStyleBackColor = true;
            this.StartLibraryButton.Click += new System.EventHandler(this.StartLibraryButton_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(122, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Fragment m/z tolerance:";
            // 
            // FragmentNumBox
            // 
            this.FragmentNumBox.DecimalPlaces = 1;
            this.FragmentNumBox.Increment = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            this.FragmentNumBox.Location = new System.Drawing.Point(140, 7);
            this.FragmentNumBox.Name = "FragmentNumBox";
            this.FragmentNumBox.Size = new System.Drawing.Size(120, 20);
            this.FragmentNumBox.TabIndex = 2;
            this.FragmentNumBox.Value = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            // 
            // SpectrumNumBox
            // 
            this.SpectrumNumBox.Location = new System.Drawing.Point(140, 33);
            this.SpectrumNumBox.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.SpectrumNumBox.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.SpectrumNumBox.Name = "SpectrumNumBox";
            this.SpectrumNumBox.Size = new System.Drawing.Size(120, 20);
            this.SpectrumNumBox.TabIndex = 4;
            this.SpectrumNumBox.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(43, 35);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(91, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Minimum Spectra:";
            // 
            // ExportForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(510, 22);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.LibraryOptionsPanel);
            this.Name = "ExportForm";
            this.Text = "Progress";
            this.LibraryOptionsPanel.ResumeLayout(false);
            this.LibraryOptionsPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.FragmentNumBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.SpectrumNumBox)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Panel LibraryOptionsPanel;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button StartLibraryButton;
        private System.Windows.Forms.NumericUpDown SpectrumNumBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.NumericUpDown FragmentNumBox;
    }
}