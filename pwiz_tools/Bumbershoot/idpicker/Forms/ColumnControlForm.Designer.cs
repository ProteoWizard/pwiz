namespace IDPicker.Forms
{
    partial class ColumnControlForm
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            this.columnOptionsDGV = new System.Windows.Forms.DataGridView();
            this.cancel_Button = new System.Windows.Forms.Button();
            this.ok_Button = new System.Windows.Forms.Button();
            this.WindowBackColorBox = new System.Windows.Forms.TextBox();
            this.WindowBackgroundColorLabel = new System.Windows.Forms.Label();
            this.WindowTextColorLabel = new System.Windows.Forms.Label();
            this.WindowTextColorBox = new System.Windows.Forms.TextBox();
            this.PreviewBox = new System.Windows.Forms.TextBox();
            this.nameColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.typeColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.decimalColumn = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.ColorColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.visibleColumn = new System.Windows.Forms.DataGridViewComboBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.columnOptionsDGV)).BeginInit();
            this.SuspendLayout();
            // 
            // columnOptionsDGV
            // 
            this.columnOptionsDGV.AllowUserToAddRows = false;
            this.columnOptionsDGV.AllowUserToDeleteRows = false;
            this.columnOptionsDGV.AllowUserToResizeRows = false;
            this.columnOptionsDGV.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.columnOptionsDGV.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.columnOptionsDGV.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.nameColumn,
            this.typeColumn,
            this.decimalColumn,
            this.ColorColumn,
            this.visibleColumn});
            this.columnOptionsDGV.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.columnOptionsDGV.Location = new System.Drawing.Point(12, 12);
            this.columnOptionsDGV.MultiSelect = false;
            this.columnOptionsDGV.Name = "columnOptionsDGV";
            this.columnOptionsDGV.RowHeadersVisible = false;
            this.columnOptionsDGV.Size = new System.Drawing.Size(483, 286);
            this.columnOptionsDGV.TabIndex = 0;
            this.columnOptionsDGV.CellBeginEdit += new System.Windows.Forms.DataGridViewCellCancelEventHandler(this.columnOptionsDGV_CellBeginEdit);
            this.columnOptionsDGV.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.columnOptionsDGV_CellClick);
            this.columnOptionsDGV.CellEnter += new System.Windows.Forms.DataGridViewCellEventHandler(this.columnOptionsDGV_CellEnter);
            // 
            // cancel_Button
            // 
            this.cancel_Button.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancel_Button.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancel_Button.Location = new System.Drawing.Point(420, 304);
            this.cancel_Button.Name = "cancel_Button";
            this.cancel_Button.Size = new System.Drawing.Size(75, 23);
            this.cancel_Button.TabIndex = 1;
            this.cancel_Button.Text = "Cancel";
            this.cancel_Button.UseVisualStyleBackColor = true;
            // 
            // ok_Button
            // 
            this.ok_Button.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.ok_Button.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.ok_Button.Location = new System.Drawing.Point(339, 304);
            this.ok_Button.Name = "ok_Button";
            this.ok_Button.Size = new System.Drawing.Size(75, 23);
            this.ok_Button.TabIndex = 2;
            this.ok_Button.Text = "OK";
            this.ok_Button.UseVisualStyleBackColor = true;
            this.ok_Button.Click += new System.EventHandler(this.ok_Button_Click);
            // 
            // WindowBackColorBox
            // 
            this.WindowBackColorBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.WindowBackColorBox.Cursor = System.Windows.Forms.Cursors.Hand;
            this.WindowBackColorBox.Location = new System.Drawing.Point(70, 306);
            this.WindowBackColorBox.Name = "WindowBackColorBox";
            this.WindowBackColorBox.Size = new System.Drawing.Size(24, 20);
            this.WindowBackColorBox.TabIndex = 3;
            this.WindowBackColorBox.TabStop = false;
            this.WindowBackColorBox.Click += new System.EventHandler(this.WindowBackColorBox_Click);
            this.WindowBackColorBox.Enter += new System.EventHandler(this.Unselectable);
            // 
            // WindowBackgroundColorLabel
            // 
            this.WindowBackgroundColorLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.WindowBackgroundColorLabel.AutoSize = true;
            this.WindowBackgroundColorLabel.Location = new System.Drawing.Point(12, 309);
            this.WindowBackgroundColorLabel.Name = "WindowBackgroundColorLabel";
            this.WindowBackgroundColorLabel.Size = new System.Drawing.Size(52, 13);
            this.WindowBackgroundColorLabel.TabIndex = 4;
            this.WindowBackgroundColorLabel.Text = "BG Color:";
            // 
            // WindowTextColorLabel
            // 
            this.WindowTextColorLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.WindowTextColorLabel.AutoSize = true;
            this.WindowTextColorLabel.Location = new System.Drawing.Point(112, 309);
            this.WindowTextColorLabel.Name = "WindowTextColorLabel";
            this.WindowTextColorLabel.Size = new System.Drawing.Size(58, 13);
            this.WindowTextColorLabel.TabIndex = 6;
            this.WindowTextColorLabel.Text = "Text Color:";
            // 
            // WindowTextColorBox
            // 
            this.WindowTextColorBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.WindowTextColorBox.Cursor = System.Windows.Forms.Cursors.Hand;
            this.WindowTextColorBox.Location = new System.Drawing.Point(176, 306);
            this.WindowTextColorBox.Name = "WindowTextColorBox";
            this.WindowTextColorBox.Size = new System.Drawing.Size(24, 20);
            this.WindowTextColorBox.TabIndex = 5;
            this.WindowTextColorBox.TabStop = false;
            this.WindowTextColorBox.Click += new System.EventHandler(this.WindowTextColorBox_Click);
            this.WindowTextColorBox.Enter += new System.EventHandler(this.Unselectable);
            // 
            // PreviewBox
            // 
            this.PreviewBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.PreviewBox.Cursor = System.Windows.Forms.Cursors.Default;
            this.PreviewBox.Location = new System.Drawing.Point(230, 306);
            this.PreviewBox.Name = "PreviewBox";
            this.PreviewBox.Size = new System.Drawing.Size(81, 20);
            this.PreviewBox.TabIndex = 7;
            this.PreviewBox.TabStop = false;
            this.PreviewBox.Text = "Color Preview";
            this.PreviewBox.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.PreviewBox.Enter += new System.EventHandler(this.Unselectable);
            // 
            // nameColumn
            // 
            this.nameColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.nameColumn.HeaderText = "Name";
            this.nameColumn.MinimumWidth = 90;
            this.nameColumn.Name = "nameColumn";
            this.nameColumn.ReadOnly = true;
            // 
            // typeColumn
            // 
            this.typeColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.typeColumn.HeaderText = "Data Type";
            this.typeColumn.MinimumWidth = 50;
            this.typeColumn.Name = "typeColumn";
            this.typeColumn.ReadOnly = true;
            this.typeColumn.Visible = false;
            // 
            // decimalColumn
            // 
            this.decimalColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            dataGridViewCellStyle1.NullValue = "n/a";
            this.decimalColumn.DefaultCellStyle = dataGridViewCellStyle1;
            this.decimalColumn.FillWeight = 50F;
            this.decimalColumn.HeaderText = "Decimal Places";
            this.decimalColumn.Items.AddRange(new object[] {
            "Auto",
            "0",
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9",
            "10",
            "11",
            "12",
            "13",
            "14",
            "15"});
            this.decimalColumn.MinimumWidth = 50;
            this.decimalColumn.Name = "decimalColumn";
            // 
            // ColorColumn
            // 
            this.ColorColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            dataGridViewCellStyle2.NullValue = "Text";
            this.ColorColumn.DefaultCellStyle = dataGridViewCellStyle2;
            this.ColorColumn.FillWeight = 1F;
            this.ColorColumn.HeaderText = "Column Color";
            this.ColorColumn.MinimumWidth = 95;
            this.ColorColumn.Name = "ColorColumn";
            this.ColorColumn.ReadOnly = true;
            this.ColorColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.ColorColumn.Width = 95;
            // 
            // visibleColumn
            // 
            this.visibleColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.visibleColumn.FillWeight = 1F;
            this.visibleColumn.HeaderText = "Visible?";
            this.visibleColumn.Items.AddRange(new object[] {
            "Always",
            "Yes",
            "No",
            "Never"});
            this.visibleColumn.MinimumWidth = 65;
            this.visibleColumn.Name = "visibleColumn";
            this.visibleColumn.Width = 65;
            // 
            // ColumnControlForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(507, 339);
            this.Controls.Add(this.PreviewBox);
            this.Controls.Add(this.WindowTextColorLabel);
            this.Controls.Add(this.WindowTextColorBox);
            this.Controls.Add(this.WindowBackgroundColorLabel);
            this.Controls.Add(this.WindowBackColorBox);
            this.Controls.Add(this.ok_Button);
            this.Controls.Add(this.cancel_Button);
            this.Controls.Add(this.columnOptionsDGV);
            this.MinimumSize = new System.Drawing.Size(500, 145);
            this.Name = "ColumnControlForm";
            this.Text = "ColumnControlForm";
            ((System.ComponentModel.ISupportInitialize)(this.columnOptionsDGV)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataGridView columnOptionsDGV;
        private System.Windows.Forms.Button cancel_Button;
        private System.Windows.Forms.Button ok_Button;
        private System.Windows.Forms.Label WindowBackgroundColorLabel;
        private System.Windows.Forms.Label WindowTextColorLabel;
        private System.Windows.Forms.TextBox PreviewBox;
        public System.Windows.Forms.TextBox WindowBackColorBox;
        public System.Windows.Forms.TextBox WindowTextColorBox;
        private System.Windows.Forms.DataGridViewTextBoxColumn nameColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn typeColumn;
        private System.Windows.Forms.DataGridViewComboBoxColumn decimalColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColorColumn;
        private System.Windows.Forms.DataGridViewComboBoxColumn visibleColumn;
    }
}