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
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnPopulate = new System.Windows.Forms.Button();
            this.btnFASTA = new System.Windows.Forms.Button();
            this.dataGridViewRename = new pwiz.Skyline.Controls.DataGridViewEx();
            this.currentNameDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.newNameDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.renameProteinsWindowBindingSource = new System.Windows.Forms.BindingSource(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewRename)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.renameProteinsWindowBindingSource)).BeginInit();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(518, 344);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(437, 344);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 2;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnPopulate
            // 
            this.btnPopulate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnPopulate.Location = new System.Drawing.Point(13, 344);
            this.btnPopulate.Name = "btnPopulate";
            this.btnPopulate.Size = new System.Drawing.Size(75, 23);
            this.btnPopulate.TabIndex = 3;
            this.btnPopulate.Text = "Populate";
            this.btnPopulate.UseVisualStyleBackColor = true;
            this.btnPopulate.Click += new System.EventHandler(this.btnPopulate_Click);
            // 
            // btnFASTA
            // 
            this.btnFASTA.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnFASTA.Location = new System.Drawing.Point(94, 344);
            this.btnFASTA.Name = "btnFASTA";
            this.btnFASTA.Size = new System.Drawing.Size(75, 23);
            this.btnFASTA.TabIndex = 4;
            this.btnFASTA.Text = "FASTA...";
            this.btnFASTA.UseVisualStyleBackColor = true;
            this.btnFASTA.Click += new System.EventHandler(this.btnFASTA_Click);
            // 
            // dataGridViewRename
            // 
            this.dataGridViewRename.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridViewRename.AutoGenerateColumns = false;
            this.dataGridViewRename.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridViewRename.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewRename.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.currentNameDataGridViewTextBoxColumn,
            this.newNameDataGridViewTextBoxColumn});
            this.dataGridViewRename.DataSource = this.renameProteinsWindowBindingSource;
            this.dataGridViewRename.Location = new System.Drawing.Point(13, 12);
            this.dataGridViewRename.Name = "dataGridViewRename";
            this.dataGridViewRename.Size = new System.Drawing.Size(580, 326);
            this.dataGridViewRename.TabIndex = 5;
            // 
            // currentNameDataGridViewTextBoxColumn
            // 
            this.currentNameDataGridViewTextBoxColumn.DataPropertyName = "CurrentName";
            this.currentNameDataGridViewTextBoxColumn.HeaderText = "CurrentName";
            this.currentNameDataGridViewTextBoxColumn.Name = "currentNameDataGridViewTextBoxColumn";
            // 
            // newNameDataGridViewTextBoxColumn
            // 
            this.newNameDataGridViewTextBoxColumn.DataPropertyName = "NewName";
            this.newNameDataGridViewTextBoxColumn.HeaderText = "NewName";
            this.newNameDataGridViewTextBoxColumn.Name = "newNameDataGridViewTextBoxColumn";
            // 
            // renameProteinsWindowBindingSource
            // 
            this.renameProteinsWindowBindingSource.DataSource = typeof(pwiz.Skyline.EditUI.RenameProteinsDlg.RenameProteins);
            // 
            // RenameProteinsDlg
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(605, 379);
            this.Controls.Add(this.dataGridViewRename);
            this.Controls.Add(this.btnFASTA);
            this.Controls.Add(this.btnPopulate);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "RenameProteinsDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Rename Proteins";
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
       
    }
}