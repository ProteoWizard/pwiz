namespace ExampleInteractiveTool
{
    partial class MainForm
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
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.viewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.infoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.selectEndNodeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.insertFASTAToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replicatesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.allToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.autoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.graph = new ZedGraph.ZedGraphControl();
            this.chromatogramGraph = new ZedGraph.ZedGraphControl();
            this.addSpectralLibraryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.viewToolStripMenuItem,
            this.replicatesToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(617, 24);
            this.menuStrip1.TabIndex = 3;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // viewToolStripMenuItem
            // 
            this.viewToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.infoToolStripMenuItem,
            this.selectEndNodeToolStripMenuItem,
            this.insertFASTAToolStripMenuItem,
            this.addSpectralLibraryToolStripMenuItem});
            this.viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            this.viewToolStripMenuItem.Size = new System.Drawing.Size(68, 20);
            this.viewToolStripMenuItem.Text = "Examples";
            // 
            // infoToolStripMenuItem
            // 
            this.infoToolStripMenuItem.Name = "infoToolStripMenuItem";
            this.infoToolStripMenuItem.Size = new System.Drawing.Size(176, 22);
            this.infoToolStripMenuItem.Text = "Show info";
            this.infoToolStripMenuItem.Click += new System.EventHandler(this.InfoClick);
            // 
            // selectEndNodeToolStripMenuItem
            // 
            this.selectEndNodeToolStripMenuItem.Name = "selectEndNodeToolStripMenuItem";
            this.selectEndNodeToolStripMenuItem.Size = new System.Drawing.Size(176, 22);
            this.selectEndNodeToolStripMenuItem.Text = "Select end node";
            this.selectEndNodeToolStripMenuItem.Click += new System.EventHandler(this.selectEndNodeToolStripMenuItem_Click);
            // 
            // insertFASTAToolStripMenuItem
            // 
            this.insertFASTAToolStripMenuItem.Name = "insertFASTAToolStripMenuItem";
            this.insertFASTAToolStripMenuItem.Size = new System.Drawing.Size(176, 22);
            this.insertFASTAToolStripMenuItem.Text = "Insert FASTA";
            this.insertFASTAToolStripMenuItem.Click += new System.EventHandler(this.insertFASTAToolStripMenuItem_Click);
            // 
            // replicatesToolStripMenuItem
            // 
            this.replicatesToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.allToolStripMenuItem,
            this.toolStripSeparator1,
            this.autoToolStripMenuItem,
            this.toolStripMenuItem1});
            this.replicatesToolStripMenuItem.Name = "replicatesToolStripMenuItem";
            this.replicatesToolStripMenuItem.Size = new System.Drawing.Size(72, 20);
            this.replicatesToolStripMenuItem.Text = "Replicates";
            // 
            // allToolStripMenuItem
            // 
            this.allToolStripMenuItem.Name = "allToolStripMenuItem";
            this.allToolStripMenuItem.Size = new System.Drawing.Size(100, 22);
            this.allToolStripMenuItem.Text = "All";
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(97, 6);
            // 
            // autoToolStripMenuItem
            // 
            this.autoToolStripMenuItem.Name = "autoToolStripMenuItem";
            this.autoToolStripMenuItem.Size = new System.Drawing.Size(100, 22);
            this.autoToolStripMenuItem.Text = "Auto";
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(97, 6);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 24);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.graph);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.chromatogramGraph);
            this.splitContainer1.Size = new System.Drawing.Size(617, 587);
            this.splitContainer1.SplitterDistance = 368;
            this.splitContainer1.SplitterWidth = 8;
            this.splitContainer1.TabIndex = 4;
            // 
            // graph
            // 
            this.graph.Dock = System.Windows.Forms.DockStyle.Fill;
            this.graph.Location = new System.Drawing.Point(0, 0);
            this.graph.Name = "graph";
            this.graph.ScrollGrace = 0D;
            this.graph.ScrollMaxX = 0D;
            this.graph.ScrollMaxY = 0D;
            this.graph.ScrollMaxY2 = 0D;
            this.graph.ScrollMinX = 0D;
            this.graph.ScrollMinY = 0D;
            this.graph.ScrollMinY2 = 0D;
            this.graph.Size = new System.Drawing.Size(617, 368);
            this.graph.TabIndex = 2;
            // 
            // chromatogramGraph
            // 
            this.chromatogramGraph.Dock = System.Windows.Forms.DockStyle.Fill;
            this.chromatogramGraph.Location = new System.Drawing.Point(0, 0);
            this.chromatogramGraph.Name = "chromatogramGraph";
            this.chromatogramGraph.ScrollGrace = 0D;
            this.chromatogramGraph.ScrollMaxX = 0D;
            this.chromatogramGraph.ScrollMaxY = 0D;
            this.chromatogramGraph.ScrollMaxY2 = 0D;
            this.chromatogramGraph.ScrollMinX = 0D;
            this.chromatogramGraph.ScrollMinY = 0D;
            this.chromatogramGraph.ScrollMinY2 = 0D;
            this.chromatogramGraph.Size = new System.Drawing.Size(617, 211);
            this.chromatogramGraph.TabIndex = 2;
            // 
            // addSpectralLibraryToolStripMenuItem
            // 
            this.addSpectralLibraryToolStripMenuItem.Name = "addSpectralLibraryToolStripMenuItem";
            this.addSpectralLibraryToolStripMenuItem.Size = new System.Drawing.Size(176, 22);
            this.addSpectralLibraryToolStripMenuItem.Text = "Add spectral library";
            this.addSpectralLibraryToolStripMenuItem.Click += new System.EventHandler(this.addSpectralLibraryToolStripMenuItem_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(617, 611);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "MainForm";
            this.Text = "Example Interactive Tool";
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem viewToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem infoToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem replicatesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem allToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private ZedGraph.ZedGraphControl graph;
        private ZedGraph.ZedGraphControl chromatogramGraph;
        private System.Windows.Forms.ToolStripMenuItem autoToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem selectEndNodeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem insertFASTAToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addSpectralLibraryToolStripMenuItem;
    }
}

