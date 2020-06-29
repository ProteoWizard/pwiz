using System.Windows.Forms;

namespace pwiz.Skyline.SettingsUI.IonMobility
{
    partial class IonMobilityFilteringUserControl
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(IonMobilityFilteringUserControl));
            this.cbUseSpectralLibraryIonMobilities = new System.Windows.Forms.CheckBox();
            this.comboIonMobilityLibrary = new System.Windows.Forms.ComboBox();
            this.labelIonMobilityLibrary = new System.Windows.Forms.Label();
            this.groupBoxIonMobilityFiltering = new System.Windows.Forms.GroupBox();
            this.textIonMobilityFilterFixedWidth = new System.Windows.Forms.TextBox();
            this.labelFixedWidth = new System.Windows.Forms.Label();
            this.comboBoxWindowType = new System.Windows.Forms.ComboBox();
            this.labelWindowType = new System.Windows.Forms.Label();
            this.textIonMobilityFilterWidthAtMobilityMax = new System.Windows.Forms.TextBox();
            this.textIonMobilityFilterWidthAtMobility0 = new System.Windows.Forms.TextBox();
            this.labelWidthAtMobilityMax = new System.Windows.Forms.Label();
            this.labelResolvingPower = new System.Windows.Forms.Label();
            this.labelWidthAtMobilityZero = new System.Windows.Forms.Label();
            this.textIonMobilityFilterResolvingPower = new System.Windows.Forms.TextBox();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.groupBoxIonMobilityFiltering.SuspendLayout();
            this.SuspendLayout();
            // 
            // cbUseSpectralLibraryIonMobilities
            // 
            resources.ApplyResources(this.cbUseSpectralLibraryIonMobilities, "cbUseSpectralLibraryIonMobilities");
            this.cbUseSpectralLibraryIonMobilities.Name = "cbUseSpectralLibraryIonMobilities";
            this.toolTip1.SetToolTip(this.cbUseSpectralLibraryIonMobilities, resources.GetString("cbUseSpectralLibraryIonMobilities.ToolTip"));
            this.cbUseSpectralLibraryIonMobilities.UseVisualStyleBackColor = true;
            this.cbUseSpectralLibraryIonMobilities.CheckedChanged += new System.EventHandler(this.cbUseSpectralLibraryIonMobilities_CheckChanged);
            // 
            // comboIonMobilityLibrary
            // 
            this.comboIonMobilityLibrary.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboIonMobilityLibrary.FormattingEnabled = true;
            resources.ApplyResources(this.comboIonMobilityLibrary, "comboIonMobilityLibrary");
            this.comboIonMobilityLibrary.Name = "comboIonMobilityLibrary";
            this.toolTip1.SetToolTip(this.comboIonMobilityLibrary, resources.GetString("comboIonMobilityLibrary.ToolTip"));
            this.comboIonMobilityLibrary.SelectedIndexChanged += new System.EventHandler(this.comboIonMobilityLibrary_SelectedIndexChanged);
            // 
            // labelIonMobilityLibrary
            // 
            resources.ApplyResources(this.labelIonMobilityLibrary, "labelIonMobilityLibrary");
            this.labelIonMobilityLibrary.Name = "labelIonMobilityLibrary";
            this.toolTip1.SetToolTip(this.labelIonMobilityLibrary, resources.GetString("labelIonMobilityLibrary.ToolTip"));
            // 
            // groupBoxIonMobilityFiltering
            // 
            resources.ApplyResources(this.groupBoxIonMobilityFiltering, "groupBoxIonMobilityFiltering");
            this.groupBoxIonMobilityFiltering.Controls.Add(this.textIonMobilityFilterFixedWidth);
            this.groupBoxIonMobilityFiltering.Controls.Add(this.labelFixedWidth);
            this.groupBoxIonMobilityFiltering.Controls.Add(this.comboBoxWindowType);
            this.groupBoxIonMobilityFiltering.Controls.Add(this.labelWindowType);
            this.groupBoxIonMobilityFiltering.Controls.Add(this.comboIonMobilityLibrary);
            this.groupBoxIonMobilityFiltering.Controls.Add(this.textIonMobilityFilterWidthAtMobilityMax);
            this.groupBoxIonMobilityFiltering.Controls.Add(this.labelIonMobilityLibrary);
            this.groupBoxIonMobilityFiltering.Controls.Add(this.textIonMobilityFilterWidthAtMobility0);
            this.groupBoxIonMobilityFiltering.Controls.Add(this.labelWidthAtMobilityMax);
            this.groupBoxIonMobilityFiltering.Controls.Add(this.labelResolvingPower);
            this.groupBoxIonMobilityFiltering.Controls.Add(this.labelWidthAtMobilityZero);
            this.groupBoxIonMobilityFiltering.Controls.Add(this.cbUseSpectralLibraryIonMobilities);
            this.groupBoxIonMobilityFiltering.Controls.Add(this.textIonMobilityFilterResolvingPower);
            this.groupBoxIonMobilityFiltering.Name = "groupBoxIonMobilityFiltering";
            this.groupBoxIonMobilityFiltering.TabStop = false;
            // 
            // textIonMobilityFilterFixedWidth
            // 
            resources.ApplyResources(this.textIonMobilityFilterFixedWidth, "textIonMobilityFilterFixedWidth");
            this.textIonMobilityFilterFixedWidth.Name = "textIonMobilityFilterFixedWidth";
            // 
            // labelFixedWidth
            // 
            resources.ApplyResources(this.labelFixedWidth, "labelFixedWidth");
            this.labelFixedWidth.Name = "labelFixedWidth";
            // 
            // comboBoxWindowType
            // 
            this.comboBoxWindowType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxWindowType.FormattingEnabled = true;
            this.comboBoxWindowType.Items.AddRange(new object[] {
            resources.GetString("comboBoxWindowType.Items"),
            resources.GetString("comboBoxWindowType.Items1"),
            resources.GetString("comboBoxWindowType.Items2"),
            resources.GetString("comboBoxWindowType.Items3")});
            resources.ApplyResources(this.comboBoxWindowType, "comboBoxWindowType");
            this.comboBoxWindowType.Name = "comboBoxWindowType";
            this.toolTip1.SetToolTip(this.comboBoxWindowType, resources.GetString("comboBoxWindowType.ToolTip"));
            this.comboBoxWindowType.SelectedIndexChanged += new System.EventHandler(this.comboBoxWindowType_SelectedIndexChanged);
            // 
            // labelWindowType
            // 
            resources.ApplyResources(this.labelWindowType, "labelWindowType");
            this.labelWindowType.Name = "labelWindowType";
            this.toolTip1.SetToolTip(this.labelWindowType, resources.GetString("labelWindowType.ToolTip"));
            // 
            // textIonMobilityFilterWidthAtMobilityMax
            // 
            resources.ApplyResources(this.textIonMobilityFilterWidthAtMobilityMax, "textIonMobilityFilterWidthAtMobilityMax");
            this.textIonMobilityFilterWidthAtMobilityMax.Name = "textIonMobilityFilterWidthAtMobilityMax";
            this.toolTip1.SetToolTip(this.textIonMobilityFilterWidthAtMobilityMax, resources.GetString("textIonMobilityFilterWidthAtMobilityMax.ToolTip"));
            // 
            // textIonMobilityFilterWidthAtMobility0
            // 
            resources.ApplyResources(this.textIonMobilityFilterWidthAtMobility0, "textIonMobilityFilterWidthAtMobility0");
            this.textIonMobilityFilterWidthAtMobility0.Name = "textIonMobilityFilterWidthAtMobility0";
            this.toolTip1.SetToolTip(this.textIonMobilityFilterWidthAtMobility0, resources.GetString("textIonMobilityFilterWidthAtMobility0.ToolTip"));
            // 
            // labelWidthAtMobilityMax
            // 
            resources.ApplyResources(this.labelWidthAtMobilityMax, "labelWidthAtMobilityMax");
            this.labelWidthAtMobilityMax.Name = "labelWidthAtMobilityMax";
            // 
            // labelResolvingPower
            // 
            resources.ApplyResources(this.labelResolvingPower, "labelResolvingPower");
            this.labelResolvingPower.Name = "labelResolvingPower";
            // 
            // labelWidthAtMobilityZero
            // 
            resources.ApplyResources(this.labelWidthAtMobilityZero, "labelWidthAtMobilityZero");
            this.labelWidthAtMobilityZero.Name = "labelWidthAtMobilityZero";
            // 
            // textIonMobilityFilterResolvingPower
            // 
            resources.ApplyResources(this.textIonMobilityFilterResolvingPower, "textIonMobilityFilterResolvingPower");
            this.textIonMobilityFilterResolvingPower.Name = "textIonMobilityFilterResolvingPower";
            this.toolTip1.SetToolTip(this.textIonMobilityFilterResolvingPower, resources.GetString("textIonMobilityFilterResolvingPower.ToolTip"));
            // 
            // IonMobilityFilteringUserControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.groupBoxIonMobilityFiltering);
            this.Name = "IonMobilityFilteringUserControl";
            this.groupBoxIonMobilityFiltering.ResumeLayout(false);
            this.groupBoxIonMobilityFiltering.PerformLayout();
            this.ResumeLayout(false);

        }


        #endregion
        private System.Windows.Forms.CheckBox cbUseSpectralLibraryIonMobilities;
        private System.Windows.Forms.ComboBox comboIonMobilityLibrary;
        private System.Windows.Forms.Label labelIonMobilityLibrary;
        private GroupBox groupBoxIonMobilityFiltering;
        private ToolTip toolTip1;
        private TextBox textIonMobilityFilterWidthAtMobilityMax;
        private TextBox textIonMobilityFilterWidthAtMobility0;
        private Label labelWidthAtMobilityMax;
        private Label labelResolvingPower;
        private Label labelWidthAtMobilityZero;
        private TextBox textIonMobilityFilterResolvingPower;
        private TextBox textIonMobilityFilterFixedWidth;
        private Label labelFixedWidth;
        private ComboBox comboBoxWindowType;
        private Label labelWindowType;
    }
}
