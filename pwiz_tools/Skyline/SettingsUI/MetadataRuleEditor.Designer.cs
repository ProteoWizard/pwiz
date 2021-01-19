namespace pwiz.Skyline.SettingsUI
{
    partial class MetadataRuleEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MetadataRuleEditor));
            this.panelButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.tbxRegularExpression = new System.Windows.Forms.TextBox();
            this.lblTarget = new System.Windows.Forms.Label();
            this.comboMetadataTarget = new System.Windows.Forms.ComboBox();
            this.lblSourceText = new System.Windows.Forms.Label();
            this.comboSourceText = new System.Windows.Forms.ComboBox();
            this.lblPreview = new System.Windows.Forms.Label();
            this.linkLabelRegularExpression = new System.Windows.Forms.LinkLabel();
            this.bindingListSource1 = new pwiz.Common.DataBinding.Controls.BindingListSource(this.components);
            this.boundDataGridView1 = new pwiz.Skyline.Controls.Databinding.BoundDataGridViewEx();
            this.lblReplacement = new System.Windows.Forms.Label();
            this.tbxReplacement = new System.Windows.Forms.TextBox();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.panelButtons.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.bindingListSource1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.boundDataGridView1)).BeginInit();
            this.SuspendLayout();
            // 
            // panelButtons
            // 
            this.panelButtons.Controls.Add(this.btnCancel);
            this.panelButtons.Controls.Add(this.btnOK);
            resources.ApplyResources(this.panelButtons, "panelButtons");
            this.panelButtons.Name = "panelButtons";
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            resources.ApplyResources(this.btnOK, "btnOK");
            this.btnOK.Name = "btnOK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // tbxRegularExpression
            // 
            resources.ApplyResources(this.tbxRegularExpression, "tbxRegularExpression");
            this.tbxRegularExpression.Name = "tbxRegularExpression";
            this.toolTip.SetToolTip(this.tbxRegularExpression, resources.GetString("tbxRegularExpression.ToolTip"));
            this.tbxRegularExpression.Leave += new System.EventHandler(this.tbxRegularExpression_Leave);
            // 
            // lblTarget
            // 
            resources.ApplyResources(this.lblTarget, "lblTarget");
            this.lblTarget.Name = "lblTarget";
            // 
            // comboMetadataTarget
            // 
            this.comboMetadataTarget.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboMetadataTarget.FormattingEnabled = true;
            resources.ApplyResources(this.comboMetadataTarget, "comboMetadataTarget");
            this.comboMetadataTarget.Name = "comboMetadataTarget";
            this.toolTip.SetToolTip(this.comboMetadataTarget, resources.GetString("comboMetadataTarget.ToolTip"));
            this.comboMetadataTarget.SelectedIndexChanged += new System.EventHandler(this.comboMetadataTarget_SelectedIndexChanged);
            // 
            // lblSourceText
            // 
            resources.ApplyResources(this.lblSourceText, "lblSourceText");
            this.lblSourceText.Name = "lblSourceText";
            // 
            // comboSourceText
            // 
            this.comboSourceText.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboSourceText.FormattingEnabled = true;
            resources.ApplyResources(this.comboSourceText, "comboSourceText");
            this.comboSourceText.Name = "comboSourceText";
            this.toolTip.SetToolTip(this.comboSourceText, resources.GetString("comboSourceText.ToolTip"));
            this.comboSourceText.SelectedIndexChanged += new System.EventHandler(this.comboSourceText_SelectedIndexChanged);
            // 
            // lblPreview
            // 
            resources.ApplyResources(this.lblPreview, "lblPreview");
            this.lblPreview.Name = "lblPreview";
            // 
            // linkLabelRegularExpression
            // 
            resources.ApplyResources(this.linkLabelRegularExpression, "linkLabelRegularExpression");
            this.linkLabelRegularExpression.Name = "linkLabelRegularExpression";
            this.linkLabelRegularExpression.TabStop = true;
            this.linkLabelRegularExpression.UseCompatibleTextRendering = true;
            this.linkLabelRegularExpression.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabelRegularExpression_LinkClicked);
            // 
            // bindingListSource1
            // 
            this.bindingListSource1.NewRowHandler = null;
            // 
            // boundDataGridView1
            // 
            resources.ApplyResources(this.boundDataGridView1, "boundDataGridView1");
            this.boundDataGridView1.AutoGenerateColumns = false;
            this.boundDataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.boundDataGridView1.DataSource = this.bindingListSource1;
            this.boundDataGridView1.MaximumColumnCount = 2000;
            this.boundDataGridView1.Name = "boundDataGridView1";
            // 
            // lblReplacement
            // 
            resources.ApplyResources(this.lblReplacement, "lblReplacement");
            this.lblReplacement.Name = "lblReplacement";
            // 
            // tbxReplacement
            // 
            resources.ApplyResources(this.tbxReplacement, "tbxReplacement");
            this.tbxReplacement.Name = "tbxReplacement";
            this.toolTip.SetToolTip(this.tbxReplacement, resources.GetString("tbxReplacement.ToolTip"));
            this.tbxReplacement.Leave += new System.EventHandler(this.tbxRegularExpression_Leave);
            // 
            // MetadataRuleEditor
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tbxReplacement);
            this.Controls.Add(this.lblReplacement);
            this.Controls.Add(this.boundDataGridView1);
            this.Controls.Add(this.linkLabelRegularExpression);
            this.Controls.Add(this.lblPreview);
            this.Controls.Add(this.comboSourceText);
            this.Controls.Add(this.lblSourceText);
            this.Controls.Add(this.comboMetadataTarget);
            this.Controls.Add(this.lblTarget);
            this.Controls.Add(this.tbxRegularExpression);
            this.Controls.Add(this.panelButtons);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "MetadataRuleEditor";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.panelButtons.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.bindingListSource1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.boundDataGridView1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.FlowLayoutPanel panelButtons;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.TextBox tbxRegularExpression;
        private System.Windows.Forms.Label lblTarget;
        private System.Windows.Forms.ComboBox comboMetadataTarget;
        private System.Windows.Forms.Label lblSourceText;
        private System.Windows.Forms.ComboBox comboSourceText;
        private System.Windows.Forms.Label lblPreview;
        private System.Windows.Forms.LinkLabel linkLabelRegularExpression;
        private Common.DataBinding.Controls.BindingListSource bindingListSource1;
        private pwiz.Skyline.Controls.Databinding.BoundDataGridViewEx boundDataGridView1;
        private System.Windows.Forms.Label lblReplacement;
        private System.Windows.Forms.TextBox tbxReplacement;
        private System.Windows.Forms.ToolTip toolTip;
    }
}