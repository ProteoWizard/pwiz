namespace pwiz.Skyline.EditUI
{
    partial class ArrangeGraphsTabbedByGroupDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ArrangeGraphsTabbedByGroupDlg));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.labelGroupBy = new System.Windows.Forms.Label();
            this.comboGroupBy = new System.Windows.Forms.ComboBox();
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
            // labelGroupBy
            //
            resources.ApplyResources(this.labelGroupBy, "labelGroupBy");
            this.labelGroupBy.Name = "labelGroupBy";
            //
            // comboGroupBy
            //
            resources.ApplyResources(this.comboGroupBy, "comboGroupBy");
            this.comboGroupBy.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboGroupBy.FormattingEnabled = true;
            this.comboGroupBy.Name = "comboGroupBy";
            //
            // ArrangeGraphsTabbedByGroupDlg
            //
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.comboGroupBy);
            this.Controls.Add(this.labelGroupBy);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ArrangeGraphsTabbedByGroupDlg";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Label labelGroupBy;
        private System.Windows.Forms.ComboBox comboGroupBy;
    }
}
