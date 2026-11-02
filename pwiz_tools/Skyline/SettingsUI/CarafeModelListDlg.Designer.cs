namespace pwiz.Skyline.SettingsUI
{
    partial class CarafeModelListDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CarafeModelListDlg));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            this.gridModels = new pwiz.Skyline.Controls.DataGridViewEx();
            this.colName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colInstrument = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colGradient = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPrecursorMz = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colNce = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colFragmentation = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTrainingRuns = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMs2Cosine = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colRtR2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSaved = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.btnAdd = new System.Windows.Forms.Button();
            this.btnEdit = new System.Windows.Forms.Button();
            this.btnRemove = new System.Windows.Forms.Button();
            this.btnUp = new System.Windows.Forms.Button();
            this.btnDown = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.gridModels)).BeginInit();
            this.SuspendLayout();
            //
            // gridModels
            //
            this.gridModels.AllowUserToAddRows = false;
            this.gridModels.AllowUserToDeleteRows = false;
            resources.ApplyResources(this.gridModels, "gridModels");
            this.gridModels.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.AllCells;
            this.gridModels.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridModels.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colName,
            this.colInstrument,
            this.colGradient,
            this.colPrecursorMz,
            this.colNce,
            this.colFragmentation,
            this.colTrainingRuns,
            this.colMs2Cosine,
            this.colRtR2,
            this.colSaved});
            this.gridModels.MultiSelect = false;
            this.gridModels.Name = "gridModels";
            this.gridModels.ReadOnly = true;
            this.gridModels.RowHeadersVisible = false;
            this.gridModels.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridModels.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.gridModels_CellDoubleClick);
            this.gridModels.SelectionChanged += new System.EventHandler(this.gridModels_SelectionChanged);
            //
            // colName
            //
            resources.ApplyResources(this.colName, "colName");
            this.colName.Name = "colName";
            this.colName.ReadOnly = true;
            //
            // colInstrument
            //
            resources.ApplyResources(this.colInstrument, "colInstrument");
            this.colInstrument.Name = "colInstrument";
            this.colInstrument.ReadOnly = true;
            //
            // colGradient
            //
            dataGridViewCellStyle1.Format = "0.#";
            this.colGradient.DefaultCellStyle = dataGridViewCellStyle1;
            resources.ApplyResources(this.colGradient, "colGradient");
            this.colGradient.Name = "colGradient";
            this.colGradient.ReadOnly = true;
            //
            // colPrecursorMz
            //
            resources.ApplyResources(this.colPrecursorMz, "colPrecursorMz");
            this.colPrecursorMz.Name = "colPrecursorMz";
            this.colPrecursorMz.ReadOnly = true;
            //
            // colNce
            //
            dataGridViewCellStyle2.Format = "0.#";
            this.colNce.DefaultCellStyle = dataGridViewCellStyle2;
            resources.ApplyResources(this.colNce, "colNce");
            this.colNce.Name = "colNce";
            this.colNce.ReadOnly = true;
            //
            // colFragmentation
            //
            resources.ApplyResources(this.colFragmentation, "colFragmentation");
            this.colFragmentation.Name = "colFragmentation";
            this.colFragmentation.ReadOnly = true;
            //
            // colTrainingRuns
            //
            dataGridViewCellStyle3.Format = "N0";
            this.colTrainingRuns.DefaultCellStyle = dataGridViewCellStyle3;
            resources.ApplyResources(this.colTrainingRuns, "colTrainingRuns");
            this.colTrainingRuns.Name = "colTrainingRuns";
            this.colTrainingRuns.ReadOnly = true;
            //
            // colMs2Cosine
            //
            dataGridViewCellStyle4.Format = "0.000";
            this.colMs2Cosine.DefaultCellStyle = dataGridViewCellStyle4;
            resources.ApplyResources(this.colMs2Cosine, "colMs2Cosine");
            this.colMs2Cosine.Name = "colMs2Cosine";
            this.colMs2Cosine.ReadOnly = true;
            //
            // colRtR2
            //
            dataGridViewCellStyle5.Format = "0.000";
            this.colRtR2.DefaultCellStyle = dataGridViewCellStyle5;
            resources.ApplyResources(this.colRtR2, "colRtR2");
            this.colRtR2.Name = "colRtR2";
            this.colRtR2.ReadOnly = true;
            //
            // colSaved
            //
            dataGridViewCellStyle6.Format = "d";
            this.colSaved.DefaultCellStyle = dataGridViewCellStyle6;
            resources.ApplyResources(this.colSaved, "colSaved");
            this.colSaved.Name = "colSaved";
            this.colSaved.ReadOnly = true;
            //
            // btnAdd
            //
            resources.ApplyResources(this.btnAdd, "btnAdd");
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.UseVisualStyleBackColor = true;
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            //
            // btnEdit
            //
            resources.ApplyResources(this.btnEdit, "btnEdit");
            this.btnEdit.Name = "btnEdit";
            this.btnEdit.UseVisualStyleBackColor = true;
            this.btnEdit.Click += new System.EventHandler(this.btnEdit_Click);
            //
            // btnRemove
            //
            resources.ApplyResources(this.btnRemove, "btnRemove");
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.UseVisualStyleBackColor = true;
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            //
            // btnUp
            //
            resources.ApplyResources(this.btnUp, "btnUp");
            this.btnUp.Name = "btnUp";
            this.btnUp.UseVisualStyleBackColor = true;
            this.btnUp.Click += new System.EventHandler(this.btnUp_Click);
            //
            // btnDown
            //
            resources.ApplyResources(this.btnDown, "btnDown");
            this.btnDown.Name = "btnDown";
            this.btnDown.UseVisualStyleBackColor = true;
            this.btnDown.Click += new System.EventHandler(this.btnDown_Click);
            //
            // btnOk
            //
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            //
            // btnCancel
            //
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            //
            // CarafeModelListDlg
            //
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnDown);
            this.Controls.Add(this.btnUp);
            this.Controls.Add(this.btnRemove);
            this.Controls.Add(this.btnEdit);
            this.Controls.Add(this.btnAdd);
            this.Controls.Add(this.gridModels);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CarafeModelListDlg";
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.gridModels)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private pwiz.Skyline.Controls.DataGridViewEx gridModels;
        private System.Windows.Forms.DataGridViewTextBoxColumn colName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colInstrument;
        private System.Windows.Forms.DataGridViewTextBoxColumn colGradient;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPrecursorMz;
        private System.Windows.Forms.DataGridViewTextBoxColumn colNce;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFragmentation;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTrainingRuns;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMs2Cosine;
        private System.Windows.Forms.DataGridViewTextBoxColumn colRtR2;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSaved;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.Button btnEdit;
        private System.Windows.Forms.Button btnRemove;
        private System.Windows.Forms.Button btnUp;
        private System.Windows.Forms.Button btnDown;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
    }
}
