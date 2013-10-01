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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PasteDlg));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle7 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle8 = new System.Windows.Forms.DataGridViewCellStyle();
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
            resources.ApplyResources(this.tbxFasta, "tbxFasta");
            this.tbxFasta.Name = "tbxFasta";
            this.tbxFasta.TextChanged += new System.EventHandler(this.tbxFasta_TextChanged);
            // 
            // btnInsert
            // 
            resources.ApplyResources(this.btnInsert, "btnInsert");
            this.btnInsert.Name = "btnInsert";
            this.btnInsert.UseVisualStyleBackColor = true;
            this.btnInsert.Click += new System.EventHandler(this.btnInsert_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnValidate
            // 
            resources.ApplyResources(this.btnValidate, "btnValidate");
            this.btnValidate.Name = "btnValidate";
            this.btnValidate.UseVisualStyleBackColor = true;
            this.btnValidate.Click += new System.EventHandler(this.btnValidate_Click);
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPageFasta);
            this.tabControl1.Controls.Add(this.tabPageProteinList);
            this.tabControl1.Controls.Add(this.tabPagePeptideList);
            this.tabControl1.Controls.Add(this.tabPageTransitionList);
            resources.ApplyResources(this.tabControl1, "tabControl1");
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.TabStop = false;
            this.tabControl1.Selecting += new System.Windows.Forms.TabControlCancelEventHandler(this.tabControl1_Selecting);
            // 
            // tabPageFasta
            // 
            this.tabPageFasta.Controls.Add(this.tbxFasta);
            this.tabPageFasta.Controls.Add(this.label1);
            resources.ApplyResources(this.tabPageFasta, "tabPageFasta");
            this.tabPageFasta.Name = "tabPageFasta";
            this.tabPageFasta.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // tabPageProteinList
            // 
            this.tabPageProteinList.Controls.Add(this.gridViewProteins);
            resources.ApplyResources(this.tabPageProteinList, "tabPageProteinList");
            this.tabPageProteinList.Name = "tabPageProteinList";
            this.tabPageProteinList.UseVisualStyleBackColor = true;
            // 
            // gridViewProteins
            // 
            this.gridViewProteins.AllowUserToOrderColumns = true;
            dataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle5.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle5.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle5.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle5.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle5.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle5.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridViewProteins.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle5;
            this.gridViewProteins.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridViewProteins.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colProteinName,
            this.colProteinDescription,
            this.colProteinSequence});
            dataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle6.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle6.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle6.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle6.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle6.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle6.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridViewProteins.DefaultCellStyle = dataGridViewCellStyle6;
            resources.ApplyResources(this.gridViewProteins, "gridViewProteins");
            this.gridViewProteins.Name = "gridViewProteins";
            this.gridViewProteins.CellBeginEdit += new System.Windows.Forms.DataGridViewCellCancelEventHandler(this.gridViewProteins_CellBeginEdit);
            this.gridViewProteins.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.OnCellEndEdit);
            this.gridViewProteins.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.gridViewProteins_CellValueChanged);
            this.gridViewProteins.EditingControlShowing += new System.Windows.Forms.DataGridViewEditingControlShowingEventHandler(this.OnEditingControlShowing);
            this.gridViewProteins.KeyDown += new System.Windows.Forms.KeyEventHandler(this.gridViewProteins_KeyDown);
            // 
            // colProteinName
            // 
            resources.ApplyResources(this.colProteinName, "colProteinName");
            this.colProteinName.Name = "colProteinName";
            // 
            // colProteinDescription
            // 
            resources.ApplyResources(this.colProteinDescription, "colProteinDescription");
            this.colProteinDescription.Name = "colProteinDescription";
            // 
            // colProteinSequence
            // 
            resources.ApplyResources(this.colProteinSequence, "colProteinSequence");
            this.colProteinSequence.Name = "colProteinSequence";
            // 
            // tabPagePeptideList
            // 
            this.tabPagePeptideList.Controls.Add(this.gridViewPeptides);
            resources.ApplyResources(this.tabPagePeptideList, "tabPagePeptideList");
            this.tabPagePeptideList.Name = "tabPagePeptideList";
            this.tabPagePeptideList.UseVisualStyleBackColor = true;
            // 
            // gridViewPeptides
            // 
            this.gridViewPeptides.AllowUserToOrderColumns = true;
            dataGridViewCellStyle7.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle7.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle7.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle7.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle7.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle7.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle7.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridViewPeptides.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle7;
            this.gridViewPeptides.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridViewPeptides.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colPeptideSequence,
            this.colPeptideProtein,
            this.colPeptideProteinDescription});
            dataGridViewCellStyle8.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle8.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle8.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle8.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle8.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle8.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle8.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridViewPeptides.DefaultCellStyle = dataGridViewCellStyle8;
            resources.ApplyResources(this.gridViewPeptides, "gridViewPeptides");
            this.gridViewPeptides.Name = "gridViewPeptides";
            this.gridViewPeptides.CellBeginEdit += new System.Windows.Forms.DataGridViewCellCancelEventHandler(this.gridViewPeptides_CellBeginEdit);
            this.gridViewPeptides.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.OnCellEndEdit);
            this.gridViewPeptides.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.gridViewPeptides_CellValueChanged);
            this.gridViewPeptides.EditingControlShowing += new System.Windows.Forms.DataGridViewEditingControlShowingEventHandler(this.OnEditingControlShowing);
            this.gridViewPeptides.KeyDown += new System.Windows.Forms.KeyEventHandler(this.gridViewPeptides_KeyDown);
            // 
            // colPeptideSequence
            // 
            resources.ApplyResources(this.colPeptideSequence, "colPeptideSequence");
            this.colPeptideSequence.Name = "colPeptideSequence";
            // 
            // colPeptideProtein
            // 
            resources.ApplyResources(this.colPeptideProtein, "colPeptideProtein");
            this.colPeptideProtein.Name = "colPeptideProtein";
            // 
            // colPeptideProteinDescription
            // 
            resources.ApplyResources(this.colPeptideProteinDescription, "colPeptideProteinDescription");
            this.colPeptideProteinDescription.Name = "colPeptideProteinDescription";
            this.colPeptideProteinDescription.ReadOnly = true;
            // 
            // tabPageTransitionList
            // 
            this.tabPageTransitionList.Controls.Add(this.gridViewTransitionList);
            resources.ApplyResources(this.tabPageTransitionList, "tabPageTransitionList");
            this.tabPageTransitionList.Name = "tabPageTransitionList";
            this.tabPageTransitionList.UseVisualStyleBackColor = true;
            // 
            // gridViewTransitionList
            // 
            this.gridViewTransitionList.AllowUserToOrderColumns = true;
            this.gridViewTransitionList.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridViewTransitionList.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colTransitionPeptide,
            this.colTransitionPrecursorMz,
            this.colTransitionProductMz,
            this.colTransitionProteinName,
            this.colTransitionProteinDescription});
            resources.ApplyResources(this.gridViewTransitionList, "gridViewTransitionList");
            this.gridViewTransitionList.Name = "gridViewTransitionList";
            this.gridViewTransitionList.CellBeginEdit += new System.Windows.Forms.DataGridViewCellCancelEventHandler(this.gridViewTransitionList_CellBeginEdit);
            this.gridViewTransitionList.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.OnCellEndEdit);
            this.gridViewTransitionList.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.gridViewTransitionList_CellValueChanged);
            this.gridViewTransitionList.EditingControlShowing += new System.Windows.Forms.DataGridViewEditingControlShowingEventHandler(this.OnEditingControlShowing);
            this.gridViewTransitionList.KeyDown += new System.Windows.Forms.KeyEventHandler(this.gridViewTransitionList_KeyDown);
            // 
            // colTransitionPeptide
            // 
            resources.ApplyResources(this.colTransitionPeptide, "colTransitionPeptide");
            this.colTransitionPeptide.Name = "colTransitionPeptide";
            // 
            // colTransitionPrecursorMz
            // 
            resources.ApplyResources(this.colTransitionPrecursorMz, "colTransitionPrecursorMz");
            this.colTransitionPrecursorMz.Name = "colTransitionPrecursorMz";
            // 
            // colTransitionProductMz
            // 
            resources.ApplyResources(this.colTransitionProductMz, "colTransitionProductMz");
            this.colTransitionProductMz.Name = "colTransitionProductMz";
            // 
            // colTransitionProteinName
            // 
            resources.ApplyResources(this.colTransitionProteinName, "colTransitionProteinName");
            this.colTransitionProteinName.Name = "colTransitionProteinName";
            // 
            // colTransitionProteinDescription
            // 
            resources.ApplyResources(this.colTransitionProteinDescription, "colTransitionProteinDescription");
            this.colTransitionProteinDescription.Name = "colTransitionProteinDescription";
            this.colTransitionProteinDescription.ReadOnly = true;
            // 
            // panelError
            // 
            this.panelError.Controls.Add(this.tbxError);
            resources.ApplyResources(this.panelError, "panelError");
            this.panelError.Name = "panelError";
            // 
            // tbxError
            // 
            resources.ApplyResources(this.tbxError, "tbxError");
            this.tbxError.Name = "tbxError";
            this.tbxError.ReadOnly = true;
            // 
            // panelButtons
            // 
            this.panelButtons.Controls.Add(this.btnValidate);
            this.panelButtons.Controls.Add(this.btnInsert);
            this.panelButtons.Controls.Add(this.btnCancel);
            resources.ApplyResources(this.panelButtons, "panelButtons");
            this.panelButtons.Name = "panelButtons";
            // 
            // PasteDlg
            // 
            this.AcceptButton = this.btnInsert;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.panelButtons);
            this.Controls.Add(this.panelError);
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PasteDlg";
            this.ShowInTaskbar = false;
            this.Load += new System.EventHandler(this.OnLoad);
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