namespace pwiz.Topograph.ui.Forms
{
    partial class SpectrumForm
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
            this.label1 = new System.Windows.Forms.Label();
            this.tbxScanIndex = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.tbxMsLevel = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.tbxTime = new System.Windows.Forms.TextBox();
            this.cbxShowPeptideMzs = new System.Windows.Forms.CheckBox();
            this.msGraphControlEx1 = new pwiz.Topograph.ui.Controls.MSGraphControlEx();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.label4 = new System.Windows.Forms.Label();
            this.tbxSumOfProfileIntensities = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.tbxScanDuration = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.tbxPeptideSequence = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.tbxMinCharge = new System.Windows.Forms.TextBox();
            this.label9 = new System.Windows.Forms.Label();
            this.tbxMaxCharge = new System.Windows.Forms.TextBox();
            this.label10 = new System.Windows.Forms.Label();
            this.tbxPeptideIntensity = new System.Windows.Forms.TextBox();
            this.cbxShowCentroids = new System.Windows.Forms.CheckBox();
            this.label11 = new System.Windows.Forms.Label();
            this.tbxMassAccuracy = new System.Windows.Forms.TextBox();
            this.cbxShowProfile = new System.Windows.Forms.CheckBox();
            this.label5 = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.tbxChromatogramIonCurrent = new System.Windows.Forms.TextBox();
            this.label13 = new System.Windows.Forms.Label();
            this.comboChromatogram = new System.Windows.Forms.ComboBox();
            this.tbxCentroidIntensitySum = new System.Windows.Forms.TextBox();
            this.label14 = new System.Windows.Forms.Label();
            this.tbxChromatogramRetentionTime = new System.Windows.Forms.TextBox();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.btnPrevPrevScan = new System.Windows.Forms.Button();
            this.btnPrevScan = new System.Windows.Forms.Button();
            this.btnNextScan = new System.Windows.Forms.Button();
            this.btnNextNextScan = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.flowLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label1.Location = new System.Drawing.Point(67, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(61, 29);
            this.label1.TabIndex = 2;
            this.label1.Text = "Scan Index";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxScanIndex
            // 
            this.tbxScanIndex.Location = new System.Drawing.Point(134, 3);
            this.tbxScanIndex.Name = "tbxScanIndex";
            this.tbxScanIndex.Size = new System.Drawing.Size(74, 20);
            this.tbxScanIndex.TabIndex = 3;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(3, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(55, 13);
            this.label2.TabIndex = 0;
            this.label2.Text = "MS Level:";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxMsLevel
            // 
            this.tbxMsLevel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxMsLevel.Location = new System.Drawing.Point(153, 3);
            this.tbxMsLevel.Name = "tbxMsLevel";
            this.tbxMsLevel.ReadOnly = true;
            this.tbxMsLevel.Size = new System.Drawing.Size(154, 20);
            this.tbxMsLevel.TabIndex = 1;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(3, 25);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(76, 13);
            this.label3.TabIndex = 2;
            this.label3.Text = "Retetion Time:";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxTime
            // 
            this.tbxTime.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxTime.Location = new System.Drawing.Point(153, 28);
            this.tbxTime.Name = "tbxTime";
            this.tbxTime.ReadOnly = true;
            this.tbxTime.Size = new System.Drawing.Size(154, 20);
            this.tbxTime.TabIndex = 3;
            // 
            // cbxShowPeptideMzs
            // 
            this.cbxShowPeptideMzs.AutoSize = true;
            this.cbxShowPeptideMzs.Checked = true;
            this.cbxShowPeptideMzs.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbxShowPeptideMzs.Location = new System.Drawing.Point(153, 348);
            this.cbxShowPeptideMzs.Name = "cbxShowPeptideMzs";
            this.cbxShowPeptideMzs.Size = new System.Drawing.Size(139, 17);
            this.cbxShowPeptideMzs.TabIndex = 27;
            this.cbxShowPeptideMzs.Text = "Show Peptide Channels";
            this.cbxShowPeptideMzs.UseVisualStyleBackColor = true;
            this.cbxShowPeptideMzs.CheckedChanged += new System.EventHandler(this.CbxShowPeptideMzsOnCheckedChanged);
            // 
            // msGraphControlEx1
            // 
            this.msGraphControlEx1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.msGraphControlEx1.EditButtons = System.Windows.Forms.MouseButtons.Left;
            this.msGraphControlEx1.EditModifierKeys = System.Windows.Forms.Keys.None;
            this.msGraphControlEx1.IsEnableVPan = false;
            this.msGraphControlEx1.IsEnableVZoom = false;
            this.msGraphControlEx1.Location = new System.Drawing.Point(0, 0);
            this.msGraphControlEx1.Name = "msGraphControlEx1";
            this.msGraphControlEx1.ScrollGrace = 0D;
            this.msGraphControlEx1.ScrollMaxX = 0D;
            this.msGraphControlEx1.ScrollMaxY = 0D;
            this.msGraphControlEx1.ScrollMaxY2 = 0D;
            this.msGraphControlEx1.ScrollMinX = 0D;
            this.msGraphControlEx1.ScrollMinY = 0D;
            this.msGraphControlEx1.ScrollMinY2 = 0D;
            this.msGraphControlEx1.Size = new System.Drawing.Size(617, 475);
            this.msGraphControlEx1.TabIndex = 0;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer1.Location = new System.Drawing.Point(0, 29);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.tableLayoutPanel2);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.msGraphControlEx1);
            this.splitContainer1.Size = new System.Drawing.Size(931, 475);
            this.splitContainer1.SplitterDistance = 310;
            this.splitContainer1.TabIndex = 2;
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.ColumnCount = 2;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 150F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.Controls.Add(this.label2, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this.tbxMsLevel, 1, 0);
            this.tableLayoutPanel2.Controls.Add(this.tbxTime, 1, 1);
            this.tableLayoutPanel2.Controls.Add(this.label3, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this.label4, 0, 3);
            this.tableLayoutPanel2.Controls.Add(this.tbxSumOfProfileIntensities, 1, 3);
            this.tableLayoutPanel2.Controls.Add(this.label6, 0, 2);
            this.tableLayoutPanel2.Controls.Add(this.tbxScanDuration, 1, 2);
            this.tableLayoutPanel2.Controls.Add(this.label7, 0, 9);
            this.tableLayoutPanel2.Controls.Add(this.tbxPeptideSequence, 1, 9);
            this.tableLayoutPanel2.Controls.Add(this.label8, 0, 10);
            this.tableLayoutPanel2.Controls.Add(this.tbxMinCharge, 1, 10);
            this.tableLayoutPanel2.Controls.Add(this.label9, 0, 11);
            this.tableLayoutPanel2.Controls.Add(this.tbxMaxCharge, 1, 11);
            this.tableLayoutPanel2.Controls.Add(this.cbxShowPeptideMzs, 1, 14);
            this.tableLayoutPanel2.Controls.Add(this.label10, 0, 13);
            this.tableLayoutPanel2.Controls.Add(this.tbxPeptideIntensity, 1, 13);
            this.tableLayoutPanel2.Controls.Add(this.cbxShowCentroids, 0, 14);
            this.tableLayoutPanel2.Controls.Add(this.label11, 0, 12);
            this.tableLayoutPanel2.Controls.Add(this.tbxMassAccuracy, 1, 12);
            this.tableLayoutPanel2.Controls.Add(this.cbxShowProfile, 0, 15);
            this.tableLayoutPanel2.Controls.Add(this.label5, 0, 6);
            this.tableLayoutPanel2.Controls.Add(this.label12, 0, 4);
            this.tableLayoutPanel2.Controls.Add(this.tbxChromatogramIonCurrent, 1, 6);
            this.tableLayoutPanel2.Controls.Add(this.label13, 0, 5);
            this.tableLayoutPanel2.Controls.Add(this.comboChromatogram, 1, 5);
            this.tableLayoutPanel2.Controls.Add(this.tbxCentroidIntensitySum, 1, 4);
            this.tableLayoutPanel2.Controls.Add(this.label14, 0, 7);
            this.tableLayoutPanel2.Controls.Add(this.tbxChromatogramRetentionTime, 1, 7);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 19;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.Size = new System.Drawing.Size(310, 475);
            this.tableLayoutPanel2.TabIndex = 0;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(3, 75);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(120, 13);
            this.label4.TabIndex = 6;
            this.label4.Text = "Sum of profile intensities";
            // 
            // tbxSumOfProfileIntensities
            // 
            this.tbxSumOfProfileIntensities.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxSumOfProfileIntensities.Location = new System.Drawing.Point(153, 78);
            this.tbxSumOfProfileIntensities.Name = "tbxSumOfProfileIntensities";
            this.tbxSumOfProfileIntensities.ReadOnly = true;
            this.tbxSumOfProfileIntensities.Size = new System.Drawing.Size(154, 20);
            this.tbxSumOfProfileIntensities.TabIndex = 7;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(3, 50);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(75, 13);
            this.label6.TabIndex = 4;
            this.label6.Text = "Scan Duration";
            // 
            // tbxScanDuration
            // 
            this.tbxScanDuration.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxScanDuration.Location = new System.Drawing.Point(153, 53);
            this.tbxScanDuration.Name = "tbxScanDuration";
            this.tbxScanDuration.ReadOnly = true;
            this.tbxScanDuration.Size = new System.Drawing.Size(154, 20);
            this.tbxScanDuration.TabIndex = 5;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(3, 225);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(43, 13);
            this.label7.TabIndex = 16;
            this.label7.Text = "Peptide";
            // 
            // tbxPeptideSequence
            // 
            this.tbxPeptideSequence.Location = new System.Drawing.Point(153, 228);
            this.tbxPeptideSequence.Name = "tbxPeptideSequence";
            this.tbxPeptideSequence.Size = new System.Drawing.Size(154, 20);
            this.tbxPeptideSequence.TabIndex = 17;
            this.tbxPeptideSequence.TextChanged += new System.EventHandler(this.TbxPeptideSequenceOnTextChanged);
            this.tbxPeptideSequence.Leave += new System.EventHandler(this.TbxPeptideSequenceOnLeave);
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(3, 250);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(61, 13);
            this.label8.TabIndex = 18;
            this.label8.Text = "Min Charge";
            // 
            // tbxMinCharge
            // 
            this.tbxMinCharge.Location = new System.Drawing.Point(153, 253);
            this.tbxMinCharge.Name = "tbxMinCharge";
            this.tbxMinCharge.Size = new System.Drawing.Size(154, 20);
            this.tbxMinCharge.TabIndex = 19;
            this.tbxMinCharge.Leave += new System.EventHandler(this.TbxMinChargeOnLeave);
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(3, 275);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(64, 13);
            this.label9.TabIndex = 20;
            this.label9.Text = "Max Charge";
            // 
            // tbxMaxCharge
            // 
            this.tbxMaxCharge.Location = new System.Drawing.Point(153, 278);
            this.tbxMaxCharge.Name = "tbxMaxCharge";
            this.tbxMaxCharge.Size = new System.Drawing.Size(154, 20);
            this.tbxMaxCharge.TabIndex = 21;
            this.tbxMaxCharge.Leave += new System.EventHandler(this.TbxMaxChargeOnLeave);
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(3, 320);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(85, 13);
            this.label10.TabIndex = 24;
            this.label10.Text = "Peptide Intensity";
            // 
            // tbxPeptideIntensity
            // 
            this.tbxPeptideIntensity.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxPeptideIntensity.Location = new System.Drawing.Point(153, 323);
            this.tbxPeptideIntensity.Name = "tbxPeptideIntensity";
            this.tbxPeptideIntensity.ReadOnly = true;
            this.tbxPeptideIntensity.Size = new System.Drawing.Size(154, 20);
            this.tbxPeptideIntensity.TabIndex = 25;
            // 
            // cbxShowCentroids
            // 
            this.cbxShowCentroids.AutoSize = true;
            this.cbxShowCentroids.Checked = true;
            this.cbxShowCentroids.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbxShowCentroids.Location = new System.Drawing.Point(3, 348);
            this.cbxShowCentroids.Name = "cbxShowCentroids";
            this.cbxShowCentroids.Size = new System.Drawing.Size(100, 17);
            this.cbxShowCentroids.TabIndex = 26;
            this.cbxShowCentroids.Text = "Show Centroids";
            this.cbxShowCentroids.UseVisualStyleBackColor = true;
            this.cbxShowCentroids.CheckedChanged += new System.EventHandler(this.CbxShowCentroidsOnCheckedChanged);
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(3, 300);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(80, 13);
            this.label11.TabIndex = 22;
            this.label11.Text = "Mass Accuracy";
            // 
            // tbxMassAccuracy
            // 
            this.tbxMassAccuracy.Location = new System.Drawing.Point(153, 303);
            this.tbxMassAccuracy.Name = "tbxMassAccuracy";
            this.tbxMassAccuracy.Size = new System.Drawing.Size(154, 20);
            this.tbxMassAccuracy.TabIndex = 23;
            this.tbxMassAccuracy.Leave += new System.EventHandler(this.TbxMassAccuracyOnLeave);
            // 
            // cbxShowProfile
            // 
            this.cbxShowProfile.AutoSize = true;
            this.cbxShowProfile.Checked = true;
            this.cbxShowProfile.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbxShowProfile.Location = new System.Drawing.Point(3, 373);
            this.cbxShowProfile.Name = "cbxShowProfile";
            this.cbxShowProfile.Size = new System.Drawing.Size(85, 17);
            this.cbxShowProfile.TabIndex = 28;
            this.cbxShowProfile.Text = "Show Profile";
            this.cbxShowProfile.UseVisualStyleBackColor = true;
            this.cbxShowProfile.CheckedChanged += new System.EventHandler(this.CbxShowProfileOnCheckedChanged);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(3, 150);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(117, 13);
            this.label5.TabIndex = 12;
            this.label5.Text = "Chromatogram Intensity";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(3, 100);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(130, 13);
            this.label12.TabIndex = 8;
            this.label12.Text = "Sum of centroid intensities";
            // 
            // tbxChromatogramIonCurrent
            // 
            this.tbxChromatogramIonCurrent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxChromatogramIonCurrent.Location = new System.Drawing.Point(153, 153);
            this.tbxChromatogramIonCurrent.Name = "tbxChromatogramIonCurrent";
            this.tbxChromatogramIonCurrent.ReadOnly = true;
            this.tbxChromatogramIonCurrent.Size = new System.Drawing.Size(154, 20);
            this.tbxChromatogramIonCurrent.TabIndex = 13;
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(3, 125);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(75, 13);
            this.label13.TabIndex = 10;
            this.label13.Text = "Chromatogram";
            // 
            // comboChromatogram
            // 
            this.comboChromatogram.Dock = System.Windows.Forms.DockStyle.Fill;
            this.comboChromatogram.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboChromatogram.FormattingEnabled = true;
            this.comboChromatogram.Location = new System.Drawing.Point(153, 128);
            this.comboChromatogram.Name = "comboChromatogram";
            this.comboChromatogram.Size = new System.Drawing.Size(154, 21);
            this.comboChromatogram.TabIndex = 11;
            this.comboChromatogram.DropDown += new System.EventHandler(this.ComboChromatogramOnDropDown);
            this.comboChromatogram.SelectedIndexChanged += new System.EventHandler(this.ComboChromatogramOnSelectedIndexChanged);
            // 
            // tbxCentroidIntensitySum
            // 
            this.tbxCentroidIntensitySum.BackColor = System.Drawing.SystemColors.Control;
            this.tbxCentroidIntensitySum.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxCentroidIntensitySum.Location = new System.Drawing.Point(153, 103);
            this.tbxCentroidIntensitySum.Name = "tbxCentroidIntensitySum";
            this.tbxCentroidIntensitySum.ReadOnly = true;
            this.tbxCentroidIntensitySum.Size = new System.Drawing.Size(154, 20);
            this.tbxCentroidIntensitySum.TabIndex = 9;
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(3, 175);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(101, 13);
            this.label14.TabIndex = 14;
            this.label14.Text = "Chromatogram Time";
            // 
            // tbxChromatogramRetentionTime
            // 
            this.tbxChromatogramRetentionTime.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxChromatogramRetentionTime.Location = new System.Drawing.Point(153, 178);
            this.tbxChromatogramRetentionTime.Name = "tbxChromatogramRetentionTime";
            this.tbxChromatogramRetentionTime.ReadOnly = true;
            this.tbxChromatogramRetentionTime.Size = new System.Drawing.Size(154, 20);
            this.tbxChromatogramRetentionTime.TabIndex = 15;
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Controls.Add(this.btnPrevPrevScan);
            this.flowLayoutPanel1.Controls.Add(this.btnPrevScan);
            this.flowLayoutPanel1.Controls.Add(this.label1);
            this.flowLayoutPanel1.Controls.Add(this.tbxScanIndex);
            this.flowLayoutPanel1.Controls.Add(this.btnNextScan);
            this.flowLayoutPanel1.Controls.Add(this.btnNextNextScan);
            this.flowLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.flowLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(931, 29);
            this.flowLayoutPanel1.TabIndex = 3;
            // 
            // btnPrevPrevScan
            // 
            this.btnPrevPrevScan.AutoSize = true;
            this.btnPrevPrevScan.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnPrevPrevScan.Location = new System.Drawing.Point(3, 3);
            this.btnPrevPrevScan.Name = "btnPrevPrevScan";
            this.btnPrevPrevScan.Size = new System.Drawing.Size(29, 23);
            this.btnPrevPrevScan.TabIndex = 0;
            this.btnPrevPrevScan.Text = "<<";
            this.toolTip1.SetToolTip(this.btnPrevPrevScan, "Previous MS1");
            this.btnPrevPrevScan.UseVisualStyleBackColor = true;
            this.btnPrevPrevScan.Click += new System.EventHandler(this.BtnPrevPrevScanOnClick);
            // 
            // btnPrevScan
            // 
            this.btnPrevScan.AutoSize = true;
            this.btnPrevScan.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnPrevScan.Location = new System.Drawing.Point(38, 3);
            this.btnPrevScan.Name = "btnPrevScan";
            this.btnPrevScan.Size = new System.Drawing.Size(23, 23);
            this.btnPrevScan.TabIndex = 1;
            this.btnPrevScan.Text = "<";
            this.toolTip1.SetToolTip(this.btnPrevScan, "Previous Scan");
            this.btnPrevScan.UseVisualStyleBackColor = true;
            this.btnPrevScan.Click += new System.EventHandler(this.BtnPrevScanOnClick);
            // 
            // btnNextScan
            // 
            this.btnNextScan.AutoSize = true;
            this.btnNextScan.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnNextScan.Location = new System.Drawing.Point(214, 3);
            this.btnNextScan.Name = "btnNextScan";
            this.btnNextScan.Size = new System.Drawing.Size(23, 23);
            this.btnNextScan.TabIndex = 4;
            this.btnNextScan.Text = ">";
            this.toolTip1.SetToolTip(this.btnNextScan, "Next Scan");
            this.btnNextScan.UseVisualStyleBackColor = true;
            this.btnNextScan.Click += new System.EventHandler(this.BtnNextScanOnClick);
            // 
            // btnNextNextScan
            // 
            this.btnNextNextScan.AutoSize = true;
            this.btnNextNextScan.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnNextNextScan.Location = new System.Drawing.Point(243, 3);
            this.btnNextNextScan.Name = "btnNextNextScan";
            this.btnNextNextScan.Size = new System.Drawing.Size(29, 23);
            this.btnNextNextScan.TabIndex = 5;
            this.btnNextNextScan.Text = ">>";
            this.toolTip1.SetToolTip(this.btnNextNextScan, "Next MS1 Scan");
            this.btnNextNextScan.UseVisualStyleBackColor = true;
            this.btnNextNextScan.Click += new System.EventHandler(this.BtnNextNextScanOnClick);
            // 
            // SpectrumForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(931, 504);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.flowLayoutPanel1);
            this.Name = "SpectrumForm";
            this.TabText = "SpectrumForm";
            this.Text = "SpectrumForm";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.flowLayoutPanel1.ResumeLayout(false);
            this.flowLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbxScanIndex;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox tbxMsLevel;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox tbxTime;
        private System.Windows.Forms.CheckBox cbxShowPeptideMzs;
        private pwiz.Topograph.ui.Controls.MSGraphControlEx msGraphControlEx1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.Button btnPrevScan;
        private System.Windows.Forms.Button btnPrevPrevScan;
        private System.Windows.Forms.Button btnNextScan;
        private System.Windows.Forms.Button btnNextNextScan;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox tbxSumOfProfileIntensities;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox tbxChromatogramIonCurrent;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox tbxScanDuration;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox tbxPeptideSequence;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox tbxMinCharge;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox tbxMaxCharge;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.TextBox tbxPeptideIntensity;
        private System.Windows.Forms.CheckBox cbxShowCentroids;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.TextBox tbxMassAccuracy;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.TextBox tbxCentroidIntensitySum;
        private System.Windows.Forms.CheckBox cbxShowProfile;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.ComboBox comboChromatogram;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.TextBox tbxChromatogramRetentionTime;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}