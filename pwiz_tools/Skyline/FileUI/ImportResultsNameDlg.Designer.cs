namespace pwiz.Skyline.FileUI
{
    partial class ImportResultsNameDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ImportResultsNameDlg));
            this.labelPrefix = new System.Windows.Forms.Label();
            this.textPrefix = new System.Windows.Forms.TextBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.labelExplanation = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.textSuffix = new System.Windows.Forms.TextBox();
            this.labelSuffix = new System.Windows.Forms.Label();
            this.radioDontRemove = new System.Windows.Forms.RadioButton();
            this.radioRemove = new System.Windows.Forms.RadioButton();
            this.listReplicateNames = new System.Windows.Forms.ListBox();
            this.labelReplicateNames = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // labelPrefix
            // 
            resources.ApplyResources(this.labelPrefix, "labelPrefix");
            this.labelPrefix.Name = "labelPrefix";
            // 
            // textPrefix
            // 
            resources.ApplyResources(this.textPrefix, "textPrefix");
            this.textPrefix.Name = "textPrefix";
            this.textPrefix.TextChanged += new System.EventHandler(this.textPrefix_TextChanged);
            // 
            // btnOK
            // 
            resources.ApplyResources(this.btnOK, "btnOK");
            this.btnOK.Name = "btnOK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // labelExplanation
            // 
            resources.ApplyResources(this.labelExplanation, "labelExplanation");
            this.labelExplanation.Name = "labelExplanation";
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // textSuffix
            // 
            resources.ApplyResources(this.textSuffix, "textSuffix");
            this.textSuffix.Name = "textSuffix";
            this.textSuffix.TextChanged += new System.EventHandler(this.textSuffix_TextChanged);
            // 
            // labelSuffix
            // 
            resources.ApplyResources(this.labelSuffix, "labelSuffix");
            this.labelSuffix.Name = "labelSuffix";
            // 
            // radioDontRemove
            // 
            resources.ApplyResources(this.radioDontRemove, "radioDontRemove");
            this.radioDontRemove.Name = "radioDontRemove";
            this.radioDontRemove.UseVisualStyleBackColor = true;
            this.radioDontRemove.CheckedChanged += new System.EventHandler(this.radioDontRemove_CheckedChanged);
            // 
            // radioRemove
            // 
            resources.ApplyResources(this.radioRemove, "radioRemove");
            this.radioRemove.Checked = true;
            this.radioRemove.Name = "radioRemove";
            this.radioRemove.TabStop = true;
            this.radioRemove.UseVisualStyleBackColor = true;
            this.radioRemove.CheckedChanged += new System.EventHandler(this.radioRemove_CheckedChanged);
            // 
            // listReplicateNames
            // 
            resources.ApplyResources(this.listReplicateNames, "listReplicateNames");
            this.listReplicateNames.FormattingEnabled = true;
            this.listReplicateNames.Name = "listReplicateNames";
            // 
            // labelReplicateNames
            // 
            resources.ApplyResources(this.labelReplicateNames, "labelReplicateNames");
            this.labelReplicateNames.Name = "labelReplicateNames";
            // 
            // ImportResultsNameDlg
            // 
            this.AcceptButton = this.btnOK;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.labelReplicateNames);
            this.Controls.Add(this.listReplicateNames);
            this.Controls.Add(this.radioRemove);
            this.Controls.Add(this.radioDontRemove);
            this.Controls.Add(this.textSuffix);
            this.Controls.Add(this.labelSuffix);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.labelExplanation);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.textPrefix);
            this.Controls.Add(this.labelPrefix);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ImportResultsNameDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelPrefix;
        private System.Windows.Forms.TextBox textPrefix;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Label labelExplanation;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.TextBox textSuffix;
        private System.Windows.Forms.Label labelSuffix;
        private System.Windows.Forms.RadioButton radioDontRemove;
        private System.Windows.Forms.RadioButton radioRemove;
        private System.Windows.Forms.ListBox listReplicateNames;
        private System.Windows.Forms.Label labelReplicateNames;
    }
}