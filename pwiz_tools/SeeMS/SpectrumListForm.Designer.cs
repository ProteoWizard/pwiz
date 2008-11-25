namespace seems
{
	partial class SpectrumListForm
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
            this.components = new System.ComponentModel.Container();
            this.gridView = new System.Windows.Forms.DataGridView();
            this.selectColumnsMenuStrip = new System.Windows.Forms.ContextMenuStrip( this.components );
            this.selectColumnsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.SpectrumId = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.NativeID = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Index = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.SpotId = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.SpectrumType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.msLevel = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ScanTime = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.DataPoints = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.BasePeakMZ = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.BasePeakIntensity = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.TotalIntensity = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.InstrumentConfigurationID = new System.Windows.Forms.DataGridViewLinkColumn();
            this.DataProcessing = new System.Windows.Forms.DataGridViewLinkColumn();
            this.PrecursorInfo = new System.Windows.Forms.DataGridViewLinkColumn();
            this.ScanInfo = new System.Windows.Forms.DataGridViewLinkColumn();
            ( (System.ComponentModel.ISupportInitialize) ( this.gridView ) ).BeginInit();
            this.selectColumnsMenuStrip.SuspendLayout();
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
            this.SpotId,
            this.SpectrumType,
            this.msLevel,
            this.ScanTime,
            this.DataPoints,
            this.BasePeakMZ,
            this.BasePeakIntensity,
            this.TotalIntensity,
            this.InstrumentConfigurationID,
            this.DataProcessing,
            this.PrecursorInfo,
            this.ScanInfo} );
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
            this.gridView.ColumnHeaderMouseClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler( this.gridView_ColumnHeaderMouseClick );
            this.gridView.CellMouseDoubleClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler( this.gridView_CellMouseDoubleClick );
            // 
            // selectColumnsMenuStrip
            // 
            this.selectColumnsMenuStrip.Items.AddRange( new System.Windows.Forms.ToolStripItem[] {
            this.selectColumnsToolStripMenuItem} );
            this.selectColumnsMenuStrip.Name = "selectColumnsMenuStrip";
            this.selectColumnsMenuStrip.Size = new System.Drawing.Size( 160, 26 );
            // 
            // selectColumnsToolStripMenuItem
            // 
            this.selectColumnsToolStripMenuItem.Name = "selectColumnsToolStripMenuItem";
            this.selectColumnsToolStripMenuItem.Size = new System.Drawing.Size( 159, 22 );
            this.selectColumnsToolStripMenuItem.Text = "Select Columns...";
            this.selectColumnsToolStripMenuItem.Click += new System.EventHandler( this.selectColumnsToolStripMenuItem_Click );
            // 
            // SpectrumId
            // 
            this.SpectrumId.HeaderText = "Id";
            this.SpectrumId.Name = "SpectrumId";
            this.SpectrumId.ReadOnly = true;
            this.SpectrumId.ToolTipText = "Unique Arbitrary Idenitifer";
            // 
            // NativeID
            // 
            this.NativeID.HeaderText = "Native Id";
            this.NativeID.Name = "NativeID";
            this.NativeID.ReadOnly = true;
            this.NativeID.ToolTipText = "Unique Vendor/Instrument spectrum Identifier";
            // 
            // Index
            // 
            this.Index.HeaderText = "Index";
            this.Index.Name = "Index";
            this.Index.ReadOnly = true;
            this.Index.ToolTipText = "Ordinal Index in Data Source";
            // 
            // SpotId
            // 
            this.SpotId.HeaderText = "MALDI Spot Id";
            this.SpotId.Name = "SpotId";
            this.SpotId.ReadOnly = true;
            this.SpotId.Visible = false;
            // 
            // SpectrumType
            // 
            this.SpectrumType.HeaderText = "Spectrum Type";
            this.SpectrumType.Name = "SpectrumType";
            this.SpectrumType.ReadOnly = true;
            // 
            // msLevel
            // 
            this.msLevel.HeaderText = "MS Level";
            this.msLevel.Name = "msLevel";
            this.msLevel.ReadOnly = true;
            // 
            // ScanTime
            // 
            this.ScanTime.HeaderText = "Scan Time";
            this.ScanTime.Name = "ScanTime";
            this.ScanTime.ReadOnly = true;
            // 
            // DataPoints
            // 
            this.DataPoints.HeaderText = "Data Points";
            this.DataPoints.Name = "DataPoints";
            this.DataPoints.ReadOnly = true;
            this.DataPoints.ToolTipText = "Number of M/Z Data Points";
            // 
            // BasePeakMZ
            // 
            this.BasePeakMZ.HeaderText = "BP m/z";
            this.BasePeakMZ.Name = "BasePeakMZ";
            this.BasePeakMZ.ReadOnly = true;
            // 
            // BasePeakIntensity
            // 
            this.BasePeakIntensity.HeaderText = "BP Intensity";
            this.BasePeakIntensity.Name = "BasePeakIntensity";
            this.BasePeakIntensity.ReadOnly = true;
            // 
            // TotalIntensity
            // 
            this.TotalIntensity.HeaderText = "Total Intensity";
            this.TotalIntensity.Name = "TotalIntensity";
            this.TotalIntensity.ReadOnly = true;
            // 
            // InstrumentConfigurationID
            // 
            this.InstrumentConfigurationID.HeaderText = "IC Id";
            this.InstrumentConfigurationID.Name = "InstrumentConfigurationID";
            this.InstrumentConfigurationID.ReadOnly = true;
            this.InstrumentConfigurationID.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.InstrumentConfigurationID.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.InstrumentConfigurationID.ToolTipText = "Instrument Configuration";
            this.InstrumentConfigurationID.TrackVisitedState = false;
            // 
            // DataProcessing
            // 
            this.DataProcessing.HeaderText = "DP Id";
            this.DataProcessing.Name = "DataProcessing";
            this.DataProcessing.ReadOnly = true;
            this.DataProcessing.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.DataProcessing.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.DataProcessing.ToolTipText = "Data Processing";
            // 
            // PrecursorInfo
            // 
            this.PrecursorInfo.HeaderText = "Precursor Info";
            this.PrecursorInfo.Name = "PrecursorInfo";
            this.PrecursorInfo.ReadOnly = true;
            this.PrecursorInfo.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.PrecursorInfo.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.PrecursorInfo.ToolTipText = "Information related to the precursor (if applicable)";
            // 
            // ScanInfo
            // 
            this.ScanInfo.HeaderText = "Scan Info";
            this.ScanInfo.Name = "ScanInfo";
            this.ScanInfo.ReadOnly = true;
            this.ScanInfo.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.ScanInfo.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.ScanInfo.ToolTipText = "Information related to the scan and the acquisitions composing it";
            // 
            // SpectrumListForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF( 6F, 13F );
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size( 976, 411 );
            this.Controls.Add( this.gridView );
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Name = "SpectrumListForm";
            this.TabText = "Spectrum List";
            this.Text = "Spectrum List";
            ( (System.ComponentModel.ISupportInitialize) ( this.gridView ) ).EndInit();
            this.selectColumnsMenuStrip.ResumeLayout( false );
            this.ResumeLayout( false );

		}

		#endregion

        private System.Windows.Forms.DataGridView gridView;
        private System.Windows.Forms.ContextMenuStrip selectColumnsMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem selectColumnsToolStripMenuItem;
        private System.Windows.Forms.DataGridViewTextBoxColumn SpectrumId;
        private System.Windows.Forms.DataGridViewTextBoxColumn NativeID;
        private System.Windows.Forms.DataGridViewTextBoxColumn Index;
        private System.Windows.Forms.DataGridViewTextBoxColumn SpotId;
        private System.Windows.Forms.DataGridViewTextBoxColumn SpectrumType;
        private System.Windows.Forms.DataGridViewTextBoxColumn msLevel;
        private System.Windows.Forms.DataGridViewTextBoxColumn ScanTime;
        private System.Windows.Forms.DataGridViewTextBoxColumn DataPoints;
        private System.Windows.Forms.DataGridViewTextBoxColumn BasePeakMZ;
        private System.Windows.Forms.DataGridViewTextBoxColumn BasePeakIntensity;
        private System.Windows.Forms.DataGridViewTextBoxColumn TotalIntensity;
        private System.Windows.Forms.DataGridViewLinkColumn InstrumentConfigurationID;
        private System.Windows.Forms.DataGridViewLinkColumn DataProcessing;
        private System.Windows.Forms.DataGridViewLinkColumn PrecursorInfo;
        private System.Windows.Forms.DataGridViewLinkColumn ScanInfo;
	}
}