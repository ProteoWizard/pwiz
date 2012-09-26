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
            this.textImWindow.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textImWindow.Location = new System.Drawing.Point(-2, -1);
            this.textImWindow.Multiline = true;
            this.textImWindow.Name = "textImWindow";
            this.textImWindow.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.textImWindow.Size = new System.Drawing.Size(406, 282);
            this.textImWindow.TabIndex = 0;
            this.textImWindow.WordWrap = false;
            this.textImWindow.DragDrop += new System.Windows.Forms.DragEventHandler(this.textImWindow_DragDrop);
            this.textImWindow.DragEnter += new System.Windows.Forms.DragEventHandler(this.textImWindow_DragEnter);
            this.textImWindow.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.textImWindow_KeyPress);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(61, 4);
            // 
            // ImmediateWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(404, 280);
            this.Controls.Add(this.textImWindow);
            this.HideOnClose = true;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ImmediateWindow";
            this.ShowInTaskbar = false;
            this.TabText = "Immediate Window";
            this.Text = "Immediate Window";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textImWindow;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
    }
}