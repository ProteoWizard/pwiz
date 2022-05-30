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
            this.tbxMinTransitions = new System.Windows.Forms.NumericUpDown();
            this.btnApply = new System.Windows.Forms.Button();
            this.btnPreview = new System.Windows.Forms.Button();
            this.lblMinTransitions = new System.Windows.Forms.Label();
            this.cbxReconsiderNonQuantitative = new System.Windows.Forms.CheckBox();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tbxMinTransitions)).BeginInit();
            this.SuspendLayout();
            // 
            // databoundGridControl
            // 
            this.databoundGridControl.Location = new System.Drawing.Point(0, 70);
            this.databoundGridControl.Size = new System.Drawing.Size(800, 380);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.cbxReconsiderNonQuantitative);
            this.panel1.Controls.Add(this.tbxMinTransitions);
            this.panel1.Controls.Add(this.btnApply);
            this.panel1.Controls.Add(this.btnPreview);
            this.panel1.Controls.Add(this.lblMinTransitions);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(800, 70);
            this.panel1.TabIndex = 1;
            // 
            // tbxMinTransitions
            // 
            this.tbxMinTransitions.Location = new System.Drawing.Point(15, 34);
            this.tbxMinTransitions.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.tbxMinTransitions.Name = "tbxMinTransitions";
            this.tbxMinTransitions.Size = new System.Drawing.Size(120, 20);
            this.tbxMinTransitions.TabIndex = 4;
            this.tbxMinTransitions.Value = new decimal(new int[] {
            1,
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
            // cbxReconsiderNonQuantitative
            // 
            this.cbxReconsiderNonQuantitative.AutoSize = true;
            this.cbxReconsiderNonQuantitative.Location = new System.Drawing.Point(213, 33);
            this.cbxReconsiderNonQuantitative.Name = "cbxReconsiderNonQuantitative";
            this.cbxReconsiderNonQuantitative.Size = new System.Drawing.Size(209, 17);
            this.cbxReconsiderNonQuantitative.TabIndex = 5;
            this.cbxReconsiderNonQuantitative.Text = "Reconsider non-quantitative transitions";
            this.cbxReconsiderNonQuantitative.UseVisualStyleBackColor = true;
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
            ((System.ComponentModel.ISupportInitialize)(this.tbxMinTransitions)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label lblMinTransitions;
        private System.Windows.Forms.Button btnApply;
        private System.Windows.Forms.Button btnPreview;
        private System.Windows.Forms.NumericUpDown tbxMinTransitions;
        private System.Windows.Forms.CheckBox cbxReconsiderNonQuantitative;
    }
}