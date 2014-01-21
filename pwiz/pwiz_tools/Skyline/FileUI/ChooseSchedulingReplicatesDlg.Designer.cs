namespace pwiz.Skyline.FileUI
{
    partial class ChooseSchedulingReplicatesDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ChooseSchedulingReplicatesDlg));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.labelInstructions = new System.Windows.Forms.Label();
            this.checkedListBoxResults = new System.Windows.Forms.CheckedListBox();
            this.checkboxSelectAll = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // labelInstructions
            // 
            resources.ApplyResources(this.labelInstructions, "labelInstructions");
            this.labelInstructions.AutoEllipsis = true;
            this.labelInstructions.Name = "labelInstructions";
            // 
            // checkedListBoxResults
            // 
            this.checkedListBoxResults.CheckOnClick = true;
            this.checkedListBoxResults.FormattingEnabled = true;
            resources.ApplyResources(this.checkedListBoxResults, "checkedListBoxResults");
            this.checkedListBoxResults.Name = "checkedListBoxResults";
            this.checkedListBoxResults.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.checkedListBoxResults_ItemCheck);
            // 
            // checkboxSelectAll
            // 
            resources.ApplyResources(this.checkboxSelectAll, "checkboxSelectAll");
            this.checkboxSelectAll.Name = "checkboxSelectAll";
            this.checkboxSelectAll.UseVisualStyleBackColor = true;
            this.checkboxSelectAll.CheckedChanged += new System.EventHandler(this.checkboxSelectAll_CheckedChanged);
            // 
            // ChooseSchedulingReplicatesDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.checkboxSelectAll);
            this.Controls.Add(this.checkedListBoxResults);
            this.Controls.Add(this.labelInstructions);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ChooseSchedulingReplicatesDlg";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Label labelInstructions;
        private System.Windows.Forms.CheckedListBox checkedListBoxResults;
        private System.Windows.Forms.CheckBox checkboxSelectAll;
    }
}