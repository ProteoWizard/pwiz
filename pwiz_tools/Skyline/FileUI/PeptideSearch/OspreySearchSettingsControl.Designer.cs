namespace pwiz.Skyline.FileUI.PeptideSearch
{
    partial class OspreySearchSettingsControl
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(OspreySearchSettingsControl));
            this.lblResolution = new System.Windows.Forms.Label();
            this.comboResolution = new System.Windows.Forms.ComboBox();
            this.lblFragmentTolerance = new System.Windows.Forms.Label();
            this.numFragmentTolerance = new System.Windows.Forms.NumericUpDown();
            this.comboFragmentUnit = new System.Windows.Forms.ComboBox();
            this.lblRunFdr = new System.Windows.Forms.Label();
            this.txtRunFdr = new System.Windows.Forms.TextBox();
            this.lblExperimentFdr = new System.Windows.Forms.Label();
            this.txtExperimentFdr = new System.Windows.Forms.TextBox();
            this.lblProteinFdr = new System.Windows.Forms.Label();
            this.txtProteinFdr = new System.Windows.Forms.TextBox();
            this.lblThreads = new System.Windows.Forms.Label();
            this.numThreads = new System.Windows.Forms.NumericUpDown();
            ((System.ComponentModel.ISupportInitialize)(this.numFragmentTolerance)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numThreads)).BeginInit();
            this.SuspendLayout();
            //
            // lblResolution
            //
            resources.ApplyResources(this.lblResolution, "lblResolution");
            this.lblResolution.Name = "lblResolution";
            //
            // comboResolution
            //
            resources.ApplyResources(this.comboResolution, "comboResolution");
            this.comboResolution.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboResolution.FormattingEnabled = true;
            this.comboResolution.Name = "comboResolution";
            this.comboResolution.SelectedIndexChanged += new System.EventHandler(this.comboResolution_SelectedIndexChanged);
            //
            // lblFragmentTolerance
            //
            resources.ApplyResources(this.lblFragmentTolerance, "lblFragmentTolerance");
            this.lblFragmentTolerance.Name = "lblFragmentTolerance";
            //
            // numFragmentTolerance
            //
            resources.ApplyResources(this.numFragmentTolerance, "numFragmentTolerance");
            this.numFragmentTolerance.DecimalPlaces = 2;
            this.numFragmentTolerance.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
            this.numFragmentTolerance.Minimum = new decimal(new int[] { 1, 0, 0, 131072 });
            this.numFragmentTolerance.Name = "numFragmentTolerance";
            this.numFragmentTolerance.Value = new decimal(new int[] { 10, 0, 0, 0 });
            //
            // comboFragmentUnit
            //
            resources.ApplyResources(this.comboFragmentUnit, "comboFragmentUnit");
            this.comboFragmentUnit.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboFragmentUnit.FormattingEnabled = true;
            this.comboFragmentUnit.Name = "comboFragmentUnit";
            //
            // lblRunFdr
            //
            resources.ApplyResources(this.lblRunFdr, "lblRunFdr");
            this.lblRunFdr.Name = "lblRunFdr";
            //
            // txtRunFdr
            //
            resources.ApplyResources(this.txtRunFdr, "txtRunFdr");
            this.txtRunFdr.Name = "txtRunFdr";
            //
            // lblExperimentFdr
            //
            resources.ApplyResources(this.lblExperimentFdr, "lblExperimentFdr");
            this.lblExperimentFdr.Name = "lblExperimentFdr";
            //
            // txtExperimentFdr
            //
            resources.ApplyResources(this.txtExperimentFdr, "txtExperimentFdr");
            this.txtExperimentFdr.Name = "txtExperimentFdr";
            //
            // lblProteinFdr
            //
            resources.ApplyResources(this.lblProteinFdr, "lblProteinFdr");
            this.lblProteinFdr.Name = "lblProteinFdr";
            //
            // txtProteinFdr
            //
            resources.ApplyResources(this.txtProteinFdr, "txtProteinFdr");
            this.txtProteinFdr.Name = "txtProteinFdr";
            //
            // lblThreads
            //
            resources.ApplyResources(this.lblThreads, "lblThreads");
            this.lblThreads.Name = "lblThreads";
            //
            // numThreads
            //
            resources.ApplyResources(this.numThreads, "numThreads");
            this.numThreads.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numThreads.Maximum = new decimal(new int[] { 128, 0, 0, 0 });
            this.numThreads.Value = new decimal(new int[] { 1, 0, 0, 0 });
            this.numThreads.Name = "numThreads";
            //
            // OspreySearchSettingsControl
            //
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.numThreads);
            this.Controls.Add(this.lblThreads);
            this.Controls.Add(this.txtProteinFdr);
            this.Controls.Add(this.lblProteinFdr);
            this.Controls.Add(this.txtExperimentFdr);
            this.Controls.Add(this.lblExperimentFdr);
            this.Controls.Add(this.txtRunFdr);
            this.Controls.Add(this.lblRunFdr);
            this.Controls.Add(this.comboFragmentUnit);
            this.Controls.Add(this.numFragmentTolerance);
            this.Controls.Add(this.lblFragmentTolerance);
            this.Controls.Add(this.comboResolution);
            this.Controls.Add(this.lblResolution);
            this.Name = "OspreySearchSettingsControl";
            ((System.ComponentModel.ISupportInitialize)(this.numFragmentTolerance)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numThreads)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblResolution;
        private System.Windows.Forms.ComboBox comboResolution;
        private System.Windows.Forms.Label lblFragmentTolerance;
        private System.Windows.Forms.NumericUpDown numFragmentTolerance;
        private System.Windows.Forms.ComboBox comboFragmentUnit;
        private System.Windows.Forms.Label lblRunFdr;
        private System.Windows.Forms.TextBox txtRunFdr;
        private System.Windows.Forms.Label lblExperimentFdr;
        private System.Windows.Forms.TextBox txtExperimentFdr;
        private System.Windows.Forms.Label lblProteinFdr;
        private System.Windows.Forms.TextBox txtProteinFdr;
        private System.Windows.Forms.Label lblThreads;
        private System.Windows.Forms.NumericUpDown numThreads;
    }
}
