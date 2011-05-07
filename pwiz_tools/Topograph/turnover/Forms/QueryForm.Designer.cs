using pwiz.Topograph.ui.Controls;

namespace pwiz.Topograph.ui.Forms
{
    partial class QueryForm
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
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.pageDesign = new System.Windows.Forms.TabPage();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.treeView1 = new System.Windows.Forms.TreeView();
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.panel3 = new System.Windows.Forms.Panel();
            this.btnAddColumn = new System.Windows.Forms.Button();
            this.panel2 = new System.Windows.Forms.Panel();
            this.comboTableName = new System.Windows.Forms.ComboBox();
            this.pageSource = new System.Windows.Forms.TabPage();
            this.tbxSource = new System.Windows.Forms.TextBox();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.panelName = new System.Windows.Forms.Panel();
            this.tbxQueryName = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.dataGridView1 = new pwiz.Topograph.ui.Controls.QueryGrid();
            this.panel1 = new System.Windows.Forms.Panel();
            this.btnExport = new System.Windows.Forms.Button();
            this.btnSaveQuery = new System.Windows.Forms.Button();
            this.btnExecuteQuery = new System.Windows.Forms.Button();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.recordNavBar1 = new pwiz.Common.Controls.RecordNavBar();
            this.tabControl1.SuspendLayout();
            this.pageDesign.SuspendLayout();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            this.panel3.SuspendLayout();
            this.panel2.SuspendLayout();
            this.pageSource.SuspendLayout();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.panelName.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.panel1.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.pageDesign);
            this.tabControl1.Controls.Add(this.pageSource);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 19);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(653, 199);
            this.tabControl1.TabIndex = 0;
            this.tabControl1.SelectedIndexChanged += new System.EventHandler(this.tabControl1_SelectedIndexChanged);
            // 
            // pageDesign
            // 
            this.pageDesign.Controls.Add(this.splitContainer2);
            this.pageDesign.Controls.Add(this.panel2);
            this.pageDesign.Location = new System.Drawing.Point(4, 22);
            this.pageDesign.Name = "pageDesign";
            this.pageDesign.Padding = new System.Windows.Forms.Padding(3);
            this.pageDesign.Size = new System.Drawing.Size(645, 173);
            this.pageDesign.TabIndex = 0;
            this.pageDesign.Text = "Design";
            this.pageDesign.UseVisualStyleBackColor = true;
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(3, 26);
            this.splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.treeView1);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.listBox1);
            this.splitContainer2.Panel2.Controls.Add(this.panel3);
            this.splitContainer2.Size = new System.Drawing.Size(639, 144);
            this.splitContainer2.SplitterDistance = 213;
            this.splitContainer2.TabIndex = 0;
            // 
            // treeView1
            // 
            this.treeView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeView1.Location = new System.Drawing.Point(0, 0);
            this.treeView1.Name = "treeView1";
            this.treeView1.Size = new System.Drawing.Size(213, 144);
            this.treeView1.TabIndex = 0;
            this.treeView1.BeforeExpand += new System.Windows.Forms.TreeViewCancelEventHandler(this.treeView1_BeforeExpand);
            this.treeView1.DoubleClick += new System.EventHandler(this.treeView1_DoubleClick);
            // 
            // listBox1
            // 
            this.listBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBox1.FormattingEnabled = true;
            this.listBox1.Location = new System.Drawing.Point(19, 0);
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(403, 134);
            this.listBox1.TabIndex = 1;
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.btnAddColumn);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Left;
            this.panel3.Location = new System.Drawing.Point(0, 0);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(19, 144);
            this.panel3.TabIndex = 0;
            // 
            // btnAddColumn
            // 
            this.btnAddColumn.Location = new System.Drawing.Point(0, 0);
            this.btnAddColumn.Name = "btnAddColumn";
            this.btnAddColumn.Size = new System.Drawing.Size(20, 23);
            this.btnAddColumn.TabIndex = 0;
            this.btnAddColumn.Text = ">";
            this.btnAddColumn.UseVisualStyleBackColor = true;
            this.btnAddColumn.Click += new System.EventHandler(this.btnAddColumn_Click);
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.comboTableName);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel2.Location = new System.Drawing.Point(3, 3);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(639, 23);
            this.panel2.TabIndex = 1;
            // 
            // comboTableName
            // 
            this.comboTableName.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboTableName.FormattingEnabled = true;
            this.comboTableName.Location = new System.Drawing.Point(92, 0);
            this.comboTableName.Name = "comboTableName";
            this.comboTableName.Size = new System.Drawing.Size(121, 21);
            this.comboTableName.TabIndex = 0;
            this.comboTableName.SelectedIndexChanged += new System.EventHandler(this.comboTableName_SelectedIndexChanged);
            // 
            // pageSource
            // 
            this.pageSource.Controls.Add(this.tbxSource);
            this.pageSource.Location = new System.Drawing.Point(4, 22);
            this.pageSource.Name = "pageSource";
            this.pageSource.Padding = new System.Windows.Forms.Padding(3);
            this.pageSource.Size = new System.Drawing.Size(645, 173);
            this.pageSource.TabIndex = 1;
            this.pageSource.Text = "Source";
            this.pageSource.UseVisualStyleBackColor = true;
            // 
            // tbxSource
            // 
            this.tbxSource.AcceptsReturn = true;
            this.tbxSource.AcceptsTab = true;
            this.tbxSource.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxSource.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tbxSource.Location = new System.Drawing.Point(3, 3);
            this.tbxSource.Multiline = true;
            this.tbxSource.Name = "tbxSource";
            this.tbxSource.Size = new System.Drawing.Size(639, 167);
            this.tbxSource.TabIndex = 0;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.tabControl1);
            this.splitContainer1.Panel1.Controls.Add(this.panelName);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.dataGridView1);
            this.splitContainer1.Panel2.Controls.Add(this.panel1);
            this.splitContainer1.Panel2.Controls.Add(this.tableLayoutPanel1);
            this.splitContainer1.Size = new System.Drawing.Size(653, 416);
            this.splitContainer1.SplitterDistance = 218;
            this.splitContainer1.TabIndex = 1;
            // 
            // panelName
            // 
            this.panelName.Controls.Add(this.tbxQueryName);
            this.panelName.Controls.Add(this.label1);
            this.panelName.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelName.Location = new System.Drawing.Point(0, 0);
            this.panelName.Name = "panelName";
            this.panelName.Size = new System.Drawing.Size(653, 19);
            this.panelName.TabIndex = 2;
            // 
            // tbxQueryName
            // 
            this.tbxQueryName.Location = new System.Drawing.Point(155, 0);
            this.tbxQueryName.Name = "tbxQueryName";
            this.tbxQueryName.Size = new System.Drawing.Size(100, 20);
            this.tbxQueryName.TabIndex = 3;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(66, 3);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(69, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Query Name:";
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.AllowUserToOrderColumns = true;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.Location = new System.Drawing.Point(0, 27);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.ReadOnly = true;
            this.dataGridView1.Size = new System.Drawing.Size(653, 138);
            this.dataGridView1.TabIndex = 0;
            this.dataGridView1.Workspace = null;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.btnExport);
            this.panel1.Controls.Add(this.btnSaveQuery);
            this.panel1.Controls.Add(this.btnExecuteQuery);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(653, 27);
            this.panel1.TabIndex = 0;
            // 
            // btnExport
            // 
            this.btnExport.Location = new System.Drawing.Point(249, 3);
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new System.Drawing.Size(112, 23);
            this.btnExport.TabIndex = 2;
            this.btnExport.Text = "Export Results...";
            this.btnExport.UseVisualStyleBackColor = true;
            this.btnExport.Click += new System.EventHandler(this.btnExport_Click);
            // 
            // btnSaveQuery
            // 
            this.btnSaveQuery.Location = new System.Drawing.Point(4, 4);
            this.btnSaveQuery.Name = "btnSaveQuery";
            this.btnSaveQuery.Size = new System.Drawing.Size(131, 23);
            this.btnSaveQuery.TabIndex = 1;
            this.btnSaveQuery.Text = "Save Query Definition";
            this.btnSaveQuery.UseVisualStyleBackColor = true;
            this.btnSaveQuery.Click += new System.EventHandler(this.btnSaveQuery_Click);
            // 
            // btnExecuteQuery
            // 
            this.btnExecuteQuery.Location = new System.Drawing.Point(141, 4);
            this.btnExecuteQuery.Name = "btnExecuteQuery";
            this.btnExecuteQuery.Size = new System.Drawing.Size(102, 23);
            this.btnExecuteQuery.TabIndex = 0;
            this.btnExecuteQuery.Text = "Preview";
            this.btnExecuteQuery.UseVisualStyleBackColor = true;
            this.btnExecuteQuery.Click += new System.EventHandler(this.btnExecuteQuery_Click);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Controls.Add(this.recordNavBar1, 0, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 165);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(653, 29);
            this.tableLayoutPanel1.TabIndex = 2;
            // 
            // recordNavBar1
            // 
            this.recordNavBar1.DataGridView = this.dataGridView1;
            this.recordNavBar1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.recordNavBar1.Location = new System.Drawing.Point(3, 3);
            this.recordNavBar1.Name = "recordNavBar1";
            this.recordNavBar1.Size = new System.Drawing.Size(647, 23);
            this.recordNavBar1.TabIndex = 0;
            // 
            // QueryForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(653, 416);
            this.Controls.Add(this.splitContainer1);
            this.Name = "QueryForm";
            this.TabText = "QueryForm";
            this.Text = "QueryForm";
            this.tabControl1.ResumeLayout(false);
            this.pageDesign.ResumeLayout(false);
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            this.splitContainer2.ResumeLayout(false);
            this.panel3.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            this.pageSource.ResumeLayout(false);
            this.pageSource.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.ResumeLayout(false);
            this.panelName.ResumeLayout(false);
            this.panelName.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.panel1.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage pageDesign;
        private System.Windows.Forms.TabPage pageSource;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private QueryGrid dataGridView1;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.ComboBox comboTableName;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.TreeView treeView1;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.Button btnAddColumn;
        private System.Windows.Forms.ListBox listBox1;
        private System.Windows.Forms.Button btnExecuteQuery;
        private System.Windows.Forms.TextBox tbxSource;
        private System.Windows.Forms.Panel panelName;
        private System.Windows.Forms.TextBox tbxQueryName;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnExport;
        private System.Windows.Forms.Button btnSaveQuery;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private pwiz.Common.Controls.RecordNavBar recordNavBar1;
    }
}