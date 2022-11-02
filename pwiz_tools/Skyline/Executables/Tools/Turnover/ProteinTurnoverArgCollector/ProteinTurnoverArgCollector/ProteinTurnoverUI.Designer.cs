namespace ProteinTurnoverArgCollector
{
    partial class ProteinTurnoverUI
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ProteinTurnoverUI));
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.textDietEnrichment = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.percentLabel = new System.Windows.Forms.Label();
            this.textAverageTurnover = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.textIDP = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.textFolderName = new System.Windows.Forms.TextBox();
            this.textQValue = new System.Windows.Forms.TextBox();
            this.labelQValue = new System.Windows.Forms.Label();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.label5 = new System.Windows.Forms.Label();
            this.labelQValueWarning = new System.Windows.Forms.Label();
            this.comboReference = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // textDietEnrichment
            // 
            resources.ApplyResources(this.textDietEnrichment, "textDietEnrichment");
            this.textDietEnrichment.Name = "textDietEnrichment";
            this.toolTip1.SetToolTip(this.textDietEnrichment, resources.GetString("textDietEnrichment.ToolTip"));
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // percentLabel
            // 
            resources.ApplyResources(this.percentLabel, "percentLabel");
            this.percentLabel.Name = "percentLabel";
            // 
            // textAverageTurnover
            // 
            resources.ApplyResources(this.textAverageTurnover, "textAverageTurnover");
            this.textAverageTurnover.Name = "textAverageTurnover";
            this.toolTip1.SetToolTip(this.textAverageTurnover, resources.GetString("textAverageTurnover.ToolTip"));
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // textIDP
            // 
            resources.ApplyResources(this.textIDP, "textIDP");
            this.textIDP.Name = "textIDP";
            this.toolTip1.SetToolTip(this.textIDP, resources.GetString("textIDP.ToolTip"));
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // textFolderName
            // 
            resources.ApplyResources(this.textFolderName, "textFolderName");
            this.textFolderName.Name = "textFolderName";
            this.toolTip1.SetToolTip(this.textFolderName, resources.GetString("textFolderName.ToolTip"));
            // 
            // textQValue
            // 
            resources.ApplyResources(this.textQValue, "textQValue");
            this.textQValue.Name = "textQValue";
            this.toolTip1.SetToolTip(this.textQValue, resources.GetString("textQValue.ToolTip"));
            // 
            // labelQValue
            // 
            resources.ApplyResources(this.labelQValue, "labelQValue");
            this.labelQValue.Name = "labelQValue";
            // 
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            // 
            // labelQValueWarning
            // 
            resources.ApplyResources(this.labelQValueWarning, "labelQValueWarning");
            this.labelQValueWarning.ForeColor = System.Drawing.Color.Red;
            this.labelQValueWarning.Name = "labelQValueWarning";
            // 
            // comboReference
            // 
            this.comboReference.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboReference.FormattingEnabled = true;
            resources.ApplyResources(this.comboReference, "comboReference");
            this.comboReference.Name = "comboReference";
            // 
            // ProteinTurnoverUI
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.comboReference);
            this.Controls.Add(this.labelQValueWarning);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.textQValue);
            this.Controls.Add(this.labelQValue);
            this.Controls.Add(this.textFolderName);
            this.Controls.Add(this.textIDP);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textAverageTurnover);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.percentLabel);
            this.Controls.Add(this.textDietEnrichment);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ProteinTurnoverUI";
            this.ShowInTaskbar = false;
            this.Load += new System.EventHandler(this.ProteinTurnoverUI_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textDietEnrichment;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label percentLabel;
        private System.Windows.Forms.TextBox textAverageTurnover;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textIDP;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textFolderName;
        private System.Windows.Forms.TextBox textQValue;
        private System.Windows.Forms.Label labelQValue;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label labelQValueWarning;
        private System.Windows.Forms.ComboBox comboReference;
    }
}

