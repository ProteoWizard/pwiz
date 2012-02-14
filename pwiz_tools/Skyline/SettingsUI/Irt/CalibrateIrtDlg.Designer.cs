namespace pwiz.Skyline.SettingsUI.Irt
{
    partial class CalibrateIrtDlg
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.textMaxIrt = new System.Windows.Forms.TextBox();
            this.textMinIrt = new System.Windows.Forms.TextBox();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnUseCurrent = new System.Windows.Forms.Button();
            this.bindingSourceStandard = new System.Windows.Forms.BindingSource(this.components);
            this.gridViewCalibrate = new pwiz.Skyline.Controls.DataGridViewEx();
            this.calibratePeptides = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.calibrateMeasuredRt = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.checkFixedPoint = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceStandard)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewCalibrate)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(76, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "&Min iRT value:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(13, 39);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(79, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Ma&x iRT value:";
            // 
            // textMaxIrt
            // 
            this.textMaxIrt.Location = new System.Drawing.Point(99, 36);
            this.textMaxIrt.Name = "textMaxIrt";
            this.textMaxIrt.Size = new System.Drawing.Size(100, 20);
            this.textMaxIrt.TabIndex = 3;
            this.textMaxIrt.Text = "100";
            // 
            // textMinIrt
            // 
            this.textMinIrt.Location = new System.Drawing.Point(99, 10);
            this.textMinIrt.Name = "textMinIrt";
            this.textMinIrt.Size = new System.Drawing.Size(100, 20);
            this.textMinIrt.TabIndex = 1;
            this.textMinIrt.Text = "0";
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(288, 8);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 8;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(288, 37);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 9;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnUseCurrent
            // 
            this.btnUseCurrent.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnUseCurrent.Location = new System.Drawing.Point(150, 290);
            this.btnUseCurrent.Name = "btnUseCurrent";
            this.btnUseCurrent.Size = new System.Drawing.Size(75, 23);
            this.btnUseCurrent.TabIndex = 19;
            this.btnUseCurrent.Text = "&Use Results";
            this.btnUseCurrent.UseVisualStyleBackColor = true;
            this.btnUseCurrent.Click += new System.EventHandler(this.btnUseCurrent_Click);
            // 
            // gridViewCalibrate
            // 
            this.gridViewCalibrate.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.gridViewCalibrate.AutoGenerateColumns = false;
            this.gridViewCalibrate.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridViewCalibrate.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.gridViewCalibrate.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridViewCalibrate.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.calibratePeptides,
            this.calibrateMeasuredRt,
            this.checkFixedPoint});
            this.gridViewCalibrate.DataSource = this.bindingSourceStandard;
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridViewCalibrate.DefaultCellStyle = dataGridViewCellStyle3;
            this.gridViewCalibrate.Location = new System.Drawing.Point(14, 79);
            this.gridViewCalibrate.Name = "gridViewCalibrate";
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle4.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle4.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle4.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle4.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridViewCalibrate.RowHeadersDefaultCellStyle = dataGridViewCellStyle4;
            this.gridViewCalibrate.Size = new System.Drawing.Size(348, 199);
            this.gridViewCalibrate.TabIndex = 7;
            // 
            // calibratePeptides
            // 
            this.calibratePeptides.DataPropertyName = "Sequence";
            this.calibratePeptides.FillWeight = 161.0009F;
            this.calibratePeptides.HeaderText = "Modified Sequence";
            this.calibratePeptides.Name = "calibratePeptides";
            // 
            // calibrateMeasuredRt
            // 
            this.calibrateMeasuredRt.DataPropertyName = "RetentionTime";
            dataGridViewCellStyle2.Format = "N2";
            dataGridViewCellStyle2.NullValue = null;
            this.calibrateMeasuredRt.DefaultCellStyle = dataGridViewCellStyle2;
            this.calibrateMeasuredRt.FillWeight = 78.08542F;
            this.calibrateMeasuredRt.HeaderText = "Retention time (min)";
            this.calibrateMeasuredRt.Name = "calibrateMeasuredRt";
            // 
            // checkFixedPoint
            // 
            this.checkFixedPoint.DataPropertyName = "FixedPoint";
            this.checkFixedPoint.FillWeight = 60.9137F;
            this.checkFixedPoint.HeaderText = "Fixed Point";
            this.checkFixedPoint.Name = "checkFixedPoint";
            // 
            // CalibrateIrtDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(375, 325);
            this.Controls.Add(this.btnUseCurrent);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.gridViewCalibrate);
            this.Controls.Add(this.textMinIrt);
            this.Controls.Add(this.textMaxIrt);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CalibrateIrtDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Calibrate iRT Calculator";
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceStandard)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewCalibrate)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textMaxIrt;
        private System.Windows.Forms.TextBox textMinIrt;
        private pwiz.Skyline.Controls.DataGridViewEx gridViewCalibrate;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnUseCurrent;
        private System.Windows.Forms.BindingSource bindingSourceStandard;
        private System.Windows.Forms.DataGridViewTextBoxColumn calibratePeptides;
        private System.Windows.Forms.DataGridViewTextBoxColumn calibrateMeasuredRt;
        private System.Windows.Forms.DataGridViewCheckBoxColumn checkFixedPoint;
    }
}