using pwiz.Common.Controls;
using pwiz.Topograph.ui.Controls;

namespace pwiz.Topograph.ui.Forms
{
    partial class PeptideAnalysesForm
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
            this.dataGridView = new System.Windows.Forms.DataGridView();
            this.colPeptide = new System.Windows.Forms.DataGridViewLinkColumn();
            this.colStatus = new pwiz.Topograph.ui.Controls.ValidationStatusColumn();
            this.colNote = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colProteinKey = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colProteinDescription = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMaxTracers = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colDataFileCount = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMinScore = new System.Windows.Forms.DataGridViewLinkColumn();
            this.colMaxScore = new System.Windows.Forms.DataGridViewLinkColumn();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.deleteMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.btnAnalyzePeptides = new System.Windows.Forms.Button();
            this.recordNavBar1 = new pwiz.Common.Controls.RecordNavBar();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).BeginInit();
            this.contextMenuStrip1.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // dataGridView
            // 
            this.dataGridView.AllowUserToAddRows = false;
            this.dataGridView.AllowUserToDeleteRows = false;
            this.dataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colPeptide,
            this.colStatus,
            this.colNote,
            this.colProteinKey,
            this.colProteinDescription,
            this.colMaxTracers,
            this.colDataFileCount,
            this.colMinScore,
            this.colMaxScore});
            this.dataGridView.ContextMenuStrip = this.contextMenuStrip1;
            this.dataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView.Location = new System.Drawing.Point(0, 29);
            this.dataGridView.Name = "dataGridView";
            this.dataGridView.Size = new System.Drawing.Size(1251, 235);
            this.dataGridView.TabIndex = 0;
            this.dataGridView.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView_CellEndEdit);
            this.dataGridView.RowHeaderMouseDoubleClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.dataGridView_RowHeaderMouseDoubleClick);
            this.dataGridView.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView_CellContentClick);
            // 
            // colPeptide
            // 
            this.colPeptide.HeaderText = "Peptide";
            this.colPeptide.Name = "colPeptide";
            this.colPeptide.ReadOnly = true;
            this.colPeptide.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.colPeptide.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.colPeptide.Width = 160;
            // 
            // colStatus
            // 
            this.colStatus.DisplayMember = "Display";
            this.colStatus.HeaderText = "Status";
            this.colStatus.Name = "colStatus";
            this.colStatus.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.colStatus.ValueMember = "Value";
            // 
            // colNote
            // 
            this.colNote.HeaderText = "Note";
            this.colNote.Name = "colNote";
            // 
            // colProteinKey
            // 
            this.colProteinKey.HeaderText = "Protein";
            this.colProteinKey.Name = "colProteinKey";
            this.colProteinKey.ReadOnly = true;
            // 
            // colProteinDescription
            // 
            this.colProteinDescription.HeaderText = "Protein Description";
            this.colProteinDescription.Name = "colProteinDescription";
            this.colProteinDescription.ReadOnly = true;
            this.colProteinDescription.Width = 240;
            // 
            // colMaxTracers
            // 
            this.colMaxTracers.HeaderText = "Max Tracers";
            this.colMaxTracers.Name = "colMaxTracers";
            this.colMaxTracers.ReadOnly = true;
            // 
            // colDataFileCount
            // 
            this.colDataFileCount.HeaderText = "# Data Files";
            this.colDataFileCount.Name = "colDataFileCount";
            this.colDataFileCount.ReadOnly = true;
            // 
            // colMinScore
            // 
            this.colMinScore.HeaderText = "Min Score";
            this.colMinScore.Name = "colMinScore";
            this.colMinScore.ReadOnly = true;
            this.colMinScore.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // colMaxScore
            // 
            this.colMaxScore.HeaderText = "Max Score";
            this.colMaxScore.Name = "colMaxScore";
            this.colMaxScore.ReadOnly = true;
            this.colMaxScore.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.deleteMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(117, 26);
            // 
            // deleteMenuItem
            // 
            this.deleteMenuItem.Name = "deleteMenuItem";
            this.deleteMenuItem.Size = new System.Drawing.Size(116, 22);
            this.deleteMenuItem.Text = "Delete...";
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 149F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Controls.Add(this.btnAnalyzePeptides, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.recordNavBar1, 1, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(1251, 29);
            this.tableLayoutPanel1.TabIndex = 1;
            // 
            // btnAnalyzePeptides
            // 
            this.btnAnalyzePeptides.Location = new System.Drawing.Point(3, 3);
            this.btnAnalyzePeptides.Name = "btnAnalyzePeptides";
            this.btnAnalyzePeptides.Size = new System.Drawing.Size(137, 23);
            this.btnAnalyzePeptides.TabIndex = 1;
            this.btnAnalyzePeptides.Text = "Analy&ze Peptides...";
            this.btnAnalyzePeptides.UseVisualStyleBackColor = true;
            this.btnAnalyzePeptides.Click += new System.EventHandler(this.btnAnalyzePeptides_Click);
            // 
            // recordNavBar1
            // 
            this.recordNavBar1.DataGridView = this.dataGridView;
            this.recordNavBar1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.recordNavBar1.Location = new System.Drawing.Point(152, 3);
            this.recordNavBar1.Name = "recordNavBar1";
            this.recordNavBar1.Size = new System.Drawing.Size(1096, 23);
            this.recordNavBar1.TabIndex = 2;
            // 
            // PeptideAnalysesForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1251, 264);
            this.Controls.Add(this.dataGridView);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "PeptideAnalysesForm";
            this.TabText = "PeptideComparisonsForm";
            this.Text = "Peptide Analyses";
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).EndInit();
            this.contextMenuStrip1.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Button btnAnalyzePeptides;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem deleteMenuItem;
        private System.Windows.Forms.DataGridViewLinkColumn colPeptide;
        private ValidationStatusColumn colStatus;
        private System.Windows.Forms.DataGridViewTextBoxColumn colNote;
        private System.Windows.Forms.DataGridViewTextBoxColumn colProteinKey;
        private System.Windows.Forms.DataGridViewTextBoxColumn colProteinDescription;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMaxTracers;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDataFileCount;
        private System.Windows.Forms.DataGridViewLinkColumn colMinScore;
        private System.Windows.Forms.DataGridViewLinkColumn colMaxScore;
        private RecordNavBar recordNavBar1;
    }
}