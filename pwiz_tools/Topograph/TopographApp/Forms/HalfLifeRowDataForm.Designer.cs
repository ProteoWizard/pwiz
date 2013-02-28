using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Internal;

namespace pwiz.Topograph.ui.Forms
{
    partial class HalfLifeRowDataForm
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
            this.boundDataGridView1 = new BoundDataGridView();
            this.bindingSource1 = new pwiz.Common.DataBinding.Controls.BindingListSource(this.components);
            this.navBar1 = new pwiz.Common.DataBinding.Controls.NavBar();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.tbxPeptide = new System.Windows.Forms.TextBox();
            this.tbxProtein = new System.Windows.Forms.TextBox();
            this.tbxCohort = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)(this.boundDataGridView1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).BeginInit();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // boundDataGridView1
            // 
            this.boundDataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.boundDataGridView1.DataSource = this.bindingSource1;
            this.boundDataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.boundDataGridView1.Location = new System.Drawing.Point(0, 125);
            this.boundDataGridView1.Name = "boundDataGridView1";
            this.boundDataGridView1.Size = new System.Drawing.Size(710, 337);
            this.boundDataGridView1.TabIndex = 0;
            // 
            // navBar1
            // 
            this.navBar1.AutoSize = true;
            this.navBar1.BindingListSource = this.bindingSource1;
            this.navBar1.Dock = System.Windows.Forms.DockStyle.Top;
            this.navBar1.Location = new System.Drawing.Point(0, 100);
            this.navBar1.Name = "navBar1";
            this.navBar1.Size = new System.Drawing.Size(710, 25);
            this.navBar1.TabIndex = 1;
            this.navBar1.WaitingMessage = "Waiting for data...";
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Controls.Add(this.tbxPeptide, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.tbxProtein, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.tbxCohort, 0, 1);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 4;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(710, 100);
            this.tableLayoutPanel1.TabIndex = 2;
            // 
            // tbxPeptide
            // 
            this.tbxPeptide.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxPeptide.Location = new System.Drawing.Point(3, 3);
            this.tbxPeptide.Name = "tbxPeptide";
            this.tbxPeptide.ReadOnly = true;
            this.tbxPeptide.Size = new System.Drawing.Size(349, 20);
            this.tbxPeptide.TabIndex = 0;
            // 
            // tbxProtein
            // 
            this.tbxProtein.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxProtein.Location = new System.Drawing.Point(358, 3);
            this.tbxProtein.Name = "tbxProtein";
            this.tbxProtein.ReadOnly = true;
            this.tbxProtein.Size = new System.Drawing.Size(349, 20);
            this.tbxProtein.TabIndex = 1;
            // 
            // tbxCohort
            // 
            this.tbxCohort.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxCohort.Location = new System.Drawing.Point(3, 28);
            this.tbxCohort.Name = "tbxCohort";
            this.tbxCohort.ReadOnly = true;
            this.tbxCohort.Size = new System.Drawing.Size(349, 20);
            this.tbxCohort.TabIndex = 2;
            // 
            // HalfLifeRowDataForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(710, 462);
            this.Controls.Add(this.boundDataGridView1);
            this.Controls.Add(this.navBar1);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "HalfLifeRowDataForm";
            this.TabText = "HalfLifeRowDataForm";
            this.Text = "HalfLifeRowDataForm";
            ((System.ComponentModel.ISupportInitialize)(this.boundDataGridView1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).EndInit();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private BoundDataGridView boundDataGridView1;
        private pwiz.Common.DataBinding.Controls.NavBar navBar1;
        private BindingListSource bindingSource1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TextBox tbxPeptide;
        private System.Windows.Forms.TextBox tbxProtein;
        private System.Windows.Forms.TextBox tbxCohort;
    }
}