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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ArrangeGraphsGroupedDlg));
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
            this.comboBoxDisplay = new System.Windows.Forms.ComboBox();
            this.labelDisplay = new System.Windows.Forms.Label();
            this.SuspendLayout();
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
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // textGroups
            // 
            resources.ApplyResources(this.textGroups, "textGroups");
            this.textGroups.Name = "textGroups";
            this.toolTipControls.SetToolTip(this.textGroups, resources.GetString("textGroups.ToolTip"));
            // 
            // radioDistribute
            // 
            resources.ApplyResources(this.radioDistribute, "radioDistribute");
            this.radioDistribute.Name = "radioDistribute";
            this.toolTipControls.SetToolTip(this.radioDistribute, resources.GetString("radioDistribute.ToolTip"));
            this.radioDistribute.UseVisualStyleBackColor = true;
            // 
            // radioSeparate
            // 
            resources.ApplyResources(this.radioSeparate, "radioSeparate");
            this.radioSeparate.Checked = true;
            this.radioSeparate.Name = "radioSeparate";
            this.radioSeparate.TabStop = true;
            this.toolTipControls.SetToolTip(this.radioSeparate, resources.GetString("radioSeparate.ToolTip"));
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
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // comboSortOrder
            // 
            this.comboSortOrder.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboSortOrder.FormattingEnabled = true;
            this.comboSortOrder.Items.AddRange(new object[] {
            resources.GetString("comboSortOrder.Items"),
            resources.GetString("comboSortOrder.Items1"),
            resources.GetString("comboSortOrder.Items2")});
            resources.ApplyResources(this.comboSortOrder, "comboSortOrder");
            this.comboSortOrder.Name = "comboSortOrder";
            // 
            // cbReversed
            // 
            resources.ApplyResources(this.cbReversed, "cbReversed");
            this.cbReversed.Name = "cbReversed";
            this.cbReversed.UseVisualStyleBackColor = true;
            // 
            // comboBoxDisplay
            // 
            this.comboBoxDisplay.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxDisplay.FormattingEnabled = true;
            this.comboBoxDisplay.Items.AddRange(new object[] {
            resources.GetString("comboBoxDisplay.Items"),
            resources.GetString("comboBoxDisplay.Items1"),
            resources.GetString("comboBoxDisplay.Items2")});
            resources.ApplyResources(this.comboBoxDisplay, "comboBoxDisplay");
            this.comboBoxDisplay.Name = "comboBoxDisplay";
            // 
            // labelDisplay
            // 
            resources.ApplyResources(this.labelDisplay, "labelDisplay");
            this.labelDisplay.Name = "labelDisplay";
            // 
            // ArrangeGraphsGroupedDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.comboBoxDisplay);
            this.Controls.Add(this.labelDisplay);
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
        private System.Windows.Forms.ComboBox comboBoxDisplay;
        private System.Windows.Forms.Label labelDisplay;
    }
}