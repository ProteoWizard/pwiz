namespace pwiz.Skyline.Controls
{
    partial class ImmediateWindow
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ImmediateWindow));
            this.textImWindow = new System.Windows.Forms.TextBox();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.SuspendLayout();
            // 
            // textImWindow
            // 
            this.textImWindow.AllowDrop = true;
            resources.ApplyResources(this.textImWindow, "textImWindow");
            this.textImWindow.Name = "textImWindow";
            this.textImWindow.DragDrop += new System.Windows.Forms.DragEventHandler(this.textImWindow_DragDrop);
            this.textImWindow.DragEnter += new System.Windows.Forms.DragEventHandler(this.textImWindow_DragEnter);
            this.textImWindow.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.textImWindow_KeyPress);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            resources.ApplyResources(this.contextMenuStrip1, "contextMenuStrip1");
            // 
            // ImmediateWindow
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.textImWindow);
            this.HideOnClose = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ImmediateWindow";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textImWindow;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
    }
}