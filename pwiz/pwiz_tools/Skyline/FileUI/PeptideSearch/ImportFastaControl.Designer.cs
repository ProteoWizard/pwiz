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
            this.tbxFasta = new System.Windows.Forms.TextBox();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.browseFastaBtn = new System.Windows.Forms.Button();
            this.panelError = new System.Windows.Forms.Panel();
            this.tbxError = new System.Windows.Forms.TextBox();
            this.panelError.SuspendLayout();
            this.SuspendLayout();
            // 
            // tbxFasta
            // 
            this.tbxFasta.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbxFasta.Location = new System.Drawing.Point(14, 35);
            this.tbxFasta.MaxLength = 2147483647;
            this.tbxFasta.Multiline = true;
            this.tbxFasta.Name = "tbxFasta";
            this.tbxFasta.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.tbxFasta.Size = new System.Drawing.Size(282, 214);
            this.tbxFasta.TabIndex = 1;
            this.tbxFasta.TextChanged += new System.EventHandler(this.tbxFasta_TextChanged);
            // 
            // textBox1
            // 
            this.textBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBox1.BackColor = System.Drawing.SystemColors.Window;
            this.textBox1.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox1.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.textBox1.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.textBox1.Location = new System.Drawing.Point(14, 3);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.ReadOnly = true;
            this.textBox1.Size = new System.Drawing.Size(363, 26);
            this.textBox1.TabIndex = 0;
            this.textBox1.Text = "FASTA records begin with \'>\' and have the protein name followed by the optional p" +
    "rotein description.";
            // 
            // browseFastaBtn
            // 
            this.browseFastaBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.browseFastaBtn.Location = new System.Drawing.Point(302, 35);
            this.browseFastaBtn.Name = "browseFastaBtn";
            this.browseFastaBtn.Size = new System.Drawing.Size(75, 23);
            this.browseFastaBtn.TabIndex = 2;
            this.browseFastaBtn.Text = "&Browse...";
            this.browseFastaBtn.UseVisualStyleBackColor = true;
            this.browseFastaBtn.Click += new System.EventHandler(this.browseFastaBtn_Click);
            // 
            // panelError
            // 
            this.panelError.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelError.Controls.Add(this.tbxError);
            this.panelError.Location = new System.Drawing.Point(14, 258);
            this.panelError.Name = "panelError";
            this.panelError.Size = new System.Drawing.Size(282, 35);
            this.panelError.TabIndex = 22;
            this.panelError.Visible = false;
            // 
            // tbxError
            // 
            this.tbxError.BackColor = System.Drawing.SystemColors.Window;
            this.tbxError.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxError.Location = new System.Drawing.Point(0, 0);
            this.tbxError.Multiline = true;
            this.tbxError.Name = "tbxError";
            this.tbxError.ReadOnly = true;
            this.tbxError.Size = new System.Drawing.Size(282, 35);
            this.tbxError.TabIndex = 0;
            // 
            // ImportFastaControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Controls.Add(this.panelError);
            this.Controls.Add(this.browseFastaBtn);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.tbxFasta);
            this.Name = "ImportFastaControl";
            this.Size = new System.Drawing.Size(381, 307);
            this.panelError.ResumeLayout(false);
            this.panelError.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox tbxFasta;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Button browseFastaBtn;
        private System.Windows.Forms.Panel panelError;
        private System.Windows.Forms.TextBox tbxError;
    }
}
