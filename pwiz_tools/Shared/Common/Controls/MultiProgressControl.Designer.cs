namespace pwiz.Common.Controls
{
    partial class MultiProgressControl
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            this.ProgressSplit = new System.Windows.Forms.SplitContainer();
            this.progressGridView = new System.Windows.Forms.DataGridView();
            this.NameColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ProgressColumn = new CustomProgressCell.DataGridViewProgressColumn();
            this.progressLogTextBox = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)(this.ProgressSplit)).BeginInit();
            this.ProgressSplit.Panel1.SuspendLayout();
            this.ProgressSplit.Panel2.SuspendLayout();
            this.ProgressSplit.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.progressGridView)).BeginInit();
            this.SuspendLayout();
            // 
            // ProgressSplit
            // 
            this.ProgressSplit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ProgressSplit.Location = new System.Drawing.Point(0, 0);
            this.ProgressSplit.Name = "ProgressSplit";
            this.ProgressSplit.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // ProgressSplit.Panel1
            // 
            this.ProgressSplit.Panel1.Controls.Add(this.progressGridView);
            this.ProgressSplit.Panel1MinSize = 45;
            // 
            // ProgressSplit.Panel2
            // 
            this.ProgressSplit.Panel2.Controls.Add(this.progressLogTextBox);
            this.ProgressSplit.Size = new System.Drawing.Size(1041, 670);
            this.ProgressSplit.SplitterDistance = 491;
            this.ProgressSplit.TabIndex = 1;
            // 
            // progressGridView
            // 
            this.progressGridView.AllowUserToAddRows = false;
            this.progressGridView.AllowUserToDeleteRows = false;
            this.progressGridView.AllowUserToResizeRows = false;
            this.progressGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.progressGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.NameColumn,
            this.ProgressColumn});
            this.progressGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.progressGridView.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.progressGridView.Location = new System.Drawing.Point(0, 0);
            this.progressGridView.MultiSelect = false;
            this.progressGridView.Name = "progressGridView";
            this.progressGridView.RowHeadersVisible = false;
            this.progressGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.progressGridView.Size = new System.Drawing.Size(1041, 491);
            this.progressGridView.TabIndex = 0;
            // 
            // NameColumn
            // 
            this.NameColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.NameColumn.HeaderText = "Name";
            this.NameColumn.Name = "NameColumn";
            // 
            // ProgressColumn
            // 
            this.ProgressColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle1.ForeColor = System.Drawing.Color.Black;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.Color.Black;
            this.ProgressColumn.DefaultCellStyle = dataGridViewCellStyle1;
            this.ProgressColumn.FillWeight = 60F;
            this.ProgressColumn.HeaderText = "Progress";
            this.ProgressColumn.MinimumWidth = 50;
            this.ProgressColumn.Name = "ProgressColumn";
            // 
            // progressLogTextBox
            // 
            this.progressLogTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.progressLogTextBox.Location = new System.Drawing.Point(0, 0);
            this.progressLogTextBox.Multiline = true;
            this.progressLogTextBox.Name = "progressLogTextBox";
            this.progressLogTextBox.ReadOnly = true;
            this.progressLogTextBox.Size = new System.Drawing.Size(1041, 175);
            this.progressLogTextBox.TabIndex = 0;
            // 
            // MultiProgressControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.ProgressSplit);
            this.Name = "MultiProgressControl";
            this.Size = new System.Drawing.Size(1041, 670);
            this.ProgressSplit.Panel1.ResumeLayout(false);
            this.ProgressSplit.Panel2.ResumeLayout(false);
            this.ProgressSplit.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.ProgressSplit)).EndInit();
            this.ProgressSplit.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.progressGridView)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.DataGridView progressGridView;
        private System.Windows.Forms.DataGridViewTextBoxColumn NameColumn;
        private CustomProgressCell.DataGridViewProgressColumn ProgressColumn;
        private System.Windows.Forms.TextBox progressLogTextBox;
        protected System.Windows.Forms.SplitContainer ProgressSplit;
    }
}
