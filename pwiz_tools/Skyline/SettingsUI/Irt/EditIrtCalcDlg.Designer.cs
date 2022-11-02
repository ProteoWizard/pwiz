namespace pwiz.Skyline.SettingsUI.Irt
{
    partial class EditIrtCalcDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditIrtCalcDlg));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle9 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle7 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle8 = new System.Windows.Forms.DataGridViewCellStyle();
            this.label1 = new System.Windows.Forms.Label();
            this.textCalculatorName = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.textDatabase = new System.Windows.Forms.TextBox();
            this.btnBrowseDb = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnCalibrate = new System.Windows.Forms.Button();
            this.btnAddResults = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.labelNumPeptides = new System.Windows.Forms.Label();
            this.btnCreateDb = new System.Windows.Forms.Button();
            this.bindingSourceLibrary = new System.Windows.Forms.BindingSource(this.components);
            this.bindingSourceStandard = new System.Windows.Forms.BindingSource(this.components);
            this.contextMenuAdd = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.addResultsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addSpectralLibraryContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addIRTDatabaseContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.btnPeptides = new System.Windows.Forms.Button();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.gridViewStandard = new pwiz.Skyline.Controls.DataGridViewEx();
            this.columnStandardSequence = new pwiz.Skyline.Controls.TargetColumn();
            this.columnStandardIrt = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.labelNumStandards = new System.Windows.Forms.Label();
            this.gridViewLibrary = new pwiz.Skyline.Controls.DataGridViewEx();
            this.columnLibrarySequence = new pwiz.Skyline.Controls.TargetColumn();
            this.columnLibraryIrt = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.comboStandards = new System.Windows.Forms.ComboBox();
            this.comboRegressionType = new System.Windows.Forms.ComboBox();
            this.label5 = new System.Windows.Forms.Label();
            this.cbRedundant = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceLibrary)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceStandard)).BeginInit();
            this.contextMenuAdd.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewStandard)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewLibrary)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // textCalculatorName
            // 
            resources.ApplyResources(this.textCalculatorName, "textCalculatorName");
            this.textCalculatorName.Name = "textCalculatorName";
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
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
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
            // btnCalibrate
            // 
            resources.ApplyResources(this.btnCalibrate, "btnCalibrate");
            this.btnCalibrate.Name = "btnCalibrate";
            this.btnCalibrate.UseVisualStyleBackColor = true;
            this.btnCalibrate.Click += new System.EventHandler(this.btnCalibrate_Click);
            // 
            // btnAddResults
            // 
            resources.ApplyResources(this.btnAddResults, "btnAddResults");
            this.btnAddResults.Name = "btnAddResults";
            this.btnAddResults.UseVisualStyleBackColor = true;
            this.btnAddResults.Click += new System.EventHandler(this.btnAddResults_Click);
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
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
            this.addResultsContextMenuItem.Click += new System.EventHandler(this.addResultsContextMenuItem_Click);
            // 
            // addSpectralLibraryContextMenuItem
            // 
            this.addSpectralLibraryContextMenuItem.Name = "addSpectralLibraryContextMenuItem";
            resources.ApplyResources(this.addSpectralLibraryContextMenuItem, "addSpectralLibraryContextMenuItem");
            this.addSpectralLibraryContextMenuItem.Click += new System.EventHandler(this.addSpectralLibraryContextMenuItem_Click);
            // 
            // addIRTDatabaseContextMenuItem
            // 
            this.addIRTDatabaseContextMenuItem.Name = "addIRTDatabaseContextMenuItem";
            resources.ApplyResources(this.addIRTDatabaseContextMenuItem, "addIRTDatabaseContextMenuItem");
            this.addIRTDatabaseContextMenuItem.Click += new System.EventHandler(this.addIRTDatabaseContextMenuItem_Click);
            // 
            // btnPeptides
            // 
            resources.ApplyResources(this.btnPeptides, "btnPeptides");
            this.btnPeptides.Name = "btnPeptides";
            this.btnPeptides.UseVisualStyleBackColor = true;
            this.btnPeptides.Click += new System.EventHandler(this.btnPeptides_Click);
            // 
            // splitContainer1
            // 
            resources.ApplyResources(this.splitContainer1, "splitContainer1");
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.gridViewStandard);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.labelNumStandards);
            this.splitContainer1.Panel2.Controls.Add(this.gridViewLibrary);
            this.splitContainer1.Panel2.Controls.Add(this.btnPeptides);
            this.splitContainer1.Panel2.Controls.Add(this.label4);
            this.splitContainer1.Panel2.Controls.Add(this.btnCalibrate);
            this.splitContainer1.TabStop = false;
            // 
            // gridViewStandard
            // 
            resources.ApplyResources(this.gridViewStandard, "gridViewStandard");
            this.gridViewStandard.AutoGenerateColumns = false;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridViewStandard.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.gridViewStandard.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridViewStandard.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.columnStandardSequence,
            this.columnStandardIrt});
            this.gridViewStandard.DataSource = this.bindingSourceStandard;
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle4.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle4.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle4.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle4.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridViewStandard.DefaultCellStyle = dataGridViewCellStyle4;
            this.gridViewStandard.Name = "gridViewStandard";
            dataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle5.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle5.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle5.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle5.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle5.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle5.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridViewStandard.RowHeadersDefaultCellStyle = dataGridViewCellStyle5;
            this.gridViewStandard.RowsAdded += new System.Windows.Forms.DataGridViewRowsAddedEventHandler(this.gridViewStandard_RowsAdded);
            this.gridViewStandard.RowsRemoved += new System.Windows.Forms.DataGridViewRowsRemovedEventHandler(this.gridViewStandard_RowsRemoved);
            // 
            // columnStandardSequence
            // 
            this.columnStandardSequence.DataPropertyName = "ModifiedTarget";
            dataGridViewCellStyle2.NullValue = null;
            this.columnStandardSequence.DefaultCellStyle = dataGridViewCellStyle2;
            resources.ApplyResources(this.columnStandardSequence, "columnStandardSequence");
            this.columnStandardSequence.Name = "columnStandardSequence";
            // 
            // columnStandardIrt
            // 
            this.columnStandardIrt.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.columnStandardIrt.DataPropertyName = "Irt";
            dataGridViewCellStyle3.Format = "N2";
            dataGridViewCellStyle3.NullValue = null;
            this.columnStandardIrt.DefaultCellStyle = dataGridViewCellStyle3;
            resources.ApplyResources(this.columnStandardIrt, "columnStandardIrt");
            this.columnStandardIrt.Name = "columnStandardIrt";
            // 
            // labelNumStandards
            // 
            resources.ApplyResources(this.labelNumStandards, "labelNumStandards");
            this.labelNumStandards.Name = "labelNumStandards";
            // 
            // gridViewLibrary
            // 
            resources.ApplyResources(this.gridViewLibrary, "gridViewLibrary");
            this.gridViewLibrary.AutoGenerateColumns = false;
            dataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle6.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle6.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle6.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle6.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle6.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle6.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridViewLibrary.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle6;
            this.gridViewLibrary.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridViewLibrary.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.columnLibrarySequence,
            this.columnLibraryIrt});
            this.gridViewLibrary.DataSource = this.bindingSourceLibrary;
            dataGridViewCellStyle9.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle9.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle9.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle9.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle9.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle9.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle9.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridViewLibrary.DefaultCellStyle = dataGridViewCellStyle9;
            this.gridViewLibrary.Name = "gridViewLibrary";
            this.gridViewLibrary.RowsAdded += new System.Windows.Forms.DataGridViewRowsAddedEventHandler(this.gridViewLibrary_RowsAdded);
            this.gridViewLibrary.RowsRemoved += new System.Windows.Forms.DataGridViewRowsRemovedEventHandler(this.gridViewLibrary_RowsRemoved);
            // 
            // columnLibrarySequence
            // 
            this.columnLibrarySequence.DataPropertyName = "ModifiedTarget";
            dataGridViewCellStyle7.NullValue = null;
            this.columnLibrarySequence.DefaultCellStyle = dataGridViewCellStyle7;
            resources.ApplyResources(this.columnLibrarySequence, "columnLibrarySequence");
            this.columnLibrarySequence.Name = "columnLibrarySequence";
            // 
            // columnLibraryIrt
            // 
            this.columnLibraryIrt.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.columnLibraryIrt.DataPropertyName = "Irt";
            dataGridViewCellStyle8.Format = "N2";
            dataGridViewCellStyle8.NullValue = null;
            this.columnLibraryIrt.DefaultCellStyle = dataGridViewCellStyle8;
            resources.ApplyResources(this.columnLibraryIrt, "columnLibraryIrt");
            this.columnLibraryIrt.Name = "columnLibraryIrt";
            // 
            // comboStandards
            // 
            resources.ApplyResources(this.comboStandards, "comboStandards");
            this.comboStandards.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboStandards.FormattingEnabled = true;
            this.comboStandards.Name = "comboStandards";
            this.comboStandards.SelectedIndexChanged += new System.EventHandler(this.comboStandards_SelectedIndexChanged);
            // 
            // comboRegressionType
            // 
            this.comboRegressionType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRegressionType.FormattingEnabled = true;
            resources.ApplyResources(this.comboRegressionType, "comboRegressionType");
            this.comboRegressionType.Name = "comboRegressionType";
            this.comboRegressionType.SelectedIndexChanged += new System.EventHandler(this.comboRegressionType_SelectedIndexChanged);
            // 
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            // 
            // cbRedundant
            // 
            resources.ApplyResources(this.cbRedundant, "cbRedundant");
            this.cbRedundant.Name = "cbRedundant";
            this.cbRedundant.UseVisualStyleBackColor = true;
            this.cbRedundant.CheckedChanged += new System.EventHandler(this.cbRedundant_CheckedChanged);
            // 
            // EditIrtCalcDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.cbRedundant);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.comboRegressionType);
            this.Controls.Add(this.comboStandards);
            this.Controls.Add(this.btnCreateDb);
            this.Controls.Add(this.labelNumPeptides);
            this.Controls.Add(this.btnAddResults);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.btnBrowseDb);
            this.Controls.Add(this.textDatabase);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textCalculatorName);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.splitContainer1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditIrtCalcDlg";
            this.ShowInTaskbar = false;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.EditIrtCalcDlg_FormClosing);
            this.Load += new System.EventHandler(this.OnLoad);
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceLibrary)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceStandard)).EndInit();
            this.contextMenuAdd.ResumeLayout(false);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridViewStandard)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewLibrary)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textCalculatorName;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textDatabase;
        private System.Windows.Forms.Button btnBrowseDb;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnCalibrate;
        private Controls.DataGridViewEx gridViewLibrary;
        private System.Windows.Forms.Button btnAddResults;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label labelNumPeptides;
        private System.Windows.Forms.Button btnCreateDb;
        private System.Windows.Forms.BindingSource bindingSourceStandard;
        private System.Windows.Forms.BindingSource bindingSourceLibrary;
        private System.Windows.Forms.ContextMenuStrip contextMenuAdd;
        private System.Windows.Forms.ToolStripMenuItem addResultsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addSpectralLibraryContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addIRTDatabaseContextMenuItem;
        private System.Windows.Forms.Button btnPeptides;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.ComboBox comboStandards;
        private System.Windows.Forms.Label labelNumStandards;
        private Controls.DataGridViewEx gridViewStandard;
        private Controls.TargetColumn columnStandardSequence;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnStandardIrt;
        private Controls.TargetColumn columnLibrarySequence;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnLibraryIrt;
        private System.Windows.Forms.ComboBox comboRegressionType;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.CheckBox cbRedundant;
    }
}