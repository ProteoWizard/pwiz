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
            this.colName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
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
            this.btnClearAll = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOk.Location = new System.Drawing.Point(390, 12);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 2;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(390, 70);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(33, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "&Note:";
            // 
            // textNote
            // 
            this.textNote.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.textNote.Location = new System.Drawing.Point(15, 25);
            this.textNote.Multiline = true;
            this.textNote.Name = "textNote";
            this.textNote.Size = new System.Drawing.Size(369, 96);
            this.textNote.TabIndex = 1;
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colName,
            this.colValue});
            this.dataGridView1.Location = new System.Drawing.Point(12, 16);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.Size = new System.Drawing.Size(453, 133);
            this.dataGridView1.TabIndex = 4;
            // 
            // colName
            // 
            this.colName.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colName.HeaderText = "Name";
            this.colName.Name = "colName";
            this.colName.ReadOnly = true;
            // 
            // colValue
            // 
            this.colValue.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colValue.HeaderText = "Value";
            this.colValue.Name = "colValue";
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
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
            this.splitContainer1.Size = new System.Drawing.Size(477, 322);
            this.splitContainer1.SplitterDistance = 157;
            this.splitContainer1.TabIndex = 5;
            // 
            // toolStrip1
            // 
            this.toolStrip1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.toolStrip1.Dock = System.Windows.Forms.DockStyle.None;
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
            this.toolStrip1.Location = new System.Drawing.Point(9, 124);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Padding = new System.Windows.Forms.Padding(0);
            this.toolStrip1.Size = new System.Drawing.Size(232, 25);
            this.toolStrip1.TabIndex = 4;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // btnOrangeRed
            // 
            this.btnOrangeRed.AutoToolTip = false;
            this.btnOrangeRed.BackColor = System.Drawing.SystemColors.Control;
            this.btnOrangeRed.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.btnOrangeRed.CheckOnClick = true;
            this.btnOrangeRed.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnOrangeRed.ForeColor = System.Drawing.Color.OrangeRed;
            this.btnOrangeRed.ImageTransparentColor = System.Drawing.Color.Transparent;
            this.btnOrangeRed.Name = "btnOrangeRed";
            this.btnOrangeRed.Size = new System.Drawing.Size(23, 22);
            this.btnOrangeRed.Text = "■";
            this.btnOrangeRed.Paint += new System.Windows.Forms.PaintEventHandler(this.btnColor_Paint);
            this.btnOrangeRed.Click += new System.EventHandler(this.btnColor_Click);
            // 
            // btnRed
            // 
            this.btnRed.AutoToolTip = false;
            this.btnRed.BackColor = System.Drawing.SystemColors.Control;
            this.btnRed.CheckOnClick = true;
            this.btnRed.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnRed.ForeColor = System.Drawing.Color.IndianRed;
            this.btnRed.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnRed.Name = "btnRed";
            this.btnRed.Size = new System.Drawing.Size(23, 22);
            this.btnRed.Text = "■";
            this.btnRed.Paint += new System.Windows.Forms.PaintEventHandler(this.btnColor_Paint);
            this.btnRed.Click += new System.EventHandler(this.btnColor_Click);
            // 
            // btnOrange
            // 
            this.btnOrange.AutoToolTip = false;
            this.btnOrange.CheckOnClick = true;
            this.btnOrange.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnOrange.ForeColor = System.Drawing.Color.DarkOrange;
            this.btnOrange.Image = ((System.Drawing.Image)(resources.GetObject("btnOrange.Image")));
            this.btnOrange.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnOrange.Name = "btnOrange";
            this.btnOrange.Size = new System.Drawing.Size(23, 22);
            this.btnOrange.Text = "■";
            this.btnOrange.Paint += new System.Windows.Forms.PaintEventHandler(this.btnColor_Paint);
            this.btnOrange.Click += new System.EventHandler(this.btnColor_Click);
            // 
            // btnYellow
            // 
            this.btnYellow.AutoToolTip = false;
            this.btnYellow.CheckOnClick = true;
            this.btnYellow.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnYellow.ForeColor = System.Drawing.Color.Yellow;
            this.btnYellow.Image = ((System.Drawing.Image)(resources.GetObject("btnYellow.Image")));
            this.btnYellow.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnYellow.Name = "btnYellow";
            this.btnYellow.Size = new System.Drawing.Size(23, 22);
            this.btnYellow.Text = "■";
            this.btnYellow.Paint += new System.Windows.Forms.PaintEventHandler(this.btnColor_Paint);
            this.btnYellow.Click += new System.EventHandler(this.btnColor_Click);
            // 
            // btnLightGreen
            // 
            this.btnLightGreen.AutoToolTip = false;
            this.btnLightGreen.CheckOnClick = true;
            this.btnLightGreen.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnLightGreen.ForeColor = System.Drawing.Color.LightGreen;
            this.btnLightGreen.Image = ((System.Drawing.Image)(resources.GetObject("btnLightGreen.Image")));
            this.btnLightGreen.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnLightGreen.Name = "btnLightGreen";
            this.btnLightGreen.Size = new System.Drawing.Size(23, 22);
            this.btnLightGreen.Text = "■";
            this.btnLightGreen.Paint += new System.Windows.Forms.PaintEventHandler(this.btnColor_Paint);
            this.btnLightGreen.Click += new System.EventHandler(this.btnColor_Click);
            // 
            // btnGreen
            // 
            this.btnGreen.AutoToolTip = false;
            this.btnGreen.CheckOnClick = true;
            this.btnGreen.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnGreen.ForeColor = System.Drawing.Color.Green;
            this.btnGreen.Image = ((System.Drawing.Image)(resources.GetObject("btnGreen.Image")));
            this.btnGreen.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnGreen.Name = "btnGreen";
            this.btnGreen.Size = new System.Drawing.Size(23, 22);
            this.btnGreen.Text = "■";
            this.btnGreen.Paint += new System.Windows.Forms.PaintEventHandler(this.btnColor_Paint);
            this.btnGreen.Click += new System.EventHandler(this.btnColor_Click);
            // 
            // btnLightBlue
            // 
            this.btnLightBlue.AutoToolTip = false;
            this.btnLightBlue.CheckOnClick = true;
            this.btnLightBlue.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnLightBlue.ForeColor = System.Drawing.Color.LightBlue;
            this.btnLightBlue.Image = ((System.Drawing.Image)(resources.GetObject("btnLightBlue.Image")));
            this.btnLightBlue.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnLightBlue.Name = "btnLightBlue";
            this.btnLightBlue.Size = new System.Drawing.Size(23, 22);
            this.btnLightBlue.Text = "■";
            this.btnLightBlue.Paint += new System.Windows.Forms.PaintEventHandler(this.btnColor_Paint);
            this.btnLightBlue.Click += new System.EventHandler(this.btnColor_Click);
            // 
            // btnBlue
            // 
            this.btnBlue.AutoToolTip = false;
            this.btnBlue.CheckOnClick = true;
            this.btnBlue.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnBlue.ForeColor = System.Drawing.Color.Blue;
            this.btnBlue.Image = ((System.Drawing.Image)(resources.GetObject("btnBlue.Image")));
            this.btnBlue.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnBlue.Name = "btnBlue";
            this.btnBlue.Size = new System.Drawing.Size(23, 22);
            this.btnBlue.Text = "■";
            this.btnBlue.Paint += new System.Windows.Forms.PaintEventHandler(this.btnColor_Paint);
            this.btnBlue.Click += new System.EventHandler(this.btnColor_Click);
            // 
            // btnBlack
            // 
            this.btnBlack.AutoToolTip = false;
            this.btnBlack.CheckOnClick = true;
            this.btnBlack.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnBlack.ForeColor = System.Drawing.Color.Purple;
            this.btnBlack.Image = ((System.Drawing.Image)(resources.GetObject("btnBlack.Image")));
            this.btnBlack.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnBlack.Name = "btnBlack";
            this.btnBlack.Size = new System.Drawing.Size(23, 22);
            this.btnBlack.Text = "■";
            this.btnBlack.Paint += new System.Windows.Forms.PaintEventHandler(this.btnColor_Paint);
            this.btnBlack.Click += new System.EventHandler(this.btnColor_Click);
            // 
            // btnPurple
            // 
            this.btnPurple.AutoToolTip = false;
            this.btnPurple.CheckOnClick = true;
            this.btnPurple.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnPurple.Image = ((System.Drawing.Image)(resources.GetObject("btnPurple.Image")));
            this.btnPurple.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnPurple.Name = "btnPurple";
            this.btnPurple.Size = new System.Drawing.Size(23, 22);
            this.btnPurple.Text = "■";
            this.btnPurple.Paint += new System.Windows.Forms.PaintEventHandler(this.btnColor_Paint);
            this.btnPurple.Click += new System.EventHandler(this.btnColor_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(66, 13);
            this.label2.TabIndex = 5;
            this.label2.Text = "Annotations:";
            // 
            // btnClearAll
            // 
            this.btnClearAll.Location = new System.Drawing.Point(390, 41);
            this.btnClearAll.Name = "btnClearAll";
            this.btnClearAll.Size = new System.Drawing.Size(75, 23);
            this.btnClearAll.TabIndex = 5;
            this.btnClearAll.Text = "Clear All";
            this.btnClearAll.UseVisualStyleBackColor = true;
            this.btnClearAll.Click += new System.EventHandler(this.btnClearAll_Click);
            // 
            // EditNoteDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(477, 322);
            this.Controls.Add(this.splitContainer1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditNoteDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit Note";
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
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
        private System.Windows.Forms.DataGridViewTextBoxColumn colName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colValue;
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
    }
}