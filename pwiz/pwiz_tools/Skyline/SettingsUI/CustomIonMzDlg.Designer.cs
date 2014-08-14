namespace pwiz.Skyline.SettingsUI
{
    partial class CustomIonMzDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CustomIonMzDlg));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            this.dataGridMz = new System.Windows.Forms.DataGridView();
            this.Charge = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Monoisotopic = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Average = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.btnClose = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridMz)).BeginInit();
            this.SuspendLayout();
            // 
            // dataGridMz
            // 
            this.dataGridMz.AllowUserToAddRows = false;
            this.dataGridMz.AllowUserToDeleteRows = false;
            resources.ApplyResources(this.dataGridMz, "dataGridMz");
            this.dataGridMz.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridMz.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Charge,
            this.Monoisotopic,
            this.Average});
            this.dataGridMz.Name = "dataGridMz";
            this.dataGridMz.ReadOnly = true;
            this.dataGridMz.RowHeadersVisible = false;
            // 
            // Charge
            // 
            resources.ApplyResources(this.Charge, "Charge");
            this.Charge.Name = "Charge";
            this.Charge.ReadOnly = true;
            // 
            // Monoisotopic
            // 
            this.Monoisotopic.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            dataGridViewCellStyle1.Format = "N4";
            dataGridViewCellStyle1.NullValue = null;
            this.Monoisotopic.DefaultCellStyle = dataGridViewCellStyle1;
            this.Monoisotopic.FillWeight = 50F;
            resources.ApplyResources(this.Monoisotopic, "Monoisotopic");
            this.Monoisotopic.Name = "Monoisotopic";
            this.Monoisotopic.ReadOnly = true;
            // 
            // Average
            // 
            this.Average.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            dataGridViewCellStyle2.Format = "N4";
            dataGridViewCellStyle2.NullValue = null;
            this.Average.DefaultCellStyle = dataGridViewCellStyle2;
            this.Average.FillWeight = 50F;
            resources.ApplyResources(this.Average, "Average");
            this.Average.Name = "Average";
            this.Average.ReadOnly = true;
            // 
            // btnClose
            // 
            resources.ApplyResources(this.btnClose, "btnClose");
            this.btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnClose.Name = "btnClose";
            this.btnClose.UseVisualStyleBackColor = true;
            // 
            // CustomIonMzDlg
            // 
            this.AcceptButton = this.btnClose;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnClose;
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.dataGridMz);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CustomIonMzDlg";
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.dataGridMz)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridMz;
        private System.Windows.Forms.DataGridViewTextBoxColumn Charge;
        private System.Windows.Forms.DataGridViewTextBoxColumn Monoisotopic;
        private System.Windows.Forms.DataGridViewTextBoxColumn Average;
        private System.Windows.Forms.Button btnClose;
    }
}