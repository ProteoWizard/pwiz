namespace pwiz.Skyline.SettingsUI
{
    partial class ChooseAnnotationsDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ChooseAnnotationsDlg));
            this.checkedListBoxAnnotations = new System.Windows.Forms.CheckedListBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnEditAnnotationList = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // checkedListBoxAnnotations
            // 
            resources.ApplyResources(this.checkedListBoxAnnotations, "checkedListBoxAnnotations");
            this.checkedListBoxAnnotations.CheckOnClick = true;
            this.checkedListBoxAnnotations.FormattingEnabled = true;
            this.checkedListBoxAnnotations.Name = "checkedListBoxAnnotations";
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
            // btnEditAnnotationList
            // 
            resources.ApplyResources(this.btnEditAnnotationList, "btnEditAnnotationList");
            this.btnEditAnnotationList.Name = "btnEditAnnotationList";
            this.btnEditAnnotationList.UseVisualStyleBackColor = true;
            this.btnEditAnnotationList.Click += new System.EventHandler(this.btnEditAnnotationList_Click);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // ChooseAnnotationsDlg
            // 
            this.AcceptButton = this.btnOK;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnEditAnnotationList);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.checkedListBoxAnnotations);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ChooseAnnotationsDlg";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.CheckedListBox checkedListBoxAnnotations;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnEditAnnotationList;
        private System.Windows.Forms.Label label1;
    }
}
