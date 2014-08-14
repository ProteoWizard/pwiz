namespace pwiz.Topograph.ui.Forms
{
    partial class IsotopeDistributionForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(IsotopeDistributionForm));
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.tbxSequence = new System.Windows.Forms.TextBox();
            this.tbxCharge = new System.Windows.Forms.TextBox();
            this.tbxTracerFormula = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.tbxMassResolution = new System.Windows.Forms.TextBox();
            this.cbxTracerPercents = new System.Windows.Forms.CheckBox();
            this.label5 = new System.Windows.Forms.Label();
            this.tbxFormula = new System.Windows.Forms.TextBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.colMass = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colIntensity = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tableLayoutPanel1.SuspendLayout();
            this.panel1.SuspendLayout();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Controls.Add(this.tbxSequence, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.tbxCharge, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this.tbxTracerFormula, 1, 3);
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.label2, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.label3, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.label4, 0, 5);
            this.tableLayoutPanel1.Controls.Add(this.tbxMassResolution, 1, 5);
            this.tableLayoutPanel1.Controls.Add(this.cbxTracerPercents, 1, 4);
            this.tableLayoutPanel1.Controls.Add(this.label5, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.tbxFormula, 1, 1);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 6;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(647, 152);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // tbxSequence
            // 
            this.tbxSequence.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxSequence.Location = new System.Drawing.Point(326, 3);
            this.tbxSequence.Name = "tbxSequence";
            this.tbxSequence.Size = new System.Drawing.Size(318, 20);
            this.tbxSequence.TabIndex = 0;
            this.tbxSequence.TextChanged += new System.EventHandler(this.TbxSequenceOnTextChanged);
            // 
            // tbxCharge
            // 
            this.tbxCharge.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxCharge.Location = new System.Drawing.Point(326, 53);
            this.tbxCharge.Name = "tbxCharge";
            this.tbxCharge.Size = new System.Drawing.Size(318, 20);
            this.tbxCharge.TabIndex = 2;
            this.tbxCharge.Text = "1";
            this.tbxCharge.TextChanged += new System.EventHandler(this.TbxChargeOnTextChanged);
            // 
            // tbxTracerFormula
            // 
            this.tbxTracerFormula.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxTracerFormula.Location = new System.Drawing.Point(326, 78);
            this.tbxTracerFormula.Name = "tbxTracerFormula";
            this.tbxTracerFormula.Size = new System.Drawing.Size(318, 20);
            this.tbxTracerFormula.TabIndex = 3;
            this.tbxTracerFormula.Leave += new System.EventHandler(this.TbxTracerFormulaOnTextChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label1.Location = new System.Drawing.Point(3, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(317, 25);
            this.label1.TabIndex = 3;
            this.label1.Text = "Sequence";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label2.Location = new System.Drawing.Point(3, 50);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(317, 25);
            this.label2.TabIndex = 4;
            this.label2.Text = "Charge";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label3.Location = new System.Drawing.Point(3, 75);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(317, 25);
            this.label3.TabIndex = 5;
            this.label3.Text = "Tracer Formula";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label4.Location = new System.Drawing.Point(3, 125);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(317, 27);
            this.label4.TabIndex = 6;
            this.label4.Text = "Mass Resolution";
            this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxMassResolution
            // 
            this.tbxMassResolution.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxMassResolution.Location = new System.Drawing.Point(326, 128);
            this.tbxMassResolution.Name = "tbxMassResolution";
            this.tbxMassResolution.Size = new System.Drawing.Size(318, 20);
            this.tbxMassResolution.TabIndex = 5;
            this.tbxMassResolution.Text = ".01";
            this.tbxMassResolution.TextChanged += new System.EventHandler(this.TbxMassResolutionOnTextChanged);
            // 
            // cbxTracerPercents
            // 
            this.cbxTracerPercents.AutoSize = true;
            this.cbxTracerPercents.Location = new System.Drawing.Point(326, 103);
            this.cbxTracerPercents.Name = "cbxTracerPercents";
            this.cbxTracerPercents.Size = new System.Drawing.Size(86, 17);
            this.cbxTracerPercents.TabIndex = 4;
            this.cbxTracerPercents.Text = "Percentages";
            this.cbxTracerPercents.UseVisualStyleBackColor = true;
            this.cbxTracerPercents.CheckedChanged += new System.EventHandler(this.CbxTracerPercentsOnCheckedChanged);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label5.Location = new System.Drawing.Point(3, 25);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(317, 25);
            this.label5.TabIndex = 9;
            this.label5.Text = "Formula";
            this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxFormula
            // 
            this.tbxFormula.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxFormula.Location = new System.Drawing.Point(326, 28);
            this.tbxFormula.Name = "tbxFormula";
            this.tbxFormula.Size = new System.Drawing.Size(318, 20);
            this.tbxFormula.TabIndex = 1;
            this.tbxFormula.TextChanged += new System.EventHandler(this.TbxFormulaOnTextChanged);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.splitContainer1);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 152);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(647, 248);
            this.panel1.TabIndex = 1;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.dataGridView1);
            this.splitContainer1.Size = new System.Drawing.Size(647, 248);
            this.splitContainer1.SplitterDistance = 247;
            this.splitContainer1.TabIndex = 0;
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colMass,
            this.colIntensity});
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.Location = new System.Drawing.Point(0, 0);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.ReadOnly = true;
            this.dataGridView1.Size = new System.Drawing.Size(247, 248);
            this.dataGridView1.TabIndex = 6;
            // 
            // colMass
            // 
            this.colMass.HeaderText = "Mass";
            this.colMass.Name = "colMass";
            this.colMass.ReadOnly = true;
            // 
            // colIntensity
            // 
            this.colIntensity.HeaderText = "Intensity";
            this.colIntensity.Name = "colIntensity";
            this.colIntensity.ReadOnly = true;
            // 
            // MercuryForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(647, 400);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "IsotopeDistributionForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.TabText = "Isotope Distributions";
            this.Text = "Isotope Distribution";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TextBox tbxSequence;
        private System.Windows.Forms.TextBox tbxCharge;
        private System.Windows.Forms.TextBox tbxTracerFormula;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox tbxMassResolution;
        private System.Windows.Forms.CheckBox cbxTracerPercents;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMass;
        private System.Windows.Forms.DataGridViewTextBoxColumn colIntensity;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox tbxFormula;
    }
}