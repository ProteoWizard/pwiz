namespace pwiz.Skyline.FileUI
{
    partial class ImportResultsDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ImportResultsDlg));
            this.radioCreateNew = new System.Windows.Forms.RadioButton();
            this.radioAddExisting = new System.Windows.Forms.RadioButton();
            this.labelNameNew = new System.Windows.Forms.Label();
            this.textName = new System.Windows.Forms.TextBox();
            this.labelNameAdd = new System.Windows.Forms.Label();
            this.comboName = new System.Windows.Forms.ComboBox();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.radioCreateMultiple = new System.Windows.Forms.RadioButton();
            this.radioCreateMultipleMulti = new System.Windows.Forms.RadioButton();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.comboOptimizing = new System.Windows.Forms.ComboBox();
            this.labelOptimizing = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // radioCreateNew
            // 
            this.radioCreateNew.AutoSize = true;
            this.radioCreateNew.Location = new System.Drawing.Point(16, 73);
            this.radioCreateNew.Name = "radioCreateNew";
            this.radioCreateNew.Size = new System.Drawing.Size(131, 17);
            this.radioCreateNew.TabIndex = 2;
            this.radioCreateNew.Text = "&Add one new replicate";
            this.helpTip.SetToolTip(this.radioCreateNew, "Add a single new replicate with any number of injections\r\nand a specific name.");
            this.radioCreateNew.UseVisualStyleBackColor = true;
            this.radioCreateNew.CheckedChanged += new System.EventHandler(this.radioCreateNew_CheckedChanged);
            // 
            // radioAddExisting
            // 
            this.radioAddExisting.AutoSize = true;
            this.radioAddExisting.Location = new System.Drawing.Point(15, 212);
            this.radioAddExisting.Name = "radioAddExisting";
            this.radioAddExisting.Size = new System.Drawing.Size(173, 17);
            this.radioAddExisting.TabIndex = 5;
            this.radioAddExisting.Text = "Add files to an &existing replicate";
            this.helpTip.SetToolTip(this.radioAddExisting, "Add injections from files to an existing replicate, collected\r\nas a multi-injecti" +
                    "on replicate but not imported in multiple\r\noperations, as the injection results " +
                    "become available.");
            this.radioAddExisting.UseVisualStyleBackColor = true;
            this.radioAddExisting.CheckedChanged += new System.EventHandler(this.radioAddExisting_CheckedChanged);
            // 
            // labelNameNew
            // 
            this.labelNameNew.AutoSize = true;
            this.labelNameNew.Enabled = false;
            this.labelNameNew.Location = new System.Drawing.Point(31, 93);
            this.labelNameNew.Name = "labelNameNew";
            this.labelNameNew.Size = new System.Drawing.Size(38, 13);
            this.labelNameNew.TabIndex = 3;
            this.labelNameNew.Text = "&Name:";
            // 
            // textName
            // 
            this.textName.Enabled = false;
            this.textName.Location = new System.Drawing.Point(31, 110);
            this.textName.Name = "textName";
            this.textName.Size = new System.Drawing.Size(209, 20);
            this.textName.TabIndex = 4;
            // 
            // labelNameAdd
            // 
            this.labelNameAdd.AutoSize = true;
            this.labelNameAdd.Enabled = false;
            this.labelNameAdd.Location = new System.Drawing.Point(30, 232);
            this.labelNameAdd.Name = "labelNameAdd";
            this.labelNameAdd.Size = new System.Drawing.Size(38, 13);
            this.labelNameAdd.TabIndex = 6;
            this.labelNameAdd.Text = "&Name:";
            // 
            // comboName
            // 
            this.comboName.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboName.Enabled = false;
            this.comboName.FormattingEnabled = true;
            this.comboName.Location = new System.Drawing.Point(30, 249);
            this.comboName.Name = "comboName";
            this.comboName.Size = new System.Drawing.Size(209, 21);
            this.comboName.TabIndex = 7;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.Location = new System.Drawing.Point(260, 10);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 12;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(260, 40);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 13;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // radioCreateMultiple
            // 
            this.radioCreateMultiple.AutoSize = true;
            this.radioCreateMultiple.Checked = true;
            this.radioCreateMultiple.Location = new System.Drawing.Point(16, 10);
            this.radioCreateMultiple.Name = "radioCreateMultiple";
            this.radioCreateMultiple.Size = new System.Drawing.Size(196, 17);
            this.radioCreateMultiple.TabIndex = 0;
            this.radioCreateMultiple.TabStop = true;
            this.radioCreateMultiple.Text = "Add &single-injection replicates in files";
            this.helpTip.SetToolTip(this.radioCreateMultiple, resources.GetString("radioCreateMultiple.ToolTip"));
            this.radioCreateMultiple.UseVisualStyleBackColor = true;
            this.radioCreateMultiple.CheckedChanged += new System.EventHandler(this.radioCreateMultiple_CheckedChanged);
            // 
            // radioCreateMultipleMulti
            // 
            this.radioCreateMultipleMulti.AutoSize = true;
            this.radioCreateMultipleMulti.Location = new System.Drawing.Point(16, 40);
            this.radioCreateMultipleMulti.Name = "radioCreateMultipleMulti";
            this.radioCreateMultipleMulti.Size = new System.Drawing.Size(220, 17);
            this.radioCreateMultipleMulti.TabIndex = 1;
            this.radioCreateMultipleMulti.TabStop = true;
            this.radioCreateMultipleMulti.Text = "Add &multi-injection replicates in directories";
            this.helpTip.SetToolTip(this.radioCreateMultipleMulti, resources.GetString("radioCreateMultipleMulti.ToolTip"));
            this.radioCreateMultipleMulti.UseVisualStyleBackColor = true;
            this.radioCreateMultipleMulti.CheckedChanged += new System.EventHandler(this.radioCreateMultipleMulti_CheckedChanged);
            // 
            // helpTip
            // 
            this.helpTip.AutoPopDelay = 15000;
            this.helpTip.InitialDelay = 500;
            this.helpTip.ReshowDelay = 100;
            // 
            // comboOptimizing
            // 
            this.comboOptimizing.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboOptimizing.Enabled = false;
            this.comboOptimizing.FormattingEnabled = true;
            this.comboOptimizing.Location = new System.Drawing.Point(31, 165);
            this.comboOptimizing.Name = "comboOptimizing";
            this.comboOptimizing.Size = new System.Drawing.Size(131, 21);
            this.comboOptimizing.TabIndex = 9;
            // 
            // labelOptimizing
            // 
            this.labelOptimizing.AutoSize = true;
            this.labelOptimizing.Enabled = false;
            this.labelOptimizing.Location = new System.Drawing.Point(31, 149);
            this.labelOptimizing.Name = "labelOptimizing";
            this.labelOptimizing.Size = new System.Drawing.Size(58, 13);
            this.labelOptimizing.TabIndex = 8;
            this.labelOptimizing.Text = "Optimi&zing:";
            // 
            // ImportResultsDlg
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(347, 291);
            this.Controls.Add(this.labelOptimizing);
            this.Controls.Add(this.comboOptimizing);
            this.Controls.Add(this.radioCreateMultipleMulti);
            this.Controls.Add(this.radioCreateMultiple);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.comboName);
            this.Controls.Add(this.labelNameAdd);
            this.Controls.Add(this.textName);
            this.Controls.Add(this.labelNameNew);
            this.Controls.Add(this.radioAddExisting);
            this.Controls.Add(this.radioCreateNew);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ImportResultsDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Import Results";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.RadioButton radioCreateNew;
        private System.Windows.Forms.RadioButton radioAddExisting;
        private System.Windows.Forms.Label labelNameNew;
        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label labelNameAdd;
        private System.Windows.Forms.ComboBox comboName;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.RadioButton radioCreateMultiple;
        private System.Windows.Forms.RadioButton radioCreateMultipleMulti;
        private System.Windows.Forms.ToolTip helpTip;
        private System.Windows.Forms.ComboBox comboOptimizing;
        private System.Windows.Forms.Label labelOptimizing;
    }
}