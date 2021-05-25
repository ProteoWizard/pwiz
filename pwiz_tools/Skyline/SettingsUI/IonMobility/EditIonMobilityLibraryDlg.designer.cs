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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            this.label1 = new System.Windows.Forms.Label();
            this.textLibraryName = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.textDatabase = new System.Windows.Forms.TextBox();
            this.btnBrowseDb = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnImportFromLibrary = new System.Windows.Forms.Button();
            this.labelNumPrecursorIons = new System.Windows.Forms.Label();
            this.btnCreateDb = new System.Windows.Forms.Button();
            this.bindingSourceLibrary = new System.Windows.Forms.BindingSource(this.components);
            this.bindingSourceStandard = new System.Windows.Forms.BindingSource(this.components);
            this.contextMenuAdd = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.addResultsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addSpectralLibraryContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addIonMobilityDatabaseContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.label4 = new System.Windows.Forms.Label();
            this.gridViewIonMobilities = new pwiz.Skyline.Controls.DataGridViewEx();
            this.columnTarget = new pwiz.Skyline.Controls.TargetColumn();
            this.columnAdduct = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnCollisionalCrossSection = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnIonMobility = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnIonMobilityUnits = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.columnHighEnergyIonMobilityOffset = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.toolTipImportBtn = new System.Windows.Forms.ToolTip(this.components);
            this.toolTipMeasuredPeptidesGrid = new System.Windows.Forms.ToolTip(this.components);
            this.btnUseResults = new System.Windows.Forms.Button();
            this.cbOffsetHighEnergySpectra = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceLibrary)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceStandard)).BeginInit();
            this.contextMenuAdd.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewIonMobilities)).BeginInit();
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
            this.textDatabase.ReadOnly = true;
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
            this.btnCancel.CausesValidation = false;
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
            // labelNumPrecursorIons
            // 
            resources.ApplyResources(this.labelNumPrecursorIons, "labelNumPrecursorIons");
            this.labelNumPrecursorIons.Name = "labelNumPrecursorIons";
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
            this.addIonMobilityDatabaseContextMenuItem});
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
            // addIonMobilityDatabaseContextMenuItem
            // 
            this.addIonMobilityDatabaseContextMenuItem.Name = "addIonMobilityDatabaseContextMenuItem";
            resources.ApplyResources(this.addIonMobilityDatabaseContextMenuItem, "addIonMobilityDatabaseContextMenuItem");
            this.addIonMobilityDatabaseContextMenuItem.Click += new System.EventHandler(this.addIonMobilityLibraryContextMenuItem_Click);
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // gridViewIonMobilities
            // 
            resources.ApplyResources(this.gridViewIonMobilities, "gridViewIonMobilities");
            this.gridViewIonMobilities.AutoGenerateColumns = false;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridViewIonMobilities.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.gridViewIonMobilities.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridViewIonMobilities.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.columnTarget,
            this.columnAdduct,
            this.columnCollisionalCrossSection,
            this.columnIonMobility,
            this.columnIonMobilityUnits,
            this.columnHighEnergyIonMobilityOffset});
            this.gridViewIonMobilities.DataSource = this.bindingSourceLibrary;
            dataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle6.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle6.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle6.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle6.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle6.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle6.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridViewIonMobilities.DefaultCellStyle = dataGridViewCellStyle6;
            this.gridViewIonMobilities.Name = "gridViewIonMobilities";
            this.gridViewIonMobilities.RowsAdded += new System.Windows.Forms.DataGridViewRowsAddedEventHandler(this.gridViewLibrary_RowsAdded);
            this.gridViewIonMobilities.RowsRemoved += new System.Windows.Forms.DataGridViewRowsRemovedEventHandler(this.gridViewLibrary_RowsRemoved);
            // 
            // columnTarget
            // 
            this.columnTarget.DataPropertyName = "PeptideModSeq";
            dataGridViewCellStyle2.NullValue = null;
            this.columnTarget.DefaultCellStyle = dataGridViewCellStyle2;
            resources.ApplyResources(this.columnTarget, "columnTarget");
            this.columnTarget.Name = "columnTarget";
            // 
            // columnAdduct
            // 
            this.columnAdduct.DataPropertyName = "PrecursorAdduct";
            resources.ApplyResources(this.columnAdduct, "columnAdduct");
            this.columnAdduct.Name = "columnAdduct";
            // 
            // columnCollisionalCrossSection
            // 
            this.columnCollisionalCrossSection.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.columnCollisionalCrossSection.DataPropertyName = "CollisionalCrossSectionNullable";
            dataGridViewCellStyle3.Format = "N5";
            dataGridViewCellStyle3.NullValue = null;
            this.columnCollisionalCrossSection.DefaultCellStyle = dataGridViewCellStyle3;
            resources.ApplyResources(this.columnCollisionalCrossSection, "columnCollisionalCrossSection");
            this.columnCollisionalCrossSection.Name = "columnCollisionalCrossSection";
            // 
            // columnIonMobility
            // 
            this.columnIonMobility.DataPropertyName = "IonMobilityNullable";
            dataGridViewCellStyle4.Format = "N5";
            this.columnIonMobility.DefaultCellStyle = dataGridViewCellStyle4;
            resources.ApplyResources(this.columnIonMobility, "columnIonMobility");
            this.columnIonMobility.Name = "columnIonMobility";
            // 
            // columnIonMobilityUnits
            // 
            this.columnIonMobilityUnits.DataPropertyName = "IonMobilityUnitsDisplay";
            resources.ApplyResources(this.columnIonMobilityUnits, "columnIonMobilityUnits");
            this.columnIonMobilityUnits.MaxDropDownItems = 1;
            this.columnIonMobilityUnits.Name = "columnIonMobilityUnits";
            this.columnIonMobilityUnits.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.columnIonMobilityUnits.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // columnHighEnergyIonMobilityOffset
            // 
            this.columnHighEnergyIonMobilityOffset.DataPropertyName = "HighEnergyIonMobilityOffsetNullable";
            dataGridViewCellStyle5.Format = "N5";
            this.columnHighEnergyIonMobilityOffset.DefaultCellStyle = dataGridViewCellStyle5;
            resources.ApplyResources(this.columnHighEnergyIonMobilityOffset, "columnHighEnergyIonMobilityOffset");
            this.columnHighEnergyIonMobilityOffset.Name = "columnHighEnergyIonMobilityOffset";
            // 
            // btnUseResults
            // 
            resources.ApplyResources(this.btnUseResults, "btnUseResults");
            this.btnUseResults.Name = "btnUseResults";
            this.toolTipMeasuredPeptidesGrid.SetToolTip(this.btnUseResults, resources.GetString("btnUseResults.ToolTip"));
            this.btnUseResults.UseVisualStyleBackColor = true;
            this.btnUseResults.Click += new System.EventHandler(this.btnUseResults_Click);
            // 
            // cbOffsetHighEnergySpectra
            // 
            resources.ApplyResources(this.cbOffsetHighEnergySpectra, "cbOffsetHighEnergySpectra");
            this.cbOffsetHighEnergySpectra.Name = "cbOffsetHighEnergySpectra";
            this.toolTipMeasuredPeptidesGrid.SetToolTip(this.cbOffsetHighEnergySpectra, resources.GetString("cbOffsetHighEnergySpectra.ToolTip"));
            this.cbOffsetHighEnergySpectra.UseVisualStyleBackColor = true;
            this.cbOffsetHighEnergySpectra.CheckedChanged += new System.EventHandler(this.cbOffsetHighEnergySpectra_CheckedChanged);
            // 
            // EditIonMobilityLibraryDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnUseResults);
            this.Controls.Add(this.cbOffsetHighEnergySpectra);
            this.Controls.Add(this.gridViewIonMobilities);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.btnCreateDb);
            this.Controls.Add(this.labelNumPrecursorIons);
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
            ((System.ComponentModel.ISupportInitialize)(this.gridViewIonMobilities)).EndInit();
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
        private System.Windows.Forms.Label labelNumPrecursorIons;
        private System.Windows.Forms.Button btnCreateDb;
        private System.Windows.Forms.BindingSource bindingSourceStandard;
        private System.Windows.Forms.BindingSource bindingSourceLibrary;
        private System.Windows.Forms.ContextMenuStrip contextMenuAdd;
        private System.Windows.Forms.ToolStripMenuItem addResultsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addSpectralLibraryContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addIonMobilityDatabaseContextMenuItem;
        private System.Windows.Forms.Label label4;
        private Controls.DataGridViewEx gridViewIonMobilities;
        private System.Windows.Forms.ToolTip toolTipImportBtn;
        private System.Windows.Forms.ToolTip toolTipMeasuredPeptidesGrid;
        private System.Windows.Forms.Button btnUseResults;
        private System.Windows.Forms.CheckBox cbOffsetHighEnergySpectra;
        private Controls.TargetColumn columnTarget;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnAdduct;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnCollisionalCrossSection;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnIonMobility;
        private System.Windows.Forms.DataGridViewComboBoxColumn columnIonMobilityUnits;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnHighEnergyIonMobilityOffset;
    }
}