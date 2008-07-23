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
        private Timer hoverTimer;
        private Timer leaveTimer;
        private TreeViewForm treeViewToolTipForm;
        private DataGridViewCell hoverCell;

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
            initializeGridView();
		}

		public SpectrumListForm( List<MassSpectrum> spectra )
		{
			InitializeComponent();

			spectrumList = spectra;
			initializeGridView();
		}

		private void initializeGridView()
		{
            gridView.Columns["SpectrumType"].ToolTipText = new CVInfo(CVID.MS_spectrum_type).def;
            gridView.Columns["msLevel"].ToolTipText = new CVInfo( CVID.MS_ms_level ).def;
            gridView.Columns["ScanTime"].ToolTipText = new CVInfo( CVID.MS_scan_time ).def;
            gridView.Columns["BasePeakMZ"].ToolTipText = new CVInfo( CVID.MS_base_peak_m_z ).def;
            gridView.Columns["BasePeakIntensity"].ToolTipText = new CVInfo( CVID.MS_base_peak_intensity ).def;
            gridView.Columns["TotalIntensity"].ToolTipText = new CVInfo( CVID.MS_total_ion_current ).def;
            gridView.Columns["Polarity"].ToolTipText = new CVInfo( CVID.MS_polarity ).def;

			foreach( MassSpectrum spectrum in spectrumList )
			{
				Add( spectrum );
			}

            hoverTimer = new Timer();
            hoverTimer.Tick += new EventHandler( hoverTimer_Tick );
            hoverCell = null;

            treeViewToolTipForm = new TreeViewForm();
            treeViewToolTipForm.MouseLeave += new EventHandler( treeViewForm_MouseLeave );

            leaveTimer = new Timer();
            leaveTimer.Interval = 50;
            leaveTimer.Tick += new EventHandler( leaveTimer_Tick );
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

            StringBuilder precursorInfo = new StringBuilder();
            if( sd.precursors.Count == 0 )
                precursorInfo.Append( "n/a" );
            else
            {
                foreach( Precursor p in sd.precursors )
                {
                    foreach( SelectedIon si in p.selectedIons )
                    {
                        if( precursorInfo.Length > 0 )
                            precursorInfo.Append( "," );
                        precursorInfo.Append( (double) si.cvParam( CVID.MS_m_z ).value );
                    }
                }
            }

            if( precursorInfo.Length == 0 )
                precursorInfo.Append( "unknown" );

            StringBuilder scanInfo = new StringBuilder();
            if( sd.acquisitionList.acquisitions.Count > 0 )
                foreach( Acquisition a in sd.acquisitionList.acquisitions )
                {
                    if( scanInfo.Length > 0 )
                        scanInfo.Append( ",");
                    scanInfo.Append( a.number.ToString());
                }
            if( scan.scanWindows.Count > 0 )
            {
                foreach( ScanWindow sw in scan.scanWindows )
                {
                    if( scanInfo.Length > 0 )
                        scanInfo.Append( "," );
                    scanInfo.AppendFormat( "[{0}-{1}]",
                                          (double) sw.cvParam( CVID.MS_scan_m_z_lower_limit ).value,
                                          (double) sw.cvParam( CVID.MS_scan_m_z_upper_limit ).value );
                }
            }

            if( scanInfo.Length == 0 )
                scanInfo.Append( "unknown" );

			int rowIndex = gridView.Rows.Add(
				s.id, s.nativeID, s.index, s.spotID,
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
				precursorInfo.ToString(),
                scanInfo.ToString()
			);
			gridView.Rows[rowIndex].Tag = spectrum;
            spectrum.Tag = gridView.Rows[rowIndex];

            if( s.spotID.Length > 0 )
                gridView.Columns["SpotId"].Visible = true;
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

        private void addParamsToTreeNode( ParamContainer pc, TreeNode node)
        {
            foreach( ParamGroup pg in pc.paramGroups )
                addParamsToTreeNode( pg as ParamContainer, node);

            foreach( CVParam param in pc.cvParams )
            {
                if( param.empty() )
                    continue;

                string nodeText;
                if( param.value.ToString().Length > 0 )
                {
                    // has value
                    if( param.units != CVID.CVID_Unknown )
                    {
                        // has value and units
                        nodeText = String.Format( "{0}: {1} {2}", param.name, param.value, param.unitsName);
                    } else
                    {
                        // has value but no units
                        nodeText = String.Format( "{0}: {1}", param.name, param.value);
                    }
                } else
                {
                    // has controlled value, look up category in the CV
                    nodeText = String.Format( "{0}: {1}", new CVInfo(new CVInfo(param.cvid).parentsIsA[0]).name, param.name);
                }
                TreeNode childNode = node.Nodes.Add(nodeText);
                childNode.ToolTipText = new CVInfo( param.cvid ).def;
            }

            foreach( UserParam param in pc.userParams )
            {
                string nodeText;
                if( param.value.ToString().Length > 0 )
                {
                    // has value
                    if( param.units != CVID.CVID_Unknown )
                    {
                        // has value and units
                        nodeText = String.Format( "{0}: {1} {2}", param.name, param.value, new CVInfo(param.units).name);
                    } else
                    {
                        // has value but no units
                        nodeText = String.Format( "{0}: {1}", param.name, param.value);
                    }
                } else
                {
                    // has uncontrolled value
                    nodeText = String.Format( "{0}", param.name);
                }
                TreeNode childNode = node.Nodes.Add(nodeText);
                childNode.ToolTipText = param.type;
            }
        }

        private void gridView_ShowCellToolTip( DataGridViewCell cell )
        {
            MassSpectrum spectrum = cell.OwningRow.Tag as MassSpectrum;
            Spectrum s = spectrum.Element;
            SpectrumDescription sd = s.spectrumDescription;

            TreeView tv = treeViewToolTipForm.TreeView;
            tv.Nodes.Clear();

            if( gridView.Columns[cell.ColumnIndex].Name == "PrecursorInfo" )
            {
                if( sd.precursors.Count == 0 )
                    tv.Nodes.Add( "No precursor information available." );
                else
                {
                    foreach( Precursor p in sd.precursors )
                    {
                        string pNodeText;
                        if( p.sourceFile != null )
                        {
                            if( p.externalNativeID.Length > 0 )
                                pNodeText = String.Format( "Precursor scan: {0}:{1}", p.sourceFile.name, p.externalNativeID );
                            else
                                pNodeText = String.Format( "Precursor scan: {0}:{1}", p.sourceFile.name, p.externalSpectrumID );
                        } else
                            pNodeText = String.Format( "Precursor scan: {0}", p.spectrumID );

                        TreeNode pNode = tv.Nodes.Add( pNodeText );
                        addParamsToTreeNode( p as ParamContainer, pNode );

                        if( p.selectedIons.Count == 0 )
                            pNode.Nodes.Add( "No selected ion list available." );
                        else
                        {
                            foreach( SelectedIon si in p.selectedIons )
                            {
                                TreeNode siNode = pNode.Nodes.Add( "Selected ion" );
                                //siNode.ToolTipText = new CVInfo(CVID.MS_selected_ion); // not yet in CV
                                addParamsToTreeNode( si as ParamContainer, siNode );
                            }
                        }

                        if( p.activation.empty() )
                            pNode.Nodes.Add( "No activation details available." );
                        else
                        {
                            TreeNode actNode = pNode.Nodes.Add( "Activation" );
                            addParamsToTreeNode( p.activation as ParamContainer, actNode );
                        }

                        if( p.isolationWindow.empty() )
                            pNode.Nodes.Add( "No isolation window details available." );
                        else
                        {
                            TreeNode iwNode = pNode.Nodes.Add( "Isolation Window" );
                            addParamsToTreeNode( p.isolationWindow as ParamContainer, iwNode );
                        }
                    }
                }
            } else if( gridView.Columns[cell.ColumnIndex].Name == "ScanInfo" )
            {
                if( sd.scan.empty() )
                    tv.Nodes.Add( "No scan details available." );
                else
                {
                    TreeNode scanNode = tv.Nodes.Add( "Scan" );
                    addParamsToTreeNode( sd.scan as ParamContainer, scanNode );
                    foreach( ScanWindow sw in sd.scan.scanWindows )
                    {
                        TreeNode swNode = scanNode.Nodes.Add( "Scan Window" );
                        addParamsToTreeNode( sw as ParamContainer, swNode );
                    }
                }

                if( sd.acquisitionList.empty() )
                    tv.Nodes.Add( "No acquisition list available." );
                else
                {
                    TreeNode alNode = tv.Nodes.Add( "Acquisition List" );
                    addParamsToTreeNode( sd.acquisitionList as ParamContainer, alNode );

                    foreach( Acquisition a in sd.acquisitionList.acquisitions )
                    {
                        TreeNode acqNode = alNode.Nodes.Add( "Acquisition" );
                        addParamsToTreeNode( a as ParamContainer, acqNode );
                    }
                }
            } else if( gridView.Columns[cell.ColumnIndex].Name == "InstrumentConfigurationID" )
            {
                InstrumentConfiguration ic = sd.scan.instrumentConfiguration;
                if( ic == null || ic.empty() )
                    tv.Nodes.Add( "No instrument configuration details available." );
                else
                {
                    TreeNode icNode = tv.Nodes.Add( String.Format( "Instrument Configuration ({0})", ic.id ) );
                    addParamsToTreeNode( ic as ParamContainer, icNode );

                    if( ic.componentList.Count == 0 )
                        icNode.Nodes.Add( "No component list available." );
                    else
                    {
                        TreeNode clNode = icNode.Nodes.Add( "Component List" );
                        foreach( pwiz.CLI.msdata.Component c in ic.componentList )
                        {
                            string cNodeText;
                            switch( c.type )
                            {
                                case ComponentType.ComponentType_Source:
                                    cNodeText = "Source";
                                    break;
                                case ComponentType.ComponentType_Analyzer:
                                    cNodeText = "Analyzer";
                                    break;
                                default:
                                case ComponentType.ComponentType_Detector:
                                    cNodeText = "Detector";
                                    break;
                            }
                            TreeNode cNode = clNode.Nodes.Add( cNodeText );
                            addParamsToTreeNode( c as ParamContainer, cNode );
                        }
                    }

                    Software sw = ic.software;
                    if( sw == null || sw.empty() )
                        icNode.Nodes.Add( "No software details available." );
                    else
                    {
                        TreeNode swNode = icNode.Nodes.Add( String.Format( "Software ({0})", sw.id ) );
                        TreeNode swNameNode = swNode.Nodes.Add( "Name: " + sw.softwareParam.name );
                        swNameNode.ToolTipText = new CVInfo( sw.softwareParam.cvid ).def;
                        swNode.Nodes.Add( "Version: " + sw.softwareParamVersion );
                    }
                }
            } else
                return;

            tv.ExpandAll();
            Point toolTipLocation = Form.MousePosition;
            toolTipLocation.Offset( -5, -5 );
            treeViewToolTipForm.Location = toolTipLocation;
            treeViewToolTipForm.DoAutoSize();
            treeViewToolTipForm.Show();
            this.Focus();
        }


        void leaveTimer_Tick( object sender, EventArgs e )
        {
            leaveTimer.Stop();
            if( !treeViewToolTipForm.Bounds.Contains( Form.MousePosition ) )
                treeViewToolTipForm.Hide();
        }

        void treeViewForm_MouseLeave( object sender, EventArgs e )
        {
            // set or reset hover timer
            leaveTimer.Start();
        }

        private void gridView_CellMouseEnter( object sender, DataGridViewCellEventArgs e )
        {
            if( e.RowIndex < 0 || e.ColumnIndex < 0 )
            {
                hoverTimer.Stop();
                hoverCell = null;
                return;
            }

            // set or reset hover timer
            hoverTimer.Interval = 1000;
            hoverTimer.Start();
            hoverCell = gridView[e.ColumnIndex, e.RowIndex];
        }

        private void hoverTimer_Tick( object sender, EventArgs e )
        {
            hoverTimer.Stop();

            if( hoverCell == null )
                return;

            Rectangle cellRectangle = gridView.GetCellDisplayRectangle( hoverCell.ColumnIndex, hoverCell.RowIndex, true );
            //Point cellLocation = new Point( cellRectangle.Left, cellRectangle.Top );
            //cellLocation.Offset( e.Location );
            Point mousePt = Form.MousePosition;
            if( !cellRectangle.Contains( gridView.PointToClient( mousePt ) ) )
                return;

            gridView_ShowCellToolTip( hoverCell );
        }
	}

    public class SpectrumListCellClickEventArgs : DataGridViewCellMouseEventArgs
	{
		private MassSpectrum spectrum;
		public MassSpectrum Spectrum { get { return spectrum; } }

		internal SpectrumListCellClickEventArgs( SpectrumListForm sender, DataGridViewCellMouseEventArgs e )
            : base( e.ColumnIndex, e.RowIndex, e.X, e.Y, e )
		{
            if( e.RowIndex > -1 && e.RowIndex < sender.GridView.RowCount )
			    spectrum = sender.GridView.Rows[e.RowIndex].Tag as MassSpectrum;
		}
	}

    public class SpectrumListCellDoubleClickEventArgs : DataGridViewCellMouseEventArgs
	{
		private MassSpectrum spectrum;
		public MassSpectrum Spectrum { get { return spectrum; } }

		internal SpectrumListCellDoubleClickEventArgs( SpectrumListForm sender, DataGridViewCellMouseEventArgs e )
            : base( e.ColumnIndex, e.RowIndex, e.X, e.Y, e )
		{
            if( e.RowIndex > -1 && e.RowIndex < sender.GridView.RowCount )
			    spectrum = sender.GridView.Rows[e.RowIndex].Tag as MassSpectrum;
		}
	}
}