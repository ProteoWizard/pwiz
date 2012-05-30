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
            this.gridStatistics.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.gridStatistics.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridStatistics.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Peptide,
            this.Hydrophobicity,
            this.Predicted,
            this.RetentionTime});
            this.gridStatistics.Location = new System.Drawing.Point(13, 35);
            this.gridStatistics.Name = "gridStatistics";
            this.gridStatistics.Size = new System.Drawing.Size(448, 253);
            this.gridStatistics.TabIndex = 1;
            this.gridStatistics.KeyDown += new System.Windows.Forms.KeyEventHandler(this.gridStatistics_KeyDown);
            // 
            // Peptide
            // 
            this.Peptide.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Peptide.HeaderText = "Peptide";
            this.Peptide.Name = "Peptide";
            this.Peptide.ReadOnly = true;
            // 
            // Hydrophobicity
            // 
            this.Hydrophobicity.HeaderText = "Hydrophobicity";
            this.Hydrophobicity.Name = "Hydrophobicity";
            this.Hydrophobicity.ReadOnly = true;
            this.Hydrophobicity.Width = 80;
            // 
            // Predicted
            // 
            this.Predicted.HeaderText = "Predicted";
            this.Predicted.Name = "Predicted";
            this.Predicted.ReadOnly = true;
            this.Predicted.Width = 80;
            // 
            // RetentionTime
            // 
            this.RetentionTime.HeaderText = "Retention Time";
            this.RetentionTime.Name = "RetentionTime";
            this.RetentionTime.ReadOnly = true;
            this.RetentionTime.Width = 80;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnOk.Location = new System.Drawing.Point(386, 300);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 2;
            this.btnOk.Text = "Close";
            this.btnOk.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(42, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "&Details:";
            // 
            // RTDetails
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnOk;
            this.ClientSize = new System.Drawing.Size(473, 335);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.gridStatistics);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "RTDetails";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Retention Time Details";
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