namespace pwiz.Skyline.SettingsUI
{
    partial class EditRTDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditRTDlg));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            this.textName = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.textSlope = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.textIntercept = new System.Windows.Forms.TextBox();
            this.btnCalculate = new System.Windows.Forms.Button();
            this.labelPeptides = new System.Windows.Forms.Label();
            this.labelRValue = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.textTimeWindow = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.comboCalculator = new System.Windows.Forms.ComboBox();
            this.label6 = new System.Windows.Forms.Label();
            this.btnUseCurrent = new System.Windows.Forms.Button();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.btnShowGraph = new System.Windows.Forms.Button();
            this.gridPeptides = new pwiz.Skyline.Controls.DataGridViewEx();
            this.Sequence = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.RetentionTime = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.bindingPeptides = new System.Windows.Forms.BindingSource(this.components);
            this.cbAutoCalc = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.gridPeptides)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingPeptides)).BeginInit();
            this.SuspendLayout();
            // 
            // textName
            // 
            resources.ApplyResources(this.textName, "textName");
            this.textName.Name = "textName";
            this.helpTip.SetToolTip(this.textName, resources.GetString("textName.ToolTip"));
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            this.helpTip.SetToolTip(this.label4, resources.GetString("label4.ToolTip"));
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.helpTip.SetToolTip(this.btnCancel, resources.GetString("btnCancel.ToolTip"));
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.helpTip.SetToolTip(this.btnOk, resources.GetString("btnOk.ToolTip"));
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            this.helpTip.SetToolTip(this.label1, resources.GetString("label1.ToolTip"));
            // 
            // textSlope
            // 
            resources.ApplyResources(this.textSlope, "textSlope");
            this.textSlope.Name = "textSlope";
            this.helpTip.SetToolTip(this.textSlope, resources.GetString("textSlope.ToolTip"));
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            this.helpTip.SetToolTip(this.label2, resources.GetString("label2.ToolTip"));
            // 
            // textIntercept
            // 
            resources.ApplyResources(this.textIntercept, "textIntercept");
            this.textIntercept.Name = "textIntercept";
            this.helpTip.SetToolTip(this.textIntercept, resources.GetString("textIntercept.ToolTip"));
            // 
            // btnCalculate
            // 
            resources.ApplyResources(this.btnCalculate, "btnCalculate");
            this.btnCalculate.Name = "btnCalculate";
            this.helpTip.SetToolTip(this.btnCalculate, resources.GetString("btnCalculate.ToolTip"));
            this.btnCalculate.UseVisualStyleBackColor = true;
            this.btnCalculate.Click += new System.EventHandler(this.btnCalculate_Click);
            // 
            // labelPeptides
            // 
            resources.ApplyResources(this.labelPeptides, "labelPeptides");
            this.labelPeptides.Name = "labelPeptides";
            this.helpTip.SetToolTip(this.labelPeptides, resources.GetString("labelPeptides.ToolTip"));
            // 
            // labelRValue
            // 
            resources.ApplyResources(this.labelRValue, "labelRValue");
            this.labelRValue.Name = "labelRValue";
            this.helpTip.SetToolTip(this.labelRValue, resources.GetString("labelRValue.ToolTip"));
            this.labelRValue.Click += new System.EventHandler(this.labelRValue_Click);
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            this.helpTip.SetToolTip(this.label3, resources.GetString("label3.ToolTip"));
            // 
            // textTimeWindow
            // 
            resources.ApplyResources(this.textTimeWindow, "textTimeWindow");
            this.textTimeWindow.Name = "textTimeWindow";
            this.helpTip.SetToolTip(this.textTimeWindow, resources.GetString("textTimeWindow.ToolTip"));
            // 
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            this.helpTip.SetToolTip(this.label5, resources.GetString("label5.ToolTip"));
            // 
            // comboCalculator
            // 
            resources.ApplyResources(this.comboCalculator, "comboCalculator");
            this.comboCalculator.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboCalculator.FormattingEnabled = true;
            this.comboCalculator.Name = "comboCalculator";
            this.helpTip.SetToolTip(this.comboCalculator, resources.GetString("comboCalculator.ToolTip"));
            this.comboCalculator.SelectedIndexChanged += new System.EventHandler(this.comboCalculator_SelectedIndexChanged);
            // 
            // label6
            // 
            resources.ApplyResources(this.label6, "label6");
            this.label6.Name = "label6";
            this.helpTip.SetToolTip(this.label6, resources.GetString("label6.ToolTip"));
            // 
            // btnUseCurrent
            // 
            resources.ApplyResources(this.btnUseCurrent, "btnUseCurrent");
            this.btnUseCurrent.Name = "btnUseCurrent";
            this.helpTip.SetToolTip(this.btnUseCurrent, resources.GetString("btnUseCurrent.ToolTip"));
            this.btnUseCurrent.UseVisualStyleBackColor = true;
            this.btnUseCurrent.Click += new System.EventHandler(this.btnUseCurrent_Click);
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
            // gridPeptides
            // 
            resources.ApplyResources(this.gridPeptides, "gridPeptides");
            this.gridPeptides.AutoGenerateColumns = false;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridPeptides.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.gridPeptides.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridPeptides.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Sequence,
            this.RetentionTime});
            this.gridPeptides.DataBindings.Add(new System.Windows.Forms.Binding("Visible", global::pwiz.Skyline.Properties.Settings.Default, "EditRTVisible", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.gridPeptides.DataSource = this.bindingPeptides;
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridPeptides.DefaultCellStyle = dataGridViewCellStyle3;
            this.gridPeptides.Name = "gridPeptides";
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle4.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle4.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle4.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle4.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridPeptides.RowHeadersDefaultCellStyle = dataGridViewCellStyle4;
            this.helpTip.SetToolTip(this.gridPeptides, resources.GetString("gridPeptides.ToolTip"));
            this.gridPeptides.Visible = global::pwiz.Skyline.Properties.Settings.Default.EditRTVisible;
            // 
            // Sequence
            // 
            this.Sequence.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Sequence.DataPropertyName = "Sequence";
            this.Sequence.FillWeight = 500F;
            resources.ApplyResources(this.Sequence, "Sequence");
            this.Sequence.Name = "Sequence";
            // 
            // RetentionTime
            // 
            this.RetentionTime.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            this.RetentionTime.DataPropertyName = "RetentionTime";
            dataGridViewCellStyle2.Format = "N2";
            dataGridViewCellStyle2.NullValue = null;
            this.RetentionTime.DefaultCellStyle = dataGridViewCellStyle2;
            resources.ApplyResources(this.RetentionTime, "RetentionTime");
            this.RetentionTime.Name = "RetentionTime";
            // 
            // cbAutoCalc
            // 
            resources.ApplyResources(this.cbAutoCalc, "cbAutoCalc");
            this.cbAutoCalc.Name = "cbAutoCalc";
            this.helpTip.SetToolTip(this.cbAutoCalc, resources.GetString("cbAutoCalc.ToolTip"));
            this.cbAutoCalc.UseVisualStyleBackColor = true;
            this.cbAutoCalc.CheckedChanged += new System.EventHandler(this.cbAutoCalc_CheckedChanged);
            // 
            // EditRTDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.cbAutoCalc);
            this.Controls.Add(this.btnShowGraph);
            this.Controls.Add(this.btnUseCurrent);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.comboCalculator);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.textTimeWindow);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.labelRValue);
            this.Controls.Add(this.gridPeptides);
            this.Controls.Add(this.labelPeptides);
            this.Controls.Add(this.btnCalculate);
            this.Controls.Add(this.textIntercept);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textSlope);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.textName);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditRTDlg";
            this.ShowInTaskbar = false;
            this.helpTip.SetToolTip(this, resources.GetString("$this.ToolTip"));
            ((System.ComponentModel.ISupportInitialize)(this.gridPeptides)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingPeptides)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textSlope;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textIntercept;
        private System.Windows.Forms.Button btnCalculate;
        private System.Windows.Forms.Label labelPeptides;
        private pwiz.Skyline.Controls.DataGridViewEx gridPeptides;
        private System.Windows.Forms.Label labelRValue;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textTimeWindow;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.ComboBox comboCalculator;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Button btnUseCurrent;
        private System.Windows.Forms.ToolTip helpTip;
        private System.Windows.Forms.Button btnShowGraph;
        private System.Windows.Forms.BindingSource bindingPeptides;
        private System.Windows.Forms.DataGridViewTextBoxColumn Sequence;
        private System.Windows.Forms.DataGridViewTextBoxColumn RetentionTime;
        private System.Windows.Forms.CheckBox cbAutoCalc;
    }
}