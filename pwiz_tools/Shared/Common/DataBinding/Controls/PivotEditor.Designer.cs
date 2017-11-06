namespace pwiz.Common.DataBinding.Controls
{
    partial class PivotEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PivotEditor));
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.valueButtonPanel = new System.Windows.Forms.Panel();
            this.valueButtons = new System.Windows.Forms.Panel();
            this.btnAddValue = new System.Windows.Forms.Button();
            this.comboAggregateOp = new System.Windows.Forms.ComboBox();
            this.availableColumnList = new pwiz.Common.DataBinding.Controls.ColumnListView();
            this.rowHeaderButtonPanel = new System.Windows.Forms.Panel();
            this.btnAddRowHeader = new System.Windows.Forms.Button();
            this.columnHeaderButtonPanel = new System.Windows.Forms.Panel();
            this.btnAddColumnHeader = new System.Windows.Forms.Button();
            this.rowHeadersPanel = new System.Windows.Forms.Panel();
            this.rowHeadersList = new pwiz.Common.DataBinding.Controls.ColumnListEditor();
            this.lblRowHeaders = new System.Windows.Forms.Label();
            this.panel3 = new System.Windows.Forms.Panel();
            this.columnHeadersList = new pwiz.Common.DataBinding.Controls.ColumnListEditor();
            this.lblColumnHeaders = new System.Windows.Forms.Label();
            this.panelValues = new System.Windows.Forms.Panel();
            this.valuesList = new pwiz.Common.DataBinding.Controls.ColumnListEditor();
            this.lblValues = new System.Windows.Forms.Label();
            this.tableLayoutPanel2.SuspendLayout();
            this.valueButtonPanel.SuspendLayout();
            this.valueButtons.SuspendLayout();
            this.rowHeaderButtonPanel.SuspendLayout();
            this.columnHeaderButtonPanel.SuspendLayout();
            this.rowHeadersPanel.SuspendLayout();
            this.panel3.SuspendLayout();
            this.panelValues.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnOK
            // 
            resources.ApplyResources(this.btnOK, "btnOK");
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.Name = "btnOK";
            this.btnOK.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // tableLayoutPanel2
            // 
            resources.ApplyResources(this.tableLayoutPanel2, "tableLayoutPanel2");
            this.tableLayoutPanel2.Controls.Add(this.valueButtonPanel, 1, 2);
            this.tableLayoutPanel2.Controls.Add(this.availableColumnList, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this.rowHeaderButtonPanel, 1, 0);
            this.tableLayoutPanel2.Controls.Add(this.columnHeaderButtonPanel, 1, 1);
            this.tableLayoutPanel2.Controls.Add(this.rowHeadersPanel, 2, 0);
            this.tableLayoutPanel2.Controls.Add(this.panel3, 2, 1);
            this.tableLayoutPanel2.Controls.Add(this.panelValues, 2, 2);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            // 
            // valueButtonPanel
            // 
            this.valueButtonPanel.Controls.Add(this.valueButtons);
            resources.ApplyResources(this.valueButtonPanel, "valueButtonPanel");
            this.valueButtonPanel.Name = "valueButtonPanel";
            this.valueButtonPanel.Resize += new System.EventHandler(this.panelValueButtonsOuter_Resize);
            // 
            // valueButtons
            // 
            this.valueButtons.Controls.Add(this.btnAddValue);
            this.valueButtons.Controls.Add(this.comboAggregateOp);
            resources.ApplyResources(this.valueButtons, "valueButtons");
            this.valueButtons.Name = "valueButtons";
            // 
            // btnAddValue
            // 
            resources.ApplyResources(this.btnAddValue, "btnAddValue");
            this.btnAddValue.Name = "btnAddValue";
            this.btnAddValue.UseVisualStyleBackColor = true;
            this.btnAddValue.Click += new System.EventHandler(this.btnAddValue_Click);
            // 
            // comboAggregateOp
            // 
            resources.ApplyResources(this.comboAggregateOp, "comboAggregateOp");
            this.comboAggregateOp.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboAggregateOp.FormattingEnabled = true;
            this.comboAggregateOp.Name = "comboAggregateOp";
            // 
            // availableColumnList
            // 
            resources.ApplyResources(this.availableColumnList, "availableColumnList");
            this.availableColumnList.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.availableColumnList.HideSelection = false;
            this.availableColumnList.Name = "availableColumnList";
            this.tableLayoutPanel2.SetRowSpan(this.availableColumnList, 3);
            this.availableColumnList.ShowItemToolTips = true;
            this.availableColumnList.UseCompatibleStateImageBehavior = false;
            this.availableColumnList.View = System.Windows.Forms.View.Details;
            this.availableColumnList.SelectedIndexChanged += new System.EventHandler(this.availableColumnList_SelectedIndexChanged);
            // 
            // rowHeaderButtonPanel
            // 
            this.rowHeaderButtonPanel.Controls.Add(this.btnAddRowHeader);
            resources.ApplyResources(this.rowHeaderButtonPanel, "rowHeaderButtonPanel");
            this.rowHeaderButtonPanel.Name = "rowHeaderButtonPanel";
            this.rowHeaderButtonPanel.Resize += new System.EventHandler(this.rowHeaderButtonPanel_Resize);
            // 
            // btnAddRowHeader
            // 
            resources.ApplyResources(this.btnAddRowHeader, "btnAddRowHeader");
            this.btnAddRowHeader.Name = "btnAddRowHeader";
            this.btnAddRowHeader.UseVisualStyleBackColor = true;
            this.btnAddRowHeader.Click += new System.EventHandler(this.btnAddRowHeader_Click);
            // 
            // columnHeaderButtonPanel
            // 
            this.columnHeaderButtonPanel.Controls.Add(this.btnAddColumnHeader);
            resources.ApplyResources(this.columnHeaderButtonPanel, "columnHeaderButtonPanel");
            this.columnHeaderButtonPanel.Name = "columnHeaderButtonPanel";
            this.columnHeaderButtonPanel.Resize += new System.EventHandler(this.columnHeaderButtonPanel_Resize);
            // 
            // btnAddColumnHeader
            // 
            resources.ApplyResources(this.btnAddColumnHeader, "btnAddColumnHeader");
            this.btnAddColumnHeader.Name = "btnAddColumnHeader";
            this.btnAddColumnHeader.UseVisualStyleBackColor = true;
            this.btnAddColumnHeader.Click += new System.EventHandler(this.btnAddColumnHeader_Click);
            // 
            // rowHeadersPanel
            // 
            this.rowHeadersPanel.Controls.Add(this.rowHeadersList);
            this.rowHeadersPanel.Controls.Add(this.lblRowHeaders);
            resources.ApplyResources(this.rowHeadersPanel, "rowHeadersPanel");
            this.rowHeadersPanel.Name = "rowHeadersPanel";
            // 
            // rowHeadersList
            // 
            resources.ApplyResources(this.rowHeadersList, "rowHeadersList");
            this.rowHeadersList.Name = "rowHeadersList";
            // 
            // lblRowHeaders
            // 
            resources.ApplyResources(this.lblRowHeaders, "lblRowHeaders");
            this.lblRowHeaders.Name = "lblRowHeaders";
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.columnHeadersList);
            this.panel3.Controls.Add(this.lblColumnHeaders);
            resources.ApplyResources(this.panel3, "panel3");
            this.panel3.Name = "panel3";
            // 
            // columnHeadersList
            // 
            resources.ApplyResources(this.columnHeadersList, "columnHeadersList");
            this.columnHeadersList.Name = "columnHeadersList";
            // 
            // lblColumnHeaders
            // 
            resources.ApplyResources(this.lblColumnHeaders, "lblColumnHeaders");
            this.lblColumnHeaders.Name = "lblColumnHeaders";
            // 
            // panelValues
            // 
            this.panelValues.Controls.Add(this.valuesList);
            this.panelValues.Controls.Add(this.lblValues);
            resources.ApplyResources(this.panelValues, "panelValues");
            this.panelValues.Name = "panelValues";
            // 
            // valuesList
            // 
            resources.ApplyResources(this.valuesList, "valuesList");
            this.valuesList.Name = "valuesList";
            // 
            // lblValues
            // 
            resources.ApplyResources(this.lblValues, "lblValues");
            this.lblValues.Name = "lblValues";
            // 
            // PivotEditor
            // 
            this.AcceptButton = this.btnOK;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.tableLayoutPanel2);
            this.MinimizeBox = false;
            this.Name = "PivotEditor";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.tableLayoutPanel2.ResumeLayout(false);
            this.valueButtonPanel.ResumeLayout(false);
            this.valueButtons.ResumeLayout(false);
            this.rowHeaderButtonPanel.ResumeLayout(false);
            this.columnHeaderButtonPanel.ResumeLayout(false);
            this.rowHeadersPanel.ResumeLayout(false);
            this.rowHeadersPanel.PerformLayout();
            this.panel3.ResumeLayout(false);
            this.panel3.PerformLayout();
            this.panelValues.ResumeLayout(false);
            this.panelValues.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.Button btnAddRowHeader;
        private System.Windows.Forms.Button btnAddColumnHeader;
        private System.Windows.Forms.Panel valueButtonPanel;
        private System.Windows.Forms.ComboBox comboAggregateOp;
        private System.Windows.Forms.Button btnAddValue;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private ColumnListEditor rowHeadersList;
        private ColumnListEditor columnHeadersList;
        private ColumnListEditor valuesList;
        private ColumnListView availableColumnList;
        private System.Windows.Forms.Panel rowHeaderButtonPanel;
        private System.Windows.Forms.Panel columnHeaderButtonPanel;
        private System.Windows.Forms.Panel rowHeadersPanel;
        private System.Windows.Forms.Label lblRowHeaders;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.Label lblColumnHeaders;
        private System.Windows.Forms.Panel panelValues;
        private System.Windows.Forms.Label lblValues;
        private System.Windows.Forms.Panel valueButtons;
    }
}