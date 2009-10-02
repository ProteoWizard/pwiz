namespace turnover.ui.Forms
{
    partial class PeptideDataForm
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
            this.tableLayoutPanelStatus = new System.Windows.Forms.TableLayoutPanel();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.tbxStatus = new System.Windows.Forms.TextBox();
            this.tableLayoutPanel2.SuspendLayout();
            this.tableLayoutPanelStatus.SuspendLayout();
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
            this.panel1.Size = new System.Drawing.Size(893, 214);
            this.panel1.TabIndex = 2;
            // 
            // tableLayoutPanelStatus
            // 
            this.tableLayoutPanelStatus.ColumnCount = 2;
            this.tableLayoutPanelStatus.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelStatus.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelStatus.Controls.Add(this.progressBar1, 1, 0);
            this.tableLayoutPanelStatus.Controls.Add(this.tbxStatus, 0, 0);
            this.tableLayoutPanelStatus.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.tableLayoutPanelStatus.Location = new System.Drawing.Point(0, 240);
            this.tableLayoutPanelStatus.Name = "tableLayoutPanelStatus";
            this.tableLayoutPanelStatus.RowCount = 1;
            this.tableLayoutPanelStatus.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelStatus.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.tableLayoutPanelStatus.Size = new System.Drawing.Size(893, 24);
            this.tableLayoutPanelStatus.TabIndex = 0;
            // 
            // progressBar1
            // 
            this.progressBar1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.progressBar1.Location = new System.Drawing.Point(449, 3);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(441, 18);
            this.progressBar1.TabIndex = 0;
            this.progressBar1.Visible = false;
            // 
            // tbxStatus
            // 
            this.tbxStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxStatus.Location = new System.Drawing.Point(3, 3);
            this.tbxStatus.Name = "tbxStatus";
            this.tbxStatus.ReadOnly = true;
            this.tbxStatus.Size = new System.Drawing.Size(440, 20);
            this.tbxStatus.TabIndex = 1;
            // 
            // PeptideDataForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(893, 264);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.tableLayoutPanelStatus);
            this.Controls.Add(this.tableLayoutPanel2);
            this.Name = "PeptideDataForm";
            this.TabText = "PeptideDataForm";
            this.Text = "PeptideDataForm";
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.tableLayoutPanelStatus.ResumeLayout(false);
            this.tableLayoutPanelStatus.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.TextBox tbxSequence;
        private System.Windows.Forms.TextBox tbxDataFile;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelStatus;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.TextBox tbxStatus;
    }
}