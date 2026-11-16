namespace pwiz.Skyline.FileUI.PeptideSearch
{
    partial class OspreyModificationsControl
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(OspreyModificationsControl));
            this.lblFixedMods = new System.Windows.Forms.Label();
            this.listFixedMods = new System.Windows.Forms.ListBox();
            this.btnAddFixedMod = new System.Windows.Forms.Button();
            this.btnRemoveFixedMod = new System.Windows.Forms.Button();
            this.lblVariableMods = new System.Windows.Forms.Label();
            this.listVariableMods = new System.Windows.Forms.ListBox();
            this.btnAddVariableMod = new System.Windows.Forms.Button();
            this.btnRemoveVariableMod = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // lblFixedMods
            //
            resources.ApplyResources(this.lblFixedMods, "lblFixedMods");
            this.lblFixedMods.Name = "lblFixedMods";
            //
            // listFixedMods
            //
            resources.ApplyResources(this.listFixedMods, "listFixedMods");
            this.listFixedMods.Name = "listFixedMods";
            //
            // btnAddFixedMod
            //
            resources.ApplyResources(this.btnAddFixedMod, "btnAddFixedMod");
            this.btnAddFixedMod.Name = "btnAddFixedMod";
            this.btnAddFixedMod.UseVisualStyleBackColor = true;
            this.btnAddFixedMod.Click += new System.EventHandler(this.btnAddFixedMod_Click);
            //
            // btnRemoveFixedMod
            //
            resources.ApplyResources(this.btnRemoveFixedMod, "btnRemoveFixedMod");
            this.btnRemoveFixedMod.Name = "btnRemoveFixedMod";
            this.btnRemoveFixedMod.UseVisualStyleBackColor = true;
            this.btnRemoveFixedMod.Click += new System.EventHandler(this.btnRemoveFixedMod_Click);
            //
            // lblVariableMods
            //
            resources.ApplyResources(this.lblVariableMods, "lblVariableMods");
            this.lblVariableMods.Name = "lblVariableMods";
            //
            // listVariableMods
            //
            resources.ApplyResources(this.listVariableMods, "listVariableMods");
            this.listVariableMods.Name = "listVariableMods";
            //
            // btnAddVariableMod
            //
            resources.ApplyResources(this.btnAddVariableMod, "btnAddVariableMod");
            this.btnAddVariableMod.Name = "btnAddVariableMod";
            this.btnAddVariableMod.UseVisualStyleBackColor = true;
            this.btnAddVariableMod.Click += new System.EventHandler(this.btnAddVariableMod_Click);
            //
            // btnRemoveVariableMod
            //
            resources.ApplyResources(this.btnRemoveVariableMod, "btnRemoveVariableMod");
            this.btnRemoveVariableMod.Name = "btnRemoveVariableMod";
            this.btnRemoveVariableMod.UseVisualStyleBackColor = true;
            this.btnRemoveVariableMod.Click += new System.EventHandler(this.btnRemoveVariableMod_Click);
            //
            // OspreyModificationsControl
            //
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.btnRemoveVariableMod);
            this.Controls.Add(this.btnAddVariableMod);
            this.Controls.Add(this.listVariableMods);
            this.Controls.Add(this.lblVariableMods);
            this.Controls.Add(this.btnRemoveFixedMod);
            this.Controls.Add(this.btnAddFixedMod);
            this.Controls.Add(this.listFixedMods);
            this.Controls.Add(this.lblFixedMods);
            this.Name = "OspreyModificationsControl";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblFixedMods;
        private System.Windows.Forms.ListBox listFixedMods;
        private System.Windows.Forms.Button btnAddFixedMod;
        private System.Windows.Forms.Button btnRemoveFixedMod;
        private System.Windows.Forms.Label lblVariableMods;
        private System.Windows.Forms.ListBox listVariableMods;
        private System.Windows.Forms.Button btnAddVariableMod;
        private System.Windows.Forms.Button btnRemoveVariableMod;
    }
}
