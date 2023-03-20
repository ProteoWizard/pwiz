//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//

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
            this.Id = new DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn();
            this.SpotId = new DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn();
            this.SpectrumType = new DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn();
            this.MsLevel = new DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn();
            this.DataPoints = new DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn();
            this.ScanTime = new DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn();
            this.BasePeakMz = new DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn();
            this.BasePeakIntensity = new DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn();
            this.TotalIonCurrent = new DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn();
            this.IcId = new DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn();
            this.DpId = new DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn();
            this.PrecursorInfo = new DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn();
            this.IsolationWindows = new DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn();
            this.ScanInfo = new DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn();
            this.IonMobility = new DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn();
            this.spectraSource = new System.Windows.Forms.BindingSource( this.components );
            this.spectrumDataSet = new seems.Misc.SpectrumDataSet();
            this.selectColumnsMenuStrip = new System.Windows.Forms.ContextMenuStrip( this.components );
            this.selectColumnsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            ( (System.ComponentModel.ISupportInitialize) ( this.gridView ) ).BeginInit();
            ( (System.ComponentModel.ISupportInitialize) ( this.spectraSource ) ).BeginInit();
            ( (System.ComponentModel.ISupportInitialize) ( this.spectrumDataSet ) ).BeginInit();
            this.selectColumnsMenuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // gridView
            // 
            this.gridView.AllowUserToAddRows = false;
            this.gridView.AllowUserToDeleteRows = false;
            this.gridView.AllowUserToOrderColumns = true;
            this.gridView.AllowUserToResizeRows = false;
            this.gridView.AutoGenerateColumns = false;
            this.gridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.gridView.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Single;
            this.gridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridView.Columns.AddRange( new System.Windows.Forms.DataGridViewColumn[] {
            this.Id,
            this.SpotId,
            this.SpectrumType,
            this.MsLevel,
            this.DataPoints,
            this.ScanTime,
            this.BasePeakMz,
            this.BasePeakIntensity,
            this.TotalIonCurrent,
            this.IcId,
            this.DpId,
            this.PrecursorInfo,
            this.IsolationWindows,
            this.ScanInfo,
            this.IonMobility} );
            this.gridView.DataSource = this.spectraSource;
            this.gridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridView.Location = new System.Drawing.Point( 0, 0 );
            this.gridView.Name = "gridView";
            this.gridView.ReadOnly = true;
            this.gridView.RowHeadersVisible = false;
            this.gridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.gridView.Size = new System.Drawing.Size( 839, 411 );
            this.gridView.TabIndex = 10;
            this.gridView.CellMouseClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler( this.gridView_CellMouseClick );
            this.gridView.ColumnHeaderMouseClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler( this.gridView_ColumnHeaderMouseClick );
            this.gridView.CellMouseDoubleClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler( this.gridView_CellMouseDoubleClick );
            // 
            // Id
            // 
            this.Id.DataPropertyName = "Id";
            this.Id.HeaderText = "Id";
            this.Id.Name = "Id";
            this.Id.ReadOnly = true;
            this.Id.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            // 
            // SpotId
            // 
            this.SpotId.DataPropertyName = "SpotId";
            this.SpotId.HeaderText = "Spot Id";
            this.SpotId.Name = "SpotId";
            this.SpotId.ReadOnly = true;
            this.SpotId.Visible = false;
            // 
            // SpectrumType
            // 
            this.SpectrumType.DataPropertyName = "SpectrumType";
            this.SpectrumType.HeaderText = "Spectrum Type";
            this.SpectrumType.Name = "SpectrumType";
            this.SpectrumType.ReadOnly = true;
            this.SpectrumType.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            // 
            // MsLevel
            // 
            this.MsLevel.DataPropertyName = "MsLevel";
            this.MsLevel.HeaderText = "MS Level";
            this.MsLevel.Name = "MsLevel";
            this.MsLevel.ReadOnly = true;
            this.MsLevel.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            // 
            // DataPoints
            // 
            this.DataPoints.DataPropertyName = "DataPoints";
            this.DataPoints.HeaderText = "Data Points";
            this.DataPoints.Name = "DataPoints";
            this.DataPoints.ReadOnly = true;
            this.DataPoints.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            // 
            // ScanTime
            // 
            this.ScanTime.DataPropertyName = "ScanTime";
            this.ScanTime.HeaderText = "Scan Time";
            this.ScanTime.Name = "ScanTime";
            this.ScanTime.ReadOnly = true;
            // 
            // BasePeakMz
            // 
            this.BasePeakMz.DataPropertyName = "BasePeakMz";
            this.BasePeakMz.HeaderText = "Base Peak m/z";
            this.BasePeakMz.Name = "BasePeakMz";
            this.BasePeakMz.ReadOnly = true;
            this.BasePeakMz.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            // 
            // BasePeakIntensity
            // 
            this.BasePeakIntensity.DataPropertyName = "BasePeakIntensity";
            this.BasePeakIntensity.HeaderText = "Base Peak Intensity";
            this.BasePeakIntensity.Name = "BasePeakIntensity";
            this.BasePeakIntensity.ReadOnly = true;
            this.BasePeakIntensity.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            // 
            // TotalIonCurrent
            // 
            this.TotalIonCurrent.DataPropertyName = "TotalIonCurrent";
            this.TotalIonCurrent.HeaderText = "Total Ion Current";
            this.TotalIonCurrent.Name = "TotalIonCurrent";
            this.TotalIonCurrent.ReadOnly = true;
            this.TotalIonCurrent.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            // 
            // IcId
            // 
            this.IcId.DataPropertyName = "IcId";
            this.IcId.HeaderText = "IC Id";
            this.IcId.Name = "IcId";
            this.IcId.ReadOnly = true;
            this.IcId.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            // 
            // DpId
            // 
            this.DpId.DataPropertyName = "DpId";
            this.DpId.HeaderText = "DP Id";
            this.DpId.Name = "DpId";
            this.DpId.ReadOnly = true;
            this.DpId.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            // 
            // PrecursorInfo
            // 
            this.PrecursorInfo.DataPropertyName = "PrecursorInfo";
            this.PrecursorInfo.HeaderText = "Precursor Info";
            this.PrecursorInfo.Name = "PrecursorInfo";
            this.PrecursorInfo.ReadOnly = true;
            this.PrecursorInfo.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            // 
            // IsolationWindows
            // 
            this.IsolationWindows.DataPropertyName = "IsolationWindows";
            this.IsolationWindows.HeaderText = "Isolation Windows";
            this.IsolationWindows.Name = "IsolationWindows";
            this.IsolationWindows.ReadOnly = true;
            this.IsolationWindows.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            // 
            // ScanInfo
            // 
            this.ScanInfo.DataPropertyName = "ScanInfo";
            this.ScanInfo.HeaderText = "Scan Info";
            this.ScanInfo.Name = "ScanInfo";
            this.ScanInfo.ReadOnly = true;
            this.ScanInfo.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            // 
            // IonMobility
            // 
            this.IonMobility.DataPropertyName = "IonMobility";
            this.IonMobility.HeaderText = "Ion Mobility";
            this.IonMobility.Name = "IonMobility";
            this.IonMobility.ReadOnly = true;
            this.IonMobility.Visible = false;
            // 
            // spectraSource
            // 
            this.spectraSource.DataMember = "SpectrumTable";
            this.spectraSource.DataSource = this.spectrumDataSet;
            // 
            // spectrumDataSet
            // 
            this.spectrumDataSet.DataSetName = "SpectrumDataSet";
            this.spectrumDataSet.SchemaSerializationMode = System.Data.SchemaSerializationMode.IncludeSchema;
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
            // SpectrumListForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF( 6F, 13F );
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size( 839, 411 );
            this.Controls.Add( this.gridView );
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.HideOnClose = true;
            this.Name = "SpectrumListForm";
            this.TabText = "Spectrum List";
            this.Text = "Spectrum List";
            ( (System.ComponentModel.ISupportInitialize) ( this.gridView ) ).EndInit();
            ( (System.ComponentModel.ISupportInitialize) ( this.spectraSource ) ).EndInit();
            ( (System.ComponentModel.ISupportInitialize) ( this.spectrumDataSet ) ).EndInit();
            this.selectColumnsMenuStrip.ResumeLayout( false );
            this.ResumeLayout( false );

		}

		#endregion

        private System.Windows.Forms.DataGridView gridView;
        private System.Windows.Forms.ContextMenuStrip selectColumnsMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem selectColumnsToolStripMenuItem;
        private System.Windows.Forms.BindingSource spectraSource;
        private global::seems.Misc.SpectrumDataSet spectrumDataSet;
        private DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn Id;
        private DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn SpotId;
        private DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn SpectrumType;
        private DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn MsLevel;
        private DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn DataPoints;
        private DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn ScanTime;
        private DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn BasePeakMz;
        private DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn BasePeakIntensity;
        private DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn TotalIonCurrent;
        private DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn IcId;
        private DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn DpId;
        private DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn PrecursorInfo;
        private DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn IsolationWindows;
        private DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn ScanInfo;
        private DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn IonMobility;
	}
}