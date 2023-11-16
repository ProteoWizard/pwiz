using pwiz.Skyline.Controls.GroupComparison;

namespace pwiz.Skyline.Controls.Graphs
{
    partial class CreateIntensityMatchExpressionDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CreateMatchExpressionDlg));
            this.cancelButton = new System.Windows.Forms.Button();
            this.okButton = new System.Windows.Forms.Button();
            this.dataGridView1 = new pwiz.Skyline.Controls.DataGridViewEx();
            this.nameColumn = new System.Windows.Forms.DataGridViewLinkColumn();
            this.bindingSource1 = new System.Windows.Forms.BindingSource(this.components);
            this.expressionTextBox = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.matchComboBox = new System.Windows.Forms.ComboBox();
            this.linkRegex = new System.Windows.Forms.LinkLabel();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).BeginInit();
            this.SuspendLayout();
            // 
            // cancelButton
            // 
            resources.ApplyResources(this.cancelButton, "cancelButton");
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.UseVisualStyleBackColor = true;
            // 
            // okButton
            // 
            resources.ApplyResources(this.okButton, "okButton");
            this.okButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.okButton.Name = "okButton";
            this.okButton.UseVisualStyleBackColor = true;
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.AllowUserToResizeRows = false;
            resources.ApplyResources(this.dataGridView1, "dataGridView1");
            this.dataGridView1.AutoGenerateColumns = false;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.nameColumn});
            this.dataGridView1.DataSource = this.bindingSource1;
            this.dataGridView1.MaximumColumnCount = null;
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.ReadOnly = true;
            this.dataGridView1.RowHeadersVisible = false;
            this.dataGridView1.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridView1.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView1_CellContentClick);
            // 
            // nameColumn
            // 
            this.nameColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.nameColumn.DataPropertyName = "Value";
            resources.ApplyResources(this.nameColumn, "nameColumn");
            this.nameColumn.Name = "nameColumn";
            this.nameColumn.ReadOnly = true;
            this.nameColumn.TrackVisitedState = false;
            // 
            // expressionTextBox
            // 
            resources.ApplyResources(this.expressionTextBox, "expressionTextBox");
            this.expressionTextBox.AutoCompleteCustomSource.AddRange(new string[] {
            resources.GetString("expressionTextBox.AutoCompleteCustomSource")});
            this.expressionTextBox.Name = "expressionTextBox";
            this.expressionTextBox.TextChanged += new System.EventHandler(this.expressionTextBox_TextChanged);
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // matchComboBox
            // 
            resources.ApplyResources(this.matchComboBox, "matchComboBox");
            this.matchComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.matchComboBox.FormattingEnabled = true;
            this.matchComboBox.Name = "matchComboBox";
            this.matchComboBox.SelectedIndexChanged += new System.EventHandler(this.comboBox_SelectedIndexChanged);
            // 
            // linkRegex
            // 
            resources.ApplyResources(this.linkRegex, "linkRegex");
            this.linkRegex.Name = "linkRegex";
            this.linkRegex.TabStop = true;
            this.linkRegex.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkRegex_LinkClicked);
            // 
            // CreateMatchExpressionDlg
            // 
            this.AcceptButton = this.okButton;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancelButton;
            this.Controls.Add(this.linkRegex);
            this.Controls.Add(this.expressionTextBox);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.matchComboBox);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.okButton);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CreateMatchExpressionDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Button okButton;
        private DataGridViewEx dataGridView1;
        private System.Windows.Forms.TextBox expressionTextBox;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox matchComboBox;
        private System.Windows.Forms.LinkLabel linkRegex;
        private System.Windows.Forms.BindingSource bindingSource1;
        private System.Windows.Forms.DataGridViewLinkColumn nameColumn;
    }
}