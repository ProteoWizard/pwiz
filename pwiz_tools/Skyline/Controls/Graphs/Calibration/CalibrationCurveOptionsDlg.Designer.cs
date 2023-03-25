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
            this.cbxLogXAxis.AutoSize = true;
            this.cbxLogXAxis.Location = new System.Drawing.Point(9, 167);
            this.cbxLogXAxis.Name = "cbxLogXAxis";
            this.cbxLogXAxis.Size = new System.Drawing.Size(112, 17);
            this.cbxLogXAxis.TabIndex = 4;
            this.cbxLogXAxis.Text = "Logarithmic X Axis";
            this.cbxLogXAxis.UseVisualStyleBackColor = true;
            // 
            // cbxLogYAxis
            // 
            this.cbxLogYAxis.AutoSize = true;
            this.cbxLogYAxis.Location = new System.Drawing.Point(9, 190);
            this.cbxLogYAxis.Name = "cbxLogYAxis";
            this.cbxLogYAxis.Size = new System.Drawing.Size(112, 17);
            this.cbxLogYAxis.TabIndex = 5;
            this.cbxLogYAxis.Text = "Logarithmic Y Axis";
            this.cbxLogYAxis.UseVisualStyleBackColor = true;
            // 
            // textSizeComboBox
            // 
            this.textSizeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.textSizeComboBox.FormattingEnabled = true;
            this.textSizeComboBox.Items.AddRange(new object[] {
            "x-small",
            "small",
            "normal",
            "large",
            "x-large"});
            this.textSizeComboBox.Location = new System.Drawing.Point(115, 28);
            this.textSizeComboBox.Name = "textSizeComboBox";
            this.textSizeComboBox.Size = new System.Drawing.Size(97, 21);
            this.textSizeComboBox.TabIndex = 3;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.label1.Location = new System.Drawing.Point(112, 12);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(52, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "&Font size:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.label3.Location = new System.Drawing.Point(6, 12);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(58, 13);
            this.label3.TabIndex = 0;
            this.label3.Text = "&Line width:";
            // 
            // checkedListBoxSampleTypes
            // 
            this.checkedListBoxSampleTypes.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.checkedListBoxSampleTypes.FormattingEnabled = true;
            this.checkedListBoxSampleTypes.Location = new System.Drawing.Point(9, 67);
            this.checkedListBoxSampleTypes.Name = "checkedListBoxSampleTypes";
            this.checkedListBoxSampleTypes.Size = new System.Drawing.Size(264, 94);
            this.checkedListBoxSampleTypes.TabIndex = 7;
            // 
            // lblShowSampleTypes
            // 
            this.lblShowSampleTypes.AutoSize = true;
            this.lblShowSampleTypes.Location = new System.Drawing.Point(6, 51);
            this.lblShowSampleTypes.Name = "lblShowSampleTypes";
            this.lblShowSampleTypes.Size = new System.Drawing.Size(101, 13);
            this.lblShowSampleTypes.TabIndex = 6;
            this.lblShowSampleTypes.Text = "Show sample types:";
            // 
            // cbxSingleBatch
            // 
            this.cbxSingleBatch.AutoSize = true;
            this.cbxSingleBatch.Location = new System.Drawing.Point(9, 213);
            this.cbxSingleBatch.Name = "cbxSingleBatch";
            this.cbxSingleBatch.Size = new System.Drawing.Size(197, 17);
            this.cbxSingleBatch.TabIndex = 8;
            this.cbxSingleBatch.Text = "Show replicates from only one batch";
            this.cbxSingleBatch.UseVisualStyleBackColor = true;
            // 
            // cbxShowLegend
            // 
            this.cbxShowLegend.AutoSize = true;
            this.cbxShowLegend.Location = new System.Drawing.Point(9, 236);
            this.cbxShowLegend.Name = "cbxShowLegend";
            this.cbxShowLegend.Size = new System.Drawing.Size(88, 17);
            this.cbxShowLegend.TabIndex = 9;
            this.cbxShowLegend.Text = "Show legend";
            this.cbxShowLegend.UseVisualStyleBackColor = true;
            // 
            // cbxShowFiguresOfMerit
            // 
            this.cbxShowFiguresOfMerit.AutoSize = true;
            this.cbxShowFiguresOfMerit.Location = new System.Drawing.Point(9, 259);
            this.cbxShowFiguresOfMerit.Name = "cbxShowFiguresOfMerit";
            this.cbxShowFiguresOfMerit.Size = new System.Drawing.Size(124, 17);
            this.cbxShowFiguresOfMerit.TabIndex = 10;
            this.cbxShowFiguresOfMerit.Text = "Show figures of merit";
            this.cbxShowFiguresOfMerit.UseVisualStyleBackColor = true;
            // 
            // cbxShowBootstrapCurves
            // 
            this.cbxShowBootstrapCurves.AutoSize = true;
            this.cbxShowBootstrapCurves.Location = new System.Drawing.Point(9, 282);
            this.cbxShowBootstrapCurves.Name = "cbxShowBootstrapCurves";
            this.cbxShowBootstrapCurves.Size = new System.Drawing.Size(135, 17);
            this.cbxShowBootstrapCurves.TabIndex = 11;
            this.cbxShowBootstrapCurves.Text = "Show bootstrap curves";
            this.cbxShowBootstrapCurves.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnCancel.Location = new System.Drawing.Point(198, 331);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 13;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnOk.Location = new System.Drawing.Point(117, 331);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 12;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // textLineWidth
            // 
            this.textLineWidth.Location = new System.Drawing.Point(9, 28);
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
            this.textLineWidth.Size = new System.Drawing.Size(100, 20);
            this.textLineWidth.TabIndex = 14;
            this.textLineWidth.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // CalibrationCurveOptionsDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(285, 366);
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
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "CalibrationCurveOptionsDlg";
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