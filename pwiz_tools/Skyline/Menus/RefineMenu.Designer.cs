namespace pwiz.Skyline.Menus
{
    partial class RefineMenu
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RefineMenu));
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.refineToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.reintegrateToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.generateDecoysMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.compareModelsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator59 = new System.Windows.Forms.ToolStripSeparator();
            this.removeMissingResultsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator45 = new System.Windows.Forms.ToolStripSeparator();
            this.acceptProteinsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeEmptyProteinsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.associateFASTAMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.renameProteinsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.sortProteinsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.sortProteinsByNameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.sortProteinsByAccessionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.sortProteinsByPreferredNameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.sortProteinsByGeneToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator43 = new System.Windows.Forms.ToolStripSeparator();
            this.acceptPeptidesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeEmptyPeptidesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeDuplicatePeptidesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeRepeatedPeptidesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator35 = new System.Windows.Forms.ToolStripSeparator();
            this.permuteIsotopeModificationsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.refineAdvancedMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.refineToolStripMenuItem});
            resources.ApplyResources(this.menuStrip1, "menuStrip1");
            this.menuStrip1.Name = "menuStrip1";
            // 
            // refineToolStripMenuItem
            // 
            this.refineToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.reintegrateToolStripMenuItem,
            this.generateDecoysMenuItem,
            this.compareModelsToolStripMenuItem,
            this.toolStripSeparator59,
            this.removeMissingResultsMenuItem,
            this.toolStripSeparator45,
            this.acceptProteinsMenuItem,
            this.removeEmptyProteinsMenuItem,
            this.associateFASTAMenuItem,
            this.renameProteinsMenuItem,
            this.sortProteinsMenuItem,
            this.toolStripSeparator43,
            this.acceptPeptidesMenuItem,
            this.removeEmptyPeptidesMenuItem,
            this.removeDuplicatePeptidesMenuItem,
            this.removeRepeatedPeptidesMenuItem,
            this.toolStripSeparator35,
            this.permuteIsotopeModificationsMenuItem,
            this.refineAdvancedMenuItem});
            this.refineToolStripMenuItem.Name = "refineToolStripMenuItem";
            resources.ApplyResources(this.refineToolStripMenuItem, "refineToolStripMenuItem");
            // 
            // reintegrateToolStripMenuItem
            // 
            this.reintegrateToolStripMenuItem.Name = "reintegrateToolStripMenuItem";
            resources.ApplyResources(this.reintegrateToolStripMenuItem, "reintegrateToolStripMenuItem");
            this.reintegrateToolStripMenuItem.Click += new System.EventHandler(this.reintegrateToolStripMenuItem_Click);
            // 
            // generateDecoysMenuItem
            // 
            this.generateDecoysMenuItem.Name = "generateDecoysMenuItem";
            resources.ApplyResources(this.generateDecoysMenuItem, "generateDecoysMenuItem");
            this.modeUIHandler.SetUIMode(this.generateDecoysMenuItem, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            this.generateDecoysMenuItem.Click += new System.EventHandler(this.generateDecoysMenuItem_Click);
            // 
            // compareModelsToolStripMenuItem
            // 
            this.compareModelsToolStripMenuItem.Name = "compareModelsToolStripMenuItem";
            resources.ApplyResources(this.compareModelsToolStripMenuItem, "compareModelsToolStripMenuItem");
            this.compareModelsToolStripMenuItem.Click += new System.EventHandler(this.compareModelsToolStripMenuItem_Click);
            // 
            // toolStripSeparator59
            // 
            this.toolStripSeparator59.Name = "toolStripSeparator59";
            resources.ApplyResources(this.toolStripSeparator59, "toolStripSeparator59");
            // 
            // removeMissingResultsMenuItem
            // 
            this.removeMissingResultsMenuItem.Name = "removeMissingResultsMenuItem";
            resources.ApplyResources(this.removeMissingResultsMenuItem, "removeMissingResultsMenuItem");
            this.removeMissingResultsMenuItem.Click += new System.EventHandler(this.removeMissingResultsMenuItem_Click);
            // 
            // toolStripSeparator45
            // 
            this.toolStripSeparator45.Name = "toolStripSeparator45";
            resources.ApplyResources(this.toolStripSeparator45, "toolStripSeparator45");
            // 
            // acceptProteinsMenuItem
            // 
            this.acceptProteinsMenuItem.Name = "acceptProteinsMenuItem";
            resources.ApplyResources(this.acceptProteinsMenuItem, "acceptProteinsMenuItem");
            this.modeUIHandler.SetUIMode(this.acceptProteinsMenuItem, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            this.acceptProteinsMenuItem.Click += new System.EventHandler(this.acceptProteinsMenuItem_Click);
            // 
            // removeEmptyProteinsMenuItem
            // 
            this.removeEmptyProteinsMenuItem.Name = "removeEmptyProteinsMenuItem";
            resources.ApplyResources(this.removeEmptyProteinsMenuItem, "removeEmptyProteinsMenuItem");
            this.removeEmptyProteinsMenuItem.Click += new System.EventHandler(this.removeEmptyProteinsMenuItem_Click);
            // 
            // associateFASTAMenuItem
            // 
            this.associateFASTAMenuItem.Name = "associateFASTAMenuItem";
            resources.ApplyResources(this.associateFASTAMenuItem, "associateFASTAMenuItem");
            this.modeUIHandler.SetUIMode(this.associateFASTAMenuItem, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            this.associateFASTAMenuItem.Click += new System.EventHandler(this.associateFASTAMenuItem_Click);
            // 
            // renameProteinsMenuItem
            // 
            this.renameProteinsMenuItem.Name = "renameProteinsMenuItem";
            resources.ApplyResources(this.renameProteinsMenuItem, "renameProteinsMenuItem");
            this.modeUIHandler.SetUIMode(this.renameProteinsMenuItem, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            this.renameProteinsMenuItem.Click += new System.EventHandler(this.renameProteinsToolStripMenuItem_Click);
            // 
            // sortProteinsMenuItem
            // 
            this.sortProteinsMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.sortProteinsByNameToolStripMenuItem,
            this.sortProteinsByAccessionToolStripMenuItem,
            this.sortProteinsByPreferredNameToolStripMenuItem,
            this.sortProteinsByGeneToolStripMenuItem});
            this.sortProteinsMenuItem.Name = "sortProteinsMenuItem";
            resources.ApplyResources(this.sortProteinsMenuItem, "sortProteinsMenuItem");
            // 
            // sortProteinsByNameToolStripMenuItem
            // 
            this.sortProteinsByNameToolStripMenuItem.Name = "sortProteinsByNameToolStripMenuItem";
            resources.ApplyResources(this.sortProteinsByNameToolStripMenuItem, "sortProteinsByNameToolStripMenuItem");
            this.sortProteinsByNameToolStripMenuItem.Click += new System.EventHandler(this.sortProteinsByNameToolStripMenuItem_Click);
            // 
            // sortProteinsByAccessionToolStripMenuItem
            // 
            this.sortProteinsByAccessionToolStripMenuItem.Name = "sortProteinsByAccessionToolStripMenuItem";
            resources.ApplyResources(this.sortProteinsByAccessionToolStripMenuItem, "sortProteinsByAccessionToolStripMenuItem");
            this.modeUIHandler.SetUIMode(this.sortProteinsByAccessionToolStripMenuItem, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            this.sortProteinsByAccessionToolStripMenuItem.Click += new System.EventHandler(this.sortProteinsByAccessionToolStripMenuItem_Click);
            // 
            // sortProteinsByPreferredNameToolStripMenuItem
            // 
            this.sortProteinsByPreferredNameToolStripMenuItem.Name = "sortProteinsByPreferredNameToolStripMenuItem";
            resources.ApplyResources(this.sortProteinsByPreferredNameToolStripMenuItem, "sortProteinsByPreferredNameToolStripMenuItem");
            this.modeUIHandler.SetUIMode(this.sortProteinsByPreferredNameToolStripMenuItem, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            this.sortProteinsByPreferredNameToolStripMenuItem.Click += new System.EventHandler(this.sortProteinsByPreferredNameToolStripMenuItem_Click);
            // 
            // sortProteinsByGeneToolStripMenuItem
            // 
            this.sortProteinsByGeneToolStripMenuItem.Name = "sortProteinsByGeneToolStripMenuItem";
            resources.ApplyResources(this.sortProteinsByGeneToolStripMenuItem, "sortProteinsByGeneToolStripMenuItem");
            this.modeUIHandler.SetUIMode(this.sortProteinsByGeneToolStripMenuItem, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            this.sortProteinsByGeneToolStripMenuItem.Click += new System.EventHandler(this.sortProteinsByGeneToolStripMenuItem_Click);
            // 
            // toolStripSeparator43
            // 
            this.toolStripSeparator43.Name = "toolStripSeparator43";
            resources.ApplyResources(this.toolStripSeparator43, "toolStripSeparator43");
            // 
            // acceptPeptidesMenuItem
            // 
            this.acceptPeptidesMenuItem.Name = "acceptPeptidesMenuItem";
            resources.ApplyResources(this.acceptPeptidesMenuItem, "acceptPeptidesMenuItem");
            this.modeUIHandler.SetUIMode(this.acceptPeptidesMenuItem, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            this.acceptPeptidesMenuItem.Click += new System.EventHandler(this.acceptPeptidesMenuItem_Click);
            // 
            // removeEmptyPeptidesMenuItem
            // 
            this.removeEmptyPeptidesMenuItem.Name = "removeEmptyPeptidesMenuItem";
            resources.ApplyResources(this.removeEmptyPeptidesMenuItem, "removeEmptyPeptidesMenuItem");
            this.removeEmptyPeptidesMenuItem.Click += new System.EventHandler(this.removeEmptyPeptidesMenuItem_Click);
            // 
            // removeDuplicatePeptidesMenuItem
            // 
            this.removeDuplicatePeptidesMenuItem.Name = "removeDuplicatePeptidesMenuItem";
            resources.ApplyResources(this.removeDuplicatePeptidesMenuItem, "removeDuplicatePeptidesMenuItem");
            this.removeDuplicatePeptidesMenuItem.Click += new System.EventHandler(this.removeDuplicatePeptidesMenuItem_Click);
            // 
            // removeRepeatedPeptidesMenuItem
            // 
            this.removeRepeatedPeptidesMenuItem.Name = "removeRepeatedPeptidesMenuItem";
            resources.ApplyResources(this.removeRepeatedPeptidesMenuItem, "removeRepeatedPeptidesMenuItem");
            this.removeRepeatedPeptidesMenuItem.Click += new System.EventHandler(this.removeRepeatedPeptidesMenuItem_Click);
            // 
            // toolStripSeparator35
            // 
            this.toolStripSeparator35.Name = "toolStripSeparator35";
            resources.ApplyResources(this.toolStripSeparator35, "toolStripSeparator35");
            // 
            // permuteIsotopeModificationsMenuItem
            // 
            this.permuteIsotopeModificationsMenuItem.Name = "permuteIsotopeModificationsMenuItem";
            resources.ApplyResources(this.permuteIsotopeModificationsMenuItem, "permuteIsotopeModificationsMenuItem");
            this.modeUIHandler.SetUIMode(this.permuteIsotopeModificationsMenuItem, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic);
            this.permuteIsotopeModificationsMenuItem.Click += new System.EventHandler(this.permuteIsotopeModificationsMenuItem_Click);
            // 
            // refineAdvancedMenuItem
            // 
            this.refineAdvancedMenuItem.Name = "refineAdvancedMenuItem";
            resources.ApplyResources(this.refineAdvancedMenuItem, "refineAdvancedMenuItem");
            this.refineAdvancedMenuItem.Click += new System.EventHandler(this.refineMenuItem_Click);
            // 
            // RefineMenu
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.menuStrip1);
            this.Name = "RefineMenu";
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem refineToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem reintegrateToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem generateDecoysMenuItem;
        private System.Windows.Forms.ToolStripMenuItem compareModelsToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator59;
        private System.Windows.Forms.ToolStripMenuItem removeMissingResultsMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator45;
        private System.Windows.Forms.ToolStripMenuItem acceptProteinsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeEmptyProteinsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem associateFASTAMenuItem;
        private System.Windows.Forms.ToolStripMenuItem renameProteinsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem sortProteinsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem sortProteinsByNameToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem sortProteinsByAccessionToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem sortProteinsByPreferredNameToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem sortProteinsByGeneToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator43;
        private System.Windows.Forms.ToolStripMenuItem acceptPeptidesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeEmptyPeptidesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeDuplicatePeptidesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeRepeatedPeptidesMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator35;
        private System.Windows.Forms.ToolStripMenuItem permuteIsotopeModificationsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem refineAdvancedMenuItem;
    }
}
