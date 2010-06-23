namespace Forms.Controls
{
    partial class AttestationDialog
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
            this.bigBDResultsLoad = new System.Windows.Forms.Button();
            this.autoAttestResultsCheckBox = new System.Windows.Forms.CheckBox();
            this.generateNewDeltaMassTableCheckBox = new System.Windows.Forms.CheckBox();
            this.deltaTICTextBox = new System.Windows.Forms.TextBox();
            this.attestationDialogOK = new System.Windows.Forms.Button();
            this.attestationDialogCancel = new System.Windows.Forms.Button();
            this.bigDBResultsFile = new System.Windows.Forms.TextBox();
            this.deltaScoreThresholdsGroupBox = new System.Windows.Forms.GroupBox();
            this.deltaXCorrThresholdTextBox = new System.Windows.Forms.TextBox();
            this.filterByXCorrRadioButton = new System.Windows.Forms.RadioButton();
            this.filterByTICRabioButton = new System.Windows.Forms.RadioButton();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.deltaScoreThresholdsGroupBox.SuspendLayout();
            this.SuspendLayout();
            // 
            // bigBDResultsLoad
            // 
            this.bigBDResultsLoad.Location = new System.Drawing.Point(12, 12);
            this.bigBDResultsLoad.Name = "bigBDResultsLoad";
            this.bigBDResultsLoad.Size = new System.Drawing.Size(124, 51);
            this.bigBDResultsLoad.TabIndex = 0;
            this.bigBDResultsLoad.Text = "Load BigDB Results";
            this.bigBDResultsLoad.UseVisualStyleBackColor = true;
            this.bigBDResultsLoad.Click += new System.EventHandler(this.bigBDResultsLoad_Click);
            // 
            // autoAttestResultsCheckBox
            // 
            this.autoAttestResultsCheckBox.AccessibleRole = System.Windows.Forms.AccessibleRole.Application;
            this.autoAttestResultsCheckBox.AutoSize = true;
            this.autoAttestResultsCheckBox.Enabled = false;
            this.autoAttestResultsCheckBox.Location = new System.Drawing.Point(12, 83);
            this.autoAttestResultsCheckBox.Name = "autoAttestResultsCheckBox";
            this.autoAttestResultsCheckBox.Size = new System.Drawing.Size(147, 21);
            this.autoAttestResultsCheckBox.TabIndex = 2;
            this.autoAttestResultsCheckBox.Text = "Auto Attest Results";
            this.autoAttestResultsCheckBox.UseVisualStyleBackColor = true;
            // 
            // generateNewDeltaMassTableCheckBox
            // 
            this.generateNewDeltaMassTableCheckBox.AutoSize = true;
            this.generateNewDeltaMassTableCheckBox.Enabled = false;
            this.generateNewDeltaMassTableCheckBox.Location = new System.Drawing.Point(34, 202);
            this.generateNewDeltaMassTableCheckBox.Name = "generateNewDeltaMassTableCheckBox";
            this.generateNewDeltaMassTableCheckBox.Size = new System.Drawing.Size(232, 21);
            this.generateNewDeltaMassTableCheckBox.TabIndex = 3;
            this.generateNewDeltaMassTableCheckBox.Text = "Generate New Delta Mass Table";
            this.generateNewDeltaMassTableCheckBox.UseVisualStyleBackColor = true;
            // 
            // deltaTICTextBox
            // 
            this.deltaTICTextBox.Enabled = false;
            this.deltaTICTextBox.Location = new System.Drawing.Point(185, 23);
            this.deltaTICTextBox.Name = "deltaTICTextBox";
            this.deltaTICTextBox.Size = new System.Drawing.Size(23, 22);
            this.deltaTICTextBox.TabIndex = 4;
            this.deltaTICTextBox.Text = "5";
            // 
            // attestationDialogOK
            // 
            this.attestationDialogOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.attestationDialogOK.Location = new System.Drawing.Point(34, 235);
            this.attestationDialogOK.Name = "attestationDialogOK";
            this.attestationDialogOK.Size = new System.Drawing.Size(102, 35);
            this.attestationDialogOK.TabIndex = 6;
            this.attestationDialogOK.Text = "OK";
            this.attestationDialogOK.UseVisualStyleBackColor = true;
            // 
            // attestationDialogCancel
            // 
            this.attestationDialogCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.attestationDialogCancel.Location = new System.Drawing.Point(165, 235);
            this.attestationDialogCancel.Name = "attestationDialogCancel";
            this.attestationDialogCancel.Size = new System.Drawing.Size(101, 35);
            this.attestationDialogCancel.TabIndex = 7;
            this.attestationDialogCancel.Text = "Cancel";
            this.attestationDialogCancel.UseVisualStyleBackColor = true;
            // 
            // bigDBResultsFile
            // 
            this.bigDBResultsFile.BackColor = System.Drawing.SystemColors.Control;
            this.bigDBResultsFile.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.bigDBResultsFile.Location = new System.Drawing.Point(144, 13);
            this.bigDBResultsFile.MaximumSize = new System.Drawing.Size(150, 50);
            this.bigDBResultsFile.MinimumSize = new System.Drawing.Size(150, 50);
            this.bigDBResultsFile.Multiline = true;
            this.bigDBResultsFile.Name = "bigDBResultsFile";
            this.bigDBResultsFile.Size = new System.Drawing.Size(150, 50);
            this.bigDBResultsFile.TabIndex = 8;
            // 
            // deltaScoreThresholdsGroupBox
            // 
            this.deltaScoreThresholdsGroupBox.Controls.Add(this.label2);
            this.deltaScoreThresholdsGroupBox.Controls.Add(this.label1);
            this.deltaScoreThresholdsGroupBox.Controls.Add(this.deltaXCorrThresholdTextBox);
            this.deltaScoreThresholdsGroupBox.Controls.Add(this.filterByXCorrRadioButton);
            this.deltaScoreThresholdsGroupBox.Controls.Add(this.deltaTICTextBox);
            this.deltaScoreThresholdsGroupBox.Controls.Add(this.filterByTICRabioButton);
            this.deltaScoreThresholdsGroupBox.Enabled = false;
            this.deltaScoreThresholdsGroupBox.Location = new System.Drawing.Point(22, 110);
            this.deltaScoreThresholdsGroupBox.Name = "deltaScoreThresholdsGroupBox";
            this.deltaScoreThresholdsGroupBox.Size = new System.Drawing.Size(244, 84);
            this.deltaScoreThresholdsGroupBox.TabIndex = 12;
            this.deltaScoreThresholdsGroupBox.TabStop = false;
            this.deltaScoreThresholdsGroupBox.Text = "Modified peptides must improve";
            // 
            // deltaXCorrThresholdTextBox
            // 
            this.deltaXCorrThresholdTextBox.Enabled = false;
            this.deltaXCorrThresholdTextBox.Location = new System.Drawing.Point(184, 54);
            this.deltaXCorrThresholdTextBox.Name = "deltaXCorrThresholdTextBox";
            this.deltaXCorrThresholdTextBox.Size = new System.Drawing.Size(22, 22);
            this.deltaXCorrThresholdTextBox.TabIndex = 6;
            this.deltaXCorrThresholdTextBox.Text = "10";
            // 
            // filterByXCorrRadioButton
            // 
            this.filterByXCorrRadioButton.AutoSize = true;
            this.filterByXCorrRadioButton.Enabled = false;
            this.filterByXCorrRadioButton.Location = new System.Drawing.Point(15, 53);
            this.filterByXCorrRadioButton.Name = "filterByXCorrRadioButton";
            this.filterByXCorrRadioButton.Size = new System.Drawing.Size(131, 21);
            this.filterByXCorrRadioButton.TabIndex = 5;
            this.filterByXCorrRadioButton.TabStop = true;
            this.filterByXCorrRadioButton.Text = "XCorr by atleast ";
            this.filterByXCorrRadioButton.UseVisualStyleBackColor = true;
            // 
            // filterByTICRabioButton
            // 
            this.filterByTICRabioButton.AutoSize = true;
            this.filterByTICRabioButton.Enabled = false;
            this.filterByTICRabioButton.Location = new System.Drawing.Point(15, 24);
            this.filterByTICRabioButton.Name = "filterByTICRabioButton";
            this.filterByTICRabioButton.Size = new System.Drawing.Size(170, 21);
            this.filterByTICRabioButton.TabIndex = 0;
            this.filterByTICRabioButton.TabStop = true;
            this.filterByTICRabioButton.Text = "Matched TIC by atleast";
            this.filterByTICRabioButton.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(210, 26);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(20, 17);
            this.label1.TabIndex = 7;
            this.label1.Text = "%";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(209, 57);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(20, 17);
            this.label2.TabIndex = 8;
            this.label2.Text = "%";
            // 
            // AttestationDialog
            // 
            this.AcceptButton = this.attestationDialogOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.attestationDialogCancel;
            this.ClientSize = new System.Drawing.Size(302, 294);
            this.Controls.Add(this.deltaScoreThresholdsGroupBox);
            this.Controls.Add(this.bigDBResultsFile);
            this.Controls.Add(this.attestationDialogCancel);
            this.Controls.Add(this.attestationDialogOK);
            this.Controls.Add(this.generateNewDeltaMassTableCheckBox);
            this.Controls.Add(this.autoAttestResultsCheckBox);
            this.Controls.Add(this.bigBDResultsLoad);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Name = "AttestationDialog";
            this.Text = "Choose attestation params";
            this.deltaScoreThresholdsGroupBox.ResumeLayout(false);
            this.deltaScoreThresholdsGroupBox.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button bigBDResultsLoad;
        private System.Windows.Forms.Button attestationDialogOK;
        private System.Windows.Forms.Button attestationDialogCancel;
        public System.Windows.Forms.TextBox bigDBResultsFile;
        public System.Windows.Forms.CheckBox autoAttestResultsCheckBox;
        public System.Windows.Forms.CheckBox generateNewDeltaMassTableCheckBox;
        public System.Windows.Forms.TextBox deltaTICTextBox;
        private System.Windows.Forms.GroupBox deltaScoreThresholdsGroupBox;
        public System.Windows.Forms.RadioButton filterByTICRabioButton;
        public System.Windows.Forms.RadioButton filterByXCorrRadioButton;
        public System.Windows.Forms.TextBox deltaXCorrThresholdTextBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
    }
}