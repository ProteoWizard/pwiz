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
            resources.ApplyResources(this.databoundGridControl, "databoundGridControl");
            // 
            // statusPanel
            // 
            this.statusPanel.Controls.Add(this.btnCancelReadingFile);
            this.statusPanel.Controls.Add(this.progressBar1);
            this.statusPanel.Controls.Add(this.lblStatus);
            resources.ApplyResources(this.statusPanel, "statusPanel");
            this.statusPanel.Name = "statusPanel";
            // 
            // btnCancelReadingFile
            // 
            resources.ApplyResources(this.btnCancelReadingFile, "btnCancelReadingFile");
            this.btnCancelReadingFile.ImageList = this.imageList1;
            this.btnCancelReadingFile.Name = "btnCancelReadingFile";
            this.toolTip1.SetToolTip(this.btnCancelReadingFile, resources.GetString("btnCancelReadingFile.ToolTip"));
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
            resources.ApplyResources(this.progressBar1, "progressBar1");
            this.progressBar1.Name = "progressBar1";
            // 
            // lblStatus
            // 
            resources.ApplyResources(this.lblStatus, "lblStatus");
            this.lblStatus.Name = "lblStatus";
            // 
            // splitContainer1
            // 
            resources.ApplyResources(this.splitContainer1, "splitContainer1");
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.lblSummary);
            this.splitContainer1.Panel1.Controls.Add(this.btnRemoveFile);
            this.splitContainer1.Panel1.Controls.Add(this.btnBrowse);
            this.splitContainer1.Panel1.Controls.Add(this.btnAddSpectrumFilter);
            this.splitContainer1.Panel1.Controls.Add(this.checkedListBoxSpectrumClassColumns);
            this.splitContainer1.Panel1.Controls.Add(this.listBoxFiles);
            // 
            // lblSummary
            // 
            resources.ApplyResources(this.lblSummary, "lblSummary");
            this.lblSummary.AutoEllipsis = true;
            this.lblSummary.Name = "lblSummary";
            // 
            // btnRemoveFile
            // 
            resources.ApplyResources(this.btnRemoveFile, "btnRemoveFile");
            this.btnRemoveFile.ImageList = this.imageList1;
            this.btnRemoveFile.Name = "btnRemoveFile";
            this.toolTip1.SetToolTip(this.btnRemoveFile, resources.GetString("btnRemoveFile.ToolTip"));
            this.btnRemoveFile.UseVisualStyleBackColor = true;
            this.btnRemoveFile.Click += new System.EventHandler(this.btnRemoveFile_Click);
            // 
            // btnBrowse
            // 
            resources.ApplyResources(this.btnBrowse, "btnBrowse");
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // btnAddSpectrumFilter
            // 
            resources.ApplyResources(this.btnAddSpectrumFilter, "btnAddSpectrumFilter");
            this.btnAddSpectrumFilter.Name = "btnAddSpectrumFilter";
            this.btnAddSpectrumFilter.UseVisualStyleBackColor = true;
            this.btnAddSpectrumFilter.Click += new System.EventHandler(this.btnAddSpectrumFilter_Click);
            // 
            // checkedListBoxSpectrumClassColumns
            // 
            resources.ApplyResources(this.checkedListBoxSpectrumClassColumns, "checkedListBoxSpectrumClassColumns");
            this.checkedListBoxSpectrumClassColumns.FormattingEnabled = true;
            this.checkedListBoxSpectrumClassColumns.Name = "checkedListBoxSpectrumClassColumns";
            this.checkedListBoxSpectrumClassColumns.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.checkedListBoxSpectrumClassColumns_ItemCheck);
            // 
            // listBoxFiles
            // 
            resources.ApplyResources(this.listBoxFiles, "listBoxFiles");
            this.listBoxFiles.FormattingEnabled = true;
            this.listBoxFiles.Name = "listBoxFiles";
            this.listBoxFiles.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.toolTip1.SetToolTip(this.listBoxFiles, resources.GetString("listBoxFiles.ToolTip"));
            this.listBoxFiles.SelectedIndexChanged += new System.EventHandler(this.listBoxFiles_SelectedIndexChanged);
            // 
            // SpectrumGridForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.statusPanel);
            this.Name = "SpectrumGridForm";
            this.ShowIcon = false;
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