namespace pwiz.Skyline.Menus
{
    partial class ContextMenuControl
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ContextMenuControl));
            this.sharedContextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.selectionContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.synchronizeSummaryZoomingContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideCvsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.SuspendLayout();
            //
            // sharedContextMenuStrip
            //
            this.sharedContextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.selectionContextMenuItem,
            this.synchronizeSummaryZoomingContextMenuItem,
            this.peptideCvsContextMenuItem});
            this.sharedContextMenuStrip.Name = "sharedContextMenuString";
            this.sharedContextMenuStrip.Size = new System.Drawing.Size(181, 26);
            //
            // selectionContextMenuItem
            //
            this.selectionContextMenuItem.CheckOnClick = true;
            this.selectionContextMenuItem.Name = "selectionContextMenuItem";
            resources.ApplyResources(this.selectionContextMenuItem, "selectionContextMenuItem");
            this.selectionContextMenuItem.Click += new System.EventHandler(this.selectionContextMenuItem_Click);
            //
            // synchronizeSummaryZoomingContextMenuItem
            //
            this.synchronizeSummaryZoomingContextMenuItem.CheckOnClick = true;
            this.synchronizeSummaryZoomingContextMenuItem.Name = "synchronizeSummaryZoomingContextMenuItem";
            resources.ApplyResources(this.synchronizeSummaryZoomingContextMenuItem, "synchronizeSummaryZoomingContextMenuItem");
            this.synchronizeSummaryZoomingContextMenuItem.Click += new System.EventHandler(this.synchronizeSummaryZoomingContextMenuItem_Click);
            //
            // peptideCvsContextMenuItem
            //
            this.peptideCvsContextMenuItem.CheckOnClick = true;
            this.peptideCvsContextMenuItem.Name = "peptideCvsContextMenuItem";
            resources.ApplyResources(this.peptideCvsContextMenuItem, "peptideCvsContextMenuItem");
            this.peptideCvsContextMenuItem.Click += new System.EventHandler(this.peptideCvsContextMenuItem_Click);
            //
            // ContextMenuControl
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Name = "ContextMenuControl";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ContextMenuStrip sharedContextMenuStrip;
        protected System.Windows.Forms.ToolStripMenuItem selectionContextMenuItem;
        protected System.Windows.Forms.ToolStripMenuItem synchronizeSummaryZoomingContextMenuItem;
        protected System.Windows.Forms.ToolStripMenuItem peptideCvsContextMenuItem;
    }
}
