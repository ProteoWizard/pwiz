namespace pwiz.Skyline.ToolsUI
{
    partial class ColorGrid<T>
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
            this.components = new System.ComponentModel.Container();
            this.bindingSource1 = new System.Windows.Forms.BindingSource(this.components);
            this.comboColorType = new System.Windows.Forms.ComboBox();
            this.dataGridViewColors = new pwiz.Common.Controls.CommonDataGridView();
            this.colorPickerDlg = new System.Windows.Forms.ColorDialog();
            this.rgbCol = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.hexCol = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colBtn = new System.Windows.Forms.DataGridViewButtonColumn();
            this.colorCol = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewColors)).BeginInit();
            this.SuspendLayout();
            // 
            // bindingSource1
            // 
            this.bindingSource1.AllowNew = true;
            // 
            // comboColorType
            // 
            this.comboColorType.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.comboColorType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboColorType.FormattingEnabled = true;
            this.comboColorType.Items.AddRange(new object[] {
            "RGB",
            "HEX"});
            this.comboColorType.Location = new System.Drawing.Point(3, 214);
            this.comboColorType.Name = "comboColorType";
            this.comboColorType.Size = new System.Drawing.Size(60, 21);
            this.comboColorType.TabIndex = 5;
            this.comboColorType.SelectedIndexChanged += new System.EventHandler(this.comboColorType_SelectedIndexChanged);
            // 
            // dataGridViewColors
            // 
            this.dataGridViewColors.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridViewColors.AutoGenerateColumns = false;
            this.dataGridViewColors.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.AllCells;
            this.dataGridViewColors.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewColors.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.rgbCol,
            this.hexCol,
            this.colBtn,
            this.colorCol});
            this.dataGridViewColors.DataSource = this.bindingSource1;
            this.dataGridViewColors.Location = new System.Drawing.Point(0, 0);
            this.dataGridViewColors.Name = "dataGridViewColors";
            this.dataGridViewColors.Size = new System.Drawing.Size(318, 200);
            this.dataGridViewColors.TabIndex = 4;
            this.dataGridViewColors.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridViewColors_CellClick);
            this.dataGridViewColors.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.dataGridViewColors_CellFormatting);
            this.dataGridViewColors.CurrentCellDirtyStateChanged += new System.EventHandler(this.dataGridViewColors_CurrentCellDirtyStateChanged);
            this.dataGridViewColors.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.dataGridViewColors_DataError);
            this.dataGridViewColors.KeyDown += new System.Windows.Forms.KeyEventHandler(this.dataGridViewColors_KeyDown);
            // 
            // rgbCol
            // 
            this.rgbCol.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.rgbCol.DataPropertyName = "Rgb";
            this.rgbCol.HeaderText = "RGB";
            this.rgbCol.MinimumWidth = 100;
            this.rgbCol.Name = "rgbCol";
            this.rgbCol.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.rgbCol.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // hexCol
            // 
            this.hexCol.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.hexCol.DataPropertyName = "Hex";
            this.hexCol.HeaderText = "HEX";
            this.hexCol.MinimumWidth = 100;
            this.hexCol.Name = "hexCol";
            // 
            // colBtn
            // 
            this.colBtn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.colBtn.HeaderText = "";
            this.colBtn.MinimumWidth = 20;
            this.colBtn.Name = "colBtn";
            this.colBtn.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.colBtn.Text = "...";
            this.colBtn.Width = 20;
            // 
            // colorCol
            // 
            this.colorCol.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.colorCol.HeaderText = "";
            this.colorCol.Name = "colorCol";
            this.colorCol.ReadOnly = true;
            this.colorCol.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.colorCol.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.colorCol.Width = 40;
            // 
            // ColorGrid
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.comboColorType);
            this.Controls.Add(this.dataGridViewColors);
            this.Name = "ColorGrid";
            this.Size = new System.Drawing.Size(318, 235);
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewColors)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.BindingSource bindingSource1;
        private System.Windows.Forms.ComboBox comboColorType;
        private Common.Controls.CommonDataGridView dataGridViewColors;
        private System.Windows.Forms.ColorDialog colorPickerDlg;
        private System.Windows.Forms.DataGridViewTextBoxColumn rgbCol;
        private System.Windows.Forms.DataGridViewTextBoxColumn hexCol;
        private System.Windows.Forms.DataGridViewButtonColumn colBtn;
        private System.Windows.Forms.DataGridViewTextBoxColumn colorCol;
    }
}
