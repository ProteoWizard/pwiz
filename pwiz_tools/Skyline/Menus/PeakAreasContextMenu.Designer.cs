namespace pwiz.Skyline.Menus
{
    partial class PeakAreasContextMenu
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
            System.Windows.Forms.ToolStripSeparator toolStripSeparator57;
            this.areaGraphContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaReplicateComparisonContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaPeptideComparisonContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaRelativeAbundanceContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCVHistogramContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCVHistogram2DContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.graphTypeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.barAreaGraphDisplayTypeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.lineAreaGraphDisplayTypeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaNormalizeContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.abundanceTargetsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.abundanceTargetsPeptidesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.abundanceTargetsProteinsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.excludeTargetsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.excludeTargetsStandardsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.excludeTargetsPeptideListMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showPeakAreaLegendContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showLibraryPeakAreaContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showDotProductToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peptideLogScaleContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.relativeAbundanceLogScaleContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaPropsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCVbinWidthToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCV05binWidthToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCV10binWidthToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCV15binWidthToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCV20binWidthToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pointsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCVtargetsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCVdecoysToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCVTransitionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCVAllTransitionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCVCountTransitionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCVBestTransitionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator58 = new System.Windows.Forms.ToolStripSeparator();
            this.areaCVPrecursorsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCVProductsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCVNormalizedToToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.areaCVLogScaleToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeAboveCVCutoffToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.relativeAbundanceFormattingMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuPeakAreas = new System.Windows.Forms.ContextMenuStrip(this.components);
            toolStripSeparator57 = new System.Windows.Forms.ToolStripSeparator();
            this.contextMenuPeakAreas.SuspendLayout();
            this.SuspendLayout();
            // 
            // areaGraphContextMenuItem
            // 
            this.areaGraphContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.areaReplicateComparisonContextMenuItem,
            this.areaPeptideComparisonContextMenuItem,
            this.areaRelativeAbundanceContextMenuItem,
            this.areaCVHistogramContextMenuItem,
            this.areaCVHistogram2DContextMenuItem});
            this.areaGraphContextMenuItem.Name = "areaGraphContextMenuItem";
            this.areaGraphContextMenuItem.Size = new System.Drawing.Size(209, 22);
            this.areaGraphContextMenuItem.Text = "Graph";
            this.areaGraphContextMenuItem.DropDownOpening += new System.EventHandler(this.areaGraphMenuItem_DropDownOpening);
            // 
            // areaReplicateComparisonContextMenuItem
            // 
            this.areaReplicateComparisonContextMenuItem.Name = "areaReplicateComparisonContextMenuItem";
            this.areaReplicateComparisonContextMenuItem.Size = new System.Drawing.Size(190, 22);
            this.areaReplicateComparisonContextMenuItem.Text = "Replicate Comparison";
            this.areaReplicateComparisonContextMenuItem.Click += new System.EventHandler(this.areaReplicateComparisonMenuItem_Click);
            // 
            // areaPeptideComparisonContextMenuItem
            // 
            this.areaPeptideComparisonContextMenuItem.Name = "areaPeptideComparisonContextMenuItem";
            this.areaPeptideComparisonContextMenuItem.Size = new System.Drawing.Size(190, 22);
            this.areaPeptideComparisonContextMenuItem.Text = "Peptide Comparison";
            this.areaPeptideComparisonContextMenuItem.Click += new System.EventHandler(this.areaPeptideComparisonMenuItem_Click);
            // 
            // areaRelativeAbundanceContextMenuItem
            // 
            this.areaRelativeAbundanceContextMenuItem.Name = "areaRelativeAbundanceContextMenuItem";
            this.areaRelativeAbundanceContextMenuItem.Size = new System.Drawing.Size(190, 22);
            this.areaRelativeAbundanceContextMenuItem.Text = "Relative Abundance";
            this.areaRelativeAbundanceContextMenuItem.Click += new System.EventHandler(this.areaRelativeAbundanceMenuItem_Click);
            // 
            // areaCVHistogramContextMenuItem
            // 
            this.areaCVHistogramContextMenuItem.Name = "areaCVHistogramContextMenuItem";
            this.areaCVHistogramContextMenuItem.Size = new System.Drawing.Size(190, 22);
            this.areaCVHistogramContextMenuItem.Text = "CV Histogram";
            this.areaCVHistogramContextMenuItem.Click += new System.EventHandler(this.areaCVHistogramToolStripMenuItem1_Click);
            // 
            // areaCVHistogram2DContextMenuItem
            // 
            this.areaCVHistogram2DContextMenuItem.Name = "areaCVHistogram2DContextMenuItem";
            this.areaCVHistogram2DContextMenuItem.Size = new System.Drawing.Size(190, 22);
            this.areaCVHistogram2DContextMenuItem.Text = "CV 2D Histogram";
            this.areaCVHistogram2DContextMenuItem.Click += new System.EventHandler(this.areaCVHistogram2DToolStripMenuItem1_Click);
            // 
            // graphTypeToolStripMenuItem
            // 
            this.graphTypeToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.barAreaGraphDisplayTypeMenuItem,
            this.lineAreaGraphDisplayTypeMenuItem});
            this.graphTypeToolStripMenuItem.Name = "graphTypeToolStripMenuItem";
            this.graphTypeToolStripMenuItem.Size = new System.Drawing.Size(209, 22);
            this.graphTypeToolStripMenuItem.Text = "Graph Type";
            // 
            // barAreaGraphDisplayTypeMenuItem
            // 
            this.barAreaGraphDisplayTypeMenuItem.Checked = true;
            this.barAreaGraphDisplayTypeMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.barAreaGraphDisplayTypeMenuItem.Name = "barAreaGraphDisplayTypeMenuItem";
            this.barAreaGraphDisplayTypeMenuItem.Size = new System.Drawing.Size(96, 22);
            this.barAreaGraphDisplayTypeMenuItem.Text = "Bar";
            this.barAreaGraphDisplayTypeMenuItem.Click += new System.EventHandler(this.barAreaGraphTypeMenuItem_Click);
            // 
            // lineAreaGraphDisplayTypeMenuItem
            // 
            this.lineAreaGraphDisplayTypeMenuItem.Name = "lineAreaGraphDisplayTypeMenuItem";
            this.lineAreaGraphDisplayTypeMenuItem.Size = new System.Drawing.Size(96, 22);
            this.lineAreaGraphDisplayTypeMenuItem.Text = "Line";
            this.lineAreaGraphDisplayTypeMenuItem.Click += new System.EventHandler(this.lineAreaGraphTypeMenuItem_Click);
            // 
            // areaNormalizeContextMenuItem
            // 
            this.areaNormalizeContextMenuItem.Name = "areaNormalizeContextMenuItem";
            this.areaNormalizeContextMenuItem.Size = new System.Drawing.Size(209, 22);
            this.areaNormalizeContextMenuItem.Text = "Normalized To";
            // 
            // abundanceTargetsMenuItem
            // 
            this.abundanceTargetsMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.abundanceTargetsPeptidesMenuItem,
            this.abundanceTargetsProteinsMenuItem});
            this.abundanceTargetsMenuItem.Name = "abundanceTargetsMenuItem";
            this.abundanceTargetsMenuItem.Size = new System.Drawing.Size(209, 22);
            this.abundanceTargetsMenuItem.Text = "Targets";
            // 
            // abundanceTargetsPeptidesMenuItem
            // 
            this.abundanceTargetsPeptidesMenuItem.Name = "abundanceTargetsPeptidesMenuItem";
            this.abundanceTargetsPeptidesMenuItem.Size = new System.Drawing.Size(119, 22);
            this.abundanceTargetsPeptidesMenuItem.Text = "Peptides";
            this.abundanceTargetsPeptidesMenuItem.Click += new System.EventHandler(this.abundanceTargetsPeptidesMenuItem_Click);
            // 
            // abundanceTargetsProteinsMenuItem
            // 
            this.abundanceTargetsProteinsMenuItem.Name = "abundanceTargetsProteinsMenuItem";
            this.abundanceTargetsProteinsMenuItem.Size = new System.Drawing.Size(119, 22);
            this.abundanceTargetsProteinsMenuItem.Text = "Proteins";
            this.abundanceTargetsProteinsMenuItem.Click += new System.EventHandler(this.abundanceTargetsProteinsMenuItem_Click);
            // 
            // excludeTargetsMenuItem
            // 
            this.excludeTargetsMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.excludeTargetsStandardsMenuItem,
            this.excludeTargetsPeptideListMenuItem});
            this.excludeTargetsMenuItem.Name = "excludeTargetsMenuItem";
            this.excludeTargetsMenuItem.Size = new System.Drawing.Size(209, 22);
            this.excludeTargetsMenuItem.Text = "Exclude";
            // 
            // excludeTargetsStandardsMenuItem
            // 
            this.excludeTargetsStandardsMenuItem.Name = "excludeTargetsStandardsMenuItem";
            this.excludeTargetsStandardsMenuItem.Size = new System.Drawing.Size(140, 22);
            this.excludeTargetsStandardsMenuItem.Text = "Standards";
            this.excludeTargetsStandardsMenuItem.Click += new System.EventHandler(this.excludeTargetsStandardsMenuItem_Click);
            // 
            // excludeTargetsPeptideListMenuItem
            // 
            this.excludeTargetsPeptideListMenuItem.Name = "excludeTargetsPeptideListMenuItem";
            this.excludeTargetsPeptideListMenuItem.Size = new System.Drawing.Size(140, 22);
            this.excludeTargetsPeptideListMenuItem.Text = "Peptide Lists";
            this.excludeTargetsPeptideListMenuItem.Click += new System.EventHandler(this.excludeTargetsPeptideListMenuItem_Click);
            // 
            // showPeakAreaLegendContextMenuItem
            // 
            this.showPeakAreaLegendContextMenuItem.Name = "showPeakAreaLegendContextMenuItem";
            this.showPeakAreaLegendContextMenuItem.Size = new System.Drawing.Size(209, 22);
            this.showPeakAreaLegendContextMenuItem.Text = "Legend";
            this.showPeakAreaLegendContextMenuItem.Click += new System.EventHandler(this.showPeakAreaLegendContextMenuItem_Click);
            // 
            // showLibraryPeakAreaContextMenuItem
            // 
            this.showLibraryPeakAreaContextMenuItem.CheckOnClick = true;
            this.showLibraryPeakAreaContextMenuItem.Name = "showLibraryPeakAreaContextMenuItem";
            this.showLibraryPeakAreaContextMenuItem.Size = new System.Drawing.Size(209, 22);
            this.showLibraryPeakAreaContextMenuItem.Text = "Show Library";
            this.showLibraryPeakAreaContextMenuItem.Click += new System.EventHandler(this.showLibraryPeakAreaContextMenuItem_Click);
            // 
            // showDotProductToolStripMenuItem
            // 
            this.showDotProductToolStripMenuItem.Name = "showDotProductToolStripMenuItem";
            this.showDotProductToolStripMenuItem.Size = new System.Drawing.Size(209, 22);
            this.showDotProductToolStripMenuItem.Text = "Show Dot Product";
            // 
            // peptideLogScaleContextMenuItem
            // 
            this.peptideLogScaleContextMenuItem.CheckOnClick = true;
            this.peptideLogScaleContextMenuItem.Name = "peptideLogScaleContextMenuItem";
            this.peptideLogScaleContextMenuItem.Size = new System.Drawing.Size(209, 22);
            this.peptideLogScaleContextMenuItem.Text = "Log Scale";
            this.peptideLogScaleContextMenuItem.Click += new System.EventHandler(this.peptideLogScaleContextMenuItem_Click);
            // 
            // relativeAbundanceLogScaleContextMenuItem
            // 
            this.relativeAbundanceLogScaleContextMenuItem.CheckOnClick = true;
            this.relativeAbundanceLogScaleContextMenuItem.Name = "relativeAbundanceLogScaleContextMenuItem";
            this.relativeAbundanceLogScaleContextMenuItem.Size = new System.Drawing.Size(209, 22);
            this.relativeAbundanceLogScaleContextMenuItem.Text = "Log Scale";
            this.relativeAbundanceLogScaleContextMenuItem.Click += new System.EventHandler(this.relativeAbundanceLogScaleContextMenuItem_Click);
            // 
            // areaPropsContextMenuItem
            // 
            this.areaPropsContextMenuItem.Name = "areaPropsContextMenuItem";
            this.areaPropsContextMenuItem.Size = new System.Drawing.Size(209, 22);
            this.areaPropsContextMenuItem.Text = "Properties...";
            this.areaPropsContextMenuItem.Click += new System.EventHandler(this.areaPropsContextMenuItem_Click);
            // 
            // areaCVbinWidthToolStripMenuItem
            // 
            this.areaCVbinWidthToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.areaCV05binWidthToolStripMenuItem,
            this.areaCV10binWidthToolStripMenuItem,
            this.areaCV15binWidthToolStripMenuItem,
            this.areaCV20binWidthToolStripMenuItem});
            this.areaCVbinWidthToolStripMenuItem.Name = "areaCVbinWidthToolStripMenuItem";
            this.areaCVbinWidthToolStripMenuItem.Size = new System.Drawing.Size(209, 22);
            this.areaCVbinWidthToolStripMenuItem.Text = "Bin Width";
            // 
            // areaCV05binWidthToolStripMenuItem
            // 
            this.areaCV05binWidthToolStripMenuItem.Name = "areaCV05binWidthToolStripMenuItem";
            this.areaCV05binWidthToolStripMenuItem.Size = new System.Drawing.Size(67, 22);
            this.areaCV05binWidthToolStripMenuItem.Click += new System.EventHandler(this.areaCV05binWidthToolStripMenuItem_Click);
            // 
            // areaCV10binWidthToolStripMenuItem
            // 
            this.areaCV10binWidthToolStripMenuItem.Name = "areaCV10binWidthToolStripMenuItem";
            this.areaCV10binWidthToolStripMenuItem.Size = new System.Drawing.Size(67, 22);
            this.areaCV10binWidthToolStripMenuItem.Click += new System.EventHandler(this.areaCV10binWidthToolStripMenuItem_Click);
            // 
            // areaCV15binWidthToolStripMenuItem
            // 
            this.areaCV15binWidthToolStripMenuItem.Name = "areaCV15binWidthToolStripMenuItem";
            this.areaCV15binWidthToolStripMenuItem.Size = new System.Drawing.Size(67, 22);
            this.areaCV15binWidthToolStripMenuItem.Click += new System.EventHandler(this.areaCV15binWidthToolStripMenuItem_Click);
            // 
            // areaCV20binWidthToolStripMenuItem
            // 
            this.areaCV20binWidthToolStripMenuItem.Name = "areaCV20binWidthToolStripMenuItem";
            this.areaCV20binWidthToolStripMenuItem.Size = new System.Drawing.Size(67, 22);
            this.areaCV20binWidthToolStripMenuItem.Click += new System.EventHandler(this.areaCV20binWidthToolStripMenuItem_Click);
            // 
            // pointsToolStripMenuItem
            // 
            this.pointsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.areaCVtargetsToolStripMenuItem,
            this.areaCVdecoysToolStripMenuItem});
            this.pointsToolStripMenuItem.Name = "pointsToolStripMenuItem";
            this.pointsToolStripMenuItem.Size = new System.Drawing.Size(209, 22);
            this.pointsToolStripMenuItem.Text = "Points";
            // 
            // areaCVtargetsToolStripMenuItem
            // 
            this.areaCVtargetsToolStripMenuItem.Name = "areaCVtargetsToolStripMenuItem";
            this.areaCVtargetsToolStripMenuItem.Size = new System.Drawing.Size(112, 22);
            this.areaCVtargetsToolStripMenuItem.Click += new System.EventHandler(this.areaCVtargetsToolStripMenuItem_Click);
            // 
            // areaCVdecoysToolStripMenuItem
            // 
            this.areaCVdecoysToolStripMenuItem.Name = "areaCVdecoysToolStripMenuItem";
            this.areaCVdecoysToolStripMenuItem.Size = new System.Drawing.Size(112, 22);
            this.areaCVdecoysToolStripMenuItem.Text = "Decoys";
            this.areaCVdecoysToolStripMenuItem.Click += new System.EventHandler(this.areaCVdecoysToolStripMenuItem_Click);
            // 
            // areaCVTransitionsToolStripMenuItem
            // 
            this.areaCVTransitionsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.areaCVAllTransitionsToolStripMenuItem,
            this.areaCVCountTransitionsToolStripMenuItem,
            this.areaCVBestTransitionsToolStripMenuItem,
            this.toolStripSeparator58,
            this.areaCVPrecursorsToolStripMenuItem,
            this.areaCVProductsToolStripMenuItem});
            this.areaCVTransitionsToolStripMenuItem.Name = "areaCVTransitionsToolStripMenuItem";
            this.areaCVTransitionsToolStripMenuItem.Size = new System.Drawing.Size(209, 22);
            this.areaCVTransitionsToolStripMenuItem.Text = "Transitions";
            // 
            // areaCVAllTransitionsToolStripMenuItem
            // 
            this.areaCVAllTransitionsToolStripMenuItem.Name = "areaCVAllTransitionsToolStripMenuItem";
            this.areaCVAllTransitionsToolStripMenuItem.Size = new System.Drawing.Size(129, 22);
            this.areaCVAllTransitionsToolStripMenuItem.Text = "All";
            this.areaCVAllTransitionsToolStripMenuItem.Click += new System.EventHandler(this.areaCVAllTransitionsToolStripMenuItem_Click);
            // 
            // areaCVCountTransitionsToolStripMenuItem
            // 
            this.areaCVCountTransitionsToolStripMenuItem.Name = "areaCVCountTransitionsToolStripMenuItem";
            this.areaCVCountTransitionsToolStripMenuItem.Size = new System.Drawing.Size(129, 22);
            this.areaCVCountTransitionsToolStripMenuItem.Text = "Count";
            // 
            // areaCVBestTransitionsToolStripMenuItem
            // 
            this.areaCVBestTransitionsToolStripMenuItem.Name = "areaCVBestTransitionsToolStripMenuItem";
            this.areaCVBestTransitionsToolStripMenuItem.Size = new System.Drawing.Size(129, 22);
            this.areaCVBestTransitionsToolStripMenuItem.Text = "Best";
            this.areaCVBestTransitionsToolStripMenuItem.Click += new System.EventHandler(this.areaCVBestTransitionsToolStripMenuItem_Click);
            // 
            // toolStripSeparator58
            // 
            this.toolStripSeparator58.Name = "toolStripSeparator58";
            this.toolStripSeparator58.Size = new System.Drawing.Size(126, 6);
            // 
            // areaCVPrecursorsToolStripMenuItem
            // 
            this.areaCVPrecursorsToolStripMenuItem.Name = "areaCVPrecursorsToolStripMenuItem";
            this.areaCVPrecursorsToolStripMenuItem.Size = new System.Drawing.Size(129, 22);
            this.areaCVPrecursorsToolStripMenuItem.Text = "Precursors";
            this.areaCVPrecursorsToolStripMenuItem.Click += new System.EventHandler(this.areaCVPrecursorsToolStripMenuItem_Click);
            // 
            // areaCVProductsToolStripMenuItem
            // 
            this.areaCVProductsToolStripMenuItem.Name = "areaCVProductsToolStripMenuItem";
            this.areaCVProductsToolStripMenuItem.Size = new System.Drawing.Size(129, 22);
            this.areaCVProductsToolStripMenuItem.Text = "Products";
            this.areaCVProductsToolStripMenuItem.Click += new System.EventHandler(this.areaCVProductsToolStripMenuItem_Click);
            // 
            // areaCVNormalizedToToolStripMenuItem
            // 
            this.areaCVNormalizedToToolStripMenuItem.Name = "areaCVNormalizedToToolStripMenuItem";
            this.areaCVNormalizedToToolStripMenuItem.Size = new System.Drawing.Size(209, 22);
            this.areaCVNormalizedToToolStripMenuItem.Text = "Normalized To";
            // 
            // areaCVLogScaleToolStripMenuItem
            // 
            this.areaCVLogScaleToolStripMenuItem.Name = "areaCVLogScaleToolStripMenuItem";
            this.areaCVLogScaleToolStripMenuItem.Size = new System.Drawing.Size(209, 22);
            this.areaCVLogScaleToolStripMenuItem.Text = "Log Scale";
            this.areaCVLogScaleToolStripMenuItem.Click += new System.EventHandler(this.areaCVLogScaleToolStripMenuItem_Click);
            // 
            // removeAboveCVCutoffToolStripMenuItem
            // 
            this.removeAboveCVCutoffToolStripMenuItem.Name = "removeAboveCVCutoffToolStripMenuItem";
            this.removeAboveCVCutoffToolStripMenuItem.Size = new System.Drawing.Size(209, 22);
            this.removeAboveCVCutoffToolStripMenuItem.Text = "Remove Above CV Cutoff";
            this.removeAboveCVCutoffToolStripMenuItem.Click += new System.EventHandler(this.removeAboveCVCutoffToolStripMenuItem_Click);
            // 
            // relativeAbundanceFormattingMenuItem
            // 
            this.relativeAbundanceFormattingMenuItem.Name = "relativeAbundanceFormattingMenuItem";
            this.relativeAbundanceFormattingMenuItem.Size = new System.Drawing.Size(209, 22);
            this.relativeAbundanceFormattingMenuItem.Text = "Formatting...";
            this.relativeAbundanceFormattingMenuItem.Click += new System.EventHandler(this.relativeAbundanceFormattingMenuItem_Click);
            // 
            // toolStripSeparator57
            // 
            toolStripSeparator57.Name = "toolStripSeparator57";
            toolStripSeparator57.Size = new System.Drawing.Size(206, 6);
            // 
            // contextMenuPeakAreas
            // 
            this.contextMenuPeakAreas.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.areaGraphContextMenuItem,
            this.graphTypeToolStripMenuItem,
            this.areaNormalizeContextMenuItem,
            this.abundanceTargetsMenuItem,
            this.excludeTargetsMenuItem,
            this.showPeakAreaLegendContextMenuItem,
            this.showLibraryPeakAreaContextMenuItem,
            this.showDotProductToolStripMenuItem,
            this.peptideLogScaleContextMenuItem,
            this.relativeAbundanceLogScaleContextMenuItem,
            this.areaPropsContextMenuItem,
            this.areaCVbinWidthToolStripMenuItem,
            this.pointsToolStripMenuItem,
            this.areaCVTransitionsToolStripMenuItem,
            this.areaCVNormalizedToToolStripMenuItem,
            this.areaCVLogScaleToolStripMenuItem,
            this.removeAboveCVCutoffToolStripMenuItem,
            this.relativeAbundanceFormattingMenuItem,
            toolStripSeparator57});
            this.contextMenuPeakAreas.Name = "contextMenuPeakAreas";
            this.contextMenuPeakAreas.Size = new System.Drawing.Size(210, 428);
            // 
            // PeakAreasContextMenu
            // 
            this.Name = "PeakAreasContextMenu";
            this.contextMenuPeakAreas.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ContextMenuStrip contextMenuPeakAreas;
        private System.Windows.Forms.ToolStripMenuItem areaGraphContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaReplicateComparisonContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaPeptideComparisonContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaRelativeAbundanceContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCVHistogramContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCVHistogram2DContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem graphTypeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem barAreaGraphDisplayTypeMenuItem;
        private System.Windows.Forms.ToolStripMenuItem lineAreaGraphDisplayTypeMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaNormalizeContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem abundanceTargetsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem abundanceTargetsProteinsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem abundanceTargetsPeptidesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem excludeTargetsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem excludeTargetsPeptideListMenuItem;
        private System.Windows.Forms.ToolStripMenuItem excludeTargetsStandardsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showPeakAreaLegendContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showLibraryPeakAreaContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showDotProductToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem peptideLogScaleContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem relativeAbundanceLogScaleContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaPropsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCVbinWidthToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCV05binWidthToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCV10binWidthToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCV15binWidthToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCV20binWidthToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pointsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCVtargetsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCVdecoysToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCVTransitionsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCVAllTransitionsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCVCountTransitionsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCVBestTransitionsToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator58;
        private System.Windows.Forms.ToolStripMenuItem areaCVPrecursorsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCVProductsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCVNormalizedToToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem areaCVLogScaleToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeAboveCVCutoffToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem relativeAbundanceFormattingMenuItem;
    }
}
