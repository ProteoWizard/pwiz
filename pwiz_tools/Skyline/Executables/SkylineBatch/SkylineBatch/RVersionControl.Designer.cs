namespace SkylineBatch
{
    partial class RVersionControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RVersionControl));
            this.labelMessage = new System.Windows.Forms.Label();
            this.comboRVersions = new System.Windows.Forms.ComboBox();
            this.labelTitle = new System.Windows.Forms.Label();
            this.btnAddDirectory = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // labelMessage
            // 
            resources.ApplyResources(this.labelMessage, "labelMessage");
            this.labelMessage.Name = "labelMessage";
            // 
            // comboRVersions
            // 
            this.comboRVersions.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRVersions.FormattingEnabled = true;
            resources.ApplyResources(this.comboRVersions, "comboRVersions");
            this.comboRVersions.Name = "comboRVersions";
            this.comboRVersions.SelectedIndexChanged += new System.EventHandler(this.comboRVersions_SelectedIndexChanged);
            // 
            // labelTitle
            // 
            resources.ApplyResources(this.labelTitle, "labelTitle");
            this.labelTitle.Name = "labelTitle";
            // 
            // btnAddDirectory
            // 
            resources.ApplyResources(this.btnAddDirectory, "btnAddDirectory");
            this.btnAddDirectory.Name = "btnAddDirectory";
            this.btnAddDirectory.UseVisualStyleBackColor = true;
            this.btnAddDirectory.Click += new System.EventHandler(this.btnAddDirectory_Click);
            // 
            // RVersionControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.btnAddDirectory);
            this.Controls.Add(this.labelTitle);
            this.Controls.Add(this.comboRVersions);
            this.Controls.Add(this.labelMessage);
            this.Name = "RVersionControl";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelMessage;
        private System.Windows.Forms.ComboBox comboRVersions;
        private System.Windows.Forms.Label labelTitle;
        private System.Windows.Forms.Button btnAddDirectory;
    }
}
