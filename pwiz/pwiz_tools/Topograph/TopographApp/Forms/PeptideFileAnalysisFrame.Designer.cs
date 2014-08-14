namespace pwiz.Topograph.ui.Forms
{
    partial class PeptideFileAnalysisFrame
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
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.tbxSequence = new System.Windows.Forms.TextBox();
            this.tbxDataFile = new System.Windows.Forms.TextBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.tableLayoutPanel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.ColumnCount = 2;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel2.Controls.Add(this.tbxSequence, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this.tbxDataFile, 1, 0);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 1;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.tableLayoutPanel2.Size = new System.Drawing.Size(893, 26);
            this.tableLayoutPanel2.TabIndex = 1;
            // 
            // tbxSequence
            // 
            this.tbxSequence.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxSequence.Location = new System.Drawing.Point(3, 3);
            this.tbxSequence.Name = "tbxSequence";
            this.tbxSequence.ReadOnly = true;
            this.tbxSequence.Size = new System.Drawing.Size(440, 20);
            this.tbxSequence.TabIndex = 0;
            // 
            // tbxDataFile
            // 
            this.tbxDataFile.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxDataFile.Location = new System.Drawing.Point(449, 3);
            this.tbxDataFile.Name = "tbxDataFile";
            this.tbxDataFile.ReadOnly = true;
            this.tbxDataFile.Size = new System.Drawing.Size(441, 20);
            this.tbxDataFile.TabIndex = 1;
            // 
            // panel1
            // 
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 26);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(893, 238);
            this.panel1.TabIndex = 2;
            // 
            // PeptideFileAnalysisFrame
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(893, 264);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.tableLayoutPanel2);
            this.Name = "PeptideFileAnalysisFrame";
            this.TabText = "PeptideDataForm";
            this.Text = "PeptideDataForm";
            this.Resize += new System.EventHandler(this.PeptideFileAnalysisFrameOnResize);
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.TextBox tbxSequence;
        private System.Windows.Forms.TextBox tbxDataFile;
    }
}