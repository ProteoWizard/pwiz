namespace pwiz.Skyline.SettingsUI
{
    partial class EditCustomMoleculeDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditCustomMoleculeDlg));
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.groupBoxOptionalValues = new System.Windows.Forms.GroupBox();
            this.textDriftTimeMsec = new System.Windows.Forms.TextBox();
            this.labelDriftTimeMsec = new System.Windows.Forms.Label();
            this.textDriftTimeHighEnergyOffsetMsec = new System.Windows.Forms.TextBox();
            this.labelDriftTimeHighEnergyOffsetMsec = new System.Windows.Forms.Label();
            this.textRetentionTime = new System.Windows.Forms.TextBox();
            this.labelRetentionTime = new System.Windows.Forms.Label();
            this.textRetentionTimeWindow = new System.Windows.Forms.TextBox();
            this.labelRetentionTimeWindow = new System.Windows.Forms.Label();
            this.labelCollisionEnergy = new System.Windows.Forms.Label();
            this.textCollisionEnergy = new System.Windows.Forms.TextBox();
            this.textCharge = new System.Windows.Forms.TextBox();
            this.labelCharge = new System.Windows.Forms.Label();
            this.textName = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.groupBoxOptionalValues.SuspendLayout();
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
            // groupBoxOptionalValues
            // 
            this.groupBoxOptionalValues.Controls.Add(this.textDriftTimeMsec);
            this.groupBoxOptionalValues.Controls.Add(this.labelDriftTimeMsec);
            this.groupBoxOptionalValues.Controls.Add(this.textDriftTimeHighEnergyOffsetMsec);
            this.groupBoxOptionalValues.Controls.Add(this.labelDriftTimeHighEnergyOffsetMsec);
            this.groupBoxOptionalValues.Controls.Add(this.textRetentionTime);
            this.groupBoxOptionalValues.Controls.Add(this.labelRetentionTime);
            this.groupBoxOptionalValues.Controls.Add(this.textRetentionTimeWindow);
            this.groupBoxOptionalValues.Controls.Add(this.labelRetentionTimeWindow);
            this.groupBoxOptionalValues.Controls.Add(this.labelCollisionEnergy);
            this.groupBoxOptionalValues.Controls.Add(this.textCollisionEnergy);
            resources.ApplyResources(this.groupBoxOptionalValues, "groupBoxOptionalValues");
            this.groupBoxOptionalValues.Name = "groupBoxOptionalValues";
            this.groupBoxOptionalValues.TabStop = false;
            // 
            // textDriftTimeMsec
            // 
            resources.ApplyResources(this.textDriftTimeMsec, "textDriftTimeMsec");
            this.textDriftTimeMsec.Name = "textDriftTimeMsec";
            this.toolTip1.SetToolTip(this.textDriftTimeMsec, resources.GetString("textDriftTimeMsec.ToolTip"));
            // 
            // labelDriftTimeMsec
            // 
            resources.ApplyResources(this.labelDriftTimeMsec, "labelDriftTimeMsec");
            this.labelDriftTimeMsec.Name = "labelDriftTimeMsec";
            // 
            // textDriftTimeHighEnergyOffsetMsec
            // 
            resources.ApplyResources(this.textDriftTimeHighEnergyOffsetMsec, "textDriftTimeHighEnergyOffsetMsec");
            this.textDriftTimeHighEnergyOffsetMsec.Name = "textDriftTimeHighEnergyOffsetMsec";
            this.toolTip1.SetToolTip(this.textDriftTimeHighEnergyOffsetMsec, resources.GetString("textDriftTimeHighEnergyOffsetMsec.ToolTip"));
            // 
            // labelDriftTimeHighEnergyOffsetMsec
            // 
            resources.ApplyResources(this.labelDriftTimeHighEnergyOffsetMsec, "labelDriftTimeHighEnergyOffsetMsec");
            this.labelDriftTimeHighEnergyOffsetMsec.Name = "labelDriftTimeHighEnergyOffsetMsec";
            // 
            // textRetentionTime
            // 
            resources.ApplyResources(this.textRetentionTime, "textRetentionTime");
            this.textRetentionTime.Name = "textRetentionTime";
            this.toolTip1.SetToolTip(this.textRetentionTime, resources.GetString("textRetentionTime.ToolTip"));
            // 
            // labelRetentionTime
            // 
            resources.ApplyResources(this.labelRetentionTime, "labelRetentionTime");
            this.labelRetentionTime.Name = "labelRetentionTime";
            // 
            // textRetentionTimeWindow
            // 
            resources.ApplyResources(this.textRetentionTimeWindow, "textRetentionTimeWindow");
            this.textRetentionTimeWindow.Name = "textRetentionTimeWindow";
            this.toolTip1.SetToolTip(this.textRetentionTimeWindow, resources.GetString("textRetentionTimeWindow.ToolTip"));
            // 
            // labelRetentionTimeWindow
            // 
            resources.ApplyResources(this.labelRetentionTimeWindow, "labelRetentionTimeWindow");
            this.labelRetentionTimeWindow.Name = "labelRetentionTimeWindow";
            // 
            // labelCollisionEnergy
            // 
            resources.ApplyResources(this.labelCollisionEnergy, "labelCollisionEnergy");
            this.labelCollisionEnergy.Name = "labelCollisionEnergy";
            // 
            // textCollisionEnergy
            // 
            resources.ApplyResources(this.textCollisionEnergy, "textCollisionEnergy");
            this.textCollisionEnergy.Name = "textCollisionEnergy";
            this.toolTip1.SetToolTip(this.textCollisionEnergy, resources.GetString("textCollisionEnergy.ToolTip"));
            // 
            // textCharge
            // 
            resources.ApplyResources(this.textCharge, "textCharge");
            this.textCharge.Name = "textCharge";
            this.textCharge.TextChanged += new System.EventHandler(this.textCharge_TextChanged);
            // 
            // labelCharge
            // 
            resources.ApplyResources(this.labelCharge, "labelCharge");
            this.labelCharge.Name = "labelCharge";
            // 
            // textName
            // 
            resources.ApplyResources(this.textName, "textName");
            this.textName.Name = "textName";
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // EditCustomMoleculeDlg
            // 
            this.AcceptButton = this.btnOK;
            this.CancelButton = this.btnCancel;
            resources.ApplyResources(this, "$this");
            this.Controls.Add(this.groupBoxOptionalValues);
            this.Controls.Add(this.textCharge);
            this.Controls.Add(this.labelCharge);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.textName);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditCustomMoleculeDlg";
            this.ShowInTaskbar = false;
            this.groupBoxOptionalValues.ResumeLayout(false);
            this.groupBoxOptionalValues.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label labelCharge;
        private System.Windows.Forms.TextBox textCharge;
        private System.Windows.Forms.Label labelCollisionEnergy;
        private System.Windows.Forms.TextBox textCollisionEnergy;
        private System.Windows.Forms.GroupBox groupBoxOptionalValues;
        private System.Windows.Forms.TextBox textRetentionTime;
        private System.Windows.Forms.Label labelRetentionTime;
        private System.Windows.Forms.TextBox textRetentionTimeWindow;
        private System.Windows.Forms.Label labelRetentionTimeWindow;
        private System.Windows.Forms.TextBox textDriftTimeMsec;
        private System.Windows.Forms.Label labelDriftTimeMsec;
        private System.Windows.Forms.TextBox textDriftTimeHighEnergyOffsetMsec;
        private System.Windows.Forms.Label labelDriftTimeHighEnergyOffsetMsec;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}
