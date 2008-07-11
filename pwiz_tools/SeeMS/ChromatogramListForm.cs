using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.CLI.msdata;

namespace seems
{
	public delegate void ChromatogramListCellClickHandler( object sender, ChromatogramListCellClickEventArgs e );
	public delegate void ChromatogramListCellDoubleClickHandler( object sender, ChromatogramListCellDoubleClickEventArgs e );

	public partial class ChromatogramListForm : DockableForm
	{
		private List<Chromatogram> chromatogramList;
		public DataGridView GridView { get { return gridView; } }

		public event ChromatogramListCellClickHandler CellClick;
		public event ChromatogramListCellDoubleClickHandler CellDoubleClick;

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

		public ChromatogramListForm()
		{
			InitializeComponent();

			chromatogramList = new List<Chromatogram>();
		}

		public ChromatogramListForm( List<Chromatogram> chromatograms )
		{
			InitializeComponent();

			chromatogramList = chromatograms;
			initializeGridView();
		}

		private void initializeGridView()
		{
			foreach( Chromatogram chromatogram in chromatogramList )
			{
				Add( chromatogram );
			}

		}

		public void Add( Chromatogram chromatogram )
		{
			pwiz.CLI.msdata.Chromatogram c = chromatogram.Element;
			int rowIndex = gridView.Rows.Add(
				c.id, c.nativeID, c.index,
				c.cvParamChild( CVID.MS_chromatogram_type ).name
			);
			gridView.Rows[rowIndex].Tag = chromatogram;
            chromatogram.Tag = gridView.Rows[rowIndex];
		}

		private void gridView_CellMouseClick( object sender, DataGridViewCellMouseEventArgs e )
		{
			OnCellClick( e );
		}

		private void gridView_CellMouseDoubleClick( object sender, DataGridViewCellMouseEventArgs e )
		{
			OnCellDoubleClick( e );
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
			    chromatogram = sender.GridView.Rows[e.RowIndex].Tag as Chromatogram;
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
			    chromatogram = sender.GridView.Rows[e.RowIndex].Tag as Chromatogram;
		}
	}
}