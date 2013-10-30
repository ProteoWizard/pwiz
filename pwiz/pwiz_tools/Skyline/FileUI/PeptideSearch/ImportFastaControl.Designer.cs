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
            this.tbxFasta.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbxFasta.Location = new System.Drawing.Point(14, 89);
            this.tbxFasta.MaxLength = 2147483647;
            this.tbxFasta.Multiline = true;
            this.tbxFasta.Name = "tbxFasta";
            this.tbxFasta.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.tbxFasta.Size = new System.Drawing.Size(282, 182);
            this.tbxFasta.TabIndex = 5;
            this.helpTipFasta.SetToolTip(this.tbxFasta, "FASTA sequence text pasted or loaded from files");
            this.tbxFasta.WordWrap = false;
            this.tbxFasta.TextChanged += new System.EventHandler(this.tbxFasta_TextChanged);
            // 
            // browseFastaBtn
            // 
            this.browseFastaBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.browseFastaBtn.Location = new System.Drawing.Point(302, 87);
            this.browseFastaBtn.Name = "browseFastaBtn";
            this.browseFastaBtn.Size = new System.Drawing.Size(75, 23);
            this.browseFastaBtn.TabIndex = 6;
            this.browseFastaBtn.Text = "&Browse...";
            this.browseFastaBtn.UseVisualStyleBackColor = true;
            this.browseFastaBtn.Click += new System.EventHandler(this.browseFastaBtn_Click);
            // 
            // panelError
            // 
            this.panelError.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelError.Controls.Add(this.tbxError);
            this.panelError.Location = new System.Drawing.Point(14, 277);
            this.panelError.Name = "panelError";
            this.panelError.Size = new System.Drawing.Size(282, 35);
            this.panelError.TabIndex = 22;
            this.panelError.Visible = false;
            // 
            // tbxError
            // 
            this.tbxError.BackColor = System.Drawing.SystemColors.Window;
            this.tbxError.Location = new System.Drawing.Point(0, 0);
            this.tbxError.Multiline = true;
            this.tbxError.Name = "tbxError";
            this.tbxError.ReadOnly = true;
            this.tbxError.Size = new System.Drawing.Size(282, 35);
            this.tbxError.TabIndex = 0;
            // 
            // clearBtn
            // 
            this.clearBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.clearBtn.Location = new System.Drawing.Point(302, 116);
            this.clearBtn.Name = "clearBtn";
            this.clearBtn.Size = new System.Drawing.Size(75, 23);
            this.clearBtn.TabIndex = 7;
            this.clearBtn.Text = "&Clear";
            this.clearBtn.UseVisualStyleBackColor = true;
            this.clearBtn.Click += new System.EventHandler(this.clearBtn_Click);
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.Location = new System.Drawing.Point(14, 57);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(363, 29);
            this.label1.TabIndex = 4;
            this.label1.Text = "FASTA records begin with \'>\' and have the protein name followed by the optional p" +
    "rotein description.";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(196, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(117, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Max missed cleavages:";
            // 
            // cbMissedCleavages
            // 
            this.cbMissedCleavages.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbMissedCleavages.FormattingEnabled = true;
            this.cbMissedCleavages.Items.AddRange(new object[] {
            "0",
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9"});
            this.cbMissedCleavages.Location = new System.Drawing.Point(199, 16);
            this.cbMissedCleavages.Name = "cbMissedCleavages";
            this.cbMissedCleavages.Size = new System.Drawing.Size(44, 21);
            this.cbMissedCleavages.TabIndex = 3;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(11, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(47, 13);
            this.label3.TabIndex = 0;
            this.label3.Text = "Enzyme:";
            // 
            // comboEnzyme
            // 
            this.comboEnzyme.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboEnzyme.FormattingEnabled = true;
            this.comboEnzyme.Location = new System.Drawing.Point(14, 16);
            this.comboEnzyme.Name = "comboEnzyme";
            this.comboEnzyme.Size = new System.Drawing.Size(169, 21);
            this.comboEnzyme.TabIndex = 1;
            this.comboEnzyme.SelectedIndexChanged += new System.EventHandler(this.enzyme_SelectedIndexChanged);
            // 
            // ImportFastaControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
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
            this.Size = new System.Drawing.Size(381, 315);
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
