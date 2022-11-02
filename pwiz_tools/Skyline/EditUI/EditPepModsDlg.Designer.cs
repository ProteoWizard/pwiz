namespace pwiz.Skyline.EditUI
{
    partial class EditPepModsDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditPepModsDlg));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.labelHeavy1 = new System.Windows.Forms.Label();
            this.comboStatic1 = new pwiz.Common.Controls.LiteDropDownList();
            this.comboHeavy1_1 = new pwiz.Common.Controls.LiteDropDownList();
            this.labelAA1 = new System.Windows.Forms.Label();
            this.panelMain = new System.Windows.Forms.Panel();
            this.btnEditLink1 = new System.Windows.Forms.Button();
            this.imageList1 = new System.Windows.Forms.ImageList(this.components);
            this.btnReset = new System.Windows.Forms.Button();
            this.cbCreateCopy = new System.Windows.Forms.CheckBox();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.panelMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // labelHeavy1
            // 
            resources.ApplyResources(this.labelHeavy1, "labelHeavy1");
            this.labelHeavy1.Name = "labelHeavy1";
            // 
            // comboStatic1
            // 
            resources.ApplyResources(this.comboStatic1, "comboStatic1");
            this.comboStatic1.Name = "comboStatic1";
            // 
            // comboHeavy1_1
            // 
            resources.ApplyResources(this.comboHeavy1_1, "comboHeavy1_1");
            this.comboHeavy1_1.Name = "comboHeavy1_1";
            // 
            // labelAA1
            // 
            resources.ApplyResources(this.labelAA1, "labelAA1");
            this.labelAA1.Name = "labelAA1";
            // 
            // panelMain
            // 
            resources.ApplyResources(this.panelMain, "panelMain");
            this.panelMain.Controls.Add(this.btnEditLink1);
            this.panelMain.Controls.Add(this.comboHeavy1_1);
            this.panelMain.Controls.Add(this.labelAA1);
            this.panelMain.Controls.Add(this.comboStatic1);
            this.panelMain.Controls.Add(this.label1);
            this.panelMain.Controls.Add(this.labelHeavy1);
            this.panelMain.Name = "panelMain";
            // 
            // btnEditLink1
            // 
            resources.ApplyResources(this.btnEditLink1, "btnEditLink1");
            this.btnEditLink1.ImageList = this.imageList1;
            this.btnEditLink1.Name = "btnEditLink1";
            this.btnEditLink1.UseVisualStyleBackColor = true;
            // 
            // imageList1
            // 
            this.imageList1.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList1.ImageStream")));
            this.imageList1.TransparentColor = System.Drawing.Color.Magenta;
            this.imageList1.Images.SetKeyName(0, "EditLink.bmp");
            // 
            // btnReset
            // 
            resources.ApplyResources(this.btnReset, "btnReset");
            this.btnReset.Name = "btnReset";
            this.btnReset.UseVisualStyleBackColor = true;
            this.btnReset.Click += new System.EventHandler(this.btnReset_Click);
            // 
            // cbCreateCopy
            // 
            resources.ApplyResources(this.cbCreateCopy, "cbCreateCopy");
            this.cbCreateCopy.Name = "cbCreateCopy";
            this.cbCreateCopy.UseVisualStyleBackColor = true;
            // 
            // EditPepModsDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.cbCreateCopy);
            this.Controls.Add(this.btnReset);
            this.Controls.Add(this.panelMain);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditPepModsDlg";
            this.ShowInTaskbar = false;
            this.panelMain.ResumeLayout(false);
            this.panelMain.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label labelHeavy1;
        private pwiz.Common.Controls.LiteDropDownList comboStatic1;
        private pwiz.Common.Controls.LiteDropDownList comboHeavy1_1;
        private System.Windows.Forms.Label labelAA1;
        private System.Windows.Forms.Panel panelMain;
        private System.Windows.Forms.Button btnReset;
        private System.Windows.Forms.CheckBox cbCreateCopy;
        private System.Windows.Forms.Button btnEditLink1;
        private System.Windows.Forms.ImageList imageList1;
        private System.Windows.Forms.ToolTip toolTip;
    }
}