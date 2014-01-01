namespace pwiz.Topograph.ui.Forms
{
    partial class StatusForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(StatusForm));
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.btnSuspendChromatogram = new System.Windows.Forms.Button();
            this.pbChromatogram = new System.Windows.Forms.ProgressBar();
            this.tbxChromatogramStatus = new System.Windows.Forms.TextBox();
            this.tbxChromatogramMessage = new System.Windows.Forms.TextBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.tbxResultCalculatorStatus = new System.Windows.Forms.TextBox();
            this.tbxResultCalculatorMessage = new System.Windows.Forms.TextBox();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.btnOK = new System.Windows.Forms.Button();
            this.tbxAnalysisCount = new System.Windows.Forms.TextBox();
            this.btnGarbageCollect = new System.Windows.Forms.Button();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.label1 = new System.Windows.Forms.Label();
            this.tbxMemory = new System.Windows.Forms.TextBox();
            this.tbxOpenAnalyses = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.btnSuspendChromatogram);
            this.groupBox1.Controls.Add(this.pbChromatogram);
            this.groupBox1.Controls.Add(this.tbxChromatogramStatus);
            this.groupBox1.Controls.Add(this.tbxChromatogramMessage);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(450, 141);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Chromatogram Generator";
            // 
            // btnSuspendChromatogram
            // 
            this.btnSuspendChromatogram.Location = new System.Drawing.Point(369, 112);
            this.btnSuspendChromatogram.Name = "btnSuspendChromatogram";
            this.btnSuspendChromatogram.Size = new System.Drawing.Size(75, 23);
            this.btnSuspendChromatogram.TabIndex = 3;
            this.btnSuspendChromatogram.Text = "Suspend";
            this.btnSuspendChromatogram.UseVisualStyleBackColor = true;
            this.btnSuspendChromatogram.Click += new System.EventHandler(this.BtnSuspendChromatogramOnClick);
            // 
            // pbChromatogram
            // 
            this.pbChromatogram.Location = new System.Drawing.Point(14, 72);
            this.pbChromatogram.Name = "pbChromatogram";
            this.pbChromatogram.Size = new System.Drawing.Size(430, 23);
            this.pbChromatogram.TabIndex = 2;
            // 
            // tbxChromatogramStatus
            // 
            this.tbxChromatogramStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbxChromatogramStatus.Location = new System.Drawing.Point(14, 46);
            this.tbxChromatogramStatus.Name = "tbxChromatogramStatus";
            this.tbxChromatogramStatus.ReadOnly = true;
            this.tbxChromatogramStatus.Size = new System.Drawing.Size(430, 20);
            this.tbxChromatogramStatus.TabIndex = 1;
            // 
            // tbxChromatogramMessage
            // 
            this.tbxChromatogramMessage.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbxChromatogramMessage.Location = new System.Drawing.Point(14, 20);
            this.tbxChromatogramMessage.Name = "tbxChromatogramMessage";
            this.tbxChromatogramMessage.ReadOnly = true;
            this.tbxChromatogramMessage.Size = new System.Drawing.Size(430, 20);
            this.tbxChromatogramMessage.TabIndex = 1;
            // 
            // groupBox2
            // 
            this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox2.Controls.Add(this.tbxResultCalculatorStatus);
            this.groupBox2.Controls.Add(this.tbxResultCalculatorMessage);
            this.groupBox2.Location = new System.Drawing.Point(12, 159);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(450, 100);
            this.groupBox2.TabIndex = 1;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Result Calculator";
            // 
            // tbxResultCalculatorStatus
            // 
            this.tbxResultCalculatorStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbxResultCalculatorStatus.Location = new System.Drawing.Point(14, 53);
            this.tbxResultCalculatorStatus.Name = "tbxResultCalculatorStatus";
            this.tbxResultCalculatorStatus.ReadOnly = true;
            this.tbxResultCalculatorStatus.Size = new System.Drawing.Size(430, 20);
            this.tbxResultCalculatorStatus.TabIndex = 1;
            // 
            // tbxResultCalculatorMessage
            // 
            this.tbxResultCalculatorMessage.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbxResultCalculatorMessage.Location = new System.Drawing.Point(14, 19);
            this.tbxResultCalculatorMessage.Name = "tbxResultCalculatorMessage";
            this.tbxResultCalculatorMessage.ReadOnly = true;
            this.tbxResultCalculatorMessage.Size = new System.Drawing.Size(430, 20);
            this.tbxResultCalculatorMessage.TabIndex = 0;
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Interval = 1000;
            this.timer1.Tick += new System.EventHandler(this.Timer1OnTick);
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnOK.Location = new System.Drawing.Point(381, 453);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 0;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.BtnOkOnClick);
            // 
            // tbxAnalysisCount
            // 
            this.tbxAnalysisCount.Location = new System.Drawing.Point(12, 274);
            this.tbxAnalysisCount.Name = "tbxAnalysisCount";
            this.tbxAnalysisCount.ReadOnly = true;
            this.tbxAnalysisCount.Size = new System.Drawing.Size(450, 20);
            this.tbxAnalysisCount.TabIndex = 2;
            // 
            // btnGarbageCollect
            // 
            this.btnGarbageCollect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnGarbageCollect.Location = new System.Drawing.Point(12, 453);
            this.btnGarbageCollect.Name = "btnGarbageCollect";
            this.btnGarbageCollect.Size = new System.Drawing.Size(100, 23);
            this.btnGarbageCollect.TabIndex = 3;
            this.btnGarbageCollect.Text = "Garbage Collect";
            this.btnGarbageCollect.UseVisualStyleBackColor = true;
            this.btnGarbageCollect.Click += new System.EventHandler(this.BtnGarbageCollectOnClick);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 200F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.tbxMemory, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.tbxOpenAnalyses, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.label2, 0, 1);
            this.tableLayoutPanel1.Location = new System.Drawing.Point(12, 300);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 2;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(450, 53);
            this.tableLayoutPanel1.TabIndex = 4;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label1.Location = new System.Drawing.Point(3, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(194, 25);
            this.label1.TabIndex = 0;
            this.label1.Text = "Memory Used:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxMemory
            // 
            this.tbxMemory.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxMemory.Location = new System.Drawing.Point(203, 3);
            this.tbxMemory.Name = "tbxMemory";
            this.tbxMemory.ReadOnly = true;
            this.tbxMemory.Size = new System.Drawing.Size(244, 20);
            this.tbxMemory.TabIndex = 1;
            // 
            // tbxOpenAnalyses
            // 
            this.tbxOpenAnalyses.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxOpenAnalyses.Location = new System.Drawing.Point(203, 28);
            this.tbxOpenAnalyses.Name = "tbxOpenAnalyses";
            this.tbxOpenAnalyses.ReadOnly = true;
            this.tbxOpenAnalyses.Size = new System.Drawing.Size(244, 20);
            this.tbxOpenAnalyses.TabIndex = 2;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label2.Location = new System.Drawing.Point(3, 25);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(194, 28);
            this.label2.TabIndex = 3;
            this.label2.Text = "Open Analyses:";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // StatusForm
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnOK;
            this.ClientSize = new System.Drawing.Size(474, 480);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Controls.Add(this.btnGarbageCollect);
            this.Controls.Add(this.tbxAnalysisCount);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Name = "StatusForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.TabText = "StatusForm";
            this.Text = "StatusForm";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.ProgressBar pbChromatogram;
        private System.Windows.Forms.TextBox tbxChromatogramStatus;
        private System.Windows.Forms.TextBox tbxChromatogramMessage;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.TextBox tbxResultCalculatorStatus;
        private System.Windows.Forms.TextBox tbxResultCalculatorMessage;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.TextBox tbxAnalysisCount;
        private System.Windows.Forms.Button btnGarbageCollect;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbxMemory;
        private System.Windows.Forms.TextBox tbxOpenAnalyses;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnSuspendChromatogram;
    }
}
