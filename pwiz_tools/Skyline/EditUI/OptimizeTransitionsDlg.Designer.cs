namespace pwiz.Skyline.EditUI
{
    partial class OptimizeTransitionsDlg
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
            this.panel1 = new System.Windows.Forms.Panel();
            this.groupBoxOptimize = new System.Windows.Forms.GroupBox();
            this.radioLOQ = new System.Windows.Forms.RadioButton();
            this.radioLOD = new System.Windows.Forms.RadioButton();
            this.cbxPreserveNonQuantitative = new System.Windows.Forms.CheckBox();
            this.tbxMinTransitions = new System.Windows.Forms.NumericUpDown();
            this.btnApply = new System.Windows.Forms.Button();
            this.btnPreview = new System.Windows.Forms.Button();
            this.lblMinTransitions = new System.Windows.Forms.Label();
            this.panel1.SuspendLayout();
            this.groupBoxOptimize.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tbxMinTransitions)).BeginInit();
            this.SuspendLayout();
            // 
            // databoundGridControl
            // 
            this.databoundGridControl.Location = new System.Drawing.Point(0, 83);
            this.databoundGridControl.Size = new System.Drawing.Size(800, 367);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.groupBoxOptimize);
            this.panel1.Controls.Add(this.cbxPreserveNonQuantitative);
            this.panel1.Controls.Add(this.tbxMinTransitions);
            this.panel1.Controls.Add(this.btnApply);
            this.panel1.Controls.Add(this.btnPreview);
            this.panel1.Controls.Add(this.lblMinTransitions);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(800, 83);
            this.panel1.TabIndex = 1;
            // 
            // groupBoxOptimize
            // 
            this.groupBoxOptimize.Controls.Add(this.radioLOQ);
            this.groupBoxOptimize.Controls.Add(this.radioLOD);
            this.groupBoxOptimize.Location = new System.Drawing.Point(232, 9);
            this.groupBoxOptimize.Name = "groupBoxOptimize";
            this.groupBoxOptimize.Size = new System.Drawing.Size(211, 70);
            this.groupBoxOptimize.TabIndex = 6;
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
            this.radioLOQ.CheckedChanged += new System.EventHandler(this.radio_CheckedChanged);
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
            this.radioLOD.CheckedChanged += new System.EventHandler(this.radio_CheckedChanged);
            // 
            // cbxPreserveNonQuantitative
            // 
            this.cbxPreserveNonQuantitative.AutoSize = true;
            this.cbxPreserveNonQuantitative.Location = new System.Drawing.Point(15, 51);
            this.cbxPreserveNonQuantitative.Name = "cbxPreserveNonQuantitative";
            this.cbxPreserveNonQuantitative.Size = new System.Drawing.Size(197, 17);
            this.cbxPreserveNonQuantitative.TabIndex = 5;
            this.cbxPreserveNonQuantitative.Text = "Preserve non-quantitative transitions";
            this.cbxPreserveNonQuantitative.UseVisualStyleBackColor = true;
            // 
            // tbxMinTransitions
            // 
            this.tbxMinTransitions.Location = new System.Drawing.Point(15, 25);
            this.tbxMinTransitions.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.tbxMinTransitions.Name = "tbxMinTransitions";
            this.tbxMinTransitions.Size = new System.Drawing.Size(120, 20);
            this.tbxMinTransitions.TabIndex = 4;
            this.tbxMinTransitions.Value = new decimal(new int[] {
            4,
            0,
            0,
            0});
            // 
            // btnApply
            // 
            this.btnApply.Location = new System.Drawing.Point(713, 33);
            this.btnApply.Name = "btnApply";
            this.btnApply.Size = new System.Drawing.Size(75, 23);
            this.btnApply.TabIndex = 3;
            this.btnApply.Text = "Apply";
            this.btnApply.UseVisualStyleBackColor = true;
            this.btnApply.Click += new System.EventHandler(this.btnApply_Click);
            // 
            // btnPreview
            // 
            this.btnPreview.Location = new System.Drawing.Point(713, 4);
            this.btnPreview.Name = "btnPreview";
            this.btnPreview.Size = new System.Drawing.Size(75, 23);
            this.btnPreview.TabIndex = 2;
            this.btnPreview.Text = "Preview";
            this.btnPreview.UseVisualStyleBackColor = true;
            this.btnPreview.Click += new System.EventHandler(this.btnPreview_Click);
            // 
            // lblMinTransitions
            // 
            this.lblMinTransitions.AutoSize = true;
            this.lblMinTransitions.Location = new System.Drawing.Point(12, 9);
            this.lblMinTransitions.Name = "lblMinTransitions";
            this.lblMinTransitions.Size = new System.Drawing.Size(148, 13);
            this.lblMinTransitions.TabIndex = 0;
            this.lblMinTransitions.Text = "Minimum number of transitions";
            // 
            // OptimizeTransitionsDlg
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.panel1);
            this.Name = "OptimizeTransitionsDlg";
            this.Text = "OptimizeTransitionsDlg";
            this.Controls.SetChildIndex(this.panel1, 0);
            this.Controls.SetChildIndex(this.databoundGridControl, 0);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.groupBoxOptimize.ResumeLayout(false);
            this.groupBoxOptimize.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tbxMinTransitions)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label lblMinTransitions;
        private System.Windows.Forms.Button btnApply;
        private System.Windows.Forms.Button btnPreview;
        private System.Windows.Forms.NumericUpDown tbxMinTransitions;
        private System.Windows.Forms.CheckBox cbxPreserveNonQuantitative;
        private System.Windows.Forms.GroupBox groupBoxOptimize;
        private System.Windows.Forms.RadioButton radioLOQ;
        private System.Windows.Forms.RadioButton radioLOD;
    }
}