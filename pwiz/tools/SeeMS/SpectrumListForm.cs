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
			int rowIndex = gridView.Rows.Add(
				s.id, s.nativeID, s.index,
				s.cvParamChild( CVID.MS_spectrum_type ).name,
                Convert.ToInt32( (string) s.cvParam( CVID.MS_ms_level ).value ),
                Convert.ToDouble( (string) scan.cvParam( CVID.MS_scan_time ).value ),
				( ic != null ? ic.id : "unknown" ),
                Convert.ToDouble( (string) sd.cvParam( CVID.MS_base_peak_m_z ).value ),
                Convert.ToDouble( (string) sd.cvParam( CVID.MS_base_peak_intensity ).value ),
				Convert.ToDouble( (string) sd.cvParam( CVID.MS_total_ion_current ).value ),
				( dp != null ? dp.id : "n/a" ),
				scan.cvParamChild( CVID.MS_polarity ).name,
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
	}

	public class SpectrumListCellClickEventArgs : MouseEventArgs
	{
		private MassSpectrum spectrum;
		public MassSpectrum Spectrum { get { return spectrum; } }

		internal SpectrumListCellClickEventArgs( SpectrumListForm sender, DataGridViewCellMouseEventArgs e )
			: base(e.Button, e.Clicks, e.X, e.Y, e.Delta)
		{
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
			spectrum = sender.GridView.Rows[e.RowIndex].Tag as MassSpectrum;
		}
	}
}