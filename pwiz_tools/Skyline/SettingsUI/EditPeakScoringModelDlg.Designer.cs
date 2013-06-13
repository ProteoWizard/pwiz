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
            this.bindingPeakCalculators = new System.Windows.Forms.BindingSource(this.components);
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.zedGraphMProphet = new ZedGraph.ZedGraphControl();
            this.btnUpdateGraphs = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.textStdev = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.textMean = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.textName = new System.Windows.Forms.TextBox();
            this.btnTrainModel = new System.Windows.Forms.Button();
            this.zedGraphSelectedCalculator = new ZedGraph.ZedGraphControl();
            this.label3 = new System.Windows.Forms.Label();
            this.lblSelectedGraph = new System.Windows.Forms.Label();
            this.gridPeakCalculators = new pwiz.Skyline.Controls.DataGridViewEx();
            this.PeakCalculatorName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.PeakCalculatorWeight = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.lblColinearWarning = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.bindingPeakCalculators)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridPeakCalculators)).BeginInit();
            this.SuspendLayout();
            // 
            // helpTip
            // 
            this.helpTip.AutoPopDelay = 15000;
            this.helpTip.InitialDelay = 500;
            this.helpTip.ReshowDelay = 100;
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
            // splitContainer1
            // 
            resources.ApplyResources(this.splitContainer1, "splitContainer1");
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.zedGraphMProphet);
            this.splitContainer1.Panel1.Controls.Add(this.btnUpdateGraphs);
            this.splitContainer1.Panel1.Controls.Add(this.label4);
            this.splitContainer1.Panel1.Controls.Add(this.groupBox1);
            this.splitContainer1.Panel1.Controls.Add(this.label5);
            this.splitContainer1.Panel1.Controls.Add(this.textName);
            this.splitContainer1.Panel1.Controls.Add(this.btnTrainModel);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.zedGraphSelectedCalculator);
            this.splitContainer1.Panel2.Controls.Add(this.label3);
            this.splitContainer1.Panel2.Controls.Add(this.lblSelectedGraph);
            this.splitContainer1.Panel2.Controls.Add(this.gridPeakCalculators);
            this.splitContainer1.TabStop = false;
            // 
            // zedGraphMProphet
            // 
            resources.ApplyResources(this.zedGraphMProphet, "zedGraphMProphet");
            this.zedGraphMProphet.IsEnableHPan = false;
            this.zedGraphMProphet.IsEnableHZoom = false;
            this.zedGraphMProphet.IsEnableVPan = false;
            this.zedGraphMProphet.IsEnableVZoom = false;
            this.zedGraphMProphet.IsEnableWheelZoom = false;
            this.zedGraphMProphet.IsShowCopyMessage = false;
            this.zedGraphMProphet.Name = "zedGraphMProphet";
            this.zedGraphMProphet.ScrollGrace = 0D;
            this.zedGraphMProphet.ScrollMaxX = 0D;
            this.zedGraphMProphet.ScrollMaxY = 0D;
            this.zedGraphMProphet.ScrollMaxY2 = 0D;
            this.zedGraphMProphet.ScrollMinX = 0D;
            this.zedGraphMProphet.ScrollMinY = 0D;
            this.zedGraphMProphet.ScrollMinY2 = 0D;
            this.zedGraphMProphet.ContextMenuBuilder += new ZedGraph.ZedGraphControl.ContextMenuBuilderEventHandler(this.zedGraph_ContextMenuBuilder);
            // 
            // btnUpdateGraphs
            // 
            resources.ApplyResources(this.btnUpdateGraphs, "btnUpdateGraphs");
            this.btnUpdateGraphs.Name = "btnUpdateGraphs";
            this.helpTip.SetToolTip(this.btnUpdateGraphs, resources.GetString("btnUpdateGraphs.ToolTip"));
            this.btnUpdateGraphs.UseVisualStyleBackColor = true;
            this.btnUpdateGraphs.Click += new System.EventHandler(this.btnUpdateGraphs_Click);
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
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
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            // 
            // textName
            // 
            resources.ApplyResources(this.textName, "textName");
            this.textName.Name = "textName";
            this.helpTip.SetToolTip(this.textName, resources.GetString("textName.ToolTip"));
            // 
            // btnTrainModel
            // 
            resources.ApplyResources(this.btnTrainModel, "btnTrainModel");
            this.btnTrainModel.Name = "btnTrainModel";
            this.helpTip.SetToolTip(this.btnTrainModel, resources.GetString("btnTrainModel.ToolTip"));
            this.btnTrainModel.UseVisualStyleBackColor = true;
            this.btnTrainModel.Click += new System.EventHandler(this.btnTrainModel_Click);
            // 
            // zedGraphSelectedCalculator
            // 
            resources.ApplyResources(this.zedGraphSelectedCalculator, "zedGraphSelectedCalculator");
            this.zedGraphSelectedCalculator.IsEnableHPan = false;
            this.zedGraphSelectedCalculator.IsEnableHZoom = false;
            this.zedGraphSelectedCalculator.IsEnableVPan = false;
            this.zedGraphSelectedCalculator.IsEnableVZoom = false;
            this.zedGraphSelectedCalculator.IsEnableWheelZoom = false;
            this.zedGraphSelectedCalculator.IsShowCopyMessage = false;
            this.zedGraphSelectedCalculator.Name = "zedGraphSelectedCalculator";
            this.zedGraphSelectedCalculator.ScrollGrace = 0D;
            this.zedGraphSelectedCalculator.ScrollMaxX = 0D;
            this.zedGraphSelectedCalculator.ScrollMaxY = 0D;
            this.zedGraphSelectedCalculator.ScrollMaxY2 = 0D;
            this.zedGraphSelectedCalculator.ScrollMinX = 0D;
            this.zedGraphSelectedCalculator.ScrollMinY = 0D;
            this.zedGraphSelectedCalculator.ScrollMinY2 = 0D;
            this.zedGraphSelectedCalculator.ContextMenuBuilder += new ZedGraph.ZedGraphControl.ContextMenuBuilderEventHandler(this.zedGraph_ContextMenuBuilder);
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // lblSelectedGraph
            // 
            resources.ApplyResources(this.lblSelectedGraph, "lblSelectedGraph");
            this.lblSelectedGraph.Name = "lblSelectedGraph";
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
            this.gridPeakCalculators.ReadOnly = true;
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
            this.gridPeakCalculators.SelectionChanged += new System.EventHandler(this.gridPeakCalculators_SelectionChanged);
            // 
            // PeakCalculatorName
            // 
            this.PeakCalculatorName.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.PeakCalculatorName.DataPropertyName = "Name";
            this.PeakCalculatorName.FillWeight = 500F;
            resources.ApplyResources(this.PeakCalculatorName, "PeakCalculatorName");
            this.PeakCalculatorName.Name = "PeakCalculatorName";
            this.PeakCalculatorName.ReadOnly = true;
            // 
            // PeakCalculatorWeight
            // 
            this.PeakCalculatorWeight.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.PeakCalculatorWeight.DataPropertyName = "Weight";
            dataGridViewCellStyle2.Format = "N4";
            this.PeakCalculatorWeight.DefaultCellStyle = dataGridViewCellStyle2;
            this.PeakCalculatorWeight.FillWeight = 80F;
            resources.ApplyResources(this.PeakCalculatorWeight, "PeakCalculatorWeight");
            this.PeakCalculatorWeight.Name = "PeakCalculatorWeight";
            this.PeakCalculatorWeight.ReadOnly = true;
            // 
            // lblColinearWarning
            // 
            resources.ApplyResources(this.lblColinearWarning, "lblColinearWarning");
            this.lblColinearWarning.ForeColor = System.Drawing.Color.Red;
            this.lblColinearWarning.Name = "lblColinearWarning";
            // 
            // EditPeakScoringModelDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.lblColinearWarning);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditPeakScoringModelDlg";
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.bindingPeakCalculators)).EndInit();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridPeakCalculators)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private pwiz.Skyline.Controls.DataGridViewEx gridPeakCalculators;
        private System.Windows.Forms.Button btnTrainModel;
        private System.Windows.Forms.ToolTip helpTip;
        private System.Windows.Forms.BindingSource bindingPeakCalculators;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TextBox textStdev;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textMean;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.DataGridViewTextBoxColumn PeakCalculatorName;
        private System.Windows.Forms.DataGridViewTextBoxColumn PeakCalculatorWeight;
        private System.Windows.Forms.Button btnUpdateGraphs;
        private System.Windows.Forms.Label lblSelectedGraph;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private ZedGraph.ZedGraphControl zedGraphMProphet;
        private ZedGraph.ZedGraphControl zedGraphSelectedCalculator;
        private System.Windows.Forms.Label lblColinearWarning;
    }
}