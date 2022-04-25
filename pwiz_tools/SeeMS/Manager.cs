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
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Linq;
using DigitalRune.Windows.Docking;
using pwiz.CLI.cv;
using pwiz.CLI.data;
using pwiz.CLI.msdata;
using pwiz.CLI.analysis;
using pwiz.Common.Collections;
using pwiz.MSGraph;
using ZedGraph;

using System.Diagnostics;
using SpyTools;

namespace seems
{
	/// <summary>
	/// Maps the filepath of a data source to its associated ManagedDataSource object
	/// </summary>
	using DataSourceMap = Dictionary<string, ManagedDataSource>;
	using GraphInfoMap = Map<GraphItem, List<RefPair<DataGridViewRow, GraphForm>>>;
	using GraphInfoList = List<RefPair<DataGridViewRow, GraphForm>>;
	using GraphInfo = RefPair<DataGridViewRow, GraphForm>;


    public delegate void GraphFormGotFocusHandler (Manager sender, GraphForm graphForm);

    public delegate void LoadDataSourceProgressEventHandler (Manager sender, string status, int percentage, System.ComponentModel.CancelEventArgs e);


	/// <summary>
	/// Contains the associated spectrum and chromatogram lists for a data source
	/// </summary>
	public class ManagedDataSource : IComparable<ManagedDataSource>
	{
		public ManagedDataSource() { }

		public ManagedDataSource( SpectrumSource source )
		{
			this.source = source;

            chromatogramListForm = new ChromatogramListForm();
            chromatogramListForm.Text = source.Name + " chromatograms";
            chromatogramListForm.TabText = source.Name + " chromatograms";
            chromatogramListForm.ShowIcon = false;

            CVID nativeIdFormat = CVID.MS_scan_number_only_nativeID_format;
            foreach (SourceFile f in source.MSDataFile.fileDescription.sourceFiles)
            {
                // the first one in the list isn't necessarily useful - could be Agilent MSCalibration.bin or the like
                nativeIdFormat = f.cvParamChild(CVID.MS_native_spectrum_identifier_format).cvid;
                if (CVID.MS_no_nativeID_format != nativeIdFormat)
                    break;
            }
            spectrumListForm = new SpectrumListForm( nativeIdFormat );
            spectrumListForm.Text = source.Name + " spectra";
            spectrumListForm.TabText = source.Name + " spectra";
            spectrumListForm.ShowIcon = false;

            spectrumDataProcessing = new DataProcessing();
            //chromatogramDataProcessing = new DataProcessing();
			//graphInfoMap = new GraphInfoMap();
		}

		private SpectrumSource source;
		public SpectrumSource Source { get { return source; } }

		private SpectrumListForm spectrumListForm;
		public SpectrumListForm SpectrumListForm { get { return spectrumListForm; } }

		private ChromatogramListForm chromatogramListForm;
		public ChromatogramListForm ChromatogramListForm { get { return chromatogramListForm; } }

        public DataProcessing spectrumDataProcessing;
        //public DataProcessing chromatogramDataProcessing;

		//private GraphInfoMap graphInfoMap;
		//public GraphInfoMap GraphInfoMap { get { return graphInfoMap; } }

        public Chromatogram GetChromatogram( int index )
        { return GetChromatogram( index, source.MSDataFile.run.chromatogramList ); }

        public Chromatogram GetChromatogram( int index, ChromatogramList chromatogramList )
        { return new Chromatogram( this, index, chromatogramList ); }

        public Chromatogram GetChromatogram( Chromatogram metaChromatogram, ChromatogramList chromatogramList )
        {
            Chromatogram chromatogram = new Chromatogram( metaChromatogram, chromatogramList.chromatogram( metaChromatogram.Index, true ) );
            return chromatogram;
        }

        public MassSpectrum GetMassSpectrum( int index )
        { return GetMassSpectrum( index, source.MSDataFile.run.spectrumList ); }

        public MassSpectrum GetMassSpectrum( int index, SpectrumList spectrumList )
        { return new MassSpectrum( this, index, spectrumList ); }

        public MassSpectrum GetMassSpectrum( MassSpectrum metaSpectrum, SpectrumList spectrumList )
        {
            MassSpectrum spectrum = new MassSpectrum( metaSpectrum, spectrumList.spectrum( metaSpectrum.Index, true ) );
            //MassSpectrum realMetaSpectrum = ( metaSpectrum.Tag as DataGridViewRow ).Tag as MassSpectrum;
            //realMetaSpectrum.Element.dataProcessing = spectrum.Element.dataProcessing;
            //realMetaSpectrum.Element.defaultArrayLength = spectrum.Element.defaultArrayLength;
            return spectrum;
        }

        public MassSpectrum GetMassSpectrum( MassSpectrum metaSpectrum, string[] spectrumListFilters )
        {
            var tmp = source.MSDataFile.run.spectrumList;
            try
            {
                SpectrumListFactory.wrap(source.MSDataFile, spectrumListFilters);
                return GetMassSpectrum(metaSpectrum, source.MSDataFile.run.spectrumList);
            }
            finally
            {
                source.MSDataFile.run.spectrumList = tmp;
            }
        }

        public int CompareTo(ManagedDataSource other)
        {
            return String.Compare(source.CurrentFilepath, other.source.CurrentFilepath, StringComparison.Ordinal);
        }
    }

    public class ManagedDockableForm : DockableForm
    {
        public ManagedDataSource Source { get; protected set; }
        public Manager Manager { get; protected set; }
    }

    /// <summary>
	/// Manages the application
	/// Tracks data sources, their spectrum/chromatogram lists, and any associated graph forms
	/// Handles events from sources, lists, and graph forms
	/// </summary>
	public class Manager
	{
		private DataSourceMap dataSourceMap;
        private DataProcessing spectrumGlobalDataProcessing;

        private SpectrumProcessingForm spectrumProcessingForm;
        public SpectrumProcessingForm SpectrumProcessingForm { get { return spectrumProcessingForm; } }

        private SpectrumAnnotationForm spectrumAnnotationForm;
        public SpectrumAnnotationForm SpectrumAnnotationForm { get { return spectrumAnnotationForm; } }

        private DockPanel dockPanel;
        public DockPanel DockPanel { get { return dockPanel; } }

        public bool ShowChromatogramListForNewSources { get; set; }
        public bool ShowSpectrumListForNewSources { get; set; }

        public bool OpenFileUsesCurrentGraphForm { get; set; }
        public bool OpenFileGivesFocus { get; set; }

        public ImmutableDictionary<string, ManagedDataSource> DataSourceMap { get { return new ImmutableDictionary<string, ManagedDataSource>(dataSourceMap); } }

        private Dictionary<ManagedDataSource, bool> dataSourceFullyLoadedMap;
        public ImmutableDictionary<ManagedDataSource, bool> IsFullyLoadedSource { get { return new ImmutableDictionary<ManagedDataSource, bool>(dataSourceFullyLoadedMap); } }

        public GraphForm CurrentGraphForm { get { return (DockPanel.ActiveDocument is GraphForm ? (GraphForm) DockPanel.ActiveDocument : null); } }

        public IList<GraphForm> CurrentGraphFormList
        {
            get
            {
                List<GraphForm> graphFormList = new List<GraphForm>();
                foreach( IDockableForm form in DockPanel.Contents )
                {
                    if( form is GraphForm )
                        graphFormList.Add( form as GraphForm );
                }
                return graphFormList;
            }
        }

        public IList<DataPointTableForm> CurrentDataPointTableFormList
        {
            get
            {
                List<DataPointTableForm> tableList = new List<DataPointTableForm>();
                foreach( IDockableForm form in DockPanel.Contents )
                {
                    if( form is DataPointTableForm )
                        tableList.Add( form as DataPointTableForm );
                }
                return tableList;
            }
        }

        /// <summary>
        /// Occurs when a child GraphForm gets focus
        /// </summary>
        public event GraphFormGotFocusHandler GraphFormGotFocus;

        public event LoadDataSourceProgressEventHandler LoadDataSourceProgress;

        /// <summary>
        /// Sends a simple progress update with a free text status and an integral percentage.
        /// </summary>
        /// <returns>True if the event handlers have set the cancel property to true, otherwise false.</returns>
        protected bool OnLoadDataSourceProgress (string status, int percentage)
        {
            if (LoadDataSourceProgress != null)
            {
                var e = new System.ComponentModel.CancelEventArgs();
                LoadDataSourceProgress(this, status, percentage, e);
                if (e.Cancel)
                    return true;
            }
            return false;
        }

        public Manager( DockPanel dockPanel )
        {
            this.dockPanel = dockPanel;
            dataSourceMap = new DataSourceMap();
            dataSourceFullyLoadedMap = new Dictionary<ManagedDataSource, bool>();

            DockPanel.ActivePaneChanged += new EventHandler( form_GotFocus );

            spectrumProcessingForm = new SpectrumProcessingForm();
            spectrumProcessingForm.ProcessingChanged += spectrumProcessingForm_ProcessingChanged;
            //spectrumProcessingForm.GlobalProcessingOverrideButton.Click += new EventHandler( processingOverrideButton_Click );
            //spectrumProcessingForm.RunProcessingOverrideButton.Click += new EventHandler( processingOverrideButton_Click );
            spectrumProcessingForm.GotFocus += new EventHandler( form_GotFocus );
            spectrumProcessingForm.HideOnClose = true;

            spectrumAnnotationForm = new SpectrumAnnotationForm();
            spectrumAnnotationForm.AnnotationChanged += new EventHandler( spectrumAnnotationForm_AnnotationChanged );
            spectrumAnnotationForm.GotFocus += new EventHandler( form_GotFocus );
            spectrumAnnotationForm.HideOnClose = true;

            spectrumGlobalDataProcessing = new DataProcessing();

            ShowChromatogramListForNewSources = true;
            ShowSpectrumListForNewSources = true;

            OpenFileUsesCurrentGraphForm = false;
            OpenFileGivesFocus = true;

            LoadDefaultAnnotationSettings();
        }

        public bool OpenFile(string filepath, bool closeIfOpen = false )
        {
            return OpenFile(filepath, -1, closeIfOpen);
        }

        public bool OpenFile(string filepath, int index, bool closeIfOpen = false )
        {
            return OpenFile(filepath, index > 0 ? new List<object> { index } : null, null, closeIfOpen);
        }

        public bool OpenFile(string filepath, string id, bool closeIfOpen = false )
        {
            return OpenFile(filepath, new List<object> { id }, null, closeIfOpen);
        }

        public bool OpenFile(string filepath, IList<object> idOrIndexList, IAnnotation annotation, bool closeIfOpen = false )
        {
            return OpenFile(filepath, idOrIndexList, annotation, "", closeIfOpen);
        }

        public bool OpenFile(string filepathOrMsDataRunPath, IList<object> idOrIndexList, IAnnotation annotation, string spectrumListFilters, bool closeIfOpen = false)
        {
            var msDataRunPath = new OpenDataSourceDialog.MSDataRunPath(filepathOrMsDataRunPath);
            string filepath = msDataRunPath.ToString();

			try
			{
                OnLoadDataSourceProgress("Opening data source: " + Path.GetFileNameWithoutExtension(msDataRunPath.Filepath), 0);

                string[] spectrumListFilterList = spectrumListFilters.Split(';').Where(o => o.Length > 0).ToArray();

			    bool fileAlreadyOpen = dataSourceMap.ContainsKey(filepath);

                if (closeIfOpen && fileAlreadyOpen)
                {
                    ManagedDataSource source = dataSourceMap[filepath];
                    source.SpectrumListForm.Close();
                    source.ChromatogramListForm.Close();
                    foreach (var form in CurrentGraphFormList.Where(g => g.Sources.Any(s => s.Source == source.Source)))
                        form.Close();
                    foreach (var form in dockPanel.Contents.ToArray())
                        if (((form as ManagedDockableForm)?.Source ?? null) == source)
                            (form as DockableForm)?.Close();
                    source.Source.MSDataFile.Dispose();
                    dataSourceMap.Remove(filepath);
                    fileAlreadyOpen = false;
                }

                if (!fileAlreadyOpen)
                {
                    var newSource = new ManagedDataSource(new SpectrumSource(msDataRunPath));
                    dataSourceMap.Add(filepath, newSource);

                    if (spectrumListFilters.Length > 0)
                        SpectrumListFactory.wrap(newSource.Source.MSDataFile, spectrumListFilterList);

                    initializeManagedDataSource( newSource, idOrIndexList, annotation, spectrumListFilterList );
				} else
				{
                    GraphForm graph = OpenFileUsesCurrentGraphForm && CurrentGraphForm != null ? CurrentGraphForm
                                                                                               : OpenGraph(OpenFileGivesFocus);
                    ManagedDataSource source = dataSourceMap[filepath];

                    var indexListByType = new Dictionary<Type, List<int>>();
                    indexListByType[typeof(MassSpectrum)] = new List<int>();
                    indexListByType[typeof(Chromatogram)] = new List<int>();

                    foreach (object idOrIndex in idOrIndexList)
                    {
                        Type indexType = typeof(MassSpectrum);
                        //int index = -1;
                        if (idOrIndex is int)
                            indexListByType[indexType].Add((int)idOrIndex);
                        else if (idOrIndex is string)
                        {
                            SpectrumList sl = source.Source.MSDataFile.run.spectrumList;

                            int findIndex = sl.find(idOrIndex as string);
                            if (findIndex != sl.size())
                            {
                                indexListByType[indexType].Add(findIndex);
                            }
                            else
                            {
                                ChromatogramList cl = source.Source.MSDataFile.run.chromatogramList;
                                indexType = typeof(Chromatogram);
                                findIndex = cl.find(idOrIndex as string);
                                if (findIndex != cl.size())
                                    indexListByType[indexType].Add(findIndex);
                                else
                                    MessageBox.Show("This id does not exist in the spectrum or chromatogram list: " + idOrIndex, "Unable to find spectrum or chromatogram", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }

                    // conditionally load the spectra and chromatograms at the specified indexes first
                    bool noSpecifiedIndexes = true;

                    foreach (int index in indexListByType[typeof(MassSpectrum)])
                    {
                        MassSpectrum spectrum = source.GetMassSpectrum(index);

                        if (spectrumListFilters.Length > 0)
                            SpectrumListFactory.wrap(source.Source.MSDataFile, spectrumListFilterList);

                        spectrum.AnnotationSettings = defaultScanAnnotationSettings;

                        if (annotation != null)
                            spectrum.AnnotationList.Add(annotation);

                        if (source.Source.Spectra.Count < source.Source.MSDataFile.run.spectrumList.size() &&
                            source.SpectrumListForm.IndexOf(spectrum) < 0)
                        {
                            source.SpectrumListForm.Add(spectrum);
                            source.Source.Spectra.Add(spectrum);
                        }

                        showData(graph, spectrum);
                        noSpecifiedIndexes = false;
                    }

                    foreach (int index in indexListByType[typeof(Chromatogram)])
                    {
                        Chromatogram chromatogram = source.GetChromatogram(index);

                        chromatogram.AnnotationSettings = defaultChromatogramAnnotationSettings;

                        if (source.Source.Chromatograms.Count < source.Source.MSDataFile.run.chromatogramList.size() &&
                            source.ChromatogramListForm.IndexOf(chromatogram) < 0)
                        {
                            source.ChromatogramListForm.Add(chromatogram);
                            source.Source.Chromatograms.Add(chromatogram);
                        }

                        showData(graph, chromatogram);
                        noSpecifiedIndexes = false;
                    }

                    if (noSpecifiedIndexes)
                    {
                        if( source.Source.Chromatograms.Count > 0 )
                            showData(graph, source.Source.Chromatograms[0]);
                        else
                            showData(graph, source.Source.Spectra[0]);
                    }
				}

			    return true;
			} catch( Exception ex )
			{
				Program.HandleException("Error opening source file", ex);
                OnLoadDataSourceProgress("Failed to load data: " + ex.Message, 100);
			    return false;
			}
		}

		public GraphForm CreateGraph()
		{
			GraphForm graphForm = new GraphForm(this);
            graphForm.ZedGraphControl.PreviewKeyDown += new PreviewKeyDownEventHandler( graphForm_PreviewKeyDown );
            graphForm.ItemGotFocus += new EventHandler( form_GotFocus );
            return graphForm;
		}

        public GraphForm OpenGraph( bool giveFocus )
        {
            GraphForm graphForm = new GraphForm(this);
            graphForm.ZedGraphControl.PreviewKeyDown += new PreviewKeyDownEventHandler( graphForm_PreviewKeyDown );
            graphForm.ItemGotFocus += new EventHandler( form_GotFocus );
            graphForm.Show( DockPanel, DockState.Document );
            if( giveFocus )
                graphForm.Activate();

            return graphForm;
        }

        protected void OnGraphFormGotFocus (Manager sender, GraphForm graphForm)
        {
            if (GraphFormGotFocus != null)
                GraphFormGotFocus(sender, graphForm);
        }

        void form_GotFocus( object sender, EventArgs e )
        {
            DockableForm form = sender as DockableForm;
            DockPanel panel = sender as DockPanel;
            if( form == null && panel != null && panel.ActivePane != null )
                form = panel.ActiveContent as DockableForm;

            if( form is GraphForm )
            {
                GraphForm graphForm = form as GraphForm;
                OnGraphFormGotFocus(this, graphForm);

                if( graphForm.FocusedPane != null &&
                    graphForm.FocusedItem.Tag is MassSpectrum )
                {
                    // if the focused pane has multiple items, and only one item has a processing or annotation, focus on that item instead
                    var pane = graphForm.FocusedPane;
                    var spectrum = graphForm.FocusedItem.Tag as MassSpectrum;
                    if (pane.CurveList.Count > 1)
                    {
                        if (!spectrum.ProcessingList.Any())
                        {
                            var processedSpectrum = pane.CurveList.Select(o => o.Tag as MassSpectrum).FirstOrDefault(o => o != null && o.ProcessingList.Any()) ?? spectrum;
                            spectrumProcessingForm.UpdateProcessing(processedSpectrum);
                        }
                        else
                            spectrumProcessingForm.UpdateProcessing(spectrum);

                        if (!spectrum.AnnotationList.Any())
                        {
                            var annotatedSpectrum = pane.CurveList.Select(o => o.Tag as MassSpectrum).FirstOrDefault(o => o != null && o.AnnotationList.Any()) ?? spectrum;
                            spectrumAnnotationForm.UpdateAnnotations(annotatedSpectrum);
                        }
                        else
                            spectrumAnnotationForm.UpdateAnnotations(spectrum);
                        return;
                    }

                    spectrumProcessingForm.UpdateProcessing(spectrum);
                    spectrumAnnotationForm.UpdateAnnotations(spectrum);
                }
            } else if( form is SpectrumListForm )
            {
                //SpectrumListForm listForm = form as SpectrumListForm;
                //listForm.GetSpectrum(0).Source
            }// else
                //seemsForm.LogEvent( sender, e );
        }

        private MassSpectrum getMetaSpectrum( MassSpectrum spectrum )
        {
            return spectrum.OwningListForm.GetSpectrum( spectrum.OwningListForm.IndexOf( spectrum ) );
        }

		private void initializeManagedDataSource( ManagedDataSource managedDataSource, IList<object> idOrIndexList, IAnnotation annotation, string[] spectrumListFilters )
        {
			try
            {
                dataSourceFullyLoadedMap[managedDataSource] = false;

				SpectrumSource source = managedDataSource.Source;
				MSData msDataFile = source.MSDataFile;
				ChromatogramListForm chromatogramListForm = managedDataSource.ChromatogramListForm;
				SpectrumListForm spectrumListForm = managedDataSource.SpectrumListForm;

                chromatogramListForm.CellDoubleClick += new ChromatogramListCellDoubleClickHandler( chromatogramListForm_CellDoubleClick );
                chromatogramListForm.CellClick += new ChromatogramListCellClickHandler( chromatogramListForm_CellClick );
                chromatogramListForm.GotFocus += new EventHandler( form_GotFocus );

                spectrumListForm.CellDoubleClick += new SpectrumListCellDoubleClickHandler( spectrumListForm_CellDoubleClick );
                spectrumListForm.CellClick += new SpectrumListCellClickHandler( spectrumListForm_CellClick );
                spectrumListForm.FilterChanged += new SpectrumListFilterChangedHandler( spectrumListForm_FilterChanged );
                spectrumListForm.GotFocus += new EventHandler( form_GotFocus );

				GraphForm firstGraph = null;

				ChromatogramList cl = msDataFile.run.chromatogramList;
				SpectrumList sl = msDataFile.run.spectrumList;
                //sl = new SpectrumList_Filter( sl, new SpectrumList_FilterAcceptSpectrum( acceptSpectrum ) );

                if (sl.size()+cl.size() == 0)
                    throw new Exception("Error loading metadata: no spectra or chromatograms");

                var indexListByType = new Dictionary<Type, Set<int>>();
                indexListByType[typeof(MassSpectrum)] = new Set<int>();
                indexListByType[typeof(Chromatogram)] = new Set<int>();

                if (idOrIndexList != null)
                    foreach (object idOrIndex in idOrIndexList)
                    {
                        Type indexType = typeof(MassSpectrum);
                        //int index = -1;
                        if (idOrIndex is int)
                            indexListByType[indexType].Add((int)idOrIndex);
                        else if (idOrIndex is string)
                        {
                            int findIndex = sl.find(idOrIndex as string);
                            if (findIndex != sl.size())
                            {
                                indexListByType[indexType].Add(findIndex);
                            }
                            else
                            {
                                indexType = typeof(Chromatogram);
                                findIndex = cl.find(idOrIndex as string);
                                if (findIndex != cl.size())
                                    indexListByType[indexType].Add(findIndex);
                                else
                                    MessageBox.Show("This id does not exist in the spectrum or chromatogram list: " + idOrIndex, "Unable to find spectrum or chromatogram", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }

                // conditionally load the spectrum at the specified index first
                foreach(int index in indexListByType[typeof(MassSpectrum)])
                {
                    MassSpectrum spectrum = managedDataSource.GetMassSpectrum(index);
                    if (spectrumListFilters.Length > 0)
                        spectrum = managedDataSource.GetMassSpectrum(spectrum, spectrumListFilters);

                    spectrum.AnnotationSettings = defaultScanAnnotationSettings;
                    spectrumListForm.Add(spectrum);
                    source.Spectra.Add(spectrum);

                    if (ShowSpectrumListForNewSources)
                    {
                        spectrumListForm.Show(DockPanel, DockState.DockBottom);
                        Application.DoEvents();
                    }

                    if (annotation != null)
                        spectrum.AnnotationList.Add(annotation);
                    
                    if (firstGraph == null)
                    {
                        firstGraph = OpenFileUsesCurrentGraphForm && CurrentGraphForm != null ? CurrentGraphForm
                                                                                              : OpenGraph(OpenFileGivesFocus);
                    }
                    showData(firstGraph, spectrum);
                }

                foreach (int index in indexListByType[typeof(Chromatogram)])
                {
                    Chromatogram chromatogram = managedDataSource.GetChromatogram(index);

                    chromatogram.AnnotationSettings = defaultChromatogramAnnotationSettings;
                    chromatogramListForm.Add(chromatogram);
                    source.Chromatograms.Add(chromatogram);

                    if (ShowChromatogramListForNewSources)
                    {
                        chromatogramListForm.Show(DockPanel, DockState.DockBottom);
                        Application.DoEvents();
                    }

                    if (firstGraph == null)
                    {
                        firstGraph = OpenFileUsesCurrentGraphForm && CurrentGraphForm != null ? CurrentGraphForm
                                                                                              : OpenGraph(OpenFileGivesFocus);
                    }
                    showData(firstGraph, chromatogram);
                }

                if (firstGraph != null)
                    return;

                LoadAllMetadata(managedDataSource);

			} catch( Exception ex )
			{
			    Program.HandleException("Error reading source metadata", ex);
				OnLoadDataSourceProgress("Failed to load data: " + ex.Message, 100);
			}
		}

        public void LoadAllMetadata(ManagedDataSource managedDataSource)
        {
            if (dataSourceFullyLoadedMap[managedDataSource])
                return;
            dataSourceFullyLoadedMap[managedDataSource] = true;

            var source = managedDataSource.Source;
            var msDataFile = source.MSDataFile;
            var chromatogramListForm = managedDataSource.ChromatogramListForm;
            var spectrumListForm = managedDataSource.SpectrumListForm;
            ChromatogramList cl = msDataFile.run.chromatogramList;
            SpectrumList sl = msDataFile.run.spectrumList;

            bool firstChromatogramLoaded = source.Chromatograms.Count > 0;
            bool firstSpectrumLoaded = source.Spectra.Count > 0;
            GraphForm firstGraph = null;

            int ticIndex = cl.find("TIC");
            if (ticIndex < cl.size())
            {
                pwiz.CLI.msdata.Chromatogram tic = cl.chromatogram(ticIndex);
                Chromatogram ticChromatogram = managedDataSource.GetChromatogram(ticIndex);
                ticChromatogram.AnnotationSettings = defaultChromatogramAnnotationSettings;
                chromatogramListForm.Add(ticChromatogram);
                source.Chromatograms.Add(ticChromatogram);
                if (!firstSpectrumLoaded)
                {
                    firstGraph = OpenGraph(true);
                    showData(firstGraph, ticChromatogram);
                    firstChromatogramLoaded = true;

                    if (ShowChromatogramListForNewSources)
                    {
                        chromatogramListForm.Show(DockPanel, DockState.DockBottom);
                        Application.DoEvents();
                    }
                }
            }

            // get spectrum type from fileContent if possible, otherwise from first spectrum
            CVParam spectrumType = msDataFile.fileDescription.fileContent.cvParamChild(CVID.MS_spectrum_type);
            if (spectrumType.cvid == CVID.CVID_Unknown && sl.size() > 0)
                spectrumType = sl.spectrum(0).cvParamChild(CVID.MS_spectrum_type);

            // load the rest of the chromatograms
            for (int i = 0; i < cl.size(); ++i)
            {
                if (i == ticIndex || source.Chromatograms.Any(o => o.Index == i))
                    continue;

                Chromatogram chromatogram = managedDataSource.GetChromatogram(i);

                if (OnLoadDataSourceProgress(String.Format("Loading chromatograms from {0} ({1} of {2})...",
                                                       managedDataSource.Source.Name, (i + 1), cl.size()),
                                             (i + 1) * 100 / cl.size()))
                    return;

                chromatogram.AnnotationSettings = defaultChromatogramAnnotationSettings;
                chromatogramListForm.Add(chromatogram);
                source.Chromatograms.Add(chromatogram);
                if (!firstSpectrumLoaded && !firstChromatogramLoaded)
                {
                    firstChromatogramLoaded = true;

                    if (ShowChromatogramListForNewSources)
                    {
                        chromatogramListForm.Show(DockPanel, DockState.DockBottom);
                        Application.DoEvents();
                    }

                    firstGraph = OpenGraph(true);
                    showData(firstGraph, chromatogram);
                }
                Application.DoEvents();
            }

            // get first 100 scans by sequential access
            spectrumListForm.BeginBulkLoad();

            if (OnLoadDataSourceProgress(String.Format("Loading first 100 spectra from {0}...", managedDataSource.Source.Name), 0))
                return;

            var preloadedSpectraIndices = new Set<int>(source.Spectra.Select(o => o.Index));

            for (int i = 0; i < Math.Min(100, sl.size()); ++i)
            {
                if (preloadedSpectraIndices.Contains(i)) // skip preloaded spectra
                    continue;

                MassSpectrum spectrum = managedDataSource.GetMassSpectrum(i);
                spectrum.AnnotationSettings = defaultScanAnnotationSettings;
                spectrumListForm.Add(spectrum);
                source.Spectra.Add(spectrum);
                if (!firstSpectrumLoaded)
                {
                    firstSpectrumLoaded = true;
                    if (firstChromatogramLoaded)
                    {
                        GraphForm spectrumGraph = CreateGraph();
                        spectrumGraph.Show(firstGraph.Pane, DockPaneAlignment.Bottom, 0.5);
                        showData(spectrumGraph, spectrum);
                    }
                    else
                    {
                        firstGraph = OpenGraph(true);
                        showData(firstGraph, spectrum);
                    }
                }
            }
            spectrumListForm.EndBulkLoad();
            spectrumListForm.Show(DockPanel, DockState.DockBottom);

            // if no spectra, focus back on chromatograms
            if (ShowChromatogramListForNewSources && sl.size() == 0)
                chromatogramListForm.Show();

            // get the rest of the scans by sequential access
            spectrumListForm.BeginBulkLoad();
            for (int i = 100; i < sl.size(); ++i)
            {
                if (preloadedSpectraIndices.Contains(i)) // skip preloaded spectra
                    continue;

                if (((i + 1) % 1000) == 0 || (i + 1) == sl.size())
                {
                    if (OnLoadDataSourceProgress(String.Format("Loading spectra from {0} ({1} of {2})...",
                                                           managedDataSource.Source.Name, (i + 1), sl.size()),
                                                 (i + 1) * 100 / sl.size()))
                        return;
                }

                MassSpectrum spectrum = managedDataSource.GetMassSpectrum(i);
                spectrum.AnnotationSettings = defaultScanAnnotationSettings;
                spectrumListForm.Add(spectrum);
                source.Spectra.Add(spectrum);
                Application.DoEvents();
            }
            spectrumListForm.EndBulkLoad();
            Application.DoEvents();

            if (managedDataSource.SpectrumListForm.GetIonMobilityRows().Any())
            {
                var heatmapForm = new HeatmapForm(this, managedDataSource);
                heatmapForm.Show(DockPanel, DockState.Document);
            }

            OnLoadDataSourceProgress("Finished loading source metadata.", 100);
        }

        void chromatogramListForm_CellClick( object sender, ChromatogramListCellClickEventArgs e )
        {
            if( e.Chromatogram == null || e.Button != MouseButtons.Right )
                return;

            ChromatogramListForm chromatogramListForm = sender as ChromatogramListForm;

            List<GraphItem> selectedGraphItems = new List<GraphItem>();
            Set<int> selectedRows = new Set<int>();
            foreach( DataGridViewCell cell in chromatogramListForm.GridView.SelectedCells )
            {
                if( selectedRows.Insert( cell.RowIndex ).WasInserted )
                    selectedGraphItems.Add( chromatogramListForm.GetChromatogram( cell.RowIndex ) as GraphItem );
            }

            if( selectedRows.Count == 0 )
                chromatogramListForm.GridView[e.ColumnIndex, e.RowIndex].Selected = true;

            ContextMenuStrip menu = new ContextMenuStrip();
            if( CurrentGraphForm != null )
            {
                if( selectedRows.Count == 1 )
                {
                    menu.Items.Add( "Show as Current Graph", null, new EventHandler( graphListForm_showAsCurrentGraph ) );
                    menu.Items.Add( "Overlay on Current Graph", null, new EventHandler( graphListForm_overlayOnCurrentGraph ) );
                    menu.Items.Add( "Stack on Current Graph", null, new EventHandler( graphListForm_stackOnCurrentGraph ) );
                } else
                {
                    menu.Items.Add( "Overlay All on Current Graph", null, new EventHandler( graphListForm_overlayAllOnCurrentGraph ) );
                    menu.Items.Add( "Stack All on Current Graph", null, new EventHandler( graphListForm_showAllAsStackOnCurrentGraph ) );
                }
            }

            if( selectedRows.Count == 1 )
            {
                menu.Items.Add( "Show as New Graph", null, new EventHandler( graphListForm_showAsNewGraph ) );
                menu.Items.Add( "Show Table of Data Points", null, new EventHandler( graphListForm_showTableOfDataPoints ) );
                menu.Items[0].Font = new Font( menu.Items[0].Font, FontStyle.Bold );
                menu.Tag = e.Chromatogram;
            } else
            {
                menu.Items.Add( "Show All as New Graphs", null, new EventHandler( graphListForm_showAllAsNewGraph ) );
                menu.Items.Add( "Overlay All on New Graph", null, new EventHandler( graphListForm_overlayAllOnNewGraph ) );
                menu.Items.Add( "Stack All on New Graph", null, new EventHandler( graphListForm_showAllAsStackOnNewGraph ) );
                menu.Tag = selectedGraphItems;
            }

            menu.Show( Form.MousePosition );
        }

        void spectrumListForm_CellClick( object sender, SpectrumListCellClickEventArgs e )
        {
            if( e.Spectrum == null || e.Button != MouseButtons.Right )
                return;

            SpectrumListForm spectrumListForm = sender as SpectrumListForm;

            List<GraphItem> selectedGraphItems = new List<GraphItem>();
            Set<int> selectedRows = new Set<int>();
            foreach( DataGridViewCell cell in spectrumListForm.GridView.SelectedCells )
            {
                if( selectedRows.Insert( cell.RowIndex ).WasInserted )
                    selectedGraphItems.Add( spectrumListForm.GetSpectrum( cell.RowIndex ) as GraphItem );
            }

            if( selectedRows.Count == 0 )
                spectrumListForm.GridView[e.ColumnIndex, e.RowIndex].Selected = true;

            ContextMenuStrip menu = new ContextMenuStrip();
            if( CurrentGraphForm != null )
            {
                if( selectedRows.Count == 1 )
                {
                    menu.Items.Add( "Show as Current Graph", null, new EventHandler( graphListForm_showAsCurrentGraph ) );
                    menu.Items.Add( "Overlay on Current Graph", null, new EventHandler( graphListForm_overlayOnCurrentGraph ) );
                    menu.Items.Add( "Stack on Current Graph", null, new EventHandler( graphListForm_stackOnCurrentGraph ) );
                } else
                {
                    menu.Items.Add( "Overlay All on Current Graph", null, new EventHandler( graphListForm_overlayAllOnCurrentGraph ) );
                    menu.Items.Add( "Stack All on Current Graph", null, new EventHandler( graphListForm_showAllAsStackOnCurrentGraph ) );
                }
            }

            if( selectedRows.Count == 1 )
            {
                menu.Items.Add( "Show as New Graph", null, new EventHandler( graphListForm_showAsNewGraph ) );
                menu.Items.Add( "Show Table of Data Points", null, new EventHandler( graphListForm_showTableOfDataPoints ) );
                menu.Items[0].Font = new Font( menu.Items[0].Font, FontStyle.Bold );
                menu.Tag = e.Spectrum;

                if ((int)spectrumListForm.GridView["MsLevel", e.RowIndex].Value > 1 && e.Spectrum.Element.precursors.Count > 0)
                {
                    menu.Items.Add("Show Precursor Spectrum as New Graph", null, graphListForm_showPrecursorSpectrumOnNewGraph);
                    menu.Items.Add("Overlay Precursor Spectrum on Current Graph", null, graphListForm_showPrecursorSpectrumOverlayOnCurrentGraph);
                    menu.Items.Add("Stack Precursor Spectrum on Current Graph", null, graphListForm_showPrecursorSpectrumStackOnCurrentGraph);
                }
            } else
            {
                menu.Items.Add( "Show All as New Graphs", null, new EventHandler( graphListForm_showAllAsNewGraph ) );
                menu.Items.Add( "Overlay All on New Graph", null, new EventHandler( graphListForm_overlayAllOnNewGraph ) );
                menu.Items.Add( "Stack All on New Graph", null, new EventHandler( graphListForm_showAllAsStackOnNewGraph ) );
                menu.Tag = selectedGraphItems;
            }

            menu.Show( Form.MousePosition );
        }

        #region various methods to create graph forms
        private void showData( GraphForm hostGraph, GraphItem item )
        {
            Pane pane;
            if( hostGraph.PaneList.Count == 0 || hostGraph.PaneList.Count > 1 )
            {
                hostGraph.PaneList.Clear();
                pane = new Pane();
                hostGraph.PaneList.Add( pane );
            } else
            {
                pane = hostGraph.PaneList[0];
                pane.Clear();
            }
            pane.Add( item );
            hostGraph.Refresh();
        }

        private void showDataOverlay(GraphForm hostGraph, GraphItem item )
        {
            hostGraph.PaneList[0].Add( item );
            hostGraph.Refresh();
        }

        private void showDataStacked(GraphForm hostGraph, GraphItem item )
        {
            Pane pane = new Pane();
            pane.Add( item );
            hostGraph.PaneList.Add( pane );
            hostGraph.Refresh();
        }

        void graphListForm_showAsCurrentGraph( object sender, EventArgs e )
        {
            GraphForm currentGraphForm = CurrentGraphForm;
            if( currentGraphForm == null )
                throw new Exception( "current graph should not be null" );
            GraphItem g = ( ( sender as ToolStripMenuItem ).Owner as ContextMenuStrip ).Tag as GraphItem;

            currentGraphForm.PaneList.Clear();
            Pane pane = new Pane();
            pane.Add( g );
            currentGraphForm.PaneList.Add( pane );
            currentGraphForm.Refresh();
        }

        void graphListForm_overlayOnCurrentGraph( object sender, EventArgs e )
        {
            GraphForm currentGraphForm = CurrentGraphForm;
            if( currentGraphForm == null )
                throw new Exception( "current graph should not be null" );
            GraphItem g = ( ( sender as ToolStripMenuItem ).Owner as ContextMenuStrip ).Tag as GraphItem;

            currentGraphForm.PaneList[0].Add( g );
            currentGraphForm.Refresh();
        }

        void graphListForm_overlayAllOnCurrentGraph( object sender, EventArgs e )
        {
            GraphForm currentGraphForm = CurrentGraphForm;
            if( currentGraphForm == null )
                throw new Exception( "current graph should not be null" );
            List<GraphItem> gList = ( ( sender as ToolStripMenuItem ).Owner as ContextMenuStrip ).Tag as List<GraphItem>;

            currentGraphForm.PaneList.Clear();
            Pane pane = new Pane();
            foreach( GraphItem g in gList )
            {
                pane.Add( g );
            }
            currentGraphForm.PaneList.Add( pane );
            currentGraphForm.Refresh();
        }

        void graphListForm_showAsNewGraph( object sender, EventArgs e )
        {
            GraphForm newGraph = OpenGraph( true );
            GraphItem g = ( ( sender as ToolStripMenuItem ).Owner as ContextMenuStrip ).Tag as GraphItem;

            Pane pane = new Pane();
            pane.Add( g );
            newGraph.PaneList.Add( pane );
            newGraph.Refresh();
        }

        void graphListForm_showAllAsNewGraph( object sender, EventArgs e )
        {
            List<GraphItem> gList = ( ( sender as ToolStripMenuItem ).Owner as ContextMenuStrip ).Tag as List<GraphItem>;
            foreach( GraphItem g in gList )
            {
                GraphForm newGraph = OpenGraph( true );

                Pane pane = new Pane();
                pane.Add( g );
                newGraph.PaneList.Add( pane );
                newGraph.Refresh();
            }
        }

        void graphListForm_stackOnCurrentGraph( object sender, EventArgs e )
        {
            GraphForm currentGraphForm = CurrentGraphForm;
            if( currentGraphForm == null )
                throw new Exception( "current graph should not be null" );
            GraphItem g = ( ( sender as ToolStripMenuItem ).Owner as ContextMenuStrip ).Tag as GraphItem;

            Pane pane = new Pane();
            pane.Add( g );
            currentGraphForm.PaneList.Add( pane );
            currentGraphForm.Refresh();
        }

        void graphListForm_showAllAsStackOnCurrentGraph( object sender, EventArgs e )
        {
            GraphForm currentGraphForm = CurrentGraphForm;
            if( currentGraphForm == null )
                throw new Exception( "current graph should not be null" );
            List<GraphItem> gList = ( ( sender as ToolStripMenuItem ).Owner as ContextMenuStrip ).Tag as List<GraphItem>;

            currentGraphForm.PaneList.Clear();
            foreach( GraphItem g in gList )
            {
                Pane pane = new Pane();
                pane.Add( g );
                currentGraphForm.PaneList.Add( pane );
            }
            currentGraphForm.Refresh();
        }

        void graphListForm_overlayAllOnNewGraph( object sender, EventArgs e )
        {
            List<GraphItem> gList = ( ( sender as ToolStripMenuItem ).Owner as ContextMenuStrip ).Tag as List<GraphItem>;

            GraphForm newGraph = OpenGraph( true );
            Pane pane = new Pane();
            foreach( GraphItem g in gList )
            {
                pane.Add( g );
            }
            newGraph.PaneList.Add( pane );
            newGraph.Refresh();
        }

        void graphListForm_showAllAsStackOnNewGraph( object sender, EventArgs e )
        {
            List<GraphItem> gList = ( ( sender as ToolStripMenuItem ).Owner as ContextMenuStrip ).Tag as List<GraphItem>;

            GraphForm newGraph = OpenGraph( true );
            foreach( GraphItem g in gList )
            {
                Pane pane = new Pane();
                pane.Add( g );
                newGraph.PaneList.Add( pane );
            }
            newGraph.Refresh();
        }
        
        /// <summary>
        /// Returns precursor index and the Precursor MSData element
        /// </summary>
        MassSpectrum getPrecursorSpectrum(MassSpectrum g)
        {
            var precursor = g.Element.precursors[0];
            string precursorId = precursor.spectrumID;
            if (precursorId.IsNullOrEmpty())
                throw new Exception("spectrum " + g.Id + " has a precursor but it does not have a spectrumID");
            int precursorIndex = g.SpectrumList.find(precursorId);
            if (precursorIndex == g.SpectrumList.size())
                throw new Exception("spectrum " + g.Id + " has a precursor spectrumID (" + precursorId + ") but it is not present in the source file");

            var filter = new IsolationWindowFilter(5, precursor.isolationWindow);
            var filteredList = new SpectrumList_PeakFilter(g.SpectrumList, filter);

            return g.Source.GetMassSpectrum(precursorIndex, filteredList);
        }

        void graphListForm_showPrecursorSpectrumOverlayOnCurrentGraph(object sender, EventArgs e)
        {
            MassSpectrum g = ((sender as ToolStripMenuItem).Owner as ContextMenuStrip).Tag as MassSpectrum;
            MassSpectrum precursorSpectrum = getPrecursorSpectrum(g);

            GraphForm currentGraphForm = CurrentGraphForm;
            if (currentGraphForm == null)
                throw new Exception("current graph should not be null");

            currentGraphForm.PaneList[0].Add(precursorSpectrum);
            currentGraphForm.Refresh();
        }

        void graphListForm_showPrecursorSpectrumStackOnCurrentGraph(object sender, EventArgs e)
        {
            MassSpectrum g = ((sender as ToolStripMenuItem).Owner as ContextMenuStrip).Tag as MassSpectrum;
            MassSpectrum precursorSpectrum = getPrecursorSpectrum(g);

            GraphForm currentGraphForm = CurrentGraphForm;
            if (currentGraphForm == null)
                throw new Exception("current graph should not be null");

            Pane pane = new Pane();
            pane.Add(precursorSpectrum);
            currentGraphForm.PaneList.Add(pane);
            currentGraphForm.Refresh();
        }

        void graphListForm_showPrecursorSpectrumOnNewGraph(object sender, EventArgs e)
        {
            MassSpectrum g = ((sender as ToolStripMenuItem).Owner as ContextMenuStrip).Tag as MassSpectrum;
            MassSpectrum precursorSpectrum = getPrecursorSpectrum(g);

            GraphForm newGraph = OpenGraph(true);
            Pane pane = new Pane();
            pane.Add(precursorSpectrum);
            newGraph.PaneList.Add(pane);
            newGraph.Refresh();
        }
        #endregion

        void graphListForm_showTableOfDataPoints( object sender, EventArgs e )
        {
            GraphItem g = ( ( sender as ToolStripMenuItem ).Owner as ContextMenuStrip ).Tag as GraphItem;

            DataPointTableForm form = new DataPointTableForm( g );
            form.Text = g.Id + " Data";

            form.Show( DockPanel, DockState.Floating );
        }

        public void ShowDataProcessing()
        {
            GraphForm currentGraphForm = CurrentGraphForm;
            if( currentGraphForm == null )
                throw new Exception( "current graph should not be null" );

            if( currentGraphForm.FocusedPane.CurrentItemType == MSGraphItemType.spectrum )
            {
                spectrumProcessingForm.UpdateProcessing( currentGraphForm.FocusedItem.Tag as MassSpectrum );
                DockPanel.DefaultFloatingWindowSize = spectrumProcessingForm.Size;
                /*spectrumProcessingForm.Show( DockPanel, DockState.Floating );
                Rectangle r = new Rectangle( mainForm.Location.X + mainForm.Width / 2 - spectrumProcessingForm.Width / 2,
                                             mainForm.Location.Y + mainForm.Height / 2 - spectrumProcessingForm.Height / 2,
                                             spectrumProcessingForm.Width,
                                             spectrumProcessingForm.Height );
                spectrumProcessingForm.FloatAt( r );*/
                spectrumProcessingForm.Show( DockPanel, DockState.DockTop );
            }
        }

        private AnnotationSettings defaultScanAnnotationSettings;
        private AnnotationSettings defaultChromatogramAnnotationSettings;
        public void LoadDefaultAnnotationSettings()
        {
            defaultScanAnnotationSettings = new AnnotationSettings();
            defaultScanAnnotationSettings.ShowXValues = Properties.Settings.Default.ShowScanMzLabels;
            defaultScanAnnotationSettings.ShowYValues = Properties.Settings.Default.ShowScanIntensityLabels;
            defaultScanAnnotationSettings.ShowMatchedAnnotations = Properties.Settings.Default.ShowScanMatchedAnnotations;
            defaultScanAnnotationSettings.ShowUnmatchedAnnotations = Properties.Settings.Default.ShowScanUnmatchedAnnotations;
            defaultScanAnnotationSettings.MatchTolerance = Properties.Settings.Default.MzMatchTolerance;
            defaultScanAnnotationSettings.MatchToleranceOverride = Properties.Settings.Default.ScanMatchToleranceOverride;
            defaultScanAnnotationSettings.MatchToleranceUnit = (MatchToleranceUnits) Properties.Settings.Default.MzMatchToleranceUnit;

            // ms-product-label -> (label alias, known color)
            defaultScanAnnotationSettings.LabelToAliasAndColorMap["y"] = new Pair<string, Color>( "y", Color.Blue );
            defaultScanAnnotationSettings.LabelToAliasAndColorMap["b"] = new Pair<string, Color>( "b", Color.Red );
            defaultScanAnnotationSettings.LabelToAliasAndColorMap["y-NH3"] = new Pair<string, Color>( "y^", Color.Green );
            defaultScanAnnotationSettings.LabelToAliasAndColorMap["y-H2O"] = new Pair<string, Color>( "y*", Color.Cyan );
            defaultScanAnnotationSettings.LabelToAliasAndColorMap["b-NH3"] = new Pair<string, Color>( "b^", Color.Orange );
            defaultScanAnnotationSettings.LabelToAliasAndColorMap["b-H2O"] = new Pair<string, Color>( "b*", Color.Violet );

            defaultChromatogramAnnotationSettings = new AnnotationSettings();
            defaultChromatogramAnnotationSettings.ShowXValues = Properties.Settings.Default.ShowChromatogramTimeLabels;
            defaultChromatogramAnnotationSettings.ShowYValues = Properties.Settings.Default.ShowChromatogramIntensityLabels;
            defaultChromatogramAnnotationSettings.ShowMatchedAnnotations = Properties.Settings.Default.ShowChromatogramMatchedAnnotations;
            defaultChromatogramAnnotationSettings.ShowUnmatchedAnnotations = Properties.Settings.Default.ShowChromatogramUnmatchedAnnotations;
            defaultChromatogramAnnotationSettings.MatchTolerance = Properties.Settings.Default.TimeMatchTolerance;
            defaultChromatogramAnnotationSettings.MatchToleranceOverride = Properties.Settings.Default.ChromatogramMatchToleranceOverride;
            defaultChromatogramAnnotationSettings.MatchToleranceUnit = (MatchToleranceUnits) Properties.Settings.Default.TimeMatchToleranceUnit;
        }

        public void ShowAnnotationForm()
        {
            GraphForm currentGraphForm = CurrentGraphForm;
            if( currentGraphForm == null )
                throw new Exception( "current graph should not be null" );

            if( currentGraphForm.FocusedPane.CurrentItemType == MSGraphItemType.spectrum )
            {
                spectrumAnnotationForm.UpdateAnnotations( currentGraphForm.FocusedItem.Tag as MassSpectrum );
                DockPanel.DefaultFloatingWindowSize = spectrumAnnotationForm.Size;
                /*spectrumAnnotationForm.Show( DockPanel, DockState.Floating );
                Rectangle r = new Rectangle( mainForm.Location.X + mainForm.Width / 2 - spectrumAnnotationForm.Width / 2,
                                             mainForm.Location.Y + mainForm.Height / 2 - spectrumAnnotationForm.Height / 2,
                                             spectrumAnnotationForm.Width,
                                             spectrumAnnotationForm.Height );
                spectrumAnnotationForm.FloatAt( r );*/
                spectrumAnnotationForm.Show( DockPanel, DockState.DockTop );
                DockPanel.DockTopPortion = 0.3;
            }
        }

        void spectrumAnnotationForm_AnnotationChanged( object sender, EventArgs e )
        {
            if( sender is SpectrumAnnotationForm )
            {
                foreach( GraphForm form in CurrentGraphFormList )
                {
                    bool refresh = false;
                    foreach( Pane pane in form.PaneList )
                        for( int i = 0; i < pane.Count && !refresh; ++i )
                        {
                            if( pane[i].IsMassSpectrum &&
                                pane[i].Id == spectrumAnnotationForm.CurrentSpectrum.Id )
                            {
                                refresh = true;
                                break;
                            }
                        }
                    if( refresh )
                        form.Refresh();
                }
            }
        }

        private void spectrumProcessingForm_ProcessingChanged( object sender, SpectrumProcessingForm.ProcessingChangedEventArgs e )
        {
            if (!(sender is SpectrumProcessingForm)) return;

            SpectrumProcessingForm spf = sender as SpectrumProcessingForm;

            var sourcesToUpdate = new Set<ManagedDataSource>();

            foreach( GraphForm form in CurrentGraphFormList )
            {
                bool refresh = false;

                foreach( Pane pane in form.PaneList )
                    foreach (GraphItem graphItem in pane)
                    {
                        if (!graphItem.IsMassSpectrum) continue;
                        var paneSpectrum = graphItem as MassSpectrum;

                        if (e.ChangeScope == SpectrumProcessingForm.ProcessingChangedEventArgs.Scope.Spectrum && paneSpectrum.Id != e.Spectrum.Id)
                            continue;
                        if (e.ChangeScope == SpectrumProcessingForm.ProcessingChangedEventArgs.Scope.Run && paneSpectrum.Source != e.Spectrum.Source)
                            continue;

                        paneSpectrum.SpectrumList = spectrumProcessingForm.GetProcessingSpectrumList(paneSpectrum, paneSpectrum.Source.Source.MSDataFile.run.spectrumList);

                        refresh = true;

                        // for spectrum changes, the spectrumList only needs to update that row and we can stop looking at other panes
                        if (e.ChangeScope == SpectrumProcessingForm.ProcessingChangedEventArgs.Scope.Spectrum)
                        {
                            graphItem.Source.SpectrumListForm.UpdateRow(paneSpectrum.Source.SpectrumListForm.IndexOf(spf.CurrentSpectrum), paneSpectrum.SpectrumList);
                            break;
                        }
                        else if (e.ChangeScope == SpectrumProcessingForm.ProcessingChangedEventArgs.Scope.Run && graphItem.Source == e.Spectrum.Source ||
                                 e.ChangeScope == SpectrumProcessingForm.ProcessingChangedEventArgs.Scope.Global)
                            sourcesToUpdate.Add(paneSpectrum.Source);
                    }
                if( refresh )
                    form.Refresh();
            }

            // update all rows for this spectrum's run (Scope.Run) or all runs (Scope.Global)
            foreach (var source in sourcesToUpdate)
                source.SpectrumListForm.UpdateAllRows();

            foreach( DataPointTableForm form in CurrentDataPointTableFormList )
            {
                bool refresh = false;
                foreach( GraphItem item in form.DataItems )
                    if( item.IsMassSpectrum &&
                        item.Id == spf.CurrentSpectrum.Id )
                    {
                        ( item as MassSpectrum ).SpectrumList = spectrumProcessingForm.GetProcessingSpectrumList(item as MassSpectrum, item.Source.Source.MSDataFile.run.spectrumList);
                        refresh = true;
                        break;
                    }
                if( refresh )
                    form.Refresh();
            }

            //getMetaSpectrum( spf.CurrentSpectrum ).DataProcessing = spf.datapro;
        }

        private void chromatogramListForm_CellDoubleClick( object sender, ChromatogramListCellDoubleClickEventArgs e )
        {
            if( e.Chromatogram == null || e.Button != MouseButtons.Left )
                return;

            GraphForm currentGraphForm = CurrentGraphForm;
            if( currentGraphForm == null )
                currentGraphForm = OpenGraph( true );

            showData(currentGraphForm, e.Chromatogram );
            currentGraphForm.ZedGraphControl.Focus();
        }

        private void spectrumListForm_CellDoubleClick( object sender, SpectrumListCellDoubleClickEventArgs e )
        {
            if( e.Spectrum == null )
                return;

            GraphForm currentGraphForm = CurrentGraphForm;
            if( currentGraphForm == null )
                currentGraphForm = OpenGraph( true );

            spectrumProcessingForm.UpdateProcessing( e.Spectrum );
            spectrumAnnotationForm.UpdateAnnotations( e.Spectrum );
            showData( currentGraphForm, e.Spectrum );
            currentGraphForm.ZedGraphControl.Focus();
        }

        private void spectrumListForm_FilterChanged( object sender, SpectrumListFilterChangedEventArgs e )
        {
            if (e.Matches == e.Total)
                OnLoadDataSourceProgress("Filters reset to show all spectra.", 100);
            else
                OnLoadDataSourceProgress(String.Format("{0} of {1} spectra matched the filter settings.", e.Matches, e.Total), 100);
        }

        private void graphForm_PreviewKeyDown( object sender, PreviewKeyDownEventArgs e )
        {
            if( !( sender is GraphForm ) && !( sender is pwiz.MSGraph.MSGraphControl ) )
                throw new Exception( "Error processing keyboard input: unable to handle sender " + sender.ToString() );

            GraphForm graphForm;
            if( sender is GraphForm )
                graphForm = sender as GraphForm;
            else
                graphForm = ( sender as pwiz.MSGraph.MSGraphControl ).Parent as GraphForm;

            GraphItem graphItem = graphForm.FocusedItem.Tag as GraphItem;
            SpectrumSource source = graphItem.Source.Source;
            if( source == null || graphItem == null )
                return;

            if (graphItem is Chromatogram && !(graphItem as Chromatogram).OwningListForm.Visible ||
                graphItem is MassSpectrum && !(graphItem as MassSpectrum).OwningListForm.Visible)
                return;

            DataGridView gridView = graphItem.IsChromatogram ? ( graphItem as Chromatogram ).OwningListForm.GridView
                                                             : ( graphItem as MassSpectrum ).OwningListForm.GridView;
            int rowIndex = graphItem.IsChromatogram ? ( graphItem as Chromatogram ).OwningListForm.IndexOf( graphItem as Chromatogram )
                                                    : ( graphItem as MassSpectrum ).OwningListForm.IndexOf( graphItem as MassSpectrum );

            int columnIndex = gridView.CurrentCell == null ? 0 : gridView.CurrentCell.ColumnIndex;

            int key = (int) e.KeyCode;
            if( ( key == (int) Keys.Left || key == (int) Keys.Up ) && rowIndex > 0 )
                gridView.CurrentCell = gridView[columnIndex, rowIndex - 1];
            else if( ( key == (int) Keys.Right || key == (int) Keys.Down ) && rowIndex < gridView.RowCount - 1 )
                gridView.CurrentCell = gridView[columnIndex, rowIndex + 1];
            else
                return;

            if( graphItem.IsMassSpectrum ) // update spectrum processing form
            {
                MassSpectrum spectrum = ( graphItem as MassSpectrum ).OwningListForm.GetSpectrum( gridView.CurrentCellAddress.Y );
                spectrumProcessingForm.UpdateProcessing( spectrum );
                spectrumAnnotationForm.UpdateAnnotations( spectrum );
                showData( graphForm, ( graphItem as MassSpectrum ).OwningListForm.GetSpectrum( gridView.CurrentCellAddress.Y ) );
            } else
            {
                showData( graphForm, ( graphItem as Chromatogram ).OwningListForm.GetChromatogram( gridView.CurrentCellAddress.Y ) );
            }

            gridView.Parent.Refresh(); // update chromatogram/spectrum list
            graphForm.Pane.Refresh(); // update tab text
            Application.DoEvents();

            //CurrentGraphForm.ZedGraphControl.PreviewKeyDown -= new PreviewKeyDownEventHandler( GraphForm_PreviewKeyDown );
            //Application.DoEvents();
            //CurrentGraphForm.ZedGraphControl.PreviewKeyDown += new PreviewKeyDownEventHandler( GraphForm_PreviewKeyDown );
        }

        public void ExportIntegration()
        {
			/*SaveFileDialog exportDialog = new SaveFileDialog();
            string filepath = CurrentGraphForm.Sources[0].Source.CurrentFilepath;
            exportDialog.InitialDirectory = Path.GetDirectoryName( filepath );
			exportDialog.OverwritePrompt = true;
			exportDialog.RestoreDirectory = true;
            exportDialog.FileName = Path.GetFileNameWithoutExtension( filepath ) + "-peaks.csv";
			if( exportDialog.ShowDialog() == DialogResult.OK )
			{
				StreamWriter writer = new StreamWriter( exportDialog.FileName );
				writer.WriteLine( "Id,Area" );
                foreach( DataGridViewRow row in dataSourceMap[filepath].ChromatogramListForm.GridView.Rows )
                {
                    GraphItem g = row.Tag as GraphItem;
                    writer.WriteLine( "{0},{1}", g.Id, g.TotalIntegratedArea );
                }
				writer.Close();
			}*/
		}

        public void ShowCurrentSourceAsMzML()
        {
            GraphForm currentGraphForm = CurrentGraphForm;
            if( currentGraphForm == null )
                throw new Exception( "current graph should not be null" );

            Form previewForm = new Form();
            previewForm.StartPosition = FormStartPosition.CenterParent;
            previewForm.Text = "MzML preview of " + currentGraphForm.PaneList[0][0].Source.Source.CurrentFilepath;
            TextBox previewText = new TextBox();
            previewText.Multiline = true;
            previewText.ReadOnly = true;
            previewText.Dock = DockStyle.Fill;
            previewForm.Controls.Add( previewText );

            //previewForm.Show( mainForm );
            //Application.DoEvents();

            string tmp = Path.GetTempFileName();
            System.Threading.ParameterizedThreadStart threadStart = new System.Threading.ParameterizedThreadStart( startWritePreviewMzML );
            System.Threading.Thread writeThread = new System.Threading.Thread( threadStart );
            writeThread.Start( new KeyValuePair<string, MSData>( tmp, currentGraphForm.PaneList[0][0].Source.Source.MSDataFile ));
            writeThread.Join( 1000 );
            while( writeThread.IsAlive )
            {
                FileStream tmpStream = File.Open( tmp, FileMode.Open, FileAccess.Read, FileShare.None );
                previewText.Text = new StreamReader( tmpStream ).ReadToEnd();
                writeThread.Join( 1000 );
            }
        }

        private void startWritePreviewMzML( object threadArg )
        {
            KeyValuePair<string, MSDataFile> sourcePair = (KeyValuePair<string, MSDataFile>) threadArg;
            sourcePair.Value.write( sourcePair.Key );
        }
	}
}
