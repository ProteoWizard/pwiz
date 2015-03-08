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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(QuickFilterForm));
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
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // comboOperation1
            // 
            resources.ApplyResources(this.comboOperation1, "comboOperation1");
            this.comboOperation1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboOperation1.FormattingEnabled = true;
            this.comboOperation1.Name = "comboOperation1";
            this.comboOperation1.SelectedIndexChanged += new System.EventHandler(this.comboOperation1_SelectedIndexChanged);
            // 
            // tbxOperand1
            // 
            resources.ApplyResources(this.tbxOperand1, "tbxOperand1");
            this.tbxOperand1.Name = "tbxOperand1";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // comboOperation2
            // 
            resources.ApplyResources(this.comboOperation2, "comboOperation2");
            this.comboOperation2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboOperation2.FormattingEnabled = true;
            this.comboOperation2.Name = "comboOperation2";
            this.comboOperation2.SelectedIndexChanged += new System.EventHandler(this.comboOperation2_SelectedIndexChanged);
            // 
            // tbxOperand2
            // 
            resources.ApplyResources(this.tbxOperand2, "tbxOperand2");
            this.tbxOperand2.Name = "tbxOperand2";
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
            // btnClearFilter
            // 
            resources.ApplyResources(this.btnClearFilter, "btnClearFilter");
            this.btnClearFilter.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnClearFilter.Name = "btnClearFilter";
            this.btnClearFilter.UseVisualStyleBackColor = true;
            this.btnClearFilter.Click += new System.EventHandler(this.btnClearFilter_Click);
            // 
            // btnClearAllFilters
            // 
            resources.ApplyResources(this.btnClearAllFilters, "btnClearAllFilters");
            this.btnClearAllFilters.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnClearAllFilters.Name = "btnClearAllFilters";
            this.btnClearAllFilters.UseVisualStyleBackColor = true;
            this.btnClearAllFilters.Click += new System.EventHandler(this.btnClearAllFilters_Click);
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Controls.Add(this.btnClearAllFilters);
            this.flowLayoutPanel1.Controls.Add(this.btnClearFilter);
            this.flowLayoutPanel1.Controls.Add(this.btnCancel);
            this.flowLayoutPanel1.Controls.Add(this.btnOK);
            resources.ApplyResources(this.flowLayoutPanel1, "flowLayoutPanel1");
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            // 
            // QuickFilterForm
            // 
            this.AcceptButton = this.btnOK;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.flowLayoutPanel1);
            this.Controls.Add(this.tbxOperand2);
            this.Controls.Add(this.comboOperation2);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.tbxOperand1);
            this.Controls.Add(this.comboOperation1);
            this.Controls.Add(this.label1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "QuickFilterForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
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