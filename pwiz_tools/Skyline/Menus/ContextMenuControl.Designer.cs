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
            this.peptideOrderContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideOrderDocumentContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideOrderRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideOrderAreaContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideOrderMassErrorContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.scopeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.documentScopeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.proteinScopeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replicatesRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.averageReplicatesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.singleReplicateRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bestReplicateRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replicateOrderContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replicateOrderDocumentContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replicateOrderAcqTimeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.groupReplicatesByContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.groupByReplicateContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.sharedContextMenuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // sharedContextMenuStrip
            // 
            this.sharedContextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.selectionContextMenuItem,
            this.synchronizeSummaryZoomingContextMenuItem,
            this.peptideCvsContextMenuItem,
            this.peptideOrderContextMenuItem,
            this.scopeContextMenuItem,
            this.replicatesRTContextMenuItem});
            this.sharedContextMenuStrip.Name = "sharedContextMenuString";
            resources.ApplyResources(this.sharedContextMenuStrip, "sharedContextMenuStrip");
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
            // peptideOrderContextMenuItem
            //
            this.peptideOrderContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.peptideOrderDocumentContextMenuItem,
            this.peptideOrderRTContextMenuItem,
            this.peptideOrderAreaContextMenuItem,
            this.peptideOrderMassErrorContextMenuItem});
            this.peptideOrderContextMenuItem.Name = "peptideOrderContextMenuItem";
            resources.ApplyResources(this.peptideOrderContextMenuItem, "peptideOrderContextMenuItem");
            this.peptideOrderContextMenuItem.DropDownOpening += new System.EventHandler(this.peptideOrderContextMenuItem_DropDownOpening);
            //
            // peptideOrderDocumentContextMenuItem
            //
            this.peptideOrderDocumentContextMenuItem.Name = "peptideOrderDocumentContextMenuItem";
            resources.ApplyResources(this.peptideOrderDocumentContextMenuItem, "peptideOrderDocumentContextMenuItem");
            this.peptideOrderDocumentContextMenuItem.Click += new System.EventHandler(this.peptideOrderDocumentContextMenuItem_Click);
            //
            // peptideOrderRTContextMenuItem
            //
            this.peptideOrderRTContextMenuItem.Name = "peptideOrderRTContextMenuItem";
            resources.ApplyResources(this.peptideOrderRTContextMenuItem, "peptideOrderRTContextMenuItem");
            this.peptideOrderRTContextMenuItem.Click += new System.EventHandler(this.peptideOrderRTContextMenuItem_Click);
            //
            // peptideOrderAreaContextMenuItem
            //
            this.peptideOrderAreaContextMenuItem.Name = "peptideOrderAreaContextMenuItem";
            resources.ApplyResources(this.peptideOrderAreaContextMenuItem, "peptideOrderAreaContextMenuItem");
            this.peptideOrderAreaContextMenuItem.Click += new System.EventHandler(this.peptideOrderAreaContextMenuItem_Click);
            //
            // peptideOrderMassErrorContextMenuItem
            //
            this.peptideOrderMassErrorContextMenuItem.Name = "peptideOrderMassErrorContextMenuItem";
            resources.ApplyResources(this.peptideOrderMassErrorContextMenuItem, "peptideOrderMassErrorContextMenuItem");
            this.peptideOrderMassErrorContextMenuItem.Click += new System.EventHandler(this.peptideOrderMassErrorContextMenuItem_Click);
            //
            // scopeContextMenuItem
            //
            this.scopeContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.documentScopeContextMenuItem,
            this.proteinScopeContextMenuItem});
            this.scopeContextMenuItem.Name = "scopeContextMenuItem";
            resources.ApplyResources(this.scopeContextMenuItem, "scopeContextMenuItem");
            this.scopeContextMenuItem.DropDownOpening += new System.EventHandler(this.scopeContextMenuItem_DropDownOpening);
            //
            // documentScopeContextMenuItem
            //
            this.documentScopeContextMenuItem.Name = "documentScopeContextMenuItem";
            resources.ApplyResources(this.documentScopeContextMenuItem, "documentScopeContextMenuItem");
            this.documentScopeContextMenuItem.Click += new System.EventHandler(this.documentScopeContextMenuItem_Click);
            //
            // proteinScopeContextMenuItem
            //
            this.proteinScopeContextMenuItem.Name = "proteinScopeContextMenuItem";
            resources.ApplyResources(this.proteinScopeContextMenuItem, "proteinScopeContextMenuItem");
            this.proteinScopeContextMenuItem.Click += new System.EventHandler(this.proteinScopeContextMenuItem_Click);
            //
            // replicatesRTContextMenuItem
            //
            this.replicatesRTContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.averageReplicatesContextMenuItem,
            this.singleReplicateRTContextMenuItem,
            this.bestReplicateRTContextMenuItem});
            this.replicatesRTContextMenuItem.Name = "replicatesRTContextMenuItem";
            resources.ApplyResources(this.replicatesRTContextMenuItem, "replicatesRTContextMenuItem");
            this.replicatesRTContextMenuItem.DropDownOpening += new System.EventHandler(this.replicatesRTContextMenuItem_DropDownOpening);
            //
            // averageReplicatesContextMenuItem
            //
            this.averageReplicatesContextMenuItem.Name = "averageReplicatesContextMenuItem";
            resources.ApplyResources(this.averageReplicatesContextMenuItem, "averageReplicatesContextMenuItem");
            this.averageReplicatesContextMenuItem.Click += new System.EventHandler(this.averageReplicatesContextMenuItem_Click);
            //
            // singleReplicateRTContextMenuItem
            //
            this.singleReplicateRTContextMenuItem.Name = "singleReplicateRTContextMenuItem";
            resources.ApplyResources(this.singleReplicateRTContextMenuItem, "singleReplicateRTContextMenuItem");
            this.singleReplicateRTContextMenuItem.Click += new System.EventHandler(this.singleReplicateRTContextMenuItem_Click);
            //
            // bestReplicateRTContextMenuItem
            //
            this.bestReplicateRTContextMenuItem.Name = "bestReplicateRTContextMenuItem";
            resources.ApplyResources(this.bestReplicateRTContextMenuItem, "bestReplicateRTContextMenuItem");
            this.bestReplicateRTContextMenuItem.Click += new System.EventHandler(this.bestReplicateRTContextMenuItem_Click);
            //
            // replicateOrderContextMenuItem
            //
            this.replicateOrderContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.replicateOrderDocumentContextMenuItem,
            this.replicateOrderAcqTimeContextMenuItem});
            this.replicateOrderContextMenuItem.Name = "replicateOrderContextMenuItem";
            resources.ApplyResources(this.replicateOrderContextMenuItem, "replicateOrderContextMenuItem");
            //
            // replicateOrderDocumentContextMenuItem
            //
            this.replicateOrderDocumentContextMenuItem.Name = "replicateOrderDocumentContextMenuItem";
            resources.ApplyResources(this.replicateOrderDocumentContextMenuItem, "replicateOrderDocumentContextMenuItem");
            this.replicateOrderDocumentContextMenuItem.Click += new System.EventHandler(this.replicateOrderDocumentContextMenuItem_Click);
            //
            // replicateOrderAcqTimeContextMenuItem
            //
            this.replicateOrderAcqTimeContextMenuItem.Name = "replicateOrderAcqTimeContextMenuItem";
            resources.ApplyResources(this.replicateOrderAcqTimeContextMenuItem, "replicateOrderAcqTimeContextMenuItem");
            this.replicateOrderAcqTimeContextMenuItem.Click += new System.EventHandler(this.replicateOrderAcqTimeContextMenuItem_Click);
            //
            // groupReplicatesByContextMenuItem
            //
            this.groupReplicatesByContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.groupByReplicateContextMenuItem});
            this.groupReplicatesByContextMenuItem.Name = "groupReplicatesByContextMenuItem";
            resources.ApplyResources(this.groupReplicatesByContextMenuItem, "groupReplicatesByContextMenuItem");
            //
            // groupByReplicateContextMenuItem
            //
            this.groupByReplicateContextMenuItem.Name = "groupByReplicateContextMenuItem";
            resources.ApplyResources(this.groupByReplicateContextMenuItem, "groupByReplicateContextMenuItem");
            this.groupByReplicateContextMenuItem.Click += new System.EventHandler(this.groupByReplicateContextMenuItem_Click);
            //
            // ContextMenuControl
            //
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Name = "ContextMenuControl";
            this.sharedContextMenuStrip.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ContextMenuStrip sharedContextMenuStrip;
        protected System.Windows.Forms.ToolStripMenuItem selectionContextMenuItem;
        protected System.Windows.Forms.ToolStripMenuItem synchronizeSummaryZoomingContextMenuItem;
        protected System.Windows.Forms.ToolStripMenuItem peptideCvsContextMenuItem;
        protected System.Windows.Forms.ToolStripMenuItem peptideOrderContextMenuItem;
        protected System.Windows.Forms.ToolStripMenuItem peptideOrderDocumentContextMenuItem;
        protected System.Windows.Forms.ToolStripMenuItem peptideOrderRTContextMenuItem;
        protected System.Windows.Forms.ToolStripMenuItem peptideOrderAreaContextMenuItem;
        protected System.Windows.Forms.ToolStripMenuItem peptideOrderMassErrorContextMenuItem;
        protected System.Windows.Forms.ToolStripMenuItem scopeContextMenuItem;
        protected System.Windows.Forms.ToolStripMenuItem documentScopeContextMenuItem;
        protected System.Windows.Forms.ToolStripMenuItem proteinScopeContextMenuItem;
        protected System.Windows.Forms.ToolStripMenuItem replicatesRTContextMenuItem;
        protected System.Windows.Forms.ToolStripMenuItem averageReplicatesContextMenuItem;
        protected System.Windows.Forms.ToolStripMenuItem singleReplicateRTContextMenuItem;
        protected System.Windows.Forms.ToolStripMenuItem bestReplicateRTContextMenuItem;
        protected System.Windows.Forms.ToolStripMenuItem replicateOrderContextMenuItem;
        protected System.Windows.Forms.ToolStripMenuItem replicateOrderDocumentContextMenuItem;
        protected System.Windows.Forms.ToolStripMenuItem replicateOrderAcqTimeContextMenuItem;
        protected System.Windows.Forms.ToolStripMenuItem groupReplicatesByContextMenuItem;
        protected System.Windows.Forms.ToolStripMenuItem groupByReplicateContextMenuItem;
    }
}
