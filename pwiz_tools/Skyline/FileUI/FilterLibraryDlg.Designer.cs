namespace pwiz.Skyline.FileUI
{
    partial class FilterLibraryDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FilterLibraryDlg));
            this.comboCategories = new System.Windows.Forms.ComboBox();
            this.comboValues = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // comboCategories
            // 
            this.comboCategories.FormattingEnabled = true;
            this.comboCategories.Location = new System.Drawing.Point(54, 74);
            this.comboCategories.Name = "comboCategories";
            this.comboCategories.Size = new System.Drawing.Size(121, 21);
            this.comboCategories.TabIndex = 0;
            // 
            // comboValues
            // 
            this.comboValues.FormattingEnabled = true;
            this.comboValues.Location = new System.Drawing.Point(360, 73);
            this.comboValues.Name = "comboValues";
            this.comboValues.Size = new System.Drawing.Size(121, 21);
            this.comboValues.TabIndex = 1;
            // 
            // FilterLibraryDlg
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.comboValues);
            this.Controls.Add(this.comboCategories);
            this.Name = "FilterLibraryDlg";
            this.Text = "FilterLibraryDlg";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ComboBox comboCategories;
        private System.Windows.Forms.ComboBox comboValues;
    }
}