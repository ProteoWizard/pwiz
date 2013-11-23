namespace pwiz.Skyline.Controls.Databinding
{
    partial class PivotReplicateAndIsotopeLabelWidget
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
            this.cbxPivotIsotopeLabel = new System.Windows.Forms.CheckBox();
            this.cbxPivotReplicate = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // cbxPivotIsotopeLabel
            // 
            this.cbxPivotIsotopeLabel.AutoSize = true;
            this.cbxPivotIsotopeLabel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.cbxPivotIsotopeLabel.Location = new System.Drawing.Point(138, 3);
            this.cbxPivotIsotopeLabel.Name = "cbxPivotIsotopeLabel";
            this.cbxPivotIsotopeLabel.Size = new System.Drawing.Size(117, 17);
            this.cbxPivotIsotopeLabel.TabIndex = 5;
            this.cbxPivotIsotopeLabel.Text = "Pivot Isotope Label";
            this.cbxPivotIsotopeLabel.UseVisualStyleBackColor = true;
            this.cbxPivotIsotopeLabel.CheckedChanged += new System.EventHandler(this.cbxPivotIsotopeLabel_CheckedChanged);
            // 
            // cbxPivotReplicate
            // 
            this.cbxPivotReplicate.AutoSize = true;
            this.cbxPivotReplicate.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.cbxPivotReplicate.Location = new System.Drawing.Point(3, 3);
            this.cbxPivotReplicate.Name = "cbxPivotReplicate";
            this.cbxPivotReplicate.Size = new System.Drawing.Size(129, 17);
            this.cbxPivotReplicate.TabIndex = 4;
            this.cbxPivotReplicate.Text = "Pivot Replicate Name";
            this.cbxPivotReplicate.UseVisualStyleBackColor = true;
            this.cbxPivotReplicate.CheckedChanged += new System.EventHandler(this.cbxPivotReplicate_CheckedChanged);
            // 
            // PivotReplicateAndIsotopeLabelWidget
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.cbxPivotIsotopeLabel);
            this.Controls.Add(this.cbxPivotReplicate);
            this.Name = "PivotReplicateAndIsotopeLabelWidget";
            this.Size = new System.Drawing.Size(259, 22);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox cbxPivotIsotopeLabel;
        private System.Windows.Forms.CheckBox cbxPivotReplicate;
    }
}
