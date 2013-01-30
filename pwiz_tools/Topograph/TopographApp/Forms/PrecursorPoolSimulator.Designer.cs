namespace pwiz.Topograph.ui.Forms
{
    partial class PrecursorPoolSimulator
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
            this.tbxPrecursorPool = new System.Windows.Forms.TextBox();
            this.tbxTurnover = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.tbxLabelCount = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.tbxNoise = new System.Windows.Forms.TextBox();
            this.btnGo = new System.Windows.Forms.Button();
            this.label5 = new System.Windows.Forms.Label();
            this.tbxPointCount = new System.Windows.Forms.TextBox();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.dataGridView = new System.Windows.Forms.DataGridView();
            this.colQuantity = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMean = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMedian = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colStdDev = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.graphResults = new pwiz.Topograph.ui.Controls.ZedGraphControlEx();
            this.graphDetails = new pwiz.Topograph.ui.Controls.ZedGraphControlEx();
            this.tableLayoutPanel1.SuspendLayout();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).BeginInit();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 57.37705F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 42.62295F));
            this.tableLayoutPanel1.Controls.Add(this.tbxPrecursorPool, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.tbxTurnover, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.label2, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.label3, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.tbxLabelCount, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this.label4, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.tbxNoise, 1, 3);
            this.tableLayoutPanel1.Controls.Add(this.btnGo, 1, 5);
            this.tableLayoutPanel1.Controls.Add(this.label5, 0, 4);
            this.tableLayoutPanel1.Controls.Add(this.tbxPointCount, 1, 4);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Left;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 7;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(244, 501);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // tbxPrecursorPool
            // 
            this.tbxPrecursorPool.Location = new System.Drawing.Point(142, 3);
            this.tbxPrecursorPool.Name = "tbxPrecursorPool";
            this.tbxPrecursorPool.Size = new System.Drawing.Size(99, 20);
            this.tbxPrecursorPool.TabIndex = 0;
            // 
            // tbxTurnover
            // 
            this.tbxTurnover.Location = new System.Drawing.Point(142, 28);
            this.tbxTurnover.Name = "tbxTurnover";
            this.tbxTurnover.Size = new System.Drawing.Size(99, 20);
            this.tbxTurnover.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(3, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(76, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Precursor Pool";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(3, 25);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(103, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "% newly synthesized";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(3, 50);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(44, 13);
            this.label3.TabIndex = 4;
            this.label3.Text = "# labels";
            // 
            // tbxLabelCount
            // 
            this.tbxLabelCount.Location = new System.Drawing.Point(142, 53);
            this.tbxLabelCount.Name = "tbxLabelCount";
            this.tbxLabelCount.Size = new System.Drawing.Size(99, 20);
            this.tbxLabelCount.TabIndex = 5;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(3, 75);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(34, 13);
            this.label4.TabIndex = 6;
            this.label4.Text = "Noise";
            // 
            // tbxNoise
            // 
            this.tbxNoise.Location = new System.Drawing.Point(142, 78);
            this.tbxNoise.Name = "tbxNoise";
            this.tbxNoise.Size = new System.Drawing.Size(99, 20);
            this.tbxNoise.TabIndex = 7;
            // 
            // btnGo
            // 
            this.btnGo.Location = new System.Drawing.Point(142, 128);
            this.btnGo.Name = "btnGo";
            this.btnGo.Size = new System.Drawing.Size(75, 19);
            this.btnGo.TabIndex = 8;
            this.btnGo.Text = "Go";
            this.btnGo.UseVisualStyleBackColor = true;
            this.btnGo.Click += new System.EventHandler(this.BtnGoOnClick);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(3, 100);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(45, 13);
            this.label5.TabIndex = 9;
            this.label5.Text = "# points";
            // 
            // tbxPointCount
            // 
            this.tbxPointCount.Location = new System.Drawing.Point(142, 103);
            this.tbxPointCount.Name = "tbxPointCount";
            this.tbxPointCount.Size = new System.Drawing.Size(99, 20);
            this.tbxPointCount.TabIndex = 10;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(244, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.graphDetails);
            this.splitContainer1.Panel1.Controls.Add(this.dataGridView);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.graphResults);
            this.splitContainer1.Size = new System.Drawing.Size(698, 501);
            this.splitContainer1.SplitterDistance = 250;
            this.splitContainer1.TabIndex = 1;
            // 
            // dataGridView
            // 
            this.dataGridView.AllowUserToAddRows = false;
            this.dataGridView.AllowUserToDeleteRows = false;
            this.dataGridView.AllowUserToOrderColumns = true;
            this.dataGridView.AllowUserToResizeColumns = false;
            this.dataGridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colQuantity,
            this.colMean,
            this.colMedian,
            this.colStdDev});
            this.dataGridView.Dock = System.Windows.Forms.DockStyle.Top;
            this.dataGridView.Location = new System.Drawing.Point(0, 0);
            this.dataGridView.Name = "dataGridView";
            this.dataGridView.Size = new System.Drawing.Size(698, 73);
            this.dataGridView.TabIndex = 0;
            // 
            // colQuantity
            // 
            this.colQuantity.HeaderText = "Quantity";
            this.colQuantity.Name = "colQuantity";
            // 
            // colMean
            // 
            this.colMean.HeaderText = "Mean";
            this.colMean.Name = "colMean";
            // 
            // colMedian
            // 
            this.colMedian.HeaderText = "Median";
            this.colMedian.Name = "colMedian";
            // 
            // colStdDev
            // 
            this.colStdDev.HeaderText = "StdDev";
            this.colStdDev.Name = "colStdDev";
            // 
            // zedGraphControlEx1
            // 
            this.graphResults.Dock = System.Windows.Forms.DockStyle.Fill;
            this.graphResults.Location = new System.Drawing.Point(0, 0);
            this.graphResults.Name = "graphResults";
            this.graphResults.ScrollGrace = 0;
            this.graphResults.ScrollMaxX = 0;
            this.graphResults.ScrollMaxY = 0;
            this.graphResults.ScrollMaxY2 = 0;
            this.graphResults.ScrollMinX = 0;
            this.graphResults.ScrollMinY = 0;
            this.graphResults.ScrollMinY2 = 0;
            this.graphResults.Size = new System.Drawing.Size(698, 247);
            this.graphResults.TabIndex = 0;
            this.graphResults.MouseUpEvent += new ZedGraph.ZedGraphControl.ZedMouseEventHandler(this.GraphResultsOnMouseUpEvent);
            // 
            // zedGraphControlEx2
            // 
            this.graphDetails.Dock = System.Windows.Forms.DockStyle.Fill;
            this.graphDetails.Location = new System.Drawing.Point(0, 73);
            this.graphDetails.Name = "graphDetails";
            this.graphDetails.ScrollGrace = 0;
            this.graphDetails.ScrollMaxX = 0;
            this.graphDetails.ScrollMaxY = 0;
            this.graphDetails.ScrollMaxY2 = 0;
            this.graphDetails.ScrollMinX = 0;
            this.graphDetails.ScrollMinY = 0;
            this.graphDetails.ScrollMinY2 = 0;
            this.graphDetails.Size = new System.Drawing.Size(698, 177);
            this.graphDetails.TabIndex = 1;
            // 
            // PrecursorPoolSimulator
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(942, 501);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "PrecursorPoolSimulator";
            this.TabText = "PrecursorPoolSimulator";
            this.Text = "PrecursorPoolSimulator";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TextBox tbxPrecursorPool;
        private System.Windows.Forms.TextBox tbxTurnover;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox tbxLabelCount;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox tbxNoise;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private pwiz.Topograph.ui.Controls.ZedGraphControlEx graphResults;
        private System.Windows.Forms.Button btnGo;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox tbxPointCount;
        private System.Windows.Forms.DataGridView dataGridView;
        private System.Windows.Forms.DataGridViewTextBoxColumn colQuantity;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMean;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMedian;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStdDev;
        private pwiz.Topograph.ui.Controls.ZedGraphControlEx graphDetails;
    }
}