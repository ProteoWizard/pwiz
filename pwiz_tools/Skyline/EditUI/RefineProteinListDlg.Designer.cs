namespace pwiz.Skyline.EditUI
{
    partial class RefineProteinListDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RefineProteinListDlg));
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.proteinPreferredNames = new System.Windows.Forms.RadioButton();
            this.proteinAccessions = new System.Windows.Forms.RadioButton();
            this.proteinNames = new System.Windows.Forms.RadioButton();
            this.label1 = new System.Windows.Forms.Label();
            this.textProteins = new System.Windows.Forms.TextBox();
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
            // 
            // proteinPreferredNames
            // 
            resources.ApplyResources(this.proteinPreferredNames, "proteinPreferredNames");
            this.proteinPreferredNames.Name = "proteinPreferredNames";
            this.proteinPreferredNames.UseVisualStyleBackColor = true;
            // 
            // proteinAccessions
            // 
            resources.ApplyResources(this.proteinAccessions, "proteinAccessions");
            this.proteinAccessions.Name = "proteinAccessions";
            this.proteinAccessions.UseVisualStyleBackColor = true;
            // 
            // proteinNames
            // 
            resources.ApplyResources(this.proteinNames, "proteinNames");
            this.proteinNames.Checked = true;
            this.proteinNames.Name = "proteinNames";
            this.proteinNames.TabStop = true;
            this.proteinNames.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // textProteins
            // 
            resources.ApplyResources(this.textProteins, "textProteins");
            this.textProteins.Name = "textProteins";
            this.textProteins.Enter += new System.EventHandler(this.textProteins_Enter);
            this.textProteins.Leave += new System.EventHandler(this.textPeptides_Leave);
            // 
            // RefineProteinListDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.proteinPreferredNames);
            this.Controls.Add(this.proteinAccessions);
            this.Controls.Add(this.proteinNames);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.textProteins);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "RefineProteinListDlg";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.TextBox textProteins;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.RadioButton proteinNames;
        private System.Windows.Forms.RadioButton proteinAccessions;
        private System.Windows.Forms.RadioButton proteinPreferredNames;
    }
}