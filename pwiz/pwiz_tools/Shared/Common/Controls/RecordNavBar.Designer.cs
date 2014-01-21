namespace pwiz.Common.Controls
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RecordNavBar));
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.btnNavFirst = new System.Windows.Forms.Button();
            this.btnNavPrev = new System.Windows.Forms.Button();
            this.tbxRecordNumber = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.btnNavNext = new System.Windows.Forms.Button();
            this.btnNavLast = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.findBox = new pwiz.Common.Controls.FindBox();
            this.lblFilteredFrom = new System.Windows.Forms.Label();
            this.navBar1 = new pwiz.Common.DataBinding.Controls.NavBar();
            this.navBar2 = new pwiz.Common.DataBinding.Controls.NavBar();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(this.tableLayoutPanel1, "tableLayoutPanel1");
            this.tableLayoutPanel1.Controls.Add(this.btnNavFirst, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.btnNavPrev, 2, 0);
            this.tableLayoutPanel1.Controls.Add(this.tbxRecordNumber, 3, 0);
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.btnNavNext, 4, 0);
            this.tableLayoutPanel1.Controls.Add(this.btnNavLast, 5, 0);
            this.tableLayoutPanel1.Controls.Add(this.label2, 6, 0);
            this.tableLayoutPanel1.Controls.Add(this.findBox, 7, 0);
            this.tableLayoutPanel1.Controls.Add(this.lblFilteredFrom, 8, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // btnNavFirst
            // 
            resources.ApplyResources(this.btnNavFirst, "btnNavFirst");
            this.btnNavFirst.Name = "btnNavFirst";
            this.btnNavFirst.UseVisualStyleBackColor = true;
            this.btnNavFirst.Click += new System.EventHandler(this.BtnNavFirstOnClick);
            // 
            // btnNavPrev
            // 
            resources.ApplyResources(this.btnNavPrev, "btnNavPrev");
            this.btnNavPrev.Name = "btnNavPrev";
            this.btnNavPrev.UseVisualStyleBackColor = true;
            this.btnNavPrev.Click += new System.EventHandler(this.BtnNavPrevOnClick);
            // 
            // tbxRecordNumber
            // 
            resources.ApplyResources(this.tbxRecordNumber, "tbxRecordNumber");
            this.tbxRecordNumber.Name = "tbxRecordNumber";
            this.tbxRecordNumber.TextChanged += new System.EventHandler(this.TbxRecordNumberOnLeave);
            this.tbxRecordNumber.Enter += new System.EventHandler(this.TbxRecordNumberOnEnter);
            this.tbxRecordNumber.Leave += new System.EventHandler(this.TbxRecordNumberOnLeave);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // btnNavNext
            // 
            resources.ApplyResources(this.btnNavNext, "btnNavNext");
            this.btnNavNext.Name = "btnNavNext";
            this.btnNavNext.UseVisualStyleBackColor = true;
            this.btnNavNext.Click += new System.EventHandler(this.BtnNavNextOnClick);
            // 
            // btnNavLast
            // 
            resources.ApplyResources(this.btnNavLast, "btnNavLast");
            this.btnNavLast.Name = "btnNavLast";
            this.btnNavLast.UseVisualStyleBackColor = true;
            this.btnNavLast.Click += new System.EventHandler(this.BtnNavLastOnClick);
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // findBox
            // 
            this.findBox.DataGridView = null;
            resources.ApplyResources(this.findBox, "findBox");
            this.findBox.Name = "findBox";
            // 
            // lblFilteredFrom
            // 
            resources.ApplyResources(this.lblFilteredFrom, "lblFilteredFrom");
            this.lblFilteredFrom.Name = "lblFilteredFrom";
            // 
            // navBar1
            // 
            resources.ApplyResources(this.navBar1, "navBar1");
            this.navBar1.BindingListSource = null;
            this.navBar1.Name = "navBar1";
            this.navBar1.ShowViewsButton = false;
            // 
            // navBar2
            // 
            resources.ApplyResources(this.navBar2, "navBar2");
            this.navBar2.BindingListSource = null;
            this.navBar2.Name = "navBar2";
            this.navBar2.ShowViewsButton = false;
            // 
            // RecordNavBar
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.navBar2);
            this.Controls.Add(this.navBar1);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "RecordNavBar";
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
