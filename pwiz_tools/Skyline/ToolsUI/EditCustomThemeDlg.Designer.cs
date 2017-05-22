namespace pwiz.Skyline.ToolsUI
{
    partial class EditCustomThemeDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditCustomThemeDlg));
            this.colorPickerDlg = new System.Windows.Forms.ColorDialog();
            this.buttonSave = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.dataGridViewColors = new System.Windows.Forms.DataGridView();
            this.colorCol = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.rgbCol = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.hexCol = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colBtn = new System.Windows.Forms.DataGridViewButtonColumn();
            this.bindingSource1 = new System.Windows.Forms.BindingSource(this.components);
            this.comboColorType = new System.Windows.Forms.ComboBox();
            this.comboBoxCategory = new System.Windows.Forms.ComboBox();
            this.groupBoxCategory = new System.Windows.Forms.GroupBox();
            this.lableColorCount = new System.Windows.Forms.Label();
            this.textBoxName = new System.Windows.Forms.TextBox();
            this.labelName = new System.Windows.Forms.Label();
            this.labelCategory = new System.Windows.Forms.Label();
            this.dataGridViewTextBoxColumn1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewColors)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).BeginInit();
            this.groupBoxCategory.SuspendLayout();
            this.SuspendLayout();
            // 
            // buttonSave
            // 
            resources.ApplyResources(this.buttonSave, "buttonSave");
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // buttonCancel
            // 
            resources.ApplyResources(this.buttonCancel, "buttonCancel");
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            // 
            // dataGridViewColors
            // 
            this.dataGridViewColors.AllowUserToOrderColumns = true;
            resources.ApplyResources(this.dataGridViewColors, "dataGridViewColors");
            this.dataGridViewColors.AutoGenerateColumns = false;
            this.dataGridViewColors.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.AllCells;
            this.dataGridViewColors.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewColors.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colorCol,
            this.rgbCol,
            this.hexCol,
            this.colBtn});
            this.dataGridViewColors.DataSource = this.bindingSource1;
            this.dataGridViewColors.Name = "dataGridViewColors";
            this.dataGridViewColors.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView1_CellClick);
            this.dataGridViewColors.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.dataGridViewColors_CellFormatting);
            this.dataGridViewColors.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.dataGridViewColors_DataError);
            this.dataGridViewColors.KeyDown += new System.Windows.Forms.KeyEventHandler(this.dataGridViewColors_KeyDown);
            // 
            // colorCol
            // 
            this.colorCol.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            resources.ApplyResources(this.colorCol, "colorCol");
            this.colorCol.Name = "colorCol";
            this.colorCol.ReadOnly = true;
            this.colorCol.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.colorCol.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // rgbCol
            // 
            this.rgbCol.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.rgbCol.DataPropertyName = "Rgb";
            resources.ApplyResources(this.rgbCol, "rgbCol");
            this.rgbCol.Name = "rgbCol";
            this.rgbCol.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.rgbCol.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // hexCol
            // 
            this.hexCol.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.hexCol.DataPropertyName = "Hex";
            resources.ApplyResources(this.hexCol, "hexCol");
            this.hexCol.Name = "hexCol";
            // 
            // colBtn
            // 
            this.colBtn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            resources.ApplyResources(this.colBtn, "colBtn");
            this.colBtn.Name = "colBtn";
            this.colBtn.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.colBtn.Text = "...";
            // 
            // bindingSource1
            // 
            this.bindingSource1.AllowNew = true;
            this.bindingSource1.ListChanged += new System.ComponentModel.ListChangedEventHandler(this.bindingSource1_ListChanged);
            // 
            // comboColorType
            // 
            resources.ApplyResources(this.comboColorType, "comboColorType");
            this.comboColorType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboColorType.FormattingEnabled = true;
            this.comboColorType.Items.AddRange(new object[] {
            resources.GetString("comboColorType.Items"),
            resources.GetString("comboColorType.Items1")});
            this.comboColorType.Name = "comboColorType";
            this.comboColorType.SelectedIndexChanged += new System.EventHandler(this.comboColorType_SelectedIndexChanged);
            // 
            // comboBoxCategory
            // 
            this.comboBoxCategory.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxCategory.FormattingEnabled = true;
            this.comboBoxCategory.Items.AddRange(new object[] {
            resources.GetString("comboBoxCategory.Items"),
            resources.GetString("comboBoxCategory.Items1")});
            resources.ApplyResources(this.comboBoxCategory, "comboBoxCategory");
            this.comboBoxCategory.Name = "comboBoxCategory";
            this.comboBoxCategory.SelectedIndexChanged += new System.EventHandler(this.comboBoxCategory_SelectedIndexChanged);
            // 
            // groupBoxCategory
            // 
            resources.ApplyResources(this.groupBoxCategory, "groupBoxCategory");
            this.groupBoxCategory.Controls.Add(this.lableColorCount);
            this.groupBoxCategory.Controls.Add(this.comboColorType);
            this.groupBoxCategory.Controls.Add(this.dataGridViewColors);
            this.groupBoxCategory.Name = "groupBoxCategory";
            this.groupBoxCategory.TabStop = false;
            // 
            // lableColorCount
            // 
            resources.ApplyResources(this.lableColorCount, "lableColorCount");
            this.lableColorCount.Name = "lableColorCount";
            // 
            // textBoxName
            // 
            resources.ApplyResources(this.textBoxName, "textBoxName");
            this.textBoxName.Name = "textBoxName";
            // 
            // labelName
            // 
            resources.ApplyResources(this.labelName, "labelName");
            this.labelName.Name = "labelName";
            // 
            // labelCategory
            // 
            resources.ApplyResources(this.labelCategory, "labelCategory");
            this.labelCategory.Name = "labelCategory";
            // 
            // dataGridViewTextBoxColumn1
            // 
            resources.ApplyResources(this.dataGridViewTextBoxColumn1, "dataGridViewTextBoxColumn1");
            this.dataGridViewTextBoxColumn1.Name = "dataGridViewTextBoxColumn1";
            this.dataGridViewTextBoxColumn1.ReadOnly = true;
            this.dataGridViewTextBoxColumn1.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridViewTextBoxColumn1.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // dataGridViewTextBoxColumn2
            // 
            resources.ApplyResources(this.dataGridViewTextBoxColumn2, "dataGridViewTextBoxColumn2");
            this.dataGridViewTextBoxColumn2.Name = "dataGridViewTextBoxColumn2";
            this.dataGridViewTextBoxColumn2.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridViewTextBoxColumn2.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // dataGridViewTextBoxColumn3
            // 
            resources.ApplyResources(this.dataGridViewTextBoxColumn3, "dataGridViewTextBoxColumn3");
            this.dataGridViewTextBoxColumn3.Name = "dataGridViewTextBoxColumn3";
            // 
            // EditCustomThemeDlg
            // 
            this.AcceptButton = this.buttonSave;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.labelCategory);
            this.Controls.Add(this.labelName);
            this.Controls.Add(this.textBoxName);
            this.Controls.Add(this.groupBoxCategory);
            this.Controls.Add(this.comboBoxCategory);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonSave);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditCustomThemeDlg";
            this.ShowIcon = false;
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewColors)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).EndInit();
            this.groupBoxCategory.ResumeLayout(false);
            this.groupBoxCategory.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ColorDialog colorPickerDlg;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.DataGridView dataGridViewColors;
        private System.Windows.Forms.ComboBox comboColorType;
        private System.Windows.Forms.ComboBox comboBoxCategory;
        private System.Windows.Forms.GroupBox groupBoxCategory;
        private System.Windows.Forms.TextBox textBoxName;
        private System.Windows.Forms.Label labelName;
        private System.Windows.Forms.Label labelCategory;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn1;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn2;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn3;
        private System.Windows.Forms.BindingSource bindingSource1;
        private System.Windows.Forms.Label lableColorCount;
        private System.Windows.Forms.DataGridViewTextBoxColumn colorCol;
        private System.Windows.Forms.DataGridViewTextBoxColumn rgbCol;
        private System.Windows.Forms.DataGridViewTextBoxColumn hexCol;
        private System.Windows.Forms.DataGridViewButtonColumn colBtn;
    }
}