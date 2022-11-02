namespace pwiz.Skyline.EditUI
{
    partial class EditLinkedPeptidesDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditLinkedPeptidesDlg));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.dataGridViewLinkedPeptides = new pwiz.Skyline.Controls.DataGridViewEx();
            this.colPeptideSequence = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colModificationsButton = new System.Windows.Forms.DataGridViewButtonColumn();
            this.lblLinkedPeptides = new System.Windows.Forms.Label();
            this.lblCrosslinks = new System.Windows.Forms.Label();
            this.dataGridViewCrosslinks = new pwiz.Skyline.Controls.DataGridViewEx();
            this.colCrosslinker = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colPeptide1 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colAminoAcid1 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colPeptide2 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colAminoAcid2 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.lblTitle = new System.Windows.Forms.Label();
            this.tbxPrimaryPeptide = new System.Windows.Forms.TextBox();
            this.dataGridViewTextBoxColumn1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewLinkedPeptides)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewCrosslinks)).BeginInit();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            resources.ApplyResources(this.splitContainer1, "splitContainer1");
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.dataGridViewLinkedPeptides);
            this.splitContainer1.Panel1.Controls.Add(this.lblLinkedPeptides);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.lblCrosslinks);
            this.splitContainer1.Panel2.Controls.Add(this.dataGridViewCrosslinks);
            // 
            // dataGridViewLinkedPeptides
            // 
            resources.ApplyResources(this.dataGridViewLinkedPeptides, "dataGridViewLinkedPeptides");
            this.dataGridViewLinkedPeptides.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewLinkedPeptides.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colPeptideSequence,
            this.colModificationsButton});
            this.dataGridViewLinkedPeptides.Name = "dataGridViewLinkedPeptides";
            this.dataGridViewLinkedPeptides.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridViewLinkedPeptides_CellContentClick);
            this.dataGridViewLinkedPeptides.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridViewLinkedPeptides_CellEndEdit);
            this.dataGridViewLinkedPeptides.CellErrorTextNeeded += new System.Windows.Forms.DataGridViewCellErrorTextNeededEventHandler(this.dataGridViewLinkedPeptides_CellErrorTextNeeded);
            this.dataGridViewLinkedPeptides.CellToolTipTextNeeded += new System.Windows.Forms.DataGridViewCellToolTipTextNeededEventHandler(this.dataGridViewLinkedPeptides_CellToolTipTextNeeded);
            // 
            // colPeptideSequence
            // 
            this.colPeptideSequence.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colPeptideSequence.DataPropertyName = "Sequence";
            resources.ApplyResources(this.colPeptideSequence, "colPeptideSequence");
            this.colPeptideSequence.Name = "colPeptideSequence";
            // 
            // colModificationsButton
            // 
            this.colModificationsButton.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle1.NullValue = "...";
            this.colModificationsButton.DefaultCellStyle = dataGridViewCellStyle1;
            resources.ApplyResources(this.colModificationsButton, "colModificationsButton");
            this.colModificationsButton.Name = "colModificationsButton";
            this.colModificationsButton.ReadOnly = true;
            // 
            // lblLinkedPeptides
            // 
            resources.ApplyResources(this.lblLinkedPeptides, "lblLinkedPeptides");
            this.lblLinkedPeptides.Name = "lblLinkedPeptides";
            // 
            // lblCrosslinks
            // 
            resources.ApplyResources(this.lblCrosslinks, "lblCrosslinks");
            this.lblCrosslinks.Name = "lblCrosslinks";
            // 
            // dataGridViewCrosslinks
            // 
            resources.ApplyResources(this.dataGridViewCrosslinks, "dataGridViewCrosslinks");
            this.dataGridViewCrosslinks.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridViewCrosslinks.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewCrosslinks.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colCrosslinker,
            this.colPeptide1,
            this.colAminoAcid1,
            this.colPeptide2,
            this.colAminoAcid2});
            this.dataGridViewCrosslinks.Name = "dataGridViewCrosslinks";
            this.dataGridViewCrosslinks.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridViewCrosslinks_CellEndEdit);
            this.dataGridViewCrosslinks.CellErrorTextNeeded += new System.Windows.Forms.DataGridViewCellErrorTextNeededEventHandler(this.dataGridViewCrosslinks_CellErrorTextNeeded);
            this.dataGridViewCrosslinks.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.dataGridViewCrosslinks_CellFormatting);
            this.dataGridViewCrosslinks.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.dataGridViewCrosslinks_DataError);
            this.dataGridViewCrosslinks.EditingControlShowing += new System.Windows.Forms.DataGridViewEditingControlShowingEventHandler(this.dataGridViewCrosslinks_EditingControlShowing);
            // 
            // colCrosslinker
            // 
            this.colCrosslinker.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colCrosslinker.DataPropertyName = "Crosslinker";
            this.colCrosslinker.DisplayStyleForCurrentCellOnly = true;
            this.colCrosslinker.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            resources.ApplyResources(this.colCrosslinker, "colCrosslinker");
            this.colCrosslinker.Name = "colCrosslinker";
            this.colCrosslinker.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.colCrosslinker.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // colPeptide1
            // 
            this.colPeptide1.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colPeptide1.DataPropertyName = "PeptideIndex1";
            this.colPeptide1.DisplayStyleForCurrentCellOnly = true;
            this.colPeptide1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            resources.ApplyResources(this.colPeptide1, "colPeptide1");
            this.colPeptide1.Name = "colPeptide1";
            this.colPeptide1.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.colPeptide1.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // colAminoAcid1
            // 
            this.colAminoAcid1.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.colAminoAcid1.DataPropertyName = "AaIndex1";
            this.colAminoAcid1.DisplayStyleForCurrentCellOnly = true;
            this.colAminoAcid1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            resources.ApplyResources(this.colAminoAcid1, "colAminoAcid1");
            this.colAminoAcid1.Name = "colAminoAcid1";
            this.colAminoAcid1.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.colAminoAcid1.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // colPeptide2
            // 
            this.colPeptide2.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colPeptide2.DataPropertyName = "PeptideIndex2";
            this.colPeptide2.DisplayStyleForCurrentCellOnly = true;
            this.colPeptide2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            resources.ApplyResources(this.colPeptide2, "colPeptide2");
            this.colPeptide2.Name = "colPeptide2";
            this.colPeptide2.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.colPeptide2.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // colAminoAcid2
            // 
            this.colAminoAcid2.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.colAminoAcid2.DataPropertyName = "AaIndex2";
            this.colAminoAcid2.DisplayStyleForCurrentCellOnly = true;
            this.colAminoAcid2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            resources.ApplyResources(this.colAminoAcid2, "colAminoAcid2");
            this.colAminoAcid2.Name = "colAminoAcid2";
            this.colAminoAcid2.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.colAminoAcid2.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // lblTitle
            // 
            resources.ApplyResources(this.lblTitle, "lblTitle");
            this.lblTitle.Name = "lblTitle";
            // 
            // tbxPrimaryPeptide
            // 
            resources.ApplyResources(this.tbxPrimaryPeptide, "tbxPrimaryPeptide");
            this.tbxPrimaryPeptide.Name = "tbxPrimaryPeptide";
            this.tbxPrimaryPeptide.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn1
            // 
            this.dataGridViewTextBoxColumn1.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.dataGridViewTextBoxColumn1.DataPropertyName = "Sequence";
            resources.ApplyResources(this.dataGridViewTextBoxColumn1, "dataGridViewTextBoxColumn1");
            this.dataGridViewTextBoxColumn1.Name = "dataGridViewTextBoxColumn1";
            this.dataGridViewTextBoxColumn1.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn2
            // 
            this.dataGridViewTextBoxColumn2.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.dataGridViewTextBoxColumn2.DataPropertyName = "Sequence";
            resources.ApplyResources(this.dataGridViewTextBoxColumn2, "dataGridViewTextBoxColumn2");
            this.dataGridViewTextBoxColumn2.Name = "dataGridViewTextBoxColumn2";
            // 
            // EditLinkedPeptidesDlg
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tbxPrimaryPeptide);
            this.Controls.Add(this.lblTitle);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.splitContainer1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditLinkedPeptidesDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewLinkedPeptides)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewCrosslinks)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private Controls.DataGridViewEx dataGridViewLinkedPeptides;
        private Controls.DataGridViewEx dataGridViewCrosslinks;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn1;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn2;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.TextBox tbxPrimaryPeptide;
        private System.Windows.Forms.Label lblLinkedPeptides;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPeptideSequence;
        private System.Windows.Forms.DataGridViewButtonColumn colModificationsButton;
        private System.Windows.Forms.Label lblCrosslinks;
        private System.Windows.Forms.DataGridViewComboBoxColumn colCrosslinker;
        private System.Windows.Forms.DataGridViewComboBoxColumn colPeptide1;
        private System.Windows.Forms.DataGridViewComboBoxColumn colAminoAcid1;
        private System.Windows.Forms.DataGridViewComboBoxColumn colPeptide2;
        private System.Windows.Forms.DataGridViewComboBoxColumn colAminoAcid2;
    }
}