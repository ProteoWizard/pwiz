namespace pwiz.Topograph.ui.Forms
{
    partial class RelexForm
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
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.barGraphControl = new pwiz.Topograph.ui.Controls.MSGraphControlEx();
            this.label1 = new System.Windows.Forms.Label();
            this.comboPrimary = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.comboSecondary = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.tbxCorrelation = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.tbxSlope = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.tbxSlopeError = new System.Windows.Forms.TextBox();
            this.tbxIntercept = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.tbxStart = new System.Windows.Forms.TextBox();
            this.btnStartLeft = new System.Windows.Forms.Button();
            this.btnStartRight = new System.Windows.Forms.Button();
            this.panel2 = new System.Windows.Forms.Panel();
            this.tbxEnd = new System.Windows.Forms.TextBox();
            this.btnEndLeft = new System.Windows.Forms.Button();
            this.btnEndRight = new System.Windows.Forms.Button();
            this.panel3 = new System.Windows.Forms.Panel();
            this.tbxOtherStart = new System.Windows.Forms.TextBox();
            this.btnOtherStartLeft = new System.Windows.Forms.Button();
            this.btnOtherStartRight = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.panel4 = new System.Windows.Forms.Panel();
            this.tbxOtherEnd = new System.Windows.Forms.TextBox();
            this.btnOtherEndLeft = new System.Windows.Forms.Button();
            this.btnOtherEndRight = new System.Windows.Forms.Button();
            this.label11 = new System.Windows.Forms.Label();
            this.tbxWidthRatio = new System.Windows.Forms.TextBox();
            this.msGraphControl = new pwiz.Topograph.ui.Controls.MSGraphControlEx();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.panel1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.panel3.SuspendLayout();
            this.panel4.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.tableLayoutPanel1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.msGraphControl);
            this.splitContainer1.Size = new System.Drawing.Size(875, 385);
            this.splitContainer1.SplitterDistance = 496;
            this.splitContainer1.TabIndex = 0;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 160F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.barGraphControl, 0, 11);
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.comboPrimary, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.label2, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.comboSecondary, 1, 3);
            this.tableLayoutPanel1.Controls.Add(this.label4, 0, 6);
            this.tableLayoutPanel1.Controls.Add(this.tbxCorrelation, 1, 6);
            this.tableLayoutPanel1.Controls.Add(this.label5, 0, 7);
            this.tableLayoutPanel1.Controls.Add(this.tbxSlope, 1, 7);
            this.tableLayoutPanel1.Controls.Add(this.label6, 0, 8);
            this.tableLayoutPanel1.Controls.Add(this.label7, 0, 9);
            this.tableLayoutPanel1.Controls.Add(this.tbxSlopeError, 1, 8);
            this.tableLayoutPanel1.Controls.Add(this.tbxIntercept, 1, 9);
            this.tableLayoutPanel1.Controls.Add(this.label8, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.label9, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.label10, 0, 4);
            this.tableLayoutPanel1.Controls.Add(this.panel1, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.panel2, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this.panel3, 1, 4);
            this.tableLayoutPanel1.Controls.Add(this.label3, 0, 5);
            this.tableLayoutPanel1.Controls.Add(this.panel4, 1, 5);
            this.tableLayoutPanel1.Controls.Add(this.label11, 0, 10);
            this.tableLayoutPanel1.Controls.Add(this.tbxWidthRatio, 1, 10);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 12;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(496, 385);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // barGraphControl
            // 
            this.tableLayoutPanel1.SetColumnSpan(this.barGraphControl, 2);
            this.barGraphControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.barGraphControl.EditButtons = System.Windows.Forms.MouseButtons.Left;
            this.barGraphControl.EditModifierKeys = System.Windows.Forms.Keys.None;
            this.barGraphControl.IsEnableVPan = false;
            this.barGraphControl.IsEnableVZoom = false;
            this.barGraphControl.Location = new System.Drawing.Point(3, 278);
            this.barGraphControl.Name = "barGraphControl";
            this.barGraphControl.ScrollGrace = 0;
            this.barGraphControl.ScrollMaxX = 0;
            this.barGraphControl.ScrollMaxY = 0;
            this.barGraphControl.ScrollMaxY2 = 0;
            this.barGraphControl.ScrollMinX = 0;
            this.barGraphControl.ScrollMinY = 0;
            this.barGraphControl.ScrollMinY2 = 0;
            this.barGraphControl.Size = new System.Drawing.Size(490, 104);
            this.barGraphControl.TabIndex = 29;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(3, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(102, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Base Chromatogram";
            // 
            // comboPrimary
            // 
            this.comboPrimary.Dock = System.Windows.Forms.DockStyle.Fill;
            this.comboPrimary.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboPrimary.FormattingEnabled = true;
            this.comboPrimary.Location = new System.Drawing.Point(163, 3);
            this.comboPrimary.Name = "comboPrimary";
            this.comboPrimary.Size = new System.Drawing.Size(330, 21);
            this.comboPrimary.TabIndex = 1;
            this.comboPrimary.SelectedIndexChanged += new System.EventHandler(this.comboPrimary_SelectedIndexChanged);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(3, 75);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(103, 13);
            this.label2.TabIndex = 4;
            this.label2.Text = "Other chromatogram";
            // 
            // comboSecondary
            // 
            this.comboSecondary.Dock = System.Windows.Forms.DockStyle.Fill;
            this.comboSecondary.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboSecondary.FormattingEnabled = true;
            this.comboSecondary.Location = new System.Drawing.Point(163, 78);
            this.comboSecondary.Name = "comboSecondary";
            this.comboSecondary.Size = new System.Drawing.Size(330, 21);
            this.comboSecondary.TabIndex = 5;
            this.comboSecondary.SelectedIndexChanged += new System.EventHandler(this.comboSecondary_SelectedIndexChanged);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(3, 150);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(57, 13);
            this.label4.TabIndex = 8;
            this.label4.Text = "Correlation";
            // 
            // tbxCorrelation
            // 
            this.tbxCorrelation.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxCorrelation.Location = new System.Drawing.Point(163, 153);
            this.tbxCorrelation.Name = "tbxCorrelation";
            this.tbxCorrelation.ReadOnly = true;
            this.tbxCorrelation.Size = new System.Drawing.Size(330, 20);
            this.tbxCorrelation.TabIndex = 9;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(3, 175);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(34, 13);
            this.label5.TabIndex = 10;
            this.label5.Text = "Slope";
            // 
            // tbxSlope
            // 
            this.tbxSlope.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxSlope.Location = new System.Drawing.Point(163, 178);
            this.tbxSlope.Name = "tbxSlope";
            this.tbxSlope.ReadOnly = true;
            this.tbxSlope.Size = new System.Drawing.Size(330, 20);
            this.tbxSlope.TabIndex = 11;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(3, 200);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(59, 13);
            this.label6.TabIndex = 12;
            this.label6.Text = "Slope Error";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(3, 225);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(49, 13);
            this.label7.TabIndex = 13;
            this.label7.Text = "Intercept";
            // 
            // tbxSlopeError
            // 
            this.tbxSlopeError.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxSlopeError.Location = new System.Drawing.Point(163, 203);
            this.tbxSlopeError.Name = "tbxSlopeError";
            this.tbxSlopeError.ReadOnly = true;
            this.tbxSlopeError.Size = new System.Drawing.Size(330, 20);
            this.tbxSlopeError.TabIndex = 14;
            // 
            // tbxIntercept
            // 
            this.tbxIntercept.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxIntercept.Location = new System.Drawing.Point(163, 228);
            this.tbxIntercept.Name = "tbxIntercept";
            this.tbxIntercept.ReadOnly = true;
            this.tbxIntercept.Size = new System.Drawing.Size(330, 20);
            this.tbxIntercept.TabIndex = 15;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(3, 25);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(29, 13);
            this.label8.TabIndex = 16;
            this.label8.Text = "Start";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(3, 50);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(26, 13);
            this.label9.TabIndex = 17;
            this.label9.Text = "End";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(3, 100);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(29, 13);
            this.label10.TabIndex = 20;
            this.label10.Text = "Start";
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.tbxStart);
            this.panel1.Controls.Add(this.btnStartLeft);
            this.panel1.Controls.Add(this.btnStartRight);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(162, 27);
            this.panel1.Margin = new System.Windows.Forms.Padding(2);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(332, 21);
            this.panel1.TabIndex = 22;
            // 
            // tbxStart
            // 
            this.tbxStart.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxStart.Location = new System.Drawing.Point(0, 0);
            this.tbxStart.Name = "tbxStart";
            this.tbxStart.Size = new System.Drawing.Size(286, 20);
            this.tbxStart.TabIndex = 4;
            this.tbxStart.Leave += new System.EventHandler(this.tbxStart_Leave);
            // 
            // btnStartLeft
            // 
            this.btnStartLeft.AutoSize = true;
            this.btnStartLeft.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnStartLeft.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnStartLeft.Location = new System.Drawing.Point(286, 0);
            this.btnStartLeft.Margin = new System.Windows.Forms.Padding(0);
            this.btnStartLeft.Name = "btnStartLeft";
            this.btnStartLeft.Size = new System.Drawing.Size(23, 21);
            this.btnStartLeft.TabIndex = 3;
            this.btnStartLeft.Text = "<";
            this.btnStartLeft.UseVisualStyleBackColor = true;
            this.btnStartLeft.Click += new System.EventHandler(this.btnStartLeft_Click);
            // 
            // btnStartRight
            // 
            this.btnStartRight.AutoSize = true;
            this.btnStartRight.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnStartRight.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnStartRight.Location = new System.Drawing.Point(309, 0);
            this.btnStartRight.Name = "btnStartRight";
            this.btnStartRight.Size = new System.Drawing.Size(23, 21);
            this.btnStartRight.TabIndex = 5;
            this.btnStartRight.Text = ">";
            this.btnStartRight.UseVisualStyleBackColor = true;
            this.btnStartRight.Click += new System.EventHandler(this.btnStartRight_Click);
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.tbxEnd);
            this.panel2.Controls.Add(this.btnEndLeft);
            this.panel2.Controls.Add(this.btnEndRight);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel2.Location = new System.Drawing.Point(162, 52);
            this.panel2.Margin = new System.Windows.Forms.Padding(2);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(332, 21);
            this.panel2.TabIndex = 23;
            // 
            // tbxEnd
            // 
            this.tbxEnd.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxEnd.Location = new System.Drawing.Point(0, 0);
            this.tbxEnd.Name = "tbxEnd";
            this.tbxEnd.Size = new System.Drawing.Size(286, 20);
            this.tbxEnd.TabIndex = 2;
            // 
            // btnEndLeft
            // 
            this.btnEndLeft.AutoSize = true;
            this.btnEndLeft.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnEndLeft.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnEndLeft.Location = new System.Drawing.Point(286, 0);
            this.btnEndLeft.Name = "btnEndLeft";
            this.btnEndLeft.Size = new System.Drawing.Size(23, 21);
            this.btnEndLeft.TabIndex = 0;
            this.btnEndLeft.Text = "<";
            this.btnEndLeft.UseVisualStyleBackColor = true;
            this.btnEndLeft.Click += new System.EventHandler(this.btnEndLeft_Click);
            // 
            // btnEndRight
            // 
            this.btnEndRight.AutoSize = true;
            this.btnEndRight.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnEndRight.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnEndRight.Location = new System.Drawing.Point(309, 0);
            this.btnEndRight.Name = "btnEndRight";
            this.btnEndRight.Size = new System.Drawing.Size(23, 21);
            this.btnEndRight.TabIndex = 1;
            this.btnEndRight.Text = ">";
            this.btnEndRight.UseVisualStyleBackColor = true;
            this.btnEndRight.Click += new System.EventHandler(this.btnEndRight_Click);
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.tbxOtherStart);
            this.panel3.Controls.Add(this.btnOtherStartLeft);
            this.panel3.Controls.Add(this.btnOtherStartRight);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel3.Location = new System.Drawing.Point(162, 102);
            this.panel3.Margin = new System.Windows.Forms.Padding(2);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(332, 21);
            this.panel3.TabIndex = 24;
            // 
            // tbxOtherStart
            // 
            this.tbxOtherStart.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxOtherStart.Location = new System.Drawing.Point(0, 0);
            this.tbxOtherStart.Name = "tbxOtherStart";
            this.tbxOtherStart.Size = new System.Drawing.Size(286, 20);
            this.tbxOtherStart.TabIndex = 2;
            // 
            // btnOtherStartLeft
            // 
            this.btnOtherStartLeft.AutoSize = true;
            this.btnOtherStartLeft.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnOtherStartLeft.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnOtherStartLeft.Location = new System.Drawing.Point(286, 0);
            this.btnOtherStartLeft.Name = "btnOtherStartLeft";
            this.btnOtherStartLeft.Size = new System.Drawing.Size(23, 21);
            this.btnOtherStartLeft.TabIndex = 0;
            this.btnOtherStartLeft.Text = "<";
            this.btnOtherStartLeft.UseVisualStyleBackColor = true;
            this.btnOtherStartLeft.Click += new System.EventHandler(this.btnOtherStartLeft_Click);
            // 
            // btnOtherStartRight
            // 
            this.btnOtherStartRight.AutoSize = true;
            this.btnOtherStartRight.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnOtherStartRight.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnOtherStartRight.Location = new System.Drawing.Point(309, 0);
            this.btnOtherStartRight.Name = "btnOtherStartRight";
            this.btnOtherStartRight.Size = new System.Drawing.Size(23, 21);
            this.btnOtherStartRight.TabIndex = 1;
            this.btnOtherStartRight.Text = ">";
            this.btnOtherStartRight.UseVisualStyleBackColor = true;
            this.btnOtherStartRight.Click += new System.EventHandler(this.btnOtherStartRight_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(3, 125);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(26, 13);
            this.label3.TabIndex = 25;
            this.label3.Text = "End";
            // 
            // panel4
            // 
            this.panel4.Controls.Add(this.tbxOtherEnd);
            this.panel4.Controls.Add(this.btnOtherEndLeft);
            this.panel4.Controls.Add(this.btnOtherEndRight);
            this.panel4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel4.Location = new System.Drawing.Point(163, 128);
            this.panel4.Name = "panel4";
            this.panel4.Size = new System.Drawing.Size(330, 19);
            this.panel4.TabIndex = 26;
            // 
            // tbxOtherEnd
            // 
            this.tbxOtherEnd.AcceptsReturn = true;
            this.tbxOtherEnd.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxOtherEnd.Location = new System.Drawing.Point(0, 0);
            this.tbxOtherEnd.Name = "tbxOtherEnd";
            this.tbxOtherEnd.Size = new System.Drawing.Size(284, 20);
            this.tbxOtherEnd.TabIndex = 0;
            // 
            // btnOtherEndLeft
            // 
            this.btnOtherEndLeft.AutoSize = true;
            this.btnOtherEndLeft.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnOtherEndLeft.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnOtherEndLeft.Location = new System.Drawing.Point(284, 0);
            this.btnOtherEndLeft.Name = "btnOtherEndLeft";
            this.btnOtherEndLeft.Size = new System.Drawing.Size(23, 19);
            this.btnOtherEndLeft.TabIndex = 2;
            this.btnOtherEndLeft.Text = "<";
            this.btnOtherEndLeft.UseVisualStyleBackColor = true;
            this.btnOtherEndLeft.Click += new System.EventHandler(this.btnOtherEndLeft_Click);
            // 
            // btnOtherEndRight
            // 
            this.btnOtherEndRight.AutoSize = true;
            this.btnOtherEndRight.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnOtherEndRight.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnOtherEndRight.Location = new System.Drawing.Point(307, 0);
            this.btnOtherEndRight.Name = "btnOtherEndRight";
            this.btnOtherEndRight.Size = new System.Drawing.Size(23, 19);
            this.btnOtherEndRight.TabIndex = 3;
            this.btnOtherEndRight.Text = ">";
            this.btnOtherEndRight.UseVisualStyleBackColor = true;
            this.btnOtherEndRight.Click += new System.EventHandler(this.btnOtherEndRight_Click);
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(3, 250);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(63, 13);
            this.label11.TabIndex = 27;
            this.label11.Text = "Width Ratio";
            // 
            // tbxWidthRatio
            // 
            this.tbxWidthRatio.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxWidthRatio.Location = new System.Drawing.Point(163, 253);
            this.tbxWidthRatio.Name = "tbxWidthRatio";
            this.tbxWidthRatio.ReadOnly = true;
            this.tbxWidthRatio.Size = new System.Drawing.Size(330, 20);
            this.tbxWidthRatio.TabIndex = 28;
            // 
            // msGraphControl
            // 
            this.msGraphControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.msGraphControl.EditButtons = System.Windows.Forms.MouseButtons.Left;
            this.msGraphControl.EditModifierKeys = System.Windows.Forms.Keys.None;
            this.msGraphControl.IsEnableVPan = false;
            this.msGraphControl.IsEnableVZoom = false;
            this.msGraphControl.Location = new System.Drawing.Point(0, 0);
            this.msGraphControl.Name = "msGraphControl";
            this.msGraphControl.ScrollGrace = 0;
            this.msGraphControl.ScrollMaxX = 0;
            this.msGraphControl.ScrollMaxY = 0;
            this.msGraphControl.ScrollMaxY2 = 0;
            this.msGraphControl.ScrollMinX = 0;
            this.msGraphControl.ScrollMinY = 0;
            this.msGraphControl.ScrollMinY2 = 0;
            this.msGraphControl.Size = new System.Drawing.Size(375, 385);
            this.msGraphControl.TabIndex = 0;
            // 
            // RelexForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(875, 385);
            this.Controls.Add(this.splitContainer1);
            this.Name = "RelexForm";
            this.TabText = "RelexForm";
            this.Text = "RelexForm";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.panel3.ResumeLayout(false);
            this.panel3.PerformLayout();
            this.panel4.ResumeLayout(false);
            this.panel4.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private pwiz.MSGraph.MSGraphControl msGraphControl;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox comboPrimary;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox comboSecondary;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox tbxCorrelation;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox tbxSlope;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox tbxSlopeError;
        private System.Windows.Forms.TextBox tbxIntercept;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button btnStartLeft;
        private System.Windows.Forms.TextBox tbxStart;
        private System.Windows.Forms.Button btnStartRight;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.TextBox tbxEnd;
        private System.Windows.Forms.Button btnEndLeft;
        private System.Windows.Forms.Button btnEndRight;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.Button btnOtherStartLeft;
        private System.Windows.Forms.TextBox tbxOtherStart;
        private System.Windows.Forms.Button btnOtherStartRight;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Panel panel4;
        private System.Windows.Forms.Button btnOtherEndLeft;
        private System.Windows.Forms.Button btnOtherEndRight;
        private System.Windows.Forms.TextBox tbxOtherEnd;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.TextBox tbxWidthRatio;
        private pwiz.MSGraph.MSGraphControl barGraphControl;
    }
}