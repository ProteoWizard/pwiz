namespace pwiz.Skyline.EditUI.OptimizeTransitions
{
    partial class OptimizeDocumentTransitionsForm
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
            this.btnDetails = new System.Windows.Forms.Button();
            this.optimizeTransitionsSettingsControl1 = new pwiz.Skyline.EditUI.OptimizeTransitions.OptimizeTransitionsSettingsControl();
            this.btnApply = new System.Windows.Forms.Button();
            this.btnPreview = new System.Windows.Forms.Button();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // databoundGridControl
            // 
            this.databoundGridControl.Location = new System.Drawing.Point(0, 105);
            this.databoundGridControl.Size = new System.Drawing.Size(800, 345);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.btnDetails);
            this.panel1.Controls.Add(this.optimizeTransitionsSettingsControl1);
            this.panel1.Controls.Add(this.btnApply);
            this.panel1.Controls.Add(this.btnPreview);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(800, 105);
            this.panel1.TabIndex = 1;
            // 
            // btnDetails
            // 
            this.btnDetails.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnDetails.Location = new System.Drawing.Point(713, 59);
            this.btnDetails.Name = "btnDetails";
            this.btnDetails.Size = new System.Drawing.Size(75, 23);
            this.btnDetails.TabIndex = 5;
            this.btnDetails.Text = "Details";
            this.btnDetails.UseVisualStyleBackColor = true;
            this.btnDetails.Click += new System.EventHandler(this.btnDetails_Click);
            // 
            // optimizeTransitionsSettingsControl1
            // 
            this.optimizeTransitionsSettingsControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.optimizeTransitionsSettingsControl1.Location = new System.Drawing.Point(0, 0);
            this.optimizeTransitionsSettingsControl1.MinNumberOfTransitions = 4;
            this.optimizeTransitionsSettingsControl1.Name = "optimizeTransitionsSettingsControl1";
            this.optimizeTransitionsSettingsControl1.PreserveNonQuantitative = false;
            this.optimizeTransitionsSettingsControl1.Size = new System.Drawing.Size(700, 104);
            this.optimizeTransitionsSettingsControl1.TabIndex = 4;
            // 
            // btnApply
            // 
            this.btnApply.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
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
            this.btnPreview.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnPreview.Location = new System.Drawing.Point(713, 4);
            this.btnPreview.Name = "btnPreview";
            this.btnPreview.Size = new System.Drawing.Size(75, 23);
            this.btnPreview.TabIndex = 2;
            this.btnPreview.Text = "Preview";
            this.btnPreview.UseVisualStyleBackColor = true;
            this.btnPreview.Click += new System.EventHandler(this.btnPreview_Click);
            // 
            // OptimizeDocumentTransitionsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.panel1);
            this.Name = "OptimizeDocumentTransitionsForm";
            this.Text = "OptimizeDocumentTransitionsForm";
            this.Controls.SetChildIndex(this.panel1, 0);
            this.Controls.SetChildIndex(this.databoundGridControl, 0);
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button btnApply;
        private System.Windows.Forms.Button btnPreview;
        private OptimizeTransitions.OptimizeTransitionsSettingsControl optimizeTransitionsSettingsControl1;
        private System.Windows.Forms.Button btnDetails;
    }
}