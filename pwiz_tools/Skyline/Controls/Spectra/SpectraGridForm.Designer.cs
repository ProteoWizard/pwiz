namespace pwiz.Skyline.Controls.Spectra
{
    partial class SpectraGridForm
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
            this.btnBrowse = new System.Windows.Forms.Button();
            this.statusPanel = new System.Windows.Forms.Panel();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.lblStatus = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.checkedListBoxSpectrumClassColumns = new System.Windows.Forms.CheckedListBox();
            this.listBoxFiles = new System.Windows.Forms.ListBox();
            this.btnAddSpectrumFilter = new System.Windows.Forms.Button();
            this.statusPanel.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // databoundGridControl
            // 
            this.databoundGridControl.Location = new System.Drawing.Point(0, 100);
            this.databoundGridControl.Size = new System.Drawing.Size(800, 315);
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
            // statusPanel
            // 
            this.statusPanel.Controls.Add(this.progressBar1);
            this.statusPanel.Controls.Add(this.lblStatus);
            this.statusPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.statusPanel.Location = new System.Drawing.Point(0, 415);
            this.statusPanel.Name = "statusPanel";
            this.statusPanel.Size = new System.Drawing.Size(800, 35);
            this.statusPanel.TabIndex = 0;
            // 
            // progressBar1
            // 
            this.progressBar1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar1.Location = new System.Drawing.Point(568, 6);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(220, 23);
            this.progressBar1.TabIndex = 1;
            // 
            // lblStatus
            // 
            this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblStatus.Location = new System.Drawing.Point(3, 6);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(559, 23);
            this.lblStatus.TabIndex = 0;
            this.lblStatus.Text = "label1";
            this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.btnAddSpectrumFilter);
            this.panel1.Controls.Add(this.checkedListBoxSpectrumClassColumns);
            this.panel1.Controls.Add(this.listBoxFiles);
            this.panel1.Controls.Add(this.btnBrowse);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(800, 100);
            this.panel1.TabIndex = 1;
            // 
            // checkedListBoxSpectrumClassColumns
            // 
            this.checkedListBoxSpectrumClassColumns.FormattingEnabled = true;
            this.checkedListBoxSpectrumClassColumns.IntegralHeight = false;
            this.checkedListBoxSpectrumClassColumns.Location = new System.Drawing.Point(6, 3);
            this.checkedListBoxSpectrumClassColumns.Name = "checkedListBoxSpectrumClassColumns";
            this.checkedListBoxSpectrumClassColumns.Size = new System.Drawing.Size(285, 91);
            this.checkedListBoxSpectrumClassColumns.TabIndex = 3;
            // 
            // listBoxFiles
            // 
            this.listBoxFiles.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listBoxFiles.FormattingEnabled = true;
            this.listBoxFiles.IntegralHeight = false;
            this.listBoxFiles.Location = new System.Drawing.Point(297, 3);
            this.listBoxFiles.Name = "listBoxFiles";
            this.listBoxFiles.Size = new System.Drawing.Size(363, 91);
            this.listBoxFiles.TabIndex = 2;
            // 
            // btnAddSpectrumFilter
            // 
            this.btnAddSpectrumFilter.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAddSpectrumFilter.Location = new System.Drawing.Point(675, 71);
            this.btnAddSpectrumFilter.Name = "btnAddSpectrumFilter";
            this.btnAddSpectrumFilter.Size = new System.Drawing.Size(113, 23);
            this.btnAddSpectrumFilter.TabIndex = 4;
            this.btnAddSpectrumFilter.Text = "Add Spectrum Filter";
            this.btnAddSpectrumFilter.UseVisualStyleBackColor = true;
            this.btnAddSpectrumFilter.Click += new System.EventHandler(this.btnAddSpectrumFilter_Click);
            // 
            // SpectraGridForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.statusPanel);
            this.Controls.Add(this.panel1);
            this.Name = "SpectraGridForm";
            this.TabText = "SpectraGridForm";
            this.Text = "SpectraGridForm";
            this.Controls.SetChildIndex(this.panel1, 0);
            this.Controls.SetChildIndex(this.statusPanel, 0);
            this.Controls.SetChildIndex(this.databoundGridControl, 0);
            this.statusPanel.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Panel statusPanel;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.ListBox listBoxFiles;
        private System.Windows.Forms.CheckedListBox checkedListBoxSpectrumClassColumns;
        private System.Windows.Forms.Button btnAddSpectrumFilter;
    }
}