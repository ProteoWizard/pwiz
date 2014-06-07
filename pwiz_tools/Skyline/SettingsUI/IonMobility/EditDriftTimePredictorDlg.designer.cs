namespace pwiz.Skyline.SettingsUI.IonMobility
{
    partial class EditDriftTimePredictorDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditDriftTimePredictorDlg));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle7 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle8 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle9 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle10 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle11 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle12 = new System.Windows.Forms.DataGridViewCellStyle();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.gridRegression = new System.Windows.Forms.DataGridView();
            this.Charge = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Slope = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Intercept = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.comboLibrary = new System.Windows.Forms.ComboBox();
            this.textResolvingPower = new System.Windows.Forms.TextBox();
            this.textName = new System.Windows.Forms.TextBox();
            this.gridMeasuredDriftTimes = new System.Windows.Forms.DataGridView();
            this.MeasuredDriftTimeSequence = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.MeasuredDriftTimeCharge = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.MeasuredDriftTimeMsec = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.bindingChargeRegressionLines = new System.Windows.Forms.BindingSource(this.components);
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.labelConversionParameters = new System.Windows.Forms.Label();
            this.labelIonMobilityLibrary = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.gridRegression)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridMeasuredDriftTimes)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingChargeRegressionLines)).BeginInit();
            this.SuspendLayout();
            // 
            // helpTip
            // 
            this.helpTip.AutoPopDelay = 15000;
            this.helpTip.InitialDelay = 500;
            this.helpTip.ReshowDelay = 100;
            // 
            // gridRegression
            // 
            resources.ApplyResources(this.gridRegression, "gridRegression");
            dataGridViewCellStyle7.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle7.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle7.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle7.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle7.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle7.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle7.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridRegression.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle7;
            this.gridRegression.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridRegression.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Charge,
            this.Slope,
            this.Intercept});
            dataGridViewCellStyle8.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle8.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle8.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle8.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle8.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle8.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle8.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridRegression.DefaultCellStyle = dataGridViewCellStyle8;
            this.gridRegression.Name = "gridRegression";
            dataGridViewCellStyle9.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle9.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle9.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle9.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle9.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle9.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle9.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridRegression.RowHeadersDefaultCellStyle = dataGridViewCellStyle9;
            this.helpTip.SetToolTip(this.gridRegression, resources.GetString("gridRegression.ToolTip"));
            this.gridRegression.KeyDown += new System.Windows.Forms.KeyEventHandler(this.gridRegression_KeyDown);
            // 
            // Charge
            // 
            resources.ApplyResources(this.Charge, "Charge");
            this.Charge.Name = "Charge";
            // 
            // Slope
            // 
            resources.ApplyResources(this.Slope, "Slope");
            this.Slope.Name = "Slope";
            // 
            // Intercept
            // 
            this.Intercept.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            resources.ApplyResources(this.Intercept, "Intercept");
            this.Intercept.Name = "Intercept";
            // 
            // comboLibrary
            // 
            resources.ApplyResources(this.comboLibrary, "comboLibrary");
            this.comboLibrary.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboLibrary.FormattingEnabled = true;
            this.comboLibrary.Name = "comboLibrary";
            this.helpTip.SetToolTip(this.comboLibrary, resources.GetString("comboLibrary.ToolTip"));
            this.comboLibrary.SelectedIndexChanged += new System.EventHandler(this.comboIonMobilityLibrary_SelectedIndexChanged);
            // 
            // textResolvingPower
            // 
            resources.ApplyResources(this.textResolvingPower, "textResolvingPower");
            this.textResolvingPower.Name = "textResolvingPower";
            this.helpTip.SetToolTip(this.textResolvingPower, resources.GetString("textResolvingPower.ToolTip"));
            // 
            // textName
            // 
            resources.ApplyResources(this.textName, "textName");
            this.textName.Name = "textName";
            this.helpTip.SetToolTip(this.textName, resources.GetString("textName.ToolTip"));
            // 
            // gridMeasuredDriftTimes
            // 
            resources.ApplyResources(this.gridMeasuredDriftTimes, "gridMeasuredDriftTimes");
            dataGridViewCellStyle10.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle10.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle10.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle10.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle10.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle10.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle10.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridMeasuredDriftTimes.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle10;
            this.gridMeasuredDriftTimes.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridMeasuredDriftTimes.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.MeasuredDriftTimeSequence,
            this.MeasuredDriftTimeCharge,
            this.MeasuredDriftTimeMsec});
            dataGridViewCellStyle11.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle11.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle11.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle11.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle11.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle11.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle11.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridMeasuredDriftTimes.DefaultCellStyle = dataGridViewCellStyle11;
            this.gridMeasuredDriftTimes.Name = "gridMeasuredDriftTimes";
            dataGridViewCellStyle12.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle12.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle12.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle12.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle12.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle12.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle12.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridMeasuredDriftTimes.RowHeadersDefaultCellStyle = dataGridViewCellStyle12;
            this.helpTip.SetToolTip(this.gridMeasuredDriftTimes, resources.GetString("gridMeasuredDriftTimes.ToolTip"));
            this.gridMeasuredDriftTimes.KeyDown += new System.Windows.Forms.KeyEventHandler(this.gridMeasuredDriftTimes_KeyDown);
            // 
            // MeasuredDriftTimeSequence
            // 
            this.MeasuredDriftTimeSequence.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            resources.ApplyResources(this.MeasuredDriftTimeSequence, "MeasuredDriftTimeSequence");
            this.MeasuredDriftTimeSequence.Name = "MeasuredDriftTimeSequence";
            // 
            // MeasuredDriftTimeCharge
            // 
            resources.ApplyResources(this.MeasuredDriftTimeCharge, "MeasuredDriftTimeCharge");
            this.MeasuredDriftTimeCharge.Name = "MeasuredDriftTimeCharge";
            // 
            // MeasuredDriftTimeMsec
            // 
            resources.ApplyResources(this.MeasuredDriftTimeMsec, "MeasuredDriftTimeMsec");
            this.MeasuredDriftTimeMsec.Name = "MeasuredDriftTimeMsec";
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
            // labelConversionParameters
            // 
            resources.ApplyResources(this.labelConversionParameters, "labelConversionParameters");
            this.labelConversionParameters.Name = "labelConversionParameters";
            // 
            // labelIonMobilityLibrary
            // 
            resources.ApplyResources(this.labelIonMobilityLibrary, "labelIonMobilityLibrary");
            this.labelIonMobilityLibrary.Name = "labelIonMobilityLibrary";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // EditDriftTimePredictorDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.label2);
            this.Controls.Add(this.gridMeasuredDriftTimes);
            this.Controls.Add(this.labelConversionParameters);
            this.Controls.Add(this.gridRegression);
            this.Controls.Add(this.comboLibrary);
            this.Controls.Add(this.labelIonMobilityLibrary);
            this.Controls.Add(this.textResolvingPower);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.textName);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditDriftTimePredictorDlg";
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.gridRegression)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridMeasuredDriftTimes)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingChargeRegressionLines)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textResolvingPower;
        private System.Windows.Forms.Label labelIonMobilityLibrary;
        private System.Windows.Forms.ComboBox comboLibrary;
        private System.Windows.Forms.ToolTip helpTip;
        private System.Windows.Forms.BindingSource bindingChargeRegressionLines;
        private System.Windows.Forms.Label labelConversionParameters;
        private System.Windows.Forms.DataGridView gridRegression;
        private System.Windows.Forms.DataGridViewTextBoxColumn Charge;
        private System.Windows.Forms.DataGridViewTextBoxColumn Slope;
        private System.Windows.Forms.DataGridViewTextBoxColumn Intercept;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.DataGridView gridMeasuredDriftTimes;
        private System.Windows.Forms.DataGridViewTextBoxColumn MeasuredDriftTimeSequence;
        private System.Windows.Forms.DataGridViewTextBoxColumn MeasuredDriftTimeCharge;
        private System.Windows.Forms.DataGridViewTextBoxColumn MeasuredDriftTimeMsec;
    }
}