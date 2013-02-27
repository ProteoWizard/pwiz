namespace pwiz.Skyline.SettingsUI
{
    partial class EditPeakScoringModelDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditPeakScoringModelDlg));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.btnShowGraph = new System.Windows.Forms.Button();
            this.btnUseCurrent = new System.Windows.Forms.Button();
            this.gridPeakCalculators = new pwiz.Skyline.Controls.DataGridViewEx();
            this.PeakCalculatorName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.PeakCalculatorWeight = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.bindingPeakCalculators = new System.Windows.Forms.BindingSource(this.components);
            this.textName = new System.Windows.Forms.TextBox();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.textStdev = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.textMean = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.gridPeakCalculators)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingPeakCalculators)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // helpTip
            // 
            this.helpTip.AutoPopDelay = 15000;
            this.helpTip.InitialDelay = 500;
            this.helpTip.ReshowDelay = 100;
            // 
            // btnShowGraph
            // 
            resources.ApplyResources(this.btnShowGraph, "btnShowGraph");
            this.btnShowGraph.Name = "btnShowGraph";
            this.helpTip.SetToolTip(this.btnShowGraph, resources.GetString("btnShowGraph.ToolTip"));
            this.btnShowGraph.UseVisualStyleBackColor = true;
            this.btnShowGraph.Click += new System.EventHandler(this.btnShowGraph_Click);
            // 
            // btnUseCurrent
            // 
            resources.ApplyResources(this.btnUseCurrent, "btnUseCurrent");
            this.btnUseCurrent.Name = "btnUseCurrent";
            this.helpTip.SetToolTip(this.btnUseCurrent, resources.GetString("btnUseCurrent.ToolTip"));
            this.btnUseCurrent.UseVisualStyleBackColor = true;
            this.btnUseCurrent.Click += new System.EventHandler(this.btnUseCurrent_Click);
            // 
            // gridPeakCalculators
            // 
            this.gridPeakCalculators.AllowUserToAddRows = false;
            this.gridPeakCalculators.AllowUserToDeleteRows = false;
            resources.ApplyResources(this.gridPeakCalculators, "gridPeakCalculators");
            this.gridPeakCalculators.AutoGenerateColumns = false;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridPeakCalculators.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.gridPeakCalculators.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridPeakCalculators.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.PeakCalculatorName,
            this.PeakCalculatorWeight});
            this.gridPeakCalculators.DataSource = this.bindingPeakCalculators;
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridPeakCalculators.DefaultCellStyle = dataGridViewCellStyle3;
            this.gridPeakCalculators.Name = "gridPeakCalculators";
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle4.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle4.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle4.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle4.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridPeakCalculators.RowHeadersDefaultCellStyle = dataGridViewCellStyle4;
            this.gridPeakCalculators.ShowEditingIcon = false;
            this.helpTip.SetToolTip(this.gridPeakCalculators, resources.GetString("gridPeakCalculators.ToolTip"));
            // 
            // PeakCalculatorName
            // 
            this.PeakCalculatorName.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.PeakCalculatorName.DataPropertyName = "Name";
            this.PeakCalculatorName.FillWeight = 500F;
            resources.ApplyResources(this.PeakCalculatorName, "PeakCalculatorName");
            this.PeakCalculatorName.Name = "PeakCalculatorName";
            // 
            // PeakCalculatorWeight
            // 
            this.PeakCalculatorWeight.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            this.PeakCalculatorWeight.DataPropertyName = "Weight";
            dataGridViewCellStyle2.Format = "N4";
            this.PeakCalculatorWeight.DefaultCellStyle = dataGridViewCellStyle2;
            resources.ApplyResources(this.PeakCalculatorWeight, "PeakCalculatorWeight");
            this.PeakCalculatorWeight.Name = "PeakCalculatorWeight";
            // 
            // textName
            // 
            resources.ApplyResources(this.textName, "textName");
            this.textName.Name = "textName";
            this.helpTip.SetToolTip(this.textName, resources.GetString("textName.ToolTip"));
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.textStdev);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.textMean);
            this.groupBox1.Controls.Add(this.label1);
            resources.ApplyResources(this.groupBox1, "groupBox1");
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.TabStop = false;
            // 
            // textStdev
            // 
            resources.ApplyResources(this.textStdev, "textStdev");
            this.textStdev.Name = "textStdev";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // textMean
            // 
            resources.ApplyResources(this.textMean, "textMean");
            this.textMean.Name = "textMean";
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // EditPeakScoringModelDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.label3);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.btnShowGraph);
            this.Controls.Add(this.btnUseCurrent);
            this.Controls.Add(this.gridPeakCalculators);
            this.Controls.Add(this.textName);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditPeakScoringModelDlg";
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.gridPeakCalculators)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingPeakCalculators)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private pwiz.Skyline.Controls.DataGridViewEx gridPeakCalculators;
        private System.Windows.Forms.Button btnUseCurrent;
        private System.Windows.Forms.ToolTip helpTip;
        private System.Windows.Forms.Button btnShowGraph;
        private System.Windows.Forms.BindingSource bindingPeakCalculators;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TextBox textStdev;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textMean;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.DataGridViewTextBoxColumn PeakCalculatorName;
        private System.Windows.Forms.DataGridViewTextBoxColumn PeakCalculatorWeight;
    }
}