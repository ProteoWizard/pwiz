using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.SettingsUI
{
    partial class EditCoVDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditCoVDlg));
            this.label1 = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.textName = new System.Windows.Forms.TextBox();
            this.textMin = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.textMax = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.textStepsRough = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.textStepsMedium = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.textStepsFine = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            resources.ApplyResources(this.btnCancel, "btnCancel");
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
            // textName
            // 
            resources.ApplyResources(this.textName, "textName");
            this.textName.Name = "textName";
            // 
            // textMin
            // 
            resources.ApplyResources(this.textMin, "textMin");
            this.textMin.Name = "textMin";
            // 
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            // 
            // textMax
            // 
            resources.ApplyResources(this.textMax, "textMax");
            this.textMax.Name = "textMax";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // textStepsRough
            // 
            resources.ApplyResources(this.textStepsRough, "textStepsRough");
            this.textStepsRough.Name = "textStepsRough";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // textStepsMedium
            // 
            resources.ApplyResources(this.textStepsMedium, "textStepsMedium");
            this.textStepsMedium.Name = "textStepsMedium";
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // textStepsFine
            // 
            resources.ApplyResources(this.textStepsFine, "textStepsFine");
            this.textStepsFine.Name = "textStepsFine";
            // 
            // label6
            // 
            resources.ApplyResources(this.label6, "label6");
            this.label6.Name = "label6";
            // 
            // EditCoVDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.textStepsFine);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.textStepsMedium);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.textStepsRough);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.textMax);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textMin);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.textName);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditCoVDlg";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.TextBox textMin;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox textMax;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textStepsRough;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textStepsMedium;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox textStepsFine;
        private System.Windows.Forms.Label label6;
    }
}