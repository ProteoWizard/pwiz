namespace pwiz.Skyline.FileUI.PeptideSearch
{
    partial class TransitionSettingsControl
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TransitionSettingsControl));
            this.lblPrecursorCharges = new System.Windows.Forms.Label();
            this.txtPrecursorCharges = new System.Windows.Forms.TextBox();
            this.lblIonCharges = new System.Windows.Forms.Label();
            this.txtIonCharges = new System.Windows.Forms.TextBox();
            this.lblIonTypes = new System.Windows.Forms.Label();
            this.txtIonTypes = new System.Windows.Forms.TextBox();
            this.lblTolerance = new System.Windows.Forms.Label();
            this.txtTolerance = new System.Windows.Forms.TextBox();
            this.lblToleranceUnits = new System.Windows.Forms.Label();
            this.lblIonCount = new System.Windows.Forms.Label();
            this.lblIonCountUnits = new System.Windows.Forms.Label();
            this.txtIonCount = new System.Windows.Forms.TextBox();
            this.cbExclusionUseDIAWindow = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // lblPrecursorCharges
            // 
            resources.ApplyResources(this.lblPrecursorCharges, "lblPrecursorCharges");
            this.lblPrecursorCharges.Name = "lblPrecursorCharges";
            // 
            // txtPrecursorCharges
            // 
            resources.ApplyResources(this.txtPrecursorCharges, "txtPrecursorCharges");
            this.txtPrecursorCharges.Name = "txtPrecursorCharges";
            // 
            // lblIonCharges
            // 
            resources.ApplyResources(this.lblIonCharges, "lblIonCharges");
            this.lblIonCharges.Name = "lblIonCharges";
            // 
            // txtIonCharges
            // 
            resources.ApplyResources(this.txtIonCharges, "txtIonCharges");
            this.txtIonCharges.Name = "txtIonCharges";
            // 
            // lblIonTypes
            // 
            resources.ApplyResources(this.lblIonTypes, "lblIonTypes");
            this.lblIonTypes.Name = "lblIonTypes";
            // 
            // txtIonTypes
            // 
            resources.ApplyResources(this.txtIonTypes, "txtIonTypes");
            this.txtIonTypes.Name = "txtIonTypes";
            // 
            // lblTolerance
            // 
            resources.ApplyResources(this.lblTolerance, "lblTolerance");
            this.lblTolerance.Name = "lblTolerance";
            // 
            // txtTolerance
            // 
            resources.ApplyResources(this.txtTolerance, "txtTolerance");
            this.txtTolerance.Name = "txtTolerance";
            // 
            // lblToleranceUnits
            // 
            resources.ApplyResources(this.lblToleranceUnits, "lblToleranceUnits");
            this.lblToleranceUnits.Name = "lblToleranceUnits";
            // 
            // lblIonCount
            // 
            resources.ApplyResources(this.lblIonCount, "lblIonCount");
            this.lblIonCount.Name = "lblIonCount";
            // 
            // lblIonCountUnits
            // 
            resources.ApplyResources(this.lblIonCountUnits, "lblIonCountUnits");
            this.lblIonCountUnits.Name = "lblIonCountUnits";
            // 
            // txtIonCount
            // 
            resources.ApplyResources(this.txtIonCount, "txtIonCount");
            this.txtIonCount.Name = "txtIonCount";
            // 
            // cbExclusionUseDIAWindow
            // 
            resources.ApplyResources(this.cbExclusionUseDIAWindow, "cbExclusionUseDIAWindow");
            this.cbExclusionUseDIAWindow.Name = "cbExclusionUseDIAWindow";
            this.cbExclusionUseDIAWindow.UseVisualStyleBackColor = true;
            // 
            // TransitionSettingsControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Controls.Add(this.cbExclusionUseDIAWindow);
            this.Controls.Add(this.lblTolerance);
            this.Controls.Add(this.txtTolerance);
            this.Controls.Add(this.lblToleranceUnits);
            this.Controls.Add(this.lblIonCount);
            this.Controls.Add(this.lblIonCountUnits);
            this.Controls.Add(this.txtIonCount);
            this.Controls.Add(this.txtIonTypes);
            this.Controls.Add(this.lblIonTypes);
            this.Controls.Add(this.txtIonCharges);
            this.Controls.Add(this.lblIonCharges);
            this.Controls.Add(this.txtPrecursorCharges);
            this.Controls.Add(this.lblPrecursorCharges);
            this.Name = "TransitionSettingsControl";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtPrecursorCharges;
        private System.Windows.Forms.Label lblPrecursorCharges;
        private System.Windows.Forms.Label lblIonCharges;
        private System.Windows.Forms.TextBox txtIonCharges;
        private System.Windows.Forms.Label lblIonTypes;
        private System.Windows.Forms.TextBox txtIonTypes;
        private System.Windows.Forms.Label lblTolerance;
        private System.Windows.Forms.TextBox txtTolerance;
        private System.Windows.Forms.Label lblToleranceUnits;
        private System.Windows.Forms.Label lblIonCount;
        private System.Windows.Forms.Label lblIonCountUnits;
        private System.Windows.Forms.TextBox txtIonCount;
        private System.Windows.Forms.CheckBox cbExclusionUseDIAWindow;
    }
}
