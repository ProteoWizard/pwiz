namespace pwiz.Common.DataBinding.Controls
{
    partial class ManageLayoutsForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ManageLayoutsForm));
            this.listViewLayouts = new pwiz.Common.DataBinding.Controls.ColumnListView();
            this.imageList1 = new System.Windows.Forms.ImageList(this.components);
            this.btnMakeDefault = new System.Windows.Forms.Button();
            this.btnDelete = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // listViewLayouts
            // 
            resources.ApplyResources(this.listViewLayouts, "listViewLayouts");
            this.listViewLayouts.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.listViewLayouts.HideSelection = false;
            this.listViewLayouts.Name = "listViewLayouts";
            this.listViewLayouts.ShowItemToolTips = true;
            this.listViewLayouts.SmallImageList = this.imageList1;
            this.listViewLayouts.UseCompatibleStateImageBehavior = false;
            this.listViewLayouts.View = System.Windows.Forms.View.Details;
            this.listViewLayouts.SelectedIndexChanged += new System.EventHandler(this.listViewLayouts_SelectedIndexChanged);
            // 
            // imageList1
            // 
            this.imageList1.ColorDepth = System.Windows.Forms.ColorDepth.Depth8Bit;
            resources.ApplyResources(this.imageList1, "imageList1");
            this.imageList1.TransparentColor = System.Drawing.Color.Magenta;
            // 
            // btnMakeDefault
            // 
            resources.ApplyResources(this.btnMakeDefault, "btnMakeDefault");
            this.btnMakeDefault.Name = "btnMakeDefault";
            this.btnMakeDefault.UseVisualStyleBackColor = true;
            this.btnMakeDefault.Click += new System.EventHandler(this.btnMakeDefault_Click);
            // 
            // btnDelete
            // 
            resources.ApplyResources(this.btnDelete, "btnDelete");
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.UseVisualStyleBackColor = true;
            this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
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
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            // 
            // ManageLayoutsForm
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnDelete);
            this.Controls.Add(this.btnMakeDefault);
            this.Controls.Add(this.listViewLayouts);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ManageLayoutsForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);

        }

        #endregion

        private ColumnListView listViewLayouts;
        private System.Windows.Forms.Button btnMakeDefault;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.ImageList imageList1;
    }
}