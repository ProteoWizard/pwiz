namespace pwiz.Skyline.Controls.GroupComparison
{
    partial class EditGroupComparisonDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditGroupComparisonDlg));
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.panelMain = new System.Windows.Forms.Panel();
            this.btnAdvanced = new System.Windows.Forms.Button();
            this.lblControlAnnotation = new System.Windows.Forms.Label();
            this.comboControlAnnotation = new System.Windows.Forms.ComboBox();
            this.groupBoxScope = new System.Windows.Forms.GroupBox();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.radioScopePeptide = new System.Windows.Forms.RadioButton();
            this.radioScopeProtein = new System.Windows.Forms.RadioButton();
            this.lblPercent = new System.Windows.Forms.Label();
            this.comboControlValue = new System.Windows.Forms.ComboBox();
            this.tbxConfidenceLevel = new System.Windows.Forms.TextBox();
            this.lblConfidenceLevel = new System.Windows.Forms.Label();
            this.lblControlValue = new System.Windows.Forms.Label();
            this.lblCompareAgainst = new System.Windows.Forms.Label();
            this.comboCaseValue = new System.Windows.Forms.ComboBox();
            this.comboNormalizationMethod = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.comboIdentityAnnotation = new System.Windows.Forms.ComboBox();
            this.lblNormalizationMethod = new System.Windows.Forms.Label();
            this.groupBoxQValueCutoff = new System.Windows.Forms.GroupBox();
            this.tbxQValueCutoff = new System.Windows.Forms.TextBox();
            this.lblQValueCutoff = new System.Windows.Forms.Label();
            this.lblSummaryMethod = new System.Windows.Forms.Label();
            this.comboSummaryMethod = new System.Windows.Forms.ComboBox();
            this.cbxUseZeroForMissingPeaks = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.tbxName = new System.Windows.Forms.TextBox();
            this.btnPreview = new System.Windows.Forms.Button();
            this.panelName = new System.Windows.Forms.Panel();
            this.panelButtons = new System.Windows.Forms.Panel();
            this.panelAdvanced = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).BeginInit();
            this.panelMain.SuspendLayout();
            this.groupBoxScope.SuspendLayout();
            this.flowLayoutPanel1.SuspendLayout();
            this.groupBoxQValueCutoff.SuspendLayout();
            this.panelName.SuspendLayout();
            this.panelButtons.SuspendLayout();
            this.panelAdvanced.SuspendLayout();
            this.SuspendLayout();
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
            // panelMain
            // 
            this.panelMain.Controls.Add(this.btnAdvanced);
            this.panelMain.Controls.Add(this.lblControlAnnotation);
            this.panelMain.Controls.Add(this.comboControlAnnotation);
            this.panelMain.Controls.Add(this.groupBoxScope);
            this.panelMain.Controls.Add(this.lblPercent);
            this.panelMain.Controls.Add(this.comboControlValue);
            this.panelMain.Controls.Add(this.tbxConfidenceLevel);
            this.panelMain.Controls.Add(this.lblConfidenceLevel);
            this.panelMain.Controls.Add(this.lblControlValue);
            this.panelMain.Controls.Add(this.lblCompareAgainst);
            this.panelMain.Controls.Add(this.comboCaseValue);
            this.panelMain.Controls.Add(this.comboNormalizationMethod);
            this.panelMain.Controls.Add(this.label2);
            this.panelMain.Controls.Add(this.comboIdentityAnnotation);
            this.panelMain.Controls.Add(this.lblNormalizationMethod);
            resources.ApplyResources(this.panelMain, "panelMain");
            this.panelMain.Name = "panelMain";
            // 
            // btnAdvanced
            // 
            resources.ApplyResources(this.btnAdvanced, "btnAdvanced");
            this.btnAdvanced.Name = "btnAdvanced";
            this.btnAdvanced.UseVisualStyleBackColor = true;
            this.btnAdvanced.Click += new System.EventHandler(this.btnAdvanced_Click);
            // 
            // lblControlAnnotation
            // 
            resources.ApplyResources(this.lblControlAnnotation, "lblControlAnnotation");
            this.lblControlAnnotation.Name = "lblControlAnnotation";
            // 
            // comboControlAnnotation
            // 
            resources.ApplyResources(this.comboControlAnnotation, "comboControlAnnotation");
            this.comboControlAnnotation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboControlAnnotation.FormattingEnabled = true;
            this.comboControlAnnotation.Name = "comboControlAnnotation";
            this.comboControlAnnotation.SelectedIndexChanged += new System.EventHandler(this.comboControlAnnotation_SelectedIndexChanged);
            // 
            // groupBoxScope
            // 
            resources.ApplyResources(this.groupBoxScope, "groupBoxScope");
            this.groupBoxScope.Controls.Add(this.flowLayoutPanel1);
            this.groupBoxScope.Name = "groupBoxScope";
            this.groupBoxScope.TabStop = false;
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Controls.Add(this.radioScopePeptide);
            this.flowLayoutPanel1.Controls.Add(this.radioScopeProtein);
            resources.ApplyResources(this.flowLayoutPanel1, "flowLayoutPanel1");
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            // 
            // radioScopePeptide
            // 
            resources.ApplyResources(this.radioScopePeptide, "radioScopePeptide");
            this.radioScopePeptide.Name = "radioScopePeptide";
            this.radioScopePeptide.TabStop = true;
            this.radioScopePeptide.UseVisualStyleBackColor = true;
            this.radioScopePeptide.CheckedChanged += new System.EventHandler(this.radioScope_CheckedChanged);
            // 
            // radioScopeProtein
            // 
            resources.ApplyResources(this.radioScopeProtein, "radioScopeProtein");
            this.radioScopeProtein.Name = "radioScopeProtein";
            this.radioScopeProtein.TabStop = true;
            this.radioScopeProtein.UseVisualStyleBackColor = true;
            this.radioScopeProtein.CheckedChanged += new System.EventHandler(this.radioScope_CheckedChanged);
            // 
            // lblPercent
            // 
            resources.ApplyResources(this.lblPercent, "lblPercent");
            this.lblPercent.Name = "lblPercent";
            // 
            // comboControlValue
            // 
            resources.ApplyResources(this.comboControlValue, "comboControlValue");
            this.comboControlValue.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboControlValue.FormattingEnabled = true;
            this.comboControlValue.Name = "comboControlValue";
            this.comboControlValue.SelectedIndexChanged += new System.EventHandler(this.comboControlValue_SelectedIndexChanged);
            // 
            // tbxConfidenceLevel
            // 
            resources.ApplyResources(this.tbxConfidenceLevel, "tbxConfidenceLevel");
            this.tbxConfidenceLevel.Name = "tbxConfidenceLevel";
            this.tbxConfidenceLevel.TextChanged += new System.EventHandler(this.tbxConfidenceLevel_TextChanged);
            // 
            // lblConfidenceLevel
            // 
            resources.ApplyResources(this.lblConfidenceLevel, "lblConfidenceLevel");
            this.lblConfidenceLevel.Name = "lblConfidenceLevel";
            // 
            // lblControlValue
            // 
            resources.ApplyResources(this.lblControlValue, "lblControlValue");
            this.lblControlValue.Name = "lblControlValue";
            // 
            // lblCompareAgainst
            // 
            this.lblCompareAgainst.AutoEllipsis = true;
            resources.ApplyResources(this.lblCompareAgainst, "lblCompareAgainst");
            this.lblCompareAgainst.Name = "lblCompareAgainst";
            // 
            // comboCaseValue
            // 
            resources.ApplyResources(this.comboCaseValue, "comboCaseValue");
            this.comboCaseValue.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboCaseValue.FormattingEnabled = true;
            this.comboCaseValue.Name = "comboCaseValue";
            this.comboCaseValue.SelectedIndexChanged += new System.EventHandler(this.comboCaseValue_SelectedIndexChanged);
            // 
            // comboNormalizationMethod
            // 
            resources.ApplyResources(this.comboNormalizationMethod, "comboNormalizationMethod");
            this.comboNormalizationMethod.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboNormalizationMethod.FormattingEnabled = true;
            this.comboNormalizationMethod.Items.AddRange(new object[] {
            resources.GetString("comboNormalizationMethod.Items"),
            resources.GetString("comboNormalizationMethod.Items1"),
            resources.GetString("comboNormalizationMethod.Items2"),
            resources.GetString("comboNormalizationMethod.Items3"),
            resources.GetString("comboNormalizationMethod.Items4")});
            this.comboNormalizationMethod.Name = "comboNormalizationMethod";
            this.comboNormalizationMethod.SelectedIndexChanged += new System.EventHandler(this.comboNormalizationMethod_SelectedIndexChanged);
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // comboIdentityAnnotation
            // 
            resources.ApplyResources(this.comboIdentityAnnotation, "comboIdentityAnnotation");
            this.comboIdentityAnnotation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboIdentityAnnotation.FormattingEnabled = true;
            this.comboIdentityAnnotation.Name = "comboIdentityAnnotation";
            this.comboIdentityAnnotation.SelectedIndexChanged += new System.EventHandler(this.comboIdentityAnnotation_SelectedIndexChanged);
            // 
            // lblNormalizationMethod
            // 
            resources.ApplyResources(this.lblNormalizationMethod, "lblNormalizationMethod");
            this.lblNormalizationMethod.Name = "lblNormalizationMethod";
            // 
            // groupBoxQValueCutoff
            // 
            resources.ApplyResources(this.groupBoxQValueCutoff, "groupBoxQValueCutoff");
            this.groupBoxQValueCutoff.Controls.Add(this.tbxQValueCutoff);
            this.groupBoxQValueCutoff.Controls.Add(this.lblQValueCutoff);
            this.groupBoxQValueCutoff.Name = "groupBoxQValueCutoff";
            this.groupBoxQValueCutoff.TabStop = false;
            // 
            // tbxQValueCutoff
            // 
            resources.ApplyResources(this.tbxQValueCutoff, "tbxQValueCutoff");
            this.tbxQValueCutoff.Name = "tbxQValueCutoff";
            this.tbxQValueCutoff.TextChanged += new System.EventHandler(this.tbxQValueCutoff_TextChanged);
            // 
            // lblQValueCutoff
            // 
            resources.ApplyResources(this.lblQValueCutoff, "lblQValueCutoff");
            this.lblQValueCutoff.AutoEllipsis = true;
            this.lblQValueCutoff.Name = "lblQValueCutoff";
            // 
            // lblSummaryMethod
            // 
            resources.ApplyResources(this.lblSummaryMethod, "lblSummaryMethod");
            this.lblSummaryMethod.Name = "lblSummaryMethod";
            // 
            // comboSummaryMethod
            // 
            resources.ApplyResources(this.comboSummaryMethod, "comboSummaryMethod");
            this.comboSummaryMethod.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboSummaryMethod.FormattingEnabled = true;
            this.comboSummaryMethod.Name = "comboSummaryMethod";
            this.comboSummaryMethod.SelectedIndexChanged += new System.EventHandler(this.comboSummaryMethod_SelectedIndexChanged);
            // 
            // cbxUseZeroForMissingPeaks
            // 
            resources.ApplyResources(this.cbxUseZeroForMissingPeaks, "cbxUseZeroForMissingPeaks");
            this.cbxUseZeroForMissingPeaks.Name = "cbxUseZeroForMissingPeaks";
            this.cbxUseZeroForMissingPeaks.UseVisualStyleBackColor = true;
            this.cbxUseZeroForMissingPeaks.CheckedChanged += new System.EventHandler(this.cbxTreatMissingAsZero_CheckedChanged);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // tbxName
            // 
            resources.ApplyResources(this.tbxName, "tbxName");
            this.tbxName.Name = "tbxName";
            // 
            // btnPreview
            // 
            resources.ApplyResources(this.btnPreview, "btnPreview");
            this.btnPreview.Name = "btnPreview";
            this.btnPreview.UseVisualStyleBackColor = true;
            this.btnPreview.Click += new System.EventHandler(this.btnPreview_Click);
            // 
            // panelName
            // 
            this.panelName.Controls.Add(this.label1);
            this.panelName.Controls.Add(this.tbxName);
            this.panelName.Controls.Add(this.btnPreview);
            resources.ApplyResources(this.panelName, "panelName");
            this.panelName.Name = "panelName";
            // 
            // panelButtons
            // 
            this.panelButtons.Controls.Add(this.btnOK);
            this.panelButtons.Controls.Add(this.btnCancel);
            resources.ApplyResources(this.panelButtons, "panelButtons");
            this.panelButtons.Name = "panelButtons";
            // 
            // panelAdvanced
            // 
            this.panelAdvanced.Controls.Add(this.groupBoxQValueCutoff);
            this.panelAdvanced.Controls.Add(this.comboSummaryMethod);
            this.panelAdvanced.Controls.Add(this.cbxUseZeroForMissingPeaks);
            this.panelAdvanced.Controls.Add(this.lblSummaryMethod);
            resources.ApplyResources(this.panelAdvanced, "panelAdvanced");
            this.panelAdvanced.Name = "panelAdvanced";
            // 
            // EditGroupComparisonDlg
            // 
            this.AcceptButton = this.btnOK;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.panelAdvanced);
            this.Controls.Add(this.panelButtons);
            this.Controls.Add(this.panelMain);
            this.Controls.Add(this.panelName);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditGroupComparisonDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).EndInit();
            this.panelMain.ResumeLayout(false);
            this.panelMain.PerformLayout();
            this.groupBoxScope.ResumeLayout(false);
            this.flowLayoutPanel1.ResumeLayout(false);
            this.flowLayoutPanel1.PerformLayout();
            this.groupBoxQValueCutoff.ResumeLayout(false);
            this.groupBoxQValueCutoff.PerformLayout();
            this.panelName.ResumeLayout(false);
            this.panelName.PerformLayout();
            this.panelButtons.ResumeLayout(false);
            this.panelAdvanced.ResumeLayout(false);
            this.panelAdvanced.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbxName;
        private System.Windows.Forms.Button btnPreview;
        private System.Windows.Forms.ComboBox comboIdentityAnnotation;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox comboNormalizationMethod;
        private System.Windows.Forms.ComboBox comboCaseValue;
        private System.Windows.Forms.Label lblCompareAgainst;
        private System.Windows.Forms.Label lblControlValue;
        private System.Windows.Forms.Label lblControlAnnotation;
        private System.Windows.Forms.ComboBox comboControlValue;
        private System.Windows.Forms.ComboBox comboControlAnnotation;
        private System.Windows.Forms.Label lblNormalizationMethod;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.TextBox tbxConfidenceLevel;
        private System.Windows.Forms.Label lblConfidenceLevel;
        private System.Windows.Forms.Label lblPercent;
        private System.Windows.Forms.GroupBox groupBoxScope;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.RadioButton radioScopePeptide;
        private System.Windows.Forms.RadioButton radioScopeProtein;
        private System.Windows.Forms.CheckBox cbxUseZeroForMissingPeaks;
        private System.Windows.Forms.Panel panelName;
        private System.Windows.Forms.Panel panelMain;
        private System.Windows.Forms.Panel panelButtons;
        private System.Windows.Forms.ComboBox comboSummaryMethod;
        private System.Windows.Forms.Label lblSummaryMethod;
        private System.Windows.Forms.TextBox tbxQValueCutoff;
        private System.Windows.Forms.Label lblQValueCutoff;
        private System.Windows.Forms.GroupBox groupBoxQValueCutoff;
        private System.Windows.Forms.Button btnAdvanced;
        private System.Windows.Forms.Panel panelAdvanced;
    }
}