namespace pwiz.Skyline.Menus
{
    partial class TreeNodeContextMenu
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TreeNodeContextMenu));
            this.contextMenuTreeNode = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.cutContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pasteContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.expandSelectionContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.expandSelectionNoneContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.expandSelectionProteinsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.expandSelectionPeptidesContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.expandSelectionPrecursorsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pickChildrenContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addMoleculeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addSmallMoleculePrecursorContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addTransitionMoleculeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removePeakContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.setStandardTypeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.noStandardContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.normStandardContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.surrogateStandardContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.qcStandardContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.irtStandardContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.modifyPeptideContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editSpectrumFilterContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toggleQuantitativeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.markTransitionsQuantitativeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator7 = new System.Windows.Forms.ToolStripSeparator();
            this.editNoteContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparatorRatios = new System.Windows.Forms.ToolStripSeparator();
            this.ratiosContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ratiosToGlobalStandardsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replicatesTreeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.singleReplicateTreeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bestReplicateTreeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuTreeNode.SuspendLayout();
            this.SuspendLayout();
            // 
            // contextMenuTreeNode
            // 
            this.contextMenuTreeNode.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.cutContextMenuItem,
            this.copyContextMenuItem,
            this.pasteContextMenuItem,
            this.deleteContextMenuItem,
            this.toolStripSeparator1,
            this.expandSelectionContextMenuItem,
            this.pickChildrenContextMenuItem,
            this.addMoleculeContextMenuItem,
            this.addSmallMoleculePrecursorContextMenuItem,
            this.addTransitionMoleculeContextMenuItem,
            this.removePeakContextMenuItem,
            this.setStandardTypeContextMenuItem,
            this.modifyPeptideContextMenuItem,
            this.editSpectrumFilterContextMenuItem,
            this.toggleQuantitativeContextMenuItem,
            this.markTransitionsQuantitativeContextMenuItem,
            this.toolStripSeparator7,
            this.editNoteContextMenuItem,
            this.toolStripSeparatorRatios,
            this.ratiosContextMenuItem,
            this.replicatesTreeContextMenuItem});
            this.contextMenuTreeNode.Name = "contextMenuTreeNode";
            resources.ApplyResources(this.contextMenuTreeNode, "contextMenuTreeNode");
            this.contextMenuTreeNode.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenuTreeNode_Opening);
            // 
            // cutContextMenuItem
            // 
            this.cutContextMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Cut;
            resources.ApplyResources(this.cutContextMenuItem, "cutContextMenuItem");
            this.cutContextMenuItem.Name = "cutContextMenuItem";
            this.cutContextMenuItem.Click += new System.EventHandler(this.cutContextMenuItem_Click);
            // 
            // copyContextMenuItem
            // 
            this.copyContextMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Copy;
            resources.ApplyResources(this.copyContextMenuItem, "copyContextMenuItem");
            this.copyContextMenuItem.Name = "copyContextMenuItem";
            this.copyContextMenuItem.Click += new System.EventHandler(this.copyContextMenuItem_Click);
            // 
            // pasteContextMenuItem
            // 
            this.pasteContextMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Paste;
            resources.ApplyResources(this.pasteContextMenuItem, "pasteContextMenuItem");
            this.pasteContextMenuItem.Name = "pasteContextMenuItem";
            this.pasteContextMenuItem.Click += new System.EventHandler(this.pasteContextMenuItem_Click);
            // 
            // deleteContextMenuItem
            // 
            this.deleteContextMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Delete;
            resources.ApplyResources(this.deleteContextMenuItem, "deleteContextMenuItem");
            this.deleteContextMenuItem.Name = "deleteContextMenuItem";
            this.deleteContextMenuItem.Click += new System.EventHandler(this.deleteContextMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            resources.ApplyResources(this.toolStripSeparator1, "toolStripSeparator1");
            // 
            // expandSelectionContextMenuItem
            // 
            this.expandSelectionContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.expandSelectionNoneContextMenuItem,
            this.expandSelectionProteinsContextMenuItem,
            this.expandSelectionPeptidesContextMenuItem,
            this.expandSelectionPrecursorsContextMenuItem});
            this.expandSelectionContextMenuItem.Name = "expandSelectionContextMenuItem";
            resources.ApplyResources(this.expandSelectionContextMenuItem, "expandSelectionContextMenuItem");
            // 
            // expandSelectionNoneContextMenuItem
            // 
            this.expandSelectionNoneContextMenuItem.Name = "expandSelectionNoneContextMenuItem";
            resources.ApplyResources(this.expandSelectionNoneContextMenuItem, "expandSelectionNoneContextMenuItem");
            this.expandSelectionNoneContextMenuItem.Tag = typeof(pwiz.Skyline.Controls.SeqNode.SrmTreeNodeParent);
            this.expandSelectionNoneContextMenuItem.Click += new System.EventHandler(this.expandSelectionNoneContextMenuItem_Click);
            // 
            // expandSelectionProteinsContextMenuItem
            // 
            this.expandSelectionProteinsContextMenuItem.Name = "expandSelectionProteinsContextMenuItem";
            resources.ApplyResources(this.expandSelectionProteinsContextMenuItem, "expandSelectionProteinsContextMenuItem");
            this.expandSelectionProteinsContextMenuItem.Tag = typeof(pwiz.Skyline.Controls.SeqNode.PeptideGroupTreeNode);
            this.expandSelectionProteinsContextMenuItem.Click += new System.EventHandler(this.expandSelectionProteinsContextMenuItem_Click);
            // 
            // expandSelectionPeptidesContextMenuItem
            // 
            this.expandSelectionPeptidesContextMenuItem.Name = "expandSelectionPeptidesContextMenuItem";
            resources.ApplyResources(this.expandSelectionPeptidesContextMenuItem, "expandSelectionPeptidesContextMenuItem");
            this.expandSelectionPeptidesContextMenuItem.Tag = typeof(pwiz.Skyline.Controls.SeqNode.PeptideTreeNode);
            this.expandSelectionPeptidesContextMenuItem.Click += new System.EventHandler(this.expandSelectionPeptidesContextMenuItem_Click);
            // 
            // expandSelectionPrecursorsContextMenuItem
            // 
            this.expandSelectionPrecursorsContextMenuItem.Name = "expandSelectionPrecursorsContextMenuItem";
            resources.ApplyResources(this.expandSelectionPrecursorsContextMenuItem, "expandSelectionPrecursorsContextMenuItem");
            this.expandSelectionPrecursorsContextMenuItem.Tag = typeof(pwiz.Skyline.Controls.SeqNode.TransitionGroupTreeNode);
            this.expandSelectionPrecursorsContextMenuItem.Click += new System.EventHandler(this.expandSelectionPrecursorsContextMenuItem_Click);
            // 
            // pickChildrenContextMenuItem
            // 
            this.pickChildrenContextMenuItem.Name = "pickChildrenContextMenuItem";
            resources.ApplyResources(this.pickChildrenContextMenuItem, "pickChildrenContextMenuItem");
            this.pickChildrenContextMenuItem.Click += new System.EventHandler(this.pickChildrenContextMenuItem_Click);
            // 
            // addMoleculeContextMenuItem
            // 
            this.addMoleculeContextMenuItem.Name = "addMoleculeContextMenuItem";
            resources.ApplyResources(this.addMoleculeContextMenuItem, "addMoleculeContextMenuItem");
            this.addMoleculeContextMenuItem.Click += new System.EventHandler(this.addMoleculeContextMenuItem_Click);
            // 
            // addSmallMoleculePrecursorContextMenuItem
            // 
            this.addSmallMoleculePrecursorContextMenuItem.Name = "addSmallMoleculePrecursorContextMenuItem";
            resources.ApplyResources(this.addSmallMoleculePrecursorContextMenuItem, "addSmallMoleculePrecursorContextMenuItem");
            this.addSmallMoleculePrecursorContextMenuItem.Click += new System.EventHandler(this.addSmallMoleculePrecursorContextMenuItem_Click);
            // 
            // addTransitionMoleculeContextMenuItem
            // 
            this.addTransitionMoleculeContextMenuItem.Name = "addTransitionMoleculeContextMenuItem";
            resources.ApplyResources(this.addTransitionMoleculeContextMenuItem, "addTransitionMoleculeContextMenuItem");
            this.addTransitionMoleculeContextMenuItem.Click += new System.EventHandler(this.addTransitionMoleculeContextMenuItem_Click);
            // 
            // removePeakContextMenuItem
            // 
            this.removePeakContextMenuItem.Name = "removePeakContextMenuItem";
            resources.ApplyResources(this.removePeakContextMenuItem, "removePeakContextMenuItem");
            this.removePeakContextMenuItem.Click += new System.EventHandler(this.removePeakContextMenuItem_Click);
            // 
            // setStandardTypeContextMenuItem
            // 
            this.setStandardTypeContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.noStandardContextMenuItem,
            this.normStandardContextMenuItem,
            this.surrogateStandardContextMenuItem,
            this.qcStandardContextMenuItem,
            this.irtStandardContextMenuItem});
            this.setStandardTypeContextMenuItem.Name = "setStandardTypeContextMenuItem";
            resources.ApplyResources(this.setStandardTypeContextMenuItem, "setStandardTypeContextMenuItem");
            this.setStandardTypeContextMenuItem.DropDownOpening += new System.EventHandler(this.setStandardTypeContextMenuItem_DropDownOpening);
            // 
            // noStandardContextMenuItem
            // 
            this.noStandardContextMenuItem.Name = "noStandardContextMenuItem";
            resources.ApplyResources(this.noStandardContextMenuItem, "noStandardContextMenuItem");
            this.noStandardContextMenuItem.Click += new System.EventHandler(this.noStandardMenuItem_Click);
            // 
            // normStandardContextMenuItem
            // 
            this.normStandardContextMenuItem.Name = "normStandardContextMenuItem";
            resources.ApplyResources(this.normStandardContextMenuItem, "normStandardContextMenuItem");
            this.normStandardContextMenuItem.Click += new System.EventHandler(this.normStandardMenuItem_Click);
            // 
            // surrogateStandardContextMenuItem
            // 
            this.surrogateStandardContextMenuItem.Name = "surrogateStandardContextMenuItem";
            resources.ApplyResources(this.surrogateStandardContextMenuItem, "surrogateStandardContextMenuItem");
            this.surrogateStandardContextMenuItem.Click += new System.EventHandler(this.surrogateStandardMenuItem_Click);
            // 
            // qcStandardContextMenuItem
            // 
            this.qcStandardContextMenuItem.Name = "qcStandardContextMenuItem";
            resources.ApplyResources(this.qcStandardContextMenuItem, "qcStandardContextMenuItem");
            this.qcStandardContextMenuItem.Click += new System.EventHandler(this.qcStandardMenuItem_Click);
            // 
            // irtStandardContextMenuItem
            // 
            this.irtStandardContextMenuItem.Name = "irtStandardContextMenuItem";
            resources.ApplyResources(this.irtStandardContextMenuItem, "irtStandardContextMenuItem");
            this.irtStandardContextMenuItem.Click += new System.EventHandler(this.irtStandardContextMenuItem_Click);
            // 
            // modifyPeptideContextMenuItem
            // 
            this.modifyPeptideContextMenuItem.Name = "modifyPeptideContextMenuItem";
            resources.ApplyResources(this.modifyPeptideContextMenuItem, "modifyPeptideContextMenuItem");
            this.modifyPeptideContextMenuItem.Click += new System.EventHandler(this.modifyPeptideMenuItem_Click);
            // 
            // editSpectrumFilterContextMenuItem
            // 
            this.editSpectrumFilterContextMenuItem.Name = "editSpectrumFilterContextMenuItem";
            resources.ApplyResources(this.editSpectrumFilterContextMenuItem, "editSpectrumFilterContextMenuItem");
            this.editSpectrumFilterContextMenuItem.Click += new System.EventHandler(this.editSpectrumFilterContextMenuItem_Click);
            // 
            // toggleQuantitativeContextMenuItem
            // 
            this.toggleQuantitativeContextMenuItem.Name = "toggleQuantitativeContextMenuItem";
            resources.ApplyResources(this.toggleQuantitativeContextMenuItem, "toggleQuantitativeContextMenuItem");
            this.toggleQuantitativeContextMenuItem.Click += new System.EventHandler(this.toggleQuantitativeContextMenuItem_Click);
            // 
            // markTransitionsQuantitativeContextMenuItem
            // 
            this.markTransitionsQuantitativeContextMenuItem.Name = "markTransitionsQuantitativeContextMenuItem";
            resources.ApplyResources(this.markTransitionsQuantitativeContextMenuItem, "markTransitionsQuantitativeContextMenuItem");
            this.markTransitionsQuantitativeContextMenuItem.Click += new System.EventHandler(this.markTransitionsQuantitativeContextMenuItem_Click);
            // 
            // toolStripSeparator7
            // 
            this.toolStripSeparator7.Name = "toolStripSeparator7";
            resources.ApplyResources(this.toolStripSeparator7, "toolStripSeparator7");
            // 
            // editNoteContextMenuItem
            // 
            this.editNoteContextMenuItem.Image = global::pwiz.Skyline.Properties.Resources.Comment;
            resources.ApplyResources(this.editNoteContextMenuItem, "editNoteContextMenuItem");
            this.editNoteContextMenuItem.Name = "editNoteContextMenuItem";
            this.editNoteContextMenuItem.Click += new System.EventHandler(this.editNoteContextMenuItem_Click);
            // 
            // toolStripSeparatorRatios
            // 
            this.toolStripSeparatorRatios.Name = "toolStripSeparatorRatios";
            resources.ApplyResources(this.toolStripSeparatorRatios, "toolStripSeparatorRatios");
            // 
            // ratiosContextMenuItem
            // 
            this.ratiosContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ratiosToGlobalStandardsMenuItem});
            this.ratiosContextMenuItem.Name = "ratiosContextMenuItem";
            resources.ApplyResources(this.ratiosContextMenuItem, "ratiosContextMenuItem");
            this.ratiosContextMenuItem.DropDownOpening += new System.EventHandler(this.ratiosContextMenuItem_DropDownOpening);
            // 
            // ratiosToGlobalStandardsMenuItem
            // 
            this.ratiosToGlobalStandardsMenuItem.Name = "ratiosToGlobalStandardsMenuItem";
            resources.ApplyResources(this.ratiosToGlobalStandardsMenuItem, "ratiosToGlobalStandardsMenuItem");
            // 
            // replicatesTreeContextMenuItem
            // 
            this.replicatesTreeContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.singleReplicateTreeContextMenuItem,
            this.bestReplicateTreeContextMenuItem});
            this.replicatesTreeContextMenuItem.Name = "replicatesTreeContextMenuItem";
            resources.ApplyResources(this.replicatesTreeContextMenuItem, "replicatesTreeContextMenuItem");
            this.replicatesTreeContextMenuItem.DropDownOpening += new System.EventHandler(this.replicatesTreeContextMenuItem_DropDownOpening);
            // 
            // singleReplicateTreeContextMenuItem
            // 
            this.singleReplicateTreeContextMenuItem.Name = "singleReplicateTreeContextMenuItem";
            resources.ApplyResources(this.singleReplicateTreeContextMenuItem, "singleReplicateTreeContextMenuItem");
            this.singleReplicateTreeContextMenuItem.Click += new System.EventHandler(this.singleReplicateTreeContextMenuItem_Click);
            // 
            // bestReplicateTreeContextMenuItem
            // 
            this.bestReplicateTreeContextMenuItem.Name = "bestReplicateTreeContextMenuItem";
            resources.ApplyResources(this.bestReplicateTreeContextMenuItem, "bestReplicateTreeContextMenuItem");
            this.bestReplicateTreeContextMenuItem.Click += new System.EventHandler(this.bestReplicateTreeContextMenuItem_Click);
            // 
            // TreeNodeContextMenu
            // 
            this.Name = "TreeNodeContextMenu";
            this.contextMenuTreeNode.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ContextMenuStrip contextMenuTreeNode;
        private System.Windows.Forms.ToolStripMenuItem cutContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pasteContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem deleteContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem expandSelectionContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem expandSelectionNoneContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem expandSelectionProteinsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem expandSelectionPeptidesContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem expandSelectionPrecursorsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pickChildrenContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addMoleculeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addSmallMoleculePrecursorContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addTransitionMoleculeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removePeakContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem setStandardTypeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem noStandardContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem normStandardContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem surrogateStandardContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem qcStandardContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem irtStandardContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem modifyPeptideContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editSpectrumFilterContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toggleQuantitativeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem markTransitionsQuantitativeContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator7;
        private System.Windows.Forms.ToolStripMenuItem editNoteContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparatorRatios;
        private System.Windows.Forms.ToolStripMenuItem ratiosContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem ratiosToGlobalStandardsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem replicatesTreeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem singleReplicateTreeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem bestReplicateTreeContextMenuItem;
    }
}
