using pwiz.Skyline.Controls;

namespace pwiz.Skyline.EditUI
{
    partial class PasteDlg
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            this.tbxFasta = new System.Windows.Forms.TextBox();
            this.btnInsert = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnValidate = new System.Windows.Forms.Button();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPageFasta = new System.Windows.Forms.TabPage();
            this.label1 = new System.Windows.Forms.Label();
            this.tabPageProteinList = new System.Windows.Forms.TabPage();
            this.gridViewProteins = new pwiz.Skyline.Controls.DataGridViewEx();
            this.colProteinName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colProteinDescription = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colProteinSequence = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tabPagePeptideList = new System.Windows.Forms.TabPage();
            this.gridViewPeptides = new pwiz.Skyline.Controls.DataGridViewEx();
            this.colPeptideSequence = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPeptideProtein = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPeptideProteinDescription = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tabPageTransitionList = new System.Windows.Forms.TabPage();
            this.gridViewTransitionList = new pwiz.Skyline.Controls.DataGridViewEx();
            this.colTransitionPeptide = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTransitionPrecursorMz = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTransitionProductMz = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTransitionProteinName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTransitionProteinDescription = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panelError = new System.Windows.Forms.Panel();
            this.tbxError = new System.Windows.Forms.TextBox();
            this.panelButtons = new System.Windows.Forms.Panel();
            this.tabControl1.SuspendLayout();
            this.tabPageFasta.SuspendLayout();
            this.tabPageProteinList.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewProteins)).BeginInit();
            this.tabPagePeptideList.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewPeptides)).BeginInit();
            this.tabPageTransitionList.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewTransitionList)).BeginInit();
            this.panelError.SuspendLayout();
            this.panelButtons.SuspendLayout();
            this.SuspendLayout();
            // 
            // tbxFasta
            // 
            this.tbxFasta.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxFasta.Location = new System.Drawing.Point(3, 16);
            this.tbxFasta.MaxLength = 2147483647;
            this.tbxFasta.Multiline = true;
            this.tbxFasta.Name = "tbxFasta";
            this.tbxFasta.Size = new System.Drawing.Size(791, 384);
            this.tbxFasta.TabIndex = 1;
            this.tbxFasta.TextChanged += new System.EventHandler(this.tbxFasta_TextChanged);
            // 
            // btnInsert
            // 
            this.btnInsert.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnInsert.Location = new System.Drawing.Point(637, 9);
            this.btnInsert.Name = "btnInsert";
            this.btnInsert.Size = new System.Drawing.Size(75, 23);
            this.btnInsert.TabIndex = 1;
            this.btnInsert.Text = "&Insert";
            this.btnInsert.UseVisualStyleBackColor = true;
            this.btnInsert.Click += new System.EventHandler(this.btnInsert_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(718, 9);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "&Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnValidate
            // 
            this.btnValidate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnValidate.Location = new System.Drawing.Point(538, 9);
            this.btnValidate.Name = "btnValidate";
            this.btnValidate.Size = new System.Drawing.Size(93, 23);
            this.btnValidate.TabIndex = 0;
            this.btnValidate.Text = "Check for &Errors";
            this.btnValidate.UseVisualStyleBackColor = true;
            this.btnValidate.Click += new System.EventHandler(this.btnValidate_Click);
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPageFasta);
            this.tabControl1.Controls.Add(this.tabPageProteinList);
            this.tabControl1.Controls.Add(this.tabPagePeptideList);
            this.tabControl1.Controls.Add(this.tabPageTransitionList);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 45);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(805, 429);
            this.tabControl1.TabIndex = 0;
            this.tabControl1.TabStop = false;
            this.tabControl1.Selecting += new System.Windows.Forms.TabControlCancelEventHandler(this.tabControl1_Selecting);
            // 
            // tabPageFasta
            // 
            this.tabPageFasta.Controls.Add(this.tbxFasta);
            this.tabPageFasta.Controls.Add(this.label1);
            this.tabPageFasta.Location = new System.Drawing.Point(4, 22);
            this.tabPageFasta.Name = "tabPageFasta";
            this.tabPageFasta.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageFasta.Size = new System.Drawing.Size(797, 403);
            this.tabPageFasta.TabIndex = 0;
            this.tabPageFasta.Text = "Fasta";
            this.tabPageFasta.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Top;
            this.label1.Location = new System.Drawing.Point(3, 3);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(482, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "FASTA records begin with \'>\' and have the protein name followed by the optional p" +
                "rotein description. ";
            // 
            // tabPageProteinList
            // 
            this.tabPageProteinList.Controls.Add(this.gridViewProteins);
            this.tabPageProteinList.Location = new System.Drawing.Point(4, 22);
            this.tabPageProteinList.Name = "tabPageProteinList";
            this.tabPageProteinList.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageProteinList.Size = new System.Drawing.Size(797, 403);
            this.tabPageProteinList.TabIndex = 1;
            this.tabPageProteinList.Text = "Protein List";
            this.tabPageProteinList.UseVisualStyleBackColor = true;
            // 
            // gridViewProteins
            // 
            this.gridViewProteins.AllowUserToOrderColumns = true;
            this.gridViewProteins.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridViewProteins.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.gridViewProteins.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridViewProteins.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colProteinName,
            this.colProteinDescription,
            this.colProteinSequence});
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridViewProteins.DefaultCellStyle = dataGridViewCellStyle2;
            this.gridViewProteins.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridViewProteins.Location = new System.Drawing.Point(3, 3);
            this.gridViewProteins.Name = "gridViewProteins";
            this.gridViewProteins.Size = new System.Drawing.Size(791, 397);
            this.gridViewProteins.TabIndex = 0;
            this.gridViewProteins.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.gridViewProteins_CellValueChanged);
            this.gridViewProteins.CellBeginEdit += new System.Windows.Forms.DataGridViewCellCancelEventHandler(this.gridViewProteins_CellBeginEdit);
            this.gridViewProteins.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.OnCellEndEdit);
            this.gridViewProteins.EditingControlShowing += new System.Windows.Forms.DataGridViewEditingControlShowingEventHandler(this.OnEditingControlShowing);
            this.gridViewProteins.KeyDown += new System.Windows.Forms.KeyEventHandler(this.gridViewProteins_KeyDown);
            // 
            // colProteinName
            // 
            this.colProteinName.HeaderText = "Name";
            this.colProteinName.Name = "colProteinName";
            // 
            // colProteinDescription
            // 
            this.colProteinDescription.HeaderText = "Description";
            this.colProteinDescription.Name = "colProteinDescription";
            // 
            // colProteinSequence
            // 
            this.colProteinSequence.HeaderText = "Sequence";
            this.colProteinSequence.Name = "colProteinSequence";
            // 
            // tabPagePeptideList
            // 
            this.tabPagePeptideList.Controls.Add(this.gridViewPeptides);
            this.tabPagePeptideList.Location = new System.Drawing.Point(4, 22);
            this.tabPagePeptideList.Name = "tabPagePeptideList";
            this.tabPagePeptideList.Padding = new System.Windows.Forms.Padding(3);
            this.tabPagePeptideList.Size = new System.Drawing.Size(797, 403);
            this.tabPagePeptideList.TabIndex = 2;
            this.tabPagePeptideList.Text = "Peptide List";
            this.tabPagePeptideList.UseVisualStyleBackColor = true;
            // 
            // gridViewPeptides
            // 
            this.gridViewPeptides.AllowUserToOrderColumns = true;
            this.gridViewPeptides.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridViewPeptides.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle3;
            this.gridViewPeptides.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridViewPeptides.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colPeptideSequence,
            this.colPeptideProtein,
            this.colPeptideProteinDescription});
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle4.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle4.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle4.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle4.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridViewPeptides.DefaultCellStyle = dataGridViewCellStyle4;
            this.gridViewPeptides.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridViewPeptides.Location = new System.Drawing.Point(3, 3);
            this.gridViewPeptides.Name = "gridViewPeptides";
            this.gridViewPeptides.Size = new System.Drawing.Size(791, 397);
            this.gridViewPeptides.TabIndex = 0;
            this.gridViewPeptides.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.gridViewPeptides_CellValueChanged);
            this.gridViewPeptides.CellBeginEdit += new System.Windows.Forms.DataGridViewCellCancelEventHandler(this.gridViewPeptides_CellBeginEdit);
            this.gridViewPeptides.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.OnCellEndEdit);
            this.gridViewPeptides.EditingControlShowing += new System.Windows.Forms.DataGridViewEditingControlShowingEventHandler(this.OnEditingControlShowing);
            this.gridViewPeptides.KeyDown += new System.Windows.Forms.KeyEventHandler(this.gridViewPeptides_KeyDown);
            // 
            // colPeptideSequence
            // 
            this.colPeptideSequence.HeaderText = "Peptide Sequence";
            this.colPeptideSequence.Name = "colPeptideSequence";
            // 
            // colPeptideProtein
            // 
            this.colPeptideProtein.HeaderText = "Protein Name";
            this.colPeptideProtein.Name = "colPeptideProtein";
            // 
            // colPeptideProteinDescription
            // 
            this.colPeptideProteinDescription.HeaderText = "Protein Description";
            this.colPeptideProteinDescription.Name = "colPeptideProteinDescription";
            this.colPeptideProteinDescription.ReadOnly = true;
            // 
            // tabPageTransitionList
            // 
            this.tabPageTransitionList.Controls.Add(this.gridViewTransitionList);
            this.tabPageTransitionList.Location = new System.Drawing.Point(4, 22);
            this.tabPageTransitionList.Name = "tabPageTransitionList";
            this.tabPageTransitionList.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageTransitionList.Size = new System.Drawing.Size(797, 403);
            this.tabPageTransitionList.TabIndex = 3;
            this.tabPageTransitionList.Text = "Transition List";
            this.tabPageTransitionList.UseVisualStyleBackColor = true;
            // 
            // gridViewTransitionList
            // 
            this.gridViewTransitionList.AllowUserToOrderColumns = true;
            this.gridViewTransitionList.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridViewTransitionList.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridViewTransitionList.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colTransitionPeptide,
            this.colTransitionPrecursorMz,
            this.colTransitionProductMz,
            this.colTransitionProteinName,
            this.colTransitionProteinDescription});
            this.gridViewTransitionList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridViewTransitionList.Location = new System.Drawing.Point(3, 3);
            this.gridViewTransitionList.Name = "gridViewTransitionList";
            this.gridViewTransitionList.Size = new System.Drawing.Size(791, 397);
            this.gridViewTransitionList.TabIndex = 0;
            this.gridViewTransitionList.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.gridViewTransitionList_CellValueChanged);
            this.gridViewTransitionList.CellBeginEdit += new System.Windows.Forms.DataGridViewCellCancelEventHandler(this.gridViewTransitionList_CellBeginEdit);
            this.gridViewTransitionList.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.OnCellEndEdit);
            this.gridViewTransitionList.EditingControlShowing += new System.Windows.Forms.DataGridViewEditingControlShowingEventHandler(this.OnEditingControlShowing);
            this.gridViewTransitionList.KeyDown += new System.Windows.Forms.KeyEventHandler(this.gridViewTransitionList_KeyDown);
            // 
            // colTransitionPeptide
            // 
            this.colTransitionPeptide.HeaderText = "Peptide";
            this.colTransitionPeptide.Name = "colTransitionPeptide";
            // 
            // colTransitionPrecursorMz
            // 
            this.colTransitionPrecursorMz.HeaderText = "Precursor m/z";
            this.colTransitionPrecursorMz.Name = "colTransitionPrecursorMz";
            // 
            // colTransitionProductMz
            // 
            this.colTransitionProductMz.HeaderText = "Product m/z";
            this.colTransitionProductMz.Name = "colTransitionProductMz";
            // 
            // colTransitionProteinName
            // 
            this.colTransitionProteinName.HeaderText = "Protein Name";
            this.colTransitionProteinName.Name = "colTransitionProteinName";
            // 
            // colTransitionProteinDescription
            // 
            this.colTransitionProteinDescription.HeaderText = "Protein Description";
            this.colTransitionProteinDescription.Name = "colTransitionProteinDescription";
            this.colTransitionProteinDescription.ReadOnly = true;
            // 
            // panelError
            // 
            this.panelError.Controls.Add(this.tbxError);
            this.panelError.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelError.Location = new System.Drawing.Point(0, 0);
            this.panelError.Name = "panelError";
            this.panelError.Size = new System.Drawing.Size(805, 45);
            this.panelError.TabIndex = 9;
            this.panelError.Visible = false;
            // 
            // tbxError
            // 
            this.tbxError.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxError.Location = new System.Drawing.Point(0, 0);
            this.tbxError.Multiline = true;
            this.tbxError.Name = "tbxError";
            this.tbxError.ReadOnly = true;
            this.tbxError.Size = new System.Drawing.Size(805, 45);
            this.tbxError.TabIndex = 0;
            // 
            // panelButtons
            // 
            this.panelButtons.Controls.Add(this.btnValidate);
            this.panelButtons.Controls.Add(this.btnInsert);
            this.panelButtons.Controls.Add(this.btnCancel);
            this.panelButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelButtons.Location = new System.Drawing.Point(0, 474);
            this.panelButtons.Name = "panelButtons";
            this.panelButtons.Size = new System.Drawing.Size(805, 44);
            this.panelButtons.TabIndex = 1;
            // 
            // PasteDlg
            // 
            this.AcceptButton = this.btnInsert;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(805, 518);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.panelButtons);
            this.Controls.Add(this.panelError);
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PasteDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Insert";
            this.tabControl1.ResumeLayout(false);
            this.tabPageFasta.ResumeLayout(false);
            this.tabPageFasta.PerformLayout();
            this.tabPageProteinList.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridViewProteins)).EndInit();
            this.tabPagePeptideList.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridViewPeptides)).EndInit();
            this.tabPageTransitionList.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridViewTransitionList)).EndInit();
            this.panelError.ResumeLayout(false);
            this.panelError.PerformLayout();
            this.panelButtons.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TextBox tbxFasta;
        private System.Windows.Forms.Button btnInsert;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnValidate;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPageFasta;
        private System.Windows.Forms.TabPage tabPageProteinList;
        private System.Windows.Forms.TabPage tabPagePeptideList;
        private DataGridViewEx gridViewProteins;
        private DataGridViewEx gridViewPeptides;
        private System.Windows.Forms.Panel panelError;
        private System.Windows.Forms.Panel panelButtons;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbxError;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPeptideSequence;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPeptideProtein;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPeptideProteinDescription;
        private System.Windows.Forms.TabPage tabPageTransitionList;
        private DataGridViewEx gridViewTransitionList;
        private System.Windows.Forms.DataGridViewTextBoxColumn colProteinName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colProteinDescription;
        private System.Windows.Forms.DataGridViewTextBoxColumn colProteinSequence;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTransitionPeptide;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTransitionPrecursorMz;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTransitionProductMz;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTransitionProteinName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTransitionProteinDescription;
    }
}