namespace pwiz.Skyline.SettingsUI
{
    partial class RTDetails
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RTDetails));
            this.gridStatistics = new System.Windows.Forms.DataGridView();
            this.Peptide = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Hydrophobicity = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Predicted = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.RetentionTime = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.btnOk = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.gridStatistics)).BeginInit();
            this.SuspendLayout();
            // 
            // gridStatistics
            // 
            resources.ApplyResources(this.gridStatistics, "gridStatistics");
            this.gridStatistics.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridStatistics.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Peptide,
            this.Hydrophobicity,
            this.Predicted,
            this.RetentionTime});
            this.gridStatistics.Name = "gridStatistics";
            this.gridStatistics.KeyDown += new System.Windows.Forms.KeyEventHandler(this.gridStatistics_KeyDown);
            // 
            // Peptide
            // 
            this.Peptide.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            resources.ApplyResources(this.Peptide, "Peptide");
            this.Peptide.Name = "Peptide";
            this.Peptide.ReadOnly = true;
            // 
            // Hydrophobicity
            // 
            resources.ApplyResources(this.Hydrophobicity, "Hydrophobicity");
            this.Hydrophobicity.Name = "Hydrophobicity";
            this.Hydrophobicity.ReadOnly = true;
            // 
            // Predicted
            // 
            resources.ApplyResources(this.Predicted, "Predicted");
            this.Predicted.Name = "Predicted";
            this.Predicted.ReadOnly = true;
            // 
            // RetentionTime
            // 
            resources.ApplyResources(this.RetentionTime, "RetentionTime");
            this.RetentionTime.Name = "RetentionTime";
            this.RetentionTime.ReadOnly = true;
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // RTDetails
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnOk;
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.gridStatistics);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "RTDetails";
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.gridStatistics)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataGridView gridStatistics;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.DataGridViewTextBoxColumn Peptide;
        private System.Windows.Forms.DataGridViewTextBoxColumn Hydrophobicity;
        private System.Windows.Forms.DataGridViewTextBoxColumn Predicted;
        private System.Windows.Forms.DataGridViewTextBoxColumn RetentionTime;
        private System.Windows.Forms.Label label1;
    }
}