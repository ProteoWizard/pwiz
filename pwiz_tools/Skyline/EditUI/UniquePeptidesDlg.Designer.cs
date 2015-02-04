namespace pwiz.Skyline.EditUI
{
    partial class UniquePeptidesDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UniquePeptidesDlg));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.includeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.excludeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.uniqueOnlyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.excludeBackgroundProteomeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.includeAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.excludeAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.tbxProteinName = new System.Windows.Forms.TextBox();
            this.tbxProteinDescription = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.richTextBoxSequence = new System.Windows.Forms.RichTextBox();
            this.tbxProteinDetails = new System.Windows.Forms.TextBox();
            this.labelProteinDetails = new System.Windows.Forms.Label();
            this.PeptideIncludedColumn = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.PeptideColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            resources.ApplyResources(this.dataGridView1, "dataGridView1");
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridView1.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.PeptideIncludedColumn,
            this.PeptideColumn});
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridView1.DefaultCellStyle = dataGridViewCellStyle2;
            this.dataGridView1.Name = "dataGridView1";
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridView1.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
            this.dataGridView1.RowHeadersVisible = false;
            this.dataGridView1.CurrentCellDirtyStateChanged += new System.EventHandler(this.dataGridView1_CurrentCellDirtyStateChanged);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.includeToolStripMenuItem,
            this.excludeToolStripMenuItem,
            this.toolStripSeparator1,
            this.uniqueOnlyToolStripMenuItem,
            this.excludeBackgroundProteomeToolStripMenuItem,
            this.toolStripSeparator2,
            this.includeAllToolStripMenuItem,
            this.excludeAllToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            resources.ApplyResources(this.contextMenuStrip1, "contextMenuStrip1");
            // 
            // includeToolStripMenuItem
            // 
            this.includeToolStripMenuItem.Name = "includeToolStripMenuItem";
            resources.ApplyResources(this.includeToolStripMenuItem, "includeToolStripMenuItem");
            this.includeToolStripMenuItem.Click += new System.EventHandler(this.includeToolStripMenuItem_Click);
            // 
            // excludeToolStripMenuItem
            // 
            this.excludeToolStripMenuItem.Name = "excludeToolStripMenuItem";
            resources.ApplyResources(this.excludeToolStripMenuItem, "excludeToolStripMenuItem");
            this.excludeToolStripMenuItem.Click += new System.EventHandler(this.excludeToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            resources.ApplyResources(this.toolStripSeparator1, "toolStripSeparator1");
            // 
            // uniqueOnlyToolStripMenuItem
            // 
            this.uniqueOnlyToolStripMenuItem.Name = "uniqueOnlyToolStripMenuItem";
            resources.ApplyResources(this.uniqueOnlyToolStripMenuItem, "uniqueOnlyToolStripMenuItem");
            this.uniqueOnlyToolStripMenuItem.Click += new System.EventHandler(this.uniqueOnlyToolStripMenuItem_Click);
            // 
            // excludeBackgroundProteomeToolStripMenuItem
            // 
            this.excludeBackgroundProteomeToolStripMenuItem.Name = "excludeBackgroundProteomeToolStripMenuItem";
            resources.ApplyResources(this.excludeBackgroundProteomeToolStripMenuItem, "excludeBackgroundProteomeToolStripMenuItem");
            this.excludeBackgroundProteomeToolStripMenuItem.Click += new System.EventHandler(this.excludeBackgroundProteomeToolStripMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            resources.ApplyResources(this.toolStripSeparator2, "toolStripSeparator2");
            // 
            // includeAllToolStripMenuItem
            // 
            this.includeAllToolStripMenuItem.Name = "includeAllToolStripMenuItem";
            resources.ApplyResources(this.includeAllToolStripMenuItem, "includeAllToolStripMenuItem");
            this.includeAllToolStripMenuItem.Click += new System.EventHandler(this.includeAllToolStripMenuItem_Click);
            // 
            // excludeAllToolStripMenuItem
            // 
            this.excludeAllToolStripMenuItem.Name = "excludeAllToolStripMenuItem";
            resources.ApplyResources(this.excludeAllToolStripMenuItem, "excludeAllToolStripMenuItem");
            this.excludeAllToolStripMenuItem.Click += new System.EventHandler(this.excludeAllToolStripMenuItem_Click);
            // 
            // btnOK
            // 
            resources.ApplyResources(this.btnOK, "btnOK");
            this.btnOK.Name = "btnOK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // tbxProteinName
            // 
            resources.ApplyResources(this.tbxProteinName, "tbxProteinName");
            this.tbxProteinName.Name = "tbxProteinName";
            this.tbxProteinName.ReadOnly = true;
            // 
            // tbxProteinDescription
            // 
            resources.ApplyResources(this.tbxProteinDescription, "tbxProteinDescription");
            this.tbxProteinDescription.Name = "tbxProteinDescription";
            this.tbxProteinDescription.ReadOnly = true;
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // richTextBoxSequence
            // 
            resources.ApplyResources(this.richTextBoxSequence, "richTextBoxSequence");
            this.richTextBoxSequence.Name = "richTextBoxSequence";
            this.richTextBoxSequence.ReadOnly = true;
            // 
            // tbxProteinDetails
            // 
            resources.ApplyResources(this.tbxProteinDetails, "tbxProteinDetails");
            this.tbxProteinDetails.Name = "tbxProteinDetails";
            this.tbxProteinDetails.ReadOnly = true;
            // 
            // labelProteinDetails
            // 
            resources.ApplyResources(this.labelProteinDetails, "labelProteinDetails");
            this.labelProteinDetails.Name = "labelProteinDetails";
            // 
            // PeptideIncludedColumn
            // 
            resources.ApplyResources(this.PeptideIncludedColumn, "PeptideIncludedColumn");
            this.PeptideIncludedColumn.Name = "PeptideIncludedColumn";
            this.PeptideIncludedColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // PeptideColumn
            // 
            resources.ApplyResources(this.PeptideColumn, "PeptideColumn");
            this.PeptideColumn.Name = "PeptideColumn";
            // 
            // UniquePeptidesDlg
            // 
            this.AcceptButton = this.btnOK;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ContextMenuStrip = this.contextMenuStrip1;
            this.Controls.Add(this.tbxProteinDetails);
            this.Controls.Add(this.labelProteinDetails);
            this.Controls.Add(this.richTextBoxSequence);
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.tbxProteinDescription);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.tbxProteinName);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.label1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "UniquePeptidesDlg";
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem includeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem excludeToolStripMenuItem;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbxProteinName;
        private System.Windows.Forms.TextBox tbxProteinDescription;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.RichTextBox richTextBoxSequence;
        private System.Windows.Forms.TextBox tbxProteinDetails;
        private System.Windows.Forms.Label labelProteinDetails;
        private System.Windows.Forms.ToolStripMenuItem uniqueOnlyToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem excludeBackgroundProteomeToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem includeAllToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem excludeAllToolStripMenuItem;
        private System.Windows.Forms.DataGridViewCheckBoxColumn PeptideIncludedColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn PeptideColumn;
    }
}