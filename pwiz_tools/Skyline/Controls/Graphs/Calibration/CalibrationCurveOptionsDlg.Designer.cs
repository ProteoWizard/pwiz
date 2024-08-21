namespace pwiz.Skyline.Controls.Graphs.Calibration
{
    partial class CalibrationCurveOptionsDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CalibrationCurveOptionsDlg));
            this.cbxLogXAxis = new System.Windows.Forms.CheckBox();
            this.cbxLogYAxis = new System.Windows.Forms.CheckBox();
            this.textSizeComboBox = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.checkedListBoxSampleTypes = new System.Windows.Forms.CheckedListBox();
            this.lblShowSampleTypes = new System.Windows.Forms.Label();
            this.cbxSingleBatch = new System.Windows.Forms.CheckBox();
            this.cbxShowLegend = new System.Windows.Forms.CheckBox();
            this.cbxShowFiguresOfMerit = new System.Windows.Forms.CheckBox();
            this.cbxShowBootstrapCurves = new System.Windows.Forms.CheckBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.textLineWidth = new System.Windows.Forms.NumericUpDown();
            ((System.ComponentModel.ISupportInitialize)(this.textLineWidth)).BeginInit();
            this.SuspendLayout();
            // 
            // cbxLogXAxis
            // 
            resources.ApplyResources(this.cbxLogXAxis, "cbxLogXAxis");
            this.cbxLogXAxis.Name = "cbxLogXAxis";
            this.cbxLogXAxis.UseVisualStyleBackColor = true;
            // 
            // cbxLogYAxis
            // 
            resources.ApplyResources(this.cbxLogYAxis, "cbxLogYAxis");
            this.cbxLogYAxis.Name = "cbxLogYAxis";
            this.cbxLogYAxis.UseVisualStyleBackColor = true;
            // 
            // textSizeComboBox
            // 
            this.textSizeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.textSizeComboBox.FormattingEnabled = true;
            this.textSizeComboBox.Items.AddRange(new object[] {
            resources.GetString("textSizeComboBox.Items"),
            resources.GetString("textSizeComboBox.Items1"),
            resources.GetString("textSizeComboBox.Items2"),
            resources.GetString("textSizeComboBox.Items3"),
            resources.GetString("textSizeComboBox.Items4")});
            resources.ApplyResources(this.textSizeComboBox, "textSizeComboBox");
            this.textSizeComboBox.Name = "textSizeComboBox";
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // checkedListBoxSampleTypes
            // 
            resources.ApplyResources(this.checkedListBoxSampleTypes, "checkedListBoxSampleTypes");
            this.checkedListBoxSampleTypes.FormattingEnabled = true;
            this.checkedListBoxSampleTypes.Name = "checkedListBoxSampleTypes";
            // 
            // lblShowSampleTypes
            // 
            resources.ApplyResources(this.lblShowSampleTypes, "lblShowSampleTypes");
            this.lblShowSampleTypes.Name = "lblShowSampleTypes";
            // 
            // cbxSingleBatch
            // 
            resources.ApplyResources(this.cbxSingleBatch, "cbxSingleBatch");
            this.cbxSingleBatch.Name = "cbxSingleBatch";
            this.cbxSingleBatch.UseVisualStyleBackColor = true;
            // 
            // cbxShowLegend
            // 
            resources.ApplyResources(this.cbxShowLegend, "cbxShowLegend");
            this.cbxShowLegend.Name = "cbxShowLegend";
            this.cbxShowLegend.UseVisualStyleBackColor = true;
            // 
            // cbxShowFiguresOfMerit
            // 
            resources.ApplyResources(this.cbxShowFiguresOfMerit, "cbxShowFiguresOfMerit");
            this.cbxShowFiguresOfMerit.Name = "cbxShowFiguresOfMerit";
            this.cbxShowFiguresOfMerit.UseVisualStyleBackColor = true;
            // 
            // cbxShowBootstrapCurves
            // 
            resources.ApplyResources(this.cbxShowBootstrapCurves, "cbxShowBootstrapCurves");
            this.cbxShowBootstrapCurves.Name = "cbxShowBootstrapCurves";
            this.cbxShowBootstrapCurves.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // textLineWidth
            // 
            resources.ApplyResources(this.textLineWidth, "textLineWidth");
            this.textLineWidth.Maximum = new decimal(new int[] {
            5,
            0,
            0,
            0});
            this.textLineWidth.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.textLineWidth.Name = "textLineWidth";
            this.textLineWidth.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // CalibrationCurveOptionsDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.textLineWidth);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.cbxShowBootstrapCurves);
            this.Controls.Add(this.cbxShowFiguresOfMerit);
            this.Controls.Add(this.cbxShowLegend);
            this.Controls.Add(this.cbxSingleBatch);
            this.Controls.Add(this.lblShowSampleTypes);
            this.Controls.Add(this.checkedListBoxSampleTypes);
            this.Controls.Add(this.textSizeComboBox);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.cbxLogYAxis);
            this.Controls.Add(this.cbxLogXAxis);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CalibrationCurveOptionsDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.textLineWidth)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox cbxLogXAxis;
        private System.Windows.Forms.CheckBox cbxLogYAxis;
        private System.Windows.Forms.ComboBox textSizeComboBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.CheckedListBox checkedListBoxSampleTypes;
        private System.Windows.Forms.Label lblShowSampleTypes;
        private System.Windows.Forms.CheckBox cbxSingleBatch;
        private System.Windows.Forms.CheckBox cbxShowLegend;
        private System.Windows.Forms.CheckBox cbxShowFiguresOfMerit;
        private System.Windows.Forms.CheckBox cbxShowBootstrapCurves;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.NumericUpDown textLineWidth;
    }
}