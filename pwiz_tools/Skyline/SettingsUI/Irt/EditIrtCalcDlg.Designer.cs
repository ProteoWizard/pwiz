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
            this.columnStandardSequence = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnStandardIrt = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.gridViewLibrary = new pwiz.Skyline.Controls.DataGridViewEx();
            this.columnLibrarySequence = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnLibraryIrt = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceLibrary)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceStandard)).BeginInit();
            this.contextMenuAdd.SuspendLayout();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewStandard)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewLibrary)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(9, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(38, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "&Name:";
            // 
            // textCalculatorName
            // 
            this.textCalculatorName.Location = new System.Drawing.Point(12, 29);
            this.textCalculatorName.Name = "textCalculatorName";
            this.textCalculatorName.Size = new System.Drawing.Size(176, 20);
            this.textCalculatorName.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(9, 71);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(74, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "iRT &database:";
            // 
            // textDatabase
            // 
            this.textDatabase.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.textDatabase.Location = new System.Drawing.Point(12, 87);
            this.textDatabase.Name = "textDatabase";
            this.textDatabase.Size = new System.Drawing.Size(254, 20);
            this.textDatabase.TabIndex = 3;
            // 
            // btnBrowseDb
            // 
            this.btnBrowseDb.Location = new System.Drawing.Point(12, 113);
            this.btnBrowseDb.Name = "btnBrowseDb";
            this.btnBrowseDb.Size = new System.Drawing.Size(75, 23);
            this.btnBrowseDb.TabIndex = 4;
            this.btnBrowseDb.Text = "&Open...";
            this.btnBrowseDb.UseVisualStyleBackColor = true;
            this.btnBrowseDb.Click += new System.EventHandler(this.btnBrowseDb_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(9, 163);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(96, 13);
            this.label3.TabIndex = 6;
            this.label3.Text = "&Standard peptides:";
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(297, 13);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 10;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(297, 43);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 11;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnCalibrate
            // 
            this.btnCalibrate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCalibrate.Location = new System.Drawing.Point(285, 2);
            this.btnCalibrate.Name = "btnCalibrate";
            this.btnCalibrate.Size = new System.Drawing.Size(75, 23);
            this.btnCalibrate.TabIndex = 1;
            this.btnCalibrate.Text = "Cali&brate...";
            this.btnCalibrate.UseVisualStyleBackColor = true;
            this.btnCalibrate.Click += new System.EventHandler(this.btnCalibrate_Click);
            // 
            // btnAddResults
            // 
            this.btnAddResults.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAddResults.Location = new System.Drawing.Point(297, 518);
            this.btnAddResults.Name = "btnAddResults";
            this.btnAddResults.Size = new System.Drawing.Size(75, 23);
            this.btnAddResults.TabIndex = 9;
            this.btnAddResults.Text = "&Add...";
            this.btnAddResults.UseVisualStyleBackColor = true;
            this.btnAddResults.Click += new System.EventHandler(this.btnAddResults_Click);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(-3, 15);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(100, 13);
            this.label4.TabIndex = 2;
            this.label4.Text = "&Measured peptides:";
            // 
            // labelNumPeptides
            // 
            this.labelNumPeptides.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.labelNumPeptides.AutoSize = true;
            this.labelNumPeptides.Location = new System.Drawing.Point(9, 523);
            this.labelNumPeptides.Name = "labelNumPeptides";
            this.labelNumPeptides.Size = new System.Drawing.Size(56, 13);
            this.labelNumPeptides.TabIndex = 8;
            this.labelNumPeptides.Text = "0 peptides";
            // 
            // btnCreateDb
            // 
            this.btnCreateDb.Location = new System.Drawing.Point(93, 113);
            this.btnCreateDb.Name = "btnCreateDb";
            this.btnCreateDb.Size = new System.Drawing.Size(75, 23);
            this.btnCreateDb.TabIndex = 5;
            this.btnCreateDb.Text = "&Create...";
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
            this.contextMenuAdd.Size = new System.Drawing.Size(190, 70);
            // 
            // addResultsContextMenuItem
            // 
            this.addResultsContextMenuItem.Name = "addResultsContextMenuItem";
            this.addResultsContextMenuItem.Size = new System.Drawing.Size(189, 22);
            this.addResultsContextMenuItem.Text = "Add &Results...";
            this.addResultsContextMenuItem.Click += new System.EventHandler(this.addResultsContextMenuItem_Click);
            // 
            // addSpectralLibraryContextMenuItem
            // 
            this.addSpectralLibraryContextMenuItem.Name = "addSpectralLibraryContextMenuItem";
            this.addSpectralLibraryContextMenuItem.Size = new System.Drawing.Size(189, 22);
            this.addSpectralLibraryContextMenuItem.Text = "Add Spectral &Library...";
            this.addSpectralLibraryContextMenuItem.Click += new System.EventHandler(this.addSpectralLibraryContextMenuItem_Click);
            // 
            // addIRTDatabaseContextMenuItem
            // 
            this.addIRTDatabaseContextMenuItem.Name = "addIRTDatabaseContextMenuItem";
            this.addIRTDatabaseContextMenuItem.Size = new System.Drawing.Size(189, 22);
            this.addIRTDatabaseContextMenuItem.Text = "Add iRT &Database...";
            this.addIRTDatabaseContextMenuItem.Click += new System.EventHandler(this.addIRTDatabaseContextMenuItem_Click);
            // 
            // btnPeptides
            // 
            this.btnPeptides.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnPeptides.Location = new System.Drawing.Point(204, 2);
            this.btnPeptides.Name = "btnPeptides";
            this.btnPeptides.Size = new System.Drawing.Size(75, 23);
            this.btnPeptides.TabIndex = 0;
            this.btnPeptides.Text = "Peptides...";
            this.btnPeptides.UseVisualStyleBackColor = true;
            this.btnPeptides.Visible = false;
            this.btnPeptides.Click += new System.EventHandler(this.btnPeptides_Click);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer1.Location = new System.Drawing.Point(12, 179);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.gridViewStandard);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.gridViewLibrary);
            this.splitContainer1.Panel2.Controls.Add(this.btnPeptides);
            this.splitContainer1.Panel2.Controls.Add(this.label4);
            this.splitContainer1.Panel2.Controls.Add(this.btnCalibrate);
            this.splitContainer1.Size = new System.Drawing.Size(360, 333);
            this.splitContainer1.SplitterDistance = 148;
            this.splitContainer1.TabIndex = 7;
            this.splitContainer1.TabStop = false;
            // 
            // gridViewStandard
            // 
            this.gridViewStandard.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.gridViewStandard.AutoGenerateColumns = false;
            this.gridViewStandard.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
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
            this.gridViewStandard.Location = new System.Drawing.Point(0, 0);
            this.gridViewStandard.Name = "gridViewStandard";
            dataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle5.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle5.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle5.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle5.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle5.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle5.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridViewStandard.RowHeadersDefaultCellStyle = dataGridViewCellStyle5;
            this.gridViewStandard.Size = new System.Drawing.Size(360, 148);
            this.gridViewStandard.TabIndex = 0;
            // 
            // columnStandardSequence
            // 
            this.columnStandardSequence.DataPropertyName = "PeptideModSeq";
            dataGridViewCellStyle2.NullValue = null;
            this.columnStandardSequence.DefaultCellStyle = dataGridViewCellStyle2;
            this.columnStandardSequence.HeaderText = "Modified Sequence";
            this.columnStandardSequence.Name = "columnStandardSequence";
            // 
            // columnStandardIrt
            // 
            this.columnStandardIrt.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.columnStandardIrt.DataPropertyName = "Irt";
            dataGridViewCellStyle3.Format = "N2";
            dataGridViewCellStyle3.NullValue = null;
            this.columnStandardIrt.DefaultCellStyle = dataGridViewCellStyle3;
            this.columnStandardIrt.HeaderText = "iRT Value";
            this.columnStandardIrt.Name = "columnStandardIrt";
            // 
            // gridViewLibrary
            // 
            this.gridViewLibrary.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.gridViewLibrary.AutoGenerateColumns = false;
            this.gridViewLibrary.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
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
            this.gridViewLibrary.Location = new System.Drawing.Point(0, 31);
            this.gridViewLibrary.Name = "gridViewLibrary";
            this.gridViewLibrary.Size = new System.Drawing.Size(360, 150);
            this.gridViewLibrary.TabIndex = 3;
            this.gridViewLibrary.RowsAdded += new System.Windows.Forms.DataGridViewRowsAddedEventHandler(this.gridViewLibrary_RowsAdded);
            this.gridViewLibrary.RowsRemoved += new System.Windows.Forms.DataGridViewRowsRemovedEventHandler(this.gridViewLibrary_RowsRemoved);
            // 
            // columnLibrarySequence
            // 
            this.columnLibrarySequence.DataPropertyName = "PeptideModSeq";
            dataGridViewCellStyle7.NullValue = null;
            this.columnLibrarySequence.DefaultCellStyle = dataGridViewCellStyle7;
            this.columnLibrarySequence.HeaderText = "Modified Sequence";
            this.columnLibrarySequence.Name = "columnLibrarySequence";
            // 
            // columnLibraryIrt
            // 
            this.columnLibraryIrt.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.columnLibraryIrt.DataPropertyName = "Irt";
            dataGridViewCellStyle8.Format = "N2";
            dataGridViewCellStyle8.NullValue = null;
            this.columnLibraryIrt.DefaultCellStyle = dataGridViewCellStyle8;
            this.columnLibraryIrt.HeaderText = "iRT Value";
            this.columnLibraryIrt.Name = "columnLibraryIrt";
            // 
            // EditIrtCalcDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(384, 554);
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
            this.MinimumSize = new System.Drawing.Size(370, 500);
            this.Name = "EditIrtCalcDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit iRT Calculator";
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceLibrary)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceStandard)).EndInit();
            this.contextMenuAdd.ResumeLayout(false);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
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
        private pwiz.Skyline.Controls.DataGridViewEx gridViewStandard;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnCalibrate;
        private pwiz.Skyline.Controls.DataGridViewEx gridViewLibrary;
        private System.Windows.Forms.Button btnAddResults;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label labelNumPeptides;
        private System.Windows.Forms.Button btnCreateDb;
        private System.Windows.Forms.BindingSource bindingSourceStandard;
        private System.Windows.Forms.BindingSource bindingSourceLibrary;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnStandardSequence;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnStandardIrt;
        private System.Windows.Forms.ContextMenuStrip contextMenuAdd;
        private System.Windows.Forms.ToolStripMenuItem addResultsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addSpectralLibraryContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addIRTDatabaseContextMenuItem;
        private System.Windows.Forms.Button btnPeptides;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnLibrarySequence;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnLibraryIrt;
        private System.Windows.Forms.SplitContainer splitContainer1;
    }
}