namespace pwiz.Skyline.Controls.Clustering
{
    partial class PcaPlot
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
            this.lblXAxis = new System.Windows.Forms.Label();
            this.numericUpDownXAxis = new System.Windows.Forms.NumericUpDown();
            this.lblYAxis = new System.Windows.Forms.Label();
            this.numericUpDownYAxis = new System.Windows.Forms.NumericUpDown();
            this.zedGraphControl1 = new ZedGraph.ZedGraphControl();
            this.lblDataset = new System.Windows.Forms.Label();
            this.comboDataset = new System.Windows.Forms.ComboBox();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownXAxis)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownYAxis)).BeginInit();
            this.SuspendLayout();
            // 
            // lblXAxis
            // 
            this.lblXAxis.AutoSize = true;
            this.lblXAxis.Location = new System.Drawing.Point(12, 9);
            this.lblXAxis.Name = "lblXAxis";
            this.lblXAxis.Size = new System.Drawing.Size(36, 13);
            this.lblXAxis.TabIndex = 0;
            this.lblXAxis.Text = "X-Axis";
            // 
            // numericUpDownXAxis
            // 
            this.numericUpDownXAxis.Location = new System.Drawing.Point(13, 26);
            this.numericUpDownXAxis.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDownXAxis.Name = "numericUpDownXAxis";
            this.numericUpDownXAxis.Size = new System.Drawing.Size(120, 20);
            this.numericUpDownXAxis.TabIndex = 1;
            this.numericUpDownXAxis.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDownXAxis.ValueChanged += new System.EventHandler(this.numericUpDown_ValueChanged);
            // 
            // lblYAxis
            // 
            this.lblYAxis.AutoSize = true;
            this.lblYAxis.Location = new System.Drawing.Point(162, 9);
            this.lblYAxis.Name = "lblYAxis";
            this.lblYAxis.Size = new System.Drawing.Size(36, 13);
            this.lblYAxis.TabIndex = 2;
            this.lblYAxis.Text = "Y-Axis";
            // 
            // numericUpDownYAxis
            // 
            this.numericUpDownYAxis.Location = new System.Drawing.Point(165, 25);
            this.numericUpDownYAxis.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDownYAxis.Name = "numericUpDownYAxis";
            this.numericUpDownYAxis.Size = new System.Drawing.Size(120, 20);
            this.numericUpDownYAxis.TabIndex = 3;
            this.numericUpDownYAxis.Value = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this.numericUpDownYAxis.ValueChanged += new System.EventHandler(this.numericUpDown_ValueChanged);
            // 
            // zedGraphControl1
            // 
            this.zedGraphControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.zedGraphControl1.Location = new System.Drawing.Point(21, 61);
            this.zedGraphControl1.Name = "zedGraphControl1";
            this.zedGraphControl1.ScrollGrace = 0D;
            this.zedGraphControl1.ScrollMaxX = 0D;
            this.zedGraphControl1.ScrollMaxY = 0D;
            this.zedGraphControl1.ScrollMaxY2 = 0D;
            this.zedGraphControl1.ScrollMinX = 0D;
            this.zedGraphControl1.ScrollMinY = 0D;
            this.zedGraphControl1.ScrollMinY2 = 0D;
            this.zedGraphControl1.Size = new System.Drawing.Size(758, 377);
            this.zedGraphControl1.TabIndex = 4;
            // 
            // lblDataset
            // 
            this.lblDataset.AutoSize = true;
            this.lblDataset.Location = new System.Drawing.Point(325, 9);
            this.lblDataset.Name = "lblDataset";
            this.lblDataset.Size = new System.Drawing.Size(47, 13);
            this.lblDataset.TabIndex = 5;
            this.lblDataset.Text = "Dataset:";
            // 
            // comboDataset
            // 
            this.comboDataset.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.comboDataset.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboDataset.FormattingEnabled = true;
            this.comboDataset.Location = new System.Drawing.Point(328, 24);
            this.comboDataset.Name = "comboDataset";
            this.comboDataset.Size = new System.Drawing.Size(451, 21);
            this.comboDataset.TabIndex = 6;
            this.comboDataset.SelectedIndexChanged += new System.EventHandler(this.comboDataset_SelectedIndexChanged);
            // 
            // PcaPlot
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.comboDataset);
            this.Controls.Add(this.lblDataset);
            this.Controls.Add(this.zedGraphControl1);
            this.Controls.Add(this.numericUpDownYAxis);
            this.Controls.Add(this.lblYAxis);
            this.Controls.Add(this.numericUpDownXAxis);
            this.Controls.Add(this.lblXAxis);
            this.Name = "PcaPlot";
            this.Text = "PcaPlot";
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownXAxis)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownYAxis)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblXAxis;
        private System.Windows.Forms.NumericUpDown numericUpDownXAxis;
        private System.Windows.Forms.Label lblYAxis;
        private System.Windows.Forms.NumericUpDown numericUpDownYAxis;
        private ZedGraph.ZedGraphControl zedGraphControl1;
        private System.Windows.Forms.Label lblDataset;
        private System.Windows.Forms.ComboBox comboDataset;
    }
}