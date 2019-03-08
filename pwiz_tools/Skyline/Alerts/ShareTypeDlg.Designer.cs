namespace pwiz.Skyline.Alerts
{
    partial class ShareTypeDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ShareTypeDlg));
            this.btnCancel = new System.Windows.Forms.Button();
            this.panelButtonBar = new System.Windows.Forms.FlowLayoutPanel();
            this.btnShare = new System.Windows.Forms.Button();
            this.comboSkylineVersion = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.radioComplete = new System.Windows.Forms.RadioButton();
            this.radioMinimal = new System.Windows.Forms.RadioButton();
            this.label3 = new System.Windows.Forms.Label();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.panelButtonBar.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // panelButtonBar
            // 
            this.panelButtonBar.BackColor = System.Drawing.SystemColors.Control;
            this.panelButtonBar.Controls.Add(this.btnCancel);
            this.panelButtonBar.Controls.Add(this.btnShare);
            resources.ApplyResources(this.panelButtonBar, "panelButtonBar");
            this.panelButtonBar.Name = "panelButtonBar";
            // 
            // btnShare
            // 
            resources.ApplyResources(this.btnShare, "btnShare");
            this.btnShare.Name = "btnShare";
            this.btnShare.UseVisualStyleBackColor = true;
            this.btnShare.Click += new System.EventHandler(this.btnShare_Click);
            // 
            // comboSkylineVersion
            // 
            resources.ApplyResources(this.comboSkylineVersion, "comboSkylineVersion");
            this.comboSkylineVersion.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboSkylineVersion.FormattingEnabled = true;
            this.comboSkylineVersion.Name = "comboSkylineVersion";
            this.toolTip1.SetToolTip(this.comboSkylineVersion, resources.GetString("comboSkylineVersion.ToolTip"));
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // radioComplete
            // 
            resources.ApplyResources(this.radioComplete, "radioComplete");
            this.radioComplete.Name = "radioComplete";
            this.radioComplete.TabStop = true;
            this.toolTip1.SetToolTip(this.radioComplete, resources.GetString("radioComplete.ToolTip"));
            this.radioComplete.UseVisualStyleBackColor = true;
            // 
            // radioMinimal
            // 
            resources.ApplyResources(this.radioMinimal, "radioMinimal");
            this.radioMinimal.Name = "radioMinimal";
            this.radioMinimal.TabStop = true;
            this.toolTip1.SetToolTip(this.radioMinimal, resources.GetString("radioMinimal.ToolTip"));
            this.radioMinimal.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // ShareTypeDlg
            // 
            this.AcceptButton = this.btnShare;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.radioMinimal);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.radioComplete);
            this.Controls.Add(this.comboSkylineVersion);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.panelButtonBar);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ShareTypeDlg";
            this.ShowInTaskbar = false;
            this.panelButtonBar.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.FlowLayoutPanel panelButtonBar;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnShare;
        private System.Windows.Forms.RadioButton radioComplete;
        private System.Windows.Forms.RadioButton radioMinimal;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox comboSkylineVersion;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}