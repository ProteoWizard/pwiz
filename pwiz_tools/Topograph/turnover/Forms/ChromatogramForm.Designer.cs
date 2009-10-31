using pwiz.Topograph.ui.Controls;

namespace pwiz.Topograph.ui.Forms
{
    partial class ChromatogramForm
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
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.gridIntensities = new pwiz.Topograph.ui.Controls.ExcludedMzsGrid();
            this.cbxAutoFindPeak = new System.Windows.Forms.CheckBox();
            this.cbxOverrideExcludedMzs = new System.Windows.Forms.CheckBox();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.toolStripMenuItemSmooth = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemShowSpectrum = new System.Windows.Forms.ToolStripMenuItem();
            this.tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridIntensities)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.gridIntensities, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.cbxAutoFindPeak, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.cbxOverrideExcludedMzs, 0, 1);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(259, 533);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // gridIntensities
            // 
            this.gridIntensities.AllowUserToAddRows = false;
            this.gridIntensities.AllowUserToDeleteRows = false;
            this.tableLayoutPanel1.SetColumnSpan(this.gridIntensities, 2);
            this.gridIntensities.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridIntensities.Location = new System.Drawing.Point(3, 53);
            this.gridIntensities.Name = "gridIntensities";
            this.gridIntensities.PeptideAnalysis = null;
            this.gridIntensities.PeptideFileAnalysis = null;
            this.gridIntensities.Size = new System.Drawing.Size(253, 477);
            this.gridIntensities.TabIndex = 11;
            // 
            // cbxAutoFindPeak
            // 
            this.cbxAutoFindPeak.AutoSize = true;
            this.tableLayoutPanel1.SetColumnSpan(this.cbxAutoFindPeak, 2);
            this.cbxAutoFindPeak.Location = new System.Drawing.Point(3, 3);
            this.cbxAutoFindPeak.Name = "cbxAutoFindPeak";
            this.cbxAutoFindPeak.Size = new System.Drawing.Size(99, 17);
            this.cbxAutoFindPeak.TabIndex = 12;
            this.cbxAutoFindPeak.Text = "Auto Find Peak";
            this.cbxAutoFindPeak.UseVisualStyleBackColor = true;
            this.cbxAutoFindPeak.CheckedChanged += new System.EventHandler(this.cbxAutoFindPeak_CheckedChanged);
            // 
            // cbxOverrideExcludedMzs
            // 
            this.cbxOverrideExcludedMzs.AutoSize = true;
            this.tableLayoutPanel1.SetColumnSpan(this.cbxOverrideExcludedMzs, 2);
            this.cbxOverrideExcludedMzs.Location = new System.Drawing.Point(3, 28);
            this.cbxOverrideExcludedMzs.Name = "cbxOverrideExcludedMzs";
            this.cbxOverrideExcludedMzs.Size = new System.Drawing.Size(134, 17);
            this.cbxOverrideExcludedMzs.TabIndex = 13;
            this.cbxOverrideExcludedMzs.Text = "Override excluded Mzs";
            this.cbxOverrideExcludedMzs.UseVisualStyleBackColor = true;
            this.cbxOverrideExcludedMzs.CheckedChanged += new System.EventHandler(this.cbxOverrideExcludedMzs_CheckedChanged);
            // 
            // toolTip1
            // 
            this.toolTip1.ShowAlways = true;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.tableLayoutPanel1);
            this.splitContainer1.Size = new System.Drawing.Size(778, 533);
            this.splitContainer1.SplitterDistance = 259;
            this.splitContainer1.TabIndex = 2;
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItemSmooth,
            this.toolStripMenuItemShowSpectrum});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.ShowCheckMargin = true;
            this.contextMenuStrip1.Size = new System.Drawing.Size(180, 70);
            // 
            // toolStripMenuItemSmooth
            // 
            this.toolStripMenuItemSmooth.CheckOnClick = true;
            this.toolStripMenuItemSmooth.Name = "toolStripMenuItemSmooth";
            this.toolStripMenuItemSmooth.Size = new System.Drawing.Size(179, 22);
            this.toolStripMenuItemSmooth.Text = "Smooth";
            this.toolStripMenuItemSmooth.Click += new System.EventHandler(this.toolStripMenuItemSmooth_Click);
            // 
            // toolStripMenuItemShowSpectrum
            // 
            this.toolStripMenuItemShowSpectrum.Name = "toolStripMenuItemShowSpectrum";
            this.toolStripMenuItemShowSpectrum.Size = new System.Drawing.Size(179, 22);
            this.toolStripMenuItemShowSpectrum.Text = "Show Spectrum";
            this.toolStripMenuItemShowSpectrum.Click += new System.EventHandler(this.toolStripMenuItemShowSpectrum_Click);
            // 
            // ChromatogramForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(778, 533);
            this.Controls.Add(this.splitContainer1);
            this.Name = "ChromatogramForm";
            this.TabText = "ChromatogramForm";
            this.Text = "ChromatogramForm";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridIntensities)).EndInit();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.ResumeLayout(false);
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private ExcludedMzsGrid gridIntensities;
        private System.Windows.Forms.CheckBox cbxAutoFindPeak;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.CheckBox cbxOverrideExcludedMzs;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemSmooth;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemShowSpectrum;
    }
}