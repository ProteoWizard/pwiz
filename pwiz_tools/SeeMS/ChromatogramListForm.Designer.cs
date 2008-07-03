namespace seems
{
	partial class ChromatogramListForm
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose( bool disposing )
		{
			if( disposing && ( components != null ) )
			{
				components.Dispose();
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.gridView = new System.Windows.Forms.DataGridView();
			this.SpectrumId = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.NativeID = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Index = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.ChromatogramType = new System.Windows.Forms.DataGridViewTextBoxColumn();
			( (System.ComponentModel.ISupportInitialize) ( this.gridView ) ).BeginInit();
			this.SuspendLayout();
			// 
			// gridView
			// 
			this.gridView.AllowUserToAddRows = false;
			this.gridView.AllowUserToDeleteRows = false;
			this.gridView.AllowUserToOrderColumns = true;
			this.gridView.AllowUserToResizeRows = false;
			this.gridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
			this.gridView.BorderStyle = System.Windows.Forms.BorderStyle.None;
			this.gridView.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Single;
			this.gridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.gridView.Columns.AddRange( new System.Windows.Forms.DataGridViewColumn[] {
            this.SpectrumId,
            this.NativeID,
            this.Index,
            this.ChromatogramType} );
			this.gridView.Cursor = System.Windows.Forms.Cursors.Hand;
			this.gridView.Dock = System.Windows.Forms.DockStyle.Fill;
			this.gridView.Location = new System.Drawing.Point( 0, 0 );
			this.gridView.Name = "gridView";
			this.gridView.ReadOnly = true;
			this.gridView.RowHeadersVisible = false;
			this.gridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
			this.gridView.Size = new System.Drawing.Size( 976, 411 );
			this.gridView.TabIndex = 10;
			this.gridView.CellMouseClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler( this.gridView_CellMouseClick );
			this.gridView.CellMouseDoubleClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler( this.gridView_CellMouseDoubleClick );
			// 
			// SpectrumId
			// 
			this.SpectrumId.HeaderText = "Id";
			this.SpectrumId.Name = "SpectrumId";
			this.SpectrumId.ReadOnly = true;
			// 
			// NativeID
			// 
			this.NativeID.HeaderText = "Native Id";
			this.NativeID.Name = "NativeID";
			this.NativeID.ReadOnly = true;
			// 
			// Index
			// 
			this.Index.HeaderText = "Index";
			this.Index.Name = "Index";
			this.Index.ReadOnly = true;
			// 
			// ChromatogramType
			// 
			this.ChromatogramType.HeaderText = "Chromatogram Type";
			this.ChromatogramType.Name = "ChromatogramType";
			this.ChromatogramType.ReadOnly = true;
			// 
			// ChromatogramListForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF( 6F, 13F );
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size( 976, 411 );
			this.Controls.Add( this.gridView );
			this.DoubleBuffered = true;
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
			this.Name = "ChromatogramListForm";
			this.TabText = "Chromatogram List";
			this.Text = "Chromatogram List";
			( (System.ComponentModel.ISupportInitialize) ( this.gridView ) ).EndInit();
			this.ResumeLayout( false );

		}

		#endregion

		private System.Windows.Forms.DataGridView gridView;
		private System.Windows.Forms.DataGridViewTextBoxColumn SpectrumId;
		private System.Windows.Forms.DataGridViewTextBoxColumn NativeID;
		private System.Windows.Forms.DataGridViewTextBoxColumn Index;
		private System.Windows.Forms.DataGridViewTextBoxColumn ChromatogramType;
	}
}