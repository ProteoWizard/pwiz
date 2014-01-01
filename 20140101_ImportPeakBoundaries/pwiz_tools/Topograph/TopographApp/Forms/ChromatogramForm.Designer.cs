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
            this.gridIntensities = new pwiz.Topograph.ui.Controls.ExcludedMzsGrid();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.cbxAutoFindPeak = new System.Windows.Forms.CheckBox();
            this.cbxSmooth = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.gridIntensities)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // gridIntensities
            // 
            this.gridIntensities.AllowUserToAddRows = false;
            this.gridIntensities.AllowUserToDeleteRows = false;
            this.tableLayoutPanel1.SetColumnSpan(this.gridIntensities, 2);
            this.gridIntensities.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridIntensities.Location = new System.Drawing.Point(3, 78);
            this.gridIntensities.Name = "gridIntensities";
            this.gridIntensities.PeptideAnalysis = null;
            this.gridIntensities.PeptideFileAnalysis = null;
            this.gridIntensities.Size = new System.Drawing.Size(291, 183);
            this.gridIntensities.TabIndex = 3;
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
            this.splitContainer1.Size = new System.Drawing.Size(893, 264);
            this.splitContainer1.SplitterDistance = 297;
            this.splitContainer1.TabIndex = 3;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.cbxAutoFindPeak, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.gridIntensities, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.cbxSmooth, 0, 2);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 4;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(297, 264);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // cbxAutoFindPeak
            // 
            this.cbxAutoFindPeak.AutoSize = true;
            this.tableLayoutPanel1.SetColumnSpan(this.cbxAutoFindPeak, 2);
            this.cbxAutoFindPeak.Location = new System.Drawing.Point(3, 3);
            this.cbxAutoFindPeak.Name = "cbxAutoFindPeak";
            this.cbxAutoFindPeak.Size = new System.Drawing.Size(99, 17);
            this.cbxAutoFindPeak.TabIndex = 0;
            this.cbxAutoFindPeak.Text = "Auto Find Peak";
            this.cbxAutoFindPeak.UseVisualStyleBackColor = true;
            this.cbxAutoFindPeak.CheckedChanged += new System.EventHandler(this.CbxAutoFindPeakOnCheckedChanged);
            // 
            // cbxSmooth
            // 
            this.cbxSmooth.AutoSize = true;
            this.cbxSmooth.Location = new System.Drawing.Point(3, 53);
            this.cbxSmooth.Name = "cbxSmooth";
            this.cbxSmooth.Size = new System.Drawing.Size(62, 17);
            this.cbxSmooth.TabIndex = 2;
            this.cbxSmooth.Text = "S&mooth";
            this.cbxSmooth.UseVisualStyleBackColor = true;
            this.cbxSmooth.CheckedChanged += new System.EventHandler(this.CbxSmoothOnCheckedChanged);
            // 
            // ChromatogramForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(893, 264);
            this.Controls.Add(this.splitContainer1);
            this.Name = "ChromatogramForm";
            this.Text = "Raw Chromatograms";
            this.ResizeEnd += new System.EventHandler(this.ChromatogramFormOnResizeEnd);
            this.Resize += new System.EventHandler(this.ChromatogramFormOnResize);
            ((System.ComponentModel.ISupportInitialize)(this.gridIntensities)).EndInit();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);

        }
        protected ExcludedMzsGrid gridIntensities;

        #endregion
        private System.Windows.Forms.SplitContainer splitContainer1;
        protected System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.CheckBox cbxAutoFindPeak;
        private System.Windows.Forms.CheckBox cbxSmooth;
    }
}