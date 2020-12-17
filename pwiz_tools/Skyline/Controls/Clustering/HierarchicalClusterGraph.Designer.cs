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
            this.zedGraphControl1 = new ZedGraph.ZedGraphControl();
            ((System.ComponentModel.ISupportInitialize)(this.ModeUIExtender)).BeginInit();
            this.SuspendLayout();
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
            this.zedGraphControl1.Size = new System.Drawing.Size(682, 450);
            this.zedGraphControl1.TabIndex = 0;
            this.zedGraphControl1.ContextMenuBuilder += new ZedGraph.ZedGraphControl.ContextMenuBuilderEventHandler(this.zedGraphControl1_ContextMenuBuilder);
            this.zedGraphControl1.Resize += new System.EventHandler(this.zedGraphControl1_Resize);
            // 
            // HierarchicalClusterGraph
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.ButtonHighlight;
            this.ClientSize = new System.Drawing.Size(682, 450);
            this.Controls.Add(this.zedGraphControl1);
            this.Name = "HierarchicalClusterGraph";
            this.ShowIcon = false;
            this.TabText = "HierarchicalClusterGraph";
            this.Text = "HierarchicalClusterGraph";
            ((System.ComponentModel.ISupportInitialize)(this.ModeUIExtender)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
        private ZedGraph.ZedGraphControl zedGraphControl1;
    }
}