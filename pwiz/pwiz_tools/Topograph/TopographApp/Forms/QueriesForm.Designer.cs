namespace pwiz.Topograph.ui.Forms
{
    partial class QueriesForm
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
            this.lbxCustomQueries = new System.Windows.Forms.ListBox();
            this.btnNewQuery = new System.Windows.Forms.Button();
            this.btnDelete = new System.Windows.Forms.Button();
            this.btnOpen = new System.Windows.Forms.Button();
            this.btnRunQuery = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.btnExecuteBuiltIn = new System.Windows.Forms.Button();
            this.lbxBuiltInQueries = new System.Windows.Forms.ListBox();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // lbxCustomQueries
            // 
            this.lbxCustomQueries.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.lbxCustomQueries.FormattingEnabled = true;
            this.lbxCustomQueries.Location = new System.Drawing.Point(8, 47);
            this.lbxCustomQueries.Name = "lbxCustomQueries";
            this.lbxCustomQueries.Size = new System.Drawing.Size(504, 121);
            this.lbxCustomQueries.TabIndex = 0;
            this.lbxCustomQueries.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.listBox1_MouseDoubleClick);
            this.lbxCustomQueries.SelectedIndexChanged += new System.EventHandler(this.listBox1_SelectedIndexChanged);
            // 
            // btnNewQuery
            // 
            this.btnNewQuery.Location = new System.Drawing.Point(170, 19);
            this.btnNewQuery.Name = "btnNewQuery";
            this.btnNewQuery.Size = new System.Drawing.Size(75, 23);
            this.btnNewQuery.TabIndex = 1;
            this.btnNewQuery.Text = "New";
            this.btnNewQuery.UseVisualStyleBackColor = true;
            this.btnNewQuery.Click += new System.EventHandler(this.btnNewQuery_Click);
            // 
            // btnDelete
            // 
            this.btnDelete.Enabled = false;
            this.btnDelete.Location = new System.Drawing.Point(251, 19);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(100, 23);
            this.btnDelete.TabIndex = 2;
            this.btnDelete.Text = "Delete";
            this.btnDelete.UseVisualStyleBackColor = true;
            this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
            // 
            // btnOpen
            // 
            this.btnOpen.Enabled = false;
            this.btnOpen.Location = new System.Drawing.Point(89, 19);
            this.btnOpen.Name = "btnOpen";
            this.btnOpen.Size = new System.Drawing.Size(75, 23);
            this.btnOpen.TabIndex = 3;
            this.btnOpen.Text = "Design";
            this.btnOpen.UseVisualStyleBackColor = true;
            this.btnOpen.Click += new System.EventHandler(this.btnOpen_Click);
            // 
            // btnRunQuery
            // 
            this.btnRunQuery.Enabled = false;
            this.btnRunQuery.Location = new System.Drawing.Point(8, 19);
            this.btnRunQuery.Name = "btnRunQuery";
            this.btnRunQuery.Size = new System.Drawing.Size(75, 23);
            this.btnRunQuery.TabIndex = 4;
            this.btnRunQuery.Text = "Execute";
            this.btnRunQuery.UseVisualStyleBackColor = true;
            this.btnRunQuery.Click += new System.EventHandler(this.btnRunQuery_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.lbxCustomQueries);
            this.groupBox1.Controls.Add(this.btnRunQuery);
            this.groupBox1.Controls.Add(this.btnDelete);
            this.groupBox1.Controls.Add(this.btnOpen);
            this.groupBox1.Controls.Add(this.btnNewQuery);
            this.groupBox1.Location = new System.Drawing.Point(12, 148);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(518, 174);
            this.groupBox1.TabIndex = 5;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Custom Queries";
            // 
            // groupBox2
            // 
            this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox2.Controls.Add(this.btnExecuteBuiltIn);
            this.groupBox2.Controls.Add(this.lbxBuiltInQueries);
            this.groupBox2.Location = new System.Drawing.Point(12, 13);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(518, 129);
            this.groupBox2.TabIndex = 6;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Built-in Queries";
            // 
            // btnExecuteBuiltIn
            // 
            this.btnExecuteBuiltIn.Enabled = false;
            this.btnExecuteBuiltIn.Location = new System.Drawing.Point(6, 12);
            this.btnExecuteBuiltIn.Name = "btnExecuteBuiltIn";
            this.btnExecuteBuiltIn.Size = new System.Drawing.Size(75, 23);
            this.btnExecuteBuiltIn.TabIndex = 1;
            this.btnExecuteBuiltIn.Text = "Execute";
            this.btnExecuteBuiltIn.UseVisualStyleBackColor = true;
            this.btnExecuteBuiltIn.Click += new System.EventHandler(this.btnExecuteBuiltIn_Click);
            // 
            // lbxBuiltInQueries
            // 
            this.lbxBuiltInQueries.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.lbxBuiltInQueries.FormattingEnabled = true;
            this.lbxBuiltInQueries.Location = new System.Drawing.Point(8, 41);
            this.lbxBuiltInQueries.Name = "lbxBuiltInQueries";
            this.lbxBuiltInQueries.Size = new System.Drawing.Size(504, 82);
            this.lbxBuiltInQueries.TabIndex = 0;
            this.lbxBuiltInQueries.SelectedIndexChanged += new System.EventHandler(this.lbxBuiltInQueries_SelectedIndexChanged);
            this.lbxBuiltInQueries.DoubleClick += new System.EventHandler(this.lbxBuiltInQueries_DoubleClick);
            // 
            // QueriesForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(542, 330);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Name = "QueriesForm";
            this.TabText = "Queries";
            this.Text = "Queries";
            this.groupBox1.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListBox lbxCustomQueries;
        private System.Windows.Forms.Button btnNewQuery;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.Button btnOpen;
        private System.Windows.Forms.Button btnRunQuery;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Button btnExecuteBuiltIn;
        private System.Windows.Forms.ListBox lbxBuiltInQueries;
    }
}