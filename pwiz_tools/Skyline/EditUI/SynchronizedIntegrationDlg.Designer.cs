namespace pwiz.Skyline.EditUI
{
    partial class SynchronizedIntegrationDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SynchronizedIntegrationDlg));
            this.comboGroupBy = new System.Windows.Forms.ComboBox();
            this.listSync = new System.Windows.Forms.CheckedListBox();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.cbSelectAll = new System.Windows.Forms.CheckBox();
            this.label2 = new System.Windows.Forms.Label();
            this.comboAlign = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // comboGroupBy
            // 
            resources.ApplyResources(this.comboGroupBy, "comboGroupBy");
            this.comboGroupBy.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboGroupBy.FormattingEnabled = true;
            this.comboGroupBy.Name = "comboGroupBy";
            this.comboGroupBy.SelectedIndexChanged += new System.EventHandler(this.comboGroupBy_SelectedIndexChanged);
            // 
            // listSync
            // 
            resources.ApplyResources(this.listSync, "listSync");
            this.listSync.CheckOnClick = true;
            this.listSync.FormattingEnabled = true;
            this.listSync.Name = "listSync";
            this.listSync.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.listSync_ItemCheck);
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
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // cbSelectAll
            // 
            resources.ApplyResources(this.cbSelectAll, "cbSelectAll");
            this.cbSelectAll.Name = "cbSelectAll";
            this.cbSelectAll.UseVisualStyleBackColor = true;
            this.cbSelectAll.CheckedChanged += new System.EventHandler(this.cbSelectAll_CheckedChanged);
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // comboAlign
            // 
            resources.ApplyResources(this.comboAlign, "comboAlign");
            this.comboAlign.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboAlign.FormattingEnabled = true;
            this.comboAlign.Name = "comboAlign";
            this.comboAlign.SelectedIndexChanged += new System.EventHandler(this.comboAlign_SelectedIndexChanged);
            // 
            // SynchronizedIntegrationDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.comboAlign);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.cbSelectAll);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.listSync);
            this.Controls.Add(this.comboGroupBy);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SynchronizedIntegrationDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ComboBox comboGroupBy;
        private System.Windows.Forms.CheckedListBox listSync;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox cbSelectAll;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox comboAlign;
    }
}