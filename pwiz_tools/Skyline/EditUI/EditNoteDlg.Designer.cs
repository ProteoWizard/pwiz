namespace pwiz.Skyline.EditUI
{
    partial class EditNoteDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditNoteDlg));
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.textNote = new System.Windows.Forms.TextBox();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.btnClearAll = new System.Windows.Forms.Button();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.btnOrangeRed = new System.Windows.Forms.ToolStripButton();
            this.btnRed = new System.Windows.Forms.ToolStripButton();
            this.btnOrange = new System.Windows.Forms.ToolStripButton();
            this.btnYellow = new System.Windows.Forms.ToolStripButton();
            this.btnLightGreen = new System.Windows.Forms.ToolStripButton();
            this.btnGreen = new System.Windows.Forms.ToolStripButton();
            this.btnLightBlue = new System.Windows.Forms.ToolStripButton();
            this.btnBlue = new System.Windows.Forms.ToolStripButton();
            this.btnBlack = new System.Windows.Forms.ToolStripButton();
            this.btnPurple = new System.Windows.Forms.ToolStripButton();
            this.label2 = new System.Windows.Forms.Label();
            this.colName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // textNote
            // 
            resources.ApplyResources(this.textNote, "textNote");
            this.textNote.Name = "textNote";
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            resources.ApplyResources(this.dataGridView1, "dataGridView1");
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colName,
            this.colValue});
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.RowHeadersVisible = false;
            this.dataGridView1.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.dataGridView1_DataError);
            // 
            // splitContainer1
            // 
            resources.ApplyResources(this.splitContainer1, "splitContainer1");
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.btnClearAll);
            this.splitContainer1.Panel1.Controls.Add(this.toolStrip1);
            this.splitContainer1.Panel1.Controls.Add(this.textNote);
            this.splitContainer1.Panel1.Controls.Add(this.btnCancel);
            this.splitContainer1.Panel1.Controls.Add(this.label1);
            this.splitContainer1.Panel1.Controls.Add(this.btnOk);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.label2);
            this.splitContainer1.Panel2.Controls.Add(this.dataGridView1);
            // 
            // btnClearAll
            // 
            resources.ApplyResources(this.btnClearAll, "btnClearAll");
            this.btnClearAll.Name = "btnClearAll";
            this.btnClearAll.UseVisualStyleBackColor = true;
            this.btnClearAll.Click += new System.EventHandler(this.btnClearAll_Click);
            // 
            // toolStrip1
            // 
            resources.ApplyResources(this.toolStrip1, "toolStrip1");
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnOrangeRed,
            this.btnRed,
            this.btnOrange,
            this.btnYellow,
            this.btnLightGreen,
            this.btnGreen,
            this.btnLightBlue,
            this.btnBlue,
            this.btnBlack,
            this.btnPurple});
            this.toolStrip1.Name = "toolStrip1";
            // 
            // btnOrangeRed
            // 
            this.btnOrangeRed.AutoToolTip = false;
            this.btnOrangeRed.BackColor = System.Drawing.SystemColors.Control;
            resources.ApplyResources(this.btnOrangeRed, "btnOrangeRed");
            this.btnOrangeRed.CheckOnClick = true;
            this.btnOrangeRed.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnOrangeRed.ForeColor = System.Drawing.Color.OrangeRed;
            this.btnOrangeRed.Name = "btnOrangeRed";
            this.btnOrangeRed.Click += new System.EventHandler(this.btnColor_Click);
            this.btnOrangeRed.Paint += new System.Windows.Forms.PaintEventHandler(this.btnColor_Paint);
            // 
            // btnRed
            // 
            this.btnRed.AutoToolTip = false;
            this.btnRed.BackColor = System.Drawing.SystemColors.Control;
            this.btnRed.CheckOnClick = true;
            this.btnRed.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnRed.ForeColor = System.Drawing.Color.IndianRed;
            resources.ApplyResources(this.btnRed, "btnRed");
            this.btnRed.Name = "btnRed";
            this.btnRed.Click += new System.EventHandler(this.btnColor_Click);
            this.btnRed.Paint += new System.Windows.Forms.PaintEventHandler(this.btnColor_Paint);
            // 
            // btnOrange
            // 
            this.btnOrange.AutoToolTip = false;
            this.btnOrange.CheckOnClick = true;
            this.btnOrange.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnOrange.ForeColor = System.Drawing.Color.DarkOrange;
            resources.ApplyResources(this.btnOrange, "btnOrange");
            this.btnOrange.Name = "btnOrange";
            this.btnOrange.Click += new System.EventHandler(this.btnColor_Click);
            this.btnOrange.Paint += new System.Windows.Forms.PaintEventHandler(this.btnColor_Paint);
            // 
            // btnYellow
            // 
            this.btnYellow.AutoToolTip = false;
            this.btnYellow.CheckOnClick = true;
            this.btnYellow.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnYellow.ForeColor = System.Drawing.Color.Yellow;
            resources.ApplyResources(this.btnYellow, "btnYellow");
            this.btnYellow.Name = "btnYellow";
            this.btnYellow.Click += new System.EventHandler(this.btnColor_Click);
            this.btnYellow.Paint += new System.Windows.Forms.PaintEventHandler(this.btnColor_Paint);
            // 
            // btnLightGreen
            // 
            this.btnLightGreen.AutoToolTip = false;
            this.btnLightGreen.CheckOnClick = true;
            this.btnLightGreen.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnLightGreen.ForeColor = System.Drawing.Color.LightGreen;
            resources.ApplyResources(this.btnLightGreen, "btnLightGreen");
            this.btnLightGreen.Name = "btnLightGreen";
            this.btnLightGreen.Click += new System.EventHandler(this.btnColor_Click);
            this.btnLightGreen.Paint += new System.Windows.Forms.PaintEventHandler(this.btnColor_Paint);
            // 
            // btnGreen
            // 
            this.btnGreen.AutoToolTip = false;
            this.btnGreen.CheckOnClick = true;
            this.btnGreen.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnGreen.ForeColor = System.Drawing.Color.Green;
            resources.ApplyResources(this.btnGreen, "btnGreen");
            this.btnGreen.Name = "btnGreen";
            this.btnGreen.Click += new System.EventHandler(this.btnColor_Click);
            this.btnGreen.Paint += new System.Windows.Forms.PaintEventHandler(this.btnColor_Paint);
            // 
            // btnLightBlue
            // 
            this.btnLightBlue.AutoToolTip = false;
            this.btnLightBlue.CheckOnClick = true;
            this.btnLightBlue.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnLightBlue.ForeColor = System.Drawing.Color.LightBlue;
            resources.ApplyResources(this.btnLightBlue, "btnLightBlue");
            this.btnLightBlue.Name = "btnLightBlue";
            this.btnLightBlue.Click += new System.EventHandler(this.btnColor_Click);
            this.btnLightBlue.Paint += new System.Windows.Forms.PaintEventHandler(this.btnColor_Paint);
            // 
            // btnBlue
            // 
            this.btnBlue.AutoToolTip = false;
            this.btnBlue.CheckOnClick = true;
            this.btnBlue.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnBlue.ForeColor = System.Drawing.Color.Blue;
            resources.ApplyResources(this.btnBlue, "btnBlue");
            this.btnBlue.Name = "btnBlue";
            this.btnBlue.Click += new System.EventHandler(this.btnColor_Click);
            this.btnBlue.Paint += new System.Windows.Forms.PaintEventHandler(this.btnColor_Paint);
            // 
            // btnBlack
            // 
            this.btnBlack.AutoToolTip = false;
            this.btnBlack.CheckOnClick = true;
            this.btnBlack.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnBlack.ForeColor = System.Drawing.Color.Purple;
            resources.ApplyResources(this.btnBlack, "btnBlack");
            this.btnBlack.Name = "btnBlack";
            this.btnBlack.Click += new System.EventHandler(this.btnColor_Click);
            this.btnBlack.Paint += new System.Windows.Forms.PaintEventHandler(this.btnColor_Paint);
            // 
            // btnPurple
            // 
            this.btnPurple.AutoToolTip = false;
            this.btnPurple.CheckOnClick = true;
            this.btnPurple.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            resources.ApplyResources(this.btnPurple, "btnPurple");
            this.btnPurple.Name = "btnPurple";
            this.btnPurple.Click += new System.EventHandler(this.btnColor_Click);
            this.btnPurple.Paint += new System.Windows.Forms.PaintEventHandler(this.btnColor_Paint);
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // colName
            // 
            this.colName.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            resources.ApplyResources(this.colName, "colName");
            this.colName.Name = "colName";
            this.colName.ReadOnly = true;
            // 
            // colValue
            // 
            this.colValue.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            resources.ApplyResources(this.colValue, "colValue");
            this.colValue.Name = "colValue";
            this.colValue.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // EditNoteDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.splitContainer1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditNoteDlg";
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textNote;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton btnOrangeRed;
        private System.Windows.Forms.ToolStripButton btnYellow;
        private System.Windows.Forms.ToolStripButton btnOrange;
        private System.Windows.Forms.ToolStripButton btnLightGreen;
        private System.Windows.Forms.ToolStripButton btnGreen;
        private System.Windows.Forms.ToolStripButton btnLightBlue;
        private System.Windows.Forms.ToolStripButton btnBlue;
        private System.Windows.Forms.ToolStripButton btnBlack;
        private System.Windows.Forms.ToolStripButton btnPurple;
        private System.Windows.Forms.ToolStripButton btnRed;
        private System.Windows.Forms.Button btnClearAll;
        private System.Windows.Forms.DataGridViewTextBoxColumn colName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colValue;
    }
}