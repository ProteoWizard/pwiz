namespace pwiz.Skyline.FileUI
{
    partial class ChooseIrtStandardPeptidesDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ChooseIrtStandardPeptidesDlg));
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.comboProteins = new System.Windows.Forms.ComboBox();
            this.radioProtein = new System.Windows.Forms.RadioButton();
            this.radioTransitionList = new System.Windows.Forms.RadioButton();
            this.txtTransitionList = new System.Windows.Forms.TextBox();
            this.btnBrowseTransitionList = new System.Windows.Forms.Button();
            this.radioExisting = new System.Windows.Forms.RadioButton();
            this.comboExisting = new System.Windows.Forms.ComboBox();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.SuspendLayout();
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // comboProteins
            // 
            resources.ApplyResources(this.comboProteins, "comboProteins");
            this.comboProteins.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboProteins.FormattingEnabled = true;
            this.comboProteins.Name = "comboProteins";
            // 
            // radioProtein
            // 
            resources.ApplyResources(this.radioProtein, "radioProtein");
            this.radioProtein.Name = "radioProtein";
            this.radioProtein.UseVisualStyleBackColor = true;
            this.radioProtein.CheckedChanged += new System.EventHandler(this.UpdateSelection);
            // 
            // radioTransitionList
            // 
            resources.ApplyResources(this.radioTransitionList, "radioTransitionList");
            this.radioTransitionList.Name = "radioTransitionList";
            this.radioTransitionList.UseVisualStyleBackColor = true;
            this.radioTransitionList.CheckedChanged += new System.EventHandler(this.UpdateSelection);
            // 
            // txtTransitionList
            // 
            resources.ApplyResources(this.txtTransitionList, "txtTransitionList");
            this.txtTransitionList.Name = "txtTransitionList";
            // 
            // btnBrowseTransitionList
            // 
            resources.ApplyResources(this.btnBrowseTransitionList, "btnBrowseTransitionList");
            this.btnBrowseTransitionList.Name = "btnBrowseTransitionList";
            this.btnBrowseTransitionList.UseVisualStyleBackColor = true;
            this.btnBrowseTransitionList.Click += new System.EventHandler(this.btnBrowseTransitionList_Click);
            // 
            // radioExisting
            // 
            resources.ApplyResources(this.radioExisting, "radioExisting");
            this.radioExisting.Checked = true;
            this.radioExisting.Name = "radioExisting";
            this.radioExisting.TabStop = true;
            this.radioExisting.UseVisualStyleBackColor = true;
            this.radioExisting.CheckedChanged += new System.EventHandler(this.UpdateSelection);
            // 
            // comboExisting
            // 
            resources.ApplyResources(this.comboExisting, "comboExisting");
            this.comboExisting.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboExisting.FormattingEnabled = true;
            this.comboExisting.Name = "comboExisting";
            // 
            // ChooseIrtStandardPeptidesDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.radioExisting);
            this.Controls.Add(this.comboExisting);
            this.Controls.Add(this.btnBrowseTransitionList);
            this.Controls.Add(this.txtTransitionList);
            this.Controls.Add(this.radioTransitionList);
            this.Controls.Add(this.radioProtein);
            this.Controls.Add(this.comboProteins);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ChooseIrtStandardPeptidesDlg";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.ComboBox comboProteins;
        private System.Windows.Forms.RadioButton radioProtein;
        private System.Windows.Forms.RadioButton radioTransitionList;
        private System.Windows.Forms.TextBox txtTransitionList;
        private System.Windows.Forms.Button btnBrowseTransitionList;
        private System.Windows.Forms.RadioButton radioExisting;
        private System.Windows.Forms.ComboBox comboExisting;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}