namespace Forms
{
    partial class ProteinValidationPane
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.splitContainer3 = new System.Windows.Forms.SplitContainer();
            this.proteinGrid = new System.Windows.Forms.DataGridView();
            this.peptideTree = new BrightIdeasSoftware.TreeListView();
            this.splitContainer4 = new System.Windows.Forms.SplitContainer();
            this.spectraGrid = new System.Windows.Forms.DataGridView();
            this.splitContainer5 = new System.Windows.Forms.SplitContainer();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            this.splitContainer3.Panel1.SuspendLayout();
            this.splitContainer3.Panel2.SuspendLayout();
            this.splitContainer3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.proteinGrid)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.peptideTree)).BeginInit();
            this.splitContainer4.Panel1.SuspendLayout();
            this.splitContainer4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spectraGrid)).BeginInit();
            this.splitContainer5.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.splitContainer2);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.splitContainer5);
            this.splitContainer1.Size = new System.Drawing.Size(1146, 569);
            this.splitContainer1.SplitterDistance = 352;
            this.splitContainer1.TabIndex = 0;
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Name = "splitContainer2";
            this.splitContainer2.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.splitContainer3);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.splitContainer4);
            this.splitContainer2.Size = new System.Drawing.Size(1146, 352);
            this.splitContainer2.SplitterDistance = 187;
            this.splitContainer2.TabIndex = 0;
            // 
            // splitContainer3
            // 
            this.splitContainer3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer3.Location = new System.Drawing.Point(0, 0);
            this.splitContainer3.Name = "splitContainer3";
            // 
            // splitContainer3.Panel1
            // 
            this.splitContainer3.Panel1.Controls.Add(this.proteinGrid);
            // 
            // splitContainer3.Panel2
            // 
            this.splitContainer3.Panel2.Controls.Add(this.peptideTree);
            this.splitContainer3.Size = new System.Drawing.Size(1146, 187);
            this.splitContainer3.SplitterDistance = 489;
            this.splitContainer3.TabIndex = 0;
            // 
            // proteinGrid
            // 
            this.proteinGrid.AllowUserToAddRows = false;
            this.proteinGrid.AllowUserToDeleteRows = false;
            this.proteinGrid.BackgroundColor = System.Drawing.SystemColors.Control;
            this.proteinGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.proteinGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.proteinGrid.Location = new System.Drawing.Point(0, 0);
            this.proteinGrid.Name = "proteinGrid";
            this.proteinGrid.ReadOnly = true;
            this.proteinGrid.RowTemplate.Height = 24;
            this.proteinGrid.Size = new System.Drawing.Size(489, 187);
            this.proteinGrid.TabIndex = 0;
            // 
            // peptideTree
            // 
            this.peptideTree.AllowDrop = true;
            this.peptideTree.AlternateRowBackColor = System.Drawing.Color.White;
            this.peptideTree.BackColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.peptideTree.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.peptideTree.Dock = System.Windows.Forms.DockStyle.Fill;
            this.peptideTree.HeaderFont = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.peptideTree.HideSelection = false;
            this.peptideTree.HighlightForegroundColor = System.Drawing.Color.White;
            this.peptideTree.Location = new System.Drawing.Point(0, 0);
            this.peptideTree.Name = "peptideTree";
            this.peptideTree.OwnerDraw = true;
            this.peptideTree.SelectedColumnTint = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.peptideTree.ShowGroups = false;
            this.peptideTree.Size = new System.Drawing.Size(653, 187);
            this.peptideTree.TabIndex = 0;
            this.peptideTree.TintSortColumn = true;
            this.peptideTree.UseCompatibleStateImageBehavior = false;
            this.peptideTree.View = System.Windows.Forms.View.Details;
            this.peptideTree.VirtualMode = true;
            // 
            // splitContainer4
            // 
            this.splitContainer4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer4.Location = new System.Drawing.Point(0, 0);
            this.splitContainer4.Name = "splitContainer4";
            // 
            // splitContainer4.Panel1
            // 
            this.splitContainer4.Panel1.Controls.Add(this.spectraGrid);
            this.splitContainer4.Size = new System.Drawing.Size(1146, 161);
            this.splitContainer4.SplitterDistance = 482;
            this.splitContainer4.TabIndex = 0;
            // 
            // spectraGrid
            // 
            this.spectraGrid.AllowUserToAddRows = false;
            this.spectraGrid.AllowUserToDeleteRows = false;
            this.spectraGrid.BackgroundColor = System.Drawing.SystemColors.Control;
            this.spectraGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.spectraGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.spectraGrid.Location = new System.Drawing.Point(0, 0);
            this.spectraGrid.Name = "spectraGrid";
            this.spectraGrid.ReadOnly = true;
            this.spectraGrid.RowTemplate.Height = 24;
            this.spectraGrid.Size = new System.Drawing.Size(482, 161);
            this.spectraGrid.TabIndex = 0;
            this.spectraGrid.Visible = false;
            // 
            // splitContainer5
            // 
            this.splitContainer5.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer5.Location = new System.Drawing.Point(0, 0);
            this.splitContainer5.Name = "splitContainer5";
            this.splitContainer5.Size = new System.Drawing.Size(1146, 213);
            this.splitContainer5.SplitterDistance = 933;
            this.splitContainer5.TabIndex = 0;
            // 
            // ProteinValidationPane
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Inherit;
            this.AutoSize = true;
            this.Controls.Add(this.splitContainer1);
            this.Name = "ProteinValidationPane";
            this.Size = new System.Drawing.Size(1146, 569);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.ResumeLayout(false);
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            this.splitContainer2.ResumeLayout(false);
            this.splitContainer3.Panel1.ResumeLayout(false);
            this.splitContainer3.Panel2.ResumeLayout(false);
            this.splitContainer3.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.proteinGrid)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.peptideTree)).EndInit();
            this.splitContainer4.Panel1.ResumeLayout(false);
            this.splitContainer4.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.spectraGrid)).EndInit();
            this.splitContainer5.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.SplitContainer splitContainer3;
        private System.Windows.Forms.DataGridView proteinGrid;
        private System.Windows.Forms.SplitContainer splitContainer4;
        private System.Windows.Forms.DataGridView spectraGrid;
        private BrightIdeasSoftware.TreeListView peptideTree;
        private System.Windows.Forms.SplitContainer splitContainer5;


    }
}
