namespace SkylineBatch
{
    partial class ReportsAddForm
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
            this.textReportName = new System.Windows.Forms.TextBox();
            this.labelConfigName = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.textReportPath = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.boxRScripts = new System.Windows.Forms.ListBox();
            this.btnSaveConfig = new System.Windows.Forms.Button();
            this.btnAddRScript = new System.Windows.Forms.Button();
            this.btnRemove = new System.Windows.Forms.Button();
            this.btnReportPath = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // textReportName
            // 
            this.textReportName.Location = new System.Drawing.Point(23, 41);
            this.textReportName.Name = "textReportName";
            this.textReportName.Size = new System.Drawing.Size(430, 20);
            this.textReportName.TabIndex = 3;
            // 
            // labelConfigName
            // 
            this.labelConfigName.AutoSize = true;
            this.labelConfigName.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelConfigName.Location = new System.Drawing.Point(22, 25);
            this.labelConfigName.Name = "labelConfigName";
            this.labelConfigName.Size = new System.Drawing.Size(79, 13);
            this.labelConfigName.TabIndex = 2;
            this.labelConfigName.Text = "Report &name";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(22, 75);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(74, 15);
            this.label1.TabIndex = 8;
            this.label1.Text = "Report &path:";
            // 
            // textReportPath
            // 
            this.textReportPath.Location = new System.Drawing.Point(23, 94);
            this.textReportPath.Name = "textReportPath";
            this.textReportPath.Size = new System.Drawing.Size(430, 20);
            this.textReportPath.TabIndex = 9;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(22, 135);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(57, 15);
            this.label2.TabIndex = 11;
            this.label2.Text = "R &scripts:";
            // 
            // boxRScripts
            // 
            this.boxRScripts.FormattingEnabled = true;
            this.boxRScripts.Location = new System.Drawing.Point(23, 153);
            this.boxRScripts.Name = "boxRScripts";
            this.boxRScripts.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.boxRScripts.Size = new System.Drawing.Size(430, 95);
            this.boxRScripts.TabIndex = 12;
            this.boxRScripts.SelectedIndexChanged += new System.EventHandler(this.boxRScripts_SelectedIndexChanged);
            // 
            // btnSaveConfig
            // 
            this.btnSaveConfig.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.btnSaveConfig.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSaveConfig.Location = new System.Drawing.Point(227, 284);
            this.btnSaveConfig.Name = "btnSaveConfig";
            this.btnSaveConfig.Size = new System.Drawing.Size(79, 26);
            this.btnSaveConfig.TabIndex = 17;
            this.btnSaveConfig.Text = "&Ok";
            this.btnSaveConfig.UseVisualStyleBackColor = true;
            this.btnSaveConfig.Click += new System.EventHandler(this.btnSaveConfig_Click);
            // 
            // btnAddRScript
            // 
            this.btnAddRScript.Location = new System.Drawing.Point(472, 153);
            this.btnAddRScript.Name = "btnAddRScript";
            this.btnAddRScript.Size = new System.Drawing.Size(74, 23);
            this.btnAddRScript.TabIndex = 19;
            this.btnAddRScript.Text = "&Add";
            this.btnAddRScript.UseVisualStyleBackColor = true;
            this.btnAddRScript.Click += new System.EventHandler(this.btnAddRScript_Click);
            // 
            // btnRemove
            // 
            this.btnRemove.Enabled = false;
            this.btnRemove.Location = new System.Drawing.Point(472, 182);
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Size = new System.Drawing.Size(74, 23);
            this.btnRemove.TabIndex = 20;
            this.btnRemove.Text = "&Remove";
            this.btnRemove.UseVisualStyleBackColor = true;
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            // 
            // btnReportPath
            // 
            this.btnReportPath.Location = new System.Drawing.Point(472, 94);
            this.btnReportPath.Name = "btnReportPath";
            this.btnReportPath.Size = new System.Drawing.Size(74, 23);
            this.btnReportPath.TabIndex = 21;
            this.btnReportPath.Text = "&Browse";
            this.btnReportPath.UseVisualStyleBackColor = true;
            this.btnReportPath.Click += new System.EventHandler(this.btnReportPath_Click);
            // 
            // ReportsAddForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(558, 339);
            this.Controls.Add(this.btnReportPath);
            this.Controls.Add(this.btnRemove);
            this.Controls.Add(this.btnAddRScript);
            this.Controls.Add(this.btnSaveConfig);
            this.Controls.Add(this.boxRScripts);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.textReportPath);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textReportName);
            this.Controls.Add(this.labelConfigName);
            this.Name = "ReportsAddForm";
            this.Text = "Add Report";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textReportName;
        private System.Windows.Forms.Label labelConfigName;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textReportPath;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ListBox boxRScripts;
        private System.Windows.Forms.Button btnSaveConfig;
        private System.Windows.Forms.Button btnAddRScript;
        private System.Windows.Forms.Button btnRemove;
        private System.Windows.Forms.Button btnReportPath;
    }
}