namespace pwiz.Skyline.Controls.Graphs
{
    partial class GraphRegression
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
            this.graphControl = new ZedGraph.ZedGraphControl();
            this.btnClose = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // graphControl
            // 
            this.graphControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.graphControl.EditButtons = System.Windows.Forms.MouseButtons.Left;
            this.graphControl.EditModifierKeys = System.Windows.Forms.Keys.None;
            this.graphControl.IsShowCopyMessage = false;
            this.graphControl.Location = new System.Drawing.Point(8, 8);
            this.graphControl.Name = "graphControl";
            this.graphControl.ScrollGrace = 0;
            this.graphControl.ScrollMaxX = 0;
            this.graphControl.ScrollMaxY = 0;
            this.graphControl.ScrollMaxY2 = 0;
            this.graphControl.ScrollMinX = 0;
            this.graphControl.ScrollMinY = 0;
            this.graphControl.ScrollMinY2 = 0;
            this.graphControl.Size = new System.Drawing.Size(501, 234);
            this.graphControl.TabIndex = 1;
            this.graphControl.ContextMenuBuilder += new ZedGraph.ZedGraphControl.ContextMenuBuilderEventHandler(graphControl_ContextMenuBuilder);
            // 
            // btnClose
            // 
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnClose.Location = new System.Drawing.Point(434, 255);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(75, 23);
            this.btnClose.TabIndex = 2;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // GraphRegression
            // 
            this.AcceptButton = this.btnClose;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnClose;
            this.ClientSize = new System.Drawing.Size(521, 290);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.graphControl);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "GraphRegression";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Regression";
            this.ResumeLayout(false);

        }

        #endregion

        private ZedGraph.ZedGraphControl graphControl;
        private System.Windows.Forms.Button btnClose;
    }
}