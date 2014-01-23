namespace pwiz.Skyline.Controls.Databinding
{
    partial class DocumentGridForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DocumentGridForm));
            this.navBar = new pwiz.Common.DataBinding.Controls.NavBar();
            this.bindingListSource = new pwiz.Common.DataBinding.Controls.BindingListSource(this.components);
            this.boundDataGridView = new pwiz.Skyline.Controls.Databinding.BoundDataGridViewEx();
            ((System.ComponentModel.ISupportInitialize)(this.bindingListSource)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.boundDataGridView)).BeginInit();
            this.SuspendLayout();
            // 
            // navBar
            // 
            resources.ApplyResources(this.navBar, "navBar");
            this.navBar.BindingListSource = this.bindingListSource;
            this.navBar.Name = "navBar";
            this.navBar.ShowViewsButton = true;
            // 
            // bindingListSource
            // 
            this.bindingListSource.RowSource = new object[0];
            // 
            // boundDataGridView
            // 
            this.boundDataGridView.AutoGenerateColumns = false;
            this.boundDataGridView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.boundDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.boundDataGridView.DataSource = this.bindingListSource;
            resources.ApplyResources(this.boundDataGridView, "boundDataGridView");
            this.boundDataGridView.Name = "boundDataGridView";
            // 
            // DocumentGridForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.boundDataGridView);
            this.Controls.Add(this.navBar);
            this.Name = "DocumentGridForm";
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.bindingListSource)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.boundDataGridView)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private Common.DataBinding.Controls.NavBar navBar;
        private Common.DataBinding.Controls.BindingListSource bindingListSource;
        private pwiz.Skyline.Controls.Databinding.BoundDataGridViewEx boundDataGridView;
    }
}