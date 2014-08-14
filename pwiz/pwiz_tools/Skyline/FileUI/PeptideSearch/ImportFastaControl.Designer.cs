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
            this.clearBtn = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.cbMissedCleavages = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.comboEnzyme = new System.Windows.Forms.ComboBox();
            this.panelError.SuspendLayout();
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
            this.tbxError.BackColor = System.Drawing.SystemColors.Window;
            resources.ApplyResources(this.tbxError, "tbxError");
            this.tbxError.Name = "tbxError";
            this.tbxError.ReadOnly = true;
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
            // ImportFastaControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Controls.Add(this.comboEnzyme);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.cbMissedCleavages);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.clearBtn);
            this.Controls.Add(this.panelError);
            this.Controls.Add(this.browseFastaBtn);
            this.Controls.Add(this.tbxFasta);
            this.Name = "ImportFastaControl";
            this.panelError.ResumeLayout(false);
            this.panelError.PerformLayout();
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
    }
}
