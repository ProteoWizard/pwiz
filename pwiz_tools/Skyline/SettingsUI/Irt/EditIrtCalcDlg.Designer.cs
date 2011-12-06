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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle7 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
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
            this.gridViewLibrary = new pwiz.Skyline.Controls.DataGridViewEx();
            this.columnLibrarySequence = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnLibraryIrt = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.gridViewStandard = new pwiz.Skyline.Controls.DataGridViewEx();
            this.columnStandardSequence = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnStandardIrt = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceLibrary)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceStandard)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewLibrary)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewStandard)).BeginInit();
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
            this.label2.Size = new System.Drawing.Size(76, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "iRT &Database:";
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
            this.label3.Size = new System.Drawing.Size(97, 13);
            this.label3.TabIndex = 6;
            this.label3.Text = "&Standard Peptides:";
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(297, 13);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 13;
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
            this.btnCancel.TabIndex = 14;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnCalibrate
            // 
            this.btnCalibrate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCalibrate.Location = new System.Drawing.Point(297, 319);
            this.btnCalibrate.Name = "btnCalibrate";
            this.btnCalibrate.Size = new System.Drawing.Size(75, 23);
            this.btnCalibrate.TabIndex = 8;
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
            this.btnAddResults.TabIndex = 12;
            this.btnAddResults.Text = "Add &Results";
            this.btnAddResults.UseVisualStyleBackColor = true;
            this.btnAddResults.Click += new System.EventHandler(this.btnAddResults_Click);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(9, 332);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(101, 13);
            this.label4.TabIndex = 9;
            this.label4.Text = "&Measured Peptides:";
            // 
            // labelNumPeptides
            // 
            this.labelNumPeptides.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.labelNumPeptides.AutoSize = true;
            this.labelNumPeptides.Location = new System.Drawing.Point(9, 523);
            this.labelNumPeptides.Name = "labelNumPeptides";
            this.labelNumPeptides.Size = new System.Drawing.Size(57, 13);
            this.labelNumPeptides.TabIndex = 11;
            this.labelNumPeptides.Text = "# peptides";
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
            // gridViewLibrary
            // 
            this.gridViewLibrary.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                                                                                 | System.Windows.Forms.AnchorStyles.Left)
                                                                                | System.Windows.Forms.AnchorStyles.Right)));
            this.gridViewLibrary.AutoGenerateColumns = false;
            this.gridViewLibrary.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridViewLibrary.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridViewLibrary.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
                                                                                                    this.columnLibrarySequence,
                                                                                                    this.columnLibraryIrt});
            this.gridViewLibrary.DataSource = this.bindingSourceLibrary;
            this.gridViewLibrary.Location = new System.Drawing.Point(12, 348);
            this.gridViewLibrary.Name = "gridViewLibrary";
            this.gridViewLibrary.Size = new System.Drawing.Size(360, 164);
            this.gridViewLibrary.TabIndex = 10;
            this.gridViewLibrary.RowsAdded += new System.Windows.Forms.DataGridViewRowsAddedEventHandler(this.gridViewLibrary_RowsAdded);
            this.gridViewLibrary.RowsRemoved += new System.Windows.Forms.DataGridViewRowsRemovedEventHandler(this.gridViewLibrary_RowsRemoved);
            // 
            // columnLibrarySequence
            // 
            this.columnLibrarySequence.DataPropertyName = "PeptideModSeq";
            dataGridViewCellStyle1.NullValue = null;
            this.columnLibrarySequence.DefaultCellStyle = dataGridViewCellStyle1;
            this.columnLibrarySequence.HeaderText = "Modified Sequence";
            this.columnLibrarySequence.Name = "columnLibrarySequence";
            this.columnLibrarySequence.ReadOnly = true;
            // 
            // columnLibraryIrt
            // 
            this.columnLibraryIrt.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.columnLibraryIrt.DataPropertyName = "Irt";
            dataGridViewCellStyle2.Format = "N2";
            dataGridViewCellStyle2.NullValue = null;
            this.columnLibraryIrt.DefaultCellStyle = dataGridViewCellStyle2;
            this.columnLibraryIrt.HeaderText = "iRT Value";
            this.columnLibraryIrt.Name = "columnLibraryIrt";
            this.columnLibraryIrt.ReadOnly = true;
            // 
            // gridViewStandard
            // 
            this.gridViewStandard.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                                                                                 | System.Windows.Forms.AnchorStyles.Right)));
            this.gridViewStandard.AutoGenerateColumns = false;
            this.gridViewStandard.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridViewStandard.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle3;
            this.gridViewStandard.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridViewStandard.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
                                                                                                     this.columnStandardSequence,
                                                                                                     this.columnStandardIrt});
            this.gridViewStandard.DataSource = this.bindingSourceStandard;
            dataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle6.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle6.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle6.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle6.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle6.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle6.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridViewStandard.DefaultCellStyle = dataGridViewCellStyle6;
            this.gridViewStandard.Location = new System.Drawing.Point(12, 179);
            this.gridViewStandard.Name = "gridViewStandard";
            dataGridViewCellStyle7.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle7.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle7.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle7.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle7.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle7.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle7.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridViewStandard.RowHeadersDefaultCellStyle = dataGridViewCellStyle7;
            this.gridViewStandard.Size = new System.Drawing.Size(359, 134);
            this.gridViewStandard.TabIndex = 7;
            // 
            // columnStandardSequence
            // 
            this.columnStandardSequence.DataPropertyName = "PeptideModSeq";
            dataGridViewCellStyle4.NullValue = null;
            this.columnStandardSequence.DefaultCellStyle = dataGridViewCellStyle4;
            this.columnStandardSequence.HeaderText = "Modified Sequence";
            this.columnStandardSequence.Name = "columnStandardSequence";
            // 
            // columnStandardIrt
            // 
            this.columnStandardIrt.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.columnStandardIrt.DataPropertyName = "Irt";
            dataGridViewCellStyle5.Format = "N2";
            dataGridViewCellStyle5.NullValue = null;
            this.columnStandardIrt.DefaultCellStyle = dataGridViewCellStyle5;
            this.columnStandardIrt.HeaderText = "iRT Value";
            this.columnStandardIrt.Name = "columnStandardIrt";
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
            this.Controls.Add(this.label4);
            this.Controls.Add(this.btnAddResults);
            this.Controls.Add(this.gridViewLibrary);
            this.Controls.Add(this.btnCalibrate);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.gridViewStandard);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.btnBrowseDb);
            this.Controls.Add(this.textDatabase);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textCalculatorName);
            this.Controls.Add(this.label1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(370, 500);
            this.Name = "EditIrtCalcDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit iRT Standard";
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceLibrary)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceStandard)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewLibrary)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewStandard)).EndInit();
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
        private System.Windows.Forms.DataGridViewTextBoxColumn columnLibrarySequence;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnLibraryIrt;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnStandardSequence;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnStandardIrt;
    }
}