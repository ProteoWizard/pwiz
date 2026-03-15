/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using DigitalRune.Windows.Docking;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.CommonMsData;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.AuditLog;
using pwiz.Skyline.Controls.Clustering;
using pwiz.Skyline.Controls.FilesTree;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.Graphs.Calibration;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.ElementLocators.ExportAnnotations;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using pwiz.Skyline.Controls.Lists;
using pwiz.Skyline.Menus;
using pwiz.Skyline.Model.Lib;
using ZedGraph;
using PeptideDocNode = pwiz.Skyline.Model.PeptideDocNode;
using User32 = pwiz.Common.SystemUtil.PInvoke.User32;

namespace pwiz.Skyline
{
    public partial class SkylineWindow :
        GraphSpectrum.IStateProvider,
        GraphChromatogram.IStateProvider,
        GraphSummary.IStateProvider
    {
        private GraphSpectrum _graphSpectrum;
        private GraphFullScan _graphFullScan;
        private readonly GraphSpectrumSettings _graphSpectrumSettings;

        private readonly List<GraphSummary> _listGraphRetentionTime = new List<GraphSummary>();
        private readonly List<GraphSummary> _listGraphPeakArea = new List<GraphSummary>();
        private readonly List<GraphSummary> _listGraphMassError = new List<GraphSummary>();
        private readonly List<GraphSummary> _listGraphDetections = new List<GraphSummary>();

        private LiveResultsGrid _resultsGridForm;
        private DocumentGridForm _documentGridForm;
        private CalibrationForm _calibrationForm;
        private AuditLogForm _auditLogForm;
        private CandidatePeakForm _candidatePeakForm;
        public static int MAX_GRAPH_CHROM => Settings.Default.MaxChromatogramGraphs; // Never show more than this many chromatograms, lest we hit the Windows handle limit
        private readonly List<GraphChromatogram> _listGraphChrom = new List<GraphChromatogram>(); // List order is MRU, with oldest in position 0
        private bool _inGraphUpdate;
        private bool _alignToPrediction;
        private bool _shouldShowFilesTree;

        public RTGraphController RTGraphController
        {
            get
            {
                var active = _listGraphRetentionTime.FirstOrDefault();

                if (active == null)
                    return null;
                return active.Controller as RTGraphController;
            }
        }



        private void dockPanel_ActiveDocumentChanged(object sender, EventArgs e)
        {
            try
            {
                ActiveDocumentChanged();
            }
            catch (Exception x)
            {
                Program.ReportException(x);
            }
        }

        private void ActiveDocumentChanged()
        {
            if (DocumentUI == null)
                return;
            var settings = DocumentUI.Settings;
            if (_closing || ComboResults == null || ComboResults.IsDisposed || _inGraphUpdate || !settings.HasResults ||
                settings.MeasuredResults.Chromatograms.Count < 2)
                return;

            var activeForm = dockPanel.ActiveDocument;

            bool activeLibrary = ReferenceEquals(_graphSpectrum, activeForm);
            _listGraphPeakArea.ForEach(g => g.ActiveLibrary = activeLibrary);
            _listGraphRetentionTime.ForEach(g => g.ActiveLibrary = activeLibrary);

            foreach (var graphChrom in _listGraphChrom.ToArray()) // List may be updating concurrent with this access, so convert to array first
            {
                if (ReferenceEquals(graphChrom, activeForm))
                    ComboResults.SelectedItem = graphChrom.TabText;
            }
        }

        public class DockPanelLayoutLock : IDisposable
        {
            private DockPanel _dockPanel;
            private Control _coverControl;
            private Cursor _cursorBegin;
            private bool _locked;
            private HashSet<Control> _suspendedControls;

            public DockPanelLayoutLock(DockPanel dockPanel, bool startLocked = false)
            {
                _dockPanel = dockPanel;
                if (startLocked)
                    EnsureLocked();
            }

            /// <summary>
            /// Called to lock layout of the <see cref="DockPanel"/>.  Locking
            /// is deferred until it is determined to be necessary to avoid the
            /// relayout calculation when locking is unnecessary.
            /// </summary>
            public void EnsureLocked()
            {
                if (!_locked)
                {
                    _locked = true;
                    _dockPanel.SuspendLayout(true);
                    _coverControl = new CoverControl(_dockPanel);
                    var cursorControl = _dockPanel.TopLevelControl ?? _dockPanel;
                    _cursorBegin = cursorControl.Cursor;
                    cursorControl.Cursor = Cursors.WaitCursor;
                    Assume.IsNull(_suspendedControls);
                    _suspendedControls = new HashSet<Control>();
                    foreach (var pane in _dockPanel.Panes)
                    {
                        EnsurePaneLocked(pane);
                    }
                }
            }

            public void Dispose()
            {
                if (_locked && _dockPanel != null)
                {
                    _dockPanel.ResumeLayout(true, true);
                    _coverControl.Dispose();
                    var cursorControl = _dockPanel.TopLevelControl ?? _dockPanel;
                    cursorControl.Cursor = _cursorBegin;
                }
                if (_suspendedControls != null)
                {
                    foreach (var control in _suspendedControls)
                    {
                        control.ResumeLayout();
                    }
                    _suspendedControls = null;
                }
                _dockPanel = null;  // Only once
            }

            /// <summary>
            /// Ensures that "SuspendControl" has been called on the DockPane, as well
            /// as its controls (specifically its DockPaneStrip which spends a long time
            /// redrawing as each child is added).
            /// </summary>
            /// <param name="dockPane"></param>
            public void EnsurePaneLocked(DockPane dockPane)
            {
                if (SuspendControl(dockPane))
                {
                    foreach (var control in dockPane.Controls.OfType<Control>())
                    {
                        SuspendControl(control);
                    }
                }
            }

            /// <summary>
            /// Ensures SuspendLayout has called on the control
            /// </summary>
            /// <returns>false if the control has already been suspended</returns>
            private bool SuspendControl(Control control)
            {
                if (control == null || _suspendedControls == null)
                {
                    return false;
                }
                if (!_suspendedControls.Add(control))
                {
                    return false;
                }
                control.SuspendLayout();
                return true;
            }
        }

        private void UpdateGraphUI(SrmSettings settingsOld, bool docIdChanged)
        {
            SrmSettings settingsNew = DocumentUI.Settings;
            if (ReferenceEquals(settingsNew, settingsOld))
            {
                // Just about any change could potentially change the list
                // or retention times or peak areas.
                if (settingsNew.HasResults)
                {
                    // UpdateGraphPanes can handle null values in the list, but
                    // only call it when at least one of the graphs is present.
                    if (_listGraphMassError.Any() || _listGraphPeakArea.Any() || _listGraphRetentionTime.Any())
                        UpdateGraphPanes(new List<IUpdatable>(_listGraphMassError.Concat(_listGraphPeakArea.Concat(_listGraphRetentionTime))));
                }
                return;                
            }
            var listUpdateGraphs = new List<IUpdatable>();
            var filterNew = settingsNew.TransitionSettings.Filter;
            var filterOld = settingsOld.TransitionSettings.Filter;
            if (!ReferenceEquals(filterNew, filterOld))
            {
                // If ion types or charges changed, make sure the new
                // ones are on and the old ones are off by default.
                bool refresh = false;
                if (!ArrayUtil.EqualsDeep(filterNew.PeptideIonTypes, filterOld.PeptideIonTypes) ||
                    !ArrayUtil.EqualsDeep(filterNew.SmallMoleculeIonTypes, filterOld.SmallMoleculeIonTypes))
                {
                    // Only turn off old ion types, if new settings are not MS1-only full-scan
                    var fullScan = settingsNew.TransitionSettings.FullScan;
                    ViewMenu.EnableProteomicIons(DocumentUI.DocumentType != SrmDocument.DOCUMENT_TYPE.small_molecules);
                    ViewMenu.EnableSmallMoleculeIons(DocumentUI.HasSmallMolecules);
                    if (!fullScan.IsEnabled || fullScan.IsEnabledMsMs)
                    {
                        CheckIonTypes(filterOld.PeptideIonTypes, false);
                        CheckIonTypes(filterOld.SmallMoleculeIonTypes, false);
                    }
                    CheckIonTypes(filterNew.PeptideIonTypes, true);
                    CheckIonTypes(filterNew.SmallMoleculeIonTypes, true);
                    refresh = true;
                }

                // Charge selection
                if (!ArrayUtil.EqualsDeep(filterNew.PeptideProductCharges, filterOld.PeptideProductCharges) ||
                    !ArrayUtil.EqualsDeep(filterNew.SmallMoleculeFragmentAdducts, filterOld.SmallMoleculeFragmentAdducts))
                {
                    // First clear any old charge enabling
                    CheckIonCharges(filterOld.PeptideProductCharges, false);
                    CheckIonCharges(filterOld.SmallMoleculeFragmentAdducts, false);
                    // Then enable based on settings and document contents
                    switch (DocumentUI.DocumentType)
                    {
                        case SrmDocument.DOCUMENT_TYPE.none:
                        case SrmDocument.DOCUMENT_TYPE.proteomic:
                            CheckIonCharges(filterNew.PeptideProductCharges, true);
                            break;
                        case SrmDocument.DOCUMENT_TYPE.small_molecules:
                            CheckIonCharges(filterNew.SmallMoleculeFragmentAdducts, true);
                            break;
                        case SrmDocument.DOCUMENT_TYPE.mixed:
                            CheckIonCharges(filterNew.PeptideProductCharges, true);
                            CheckIonCharges(filterNew.PeptideProductCharges, true);
                            break;
                    }
                    refresh = true;
                }

                if (refresh && _graphSpectrum != null)
                    listUpdateGraphs.Add(_graphSpectrum);
            }

            using (var layoutLock = new DockPanelLayoutLock(dockPanel))
            {
                bool deserialized = false;
                string layoutFile = GetViewFile(DocumentFilePath);
                if (docIdChanged && File.Exists(layoutFile))
                {
                    layoutLock.EnsureLocked();
                    try
                    {
                        using (var layoutReader = new StreamReader(layoutFile))
                        {
                            LoadLayout(layoutReader.BaseStream);
                        }
                        deserialized = true;
                    }
                    catch (Exception x)
                    {
                        var message = TextUtil.LineSeparate(string.Format(SkylineResources.SkylineWindow_UpdateGraphUI_Failure_attempting_to_load_the_window_layout_file__0__, layoutFile),
                                                                            SkylineResources.SkylineWindow_UpdateGraphUI_Rename_or_delete_this_file_to_restore_the_default_layout, 
                                                                            SkylineResources.SkylineWindow_UpdateGraphUI_Skyline_may_also_need_to_be_restarted);
                        throw new IOException(message, x);
                    }
                }

                ViewMenu.UpdateGraphUi(layoutLock.EnsureLocked, settingsNew, deserialized);

                var enable = settingsNew.HasResults;
                if (_graphFullScan != null && _graphFullScan.Visible && !enable)
                {
                    layoutLock.EnsureLocked();
                    DestroyGraphFullScan();
                }

                if (!ReferenceEquals(settingsNew.MeasuredResults, settingsOld.MeasuredResults))
                {
                    // First hide all graph windows for results that no longer exist in the document
                    foreach (var graphChromatogram in _listGraphChrom.ToArray())
                    {
                        string name = graphChromatogram.NameSet;
                        // Look for matching chromatogram sets across the documents
                        ChromatogramSet chromSetOld;
                        ChromatogramSet chromSetNew;
                        if (settingsOld.HasResults &&
                            settingsOld.MeasuredResults.TryGetChromatogramSet(name, out chromSetOld, out _) &&
                            settingsNew.HasResults &&
                            settingsNew.MeasuredResults.TryGetChromatogramSet(chromSetOld.Id.GlobalIndex, out chromSetNew, out _))
                        {
                            // If matching chromatogram found, but name has changed, then
                            // update the graph pane
                            if (!Equals(chromSetNew.Name, chromSetOld.Name))
                                name = graphChromatogram.NameSet = chromSetNew.Name;
                        }
                        var results = settingsNew.MeasuredResults;
                        if (results == null || !results.Chromatograms.Contains(chrom => Equals(chrom.Name, name)))
                        {
                            layoutLock.EnsureLocked();
                            ShowGraphChrom(graphChromatogram.NameSet, false);
                            // If changed to a new document, destroy unused panes
                            if (docIdChanged)
                            {
                                var graphChrom = GetGraphChrom(name);
                                if (graphChrom != null)
                                {
                                    RemoveGraphChromFromList(graphChrom);
                                }
                            }
                        }
                    }
                    // Next show any graph windows for results that were not previously part of
                    // the document.
                    if (settingsNew.MeasuredResults != null && !deserialized)
                    {
                        // Keep changes in graph panes from stealing the focus
                        var focusStart = User32.GetFocusedControl();
                        _inGraphUpdate = true;
                        try
                        {
                            string nameFirst = null;
                            string nameLast = SelectedGraphChromName;

                            foreach (var chromatogram in settingsNew.MeasuredResults.Chromatograms)
                            {
                                string name = chromatogram.Name;
                                var graphChrom = GetGraphChrom(name);
                                if (graphChrom == null)
                                {
                                    if (_listGraphChrom.Count < MAX_GRAPH_CHROM) // Limit window count to conserve win32 handles
                                    {
                                        layoutLock.EnsureLocked();
                                        graphChrom = CreateGraphChrom(name, nameLast, false);
                                        layoutLock.EnsurePaneLocked(graphChrom.Pane);

                                        nameFirst = nameFirst ?? name;
                                        nameLast = name;
                                    }
                                }
                                    // If the pane is not showing a tab for this graph, than add one.
                                else if (graphChrom.Pane == null ||
                                         !graphChrom.Pane.DisplayingContents.Contains(graphChrom))
                                {
                                    layoutLock.EnsureLocked();
                                    ShowGraphChrom(name, true);

                                    nameFirst = nameFirst ?? name;
                                    nameLast = name;
                                }
                            }

                            // Put the first set on top, since it will get populated with data first
                            if (nameFirst != null)
                            {
                                layoutLock.EnsureLocked();
                                ShowGraphChrom(nameFirst, true);                                
                            }
                        }
                        finally
                        {
                            _inGraphUpdate = false;
                        }

                        if (focusStart != null)
                            focusStart.Focus();
                    }

                    // Update displayed graphs, which are no longer valid
                    var listGraphUpdate = from graph in _listGraphChrom
                                          where graph.Visible
                                          where !graph.IsCurrent(settingsOld, settingsNew)
                                          select graph;
                    listUpdateGraphs.AddRange(listGraphUpdate.ToArray());

//                    UpdateReplicateMenuItems(enabled);

                    // CONSIDER: Enable/disable submenus too?
                }
                else if (!ReferenceEquals(settingsNew.PeptideSettings.Prediction.RetentionTime,
                                          settingsOld.PeptideSettings.Prediction.RetentionTime))
                {
                    // If retention time regression changed, and retention prediction is showing
                    // update all displayed graphs
                    if (Settings.Default.ShowRetentionTimePred)
                        listUpdateGraphs.AddRange(_listGraphChrom.ToArray());
                }
            } // layoutLock.Dispose()

            // Do this after layout is unlocked, because it messes up the selected graph otherwise
            if (_sequenceTreeForm == null)
            {
                ShowSequenceTreeForm(true);
            }

            // Just about any change could potentially change these panes.
            if (settingsNew.HasResults)
            {
                listUpdateGraphs.AddRange(_listGraphRetentionTime);
                listUpdateGraphs.AddRange(_listGraphPeakArea);
                listUpdateGraphs.AddRange(_listGraphMassError);
            }

            UpdateGraphPanes(listUpdateGraphs);
            FoldChangeForm.CloseInapplicableForms(this);
            ListGridForm.CloseInapplicableForms(this);
        }

        public void UpdateGraphSpectrumEnabled()
        {
            using (var layoutLock = new DockPanelLayoutLock(dockPanel))
            {
                ViewMenu.EnableGraphSpectrum(layoutLock.EnsureLocked, DocumentUI.Settings, false);
            }
        }

        private void RemoveGraphChromFromList(GraphChromatogram graphChrom)
        {
            DestroyGraphChrom(graphChrom);
        }

        // Load view layout from the given stream.
        public void LoadLayout(Stream layoutStream)
        {
            using (new DockPanelLayoutLock(dockPanel, true))
            {
                if (Program.SkylineOffscreen)
                {
                    layoutStream = MoveLayoutOffScreen(layoutStream);
                }
                LoadLayoutLocked(layoutStream);
            }
        }

        // Load view layout from the given stream.
        private void LoadLayoutLocked(Stream layoutStream)
        {
            // Get rid of any existing graph windows, since the layout
            // deserialization has problems using existing windows.
            DestroySequenceTreeForm();
            DestroyGraphSpectrum();
            DestroyFilesTreeForm();

            var type = RTGraphController.GraphType;
            _listGraphRetentionTime.ToList().ForEach(DestroyGraphRetentionTime);
            RTGraphController.GraphType = type;
           
            type = AreaGraphController.GraphType;
            _listGraphPeakArea.ToList().ForEach(DestroyGraphPeakArea);
            AreaGraphController.GraphType = type;

            type = MassErrorGraphController.GraphType;
            _listGraphMassError.ToList().ForEach(DestroyGraphMassError);
            MassErrorGraphController.GraphType = type;

            type = DetectionsGraphController.GraphType;
            _listGraphDetections.ToList().ForEach(DestroyGraphDetections);
            DetectionsGraphController.GraphType = type;

            FormUtil.OpenForms.OfType<FoldChangeForm>().ForEach(f => f.Close());
            FormUtil.OpenForms.OfType<ListGridForm>().ForEach(f => f.Close());

            DestroyResultsGrid();
            DestroyDocumentGrid();
            DestroyAuditLogForm();
            DestroyCalibrationForm();
            DestroyCandidatePeakForm();

            DestroyImmediateWindow();
            HideFindResults(true);
            foreach (var graphChrom in _listGraphChrom.ToArray())
                DestroyGraphChrom(graphChrom);
            DestroyGraphFullScan();
            dockPanel.LoadFromXml(layoutStream, DeserializeForm);

            InsertFilesViewIntoLegacyLayout();

            // TreeViews resizes often prior to display, so we must restore horizontal scrolling after
            // all resizing has occurred
            ResetHorizontalScroll(SequenceTree);
            ResetHorizontalScroll(FilesTree);

            EnsureFloatingWindowsVisible();
        }

        private static void ResetHorizontalScroll(TreeViewMS treeView)
        {
            if (treeView == null)
                return;
            treeView.UpdateTopNode();
            treeView.SetScrollPos(Orientation.Horizontal, 0);
        }

        private void InsertFilesViewIntoLegacyLayout()
        {
            if (_filesTreeForm == null && _shouldShowFilesTree)
            {
                // Store whatever is active now
                var activeForm = dockPanel.ActiveContent as DockableForm;

                // First time displaying FilesTree so no view state to restore
                _filesTreeForm = CreateFilesTreeForm(null);
            
                // If SequenceTree exists, put FilesTree in a tab behind SequenceTree
                if (_sequenceTreeForm != null) 
                {
                    var sequenceTreeDockState = _sequenceTreeForm.DockState;
                    if (sequenceTreeDockState != DockState.Hidden)
                    {
                        var sequencePane = _sequenceTreeForm.Pane;
                        // Show FilesTree in the same pane as SequenceTree - note that it is not
                        // possible to show after the SequenceTree. So, we activate it after showing.
                        if (sequencePane != null)
                        {
                            // Add as a tab in the same pane
                            _filesTreeForm.Show(sequencePane, null);
                        }
                        else
                        {
                            // Hacky fallback that often works if pane is null
                            _filesTreeForm.Show(dockPanel, sequenceTreeDockState);
                        }

                        // Activate SequenceTree again to keep it on top but re-activate whatever was active before
                        _sequenceTreeForm.Activate();
                    }
                    // If SequenceTree is hidden, skip.
                    // CONSIDER: if SequenceTree exists but is hidden, FilesTree cannot be added. Ignoring that case for now.

                    activeForm?.Activate();
                }
                else
                {
                    // Could not find SequenceTree so put Files in its default location
                    _filesTreeForm.Show(dockPanel, DockState.DockLeft);
                }
            
                _shouldShowFilesTree = false;
            }
        }

        /// <summary>
        /// Change the "Bounds" attribute of the "FloatingWindow" elements in the .sky.view file
        /// to a point offscreen.
        /// </summary>
        private static MemoryStream MoveLayoutOffScreen(Stream layoutStream)
        {
            const string attrBounds = @"Bounds";
            var xd = new XmlDocument();
            xd.Load(layoutStream);
            var rectangleConverter = new RectangleConverter();
            foreach (XmlElement el in xd.SelectNodes(@"//FloatingWindow")!)
            {
                var strBounds = el.GetAttribute(attrBounds);
                if (!string.IsNullOrEmpty(strBounds))
                {
                    if (rectangleConverter.ConvertFromInvariantString(el.GetAttribute(attrBounds)) 
                        is Rectangle rectBounds)
                    {
                        var newBounds = new Rectangle(GetOffscreenPoint(), rectBounds.Size);
                        el.SetAttribute(attrBounds, rectangleConverter.ConvertToInvariantString(newBounds));
                    }
                }
            }

            var memoryStream = new MemoryStream();
            var xmlTextWriter = new XmlTextWriter(memoryStream, new UTF8Encoding(false)) // UTF-8 without BOM
            {
                Formatting = Formatting.Indented
            };
            xd.Save(xmlTextWriter);
            memoryStream.Position = 0;
            return memoryStream;
        }

        public void DestroyAllChromatogramsGraph()
        {
            // Remove any multi-progress left in the list
            lock (_listProgress)
            {
                int multiIndex = _listProgress.IndexOf(s => s is MultiProgressStatus);
                if (multiIndex != -1)
                    _listProgress.RemoveAt(multiIndex);
            }
            if (ImportingResultsWindow != null)
            {
                ImportingResultsWindow.Close();
                ImportingResultsWindow = null;

                // Reset progress for the current document
                _chromatogramManager.ResetProgress(Document);
            }
        }

        public void InvalidateChromatogramGraphs()
        {
            _listGraphChrom.ForEach(graph => graph.IsCacheInvalidated = true);
        }

        private void EnsureFloatingWindowsVisible()
        {
            if (Program.SkylineOffscreen)
                return;

            foreach (var floatingWindow in dockPanel.FloatingWindows)
            {
                var screen = Screen.FromControl(floatingWindow);
                var rectScreen = screen.WorkingArea;
                var rectWindow = floatingWindow.Bounds;
                rectWindow.Width = Math.Min(rectWindow.Width, rectScreen.Width);
                rectWindow.Height = Math.Min(rectWindow.Height, rectScreen.Height);
                rectWindow.X = Math.Max(rectScreen.X,
                    Math.Min(rectWindow.X, rectScreen.X + rectScreen.Width - rectWindow.Width));
                rectWindow.Y = Math.Max(rectScreen.Y,
                    Math.Min(rectWindow.Y, rectScreen.Y + rectScreen.Height - rectWindow.Height));
                if (!Equals(rectWindow, floatingWindow.Bounds))
                    floatingWindow.Bounds = rectWindow;
            }
        }

        private IDockableForm DeserializeForm(string persistentString)
        {
            if (persistentString.StartsWith(typeof(SequenceTreeForm).ToString()))
            {
                return _sequenceTreeForm ?? CreateSequenceTreeForm(persistentString);
            }
            else if (Equals(persistentString, typeof(GraphSpectrum).ToString()))
            {
                return _graphSpectrum ?? CreateGraphSpectrum();                
            }
            else if (persistentString.StartsWith(typeof(FilesTreeForm).ToString()))
            {
                // show FilesTree if it has serialized state in the .view file
                return FilesTreeForm ?? CreateFilesTreeForm(persistentString);
            }

            var split = persistentString.Split('|');
            var splitLength = split.Length;
            var graphSummaryTypeName = split[0];
            var controllerTypeName = string.Empty;
            var graphTypeName = string.Empty;

            if (splitLength > 1)
                controllerTypeName = split[1];
            if (splitLength > 2)
                graphTypeName = split[2];

            // Backward compatibility
            if (persistentString.EndsWith(@"Skyline.Controls.GraphRetentionTime") ||
                splitLength == 2 && graphSummaryTypeName == typeof(GraphSummary).ToString() &&
                controllerTypeName == typeof(RTGraphController).Name)
            {
                var type = RTGraphController.GraphType;
                return _listGraphRetentionTime.FirstOrDefault(g => g.Type == type) ?? CreateGraphRetentionTime(type);
            }

            // Backward compatibility
            if (persistentString.EndsWith(@"Skyline.Controls.GraphPeakArea") ||
            splitLength == 2 && graphSummaryTypeName == typeof(GraphSummary).ToString() &&
            controllerTypeName == typeof(AreaGraphController).Name)
            {
                var type = AreaGraphController.GraphType;
                return _listGraphPeakArea.FirstOrDefault(g => g.Type == type) ?? CreateGraphPeakArea(type);
            }

            // Backward compatibility
            if (splitLength == 2 && graphSummaryTypeName == typeof(GraphSummary).ToString() &&
                controllerTypeName == typeof(MassErrorGraphController).Name)
            {
                var type = MassErrorGraphController.GraphType;
                return _listGraphMassError.FirstOrDefault(g => g.Type == type) ?? CreateGraphMassError(type);
            }

            if (splitLength >= 3 && graphSummaryTypeName == typeof(GraphSummary).ToString())
            {
                var type = Helpers.ParseEnum(graphTypeName, GraphTypeSummary.invalid);

                GraphSummary graphSummary = null;
                if (controllerTypeName == typeof(RTGraphController).Name)
                    graphSummary = _listGraphRetentionTime.FirstOrDefault(g => g.Type == type) ?? CreateGraphRetentionTime(type);
                else if (controllerTypeName == typeof(AreaGraphController).Name)
                    graphSummary = _listGraphPeakArea.FirstOrDefault(g => g.Type == type) ?? CreateGraphPeakArea(type);
                else if (controllerTypeName == typeof(MassErrorGraphController).Name)
                    graphSummary = _listGraphMassError.FirstOrDefault(g => g.Type == type) ?? CreateGraphMassError(type);
                else if (controllerTypeName == typeof(DetectionsGraphController).Name)
                    graphSummary = _listGraphDetections.FirstOrDefault(g => g.Type == type) ?? CreateGraphDetections(type);
                if (graphSummary != null && splitLength > 3)
                    graphSummary.LabelLayoutString = Uri.UnescapeDataString(split[3]);
                return graphSummary;
            }

            if (Equals(persistentString, typeof(ResultsGridForm).ToString()) || Equals(persistentString, typeof (LiveResultsGrid).ToString()))
            {
                return _resultsGridForm ?? CreateResultsGrid();
            }
            if (Equals(persistentString, typeof(CandidatePeakForm).ToString()))
            {
                return _candidatePeakForm ?? CreateCandidatePeakForm();
            }
            if (Equals(persistentString, typeof (DocumentGridForm).ToString()))
            {
                return _documentGridForm ?? CreateDocumentGrid();
            }
            if (Equals(persistentString, typeof (CalibrationForm).ToString()))
            {
                return _calibrationForm ?? CreateCalibrationForm();
            }
            if (Equals(persistentString, typeof(AuditLogForm).ToString()))
            {
                return _auditLogForm ?? CreateAuditLogForm();
            }
            if (persistentString.StartsWith(typeof(ListGridForm).ToString()))
            {
                return CreateListForm(ListGridForm.GetListName(persistentString));
            }
            if (Equals(persistentString, typeof(ImmediateWindow).ToString()))
            {
                return _immediateWindow ?? CreateImmediateWindow();
            }
            if (persistentString.StartsWith(typeof(GraphChromatogram).ToString()))
            {
                if (_listGraphChrom.Count >= MAX_GRAPH_CHROM)
                {
                    return null;
                }
                string name = GraphChromatogram.GetTabText(persistentString);
                var settings = DocumentUI.Settings;
                if (settings.HasResults)
                {
                    bool hasName = settings.MeasuredResults.ContainsChromatogram(name);
                    // For tests with persisted layouts containing the default chromatogram name
                    // check for the default name in the current language
                    if (!hasName && Equals(name, Resources.ResourceManager.GetString(
                        @"ImportResultsDlg_DefaultNewName_Default_Name", CultureInfo.InvariantCulture)))
                    {
                        name = Resources.ImportResultsDlg_DefaultNewName_Default_Name;
                        hasName = settings.MeasuredResults.ContainsChromatogram(name);
                    }
                    if (hasName)
                        return GetGraphChrom(name) ?? CreateGraphChrom(name);
                }
            }

            var databoundForm = (IDockableForm) FoldChangeForm.RestoreFoldChangeForm(this, persistentString)
                                ?? DataboundGraph.RestoreDataboundGraph(this, persistentString);
            if (null != databoundForm)
            {
                return databoundForm;
            }

            if (Equals(persistentString, typeof(GraphFullScan).ToString()))
            {
                return _graphFullScan ?? CreateGraphFullScan();
            }
            return null;
        }

        public void UpdateGraphPanes()
        {
            // Add only visible graphs to the update list, since each update
            // must pass through the Windows message queue on a WM_TIMER.
            var listVisibleChrom = from graphChrom in _listGraphChrom
                                   where graphChrom.Visible
                                   select graphChrom;

            var listUpdateGraphs = new List<IUpdatable>(listVisibleChrom.ToArray());
            
            foreach(var spectrumGraph in ListMzScaleCopyables())
                if(spectrumGraph is IUpdatable updatable)
                    listUpdateGraphs.Add(updatable);

            listUpdateGraphs.AddRange(_listGraphRetentionTime.Where(g => g.Visible));
            listUpdateGraphs.AddRange(_listGraphPeakArea.Where(g => g.Visible));
            listUpdateGraphs.AddRange(_listGraphMassError.Where(g => g.Visible));
            listUpdateGraphs.AddRange(_listGraphDetections.Where(g => g.Visible));
            if (_calibrationForm != null && _calibrationForm.Visible)
                listUpdateGraphs.Add(_calibrationForm);

            UpdateGraphPanes(listUpdateGraphs);
            // make sure the Volcano Plot is updated as well
            FormUtil.OpenForms.OfType<FoldChangeVolcanoPlot>().ForEach(form => form.QueueUpdateGraph());
        }

        private void UpdateGraphPanes(ICollection<IUpdatable> graphPanes)
        {
            if (graphPanes.Count == 0)
                return;
            // Restart the timer at 100ms, giving the UI time to interrupt.
            _timerGraphs.Stop();
            _timerGraphs.Interval = 100;
            var previousGraphPanes = _timerGraphs.Tag as ICollection<IUpdatable>;
            if (previousGraphPanes != null && previousGraphPanes.Count > 0)
            {
                _timerGraphs.Tag = previousGraphPanes.Concat(graphPanes).Distinct().ToList();
            }
            else
            {
                _timerGraphs.Tag = graphPanes;
            }
            _timerGraphs.Start();
        }

        private void UpdateGraphPanes(object sender, EventArgs e)
        {
            // Stop the timer immediately, to keep from getting called again
            // for the same triggering event.
            _timerGraphs.Stop();

            IList<IUpdatable> listGraphPanes = (IList<IUpdatable>)_timerGraphs.Tag;
            int count = 0;
            if (listGraphPanes != null && listGraphPanes.Count > 0)
            {
                // Allow nulls in the list
                while (listGraphPanes.Count > 0 && listGraphPanes[0] == null)
                    listGraphPanes.RemoveAt(0);

                if (listGraphPanes.Count > 0)
                {
                    listGraphPanes[0].UpdateUI();
                    listGraphPanes.RemoveAt(0);                    
                }
                count = listGraphPanes.Count;
            }
            if (count != 0)
            {
                // If more graphs to update, update them as quickly as possible.
                _timerGraphs.Interval = 1;
                _timerGraphs.Start();
            }
        }

        /// <summary>
        /// Returns true if the graph panels still need to be updated to show the current selection.
        /// Used for testing. 
        /// </summary>
        public bool IsGraphUpdatePending
        {
            get
            {
                // The implementation of "UpdateGraphs" is such that this question should only be
                // asked on the event thread
                if (InvokeRequired)
                {
                    throw new InvalidOperationException(
                        SkylineResources.SkylineWindow_IsGraphUpdatePending_Must_be_called_from_event_thread);
                }
                return _timerGraphs.Enabled || (_graphSpectrum != null && _graphSpectrum.IsGraphUpdatePending) || ProductionFacility.DEFAULT.IsWaiting();
            }
        }

        public bool AlignToRtPrediction
        {
            get { return _alignToPrediction; }
            set
            {
                if (value == AlignToRtPrediction)
                {
                    return;
                }
                _alignToPrediction = value;
                UpdateGraphPanes();
            }
        }

        public GraphValues.IRetentionTimeTransformOp GetRetentionTimeTransformOperation()
        {
            if (AlignToRtPrediction)
            {
                return new GraphValues.RetentionTimeAlignmentTransformOp(Document.Settings);
            }
            return null;
        }


        #region Spectrum graph

        public GraphSpectrum GraphSpectrum { get { return _graphSpectrum; } }
        public GraphSpectrumSettings GraphSpectrumSettings { get { return _graphSpectrumSettings; } }

        public void ShowAIons(bool show)
        {
            _graphSpectrumSettings.ShowAIons = show;
        }
        public void ShowBIons(bool show)
        {
            _graphSpectrumSettings.ShowBIons = show;
        }
        public void ShowCIons(bool show)
        {
            _graphSpectrumSettings.ShowCIons = show;
        }
        public void ShowXIons(bool show)
        {
            _graphSpectrumSettings.ShowXIons = show;
        }
        public void ShowYIons(bool show)
        {
            _graphSpectrumSettings.ShowYIons = show;
        }
        public void ShowZIons(bool show)
        {
            _graphSpectrumSettings.ShowZIons = show;
        }
        public void ShowZHIons(bool show)
        {
            _graphSpectrumSettings.ShowZHIons = show;
        }
        public void ShowZHHIons(bool show)
        {
            _graphSpectrumSettings.ShowZHHIons = show;
        }
        public void ShowFragmentIons(bool show)
        {
            _graphSpectrumSettings.ShowFragmentIons = show;
        }
        public void ShowPrecursorIon(bool show)
        {
            _graphSpectrumSettings.ShowPrecursorIon = show;
        }
        public void ShowSpecialIons(bool show)
        {
            _graphSpectrumSettings.ShowSpecialIons = show;
        }
        public void ShowCharge1(bool show)
        {
            _graphSpectrumSettings.ShowCharge1 = show;
        }
        public void ShowCharge2(bool show)
        {
            _graphSpectrumSettings.ShowCharge2 = show;
        }
        public void ShowCharge3(bool show)
        {
            _graphSpectrumSettings.ShowCharge3 = show;
        }
        public void ShowCharge4(bool show)
        {
            _graphSpectrumSettings.ShowCharge4 = show;
        }

        public void ShowLosses(IEnumerable<string> losses)
        {
            _graphSpectrumSettings.ShowLosses = new List<string>(losses);
        }

        public void IonTypeSelector_IonTypeChanges(IonType type, bool show)
        {
            switch (type)
            {
                case IonType.a:
                    ShowAIons(show);
                    break;
                case IonType.b:
                    ShowBIons(show);
                    break;
                case IonType.c:
                    ShowCIons(show);
                    break;
                case IonType.x:
                    ShowXIons(show);
                    break;
                case IonType.y:
                    ShowYIons(show);
                    break;
                case IonType.z:
                    ShowZIons(show);
                    break;
                case IonType.zh:
                    ShowZHIons(show);
                    break;
                case IonType.zhh:
                    ShowZHHIons(show);
                    break;
            }
        }

        public void IonChargeSelector_ionChargeChanged(int charge, bool show)
        {
            switch (charge)
            {
                case 1:
                    ShowCharge1(show);
                    break;
                case 2:
                    ShowCharge2(show);
                    break;
                case 3:
                    ShowCharge3(show);
                    break;
                case 4:
                    ShowCharge4(show);
                    break;
            }
        }

        public void IonTypeSelector_LossChanged(string[] losses)
        {
            ShowLosses(losses);
        }

        public void SynchMzScaleToolStripMenuItemClick(bool syncMz, IMzScalePlot source = null)
        {
            if (ListMzScaleCopyables().Count() < 2)
                return;
            Settings.Default.SyncMZScale = syncMz;
            if (!Settings.Default.SyncMZScale)
                return;

            if (source == null)
                return;

            foreach (var targetGraph in ListMzScaleCopyables())
            {
                if(!ReferenceEquals(targetGraph, source))
                    targetGraph.SetMzScale(source.Range);
            }
        }

        // Testing support
        public void SynchMzScale(IMzScalePlot source, bool setSynchMz = true)
        {
            SynchMzScaleToolStripMenuItemClick(setSynchMz, source);
        }


        private void editToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            EditMenu.EditToolStripMenuItemDropDownOpening();
        }
        public ChromFileInfoId GetSelectedChromFileId()
        {
            var document = Document;
            if (!document.Settings.HasResults || SelectedResultsIndex < 0 || SelectedResultsIndex >= document.Settings.MeasuredResults.Chromatograms.Count)
            {
                return null;
            }
            var graphChrom = GetGraphChrom(Document.MeasuredResults.Chromatograms[SelectedResultsIndex].Name);
            if (graphChrom == null)
            {
                return null;
            }
            return graphChrom.GetChromFileInfoId();
        }



        void GraphSpectrum.IStateProvider.BuildSpectrumMenu(bool isProteomic, ZedGraphControl zedGraphControl, ContextMenuStrip menuStrip)
        {
            using var spectrumContextMenu = new SpectrumContextMenu(this);
            spectrumContextMenu.BuildSpectrumMenu(isProteomic, zedGraphControl, menuStrip);
        }


        public void ShowSpectrumProperties()
        {
            using (var dlg = new SpectrumChartPropertyDlg())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    UpdateSpectrumGraph(false);
            }
        }


        public void ShowGraphSpectrum(bool show)
        {
            if (show)
            {
                if (_graphSpectrum != null)
                {
                    _graphSpectrum.Show(dockPanel, DockState.DockRight);
                    _graphSpectrum.Focus();
                }
                else
                {
                    _graphSpectrum = CreateGraphSpectrum();
                    _graphSpectrum.Show(dockPanel, DockState.DockRight);
                }
            }
            else if (_graphSpectrum != null)
            {
                // Save current setting for showing spectra
                show = Settings.Default.ShowSpectra;
                // Close the spectrum graph window
                _graphSpectrum.Hide();
                // Restore setting and menuitem from saved value
                Settings.Default.ShowSpectra = show;
            }
        }

        // Testing
        public bool IsGraphSpectrumVisible
        {
            get { return _graphSpectrum != null && _graphSpectrum.Visible; }
        }

        public void SetShowMassError(bool show)
        {
            Settings.Default.ShowFullScanMassError = show;
            UpdateSpectrumGraph(false);

        }

        private GraphSpectrum CreateGraphSpectrum()
        {
            // Create a new spectrum graph
            _graphSpectrum = new GraphSpectrum(this);
            _graphSpectrum.UpdateUI();
            _graphSpectrum.FormClosed += graphSpectrum_FormClosed;
            _graphSpectrum.VisibleChanged += graphSpectrum_VisibleChanged;
            _graphSpectrum.SelectedSpectrumChanged += graphSpectrum_SelectedSpectrumChanged;
            _graphSpectrum.ZoomEvent += mzGraph_ZoomAllMz;
            return _graphSpectrum;
        }

        private void DestroyGraphSpectrum()
        {
            if (_graphSpectrum != null)
            {
                _graphSpectrum.FormClosed -= graphSpectrum_FormClosed;
                _graphSpectrum.VisibleChanged -= graphSpectrum_VisibleChanged;
                _graphSpectrum.SelectedSpectrumChanged -= graphSpectrum_SelectedSpectrumChanged;
                _graphSpectrum.ZoomEvent -= mzGraph_ZoomAllMz;
                _graphSpectrum.HideOnClose = false;
                _graphSpectrum.Close();
                _graphSpectrum = null;
            }
        }

        private void graphSpectrum_VisibleChanged(object sender, EventArgs e)
        {
            if (_graphSpectrum != null)
                Settings.Default.ShowSpectra = _graphSpectrum.Visible;
        }

        private void graphSpectrum_FormClosed(object sender, FormClosedEventArgs e)
        {
            // Update settings and menu check
            Settings.Default.ShowSpectra = false;
            _graphSpectrum = null;
        }

        private void graphSpectrum_SelectedSpectrumChanged(object sender, SelectedSpectrumEventArgs e)
        {
            // Might need to update the selected MS/MS spectrum, if full-scan
            // filtering was used.
            if (DocumentUI.Settings.HasResults &&
                (DocumentUI.Settings.TransitionSettings.FullScan.IsEnabled || DocumentUI.Settings.PeptideSettings.Libraries.HasMidasLibrary))
            {
                if (e.Spectrum != null && e.IsUserAction)
                {
                    // Activate the selected replicate, if there is one associated with
                    // the selected spectrum.
                    string replicateName = e.Spectrum.ReplicateName;
                    if (!string.IsNullOrEmpty(replicateName))
                    {
                        int resultsIndex = DocumentUI.Settings.MeasuredResults.Chromatograms.IndexOf(chrom =>
                            Equals(replicateName, chrom.Name));
                        if (resultsIndex != -1)
                            SelectedResultsIndex = resultsIndex;
                    }
                }

                UpdateChromGraphs();
            }
        }

        public void UpdateSpectrumGraph(bool selectionChanged)
        {
            if (_graphSpectrum != null)
                _graphSpectrum.UpdateUI(selectionChanged);
            _graphFullScan?.UpdateUI();
        }

//        private static bool SameChargeGroups(PeptideTreeNode nodeTree)
//        {
//            // Check to see if all transition groups under a peptide tree node
//            // have the same precursor charge.
//            int charge = 0;
//            foreach (TransitionGroupDocNode nodeGroup in nodeTree.DocNode.Children)
//            {
//                if (charge == 0)
//                    charge = nodeGroup.TransitionGroup.PrecursorCharge;
//                else if (charge != nodeGroup.TransitionGroup.PrecursorCharge)
//                    return false;
//            }
//            // True only if there was at least one group
//            return (charge != 0);
//        }

        IList<IonType> GraphSpectrum.IStateProvider.ShowIonTypes(bool isProteomic)
        {
            return _graphSpectrumSettings.ShowIonTypes(isProteomic); 
        }

        IList<string> GraphSpectrum.IStateProvider.ShowLosses()
        {
            return _graphSpectrumSettings.ShowLosses.ToList();
        }


        private void CheckIonTypes(IEnumerable<IonType> types, bool check)
        {
            foreach (var type in types)
                CheckIonType(type, check);
        }

        private void CheckIonType(IonType type, bool check)
        {
            ViewMenu.CheckIonType(type, check);
        }

        // N.B. we're interested in the absolute value of charge here, so output list may be shorter than input list
        // CONSIDER(bspratt): we may want finer adduct-level control for small molecule use
        IList<int> GraphSpectrum.IStateProvider.ShowIonCharges(IEnumerable<Adduct> adductPriority)
        {
            return _graphSpectrumSettings.ShowIonCharges(adductPriority);
        }

        private void CheckIonCharges(IEnumerable<Adduct> charges, bool check)
        {
            foreach (var charge in charges)
                CheckIonCharge(charge, check);
        }

        private void CheckIonCharge(Adduct adduct, bool check)
        {
            ViewMenu.CheckIonCharge(adduct, check);
        }

        public void HideFullScanGraph()
        {
            if (_graphFullScan != null)
                _graphFullScan.Hide();
        }

        internal void ShowGraphFullScan(IScanProvider scanProvider, int transitionIndex, int scanIndex, int? optStep)
        {
            if (_graphFullScan != null)
            {
                _graphFullScan.Activate();
                _graphFullScan.Focus();
            }
            else
            {
                _graphFullScan = CreateGraphFullScan();

                // Choose a position to float the window
                var rectFloat = GetFloatingRectangleForNewWindow();
                _graphFullScan.Show(dockPanel, rectFloat);
            }

            _graphFullScan.ShowSpectrum(scanProvider, transitionIndex, scanIndex, optStep);
        }

        // Testing
        public GraphFullScan GraphFullScan
        {
            get { return _graphFullScan; }
        }

        private GraphFullScan CreateGraphFullScan()
        {
            // Create a new spectrum graph
            _graphFullScan = new GraphFullScan(this);
            _graphFullScan.UpdateUI();
            _graphFullScan.FormClosed += graphFullScan_FormClosed;
            _graphFullScan.VisibleChanged += graphFullScan_VisibleChanged;
            _graphFullScan.SelectedScanChanged += graphFullScan_SelectedScanChanged;
            _graphFullScan.ZoomEvent += mzGraph_ZoomAllMz;
            return _graphFullScan;
        }

        private void DestroyGraphFullScan()
        {
            if (_graphFullScan != null)
            {
                _graphFullScan.FormClosed -= graphFullScan_FormClosed;
                _graphFullScan.VisibleChanged -= graphFullScan_VisibleChanged;
                _graphFullScan.SelectedScanChanged -= graphFullScan_SelectedScanChanged;
                _graphFullScan.ZoomEvent -= mzGraph_ZoomAllMz;
                _graphFullScan.HideOnClose = false;
                _graphFullScan.Close();
                _graphFullScan = null;
            }
        }

        private void graphFullScan_VisibleChanged(object sender, EventArgs e)
        {
            if (_graphFullScan != null)
                Settings.Default.ShowFullScan = _graphFullScan.Visible;
        }

        private void graphFullScan_FormClosed(object sender, FormClosedEventArgs e)
        {
            // Update settings and menu check
            Settings.Default.ShowFullScan = false;
            _graphFullScan = null;
        }

        private void graphFullScan_SelectedScanChanged(object sender, SelectedScanEventArgs e)
        {
            SelectedScanFile = e.DataFile;
            SelectedScanRetentionTime = e.RetentionTime;
            SelectedScanTransition = e.TransitionId;
            SelectedScanOptStep = e.OptStep;
            UpdateChromGraphs();
        }


        private void mzGraph_ZoomAllMz(object sender, ZoomEventArgs newState)
        {
            foreach (var target in ListMzScaleCopyables())
            {
                if (!ReferenceEquals(target, sender))
                    target.ApplyMZZoomState(newState.ZoomState);
            }
        }

        private IEnumerable<IMzScalePlot> ListMzScaleCopyables()
        {
            if (_graphFullScan != null && _graphFullScan.Visible)
            {
                yield return _graphFullScan;
            }

            if (_graphSpectrum != null && _graphSpectrum.Visible)
            {
                yield return _graphSpectrum;
            }
        }

        MzRange ISpectrumScaleProvider.GetMzRange(SpectrumControlType controlType)
        {
            switch (controlType)
            {
                case SpectrumControlType.FullScanViewer:
                    return _graphFullScan?.Range;
                case SpectrumControlType.LibraryMatch:
                    return _graphSpectrum?.Range;
                default:
                    return null;
            }
        }
        #endregion

        #region Chromatogram graphs

        public PeptideGraphInfo GetPeptideGraphInfo(DocNode docNode)
        {
            return SequenceTree.GetPeptideGraphInfo(docNode);
        }

        void GraphChromatogram.IStateProvider.BuildChromatogramMenu(ZedGraphControl zedGraphControl, PaneKey paneKey, ContextMenuStrip menuStrip, ChromFileInfoId chromFileInfoId)
        {
            PrepareZedGraphContextMenu(zedGraphControl, menuStrip);
            using var chromatogramContextMenu = new ChromatogramContextMenu(this);
            chromatogramContextMenu.BuildChromatogramMenu(paneKey, menuStrip, chromFileInfoId);
        }

        public void ShowChromatogramLegends(bool show)
        {
            Settings.Default.ShowChromatogramLegend = show;
            UpdateChromGraphs();
        }

        public void ShowExemplaryPeak(bool show)
        {
            Settings.Default.ShowExemplaryPeakBounds = show;
            UpdateChromGraphs();
        }


        public void ToggleRawTimesMenuItem()
        {
            Settings.Default.ChromShowRawTimes = !Settings.Default.ChromShowRawTimes;
            UpdateChromGraphs();
        }


        public void ShowPeptideIDTimes(bool show)
        {
            Settings.Default.ShowPeptideIdTimes = show;
            UpdateChromGraphs();
        }

        public void ShowAlignedPeptideIDTimes(bool show)
        {
            Settings.Default.ShowAlignedPeptideIdTimes = show;
            UpdateChromGraphs();
        }

        public void ShowOtherRunPeptideIDTimes(bool show)
        {
            Settings.Default.ShowUnalignedPeptideIdTimes = show;
            UpdateChromGraphs();
        }


        public bool IsMultipleIonSources
        {
            get
            {
                var nodeTreePep = SequenceTree.GetNodeOfType<PeptideTreeNode>();
                if (nodeTreePep == null)
                    return false;
                var fullScan = DocumentUI.Settings.TransitionSettings.FullScan;
                return nodeTreePep.DocNode.TransitionGroups.Contains(
                    nodeGroup => GraphChromatogram.IsMultipleIonSources(fullScan, nodeGroup));
            }
        }

        public void ApplyPeak(bool subsequent, bool group)
        {
            EditMenu.ApplyPeak(subsequent, group);
        }

        public void RemovePeak()
        {
            EditMenu.RemovePeak(false);
        }

        public void RemovePeak(IdentityPath groupPath, TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran)
        {
            EditMenu.RemovePeak(groupPath, nodeGroup, nodeTran);
        }

        public void ShowSingleTransition()
        {
            SetDisplayTypeChrom(DisplayTypeChrom.single);
        }

        public void ShowPrecursorTransitions()
        {
            SetDisplayTypeChrom(DisplayTypeChrom.precursors);
        }

        public void ShowProductTransitions()
        {
            SetDisplayTypeChrom(DisplayTypeChrom.products);
        }

        public void ShowAllTransitions()
        {
            SetDisplayTypeChrom(DisplayTypeChrom.all);
        }

        public void ShowTotalTransitions()
        {
            SetDisplayTypeChrom(DisplayTypeChrom.total);
        }

        public void ShowBasePeak()
        {
            SetDisplayTypeChrom(DisplayTypeChrom.base_peak);
        }

        public void ShowTic()
        {
            SetDisplayTypeChrom(DisplayTypeChrom.tic);
        }

        public void ShowQc(string qcTraceName)
        {
            Settings.Default.ShowQcTraceName = qcTraceName;
            SetDisplayTypeChrom(DisplayTypeChrom.qc);
        }

        public void SetDisplayTypeChrom(DisplayTypeChrom displayType)
        {
            Settings.Default.ShowTransitionGraphs = displayType.ToString();
            UpdateChromGraphs();
            UpdateSpectrumGraph(false);
            UpdateRetentionTimeGraph();
            UpdatePeakAreaGraph();
            UpdateMassErrorGraph();
        }

        public void SetTransformChrom(TransformChrom transformChrom)
        {
            Settings.Default.TransformTypeChromatogram = transformChrom.ToString();
            UpdateChromGraphs();
        }

        public void LockYChrom (bool locked)
        {
            bool lockY = Settings.Default.LockYChrom = locked;
            // Avoid updating the rest of the chart just to change the y-axis lock state
            foreach (var chromatogram in _listGraphChrom)
                chromatogram.LockYAxis(lockY);
        }

        public void SynchronizeZooming(bool isChecked)
        {
            bool zoomAll = Settings.Default.AutoZoomAllChromatograms = isChecked;

            if (zoomAll)
            {
                (dockPanel.ActiveContent as GraphChromatogram)?.OnZoom();
            }
        }

        public AutoZoomChrom EffectiveAutoZoom
        {
            get
            {
                var zoom = GraphChromatogram.AutoZoom;
                bool hasRt = (Document.Settings.PeptideSettings.Prediction.RetentionTime != null);
                if (!hasRt)
                {
                    if (zoom == AutoZoomChrom.window)
                    {
                        return AutoZoomChrom.none;
                    }

                    if (zoom == AutoZoomChrom.both)
                    {
                        return AutoZoomChrom.peak;
                    }
                }

                return zoom;
            }
        }

        public void AutoZoomNone()
        {
            SetAutoZoomChrom(AutoZoomChrom.none);
        }

        public void AutoZoomRTWindow()
        {
            SetAutoZoomChrom(AutoZoomChrom.window);
        }

        public void SetAutoZoomChrom(AutoZoomChrom autoZoomChrom)
        {
            Settings.Default.AutoZoomChromatogram = autoZoomChrom.ToString();
            UpdateChromGraphs();
        }

        public void AutoZoomBestPeak()
        {
            SetAutoZoomChrom(AutoZoomChrom.peak);
        }

        public void AutoZoomBoth()
        {
            SetAutoZoomChrom(AutoZoomChrom.both);
        }

        public void ShowChromatogramProperties()
        {
            using (var dlg = new ChromChartPropertyDlg())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    UpdateChromGraphs();
            }
        }

        public void ShowChromatogramRTThresholdDlg()
        {
            using (var dlg = new ChromatogramRTThresholdDlg())
            {
                double threshold = Settings.Default.ShowRetentionTimesThreshold;
                if (threshold > 0)
                    dlg.Threshold = threshold;

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.ShowRetentionTimesThreshold = dlg.Threshold;
                    Settings.Default.ShowRetentionTimesEnum = ShowRTChrom.threshold.ToString();
                    UpdateChromGraphs();
                }
            }
        }

        internal void ShowGraphChrom(string name, bool show)
        {
            var graphChrom = GetGraphChrom(name);
            if (graphChrom != null)
            {
                if (show)
                {
                    graphChrom.Activate();
                    graphChrom.Focus();
                }
                else
                    if(graphChrom.DockState != DockState.Hidden)
                        graphChrom.Hide();
            }
            else if (show)
            {
                if (_listGraphChrom.Count >= MAX_GRAPH_CHROM)
                {
                    // List is too long, re-purpose least recently used
                    graphChrom = _listGraphChrom[0];
                    graphChrom.ChangeChromatogram(name);
                    graphChrom.Activate();
                    graphChrom.Visible = true;
                    graphChrom.Focus();
                }
                else
                {
                    graphChrom = CreateGraphChrom(name, SelectedGraphChromName, false);
                }
            }

            if (show)
            {
                // Move this to end of MRU so it's seen as most recent
                _listGraphChrom.Remove(graphChrom);
                _listGraphChrom.Add(graphChrom);
            }
        }

        public IEnumerable<GraphChromatogram> GraphChromatograms { get { return _listGraphChrom; } }

        public GraphChromatogram GetGraphChrom(string name)
        {
            int iGraph = _listGraphChrom.IndexOf(graph => Equals(graph.NameSet, name));
            return (iGraph != -1 ? _listGraphChrom[iGraph] : null);
        }

//        private bool IsGraphChromVisible(string name)
//        {
//            int iGraph = _listGraphChrom.IndexOf(graph => Equals(graph.NameSet, name));
//            return iGraph != -1 && !_listGraphChrom[iGraph].IsHidden;
//        }

        public string SelectedGraphChromName
        {
            get
            {
                return GetGraphChromStrings(SelectedResultsIndex, null, out _);
            }
        }

        public string GetGraphChromStrings(int iResult, ChromFileInfoId fileId, out MsDataFileUri filePath)
        {
            filePath = null;
            if (iResult != -1)
            {
                var settings = DocumentUI.Settings;
                if (settings.HasResults && iResult < settings.MeasuredResults.Chromatograms.Count)
                {
                    var chromatogramSet = settings.MeasuredResults.Chromatograms[iResult];
                    if (fileId != null)
                        filePath = chromatogramSet.GetFileInfo(fileId).FilePath;
                    return chromatogramSet.Name;                    
                }
            }
            return null;
        }

        private GraphChromatogram CreateGraphChrom(string name)
        {
            var graphChrom = new GraphChromatogram(this, this, name);
            graphChrom.FormClosed += graphChromatogram_FormClosed;
            graphChrom.PickedPeak += graphChromatogram_PickedPeak;
            graphChrom.ClickedChromatogram += graphChromatogram_ClickedChromatogram;
            graphChrom.ChangedPeakBounds += graphChromatogram_ChangedPeakBounds;
            graphChrom.PickedSpectrum += graphChromatogram_PickedSpectrum;
            _listGraphChrom.Add(graphChrom);
            return graphChrom;
        }

        private void DestroyGraphChrom(GraphChromatogram graphChrom)
        {
            _listGraphChrom.Remove(graphChrom);

            // Detach event handlers and dispose
            graphChrom.FormClosed -= graphChromatogram_FormClosed;
            graphChrom.PickedPeak -= graphChromatogram_PickedPeak;
            graphChrom.ClickedChromatogram -= graphChromatogram_ClickedChromatogram;
            graphChrom.ChangedPeakBounds -= graphChromatogram_ChangedPeakBounds;
            graphChrom.PickedSpectrum -= graphChromatogram_PickedSpectrum;
            graphChrom.HideOnClose = false;
            graphChrom.Close();
        }

        private GraphChromatogram CreateGraphChrom(string name, string namePosition, bool split)
        {
            // Create a new spectrum graph
            var graphChrom = CreateGraphChrom(name);
            int firstDocumentPane = FirstDocumentPane;
            if (firstDocumentPane == -1)
                graphChrom.Show(dockPanel, DockState.Document);
            else
            {
                var graphPosition = GetGraphChrom(namePosition);

                DockPane paneExisting = FindChromatogramPane(graphPosition);
                if (paneExisting == null)
                    graphChrom.Show(dockPanel.Panes[firstDocumentPane], DockPaneAlignment.Left, 0.5);
                else if (!split)
                {
                    graphChrom.Show(paneExisting, null);  // Add to the end
                }
                else
                {
                    var alignment = (graphChrom.Width > graphChrom.Height ?
                        DockPaneAlignment.Right : DockPaneAlignment.Bottom);
                    graphChrom.Show(paneExisting, alignment, 0.5);
                }
            }
            return graphChrom;
        }

        private int FirstDocumentPane
        {
            get
            {
                return dockPanel.Panes.IndexOf(pane => !pane.IsHidden && pane.DockState == DockState.Document);
            }
        }

        private DockPane FindChromatogramPane(GraphChromatogram graphChrom)
        {
            foreach (var pane in dockPanel.Panes)
            {
                foreach (IDockableForm form in pane.Contents)
                {
                    if (form is GraphChromatogram &&
                        (graphChrom == null || graphChrom == form))
                    {
                        return pane;
                    }
                }
            }
            return null;
        }

        private DockPane FindPane(IDockableForm dockableForm)
        {
            // Floating panes may be created but hidden for windows that allow floating
            // Have to check "DisplayingContents.Count > 0" instead of "IsHidden" here, since "IsHidden" does not get updated when
            // SuspendLayout is on.
            int iPane = dockPanel.Panes.IndexOf(pane => pane.Contents.Contains(dockableForm) && pane.DisplayingContents.Count > 0);
            return (iPane != -1 ? dockPanel.Panes[iPane] : null);
        }

        private void graphChromatogram_FormClosed(object sender, FormClosedEventArgs e)
        {
            _listGraphChrom.Remove((GraphChromatogram)sender);
        }

        private void graphChromatogram_PickedPeak(object sender, PickedPeakEventArgs e)
        {
            if (!EnsureLibrariesLoadedForPeakIntegration())
                return;
            var graphChrom = sender as GraphChromatogram;
            if (graphChrom != null)
                graphChrom.LockZoom();
            try
            {
                ModifyDocument(string.Format(SkylineResources.SkylineWindow_graphChromatogram_PickedPeak_Pick_peak__0_F01_, e.RetentionTime),
                    doc => PickPeak(doc, e), docPair =>
                    {
                        var name = GetPropertyName(docPair.OldDoc, e.GroupPath, e.TransitionId);

                        return AuditLogEntry.CreateSimpleEntry(MessageType.picked_peak, docPair.OldDocumentType, name, e.NameSet,
                            e.RetentionTime.MeasuredTime.ToString(@"#.0", CultureInfo.CurrentCulture));
                    });
            }
            finally
            {
                if (graphChrom != null)
                    graphChrom.UnlockZoom();
            }
        }

        private static PropertyName GetPropertyName(SrmDocument doc, IdentityPath groupPath, Identity transitionId)
        {
            var node = doc.FindNode(groupPath);

            if (transitionId != null)
                node = ((TransitionGroupDocNode)node).FindNode(transitionId);

            return AuditLogEntry.GetNodeName(doc, node);
        }

        private void graphChromatogram_ClickedChromatogram(object sender, ClickedChromatogramEventArgs e)
        {
            if (e.ScanProvider != null)
            {
                var dataFile = e.ScanProvider.DataFilePath;
                if (e.ScanIndex == -1)
                {
                    MessageDlg.Show(this, 
                        string.Format(SkylineResources.SkylineWindow_graphChromatogram_ClickedChromatogram_The_raw_file_must_be_re_imported_in_order_to_show_full_scans___0_, dataFile));
                    return;
                }
            }

            ShowGraphFullScan(e.ScanProvider, e.TransitionIndex, e.ScanIndex, e.OptStep);
        }

        /// <summary>
        /// Modifies a document in response to the user clicking on a peak in the GraphChromatogram.
        /// </summary>
        private SrmDocument PickPeak(SrmDocument document, PickedPeakEventArgs e)
        {
            document = document.ChangePeak(e.GroupPath, e.NameSet, e.FilePath, e.TransitionId, e.RetentionTime.MeasuredTime, UserSet.TRUE);

            var activeTransitionGroup = (TransitionGroupDocNode) document.FindNode(e.GroupPath);
            var activeChromInfo = FindChromInfo(document, activeTransitionGroup, e.NameSet, e.FilePath);

            document = ChangePeakBounds(document, GetSynchronizedPeakBoundChanges(document,
                new ChangedPeakBoundsEventArgs(e.GroupPath, null, e.NameSet, e.FilePath,
                    new ScaledRetentionTime(activeChromInfo.StartRetentionTime.GetValueOrDefault()),
                    new ScaledRetentionTime(activeChromInfo.EndRetentionTime.GetValueOrDefault()), null,
                    PeakBoundsChangeType.both), false));

            if (activeTransitionGroup.RelativeRT != RelativeRT.Matching)
            {
                return document;
            }
            var peptide = (PeptideDocNode) document.FindNode(e.GroupPath.Parent);
            // See if there are any other transition groups that should have their peak bounds set to the same value
            foreach (var transitionGroup in peptide.TransitionGroups)
            {
                if (transitionGroup.RelativeRT != RelativeRT.Matching)
                {
                    continue;
                }
                var groupPath = new IdentityPath(e.GroupPath.Parent, transitionGroup.TransitionGroup);
                if (Equals(groupPath, e.GroupPath))
                {
                    continue;
                }
                var chromInfo = FindChromInfo(document, transitionGroup, e.NameSet, e.FilePath);
                if (null == chromInfo)
                {
                    continue;
                }
                document = document.ChangePeak(groupPath, e.NameSet, e.FilePath, null, 
                    activeChromInfo.StartRetentionTime, activeChromInfo.EndRetentionTime, UserSet.TRUE, activeChromInfo.Identified, true);
            }
            return document;
        }

        /// <summary>
        /// Finds the TransitionGroupChromInfo that matches the specified ChromatogramSet name and file path.
        /// </summary>
        public static TransitionGroupChromInfo FindChromInfo(SrmDocument document,
            TransitionGroupDocNode transitionGroupDocNode, string nameChromatogramSet, MsDataFileUri filePath)
        {
            ChromatogramSet chromatogramSet;
            int indexSet;
            if (!document.Settings.MeasuredResults.TryGetChromatogramSet(nameChromatogramSet, out chromatogramSet, out indexSet))
            {
                return null;
            }
            var chromFileInfoId = chromatogramSet.FindFile(filePath);
            if (null == chromFileInfoId)
            {
                return null;
            }
            var results = transitionGroupDocNode.Results[indexSet];
            if (results.IsEmpty)
            {
                return null;
            }
            return results.FirstOrDefault(chromInfo => ReferenceEquals(chromFileInfoId, chromInfo.FileId));
        }

        private void graphChromatogram_ChangedPeakBounds(object sender, ChangedMultiPeakBoundsEventArgs eMulti)
        {
            if (!EnsureLibrariesLoadedForPeakIntegration())
                return;

            var graphChrom = sender as GraphChromatogram;
            if (graphChrom != null)
                graphChrom.LockZoom();
            try
            {
                string message;
                // Handle most common case of a change to a single group first.
                if (eMulti.Changes.Length == 1)
                {
                    ChangedPeakBoundsEventArgs e = eMulti.Changes[0];
                    if (Equals(e.StartTime, e.EndTime))
                        message = SkylineResources.SkylineWindow_graphChromatogram_ChangedPeakBounds_Remove_peak;
                    else if (e.ChangeType == PeakBoundsChangeType.both)
                        message = string.Format(SkylineResources.SkylineWindow_graphChromatogram_ChangedPeakBounds_Change_peak_to__0_F01___1_F01_, e.StartTime, e.EndTime); 
                    else if (e.ChangeType == PeakBoundsChangeType.start)
                        message = string.Format(SkylineResources.SkylineWindow_graphChromatogram_ChangedPeakBounds_Change_peak_start_to__0_F01_, e.StartTime); 
                    else
                        message = string.Format(SkylineResources.SkylineWindow_graphChromatogram_ChangedPeakBounds_Change_peak_end_to__0_F01_, e.EndTime); 
                }
                else
                {
                    message = SkylineResources.SkylineWindow_graphChromatogram_ChangedPeakBounds_Change_peaks;
                }
                ModifyDocument(message,
                    doc => ChangePeakBounds(doc, eMulti.Changes.SelectMany(change => GetSynchronizedPeakBoundChanges(doc, change, true))),
                    docPair =>
                    {
                        var names = eMulti.Changes.Select(change =>
                            GetPropertyName(docPair.OldDoc, change.GroupPath, change.Transition)).ToArray();

                        var messages = eMulti.Changes
                            .SelectMany((change, index) => GetMessagesForPeakBoundsChange(names[index], change))
                            .ToList();
                        if (messages.Count == 1)
                        {
                            return AuditLogEntry.CreateSingleMessageEntry(messages[0]);
                        }                 
                        else if (messages.Count > 1)
                        {
                            var firstName = names.First();
                            if (names.All(name => Equals(name, firstName)))
                            {
                                return AuditLogEntry
                                    .CreateSimpleEntry(MessageType.changed_peak_bounds_of, docPair.OldDocumentType, firstName)
                                    .ChangeAllInfo(messages);
                            }
                            else // TODO: is this even possible?+
                            {
                                return AuditLogEntry
                                    .CreateSimpleEntry(MessageType.changed_peak_bounds, docPair.OldDocumentType)
                                    .ChangeAllInfo(messages);
                            }
                        }

                        return null;

                    });
            }
            finally
            {
                if (graphChrom != null)
                    graphChrom.UnlockZoom();
            }
        }

        private List<MessageInfo> GetMessagesForPeakBoundsChange(PropertyName name, ChangedPeakBoundsEventArgs args)
        {
            var singleTransitionDisplay = args.Transition != null;
            var result = new List<MessageInfo>();

            var transitionGroupDocNode = (TransitionGroupDocNode) Document.FindNode(args.GroupPath);
            var transitionDocNode = singleTransitionDisplay
                ? transitionGroupDocNode.Transitions.FirstOrDefault(tr => ReferenceEquals(tr.Id, args.Transition))
                : null;

            int indexSet;
            if (!Document.Settings.HasResults ||
                !Document.Settings.MeasuredResults.TryGetChromatogramSet(args.NameSet, out _, out indexSet))
                return result;

            float? startTime = null;
            float? endTime = null;

            if (singleTransitionDisplay)
            {
                if (transitionDocNode != null)
                {
                    var chromInfo = transitionDocNode.Results[indexSet].FirstOrDefault(ci => ci.OptimizationStep == 0);
                    if (chromInfo != null)
                    {
                        startTime = chromInfo.StartRetentionTime;
                        endTime = chromInfo.EndRetentionTime;
                    }
                }
            }
            else
            {
                var chromInfo = transitionGroupDocNode.Results[indexSet].FirstOrDefault(ci => ci.OptimizationStep == 0);
                if (chromInfo != null)
                {
                    startTime = chromInfo.StartRetentionTime;
                    endTime = chromInfo.EndRetentionTime;
                }
            }

            if (args.ChangeType == PeakBoundsChangeType.start || args.ChangeType == PeakBoundsChangeType.both)
            {
                result.Add(new MessageInfo(
                    singleTransitionDisplay ? MessageType.changed_peak_start : MessageType.changed_peak_start_all,
                    Document.DocumentType,
                    name, args.NameSet, LogMessage.RoundDecimal(startTime, 2),
                    LogMessage.RoundDecimal(args.StartTime.MeasuredTime, 2)));
            }

            if (args.ChangeType == PeakBoundsChangeType.end || args.ChangeType == PeakBoundsChangeType.both)
            {
                result.Add(new MessageInfo(
                    singleTransitionDisplay ? MessageType.changed_peak_end : MessageType.changed_peak_end_all, Document.DocumentType, name,
                    args.NameSet, LogMessage.RoundDecimal(endTime, 2),
                    LogMessage.RoundDecimal(args.EndTime.MeasuredTime, 2)));
            }

            return result;
        }

        /// <summary>
        /// Checks that document libraries are loaded, which is required for peak integration
        /// changes that need to look up peptide ID times. Shows a message to the user if not.
        /// </summary>
        /// <returns>True if libraries are loaded and peak integration can proceed</returns>
        public bool EnsureLibrariesLoadedForPeakIntegration()
        {
            if (!DocumentUI.Settings.PeptideSettings.Libraries.IsLoaded)
            {
                MessageDlg.Show(this,
                    SkylineResources.SkylineWindow_graphChromatogram_PickedPeak_Libraries_must_be_loaded);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Modifies a document in response to a user's mouse dragging on a GraphChromatogram.
        /// </summary>
        public SrmDocument ChangePeakBounds(SrmDocument document, IEnumerable<ChangedPeakBoundsEventArgs> changes)
        {
            SrmDocument beforeDefer = null;
            var changesArr = changes.ToArray();
            if (changesArr.Length > 1 && !document.DeferSettingsChanges)
            {
                beforeDefer = document;
                document = document.BeginDeferSettingsChanges();
            }

            var changedGroupIds = new HashSet<Tuple<IdentityPath, MsDataFileUri>>();
            var peptideChanges = new Dictionary<IdentityPath, Dictionary<MsDataFileUri, ChangedPeakBoundsEventArgs>>();
            foreach (var change in changesArr)
            {
                var find = new SrmDocument.FindChromInfos(document, change.GroupPath, change.NameSet, change.FilePath);
                if (find.IndexInfo == -1)
                    continue;

                document = document.ChangePeak(change.GroupPath, change.NameSet, change.FilePath, change.Transition,
                    change.StartTime.MeasuredTime, change.EndTime.MeasuredTime, UserSet.TRUE, change.Identified, false);

                changedGroupIds.Add(Tuple.Create(change.GroupPath, change.FilePath));

                var peptidePath = change.GroupPath.Parent;
                if (!peptideChanges.TryGetValue(peptidePath, out var changesByFile))
                {
                    changesByFile = new Dictionary<MsDataFileUri, ChangedPeakBoundsEventArgs>();
                    peptideChanges[peptidePath] = changesByFile;
                }
                if (!changesByFile.ContainsKey(change.FilePath))
                {
                    var transitionGroup = (TransitionGroupDocNode) document.FindNode(change.GroupPath);
                    if (transitionGroup.RelativeRT == RelativeRT.Matching)
                    {
                        changesByFile.Add(change.FilePath, change);
                    }
                }
            }

            // See if there are any other TransitionGroups that also have RelativeRT matching,
            // and set their peak boundaries to the same.
            foreach (var entry in peptideChanges)
            {
                var peptide = (PeptideDocNode)document.FindNode(entry.Key);
                foreach (var change in entry.Value.Select(v => v.Value))
                {
                    foreach (var transitionGroup in peptide.TransitionGroups)
                    {
                        if (transitionGroup.RelativeRT != RelativeRT.Matching)
                        {
                            continue;
                        }
                        var groupId = new IdentityPath(entry.Key, transitionGroup.TransitionGroup);
                        if (changedGroupIds.Contains(Tuple.Create(groupId, change.FilePath)))
                        {
                            continue;
                        }
                        if (null == FindChromInfo(document, transitionGroup, change.NameSet, change.FilePath))
                        {
                            continue;
                        }
                        document = document.ChangePeak(groupId, change.NameSet, change.FilePath, null,
                            change.StartTime.MeasuredTime, change.EndTime.MeasuredTime, UserSet.TRUE, change.Identified, true);
                    }
                }
            }
            return beforeDefer == null ? document : document.EndDeferSettingsChanges(beforeDefer, null);
        }

        public IEnumerable<ChangedPeakBoundsEventArgs> GetSynchronizedPeakBoundChanges(SrmDocument document, ChangedPeakBoundsEventArgs change, bool includeSelf)
        {
            if (includeSelf)
                yield return change;

            ChromatogramSet thisChromSet = null;
            var syncTargets = new List<ChromatogramSet>();
            foreach (var syncTarget in document.GetSynchronizeIntegrationChromatogramSets())
            {
                syncTargets.Add(syncTarget);
                if (thisChromSet == null && Equals(change.NameSet, syncTarget.Name))
                    thisChromSet = syncTarget;
            }

            if (thisChromSet == null)
                yield break; // This chromatogram is not selected for synchronized integration

            var thisFile = thisChromSet.FindFile(change.FilePath);

            var transformOp = GetRetentionTimeTransformOperation();
            var thisStart = change.StartTime.MeasuredTime;
            var thisEnd = change.EndTime.MeasuredTime;
            if (transformOp != null)
            {
                transformOp.TryGetRegressionFunction(change.FilePath, out var regressionThis);
                if (regressionThis != null)
                {
                    thisStart = regressionThis.GetY(thisStart);
                    thisEnd = regressionThis.GetY(thisEnd);
                }
            }

            var groupId = change.GroupPath.Child;
            var nodePep = (PeptideDocNode)document.FindNode(change.GroupPath.Parent);
            if (nodePep == null)
                throw new IdentityNotFoundException(groupId);
            var nodeGroup = (TransitionGroupDocNode)nodePep.FindNode(groupId);
            if (nodeGroup == null)
                throw new IdentityNotFoundException(groupId);

            foreach (var chromSet in syncTargets)
            {
                if (!document.Settings.MeasuredResults.TryLoadChromatogram(chromSet, nodePep, nodeGroup,
                    (float)document.Settings.TransitionSettings.Instrument.MzMatchTolerance, out var chromInfos))
                    continue;

                foreach (var info in chromSet.MSDataFileInfos)
                {
                    if (ReferenceEquals(thisChromSet, chromSet) && ReferenceEquals(thisFile, info.FileId) ||
                        chromInfos.IndexOf(info2 => Equals(info.FilePath, info2.FilePath)) == -1)
                        continue;

                    var start = thisStart;
                    var end = thisEnd;

                    if (transformOp != null)
                    {
                        transformOp.TryGetRegressionFunction(info.FilePath, out var regression);
                        if (regression != null)
                        {
                            start = regression.GetX(thisStart);
                            end = regression.GetX(thisEnd);
                        }
                    }

                    yield return new ChangedPeakBoundsEventArgs(change.GroupPath, null, chromSet.Name, info.FilePath,
                        new ScaledRetentionTime(start), new ScaledRetentionTime(end), null, change.ChangeType);
                }
            }
        }

        private void graphChromatogram_PickedSpectrum(object sender, PickedSpectrumEventArgs e)
        {
            if (_graphSpectrum == null || !_graphSpectrum.Visible)
            {
                ShowGraphSpectrum(true);
            }
            if (_graphSpectrum != null)
                _graphSpectrum.SelectSpectrum(e.SpectrumId);
        }

        public void UpdateChromGraphs()
        {
            foreach (var graphChrom in _listGraphChrom)
                graphChrom.UpdateUI();

            // TODO(nicksh): we want to also update GraphSpectrum at this time, but there are issues with
            // this being called reentrantly.
//            if (null != GraphSpectrum)
//            {
//                GraphSpectrum.UpdateUI();
//            }
        }

        public void CloseAllChromatograms()
        {
            foreach (var graphChromatogram in _listGraphChrom.ToList())
            {
                graphChromatogram.Hide();
            }
        }

        public void CloseMostRecentChromatogram()
        {
            var graphChromatogram = _listGraphChrom.LastOrDefault(g => !g.IsHidden);
            if (graphChromatogram != null)
            {
                graphChromatogram.Hide();
                graphChromatogram = _listGraphChrom.LastOrDefault(g => !g.IsHidden);
                graphChromatogram?.Activate();
            }
        }

        #endregion

        public void ShowSplitChromatogramGraph(bool split)
        {
            Settings.Default.SplitChromatogramGraph = split;
            UpdateGraphPanes();
        }

        public void ShowOnlyQuantitative(bool showOnlyQuantitative)
        {
            Settings.Default.ShowQuantitativeOnly = showOnlyQuantitative;
            UpdateGraphPanes();
        }

        public bool ShowIonMobility
        {
            get { return Settings.Default.ShowIonMobility; }
            set
            {
                if (value == Settings.Default.ShowIonMobility)
                {
                    return;
                }
                Settings.Default.ShowIonMobility = value;
                UpdateGraphPanes();
            }
        }

        public bool ShowCollisionCrossSection
        {
            get { return Settings.Default.ShowCollisionCrossSection; }
            set
            {
                if (value == Settings.Default.ShowCollisionCrossSection)
                {
                    return;
                }
                Settings.Default.ShowCollisionCrossSection = value;
                UpdateGraphPanes();
            }
        }

        /// <summary>
        /// Returns a rectangle suitable for positioning a floating DockableForm.
        /// The size of the rectangle is based off of the size of the DockPanel, and the size of the screen.
        /// </summary>
        private Rectangle GetFloatingRectangleForNewWindow()
        {
            return FormGroup.GetFloatingRectangleForNewWindow(dockPanel);
        }

        private bool GraphVisible(IEnumerable<GraphSummary> graphs, GraphTypeSummary type)
        {
            return graphs.Any(g => g.Type == type && !g.IsHidden);
        }

        public bool GraphChecked(IEnumerable<GraphSummary> graphs, IList<GraphTypeSummary> types, GraphTypeSummary type)
        {
            return types.Contains(type) && GraphVisible(graphs, type);
        }

        private void ShowGraph(List<GraphSummary> graphs, bool show, GraphTypeSummary type,
            Func<GraphTypeSummary, GraphSummary> createGraph)
        {
            var graph = graphs.FirstOrDefault(g => g.Type == type);
            if (show)
            {
                if (graph != null && !Program.SkylineOffscreen)
                {
                    graphs.Remove(graph);
                    graphs.Insert(0, graph);
                    graph.Controller.GraphTypes.Insert(0, type);

                    if (graphs.Count > 1 && !graphs[1].IsHidden)
                        graph.Show(FindPane(graphs[1]), null);
                    else
                        graph.Activate();
                }
                else
                {
                    if (graph == null)
                        graph = createGraph(type);

                    if (graphs.Count > 1 && !graphs[1].IsHidden)
                    {
                        graph.Show(FindPane(graphs[1]), null);
                    }
                    else
                    {
                        // Choose a position to float the window
                        var rectFloat = GetFloatingRectangleForNewWindow();
                        graph.Show(dockPanel, rectFloat);
                    }
                }
            }
            else if (graph != null)
            {
                graph.Hide();
            }
        }

        #region Retention time graph

        public GraphSummary GraphRetentionTime { get { return _listGraphRetentionTime.FirstOrDefault(); } }

        public bool IsGraphRetentionTimeShown(GraphTypeSummary type)
        {
            return _listGraphRetentionTime.Any(g => g.Type == type && !g.IsHidden);
        }

        public void UpdateUIGraphRetentionTime(Func<GraphTypeSummary, bool> isEnabled)
        {
            var list = Settings.Default.RTGraphTypes.ToArray();
            ShowGraphRetentionTime(isEnabled);
            if (!list.All(isEnabled))
            {
                Settings.Default.RTGraphTypes.Clear();
                Settings.Default.RTGraphTypes.AddRange(list);
            }
        }

        public void ShowGraphRetentionTime(bool show)
        {
            ShowGraphRetentionTime(t => show && IsRetentionTimeGraphTypeEnabled(t));
        }

        public bool IsRetentionTimeGraphTypeEnabled(GraphTypeSummary type)
        {
            bool enabled = DocumentUI.Settings.HasResults;
            switch (type)
            {
                // Can only do run to run regression with at least 2 replicates
                case GraphTypeSummary.run_to_run_regression:
                    return enabled && DocumentUI.Settings.MeasuredResults.Chromatograms.Count > 1;
                // Scheduling can be enabled with a predictor even if there are no results
                case GraphTypeSummary.schedule:
                    return enabled || DocumentUI.Settings.PeptideSettings.Prediction.RetentionTime != null;
                default:
                    return enabled;
            }
        }

        private void ShowGraphRetentionTime(Func<GraphTypeSummary, bool> isEnabled)
        {
            Settings.Default.RTGraphTypes.ToList().ForEach(t =>
                ShowGraphRetentionTime(isEnabled(t), t));
        }

        public void ShowGraphRetentionTime(bool show, GraphTypeSummary type)
        {
            ShowGraph(_listGraphRetentionTime, show, type, CreateGraphRetentionTime);
        }

        private GraphSummary CreateGraphRetentionTime(GraphTypeSummary type)
        {
            if (type == GraphTypeSummary.invalid)
                return null;

            var targetIndex = SelectedResultsIndex;

            var origIndex = -1;
            if(ComboResults != null && ComboResults.Items.Count > 0)
                origIndex = (SelectedResultsIndex + 1) % ComboResults.Items.Count;
            var graph = new GraphSummary(type, this, new RTGraphController(), targetIndex, origIndex);
            graph.FormClosed += graphRetentionTime_FormClosed;
            graph.VisibleChanged += graphRetentionTime_VisibleChanged;
            graph.GraphControl.ZoomEvent += GraphControl_ZoomEvent;
            graph.Toolbar = new RunToRunRegressionToolbar(graph);
            _listGraphRetentionTime.Insert(0, graph);
            

            return graph;
        }

        private void DestroyGraphRetentionTime(GraphSummary graph)
        {
            graph.FormClosed -= graphRetentionTime_FormClosed;
            graph.VisibleChanged -= graphRetentionTime_VisibleChanged;
            graph.HideOnClose = false;
            graph.Close();
            _listGraphRetentionTime.Remove(graph);
            Settings.Default.RTGraphTypes.Remove(graph.Type);
        }

        private void graphRetentionTime_VisibleChanged(object sender, EventArgs e)
        {
            var graph = (GraphSummary) sender;
            if (graph.Visible)
            {
                Settings.Default.RTGraphTypes.Insert(0, graph.Type);
                _listGraphRetentionTime.Remove(graph);
                _listGraphRetentionTime.Insert(0, graph);
            }
            else if (graph.IsHidden)
            {
                Settings.Default.RTGraphTypes.Remove(graph.Type);
            }
        }

        private void graphRetentionTime_FormClosed(object sender, FormClosedEventArgs e)
        {
            GraphSummary graph = (GraphSummary)sender;
            _listGraphRetentionTime.Remove(graph);
            Settings.Default.RTGraphTypes.Remove(graph.Type);
        }

        void GraphSummary.IStateProvider.BuildGraphMenu(ZedGraphControl zedGraphControl, ContextMenuStrip menuStrip, Point mousePt,
            GraphSummary.IController controller)
        {
            PrepareZedGraphContextMenu(zedGraphControl, menuStrip);
            var graphController = controller as RTGraphController;
            if (graphController != null)
            {
                using var retentionTimesContextMenu = new RetentionTimesContextMenu(this);
                retentionTimesContextMenu.BuildRTGraphMenu(controller.GraphSummary, menuStrip, mousePt, graphController);
            }
            else if (controller is AreaGraphController)
            {
                using var peakAreasContextMenu = new PeakAreasContextMenu(this, controller.GraphSummary);
                peakAreasContextMenu.BuildAreaGraphMenu(menuStrip, mousePt);
            }
            else if (controller is MassErrorGraphController)
            {
                using var massErrorsContextMenu = new MassErrorsContextMenu(this);
                massErrorsContextMenu.BuildMassErrorGraphMenu(controller.GraphSummary, menuStrip);
            }
            else if (controller is DetectionsGraphController)
            {
                using var detectionsContextMenu = new DetectionsContextMenu(this);
                detectionsContextMenu.BuildDetectionsGraphMenu(controller.GraphSummary, menuStrip);
            }

        }

        /// <summary>
        /// Removes "Set Scale to Default" and "Show Point Values" from ZedGraph context menu.
        /// Adds separator before "Unzoom".
        /// Adds "Copy Metafile" and "Copy Data" menu items. 
        /// </summary>
        private void PrepareZedGraphContextMenu(ZedGraphControl zedGraphControl, ContextMenuStrip menuStrip)
        {
            for (int i = menuStrip.Items.Count - 1; i >= 0; i--)
            {
                string tag = (string)menuStrip.Items[i].Tag;
                if (tag == @"set_default" || tag == @"show_val")
                    menuStrip.Items.RemoveAt(i);
            }
            int iUnzoom = menuStrip.Items.Cast<ToolStripItem>().ToList().FindIndex(item => (string)item.Tag == @"unzoom");
            if (iUnzoom >= 0)
            {
                menuStrip.Items.Insert(iUnzoom, new ToolStripSeparator());
            }
            ZedGraphClipboard.AddToContextMenu(zedGraphControl, menuStrip);
        }

        public SrmDocument SelectionDocument
        {
            get { return SequenceTree != null ? SequenceTree.Document : null; }
        }

        public TreeNodeMS SelectedNode
        {
            get { return SequenceTree != null ? SequenceTree.SelectedNode as TreeNodeMS : null; }
        }

        public IdentityPath SelectedPath
        {
            get { return SequenceTree != null ? SequenceTree.SelectedPath : new IdentityPath(); }
            set { SequenceTree.SelectedPath = value; }
        }

        public IList<TreeNodeMS> SelectedNodes
        {
            get { return SequenceTree != null ? SequenceTree.SelectedNodes.ToArray() : new TreeNodeMS[0]; }
        }

        public int SelectedResultsIndex
        {
            get { return ComboResults != null ? ComboResults.SelectedIndex : -1; }
            set
            {
                if (ComboResults != null && 0 <= value && value < ComboResults.Items.Count)
                {
                    var focusStart = User32.GetFocusedControl();
                    ComboResults.SelectedIndex = value;
                    if (focusStart != null)
                    {
                        // Avoid just setting focus back to the chromatogram graph
                        // that just lost activation and reactivating it.
                        if (IsChromatogramGraph(focusStart))
                            dockPanel.ActivePane.Focus();
                        else
                            focusStart.Focus();
                    }
                }
            }
        }

        /// <summary>
        /// Returns true if a control is or belongs to a <see cref="GraphChromatogram"/>.
        /// </summary>
        private static bool IsChromatogramGraph(Control control)
        {
            while (control != null)
            {
                if (control is GraphChromatogram)
                    return true;
                control = control.Parent;
            }
            return false;
        }

        public MsDataFileUri SelectedScanFile { get; set; }
        public double SelectedScanRetentionTime { get; set; }
        public Identity SelectedScanTransition { get; set; }
        public int? SelectedScanOptStep { get; set; }

        public void ActivateReplicate(string name)
        {
            int index;

            if (DocumentUI.Settings.MeasuredResults.TryGetChromatogramSet(name, out _, out index))
            {
                SelectedResultsIndex = index;
            }
        }

        public void SelectPath(IdentityPath focusPath)
        {
            SequenceTree.SelectPath(focusPath);
            UpdateGraphPanes();
        }

        public SpectrumDisplayInfo SelectedSpectrum
        {
            get { return _graphSpectrum != null ? _graphSpectrum.SelectedSpectrum : null; }
        }

        public void ActivateSpectrum()
        {
            ShowGraphSpectrum(true);
        }

        // BuildRTGraphMenu has been moved to RetentionTimesContextMenu

        // AddScopeContextMenu, AddReplicatesContextMenu, AddPeptideOrderContextMenu moved to ContextMenuControl

        // timeGraphMenuItem_DropDownOpening, regressionMenuItem_Click, fullReplicateComparisonToolStripMenuItem_Click
        // moved to RetentionTimesContextMenu

        public void ShowRTRegressionGraphScoreToRun()
        {
            Settings.Default.RTGraphTypes.Insert(0, GraphTypeSummary.score_to_run_regression);
            ShowGraphRetentionTime(true, GraphTypeSummary.score_to_run_regression);
            UpdateRetentionTimeGraph();
        }

        public void ShowRTRegressionGraphRunToRun()
        {
            Settings.Default.RTGraphTypes.Insert(0, GraphTypeSummary.run_to_run_regression);
            ShowGraphRetentionTime(true, GraphTypeSummary.run_to_run_regression);
            UpdateRetentionTimeGraph();
        }

        // RT regression/plot/points event handlers moved to RetentionTimesContextMenu

        public void ShowPlotType(PlotTypeRT plotTypeRT)
        {
            RTGraphController.PlotType = plotTypeRT;
            UpdateRetentionTimeGraph();
        }

        public void ShowPointsType(PointsTypeRT pointsTypeRT)
        {
            RTGraphController.PointsType = pointsTypeRT;
            UpdateRetentionTimeGraph();
        }

        public void ShowRegressionMethod(RegressionMethodRT regressionMethod)
        {
            RTGraphController.RegressionMethod = regressionMethod;
            UpdateRetentionTimeGraph();
        }

        // timePeptideComparisonMenuItem_Click moved to RetentionTimesContextMenu

        public void ShowRTPeptideGraph()
        {
            Settings.Default.RTGraphTypes.Insert(0, GraphTypeSummary.peptide);
            ShowGraphRetentionTime(true, GraphTypeSummary.peptide);
            UpdateRetentionTimeGraph();
            SynchronizeSummaryZooming();
        }

        // showRTLegendContextMenuItem_Click moved to RetentionTimesContextMenu

        public void ShowRTLegend(bool show)
        {
            Settings.Default.ShowRetentionTimesLegend = show;
            UpdateRetentionTimeGraph();
        }

        // replicateComparisonMenuItem_Click moved to RetentionTimesContextMenu

        public void ShowRTReplicateGraph()
        {
            Settings.Default.RTGraphTypes.Insert(0, GraphTypeSummary.replicate);
            ShowGraphRetentionTime(true, GraphTypeSummary.replicate);
            UpdateRetentionTimeGraph();
            SynchronizeSummaryZooming();
        }

        // schedulingMenuItem_Click moved to RetentionTimesContextMenu

        public void ShowRTSchedulingGraph()
        {
            Settings.Default.RTGraphTypes.Insert(0, GraphTypeSummary.schedule);
            ShowGraphRetentionTime(true, GraphTypeSummary.schedule);
            UpdateRetentionTimeGraph();
        }

        // refineRTContextMenuItem_Click, predictionRTContextMenuItem_Click moved to RetentionTimesContextMenu

        // averageReplicatesContextMenuItem_Click moved to ContextMenuControl

        public void ShowAverageReplicates()
        {
            Settings.Default.ShowRegressionReplicateEnum = ReplicateDisplay.all.ToString();
            UpdateSummaryGraphs();
        }

        // singleReplicateRTContextMenuItem_Click moved to ContextMenuControl

        public void ShowSingleReplicate()
        {
            Settings.Default.ShowRegressionReplicateEnum = ReplicateDisplay.single.ToString();
            // No CVs with single replicate data views
            Settings.Default.ShowPeptideCV = false;
            UpdateSummaryGraphs();
        }

        // bestReplicateRTContextMenuItem_Click, replicatesRTContextMenuItem_DropDownOpening moved to ContextMenuControl

        // setRTThresholdContextMenuItem_Click moved to RetentionTimesContextMenu

        public void ShowRegressionRTThresholdDlg()
        {
            using (var dlg = new RegressionRTThresholdDlg())
            {
                dlg.Threshold = Settings.Default.RTResidualRThreshold;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.RTResidualRThreshold = dlg.Threshold;
                    UpdateRetentionTimeGraph();
                }
            }
        }

        // createRTRegressionContextMenuItem_Click moved to RetentionTimesContextMenu
        public void CreateRegression()
        {
            var listRegression = Settings.Default.RetentionTimeList;
            var regression = RTGraphController.RegressionRefined;
            string name = Path.GetFileNameWithoutExtension(DocumentFilePath);
            if (listRegression.ContainsKey(name))
            {
                int i = 2;
                while (listRegression.ContainsKey(name + i))
                    i++;
                name += i;
            }
            if (regression != null)
                regression = (RetentionTimeRegression) regression.ChangeName(name);

            using (var dlg = new EditRTDlg(listRegression))
            {
                dlg.Regression = regression;
                dlg.ShowPeptides(true);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    regression = dlg.Regression;
                    listRegression.Add(regression);

                    ModifyDocument(string.Format(SkylineResources.SkylineWindow_CreateRegression_Set_regression__0__, regression!.Name),
                                   doc =>
                                   doc.ChangeSettings(
                                       doc.Settings.ChangePeptidePrediction(p => p.ChangeRetentionTime(regression))), AuditLogEntry.SettingsLogFunction);
                }
            }
        }

        // chooseCalculatorContextMenuItem_DropDownOpening, SetupCalculatorChooser moved to RetentionTimesContextMenu

        public void ChooseCalculator(RtCalculatorOption option)
        {
            Settings.Default.RtCalculatorOption = option;
            UpdateRetentionTimeGraph();
        }

        public void ChooseCalculator(string irtCalc)
        {
            ChooseCalculator(new RtCalculatorOption.Irt(irtCalc));
        }

        // addCalculatorContextMenuItem_Click, updateCalculatorContextMenuItem_Click moved to RetentionTimesContextMenu

        public void ShowEditCalculatorDlg()
        {
            var list = Settings.Default.RTScoreCalculatorList;
            var regressionRT = RTGraphController.RegressionRefined;
            if (regressionRT != null && list.CanEditItem(regressionRT.Calculator))
            {
                var calcOld = regressionRT.Calculator;
                var calcNew = list.EditItem(this, calcOld, list, null);
                if (calcNew != null && !Equals(calcNew, calcOld))
                {
                    list.SetValue(calcNew);

                    var regressionRTDoc = DocumentUI.Settings.PeptideSettings.Prediction.RetentionTime;
                    if (regressionRTDoc != null && Equals(calcOld.Name, regressionRTDoc.Calculator.Name) &&
                        !Equals(calcNew, regressionRTDoc.Calculator))
                    {
                        ModifyDocument(string.Format(SkylineResources.SkylineWindow_ShowEditCalculatorDlg_Update__0__calculator, calcNew.Name), doc =>
                            doc.ChangeSettings(doc.Settings.ChangePeptidePrediction(predict =>
                                predict.ChangeRetentionTime(predict.RetentionTime.ChangeCalculator(calcNew)))), AuditLogEntry.SettingsLogFunction);
                    }
                }
            }
        }

        // removeRTOutliersContextMenuItem_Click moved to RetentionTimesContextMenu

        public void RemoveRTOutliers()
        {
            var outliers = RTGraphController.Outliers;
            var outlierIds = new HashSet<int>();
            foreach (var outlier in outliers)
                outlierIds.Add(outlier.Id.GlobalIndex);

            ModifyDocument(SkylineResources.SkylineWindow_RemoveRTOutliers_Remove_retention_time_outliers,
                doc => (SrmDocument) doc.RemoveAll(outlierIds),
                docPair => AuditLogEntry.CreateCountChangeEntry(MessageType.removed_rt_outlier,
                    MessageType.removed_rt_outliers, docPair.OldDocumentType, RTGraphController.Outliers, outlier =>  MessageArgs.Create(AuditLogEntry.GetNodeName(docPair.OldDoc, outlier)), null));
        }

        // removeRTContextMenuItem_Click, peptideRTValueMenuItem_DropDownOpening, InsertAlignmentMenuItems,
        // allRTValueContextMenuItem_Click, timeRTValueContextMenuItem_Click, fwhmRTValueContextMenuItem_Click,
        // fwbRTValueContextMenuItem_Click moved to RetentionTimesContextMenu

        public void ShowRTPeptideValue(RTPeptideValue value)
        {
            Settings.Default.RTPeptideValue = value.ToString();
            UpdateRetentionTimeGraph();
        }

        // timePropsContextMenuItem_Click moved to RetentionTimesContextMenu

        public void ShowRTPropertyDlg(GraphSummary graph)
        {
            if (graph.Type == GraphTypeSummary.schedule)
            {
                using (var dlg = new SchedulingGraphPropertyDlg())
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        UpdateRetentionTimeGraph();
                    }
                }
            }
            else
            {
                using (var dlg = new RTChartPropertyDlg())
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        UpdateSummaryGraphs();
                    }
                }
            }
        }

        public void UpdateRetentionTimeGraph()
        {
            _listGraphRetentionTime.ForEach(g =>
            {
                try
                {
                    g.UpdateUI();
                }
                catch (CalculatorException e)
                {
                    MessageDlg.ShowException(this, e);
                    Settings.Default.RTCalculatorName = string.Empty;
                }
            });
        }

        public AlignmentForm ShowRetentionTimeAlignmentForm()
        {
            var form = FormUtil.OpenForms.OfType<AlignmentForm>().FirstOrDefault();
            if (form == null)
            {
                form = new AlignmentForm(this);
                form.Show(this);
            }
            else
            {
                form.Activate();
            }
            return form;
        }

        #endregion

        #region Peak area graph

        public GraphSummary GraphPeakArea { get { return _listGraphPeakArea.FirstOrDefault(); } }
        public ICollection<GraphSummary> ListGraphPeakArea
        {
            get { return _listGraphPeakArea; }
        }

        public ICollection<GraphSummary> ListGraphDetections
        {
            get
            {
                return _listGraphDetections;
            }
        }

        public ICollection<GraphSummary> ListGraphMassError
        {
            get
            {
                return _listGraphMassError;
            }
        }

        public ICollection<GraphSummary> ListGraphRetentionTime
        {
            get { return _listGraphRetentionTime; }
        }

        public void UpdateUIGraphPeakArea(bool visible)
        {
            var list = Settings.Default.AreaGraphTypes.ToArray();
            ShowGraphPeakArea(visible);
            if (!visible)
            {
                Settings.Default.AreaGraphTypes.Clear();
                Settings.Default.AreaGraphTypes.AddRange(list);
            }
        }

        public void ShowGraphPeakArea(bool show)
        {
            Settings.Default.AreaGraphTypes.ToList().ForEach(t => ShowGraphPeakArea(show, t));
        }

        public void ShowGraphPeakArea(bool show, GraphTypeSummary type)
        {
            ShowGraph(_listGraphPeakArea, show, type, CreateGraphPeakArea);
        }

        private GraphSummary CreateGraphPeakArea(GraphTypeSummary type)
        {
            if (type == GraphTypeSummary.invalid)
                return null;

            GraphSummary graph = new GraphSummary(type, this, new AreaGraphController(), SelectedResultsIndex);
            graph.FormClosed += graphPeakArea_FormClosed;
            graph.VisibleChanged += graphPeakArea_VisibleChanged;
            graph.GraphControl.ZoomEvent += GraphControl_ZoomEvent;
            graph.Toolbar = new AreaCVToolbar(graph);
            _listGraphPeakArea.Insert(0, graph);

            return graph;
        }

        private void DestroyGraphPeakArea(GraphSummary graph)
        {
            graph.FormClosed -= graphPeakArea_FormClosed;
            graph.VisibleChanged -= graphPeakArea_VisibleChanged;
            graph.HideOnClose = false;
            graph.Close();
            _listGraphPeakArea.Remove(graph);
            Settings.Default.AreaGraphTypes.Remove(graph.Type);
        }

        private void graphPeakArea_VisibleChanged(object sender, EventArgs e)
        {
            var graph = (GraphSummary)sender;
            if (graph.Visible)
            {
                Settings.Default.AreaGraphTypes.Insert(0, graph.Type);
                _listGraphPeakArea.Remove(graph);
                _listGraphPeakArea.Insert(0, graph);
            }
            else if (graph.IsHidden)
            {
                Settings.Default.AreaGraphTypes.Remove(graph.Type);
            }   
        }

        private void graphPeakArea_FormClosed(object sender, FormClosedEventArgs e)
        {
            GraphSummary graph = (GraphSummary)sender;
            _listGraphPeakArea.Remove(graph);
            Settings.Default.AreaGraphTypes.Remove(graph.Type);
        }

        // BuildAreaGraphMenu and peak area event handlers moved to PeakAreasContextMenu

        public void SetAreaCVTransitions(AreaCVTransitions transitions, int count)
        {
            AreaGraphController.AreaCVTransitionsCount = count;
            AreaGraphController.AreaCVTransitions = transitions;
            UpdatePeakAreaGraph();
        }


        public void SetAreaGraphDisplayType(AreaGraphDisplayType displayType)
        {
            AreaGraphController.GraphDisplayType = displayType;
            UpdatePeakAreaGraph();
        }

        public void SynchronizeSummaryZooming(GraphSummary graphSummary = null, ZoomState zoomState = null)
        {
            var activeGraphSummary = graphSummary ?? dockPanel.ActiveContent as GraphSummary;

            if (!Settings.Default.SynchronizeSummaryZooming || activeGraphSummary == null)
            {
                return;
            }

            var activePane = activeGraphSummary.GraphControl.GraphPane;
                
            GraphSummary[] graphSummaries = new List<GraphSummary>(_listGraphMassError.Concat(_listGraphPeakArea.Concat(_listGraphRetentionTime))).ToArray();
            
            // Find the correct GraphSummary
            int index = graphSummaries.IndexOf(g => ReferenceEquals(g, activeGraphSummary));

            // If zoomstate is null, we use the current state of the active pane
            var xScaleState = zoomState == null ? new ScaleState(activePane.XAxis) : zoomState.XAxis;
            var x2ScaleState = zoomState == null ? new ScaleState(activePane.X2Axis) : zoomState.X2Axis;

            double add = 0.0;

            // If the expected value (library) is visible the zoom has to be shifted
            if (activePane is AreaReplicateGraphPane && (activePane as AreaReplicateGraphPane).ExpectedVisible.IsVisible())
                add = -1.0;
       
            for (int i = 0; i < graphSummaries.Length; ++i)
            {
                // Make sure we are not syncing the same graph or graphs of different types
                if (i != index && graphSummaries[i] != null && graphSummaries[i].Type == graphSummaries[index].Type && graphSummaries[i].Visible)
                {
                    bool isExpectedVisible = graphSummaries[i].GraphControl.GraphPane is AreaReplicateGraphPane && ((AreaReplicateGraphPane)graphSummaries[i].GraphControl.GraphPane).ExpectedVisible.IsVisible();
                    
                    if (isExpectedVisible)
                        ++add;

                    graphSummaries[i].GraphControl.GraphPane.XAxis.Scale.Min = xScaleState.Min + add;
                    graphSummaries[i].GraphControl.GraphPane.XAxis.Scale.Max = xScaleState.Max + add;
                    graphSummaries[i].GraphControl.GraphPane.X2Axis.Scale.Min = x2ScaleState.Min + add;
                    graphSummaries[i].GraphControl.GraphPane.X2Axis.Scale.Max = x2ScaleState.Max + add;

                    if (isExpectedVisible)
                        --add;

                    graphSummaries[i].UpdateUI(false);
                }
            }
        }

        void GraphControl_ZoomEvent(ZedGraphControl sender, ZoomState oldState, ZoomState newState, PointF mousePosition)
        {
            // We pass in a GraphSummary here because sometimes dockPanel.ActiveContent is not the graph the user is zooming in on
            GraphSummary[] graphSummaries = new List<GraphSummary>(_listGraphMassError.Concat(_listGraphPeakArea.Concat(_listGraphRetentionTime))).ToArray();
            SynchronizeSummaryZooming(graphSummaries.FirstOrDefault(gs => gs != null && ReferenceEquals(gs.GraphControl, sender)), newState);
        }

        public void SetGroupApplyToBy(ReplicateValue replicateValue)
        {
            Settings.Default.GroupApplyToBy = replicateValue?.ToPersistedString();
        }

        // AddReplicateOrderAndGroupByMenuItems, ReplicateOrderContextMenuItem, ReplicateGroupByContextMenuItem,
        // GroupByReplicateAnnotationMenuItem, OrderByReplicateAnnotationMenuItem moved to ContextMenuControl

        public void RemoveAboveCVCutoff(GraphSummary graphSummary)
        {
            var pane = graphSummary.GraphPanes.First() as IAreaCVHistogramInfo;
            if (pane == null ||
                (graphSummary.Type != GraphTypeSummary.histogram && graphSummary.Type != GraphTypeSummary.histogram2d))
                return;

            var cutoff = Settings.Default.AreaCVCVCutoff / AreaGraphController.GetAreaCVFactorToDecimal();
            // Create a set of everything that should remain, so that peptides excluded by
            // the q value cut-off will also be removed
            var ids = new HashSet<int>(pane.CurrentData.Data.Where(d => d.CV < cutoff)
                    .SelectMany(d => d.PeptideAnnotationPairs)
                    .Select(pair => pair.TransitionGroup.Id.GlobalIndex));

            var nodeCount = 0;
            // Remove everything not in the set
            ModifyDocument(SkylineResources.SkylineWindow_RemoveAboveCVCutoff_Remove_peptides_above_CV_cutoff, doc =>
            {
                var setRemove = AreaCVRefinementData.IndicesToRemove(doc, ids);
                nodeCount = setRemove.Count;
                return (SrmDocument)doc.RemoveAll(setRemove, null, (int) SrmDocument.Level.Molecules);
            }, docPair => AuditLogEntry.CreateSimpleEntry(nodeCount == 1 ? MessageType.removed_peptide_above_cutoff : MessageType.removed_peptides_above_cutoff, docPair.OldDocumentType,
                nodeCount, Settings.Default.AreaCVCVCutoff * AreaGraphController.GetAreaCVFactorToPercentage()));
        }

        public void SetAreaCVGroup(ReplicateValue replicateValue)
        {
            AreaGraphController.GroupByGroup = replicateValue?.ToPersistedString();
            if (null == replicateValue)
                AreaGraphController.GroupByAnnotation = null;
            UpdatePeakAreaGraph();
        }

        public void SetAreaCVAnnotation(object annotationValue, bool update = true)
        {
            AreaGraphController.GroupByAnnotation = annotationValue;

            if(update)
                UpdatePeakAreaGraph();
        }

        public void SetAreaCVPointsType(PointsTypePeakArea pointsType)
        {
            AreaGraphController.PointsType = pointsType;
            UpdatePeakAreaGraph();
        }

        public bool GraphChecked(GraphTypeSummary type)
        {
            UniqueList<GraphTypeSummary> types;
            List<GraphSummary> list;
            switch (type)
            {
                case GraphTypeSummary.replicate:
                case GraphTypeSummary.peptide:
                case GraphTypeSummary.histogram:
                case GraphTypeSummary.histogram2d:
                    types = Settings.Default.AreaGraphTypes;
                    list = _listGraphPeakArea;
                    break;
                case GraphTypeSummary.detections:
                case GraphTypeSummary.detections_histogram:
                    types = Settings.Default.DetectionGraphTypes;
                    list = _listGraphDetections;
                    break;
                default: throw new ArgumentException();
            }

            return GraphChecked(list, types, type);
        }

        public void SetAreaCVBinWidth(double binWidth)
        {
            Settings.Default.AreaCVHistogramBinWidth = binWidth;
            UpdatePeakAreaGraph();
        }

        public void ShowPeakAreaReplicateComparison()
        {
            Settings.Default.AreaGraphTypes.Insert(0, GraphTypeSummary.replicate);
            ShowGraphPeakArea(true, GraphTypeSummary.replicate);
            UpdatePeakAreaGraph();
            SynchronizeSummaryZooming();
        }


        public void SetNormalizationMethod(NormalizeOption normalizeOption)
        {
            Settings.Default.AreaNormalizeOption = normalizeOption;
            SequenceTree.NormalizeOption = normalizeOption;
            UpdatePeakAreaGraph();
        }

        public NormalizeOption AreaNormalizeOption
        {
            get
            {
                return Settings.Default.AreaNormalizeOption;
            }
            set
            {
                if (!Equals(value, AreaNormalizeOption))
                {
                    SetNormalizationMethod(value);
                }
                else
                {
                    SequenceTree.NormalizeOption = value;
                }
            }
        }

        public void SetNormalizationMethod(NormalizationMethod normalizationMethod)
        {
            SetNormalizationMethod(NormalizeOption.FromNormalizationMethod(normalizationMethod));
        }

        public void ShowPeakAreaPeptideGraph()
        {
            Settings.Default.AreaGraphTypes.Insert(0, GraphTypeSummary.peptide);
            ShowGraphPeakArea(true, GraphTypeSummary.peptide);
            UpdatePeakAreaGraph();
            SynchronizeSummaryZooming();
        }

        public void ShowPeakAreaRelativeAbundanceGraph()
        {
            Settings.Default.AreaGraphTypes.Insert(0, GraphTypeSummary.abundance);
            ShowGraphPeakArea(true, GraphTypeSummary.abundance);
            UpdatePeakAreaGraph();
        }

        public void ShowPeakAreaCVHistogram()
        {
            Settings.Default.AreaGraphTypes.Insert(0, GraphTypeSummary.histogram);
            ShowGraphPeakArea(true, GraphTypeSummary.histogram);
            UpdatePeakAreaGraph();
        }

        public void ShowPeakAreaCVHistogram2D()
        {
            Settings.Default.AreaGraphTypes.Insert(0, GraphTypeSummary.histogram2d);
            ShowGraphPeakArea(true, GraphTypeSummary.histogram2d);
            UpdatePeakAreaGraph();
        }

        // replicateOrderDocumentContextMenuItem_Click, replicateOrderAcqTimeContextMenuItem_Click moved to ContextMenuControl

        public void ShowReplicateOrder(SummaryReplicateOrder order)
        {
            SummaryReplicateGraphPane.ReplicateOrder = order;
            SummaryReplicateGraphPane.OrderByReplicateAnnotation = null;
            UpdateSummaryGraphs();
        }

        // groupByReplicateContextMenuItem_Click moved to ContextMenuControl

        public void GroupByReplicateValue(ReplicateValue replicateValue)
        {
            SummaryReplicateGraphPane.GroupByReplicateAnnotation = replicateValue?.ToPersistedString();
            UpdateSummaryGraphs();
        }

        public void GroupByReplicateAnnotation(string annotationName)
        {
            SummaryReplicateGraphPane.GroupByReplicateAnnotation =
                DocumentAnnotations.ANNOTATION_PREFIX + annotationName;
            UpdateSummaryGraphs();
        }

        public void OrderByReplicateAnnotation(ReplicateValue replicateValue)
        {
            SummaryReplicateGraphPane.OrderByReplicateAnnotation = replicateValue.ToPersistedString();
            UpdateSummaryGraphs();
        }

        // scopeContextMenuItem_DropDownOpening, documentScopeContextMenuItem_Click,
        // proteinScopeContextMenuItem_Click moved to ContextMenuControl

        public void AreaScopeTo(AreaScope areaScope)
        {
            AreaGraphController.AreaScope = areaScope;
            UpdateSummaryGraphs();
        }

        // peptideOrderContextMenuItem_DropDownOpening, peptideOrderDocumentContextMenuItem_Click,
        // peptideOrderRTContextMenuItem_Click, peptideOrderAreaContextMenuItem_Click,
        // peptideOrderMassErrorContextMenuItem_Click moved to ContextMenuControl

        public void ShowPeptideOrder(SummaryPeptideOrder order)
        {
            SummaryPeptideGraphPane.PeptideOrder = order;
            UpdateSummaryGraphs();
        }

        public void NormalizeAreaGraphTo(NormalizeOption areaView)
        {
            AreaNormalizeOption = areaView;

            UpdatePeakAreaGraph();
        }

        public void ShowPeptideLogScale(bool isChecked)
        {
            Settings.Default.AreaLogScale = isChecked ;
            if (isChecked && !AreaNormalizeOption.AllowLogScale)
            {
                AreaNormalizeOption = NormalizeOption.NONE;
            }
            UpdateSummaryGraphs();
        }


        public void SetAreaProteinTargets(bool areaProteinTargets)
        {
            Settings.Default.AreaProteinTargets = areaProteinTargets;
            UpdateSummaryGraphs();
        }
        public void SetExcludePeptideListsFromAbundanceGraph(bool excludePeptideLists)
        {
            Settings.Default.ExcludePeptideListsFromAbundanceGraph = excludePeptideLists;
            UpdateSummaryGraphs();
        }

        public void ShowCVValues(bool isChecked)
        {
            Settings.Default.ShowPeptideCV = isChecked;
            // Showing CVs only makes sense for Replicates = All
            Settings.Default.ShowRegressionReplicateEnum = ReplicateDisplay.all.ToString();
            // Showing CVs does not make sense for All retention time values at once
            // But this is confusing now, with replicate annotation grouping
//            if (RTPeptideGraphPane.RTValue == RTPeptideValue.All)
//                Settings.Default.RTPeptideValue = RTPeptideValue.Retention.ToString();
            UpdateSummaryGraphs();
        }

        public void ShowAreaCVPropertyDlg(GraphSummary graphSummary)
        {

            using (var dlgProperties = new AreaCVToolbarProperties(graphSummary))
            {
                if (dlgProperties.ShowDialog(this) == DialogResult.OK)
                    UpdatePeakAreaGraph();
            }
        }

        public void ShowAreaPropertyDlg()
        {
            using (var dlg = new AreaChartPropertyDlg())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    UpdateSummaryGraphs();
                }
            }
        }

        private class SelectNormalizeHandler : SelectRatioHandler
        {
            public SelectNormalizeHandler(SkylineWindow skyline, NormalizeOption ratioIndex) : base(skyline, ratioIndex)
            {
            }

            protected override void OnMenuItemClick()
            {
                base.OnMenuItemClick();

                _skyline.UpdatePeakAreaGraph();
            }
        }

        public void SetNormalizeIndex(NormalizeOption index)
        {
            new SelectNormalizeHandler(this, index).Select();
        }

        public void ShowPeakAreaLegend(bool show)
        {
            Settings.Default.ShowPeakAreaLegend = show;
            UpdateSummaryGraphs();
        }

        public void UpdatePeakAreaGraph()
        {
            _listGraphPeakArea.ForEach(g => g.UpdateUI());
        }

        public void UpdateRelativeAbundanceGraphs()
        {
            _listGraphPeakArea.FindAll(g => g.Type == GraphTypeSummary.abundance).ForEach(g => g.UpdateUI());
        }
        internal void UpdateSummaryGraphs()
        {
            UpdateRetentionTimeGraph();
            UpdatePeakAreaGraph();    
            UpdateMassErrorGraph();
            UpdateDetectionsGraph();
        }

        #endregion

        #region Mass error graph

        public GraphSummary GraphMassError { get { return _listGraphMassError.FirstOrDefault(); } }

        public void UpdateUIGraphMassError(bool visible)
        {
            var list = Settings.Default.MassErrorGraphTypes.ToArray();
            ShowGraphMassError(visible);
            if (!visible)
            {
                Settings.Default.MassErrorGraphTypes.Clear();
                Settings.Default.MassErrorGraphTypes.AddRange(list);
            }
        }
        public void ShowGraphMassError(bool show)
        {
            Settings.Default.MassErrorGraphTypes.ToList().ForEach(t => ShowGraphMassError(show, t));
        }

        public void ShowGraphMassError(bool show, GraphTypeSummary type)
        {
            ShowGraph(_listGraphMassError, show, type, CreateGraphMassError);
        }

        private GraphSummary CreateGraphMassError(GraphTypeSummary type)
        {
            if (type == GraphTypeSummary.invalid)
                return null;

            var graph = new GraphSummary(type, this, new MassErrorGraphController(), SelectedResultsIndex);
            graph.FormClosed += graphMassError_FormClosed;
            graph.VisibleChanged += graphMassError_VisibleChanged;
            graph.GraphControl.ZoomEvent += GraphControl_ZoomEvent;
            _listGraphMassError.Insert(0, graph);

            return graph;
        }

        private void DestroyGraphMassError(GraphSummary graph)
        {
            graph.FormClosed -= graphMassError_FormClosed;
            graph.VisibleChanged -= graphMassError_VisibleChanged;
            graph.HideOnClose = false;
            graph.Close();
            _listGraphMassError.Remove(graph);
            Settings.Default.MassErrorGraphTypes.Remove(graph.Type);
        }

        private void graphMassError_VisibleChanged(object sender, EventArgs e)
        {
            var graph = (GraphSummary)sender;
            if (graph.Visible)
            {
                Settings.Default.MassErrorGraphTypes.Insert(0, graph.Type);
                _listGraphMassError.Remove(graph);
                _listGraphMassError.Insert(0, graph);
            }
            else if (graph.IsHidden)
            {
                Settings.Default.MassErrorGraphTypes.Remove(graph.Type);
            }   
        }

        private void graphMassError_FormClosed(object sender, FormClosedEventArgs e)
        {
            GraphSummary graph = (GraphSummary) sender;
            _listGraphMassError.Remove(graph);
            Settings.Default.MassErrorGraphTypes.Remove(graph.Type);
        }

        public void ShowMassErrorReplicateComparison()
        {
            Settings.Default.MassErrorGraphTypes.Insert(0, GraphTypeSummary.replicate);
            ShowGraphMassError(true, GraphTypeSummary.replicate);
            UpdateMassErrorGraph();
            SynchronizeSummaryZooming();
        }

        public void ShowMassErrorPeptideGraph()
        {
            Settings.Default.MassErrorGraphTypes.Insert(0, GraphTypeSummary.peptide);
            ShowGraphMassError(true, GraphTypeSummary.peptide);
            UpdateMassErrorGraph();
            SynchronizeSummaryZooming();
        }

        public void ShowMassErrorHistogramGraph()
        {
            Settings.Default.MassErrorGraphTypes.Insert(0, GraphTypeSummary.histogram);
            ShowGraphMassError(true, GraphTypeSummary.histogram);
            UpdateMassErrorGraph();
        }

        public void ShowMassErrorHistogramGraph2D()
        {
            Settings.Default.MassErrorGraphTypes.Insert(0, GraphTypeSummary.histogram2d);
            ShowGraphMassError(true, GraphTypeSummary.histogram2d);
            UpdateMassErrorGraph();
        }

        public void UpdateMassErrorGraph()
        {
            _listGraphMassError.ForEach(g => g.UpdateUI());
        }



        public void ChangeMassErrorTransition(TransitionMassError transitionMassError)
        {
            MassErrorGraphController.HistogramTransiton = transitionMassError;
            UpdateMassErrorGraph();
        }

        public void ChangeMassErrorDisplayType(DisplayTypeMassError displayType)
        {
            MassErrorGraphController.HistogramDisplayType = displayType;
            UpdateMassErrorGraph();
        }

        public void UpdateXAxis(Histogram2DXAxis Xaxis)
        {
            MassErrorGraphController.Histogram2DXAxis = Xaxis;
            UpdateMassErrorGraph();
        }

        public void ShowMassErrorLegend(bool show)
        {
            Settings.Default.ShowMassErrorLegend = show;
            UpdateSummaryGraphs();
        }

        public void SwitchLogScale()
        {
            Settings.Default.MassErrorHistogram2DLogScale = !Settings.Default.MassErrorHistogram2DLogScale;
            UpdateMassErrorGraph();
        }

        public void UpdateBinSize(double bin)
        {
            Settings.Default.MassErorrHistogramBinSize = bin;
            UpdateMassErrorGraph();
        }

        public void ShowPointsTypeMassError(PointsTypeMassError pointsTypeMassError)
        {
            MassErrorGraphController.PointsType = pointsTypeMassError;
            UpdateMassErrorGraph();
        }

        public void ShowMassErrorPropertyDlg()
        {
            using (var dlg = new MassErrorChartPropertyDlg())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    UpdateSummaryGraphs();
                }
            }
        }

        #endregion

        #region Detections Graph

        public void UpdateUIGraphDetection(bool visible)
        {
            var list = Settings.Default.DetectionGraphTypes.ToArray();
            ShowGraphDetection(visible);
            if (!visible)
            {
                Settings.Default.DetectionGraphTypes.Clear();
                Settings.Default.DetectionGraphTypes.AddRange(list);
            }
        }

        public void ShowDetectionsReplicateComparisonGraph()
        {
            Settings.Default.DetectionGraphTypes.Insert(0, GraphTypeSummary.detections);
            ShowGraphDetection(true, GraphTypeSummary.detections);
            UpdateDetectionsGraph();
        }

        public void ShowDetectionsHistogramGraph()
        {
            Settings.Default.DetectionGraphTypes.Insert(0, GraphTypeSummary.detections_histogram);
            ShowGraphDetection(true, GraphTypeSummary.detections_histogram);
            UpdateDetectionsGraph();
        }

        public void ShowGraphDetection(bool show)
        {
            Settings.Default.DetectionGraphTypes.ToList().ForEach(t => ShowGraphDetection(show, t));
        }

        public void ShowGraphDetection(bool show, GraphTypeSummary type)
        {
            ShowGraph(_listGraphDetections, show, type, CreateGraphDetections);
        } 

        private GraphSummary CreateGraphDetections(GraphTypeSummary type)
        {
            if (type == GraphTypeSummary.invalid)
                return null;

            GraphSummary graph = new GraphSummary(type, this, new DetectionsGraphController(), SelectedResultsIndex);
            graph.FormClosed += graphDetections_FormClosed;
            graph.VisibleChanged += graphDetections_VisibleChanged;
            graph.GraphControl.ZoomEvent += GraphControl_ZoomEvent;
            graph.Toolbar = new DetectionsToolbar(graph);
            _listGraphDetections.Insert(0, graph);

            return graph;
        }

        private void DestroyGraphDetections(GraphSummary graph)
        {
            graph.FormClosed -= graphDetections_FormClosed;
            graph.VisibleChanged -= graphDetections_VisibleChanged;
            graph.HideOnClose = false;                   
            graph.Close();
            _listGraphDetections.Remove(graph);
            Settings.Default.DetectionGraphTypes.Remove(graph.Type);
        }

        private void graphDetections_VisibleChanged(object sender, EventArgs e)
        {
            var graph = (GraphSummary)sender;
            if (graph.Visible)
            {
                Settings.Default.DetectionGraphTypes.Insert(0, graph.Type);
                _listGraphDetections.Remove(graph);
                _listGraphDetections.Insert(0, graph);
            }
            else if (graph.IsHidden)
            {
                Settings.Default.DetectionGraphTypes.Remove(graph.Type);
            }
        }

        private void graphDetections_FormClosed(object sender, FormClosedEventArgs e)
        {
            GraphSummary graph = (GraphSummary)sender;
            _listGraphDetections.Remove(graph);
            Settings.Default.DetectionGraphTypes.Remove(graph.Type);
        }
        public void UpdateDetectionsGraph()
        {
            _listGraphDetections.ForEach(g => g.UpdateUI());
        }

        public GraphSummary DetectionsPlot { get { return _listGraphDetections.FirstOrDefault(); } }

        public void ShowDetectionsPropertyDlg(GraphSummary graph)
        {
            using (var dlg = new DetectionToolbarProperties(graph))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    UpdateSummaryGraphs();
                }
            }
        }

        #endregion

        #region Results Grid

        public void ShowResultsGrid(bool show)
        {
            if (show)
            {
                if (_resultsGridForm != null && !Program.SkylineOffscreen)
                {
                    _resultsGridForm.Activate();
                }
                else
                {
                    _resultsGridForm = _resultsGridForm ?? CreateResultsGrid();

                    var rectFloat = GetFloatingRectangleForNewWindow();
                    _resultsGridForm.Show(dockPanel, rectFloat);
                }
            }
            else
            {
                if (_resultsGridForm != null)
                {
                    _resultsGridForm.Hide();
                }
            }
        }

        public LiveResultsGrid CreateResultsGrid()
        {
            Debug.Assert(null == _resultsGridForm);
            _resultsGridForm = new LiveResultsGrid(this);
            _resultsGridForm.FormClosed += resultsGrid_FormClosed;
            _resultsGridForm.VisibleChanged += resultsGrid_VisibleChanged;
            return _resultsGridForm;
        }

        private void DestroyResultsGrid()
        {
            if (_resultsGridForm != null)
            {
                _resultsGridForm.FormClosed -= resultsGrid_FormClosed;
                _resultsGridForm.VisibleChanged -= resultsGrid_VisibleChanged;
                _resultsGridForm.HideOnClose = false;
                _resultsGridForm.Close();
                _resultsGridForm = null;
            }
        }
        private void resultsGrid_VisibleChanged(object sender, EventArgs e)
        {
            Settings.Default.ShowResultsGrid = (_resultsGridForm != null && _resultsGridForm.Visible);
        }

        void resultsGrid_FormClosed(object sender, FormClosedEventArgs e)
        {
            // Update settings and menu check
            Settings.Default.ShowResultsGrid = false;
            _resultsGridForm = null;
        }

        #endregion

        #region Document Grid

        public void ShowDocumentGrid(bool show)
        {
            if (show)
            {
                if (_documentGridForm != null && !Program.SkylineOffscreen)
                {
                    _documentGridForm.Activate();
                }
                else
                {
                    _documentGridForm = _documentGridForm ?? CreateDocumentGrid();
                    if (_documentGridForm != null)
                    {
                        var rectFloat = GetFloatingRectangleForNewWindow();
                        _documentGridForm.Show(dockPanel, rectFloat);
                    }
                }
            }
            else
            {
                if (_documentGridForm != null)
                {
                    _documentGridForm.Close();
                }
            }
            
        }

        public DocumentGridForm CreateDocumentGrid()
        {
            Assume.IsNull(_documentGridForm);
            _documentGridForm = new DocumentGridForm(this);
            _documentGridForm.FormClosed += documentGrid_FormClosed;
            if (!string.IsNullOrEmpty(Settings.Default.DocumentGridView))
            {
                var viewName = ViewName.Parse(Settings.Default.DocumentGridView);
                if (viewName.HasValue)
                {
                    _documentGridForm.DataboundGridControl.ChooseView(viewName.Value);
                }
            }
            return _documentGridForm;
        }

        private void DestroyDocumentGrid()
        {
            if (null != _documentGridForm)
            {
                _documentGridForm.FormClosed -= documentGrid_FormClosed;
                _documentGridForm.Close();
                _documentGridForm = null;
            }
        }

        void documentGrid_FormClosed(object sender, FormClosedEventArgs e)
        {
            _documentGridForm = null;
        }

        #endregion

        #region Calibration Curves

        private CalibrationForm CreateCalibrationForm()
        {
            Assume.IsNull(_calibrationForm);
            _calibrationForm = new CalibrationForm(this);
            _calibrationForm.FormClosed += calibrationForm_FormClosed;
            return _calibrationForm;
        }

        void calibrationForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            _calibrationForm = null;
        }

        private void DestroyCalibrationForm()
        {
            if (null != _calibrationForm)
            {
                _calibrationForm.FormClosed -= calibrationForm_FormClosed;
                _calibrationForm.Close();
                _calibrationForm = null;
            }

        }

        public CalibrationForm ShowCalibrationForm()
        {
            if (null != _calibrationForm)
            {
                _calibrationForm.Activate();
            }
            else
            {
                var rectFloat = GetFloatingRectangleForNewWindow();
                CreateCalibrationForm().Show(dockPanel, rectFloat);
            }
            return _calibrationForm;
        }

        #endregion

        #region Audit Log

        public void ShowAuditLog()
        {
            if (_auditLogForm != null && !Program.SkylineOffscreen)
            {
                _auditLogForm.Activate();
            }
            else
            {
                _auditLogForm = _auditLogForm ?? CreateAuditLogForm();
                if (_auditLogForm != null)
                {
                    var rectFloat = GetFloatingRectangleForNewWindow();
                    // Ensure the audit log window is wide enough to show the "Enable audit logging" checkbox
                    rectFloat.Width = Math.Max(800, rectFloat.Width);

                    _auditLogForm.Show(dockPanel, rectFloat);
                }
            }
        }

        private AuditLogForm CreateAuditLogForm()
        {
            if (_auditLogForm == null)
            {
                _auditLogForm = AuditLogForm.MakeAuditLogForm(this);
                _auditLogForm.FormClosed += _auditLogForm_FormClosed;
            }

            return _auditLogForm;
        }

        private void DestroyAuditLogForm()
        {
            if (_auditLogForm != null)
            {
                _auditLogForm.FormClosed -= _auditLogForm_FormClosed;
                _auditLogForm.Close();
                _auditLogForm = null;
            }
        }

        private void _auditLogForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            _auditLogForm = null;
        }

        public void ClearAuditLog()
        {
            if (!Document.AuditLog.AuditLogEntries.IsRoot)
            {
                ModifyDocument(AuditLogStrings.AuditLogForm__clearLogButton_Click_Clear_audit_log,
                    document => document.ChangeAuditLog(AuditLogEntry.ROOT), docPair => AuditLogEntry.ClearLogEntry(docPair.OldDoc));
            } 
        }

        public AuditLogForm AuditLogForm
        {
            get { return _auditLogForm; }
        }

        #endregion

        #region Graph layout

        private const double MAX_TILED_ASPECT_RATIO = 2;

        public void ArrangeGraphsTiled()
        {
            ArrangeGraphs(DisplayGraphsType.Tiled);
        }

        public void ArrangeGraphs(DisplayGraphsType displayGraphsType)
        {
            var listGraphs = GetArrangeableGraphs();
            if (listGraphs.Count < 2)
                return;
            using (new DockPanelLayoutLock(dockPanel, true))
            {
                ArrangeGraphsGrouped(listGraphs, listGraphs.Count, GroupGraphsType.separated, displayGraphsType);
            }
        }

        public void ArrangeGraphsTabbed()
        {
            var listGraphs = GetArrangeableGraphs();
            if (listGraphs.Count < 2)
                return;
            using (new DockPanelLayoutLock(dockPanel, true))
            {
                ArrangeGraphsTabbed(listGraphs);
            }
        }

        public void ArrangeGraphsGrouped()
        {
            var order = Helpers.ParseEnum(Settings.Default.ArrangeGraphsOrder, GroupGraphsOrder.Position);
            bool reversed = Settings.Default.ArrangeGraphsReversed;
            var listGraphs = GetArrangeableGraphs(order, reversed);

            using (var dlg = new ArrangeGraphsGroupedDlg(listGraphs.Count))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (order != dlg.GroupOrder || reversed != dlg.Reversed)
                        listGraphs = GetArrangeableGraphs(dlg.GroupOrder, dlg.Reversed);
                    if (listGraphs.Count < 2)
                        return;

                    using (new DockPanelLayoutLock(dockPanel, true))
                    {
                        ArrangeGraphsGrouped(listGraphs, dlg.Groups, dlg.GroupType, dlg.DisplayType);
                    }
                }
            }
        }

        private void ArrangeGraphsGrouped(IList<DockableForm> listGraphs, int groups, GroupGraphsType groupType, DisplayGraphsType displayType)
        {
            // First just arrange everything into a single pane
            ArrangeGraphsTabbed(listGraphs);

            // Figure out how to distribute the panes into rows and columns
            var documentPane = FindPane(listGraphs[0]);
            double width = documentPane.Width;
            double height = documentPane.Height;
            int rows;
            if (displayType == DisplayGraphsType.Row)
            {
                rows = 1;
            }
            else if (displayType == DisplayGraphsType.Column)
            {
                rows = groups;
            }
            else
            {
                rows = 1;
                // ReSharper disable once PossibleLossOfFraction
                while ((height / rows) / (width / (groups / rows + (groups % rows > 0 ? 1 : 0))) > MAX_TILED_ASPECT_RATIO)
                    rows++;
            }

            int longRows = groups%rows;
            int columnsShort = groups/rows;

            // Distribute the forms into lists representing rows, columns, and groups
            var listTiles = new List<List<List<DockableForm>>>();
            if (groupType == GroupGraphsType.distributed)
            {
                // As if dealing a card deck over the groups
                int iForm = 0;
                int forms = listGraphs.Count;
                while (iForm < listGraphs.Count)
                {
                    for (int iRow = 0; iRow < rows; iRow++)
                    {
                        if (listTiles.Count <= iRow)
                            listTiles.Add(new List<List<DockableForm>>());
                        var rowTiles = listTiles[iRow];
                        int columns = columnsShort + (iRow < longRows ? 1 : 0);
                        for (int iCol = 0; iCol < columns && iForm < forms; iCol++)
                        {
                            if (rowTiles.Count <= iCol)
                                rowTiles.Add(new List<DockableForm>());
                            var tabbedForms = rowTiles[iCol];
                            tabbedForms.Add(listGraphs[iForm++]);
                        }
                    }
                }
            }
            else
            {
                // Filling each group before continuing to the next
                int count = listGraphs.Count;
                int longGroups = count % groups;
                int tabsShort = count / groups;
                for (int iRow = 0, iGroup = 0, iForm = 0; iRow < rows; iRow++)
                {
                    var rowTiles = new List<List<DockableForm>>();
                    listTiles.Add(rowTiles);
                    int columns = columnsShort + (iRow < longRows ? 1 : 0);
                    for (int iCol = 0; iCol < columns; iCol++)
                    {
                        var tabbedForms = new List<DockableForm>();
                        rowTiles.Add(tabbedForms);
                        int tabs = tabsShort + (iGroup++ < longGroups ? 1 : 0);
                        for (int iTab = 0; iTab < tabs; iTab++)
                        {
                            tabbedForms.Add(listGraphs[iForm++]);                            
                        }
                    }
                }                
            }

            // Place the forms in the dock panel
            // Rows first
            for (int i = 1; i < rows; i++)
            {
                PlacePane(i, 0, rows, DockPaneAlignment.Bottom, listTiles);
            }
            // Then columns in the rows
            for (int i = 0; i < rows; i++)
            {
                int columns = listTiles[i].Count;
                for (int j = 1; j < columns; j++)
                {
                    PlacePane(i, j, columns, DockPaneAlignment.Right, listTiles);
                }
            }            
        }

        private void PlacePane(int row, int col, int count,
            DockPaneAlignment alignment, IList<List<List<DockableForm>>> listTiles)
        {
            if (row >= listTiles.Count || col >= listTiles[row].Count)
                return;
            int previousIndex = alignment == DockPaneAlignment.Bottom ? row - 1 : col - 1;
            if (previousIndex < 0)
                return;
            if (alignment == DockPaneAlignment.Bottom && col >= listTiles[previousIndex].Count)
                return;
            DockableForm previousForm = alignment == DockPaneAlignment.Bottom
                                            ? listTiles[previousIndex][col][0]
                                            : listTiles[row][previousIndex][0];
            DockPane previousPane = FindPane(previousForm);
            var groupForms = listTiles[row][col];
            var dockableForm = groupForms[0];
            int dim = alignment == DockPaneAlignment.Bottom ? row : col;
            dockableForm.Show(previousPane, alignment,
                              ((double)(count - dim)) / (count - dim + 1));
            ArrangeGraphsTabbed(groupForms);
        }

        private void ArrangeGraphsTabbed(IList<DockableForm> groupForms)
        {
            if (groupForms.Count < 2)
                return;
            DockPane primaryPane = FindPane(groupForms[0]);
            for (int i = 1; i < groupForms.Count; i++)
                groupForms[i].Show(primaryPane, null);
        }

        private List<DockableForm> GetArrangeableGraphs()
        {
            var order = Helpers.ParseEnum(Settings.Default.ArrangeGraphsOrder, GroupGraphsOrder.Position);
            return GetArrangeableGraphs(order, Settings.Default.ArrangeGraphsReversed);
        }

        private List<DockableForm> GetArrangeableGraphs(GroupGraphsOrder order, bool reversed)
        {
            List<DockPane> listPanes = dockPanel.Panes
                .Where(pane => !pane.IsHidden && pane.DockState == DockState.Document)
                .ToList();
            if (order == GroupGraphsOrder.Position)
            {
                listPanes.Sort((p1, p2) =>
                {
                    if (p1.Top != p2.Top)
                        return p1.Top - p2.Top;
                    return p1.Left - p2.Left;
                });
                if (reversed)
                    listPanes.Reverse();
            }

            var listGraphs = new List<DockableForm>();
            foreach (var pane in listPanes)
            {
                IEnumerable<IDockableForm> listForms = pane.Contents;
                if (order == GroupGraphsOrder.Position && reversed)
                    listForms = listForms.Reverse();
                foreach (DockableForm dockableForm in listForms)
                {
                    if (dockableForm.IsHidden || dockableForm.DockState != DockState.Document)
                        continue;
                    listGraphs.Add(dockableForm);
                }                
            }

            if (order != GroupGraphsOrder.Position)
            {
                // Populate a dictionary with the desired document order
                var dictOrder = new Dictionary<DockableForm, int>();
                int iOrder = 0;
                if (_graphSpectrum != null)
                    dictOrder.Add(_graphSpectrum, iOrder++);
                _listGraphRetentionTime.ForEach(g => dictOrder.Add(g, iOrder++));
                _listGraphPeakArea.ForEach(g => dictOrder.Add(g, iOrder++));
                if (DocumentUI.Settings.HasResults)
                {
                    var chromatograms = DocumentUI.Settings.MeasuredResults.Chromatograms.ToList();
                    if (order == GroupGraphsOrder.Acquired_Time)
                    {
                        chromatograms.Sort((c1, c2) =>
                        {
                            var time1 = GetRunStartTime(c1);
                            var time2 = GetRunStartTime(c2);
                            if (!time1.HasValue && !time2.HasValue)
                            {
                                return 0;
                            }
                            else if (!time1.HasValue)
                            {
                                return 1;
                            }
                            else if (!time2.HasValue)
                            {
                                return -1;
                            }
                            return time1.Value.CompareTo(time2.Value);
                        });
                    }
                    foreach (var chromatogramSet in chromatograms)
                    {
                        var graphChrom = GetGraphChrom(chromatogramSet.Name);
                        if (graphChrom != null)
                            dictOrder.Add(graphChrom, iOrder++);
                    }
                }
                // Make sure everything is represented, though it should
                // already be.
                foreach (var graph in listGraphs)
                {
                    if (!dictOrder.TryGetValue(graph, out _))
                        dictOrder.Add(graph, iOrder++);
                }

                // Sort the list of visible document panes by the document order
                // in the dictionary
                listGraphs.Sort((g1, g2) => dictOrder[g1] - dictOrder[g2]);
                if (reversed)
                    listGraphs.Reverse();
            }
            return listGraphs;
        }

        public DateTime? GetRunStartTime(ChromatogramSet chromatogramSet)
        {
            DateTime? runStartTime = null;
            foreach (var fileInfo in chromatogramSet.MSDataFileInfos)
            {
                if (!fileInfo.RunStartTime.HasValue)
                {
                    continue;
                }
                if (!runStartTime.HasValue || runStartTime.Value > fileInfo.RunStartTime.Value)
                {
                    runStartTime = fileInfo.RunStartTime;
                }
            }
            return runStartTime;
        }

        #endregion

        #region Candidate Peaks
        public void ShowCandidatePeaks()
        {
            if (_candidatePeakForm!= null && !Program.SkylineOffscreen)
            {
                _candidatePeakForm.Activate();
            }
            else
            {
                _candidatePeakForm = _candidatePeakForm ?? CreateCandidatePeakForm();
                if (_candidatePeakForm != null)
                {
                    var rectFloat = GetFloatingRectangleForNewWindow();
                    _candidatePeakForm.Show(dockPanel, rectFloat);
                }
            }
        }

        public CandidatePeakForm CreateCandidatePeakForm()
        {
            Assume.IsNull(_candidatePeakForm);
            _candidatePeakForm = new CandidatePeakForm(this);
            _candidatePeakForm.FormClosed += candidatePeakForm_FormClosed;
            return _candidatePeakForm;
        }

        private void DestroyCandidatePeakForm()
        {
            if (null != _candidatePeakForm)
            {
                _candidatePeakForm.FormClosed -= candidatePeakForm_FormClosed;
                _candidatePeakForm.Close();
                _candidatePeakForm = null;
            }
        }

        void candidatePeakForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            _candidatePeakForm = null;
        }
        #endregion
    }
}
