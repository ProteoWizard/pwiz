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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.CLI.cv;
using pwiz.CLI.data;
using pwiz.CLI.msdata;
using pwiz.CLI.analysis;
using seems.Misc;

namespace seems
{
	public delegate void ChromatogramListCellClickHandler( object sender, ChromatogramListCellClickEventArgs e );
	public delegate void ChromatogramListCellDoubleClickHandler( object sender, ChromatogramListCellDoubleClickEventArgs e );
    public delegate void ChromatogramListFilterChangedHandler( object sender, ChromatogramListFilterChangedEventArgs e );

	public partial class ChromatogramListForm : DockableForm
	{
        Dictionary<int, Chromatogram> chromatogramList; // indexable by Chromatogram.Index

		public DataGridView GridView { get { return gridView; } }

		public event ChromatogramListCellClickHandler CellClick;
		public event ChromatogramListCellDoubleClickHandler CellDoubleClick;
        public event ChromatogramListFilterChangedHandler FilterChanged;

		protected void OnCellClick( DataGridViewCellMouseEventArgs e )
		{
			if( CellClick != null )
				CellClick( this, new ChromatogramListCellClickEventArgs( this, e ) );
		}

		protected void OnCellDoubleClick( DataGridViewCellMouseEventArgs e )
		{
			if( CellDoubleClick != null )
				CellDoubleClick( this, new ChromatogramListCellDoubleClickEventArgs( this, e ) );
		}

        protected void OnFilterChanged()
        {
            if( FilterChanged != null )
                FilterChanged( this, new ChromatogramListFilterChangedEventArgs( this, chromatogramDataSet ) );
        }

		public ChromatogramListForm()
		{
			InitializeComponent();

            initializeGridView();
		}

		private void initializeGridView()
		{
            chromatogramList = new Dictionary<int, Chromatogram>();

            typeDataGridViewTextBoxColumn.ToolTipText = new CVTermInfo( CVID.MS_chromatogram_type ).def;
		}

        private void gridView_DataBindingComplete( object sender, DataGridViewBindingCompleteEventArgs e )
        {
            if( e.ListChangedType == ListChangedType.Reset )
                OnFilterChanged();
        }

        public void updateRow( ChromatogramDataSet.ChromatogramTableRow row, Chromatogram chromatogram )
        {
            chromatogramList[chromatogram.Index] = chromatogram;

            pwiz.CLI.msdata.Chromatogram c = chromatogram.Element;
            DataProcessing dp = chromatogram.DataProcessing;
            if( dp == null )
                dp = c.dataProcessing;

            row.Type = c.cvParamChild( CVID.MS_chromatogram_type ).name;
            row.DataPoints = c.defaultArrayLength;
            row.DpId = ( dp == null || dp.id.Length == 0 ? "unknown" : dp.id );
        }

        public void Add( Chromatogram chromatogram )
        {
            ChromatogramDataSet.ChromatogramTableRow row = chromatogramDataSet.ChromatogramTable.NewChromatogramTableRow();
            row.Id = chromatogram.Id;

            /*if( nativeIdFormat != CVID.CVID_Unknown )
            {
                gridView.Columns["Id"].Visible = false;

                string[] nameValuePairs = chromatogram.Id.Split( " ".ToCharArray() );
                foreach( string nvp in nameValuePairs )
                {
                    string[] nameValuePair = nvp.Split( "=".ToCharArray() );
                    row[nameValuePair[0]] = nameValuePair[1];
                }
            }*/

            row.Index = chromatogram.Index;
            updateRow( row, chromatogram );
            chromatogramDataSet.ChromatogramTable.AddChromatogramTableRow( row );

            //int rowIndex = gridView.Rows.Add();
            //gridView.Rows[rowIndex].Tag = chromatogram;
            chromatogram.Tag = this;

            //UpdateRow( rowIndex );
        }

        public int IndexOf( Chromatogram chromatogram )
        {
            return chromatogramBindingSource.Find( "Index", chromatogram.Index );
        }

        public Chromatogram GetChromatogram( int index )
        {
            return chromatogramList[(int) ( chromatogramBindingSource[index] as DataRowView )[1]];
        }

        public void UpdateAllRows()
        {
            for( int i = 0; i < gridView.RowCount; ++i )
                UpdateRow( i );
        }

        public void UpdateRow( int rowIndex )
        {
            UpdateRow( rowIndex, null );
        }

        public void UpdateRow( int rowIndex, ChromatogramList chromatogramList )
        {
            ChromatogramDataSet.ChromatogramTableRow row = ( chromatogramBindingSource[rowIndex] as DataRowView ).Row as ChromatogramDataSet.ChromatogramTableRow;

            if( chromatogramList != null )
            {
                this.chromatogramList[row.Index].ChromatogramList = chromatogramList;
                updateRow( row, this.chromatogramList[row.Index] );
                //dp = rowChromatogram.DataProcessing;
                //row.Tag = rowChromatogram = new Chromatogram( rowChromatogram, s );
                //rowChromatogram.DataProcessing = dp;
            } else
            {
                updateRow( row, this.chromatogramList[row.Index] );
                //s = rowChromatogram.Element;
                //dp = rowChromatogram.DataProcessing;
            }
        }

        private void gridView_CellMouseClick( object sender, DataGridViewCellMouseEventArgs e )
        {
            //if( e.RowIndex > -1 && gridView.Columns[e.ColumnIndex] is DataGridViewLinkColumn )
            //    gridView_ShowCellToolTip( gridView[e.ColumnIndex, e.RowIndex] );
            OnCellClick( e );
        }

        private void gridView_CellMouseDoubleClick( object sender, DataGridViewCellMouseEventArgs e )
        {
            OnCellDoubleClick( e );
        }

        private void selectColumnsToolStripMenuItem_Click( object sender, EventArgs e )
        {

        }

        private void gridView_ColumnHeaderMouseClick( object sender, DataGridViewCellMouseEventArgs e )
        {
            if( e.Button == MouseButtons.Right )
            {
                Rectangle cellRectangle = gridView.GetCellDisplayRectangle( e.ColumnIndex, e.RowIndex, true );
                Point cellLocation = new Point( cellRectangle.Left, cellRectangle.Top );
                cellLocation.Offset( e.Location );
                selectColumnsMenuStrip.Show( gridView, cellLocation );
            }
        }
	}

    public class ChromatogramListCellClickEventArgs : DataGridViewCellMouseEventArgs
	{
		private Chromatogram chromatogram;
		public Chromatogram Chromatogram { get { return chromatogram; } }

		internal ChromatogramListCellClickEventArgs( ChromatogramListForm sender, DataGridViewCellMouseEventArgs e )
            : base( e.ColumnIndex, e.RowIndex, e.X, e.Y, e )
		{
            if( e.RowIndex > -1 && e.RowIndex < sender.GridView.RowCount )
                chromatogram = sender.GetChromatogram( e.RowIndex );
		}
	}

    public class ChromatogramListCellDoubleClickEventArgs : DataGridViewCellMouseEventArgs
	{
		private Chromatogram chromatogram;
		public Chromatogram Chromatogram { get { return chromatogram; } }

		internal ChromatogramListCellDoubleClickEventArgs( ChromatogramListForm sender, DataGridViewCellMouseEventArgs e )
			: base( e.ColumnIndex, e.RowIndex, e.X, e.Y, e )
		{
            if( e.RowIndex > -1 && e.RowIndex < sender.GridView.RowCount )
                chromatogram = sender.GetChromatogram( e.RowIndex );
		}
	}

    public class ChromatogramListFilterChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The number of spectra that matched the new filter.
        /// </summary>
        public int Matches { get { return matches; } }
        private int matches;

        /// <summary>
        /// The total number of spectra.
        /// </summary>
        public int Total { get { return total; } }
        private int total;

        internal ChromatogramListFilterChangedEventArgs( ChromatogramListForm sender, ChromatogramDataSet dataSet )
        {
            matches = sender.GridView.RowCount;
            total = dataSet.ChromatogramTable.Count;
        }
    }
}