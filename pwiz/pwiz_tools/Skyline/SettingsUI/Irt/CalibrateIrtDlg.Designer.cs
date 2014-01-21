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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CalibrateIrtDlg));
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
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // textMaxIrt
            // 
            resources.ApplyResources(this.textMaxIrt, "textMaxIrt");
            this.textMaxIrt.Name = "textMaxIrt";
            // 
            // textMinIrt
            // 
            resources.ApplyResources(this.textMinIrt, "textMinIrt");
            this.textMinIrt.Name = "textMinIrt";
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
            // btnUseCurrent
            // 
            resources.ApplyResources(this.btnUseCurrent, "btnUseCurrent");
            this.btnUseCurrent.Name = "btnUseCurrent";
            this.btnUseCurrent.UseVisualStyleBackColor = true;
            this.btnUseCurrent.Click += new System.EventHandler(this.btnUseCurrent_Click);
            // 
            // gridViewCalibrate
            // 
            resources.ApplyResources(this.gridViewCalibrate, "gridViewCalibrate");
            this.gridViewCalibrate.AutoGenerateColumns = false;
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
            this.gridViewCalibrate.Name = "gridViewCalibrate";
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle4.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle4.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle4.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle4.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridViewCalibrate.RowHeadersDefaultCellStyle = dataGridViewCellStyle4;
            // 
            // calibratePeptides
            // 
            this.calibratePeptides.DataPropertyName = "Sequence";
            this.calibratePeptides.FillWeight = 161.0009F;
            resources.ApplyResources(this.calibratePeptides, "calibratePeptides");
            this.calibratePeptides.Name = "calibratePeptides";
            // 
            // calibrateMeasuredRt
            // 
            this.calibrateMeasuredRt.DataPropertyName = "RetentionTime";
            dataGridViewCellStyle2.Format = "N2";
            dataGridViewCellStyle2.NullValue = null;
            this.calibrateMeasuredRt.DefaultCellStyle = dataGridViewCellStyle2;
            this.calibrateMeasuredRt.FillWeight = 78.08542F;
            resources.ApplyResources(this.calibrateMeasuredRt, "calibrateMeasuredRt");
            this.calibrateMeasuredRt.Name = "calibrateMeasuredRt";
            // 
            // checkFixedPoint
            // 
            this.checkFixedPoint.DataPropertyName = "FixedPoint";
            this.checkFixedPoint.FillWeight = 60.9137F;
            resources.ApplyResources(this.checkFixedPoint, "checkFixedPoint");
            this.checkFixedPoint.Name = "checkFixedPoint";
            // 
            // CalibrateIrtDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
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
            this.Load += new System.EventHandler(this.OnLoad);
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