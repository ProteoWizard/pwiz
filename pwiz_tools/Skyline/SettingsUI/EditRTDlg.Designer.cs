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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
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
            this.gridPeptides = new System.Windows.Forms.DataGridView();
            this.Sequence = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.RetentionTime = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.btnUseCurrent = new System.Windows.Forms.Button();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.gridPeptides)).BeginInit();
            this.SuspendLayout();
            // 
            // textName
            // 
            this.textName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.textName.Location = new System.Drawing.Point(12, 25);
            this.textName.Name = "textName";
            this.textName.Size = new System.Drawing.Size(245, 20);
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
            this.btnCancel.Location = new System.Drawing.Point(281, 39);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 17;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(281, 9);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 16;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(9, 64);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(37, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "&Slope:";
            // 
            // textSlope
            // 
            this.textSlope.Location = new System.Drawing.Point(12, 80);
            this.textSlope.Name = "textSlope";
            this.textSlope.Size = new System.Drawing.Size(100, 20);
            this.textSlope.TabIndex = 3;
            this.helpTip.SetToolTip(this.textSlope, "Slope used to calculate the predicted retention time from\r\na calculated peptide s" +
                    "core with an equation of the form:\r\n\r\nRT = slope * score + intercept");
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(154, 64);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(52, 13);
            this.label2.TabIndex = 4;
            this.label2.Text = "&Intercept:";
            // 
            // textIntercept
            // 
            this.textIntercept.Location = new System.Drawing.Point(157, 80);
            this.textIntercept.Name = "textIntercept";
            this.textIntercept.Size = new System.Drawing.Size(100, 20);
            this.textIntercept.TabIndex = 5;
            this.helpTip.SetToolTip(this.textIntercept, "Y-intercept used to calculate the predicted retention time from\r\na calculated pep" +
                    "tide score with an equation of the form:\r\n\r\nRT = slope * score + intercept\r\n");
            // 
            // btnCalculate
            // 
            this.btnCalculate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCalculate.Location = new System.Drawing.Point(281, 193);
            this.btnCalculate.Name = "btnCalculate";
            this.btnCalculate.Size = new System.Drawing.Size(75, 23);
            this.btnCalculate.TabIndex = 12;
            this.btnCalculate.Text = "&Calculate >>";
            this.helpTip.SetToolTip(this.btnCalculate, "Show/hide peptide values used to calculate the equation with\r\nlinear regression");
            this.btnCalculate.UseVisualStyleBackColor = true;
            this.btnCalculate.Click += new System.EventHandler(this.btnCalculate_Click);
            // 
            // labelPeptides
            // 
            this.labelPeptides.AutoSize = true;
            this.labelPeptides.Location = new System.Drawing.Point(13, 238);
            this.labelPeptides.Name = "labelPeptides";
            this.labelPeptides.Size = new System.Drawing.Size(100, 13);
            this.labelPeptides.TabIndex = 14;
            this.labelPeptides.Text = "&Measured peptides:";
            // 
            // labelRValue
            // 
            this.labelRValue.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.labelRValue.AutoSize = true;
            this.labelRValue.Location = new System.Drawing.Point(254, 238);
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
            this.label3.Location = new System.Drawing.Point(9, 121);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(72, 13);
            this.label3.TabIndex = 7;
            this.label3.Text = "Time &window:";
            // 
            // textTimeWindow
            // 
            this.textTimeWindow.Location = new System.Drawing.Point(12, 137);
            this.textTimeWindow.Name = "textTimeWindow";
            this.textTimeWindow.Size = new System.Drawing.Size(100, 20);
            this.textTimeWindow.TabIndex = 8;
            this.helpTip.SetToolTip(this.textTimeWindow, resources.GetString("textTimeWindow.ToolTip"));
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(12, 179);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(57, 13);
            this.label5.TabIndex = 10;
            this.label5.Text = "&Calculator:";
            // 
            // comboCalculator
            // 
            this.comboCalculator.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboCalculator.FormattingEnabled = true;
            this.comboCalculator.ItemHeight = 13;
            this.comboCalculator.Location = new System.Drawing.Point(12, 195);
            this.comboCalculator.Name = "comboCalculator";
            this.comboCalculator.Size = new System.Drawing.Size(245, 21);
            this.comboCalculator.TabIndex = 11;
            this.helpTip.SetToolTip(this.comboCalculator, "Calculator algorithm to use:\r\nSSRCalc - Sequence Specific Retention Calculator, a" +
                    " hydrophobicity calculator (Krokhin, et al.)");
            this.comboCalculator.SelectedIndexChanged += new System.EventHandler(this.comboCalculator_SelectedIndexChanged);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(118, 140);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(43, 13);
            this.label6.TabIndex = 8;
            this.label6.Text = "minutes";
            // 
            // gridPeptides
            // 
            this.gridPeptides.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle4.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle4.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle4.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle4.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridPeptides.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle4;
            this.gridPeptides.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridPeptides.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Sequence,
            this.RetentionTime});
            this.gridPeptides.DataBindings.Add(new System.Windows.Forms.Binding("Visible", global::pwiz.Skyline.Properties.Settings.Default, "EditRTVisible", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            dataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle5.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle5.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle5.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle5.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle5.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle5.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridPeptides.DefaultCellStyle = dataGridViewCellStyle5;
            this.gridPeptides.Location = new System.Drawing.Point(15, 255);
            this.gridPeptides.Name = "gridPeptides";
            dataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle6.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle6.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle6.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle6.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle6.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle6.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridPeptides.RowHeadersDefaultCellStyle = dataGridViewCellStyle6;
            this.gridPeptides.Size = new System.Drawing.Size(342, 217);
            this.gridPeptides.TabIndex = 15;
            this.helpTip.SetToolTip(this.gridPeptides, "Peptides and their measured retention times used to calculate the equation\r\nwith " +
                    "linear regression");
            this.gridPeptides.Visible = global::pwiz.Skyline.Properties.Settings.Default.EditRTVisible;
            this.gridPeptides.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.gridPeptides_CellEndEdit);
            this.gridPeptides.KeyDown += new System.Windows.Forms.KeyEventHandler(this.gridPeptides_KeyDown);
            // 
            // Sequence
            // 
            this.Sequence.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Sequence.FillWeight = 500F;
            this.Sequence.HeaderText = "Sequence";
            this.Sequence.MinimumWidth = 120;
            this.Sequence.Name = "Sequence";
            // 
            // RetentionTime
            // 
            this.RetentionTime.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            this.RetentionTime.HeaderText = "Retention time (min)";
            this.RetentionTime.MinimumWidth = 125;
            this.RetentionTime.Name = "RetentionTime";
            this.RetentionTime.Width = 125;
            // 
            // btnUseCurrent
            // 
            this.btnUseCurrent.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnUseCurrent.Location = new System.Drawing.Point(149, 482);
            this.btnUseCurrent.Name = "btnUseCurrent";
            this.btnUseCurrent.Size = new System.Drawing.Size(75, 23);
            this.btnUseCurrent.TabIndex = 18;
            this.btnUseCurrent.Text = "&Use results";
            this.helpTip.SetToolTip(this.btnUseCurrent, "Click to use currently imported results data with peptides in this\r\ndocument to c" +
                    "alculate the equation with linear regression");
            this.btnUseCurrent.UseVisualStyleBackColor = true;
            this.btnUseCurrent.Click += new System.EventHandler(this.btnUseCurrent_Click);
            // 
            // EditRTDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(369, 517);
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
            this.Text = "Edit Retention Time Equation";
            ((System.ComponentModel.ISupportInitialize)(this.gridPeptides)).EndInit();
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
        private System.Windows.Forms.DataGridView gridPeptides;
        private System.Windows.Forms.DataGridViewTextBoxColumn Sequence;
        private System.Windows.Forms.DataGridViewTextBoxColumn RetentionTime;
        private System.Windows.Forms.Label labelRValue;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textTimeWindow;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.ComboBox comboCalculator;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Button btnUseCurrent;
        private System.Windows.Forms.ToolTip helpTip;
    }
}