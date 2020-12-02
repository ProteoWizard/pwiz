namespace pwiz.Skyline.Controls.Clustering
{
    partial class HierarchicalClusterGraph
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
            this.splitContainerVertical = new System.Windows.Forms.SplitContainer();
            this.rowDendrogram = new pwiz.Common.Controls.Clustering.DendrogramControl();
            this.splitContainerHorizontal = new System.Windows.Forms.SplitContainer();
            this.columnDendrogram = new pwiz.Common.Controls.Clustering.DendrogramControl();
            this.zedGraphControl1 = new ZedGraph.ZedGraphControl();
            ((System.ComponentModel.ISupportInitialize)(this.ModeUIExtender)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerVertical)).BeginInit();
            this.splitContainerVertical.Panel1.SuspendLayout();
            this.splitContainerVertical.Panel2.SuspendLayout();
            this.splitContainerVertical.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerHorizontal)).BeginInit();
            this.splitContainerHorizontal.Panel1.SuspendLayout();
            this.splitContainerHorizontal.Panel2.SuspendLayout();
            this.splitContainerHorizontal.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainerVertical
            // 
            this.splitContainerVertical.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerVertical.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainerVertical.Location = new System.Drawing.Point(0, 0);
            this.splitContainerVertical.Name = "splitContainerVertical";
            // 
            // splitContainerVertical.Panel1
            // 
            this.splitContainerVertical.Panel1.Controls.Add(this.rowDendrogram);
            // 
            // splitContainerVertical.Panel2
            // 
            this.splitContainerVertical.Panel2.Controls.Add(this.splitContainerHorizontal);
            this.splitContainerVertical.Size = new System.Drawing.Size(800, 450);
            this.splitContainerVertical.SplitterDistance = 100;
            this.splitContainerVertical.TabIndex = 0;
            // 
            // rowDendrogram
            // 
            this.rowDendrogram.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.rowDendrogram.DendrogramLocation = System.Windows.Forms.DockStyle.Left;
            this.rowDendrogram.Location = new System.Drawing.Point(0, 180);
            this.rowDendrogram.Name = "rowDendrogram";
            this.rowDendrogram.RectilinearLines = true;
            this.rowDendrogram.Size = new System.Drawing.Size(100, 270);
            this.rowDendrogram.TabIndex = 0;
            // 
            // splitContainerHorizontal
            // 
            this.splitContainerHorizontal.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerHorizontal.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainerHorizontal.Location = new System.Drawing.Point(0, 0);
            this.splitContainerHorizontal.Name = "splitContainerHorizontal";
            this.splitContainerHorizontal.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainerHorizontal.Panel1
            // 
            this.splitContainerHorizontal.Panel1.Controls.Add(this.columnDendrogram);
            // 
            // splitContainerHorizontal.Panel2
            // 
            this.splitContainerHorizontal.Panel2.Controls.Add(this.zedGraphControl1);
            this.splitContainerHorizontal.Size = new System.Drawing.Size(696, 450);
            this.splitContainerHorizontal.SplitterDistance = 100;
            this.splitContainerHorizontal.TabIndex = 0;
            // 
            // columnDendrogram
            // 
            this.columnDendrogram.DendrogramLocation = System.Windows.Forms.DockStyle.Top;
            this.columnDendrogram.Dock = System.Windows.Forms.DockStyle.Fill;
            this.columnDendrogram.Location = new System.Drawing.Point(0, 0);
            this.columnDendrogram.Name = "columnDendrogram";
            this.columnDendrogram.RectilinearLines = true;
            this.columnDendrogram.Size = new System.Drawing.Size(696, 100);
            this.columnDendrogram.TabIndex = 0;
            // 
            // zedGraphControl1
            // 
            this.zedGraphControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.zedGraphControl1.Location = new System.Drawing.Point(0, 0);
            this.zedGraphControl1.Name = "zedGraphControl1";
            this.zedGraphControl1.ScrollGrace = 0D;
            this.zedGraphControl1.ScrollMaxX = 0D;
            this.zedGraphControl1.ScrollMaxY = 0D;
            this.zedGraphControl1.ScrollMaxY2 = 0D;
            this.zedGraphControl1.ScrollMinX = 0D;
            this.zedGraphControl1.ScrollMinY = 0D;
            this.zedGraphControl1.ScrollMinY2 = 0D;
            this.zedGraphControl1.Size = new System.Drawing.Size(696, 346);
            this.zedGraphControl1.TabIndex = 0;
            this.zedGraphControl1.ZoomEvent += new ZedGraph.ZedGraphControl.ZoomEventHandler(this.zedGraphControl1_ZoomEvent);
            this.zedGraphControl1.Resize += new System.EventHandler(this.zedGraphControl1_Resize);
            // 
            // HierarchicalClusterGraph
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.splitContainerVertical);
            this.Name = "HierarchicalClusterGraph";
            this.TabText = "HierarchicalClusterGraph";
            this.Text = "HierarchicalClusterGraph";
            ((System.ComponentModel.ISupportInitialize)(this.ModeUIExtender)).EndInit();
            this.splitContainerVertical.Panel1.ResumeLayout(false);
            this.splitContainerVertical.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerVertical)).EndInit();
            this.splitContainerVertical.ResumeLayout(false);
            this.splitContainerHorizontal.Panel1.ResumeLayout(false);
            this.splitContainerHorizontal.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerHorizontal)).EndInit();
            this.splitContainerHorizontal.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainerVertical;
        private System.Windows.Forms.SplitContainer splitContainerHorizontal;
        private Common.Controls.Clustering.DendrogramControl rowDendrogram;
        private Common.Controls.Clustering.DendrogramControl columnDendrogram;
        private ZedGraph.ZedGraphControl zedGraphControl1;
    }
}