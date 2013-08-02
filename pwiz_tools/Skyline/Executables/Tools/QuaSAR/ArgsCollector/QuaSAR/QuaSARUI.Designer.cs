namespace QuaSAR
{
    partial class QuaSARUI
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
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.labelTitle = new System.Windows.Forms.Label();
            this.tboxTitle = new System.Windows.Forms.TextBox();
            this.gboxGenerate = new System.Windows.Forms.GroupBox();
            this.cboxLODLOQComp = new System.Windows.Forms.CheckBox();
            this.cboxPeakAreaPlots = new System.Windows.Forms.CheckBox();
            this.cboxLODLOQTable = new System.Windows.Forms.CheckBox();
            this.cboxCalCurves = new System.Windows.Forms.CheckBox();
            this.cboxCVTable = new System.Windows.Forms.CheckBox();
            this.cboxPAR = new System.Windows.Forms.CheckBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.tboxLogScale = new System.Windows.Forms.TextBox();
            this.tboxLinearScale = new System.Windows.Forms.TextBox();
            this.labelLogScale = new System.Windows.Forms.Label();
            this.labelLinearScale = new System.Windows.Forms.Label();
            this.cboxAuDIT = new System.Windows.Forms.CheckBox();
            this.tboxAuDITCVThreshold = new System.Windows.Forms.TextBox();
            this.labelAuDITCV = new System.Windows.Forms.Label();
            this.btnDefault = new System.Windows.Forms.Button();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.cboxEndogenousCalc = new System.Windows.Forms.CheckBox();
            this.labelEndogenousConfidence = new System.Windows.Forms.Label();
            this.tboxEndoConf = new System.Windows.Forms.TextBox();
            this.cboxStandardPresent = new System.Windows.Forms.CheckBox();
            this.labelNumberTransitions = new System.Windows.Forms.Label();
            this.numberTransitions = new System.Windows.Forms.NumericUpDown();
            this.calcurvePanel = new System.Windows.Forms.Panel();
            this.gboxGenerate.SuspendLayout();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numberTransitions)).BeginInit();
            this.calcurvePanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(273, 10);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 17;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(273, 38);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 18;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // labelTitle
            // 
            this.labelTitle.AutoSize = true;
            this.labelTitle.Location = new System.Drawing.Point(12, 15);
            this.labelTitle.Name = "labelTitle";
            this.labelTitle.Size = new System.Drawing.Size(27, 13);
            this.labelTitle.TabIndex = 0;
            this.labelTitle.Text = "Title";
            // 
            // tboxTitle
            // 
            this.tboxTitle.Location = new System.Drawing.Point(45, 12);
            this.tboxTitle.Name = "tboxTitle";
            this.tboxTitle.Size = new System.Drawing.Size(210, 20);
            this.tboxTitle.TabIndex = 1;
            // 
            // gboxGenerate
            // 
            this.gboxGenerate.Controls.Add(this.cboxLODLOQComp);
            this.gboxGenerate.Controls.Add(this.cboxPeakAreaPlots);
            this.gboxGenerate.Controls.Add(this.cboxLODLOQTable);
            this.gboxGenerate.Location = new System.Drawing.Point(15, 255);
            this.gboxGenerate.Name = "gboxGenerate";
            this.gboxGenerate.Size = new System.Drawing.Size(243, 94);
            this.gboxGenerate.TabIndex = 8;
            this.gboxGenerate.TabStop = false;
            this.gboxGenerate.Text = "Generate";
            // 
            // cboxLODLOQComp
            // 
            this.cboxLODLOQComp.AutoSize = true;
            this.cboxLODLOQComp.Location = new System.Drawing.Point(15, 61);
            this.cboxLODLOQComp.Name = "cboxLODLOQComp";
            this.cboxLODLOQComp.Size = new System.Drawing.Size(132, 17);
            this.cboxLODLOQComp.TabIndex = 2;
            this.cboxLODLOQComp.Text = "LOD/LOQ comparison";
            this.cboxLODLOQComp.UseVisualStyleBackColor = true;
            // 
            // cboxPeakAreaPlots
            // 
            this.cboxPeakAreaPlots.AutoSize = true;
            this.cboxPeakAreaPlots.Location = new System.Drawing.Point(15, 28);
            this.cboxPeakAreaPlots.Name = "cboxPeakAreaPlots";
            this.cboxPeakAreaPlots.Size = new System.Drawing.Size(100, 17);
            this.cboxPeakAreaPlots.TabIndex = 0;
            this.cboxPeakAreaPlots.Text = "Peak area plots";
            this.cboxPeakAreaPlots.UseVisualStyleBackColor = true;
            // 
            // cboxLODLOQTable
            // 
            this.cboxLODLOQTable.AutoSize = true;
            this.cboxLODLOQTable.Checked = true;
            this.cboxLODLOQTable.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cboxLODLOQTable.Location = new System.Drawing.Point(121, 28);
            this.cboxLODLOQTable.Name = "cboxLODLOQTable";
            this.cboxLODLOQTable.Size = new System.Drawing.Size(101, 17);
            this.cboxLODLOQTable.TabIndex = 1;
            this.cboxLODLOQTable.Text = "LOD/LOQ table";
            this.cboxLODLOQTable.UseVisualStyleBackColor = true;
            // 
            // cboxCalCurves
            // 
            this.cboxCalCurves.AutoSize = true;
            this.cboxCalCurves.Checked = true;
            this.cboxCalCurves.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cboxCalCurves.Location = new System.Drawing.Point(15, 74);
            this.cboxCalCurves.Name = "cboxCalCurves";
            this.cboxCalCurves.Size = new System.Drawing.Size(156, 17);
            this.cboxCalCurves.TabIndex = 6;
            this.cboxCalCurves.Text = "Generate calibration curves";
            this.cboxCalCurves.UseVisualStyleBackColor = true;
            this.cboxCalCurves.CheckedChanged += new System.EventHandler(this.cboxCalCurves_CheckedChanged);
            // 
            // cboxCVTable
            // 
            this.cboxCVTable.AutoSize = true;
            this.cboxCVTable.Checked = true;
            this.cboxCVTable.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cboxCVTable.Location = new System.Drawing.Point(129, 42);
            this.cboxCVTable.Name = "cboxCVTable";
            this.cboxCVTable.Size = new System.Drawing.Size(113, 17);
            this.cboxCVTable.TabIndex = 5;
            this.cboxCVTable.Text = "Generate CV table";
            this.cboxCVTable.UseVisualStyleBackColor = true;
            // 
            // cboxPAR
            // 
            this.cboxPAR.AutoSize = true;
            this.cboxPAR.Location = new System.Drawing.Point(15, 365);
            this.cboxPAR.Name = "cboxPAR";
            this.cboxPAR.Size = new System.Drawing.Size(125, 17);
            this.cboxPAR.TabIndex = 9;
            this.cboxPAR.Text = "Use PAR for analysis";
            this.cboxPAR.UseVisualStyleBackColor = true;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.tboxLogScale);
            this.groupBox1.Controls.Add(this.tboxLinearScale);
            this.groupBox1.Controls.Add(this.labelLogScale);
            this.groupBox1.Controls.Add(this.labelLinearScale);
            this.groupBox1.Location = new System.Drawing.Point(0, 55);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(240, 73);
            this.groupBox1.TabIndex = 2;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Maximum calibration curve value";
            // 
            // tboxLogScale
            // 
            this.tboxLogScale.Location = new System.Drawing.Point(121, 43);
            this.tboxLogScale.Name = "tboxLogScale";
            this.tboxLogScale.Size = new System.Drawing.Size(82, 20);
            this.tboxLogScale.TabIndex = 3;
            this.tboxLogScale.Text = "150";
            this.tboxLogScale.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumericTextBox_KeyPress);
            // 
            // tboxLinearScale
            // 
            this.tboxLinearScale.Location = new System.Drawing.Point(18, 43);
            this.tboxLinearScale.Name = "tboxLinearScale";
            this.tboxLinearScale.Size = new System.Drawing.Size(82, 20);
            this.tboxLinearScale.TabIndex = 1;
            this.tboxLinearScale.Text = "150";
            this.tboxLinearScale.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumericTextBox_KeyPress);
            // 
            // labelLogScale
            // 
            this.labelLogScale.AutoSize = true;
            this.labelLogScale.Location = new System.Drawing.Point(111, 27);
            this.labelLogScale.Name = "labelLogScale";
            this.labelLogScale.Size = new System.Drawing.Size(53, 13);
            this.labelLogScale.TabIndex = 2;
            this.labelLogScale.Text = "Log scale";
            // 
            // labelLinearScale
            // 
            this.labelLinearScale.AutoSize = true;
            this.labelLinearScale.Location = new System.Drawing.Point(15, 27);
            this.labelLinearScale.Name = "labelLinearScale";
            this.labelLinearScale.Size = new System.Drawing.Size(64, 13);
            this.labelLinearScale.TabIndex = 0;
            this.labelLinearScale.Text = "Linear scale";
            // 
            // cboxAuDIT
            // 
            this.cboxAuDIT.AutoSize = true;
            this.cboxAuDIT.Checked = true;
            this.cboxAuDIT.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cboxAuDIT.Location = new System.Drawing.Point(15, 397);
            this.cboxAuDIT.Name = "cboxAuDIT";
            this.cboxAuDIT.Size = new System.Drawing.Size(96, 17);
            this.cboxAuDIT.TabIndex = 10;
            this.cboxAuDIT.Text = "Perform AuDIT";
            this.cboxAuDIT.UseVisualStyleBackColor = true;
            this.cboxAuDIT.CheckedChanged += new System.EventHandler(this.cboxAuDIT_CheckedChanged);
            // 
            // tboxAuDITCVThreshold
            // 
            this.tboxAuDITCVThreshold.Location = new System.Drawing.Point(119, 423);
            this.tboxAuDITCVThreshold.Name = "tboxAuDITCVThreshold";
            this.tboxAuDITCVThreshold.Size = new System.Drawing.Size(136, 20);
            this.tboxAuDITCVThreshold.TabIndex = 12;
            this.tboxAuDITCVThreshold.Text = "0.2";
            this.tboxAuDITCVThreshold.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumericTextBox_KeyPress);
            // 
            // labelAuDITCV
            // 
            this.labelAuDITCV.AutoSize = true;
            this.labelAuDITCV.Location = new System.Drawing.Point(12, 426);
            this.labelAuDITCV.Name = "labelAuDITCV";
            this.labelAuDITCV.Size = new System.Drawing.Size(101, 13);
            this.labelAuDITCV.TabIndex = 11;
            this.labelAuDITCV.Text = "AuDIT CV threshold";
            // 
            // btnDefault
            // 
            this.btnDefault.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.btnDefault.Location = new System.Drawing.Point(81, 523);
            this.btnDefault.Name = "btnDefault";
            this.btnDefault.Size = new System.Drawing.Size(124, 23);
            this.btnDefault.TabIndex = 16;
            this.btnDefault.Text = "Use Default Values";
            this.btnDefault.UseVisualStyleBackColor = true;
            this.btnDefault.Click += new System.EventHandler(this.btnDefault_Click);
            // 
            // cboxEndogenousCalc
            // 
            this.cboxEndogenousCalc.AutoSize = true;
            this.cboxEndogenousCalc.Location = new System.Drawing.Point(15, 459);
            this.cboxEndogenousCalc.Name = "cboxEndogenousCalc";
            this.cboxEndogenousCalc.Size = new System.Drawing.Size(190, 17);
            this.cboxEndogenousCalc.TabIndex = 13;
            this.cboxEndogenousCalc.Text = "Perform endogenous determination";
            this.cboxEndogenousCalc.UseVisualStyleBackColor = true;
            this.cboxEndogenousCalc.CheckedChanged += new System.EventHandler(this.cboxEndogenousCalc_CheckedChanged);
            // 
            // labelEndogenousConfidence
            // 
            this.labelEndogenousConfidence.AutoSize = true;
            this.labelEndogenousConfidence.Location = new System.Drawing.Point(12, 490);
            this.labelEndogenousConfidence.Name = "labelEndogenousConfidence";
            this.labelEndogenousConfidence.Size = new System.Drawing.Size(148, 13);
            this.labelEndogenousConfidence.TabIndex = 14;
            this.labelEndogenousConfidence.Text = "Endogenous confidence level";
            // 
            // tboxEndoConf
            // 
            this.tboxEndoConf.Enabled = false;
            this.tboxEndoConf.Location = new System.Drawing.Point(166, 487);
            this.tboxEndoConf.Name = "tboxEndoConf";
            this.tboxEndoConf.Size = new System.Drawing.Size(89, 20);
            this.tboxEndoConf.TabIndex = 15;
            this.tboxEndoConf.Text = "0.95";
            this.tboxEndoConf.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumericTextBox_KeyPress);
            // 
            // cboxStandardPresent
            // 
            this.cboxStandardPresent.AutoSize = true;
            this.cboxStandardPresent.Location = new System.Drawing.Point(15, 42);
            this.cboxStandardPresent.Name = "cboxStandardPresent";
            this.cboxStandardPresent.Size = new System.Drawing.Size(107, 17);
            this.cboxStandardPresent.TabIndex = 4;
            this.cboxStandardPresent.Text = "Standard present";
            this.cboxStandardPresent.UseVisualStyleBackColor = true;
            this.cboxStandardPresent.CheckedChanged += new System.EventHandler(this.cboxStandardPresent_CheckedChanged);
            // 
            // labelNumberTransitions
            // 
            this.labelNumberTransitions.AutoSize = true;
            this.labelNumberTransitions.Location = new System.Drawing.Point(-3, 12);
            this.labelNumberTransitions.Name = "labelNumberTransitions";
            this.labelNumberTransitions.Size = new System.Drawing.Size(141, 13);
            this.labelNumberTransitions.TabIndex = 0;
            this.labelNumberTransitions.Text = "Number of transitions to plot:";
            // 
            // numberTransitions
            // 
            this.numberTransitions.Location = new System.Drawing.Point(144, 10);
            this.numberTransitions.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numberTransitions.Name = "numberTransitions";
            this.numberTransitions.Size = new System.Drawing.Size(96, 20);
            this.numberTransitions.TabIndex = 1;
            this.numberTransitions.Value = new decimal(new int[] {
            3,
            0,
            0,
            0});
            // 
            // calcurvePanel
            // 
            this.calcurvePanel.Controls.Add(this.labelNumberTransitions);
            this.calcurvePanel.Controls.Add(this.groupBox1);
            this.calcurvePanel.Controls.Add(this.numberTransitions);
            this.calcurvePanel.Location = new System.Drawing.Point(15, 107);
            this.calcurvePanel.Name = "calcurvePanel";
            this.calcurvePanel.Size = new System.Drawing.Size(240, 131);
            this.calcurvePanel.TabIndex = 7;
            // 
            // QuaSARUI
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(360, 558);
            this.ControlBox = false;
            this.Controls.Add(this.calcurvePanel);
            this.Controls.Add(this.cboxStandardPresent);
            this.Controls.Add(this.cboxCalCurves);
            this.Controls.Add(this.tboxEndoConf);
            this.Controls.Add(this.labelEndogenousConfidence);
            this.Controls.Add(this.cboxEndogenousCalc);
            this.Controls.Add(this.cboxCVTable);
            this.Controls.Add(this.btnDefault);
            this.Controls.Add(this.labelAuDITCV);
            this.Controls.Add(this.tboxAuDITCVThreshold);
            this.Controls.Add(this.cboxAuDIT);
            this.Controls.Add(this.cboxPAR);
            this.Controls.Add(this.gboxGenerate);
            this.Controls.Add(this.tboxTitle);
            this.Controls.Add(this.labelTitle);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "QuaSARUI";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.Text = "QuaSAR";
            this.Load += new System.EventHandler(this.QuaSAR_Load);
            this.gboxGenerate.ResumeLayout(false);
            this.gboxGenerate.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numberTransitions)).EndInit();
            this.calcurvePanel.ResumeLayout(false);
            this.calcurvePanel.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label labelTitle;
        private System.Windows.Forms.TextBox tboxTitle;
        private System.Windows.Forms.GroupBox gboxGenerate;
        private System.Windows.Forms.CheckBox cboxPeakAreaPlots;
        private System.Windows.Forms.CheckBox cboxLODLOQTable;
        private System.Windows.Forms.CheckBox cboxCalCurves;
        private System.Windows.Forms.CheckBox cboxCVTable;
        private System.Windows.Forms.CheckBox cboxPAR;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TextBox tboxLogScale;
        private System.Windows.Forms.TextBox tboxLinearScale;
        private System.Windows.Forms.Label labelLogScale;
        private System.Windows.Forms.Label labelLinearScale;
        private System.Windows.Forms.CheckBox cboxAuDIT;
        private System.Windows.Forms.TextBox tboxAuDITCVThreshold;
        private System.Windows.Forms.Label labelAuDITCV;
        private System.Windows.Forms.Button btnDefault;
        private System.Windows.Forms.ToolTip helpTip;
        private System.Windows.Forms.CheckBox cboxLODLOQComp;
        private System.Windows.Forms.CheckBox cboxEndogenousCalc;
        private System.Windows.Forms.Label labelEndogenousConfidence;
        private System.Windows.Forms.TextBox tboxEndoConf;
        private System.Windows.Forms.CheckBox cboxStandardPresent;
        private System.Windows.Forms.Label labelNumberTransitions;
        private System.Windows.Forms.NumericUpDown numberTransitions;
        private System.Windows.Forms.Panel calcurvePanel;
    }
}