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
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.deleteMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.btnAnalyzePeptides = new System.Windows.Forms.Button();
            this.colPeptide = new System.Windows.Forms.DataGridViewLinkColumn();
            this.colStatus = new pwiz.Topograph.ui.Controls.ValidationStatusColumn();
            this.colNote = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colProtein = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colProteinDescription = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMaxTracers = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMinScore = new System.Windows.Forms.DataGridViewLinkColumn();
            this.colMaxScore = new System.Windows.Forms.DataGridViewLinkColumn();
            this.colHalfLifePrecursorEnrichment = new System.Windows.Forms.DataGridViewLinkColumn();
            this.colHalfLifeTracerCount = new System.Windows.Forms.DataGridViewLinkColumn();
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
            this.colProtein,
            this.colProteinDescription,
            this.colMaxTracers,
            this.colMinScore,
            this.colMaxScore,
            this.colHalfLifePrecursorEnrichment,
            this.colHalfLifeTracerCount});
            this.dataGridView.ContextMenuStrip = this.contextMenuStrip1;
            this.dataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView.Location = new System.Drawing.Point(0, 29);
            this.dataGridView.Name = "dataGridView";
            this.dataGridView.Size = new System.Drawing.Size(949, 235);
            this.dataGridView.TabIndex = 0;
            this.dataGridView.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView_CellEndEdit);
            this.dataGridView.RowHeaderMouseDoubleClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.dataGridView_RowHeaderMouseDoubleClick);
            this.dataGridView.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView_CellContentClick);
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
            this.tableLayoutPanel1.ColumnCount = 4;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.Controls.Add(this.btnAnalyzePeptides, 0, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(949, 29);
            this.tableLayoutPanel1.TabIndex = 1;
            // 
            // btnAnalyzePeptides
            // 
            this.btnAnalyzePeptides.Location = new System.Drawing.Point(3, 3);
            this.btnAnalyzePeptides.Name = "btnAnalyzePeptides";
            this.btnAnalyzePeptides.Size = new System.Drawing.Size(137, 23);
            this.btnAnalyzePeptides.TabIndex = 1;
            this.btnAnalyzePeptides.Text = "Analyze Peptides...";
            this.btnAnalyzePeptides.UseVisualStyleBackColor = true;
            this.btnAnalyzePeptides.Click += new System.EventHandler(this.btnAnalyzePeptides_Click);
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
            // colProtein
            // 
            this.colProtein.HeaderText = "Protein";
            this.colProtein.Name = "colProtein";
            this.colProtein.ReadOnly = true;
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
            // colHalfLifePrecursorEnrichment
            // 
            this.colHalfLifePrecursorEnrichment.HeaderText = "Half Life (% Enrichment)";
            this.colHalfLifePrecursorEnrichment.Name = "colHalfLifePrecursorEnrichment";
            this.colHalfLifePrecursorEnrichment.ReadOnly = true;
            this.colHalfLifePrecursorEnrichment.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.colHalfLifePrecursorEnrichment.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // colHalfLifeTracerCount
            // 
            this.colHalfLifeTracerCount.HeaderText = "Half Life (# Tracers)";
            this.colHalfLifeTracerCount.Name = "colHalfLifeTracerCount";
            this.colHalfLifeTracerCount.ReadOnly = true;
            this.colHalfLifeTracerCount.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.colHalfLifeTracerCount.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // PeptideAnalysesForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(949, 264);
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
        private System.Windows.Forms.DataGridViewTextBoxColumn colProtein;
        private System.Windows.Forms.DataGridViewTextBoxColumn colProteinDescription;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMaxTracers;
        private System.Windows.Forms.DataGridViewLinkColumn colMinScore;
        private System.Windows.Forms.DataGridViewLinkColumn colMaxScore;
        private System.Windows.Forms.DataGridViewLinkColumn colHalfLifePrecursorEnrichment;
        private System.Windows.Forms.DataGridViewLinkColumn colHalfLifeTracerCount;
    }
}