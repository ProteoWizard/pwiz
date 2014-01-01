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
            this.cboxCalCurves = new System.Windows.Forms.CheckBox();
            this.cboxLODLOQTable = new System.Windows.Forms.CheckBox();
            this.cboxCVTable = new System.Windows.Forms.CheckBox();
            this.cboxPAR = new System.Windows.Forms.CheckBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.tboxLogScale = new System.Windows.Forms.TextBox();
            this.tboxLinearScale = new System.Windows.Forms.TextBox();
            this.labelLogScale = new System.Windows.Forms.Label();
            this.labelLinearScale = new System.Windows.Forms.Label();
            this.cboxAuDIT = new System.Windows.Forms.CheckBox();
            this.cboxGraphPlot = new System.Windows.Forms.CheckBox();
            this.labelGraphPlot = new System.Windows.Forms.Label();
            this.tboxAuDITCVThreshold = new System.Windows.Forms.TextBox();
            this.labelAuDITCV = new System.Windows.Forms.Label();
            this.btnDefault = new System.Windows.Forms.Button();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.cboxEndogenousCalc = new System.Windows.Forms.CheckBox();
            this.labelEndogenousConfidence = new System.Windows.Forms.Label();
            this.tboxEndoConf = new System.Windows.Forms.TextBox();
            this.labelNumberTransitions = new System.Windows.Forms.Label();
            this.numberTransitions = new System.Windows.Forms.ComboBox();
            this.gboxOptions = new System.Windows.Forms.GroupBox();
            this.comboBoxStandard = new System.Windows.Forms.ComboBox();
            this.comboBoxAnalyte = new System.Windows.Forms.ComboBox();
            this.tboxUnits = new System.Windows.Forms.TextBox();
            this.labelUnits = new System.Windows.Forms.Label();
            this.labelStandard = new System.Windows.Forms.Label();
            this.labelAnalyte = new System.Windows.Forms.Label();
            this.gboxPlots = new System.Windows.Forms.GroupBox();
            this.gboxAuDIT = new System.Windows.Forms.GroupBox();
            this.gboxEndogenousEstimation = new System.Windows.Forms.GroupBox();
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.gboxGenerate.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.gboxOptions.SuspendLayout();
            this.gboxPlots.SuspendLayout();
            this.gboxAuDIT.SuspendLayout();
            this.gboxEndogenousEstimation.SuspendLayout();
            this.tabControl.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(280, 10);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 4;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(280, 38);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 5;
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
            this.labelTitle.Text = "&Title";
            // 
            // tboxTitle
            // 
            this.tboxTitle.Location = new System.Drawing.Point(45, 12);
            this.tboxTitle.Name = "tboxTitle";
            this.tboxTitle.Size = new System.Drawing.Size(222, 20);
            this.tboxTitle.TabIndex = 1;
            // 
            // gboxGenerate
            // 
            this.gboxGenerate.Controls.Add(this.cboxLODLOQComp);
            this.gboxGenerate.Controls.Add(this.cboxPeakAreaPlots);
            this.gboxGenerate.Controls.Add(this.cboxCalCurves);
            this.gboxGenerate.Controls.Add(this.cboxLODLOQTable);
            this.gboxGenerate.Controls.Add(this.cboxCVTable);
            this.gboxGenerate.Location = new System.Drawing.Point(9, 6);
            this.gboxGenerate.Name = "gboxGenerate";
            this.gboxGenerate.Size = new System.Drawing.Size(221, 161);
            this.gboxGenerate.TabIndex = 0;
            this.gboxGenerate.TabStop = false;
            this.gboxGenerate.Text = "&Generate";
            // 
            // cboxLODLOQComp
            // 
            this.cboxLODLOQComp.AutoSize = true;
            this.cboxLODLOQComp.Location = new System.Drawing.Point(34, 94);
            this.cboxLODLOQComp.Name = "cboxLODLOQComp";
            this.cboxLODLOQComp.Size = new System.Drawing.Size(132, 17);
            this.cboxLODLOQComp.TabIndex = 3;
            this.cboxLODLOQComp.Text = "LOD/LOQ co&mparison";
            this.cboxLODLOQComp.UseVisualStyleBackColor = true;
            // 
            // cboxPeakAreaPlots
            // 
            this.cboxPeakAreaPlots.AutoSize = true;
            this.cboxPeakAreaPlots.Location = new System.Drawing.Point(16, 126);
            this.cboxPeakAreaPlots.Name = "cboxPeakAreaPlots";
            this.cboxPeakAreaPlots.Size = new System.Drawing.Size(100, 17);
            this.cboxPeakAreaPlots.TabIndex = 4;
            this.cboxPeakAreaPlots.Text = "&Peak area plots";
            this.cboxPeakAreaPlots.UseVisualStyleBackColor = true;
            // 
            // cboxCalCurves
            // 
            this.cboxCalCurves.AutoSize = true;
            this.cboxCalCurves.Checked = true;
            this.cboxCalCurves.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cboxCalCurves.Location = new System.Drawing.Point(16, 30);
            this.cboxCalCurves.Name = "cboxCalCurves";
            this.cboxCalCurves.Size = new System.Drawing.Size(109, 17);
            this.cboxCalCurves.TabIndex = 1;
            this.cboxCalCurves.Text = "&Plot each peptide";
            this.cboxCalCurves.UseVisualStyleBackColor = true;
            // 
            // cboxLODLOQTable
            // 
            this.cboxLODLOQTable.AutoSize = true;
            this.cboxLODLOQTable.Checked = true;
            this.cboxLODLOQTable.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cboxLODLOQTable.Location = new System.Drawing.Point(16, 62);
            this.cboxLODLOQTable.Name = "cboxLODLOQTable";
            this.cboxLODLOQTable.Size = new System.Drawing.Size(101, 17);
            this.cboxLODLOQTable.TabIndex = 2;
            this.cboxLODLOQTable.Text = "&LOD/LOQ table";
            this.cboxLODLOQTable.UseVisualStyleBackColor = true;
            this.cboxLODLOQTable.CheckedChanged += new System.EventHandler(this.cboxLODLOQTable_CheckedChanged);
            // 
            // cboxCVTable
            // 
            this.cboxCVTable.AutoSize = true;
            this.cboxCVTable.Checked = true;
            this.cboxCVTable.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cboxCVTable.Location = new System.Drawing.Point(137, 30);
            this.cboxCVTable.Name = "cboxCVTable";
            this.cboxCVTable.Size = new System.Drawing.Size(69, 17);
            this.cboxCVTable.TabIndex = 1;
            this.cboxCVTable.Text = " C&V table";
            this.cboxCVTable.UseVisualStyleBackColor = true;
            // 
            // cboxPAR
            // 
            this.cboxPAR.AutoSize = true;
            this.cboxPAR.Location = new System.Drawing.Point(16, 28);
            this.cboxPAR.Name = "cboxPAR";
            this.cboxPAR.Size = new System.Drawing.Size(125, 17);
            this.cboxPAR.TabIndex = 1;
            this.cboxPAR.Text = "&Use PAR for analysis";
            this.cboxPAR.UseVisualStyleBackColor = true;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.tboxLogScale);
            this.groupBox1.Controls.Add(this.tboxLinearScale);
            this.groupBox1.Controls.Add(this.labelLogScale);
            this.groupBox1.Controls.Add(this.labelLinearScale);
            this.groupBox1.Location = new System.Drawing.Point(16, 76);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(191, 73);
            this.groupBox1.TabIndex = 2;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "&Maximum calibration curve value";
            // 
            // tboxLogScale
            // 
            this.tboxLogScale.Location = new System.Drawing.Point(111, 43);
            this.tboxLogScale.Name = "tboxLogScale";
            this.tboxLogScale.Size = new System.Drawing.Size(64, 20);
            this.tboxLogScale.TabIndex = 3;
            this.tboxLogScale.Text = "150";
            this.tboxLogScale.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumericTextBox_KeyPress);
            // 
            // tboxLinearScale
            // 
            this.tboxLinearScale.Location = new System.Drawing.Point(18, 43);
            this.tboxLinearScale.Name = "tboxLinearScale";
            this.tboxLinearScale.Size = new System.Drawing.Size(64, 20);
            this.tboxLinearScale.TabIndex = 1;
            this.tboxLinearScale.Text = "150";
            this.tboxLinearScale.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumericTextBox_KeyPress);
            // 
            // labelLogScale
            // 
            this.labelLogScale.AutoSize = true;
            this.labelLogScale.Location = new System.Drawing.Point(108, 27);
            this.labelLogScale.Name = "labelLogScale";
            this.labelLogScale.Size = new System.Drawing.Size(56, 13);
            this.labelLogScale.TabIndex = 2;
            this.labelLogScale.Text = "L&og scale:";
            // 
            // labelLinearScale
            // 
            this.labelLinearScale.AutoSize = true;
            this.labelLinearScale.Location = new System.Drawing.Point(15, 27);
            this.labelLinearScale.Name = "labelLinearScale";
            this.labelLinearScale.Size = new System.Drawing.Size(67, 13);
            this.labelLinearScale.TabIndex = 0;
            this.labelLinearScale.Text = "&Linear scale:";
            // 
            // cboxAuDIT
            // 
            this.cboxAuDIT.AutoSize = true;
            this.cboxAuDIT.Checked = true;
            this.cboxAuDIT.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cboxAuDIT.Location = new System.Drawing.Point(13, 28);
            this.cboxAuDIT.Name = "cboxAuDIT";
            this.cboxAuDIT.Size = new System.Drawing.Size(96, 17);
            this.cboxAuDIT.TabIndex = 0;
            this.cboxAuDIT.Text = "&Perform AuDIT";
            this.cboxAuDIT.UseVisualStyleBackColor = true;
            this.cboxAuDIT.CheckedChanged += new System.EventHandler(this.cboxAuDIT_CheckedChanged);
            // 
            // cboxGraphPlot
            // 
            this.cboxGraphPlot.AutoSize = true;
            this.cboxGraphPlot.Location = new System.Drawing.Point(17, 50);
            this.cboxGraphPlot.Name = "cboxGraphPlot";
            this.cboxGraphPlot.Size = new System.Drawing.Size(154, 17);
            this.cboxGraphPlot.TabIndex = 0;
            this.cboxGraphPlot.Text = "&Generate graphs as JPEGs";
            this.cboxGraphPlot.UseVisualStyleBackColor = true;
            // 
            // labelGraphPlot
            // 
            this.labelGraphPlot.Location = new System.Drawing.Point(0, 0);
            this.labelGraphPlot.Name = "labelGraphPlot";
            this.labelGraphPlot.Size = new System.Drawing.Size(100, 23);
            this.labelGraphPlot.TabIndex = 0;
            // 
            // tboxAuDITCVThreshold
            // 
            this.tboxAuDITCVThreshold.Location = new System.Drawing.Point(117, 55);
            this.tboxAuDITCVThreshold.Name = "tboxAuDITCVThreshold";
            this.tboxAuDITCVThreshold.Size = new System.Drawing.Size(90, 20);
            this.tboxAuDITCVThreshold.TabIndex = 2;
            this.tboxAuDITCVThreshold.Text = "0.2";
            this.tboxAuDITCVThreshold.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumericTextBox_KeyPress);
            // 
            // labelAuDITCV
            // 
            this.labelAuDITCV.AutoSize = true;
            this.labelAuDITCV.Location = new System.Drawing.Point(10, 58);
            this.labelAuDITCV.Name = "labelAuDITCV";
            this.labelAuDITCV.Size = new System.Drawing.Size(104, 13);
            this.labelAuDITCV.TabIndex = 1;
            this.labelAuDITCV.Text = "AuDIT C&V threshold:";
            // 
            // btnDefault
            // 
            this.btnDefault.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.btnDefault.Location = new System.Drawing.Point(97, 455);
            this.btnDefault.Name = "btnDefault";
            this.btnDefault.Size = new System.Drawing.Size(85, 23);
            this.btnDefault.TabIndex = 3;
            this.btnDefault.Text = "Use &Defaults";
            this.btnDefault.UseVisualStyleBackColor = true;
            this.btnDefault.Click += new System.EventHandler(this.btnDefault_Click);
            // 
            // cboxEndogenousCalc
            // 
            this.cboxEndogenousCalc.AutoSize = true;
            this.cboxEndogenousCalc.Location = new System.Drawing.Point(16, 29);
            this.cboxEndogenousCalc.Name = "cboxEndogenousCalc";
            this.cboxEndogenousCalc.Size = new System.Drawing.Size(190, 17);
            this.cboxEndogenousCalc.TabIndex = 0;
            this.cboxEndogenousCalc.Text = "Perform endogenous &determination";
            this.cboxEndogenousCalc.UseVisualStyleBackColor = true;
            this.cboxEndogenousCalc.CheckedChanged += new System.EventHandler(this.cboxEndogenousCalc_CheckedChanged);
            // 
            // labelEndogenousConfidence
            // 
            this.labelEndogenousConfidence.AutoSize = true;
            this.labelEndogenousConfidence.Location = new System.Drawing.Point(13, 58);
            this.labelEndogenousConfidence.Name = "labelEndogenousConfidence";
            this.labelEndogenousConfidence.Size = new System.Drawing.Size(151, 13);
            this.labelEndogenousConfidence.TabIndex = 1;
            this.labelEndogenousConfidence.Text = "Endogenous &confidence level:";
            // 
            // tboxEndoConf
            // 
            this.tboxEndoConf.Enabled = false;
            this.tboxEndoConf.Location = new System.Drawing.Point(167, 55);
            this.tboxEndoConf.Name = "tboxEndoConf";
            this.tboxEndoConf.Size = new System.Drawing.Size(40, 20);
            this.tboxEndoConf.TabIndex = 2;
            this.tboxEndoConf.Text = "0.95";
            this.tboxEndoConf.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NumericTextBox_KeyPress);
            // 
            // labelNumberTransitions
            // 
            this.labelNumberTransitions.AutoSize = true;
            this.labelNumberTransitions.Location = new System.Drawing.Point(13, 25);
            this.labelNumberTransitions.Name = "labelNumberTransitions";
            this.labelNumberTransitions.Size = new System.Drawing.Size(141, 13);
            this.labelNumberTransitions.TabIndex = 0;
            this.labelNumberTransitions.Text = "&Number of transitions to plot:";
            // 
            // numberTransitions
            // 
            this.numberTransitions.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.numberTransitions.Items.AddRange(new object[] {
            "1",
            "2",
            "3",
            "4",
            "5",
            "6"});
            this.numberTransitions.Location = new System.Drawing.Point(160, 23);
            this.numberTransitions.Name = "numberTransitions";
            this.numberTransitions.Size = new System.Drawing.Size(47, 21);
            this.numberTransitions.TabIndex = 2;
            // 
            // gboxOptions
            // 
            this.gboxOptions.Controls.Add(this.comboBoxStandard);
            this.gboxOptions.Controls.Add(this.comboBoxAnalyte);
            this.gboxOptions.Controls.Add(this.tboxUnits);
            this.gboxOptions.Controls.Add(this.labelUnits);
            this.gboxOptions.Controls.Add(this.labelStandard);
            this.gboxOptions.Controls.Add(this.labelAnalyte);
            this.gboxOptions.Controls.Add(this.cboxPAR);
            this.gboxOptions.Location = new System.Drawing.Point(9, 184);
            this.gboxOptions.Name = "gboxOptions";
            this.gboxOptions.Size = new System.Drawing.Size(221, 162);
            this.gboxOptions.TabIndex = 1;
            this.gboxOptions.TabStop = false;
            this.gboxOptions.Text = "&Options";
            // 
            // comboBoxStandard
            // 
            this.comboBoxStandard.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxStandard.FormattingEnabled = true;
            this.comboBoxStandard.Location = new System.Drawing.Point(69, 88);
            this.comboBoxStandard.Name = "comboBoxStandard";
            this.comboBoxStandard.Size = new System.Drawing.Size(134, 21);
            this.comboBoxStandard.TabIndex = 5;
            // 
            // comboBoxAnalyte
            // 
            this.comboBoxAnalyte.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxAnalyte.FormattingEnabled = true;
            this.comboBoxAnalyte.Location = new System.Drawing.Point(69, 56);
            this.comboBoxAnalyte.Name = "comboBoxAnalyte";
            this.comboBoxAnalyte.Size = new System.Drawing.Size(134, 21);
            this.comboBoxAnalyte.TabIndex = 3;
            // 
            // tboxUnits
            // 
            this.tboxUnits.Location = new System.Drawing.Point(69, 121);
            this.tboxUnits.Name = "tboxUnits";
            this.tboxUnits.Size = new System.Drawing.Size(134, 20);
            this.tboxUnits.TabIndex = 7;
            this.tboxUnits.Text = "fmol/ul";
            // 
            // labelUnits
            // 
            this.labelUnits.AutoSize = true;
            this.labelUnits.Location = new System.Drawing.Point(14, 124);
            this.labelUnits.Name = "labelUnits";
            this.labelUnits.Size = new System.Drawing.Size(34, 13);
            this.labelUnits.TabIndex = 6;
            this.labelUnits.Text = "U&nits:";
            // 
            // labelStandard
            // 
            this.labelStandard.AutoSize = true;
            this.labelStandard.Location = new System.Drawing.Point(13, 91);
            this.labelStandard.Name = "labelStandard";
            this.labelStandard.Size = new System.Drawing.Size(53, 13);
            this.labelStandard.TabIndex = 4;
            this.labelStandard.Text = "&Standard:";
            // 
            // labelAnalyte
            // 
            this.labelAnalyte.AutoSize = true;
            this.labelAnalyte.Location = new System.Drawing.Point(13, 59);
            this.labelAnalyte.Name = "labelAnalyte";
            this.labelAnalyte.Size = new System.Drawing.Size(45, 13);
            this.labelAnalyte.TabIndex = 2;
            this.labelAnalyte.Text = "&Analyte:";
            // 
            // gboxPlots
            // 
            this.gboxPlots.Controls.Add(this.groupBox1);
            this.gboxPlots.Controls.Add(this.labelNumberTransitions);
            this.gboxPlots.Controls.Add(this.numberTransitions);
            this.gboxPlots.Controls.Add(this.cboxGraphPlot);
            this.gboxPlots.Location = new System.Drawing.Point(9, 6);
            this.gboxPlots.Name = "gboxPlots";
            this.gboxPlots.Size = new System.Drawing.Size(220, 158);
            this.gboxPlots.TabIndex = 0;
            this.gboxPlots.TabStop = false;
            this.gboxPlots.Text = "&Plots";
            // 
            // gboxAuDIT
            // 
            this.gboxAuDIT.Controls.Add(this.cboxAuDIT);
            this.gboxAuDIT.Controls.Add(this.labelAuDITCV);
            this.gboxAuDIT.Controls.Add(this.tboxAuDITCVThreshold);
            this.gboxAuDIT.Location = new System.Drawing.Point(9, 6);
            this.gboxAuDIT.Name = "gboxAuDIT";
            this.gboxAuDIT.Size = new System.Drawing.Size(221, 94);
            this.gboxAuDIT.TabIndex = 0;
            this.gboxAuDIT.TabStop = false;
            this.gboxAuDIT.Text = "&AuDIT";
            // 
            // gboxEndogenousEstimation
            // 
            this.gboxEndogenousEstimation.Controls.Add(this.cboxEndogenousCalc);
            this.gboxEndogenousEstimation.Controls.Add(this.labelEndogenousConfidence);
            this.gboxEndogenousEstimation.Controls.Add(this.tboxEndoConf);
            this.gboxEndogenousEstimation.Location = new System.Drawing.Point(9, 112);
            this.gboxEndogenousEstimation.Name = "gboxEndogenousEstimation";
            this.gboxEndogenousEstimation.Size = new System.Drawing.Size(221, 91);
            this.gboxEndogenousEstimation.TabIndex = 1;
            this.gboxEndogenousEstimation.TabStop = false;
            this.gboxEndogenousEstimation.Text = "&Endogenous estimation";
            // 
            // tabControl
            // 
            this.tabControl.Controls.Add(this.tabPage1);
            this.tabControl.Controls.Add(this.tabPage2);
            this.tabControl.Controls.Add(this.tabPage3);
            this.tabControl.Location = new System.Drawing.Point(15, 58);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(252, 390);
            this.tabControl.TabIndex = 2;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.gboxGenerate);
            this.tabPage1.Controls.Add(this.gboxOptions);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(244, 364);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Basic";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.gboxPlots);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(244, 364);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Plots";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.gboxAuDIT);
            this.tabPage3.Controls.Add(this.gboxEndogenousEstimation);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Size = new System.Drawing.Size(244, 364);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Additional Tools";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // QuaSARUI
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(367, 490);
            this.ControlBox = false;
            this.Controls.Add(this.tabControl);
            this.Controls.Add(this.btnDefault);
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
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "QuaSAR";
            this.Load += new System.EventHandler(this.QuaSAR_Load);
            this.gboxGenerate.ResumeLayout(false);
            this.gboxGenerate.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.gboxOptions.ResumeLayout(false);
            this.gboxOptions.PerformLayout();
            this.gboxPlots.ResumeLayout(false);
            this.gboxPlots.PerformLayout();
            this.gboxAuDIT.ResumeLayout(false);
            this.gboxAuDIT.PerformLayout();
            this.gboxEndogenousEstimation.ResumeLayout(false);
            this.gboxEndogenousEstimation.PerformLayout();
            this.tabControl.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            this.tabPage3.ResumeLayout(false);
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
        private System.Windows.Forms.CheckBox cboxGraphPlot;
        private System.Windows.Forms.Label labelGraphPlot;
        private System.Windows.Forms.TextBox tboxAuDITCVThreshold;
        private System.Windows.Forms.Label labelAuDITCV;
        private System.Windows.Forms.Button btnDefault;
        private System.Windows.Forms.ToolTip helpTip;
        private System.Windows.Forms.CheckBox cboxLODLOQComp;
        private System.Windows.Forms.CheckBox cboxEndogenousCalc;
        private System.Windows.Forms.Label labelEndogenousConfidence;
        private System.Windows.Forms.TextBox tboxEndoConf;
        private System.Windows.Forms.Label labelNumberTransitions;
        private System.Windows.Forms.GroupBox gboxOptions;
        private System.Windows.Forms.TextBox tboxUnits;
        private System.Windows.Forms.Label labelUnits;
        private System.Windows.Forms.Label labelStandard;
        private System.Windows.Forms.Label labelAnalyte;
        private System.Windows.Forms.GroupBox gboxPlots;
        private System.Windows.Forms.GroupBox gboxAuDIT;
        private System.Windows.Forms.GroupBox gboxEndogenousEstimation;
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.ComboBox comboBoxStandard;
        private System.Windows.Forms.ComboBox comboBoxAnalyte;
        private System.Windows.Forms.ComboBox numberTransitions;
    }
}
