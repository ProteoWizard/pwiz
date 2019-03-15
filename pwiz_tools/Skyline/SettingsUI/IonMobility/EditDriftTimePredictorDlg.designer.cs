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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.gridRegression = new pwiz.Common.Controls.CommonDataGridView();
            this.Charge = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Slope = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Intercept = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.comboLibrary = new System.Windows.Forms.ComboBox();
            this.textResolvingPower = new System.Windows.Forms.TextBox();
            this.textName = new System.Windows.Forms.TextBox();
            this.gridMeasuredDriftTimes = new pwiz.Common.Controls.CommonDataGridView();
            this.MeasuredDriftTimeSequence = new pwiz.Skyline.Controls.TargetColumn();
            this.MeasuredDriftTimeCharge = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.MeasuredIonMobility = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.MeasuredDriftTimeCCSsqA = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.MeasuredIonMobilityHighEnergyOffsetMsec = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.cbOffsetHighEnergySpectra = new System.Windows.Forms.CheckBox();
            this.btnUseResults = new System.Windows.Forms.Button();
            this.cbLinear = new System.Windows.Forms.CheckBox();
            this.textWidthAtDt0 = new System.Windows.Forms.TextBox();
            this.textWidthAtDtMax = new System.Windows.Forms.TextBox();
            this.bindingChargeRegressionLines = new System.Windows.Forms.BindingSource(this.components);
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.labelConversionParameters = new System.Windows.Forms.Label();
            this.labelIonMobilityLibrary = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.labelWidthDtZero = new System.Windows.Forms.Label();
            this.labelWidthDtMax = new System.Windows.Forms.Label();
            this.labelWidthDtZeroUnits = new System.Windows.Forms.Label();
            this.labelWidthDtMaxUnits = new System.Windows.Forms.Label();
            this.comboBoxIonMobilityUnits = new System.Windows.Forms.ComboBox();
            this.labelIonMobilityUnits = new System.Windows.Forms.Label();
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
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridRegression.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.gridRegression.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridRegression.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Charge,
            this.Slope,
            this.Intercept});
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridRegression.DefaultCellStyle = dataGridViewCellStyle2;
            this.gridRegression.MaximumColumnCount = null;
            this.gridRegression.Name = "gridRegression";
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridRegression.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
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
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle4.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle4.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle4.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle4.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridMeasuredDriftTimes.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle4;
            this.gridMeasuredDriftTimes.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridMeasuredDriftTimes.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.MeasuredDriftTimeSequence,
            this.MeasuredDriftTimeCharge,
            this.MeasuredIonMobility,
            this.MeasuredDriftTimeCCSsqA,
            this.MeasuredIonMobilityHighEnergyOffsetMsec});
            dataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle5.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle5.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle5.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle5.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle5.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle5.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridMeasuredDriftTimes.DefaultCellStyle = dataGridViewCellStyle5;
            this.gridMeasuredDriftTimes.MaximumColumnCount = null;
            this.gridMeasuredDriftTimes.Name = "gridMeasuredDriftTimes";
            dataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle6.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle6.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle6.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle6.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle6.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle6.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridMeasuredDriftTimes.RowHeadersDefaultCellStyle = dataGridViewCellStyle6;
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
            // MeasuredIonMobility
            // 
            resources.ApplyResources(this.MeasuredIonMobility, "MeasuredIonMobility");
            this.MeasuredIonMobility.Name = "MeasuredIonMobility";
            // 
            // MeasuredDriftTimeCCSsqA
            // 
            resources.ApplyResources(this.MeasuredDriftTimeCCSsqA, "MeasuredDriftTimeCCSsqA");
            this.MeasuredDriftTimeCCSsqA.Name = "MeasuredDriftTimeCCSsqA";
            // 
            // MeasuredIonMobilityHighEnergyOffsetMsec
            // 
            resources.ApplyResources(this.MeasuredIonMobilityHighEnergyOffsetMsec, "MeasuredIonMobilityHighEnergyOffsetMsec");
            this.MeasuredIonMobilityHighEnergyOffsetMsec.Name = "MeasuredIonMobilityHighEnergyOffsetMsec";
            // 
            // cbOffsetHighEnergySpectra
            // 
            resources.ApplyResources(this.cbOffsetHighEnergySpectra, "cbOffsetHighEnergySpectra");
            this.cbOffsetHighEnergySpectra.Name = "cbOffsetHighEnergySpectra";
            this.helpTip.SetToolTip(this.cbOffsetHighEnergySpectra, resources.GetString("cbOffsetHighEnergySpectra.ToolTip"));
            this.cbOffsetHighEnergySpectra.UseVisualStyleBackColor = true;
            this.cbOffsetHighEnergySpectra.CheckedChanged += new System.EventHandler(this.cbOffsetHighEnergySpectra_CheckedChanged);
            // 
            // btnUseResults
            // 
            resources.ApplyResources(this.btnUseResults, "btnUseResults");
            this.btnUseResults.Name = "btnUseResults";
            this.helpTip.SetToolTip(this.btnUseResults, resources.GetString("btnUseResults.ToolTip"));
            this.btnUseResults.UseVisualStyleBackColor = true;
            this.btnUseResults.Click += new System.EventHandler(this.btnGenerateFromDocument_Click);
            // 
            // cbLinear
            // 
            resources.ApplyResources(this.cbLinear, "cbLinear");
            this.cbLinear.Name = "cbLinear";
            this.helpTip.SetToolTip(this.cbLinear, resources.GetString("cbLinear.ToolTip"));
            this.cbLinear.UseVisualStyleBackColor = true;
            this.cbLinear.CheckedChanged += new System.EventHandler(this.cbLinear_CheckedChanged);
            // 
            // textWidthAtDt0
            // 
            resources.ApplyResources(this.textWidthAtDt0, "textWidthAtDt0");
            this.textWidthAtDt0.Name = "textWidthAtDt0";
            this.helpTip.SetToolTip(this.textWidthAtDt0, resources.GetString("textWidthAtDt0.ToolTip"));
            // 
            // textWidthAtDtMax
            // 
            resources.ApplyResources(this.textWidthAtDtMax, "textWidthAtDtMax");
            this.textWidthAtDtMax.Name = "textWidthAtDtMax";
            this.helpTip.SetToolTip(this.textWidthAtDtMax, resources.GetString("textWidthAtDtMax.ToolTip"));
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
            // labelWidthDtZero
            // 
            resources.ApplyResources(this.labelWidthDtZero, "labelWidthDtZero");
            this.labelWidthDtZero.Name = "labelWidthDtZero";
            // 
            // labelWidthDtMax
            // 
            resources.ApplyResources(this.labelWidthDtMax, "labelWidthDtMax");
            this.labelWidthDtMax.Name = "labelWidthDtMax";
            // 
            // labelWidthDtZeroUnits
            // 
            resources.ApplyResources(this.labelWidthDtZeroUnits, "labelWidthDtZeroUnits");
            this.labelWidthDtZeroUnits.Name = "labelWidthDtZeroUnits";
            // 
            // labelWidthDtMaxUnits
            // 
            resources.ApplyResources(this.labelWidthDtMaxUnits, "labelWidthDtMaxUnits");
            this.labelWidthDtMaxUnits.Name = "labelWidthDtMaxUnits";
            // 
            // comboBoxIonMobilityUnits
            // 
            resources.ApplyResources(this.comboBoxIonMobilityUnits, "comboBoxIonMobilityUnits");
            this.comboBoxIonMobilityUnits.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxIonMobilityUnits.FormattingEnabled = true;
            this.comboBoxIonMobilityUnits.Name = "comboBoxIonMobilityUnits";
            // 
            // labelIonMobilityUnits
            // 
            resources.ApplyResources(this.labelIonMobilityUnits, "labelIonMobilityUnits");
            this.labelIonMobilityUnits.Name = "labelIonMobilityUnits";
            // 
            // EditDriftTimePredictorDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.comboBoxIonMobilityUnits);
            this.Controls.Add(this.labelIonMobilityUnits);
            this.Controls.Add(this.labelWidthDtMaxUnits);
            this.Controls.Add(this.labelWidthDtZeroUnits);
            this.Controls.Add(this.textWidthAtDtMax);
            this.Controls.Add(this.textWidthAtDt0);
            this.Controls.Add(this.labelWidthDtMax);
            this.Controls.Add(this.labelWidthDtZero);
            this.Controls.Add(this.cbLinear);
            this.Controls.Add(this.btnUseResults);
            this.Controls.Add(this.cbOffsetHighEnergySpectra);
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
        private pwiz.Common.Controls.CommonDataGridView gridRegression;
        private System.Windows.Forms.DataGridViewTextBoxColumn Charge;
        private System.Windows.Forms.DataGridViewTextBoxColumn Slope;
        private System.Windows.Forms.DataGridViewTextBoxColumn Intercept;
        private System.Windows.Forms.Label label2;
        private pwiz.Common.Controls.CommonDataGridView gridMeasuredDriftTimes;
        private System.Windows.Forms.CheckBox cbOffsetHighEnergySpectra;
        private System.Windows.Forms.Button btnUseResults;
        private System.Windows.Forms.CheckBox cbLinear;
        private System.Windows.Forms.Label labelWidthDtZero;
        private System.Windows.Forms.Label labelWidthDtMax;
        private System.Windows.Forms.TextBox textWidthAtDt0;
        private System.Windows.Forms.TextBox textWidthAtDtMax;
        private System.Windows.Forms.Label labelWidthDtZeroUnits;
        private System.Windows.Forms.Label labelWidthDtMaxUnits;
        private System.Windows.Forms.ComboBox comboBoxIonMobilityUnits;
        private System.Windows.Forms.Label labelIonMobilityUnits;
        private pwiz.Skyline.Controls.TargetColumn MeasuredDriftTimeSequence;
        private System.Windows.Forms.DataGridViewTextBoxColumn MeasuredDriftTimeCharge;
        private System.Windows.Forms.DataGridViewTextBoxColumn MeasuredIonMobility;
        private System.Windows.Forms.DataGridViewTextBoxColumn MeasuredDriftTimeCCSsqA;
        private System.Windows.Forms.DataGridViewTextBoxColumn MeasuredIonMobilityHighEnergyOffsetMsec;
    }
}