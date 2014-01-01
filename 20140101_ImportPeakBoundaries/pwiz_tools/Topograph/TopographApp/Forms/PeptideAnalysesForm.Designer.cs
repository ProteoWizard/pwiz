using pwiz.Common.Controls;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Internal;
using pwiz.Topograph.ui.Controls;

namespace pwiz.Topograph.ui.Forms
{
    partial class PeptideAnalysesForm
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
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.deleteMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.btnAnalyzePeptides = new System.Windows.Forms.Button();
            this.bindingListSource1 = new pwiz.Common.DataBinding.Controls.BindingListSource(this.components);
            this.boundDataGridView1 = new BoundDataGridView();
            this.navBar1 = new pwiz.Common.DataBinding.Controls.NavBar();
            this.contextMenuStrip1.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.bindingListSource1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.boundDataGridView1)).BeginInit();
            this.SuspendLayout();
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.deleteMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(117, 26);
            // 
            // deleteMenuItem
            // 
            this.deleteMenuItem.Name = "deleteMenuItem";
            this.deleteMenuItem.Size = new System.Drawing.Size(116, 22);
            this.deleteMenuItem.Text = "Delete...";
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 149F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Controls.Add(this.btnAnalyzePeptides, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.navBar1, 1, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(1251, 29);
            this.tableLayoutPanel1.TabIndex = 1;
            // 
            // btnAnalyzePeptides
            // 
            this.btnAnalyzePeptides.Location = new System.Drawing.Point(3, 3);
            this.btnAnalyzePeptides.Name = "btnAnalyzePeptides";
            this.btnAnalyzePeptides.Size = new System.Drawing.Size(137, 23);
            this.btnAnalyzePeptides.TabIndex = 1;
            this.btnAnalyzePeptides.Text = "Analy&ze Peptides...";
            this.btnAnalyzePeptides.UseVisualStyleBackColor = true;
            this.btnAnalyzePeptides.Click += new System.EventHandler(this.btnAnalyzePeptides_Click);
            // 
            // bindingListSource1
            // 
            this.bindingListSource1.RowSource = null;
            // 
            // boundDataGridView1
            // 
            this.boundDataGridView1.AllowUserToAddRows = false;
            this.boundDataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.boundDataGridView1.DataSource = this.bindingListSource1;
            this.boundDataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.boundDataGridView1.Location = new System.Drawing.Point(0, 29);
            this.boundDataGridView1.Name = "boundDataGridView1";
            this.boundDataGridView1.Size = new System.Drawing.Size(1251, 235);
            this.boundDataGridView1.TabIndex = 2;
            // 
            // navBar1
            // 
            this.navBar1.AutoSize = true;
            this.navBar1.BindingListSource = this.bindingListSource1;
            this.navBar1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.navBar1.Location = new System.Drawing.Point(152, 3);
            this.navBar1.Name = "navBar1";
            this.navBar1.Size = new System.Drawing.Size(1096, 23);
            this.navBar1.TabIndex = 2;
            // 
            // PeptideAnalysesForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1251, 264);
            this.Controls.Add(this.boundDataGridView1);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "PeptideAnalysesForm";
            this.TabText = "PeptideComparisonsForm";
            this.Text = "Peptide Analyses";
            this.contextMenuStrip1.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.bindingListSource1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.boundDataGridView1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Button btnAnalyzePeptides;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem deleteMenuItem;
        private Common.DataBinding.Controls.BindingListSource bindingListSource1;
        private BoundDataGridView boundDataGridView1;
        private Common.DataBinding.Controls.NavBar navBar1;
    }
}