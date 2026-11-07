namespace pwiz.Skyline.FileUI.PeptideSearch
{
    partial class OspreyFastaControl
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(OspreyFastaControl));
            this.lblFasta = new System.Windows.Forms.Label();
            this.textFasta = new System.Windows.Forms.TextBox();
            this.btnBrowseFasta = new System.Windows.Forms.Button();
            this.lblEnzyme = new System.Windows.Forms.Label();
            this.comboEnzyme = new System.Windows.Forms.ComboBox();
            this.lblMissedCleavages = new System.Windows.Forms.Label();
            this.comboMissedCleavages = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            //
            // lblFasta
            //
            resources.ApplyResources(this.lblFasta, "lblFasta");
            this.lblFasta.Name = "lblFasta";
            //
            // textFasta
            //
            resources.ApplyResources(this.textFasta, "textFasta");
            this.textFasta.Name = "textFasta";
            //
            // btnBrowseFasta
            //
            resources.ApplyResources(this.btnBrowseFasta, "btnBrowseFasta");
            this.btnBrowseFasta.Name = "btnBrowseFasta";
            this.btnBrowseFasta.UseVisualStyleBackColor = true;
            this.btnBrowseFasta.Click += new System.EventHandler(this.btnBrowseFasta_Click);
            //
            // lblEnzyme
            //
            resources.ApplyResources(this.lblEnzyme, "lblEnzyme");
            this.lblEnzyme.Name = "lblEnzyme";
            //
            // comboEnzyme
            //
            resources.ApplyResources(this.comboEnzyme, "comboEnzyme");
            this.comboEnzyme.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboEnzyme.FormattingEnabled = true;
            this.comboEnzyme.Name = "comboEnzyme";
            this.comboEnzyme.SelectedIndexChanged += new System.EventHandler(this.comboEnzyme_SelectedIndexChanged);
            //
            // lblMissedCleavages
            //
            resources.ApplyResources(this.lblMissedCleavages, "lblMissedCleavages");
            this.lblMissedCleavages.Name = "lblMissedCleavages";
            //
            // comboMissedCleavages
            //
            resources.ApplyResources(this.comboMissedCleavages, "comboMissedCleavages");
            this.comboMissedCleavages.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboMissedCleavages.FormattingEnabled = true;
            this.comboMissedCleavages.Name = "comboMissedCleavages";
            //
            // OspreyFastaControl
            //
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.comboMissedCleavages);
            this.Controls.Add(this.lblMissedCleavages);
            this.Controls.Add(this.comboEnzyme);
            this.Controls.Add(this.lblEnzyme);
            this.Controls.Add(this.btnBrowseFasta);
            this.Controls.Add(this.textFasta);
            this.Controls.Add(this.lblFasta);
            this.Name = "OspreyFastaControl";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblFasta;
        private System.Windows.Forms.TextBox textFasta;
        private System.Windows.Forms.Button btnBrowseFasta;
        private System.Windows.Forms.Label lblEnzyme;
        private System.Windows.Forms.ComboBox comboEnzyme;
        private System.Windows.Forms.Label lblMissedCleavages;
        private System.Windows.Forms.ComboBox comboMissedCleavages;
    }
}
