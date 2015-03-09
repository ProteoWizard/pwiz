namespace pwiz.Skyline.SettingsUI.IonMobility
{
    partial class EditIonMobilityLibraryDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditIonMobilityLibraryDlg));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            this.label1 = new System.Windows.Forms.Label();
            this.textLibraryName = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.textDatabase = new System.Windows.Forms.TextBox();
            this.btnBrowseDb = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnImportFromLibrary = new System.Windows.Forms.Button();
            this.labelNumPeptides = new System.Windows.Forms.Label();
            this.btnCreateDb = new System.Windows.Forms.Button();
            this.bindingSourceLibrary = new System.Windows.Forms.BindingSource(this.components);
            this.bindingSourceStandard = new System.Windows.Forms.BindingSource(this.components);
            this.contextMenuAdd = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.addResultsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addSpectralLibraryContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addIRTDatabaseContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.label4 = new System.Windows.Forms.Label();
            this.gridViewMeasuredPeptides = new pwiz.Skyline.Controls.DataGridViewEx();
            this.toolTipImportBtn = new System.Windows.Forms.ToolTip(this.components);
            this.toolTipMeasuredPeptidesGrid = new System.Windows.Forms.ToolTip(this.components);
            this.columnLibrarySequence = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnLibraryCollisionalCrossSection = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnLibraryHighEnergyDriftTimeOffsetMsec = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceLibrary)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceStandard)).BeginInit();
            this.contextMenuAdd.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewMeasuredPeptides)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // textLibraryName
            // 
            resources.ApplyResources(this.textLibraryName, "textLibraryName");
            this.textLibraryName.Name = "textLibraryName";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // textDatabase
            // 
            resources.ApplyResources(this.textDatabase, "textDatabase");
            this.textDatabase.Name = "textDatabase";
            // 
            // btnBrowseDb
            // 
            resources.ApplyResources(this.btnBrowseDb, "btnBrowseDb");
            this.btnBrowseDb.Name = "btnBrowseDb";
            this.btnBrowseDb.UseVisualStyleBackColor = true;
            this.btnBrowseDb.Click += new System.EventHandler(this.btnBrowseDb_Click);
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
            // btnImportFromLibrary
            // 
            resources.ApplyResources(this.btnImportFromLibrary, "btnImportFromLibrary");
            this.btnImportFromLibrary.Name = "btnImportFromLibrary";
            this.toolTipImportBtn.SetToolTip(this.btnImportFromLibrary, resources.GetString("btnImportFromLibrary.ToolTip"));
            this.btnImportFromLibrary.UseVisualStyleBackColor = true;
            this.btnImportFromLibrary.Click += new System.EventHandler(this.btnImportFromLibrary_Click);
            // 
            // labelNumPeptides
            // 
            resources.ApplyResources(this.labelNumPeptides, "labelNumPeptides");
            this.labelNumPeptides.Name = "labelNumPeptides";
            // 
            // btnCreateDb
            // 
            resources.ApplyResources(this.btnCreateDb, "btnCreateDb");
            this.btnCreateDb.Name = "btnCreateDb";
            this.btnCreateDb.UseVisualStyleBackColor = true;
            this.btnCreateDb.Click += new System.EventHandler(this.btnCreateDb_Click);
            // 
            // contextMenuAdd
            // 
            this.contextMenuAdd.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addResultsContextMenuItem,
            this.addSpectralLibraryContextMenuItem,
            this.addIRTDatabaseContextMenuItem});
            this.contextMenuAdd.Name = "contextMenuAdd";
            resources.ApplyResources(this.contextMenuAdd, "contextMenuAdd");
            // 
            // addResultsContextMenuItem
            // 
            this.addResultsContextMenuItem.Name = "addResultsContextMenuItem";
            resources.ApplyResources(this.addResultsContextMenuItem, "addResultsContextMenuItem");
            // 
            // addSpectralLibraryContextMenuItem
            // 
            this.addSpectralLibraryContextMenuItem.Name = "addSpectralLibraryContextMenuItem";
            resources.ApplyResources(this.addSpectralLibraryContextMenuItem, "addSpectralLibraryContextMenuItem");
            // 
            // addIRTDatabaseContextMenuItem
            // 
            this.addIRTDatabaseContextMenuItem.Name = "addIRTDatabaseContextMenuItem";
            resources.ApplyResources(this.addIRTDatabaseContextMenuItem, "addIRTDatabaseContextMenuItem");
            this.addIRTDatabaseContextMenuItem.Click += new System.EventHandler(this.addIonMobilityLibraryContextMenuItem_Click);
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // gridViewMeasuredPeptides
            // 
            resources.ApplyResources(this.gridViewMeasuredPeptides, "gridViewMeasuredPeptides");
            this.gridViewMeasuredPeptides.AutoGenerateColumns = false;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridViewMeasuredPeptides.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.gridViewMeasuredPeptides.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridViewMeasuredPeptides.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.columnLibrarySequence,
            this.columnLibraryCollisionalCrossSection,
            this.columnLibraryHighEnergyDriftTimeOffsetMsec});
            this.gridViewMeasuredPeptides.DataSource = this.bindingSourceLibrary;
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle4.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle4.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle4.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle4.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridViewMeasuredPeptides.DefaultCellStyle = dataGridViewCellStyle4;
            this.gridViewMeasuredPeptides.Name = "gridViewMeasuredPeptides";
            this.toolTipMeasuredPeptidesGrid.SetToolTip(this.gridViewMeasuredPeptides, resources.GetString("gridViewMeasuredPeptides.ToolTip"));
            this.toolTipImportBtn.SetToolTip(this.gridViewMeasuredPeptides, resources.GetString("gridViewMeasuredPeptides.ToolTip1"));
            this.gridViewMeasuredPeptides.RowsAdded += new System.Windows.Forms.DataGridViewRowsAddedEventHandler(this.gridViewLibrary_RowsAdded);
            this.gridViewMeasuredPeptides.RowsRemoved += new System.Windows.Forms.DataGridViewRowsRemovedEventHandler(this.gridViewLibrary_RowsRemoved);
            // 
            // columnLibrarySequence
            // 
            this.columnLibrarySequence.DataPropertyName = "PeptideModSeq";
            dataGridViewCellStyle2.NullValue = null;
            this.columnLibrarySequence.DefaultCellStyle = dataGridViewCellStyle2;
            resources.ApplyResources(this.columnLibrarySequence, "columnLibrarySequence");
            this.columnLibrarySequence.Name = "columnLibrarySequence";
            // 
            // columnLibraryCollisionalCrossSection
            // 
            this.columnLibraryCollisionalCrossSection.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.columnLibraryCollisionalCrossSection.DataPropertyName = "CollisionalCrossSection";
            dataGridViewCellStyle3.Format = "N2";
            dataGridViewCellStyle3.NullValue = null;
            this.columnLibraryCollisionalCrossSection.DefaultCellStyle = dataGridViewCellStyle3;
            resources.ApplyResources(this.columnLibraryCollisionalCrossSection, "columnLibraryCollisionalCrossSection");
            this.columnLibraryCollisionalCrossSection.Name = "columnLibraryCollisionalCrossSection";
            // 
            // columnLibraryHighEnergyDriftTimeOffsetMsec
            // 
            this.columnLibraryHighEnergyDriftTimeOffsetMsec.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.columnLibraryHighEnergyDriftTimeOffsetMsec.DataPropertyName = "HighEnergyDriftTimeOffsetMsec";
            this.columnLibraryHighEnergyDriftTimeOffsetMsec.DefaultCellStyle = dataGridViewCellStyle3;
            resources.ApplyResources(this.columnLibraryHighEnergyDriftTimeOffsetMsec, "columnHighEnergyDriftTimeOffsetMsec");
            this.columnLibraryHighEnergyDriftTimeOffsetMsec.Name = "columnHighEnergyDriftTimeOffsetMsec";
            // 
            // EditIonMobilityLibraryDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.gridViewMeasuredPeptides);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.btnCreateDb);
            this.Controls.Add(this.labelNumPeptides);
            this.Controls.Add(this.btnImportFromLibrary);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnBrowseDb);
            this.Controls.Add(this.textDatabase);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textLibraryName);
            this.Controls.Add(this.label1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditIonMobilityLibraryDlg";
            this.ShowInTaskbar = false;
            this.Load += new System.EventHandler(this.OnLoad);
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceLibrary)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceStandard)).EndInit();
            this.contextMenuAdd.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridViewMeasuredPeptides)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textLibraryName;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textDatabase;
        private System.Windows.Forms.Button btnBrowseDb;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnImportFromLibrary;
        private System.Windows.Forms.Label labelNumPeptides;
        private System.Windows.Forms.Button btnCreateDb;
        private System.Windows.Forms.BindingSource bindingSourceStandard;
        private System.Windows.Forms.BindingSource bindingSourceLibrary;
        private System.Windows.Forms.ContextMenuStrip contextMenuAdd;
        private System.Windows.Forms.ToolStripMenuItem addResultsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addSpectralLibraryContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addIRTDatabaseContextMenuItem;
        private System.Windows.Forms.Label label4;
        private Controls.DataGridViewEx gridViewMeasuredPeptides;
        private System.Windows.Forms.ToolTip toolTipImportBtn;
        private System.Windows.Forms.ToolTip toolTipMeasuredPeptidesGrid;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnLibrarySequence;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnLibraryCollisionalCrossSection;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnLibraryHighEnergyDriftTimeOffsetMsec;
    }
}