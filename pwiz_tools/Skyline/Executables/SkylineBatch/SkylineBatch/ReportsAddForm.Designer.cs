namespace SkylineBatch
{
    partial class ReportsAddForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ReportsAddForm));
            this.textReportName = new System.Windows.Forms.TextBox();
            this.labelConfigName = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.textReportPath = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.boxRScripts = new System.Windows.Forms.ListBox();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnAddRScript = new System.Windows.Forms.Button();
            this.btnRemove = new System.Windows.Forms.Button();
            this.btnReportPath = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // textReportName
            // 
            resources.ApplyResources(this.textReportName, "textReportName");
            this.textReportName.Name = "textReportName";
            // 
            // labelConfigName
            // 
            resources.ApplyResources(this.labelConfigName, "labelConfigName");
            this.labelConfigName.Name = "labelConfigName";
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // textReportPath
            // 
            resources.ApplyResources(this.textReportPath, "textReportPath");
            this.textReportPath.Name = "textReportPath";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // boxRScripts
            // 
            resources.ApplyResources(this.boxRScripts, "boxRScripts");
            this.boxRScripts.FormattingEnabled = true;
            this.boxRScripts.Name = "boxRScripts";
            this.boxRScripts.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.boxRScripts.SelectedIndexChanged += new System.EventHandler(this.boxRScripts_SelectedIndexChanged);
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnAddRScript
            // 
            resources.ApplyResources(this.btnAddRScript, "btnAddRScript");
            this.btnAddRScript.Name = "btnAddRScript";
            this.btnAddRScript.UseVisualStyleBackColor = true;
            this.btnAddRScript.Click += new System.EventHandler(this.btnAddRScript_Click);
            // 
            // btnRemove
            // 
            resources.ApplyResources(this.btnRemove, "btnRemove");
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.UseVisualStyleBackColor = true;
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            // 
            // btnReportPath
            // 
            resources.ApplyResources(this.btnReportPath, "btnReportPath");
            this.btnReportPath.Name = "btnReportPath";
            this.btnReportPath.UseVisualStyleBackColor = true;
            this.btnReportPath.Click += new System.EventHandler(this.btnReportPath_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // ReportsAddForm
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnReportPath);
            this.Controls.Add(this.btnRemove);
            this.Controls.Add(this.btnAddRScript);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.boxRScripts);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.textReportPath);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textReportName);
            this.Controls.Add(this.labelConfigName);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ReportsAddForm";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textReportName;
        private System.Windows.Forms.Label labelConfigName;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textReportPath;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ListBox boxRScripts;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnAddRScript;
        private System.Windows.Forms.Button btnRemove;
        private System.Windows.Forms.Button btnReportPath;
        private System.Windows.Forms.Button btnCancel;
    }
}