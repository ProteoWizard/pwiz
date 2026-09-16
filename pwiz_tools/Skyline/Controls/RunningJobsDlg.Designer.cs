namespace pwiz.Skyline.Controls
{
    partial class RunningJobsDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RunningJobsDlg));
            this.listJobs = new System.Windows.Forms.ListView();
            this.colDescription = new System.Windows.Forms.ColumnHeader();
            this.colMessage = new System.Windows.Forms.ColumnHeader();
            this.colProgress = new System.Windows.Forms.ColumnHeader();
            this.btnCancelJob = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.timerRefresh = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            //
            // listJobs
            //
            resources.ApplyResources(this.listJobs, "listJobs");
            this.listJobs.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colDescription,
            this.colMessage,
            this.colProgress});
            this.listJobs.FullRowSelect = true;
            this.listJobs.HideSelection = false;
            this.listJobs.MultiSelect = false;
            this.listJobs.Name = "listJobs";
            this.listJobs.UseCompatibleStateImageBehavior = false;
            this.listJobs.View = System.Windows.Forms.View.Details;
            this.listJobs.SelectedIndexChanged += new System.EventHandler(this.listJobs_SelectedIndexChanged);
            //
            // colDescription
            //
            resources.ApplyResources(this.colDescription, "colDescription");
            //
            // colMessage
            //
            resources.ApplyResources(this.colMessage, "colMessage");
            //
            // colProgress
            //
            resources.ApplyResources(this.colProgress, "colProgress");
            //
            // btnCancelJob
            //
            resources.ApplyResources(this.btnCancelJob, "btnCancelJob");
            this.btnCancelJob.Name = "btnCancelJob";
            this.btnCancelJob.UseVisualStyleBackColor = true;
            this.btnCancelJob.Click += new System.EventHandler(this.btnCancelJob_Click);
            //
            // btnClose
            //
            resources.ApplyResources(this.btnClose, "btnClose");
            this.btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnClose.Name = "btnClose";
            this.btnClose.UseVisualStyleBackColor = true;
            //
            // timerRefresh
            //
            this.timerRefresh.Enabled = true;
            this.timerRefresh.Interval = 500;
            this.timerRefresh.Tick += new System.EventHandler(this.timerRefresh_Tick);
            //
            // RunningJobsDlg
            //
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnClose;
            this.Controls.Add(this.listJobs);
            this.Controls.Add(this.btnCancelJob);
            this.Controls.Add(this.btnClose);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "RunningJobsDlg";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListView listJobs;
        private System.Windows.Forms.ColumnHeader colDescription;
        private System.Windows.Forms.ColumnHeader colMessage;
        private System.Windows.Forms.ColumnHeader colProgress;
        private System.Windows.Forms.Button btnCancelJob;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Timer timerRefresh;
    }
}
