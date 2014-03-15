namespace pwiz.Skyline.FileUI
{
    partial class CreateIrtCalculatorDlg
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
            this.label2 = new System.Windows.Forms.Label();
            this.btnCreateDb = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnBrowseDb = new System.Windows.Forms.Button();
            this.textOpenDatabase = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.textCalculatorName = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.radioUseExisting = new System.Windows.Forms.RadioButton();
            this.radioCreateNew = new System.Windows.Forms.RadioButton();
            this.textNewDatabase = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.textImportText = new System.Windows.Forms.TextBox();
            this.btnBrowseText = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // label2
            // 
            this.label2.Location = new System.Drawing.Point(9, 13);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(255, 55);
            this.label2.TabIndex = 0;
            this.label2.Text = "Document does not have an iRT calculator.  An iRT calculator is necessary to add " +
                "iRT library values.  Create an iRT calculator?";
            // 
            // btnCreateDb
            // 
            this.btnCreateDb.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnCreateDb.Location = new System.Drawing.Point(209, 296);
            this.btnCreateDb.Name = "btnCreateDb";
            this.btnCreateDb.Size = new System.Drawing.Size(75, 23);
            this.btnCreateDb.TabIndex = 13;
            this.btnCreateDb.Text = "Brow&se...";
            this.btnCreateDb.UseVisualStyleBackColor = true;
            this.btnCreateDb.Click += new System.EventHandler(this.btnCreateDb_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnCancel.Location = new System.Drawing.Point(272, 45);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 15;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnOk.Location = new System.Drawing.Point(273, 13);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 14;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnBrowseDb
            // 
            this.btnBrowseDb.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnBrowseDb.Location = new System.Drawing.Point(208, 168);
            this.btnBrowseDb.Name = "btnBrowseDb";
            this.btnBrowseDb.Size = new System.Drawing.Size(75, 23);
            this.btnBrowseDb.TabIndex = 6;
            this.btnBrowseDb.Text = "&Browse...";
            this.btnBrowseDb.UseVisualStyleBackColor = true;
            this.btnBrowseDb.Click += new System.EventHandler(this.btnBrowseDb_Click);
            // 
            // textOpenDatabase
            // 
            this.textOpenDatabase.Location = new System.Drawing.Point(26, 171);
            this.textOpenDatabase.Name = "textOpenDatabase";
            this.textOpenDatabase.Size = new System.Drawing.Size(176, 20);
            this.textOpenDatabase.TabIndex = 5;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.label4.Location = new System.Drawing.Point(27, 151);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(74, 13);
            this.label4.TabIndex = 4;
            this.label4.Text = "i&RT database:";
            // 
            // textCalculatorName
            // 
            this.textCalculatorName.Location = new System.Drawing.Point(12, 82);
            this.textCalculatorName.Name = "textCalculatorName";
            this.textCalculatorName.Size = new System.Drawing.Size(176, 20);
            this.textCalculatorName.TabIndex = 2;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.label5.Location = new System.Drawing.Point(11, 63);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(38, 13);
            this.label5.TabIndex = 1;
            this.label5.Text = "&Name:";
            // 
            // radioUseExisting
            // 
            this.radioUseExisting.AutoSize = true;
            this.radioUseExisting.Checked = true;
            this.radioUseExisting.Location = new System.Drawing.Point(12, 124);
            this.radioUseExisting.Name = "radioUseExisting";
            this.radioUseExisting.Size = new System.Drawing.Size(158, 17);
            this.radioUseExisting.TabIndex = 3;
            this.radioUseExisting.TabStop = true;
            this.radioUseExisting.Text = "&Open existing iRT calculator";
            this.radioUseExisting.UseVisualStyleBackColor = true;
            this.radioUseExisting.CheckedChanged += new System.EventHandler(this.radioUseExisting_CheckedChanged);
            // 
            // radioCreateNew
            // 
            this.radioCreateNew.AutoSize = true;
            this.radioCreateNew.Location = new System.Drawing.Point(12, 208);
            this.radioCreateNew.Name = "radioCreateNew";
            this.radioCreateNew.Size = new System.Drawing.Size(314, 17);
            this.radioCreateNew.TabIndex = 7;
            this.radioCreateNew.Text = "&Create new iRT calculator from iRT standards in transition list:";
            this.radioCreateNew.UseVisualStyleBackColor = true;
            this.radioCreateNew.CheckedChanged += new System.EventHandler(this.radioCreateNew_CheckedChanged);
            // 
            // textNewDatabase
            // 
            this.textNewDatabase.Location = new System.Drawing.Point(27, 298);
            this.textNewDatabase.Name = "textNewDatabase";
            this.textNewDatabase.Size = new System.Drawing.Size(176, 20);
            this.textNewDatabase.TabIndex = 12;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.label1.Location = new System.Drawing.Point(28, 280);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(74, 13);
            this.label1.TabIndex = 11;
            this.label1.Text = "iRT &database:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.label3.Location = new System.Drawing.Point(27, 232);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(71, 13);
            this.label3.TabIndex = 8;
            this.label3.Text = "&Transition list:";
            // 
            // textImportText
            // 
            this.textImportText.Location = new System.Drawing.Point(26, 253);
            this.textImportText.Name = "textImportText";
            this.textImportText.Size = new System.Drawing.Size(176, 20);
            this.textImportText.TabIndex = 9;
            // 
            // btnBrowseText
            // 
            this.btnBrowseText.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnBrowseText.Location = new System.Drawing.Point(209, 251);
            this.btnBrowseText.Name = "btnBrowseText";
            this.btnBrowseText.Size = new System.Drawing.Size(75, 23);
            this.btnBrowseText.TabIndex = 10;
            this.btnBrowseText.Text = "Bro&wse...";
            this.btnBrowseText.UseVisualStyleBackColor = true;
            this.btnBrowseText.Click += new System.EventHandler(this.btnBrowseText_Click);
            // 
            // CreateIrtCalculatorDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(358, 343);
            this.Controls.Add(this.btnBrowseText);
            this.Controls.Add(this.textImportText);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.textNewDatabase);
            this.Controls.Add(this.radioCreateNew);
            this.Controls.Add(this.radioUseExisting);
            this.Controls.Add(this.btnCreateDb);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnBrowseDb);
            this.Controls.Add(this.textOpenDatabase);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.textCalculatorName);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label2);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CreateIrtCalculatorDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Create iRT calculator";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnCreateDb;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnBrowseDb;
        private System.Windows.Forms.TextBox textOpenDatabase;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox textCalculatorName;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.RadioButton radioUseExisting;
        private System.Windows.Forms.RadioButton radioCreateNew;
        private System.Windows.Forms.TextBox textNewDatabase;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textImportText;
        private System.Windows.Forms.Button btnBrowseText;
    }
}