namespace pwiz.Skyline.Controls.GroupComparison
{
    partial class VolcanoPlotPropertiesDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VolcanoPlotPropertiesDlg));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.textFoldChange = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.textPValue = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.checkBoxLog = new System.Windows.Forms.CheckBox();
            this.foldChangeUnitLabel = new System.Windows.Forms.Label();
            this.pValueLowerBoundLabel = new System.Windows.Forms.Label();
            this.checkBoxFilter = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).BeginInit();
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
            // textFoldChange
            // 
            resources.ApplyResources(this.textFoldChange, "textFoldChange");
            this.textFoldChange.Name = "textFoldChange";
            this.textFoldChange.Leave += new System.EventHandler(this.Preview);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // textPValue
            // 
            resources.ApplyResources(this.textPValue, "textPValue");
            this.textPValue.Name = "textPValue";
            this.textPValue.Leave += new System.EventHandler(this.Preview);
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // checkBoxLog
            // 
            resources.ApplyResources(this.checkBoxLog, "checkBoxLog");
            this.checkBoxLog.Checked = true;
            this.checkBoxLog.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxLog.Name = "checkBoxLog";
            this.checkBoxLog.UseVisualStyleBackColor = true;
            this.checkBoxLog.CheckedChanged += new System.EventHandler(this.checkBoxLog_CheckedChanged);
            // 
            // foldChangeUnitLabel
            // 
            resources.ApplyResources(this.foldChangeUnitLabel, "foldChangeUnitLabel");
            this.foldChangeUnitLabel.Name = "foldChangeUnitLabel";
            // 
            // pValueLowerBoundLabel
            // 
            resources.ApplyResources(this.pValueLowerBoundLabel, "pValueLowerBoundLabel");
            this.pValueLowerBoundLabel.Name = "pValueLowerBoundLabel";
            // 
            // checkBoxFilter
            // 
            resources.ApplyResources(this.checkBoxFilter, "checkBoxFilter");
            this.checkBoxFilter.Name = "checkBoxFilter";
            this.checkBoxFilter.UseVisualStyleBackColor = true;
            this.checkBoxFilter.CheckedChanged += new System.EventHandler(this.Preview);
            // 
            // VolcanoPlotPropertiesDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.checkBoxFilter);
            this.Controls.Add(this.pValueLowerBoundLabel);
            this.Controls.Add(this.foldChangeUnitLabel);
            this.Controls.Add(this.checkBoxLog);
            this.Controls.Add(this.textFoldChange);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.textPValue);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "VolcanoPlotPropertiesDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.VolcanoPlotProperties_FormClosing);
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.TextBox textFoldChange;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textPValue;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.CheckBox checkBoxLog;
        private System.Windows.Forms.Label foldChangeUnitLabel;
        private System.Windows.Forms.Label pValueLowerBoundLabel;
        private System.Windows.Forms.CheckBox checkBoxFilter;
    }
}