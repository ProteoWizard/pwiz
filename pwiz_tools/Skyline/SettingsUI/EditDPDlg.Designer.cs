namespace pwiz.Skyline.SettingsUI
{
    partial class EditDPDlg
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
            this.textName = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.textIntercept = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.textSlope = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.textStepCount = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.textStepSize = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label6 = new System.Windows.Forms.Label();
            this.btnShowGraph = new System.Windows.Forms.Button();
            this.btnUseCurrent = new System.Windows.Forms.Button();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.SuspendLayout();
            // 
            // textName
            // 
            this.textName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.textName.Location = new System.Drawing.Point(16, 29);
            this.textName.Name = "textName";
            this.textName.Size = new System.Drawing.Size(245, 20);
            this.textName.TabIndex = 1;
            this.helpTip.SetToolTip(this.textName, "Name used to list this equation in the Transition Settings form");
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(13, 13);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(38, 13);
            this.label4.TabIndex = 0;
            this.label4.Text = "&Name:";
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(286, 42);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 15;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(286, 12);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 14;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // textIntercept
            // 
            this.textIntercept.Location = new System.Drawing.Point(161, 85);
            this.textIntercept.Name = "textIntercept";
            this.textIntercept.Size = new System.Drawing.Size(100, 20);
            this.textIntercept.TabIndex = 5;
            this.helpTip.SetToolTip(this.textIntercept, "Intercept used to calculate the predicted optimal declustering potential\r\nfrom th" +
                    "e precursor m/z with an equation of the form:\r\n\r\nDP = slope * precursor m/z + in" +
                    "tercept");
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(158, 69);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(52, 13);
            this.label2.TabIndex = 4;
            this.label2.Text = "&Intercept:";
            // 
            // textSlope
            // 
            this.textSlope.Location = new System.Drawing.Point(16, 85);
            this.textSlope.Name = "textSlope";
            this.textSlope.Size = new System.Drawing.Size(100, 20);
            this.textSlope.TabIndex = 3;
            this.helpTip.SetToolTip(this.textSlope, "Slope used to calculate the predicted optimal declustering potential\r\nfrom the pr" +
                    "ecursor m/z with an equation of the form:\r\n\r\nDP = slope * precursor m/z + interc" +
                    "ept");
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 69);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(37, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "&Slope:";
            // 
            // textStepCount
            // 
            this.textStepCount.Location = new System.Drawing.Point(161, 183);
            this.textStepCount.Name = "textStepCount";
            this.textStepCount.Size = new System.Drawing.Size(100, 20);
            this.textStepCount.TabIndex = 11;
            this.helpTip.SetToolTip(this.textStepCount, "Number of values used in DP optimization methods where the predicted optimal\r\nDP " +
                    "is measured, along with this number of values on either side of the predicted\r\nv" +
                    "alue, each separated by step size units");
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(158, 166);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(62, 13);
            this.label3.TabIndex = 10;
            this.label3.Text = "Step &count:";
            // 
            // textStepSize
            // 
            this.textStepSize.Location = new System.Drawing.Point(16, 183);
            this.textStepSize.Name = "textStepSize";
            this.textStepSize.Size = new System.Drawing.Size(100, 20);
            this.textStepSize.TabIndex = 9;
            this.helpTip.SetToolTip(this.textStepSize, "Interval used in DP optimization methods where the predicted optimal\r\nDP is measu" +
                    "red, along with step count values on either side of the predicted\r\nvalue, each s" +
                    "eparated by step size units");
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(13, 166);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(53, 13);
            this.label5.TabIndex = 8;
            this.label5.Text = "Step si&ze:";
            // 
            // groupBox1
            // 
            this.groupBox1.Location = new System.Drawing.Point(86, 132);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(175, 10);
            this.groupBox1.TabIndex = 7;
            this.groupBox1.TabStop = false;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(13, 132);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(67, 13);
            this.label6.TabIndex = 6;
            this.label6.Text = "Optimization:";
            // 
            // btnShowGraph
            // 
            this.btnShowGraph.Location = new System.Drawing.Point(141, 222);
            this.btnShowGraph.Name = "btnShowGraph";
            this.btnShowGraph.Size = new System.Drawing.Size(89, 23);
            this.btnShowGraph.TabIndex = 13;
            this.btnShowGraph.Text = "&Show Graph...";
            this.helpTip.SetToolTip(this.btnShowGraph, "Show a linear regression graph  of  currently imported optimization results data\r" +
                    "\nfor peptides in this document");
            this.btnShowGraph.UseVisualStyleBackColor = true;
            this.btnShowGraph.Click += new System.EventHandler(this.btnShowGraph_Click);
            // 
            // btnUseCurrent
            // 
            this.btnUseCurrent.Enabled = false;
            this.btnUseCurrent.Location = new System.Drawing.Point(46, 223);
            this.btnUseCurrent.Name = "btnUseCurrent";
            this.btnUseCurrent.Size = new System.Drawing.Size(89, 23);
            this.btnUseCurrent.TabIndex = 12;
            this.btnUseCurrent.Text = "&Use Results";
            this.helpTip.SetToolTip(this.btnUseCurrent, "Click to use currently imported optimization results data for peptides\r\nin this d" +
                    "ocument to calculate equations with linear regression");
            this.btnUseCurrent.UseVisualStyleBackColor = true;
            this.btnUseCurrent.Click += new System.EventHandler(this.btnUseCurrent_Click);
            // 
            // EditDPDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(375, 258);
            this.Controls.Add(this.btnShowGraph);
            this.Controls.Add(this.btnUseCurrent);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.textStepCount);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.textIntercept);
            this.Controls.Add(this.textStepSize);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.textSlope);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.textName);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditDPDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit Declustering Potential Equation";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.TextBox textIntercept;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textSlope;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textStepCount;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textStepSize;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Button btnShowGraph;
        private System.Windows.Forms.Button btnUseCurrent;
        private System.Windows.Forms.ToolTip helpTip;
    }
}