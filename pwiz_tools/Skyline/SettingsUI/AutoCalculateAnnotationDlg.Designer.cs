namespace pwiz.Skyline.SettingsUI
{
    partial class AutoCalculateAnnotationDlg
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
            this.availableFieldsTree1 = new pwiz.Common.DataBinding.Controls.Editor.AvailableFieldsTree();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).BeginInit();
            this.SuspendLayout();
            // 
            // availableFieldsTree1
            // 
            this.availableFieldsTree1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.availableFieldsTree1.CheckedColumns = new pwiz.Common.DataBinding.PropertyPath[0];
            this.availableFieldsTree1.DrawMode = System.Windows.Forms.TreeViewDrawMode.OwnerDrawText;
            this.availableFieldsTree1.HideCollections = true;
            this.availableFieldsTree1.ImageIndex = 0;
            this.availableFieldsTree1.Location = new System.Drawing.Point(12, 47);
            this.availableFieldsTree1.Name = "availableFieldsTree1";
            this.availableFieldsTree1.SelectedImageIndex = 0;
            this.availableFieldsTree1.ShowHiddenFields = false;
            this.availableFieldsTree1.ShowNodeToolTips = true;
            this.availableFieldsTree1.Size = new System.Drawing.Size(292, 362);
            this.availableFieldsTree1.TabIndex = 0;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnCancel.Location = new System.Drawing.Point(229, 415);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 10;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnOK.Location = new System.Drawing.Point(148, 415);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 9;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.Location = new System.Drawing.Point(14, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(290, 35);
            this.label1.TabIndex = 11;
            this.label1.Text = "Choose the value from the tree that you would like the annotation value to be.";
            // 
            // AutoCalculateAnnotationDlg
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(316, 450);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.availableFieldsTree1);
            this.Name = "AutoCalculateAnnotationDlg";
            this.ShowIcon = false;
            this.Text = "AutoCalculateAnnotationDlg";
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private Common.DataBinding.Controls.Editor.AvailableFieldsTree availableFieldsTree1;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Label label1;
    }
}