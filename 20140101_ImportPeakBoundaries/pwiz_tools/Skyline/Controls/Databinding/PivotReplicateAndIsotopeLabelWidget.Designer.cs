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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PivotReplicateAndIsotopeLabelWidget));
            this.cbxPivotIsotopeLabel = new System.Windows.Forms.CheckBox();
            this.cbxPivotReplicate = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // cbxPivotIsotopeLabel
            // 
            resources.ApplyResources(this.cbxPivotIsotopeLabel, "cbxPivotIsotopeLabel");
            this.cbxPivotIsotopeLabel.Name = "cbxPivotIsotopeLabel";
            this.cbxPivotIsotopeLabel.UseVisualStyleBackColor = true;
            this.cbxPivotIsotopeLabel.CheckedChanged += new System.EventHandler(this.cbxPivotIsotopeLabel_CheckedChanged);
            // 
            // cbxPivotReplicate
            // 
            resources.ApplyResources(this.cbxPivotReplicate, "cbxPivotReplicate");
            this.cbxPivotReplicate.Name = "cbxPivotReplicate";
            this.cbxPivotReplicate.UseVisualStyleBackColor = true;
            this.cbxPivotReplicate.CheckedChanged += new System.EventHandler(this.cbxPivotReplicate_CheckedChanged);
            // 
            // PivotReplicateAndIsotopeLabelWidget
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.cbxPivotIsotopeLabel);
            this.Controls.Add(this.cbxPivotReplicate);
            this.Name = "PivotReplicateAndIsotopeLabelWidget";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox cbxPivotIsotopeLabel;
        private System.Windows.Forms.CheckBox cbxPivotReplicate;
    }
}
