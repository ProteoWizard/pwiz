
namespace pwiz.Skyline.Controls
{
    public partial class FilesTreeForm
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

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FilesTreeForm));
            this.filesTree = new FilesTree();
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // filesTree
            // 
            resources.ApplyResources(this.filesTree, "filesTree");
            this.filesTree.AutoExpandSingleNodes = true;
            this.filesTree.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.filesTree.HideSelection = false;
            this.filesTree.ItemHeight = 16;
            this.filesTree.LabelEdit = true;
            this.filesTree.Name = "filesTree";
            this.filesTree.RestoredFromPersistentString = false;
            this.filesTree.UseKeysOverride = false;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.filesTree);
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Name = "panel1";
            // 
            // FilesTreeForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panel1);
            this.HideOnClose = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FilesTreeForm";
            this.ShowInTaskbar = false;
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private FilesTree filesTree;
        private System.Windows.Forms.Panel panel1;
    }
}
