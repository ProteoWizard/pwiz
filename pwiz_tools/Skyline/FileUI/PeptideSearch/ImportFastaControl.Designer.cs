namespace pwiz.Skyline.FileUI.PeptideSearch
{
    partial class ImportFastaControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ImportFastaControl));
            this.tbxFasta = new System.Windows.Forms.TextBox();
            this.browseFastaBtn = new System.Windows.Forms.Button();
            this.panelError = new System.Windows.Forms.Panel();
            this.tbxError = new System.Windows.Forms.TextBox();
            this.helpTipFasta = new System.Windows.Forms.ToolTip(this.components);
            this.txtNumDecoys = new System.Windows.Forms.TextBox();
            this.clearBtn = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.cbMissedCleavages = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.comboEnzyme = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.cbDecoyMethod = new System.Windows.Forms.ComboBox();
            this.panelDecoys = new System.Windows.Forms.Panel();
            this.cbAutoTrain = new System.Windows.Forms.CheckBox();
            this.cbImportFromSeparateFasta = new System.Windows.Forms.CheckBox();
            this.tbxFastaTargets = new System.Windows.Forms.TextBox();
            this.browseFastaTargetsBtn = new System.Windows.Forms.Button();
            this.targetFastaPanel = new System.Windows.Forms.Panel();
            this.panelError.SuspendLayout();
            this.panelDecoys.SuspendLayout();
            this.targetFastaPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // tbxFasta
            // 
            this.tbxFasta.AcceptsReturn = true;
            resources.ApplyResources(this.tbxFasta, "tbxFasta");
            this.tbxFasta.Name = "tbxFasta";
            this.helpTipFasta.SetToolTip(this.tbxFasta, resources.GetString("tbxFasta.ToolTip"));
            this.tbxFasta.TextChanged += new System.EventHandler(this.tbxFasta_TextChanged);
            // 
            // browseFastaBtn
            // 
            resources.ApplyResources(this.browseFastaBtn, "browseFastaBtn");
            this.browseFastaBtn.Name = "browseFastaBtn";
            this.browseFastaBtn.UseVisualStyleBackColor = true;
            this.browseFastaBtn.Click += new System.EventHandler(this.browseFastaBtn_Click);
            // 
            // panelError
            // 
            resources.ApplyResources(this.panelError, "panelError");
            this.panelError.Controls.Add(this.tbxError);
            this.panelError.Name = "panelError";
            // 
            // tbxError
            // 
            resources.ApplyResources(this.tbxError, "tbxError");
            this.tbxError.BackColor = System.Drawing.SystemColors.Window;
            this.tbxError.Name = "tbxError";
            this.tbxError.ReadOnly = true;
            // 
            // helpTipFasta
            // 
            this.helpTipFasta.AutoPopDelay = 32767;
            this.helpTipFasta.InitialDelay = 500;
            this.helpTipFasta.ReshowDelay = 100;
            // 
            // txtNumDecoys
            // 
            resources.ApplyResources(this.txtNumDecoys, "txtNumDecoys");
            this.txtNumDecoys.Name = "txtNumDecoys";
            this.helpTipFasta.SetToolTip(this.txtNumDecoys, resources.GetString("txtNumDecoys.ToolTip"));
            // 
            // clearBtn
            // 
            resources.ApplyResources(this.clearBtn, "clearBtn");
            this.clearBtn.Name = "clearBtn";
            this.clearBtn.UseVisualStyleBackColor = true;
            this.clearBtn.Click += new System.EventHandler(this.clearBtn_Click);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // cbMissedCleavages
            // 
            this.cbMissedCleavages.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbMissedCleavages.FormattingEnabled = true;
            this.cbMissedCleavages.Items.AddRange(new object[] {
            resources.GetString("cbMissedCleavages.Items"),
            resources.GetString("cbMissedCleavages.Items1"),
            resources.GetString("cbMissedCleavages.Items2"),
            resources.GetString("cbMissedCleavages.Items3"),
            resources.GetString("cbMissedCleavages.Items4"),
            resources.GetString("cbMissedCleavages.Items5"),
            resources.GetString("cbMissedCleavages.Items6"),
            resources.GetString("cbMissedCleavages.Items7"),
            resources.GetString("cbMissedCleavages.Items8"),
            resources.GetString("cbMissedCleavages.Items9")});
            resources.ApplyResources(this.cbMissedCleavages, "cbMissedCleavages");
            this.cbMissedCleavages.Name = "cbMissedCleavages";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // comboEnzyme
            // 
            this.comboEnzyme.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboEnzyme.FormattingEnabled = true;
            resources.ApplyResources(this.comboEnzyme, "comboEnzyme");
            this.comboEnzyme.Name = "comboEnzyme";
            this.comboEnzyme.SelectedIndexChanged += new System.EventHandler(this.enzyme_SelectedIndexChanged);
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            // 
            // cbDecoyMethod
            // 
            this.cbDecoyMethod.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbDecoyMethod.FormattingEnabled = true;
            resources.ApplyResources(this.cbDecoyMethod, "cbDecoyMethod");
            this.cbDecoyMethod.Name = "cbDecoyMethod";
            this.cbDecoyMethod.SelectedIndexChanged += new System.EventHandler(this.cbDecoyMethod_SelectedIndexChanged);
            // 
            // panelDecoys
            // 
            resources.ApplyResources(this.panelDecoys, "panelDecoys");
            this.panelDecoys.Controls.Add(this.cbAutoTrain);
            this.panelDecoys.Controls.Add(this.label5);
            this.panelDecoys.Controls.Add(this.txtNumDecoys);
            this.panelDecoys.Controls.Add(this.cbDecoyMethod);
            this.panelDecoys.Controls.Add(this.label4);
            this.panelDecoys.Name = "panelDecoys";
            // 
            // cbAutoTrain
            // 
            resources.ApplyResources(this.cbAutoTrain, "cbAutoTrain");
            this.cbAutoTrain.Name = "cbAutoTrain";
            this.cbAutoTrain.UseVisualStyleBackColor = true;
            // 
            // cbImportFromSeparateFasta
            // 
            resources.ApplyResources(this.cbImportFromSeparateFasta, "cbImportFromSeparateFasta");
            this.cbImportFromSeparateFasta.Name = "cbImportFromSeparateFasta";
            this.cbImportFromSeparateFasta.UseVisualStyleBackColor = true;
            this.cbImportFromSeparateFasta.CheckedChanged += new System.EventHandler(this.cbImportFromSeparateFasta_CheckedChanged);
            // 
            // tbxFastaTargets
            // 
            resources.ApplyResources(this.tbxFastaTargets, "tbxFastaTargets");
            this.tbxFastaTargets.Name = "tbxFastaTargets";
            // 
            // browseFastaTargetsBtn
            // 
            resources.ApplyResources(this.browseFastaTargetsBtn, "browseFastaTargetsBtn");
            this.browseFastaTargetsBtn.Name = "browseFastaTargetsBtn";
            this.browseFastaTargetsBtn.UseVisualStyleBackColor = true;
            this.browseFastaTargetsBtn.Click += new System.EventHandler(this.browseFastaTargetsBtn_Click);
            // 
            // targetFastaPanel
            // 
            resources.ApplyResources(this.targetFastaPanel, "targetFastaPanel");
            this.targetFastaPanel.Controls.Add(this.cbImportFromSeparateFasta);
            this.targetFastaPanel.Controls.Add(this.tbxFastaTargets);
            this.targetFastaPanel.Controls.Add(this.browseFastaTargetsBtn);
            this.targetFastaPanel.Name = "targetFastaPanel";
            // 
            // ImportFastaControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Controls.Add(this.targetFastaPanel);
            this.Controls.Add(this.tbxFasta);
            this.Controls.Add(this.panelDecoys);
            this.Controls.Add(this.comboEnzyme);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.cbMissedCleavages);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.clearBtn);
            this.Controls.Add(this.panelError);
            this.Controls.Add(this.browseFastaBtn);
            this.Name = "ImportFastaControl";
            this.panelError.ResumeLayout(false);
            this.panelError.PerformLayout();
            this.panelDecoys.ResumeLayout(false);
            this.panelDecoys.PerformLayout();
            this.targetFastaPanel.ResumeLayout(false);
            this.targetFastaPanel.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox tbxFasta;
        private System.Windows.Forms.Button browseFastaBtn;
        private System.Windows.Forms.Panel panelError;
        private System.Windows.Forms.TextBox tbxError;
        private System.Windows.Forms.ToolTip helpTipFasta;
        private System.Windows.Forms.Button clearBtn;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox cbMissedCleavages;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox comboEnzyme;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.ComboBox cbDecoyMethod;
        private System.Windows.Forms.TextBox txtNumDecoys;
        private System.Windows.Forms.Panel panelDecoys;
        private System.Windows.Forms.CheckBox cbAutoTrain;
        private System.Windows.Forms.CheckBox cbImportFromSeparateFasta;
        private System.Windows.Forms.TextBox tbxFastaTargets;
        private System.Windows.Forms.Button browseFastaTargetsBtn;
        private System.Windows.Forms.Panel targetFastaPanel;
    }
}
