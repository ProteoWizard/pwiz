namespace pwiz.Skyline.Alerts
{
    partial class SpectrumLibraryInfoDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SpectrumLibraryInfoDlg));
            this.btnOk = new System.Windows.Forms.Button();
            this.libraryGridView = new pwiz.Skyline.Controls.DataGridViewEx();
            this.fileNameCol = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.scoreTypeCol = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.cutoffScoreCol = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.matchingTimesCol = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.bestTimesCol = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.labelLibInfo = new System.Windows.Forms.Label();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.linkSpecLibLinks = new System.Windows.Forms.LinkLabel();
            ((System.ComponentModel.ISupportInitialize)(this.libraryGridView)).BeginInit();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Cursor = System.Windows.Forms.Cursors.AppStarting;
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // libraryGridView
            // 
            this.libraryGridView.AllowUserToAddRows = false;
            this.libraryGridView.AllowUserToDeleteRows = false;
            this.libraryGridView.AllowUserToResizeRows = false;
            this.libraryGridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.libraryGridView.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this.libraryGridView.BackgroundColor = System.Drawing.SystemColors.ButtonFace;
            this.libraryGridView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.libraryGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.fileNameCol,
            this.scoreTypeCol,
            this.cutoffScoreCol,
            this.matchingTimesCol,
            this.bestTimesCol});
            resources.ApplyResources(this.libraryGridView, "libraryGridView");
            this.libraryGridView.Name = "libraryGridView";
            this.libraryGridView.RowHeadersVisible = false;
            this.libraryGridView.CellPainting += new System.Windows.Forms.DataGridViewCellPaintingEventHandler(this.libraryGridView_CellPainting);
            // 
            // fileNameCol
            // 
            this.fileNameCol.DataPropertyName = "FileName";
            resources.ApplyResources(this.fileNameCol, "fileNameCol");
            this.fileNameCol.Name = "fileNameCol";
            // 
            // scoreTypeCol
            // 
            this.scoreTypeCol.DataPropertyName = "ScoreType";
            resources.ApplyResources(this.scoreTypeCol, "scoreTypeCol");
            this.scoreTypeCol.Name = "scoreTypeCol";
            // 
            // cutoffScoreCol
            // 
            this.cutoffScoreCol.DataPropertyName = "ScoreThreshold";
            resources.ApplyResources(this.cutoffScoreCol, "cutoffScoreCol");
            this.cutoffScoreCol.Name = "cutoffScoreCol";
            // 
            // matchingTimesCol
            // 
            this.matchingTimesCol.DataPropertyName = "MatchedCount";
            resources.ApplyResources(this.matchingTimesCol, "matchingTimesCol");
            this.matchingTimesCol.Name = "matchingTimesCol";
            // 
            // bestTimesCol
            // 
            this.bestTimesCol.DataPropertyName = "SpectrumCount";
            resources.ApplyResources(this.bestTimesCol, "bestTimesCol");
            this.bestTimesCol.Name = "bestTimesCol";
            // 
            // labelLibInfo
            // 
            resources.ApplyResources(this.labelLibInfo, "labelLibInfo");
            this.labelLibInfo.Name = "labelLibInfo";
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(this.tableLayoutPanel1, "tableLayoutPanel1");
            this.tableLayoutPanel1.Controls.Add(this.labelLibInfo, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.linkSpecLibLinks, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.libraryGridView, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.btnOk, 0, 3);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // linkSpecLibLinks
            // 
            resources.ApplyResources(this.linkSpecLibLinks, "linkSpecLibLinks");
            this.linkSpecLibLinks.Name = "linkSpecLibLinks";
            this.linkSpecLibLinks.TabStop = true;
            this.linkSpecLibLinks.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);
            // 
            // SpectrumLibraryInfoDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnOk;
            this.CausesValidation = false;
            this.Controls.Add(this.tableLayoutPanel1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SpectrumLibraryInfoDlg";
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.libraryGridView)).EndInit();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Label labelLibInfo;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.LinkLabel linkSpecLibLinks;
        private pwiz.Skyline.Controls.DataGridViewEx libraryGridView;
        private System.Windows.Forms.DataGridViewTextBoxColumn fileNameCol;
        private System.Windows.Forms.DataGridViewTextBoxColumn scoreTypeCol;
        private System.Windows.Forms.DataGridViewTextBoxColumn cutoffScoreCol;
        private System.Windows.Forms.DataGridViewTextBoxColumn matchingTimesCol;
        private System.Windows.Forms.DataGridViewTextBoxColumn bestTimesCol;
    }
}