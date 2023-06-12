namespace SkylineBatch
{
    partial class RemoteFileControl
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RemoteFileControl));
            this.label2 = new System.Windows.Forms.Label();
            this.textRelativePath = new System.Windows.Forms.TextBox();
            this.comboRemoteFileSource = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.btnOpenFromPanorama = new System.Windows.Forms.Button();
            this.flowRelativePath = new System.Windows.Forms.FlowLayoutPanel();
            this.flowRelativePath.SuspendLayout();
            this.SuspendLayout();
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // textRelativePath
            // 
            resources.ApplyResources(this.textRelativePath, "textRelativePath");
            this.textRelativePath.Name = "textRelativePath";
            // 
            // comboRemoteFileSource
            // 
            resources.ApplyResources(this.comboRemoteFileSource, "comboRemoteFileSource");
            this.comboRemoteFileSource.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRemoteFileSource.FormattingEnabled = true;
            this.comboRemoteFileSource.Name = "comboRemoteFileSource";
            this.comboRemoteFileSource.SelectedIndexChanged += new System.EventHandler(this.comboRemoteFileSource_SelectedIndexChanged);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // btnOpenFromPanorama
            // 
            this.btnOpenFromPanorama.Image = global::SkylineBatch.Properties.Resources.Panorama;
            resources.ApplyResources(this.btnOpenFromPanorama, "btnOpenFromPanorama");
            this.btnOpenFromPanorama.Name = "btnOpenFromPanorama";
            this.btnOpenFromPanorama.UseVisualStyleBackColor = true;
            this.btnOpenFromPanorama.Click += new System.EventHandler(this.btnOpenFromPanorama_Click);
            // 
            // flowRelativePath
            // 
            resources.ApplyResources(this.flowRelativePath, "flowRelativePath");
            this.flowRelativePath.Controls.Add(this.textRelativePath);
            this.flowRelativePath.Controls.Add(this.btnOpenFromPanorama);
            this.flowRelativePath.Name = "flowRelativePath";
            // 
            // RemoteFileControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.flowRelativePath);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.comboRemoteFileSource);
            this.Controls.Add(this.label1);
            this.Name = "RemoteFileControl";
            this.flowRelativePath.ResumeLayout(false);
            this.flowRelativePath.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Label label2;
        public System.Windows.Forms.TextBox textRelativePath;
        public System.Windows.Forms.ComboBox comboRemoteFileSource;
        private System.Windows.Forms.Label label1;
        public System.Windows.Forms.Button btnOpenFromPanorama;
        private System.Windows.Forms.FlowLayoutPanel flowRelativePath;
    }
}
