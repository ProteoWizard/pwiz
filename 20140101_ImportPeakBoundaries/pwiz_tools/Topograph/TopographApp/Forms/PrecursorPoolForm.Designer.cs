namespace pwiz.Topograph.ui.Forms
{
    partial class PrecursorPoolForm
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
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.label1 = new System.Windows.Forms.Label();
            this.tbxTurnover = new System.Windows.Forms.TextBox();
            this.dataGridViewPrecursorPool = new System.Windows.Forms.DataGridView();
            this.colTracer = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPercent = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.label2 = new System.Windows.Forms.Label();
            this.tbxTurnoverScore = new System.Windows.Forms.TextBox();
            this.zedGraphControl = new pwiz.Topograph.ui.Controls.ZedGraphControlEx();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewPrecursorPool)).BeginInit();
            this.SuspendLayout();
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
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.zedGraphControl);
            this.splitContainer1.Size = new System.Drawing.Size(839, 420);
            this.splitContainer1.SplitterDistance = 279;
            this.splitContainer1.TabIndex = 0;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.tbxTurnover, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.dataGridViewPrecursorPool, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.label2, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.tbxTurnoverScore, 1, 1);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(279, 420);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(3, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(50, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Turnover";
            // 
            // tbxTurnover
            // 
            this.tbxTurnover.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxTurnover.Location = new System.Drawing.Point(142, 3);
            this.tbxTurnover.Name = "tbxTurnover";
            this.tbxTurnover.ReadOnly = true;
            this.tbxTurnover.Size = new System.Drawing.Size(134, 20);
            this.tbxTurnover.TabIndex = 1;
            // 
            // dataGridViewPrecursorPool
            // 
            this.dataGridViewPrecursorPool.AllowUserToAddRows = false;
            this.dataGridViewPrecursorPool.AllowUserToDeleteRows = false;
            this.dataGridViewPrecursorPool.AllowUserToOrderColumns = true;
            this.dataGridViewPrecursorPool.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewPrecursorPool.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colTracer,
            this.colPercent});
            this.tableLayoutPanel1.SetColumnSpan(this.dataGridViewPrecursorPool, 2);
            this.dataGridViewPrecursorPool.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewPrecursorPool.Location = new System.Drawing.Point(3, 53);
            this.dataGridViewPrecursorPool.Name = "dataGridViewPrecursorPool";
            this.dataGridViewPrecursorPool.ReadOnly = true;
            this.dataGridViewPrecursorPool.Size = new System.Drawing.Size(273, 364);
            this.dataGridViewPrecursorPool.TabIndex = 2;
            // 
            // colTracer
            // 
            this.colTracer.HeaderText = "Tracer";
            this.colTracer.Name = "colTracer";
            this.colTracer.ReadOnly = true;
            // 
            // colPercent
            // 
            this.colPercent.HeaderText = "Percent";
            this.colPercent.Name = "colPercent";
            this.colPercent.ReadOnly = true;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(3, 25);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(81, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Turnover Score";
            // 
            // tbxTurnoverScore
            // 
            this.tbxTurnoverScore.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxTurnoverScore.Location = new System.Drawing.Point(142, 28);
            this.tbxTurnoverScore.Name = "tbxTurnoverScore";
            this.tbxTurnoverScore.ReadOnly = true;
            this.tbxTurnoverScore.Size = new System.Drawing.Size(134, 20);
            this.tbxTurnoverScore.TabIndex = 4;
            // 
            // zedGraphControl
            // 
            this.zedGraphControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.zedGraphControl.EditButtons = System.Windows.Forms.MouseButtons.Left;
            this.zedGraphControl.EditModifierKeys = System.Windows.Forms.Keys.None;
            this.zedGraphControl.IsEnableVPan = false;
            this.zedGraphControl.IsEnableVZoom = false;
            this.zedGraphControl.Location = new System.Drawing.Point(0, 0);
            this.zedGraphControl.Name = "zedGraphControl";
            this.zedGraphControl.ScrollGrace = 0;
            this.zedGraphControl.ScrollMaxX = 0;
            this.zedGraphControl.ScrollMaxY = 0;
            this.zedGraphControl.ScrollMaxY2 = 0;
            this.zedGraphControl.ScrollMinX = 0;
            this.zedGraphControl.ScrollMinY = 0;
            this.zedGraphControl.ScrollMinY2 = 0;
            this.zedGraphControl.Size = new System.Drawing.Size(556, 420);
            this.zedGraphControl.TabIndex = 0;
            // 
            // PrecursorPoolForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(839, 420);
            this.Controls.Add(this.splitContainer1);
            this.Name = "PrecursorPoolForm";
            this.TabText = "PrecursorPoolForm";
            this.Text = "PrecursorPoolForm";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewPrecursorPool)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbxTurnover;
        private System.Windows.Forms.DataGridView dataGridViewPrecursorPool;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTracer;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPercent;
        private pwiz.Topograph.ui.Controls.ZedGraphControlEx zedGraphControl;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox tbxTurnoverScore;
    }
}