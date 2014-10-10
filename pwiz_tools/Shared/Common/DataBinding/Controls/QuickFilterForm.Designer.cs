namespace pwiz.Common.DataBinding.Controls
{
    partial class QuickFilterForm
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
            this.label1 = new System.Windows.Forms.Label();
            this.comboOperation1 = new System.Windows.Forms.ComboBox();
            this.tbxOperand1 = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.comboOperation2 = new System.Windows.Forms.ComboBox();
            this.tbxOperand2 = new System.Windows.Forms.TextBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnClearFilter = new System.Windows.Forms.Button();
            this.btnClearAllFilters = new System.Windows.Forms.Button();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.flowLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(59, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Filter Type:";
            // 
            // comboOperation1
            // 
            this.comboOperation1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.comboOperation1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboOperation1.FormattingEnabled = true;
            this.comboOperation1.Location = new System.Drawing.Point(113, 6);
            this.comboOperation1.Name = "comboOperation1";
            this.comboOperation1.Size = new System.Drawing.Size(440, 21);
            this.comboOperation1.TabIndex = 1;
            this.comboOperation1.SelectedIndexChanged += new System.EventHandler(this.comboOperation1_SelectedIndexChanged);
            // 
            // tbxOperand1
            // 
            this.tbxOperand1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbxOperand1.Location = new System.Drawing.Point(113, 33);
            this.tbxOperand1.Name = "tbxOperand1";
            this.tbxOperand1.Size = new System.Drawing.Size(440, 20);
            this.tbxOperand1.TabIndex = 2;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 61);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(28, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "and:";
            // 
            // comboOperation2
            // 
            this.comboOperation2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.comboOperation2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboOperation2.FormattingEnabled = true;
            this.comboOperation2.Location = new System.Drawing.Point(113, 59);
            this.comboOperation2.Name = "comboOperation2";
            this.comboOperation2.Size = new System.Drawing.Size(440, 21);
            this.comboOperation2.TabIndex = 4;
            this.comboOperation2.SelectedIndexChanged += new System.EventHandler(this.comboOperation2_SelectedIndexChanged);
            // 
            // tbxOperand2
            // 
            this.tbxOperand2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbxOperand2.Location = new System.Drawing.Point(113, 86);
            this.tbxOperand2.Name = "tbxOperand2";
            this.tbxOperand2.Size = new System.Drawing.Size(440, 20);
            this.tbxOperand2.TabIndex = 5;
            // 
            // btnOK
            // 
            this.btnOK.AutoSize = true;
            this.btnOK.Location = new System.Drawing.Point(234, 3);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 6;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.AutoSize = true;
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(315, 3);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 7;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnClearFilter
            // 
            this.btnClearFilter.AutoSize = true;
            this.btnClearFilter.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnClearFilter.Location = new System.Drawing.Point(396, 3);
            this.btnClearFilter.Name = "btnClearFilter";
            this.btnClearFilter.Size = new System.Drawing.Size(75, 23);
            this.btnClearFilter.TabIndex = 8;
            this.btnClearFilter.Text = "Clear Filter";
            this.btnClearFilter.UseVisualStyleBackColor = true;
            this.btnClearFilter.Click += new System.EventHandler(this.btnClearFilter_Click);
            // 
            // btnClearAllFilters
            // 
            this.btnClearAllFilters.AutoSize = true;
            this.btnClearAllFilters.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnClearAllFilters.Location = new System.Drawing.Point(477, 3);
            this.btnClearAllFilters.Name = "btnClearAllFilters";
            this.btnClearAllFilters.Size = new System.Drawing.Size(85, 23);
            this.btnClearAllFilters.TabIndex = 9;
            this.btnClearAllFilters.Text = "Clear All Filters";
            this.btnClearAllFilters.UseVisualStyleBackColor = true;
            this.btnClearAllFilters.Click += new System.EventHandler(this.btnClearAllFilters_Click);
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Controls.Add(this.btnClearAllFilters);
            this.flowLayoutPanel1.Controls.Add(this.btnClearFilter);
            this.flowLayoutPanel1.Controls.Add(this.btnCancel);
            this.flowLayoutPanel1.Controls.Add(this.btnOK);
            this.flowLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.flowLayoutPanel1.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.flowLayoutPanel1.Location = new System.Drawing.Point(0, 112);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(565, 32);
            this.flowLayoutPanel1.TabIndex = 10;
            // 
            // QuickFilterForm
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(565, 144);
            this.Controls.Add(this.flowLayoutPanel1);
            this.Controls.Add(this.tbxOperand2);
            this.Controls.Add(this.comboOperation2);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.tbxOperand1);
            this.Controls.Add(this.comboOperation1);
            this.Controls.Add(this.label1);
            this.Name = "QuickFilterForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "QuickFilterForm";
            this.flowLayoutPanel1.ResumeLayout(false);
            this.flowLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox comboOperation1;
        private System.Windows.Forms.TextBox tbxOperand1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox comboOperation2;
        private System.Windows.Forms.TextBox tbxOperand2;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnClearFilter;
        private System.Windows.Forms.Button btnClearAllFilters;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
    }
}