namespace pwiz.Skyline.Alerts
{
    partial class ImportDocResultsDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ImportDocResultsDlg));
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.radioRemove = new System.Windows.Forms.RadioButton();
            this.radioMergeByName = new System.Windows.Forms.RadioButton();
            this.radioMergeByIndex = new System.Windows.Forms.RadioButton();
            this.radioAdd = new System.Windows.Forms.RadioButton();
            this.cbMergePeptides = new System.Windows.Forms.CheckBox();
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
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // radioRemove
            // 
            resources.ApplyResources(this.radioRemove, "radioRemove");
            this.radioRemove.Checked = true;
            this.radioRemove.Name = "radioRemove";
            this.radioRemove.TabStop = true;
            this.radioRemove.UseVisualStyleBackColor = true;
            // 
            // radioMergeByName
            // 
            resources.ApplyResources(this.radioMergeByName, "radioMergeByName");
            this.radioMergeByName.Name = "radioMergeByName";
            this.radioMergeByName.UseVisualStyleBackColor = true;
            // 
            // radioMergeByIndex
            // 
            resources.ApplyResources(this.radioMergeByIndex, "radioMergeByIndex");
            this.radioMergeByIndex.Name = "radioMergeByIndex";
            this.radioMergeByIndex.UseVisualStyleBackColor = true;
            // 
            // radioAdd
            // 
            resources.ApplyResources(this.radioAdd, "radioAdd");
            this.radioAdd.Name = "radioAdd";
            this.radioAdd.UseVisualStyleBackColor = true;
            // 
            // cbMergePeptides
            // 
            resources.ApplyResources(this.cbMergePeptides, "cbMergePeptides");
            this.cbMergePeptides.Name = "cbMergePeptides";
            this.cbMergePeptides.UseVisualStyleBackColor = true;
            // 
            // ImportDocResultsDlg
            // 
            this.AcceptButton = this.btnOK;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.cbMergePeptides);
            this.Controls.Add(this.radioAdd);
            this.Controls.Add(this.radioMergeByIndex);
            this.Controls.Add(this.radioMergeByName);
            this.Controls.Add(this.radioRemove);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ImportDocResultsDlg";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.RadioButton radioRemove;
        private System.Windows.Forms.RadioButton radioMergeByName;
        private System.Windows.Forms.RadioButton radioMergeByIndex;
        private System.Windows.Forms.RadioButton radioAdd;
        private System.Windows.Forms.CheckBox cbMergePeptides;
    }
}