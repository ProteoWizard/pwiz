using System.Windows.Forms;

namespace pwiz.Skyline.SettingsUI.IonMobility
{
    partial class UseSpectralLibraryIonMobilityValuesControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UseSpectralLibraryIonMobilityValuesControl));
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.textSpectralLibraryIonMobilityValuesResolvingPower = new System.Windows.Forms.TextBox();
            this.cbUseSpectralLibraryIonMobilityValues = new System.Windows.Forms.CheckBox();
            this.groupBoxUseSpectralLibraryIonMolbilityInfo = new System.Windows.Forms.GroupBox();
            this.textSpectralLibraryIonMobilityWindowWidthAtDtMax = new System.Windows.Forms.TextBox();
            this.textSpectralLibraryIonMobilityWindowWidthAtDt0 = new System.Windows.Forms.TextBox();
            this.labelWidthIMMax = new System.Windows.Forms.Label();
            this.labelWidthIMZero = new System.Windows.Forms.Label();
            this.cbLinear = new System.Windows.Forms.CheckBox();
            this.labelResolvingPower = new System.Windows.Forms.Label();
            this.groupBoxUseSpectralLibraryIonMolbilityInfo.SuspendLayout();
            this.SuspendLayout();
            // 
            // textSpectralLibraryDriftTimesResolvingPower
            // 
            resources.ApplyResources(this.textSpectralLibraryIonMobilityValuesResolvingPower, "textSpectralLibraryIonMobilityValuesResolvingPower");
            this.textSpectralLibraryIonMobilityValuesResolvingPower.Name = "textSpectralLibraryIonMobilityValuesResolvingPower";
            this.toolTip.SetToolTip(this.textSpectralLibraryIonMobilityValuesResolvingPower, resources.GetString("textSpectralLibraryDriftTimesResolvingPower.ToolTip"));
            // 
            // cbUseSpectralLibraryDriftTimes
            // 
            resources.ApplyResources(this.cbUseSpectralLibraryIonMobilityValues, "cbUseSpectralLibraryIonMobilityValues");
            this.cbUseSpectralLibraryIonMobilityValues.Name = "cbUseSpectralLibraryIonMobilityValues";
            this.toolTip.SetToolTip(this.cbUseSpectralLibraryIonMobilityValues, resources.GetString("cbUseSpectralLibraryDriftTimes.ToolTip"));
            this.cbUseSpectralLibraryIonMobilityValues.UseVisualStyleBackColor = true;
            this.cbUseSpectralLibraryIonMobilityValues.CheckedChanged += new System.EventHandler(this.cbUseSpectralLibraryIonMobilityValues_CheckChanged);
            // 
            // groupBoxUseSpectralLibraryIonMolbilityInfo
            // 
            resources.ApplyResources(this.groupBoxUseSpectralLibraryIonMolbilityInfo, "groupBoxUseSpectralLibraryIonMolbilityInfo");
            this.groupBoxUseSpectralLibraryIonMolbilityInfo.Controls.Add(this.textSpectralLibraryIonMobilityWindowWidthAtDtMax);
            this.groupBoxUseSpectralLibraryIonMolbilityInfo.Controls.Add(this.textSpectralLibraryIonMobilityWindowWidthAtDt0);
            this.groupBoxUseSpectralLibraryIonMolbilityInfo.Controls.Add(this.labelWidthIMMax);
            this.groupBoxUseSpectralLibraryIonMolbilityInfo.Controls.Add(this.labelWidthIMZero);
            this.groupBoxUseSpectralLibraryIonMolbilityInfo.Controls.Add(this.cbLinear);
            this.groupBoxUseSpectralLibraryIonMolbilityInfo.Controls.Add(this.textSpectralLibraryIonMobilityValuesResolvingPower);
            this.groupBoxUseSpectralLibraryIonMolbilityInfo.Controls.Add(this.cbUseSpectralLibraryIonMobilityValues);
            this.groupBoxUseSpectralLibraryIonMolbilityInfo.Controls.Add(this.labelResolvingPower);
            this.groupBoxUseSpectralLibraryIonMolbilityInfo.Name = "groupBoxUseSpectralLibraryIonMolbilityInfo";
            this.groupBoxUseSpectralLibraryIonMolbilityInfo.TabStop = false;
            // 
            // textSpectralLibraryDriftTimesWidthAtDtMax
            // 
            resources.ApplyResources(this.textSpectralLibraryIonMobilityWindowWidthAtDtMax, "textSpectralLibraryIonMobilityWindowWidthAtDtMax");
            this.textSpectralLibraryIonMobilityWindowWidthAtDtMax.Name = "textSpectralLibraryIonMobilityWindowWidthAtDtMax";
            this.toolTip.SetToolTip(this.textSpectralLibraryIonMobilityWindowWidthAtDtMax, resources.GetString("textSpectralLibraryDriftTimesWidthAtDtMax.ToolTip"));
            // 
            // textSpectralLibraryDriftTimesWidthAtDt0
            // 
            resources.ApplyResources(this.textSpectralLibraryIonMobilityWindowWidthAtDt0, "textSpectralLibraryIonMobilityWindowWidthAtDt0");
            this.textSpectralLibraryIonMobilityWindowWidthAtDt0.Name = "textSpectralLibraryIonMobilityWindowWidthAtDt0";
            this.toolTip.SetToolTip(this.textSpectralLibraryIonMobilityWindowWidthAtDt0, resources.GetString("textSpectralLibraryDriftTimesWidthAtDt0.ToolTip"));
            // 
            // labelWidthDtMax
            // 
            resources.ApplyResources(this.labelWidthIMMax, "labelWidthIMMax");
            this.labelWidthIMMax.Name = "labelWidthIMMax";
            // 
            // labelWidthDtZero
            // 
            resources.ApplyResources(this.labelWidthIMZero, "labelWidthIMZero");
            this.labelWidthIMZero.Name = "labelWidthIMZero";
            // 
            // cbLinear
            // 
            resources.ApplyResources(this.cbLinear, "cbLinear");
            this.cbLinear.Name = "cbLinear";
            this.toolTip.SetToolTip(this.cbLinear, resources.GetString("cbLinear.ToolTip"));
            this.cbLinear.UseVisualStyleBackColor = true;
            this.cbLinear.CheckedChanged += new System.EventHandler(this.cbLinear_CheckedChanged);
            // 
            // labelResolvingPower
            // 
            resources.ApplyResources(this.labelResolvingPower, "labelResolvingPower");
            this.labelResolvingPower.Name = "labelResolvingPower";
            // 
            // UseSpectralLibraryIonMobilityValuesControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.groupBoxUseSpectralLibraryIonMolbilityInfo);
            this.Name = "UseSpectralLibraryIonMobilityValuesControl";
            this.groupBoxUseSpectralLibraryIonMolbilityInfo.ResumeLayout(false);
            this.groupBoxUseSpectralLibraryIonMolbilityInfo.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private ToolTip toolTip;
        private GroupBox groupBoxUseSpectralLibraryIonMolbilityInfo;
        private TextBox textSpectralLibraryIonMobilityWindowWidthAtDtMax;
        private TextBox textSpectralLibraryIonMobilityWindowWidthAtDt0;
        private Label labelWidthIMMax;
        private Label labelWidthIMZero;
        private CheckBox cbLinear;
        private TextBox textSpectralLibraryIonMobilityValuesResolvingPower;
        private CheckBox cbUseSpectralLibraryIonMobilityValues;
        private Label labelResolvingPower;
    }
}
