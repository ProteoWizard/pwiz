namespace pwiz.Skyline.SettingsUI
{
    partial class DefineAnnotationDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DefineAnnotationDlg));
            this.label1 = new System.Windows.Forms.Label();
            this.tbxName = new System.Windows.Forms.TextBox();
            this.comboType = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.tbxValues = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.checkedListBoxAppliesTo = new System.Windows.Forms.CheckedListBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.imageList1 = new System.Windows.Forms.ImageList(this.components);
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPageEditable = new System.Windows.Forms.TabPage();
            this.tabPageCalculated = new System.Windows.Forms.TabPage();
            this.comboAggregateOperation = new System.Windows.Forms.ComboBox();
            this.lblAggregateOperation = new System.Windows.Forms.Label();
            this.availableFieldsTree1 = new pwiz.Common.DataBinding.Controls.Editor.AvailableFieldsTree();
            this.label5 = new System.Windows.Forms.Label();
            this.comboAppliesTo = new System.Windows.Forms.ComboBox();
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).BeginInit();
            this.tabControl1.SuspendLayout();
            this.tabPageEditable.SuspendLayout();
            this.tabPageCalculated.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // tbxName
            // 
            resources.ApplyResources(this.tbxName, "tbxName");
            this.tbxName.Name = "tbxName";
            // 
            // comboType
            // 
            this.comboType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboType.FormattingEnabled = true;
            resources.ApplyResources(this.comboType, "comboType");
            this.comboType.Name = "comboType";
            this.comboType.SelectedIndexChanged += new System.EventHandler(this.comboType_SelectedIndexChanged);
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // tbxValues
            // 
            this.tbxValues.AcceptsReturn = true;
            resources.ApplyResources(this.tbxValues, "tbxValues");
            this.tbxValues.Name = "tbxValues";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // checkedListBoxAppliesTo
            // 
            resources.ApplyResources(this.checkedListBoxAppliesTo, "checkedListBoxAppliesTo");
            this.checkedListBoxAppliesTo.CheckOnClick = true;
            this.checkedListBoxAppliesTo.FormattingEnabled = true;
            this.checkedListBoxAppliesTo.Name = "checkedListBoxAppliesTo";
            // 
            // btnOK
            // 
            resources.ApplyResources(this.btnOK, "btnOK");
            this.btnOK.Name = "btnOK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // imageList1
            // 
            this.imageList1.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList1.ImageStream")));
            this.imageList1.TransparentColor = System.Drawing.Color.Magenta;
            this.imageList1.Images.SetKeyName(0, "Delete.bmp");
            // 
            // tabControl1
            // 
            resources.ApplyResources(this.tabControl1, "tabControl1");
            this.tabControl1.Controls.Add(this.tabPageEditable);
            this.tabControl1.Controls.Add(this.tabPageCalculated);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            // 
            // tabPageEditable
            // 
            this.tabPageEditable.Controls.Add(this.label2);
            this.tabPageEditable.Controls.Add(this.comboType);
            this.tabPageEditable.Controls.Add(this.label4);
            this.tabPageEditable.Controls.Add(this.tbxValues);
            this.tabPageEditable.Controls.Add(this.label3);
            this.tabPageEditable.Controls.Add(this.checkedListBoxAppliesTo);
            resources.ApplyResources(this.tabPageEditable, "tabPageEditable");
            this.tabPageEditable.Name = "tabPageEditable";
            this.tabPageEditable.UseVisualStyleBackColor = true;
            // 
            // tabPageCalculated
            // 
            this.tabPageCalculated.Controls.Add(this.comboAggregateOperation);
            this.tabPageCalculated.Controls.Add(this.lblAggregateOperation);
            this.tabPageCalculated.Controls.Add(this.availableFieldsTree1);
            this.tabPageCalculated.Controls.Add(this.label5);
            this.tabPageCalculated.Controls.Add(this.comboAppliesTo);
            resources.ApplyResources(this.tabPageCalculated, "tabPageCalculated");
            this.tabPageCalculated.Name = "tabPageCalculated";
            this.tabPageCalculated.UseVisualStyleBackColor = true;
            // 
            // comboAggregateOperation
            // 
            resources.ApplyResources(this.comboAggregateOperation, "comboAggregateOperation");
            this.comboAggregateOperation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboAggregateOperation.FormattingEnabled = true;
            this.comboAggregateOperation.Name = "comboAggregateOperation";
            // 
            // lblAggregateOperation
            // 
            resources.ApplyResources(this.lblAggregateOperation, "lblAggregateOperation");
            this.lblAggregateOperation.Name = "lblAggregateOperation";
            // 
            // availableFieldsTree1
            // 
            resources.ApplyResources(this.availableFieldsTree1, "availableFieldsTree1");
            this.availableFieldsTree1.CheckedColumns = new pwiz.Common.DataBinding.PropertyPath[0];
            this.availableFieldsTree1.DrawMode = System.Windows.Forms.TreeViewDrawMode.OwnerDrawText;
            this.availableFieldsTree1.Name = "availableFieldsTree1";
            this.availableFieldsTree1.ShowNodeToolTips = true;
            this.availableFieldsTree1.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.availableFieldsTree1_AfterSelect);
            // 
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            // 
            // comboAppliesTo
            // 
            this.comboAppliesTo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboAppliesTo.FormattingEnabled = true;
            resources.ApplyResources(this.comboAppliesTo, "comboAppliesTo");
            this.comboAppliesTo.Name = "comboAppliesTo";
            this.comboAppliesTo.SelectedIndexChanged += new System.EventHandler(this.comboAppliesTo_SelectedIndexChanged);
            // 
            // DefineAnnotationDlg
            // 
            this.AcceptButton = this.btnOK;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.tbxName);
            this.Controls.Add(this.label1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DefineAnnotationDlg";
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).EndInit();
            this.tabControl1.ResumeLayout(false);
            this.tabPageEditable.ResumeLayout(false);
            this.tabPageEditable.PerformLayout();
            this.tabPageCalculated.ResumeLayout(false);
            this.tabPageCalculated.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbxName;
        private System.Windows.Forms.ComboBox comboType;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox tbxValues;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.CheckedListBox checkedListBoxAppliesTo;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ImageList imageList1;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPageEditable;
        private System.Windows.Forms.TabPage tabPageCalculated;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.ComboBox comboAppliesTo;
        private Common.DataBinding.Controls.Editor.AvailableFieldsTree availableFieldsTree1;
        private System.Windows.Forms.ComboBox comboAggregateOperation;
        private System.Windows.Forms.Label lblAggregateOperation;
    }
}
