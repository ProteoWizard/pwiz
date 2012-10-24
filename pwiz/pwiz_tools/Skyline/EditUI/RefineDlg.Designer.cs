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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RefineDlg));
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
            this.textMaxPepPeakRank = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
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
            this.textMinIdotProduct = new System.Windows.Forms.TextBox();
            this.labelMinIdotProduct = new System.Windows.Forms.Label();
            this.textMinDotProduct = new System.Windows.Forms.TextBox();
            this.labelMinDotProduct = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.textRTRegressionThreshold = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.tabControl1.SuspendLayout();
            this.tabDocument.SuspendLayout();
            this.tabResults.SuspendLayout();
            this.groupLibCorr.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            resources.ApplyResources(this.tabControl1, "tabControl1");
            this.tabControl1.Controls.Add(this.tabDocument);
            this.tabControl1.Controls.Add(this.tabResults);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
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
            resources.ApplyResources(this.tabDocument, "tabDocument");
            this.tabDocument.Name = "tabDocument";
            this.tabDocument.UseVisualStyleBackColor = true;
            // 
            // cbAutoTransitions
            // 
            resources.ApplyResources(this.cbAutoTransitions, "cbAutoTransitions");
            this.cbAutoTransitions.Name = "cbAutoTransitions";
            this.cbAutoTransitions.UseVisualStyleBackColor = true;
            // 
            // cbAutoPrecursors
            // 
            resources.ApplyResources(this.cbAutoPrecursors, "cbAutoPrecursors");
            this.cbAutoPrecursors.Name = "cbAutoPrecursors";
            this.cbAutoPrecursors.UseVisualStyleBackColor = true;
            // 
            // cbAutoPeptides
            // 
            resources.ApplyResources(this.cbAutoPeptides, "cbAutoPeptides");
            this.cbAutoPeptides.Name = "cbAutoPeptides";
            this.cbAutoPeptides.UseVisualStyleBackColor = true;
            // 
            // label7
            // 
            resources.ApplyResources(this.label7, "label7");
            this.label7.Name = "label7";
            // 
            // cbAdd
            // 
            resources.ApplyResources(this.cbAdd, "cbAdd");
            this.cbAdd.Name = "cbAdd";
            this.cbAdd.UseVisualStyleBackColor = true;
            this.cbAdd.CheckedChanged += new System.EventHandler(this.cbAdd_CheckedChanged);
            // 
            // labelLabelType
            // 
            resources.ApplyResources(this.labelLabelType, "labelLabelType");
            this.labelLabelType.Name = "labelLabelType";
            // 
            // comboRefineLabelType
            // 
            this.comboRefineLabelType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRefineLabelType.FormattingEnabled = true;
            resources.ApplyResources(this.comboRefineLabelType, "comboRefineLabelType");
            this.comboRefineLabelType.Name = "comboRefineLabelType";
            // 
            // cbRemoveRepeatedPeptides
            // 
            resources.ApplyResources(this.cbRemoveRepeatedPeptides, "cbRemoveRepeatedPeptides");
            this.cbRemoveRepeatedPeptides.Name = "cbRemoveRepeatedPeptides";
            this.helpTip.SetToolTip(this.cbRemoveRepeatedPeptides, resources.GetString("cbRemoveRepeatedPeptides.ToolTip"));
            this.cbRemoveRepeatedPeptides.UseVisualStyleBackColor = true;
            // 
            // cbRemoveDuplicatePeptides
            // 
            resources.ApplyResources(this.cbRemoveDuplicatePeptides, "cbRemoveDuplicatePeptides");
            this.cbRemoveDuplicatePeptides.Name = "cbRemoveDuplicatePeptides";
            this.helpTip.SetToolTip(this.cbRemoveDuplicatePeptides, resources.GetString("cbRemoveDuplicatePeptides.ToolTip"));
            this.cbRemoveDuplicatePeptides.UseVisualStyleBackColor = true;
            // 
            // textMinTransitions
            // 
            resources.ApplyResources(this.textMinTransitions, "textMinTransitions");
            this.textMinTransitions.Name = "textMinTransitions";
            this.helpTip.SetToolTip(this.textMinTransitions, resources.GetString("textMinTransitions.ToolTip"));
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // textMinPeptides
            // 
            resources.ApplyResources(this.textMinPeptides, "textMinPeptides");
            this.textMinPeptides.Name = "textMinPeptides";
            this.helpTip.SetToolTip(this.textMinPeptides, resources.GetString("textMinPeptides.ToolTip"));
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
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
            resources.ApplyResources(this.tabResults, "tabResults");
            this.tabResults.Name = "tabResults";
            this.tabResults.UseVisualStyleBackColor = true;
            // 
            // textMaxPepPeakRank
            // 
            resources.ApplyResources(this.textMaxPepPeakRank, "textMaxPepPeakRank");
            this.textMaxPepPeakRank.Name = "textMaxPepPeakRank";
            this.helpTip.SetToolTip(this.textMaxPepPeakRank, resources.GetString("textMaxPepPeakRank.ToolTip"));
            // 
            // label8
            // 
            resources.ApplyResources(this.label8, "label8");
            this.label8.Name = "label8";
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // comboReplicateUse
            // 
            this.comboReplicateUse.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboReplicateUse.FormattingEnabled = true;
            this.comboReplicateUse.Items.AddRange(new object[] {
            resources.GetString("comboReplicateUse.Items"),
            resources.GetString("comboReplicateUse.Items1")});
            resources.ApplyResources(this.comboReplicateUse, "comboReplicateUse");
            this.comboReplicateUse.Name = "comboReplicateUse";
            this.helpTip.SetToolTip(this.comboReplicateUse, resources.GetString("comboReplicateUse.ToolTip"));
            // 
            // cbPreferLarger
            // 
            resources.ApplyResources(this.cbPreferLarger, "cbPreferLarger");
            this.cbPreferLarger.Name = "cbPreferLarger";
            this.helpTip.SetToolTip(this.cbPreferLarger, resources.GetString("cbPreferLarger.ToolTip"));
            this.cbPreferLarger.UseVisualStyleBackColor = true;
            // 
            // textMaxPeakRank
            // 
            resources.ApplyResources(this.textMaxPeakRank, "textMaxPeakRank");
            this.textMaxPeakRank.Name = "textMaxPeakRank";
            this.helpTip.SetToolTip(this.textMaxPeakRank, resources.GetString("textMaxPeakRank.ToolTip"));
            this.textMaxPeakRank.TextChanged += new System.EventHandler(this.textMaxPeakRank_TextChanged);
            // 
            // labelMaxPeakRank
            // 
            resources.ApplyResources(this.labelMaxPeakRank, "labelMaxPeakRank");
            this.labelMaxPeakRank.Name = "labelMaxPeakRank";
            // 
            // textMaxPeakFoundRatio
            // 
            resources.ApplyResources(this.textMaxPeakFoundRatio, "textMaxPeakFoundRatio");
            this.textMaxPeakFoundRatio.Name = "textMaxPeakFoundRatio";
            this.helpTip.SetToolTip(this.textMaxPeakFoundRatio, resources.GetString("textMaxPeakFoundRatio.ToolTip"));
            // 
            // label6
            // 
            resources.ApplyResources(this.label6, "label6");
            this.label6.Name = "label6";
            // 
            // radioRemoveMissing
            // 
            resources.ApplyResources(this.radioRemoveMissing, "radioRemoveMissing");
            this.radioRemoveMissing.Name = "radioRemoveMissing";
            this.radioRemoveMissing.TabStop = true;
            this.helpTip.SetToolTip(this.radioRemoveMissing, resources.GetString("radioRemoveMissing.ToolTip"));
            this.radioRemoveMissing.UseVisualStyleBackColor = true;
            // 
            // radioIgnoreMissing
            // 
            resources.ApplyResources(this.radioIgnoreMissing, "radioIgnoreMissing");
            this.radioIgnoreMissing.Checked = true;
            this.radioIgnoreMissing.Name = "radioIgnoreMissing";
            this.radioIgnoreMissing.TabStop = true;
            this.helpTip.SetToolTip(this.radioIgnoreMissing, resources.GetString("radioIgnoreMissing.ToolTip"));
            this.radioIgnoreMissing.UseVisualStyleBackColor = true;
            // 
            // textMinPeakFoundRatio
            // 
            resources.ApplyResources(this.textMinPeakFoundRatio, "textMinPeakFoundRatio");
            this.textMinPeakFoundRatio.Name = "textMinPeakFoundRatio";
            this.helpTip.SetToolTip(this.textMinPeakFoundRatio, resources.GetString("textMinPeakFoundRatio.ToolTip"));
            // 
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            // 
            // groupLibCorr
            // 
            this.groupLibCorr.Controls.Add(this.textMinIdotProduct);
            this.groupLibCorr.Controls.Add(this.labelMinIdotProduct);
            this.groupLibCorr.Controls.Add(this.textMinDotProduct);
            this.groupLibCorr.Controls.Add(this.labelMinDotProduct);
            resources.ApplyResources(this.groupLibCorr, "groupLibCorr");
            this.groupLibCorr.Name = "groupLibCorr";
            this.groupLibCorr.TabStop = false;
            // 
            // textMinIdotProduct
            // 
            resources.ApplyResources(this.textMinIdotProduct, "textMinIdotProduct");
            this.textMinIdotProduct.Name = "textMinIdotProduct";
            this.helpTip.SetToolTip(this.textMinIdotProduct, resources.GetString("textMinIdotProduct.ToolTip"));
            // 
            // labelMinIdotProduct
            // 
            resources.ApplyResources(this.labelMinIdotProduct, "labelMinIdotProduct");
            this.labelMinIdotProduct.Name = "labelMinIdotProduct";
            // 
            // textMinDotProduct
            // 
            resources.ApplyResources(this.textMinDotProduct, "textMinDotProduct");
            this.textMinDotProduct.Name = "textMinDotProduct";
            this.helpTip.SetToolTip(this.textMinDotProduct, resources.GetString("textMinDotProduct.ToolTip"));
            // 
            // labelMinDotProduct
            // 
            resources.ApplyResources(this.labelMinDotProduct, "labelMinDotProduct");
            this.labelMinDotProduct.Name = "labelMinDotProduct";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.textRTRegressionThreshold);
            this.groupBox1.Controls.Add(this.label3);
            resources.ApplyResources(this.groupBox1, "groupBox1");
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.TabStop = false;
            // 
            // textRTRegressionThreshold
            // 
            resources.ApplyResources(this.textRTRegressionThreshold, "textRTRegressionThreshold");
            this.textRTRegressionThreshold.Name = "textRTRegressionThreshold";
            this.helpTip.SetToolTip(this.textRTRegressionThreshold, resources.GetString("textRTRegressionThreshold.ToolTip"));
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // btnOK
            // 
            resources.ApplyResources(this.btnOK, "btnOK");
            this.btnOK.Name = "btnOK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // RefineDlg
            // 
            this.AcceptButton = this.btnOK;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.tabControl1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "RefineDlg";
            this.ShowInTaskbar = false;
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