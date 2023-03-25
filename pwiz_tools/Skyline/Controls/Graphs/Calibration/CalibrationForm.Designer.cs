namespace pwiz.Skyline.Controls.Graphs.Calibration
{
    partial class CalibrationForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CalibrationForm));
            this.calibrationGraphControl1 = new pwiz.Skyline.Controls.Graphs.Calibration.CalibrationGraphControl();
            this.SuspendLayout();
            // 
            // calibrationGraphControl1
            // 
            resources.ApplyResources(this.calibrationGraphControl1, "calibrationGraphControl1");
            this.calibrationGraphControl1.ModeUIAwareFormHelper = null;
            this.calibrationGraphControl1.Name = "calibrationGraphControl1";
            this.calibrationGraphControl1.Options = null;
            this.calibrationGraphControl1.PointClicked += new System.Action<pwiz.Skyline.Model.DocSettings.AbsoluteQuantification.CalibrationPoint>(this.calibrationGraphControl1_PointClicked);
            // 
            // CalibrationForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.calibrationGraphControl1);
            this.KeyPreview = true;
            this.Name = "CalibrationForm";
            this.ShowInTaskbar = false;
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.CalibrationForm_KeyDown);
            this.ResumeLayout(false);

        }

        #endregion
        private CalibrationGraphControl calibrationGraphControl1;
    }
}