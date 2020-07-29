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
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).BeginInit();
            this.panelButtons.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.bindingListSource1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.boundDataGridView1)).BeginInit();
            this.SuspendLayout();
            // 
            // panelButtons
            // 
            this.panelButtons.Controls.Add(this.btnCancel);
            this.panelButtons.Controls.Add(this.btnOK);
            this.panelButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelButtons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.panelButtons.Location = new System.Drawing.Point(0, 421);
            this.panelButtons.Name = "panelButtons";
            this.panelButtons.Size = new System.Drawing.Size(583, 29);
            this.panelButtons.TabIndex = 8;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnCancel.Location = new System.Drawing.Point(505, 3);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnOK.Location = new System.Drawing.Point(424, 3);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 0;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // tbxRegularExpression
            // 
            this.tbxRegularExpression.Location = new System.Drawing.Point(12, 66);
            this.tbxRegularExpression.Name = "tbxRegularExpression";
            this.tbxRegularExpression.Size = new System.Drawing.Size(268, 20);
            this.tbxRegularExpression.TabIndex = 3;
            this.tbxRegularExpression.Leave += new System.EventHandler(this.tbxRegularExpression_Leave);
            // 
            // lblTarget
            // 
            this.lblTarget.AutoSize = true;
            this.lblTarget.Location = new System.Drawing.Point(12, 128);
            this.lblTarget.Name = "lblTarget";
            this.lblTarget.Size = new System.Drawing.Size(41, 13);
            this.lblTarget.TabIndex = 4;
            this.lblTarget.Text = "Target:";
            // 
            // comboMetadataTarget
            // 
            this.comboMetadataTarget.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboMetadataTarget.FormattingEnabled = true;
            this.comboMetadataTarget.Location = new System.Drawing.Point(12, 144);
            this.comboMetadataTarget.Name = "comboMetadataTarget";
            this.comboMetadataTarget.Size = new System.Drawing.Size(268, 21);
            this.comboMetadataTarget.TabIndex = 5;
            this.comboMetadataTarget.SelectedIndexChanged += new System.EventHandler(this.comboMetadataTarget_SelectedIndexChanged);
            // 
            // lblSourceText
            // 
            this.lblSourceText.AutoSize = true;
            this.lblSourceText.Location = new System.Drawing.Point(12, 10);
            this.lblSourceText.Name = "lblSourceText";
            this.lblSourceText.Size = new System.Drawing.Size(73, 13);
            this.lblSourceText.TabIndex = 0;
            this.lblSourceText.Text = "Source value:";
            // 
            // comboSourceText
            // 
            this.comboSourceText.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboSourceText.FormattingEnabled = true;
            this.comboSourceText.Location = new System.Drawing.Point(12, 26);
            this.comboSourceText.Name = "comboSourceText";
            this.comboSourceText.Size = new System.Drawing.Size(268, 21);
            this.comboSourceText.TabIndex = 1;
            this.comboSourceText.SelectedIndexChanged += new System.EventHandler(this.comboSourceText_SelectedIndexChanged);
            // 
            // lblPreview
            // 
            this.lblPreview.AutoSize = true;
            this.lblPreview.Location = new System.Drawing.Point(12, 168);
            this.lblPreview.Name = "lblPreview";
            this.lblPreview.Size = new System.Drawing.Size(48, 13);
            this.lblPreview.TabIndex = 6;
            this.lblPreview.Text = "Preview:";
            // 
            // linkLabelRegularExpression
            // 
            this.linkLabelRegularExpression.AutoSize = true;
            this.linkLabelRegularExpression.Location = new System.Drawing.Point(12, 50);
            this.linkLabelRegularExpression.Name = "linkLabelRegularExpression";
            this.linkLabelRegularExpression.Size = new System.Drawing.Size(100, 13);
            this.linkLabelRegularExpression.TabIndex = 2;
            this.linkLabelRegularExpression.TabStop = true;
            this.linkLabelRegularExpression.Text = "Regular expression:";
            this.linkLabelRegularExpression.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabelRegularExpression_LinkClicked);
            // 
            // bindingListSource1
            // 
            this.bindingListSource1.NewRowHandler = null;
            // 
            // boundDataGridView1
            // 
            this.boundDataGridView1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.boundDataGridView1.AutoGenerateColumns = false;
            this.boundDataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.boundDataGridView1.DataSource = this.bindingListSource1;
            this.boundDataGridView1.Location = new System.Drawing.Point(12, 184);
            this.boundDataGridView1.MaximumColumnCount = 2000;
            this.boundDataGridView1.Name = "boundDataGridView1";
            this.boundDataGridView1.Size = new System.Drawing.Size(551, 221);
            this.boundDataGridView1.TabIndex = 7;
            // 
            // lblReplacement
            // 
            this.lblReplacement.AutoSize = true;
            this.lblReplacement.Location = new System.Drawing.Point(12, 89);
            this.lblReplacement.Name = "lblReplacement";
            this.lblReplacement.Size = new System.Drawing.Size(73, 13);
            this.lblReplacement.TabIndex = 9;
            this.lblReplacement.Text = "Replacement:";
            // 
            // tbxReplacement
            // 
            this.tbxReplacement.Location = new System.Drawing.Point(12, 105);
            this.tbxReplacement.Name = "tbxReplacement";
            this.tbxReplacement.Size = new System.Drawing.Size(268, 20);
            this.tbxReplacement.TabIndex = 10;
            // 
            // MetadataRuleEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(583, 450);
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
            this.Name = "MetadataRuleEditor";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Result File Metadata Rule";
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).EndInit();
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
    }
}