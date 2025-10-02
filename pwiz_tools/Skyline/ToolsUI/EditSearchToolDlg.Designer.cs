namespace pwiz.Skyline.ToolsUI
{
    partial class EditSearchToolDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditSearchToolDlg));
            this.toolTipAnonymous = new System.Windows.Forms.ToolTip(this.components);
            this.comboToolName = new System.Windows.Forms.ComboBox();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.lblToolName = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.tbExtraArgs = new System.Windows.Forms.TextBox();
            this.lblExtraArgs = new System.Windows.Forms.Label();
            this.lblPath = new System.Windows.Forms.Label();
            this.tbPath = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // comboToolName
            // 
            this.comboToolName.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboToolName.FormattingEnabled = true;
            resources.ApplyResources(this.comboToolName, "comboToolName");
            this.comboToolName.Name = "comboToolName";
            this.toolTipAnonymous.SetToolTip(this.comboToolName, resources.GetString("comboToolName.ToolTip"));
            // 
            // btnBrowse
            // 
            resources.ApplyResources(this.btnBrowse, "btnBrowse");
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // lblToolName
            // 
            resources.ApplyResources(this.lblToolName, "lblToolName");
            this.lblToolName.Name = "lblToolName";
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            resources.ApplyResources(this.btnOK, "btnOK");
            this.btnOK.Name = "btnOK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // tbExtraArgs
            // 
            resources.ApplyResources(this.tbExtraArgs, "tbExtraArgs");
            this.tbExtraArgs.Name = "tbExtraArgs";
            // 
            // lblExtraArgs
            // 
            resources.ApplyResources(this.lblExtraArgs, "lblExtraArgs");
            this.lblExtraArgs.Name = "lblExtraArgs";
            // 
            // lblPath
            // 
            resources.ApplyResources(this.lblPath, "lblPath");
            this.lblPath.Name = "lblPath";
            // 
            // tbPath
            // 
            resources.ApplyResources(this.tbPath, "tbPath");
            this.tbPath.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.tbPath.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystem;
            this.tbPath.Name = "tbPath";
            this.tbPath.TextChanged += new System.EventHandler(this.tbPath_TextChanged);
            // 
            // EditSearchToolDlg
            // 
            this.AcceptButton = this.btnOK;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnBrowse);
            this.Controls.Add(this.comboToolName);
            this.Controls.Add(this.lblToolName);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.tbExtraArgs);
            this.Controls.Add(this.lblExtraArgs);
            this.Controls.Add(this.lblPath);
            this.Controls.Add(this.tbPath);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditSearchToolDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ToolTip toolTipAnonymous;
        private System.Windows.Forms.ComboBox comboToolName;
        private System.Windows.Forms.Label lblToolName;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        internal System.Windows.Forms.TextBox tbExtraArgs;
        private System.Windows.Forms.Label lblExtraArgs;
        private System.Windows.Forms.Label lblPath;
        internal System.Windows.Forms.TextBox tbPath;
        private System.Windows.Forms.Button btnBrowse;
    }
}