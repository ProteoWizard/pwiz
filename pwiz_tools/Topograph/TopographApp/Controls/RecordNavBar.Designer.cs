namespace pwiz.Topograph.ui.Controls
{
    partial class RecordNavBar
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
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.btnNavFirst = new System.Windows.Forms.Button();
            this.btnNavPrev = new System.Windows.Forms.Button();
            this.tbxRecordNumber = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.btnNavNext = new System.Windows.Forms.Button();
            this.btnNavLast = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.findBox = new FindBox();
            this.lblFilteredFrom = new System.Windows.Forms.Label();
            this.navBar1 = new pwiz.Common.DataBinding.Controls.NavBar();
            this.navBar2 = new pwiz.Common.DataBinding.Controls.NavBar();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 9;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Controls.Add(this.btnNavFirst, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.btnNavPrev, 2, 0);
            this.tableLayoutPanel1.Controls.Add(this.tbxRecordNumber, 3, 0);
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.btnNavNext, 4, 0);
            this.tableLayoutPanel1.Controls.Add(this.btnNavLast, 5, 0);
            this.tableLayoutPanel1.Controls.Add(this.label2, 6, 0);
            this.tableLayoutPanel1.Controls.Add(this.findBox, 7, 0);
            this.tableLayoutPanel1.Controls.Add(this.lblFilteredFrom, 8, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 21F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(667, 21);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // btnNavFirst
            // 
            this.btnNavFirst.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnNavFirst.Location = new System.Drawing.Point(51, 0);
            this.btnNavFirst.Margin = new System.Windows.Forms.Padding(0);
            this.btnNavFirst.Name = "btnNavFirst";
            this.btnNavFirst.Size = new System.Drawing.Size(30, 21);
            this.btnNavFirst.TabIndex = 0;
            this.btnNavFirst.Text = "|<";
            this.btnNavFirst.UseVisualStyleBackColor = true;
            this.btnNavFirst.Click += new System.EventHandler(this.BtnNavFirstOnClick);
            // 
            // btnNavPrev
            // 
            this.btnNavPrev.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnNavPrev.Location = new System.Drawing.Point(81, 0);
            this.btnNavPrev.Margin = new System.Windows.Forms.Padding(0);
            this.btnNavPrev.Name = "btnNavPrev";
            this.btnNavPrev.Size = new System.Drawing.Size(30, 21);
            this.btnNavPrev.TabIndex = 1;
            this.btnNavPrev.Text = "<";
            this.btnNavPrev.UseVisualStyleBackColor = true;
            this.btnNavPrev.Click += new System.EventHandler(this.BtnNavPrevOnClick);
            // 
            // tbxRecordNumber
            // 
            this.tbxRecordNumber.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxRecordNumber.Location = new System.Drawing.Point(111, 0);
            this.tbxRecordNumber.Margin = new System.Windows.Forms.Padding(0);
            this.tbxRecordNumber.Name = "tbxRecordNumber";
            this.tbxRecordNumber.Size = new System.Drawing.Size(139, 20);
            this.tbxRecordNumber.TabIndex = 2;
            this.tbxRecordNumber.Text = "####### of #######";
            this.tbxRecordNumber.TextChanged += new System.EventHandler(this.TbxRecordNumberOnLeave);
            this.tbxRecordNumber.Enter += new System.EventHandler(this.TbxRecordNumberOnEnter);
            this.tbxRecordNumber.Leave += new System.EventHandler(this.TbxRecordNumberOnLeave);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label1.Location = new System.Drawing.Point(3, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(45, 21);
            this.label1.TabIndex = 3;
            this.label1.Text = "Record:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // btnNavNext
            // 
            this.btnNavNext.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnNavNext.Location = new System.Drawing.Point(250, 0);
            this.btnNavNext.Margin = new System.Windows.Forms.Padding(0);
            this.btnNavNext.Name = "btnNavNext";
            this.btnNavNext.Size = new System.Drawing.Size(30, 21);
            this.btnNavNext.TabIndex = 4;
            this.btnNavNext.Text = ">";
            this.btnNavNext.UseVisualStyleBackColor = true;
            this.btnNavNext.Click += new System.EventHandler(this.BtnNavNextOnClick);
            // 
            // btnNavLast
            // 
            this.btnNavLast.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnNavLast.Location = new System.Drawing.Point(280, 0);
            this.btnNavLast.Margin = new System.Windows.Forms.Padding(0);
            this.btnNavLast.Name = "btnNavLast";
            this.btnNavLast.Size = new System.Drawing.Size(30, 21);
            this.btnNavLast.TabIndex = 5;
            this.btnNavLast.Text = ">|";
            this.btnNavLast.UseVisualStyleBackColor = true;
            this.btnNavLast.Click += new System.EventHandler(this.BtnNavLastOnClick);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label2.Location = new System.Drawing.Point(313, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(32, 21);
            this.label2.TabIndex = 7;
            this.label2.Text = "Filter:";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // findBox
            // 
            this.findBox.DataGridView = null;
            this.findBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.findBox.Location = new System.Drawing.Point(348, 0);
            this.findBox.Margin = new System.Windows.Forms.Padding(0);
            this.findBox.Name = "findBox";
            this.findBox.Size = new System.Drawing.Size(184, 21);
            this.findBox.TabIndex = 8;
            // 
            // lblFilteredFrom
            // 
            this.lblFilteredFrom.AutoSize = true;
            this.lblFilteredFrom.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblFilteredFrom.Location = new System.Drawing.Point(535, 0);
            this.lblFilteredFrom.Name = "lblFilteredFrom";
            this.lblFilteredFrom.Size = new System.Drawing.Size(129, 21);
            this.lblFilteredFrom.TabIndex = 9;
            this.lblFilteredFrom.Text = "(Filtered from ########)";
            this.lblFilteredFrom.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // navBar1
            // 
            this.navBar1.AutoSize = true;
            this.navBar1.BindingListSource = null;
            this.navBar1.Location = new System.Drawing.Point(0, 0);
            this.navBar1.Name = "navBar1";
            this.navBar1.ShowViewsButton = false;
            this.navBar1.Size = new System.Drawing.Size(0, 25);
            this.navBar1.TabIndex = 1;
            // 
            // navBar2
            // 
            this.navBar2.AutoSize = true;
            this.navBar2.BindingListSource = null;
            this.navBar2.Location = new System.Drawing.Point(0, 0);
            this.navBar2.Name = "navBar2";
            this.navBar2.ShowViewsButton = false;
            this.navBar2.Size = new System.Drawing.Size(0, 25);
            this.navBar2.TabIndex = 2;
            // 
            // RecordNavBar
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.navBar2);
            this.Controls.Add(this.navBar1);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "RecordNavBar";
            this.Size = new System.Drawing.Size(667, 21);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Button btnNavFirst;
        private System.Windows.Forms.Button btnNavPrev;
        private System.Windows.Forms.TextBox tbxRecordNumber;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnNavNext;
        private System.Windows.Forms.Button btnNavLast;
        private System.Windows.Forms.Label label2;
        private FindBox findBox;
        private System.Windows.Forms.Label lblFilteredFrom;
        private pwiz.Common.DataBinding.Controls.NavBar navBar1;
        private pwiz.Common.DataBinding.Controls.NavBar navBar2;

    }
}
