namespace pwiz.Skyline.Controls.Databinding
{
    partial class LiveResultsGrid
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LiveResultsGrid));
            this.boundDataGridView = new pwiz.Common.DataBinding.Controls.BoundDataGridView();
            this.bindingListSource = new pwiz.Common.DataBinding.Controls.BindingListSource(this.components);
            this.navBar = new pwiz.Common.DataBinding.Controls.NavBar();
            ((System.ComponentModel.ISupportInitialize)(this.boundDataGridView)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingListSource)).BeginInit();
            this.SuspendLayout();
            // 
            // boundDataGridView
            // 
            this.boundDataGridView.AutoGenerateColumns = false;
            this.boundDataGridView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.boundDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.boundDataGridView.DataSource = this.bindingListSource;
            resources.ApplyResources(this.boundDataGridView, "boundDataGridView");
            this.boundDataGridView.Name = "boundDataGridView";
            this.boundDataGridView.DataBindingComplete += new System.Windows.Forms.DataGridViewBindingCompleteEventHandler(this.boundDataGridView_DataBindingComplete);
            // 
            // bindingListSource
            // 
            this.bindingListSource.RowSource = new object[0];
            this.bindingListSource.ListChanged += new System.ComponentModel.ListChangedEventHandler(this.bindingListSource_ListChanged);
            // 
            // navBar
            // 
            resources.ApplyResources(this.navBar, "navBar");
            this.navBar.BindingListSource = this.bindingListSource;
            this.navBar.Name = "navBar";
            this.navBar.ShowViewsButton = true;
            // 
            // LiveResultsGrid
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.boundDataGridView);
            this.Controls.Add(this.navBar);
            this.Name = "LiveResultsGrid";
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.boundDataGridView)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingListSource)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private Common.DataBinding.Controls.BindingListSource bindingListSource;
        private Common.DataBinding.Controls.BoundDataGridView boundDataGridView;
        private Common.DataBinding.Controls.NavBar navBar;
    }
}