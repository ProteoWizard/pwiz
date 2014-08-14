namespace pwiz.Skyline.EditUI
{
    partial class RenameProteinsDlg
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RenameProteinsDlg));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnPopulate = new System.Windows.Forms.Button();
            this.btnFASTA = new System.Windows.Forms.Button();
            this.dataGridViewRename = new pwiz.Skyline.Controls.DataGridViewEx();
            this.currentNameDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.newNameDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.renameProteinsWindowBindingSource = new System.Windows.Forms.BindingSource(this.components);
            this.btnAccession = new System.Windows.Forms.Button();
            this.btnGene = new System.Windows.Forms.Button();
            this.btnPreferredName = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewRename)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.renameProteinsWindowBindingSource)).BeginInit();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            resources.ApplyResources(this.btnOK, "btnOK");
            this.btnOK.Name = "btnOK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnPopulate
            // 
            resources.ApplyResources(this.btnPopulate, "btnPopulate");
            this.btnPopulate.Name = "btnPopulate";
            this.btnPopulate.UseVisualStyleBackColor = true;
            this.btnPopulate.Click += new System.EventHandler(this.btnPopulate_Click);
            // 
            // btnFASTA
            // 
            resources.ApplyResources(this.btnFASTA, "btnFASTA");
            this.btnFASTA.Name = "btnFASTA";
            this.btnFASTA.UseVisualStyleBackColor = true;
            this.btnFASTA.Click += new System.EventHandler(this.btnFASTA_Click);
            // 
            // dataGridViewRename
            // 
            resources.ApplyResources(this.dataGridViewRename, "dataGridViewRename");
            this.dataGridViewRename.AutoGenerateColumns = false;
            this.dataGridViewRename.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewRename.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.currentNameDataGridViewTextBoxColumn,
            this.newNameDataGridViewTextBoxColumn});
            this.dataGridViewRename.DataSource = this.renameProteinsWindowBindingSource;
            this.dataGridViewRename.Name = "dataGridViewRename";
            // 
            // currentNameDataGridViewTextBoxColumn
            // 
            this.currentNameDataGridViewTextBoxColumn.DataPropertyName = "CurrentName";
            resources.ApplyResources(this.currentNameDataGridViewTextBoxColumn, "currentNameDataGridViewTextBoxColumn");
            this.currentNameDataGridViewTextBoxColumn.Name = "currentNameDataGridViewTextBoxColumn";
            // 
            // newNameDataGridViewTextBoxColumn
            // 
            this.newNameDataGridViewTextBoxColumn.DataPropertyName = "NewName";
            resources.ApplyResources(this.newNameDataGridViewTextBoxColumn, "newNameDataGridViewTextBoxColumn");
            this.newNameDataGridViewTextBoxColumn.Name = "newNameDataGridViewTextBoxColumn";
            // 
            // renameProteinsWindowBindingSource
            // 
            this.renameProteinsWindowBindingSource.DataSource = typeof(pwiz.Skyline.EditUI.RenameProteinsDlg.RenameProteins);
            // 
            // btnAccession
            // 
            resources.ApplyResources(this.btnAccession, "btnAccession");
            this.btnAccession.Name = "btnAccession";
            this.btnAccession.UseVisualStyleBackColor = true;
            this.btnAccession.Click += new System.EventHandler(this.Accession_Click);
            // 
            // btnGene
            // 
            resources.ApplyResources(this.btnGene, "btnGene");
            this.btnGene.Name = "btnGene";
            this.btnGene.UseVisualStyleBackColor = true;
            this.btnGene.Click += new System.EventHandler(this.Gene_Click);
            // 
            // btnPreferredName
            // 
            resources.ApplyResources(this.btnPreferredName, "btnPreferredName");
            this.btnPreferredName.Name = "btnPreferredName";
            this.btnPreferredName.UseVisualStyleBackColor = true;
            this.btnPreferredName.Click += new System.EventHandler(this.PreferredName_Click);
            // 
            // RenameProteinsDlg
            // 
            this.AcceptButton = this.btnOK;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnPreferredName);
            this.Controls.Add(this.btnGene);
            this.Controls.Add(this.btnAccession);
            this.Controls.Add(this.dataGridViewRename);
            this.Controls.Add(this.btnFASTA);
            this.Controls.Add(this.btnPopulate);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "RenameProteinsDlg";
            this.ShowInTaskbar = false;
            this.Load += new System.EventHandler(this.OnLoad);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewRename)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.renameProteinsWindowBindingSource)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnPopulate;
        private System.Windows.Forms.Button btnFASTA;
        private Controls.DataGridViewEx dataGridViewRename;
        private System.Windows.Forms.BindingSource renameProteinsWindowBindingSource;
        private System.Windows.Forms.DataGridViewTextBoxColumn currentNameDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn newNameDataGridViewTextBoxColumn;
        private System.Windows.Forms.Button btnAccession;
        private System.Windows.Forms.Button btnGene;
        private System.Windows.Forms.Button btnPreferredName;
       
    }
}