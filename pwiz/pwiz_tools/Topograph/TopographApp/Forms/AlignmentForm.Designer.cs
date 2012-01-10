namespace pwiz.Topograph.ui.Forms
{
    partial class AlignmentForm
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
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.comboDataFile1 = new System.Windows.Forms.ComboBox();
            this.comboDataFile2 = new System.Windows.Forms.ComboBox();
            this.zedGraphControlEx1 = new pwiz.Topograph.ui.Controls.ZedGraphControlEx();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 4;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.label2, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.comboDataFile1, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.comboDataFile2, 1, 1);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 2;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(927, 48);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(3, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(71, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "First Data File";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(3, 24);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(89, 13);
            this.label2.TabIndex = 1;
            this.label2.Text = "Second Data File";
            // 
            // comboDataFile1
            // 
            this.comboDataFile1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.comboDataFile1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboDataFile1.FormattingEnabled = true;
            this.comboDataFile1.Location = new System.Drawing.Point(234, 3);
            this.comboDataFile1.Name = "comboDataFile1";
            this.comboDataFile1.Size = new System.Drawing.Size(225, 21);
            this.comboDataFile1.TabIndex = 2;
            // 
            // comboDataFile2
            // 
            this.comboDataFile2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.comboDataFile2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboDataFile2.FormattingEnabled = true;
            this.comboDataFile2.Location = new System.Drawing.Point(234, 27);
            this.comboDataFile2.Name = "comboDataFile2";
            this.comboDataFile2.Size = new System.Drawing.Size(225, 21);
            this.comboDataFile2.TabIndex = 3;
            // 
            // zedGraphControlEx1
            // 
            this.zedGraphControlEx1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.zedGraphControlEx1.Location = new System.Drawing.Point(0, 48);
            this.zedGraphControlEx1.Name = "zedGraphControlEx1";
            this.zedGraphControlEx1.ScrollGrace = 0;
            this.zedGraphControlEx1.ScrollMaxX = 0;
            this.zedGraphControlEx1.ScrollMaxY = 0;
            this.zedGraphControlEx1.ScrollMaxY2 = 0;
            this.zedGraphControlEx1.ScrollMinX = 0;
            this.zedGraphControlEx1.ScrollMinY = 0;
            this.zedGraphControlEx1.ScrollMinY2 = 0;
            this.zedGraphControlEx1.Size = new System.Drawing.Size(927, 214);
            this.zedGraphControlEx1.TabIndex = 1;
            // 
            // AlignmentForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(927, 262);
            this.Controls.Add(this.zedGraphControlEx1);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "AlignmentForm";
            this.TabText = "AlignmentForm";
            this.Text = "AlignmentForm";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox comboDataFile1;
        private System.Windows.Forms.ComboBox comboDataFile2;
        private pwiz.Topograph.ui.Controls.ZedGraphControlEx zedGraphControlEx1;
    }
}