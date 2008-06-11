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
	public delegate void SpectrumListCellClickHandler( object sender, SpectrumListCellClickEventArgs e );
	public delegate void SpectrumListCellDoubleClickHandler( object sender, SpectrumListCellDoubleClickEventArgs e );

	public partial class SpectrumListForm : DockableForm
	{
		private List<MassSpectrum> spectrumList;
		public DataGridView GridView { get { return gridView; } }

		public event SpectrumListCellClickHandler CellClick;
		public event SpectrumListCellDoubleClickHandler CellDoubleClick;

		protected void OnCellClick( DataGridViewCellMouseEventArgs e )
		{
			if( CellClick != null )
				CellClick( this, new SpectrumListCellClickEventArgs( this, e ) );
		}

		protected void OnCellDoubleClick( DataGridViewCellMouseEventArgs e )
		{
			if( CellDoubleClick != null )
				CellDoubleClick( this, new SpectrumListCellDoubleClickEventArgs( this, e ) );
		}

		public SpectrumListForm()
		{
			InitializeComponent();

			spectrumList = new List<MassSpectrum>();
		}

		public SpectrumListForm( List<MassSpectrum> spectra )
		{
			InitializeComponent();

			spectrumList = spectra;
			initializeGridView();
		}

		private void initializeGridView()
		{
			foreach( MassSpectrum spectrum in spectrumList )
			{
				Add( spectrum );
			}

		}

		public void Add( MassSpectrum spectrum )
		{
			Spectrum s = spectrum.Element;
			SpectrumDescription sd = s.spectrumDescription;
			Scan scan = sd.scan;
			InstrumentConfiguration ic = scan.instrumentConfiguration;
			DataProcessing dp = s.dataProcessing;

            CVParam param;

            param = s.cvParam( CVID.MS_ms_level );
            int msLevel = param.cvid != CVID.CVID_Unknown ? Convert.ToInt32( (string) param.value ) : 0;

            param = scan.cvParam( CVID.MS_scan_time );
            double scanTime = param.cvid != CVID.CVID_Unknown ? Convert.ToDouble( (string) param.value ) : 0;

            param = sd.cvParam( CVID.MS_base_peak_m_z );
            double bpmz = param.cvid != CVID.CVID_Unknown ? Convert.ToDouble( (string) param.value ) : 0;

            param = sd.cvParam( CVID.MS_base_peak_intensity );
            double bpi = param.cvid != CVID.CVID_Unknown ? Convert.ToDouble( (string) param.value ) : 0;

            param = sd.cvParam( CVID.MS_total_ion_current );
            double tic = param.cvid != CVID.CVID_Unknown ? Convert.ToDouble( (string) param.value ) : 0;

            param = scan.cvParamChild( CVID.MS_polarity );
            string polarity = param.cvid != CVID.CVID_Unknown ? param.name : "unknown";

			int rowIndex = gridView.Rows.Add(
				s.id, s.nativeID, s.index,
				s.cvParamChild( CVID.MS_spectrum_type ).name,
                msLevel,
                scanTime,
                s.defaultArrayLength,
				( ic != null ? ic.id : "unknown" ),
                bpmz,
                bpi,
                tic,
				( dp != null ? dp.id : "unknown" ),
                polarity,
				"",
				""
			);
			gridView.Rows[rowIndex].Tag = spectrum;
            spectrum.Tag = gridView.Rows[rowIndex];
		}

		private void gridView_CellMouseClick( object sender, DataGridViewCellMouseEventArgs e )
		{
			OnCellClick( e );
		}

		private void gridView_CellMouseDoubleClick( object sender, DataGridViewCellMouseEventArgs e )
		{
			OnCellDoubleClick( e );
		}

        private void selectColumnsToolStripMenuItem_Click( object sender, EventArgs e )
        {
            SelectColumnsDialog dialog = new SelectColumnsDialog( gridView );
            dialog.ShowDialog();
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

	public class SpectrumListCellClickEventArgs : MouseEventArgs
	{
		private MassSpectrum spectrum;
		public MassSpectrum Spectrum { get { return spectrum; } }

		internal SpectrumListCellClickEventArgs( SpectrumListForm sender, DataGridViewCellMouseEventArgs e )
			: base(e.Button, e.Clicks, e.X, e.Y, e.Delta)
		{
            if( e.RowIndex > -1 && e.RowIndex < sender.GridView.RowCount )
			    spectrum = sender.GridView.Rows[e.RowIndex].Tag as MassSpectrum;
		}
	}

	public class SpectrumListCellDoubleClickEventArgs : MouseEventArgs
	{
		private MassSpectrum spectrum;
		public MassSpectrum Spectrum { get { return spectrum; } }

		internal SpectrumListCellDoubleClickEventArgs( SpectrumListForm sender, DataGridViewCellMouseEventArgs e )
			: base( e.Button, e.Clicks, e.X, e.Y, e.Delta )
		{
            if( e.RowIndex > -1 && e.RowIndex < sender.GridView.RowCount )
			    spectrum = sender.GridView.Rows[e.RowIndex].Tag as MassSpectrum;
		}
	}
}