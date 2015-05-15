namespace pwiz.Skyline.SettingsUI
{
    partial class EditOptimizationLibraryDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditOptimizationLibraryDlg));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            this.btnCreate = new System.Windows.Forms.Button();
            this.labelNumOptimizations = new System.Windows.Forms.Label();
            this.btnAdd = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnOpen = new System.Windows.Forms.Button();
            this.textDatabase = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.textName = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.gridViewLibrary = new pwiz.Skyline.Controls.DataGridViewEx();
            this.columnSequence = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnProductIon = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnOptType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnCharge = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnProductCharge = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.bindingSourceLibrary = new System.Windows.Forms.BindingSource(this.components);
            this.contextMenuAdd = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.addFromResultsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addFromFileMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.label3 = new System.Windows.Forms.Label();
            this.comboType = new System.Windows.Forms.ComboBox();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewLibrary)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceLibrary)).BeginInit();
            this.contextMenuAdd.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCreate
            // 
            resources.ApplyResources(this.btnCreate, "btnCreate");
            this.btnCreate.Name = "btnCreate";
            this.btnCreate.UseVisualStyleBackColor = true;
            this.btnCreate.Click += new System.EventHandler(this.btnCreate_Click);
            // 
            // labelNumOptimizations
            // 
            resources.ApplyResources(this.labelNumOptimizations, "labelNumOptimizations");
            this.labelNumOptimizations.Name = "labelNumOptimizations";
            // 
            // btnAdd
            // 
            resources.ApplyResources(this.btnAdd, "btnAdd");
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.UseVisualStyleBackColor = true;
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnOpen
            // 
            resources.ApplyResources(this.btnOpen, "btnOpen");
            this.btnOpen.Name = "btnOpen";
            this.btnOpen.UseVisualStyleBackColor = true;
            this.btnOpen.Click += new System.EventHandler(this.btnOpen_Click);
            // 
            // textDatabase
            // 
            resources.ApplyResources(this.textDatabase, "textDatabase");
            this.textDatabase.Name = "textDatabase";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // textName
            // 
            resources.ApplyResources(this.textName, "textName");
            this.textName.Name = "textName";
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // gridViewLibrary
            // 
            resources.ApplyResources(this.gridViewLibrary, "gridViewLibrary");
            this.gridViewLibrary.AutoGenerateColumns = false;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridViewLibrary.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.gridViewLibrary.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridViewLibrary.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.columnSequence,
            this.columnProductIon,
            this.columnValue,
            this.columnOptType,
            this.columnCharge,
            this.columnProductCharge});
            this.gridViewLibrary.DataSource = this.bindingSourceLibrary;
            dataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle5.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle5.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle5.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle5.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle5.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle5.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridViewLibrary.DefaultCellStyle = dataGridViewCellStyle5;
            this.gridViewLibrary.Name = "gridViewLibrary";
            // 
            // columnSequence
            // 
            this.columnSequence.DataPropertyName = "PeptideModSeq";
            dataGridViewCellStyle2.NullValue = null;
            this.columnSequence.DefaultCellStyle = dataGridViewCellStyle2;
            resources.ApplyResources(this.columnSequence, "columnSequence");
            this.columnSequence.Name = "columnSequence";
            // 
            // columnProductIon
            // 
            this.columnProductIon.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.columnProductIon.DataPropertyName = "FragmentIon";
            dataGridViewCellStyle3.NullValue = null;
            this.columnProductIon.DefaultCellStyle = dataGridViewCellStyle3;
            resources.ApplyResources(this.columnProductIon, "columnProductIon");
            this.columnProductIon.Name = "columnProductIon";
            // 
            // columnValue
            // 
            this.columnValue.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.columnValue.DataPropertyName = "Value";
            dataGridViewCellStyle4.Format = "N2";
            dataGridViewCellStyle4.NullValue = null;
            this.columnValue.DefaultCellStyle = dataGridViewCellStyle4;
            resources.ApplyResources(this.columnValue, "columnValue");
            this.columnValue.Name = "columnValue";
            // 
            // columnOptType
            // 
            this.columnOptType.DataPropertyName = "Type";
            resources.ApplyResources(this.columnOptType, "columnOptType");
            this.columnOptType.Name = "columnOptType";
            // 
            // columnCharge
            // 
            this.columnCharge.DataPropertyName = "Charge";
            resources.ApplyResources(this.columnCharge, "columnCharge");
            this.columnCharge.Name = "columnCharge";
            // 
            // columnProductCharge
            // 
            this.columnProductCharge.DataPropertyName = "ProductCharge";
            resources.ApplyResources(this.columnProductCharge, "columnProductCharge");
            this.columnProductCharge.Name = "columnProductCharge";
            // 
            // contextMenuAdd
            // 
            this.contextMenuAdd.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addFromResultsMenuItem,
            this.addFromFileMenuItem});
            this.contextMenuAdd.Name = "contextMenuAdd";
            resources.ApplyResources(this.contextMenuAdd, "contextMenuAdd");
            // 
            // addFromResultsMenuItem
            // 
            this.addFromResultsMenuItem.Name = "addFromResultsMenuItem";
            resources.ApplyResources(this.addFromResultsMenuItem, "addFromResultsMenuItem");
            this.addFromResultsMenuItem.Click += new System.EventHandler(this.addFromResultsMenuItem_Click);
            // 
            // addFromFileMenuItem
            // 
            this.addFromFileMenuItem.Name = "addFromFileMenuItem";
            resources.ApplyResources(this.addFromFileMenuItem, "addFromFileMenuItem");
            this.addFromFileMenuItem.Click += new System.EventHandler(this.addFromFileMenuItem_Click);
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // comboType
            // 
            this.comboType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboType.FormattingEnabled = true;
            resources.ApplyResources(this.comboType, "comboType");
            this.comboType.Name = "comboType";
            this.comboType.SelectedIndexChanged += new System.EventHandler(this.comboType_SelectedIndexChanged);
            // 
            // EditOptimizationLibraryDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.comboType);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.gridViewLibrary);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.btnCreate);
            this.Controls.Add(this.labelNumOptimizations);
            this.Controls.Add(this.btnAdd);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnOpen);
            this.Controls.Add(this.textDatabase);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textName);
            this.Controls.Add(this.label1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditOptimizationLibraryDlg";
            this.ShowInTaskbar = false;
            this.Load += new System.EventHandler(this.OnLoad);
            ((System.ComponentModel.ISupportInitialize)(this.gridViewLibrary)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceLibrary)).EndInit();
            this.contextMenuAdd.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCreate;
        private System.Windows.Forms.Label labelNumOptimizations;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnOpen;
        private System.Windows.Forms.TextBox textDatabase;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label label1;
        private Controls.DataGridViewEx gridViewLibrary;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.BindingSource bindingSourceLibrary;
        private System.Windows.Forms.ContextMenuStrip contextMenuAdd;
        private System.Windows.Forms.ToolStripMenuItem addFromResultsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addFromFileMenuItem;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox comboType;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnSequence;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnProductIon;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnValue;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnOptType;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnCharge;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnProductCharge;

    }
}