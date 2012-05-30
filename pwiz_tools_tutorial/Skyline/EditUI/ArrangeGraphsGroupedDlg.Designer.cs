namespace pwiz.Skyline.EditUI
{
    partial class ArrangeGraphsGroupedDlg
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
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.textGroups = new System.Windows.Forms.TextBox();
            this.radioDistribute = new System.Windows.Forms.RadioButton();
            this.radioSeparate = new System.Windows.Forms.RadioButton();
            this.toolTipControls = new System.Windows.Forms.ToolTip(this.components);
            this.label2 = new System.Windows.Forms.Label();
            this.comboSortOrder = new System.Windows.Forms.ComboBox();
            this.cbReversed = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(211, 42);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 5;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(211, 12);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 4;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 12);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(71, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "&Group panes:";
            // 
            // textGroups
            // 
            this.textGroups.Location = new System.Drawing.Point(16, 29);
            this.textGroups.Name = "textGroups";
            this.textGroups.Size = new System.Drawing.Size(100, 20);
            this.textGroups.TabIndex = 1;
            this.toolTipControls.SetToolTip(this.textGroups, "Enter the number of panes into which\r\nthe graphs will be grouped.");
            // 
            // radioDistribute
            // 
            this.radioDistribute.AutoSize = true;
            this.radioDistribute.Location = new System.Drawing.Point(16, 99);
            this.radioDistribute.Name = "radioDistribute";
            this.radioDistribute.Size = new System.Drawing.Size(174, 17);
            this.radioDistribute.TabIndex = 2;
            this.radioDistribute.Text = "&Distribute graphs among groups";
            this.toolTipControls.SetToolTip(this.radioDistribute, "Graphs are distributed among the panes as if\r\ndealing a deck of cards.");
            this.radioDistribute.UseVisualStyleBackColor = true;
            // 
            // radioSeparate
            // 
            this.radioSeparate.AutoSize = true;
            this.radioSeparate.Checked = true;
            this.radioSeparate.Location = new System.Drawing.Point(16, 76);
            this.radioSeparate.Name = "radioSeparate";
            this.radioSeparate.Size = new System.Drawing.Size(158, 17);
            this.radioSeparate.TabIndex = 3;
            this.radioSeparate.TabStop = true;
            this.radioSeparate.Text = "&Separate graphs into groups";
            this.toolTipControls.SetToolTip(this.radioSeparate, "Graphs are separated into groups in sequence,\r\nwith each pane receiving its share" +
                    " before any are\r\nadded to the next pane.");
            this.radioSeparate.UseVisualStyleBackColor = true;
            // 
            // toolTipControls
            // 
            this.toolTipControls.AutoPopDelay = 10000;
            this.toolTipControls.InitialDelay = 500;
            this.toolTipControls.ReshowDelay = 100;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(13, 140);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(56, 13);
            this.label2.TabIndex = 6;
            this.label2.Text = "Sort &order:";
            // 
            // comboSortOrder
            // 
            this.comboSortOrder.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboSortOrder.FormattingEnabled = true;
            this.comboSortOrder.Items.AddRange(new object[] {
            "Position",
            "Document"});
            this.comboSortOrder.Location = new System.Drawing.Point(16, 156);
            this.comboSortOrder.Name = "comboSortOrder";
            this.comboSortOrder.Size = new System.Drawing.Size(121, 21);
            this.comboSortOrder.TabIndex = 7;
            // 
            // cbReversed
            // 
            this.cbReversed.AutoSize = true;
            this.cbReversed.Location = new System.Drawing.Point(16, 183);
            this.cbReversed.Name = "cbReversed";
            this.cbReversed.Size = new System.Drawing.Size(72, 17);
            this.cbReversed.TabIndex = 8;
            this.cbReversed.Text = "&Reversed";
            this.cbReversed.UseVisualStyleBackColor = true;
            // 
            // ArrangeGraphsGroupedDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(298, 219);
            this.Controls.Add(this.cbReversed);
            this.Controls.Add(this.comboSortOrder);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.radioSeparate);
            this.Controls.Add(this.radioDistribute);
            this.Controls.Add(this.textGroups);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ArrangeGraphsGroupedDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Arrange Graphs Grouped";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textGroups;
        private System.Windows.Forms.RadioButton radioDistribute;
        private System.Windows.Forms.RadioButton radioSeparate;
        private System.Windows.Forms.ToolTip toolTipControls;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox comboSortOrder;
        private System.Windows.Forms.CheckBox cbReversed;
    }
}