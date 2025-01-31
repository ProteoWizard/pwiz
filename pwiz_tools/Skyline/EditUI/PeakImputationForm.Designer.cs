namespace pwiz.Skyline.EditUI
{
    partial class PeakImputationForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PeakImputationForm));
            this.panel1 = new System.Windows.Forms.Panel();
            this.linkLabelViewRegression = new System.Windows.Forms.LinkLabel();
            this.btnImputeBoundaries = new System.Windows.Forms.Button();
            this.lblPercentPeakWidth = new System.Windows.Forms.Label();
            this.lblMinutes = new System.Windows.Forms.Label();
            this.tbxMaxPeakWidthVariation = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.cbxAlignAllGraphs = new System.Windows.Forms.CheckBox();
            this.groupBoxScope = new System.Windows.Forms.GroupBox();
            this.radioScopeDocument = new System.Windows.Forms.RadioButton();
            this.radioScopeSelection = new System.Windows.Forms.RadioButton();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.cbxOverwriteManual = new System.Windows.Forms.CheckBox();
            this.tbxRtDeviationCutoff = new System.Windows.Forms.TextBox();
            this.lblSdCutoff = new System.Windows.Forms.Label();
            this.comboRtCalculator = new System.Windows.Forms.ComboBox();
            this.lblRetentionTimeAlignment = new System.Windows.Forms.Label();
            this.groupBoxResults = new System.Windows.Forms.GroupBox();
            this.tbxExemplary = new System.Windows.Forms.TextBox();
            this.lblExemplary = new System.Windows.Forms.Label();
            this.tbxNeedsRemoval = new System.Windows.Forms.TextBox();
            this.lblNeedRemoval = new System.Windows.Forms.Label();
            this.tbxRejected = new System.Windows.Forms.TextBox();
            this.lblRejected = new System.Windows.Forms.Label();
            this.tbxAccepted = new System.Windows.Forms.TextBox();
            this.lblAccepted = new System.Windows.Forms.Label();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.saveFileDialog1 = new System.Windows.Forms.SaveFileDialog();
            this.imageList1 = new System.Windows.Forms.ImageList(this.components);
            this.panel1.SuspendLayout();
            this.groupBoxScope.SuspendLayout();
            this.groupBoxResults.SuspendLayout();
            this.SuspendLayout();
            // 
            // databoundGridControl
            // 
            resources.ApplyResources(this.databoundGridControl, "databoundGridControl");
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.linkLabelViewRegression);
            this.panel1.Controls.Add(this.btnImputeBoundaries);
            this.panel1.Controls.Add(this.lblPercentPeakWidth);
            this.panel1.Controls.Add(this.lblMinutes);
            this.panel1.Controls.Add(this.tbxMaxPeakWidthVariation);
            this.panel1.Controls.Add(this.label2);
            this.panel1.Controls.Add(this.cbxAlignAllGraphs);
            this.panel1.Controls.Add(this.groupBoxScope);
            this.panel1.Controls.Add(this.progressBar1);
            this.panel1.Controls.Add(this.cbxOverwriteManual);
            this.panel1.Controls.Add(this.tbxRtDeviationCutoff);
            this.panel1.Controls.Add(this.lblSdCutoff);
            this.panel1.Controls.Add(this.comboRtCalculator);
            this.panel1.Controls.Add(this.lblRetentionTimeAlignment);
            this.panel1.Controls.Add(this.groupBoxResults);
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Name = "panel1";
            // 
            // linkLabelViewRegression
            // 
            resources.ApplyResources(this.linkLabelViewRegression, "linkLabelViewRegression");
            this.linkLabelViewRegression.Name = "linkLabelViewRegression";
            this.linkLabelViewRegression.TabStop = true;
            this.linkLabelViewRegression.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabelViewRegression_LinkClicked);
            // 
            // btnImputeBoundaries
            // 
            resources.ApplyResources(this.btnImputeBoundaries, "btnImputeBoundaries");
            this.btnImputeBoundaries.Name = "btnImputeBoundaries";
            this.toolTip1.SetToolTip(this.btnImputeBoundaries, resources.GetString("btnImputeBoundaries.ToolTip"));
            this.btnImputeBoundaries.UseVisualStyleBackColor = true;
            this.btnImputeBoundaries.Click += new System.EventHandler(this.btnImputeBoundaries_Click);
            // 
            // lblPercentPeakWidth
            // 
            resources.ApplyResources(this.lblPercentPeakWidth, "lblPercentPeakWidth");
            this.lblPercentPeakWidth.Name = "lblPercentPeakWidth";
            // 
            // lblMinutes
            // 
            resources.ApplyResources(this.lblMinutes, "lblMinutes");
            this.lblMinutes.Name = "lblMinutes";
            // 
            // tbxMaxPeakWidthVariation
            // 
            resources.ApplyResources(this.tbxMaxPeakWidthVariation, "tbxMaxPeakWidthVariation");
            this.tbxMaxPeakWidthVariation.Name = "tbxMaxPeakWidthVariation";
            this.toolTip1.SetToolTip(this.tbxMaxPeakWidthVariation, resources.GetString("tbxMaxPeakWidthVariation.ToolTip"));
            this.tbxMaxPeakWidthVariation.Leave += new System.EventHandler(this.SettingsControlChanged);
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // cbxAlignAllGraphs
            // 
            resources.ApplyResources(this.cbxAlignAllGraphs, "cbxAlignAllGraphs");
            this.cbxAlignAllGraphs.Name = "cbxAlignAllGraphs";
            this.toolTip1.SetToolTip(this.cbxAlignAllGraphs, resources.GetString("cbxAlignAllGraphs.ToolTip"));
            this.cbxAlignAllGraphs.UseVisualStyleBackColor = true;
            this.cbxAlignAllGraphs.CheckedChanged += new System.EventHandler(this.SettingsControlChanged);
            // 
            // groupBoxScope
            // 
            this.groupBoxScope.Controls.Add(this.radioScopeDocument);
            this.groupBoxScope.Controls.Add(this.radioScopeSelection);
            resources.ApplyResources(this.groupBoxScope, "groupBoxScope");
            this.groupBoxScope.Name = "groupBoxScope";
            this.groupBoxScope.TabStop = false;
            // 
            // radioScopeDocument
            // 
            resources.ApplyResources(this.radioScopeDocument, "radioScopeDocument");
            this.radioScopeDocument.Name = "radioScopeDocument";
            this.radioScopeDocument.UseVisualStyleBackColor = true;
            this.radioScopeDocument.Click += new System.EventHandler(this.SettingsControlChanged);
            // 
            // radioScopeSelection
            // 
            resources.ApplyResources(this.radioScopeSelection, "radioScopeSelection");
            this.radioScopeSelection.Checked = true;
            this.radioScopeSelection.Name = "radioScopeSelection";
            this.radioScopeSelection.TabStop = true;
            this.radioScopeSelection.UseVisualStyleBackColor = true;
            this.radioScopeSelection.Click += new System.EventHandler(this.SettingsControlChanged);
            // 
            // progressBar1
            // 
            resources.ApplyResources(this.progressBar1, "progressBar1");
            this.progressBar1.Maximum = 10000;
            this.progressBar1.Name = "progressBar1";
            // 
            // cbxOverwriteManual
            // 
            resources.ApplyResources(this.cbxOverwriteManual, "cbxOverwriteManual");
            this.cbxOverwriteManual.Name = "cbxOverwriteManual";
            this.cbxOverwriteManual.UseVisualStyleBackColor = true;
            this.cbxOverwriteManual.CheckedChanged += new System.EventHandler(this.SettingsControlChanged);
            // 
            // tbxRtDeviationCutoff
            // 
            resources.ApplyResources(this.tbxRtDeviationCutoff, "tbxRtDeviationCutoff");
            this.tbxRtDeviationCutoff.Name = "tbxRtDeviationCutoff";
            this.toolTip1.SetToolTip(this.tbxRtDeviationCutoff, resources.GetString("tbxRtDeviationCutoff.ToolTip"));
            this.tbxRtDeviationCutoff.Leave += new System.EventHandler(this.SettingsControlChanged);
            // 
            // lblSdCutoff
            // 
            resources.ApplyResources(this.lblSdCutoff, "lblSdCutoff");
            this.lblSdCutoff.Name = "lblSdCutoff";
            // 
            // comboRtCalculator
            // 
            this.comboRtCalculator.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRtCalculator.FormattingEnabled = true;
            resources.ApplyResources(this.comboRtCalculator, "comboRtCalculator");
            this.comboRtCalculator.Name = "comboRtCalculator";
            this.toolTip1.SetToolTip(this.comboRtCalculator, resources.GetString("comboRtCalculator.ToolTip"));
            this.comboRtCalculator.SelectedIndexChanged += new System.EventHandler(this.SettingsControlChanged);
            // 
            // lblRetentionTimeAlignment
            // 
            resources.ApplyResources(this.lblRetentionTimeAlignment, "lblRetentionTimeAlignment");
            this.lblRetentionTimeAlignment.Name = "lblRetentionTimeAlignment";
            // 
            // groupBoxResults
            // 
            resources.ApplyResources(this.groupBoxResults, "groupBoxResults");
            this.groupBoxResults.Controls.Add(this.tbxExemplary);
            this.groupBoxResults.Controls.Add(this.lblExemplary);
            this.groupBoxResults.Controls.Add(this.tbxNeedsRemoval);
            this.groupBoxResults.Controls.Add(this.lblNeedRemoval);
            this.groupBoxResults.Controls.Add(this.tbxRejected);
            this.groupBoxResults.Controls.Add(this.lblRejected);
            this.groupBoxResults.Controls.Add(this.tbxAccepted);
            this.groupBoxResults.Controls.Add(this.lblAccepted);
            this.groupBoxResults.Name = "groupBoxResults";
            this.groupBoxResults.TabStop = false;
            this.toolTip1.SetToolTip(this.groupBoxResults, resources.GetString("groupBoxResults.ToolTip"));
            // 
            // tbxExemplary
            // 
            resources.ApplyResources(this.tbxExemplary, "tbxExemplary");
            this.tbxExemplary.Name = "tbxExemplary";
            this.tbxExemplary.ReadOnly = true;
            this.toolTip1.SetToolTip(this.tbxExemplary, resources.GetString("tbxExemplary.ToolTip"));
            // 
            // lblExemplary
            // 
            resources.ApplyResources(this.lblExemplary, "lblExemplary");
            this.lblExemplary.Name = "lblExemplary";
            // 
            // tbxNeedsRemoval
            // 
            resources.ApplyResources(this.tbxNeedsRemoval, "tbxNeedsRemoval");
            this.tbxNeedsRemoval.Name = "tbxNeedsRemoval";
            this.tbxNeedsRemoval.ReadOnly = true;
            this.toolTip1.SetToolTip(this.tbxNeedsRemoval, resources.GetString("tbxNeedsRemoval.ToolTip"));
            // 
            // lblNeedRemoval
            // 
            resources.ApplyResources(this.lblNeedRemoval, "lblNeedRemoval");
            this.lblNeedRemoval.Name = "lblNeedRemoval";
            // 
            // tbxRejected
            // 
            resources.ApplyResources(this.tbxRejected, "tbxRejected");
            this.tbxRejected.Name = "tbxRejected";
            this.tbxRejected.ReadOnly = true;
            this.toolTip1.SetToolTip(this.tbxRejected, resources.GetString("tbxRejected.ToolTip"));
            // 
            // lblRejected
            // 
            resources.ApplyResources(this.lblRejected, "lblRejected");
            this.lblRejected.Name = "lblRejected";
            // 
            // tbxAccepted
            // 
            resources.ApplyResources(this.tbxAccepted, "tbxAccepted");
            this.tbxAccepted.Name = "tbxAccepted";
            this.tbxAccepted.ReadOnly = true;
            this.toolTip1.SetToolTip(this.tbxAccepted, resources.GetString("tbxAccepted.ToolTip"));
            // 
            // lblAccepted
            // 
            resources.ApplyResources(this.lblAccepted, "lblAccepted");
            this.lblAccepted.Name = "lblAccepted";
            // 
            // imageList1
            // 
            this.imageList1.ColorDepth = System.Windows.Forms.ColorDepth.Depth8Bit;
            resources.ApplyResources(this.imageList1, "imageList1");
            this.imageList1.TransparentColor = System.Drawing.Color.Transparent;
            // 
            // PeakImputationForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panel1);
            this.Name = "PeakImputationForm";
            this.Controls.SetChildIndex(this.panel1, 0);
            this.Controls.SetChildIndex(this.databoundGridControl, 0);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.groupBoxScope.ResumeLayout(false);
            this.groupBoxScope.PerformLayout();
            this.groupBoxResults.ResumeLayout(false);
            this.groupBoxResults.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label lblRetentionTimeAlignment;
        private System.Windows.Forms.ComboBox comboRtCalculator;
        private System.Windows.Forms.TextBox tbxRtDeviationCutoff;
        private System.Windows.Forms.Label lblSdCutoff;
        private System.Windows.Forms.CheckBox cbxOverwriteManual;
        private System.Windows.Forms.Button btnImputeBoundaries;
        private System.Windows.Forms.GroupBox groupBoxResults;
        private System.Windows.Forms.TextBox tbxRejected;
        private System.Windows.Forms.Label lblRejected;
        private System.Windows.Forms.TextBox tbxAccepted;
        private System.Windows.Forms.Label lblAccepted;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.GroupBox groupBoxScope;
        private System.Windows.Forms.RadioButton radioScopeDocument;
        private System.Windows.Forms.RadioButton radioScopeSelection;
        private System.Windows.Forms.TextBox tbxNeedsRemoval;
        private System.Windows.Forms.Label lblNeedRemoval;
        private System.Windows.Forms.CheckBox cbxAlignAllGraphs;
        private System.Windows.Forms.SaveFileDialog saveFileDialog1;
        private System.Windows.Forms.Label lblPercentPeakWidth;
        private System.Windows.Forms.Label lblMinutes;
        private System.Windows.Forms.TextBox tbxMaxPeakWidthVariation;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ImageList imageList1;
        private System.Windows.Forms.LinkLabel linkLabelViewRegression;
        private System.Windows.Forms.TextBox tbxExemplary;
        private System.Windows.Forms.Label lblExemplary;
    }
}