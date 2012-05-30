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
            this.textName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.textName.Location = new System.Drawing.Point(12, 25);
            this.textName.Name = "textName";
            this.textName.Size = new System.Drawing.Size(238, 20);
            this.textName.TabIndex = 1;
            this.helpTip.SetToolTip(this.textName, "Name used to list this equation in the Peptide Settings form");
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(9, 9);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(38, 13);
            this.label4.TabIndex = 0;
            this.label4.Text = "&Name:";
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(274, 39);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 18;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(274, 9);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 17;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(9, 83);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(37, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "&Slope:";
            // 
            // textSlope
            // 
            this.textSlope.Location = new System.Drawing.Point(12, 99);
            this.textSlope.Name = "textSlope";
            this.textSlope.Size = new System.Drawing.Size(100, 20);
            this.textSlope.TabIndex = 3;
            this.helpTip.SetToolTip(this.textSlope, "Slope used to calculate the predicted retention time from\r\na calculated peptide s" +
                    "core with an equation of the form:\r\n\r\nRT = slope * score + intercept");
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(154, 83);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(52, 13);
            this.label2.TabIndex = 4;
            this.label2.Text = "&Intercept:";
            // 
            // textIntercept
            // 
            this.textIntercept.Location = new System.Drawing.Point(157, 99);
            this.textIntercept.Name = "textIntercept";
            this.textIntercept.Size = new System.Drawing.Size(100, 20);
            this.textIntercept.TabIndex = 5;
            this.helpTip.SetToolTip(this.textIntercept, "Y-intercept used to calculate the predicted retention time from\r\na calculated pep" +
                    "tide score with an equation of the form:\r\n\r\nRT = slope * score + intercept\r\n");
            // 
            // btnCalculate
            // 
            this.btnCalculate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCalculate.Location = new System.Drawing.Point(274, 212);
            this.btnCalculate.Name = "btnCalculate";
            this.btnCalculate.Size = new System.Drawing.Size(75, 23);
            this.btnCalculate.TabIndex = 11;
            this.btnCalculate.Text = "Calculate >>";
            this.helpTip.SetToolTip(this.btnCalculate, "Show/hide peptide values used to calculate the equation with\r\nlinear regression");
            this.btnCalculate.UseVisualStyleBackColor = true;
            this.btnCalculate.Click += new System.EventHandler(this.btnCalculate_Click);
            // 
            // labelPeptides
            // 
            this.labelPeptides.AutoSize = true;
            this.labelPeptides.Location = new System.Drawing.Point(9, 257);
            this.labelPeptides.Name = "labelPeptides";
            this.labelPeptides.Size = new System.Drawing.Size(100, 13);
            this.labelPeptides.TabIndex = 12;
            this.labelPeptides.Text = "&Measured peptides:";
            // 
            // labelRValue
            // 
            this.labelRValue.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.labelRValue.AutoSize = true;
            this.labelRValue.Location = new System.Drawing.Point(247, 258);
            this.labelRValue.Name = "labelRValue";
            this.labelRValue.Size = new System.Drawing.Size(103, 13);
            this.labelRValue.TabIndex = 13;
            this.labelRValue.Text = "(0 peptides, R = 0.0)";
            this.helpTip.SetToolTip(this.labelRValue, "Number of peptides currently used to calculate the equation with\r\nlinear regressi" +
                    "on, and R for the regression");
            this.labelRValue.Click += new System.EventHandler(this.labelRValue_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(9, 140);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(72, 13);
            this.label3.TabIndex = 6;
            this.label3.Text = "Time &window:";
            // 
            // textTimeWindow
            // 
            this.textTimeWindow.Location = new System.Drawing.Point(12, 156);
            this.textTimeWindow.Name = "textTimeWindow";
            this.textTimeWindow.Size = new System.Drawing.Size(100, 20);
            this.textTimeWindow.TabIndex = 7;
            this.helpTip.SetToolTip(this.textTimeWindow, resources.GetString("textTimeWindow.ToolTip"));
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(9, 198);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(57, 13);
            this.label5.TabIndex = 9;
            this.label5.Text = "&Calculator:";
            // 
            // comboCalculator
            // 
            this.comboCalculator.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboCalculator.FormattingEnabled = true;
            this.comboCalculator.ItemHeight = 13;
            this.comboCalculator.Location = new System.Drawing.Point(12, 214);
            this.comboCalculator.Name = "comboCalculator";
            this.comboCalculator.Size = new System.Drawing.Size(245, 21);
            this.comboCalculator.TabIndex = 10;
            this.helpTip.SetToolTip(this.comboCalculator, "Calculator algorithm to use:\r\nSSRCalc - Sequence Specific Retention Calculator, a" +
                    " hydrophobicity calculator (Krokhin, et al.)");
            this.comboCalculator.SelectedIndexChanged += new System.EventHandler(this.comboCalculator_SelectedIndexChanged);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(118, 159);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(23, 13);
            this.label6.TabIndex = 8;
            this.label6.Text = "min";
            // 
            // btnUseCurrent
            // 
            this.btnUseCurrent.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnUseCurrent.Location = new System.Drawing.Point(92, 459);
            this.btnUseCurrent.Name = "btnUseCurrent";
            this.btnUseCurrent.Size = new System.Drawing.Size(89, 23);
            this.btnUseCurrent.TabIndex = 15;
            this.btnUseCurrent.Text = "&Use Results";
            this.helpTip.SetToolTip(this.btnUseCurrent, "Click to use currently imported results data with peptides in this\r\ndocument to c" +
                    "alculate the equation with linear regression");
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
            this.btnShowGraph.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnShowGraph.Location = new System.Drawing.Point(187, 459);
            this.btnShowGraph.Name = "btnShowGraph";
            this.btnShowGraph.Size = new System.Drawing.Size(89, 23);
            this.btnShowGraph.TabIndex = 16;
            this.btnShowGraph.Text = "Show &Graph...";
            this.helpTip.SetToolTip(this.btnShowGraph, "Show a linear regression graph  of  currently imported optimization results data\r" +
                    "\nfor peptides in this document");
            this.btnShowGraph.UseVisualStyleBackColor = true;
            this.btnShowGraph.Click += new System.EventHandler(this.btnShowGraph_Click);
            // 
            // gridPeptides
            // 
            this.gridPeptides.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
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
            this.gridPeptides.Location = new System.Drawing.Point(12, 274);
            this.gridPeptides.Name = "gridPeptides";
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle4.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle4.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle4.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle4.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridPeptides.RowHeadersDefaultCellStyle = dataGridViewCellStyle4;
            this.gridPeptides.Size = new System.Drawing.Size(338, 175);
            this.gridPeptides.TabIndex = 14;
            this.helpTip.SetToolTip(this.gridPeptides, "Peptides and their measured retention times used to calculate the equation\r\nwith " +
                    "linear regression");
            this.gridPeptides.Visible = global::pwiz.Skyline.Properties.Settings.Default.EditRTVisible;
            // 
            // Sequence
            // 
            this.Sequence.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Sequence.DataPropertyName = "Sequence";
            this.Sequence.FillWeight = 500F;
            this.Sequence.HeaderText = "Modified Sequence";
            this.Sequence.MinimumWidth = 120;
            this.Sequence.Name = "Sequence";
            // 
            // RetentionTime
            // 
            this.RetentionTime.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            this.RetentionTime.DataPropertyName = "RetentionTime";
            dataGridViewCellStyle2.Format = "N2";
            dataGridViewCellStyle2.NullValue = null;
            this.RetentionTime.DefaultCellStyle = dataGridViewCellStyle2;
            this.RetentionTime.HeaderText = "Retention time (min)";
            this.RetentionTime.MinimumWidth = 125;
            this.RetentionTime.Name = "RetentionTime";
            this.RetentionTime.Width = 125;
            // 
            // cbAutoCalc
            // 
            this.cbAutoCalc.AutoSize = true;
            this.cbAutoCalc.Location = new System.Drawing.Point(12, 63);
            this.cbAutoCalc.Name = "cbAutoCalc";
            this.cbAutoCalc.Size = new System.Drawing.Size(145, 17);
            this.cbAutoCalc.TabIndex = 19;
            this.cbAutoCalc.Text = "Aut&o-calculate regression";
            this.helpTip.SetToolTip(this.cbAutoCalc, "Check to have Skyline calculate slope and intercept\r\nfor the predictor based on a" +
                    " separate regression of\r\nretention time by score for each imported file");
            this.cbAutoCalc.UseVisualStyleBackColor = true;
            this.cbAutoCalc.CheckedChanged += new System.EventHandler(this.cbAutoCalc_CheckedChanged);
            // 
            // EditRTDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(362, 492);
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
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit Retention Time Predictor";
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