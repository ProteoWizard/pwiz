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
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.dataGridViewLinkedPeptides = new System.Windows.Forms.DataGridView();
            this.colModificationsButton = new System.Windows.Forms.DataGridViewButtonColumn();
            this.toolStripPeptides = new System.Windows.Forms.MenuStrip();
            this.btnUpPeptide = new System.Windows.Forms.ToolStripButton();
            this.btnDownPeptide = new System.Windows.Forms.ToolStripButton();
            this.btnDeletePeptide = new System.Windows.Forms.ToolStripButton();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.btnUpCrosslink = new System.Windows.Forms.ToolStripButton();
            this.btnDownCrosslink = new System.Windows.Forms.ToolStripButton();
            this.btnDeleteCrosslink = new System.Windows.Forms.ToolStripButton();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.dataGridViewTextBoxColumn1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPeptideNumber = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPeptideSequence = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewCrosslinks = new pwiz.Skyline.Controls.DataGridViewEx();
            this.colCrosslinker = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colPeptide1 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colAminoAcid1 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colPeptide2 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colAminoAcid2 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewLinkedPeptides)).BeginInit();
            this.toolStripPeptides.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewCrosslinks)).BeginInit();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer1.Location = new System.Drawing.Point(0, 31);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
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
            this.splitContainer1.Size = new System.Drawing.Size(675, 241);
            this.splitContainer1.SplitterDistance = 120;
            this.splitContainer1.TabIndex = 0;
            // 
            // dataGridViewLinkedPeptides
            // 
            this.dataGridViewLinkedPeptides.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewLinkedPeptides.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colPeptideNumber,
            this.colPeptideSequence,
            this.colModificationsButton});
            this.dataGridViewLinkedPeptides.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewLinkedPeptides.Location = new System.Drawing.Point(0, 0);
            this.dataGridViewLinkedPeptides.Name = "dataGridViewLinkedPeptides";
            this.dataGridViewLinkedPeptides.Size = new System.Drawing.Size(643, 120);
            this.dataGridViewLinkedPeptides.TabIndex = 0;
            this.dataGridViewLinkedPeptides.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridViewLinkedPeptides_CellContentClick);
            this.dataGridViewLinkedPeptides.CellErrorTextNeeded += new System.Windows.Forms.DataGridViewCellErrorTextNeededEventHandler(this.dataGridViewLinkedPeptides_CellErrorTextNeeded);
            // 
            // colModificationsButton
            // 
            this.colModificationsButton.HeaderText = "";
            this.colModificationsButton.Name = "colModificationsButton";
            this.colModificationsButton.ReadOnly = true;
            this.colModificationsButton.Width = 20;
            // 
            // toolStripPeptides
            // 
            this.toolStripPeptides.AutoSize = false;
            this.toolStripPeptides.Dock = System.Windows.Forms.DockStyle.Right;
            this.toolStripPeptides.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnUpPeptide,
            this.btnDownPeptide,
            this.btnDeletePeptide});
            this.toolStripPeptides.Location = new System.Drawing.Point(643, 0);
            this.toolStripPeptides.Name = "toolStripPeptides";
            this.toolStripPeptides.Size = new System.Drawing.Size(32, 120);
            this.toolStripPeptides.TabIndex = 1;
            this.toolStripPeptides.Text = "menuStrip1";
            // 
            // btnUpPeptide
            // 
            this.btnUpPeptide.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnUpPeptide.Image = global::pwiz.Skyline.Properties.Resources.up_pro32;
            this.btnUpPeptide.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnUpPeptide.Name = "btnUpPeptide";
            this.btnUpPeptide.Size = new System.Drawing.Size(25, 20);
            this.btnUpPeptide.Text = "Remove";
            // 
            // btnDownPeptide
            // 
            this.btnDownPeptide.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnDownPeptide.Image = global::pwiz.Skyline.Properties.Resources.down_pro32;
            this.btnDownPeptide.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnDownPeptide.Name = "btnDownPeptide";
            this.btnDownPeptide.Size = new System.Drawing.Size(25, 20);
            this.btnDownPeptide.Text = "Up";
            // 
            // btnDeletePeptide
            // 
            this.btnDeletePeptide.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnDeletePeptide.Image = global::pwiz.Skyline.Properties.Resources.Delete;
            this.btnDeletePeptide.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnDeletePeptide.Name = "btnDeletePeptide";
            this.btnDeletePeptide.Size = new System.Drawing.Size(25, 20);
            this.btnDeletePeptide.Text = "Down";
            // 
            // menuStrip1
            // 
            this.menuStrip1.AutoSize = false;
            this.menuStrip1.Dock = System.Windows.Forms.DockStyle.Right;
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnUpCrosslink,
            this.btnDownCrosslink,
            this.btnDeleteCrosslink});
            this.menuStrip1.Location = new System.Drawing.Point(643, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(32, 117);
            this.menuStrip1.TabIndex = 2;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // btnUpCrosslink
            // 
            this.btnUpCrosslink.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnUpCrosslink.Image = global::pwiz.Skyline.Properties.Resources.up_pro32;
            this.btnUpCrosslink.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnUpCrosslink.Name = "btnUpCrosslink";
            this.btnUpCrosslink.Size = new System.Drawing.Size(25, 20);
            this.btnUpCrosslink.Text = "Remove";
            // 
            // btnDownCrosslink
            // 
            this.btnDownCrosslink.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnDownCrosslink.Image = global::pwiz.Skyline.Properties.Resources.down_pro32;
            this.btnDownCrosslink.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnDownCrosslink.Name = "btnDownCrosslink";
            this.btnDownCrosslink.Size = new System.Drawing.Size(25, 20);
            this.btnDownCrosslink.Text = "Up";
            // 
            // btnDeleteCrosslink
            // 
            this.btnDeleteCrosslink.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnDeleteCrosslink.Image = global::pwiz.Skyline.Properties.Resources.Delete;
            this.btnDeleteCrosslink.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnDeleteCrosslink.Name = "btnDeleteCrosslink";
            this.btnDeleteCrosslink.Size = new System.Drawing.Size(25, 20);
            this.btnDeleteCrosslink.Text = "Down";
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnCancel.Location = new System.Drawing.Point(586, 284);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 9;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOk.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnOk.Location = new System.Drawing.Point(505, 284);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 8;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            // 
            // dataGridViewTextBoxColumn1
            // 
            this.dataGridViewTextBoxColumn1.HeaderText = "#";
            this.dataGridViewTextBoxColumn1.Name = "dataGridViewTextBoxColumn1";
            this.dataGridViewTextBoxColumn1.ReadOnly = true;
            this.dataGridViewTextBoxColumn1.Width = 40;
            // 
            // dataGridViewTextBoxColumn2
            // 
            this.dataGridViewTextBoxColumn2.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.dataGridViewTextBoxColumn2.DataPropertyName = "Sequence";
            this.dataGridViewTextBoxColumn2.HeaderText = "Peptide Sequence";
            this.dataGridViewTextBoxColumn2.Name = "dataGridViewTextBoxColumn2";
            // 
            // colPeptideNumber
            // 
            this.colPeptideNumber.HeaderText = "#";
            this.colPeptideNumber.Name = "colPeptideNumber";
            this.colPeptideNumber.ReadOnly = true;
            this.colPeptideNumber.Width = 40;
            // 
            // colPeptideSequence
            // 
            this.colPeptideSequence.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colPeptideSequence.DataPropertyName = "Sequence";
            this.colPeptideSequence.HeaderText = "Peptide Sequence";
            this.colPeptideSequence.Name = "colPeptideSequence";
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
            this.dataGridViewCrosslinks.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewCrosslinks.Location = new System.Drawing.Point(0, 0);
            this.dataGridViewCrosslinks.Name = "dataGridViewCrosslinks";
            this.dataGridViewCrosslinks.Size = new System.Drawing.Size(643, 117);
            this.dataGridViewCrosslinks.TabIndex = 0;
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
            this.colCrosslinker.HeaderText = "Crosslinker";
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
            this.colPeptide1.HeaderText = "Peptide #1";
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
            this.colAminoAcid1.HeaderText = "Amino Acid #1";
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
            this.colPeptide2.HeaderText = "Peptide #2";
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
            this.colAminoAcid2.HeaderText = "Amino Acid #2";
            this.colAminoAcid2.Name = "colAminoAcid2";
            this.colAminoAcid2.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.colAminoAcid2.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // EditLinkedPeptidesDlg
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(673, 319);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.splitContainer1);
            this.MainMenuStrip = this.toolStripPeptides;
            this.Name = "EditLinkedPeptidesDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit Linked Peptides";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewLinkedPeptides)).EndInit();
            this.toolStripPeptides.ResumeLayout(false);
            this.toolStripPeptides.PerformLayout();
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewCrosslinks)).EndInit();
            this.ResumeLayout(false);

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
        private System.Windows.Forms.DataGridViewTextBoxColumn colPeptideNumber;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPeptideSequence;
        private System.Windows.Forms.DataGridViewButtonColumn colModificationsButton;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.DataGridViewComboBoxColumn colCrosslinker;
        private System.Windows.Forms.DataGridViewComboBoxColumn colPeptide1;
        private System.Windows.Forms.DataGridViewComboBoxColumn colAminoAcid1;
        private System.Windows.Forms.DataGridViewComboBoxColumn colPeptide2;
        private System.Windows.Forms.DataGridViewComboBoxColumn colAminoAcid2;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn1;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn2;
    }
}