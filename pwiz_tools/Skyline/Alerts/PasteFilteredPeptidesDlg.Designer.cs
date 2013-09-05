namespace pwiz.Skyline.Alerts
{
    partial class PasteFilteredPeptidesDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PasteFilteredPeptidesDlg));
            this.btnFilter = new System.Windows.Forms.Button();
            this.btnKeep = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.labelIssue = new System.Windows.Forms.Label();
            this.labelQuestion = new System.Windows.Forms.Label();
            this.panelList = new System.Windows.Forms.Panel();
            this.labelList = new System.Windows.Forms.Label();
            this.panelList.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnFilter
            // 
            resources.ApplyResources(this.btnFilter, "btnFilter");
            this.btnFilter.Name = "btnFilter";
            this.btnFilter.UseVisualStyleBackColor = true;
            this.btnFilter.Click += new System.EventHandler(this.btnFilter_Click);
            // 
            // btnKeep
            // 
            resources.ApplyResources(this.btnKeep, "btnKeep");
            this.btnKeep.Name = "btnKeep";
            this.btnKeep.UseVisualStyleBackColor = true;
            this.btnKeep.Click += new System.EventHandler(this.btnKeep_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // labelIssue
            // 
            resources.ApplyResources(this.labelIssue, "labelIssue");
            this.labelIssue.Name = "labelIssue";
            // 
            // labelQuestion
            // 
            resources.ApplyResources(this.labelQuestion, "labelQuestion");
            this.labelQuestion.Name = "labelQuestion";
            // 
            // panelList
            // 
            resources.ApplyResources(this.panelList, "panelList");
            this.panelList.Controls.Add(this.labelList);
            this.panelList.Name = "panelList";
            // 
            // labelList
            // 
            resources.ApplyResources(this.labelList, "labelList");
            this.labelList.Name = "labelList";
            // 
            // PasteFilteredPeptidesDlg
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.panelList);
            this.Controls.Add(this.labelQuestion);
            this.Controls.Add(this.labelIssue);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnKeep);
            this.Controls.Add(this.btnFilter);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PasteFilteredPeptidesDlg";
            this.ShowInTaskbar = false;
            this.panelList.ResumeLayout(false);
            this.panelList.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnFilter;
        private System.Windows.Forms.Button btnKeep;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label labelIssue;
        private System.Windows.Forms.Label labelQuestion;
        private System.Windows.Forms.Panel panelList;
        private System.Windows.Forms.Label labelList;
    }
}