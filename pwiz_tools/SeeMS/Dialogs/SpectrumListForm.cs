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
using System.Text.RegularExpressions;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.CLI.cv;
using pwiz.CLI.data;
using pwiz.CLI.msdata;
using pwiz.CLI.analysis;
using seems.Misc;

namespace seems
{
	public delegate void SpectrumListCellClickHandler( object sender, SpectrumListCellClickEventArgs e );
	public delegate void SpectrumListCellDoubleClickHandler( object sender, SpectrumListCellDoubleClickEventArgs e );
    public delegate void SpectrumListFilterChangedHandler( object sender, SpectrumListFilterChangedEventArgs e );

	public partial class SpectrumListForm : DockableForm
	{
        Dictionary<int, MassSpectrum> spectrumList; // indexable by MassSpectrum.Index

		public DataGridView GridView { get { return gridView; } }

		public event SpectrumListCellClickHandler CellClick;
		public event SpectrumListCellDoubleClickHandler CellDoubleClick;
        public event SpectrumListFilterChangedHandler FilterChanged;

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

        protected void OnFilterChanged()
        {
            if( FilterChanged != null )
                FilterChanged( this, new SpectrumListFilterChangedEventArgs( this, spectrumDataSet ) );
        }

        public SpectrumListForm( CVID nativeIdFormat )
		{
			InitializeComponent();

            initializeGridView( nativeIdFormat );
		}

        private CVID nativeIdFormat = CVID.CVID_Unknown;
        public CVID NativeIdFormat { get { return nativeIdFormat; } }

        private bool hasMergedSpectra = false;
        private bool hasNativeIdSpectra = false;

		private void initializeGridView( CVID nativeIdFormat )
		{
            // force handle creation
            IntPtr dummy = gridView.Handle;
            spectrumList = new Dictionary<int, MassSpectrum>();

            this.nativeIdFormat = nativeIdFormat;
            if ((nativeIdFormat != CVID.CVID_Unknown) && (nativeIdFormat != CVID.MS_no_nativeID_format))
            {
                string nativeIdDefinition = new CVTermInfo( nativeIdFormat ).def.Replace("Native format defined by ", "");
                string[] nameValuePairs = nativeIdDefinition.Split( " ".ToCharArray() );
                for( int i = 0; i < nameValuePairs.Length; ++i )
                {
                    string[] nameValuePair = nameValuePairs[i].Split( "=".ToCharArray() );
                    DataGridViewColumn nameColumn = new DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn();
                    nameColumn.Name = nameValuePair[0] + "NativeIdColumn";
                    nameColumn.HeaderText = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase( nameValuePair[0] );
                    nameColumn.DataPropertyName = nameValuePair[0];
                    gridView.Columns.Insert( 1 + i, nameColumn );
                    string type = null;
                    if( nameValuePair[0] == "file" || nameValuePair[1] == "xsd:string" )
                        type = "System.String";
                    else
                        type = "System.Int32";
                    spectrumDataSet.SpectrumTable.Columns.Add( nameValuePair[0], Type.GetType( type ) );
                }
            }

            gridView.Columns["SpectrumType"].ToolTipText = new CVTermInfo(CVID.MS_spectrum_type).def;
            gridView.Columns["MsLevel"].ToolTipText = new CVTermInfo( CVID.MS_ms_level ).def;
            gridView.Columns["ScanTime"].ToolTipText = new CVTermInfo( CVID.MS_scan_start_time ).def;
            gridView.Columns["BasePeakMz"].ToolTipText = new CVTermInfo( CVID.MS_base_peak_m_z ).def;
            gridView.Columns["BasePeakIntensity"].ToolTipText = new CVTermInfo( CVID.MS_base_peak_intensity ).def;
            gridView.Columns["TotalIonCurrent"].ToolTipText = new CVTermInfo( CVID.MS_total_ion_current ).def;

	        gridView.Columns["ScanTime"].HeaderText += Properties.Settings.Default.TimeInMinutes ? " (min)" : " (sec)";

            gridView.DataBindingComplete += new DataGridViewBindingCompleteEventHandler( gridView_DataBindingComplete );
        }

        void gridView_DataBindingComplete( object sender, DataGridViewBindingCompleteEventArgs e )
        {
            if( e.ListChangedType == ListChangedType.Reset )
                OnFilterChanged();
        }

        public void updateRow( SpectrumDataSet.SpectrumTableRow row, MassSpectrum spectrum )
        {
            spectrumList[spectrum.Index] = spectrum;

            Spectrum s = spectrum.Element; //GetElement(false);
            DataProcessing dp = spectrum.DataProcessing;
            Scan scan = null;
            InstrumentConfiguration ic = null;

            if(s.scanList.scans.Count > 0)
            {
                scan = s.scanList.scans[0];
                ic = scan.instrumentConfiguration;
            }

            if( dp == null )
                dp = s.dataProcessing;

            CVParam param;

            param = s.cvParam( CVID.MS_ms_level );
            row.MsLevel = !param.empty() ? (int) param.value : 0;

            param = scan != null ? scan.cvParam( CVID.MS_scan_start_time ) : new CVParam();
            row.ScanTime = !param.empty() ? param.timeInSeconds() : 0;
            if (Properties.Settings.Default.TimeInMinutes)
                row.ScanTime /= 60;

            param = s.cvParam( CVID.MS_base_peak_m_z );
            row.BasePeakMz = !param.empty() ? (double) param.value : 0;

            param = s.cvParam( CVID.MS_base_peak_intensity );
            row.BasePeakIntensity = !param.empty() ? (double) param.value : 0;

            param = s.cvParam( CVID.MS_total_ion_current );
            row.TotalIonCurrent = !param.empty() ? (double) param.value : 0;

            var precursorInfo = new StringBuilder();
            var isolationWindows = new StringBuilder();
            if( row.MsLevel == 1 || s.precursors.Count == 0 )
            {
                precursorInfo.Append( "n/a" );
                isolationWindows.Append( "n/a" );
            }
            else
            {
                foreach( Precursor p in s.precursors )
                {
                    foreach( SelectedIon si in p.selectedIons )
                    {
                        if( precursorInfo.Length > 0 )
                            precursorInfo.Append( "," );
                        precursorInfo.AppendFormat("{0:G8}", (double) si.cvParam( CVID.MS_selected_ion_m_z ).value );
                    }

                    var iw = p.isolationWindow;
                    CVParam isolationTarget = iw.cvParam(CVID.MS_isolation_window_target_m_z);
                    if (!isolationTarget.empty())
                    {
                        double iwMz = (double) isolationTarget.value;

                        if (isolationWindows.Length > 0)
                            isolationWindows.Append(",");

                        CVParam lowerOffset = iw.cvParam(CVID.MS_isolation_window_lower_offset);
                        CVParam upperOffset = iw.cvParam(CVID.MS_isolation_window_upper_offset);
                        if (lowerOffset.empty() || upperOffset.empty())
                            isolationWindows.AppendFormat("{0:G8}", iwMz);
                        else
                            isolationWindows.AppendFormat("[{0:G8}-{1:G8}]", iwMz - (double)lowerOffset.value, iwMz + (double)upperOffset.value);
                    }
                }
            }

            if( precursorInfo.Length == 0 )
                precursorInfo.Append( "unknown" );
            row.PrecursorInfo = precursorInfo.ToString();

            if (isolationWindows.Length == 0)
                isolationWindows.Append("unknown");
            row.IsolationWindows = isolationWindows.ToString();

            StringBuilder scanInfo = new StringBuilder();
            foreach( Scan scan2 in s.scanList.scans )
            {
                if( scan2.scanWindows.Count > 0 )
                {
                    foreach( ScanWindow sw in scan2.scanWindows )
                    {
                        if( scanInfo.Length > 0 )
                            scanInfo.Append( "," );
                        scanInfo.AppendFormat( "[{0:G8}-{1:G8}]",
                                              (double) sw.cvParam( CVID.MS_scan_window_lower_limit ).value,
                                              (double) sw.cvParam( CVID.MS_scan_window_upper_limit ).value );
                    }
                }
            }

            if( scanInfo.Length == 0 )
                scanInfo.Append( "unknown" );
            row.ScanInfo = scanInfo.ToString();

            row.IonMobility = scan != null ? (double) scan.cvParam(CVID.MS_ion_mobility_drift_time).value : 0;
            if (row.IonMobility == 0 && scan != null)
            {
                row.IonMobility = (double) scan.cvParam(CVID.MS_inverse_reduced_ion_mobility).value;
                if (row.IonMobility == 0)
                {
                    // Early version of drift time info, before official CV params
                    var userparam = scan.userParam("drift time");
                    if (!userparam.empty())
                        row.IonMobility = userparam.timeInSeconds() * 1000.0;

                }
            }

            if (row.IonMobility == 0)
            {
                row.IonMobility = (double) s.cvParam(CVID.MS_FAIMS_compensation_voltage).value;
                row.IonMobilityType = SpectrumDataSet.IonMobilityType_CompensationVoltage;
            }

            if (row.IonMobilityType == SpectrumDataSet.IonMobilityType_None && row.IonMobility != 0)
                row.IonMobilityType = SpectrumDataSet.IonMobilityType_SingleValue;
            else if (s.id.Contains("frame=") || s.id.Contains("block=") || s.GetIonMobilityArray() != null)
                row.IonMobilityType = SpectrumDataSet.IonMobilityType_Array;

            row.SpotId = s.spotID;
            row.SpectrumType = s.cvParamChild( CVID.MS_spectrum_type ).name;
            row.DataPoints = s.defaultArrayLength;
            row.IcId = ( ic == null || ic.id.Length == 0 ? "unknown" : ic.id );
            row.DpId = ( dp == null || dp.id.Length == 0 ? "unknown" : dp.id );
        }

        public IEnumerable<SpectrumDataSet.SpectrumTableRow> GetIonMobilityRows()
        {
            var dgv = GridView;

            foreach (DataGridViewRow row in dgv.Rows)
            {
                var spectrumRow = (SpectrumDataSet.SpectrumTableRow)((DataRowView)row.DataBoundItem).Row;
                if (spectrumRow.IonMobilityType == SpectrumDataSet.IonMobilityType_None ||
                    spectrumRow.IonMobilityType == SpectrumDataSet.IonMobilityType_CompensationVoltage)
                    continue;

                if (spectrumRow.DataPoints == 0 ||
                    (spectrumRow.IonMobilityType == Misc.SpectrumDataSet.IonMobilityType_SingleValue && spectrumRow.IonMobility == 0))
                    continue;

                yield return spectrumRow;
            }
        }

        public void BeginBulkLoad()
        {
            spectrumDataSet.SpectrumTable.BeginLoadData();
            spectraSource.SuspendBinding();
            spectraSource.RaiseListChangedEvents = false;
        }

        public void EndBulkLoad()
        {
            spectrumDataSet.SpectrumTable.EndLoadData();
            spectraSource.ResumeBinding();
            spectraSource.RaiseListChangedEvents = true;
            spectraSource.ResetBindings(true);
        }

		public void Add( MassSpectrum spectrum )
		{
            SpectrumDataSet.SpectrumTableRow row = spectrumDataSet.SpectrumTable.NewSpectrumTableRow();
            row.Id = spectrum.Id;

            if (spectrum.Id.StartsWith("merged="))
            {
                if (!hasMergedSpectra)
                {
                    hasMergedSpectra = true;
                    gridView.Columns["Id"].Visible = true;
                }

                if (!hasNativeIdSpectra)
                {
                    foreach (DataGridViewColumn column in gridView.Columns)
                        if (column.Name.EndsWith("NativeIdColumn"))
                            column.Visible = false;
                }
            }
            else if( nativeIdFormat != CVID.CVID_Unknown )
            {
                if (!hasMergedSpectra)
                    gridView.Columns["Id"].Visible = false;

                if (!hasNativeIdSpectra)
                {
                    hasNativeIdSpectra = true;
                    foreach (DataGridViewColumn column in gridView.Columns)
                        if (column.Name.EndsWith("NativeIdColumn"))
                            column.Visible = true;
                }

                // guard against case where input is mzXML which
                // is identified as, say, Agilent-derived, but 
                // which uses "scan" (as mzXML must) instead
                // of the Agilent "scanID" (as this mzML-centric code expects)
                bool foundit = false;

                string[] nameValuePairs = spectrum.Id.Split(' ');
                foreach( string nvp in nameValuePairs )
                {
                    string[] nameValuePair = nvp.Split('=');
                    if (row.Table.Columns.Contains(nameValuePair[0]))
                    {
                        row[nameValuePair[0]] = nameValuePair[1];
                        foundit = true;
                    }
                }
                if (!foundit)
                {
                    // mismatch between nativeID format and actual (probably mzXML) format
                    // better to show an ill-fit match - eg "scan" (mzXML) and "scanID" (Agilent)
                    // than no info at all
                    string nativeIdDefinition = new CVTermInfo(nativeIdFormat).def;
                    string[] idPair = nativeIdDefinition.Split('=');
                    if (row.Table.Columns.Contains(idPair[0]))
                    {
                        string[] valPair = spectrum.Id.Split('=');
                        row[idPair[0]] = (valPair.Length > 1) ? valPair[1] : spectrum.Id;
                        foundit = true;
                    }
                }
            }

            row.Index = spectrum.Index;
            updateRow( row, spectrum );
            spectrumDataSet.SpectrumTable.LoadDataRow(row.ItemArray, true);

            //int rowIndex = gridView.Rows.Add();
			//gridView.Rows[rowIndex].Tag = spectrum;
            spectrum.Tag = this;


            if (row.IonMobility != 0)
                gridView.Columns["IonMobility"].Visible = true;

            if( spectrum.Element.spotID.Length > 0 )
                gridView.Columns["SpotId"].Visible = true;

            //UpdateRow( rowIndex );
		}

        public int IndexOf( MassSpectrum spectrum )
        {
            return spectraSource.Find( "Id", spectrum.Id );
        }

        public MassSpectrum GetSpectrum( int index )
        {
            return spectrumList[(int) (spectraSource[index] as DataRowView)[1]];
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

        public void UpdateRow( int rowIndex, SpectrumList spectrumList )
        {
            SpectrumDataSet.SpectrumTableRow row = ( spectraSource[rowIndex] as DataRowView ).Row as SpectrumDataSet.SpectrumTableRow;

            if( spectrumList != null )
            {
                this.spectrumList[row.Index].SpectrumList = spectrumList;
                updateRow( row, this.spectrumList[row.Index] );
                //dp = rowSpectrum.DataProcessing;
                //row.Tag = rowSpectrum = new MassSpectrum( rowSpectrum, s );
                //rowSpectrum.DataProcessing = dp;
            } else
            {
                updateRow( row, this.spectrumList[row.Index] );
                //s = rowSpectrum.Element;
                //dp = rowSpectrum.DataProcessing;
            }
        }

		private void gridView_CellMouseClick( object sender, DataGridViewCellMouseEventArgs e )
		{
            if( e.RowIndex > -1 && gridView.Columns[e.ColumnIndex] is DataGridViewLinkColumn )
                gridView_ShowCellToolTip( gridView[e.ColumnIndex, e.RowIndex] );
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
                    nodeText = String.Format( "{0}: {1}", new CVTermInfo(new CVTermInfo(param.cvid).parentsIsA[0]).name, param.name);
                }
                TreeNode childNode = node.Nodes.Add(nodeText);
                childNode.ToolTipText = new CVTermInfo( param.cvid ).def;
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
                        nodeText = String.Format( "{0}: {1} {2}", param.name, param.value, new CVTermInfo(param.units).name);
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

            TreeViewForm treeViewForm = new TreeViewForm( spectrum );
            TreeView tv = treeViewForm.TreeView;

            if( gridView.Columns[cell.ColumnIndex].Name == "PrecursorInfo" )
            {
                treeViewForm.Text = "Precursor Details";
                if( s.precursors.Count == 0 )
                    tv.Nodes.Add( "No precursor information available." );
                else
                {
                    foreach( Precursor p in s.precursors )
                    {
                        string pNodeText = "Precursor scan";
                        if( p.sourceFile != null && p.externalSpectrumID.Length > 0 )
                            pNodeText += String.Format( ": {0}:{1}", p.sourceFile.name, p.externalSpectrumID );
                        else if( p.spectrumID.Length > 0 )
                            pNodeText += String.Format( ": {0}", p.spectrumID );

                        TreeNode pNode = tv.Nodes.Add( pNodeText );
                        addParamsToTreeNode( p as ParamContainer, pNode );

                        if( p.selectedIons.Count == 0 )
                            pNode.Nodes.Add( "No selected ion list available." );
                        else
                        {
                            foreach( SelectedIon si in p.selectedIons )
                            {
                                TreeNode siNode = pNode.Nodes.Add( "Selected ion" );
                                //siNode.ToolTipText = new CVTermInfo(CVID.MS_selected_ion); // not yet in CV
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
                treeViewForm.Text = "Scan Configuration Details";
                if( s.scanList.empty() )
                    tv.Nodes.Add( "No scan details available." );
                else
                {
                    TreeNode slNode = tv.Nodes.Add( "Scan List" );
                    addParamsToTreeNode( s.scanList as ParamContainer, slNode );

                    foreach( Scan scan in s.scanList.scans )
                    {
                        TreeNode scanNode = slNode.Nodes.Add( "Acquisition" );
                        addParamsToTreeNode( scan as ParamContainer, scanNode );

                        foreach( ScanWindow sw in scan.scanWindows )
                        {
                            TreeNode swNode = scanNode.Nodes.Add( "Scan Window" );
                            addParamsToTreeNode( sw as ParamContainer, swNode );
                        }
                    }
                }
            } else if( gridView.Columns[cell.ColumnIndex].Name == "InstrumentConfigurationID" )
            {
                treeViewForm.Text = "Instrument Configuration Details";
                InstrumentConfiguration ic = s.scanList.scans[0].instrumentConfiguration;
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
                        CVParam softwareParam = sw.cvParamChild( CVID.MS_software );
                        TreeNode swNameNode = swNode.Nodes.Add( "Name: " + softwareParam.name );
                        swNameNode.ToolTipText = new CVTermInfo( softwareParam.cvid ).def;
                        swNode.Nodes.Add( "Version: " + sw.version );
                    }
                }
            } else if( gridView.Columns[cell.ColumnIndex].Name == "DataProcessing" )
            {
                treeViewForm.Text = "Data Processing Details";
                DataProcessing dp = s.dataProcessing;
                if( dp == null || dp.empty() )
                    tv.Nodes.Add( "No data processing details available." );
                else
                {
                    TreeNode dpNode = tv.Nodes.Add( String.Format( "Data Processing ({0})", dp.id ) );

                    if( dp.processingMethods.Count == 0 )
                        dpNode.Nodes.Add( "No component list available." );
                    else
                    {
                        TreeNode pmNode = dpNode.Nodes.Add( "Processing Methods" );
                        foreach( ProcessingMethod pm in dp.processingMethods )
                        {
                            addParamsToTreeNode( pm as ParamContainer, pmNode );
                        }
                    }
                }
            } else
                return;

            tv.ExpandAll();
            treeViewForm.StartPosition = FormStartPosition.CenterParent;
            treeViewForm.AutoSize = true;
            //treeViewForm.DoAutoSize();
            treeViewForm.Show( this.DockPanel );
            //leaveTimer.Start();
            this.Focus();
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
                spectrum = sender.GetSpectrum( e.RowIndex );
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
                spectrum = sender.GetSpectrum( e.RowIndex );
		}
	}

    public class SpectrumListFilterChangedEventArgs : EventArgs
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

        internal SpectrumListFilterChangedEventArgs( SpectrumListForm sender, SpectrumDataSet dataSet )
        {
            matches = sender.GridView.RowCount;
            total = dataSet.SpectrumTable.Count;
        }
    }
}