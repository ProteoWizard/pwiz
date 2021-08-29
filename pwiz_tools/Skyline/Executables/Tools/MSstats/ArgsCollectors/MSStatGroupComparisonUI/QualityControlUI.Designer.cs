namespace MSStatArgsCollector
{
    partial class QualityControlUI
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
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.groupBoxPlotSize = new System.Windows.Forms.GroupBox();
            this.tbxHeight = new System.Windows.Forms.TextBox();
            this.lblHeight = new System.Windows.Forms.Label();
            this.tbxWidth = new System.Windows.Forms.TextBox();
            this.lblWidth = new System.Windows.Forms.Label();
            this.commonOptionsControl1 = new MSStatArgsCollector.CommonOptionsControl();
            this.groupBoxPlotSize.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(178, 353);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(3, 3, 15, 3);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(85, 353);
            this.btnOK.Margin = new System.Windows.Forms.Padding(3, 3, 15, 3);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 3;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // groupBoxPlotSize
            // 
            this.groupBoxPlotSize.Controls.Add(this.tbxHeight);
            this.groupBoxPlotSize.Controls.Add(this.lblHeight);
            this.groupBoxPlotSize.Controls.Add(this.tbxWidth);
            this.groupBoxPlotSize.Controls.Add(this.lblWidth);
            this.groupBoxPlotSize.Location = new System.Drawing.Point(12, 172);
            this.groupBoxPlotSize.Name = "groupBoxPlotSize";
            this.groupBoxPlotSize.Size = new System.Drawing.Size(241, 113);
            this.groupBoxPlotSize.TabIndex = 5;
            this.groupBoxPlotSize.TabStop = false;
            this.groupBoxPlotSize.Text = "Size of profile and QC plots";
            // 
            // tbxHeight
            // 
            this.tbxHeight.Location = new System.Drawing.Point(8, 79);
            this.tbxHeight.Name = "tbxHeight";
            this.tbxHeight.Size = new System.Drawing.Size(100, 20);
            this.tbxHeight.TabIndex = 3;
            this.tbxHeight.Text = "10";
            // 
            // lblHeight
            // 
            this.lblHeight.AutoSize = true;
            this.lblHeight.Location = new System.Drawing.Point(5, 60);
            this.lblHeight.Name = "lblHeight";
            this.lblHeight.Size = new System.Drawing.Size(38, 13);
            this.lblHeight.TabIndex = 2;
            this.lblHeight.Text = "Height";
            // 
            // tbxWidth
            // 
            this.tbxWidth.Location = new System.Drawing.Point(8, 37);
            this.tbxWidth.Name = "tbxWidth";
            this.tbxWidth.Size = new System.Drawing.Size(100, 20);
            this.tbxWidth.TabIndex = 1;
            this.tbxWidth.Text = "10";
            // 
            // lblWidth
            // 
            this.lblWidth.AutoSize = true;
            this.lblWidth.Location = new System.Drawing.Point(5, 21);
            this.lblWidth.Name = "lblWidth";
            this.lblWidth.Size = new System.Drawing.Size(35, 13);
            this.lblWidth.TabIndex = 0;
            this.lblWidth.Text = "Width";
            // 
            // commonOptionsControl1
            // 
            this.commonOptionsControl1.Location = new System.Drawing.Point(12, 2);
            this.commonOptionsControl1.Name = "commonOptionsControl1";
            this.commonOptionsControl1.Size = new System.Drawing.Size(203, 164);
            this.commonOptionsControl1.TabIndex = 6;
            // 
            // QualityControlUI
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(268, 388);
            this.Controls.Add(this.commonOptionsControl1);
            this.Controls.Add(this.groupBoxPlotSize);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "QualityControlUI";
            this.ShowInTaskbar = false;
            this.Text = "MSstats QC";
            this.groupBoxPlotSize.ResumeLayout(false);
            this.groupBoxPlotSize.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.GroupBox groupBoxPlotSize;
        private System.Windows.Forms.TextBox tbxHeight;
        private System.Windows.Forms.Label lblHeight;
        private System.Windows.Forms.TextBox tbxWidth;
        private System.Windows.Forms.Label lblWidth;
        private CommonOptionsControl commonOptionsControl1;
    }
}