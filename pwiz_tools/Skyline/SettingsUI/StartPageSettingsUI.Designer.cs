namespace pwiz.Skyline.SettingsUI
{
    partial class StartPageSettingsUI
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(StartPageSettingsUI));
            this.pictureBox3 = new System.Windows.Forms.PictureBox();
            this.pictureBox2 = new System.Windows.Forms.PictureBox();
            this.settingsInfoLabel = new System.Windows.Forms.Label();
            this.closeBtn = new System.Windows.Forms.Button();
            this.nextBtn = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.btnResetDefaults = new System.Windows.Forms.Button();
            this.radioBtnRefine = new System.Windows.Forms.RadioButton();
            this.radioBtnQuant = new System.Windows.Forms.RadioButton();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.labelIntegrateAll = new System.Windows.Forms.Label();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox3)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // pictureBox3
            // 
            this.pictureBox3.Image = global::pwiz.Skyline.Properties.Resources.WizardTransitionIcon;
            resources.ApplyResources(this.pictureBox3, "pictureBox3");
            this.pictureBox3.Name = "pictureBox3";
            this.pictureBox3.TabStop = false;
            this.pictureBox3.Click += new System.EventHandler(this.transitionSettingsBtn_Click);
            // 
            // pictureBox2
            // 
            this.pictureBox2.Image = global::pwiz.Skyline.Properties.Resources.WizardPeptideIcon;
            resources.ApplyResources(this.pictureBox2, "pictureBox2");
            this.pictureBox2.Name = "pictureBox2";
            this.pictureBox2.TabStop = false;
            this.pictureBox2.Click += new System.EventHandler(this.peptideSettingsBtn_Click);
            // 
            // settingsInfoLabel
            // 
            resources.ApplyResources(this.settingsInfoLabel, "settingsInfoLabel");
            this.settingsInfoLabel.AutoEllipsis = true;
            this.settingsInfoLabel.Name = "settingsInfoLabel";
            // 
            // closeBtn
            // 
            resources.ApplyResources(this.closeBtn, "closeBtn");
            this.closeBtn.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.closeBtn.Name = "closeBtn";
            this.closeBtn.UseVisualStyleBackColor = true;
            // 
            // nextBtn
            // 
            resources.ApplyResources(this.nextBtn, "nextBtn");
            this.nextBtn.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.nextBtn.Name = "nextBtn";
            this.nextBtn.UseVisualStyleBackColor = true;
            // 
            // button1
            // 
            resources.ApplyResources(this.button1, "button1");
            this.button1.Name = "button1";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.peptideSettingsBtn_Click);
            // 
            // button2
            // 
            resources.ApplyResources(this.button2, "button2");
            this.button2.Name = "button2";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.transitionSettingsBtn_Click);
            // 
            // btnResetDefaults
            // 
            resources.ApplyResources(this.btnResetDefaults, "btnResetDefaults");
            this.btnResetDefaults.Name = "btnResetDefaults";
            this.toolTip1.SetToolTip(this.btnResetDefaults, resources.GetString("btnResetDefaults.ToolTip"));
            this.btnResetDefaults.UseVisualStyleBackColor = true;
            this.btnResetDefaults.Click += new System.EventHandler(this.btnResetDefaults_Click);
            // 
            // radioBtnRefine
            // 
            resources.ApplyResources(this.radioBtnRefine, "radioBtnRefine");
            this.radioBtnRefine.Checked = true;
            this.radioBtnRefine.Name = "radioBtnRefine";
            this.radioBtnRefine.TabStop = true;
            this.toolTip1.SetToolTip(this.radioBtnRefine, resources.GetString("radioBtnRefine.ToolTip"));
            this.radioBtnRefine.UseVisualStyleBackColor = true;
            // 
            // radioBtnQuant
            // 
            resources.ApplyResources(this.radioBtnQuant, "radioBtnQuant");
            this.radioBtnQuant.Name = "radioBtnQuant";
            this.toolTip1.SetToolTip(this.radioBtnQuant, resources.GetString("radioBtnQuant.ToolTip"));
            this.radioBtnQuant.UseVisualStyleBackColor = true;
            this.radioBtnQuant.CheckedChanged += new System.EventHandler(this.radioBtnQuant_CheckedChanged);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.labelIntegrateAll);
            this.groupBox1.Controls.Add(this.radioBtnRefine);
            this.groupBox1.Controls.Add(this.radioBtnQuant);
            resources.ApplyResources(this.groupBox1, "groupBox1");
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.TabStop = false;
            // 
            // labelIntegrateAll
            // 
            resources.ApplyResources(this.labelIntegrateAll, "labelIntegrateAll");
            this.labelIntegrateAll.Name = "labelIntegrateAll";
            // 
            // StartPageSettingsUI
            // 
            this.AcceptButton = this.nextBtn;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.closeBtn;
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.btnResetDefaults);
            this.Controls.Add(this.pictureBox3);
            this.Controls.Add(this.pictureBox2);
            this.Controls.Add(this.settingsInfoLabel);
            this.Controls.Add(this.closeBtn);
            this.Controls.Add(this.nextBtn);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.button2);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = global::pwiz.Skyline.Properties.Resources.Skyline;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "StartPageSettingsUI";
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox3)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button nextBtn;
        private System.Windows.Forms.Button closeBtn;
        private System.Windows.Forms.Label settingsInfoLabel;
        private System.Windows.Forms.PictureBox pictureBox2;
        private System.Windows.Forms.PictureBox pictureBox3;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button btnResetDefaults;
        private System.Windows.Forms.RadioButton radioBtnRefine;
        private System.Windows.Forms.RadioButton radioBtnQuant;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.Label labelIntegrateAll;
    }
}