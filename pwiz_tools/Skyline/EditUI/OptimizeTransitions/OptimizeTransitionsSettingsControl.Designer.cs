namespace pwiz.Skyline.EditUI.OptimizeTransitions
{
    partial class OptimizeTransitionsSettingsControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.groupBoxOptimize = new System.Windows.Forms.GroupBox();
            this.radioLOQ = new System.Windows.Forms.RadioButton();
            this.radioLOD = new System.Windows.Forms.RadioButton();
            this.cbxPreserveNonQuantitative = new System.Windows.Forms.CheckBox();
            this.tbxMinTransitions = new System.Windows.Forms.NumericUpDown();
            this.lblMinTransitions = new System.Windows.Forms.Label();
            this.groupBoxOptimize.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tbxMinTransitions)).BeginInit();
            this.SuspendLayout();
            // 
            // groupBoxOptimize
            // 
            this.groupBoxOptimize.Controls.Add(this.radioLOQ);
            this.groupBoxOptimize.Controls.Add(this.radioLOD);
            this.groupBoxOptimize.Location = new System.Drawing.Point(223, 13);
            this.groupBoxOptimize.Name = "groupBoxOptimize";
            this.groupBoxOptimize.Size = new System.Drawing.Size(211, 70);
            this.groupBoxOptimize.TabIndex = 12;
            this.groupBoxOptimize.TabStop = false;
            this.groupBoxOptimize.Text = "Optimize";
            // 
            // radioLOQ
            // 
            this.radioLOQ.AutoSize = true;
            this.radioLOQ.Checked = true;
            this.radioLOQ.Location = new System.Drawing.Point(6, 42);
            this.radioLOQ.Name = "radioLOQ";
            this.radioLOQ.Size = new System.Drawing.Size(124, 17);
            this.radioLOQ.TabIndex = 1;
            this.radioLOQ.TabStop = true;
            this.radioLOQ.Text = "Limit of quantification";
            this.radioLOQ.UseVisualStyleBackColor = true;
            this.radioLOQ.CheckedChanged += new System.EventHandler(this.SettingsValueChange);
            // 
            // radioLOD
            // 
            this.radioLOD.AutoSize = true;
            this.radioLOD.Location = new System.Drawing.Point(6, 19);
            this.radioLOD.Name = "radioLOD";
            this.radioLOD.Size = new System.Drawing.Size(105, 17);
            this.radioLOD.TabIndex = 0;
            this.radioLOD.Text = "Limit of detection";
            this.radioLOD.UseVisualStyleBackColor = true;
            this.radioLOD.CheckedChanged += new System.EventHandler(this.SettingsValueChange);
            // 
            // cbxPreserveNonQuantitative
            // 
            this.cbxPreserveNonQuantitative.AutoSize = true;
            this.cbxPreserveNonQuantitative.Location = new System.Drawing.Point(6, 55);
            this.cbxPreserveNonQuantitative.Name = "cbxPreserveNonQuantitative";
            this.cbxPreserveNonQuantitative.Size = new System.Drawing.Size(197, 17);
            this.cbxPreserveNonQuantitative.TabIndex = 11;
            this.cbxPreserveNonQuantitative.Text = "Preserve non-quantitative transitions";
            this.cbxPreserveNonQuantitative.UseVisualStyleBackColor = true;
            this.cbxPreserveNonQuantitative.CheckedChanged += new System.EventHandler(this.SettingsValueChange);
            // 
            // tbxMinTransitions
            // 
            this.tbxMinTransitions.Location = new System.Drawing.Point(6, 29);
            this.tbxMinTransitions.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.tbxMinTransitions.Name = "tbxMinTransitions";
            this.tbxMinTransitions.Size = new System.Drawing.Size(120, 20);
            this.tbxMinTransitions.TabIndex = 10;
            this.tbxMinTransitions.Value = new decimal(new int[] {
            4,
            0,
            0,
            0});
            this.tbxMinTransitions.ValueChanged += new System.EventHandler(this.SettingsValueChange);
            // 
            // lblMinTransitions
            // 
            this.lblMinTransitions.AutoSize = true;
            this.lblMinTransitions.Location = new System.Drawing.Point(3, 13);
            this.lblMinTransitions.Name = "lblMinTransitions";
            this.lblMinTransitions.Size = new System.Drawing.Size(148, 13);
            this.lblMinTransitions.TabIndex = 9;
            this.lblMinTransitions.Text = "Minimum number of transitions";
            // 
            // OptimizeTransitionsSettingsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.groupBoxOptimize);
            this.Controls.Add(this.cbxPreserveNonQuantitative);
            this.Controls.Add(this.tbxMinTransitions);
            this.Controls.Add(this.lblMinTransitions);
            this.Name = "OptimizeTransitionsSettingsControl";
            this.Size = new System.Drawing.Size(563, 85);
            this.groupBoxOptimize.ResumeLayout(false);
            this.groupBoxOptimize.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tbxMinTransitions)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.GroupBox groupBoxOptimize;
        private System.Windows.Forms.RadioButton radioLOQ;
        private System.Windows.Forms.RadioButton radioLOD;
        private System.Windows.Forms.CheckBox cbxPreserveNonQuantitative;
        private System.Windows.Forms.NumericUpDown tbxMinTransitions;
        private System.Windows.Forms.Label lblMinTransitions;
    }
}
