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
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.dataGridViewLinkedPeptides = new System.Windows.Forms.DataGridView();
            this.colPeptideSequence = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colModificationsButton = new System.Windows.Forms.DataGridViewButtonColumn();
            this.toolStripPeptides = new System.Windows.Forms.MenuStrip();
            this.btnUpPeptide = new System.Windows.Forms.ToolStripButton();
            this.btnDownPeptide = new System.Windows.Forms.ToolStripButton();
            this.btnDeletePeptide = new System.Windows.Forms.ToolStripButton();
            this.dataGridViewCrosslinks = new pwiz.Skyline.Controls.DataGridViewEx();
            this.colCrosslinker = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colPeptide1 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colAminoAcid1 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colPeptide2 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colAminoAcid2 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.btnUpCrosslink = new System.Windows.Forms.ToolStripButton();
            this.btnDownCrosslink = new System.Windows.Forms.ToolStripButton();
            this.btnDeleteCrosslink = new System.Windows.Forms.ToolStripButton();
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
            this.toolStripPeptides.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewCrosslinks)).BeginInit();
            this.menuStrip1.SuspendLayout();
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
            this.splitContainer1.Panel1.Controls.Add(this.toolStripPeptides);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.dataGridViewCrosslinks);
            this.splitContainer1.Panel2.Controls.Add(this.menuStrip1);
            // 
            // dataGridViewLinkedPeptides
            // 
            this.dataGridViewLinkedPeptides.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewLinkedPeptides.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colPeptideSequence,
            this.colModificationsButton});
            resources.ApplyResources(this.dataGridViewLinkedPeptides, "dataGridViewLinkedPeptides");
            this.dataGridViewLinkedPeptides.Name = "dataGridViewLinkedPeptides";
            this.dataGridViewLinkedPeptides.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridViewLinkedPeptides_CellContentClick);
            this.dataGridViewLinkedPeptides.CellErrorTextNeeded += new System.Windows.Forms.DataGridViewCellErrorTextNeededEventHandler(this.dataGridViewLinkedPeptides_CellErrorTextNeeded);
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
            resources.ApplyResources(this.colModificationsButton, "colModificationsButton");
            this.colModificationsButton.Name = "colModificationsButton";
            this.colModificationsButton.ReadOnly = true;
            // 
            // toolStripPeptides
            // 
            resources.ApplyResources(this.toolStripPeptides, "toolStripPeptides");
            this.toolStripPeptides.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnUpPeptide,
            this.btnDownPeptide,
            this.btnDeletePeptide});
            this.toolStripPeptides.Name = "toolStripPeptides";
            // 
            // btnUpPeptide
            // 
            this.btnUpPeptide.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnUpPeptide.Image = global::pwiz.Skyline.Properties.Resources.up_pro32;
            resources.ApplyResources(this.btnUpPeptide, "btnUpPeptide");
            this.btnUpPeptide.Name = "btnUpPeptide";
            // 
            // btnDownPeptide
            // 
            this.btnDownPeptide.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnDownPeptide.Image = global::pwiz.Skyline.Properties.Resources.down_pro32;
            resources.ApplyResources(this.btnDownPeptide, "btnDownPeptide");
            this.btnDownPeptide.Name = "btnDownPeptide";
            // 
            // btnDeletePeptide
            // 
            this.btnDeletePeptide.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnDeletePeptide.Image = global::pwiz.Skyline.Properties.Resources.Delete;
            resources.ApplyResources(this.btnDeletePeptide, "btnDeletePeptide");
            this.btnDeletePeptide.Name = "btnDeletePeptide";
            // 
            // dataGridViewCrosslinks
            // 
            this.dataGridViewCrosslinks.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridViewCrosslinks.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewCrosslinks.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colCrosslinker,
            this.colPeptide1,
            this.colAminoAcid1,
            this.colPeptide2,
            this.colAminoAcid2});
            resources.ApplyResources(this.dataGridViewCrosslinks, "dataGridViewCrosslinks");
            this.dataGridViewCrosslinks.Name = "dataGridViewCrosslinks";
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
            // menuStrip1
            // 
            resources.ApplyResources(this.menuStrip1, "menuStrip1");
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnUpCrosslink,
            this.btnDownCrosslink,
            this.btnDeleteCrosslink});
            this.menuStrip1.Name = "menuStrip1";
            // 
            // btnUpCrosslink
            // 
            this.btnUpCrosslink.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnUpCrosslink.Image = global::pwiz.Skyline.Properties.Resources.up_pro32;
            resources.ApplyResources(this.btnUpCrosslink, "btnUpCrosslink");
            this.btnUpCrosslink.Name = "btnUpCrosslink";
            // 
            // btnDownCrosslink
            // 
            this.btnDownCrosslink.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnDownCrosslink.Image = global::pwiz.Skyline.Properties.Resources.down_pro32;
            resources.ApplyResources(this.btnDownCrosslink, "btnDownCrosslink");
            this.btnDownCrosslink.Name = "btnDownCrosslink";
            // 
            // btnDeleteCrosslink
            // 
            this.btnDeleteCrosslink.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnDeleteCrosslink.Image = global::pwiz.Skyline.Properties.Resources.Delete;
            resources.ApplyResources(this.btnDeleteCrosslink, "btnDeleteCrosslink");
            this.btnDeleteCrosslink.Name = "btnDeleteCrosslink";
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
            this.MainMenuStrip = this.toolStripPeptides;
            this.Name = "EditLinkedPeptidesDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewLinkedPeptides)).EndInit();
            this.toolStripPeptides.ResumeLayout(false);
            this.toolStripPeptides.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewCrosslinks)).EndInit();
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.DataGridView dataGridViewLinkedPeptides;
        private System.Windows.Forms.MenuStrip toolStripPeptides;
        private System.Windows.Forms.ToolStripButton btnUpPeptide;
        private System.Windows.Forms.ToolStripButton btnDownPeptide;
        private System.Windows.Forms.ToolStripButton btnDeletePeptide;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripButton btnUpCrosslink;
        private System.Windows.Forms.ToolStripButton btnDownCrosslink;
        private System.Windows.Forms.ToolStripButton btnDeleteCrosslink;
        private Controls.DataGridViewEx dataGridViewCrosslinks;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.DataGridViewComboBoxColumn colCrosslinker;
        private System.Windows.Forms.DataGridViewComboBoxColumn colPeptide1;
        private System.Windows.Forms.DataGridViewComboBoxColumn colAminoAcid1;
        private System.Windows.Forms.DataGridViewComboBoxColumn colPeptide2;
        private System.Windows.Forms.DataGridViewComboBoxColumn colAminoAcid2;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn1;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn2;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPeptideSequence;
        private System.Windows.Forms.DataGridViewButtonColumn colModificationsButton;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.TextBox tbxPrimaryPeptide;
    }
}