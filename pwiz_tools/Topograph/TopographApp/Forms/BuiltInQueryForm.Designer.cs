using pwiz.Topograph.ui.Controls;

namespace pwiz.Topograph.ui.Forms
{
    partial class BuiltInQueryForm
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
            this.dataGridView1 = new pwiz.Topograph.ui.Controls.QueryGrid();
            this.btnExportResults = new System.Windows.Forms.Button();
            this.btnRequery = new System.Windows.Forms.Button();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.recordNavBar1 = new pwiz.Common.Controls.RecordNavBar();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.AllowUserToOrderColumns = true;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.Location = new System.Drawing.Point(0, 29);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.ReadOnly = true;
            this.dataGridView1.Size = new System.Drawing.Size(833, 353);
            this.dataGridView1.TabIndex = 0;
            this.dataGridView1.Workspace = null;
            // 
            // btnExportResults
            // 
            this.btnExportResults.Location = new System.Drawing.Point(3, 3);
            this.btnExportResults.Name = "btnExportResults";
            this.btnExportResults.Size = new System.Drawing.Size(94, 23);
            this.btnExportResults.TabIndex = 1;
            this.btnExportResults.Text = "Export Results...";
            this.btnExportResults.UseVisualStyleBackColor = true;
            this.btnExportResults.Click += new System.EventHandler(this.btnExportResults_Click);
            // 
            // btnRequery
            // 
            this.btnRequery.Location = new System.Drawing.Point(110, 3);
            this.btnRequery.Name = "btnRequery";
            this.btnRequery.Size = new System.Drawing.Size(75, 23);
            this.btnRequery.TabIndex = 2;
            this.btnRequery.Text = "Requery";
            this.btnRequery.UseVisualStyleBackColor = true;
            this.btnRequery.Click += new System.EventHandler(this.btnRequery_Click);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 3;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 107F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 85F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Controls.Add(this.btnExportResults, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.btnRequery, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.recordNavBar1, 2, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(833, 29);
            this.tableLayoutPanel1.TabIndex = 3;
            // 
            // recordNavBar1
            // 
            this.recordNavBar1.DataGridView = this.dataGridView1;
            this.recordNavBar1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.recordNavBar1.Location = new System.Drawing.Point(195, 3);
            this.recordNavBar1.Name = "recordNavBar1";
            this.recordNavBar1.Size = new System.Drawing.Size(635, 23);
            this.recordNavBar1.TabIndex = 3;
            // 
            // BuiltInQueryForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(833, 382);
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "BuiltInQueryForm";
            this.TabText = "BuiltInQueryForm";
            this.Text = "BuiltInQueryForm";
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private QueryGrid dataGridView1;
        private System.Windows.Forms.Button btnExportResults;
        private System.Windows.Forms.Button btnRequery;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private pwiz.Common.Controls.RecordNavBar recordNavBar1;
    }
}