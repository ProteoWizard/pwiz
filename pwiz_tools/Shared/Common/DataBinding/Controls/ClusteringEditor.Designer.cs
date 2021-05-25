namespace pwiz.Common.DataBinding.Controls
{
    partial class ClusteringEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ClusteringEditor));
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.columnsDataGridView = new pwiz.Common.Controls.CommonDataGridView();
            this.colColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTransform = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.lblDistanceMetric = new System.Windows.Forms.Label();
            this.comboDistanceMetric = new System.Windows.Forms.ComboBox();
            ((System.ComponentModel.ISupportInitialize)(this.columnsDataGridView)).BeginInit();
            this.SuspendLayout();
            // 
            // btnOK
            // 
            resources.ApplyResources(this.btnOK, "btnOK");
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.Name = "btnOK";
            this.btnOK.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // columnsDataGridView
            // 
            resources.ApplyResources(this.columnsDataGridView, "columnsDataGridView");
            this.columnsDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.columnsDataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colColumn,
            this.colTransform});
            this.columnsDataGridView.Name = "columnsDataGridView";
            this.columnsDataGridView.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.columnsDataGridView_CellEndEdit);
            this.columnsDataGridView.EditingControlShowing += new System.Windows.Forms.DataGridViewEditingControlShowingEventHandler(this.columnsDataGridView_EditingControlShowing);
            // 
            // colColumn
            // 
            this.colColumn.DataPropertyName = "Column";
            resources.ApplyResources(this.colColumn, "colColumn");
            this.colColumn.Name = "colColumn";
            this.colColumn.ReadOnly = true;
            // 
            // colTransform
            // 
            this.colTransform.DataPropertyName = "Transform";
            this.colTransform.DisplayStyleForCurrentCellOnly = true;
            this.colTransform.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            resources.ApplyResources(this.colTransform, "colTransform");
            this.colTransform.Name = "colTransform";
            // 
            // lblDistanceMetric
            // 
            resources.ApplyResources(this.lblDistanceMetric, "lblDistanceMetric");
            this.lblDistanceMetric.Name = "lblDistanceMetric";
            // 
            // comboDistanceMetric
            // 
            this.comboDistanceMetric.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboDistanceMetric.FormattingEnabled = true;
            resources.ApplyResources(this.comboDistanceMetric, "comboDistanceMetric");
            this.comboDistanceMetric.Name = "comboDistanceMetric";
            // 
            // ClusteringEditor
            // 
            this.AcceptButton = this.btnOK;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.comboDistanceMetric);
            this.Controls.Add(this.lblDistanceMetric);
            this.Controls.Add(this.columnsDataGridView);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ClusteringEditor";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.columnsDataGridView)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private Common.Controls.CommonDataGridView columnsDataGridView;
        private System.Windows.Forms.Label lblDistanceMetric;
        private System.Windows.Forms.ComboBox comboDistanceMetric;
        private System.Windows.Forms.DataGridViewTextBoxColumn colColumn;
        private System.Windows.Forms.DataGridViewComboBoxColumn colTransform;
    }
}