using pwiz.Topograph.ui.Controls;

namespace pwiz.Topograph.ui.Forms
{
    partial class AbstractChromatogramForm
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
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.toolStripMenuItemSmooth = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemShowSpectrum = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolTip1
            // 
            this.toolTip1.ShowAlways = true;
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItemSmooth,
            this.toolStripMenuItemShowSpectrum});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.ShowCheckMargin = true;
            this.contextMenuStrip1.Size = new System.Drawing.Size(180, 70);
            // 
            // toolStripMenuItemSmooth
            // 
            this.toolStripMenuItemSmooth.CheckOnClick = true;
            this.toolStripMenuItemSmooth.Name = "toolStripMenuItemSmooth";
            this.toolStripMenuItemSmooth.Size = new System.Drawing.Size(179, 22);
            this.toolStripMenuItemSmooth.Text = "Smooth";
            this.toolStripMenuItemSmooth.Click += new System.EventHandler(this.ToolStripMenuItemSmoothOnClick);
            // 
            // toolStripMenuItemShowSpectrum
            // 
            this.toolStripMenuItemShowSpectrum.Name = "toolStripMenuItemShowSpectrum";
            this.toolStripMenuItemShowSpectrum.Size = new System.Drawing.Size(179, 22);
            this.toolStripMenuItemShowSpectrum.Text = "Show Spectrum";
            this.toolStripMenuItemShowSpectrum.Click += new System.EventHandler(this.ToolStripMenuItemShowSpectrumOnClick);
            // 
            // AbstractChromatogramForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(778, 533);
            this.Name = "AbstractChromatogramForm";
            this.TabText = "ChromatogramForm";
            this.Text = "ChromatogramForm";
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemSmooth;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemShowSpectrum;
    }
}