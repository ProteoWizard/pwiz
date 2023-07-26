namespace pwiz.Skyline.Controls.Spectra
{
    partial class SpectrumGridForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SpectrumGridForm));
            this.statusPanel = new System.Windows.Forms.Panel();
            this.btnCancelReadingFile = new System.Windows.Forms.Button();
            this.imageList1 = new System.Windows.Forms.ImageList(this.components);
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.lblStatus = new System.Windows.Forms.Label();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.lblSummary = new System.Windows.Forms.Label();
            this.btnRemoveFile = new System.Windows.Forms.Button();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.btnAddSpectrumFilter = new System.Windows.Forms.Button();
            this.checkedListBoxSpectrumClassColumns = new System.Windows.Forms.CheckedListBox();
            this.listBoxFiles = new System.Windows.Forms.ListBox();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.statusPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // databoundGridControl
            // 
            this.databoundGridControl.Dock = System.Windows.Forms.DockStyle.None;
            this.databoundGridControl.Location = new System.Drawing.Point(12, 123);
            this.databoundGridControl.Size = new System.Drawing.Size(765, 286);
            // 
            // statusPanel
            // 
            this.statusPanel.Controls.Add(this.btnCancelReadingFile);
            this.statusPanel.Controls.Add(this.progressBar1);
            this.statusPanel.Controls.Add(this.lblStatus);
            this.statusPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.statusPanel.Location = new System.Drawing.Point(0, 415);
            this.statusPanel.Name = "statusPanel";
            this.statusPanel.Size = new System.Drawing.Size(800, 35);
            this.statusPanel.TabIndex = 0;
            this.statusPanel.Visible = false;
            // 
            // btnCancelReadingFile
            // 
            this.btnCancelReadingFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancelReadingFile.ImageIndex = 0;
            this.btnCancelReadingFile.ImageList = this.imageList1;
            this.btnCancelReadingFile.Location = new System.Drawing.Point(767, 6);
            this.btnCancelReadingFile.Name = "btnCancelReadingFile";
            this.btnCancelReadingFile.Size = new System.Drawing.Size(23, 23);
            this.btnCancelReadingFile.TabIndex = 6;
            this.toolTip1.SetToolTip(this.btnCancelReadingFile, "Cancel");
            this.btnCancelReadingFile.UseVisualStyleBackColor = true;
            this.btnCancelReadingFile.Click += new System.EventHandler(this.btnCancelReadingFile_Click);
            // 
            // imageList1
            // 
            this.imageList1.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList1.ImageStream")));
            this.imageList1.TransparentColor = System.Drawing.Color.Magenta;
            this.imageList1.Images.SetKeyName(0, "Delete.bmp");
            // 
            // progressBar1
            // 
            this.progressBar1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar1.Location = new System.Drawing.Point(547, 6);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(217, 23);
            this.progressBar1.TabIndex = 1;
            // 
            // lblStatus
            // 
            this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblStatus.Location = new System.Drawing.Point(3, 6);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(538, 23);
            this.lblStatus.TabIndex = 0;
            this.lblStatus.Text = "File reading status";
            this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.lblSummary);
            this.splitContainer1.Panel1.Controls.Add(this.btnRemoveFile);
            this.splitContainer1.Panel1.Controls.Add(this.btnBrowse);
            this.splitContainer1.Panel1.Controls.Add(this.btnAddSpectrumFilter);
            this.splitContainer1.Panel1.Controls.Add(this.checkedListBoxSpectrumClassColumns);
            this.splitContainer1.Panel1.Controls.Add(this.listBoxFiles);
            this.splitContainer1.Size = new System.Drawing.Size(800, 415);
            this.splitContainer1.SplitterDistance = 122;
            this.splitContainer1.TabIndex = 2;
            // 
            // lblSummary
            // 
            this.lblSummary.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblSummary.AutoEllipsis = true;
            this.lblSummary.Location = new System.Drawing.Point(10, 100);
            this.lblSummary.Name = "lblSummary";
            this.lblSummary.Size = new System.Drawing.Size(623, 23);
            this.lblSummary.TabIndex = 6;
            this.lblSummary.Text = "Summary";
            // 
            // btnRemoveFile
            // 
            this.btnRemoveFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRemoveFile.Enabled = false;
            this.btnRemoveFile.ImageIndex = 0;
            this.btnRemoveFile.ImageList = this.imageList1;
            this.btnRemoveFile.Location = new System.Drawing.Point(639, 3);
            this.btnRemoveFile.Name = "btnRemoveFile";
            this.btnRemoveFile.Size = new System.Drawing.Size(23, 23);
            this.btnRemoveFile.TabIndex = 5;
            this.toolTip1.SetToolTip(this.btnRemoveFile, "Remove from list");
            this.btnRemoveFile.UseVisualStyleBackColor = true;
            this.btnRemoveFile.Click += new System.EventHandler(this.btnRemoveFile_Click);
            // 
            // btnBrowse
            // 
            this.btnBrowse.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowse.Location = new System.Drawing.Point(675, 3);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(113, 28);
            this.btnBrowse.TabIndex = 1;
            this.btnBrowse.Text = "Browse...";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // btnAddSpectrumFilter
            // 
            this.btnAddSpectrumFilter.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAddSpectrumFilter.Location = new System.Drawing.Point(639, 96);
            this.btnAddSpectrumFilter.Name = "btnAddSpectrumFilter";
            this.btnAddSpectrumFilter.Size = new System.Drawing.Size(149, 23);
            this.btnAddSpectrumFilter.TabIndex = 4;
            this.btnAddSpectrumFilter.Text = "Add Spectrum Filter...";
            this.btnAddSpectrumFilter.UseVisualStyleBackColor = true;
            this.btnAddSpectrumFilter.Click += new System.EventHandler(this.btnAddSpectrumFilter_Click);
            // 
            // checkedListBoxSpectrumClassColumns
            // 
            this.checkedListBoxSpectrumClassColumns.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.checkedListBoxSpectrumClassColumns.FormattingEnabled = true;
            this.checkedListBoxSpectrumClassColumns.IntegralHeight = false;
            this.checkedListBoxSpectrumClassColumns.Location = new System.Drawing.Point(3, 3);
            this.checkedListBoxSpectrumClassColumns.Name = "checkedListBoxSpectrumClassColumns";
            this.checkedListBoxSpectrumClassColumns.Size = new System.Drawing.Size(285, 93);
            this.checkedListBoxSpectrumClassColumns.TabIndex = 3;
            this.checkedListBoxSpectrumClassColumns.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.checkedListBoxSpectrumClassColumns_ItemCheck);
            // 
            // listBoxFiles
            // 
            this.listBoxFiles.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listBoxFiles.FormattingEnabled = true;
            this.listBoxFiles.IntegralHeight = false;
            this.listBoxFiles.Location = new System.Drawing.Point(294, 3);
            this.listBoxFiles.Name = "listBoxFiles";
            this.listBoxFiles.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.listBoxFiles.Size = new System.Drawing.Size(339, 93);
            this.listBoxFiles.TabIndex = 2;
            this.toolTip1.SetToolTip(this.listBoxFiles, "Remove from list");
            this.listBoxFiles.SelectedIndexChanged += new System.EventHandler(this.listBoxFiles_SelectedIndexChanged);
            // 
            // SpectraGridForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.statusPanel);
            this.Name = "SpectraGridForm";
            this.ShowIcon = false;
            this.TabText = "SpectraGridForm";
            this.Text = "SpectraGridForm";
            this.Controls.SetChildIndex(this.statusPanel, 0);
            this.Controls.SetChildIndex(this.splitContainer1, 0);
            this.Controls.SetChildIndex(this.databoundGridControl, 0);
            this.statusPanel.ResumeLayout(false);
            this.splitContainer1.Panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Panel statusPanel;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Button btnAddSpectrumFilter;
        private System.Windows.Forms.CheckedListBox checkedListBoxSpectrumClassColumns;
        private System.Windows.Forms.ListBox listBoxFiles;
        private System.Windows.Forms.Button btnRemoveFile;
        private System.Windows.Forms.ImageList imageList1;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.Button btnCancelReadingFile;
        private System.Windows.Forms.Label lblSummary;
    }
}