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
            this.boundDataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.boundDataGridView.Location = new System.Drawing.Point(0, 25);
            this.boundDataGridView.Name = "boundDataGridView";
            this.boundDataGridView.Size = new System.Drawing.Size(673, 433);
            this.boundDataGridView.TabIndex = 0;
            // 
            // bindingListSource
            // 
            this.bindingListSource.CurrentChanged += new System.EventHandler(this.bindingListSource_CurrentChanged);
            // 
            // navBar
            // 
            this.navBar.AutoSize = true;
            this.navBar.BindingListSource = this.bindingListSource;
            this.navBar.Dock = System.Windows.Forms.DockStyle.Top;
            this.navBar.Location = new System.Drawing.Point(0, 0);
            this.navBar.Name = "navBar";
            this.navBar.Size = new System.Drawing.Size(673, 25);
            this.navBar.TabIndex = 1;
            // 
            // LiveResultsGrid
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(673, 458);
            this.Controls.Add(this.boundDataGridView);
            this.Controls.Add(this.navBar);
            this.Name = "LiveResultsGrid";
            this.TabText = "Results Grid";
            this.Text = "Results Grid";
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