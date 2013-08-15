using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Internal;

namespace pwiz.Topograph.ui.Forms
{
    partial class PeptidesForm
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
            this.btnAnalyzePeptides = new System.Windows.Forms.Button();
            this.btnAddSearchResults = new System.Windows.Forms.Button();
            this.dataGridView = new BoundDataGridView();
            this.peptidesBindingSource = new BindingListSource(this.components);
            this.navBar1 = new pwiz.Common.DataBinding.Controls.NavBar();
            this.tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.peptidesBindingSource)).BeginInit();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 3;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 152F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 124F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Controls.Add(this.btnAnalyzePeptides, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.btnAddSearchResults, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.navBar1, 2, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(941, 27);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // btnAnalyzePeptides
            // 
            this.btnAnalyzePeptides.Enabled = false;
            this.btnAnalyzePeptides.Location = new System.Drawing.Point(155, 3);
            this.btnAnalyzePeptides.Name = "btnAnalyzePeptides";
            this.btnAnalyzePeptides.Size = new System.Drawing.Size(117, 24);
            this.btnAnalyzePeptides.TabIndex = 4;
            this.btnAnalyzePeptides.Text = "Analyze Peptides...";
            this.btnAnalyzePeptides.UseVisualStyleBackColor = true;
            this.btnAnalyzePeptides.Click += new System.EventHandler(this.BtnAnalyzePeptidesOnClick);
            // 
            // btnAddSearchResults
            // 
            this.btnAddSearchResults.Location = new System.Drawing.Point(3, 3);
            this.btnAddSearchResults.Name = "btnAddSearchResults";
            this.btnAddSearchResults.Size = new System.Drawing.Size(145, 23);
            this.btnAddSearchResults.TabIndex = 6;
            this.btnAddSearchResults.Text = "Add Search Results...";
            this.btnAddSearchResults.UseVisualStyleBackColor = true;
            this.btnAddSearchResults.Click += new System.EventHandler(this.BtnAddSearchResultsOnClick);
            // 
            // dataGridView
            // 
            this.dataGridView.AllowUserToAddRows = false;
            this.dataGridView.AllowUserToDeleteRows = false;
            this.dataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView.DataSource = this.peptidesBindingSource;
            this.dataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView.Location = new System.Drawing.Point(0, 27);
            this.dataGridView.Name = "dataGridView";
            this.dataGridView.ReadOnly = true;
            this.dataGridView.Size = new System.Drawing.Size(941, 535);
            this.dataGridView.TabIndex = 0;
            // 
            // navBar1
            // 
            this.navBar1.AutoSize = true;
            this.navBar1.BindingListSource = this.peptidesBindingSource;
            this.navBar1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.navBar1.Location = new System.Drawing.Point(279, 3);
            this.navBar1.Name = "navBar1";
            this.navBar1.Size = new System.Drawing.Size(659, 24);
            this.navBar1.TabIndex = 7;
            this.navBar1.WaitingMessage = "Waiting for data...";
            // 
            // PeptidesForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(941, 562);
            this.Controls.Add(this.dataGridView);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "PeptidesForm";
            this.TabText = "PeptidesForm";
            this.Text = "PeptidesForm";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.peptidesBindingSource)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Button btnAnalyzePeptides;
        private System.Windows.Forms.Button btnAddSearchResults;
        private pwiz.Common.DataBinding.Controls.NavBar navBar1;
        private BindingListSource peptidesBindingSource;
        private BoundDataGridView dataGridView;

    }
}