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
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnOK.Location = new System.Drawing.Point(313, 289);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 3;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnCancel.Location = new System.Drawing.Point(394, 289);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // columnsDataGridView
            // 
            this.columnsDataGridView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.columnsDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.columnsDataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colColumn,
            this.colTransform});
            this.columnsDataGridView.Location = new System.Drawing.Point(11, 72);
            this.columnsDataGridView.Name = "columnsDataGridView";
            this.columnsDataGridView.Size = new System.Drawing.Size(458, 211);
            this.columnsDataGridView.TabIndex = 5;
            this.columnsDataGridView.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.columnsDataGridView_CellEndEdit);
            this.columnsDataGridView.EditingControlShowing += new System.Windows.Forms.DataGridViewEditingControlShowingEventHandler(this.columnsDataGridView_EditingControlShowing);
            // 
            // colColumn
            // 
            this.colColumn.DataPropertyName = "Column";
            this.colColumn.HeaderText = "Column";
            this.colColumn.Name = "colColumn";
            this.colColumn.ReadOnly = true;
            this.colColumn.Width = 200;
            // 
            // colTransform
            // 
            this.colTransform.DataPropertyName = "Transform";
            this.colTransform.DisplayStyleForCurrentCellOnly = true;
            this.colTransform.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.colTransform.HeaderText = "Transform";
            this.colTransform.Name = "colTransform";
            this.colTransform.Width = 200;
            // 
            // lblDistanceMetric
            // 
            this.lblDistanceMetric.AutoSize = true;
            this.lblDistanceMetric.Location = new System.Drawing.Point(12, 11);
            this.lblDistanceMetric.Name = "lblDistanceMetric";
            this.lblDistanceMetric.Size = new System.Drawing.Size(83, 13);
            this.lblDistanceMetric.TabIndex = 6;
            this.lblDistanceMetric.Text = "Distance metric:";
            // 
            // comboDistanceMetric
            // 
            this.comboDistanceMetric.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboDistanceMetric.FormattingEnabled = true;
            this.comboDistanceMetric.Location = new System.Drawing.Point(12, 36);
            this.comboDistanceMetric.Name = "comboDistanceMetric";
            this.comboDistanceMetric.Size = new System.Drawing.Size(280, 21);
            this.comboDistanceMetric.TabIndex = 7;
            // 
            // ClusteringEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(481, 324);
            this.Controls.Add(this.comboDistanceMetric);
            this.Controls.Add(this.lblDistanceMetric);
            this.Controls.Add(this.columnsDataGridView);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.Name = "ClusteringEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Clustering Editor";
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