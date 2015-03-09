namespace pwiz.Skyline.Controls.GroupComparison
{
    partial class GroupComparisonSettingsForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GroupComparisonSettingsForm));
            this.groupBoxScope = new System.Windows.Forms.GroupBox();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.radioScopePeptide = new System.Windows.Forms.RadioButton();
            this.radioScopeProtein = new System.Windows.Forms.RadioButton();
            this.lblPercent = new System.Windows.Forms.Label();
            this.tbxConfidenceLevel = new System.Windows.Forms.TextBox();
            this.lblConfidenceLevel = new System.Windows.Forms.Label();
            this.lblNormalizationMethod = new System.Windows.Forms.Label();
            this.comboIdentityAnnotation = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.comboNormalizationMethod = new System.Windows.Forms.ComboBox();
            this.comboCaseValue = new System.Windows.Forms.ComboBox();
            this.lblCompareAgainst = new System.Windows.Forms.Label();
            this.lblControlValue = new System.Windows.Forms.Label();
            this.lblControlAnnotation = new System.Windows.Forms.Label();
            this.comboControlValue = new System.Windows.Forms.ComboBox();
            this.comboControlAnnotation = new System.Windows.Forms.ComboBox();
            this.groupBoxScope.SuspendLayout();
            this.flowLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
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
            // 
            // radioScopeProtein
            // 
            resources.ApplyResources(this.radioScopeProtein, "radioScopeProtein");
            this.radioScopeProtein.Name = "radioScopeProtein";
            this.radioScopeProtein.TabStop = true;
            this.radioScopeProtein.UseVisualStyleBackColor = true;
            // 
            // lblPercent
            // 
            resources.ApplyResources(this.lblPercent, "lblPercent");
            this.lblPercent.Name = "lblPercent";
            // 
            // tbxConfidenceLevel
            // 
            resources.ApplyResources(this.tbxConfidenceLevel, "tbxConfidenceLevel");
            this.tbxConfidenceLevel.Name = "tbxConfidenceLevel";
            // 
            // lblConfidenceLevel
            // 
            resources.ApplyResources(this.lblConfidenceLevel, "lblConfidenceLevel");
            this.lblConfidenceLevel.Name = "lblConfidenceLevel";
            // 
            // lblNormalizationMethod
            // 
            resources.ApplyResources(this.lblNormalizationMethod, "lblNormalizationMethod");
            this.lblNormalizationMethod.Name = "lblNormalizationMethod";
            // 
            // comboIdentityAnnotation
            // 
            resources.ApplyResources(this.comboIdentityAnnotation, "comboIdentityAnnotation");
            this.comboIdentityAnnotation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboIdentityAnnotation.FormattingEnabled = true;
            this.comboIdentityAnnotation.Name = "comboIdentityAnnotation";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
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
            // 
            // comboCaseValue
            // 
            resources.ApplyResources(this.comboCaseValue, "comboCaseValue");
            this.comboCaseValue.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboCaseValue.FormattingEnabled = true;
            this.comboCaseValue.Name = "comboCaseValue";
            // 
            // lblCompareAgainst
            // 
            resources.ApplyResources(this.lblCompareAgainst, "lblCompareAgainst");
            this.lblCompareAgainst.AutoEllipsis = true;
            this.lblCompareAgainst.Name = "lblCompareAgainst";
            // 
            // lblControlValue
            // 
            resources.ApplyResources(this.lblControlValue, "lblControlValue");
            this.lblControlValue.Name = "lblControlValue";
            // 
            // lblControlAnnotation
            // 
            resources.ApplyResources(this.lblControlAnnotation, "lblControlAnnotation");
            this.lblControlAnnotation.Name = "lblControlAnnotation";
            // 
            // comboControlValue
            // 
            resources.ApplyResources(this.comboControlValue, "comboControlValue");
            this.comboControlValue.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboControlValue.FormattingEnabled = true;
            this.comboControlValue.Name = "comboControlValue";
            // 
            // comboControlAnnotation
            // 
            resources.ApplyResources(this.comboControlAnnotation, "comboControlAnnotation");
            this.comboControlAnnotation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboControlAnnotation.FormattingEnabled = true;
            this.comboControlAnnotation.Name = "comboControlAnnotation";
            // 
            // GroupComparisonSettingsForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.groupBoxScope);
            this.Controls.Add(this.lblPercent);
            this.Controls.Add(this.tbxConfidenceLevel);
            this.Controls.Add(this.lblConfidenceLevel);
            this.Controls.Add(this.lblNormalizationMethod);
            this.Controls.Add(this.comboIdentityAnnotation);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.comboNormalizationMethod);
            this.Controls.Add(this.comboCaseValue);
            this.Controls.Add(this.lblCompareAgainst);
            this.Controls.Add(this.lblControlValue);
            this.Controls.Add(this.lblControlAnnotation);
            this.Controls.Add(this.comboControlValue);
            this.Controls.Add(this.comboControlAnnotation);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "GroupComparisonSettingsForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.groupBoxScope.ResumeLayout(false);
            this.flowLayoutPanel1.ResumeLayout(false);
            this.flowLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBoxScope;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.RadioButton radioScopePeptide;
        private System.Windows.Forms.RadioButton radioScopeProtein;
        private System.Windows.Forms.Label lblPercent;
        private System.Windows.Forms.TextBox tbxConfidenceLevel;
        private System.Windows.Forms.Label lblConfidenceLevel;
        private System.Windows.Forms.Label lblNormalizationMethod;
        private System.Windows.Forms.ComboBox comboIdentityAnnotation;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox comboNormalizationMethod;
        private System.Windows.Forms.ComboBox comboCaseValue;
        private System.Windows.Forms.Label lblCompareAgainst;
        private System.Windows.Forms.Label lblControlValue;
        private System.Windows.Forms.Label lblControlAnnotation;
        private System.Windows.Forms.ComboBox comboControlValue;
        private System.Windows.Forms.ComboBox comboControlAnnotation;
    }
}