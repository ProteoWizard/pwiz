namespace pwiz.Skyline.EditUI
{
    partial class RefineDlg
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
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabDocument = new System.Windows.Forms.TabPage();
            this.cbAutoTransitions = new System.Windows.Forms.CheckBox();
            this.cbAutoPrecursors = new System.Windows.Forms.CheckBox();
            this.cbAutoPeptides = new System.Windows.Forms.CheckBox();
            this.label7 = new System.Windows.Forms.Label();
            this.cbAdd = new System.Windows.Forms.CheckBox();
            this.labelLabelType = new System.Windows.Forms.Label();
            this.comboRefineLabelType = new System.Windows.Forms.ComboBox();
            this.cbRemoveRepeatedPeptides = new System.Windows.Forms.CheckBox();
            this.cbRemoveDuplicatePeptides = new System.Windows.Forms.CheckBox();
            this.textMinTransitions = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.textMinPeptides = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.tabResults = new System.Windows.Forms.TabPage();
            this.label4 = new System.Windows.Forms.Label();
            this.comboReplicateUse = new System.Windows.Forms.ComboBox();
            this.cbPreferLarger = new System.Windows.Forms.CheckBox();
            this.textMaxPeakRank = new System.Windows.Forms.TextBox();
            this.labelMaxPeakRank = new System.Windows.Forms.Label();
            this.textMaxPeakFoundRatio = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.radioRemoveMissing = new System.Windows.Forms.RadioButton();
            this.radioIgnoreMissing = new System.Windows.Forms.RadioButton();
            this.textMinPeakFoundRatio = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.groupLibCorr = new System.Windows.Forms.GroupBox();
            this.textMinDotProduct = new System.Windows.Forms.TextBox();
            this.labelMinDotProduct = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.textRTRegressionThreshold = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.textMaxPepPeakRank = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.textMinIdotProduct = new System.Windows.Forms.TextBox();
            this.labelMinIdotProduct = new System.Windows.Forms.Label();
            this.tabControl1.SuspendLayout();
            this.tabDocument.SuspendLayout();
            this.tabResults.SuspendLayout();
            this.groupLibCorr.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl1.Controls.Add(this.tabDocument);
            this.tabControl1.Controls.Add(this.tabResults);
            this.tabControl1.Location = new System.Drawing.Point(13, 13);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(395, 503);
            this.tabControl1.TabIndex = 0;
            // 
            // tabDocument
            // 
            this.tabDocument.Controls.Add(this.cbAutoTransitions);
            this.tabDocument.Controls.Add(this.cbAutoPrecursors);
            this.tabDocument.Controls.Add(this.cbAutoPeptides);
            this.tabDocument.Controls.Add(this.label7);
            this.tabDocument.Controls.Add(this.cbAdd);
            this.tabDocument.Controls.Add(this.labelLabelType);
            this.tabDocument.Controls.Add(this.comboRefineLabelType);
            this.tabDocument.Controls.Add(this.cbRemoveRepeatedPeptides);
            this.tabDocument.Controls.Add(this.cbRemoveDuplicatePeptides);
            this.tabDocument.Controls.Add(this.textMinTransitions);
            this.tabDocument.Controls.Add(this.label2);
            this.tabDocument.Controls.Add(this.textMinPeptides);
            this.tabDocument.Controls.Add(this.label1);
            this.tabDocument.Location = new System.Drawing.Point(4, 22);
            this.tabDocument.Name = "tabDocument";
            this.tabDocument.Padding = new System.Windows.Forms.Padding(3);
            this.tabDocument.Size = new System.Drawing.Size(387, 452);
            this.tabDocument.TabIndex = 0;
            this.tabDocument.Text = "Document";
            this.tabDocument.UseVisualStyleBackColor = true;
            // 
            // cbAutoTransitions
            // 
            this.cbAutoTransitions.AutoSize = true;
            this.cbAutoTransitions.Location = new System.Drawing.Point(19, 379);
            this.cbAutoTransitions.Name = "cbAutoTransitions";
            this.cbAutoTransitions.Size = new System.Drawing.Size(77, 17);
            this.cbAutoTransitions.TabIndex = 9;
            this.cbAutoTransitions.Text = "Tra&nsitions";
            this.cbAutoTransitions.UseVisualStyleBackColor = true;
            // 
            // cbAutoPrecursors
            // 
            this.cbAutoPrecursors.AutoSize = true;
            this.cbAutoPrecursors.Location = new System.Drawing.Point(19, 356);
            this.cbAutoPrecursors.Name = "cbAutoPrecursors";
            this.cbAutoPrecursors.Size = new System.Drawing.Size(76, 17);
            this.cbAutoPrecursors.TabIndex = 8;
            this.cbAutoPrecursors.Text = "Pr&ecursors";
            this.cbAutoPrecursors.UseVisualStyleBackColor = true;
            // 
            // cbAutoPeptides
            // 
            this.cbAutoPeptides.AutoSize = true;
            this.cbAutoPeptides.Location = new System.Drawing.Point(19, 333);
            this.cbAutoPeptides.Name = "cbAutoPeptides";
            this.cbAutoPeptides.Size = new System.Drawing.Size(67, 17);
            this.cbAutoPeptides.TabIndex = 7;
            this.cbAutoPeptides.Text = "&Peptides";
            this.cbAutoPeptides.UseVisualStyleBackColor = true;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(16, 313);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(76, 13);
            this.label7.TabIndex = 6;
            this.label7.Text = "Auto-&select all:";
            // 
            // cbAdd
            // 
            this.cbAdd.AutoSize = true;
            this.cbAdd.Location = new System.Drawing.Point(155, 250);
            this.cbAdd.Name = "cbAdd";
            this.cbAdd.Size = new System.Drawing.Size(45, 17);
            this.cbAdd.TabIndex = 12;
            this.cbAdd.Text = "&Add";
            this.cbAdd.UseVisualStyleBackColor = true;
            this.cbAdd.CheckedChanged += new System.EventHandler(this.cbAdd_CheckedChanged);
            // 
            // labelLabelType
            // 
            this.labelLabelType.AutoSize = true;
            this.labelLabelType.Location = new System.Drawing.Point(16, 232);
            this.labelLabelType.Name = "labelLabelType";
            this.labelLabelType.Size = new System.Drawing.Size(98, 13);
            this.labelLabelType.TabIndex = 10;
            this.labelLabelType.Text = "Remove la&bel type:";
            // 
            // comboRefineLabelType
            // 
            this.comboRefineLabelType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRefineLabelType.FormattingEnabled = true;
            this.comboRefineLabelType.Location = new System.Drawing.Point(19, 248);
            this.comboRefineLabelType.Name = "comboRefineLabelType";
            this.comboRefineLabelType.Size = new System.Drawing.Size(121, 21);
            this.comboRefineLabelType.TabIndex = 11;
            // 
            // cbRemoveRepeatedPeptides
            // 
            this.cbRemoveRepeatedPeptides.AutoSize = true;
            this.cbRemoveRepeatedPeptides.Location = new System.Drawing.Point(19, 98);
            this.cbRemoveRepeatedPeptides.Name = "cbRemoveRepeatedPeptides";
            this.cbRemoveRepeatedPeptides.Size = new System.Drawing.Size(154, 17);
            this.cbRemoveRepeatedPeptides.TabIndex = 2;
            this.cbRemoveRepeatedPeptides.Text = "&Remove repeated peptides";
            this.helpTip.SetToolTip(this.cbRemoveRepeatedPeptides, "All repeated peptides will be removed to leave only the\r\nfirst occurrence of any " +
        "peptide.");
            this.cbRemoveRepeatedPeptides.UseVisualStyleBackColor = true;
            // 
            // cbRemoveDuplicatePeptides
            // 
            this.cbRemoveDuplicatePeptides.AutoSize = true;
            this.cbRemoveDuplicatePeptides.Location = new System.Drawing.Point(203, 98);
            this.cbRemoveDuplicatePeptides.Name = "cbRemoveDuplicatePeptides";
            this.cbRemoveDuplicatePeptides.Size = new System.Drawing.Size(155, 17);
            this.cbRemoveDuplicatePeptides.TabIndex = 3;
            this.cbRemoveDuplicatePeptides.Text = "Remove &duplicate peptides";
            this.helpTip.SetToolTip(this.cbRemoveDuplicatePeptides, "All peptides that are not unique within the document\r\nwill be removed.");
            this.cbRemoveDuplicatePeptides.UseVisualStyleBackColor = true;
            // 
            // textMinTransitions
            // 
            this.textMinTransitions.Location = new System.Drawing.Point(19, 176);
            this.textMinTransitions.Name = "textMinTransitions";
            this.textMinTransitions.Size = new System.Drawing.Size(65, 20);
            this.textMinTransitions.TabIndex = 5;
            this.helpTip.SetToolTip(this.textMinTransitions, "Precursors with fewer than this number of transitions will be\r\nremoved from the d" +
        "ocument.");
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(16, 159);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(142, 13);
            this.label2.TabIndex = 4;
            this.label2.Text = "Min &transitions per precursor:";
            // 
            // textMinPeptides
            // 
            this.textMinPeptides.Location = new System.Drawing.Point(19, 43);
            this.textMinPeptides.Name = "textMinPeptides";
            this.textMinPeptides.Size = new System.Drawing.Size(65, 20);
            this.textMinPeptides.TabIndex = 1;
            this.helpTip.SetToolTip(this.textMinPeptides, "Proteins with fewer than this number of peptides will be\r\nremoved from the docume" +
        "nt.");
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(16, 26);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(123, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "&Min peptides per protein:";
            // 
            // tabResults
            // 
            this.tabResults.Controls.Add(this.textMaxPepPeakRank);
            this.tabResults.Controls.Add(this.label8);
            this.tabResults.Controls.Add(this.label4);
            this.tabResults.Controls.Add(this.comboReplicateUse);
            this.tabResults.Controls.Add(this.cbPreferLarger);
            this.tabResults.Controls.Add(this.textMaxPeakRank);
            this.tabResults.Controls.Add(this.labelMaxPeakRank);
            this.tabResults.Controls.Add(this.textMaxPeakFoundRatio);
            this.tabResults.Controls.Add(this.label6);
            this.tabResults.Controls.Add(this.radioRemoveMissing);
            this.tabResults.Controls.Add(this.radioIgnoreMissing);
            this.tabResults.Controls.Add(this.textMinPeakFoundRatio);
            this.tabResults.Controls.Add(this.label5);
            this.tabResults.Controls.Add(this.groupLibCorr);
            this.tabResults.Controls.Add(this.groupBox1);
            this.tabResults.Location = new System.Drawing.Point(4, 22);
            this.tabResults.Name = "tabResults";
            this.tabResults.Padding = new System.Windows.Forms.Padding(3);
            this.tabResults.Size = new System.Drawing.Size(387, 477);
            this.tabResults.TabIndex = 1;
            this.tabResults.Text = "Results";
            this.tabResults.UseVisualStyleBackColor = true;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(25, 414);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(99, 13);
            this.label4.TabIndex = 13;
            this.label4.Text = "Replicate in&clusion:";
            // 
            // comboReplicateUse
            // 
            this.comboReplicateUse.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboReplicateUse.FormattingEnabled = true;
            this.comboReplicateUse.Items.AddRange(new object[] {
            "All",
            "Best"});
            this.comboReplicateUse.Location = new System.Drawing.Point(28, 430);
            this.comboReplicateUse.Name = "comboReplicateUse";
            this.comboReplicateUse.Size = new System.Drawing.Size(134, 21);
            this.comboReplicateUse.TabIndex = 14;
            this.helpTip.SetToolTip(this.comboReplicateUse, "Determines replicate results are used in refinement:\r\nAll - replicate values are " +
        "averaged, use for technical replicates\r\nBest - only the best replicate is used f" +
        "or each peptide, use for fractionation");
            // 
            // cbPreferLarger
            // 
            this.cbPreferLarger.AutoSize = true;
            this.cbPreferLarger.Enabled = false;
            this.cbPreferLarger.Location = new System.Drawing.Point(171, 123);
            this.cbPreferLarger.Name = "cbPreferLarger";
            this.cbPreferLarger.Size = new System.Drawing.Size(144, 17);
            this.cbPreferLarger.TabIndex = 8;
            this.cbPreferLarger.Text = "&Prefer larger product ions";
            this.helpTip.SetToolTip(this.cbPreferLarger, "Causes refinement to choose larger product ions\r\nwhen smaller, less selective ion" +
        "s yeild only fractionally\r\ngreater peak area.");
            this.cbPreferLarger.UseVisualStyleBackColor = true;
            // 
            // textMaxPeakRank
            // 
            this.textMaxPeakRank.Location = new System.Drawing.Point(171, 95);
            this.textMaxPeakRank.Name = "textMaxPeakRank";
            this.textMaxPeakRank.Size = new System.Drawing.Size(65, 20);
            this.textMaxPeakRank.TabIndex = 7;
            this.helpTip.SetToolTip(this.textMaxPeakRank, "All transitions with an average area peak ranking\r\ngreater than this number will " +
        "be removed from the\r\ndocument.");
            this.textMaxPeakRank.TextChanged += new System.EventHandler(this.textMaxPeakRank_TextChanged);
            // 
            // labelMaxPeakRank
            // 
            this.labelMaxPeakRank.AutoSize = true;
            this.labelMaxPeakRank.Location = new System.Drawing.Point(168, 79);
            this.labelMaxPeakRank.Name = "labelMaxPeakRank";
            this.labelMaxPeakRank.Size = new System.Drawing.Size(126, 13);
            this.labelMaxPeakRank.TabIndex = 6;
            this.labelMaxPeakRank.Text = "Max &transition peak rank:";
            // 
            // textMaxPeakFoundRatio
            // 
            this.textMaxPeakFoundRatio.Location = new System.Drawing.Point(171, 41);
            this.textMaxPeakFoundRatio.Name = "textMaxPeakFoundRatio";
            this.textMaxPeakFoundRatio.Size = new System.Drawing.Size(65, 20);
            this.textMaxPeakFoundRatio.TabIndex = 3;
            this.helpTip.SetToolTip(this.textMaxPeakFoundRatio, "All elements with peak found ratio above this number\r\nwill be removed from the do" +
        "cument:\r\n\r\nGreen = 1.0\r\nOrange >= 0.5\r\nRed < 0.5");
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(168, 25);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(110, 13);
            this.label6.TabIndex = 2;
            this.label6.Text = "Ma&x peak found ratio:";
            // 
            // radioRemoveMissing
            // 
            this.radioRemoveMissing.AutoSize = true;
            this.radioRemoveMissing.Location = new System.Drawing.Point(25, 178);
            this.radioRemoveMissing.Name = "radioRemoveMissing";
            this.radioRemoveMissing.Size = new System.Drawing.Size(167, 17);
            this.radioRemoveMissing.TabIndex = 10;
            this.radioRemoveMissing.TabStop = true;
            this.radioRemoveMissing.Text = "&Remove nodes missing results";
            this.helpTip.SetToolTip(this.radioRemoveMissing, "All elements without measured results will be\r\nremoved from the document.");
            this.radioRemoveMissing.UseVisualStyleBackColor = true;
            // 
            // radioIgnoreMissing
            // 
            this.radioIgnoreMissing.AutoSize = true;
            this.radioIgnoreMissing.Checked = true;
            this.radioIgnoreMissing.Location = new System.Drawing.Point(25, 155);
            this.radioIgnoreMissing.Name = "radioIgnoreMissing";
            this.radioIgnoreMissing.Size = new System.Drawing.Size(157, 17);
            this.radioIgnoreMissing.TabIndex = 9;
            this.radioIgnoreMissing.TabStop = true;
            this.radioIgnoreMissing.Text = "Ig&nore nodes missing results";
            this.helpTip.SetToolTip(this.radioIgnoreMissing, "No action will be taken for elements without\r\nmeasured results.");
            this.radioIgnoreMissing.UseVisualStyleBackColor = true;
            // 
            // textMinPeakFoundRatio
            // 
            this.textMinPeakFoundRatio.Location = new System.Drawing.Point(25, 41);
            this.textMinPeakFoundRatio.Name = "textMinPeakFoundRatio";
            this.textMinPeakFoundRatio.Size = new System.Drawing.Size(65, 20);
            this.textMinPeakFoundRatio.TabIndex = 1;
            this.helpTip.SetToolTip(this.textMinPeakFoundRatio, "All elements with peak found ratio below this number\r\nwill be removed from the do" +
        "cument:\r\n\r\nGreen = 1.0\r\nOrange >= 0.5\r\nRed < 0.5");
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(22, 25);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(107, 13);
            this.label5.TabIndex = 0;
            this.label5.Text = "&Min peak found ratio:";
            // 
            // groupLibCorr
            // 
            this.groupLibCorr.Controls.Add(this.textMinIdotProduct);
            this.groupLibCorr.Controls.Add(this.labelMinIdotProduct);
            this.groupLibCorr.Controls.Add(this.textMinDotProduct);
            this.groupLibCorr.Controls.Add(this.labelMinDotProduct);
            this.groupLibCorr.Enabled = false;
            this.groupLibCorr.Location = new System.Drawing.Point(25, 314);
            this.groupLibCorr.Name = "groupLibCorr";
            this.groupLibCorr.Size = new System.Drawing.Size(332, 80);
            this.groupLibCorr.TabIndex = 12;
            this.groupLibCorr.TabStop = false;
            this.groupLibCorr.Text = "&Expected relative intensity correlation:";
            // 
            // textMinDotProduct
            // 
            this.textMinDotProduct.Enabled = false;
            this.textMinDotProduct.Location = new System.Drawing.Point(33, 41);
            this.textMinDotProduct.Name = "textMinDotProduct";
            this.textMinDotProduct.Size = new System.Drawing.Size(65, 20);
            this.textMinDotProduct.TabIndex = 1;
            this.helpTip.SetToolTip(this.textMinDotProduct, "All precursors with a product ion peak area to library\r\nspectrum dot-product belo" +
        "w this threshold will be removed\r\nfrom the document.");
            // 
            // labelMinDotProduct
            // 
            this.labelMinDotProduct.AutoSize = true;
            this.labelMinDotProduct.Enabled = false;
            this.labelMinDotProduct.Location = new System.Drawing.Point(30, 25);
            this.labelMinDotProduct.Name = "labelMinDotProduct";
            this.labelMinDotProduct.Size = new System.Drawing.Size(51, 13);
            this.labelMinDotProduct.TabIndex = 0;
            this.labelMinDotProduct.Text = "Min dotp:";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.textRTRegressionThreshold);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Location = new System.Drawing.Point(25, 216);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(332, 80);
            this.groupBox1.TabIndex = 11;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Retention time &outliers:";
            // 
            // textRTRegressionThreshold
            // 
            this.textRTRegressionThreshold.Location = new System.Drawing.Point(33, 42);
            this.textRTRegressionThreshold.Name = "textRTRegressionThreshold";
            this.textRTRegressionThreshold.Size = new System.Drawing.Size(65, 20);
            this.textRTRegressionThreshold.TabIndex = 1;
            this.helpTip.SetToolTip(this.textRTRegressionThreshold, "Precursors will be removed from the document\r\nuntil the target value for the risi" +
        "duals of a linear\r\nregression with the optimal retention time calculator\r\nexceed" +
        " this threshold.");
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(30, 25);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(170, 13);
            this.label3.TabIndex = 0;
            this.label3.Text = "Target r value for linear regression:";
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(252, 522);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 1;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(333, 522);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // textMaxPepPeakRank
            // 
            this.textMaxPepPeakRank.Location = new System.Drawing.Point(25, 95);
            this.textMaxPepPeakRank.Name = "textMaxPepPeakRank";
            this.textMaxPepPeakRank.Size = new System.Drawing.Size(65, 20);
            this.textMaxPepPeakRank.TabIndex = 5;
            this.helpTip.SetToolTip(this.textMaxPepPeakRank, "All transitions with an average area peak ranking\r\ngreater than this number will " +
        "be removed from the\r\ndocument.");
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(22, 79);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(119, 13);
            this.label8.TabIndex = 4;
            this.label8.Text = "Max peptide pea&k rank:";
            // 
            // textMinIdotProduct
            // 
            this.textMinIdotProduct.Enabled = false;
            this.textMinIdotProduct.Location = new System.Drawing.Point(146, 41);
            this.textMinIdotProduct.Name = "textMinIdotProduct";
            this.textMinIdotProduct.Size = new System.Drawing.Size(65, 20);
            this.textMinIdotProduct.TabIndex = 3;
            this.helpTip.SetToolTip(this.textMinIdotProduct, "All precursors with a MS1 precursor peak area to expected\r\nisotope distribution d" +
        "ot-product below this threshold will be\r\nremoved from the document.");
            // 
            // labelMinIdotProduct
            // 
            this.labelMinIdotProduct.AutoSize = true;
            this.labelMinIdotProduct.Enabled = false;
            this.labelMinIdotProduct.Location = new System.Drawing.Point(143, 25);
            this.labelMinIdotProduct.Name = "labelMinIdotProduct";
            this.labelMinIdotProduct.Size = new System.Drawing.Size(53, 13);
            this.labelMinIdotProduct.TabIndex = 2;
            this.labelMinIdotProduct.Text = "Min idotp:";
            // 
            // RefineDlg
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(420, 557);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.tabControl1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "RefineDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Refine";
            this.tabControl1.ResumeLayout(false);
            this.tabDocument.ResumeLayout(false);
            this.tabDocument.PerformLayout();
            this.tabResults.ResumeLayout(false);
            this.tabResults.PerformLayout();
            this.groupLibCorr.ResumeLayout(false);
            this.groupLibCorr.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabDocument;
        private System.Windows.Forms.TabPage tabResults;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.TextBox textMinTransitions;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textMinPeptides;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox cbRemoveRepeatedPeptides;
        private System.Windows.Forms.CheckBox cbRemoveDuplicatePeptides;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupLibCorr;
        private System.Windows.Forms.Label labelMinDotProduct;
        private System.Windows.Forms.TextBox textRTRegressionThreshold;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox textMinDotProduct;
        private System.Windows.Forms.RadioButton radioRemoveMissing;
        private System.Windows.Forms.RadioButton radioIgnoreMissing;
        private System.Windows.Forms.TextBox textMinPeakFoundRatio;
        private System.Windows.Forms.TextBox textMaxPeakFoundRatio;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox textMaxPeakRank;
        private System.Windows.Forms.Label labelMaxPeakRank;
        private System.Windows.Forms.ToolTip helpTip;
        private System.Windows.Forms.Label labelLabelType;
        private System.Windows.Forms.ComboBox comboRefineLabelType;
        private System.Windows.Forms.CheckBox cbAdd;
        private System.Windows.Forms.CheckBox cbPreferLarger;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox comboReplicateUse;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.CheckBox cbAutoTransitions;
        private System.Windows.Forms.CheckBox cbAutoPrecursors;
        private System.Windows.Forms.CheckBox cbAutoPeptides;
        private System.Windows.Forms.TextBox textMaxPepPeakRank;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox textMinIdotProduct;
        private System.Windows.Forms.Label labelMinIdotProduct;
    }
}