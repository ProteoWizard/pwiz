namespace pwiz.Topograph.ui.Forms
{
    partial class PeptideAnalysisFrame
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
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.tbxSequence = new System.Windows.Forms.TextBox();
            this.tbxProtein = new System.Windows.Forms.TextBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Controls.Add(this.tbxSequence, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.tbxProtein, 1, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(284, 28);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // tbxSequence
            // 
            this.tbxSequence.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxSequence.Location = new System.Drawing.Point(3, 3);
            this.tbxSequence.Name = "tbxSequence";
            this.tbxSequence.ReadOnly = true;
            this.tbxSequence.Size = new System.Drawing.Size(136, 20);
            this.tbxSequence.TabIndex = 0;
            // 
            // tbxProtein
            // 
            this.tbxProtein.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxProtein.Location = new System.Drawing.Point(145, 3);
            this.tbxProtein.Name = "tbxProtein";
            this.tbxProtein.ReadOnly = true;
            this.tbxProtein.Size = new System.Drawing.Size(136, 20);
            this.tbxProtein.TabIndex = 1;
            // 
            // panel1
            // 
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 28);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(284, 236);
            this.panel1.TabIndex = 1;
            // 
            // PeptideAnalysisFrame
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 264);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "PeptideAnalysisFrame";
            this.TabText = "PeptideAnalysisFrame";
            this.Text = "PeptideAnalysisFrame";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TextBox tbxSequence;
        private System.Windows.Forms.TextBox tbxProtein;
        private System.Windows.Forms.Panel panel1;
    }
}