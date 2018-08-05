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
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Deployment.Application;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using DigitalRune.Windows.Docking;
using log4net;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.Util;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Controls.Startup;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Find;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Optimization;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using DataFormats = System.Windows.Forms.DataFormats;
using Timer = System.Windows.Forms.Timer;

namespace pwiz.Skyline
{
    /// <summary>
    /// Main window class for the Skyline application.  Skyline is an SDI application,
    /// but it is intentionally designed around a document window instance without
    /// assuming that it is the only such window in the application to allow it to
    /// become either MDI or multiple-SDI per process.
    /// </summary>
    public partial class SkylineWindow
        : FormEx,
            IUndoable,
            IDocumentUIContainer,
            IProgressMonitor,
            ILibraryBuildNotificationContainer,
            IToolMacroProvider
    {
        private SequenceTreeForm _sequenceTreeForm;
        private ImmediateWindow _immediateWindow;

        private SrmDocument _document;
        private SrmDocument _documentUI;
        private int _savedVersion;
        private bool _closing;
        private readonly UndoManager _undoManager;
        private readonly BackgroundProteomeManager _backgroundProteomeManager;
        private readonly ProteinMetadataManager _proteinMetadataManager;
        private readonly IrtDbManager _irtDbManager;
        private readonly OptimizationDbManager _optDbManager;
        private readonly RetentionTimeManager _retentionTimeManager;
        private readonly IonMobilityLibraryManager _ionMobilityLibraryManager;
        private readonly LibraryManager _libraryManager;
        private readonly LibraryBuildNotificationHandler _libraryBuildNotificationHandler;
        private readonly ChromatogramManager _chromatogramManager;
        private readonly ImportPeptideSearchManager _importPeptideSearchManager;

        public event EventHandler<DocumentChangedEventArgs> DocumentChangedEvent;
        public event EventHandler<DocumentChangedEventArgs> DocumentUIChangedEvent;

        private readonly List<IProgressStatus> _listProgress;
        private readonly TaskbarProgress _taskbarProgress = new TaskbarProgress();
        private readonly Timer _timerProgress;
        private readonly Timer _timerGraphs;
        private readonly List<BackgroundLoader> _backgroundLoaders;
        private readonly object _documentChangeLock = new object();

        /// <summary>
        /// Constructor for the main window of the Skyline program.
        /// </summary>
        public SkylineWindow(string[] args = null)
        {
            InitializeComponent();
            
            _undoManager = new UndoManager(this);
            var undoRedoButtons = new UndoRedoButtons(_undoManager,
                undoMenuItem, undoToolBarButton,
                redoMenuItem, redoToolBarButton,
                RunUIAction);
            undoRedoButtons.AttachEventHandlers();

            _backgroundLoaders = new List<BackgroundLoader>();

            _graphSpectrumSettings = new GraphSpectrumSettings(UpdateSpectrumGraph);

            _listProgress = new List<IProgressStatus>();
            _timerProgress = new Timer { Interval = 100 };
            _timerProgress.Tick += UpdateProgressUI;
            _timerGraphs = new Timer { Interval = 100 };
            _timerGraphs.Tick += UpdateGraphPanes;

            _libraryManager = new LibraryManager();
            _libraryManager.ProgressUpdateEvent += UpdateProgress;
            _libraryManager.Register(this);
            _libraryBuildNotificationHandler = new LibraryBuildNotificationHandler(this);

            _backgroundProteomeManager = new BackgroundProteomeManager();
            _backgroundProteomeManager.ProgressUpdateEvent += UpdateProgress;
            _backgroundProteomeManager.Register(this);
            _chromatogramManager = new ChromatogramManager(false) { SupportAllGraphs = !Program.NoAllChromatogramsGraph };
            _chromatogramManager.ProgressUpdateEvent += UpdateProgress;
            _chromatogramManager.Register(this);
            _irtDbManager = new IrtDbManager();
            _irtDbManager.ProgressUpdateEvent += UpdateProgress;
            _irtDbManager.Register(this);
            _optDbManager = new OptimizationDbManager();
            _optDbManager.ProgressUpdateEvent += UpdateProgress;
            _optDbManager.Register(this);
            _retentionTimeManager = new RetentionTimeManager();
            _retentionTimeManager.ProgressUpdateEvent += UpdateProgress;
            _retentionTimeManager.Register(this);
            _ionMobilityLibraryManager = new IonMobilityLibraryManager();
            _ionMobilityLibraryManager.ProgressUpdateEvent += UpdateProgress;
            _ionMobilityLibraryManager.Register(this);
            _proteinMetadataManager = new ProteinMetadataManager();
            _proteinMetadataManager.ProgressUpdateEvent += UpdateProgress;
            _proteinMetadataManager.Register(this);
            _importPeptideSearchManager = new ImportPeptideSearchManager();
            _importPeptideSearchManager.ProgressUpdateEvent += UpdateProgress;
            _importPeptideSearchManager.Register(this);

            DocumentUIChangedEvent += ShowAutoTrainResults;

            checkForUpdatesMenuItem.Visible =
                checkForUpdatesSeparator.Visible = ApplicationDeployment.IsNetworkDeployed;

            // Begin ToolStore check for updates to currently installed tools, if any
            if (ToolStoreUtil.UpdatableTools(Settings.Default.ToolList).Any())
                ActionUtil.RunAsync(() => ToolStoreUtil.CheckForUpdates(Settings.Default.ToolList.ToArray()), "Check for tool updates");    // Not L10N

            // Get placement values before changing anything.
            bool maximize = Settings.Default.MainWindowMaximized || Program.DemoMode;
            Size size = Settings.Default.MainWindowSize;
            if (!size.IsEmpty)
                Size = size;

            // Restore window placement.
            Point location = Settings.Default.MainWindowLocation;
            if (!location.IsEmpty)
            {
                StartPosition = FormStartPosition.Manual;

                Location = location;
                ForceOnScreen();
            }
            if (maximize)
                WindowState = FormWindowState.Maximized;

            // Restore status bar and graph pane
            statusToolStripMenuItem.Checked = Settings.Default.ShowStatusBar;
            if (!statusToolStripMenuItem.Checked)
                statusToolStripMenuItem_Click(this, new EventArgs());
            toolBarToolStripMenuItem.Checked = Settings.Default.RTPredictorVisible;
            if (!toolBarToolStripMenuItem.Checked)
            {
                toolBarToolStripMenuItem_Click(this, new EventArgs());
            }

            largeToolStripMenuItem.Checked = Settings.Default.TextZoom == TreeViewMS.LRG_TEXT_FACTOR;
            extraLargeToolStripMenuItem.Checked = Settings.Default.TextZoom == TreeViewMS.XLRG_TEXT_FACTOR;
            defaultTextToolStripMenuItem.Checked = 
                !(largeToolStripMenuItem.Checked || extraLargeToolStripMenuItem.Checked);

            ShowSequenceTreeForm(true);

            // Force the handle into existence before any background threads
            // are started by setting the initial document.  Otherwise, calls
            // to InvokeRequired will return false, even on background worker
            // threads.
            if (Equals(Handle, default(IntPtr)))
                throw new InvalidOperationException(Resources.SkylineWindow_SkylineWindow_Must_have_a_window_handle_to_begin_processing);

            // Load any file the user may have double-clicked on to run this application
            if (args == null || args.Length == 0)
            {
                var activationArgs = AppDomain.CurrentDomain.SetupInformation.ActivationArguments;
                args = (activationArgs != null ? activationArgs.ActivationData : null);
            }
            if (args != null && args.Length != 0)
            {
                _fileToOpen = args[args.Length-1];
            }
            NewDocument();
            chorusRequestToolStripMenuItem.Visible = Settings.Default.EnableChorus;
        }

        public AllChromatogramsGraph ImportingResultsWindow { get; private set; }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (_fileToOpen != null)
            {
                try
                {
                    LoadFile(_fileToOpen);
                }
                catch (UriFormatException)
                {
                    MessageBox.Show(this, Resources.SkylineWindow_SkylineWindow_Invalid_file_specified, Program.Name);
                }
                _fileToOpen = null;
            }
        }

        public void OpenPasteFileDlg(PasteFormat pf)
        {
            using (var pasteDlg = new PasteDlg(this)
            {
                SelectedPath = SelectedPath,
                PasteFormat = pf
            })
            {
                if (pasteDlg.ShowDialog(this) == DialogResult.OK)
                    SelectedPath = pasteDlg.SelectedPath;
            }
        }

        public bool LoadFile(string file, FormEx parentWindow = null)
        {
            Uri uri = new Uri(file);
            if (!uri.IsFile)
                throw new UriFormatException(String.Format(Resources.SkylineWindow_SkylineWindow_The_URI__0__is_not_a_file, uri));

            string pathOpen = Uri.UnescapeDataString(uri.AbsolutePath).Replace("/", @"\"); // Not L10N
            
            // If the file chosen was the cache file, open its associated document.)
            if (Equals(Path.GetExtension(pathOpen), ChromatogramCache.EXT))
                pathOpen = Path.ChangeExtension(pathOpen, SrmDocument.EXT);
            // Handle direct open from UNC path names
            if (!string.IsNullOrEmpty(uri.Host))
                pathOpen = "//" + uri.Host + pathOpen; // Not L10N

            if (pathOpen.EndsWith(SrmDocumentSharing.EXT))
            {
                return OpenSharedFile(pathOpen, parentWindow);
            }
            else
            {
                return OpenFile(pathOpen, parentWindow);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            UpgradeManager.CheckForUpdateAsync(this);

            base.OnHandleCreated(e);
        }

        public void Listen(EventHandler<DocumentChangedEventArgs> listener)
        {
            DocumentChangedEvent += listener;
        }

        void IDocumentContainer.Unlisten(EventHandler<DocumentChangedEventArgs> listener)
        {
            DocumentChangedEvent -= listener;
        }

        void IDocumentUIContainer.ListenUI(EventHandler<DocumentChangedEventArgs> listener)
        {
            DocumentUIChangedEvent += listener;
        }

        void IDocumentUIContainer.UnlistenUI(EventHandler<DocumentChangedEventArgs> listener)
        {
            DocumentUIChangedEvent -= listener;
        }

        /// <summary>
        /// The current thread-safe document.
        /// </summary>
        public SrmDocument Document
        {
            get
            {
                return _document;
            }
        }

        /// <summary>
        /// The current document displayed in the UI.  Access only from the UI.
        /// </summary>
        public SrmDocument DocumentUI
        {
            get
            {
                // May only be accessed from the UI thread.
                if (InvokeRequired)
                    throw new InvalidOperationException(Resources.SkylineWindow_DocumentUI_The_DocumentUI_property_may_only_be_accessed_on_the_UI_thread);

                return _documentUI;
            }
        }

        /// <summary>
        /// The currently saved location of the document
        /// </summary>
        public string DocumentFilePath { get; set; }

        public BackgroundProteomeManager BackgroundProteomeManager
        {
            get { return _backgroundProteomeManager; }
        }

        public ProteinMetadataManager ProteinMetadataManager
        {
            get { return _proteinMetadataManager; }
        }

        public IrtDbManager IrtDbManager
        {
            get { return _irtDbManager; }
        }

        public OptimizationDbManager OptDbManager
        {
            get { return _optDbManager; }
        }

        public RetentionTimeManager RetentionTimeManager
        {
            get { return _retentionTimeManager; }
        }

        public IonMobilityLibraryManager IonMobilityLibraryManager
        {
            get { return _ionMobilityLibraryManager; }
        }

        public ImportPeptideSearchManager ImportPeptideSearchManager
        {
            get { return _importPeptideSearchManager; }
        }

        private bool _useKeysOverride;

        public bool UseKeysOverride
        {
            get { return _useKeysOverride; }
            set
            {
                _useKeysOverride = value;
                if (SequenceTree != null)
                    SequenceTree.UseKeysOverride = _useKeysOverride;
            }
        }
        public SequenceTree SequenceTree
        {
            get { return _sequenceTreeForm != null ? _sequenceTreeForm.SequenceTree : null; }
        }

        public ToolStripComboBox ComboResults
        {
            get { return _sequenceTreeForm != null ? _sequenceTreeForm.ComboResults : null; }
        }

        public ImmediateWindow ImmediateWindow
        {
            get { return _immediateWindow; }
        }

        public bool DiscardChanges { get; set; }

        /// <summary>
        /// True if the active document has been modified.
        /// </summary>
        public bool Dirty
        {
            get
            {
                return !DiscardChanges && _documentUI != null && _savedVersion != _documentUI.UserRevisionIndex;
            }
        }

        public bool IsClosing { get { return _closing; } }

        /// <summary>
        /// Tracking active background loaders for a container - helps in test harness SkylineWindow teardown
        /// </summary>
        public IEnumerable<BackgroundLoader> BackgroundLoaders
        {
            get { return _backgroundLoaders; }
        }

        public void AddBackgroundLoader(BackgroundLoader loader)
        {
            _backgroundLoaders.Add(loader);
        }

        public void RemoveBackgroundLoader(BackgroundLoader loader)
        {
            _backgroundLoaders.Remove(loader);
        }

        public bool CopyMenuItemEnabled()
        {
            return copyMenuItem.Enabled;
        }

        public bool PasteMenuItemEnabled()
        {
            return pasteMenuItem.Enabled;
        }

        /// <summary>
        /// Function guaranteed to run on the UI thread that handles
        /// main window UI updates and firing the <see cref="DocumentUIChangedEvent"/>
        /// whenever the <see cref="Document"/> property changes.
        /// </summary>
        private void UpdateDocumentUI()
        {
            // Can only be accessed from the UI thread.
            Debug.Assert(!InvokeRequired);
            SrmDocument documentPrevious = _documentUI;
            _documentUI = Document;

            // The previous document will be null at application start-up.
            if (documentPrevious != null)
            {
                // Clear the UndoManager, if this is a different document.
                if (!ReferenceEquals(_documentUI.Id, documentPrevious.Id))
                    _undoManager.Clear();
			}

            // Call the even handler for this window directly, since it may
            // close other listeners, and it is not possible to remove a listener
            // in the middle of firing an event.
            OnDocumentUIChanged(documentPrevious);
        }

        private void OnDocumentUIChanged(SrmDocument documentPrevious)
        {
            SrmSettings settingsNew = DocumentUI.Settings;
            SrmSettings settingsOld = SrmSettingsList.GetDefault();
            bool docIdChanged = true;
            if (documentPrevious != null)
            {
                settingsOld = documentPrevious.Settings;
                docIdChanged = !ReferenceEquals(DocumentUI.Id, documentPrevious.Id);
            }

            if (null != AlignToFile)
            {
                if (!settingsNew.HasResults || !settingsNew.MeasuredResults.Chromatograms
                    .SelectMany(chromatograms=>chromatograms.MSDataFileInfos)
                    .Any(chromFileInfo=>ReferenceEquals(chromFileInfo.FileId, AlignToFile)))
                {
                    AlignToFile = null;
                }
            }
            // Update results combo UI and sequence tree
            var e = new DocumentChangedEventArgs(documentPrevious,
                _sequenceTreeForm != null && _sequenceTreeForm.IsInUpdateDoc);
            if (_sequenceTreeForm != null)
            {
                // This has to be done before the graph UI updates, since it updates
                // the tree, and the graph UI depends on the tree being up to date.
                _sequenceTreeForm.UpdateResultsUI(settingsNew, settingsOld);
                _sequenceTreeForm.SequenceTree.OnDocumentChanged(this, e);
            }

            // Fire event to allow listeners to update.
            if (DocumentUIChangedEvent != null)
                DocumentUIChangedEvent(this, e);

            // Update graph pane UI
            UpdateGraphUI(settingsOld, docIdChanged);

            // Update title and status bar.
            UpdateTitle();
            UpdateNodeCountStatus();

            integrateAllMenuItem.Checked = settingsNew.TransitionSettings.Integration.IsIntegrateAll;
        }

        public void ShowAutoTrainResults(object sender, DocumentChangedEventArgs e)
        {
            if (!PeptideIntegration.AutoTrainCompleted(DocumentUI, e.DocumentPrevious))
                return;

            var model = DocumentUI.Settings.PeptideSettings.Integration.PeakScoringModel;
            Settings.Default.PeakScoringModelList.Add(model);
            var modelIndex = Settings.Default.PeakScoringModelList.IndexOf(model);
            var newModel = Settings.Default.PeakScoringModelList.EditItem(this, model, Settings.Default.PeakScoringModelList, null);
            if (newModel == null || model.Equals(newModel))
                return;

            Settings.Default.PeakScoringModelList[modelIndex] = newModel;
            SrmDocument docCurrent, docNew;
            do
            {
                docCurrent = DocumentUI;
                docNew = docCurrent.ChangeSettings(docCurrent.Settings.ChangePeptideIntegration(i => i.ChangePeakScoringModel(newModel)));
                var resultsHandler = new MProphetResultsHandler(docNew, newModel);
                using (var longWaitDlg = new LongWaitDlg {Text = Resources.ReintegrateDlg_OkDialog_Reintegrating})
                {
                    try
                    {
                        longWaitDlg.PerformWork(this, 1000, pm =>
                        {
                            resultsHandler.ScoreFeatures(pm);
                            if (resultsHandler.IsMissingScores())
                                throw new InvalidDataException(Resources.ImportPeptideSearchManager_LoadBackground_The_current_peak_scoring_model_is_incompatible_with_one_or_more_peptides_in_the_document_);
                            docNew = resultsHandler.ChangePeaks(pm);
                        });
                        if (longWaitDlg.IsCanceled)
                            return;
                    }
                    catch (Exception x)
                    {
                        var message = TextUtil.LineSeparate(
                            string.Format(Resources.ReintegrateDlg_OkDialog_Failed_attempting_to_reintegrate_peaks_),
                            x.Message);
                        MessageDlg.ShowWithException(this, message, x);
                        return;
                    }
                }
                docNew = docNew.ChangeSettings(Document.Settings.ChangePeptideIntegration(i => i.ChangePeakScoringModel(newModel)));
            } while (!SetDocument(docNew, docCurrent));
        }

        /// <summary>
        /// Thread-safe function for setting the master <see cref="Document"/>
        /// property.  Both the desired new document, and the original document
        /// from which it was must be provided.
        /// 
        /// If the value stored in the <see cref="Document"/> property matches
        /// the original at the time the property set is performed, then it
        /// is changed to the new value, and this function returns true.
        /// 
        /// If it has been set by another thread, since the current thread
        /// started its processing, then this function will return false, and the
        /// caller is required to re-query the <see cref="Document"/> property
        /// and retry its operation on the modified document.
        /// </summary>
        /// <param name="docNew">Modified document to replace current</param>
        /// <param name="docOriginal">Original document from which the new was derived</param>
        /// <returns>True if the change was successful</returns>
        public bool SetDocument(SrmDocument docNew, SrmDocument docOriginal)
        {
            // Not allowed to set the document to null.
            Debug.Assert(docNew != null);
            if (docNew.DeferSettingsChanges)
            {
                throw new InvalidOperationException();
            }
            
            // For debugging tests with unexpected document change failures
            var logChange = LogChange;
            if (logChange != null)
                logChange(docNew, docOriginal);

            SrmDocument docResult;
            lock (_documentChangeLock)
            {
                docResult = Interlocked.CompareExchange(ref _document, docNew, docOriginal);
            }

            if (!ReferenceEquals(docResult, docOriginal))
                return false;

            if (DocumentChangedEvent != null)
                DocumentChangedEvent(this, new DocumentChangedEventArgs(docOriginal));

            RunUIActionAsync(UpdateDocumentUI);

            return true;
        }

        public object GetDocumentChangeLock()
        {
            return _documentChangeLock;
        }

        public Action<SrmDocument, SrmDocument> LogChange { get; set; }

        public void ModifyDocument(string description, Func<SrmDocument, SrmDocument> act)
        {
            ModifyDocument(description, null, act, null, null);
        }

        public void ModifyDocument(string description, Func<SrmDocument, SrmDocument> act, Func<SrmDocument, SrmDocument, AuditLogEntry> logFunc)
        {
            ModifyDocument(description, null, act, null, null, logFunc);
        }

        public static AuditLogEntry SettingsLogFunction(SrmDocument oldDoc, SrmDocument newDoc)
        {
            try
            {
                var tree = Reflector<SrmDocument>.BuildDiffTree(Property.ROOT_PROPERTY, oldDoc, newDoc,
                    DateTime.Now); // Not L10N
                return tree != null && tree.Root != null ? new AuditLogEntry(oldDoc.FormatVersion, tree) : null;
            }
            catch (Exception e)
            {
                Program.ReportException(e);
                return null;
            }
        }

        public void ModifyDocument(string description, IUndoState undoState, Func<SrmDocument, SrmDocument> act, Action onModifying, Action onModified, Func<SrmDocument, SrmDocument, AuditLogEntry> logFunc = null)
        {
            try
            {
                using (var undo = BeginUndo(undoState))
                {
                    AuditLogEntry entry;
                    if (ModifyDocumentInner(act, onModifying, onModified, logFunc, out entry))
                    {
                        undo.Commit(entry != null ? entry.UndoRedo.ToString() : description);
                        AddAuditLogEntry(entry);
                    }   
                }
            }
            catch (IdentityNotFoundException)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_ModifyDocument_Failure_attempting_to_modify_the_document);
            }
            catch (InvalidDataException x)
            {
                MessageDlg.Show(this, TextUtil.LineSeparate(Resources.SkylineWindow_ModifyDocument_Failure_attempting_to_modify_the_document, x.Message)); // Not L10N
            }
            catch (IOException x)
            {
                MessageDlg.Show(this, TextUtil.LineSeparate(Resources.SkylineWindow_ModifyDocument_Failure_attempting_to_modify_the_document, x.Message)); // Not L10N
            }
        }

        private void AddAuditLogEntry(AuditLogEntry entry)
        {
            if (entry != null)
            {
                if (Settings.Default.AuditLogging || entry.CountEntryType == MessageType.log_cleared)
                {
                    ModifyDocumentNoUndo(d => d.ChangeAuditLog(ImmutableList<AuditLogEntry>.ValueOf(Document.AuditLog.AuditLogEntries.Concat(new[] { entry }))));
                }
                else
                    UpdateCountLogEntry(1, entry.AllInfo.Count, MessageType.log_unlogged_changes);
            }  
        }

        private AuditLogEntry UpdateCountLogEntry(int undoRedoCount, int allInfoCount, MessageType type, bool addToDoc = true)
        {
            var logEntries = new List<AuditLogEntry>(Document.AuditLog.AuditLogEntries);
            var countEntry = logEntries.FirstOrDefault(e => e.CountEntryType == type);

            if (countEntry != null)
            {
                var countEntries = logEntries.Where(e =>
                    e.CountEntryType == MessageType.log_cleared ||
                    e.CountEntryType == MessageType.log_unlogged_changes).ToArray();

                undoRedoCount += countEntries.Sum(e => int.Parse(e.UndoRedo.Names[0])) - countEntries.Length;
                allInfoCount += countEntries.Sum(e => int.Parse(e.AllInfo[0].Names[0])) - countEntries.Length;
                logEntries.Remove(countEntry);
            }

            var newCountEntry = AuditLogEntry.MakeCountEntry(type, Document.FormatVersion,
                DateTime.Now, undoRedoCount, allInfoCount);
            if (addToDoc)
            {
                logEntries.Add(newCountEntry);

                ModifyDocumentNoUndo(d => d.ChangeAuditLog(ImmutableList<AuditLogEntry>.ValueOf(logEntries)));
            }

            return newCountEntry;
        }

        public void ModifyDocumentNoUndo(Func<SrmDocument, SrmDocument> act)
        {
            ModifyDocumentInner(act, null, null);
        }

        private void ModifyDocumentInner(Func<SrmDocument, SrmDocument> act, Action onModifying, Action onModified)
        {
            AuditLogEntry unused;
            ModifyDocumentInner(act, onModifying, onModified, null, out unused);
        }

        private bool ModifyDocumentInner(Func<SrmDocument, SrmDocument> act, Action onModifying, Action onModified, Func<SrmDocument, SrmDocument, AuditLogEntry> logFunc, out AuditLogEntry resultEntry)
        {
            SrmDocument docOriginal;
            SrmDocument docNew;
            do
            {
                // Make sure cancel is enabled if it is going to be disabled below
                if (onModifying != null)
                    onModifying();

                docOriginal = Document;
                docNew = act(docOriginal);

                resultEntry = logFunc != null ? logFunc(docOriginal, docNew) : null;

                // If no change has been made, return without committing a
                // new undo record to the undo stack.
                if (ReferenceEquals(docOriginal, docNew))
                    return false;

                // And mark the document as changed by the user.
                docNew = docNew.IncrementUserRevisionIndex();

                // It's now too late to quit - if caller has a cancel button or equivalent it should be disabled now,
                // as leaving it enabled during what could be a long period of updating (including UI, possibly) can
                // lead to confusing behavior.  Possibly there are other reasons to provide a onModified Action, but this 
                // is the original use case.
                if (onModified != null)
                {
                    onModified();
                }
            }
            while (!SetDocument(docNew, docOriginal));

            return true;
        }

        public void SwitchDocument(SrmDocument document, string pathOnDisk)
        {
            // Get rid of any existing import progress UI
            DestroyAllChromatogramsGraph();

            // Some hoops are jumped through here to make sure the
            // document path is correct for listeners on the Document
            // at the time the document change event notifications
            // are fired.

            // CONSIDER: This is not strictly synchronization safe, since
            //           it still leaves open the possibility that a thread
            //           will get the wrong path for the current document.
            //           It may really be necessary to synchronize access
            //           to DocumentFilePath.
            var documentPrevious = Document;
            string pathPrevious = DocumentFilePath;
            DocumentFilePath = pathOnDisk;

            try
            {
                RestoreDocument(document);
            }
            finally
            {
                // If an exception caused setting the document to fail,
                // revert to the previous path.
                if (!ReferenceEquals(Document.Id, document.Id))
                {
                    Assume.IsTrue(ReferenceEquals(Document.Id, documentPrevious.Id));
                    DocumentFilePath = pathPrevious;
                }
                // Otherwise, try to update the UI to show the new active
                // document, no matter whether an exception was thrown or not
                else
                {
                    _savedVersion = document.UserRevisionIndex;

                    SetActiveFile(pathOnDisk);                    
                }
            }
        }

        public IUndoTransaction BeginUndo(IUndoState undoState = null)
        {
            return _undoManager.BeginTransaction(undoState);
        }

        public bool InUndoRedo { get { return _undoManager.InUndoRedo; } }

        /// <summary>
        /// Kills all background processing, and then restores a specific document
        /// as the current document.  After which background processing is restarted
        /// based on the contents of the restored document.
        /// 
        /// This heavy hammer is for use with undo/redo only.
        /// </summary>
        /// <param name="docUndo">The document instance to restore as current</param>
        /// <returns>A reference to the document the user was viewing in the UI at the
        ///          time the undo/redo was executed</returns>
        private SrmDocument RestoreDocument(SrmDocument docUndo)
        {
            // User will want to restore whatever was displayed in the UI at the time.
            SrmDocument docReplaced = DocumentUI;

            bool replaced = SetDocument(docUndo, Document);

            // If no background processing exists, this should succeed.
            if (!replaced)
                throw new InvalidOperationException(Resources.SkylineWindow_RestoreDocument_Failed_to_restore_document);

            return docReplaced;
        }

        #region Implementation of IUndoable

        public IUndoState GetUndoState()
        {
            return new UndoState(this);
        }

        private class UndoState : IUndoState
        {
            private readonly SkylineWindow _window;
            private readonly SrmDocument _document;
            private readonly IdentityPath _treeSelection;
            private readonly IList<IdentityPath> _treeSelections;
            private readonly string _resultName;

            public UndoState(SkylineWindow window)
            {
                _window = window;
                _document = window.DocumentUI;
                _treeSelections = window.SequenceTree.SelectedPaths;
                _treeSelection = window.SequenceTree.SelectedPath;
                _resultName = ResultNameCurrent;
            }

            private UndoState(SkylineWindow window, SrmDocument document, IList<IdentityPath> treeSelections,
                IdentityPath treeSelection, string resultName)
            {
                _window = window;
                _document = document;
                _treeSelections = treeSelections;
                _treeSelection = treeSelection;
                _resultName = resultName;
            }

            private string ResultNameCurrent
            {
                get
                {
                    var selItem = _window.ComboResults.SelectedItem;
                    return (selItem != null ? selItem.ToString() : null);
                }
            }

            public IUndoState Restore()
            {
                // Get current tree selections
                IList<IdentityPath> treeSelections = _window.SequenceTree.SelectedPaths;

                // Get current tree selection
                IdentityPath treeSelection = _window.SequenceTree.SelectedPath;

                // Get results name
                string resultName = ResultNameCurrent;

                // Restore document state
                SrmDocument docReplaced = _window.RestoreDocument(_document);

                // Restore previous tree selection
                _window.SequenceTree.SelectedPath = _treeSelection;

                // Restore previous tree selections
                _window.SequenceTree.SelectedPaths = _treeSelections;

                _window.SequenceTree.Invalidate();

                // Restore selected result
                if (_resultName != null)
                    _window.ComboResults.SelectedItem = _resultName;

                // Return a record that can be used to restore back to the state
                // before this action.
                return new UndoState(_window, docReplaced, treeSelections, treeSelection, resultName);
            }
        }

        #endregion

        private void UpdateTitle()
        {
            string filePath = DocumentFilePath;
            if (string.IsNullOrEmpty(filePath))
                Text = Program.Name;
            else
            {
                string dirtyMark = (Dirty ? " *" : string.Empty); // Not L10N
                Text = string.Format("{0} - {1}{2}", Program.Name, Path.GetFileName(filePath), dirtyMark); // Not L10N
            }
        }

        private void SkylineWindow_Activated(object sender, EventArgs e)
        {
            if (_sequenceTreeForm != null && !_sequenceTreeForm.IsFloating)
                FocusDocument();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);

            if (_sequenceTreeForm != null && !_sequenceTreeForm.IsFloating)
                FocusDocument();
        }

        public void FocusDocument()
        {
            if (SequenceTree != null)
                SequenceTree.Focus();   
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.F3 | Keys.Shift:
                    SequenceTree.UseKeysOverride = true;
                    SequenceTree.KeysOverride = Keys.None;
                    FindNext(true);
                    SequenceTree.UseKeysOverride = false;
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = false;

            if (!CheckSaveDocument())
            {
                e.Cancel = true;
                return;
            }

            if (!Program.NoSaveSettings)
            {
                try
                {
                    Settings.Default.ReloadAndMerge();
                    Settings.Default.SaveException = null;
                    Settings.Default.Save();
                }
                catch (Exception)
                {
                    MessageDlg.Show(this, Resources.SkylineWindow_OnClosing_An_unexpected_error_has_prevented_global_settings_changes_from_this_session_from_being_saved);
                }

                // System.Xml swallows too many exceptions, so we can't catch them in the usual way.
                // Instead we save exceptions thrown at a lower level, then rethrow them here.  These
                // will generate reportable errors so we can see what might be going wrong in the field.
                if (Settings.Default.SaveException != null)
                {
                    e.Cancel = true;
                    Program.NoSaveSettings = true;  // let the user close the window without errors next time
                    var x = Settings.Default.SaveException;
                    throw new TargetInvocationException(x.Message, x);
                }
            }

            _closing = true;

            // Stop listening for progress from background loaders
            _libraryManager.ProgressUpdateEvent -= UpdateProgress;
            _backgroundProteomeManager.ProgressUpdateEvent -= UpdateProgress;
            _chromatogramManager.ProgressUpdateEvent -= UpdateProgress;
            _irtDbManager.ProgressUpdateEvent -= UpdateProgress;
            _optDbManager.ProgressUpdateEvent -= UpdateProgress;
            _retentionTimeManager.ProgressUpdateEvent -= UpdateProgress;
            _ionMobilityLibraryManager.ProgressUpdateEvent -= UpdateProgress;
            _proteinMetadataManager.ProgressUpdateEvent -= UpdateProgress;
            _importPeptideSearchManager.ProgressUpdateEvent -= UpdateProgress;
            
            DestroyAllChromatogramsGraph();

            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _chromatogramManager.Dispose();

            _timerGraphs.Dispose();
            _timerProgress.Dispose();

            DatabaseResources.ReleaseAll(); // Let go of protDB SessionFactories

            foreach (var loader in BackgroundLoaders)
            {
                loader.ClearCache();
            }

            if (!Program.FunctionalTest)
                LogManager.GetLogger(typeof(SkylineWindow)).Info("Skyline closed.\r\n-----------------------"); // Not L10N
            
            base.OnClosed(e);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            
            if (!Program.FunctionalTest)
            {
                // HACK: until the "invalid string binding" error is resolved, this will prevent an error dialog at exit
                Process.GetCurrentProcess().Kill();
            }
        }

        #region File menu

        // See SkylineFiles.cs

        private void exitMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        #endregion // File menu

        #region Edit menu

        public void Undo()
        {
            if (StatementCompletionAction(textBox => textBox.Undo()))
                return;

            _undoManager.Undo();
        }

        public void UndoRestore(int index)
        {
            _undoManager.UndoRestore(index);
        }

        public void Redo()
        {
            if (StatementCompletionAction(textBox => textBox.Undo()))
                return;

            _undoManager.Redo();
        }

        private void sequenceTree_SelectedNodeChanged(object sender, TreeViewEventArgs e)
        {
            sequenceTree_AfterSelect(sender, e);
        }

        private void sequenceTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            // Hide any tool tips when selection changes
            SequenceTree.HideEffects();

            // Update edit menus
            UpdateClipboardMenuItems();
            SrmTreeNode nodeTree = SequenceTree.SelectedNode as SrmTreeNode;
            var enabled = nodeTree != null;
            editNoteToolStripMenuItem.Enabled = enabled;
            manageUniquePeptidesMenuItem.Enabled = UniquePeptidesDlg.PeptideSelection(SequenceTree).Any(); // Only works for peptide molecules, and only if selected
            var nodePepTree = SequenceTree.GetNodeOfType<PeptideTreeNode>();
            modifyPeptideMenuItem.Enabled = nodePepTree != null;
            setStandardTypeMenuItem.Enabled = HasSelectedTargetPeptides();

            // Update active replicate, if using best replicate
            if (nodePepTree != null && SequenceTree.ShowReplicate == ReplicateDisplay.best)
            {
                int iBest = nodePepTree.DocNode.BestResult;
                if (iBest != -1)
                    SelectedResultsIndex = iBest;
            }

            // Update any visible graphs
            UpdateGraphPanes();
            UpdateNodeCountStatus();

            // Notify interested tools.
            if (Program.MainToolService != null)
                Program.MainToolService.SendSelectionChange();
        }

        private bool StatementCompletionAction(Action<TextBox> act)
        {
            var completionEditBox = SequenceTree.StatementCompletionEditBox;
            if (completionEditBox == null)
                return false;

            act(completionEditBox.TextBox);
            return true;
        }

        private void cutMenuItem_Click(object sender, EventArgs e) { Cut(); }
        public void Cut()
        {
            if (StatementCompletionAction(textBox => textBox.Cut()))
                return;

            Copy();
            EditDelete();
        }

        private static int CompareNodeBounds(TreeNode x, TreeNode y)
        {
            return Comparer<int>.Default.Compare(x.Bounds.Top, y.Bounds.Top);
        }

        private int GetLineBreakCount(TreeNodeMS curNode, TreeNodeMS prevNode)
        {
            int count = 0;
            if (prevNode != null)
                count++;
            if (curNode == null || prevNode == null)
                return count;
            TreeNodeMS nodeParent = curNode;
            while (nodeParent != null)
            {
                if (nodeParent == prevNode)
                    return count;
                nodeParent = (TreeNodeMS)nodeParent.Parent;
            }
            TreeNodeMS nodeVisible = curNode;
            while (nodeVisible != prevNode)
            {
                if (!SequenceTree.SelectedNodes.Contains(nodeVisible) && nodeVisible.Level < curNode.Level)
                    return count + 1;
                nodeVisible = (TreeNodeMS)nodeVisible.PrevVisibleNode;
                if (nodeVisible == null)
                    return count;
            }
            return count;
        }

        private void copyMenuItem_Click(object sender, EventArgs e) { Copy(); }
        public void Copy()
        {
            if (StatementCompletionAction(textBox => textBox.Copy()) || SequenceTree.SelectedNodes.Count < 0)
                return;
            
            List<TreeNode> sortedNodes = new List<TreeNode>();
            int shallowestLevel = int.MaxValue;
            foreach (TreeNodeMS node in SequenceTree.SelectedNodes)
            {
                shallowestLevel = Math.Min(shallowestLevel, node.Level);
                sortedNodes.Add(node);
            }
            sortedNodes.Sort(CompareNodeBounds);

            StringBuilder htmlSb = new StringBuilder();
            StringBuilder textSb = new StringBuilder();

            TreeNodeMS prev = null;
            foreach (TreeNodeMS node in sortedNodes)
            {
                IClipboardDataProvider provider = node as IClipboardDataProvider;
                if (provider == null)
                    continue;

                DataObject data = provider.ProvideData();
                int levels = node.Level - shallowestLevel;
                int lineBreaks = GetLineBreakCount(node, prev);
                string providerHtml = (string)data.GetData(DataFormats.Html);
                if (providerHtml != null)
                    AppendClipboardText(htmlSb, new HtmlFragment(providerHtml).Fragment,
                        "<br>\r\n", "&nbsp;&nbsp;&nbsp;&nbsp;", levels, lineBreaks); // Not L10N
                string providerText = (string)data.GetData("Text"); // Not L10N
                if (providerText != null)
                    AppendClipboardText(textSb, providerText, "\r\n", "    ", levels, lineBreaks); // Not L10N

                prev = node;
            }
            DataObject dataObj = new DataObject();
            if (htmlSb.Length > 0)
                dataObj.SetData(DataFormats.Html, HtmlFragment.ClipBoardText(htmlSb.AppendLine().ToString()));
            if (textSb.Length > 0)
                dataObj.SetData(DataFormats.Text, textSb.AppendLine().ToString());

            bool selectionContainsProteins = SequenceTree.SelectedDocNodes.Contains(node =>
                node is PeptideGroupDocNode); 

            var docCopy = DocumentUI.RemoveAllBut(SequenceTree.SelectedDocNodes);
            docCopy = docCopy.ChangeMeasuredResults(null);
            var stringWriter = new XmlStringWriter();
            using (var writer = new XmlTextWriter(stringWriter) { Formatting = Formatting.Indented })
            {
                XmlSerializer ser = new XmlSerializer(typeof(SrmDocument));
                ser.Serialize(writer, docCopy);
            }

            var sbData = new StringBuilder();
            sbData.Append("proteins-selected=").Append(selectionContainsProteins).AppendLine(); // Not L10N
            sbData.AppendLine();
            sbData.Append(stringWriter);
            dataObj.SetData(ClipboardEx.SKYLINE_FORMAT, sbData.ToString());

            try
            {
                ClipboardEx.Clear();
                ClipboardEx.SetDataObject(dataObj);
            }
            catch (ExternalException)
            {
                MessageDlg.Show(this, ClipboardHelper.GetCopyErrorMessage());
            }
        }

        private static void AppendClipboardText(StringBuilder sb, string text, string lineSep, string indent, int levels, int lineBreaks)
        {
            for (int i = 0; i < lineBreaks; i++)
                sb.Append(lineSep);
            for (int i = 0; i < levels; i++)
                sb.Append(indent);
            sb.Append(text);
        }

        private void pasteMenuItem_Click(object sender, EventArgs e) { Paste(); }
        public void Paste()
        {
            if (StatementCompletionAction(textBox => textBox.Paste()))
                return;

            string dataObjectSkyline;

            try
            {
                dataObjectSkyline = (string)ClipboardEx.GetData(ClipboardEx.SKYLINE_FORMAT);
            }
            catch (ExternalException)
            {
                MessageDlg.Show(this, ClipboardHelper.GetPasteErrorMessage());
                return;
            }
        
            if (dataObjectSkyline != null)
            {
                SrmTreeNode nodePaste = SequenceTree.SelectedNode as SrmTreeNode;

                bool pasteToPeptideList  = false;

                if (dataObjectSkyline.Substring(0, dataObjectSkyline.IndexOf('\r')).Equals("proteins-selected=False")) // Not L10N
                {
                    if (nodePaste != null)
                        pasteToPeptideList = !(nodePaste.Path.GetIdentity((int) SrmDocument.Level.MoleculeGroups) is FastaSequence);
                }

                IdentityPath selectPath = null;

                try
                {
                    IdentityPath nextAdd;
                    ModifyDocument(string.Format(Resources.SkylineWindow_Paste_Paste__0__, (pasteToPeptideList ? Resources.SkylineWindow_Paste_peptides : Resources.SkylineWindow_Paste_proteins)), doc => // Not L10N
                        doc.ImportDocumentXml(new StringReader(dataObjectSkyline.Substring(dataObjectSkyline.IndexOf('<'))), // Not L10N
                            null,
                            MeasuredResults.MergeAction.remove,
                            false,
                            FindSpectralLibrary,
                            Settings.Default.StaticModList,
                            Settings.Default.HeavyModList,
                            nodePaste != null ? nodePaste.Path : null,
                            out selectPath,
                            out nextAdd,
                            pasteToPeptideList));
                }
                catch (Exception)
                {
                    MessageDlg.Show(this, Resources.SkylineWindow_Paste_Failed_reading_Skyline_document_from_the_clipboard_);
                    return;
                }

                if (selectPath != null)
                    SequenceTree.SelectedPath = selectPath;
            }
            else
            {
                string text;
                string textCsv;
                try
                {
                    text = ClipboardEx.GetText().Trim();
                    textCsv = ClipboardEx.GetText(TextDataFormat.CommaSeparatedValue);
                }
                catch (Exception)
                {
                    MessageDlg.Show(this, ClipboardHelper.GetPasteErrorMessage());
                    return;
                }
                try
                {
                    Paste(string.IsNullOrEmpty(textCsv) ? text : textCsv);
                }
                catch (Exception x)
                {
                    MessageDlg.ShowException(this, x);
                }
            }
        }

        public void Paste(string text)
        {
            bool peptideList = false;
            Type[] columnTypes;
            IFormatProvider formatProvider;
            char separator;

            // Check for a FASTA header
            if (text.StartsWith(">")) // Not L10N
            {
                // Make sure there is sequence information
                string[] lines = text.Split('\n'); // Not L10N
                int aa = 0;
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (line.StartsWith(">")) // Not L10N
                    {
                        if (i > 0 && aa == 0)
                        {
                            throw new InvalidDataException(
                                string.Format(Resources.SkylineWindow_Paste_Empty_sequence_found_at_line__0__, i + 1));
                        }
                        aa = 0;
                        continue;
                    }

                    foreach (char c in line)
                    {
                        if (AminoAcid.IsExAA(c))
                            aa++;
                        else if (!char.IsWhiteSpace(c) && c != '*') // Not L10N
                        {
                            throw new InvalidDataException(
                                string.Format(Resources.SkylineWindow_Unexpected_character__0__found_on_line__1__, c,
                                              i + 1));
                        }
                    }
                }
            }
            // Perhaps it's a small molecule list with headers
            else if (SmallMoleculeTransitionListCSVReader.IsPlausibleSmallMoleculeTransitionList(text) &&
                     MassListImporter.IsColumnar(text, out formatProvider, out separator, out columnTypes))
            {
                InsertSmallMoleculeTransitionList(text, Resources.SkylineWindow_Paste_Paste_transition_list);
            }
            // If the text contains numbers, see if it can be imported as a mass list.
            // It is definitly not a sequence, if it has numbers.  Whereas, sequences do
            // allow internal white space including tabs.
            else if (MassListImporter.IsColumnar(Transition.StripChargeIndicators(text, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE),
                     out formatProvider, out separator, out columnTypes))
            {
                // If no numeric type is found, try the second line.  Ther first may be
                // a header row.
                if (!MassListImporter.HasNumericColumn(columnTypes))
                {
                    int endLine = text.IndexOf('\n'); // Not L10N
                    if (endLine != -1)
                    {
                        MassListImporter.IsColumnar(text.Substring(endLine + 1),
                            out formatProvider, out separator, out columnTypes);
                    }
                }

                if (MassListImporter.HasNumericColumn(columnTypes))
                    ImportMassList(new MassListInputs(text, formatProvider, separator), Resources.SkylineWindow_Paste_Paste_transition_list, false);
                // Handle unusual corner case where data is found to be columnar and contains numbers, 
                // but first line is missing
                else if (columnTypes.Length == 0)
                {
                    throw new InvalidDataException(Resources.CopyPasteTest_DoTest_Could_not_read_the_pasted_transition_list___Transition_list_must_be_in_separated_columns_and_cannot_contain_blank_lines_);
                }
                else if (columnTypes.Length <= 3 && columnTypes[columnTypes.Length - 1] != typeof(FastaSequence)) // Name, Description, Sequence
                {
                    var message = TextUtil.LineSeparate(Resources.SkylineWindow_Paste_Protein_sequence_not_found,
                                                        Resources.SkylineWindow_Paste_The_protein_sequence_must_be_the_last_value_in_each_line);
                    throw new InvalidDataException(message);
                }
                else
                {
                    string textFasta;
                    try
                    {
                         textFasta = FastaImporter.ToFasta(text, separator);
                    }
                    catch (LineColNumberedIoException x)
                    {                        
                        throw new InvalidDataException(x.Message, x);
                    }
                    ImportFasta(new StringReader(textFasta), Helpers.CountLinesInString(textFasta),
                        false, Resources.SkylineWindow_Paste_Paste_proteins);
                }
                return;
            }
            // Otherwise, look for a list of peptides, or a bare sequence
            else
            {
                // First make sure it looks like a sequence.
                List<double> lineLengths = new List<double>();
                int lineLen = 0;
                var textNoMods = FastaSequence.StripModifications(Transition.StripChargeIndicators(text, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE));
                foreach (char c in textNoMods)
                {
                    if (!AminoAcid.IsExAA(c) && !char.IsWhiteSpace(c) && c != '*' && c != '.') // Not L10N
                    {
                        MessageDlg.Show(this, string.Format(Resources.SkylineWindow_Unexpected_character__0__found_on_line__1__, c, lineLengths.Count + 1));
                        return;
                    }
                    if (c == '\n') // Not L10N
                    {
                        lineLengths.Add(lineLen);
                        lineLen = 0;
                    }
                    else if (!char.IsWhiteSpace(c))
                    {
                        lineLen++;
                    }
                }
                lineLengths.Add(lineLen);

                // Check to see if the pasted text looks like a peptide list.
                PeptideFilter filter = DocumentUI.Settings.PeptideSettings.Filter;
                if (lineLengths.Count == 1 && lineLen < filter.MaxPeptideLength)
                    peptideList = true;
                else
                {
                    Statistics stats = new Statistics(lineLengths);
                    // All lines smaller than the peptide filter
                    if (stats.Max() <= filter.MaxPeptideLength ||
                        // 3 out of 4 are peptide length
                            (lineLengths.Count > 3 && stats.Percentile(0.75) <= filter.MaxPeptideLength))
                        peptideList = true;
                    // Probably a FASTA sequence, but ask if average line length is less than 40
                    else if (stats.Mean() < 40)
                    {
                        using (PasteTypeDlg dlg = new PasteTypeDlg())
                        {
                            if (dlg.ShowDialog(this) == DialogResult.Cancel)
                                return;
                            peptideList = dlg.PeptideList;
                        }
                    }
                }

                if (peptideList)
                {
                    text = FilterPeptideList(text);
                    if (text == null)
                        return; // Canceled
                }
                else if (text.Contains(".")) // Not L10N
                {
                    MessageBox.Show(this, Resources.SkylineWindow_Paste_Unexpected_character_period_found); 
                    return;
                }

                // Choose an unused ID
                string seqId = Document.GetPeptideGroupId(peptideList);

                // Construct valid FASTA format (with >> to indicate custom name)
                text = ">>" + TextUtil.LineSeparate(seqId,text); // Not L10N
            }

            string description = (peptideList ? Resources.SkylineWindow_Paste_Paste_peptide_list : Resources.SkylineWindow_Paste_Paste_FASTA);
            ImportFasta(new StringReader(text), Helpers.CountLinesInString(text),
                peptideList, description);
        }

        private string FilterPeptideList(string text)
        {
            SrmSettings settings = DocumentUI.Settings;
//            Enzyme enzyme = settings.PeptideSettings.Enzyme;

            // Check to see if any of the peptides would be filtered
            // by the current settings.
            string[] pepSequences = text.Split('\n'); // Not L10N
            var setAdded = new HashSet<string>();
            var listAllPeptides = new List<string>();
            var listAcceptPeptides = new List<string>();
            var listFilterPeptides = new List<string>();
            for (int i = 0; i < pepSequences.Length; i++)
            {
                var charge = Transition.GetChargeFromIndicator(pepSequences[i].Trim(), TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE);
                string pepSeqMod = CleanPeptideSequence(Transition.StripChargeIndicators(pepSequences[i], TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE));
                string pepSeqClean = FastaSequence.StripModifications(pepSeqMod);
                if (!charge.IsEmpty)
                    pepSeqMod += Transition.GetChargeIndicator(charge);
                if (string.IsNullOrEmpty(pepSeqMod))
                    continue;
                if (pepSeqClean.Contains(".")) // Not L10N
                {
                    MessageBox.Show(this, string.Format(Resources.SkylineWindow_Unexpected_character__0__found_on_line__1__, ".", i + 1)); // Not L10N
                    return null;
                }

                // Make sure no duplicates are added during a paste
                // With explicit modifications, there is now reason to add duplicates,
                // when multiple modified forms are desired.
                // if (setAdded.Contains(pepSeqClean))
                //    continue;
                setAdded.Add(pepSeqMod);
                listAllPeptides.Add(pepSeqMod);

                if (settings.Accept(pepSeqClean))
                    listAcceptPeptides.Add(pepSeqMod);
                else
                    listFilterPeptides.Add(pepSeqMod);
            }

            // If filtered peptides, ask the user whether to filter or keep.
            if (listFilterPeptides.Count > 0)
            {
                using (var dlg = new PasteFilteredPeptidesDlg { Peptides = listFilterPeptides })
                {
                    switch (dlg.ShowDialog(this))
                    {
                        case DialogResult.Cancel:
                            return null;
                        case DialogResult.Yes:
                            if (listAcceptPeptides.Count == 0)
                                return null;
                            return TextUtil.LineSeparate(listAcceptPeptides);
                    }
                }
            }
            return TextUtil.LineSeparate(listAllPeptides);
        }

        // CONSIDER: Probably should go someplace else
        private static string CleanPeptideSequence(string s)
        {
            s = s.Trim();
            if (s.IndexOfAny(new[] { '\n', '\r', '\t', ' ', '.' }) == -1) // Not L10N
                return s;
            // Internal whitespace
            var sb = new StringBuilder();
            bool inParen = false;
            foreach (char c in s)
            {
                if (c == '[' || c == '{') // Not L10N
                    inParen = true;
                if (c == ']' || c == '}') // Not L10N
                    inParen = false;
                // Preserve spaces inside brackets - modification names can have spaces.
                if (inParen || !char.IsWhiteSpace(c))
                    sb.Append(c);
            }
            // If the peptide is in the format K.PEPTIDER.C, then remove the periods
            // and the preceding and trailing amino acids.
            if (sb.Length > 4 && sb[1] == '.' && sb[sb.Length - 2] == '.') // Not L10N
            {
                sb.Remove(0, 2);
                sb.Remove(sb.Length - 2, 2);
            }
            return sb.ToString();
        }

        private Control _activeClipboardControl;
        private string _fileToOpen;

        public void ClipboardControlGotFocus(Control clipboardControl)
        {
            _activeClipboardControl = clipboardControl;
            UpdateClipboardMenuItems();
        }

        public void ClipboardControlLostFocus(Control clipboardControl)
        {
            if (_activeClipboardControl == clipboardControl)
            {
                _activeClipboardControl = null;
            }
            UpdateClipboardMenuItems();
        }

        private void UpdateClipboardMenuItems()
        {
            if (_activeClipboardControl != null)
            {
                // If some other control wants to handle these commands, then we disable
                // the menu items so the keystrokes don't get eaten up by TranslateMessage
                cutToolBarButton.Enabled = cutMenuItem.Enabled = false;
                copyToolBarButton.Enabled = copyMenuItem.Enabled = false;
                pasteToolBarButton.Enabled = pasteMenuItem.Enabled = false;
                deleteMenuItem.Enabled = false;
                // If it is a grid, then disable next and previous replicate keys in favor of ctrl-Up and ctrl-Down
                // working in the grid
                if (_activeClipboardControl is DataboundGridControl)
                    nextReplicateMenuItem.Enabled = previousReplicateMenuItem.Enabled = false;
                return;
            }

            // Allow deletion, copy/paste for any selection that contains a tree node.
            bool enabled = SequenceTree != null && SequenceTree.SelectedNodes.Any(n => n is SrmTreeNode);
            cutToolBarButton.Enabled = cutMenuItem.Enabled = enabled;
            copyToolBarButton.Enabled = copyMenuItem.Enabled = enabled;
            pasteToolBarButton.Enabled = pasteMenuItem.Enabled = true;
            deleteMenuItem.Enabled = enabled;
            // Always enable these, as they are harmless if enabled with no results and otherwise unmanaged.
            nextReplicateMenuItem.Enabled = previousReplicateMenuItem.Enabled = true;
        }

        private void deleteMenuItem_Click(object sender, EventArgs e) { EditDelete(); }
        public void EditDelete()
        {
            string undoText = Resources.SkylineWindow_EditDelete_items;
            if (SequenceTree.SelectedNodes.Count == 1)
            {
                SrmTreeNode node = SequenceTree.SelectedNode as SrmTreeNode;
                if (node != null)
                    undoText = node.Text;
            }
            ModifyDocument(string.Format(Resources.SkylineWindow_EditDelete_Delete__0__, undoText), doc =>
            {
                var listRemoveParams = new List<RemoveParams>();    // Keep removals in order
                var dictRemove = new Dictionary<int, RemoveParams>();   // Minimize removal operations
                var setRemove = new HashSet<int>(); // Keep track of what is being removed
                foreach (TreeNodeMS nodeTree in SequenceTree.SelectedNodes)
                {
                    var node = nodeTree as SrmTreeNode;
                    if (node == null)
                        continue;

                    int indexRemove = node.Model.Id.GlobalIndex;
                    setRemove.Add(indexRemove);

                    IdentityPath path = node.Path;
                    IdentityPath parentPath = path.Parent;
                    int indexParent = parentPath.IsRoot
                        ? -1
                        : parentPath.GetIdentity(parentPath.Length - 1).GlobalIndex;
                    // If parent is being removed, ignore this element
                    if (setRemove.Contains(indexParent))
                        continue;
                    RemoveParams removeParams;
                    if (!dictRemove.TryGetValue(indexParent, out removeParams))
                    {
                        removeParams = new RemoveParams(parentPath);
                        listRemoveParams.Add(removeParams);
                        dictRemove.Add(indexParent, removeParams);
                    }
                    removeParams.RemoveIds.Add(indexRemove);
                }
                foreach (var removeParams in listRemoveParams)
                {
                    var nodeParent = doc.FindNode(removeParams.ParentPath);
                    if (nodeParent is TransitionGroupDocNode)
                        doc = (SrmDocument) doc.RemoveAllSynched(removeParams.ParentPath.Parent, removeParams.ParentPath.Child, removeParams.RemoveIds);
                    else if (nodeParent != null)
                        doc = (SrmDocument) doc.RemoveAll(removeParams.ParentPath, removeParams.RemoveIds);
                }
                return doc;
            });
        }

        private class RemoveParams
        {
            public RemoveParams(IdentityPath parentPath)
            {
                ParentPath = parentPath;
                RemoveIds = new HashSet<int>();
            }

            public IdentityPath ParentPath { get; private set; }
            public ICollection<int> RemoveIds { get; private set; }
        }

        private void selectAllMenuItem_Click(object sender, EventArgs e) { SelectAll(); }
        public void SelectAll()
        {
            TreeNode node = SequenceTree.Nodes[0];
            SequenceTree.SelectedNode = node;
            while(node.NextVisibleNode != null)
            {
                node = node.NextVisibleNode;
            }
            bool usingKeysOverride = SequenceTree.UseKeysOverride;
            SequenceTree.UseKeysOverride = true;
            SequenceTree.KeysOverride = Keys.Shift;
            SequenceTree.SelectedNode = node;
            SequenceTree.KeysOverride = Keys.None;
            SequenceTree.UseKeysOverride = usingKeysOverride;
        }

        
        private void editNoteMenuItem_Click(object sender, EventArgs e) { EditNote(); }
        public void EditNote()
        {
            IList<IdentityPath> selPaths = SequenceTree.SelectedPaths;
            var selectedSrmTreeNode = SequenceTree.SelectedNode as SrmTreeNode;
            // Should not be possible to get here through the UI without a SrmTreeNode selected,
            // but a stack trace for it seems to have been posted to the unexpected error page
            if (selectedSrmTreeNode == null)
                return;

            using (EditNoteDlg dlg = new EditNoteDlg
            {
                Text = selPaths.Count > 1
                            ? Resources.SkylineWindow_EditNote_Edit_Note
                            : TextUtil.SpaceSeparate(Resources.SkylineWindow_EditNote_Edit_Note, selectedSrmTreeNode.Heading, SequenceTree.SelectedNode.Text)
            })
            {
                dlg.Init(selectedSrmTreeNode.Document, selPaths);

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    bool clearAll = dlg.ClearAll;
                    var resultAnnotations = dlg.GetChangedAnnotations();
                    var resultColorIndex = dlg.ColorIndex;
                    string resultText = dlg.GetText();
                    if (resultColorIndex != -1)
                        Settings.Default.AnnotationColor = dlg.ColorIndex;
                    ModifyDocument(Resources.SkylineWindow_EditNote_Edit_Note, doc => // Not L10N
                                                    {
                                                        foreach (IdentityPath nodePath in selPaths)
                                                        {
                                                            if (Equals(nodePath.Child, SequenceTree.NODE_INSERT_ID))
                                                                continue;
                                                            var nodeInDoc = doc.FindNode(nodePath);
                                                            var newAnnotations = clearAll
                                                                                     ? Annotations.EMPTY
                                                                                     : nodeInDoc.Annotations.
                                                                                           MergeNewAnnotations(
                                                                                               resultText,
                                                                                               resultColorIndex,
                                                                                               resultAnnotations);
                                                            doc = (SrmDocument) doc.ReplaceChild(nodePath.Parent,
                                                                                                 nodeInDoc.
                                                                                                     ChangeAnnotations(
                                                                                                         newAnnotations));
                                                        }
                                                        return doc;
                                                    });
                }
            }
        }

        private void expandProteinsMenuItem_Click(object sender, EventArgs e) { ExpandProteins(); }
        public void ExpandProteins()
        {
            if (Document.MoleculeCount > SequenceTree.MAX_PEPTIDE_EXPANSION)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_ExpandProteins_The_number_of_targets_exceeds_the_limit_for_this_operation_);
                return;
            }
            BulkUpdateTreeNodes<TreeNode>(() =>
            {
                foreach (PeptideGroupTreeNode node in SequenceTree.GetSequenceNodes())
                    node.Expand();
            });
            Settings.Default.SequenceTreeExpandProteins = true;
        }

        private void expandPeptidesMenuItem_Click(object sender, EventArgs e) { ExpandPeptides(); }
        public void ExpandPeptides()
        {
            if (Document.MoleculeCount > SequenceTree.MAX_PEPTIDE_EXPANSION)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_ExpandProteins_The_number_of_targets_exceeds_the_limit_for_this_operation_);
                return;
            }
            BulkUpdateTreeNodes<TreeNode>(() =>
            {
                foreach (PeptideGroupTreeNode node in SequenceTree.GetSequenceNodes())
                {
                    node.Expand();
                    foreach (TreeNode nodeChild in node.Nodes)
                        nodeChild.Expand();
                }
            });
            Settings.Default.SequenceTreeExpandPeptides =
                Settings.Default.SequenceTreeExpandProteins = true;
        }

        private void expandPrecursorsMenuItem_Click(object sender, EventArgs e) { ExpandPrecursors(); }
        public void ExpandPrecursors()
        {
            if (Document.MoleculeTransitionCount > SequenceTree.MAX_TRANSITION_EXPANSTION)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_ExpandProteins_The_number_of_targets_exceeds_the_limit_for_this_operation_);
                return;
            }
            BulkUpdateTreeNodes<TreeNode>(() =>
            {
                foreach (TreeNode node in SequenceTree.Nodes)
                    node.ExpandAll();
            });
            Settings.Default.SequenceTreeExpandPrecursors =
                Settings.Default.SequenceTreeExpandPeptides =
                Settings.Default.SequenceTreeExpandProteins = true;
        }

        private void collapseProteinsMenuItem_Click(object sender, EventArgs e) { CollapseProteins(); }
        public void CollapseProteins()
        {
            BulkUpdateTreeNodes<PeptideGroupTreeNode>(() =>
            {
                foreach (PeptideGroupTreeNode node in SequenceTree.GetSequenceNodes())
                    node.CollapseAndClear();
            });
            Settings.Default.SequenceTreeExpandProteins =
                Settings.Default.SequenceTreeExpandPeptides =
                Settings.Default.SequenceTreeExpandPrecursors = false;
        }

        private void collapsePeptidesMenuItem_Click(object sender, EventArgs e) { CollapsePeptides(); }
        public void CollapsePeptides()
        {
            BulkUpdateTreeNodes<PeptideTreeNode>(() =>
            {
                foreach (PeptideGroupTreeNode node in SequenceTree.GetSequenceNodes())
                    foreach (PeptideTreeNode child in node.Nodes.OfType<PeptideTreeNode>())
                        child.CollapseAndClear();
            });
            Settings.Default.SequenceTreeExpandPeptides =
                Settings.Default.SequenceTreeExpandPrecursors = false;
       }

        private void collapsePrecursorsMenuItem_Click(object sender, EventArgs e) { CollapsePrecursors(); }
        public void CollapsePrecursors()
        {
            BulkUpdateTreeNodes<PeptideTreeNode>(() =>
            {
                foreach (PeptideGroupTreeNode node in SequenceTree.GetSequenceNodes())
                    foreach (PeptideTreeNode child in node.Nodes.OfType<PeptideTreeNode>())
                        foreach (TransitionGroupTreeNode grandChild in child.Nodes.OfType<TransitionGroupTreeNode>())
                            grandChild.CollapseAndClear();
            });
            Settings.Default.SequenceTreeExpandPrecursors = false;
        }

        private void BulkUpdateTreeNodes<TNode>(Action update)
            where TNode : TreeNode
        {
            TreeNode nodeTop = SequenceTree.GetNodeOfType<TNode>(SequenceTree.TopNode) ??
                SequenceTree.TopNode;

            using (SequenceTree.BeginLargeUpdate())
            {
                update();
            }
            if (nodeTop != null)
                SequenceTree.TopNode = nodeTop;
        }

        private void findMenuItem_Click(object sender, EventArgs e)
        {
            var index = OwnedForms.IndexOf(form => form is FindNodeDlg);
            if (index != -1)
                OwnedForms[index].Activate();
            else
                ShowFindNodeDlg();
        }

        private void findNextMenuItem_Click(object sender, EventArgs e)
        {
            FindNext(false);
        }

        public void ShowFindNodeDlg()
        {
            var dlg = new FindNodeDlg
            {
                FindOptions = FindOptions.ReadFromSettings(Settings.Default)
            };
            dlg.Show(this);
        }

        public void FindNext(bool reverse)
        {
            var findOptions = FindOptions.ReadFromSettings(Settings.Default);
            findOptions = findOptions.ChangeForward(!reverse);
            var startPath = SequenceTree.SelectedPath;
            // If the insert node is selected, start from the root.
            if (SequenceTree.IsInsertPath(startPath))
                startPath = IdentityPath.ROOT;
            var displaySettings = SequenceTree.GetDisplaySettings(null);
            var bookmark = new Bookmark(startPath);
            if (_resultsGridForm != null && _resultsGridForm.Visible)
            {
                var liveResultsGrid = _resultsGridForm as LiveResultsGrid;
                if (null != liveResultsGrid)
                {
                    bookmark = bookmark.ChangeChromFileInfoId(liveResultsGrid.GetCurrentChromFileInfoId());
                }
            }            
            var findResult = DocumentUI.SearchDocument(bookmark,
                findOptions, displaySettings);

            if (findResult == null)
            {
                MessageBox.Show(this, findOptions.GetNotFoundMessage());
            }
            else
                DisplayFindResult(null, findResult);
        }

        private IEnumerable<FindResult> FindAll(ILongWaitBroker longWaitBroker, FindPredicate findPredicate)
        {
            return findPredicate.FindAll(longWaitBroker, Document);
        }

        public void FindAll(Control parent, FindOptions findOptions = null)
        {
            if (findOptions == null)
                findOptions = FindOptions.ReadFromSettings(Settings.Default);
            var findPredicate = new FindPredicate(findOptions, SequenceTree.GetDisplaySettings(null));
            IList<FindResult> results = null;
            using (var longWaitDlg = new LongWaitDlg(this))
            {
                longWaitDlg.PerformWork(parent, 2000, lwb => results = FindAll(lwb, findPredicate).ToArray());
                if (results.Count == 0)
                {
                    if (!longWaitDlg.IsCanceled)
                    {
                        MessageBox.Show(parent.TopLevelControl, findOptions.GetNotFoundMessage());
                    }
                    return;
                }
            } 

            ShowFindResults(results);
        }

        public void ShowFindResults(IList<FindResult> findResults)
        {
            // Consider(nicksh): if there is only one match, then perhaps just navigate to it instead
            // of displaying FindResults window
//            if (results.Count() == 1)
//            {
//                DisplayFindResult(results[0]);
//            }
            var findResultsForm = FormUtil.OpenForms.OfType<FindResultsForm>().FirstOrDefault();
            if (findResultsForm == null)
            {
                findResultsForm = new FindResultsForm(this, findResults);
                findResultsForm.Show(dockPanel, DockState.DockBottom);
            }
            else
            {
                findResultsForm.ChangeResults(findResults);
                findResultsForm.Activate();
            }
        }

        public void HideFindResults(bool destroy = false)
        {
            var findResultsForm = FormUtil.OpenForms.OfType<FindResultsForm>().FirstOrDefault();
            if (findResultsForm != null)
            {
                findResultsForm.HideOnClose = !destroy;
                findResultsForm.Close();
            }
        }

        public bool NavigateToBookmark(Bookmark bookmark)
        {
            var bookmarkEnumerator = BookmarkEnumerator.TryGet(DocumentUI, bookmark);
            if (bookmarkEnumerator == null)
            {
                return false;
            }
            SequenceTree.SelectedPath = bookmarkEnumerator.IdentityPath;
            int resultsIndex = bookmarkEnumerator.ResultsIndex;
            if (resultsIndex >= 0)
            {
                ComboResults.SelectedIndex = resultsIndex;
            }
            return true;
        }

        /// <summary>
        /// Navigates the UI to the appropriate spot to display to the user
        /// where text was found.
        /// </summary>
        /// <param name="owner">The control which currently has the focus.  If displaying the find result
        /// requires showing a tooltip, the tooltip will remain displayed as long as owner has focus.
        /// If owner is null, then the SequenceTree will set the focus to itself if a tooltip needs to 
        /// be displayed.
        /// </param>
        /// <param name="findResult">The find result to display</param>
        public void DisplayFindResult(Control owner, FindResult findResult)
        {
            if (findResult.FindMatch == null)
            {
                return;
            }
            if (!NavigateToBookmark(findResult.Bookmark))
            {
                return;
            }
            bool isAnnotationOrNote = findResult.FindMatch.AnnotationName != null || findResult.FindMatch.Note;
            if (isAnnotationOrNote && findResult.Bookmark.ChromFileInfoId != null)
            {
                ShowResultsGrid(true);
                LiveResultsGrid liveResultGrid = _resultsGridForm as LiveResultsGrid;
                if (null != liveResultGrid)
                {
                    liveResultGrid.HighlightFindResult(findResult);
                }
                return;
            }
            SequenceTree.HighlightFindMatch(owner, findResult.FindMatch);
        }

        private void modifyPeptideMenuItem_Click(object sender, EventArgs e)
        {
            var nodeTranGroupTree = SequenceTree.GetNodeOfType<TransitionGroupTreeNode>();
            var nodeTranTree = SequenceTree.GetNodeOfType<TransitionTreeNode>();
            if (nodeTranTree == null && nodeTranGroupTree != null && nodeTranGroupTree.DocNode.TransitionGroup.IsCustomIon)
            {
                ModifySmallMoleculeTransitionGroup();
            }
            else if (nodeTranTree != null && nodeTranTree.DocNode.Transition.IsNonPrecursorNonReporterCustomIon())
            {
                ModifyTransition(nodeTranTree);
            }
            else
            {
                ModifyPeptide();
            }
        }

        public void ModifySmallMoleculeTransitionGroup()
        {
            var nodeTransitionGroupTree = SequenceTree.GetNodeOfType<TransitionGroupTreeNode>();

            if (nodeTransitionGroupTree == null)
                return;

            var nodeTransGroup = nodeTransitionGroupTree.DocNode;
            if (!nodeTransGroup.Peptide.IsCustomMolecule)
                return;

            var existingPrecursors = nodeTransitionGroupTree.PepNode.TransitionGroups.Select(child => child.TransitionGroup).Where(c => c.IsCustomIon).ToArray();
            using (var dlg = new EditCustomMoleculeDlg(this, 
                    EditCustomMoleculeDlg.UsageMode.precursor,
                    Resources.SkylineWindow_ModifySmallMoleculeTransitionGroup_Modify_Custom_Ion_Precursor, 
                    nodeTransGroup.TransitionGroup, // Self
                    existingPrecursors, // Self and siblings
                    TransitionGroup.MIN_PRECURSOR_CHARGE,
                    TransitionGroup.MAX_PRECURSOR_CHARGE,
                    Document.Settings,
                    nodeTransGroup.TransitionGroup.CustomMolecule,
                    nodeTransGroup.TransitionGroup.PrecursorAdduct,
                    nodeTransGroup.ExplicitValues,
                    null,
                    nodeTransGroup.TransitionGroup.LabelType))
            {
                dlg.SetResult(nodeTransGroup.CustomMolecule, nodeTransGroup.PrecursorAdduct); // Set initial value change check
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Assume.IsTrue(Equals(dlg.ResultCustomMolecule, nodeTransGroup.Peptide.CustomMolecule)); // Not expecting any change to neutral molecule
                    var newTransGroup = new TransitionGroup(nodeTransGroup.Peptide, dlg.Adduct, dlg.IsotopeLabelType);
                    ModifyDocument(string.Format(Resources.SkylineWindow_ModifyPeptide_Modify__0__,
                            nodeTransitionGroupTree.Text),
                        doc =>
                        {
                            if (!Equals(newTransGroup, nodeTransGroup.Id))
                            {
                                // ID Changes impact all children, because IDs have back pointers to their parents.
                                // User altered some identity item so we have to insert a copy of the tree from here down, and delete  
                                var newNode = nodeTransGroup.UpdateSmallMoleculeTransitionGroup(nodeTransGroup.Peptide, newTransGroup, Document.Settings);
                                var newdoc = (SrmDocument) doc.Insert(nodeTransitionGroupTree.Path, newNode);
                                return (SrmDocument) newdoc.RemoveChild(nodeTransitionGroupTree.Path.Parent, nodeTransGroup);
                            }
                            else
                            {
                                var newNode =
                                    new TransitionGroupDocNode(nodeTransGroup.TransitionGroup,
                                        nodeTransGroup.Annotations, Document.Settings, null, null,
                                        dlg.ResultExplicitTransitionGroupValues, nodeTransGroup.Results,
                                        nodeTransGroup.Children.Cast<TransitionDocNode>().ToArray(),
                                        nodeTransGroup.AutoManageChildren);
                                return (SrmDocument) doc.ReplaceChild(nodeTransitionGroupTree.Path.Parent, newNode);
                            }
                        });
                }
            }
        }

        public void ModifyPeptide()
        {
            var nodePepTree = SequenceTree.GetNodeOfType<PeptideTreeNode>();
            if (nodePepTree != null)
            {
                var nodePep = nodePepTree.DocNode;
                if (nodePep.Peptide.IsCustomMolecule)
                {
                    // End up here when modifying the small molecule equivalent of a peptide
                    // We want to be able to adjust explicit retention time in particular
                    using (
                        var dlg = new EditCustomMoleculeDlg(this, Resources.SkylineWindow_ModifyPeptide_Modify_Small_Molecule,
                            Document.Settings,
                            nodePep.Peptide.CustomMolecule,
                            nodePep.ExplicitRetentionTime ?? ExplicitRetentionTimeInfo.EMPTY))
                    {
                        dlg.SetResult(nodePep.Peptide.CustomMolecule, Adduct.EMPTY);
                        if (dlg.ShowDialog(this) == DialogResult.OK)
                        {
                            ModifyDocument(
                                string.Format(Resources.SkylineWindow_ModifyPeptide_Modify__0__, nodePepTree.Text),
                                doc =>
                                {
                                    // If we didn't change the custom molecule's formula and/or mass, the ID should be unchanged and we can just change any needed transition info.
                                    // But if custom molecule has changed then we can't "Replace", since newNode has a different identity and isn't in the document.  We have to 
                                    // insert and delete instead.  
                                    var newNode = nodePep.ChangeCustomIonValues(doc.Settings, dlg.ResultCustomMolecule, dlg.ExplicitRetentionTimeInfo);
                                    if (!nodePep.Peptide.Equals(newNode.Peptide)) // Did that change the Id object?
                                    {
                                        // We don't want the tree selection to change, so note this as a replacement.
                                        var newdoc = (SrmDocument)doc.Insert(nodePepTree.Path, newNode.ChangeReplacedId(nodePep.Id));
                                        return (SrmDocument)newdoc.RemoveChild(nodePepTree.Path.Parent, nodePep);
                                    }
                                    else
                                    {
                                        return (SrmDocument)doc.ReplaceChild(nodePepTree.Path.Parent, newNode);
                                    }
                                });
                        }
                    }
                }
                else
                {
                    using (var dlg = new EditPepModsDlg(DocumentUI.Settings, nodePep))
                    {
                        dlg.Height = Math.Min(dlg.Height, Screen.FromControl(this).WorkingArea.Height);
                        if (dlg.ShowDialog(this) == DialogResult.OK)
                        {
                            var listStaticMods = Settings.Default.StaticModList;
                            var listHeavyMods = Settings.Default.HeavyModList;
                            ModifyDocument(
                                string.Format(Resources.SkylineWindow_ModifyPeptide_Modify__0__, nodePepTree.Text),
                                doc =>
                                    doc.ChangePeptideMods(nodePepTree.Path,
                                        dlg.ExplicitMods,
                                        dlg.IsCreateCopy,
                                        listStaticMods,
                                        listHeavyMods));
                        }
                    }
                }
            }
        }

        public void ModifyTransition(TransitionTreeNode nodeTranTree)
        {
            var nodeTran = nodeTranTree.DocNode;
            if (nodeTran.Transition.IsNonPrecursorNonReporterCustomIon())
            {
                var nodeGroupTree = SequenceTree.GetNodeOfType<TransitionGroupTreeNode>();
                // Existing transitions to avoid duplication
                var existingMolecules = nodeGroupTree.DocNode.Transitions
                        .Select(nodeT => nodeT.Transition)
                        .Where(t => t.IsNonReporterCustomIon()).ToArray();

                using (var dlg = new EditCustomMoleculeDlg(this,
                    EditCustomMoleculeDlg.UsageMode.fragment,
                    Resources.SkylineWindow_ModifyTransition_Modify_Transition, 
                    nodeTran.Transition, existingMolecules, // Self and siblings
                    Transition.MIN_PRODUCT_CHARGE,
                    Transition.MAX_PRODUCT_CHARGE,
                    Document.Settings,
                    nodeTran.Transition.CustomIon,
                    nodeTran.Transition.Adduct, null, null, null))
                {
                    dlg.SetResult(nodeTran.Transition.CustomIon, nodeTran.Transition.Adduct);
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        ModifyDocument(string.Format(Resources.SkylineWindow_ModifyTransition_Modify_product_ion__0_,
                                nodeTranTree.Text),
                            doc =>
                            {
                                Assume.IsTrue(ReferenceEquals(nodeGroupTree.DocNode.TransitionGroup, nodeTran.Transition.Group));
                                var mass = dlg.ResultCustomMolecule.GetMass(
                                        doc.Settings.TransitionSettings.Prediction.FragmentMassType);
                                var transition = new Transition(nodeTran.Transition.Group, dlg.Adduct, null,
                                    dlg.ResultCustomMolecule);
                                var newNode = new TransitionDocNode(transition,
                                        nodeTran.Annotations,
                                        nodeTran.Losses,
                                        mass,
                                        TransitionDocNode.TransitionQuantInfo.DEFAULT, 
                                        null);
                                // Note we can't just Replace the node, as it has a new Id that's not in the doc.
                                // But neither do we want the tree selection to change, so note this as a replacement.
                                var newDoc = doc.Insert(nodeTranTree.Path, newNode.ChangeReplacedId(nodeTran.Id));
                                return (SrmDocument)newDoc.RemoveChild(nodeTranTree.Path.Parent, nodeTran);
                            });
                    }
                }
            }
        }

        private void noStandardMenuItem_Click(object sender, EventArgs e)
        {
            SetStandardType(null);
        }

        private void qcStandardMenuItem_Click(object sender, EventArgs e)
        {
            SetStandardType(PeptideDocNode.STANDARD_TYPE_QC);
        }

        private void normStandardMenuItem_Click(object sender, EventArgs e)
        {
            SetStandardType(StandardType.GLOBAL_STANDARD);
        }

        private void surrogateStandardMenuItem_Click(object sender, EventArgs e)
        {
            SetStandardType(StandardType.SURROGATE_STANDARD);
        }

        private void irtStandardContextMenuItem_Click(object sender, EventArgs e)
        {
            MessageDlg.Show(this, TextUtil.LineSeparate(Resources.SkylineWindow_irtStandardContextMenuItem_Click_The_standard_peptides_for_an_iRT_calculator_can_only_be_set_in_the_iRT_calculator_editor_,
                Resources.SkylineWindow_irtStandardContextMenuItem_Click_In_the_Peptide_Settings___Prediction_tab__click_the_calculator_button_to_edit_the_current_iRT_calculator_));
        }


        private void setStandardTypeContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            UpdateStandardTypeMenu();
        }

        private void setStandardTypeMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            UpdateStandardTypeMenu();
        }

        private void UpdateStandardTypeMenu()
        {
            var selectedPeptides = SequenceTree.SelectedDocNodes
                .OfType<PeptideDocNode>().ToArray();
            var selectedStandardTypes = selectedPeptides.Select(peptide => peptide.GlobalStandardType)
                .Distinct().ToArray();
            foreach (var menuItemStandardType in GetStandardTypeMenuItems())
            {
                var toolStripMenuItem = menuItemStandardType.Key;
                var standardType = menuItemStandardType.Value;
                if (standardType == StandardType.IRT)
                {
                    // Only show iRT menu item when there is an iRT calculator
                    var rtRegression = Document.Settings.PeptideSettings.Prediction.RetentionTime;
                    toolStripMenuItem.Visible = rtRegression == null || !(rtRegression.Calculator is RCalcIrt);
                    toolStripMenuItem.Enabled = selectedStandardTypes.Contains(StandardType.IRT);
                }
                else
                {
                    toolStripMenuItem.Enabled = selectedPeptides.Length >= 1 &&
                                                !selectedStandardTypes.Contains(StandardType.IRT);
                }
                toolStripMenuItem.Checked = selectedStandardTypes.Length == 1 &&
                                            selectedStandardTypes[0] == standardType;
            }
        }

        public void SetStandardType(StandardType standardType)
        {
            IEnumerable<IdentityPath> selPaths = SequenceTree.SelectedPaths
                .Where(idPath => !ReferenceEquals(idPath.Child, SequenceTree.NODE_INSERT_ID));
            string message = standardType == null
                ? Resources.SkylineWindow_SetStandardType_Clear_standard_type
                : string.Format(Resources.SkylineWindow_SetStandardType_Set_standard_type_to__0_, standardType);

            ModifyDocument(message, doc => doc.ChangeStandardType(standardType, selPaths));
        }

        public IDictionary<ToolStripMenuItem, StandardType> GetStandardTypeMenuItems()
        {
            return new Dictionary<ToolStripMenuItem, StandardType>
            {
                {noStandardMenuItem, null},
                {noStandardContextMenuItem, null},
                {normStandardMenuItem, StandardType.GLOBAL_STANDARD},
                {normStandardContextMenuItem, StandardType.GLOBAL_STANDARD},
                {surrogateStandardMenuItem, StandardType.SURROGATE_STANDARD},
                {surrogateStandardContextMenuItem, StandardType.SURROGATE_STANDARD},
                {qcStandardMenuItem, StandardType.QC},
                {qcStandardContextMenuItem, StandardType.QC},
                {irtStandardMenuItem, StandardType.IRT},
                {irtStandardContextMenuItem, StandardType.IRT},
            };
        }

        private bool HasSelectedTargetPeptides()
        {
            return SequenceTree.SelectedDocNodes.Any(nodeSel =>
                {
                    var nodePep = nodeSel as PeptideDocNode;
                    return nodePep != null && !nodePep.IsDecoy;
                });
        }

        private void manageUniquePeptidesMenuItem_Click(object sender, EventArgs e)
        {
            ShowUniquePeptidesDlg();
        }

        public void ShowUniquePeptidesDlg()
        {
            if (DocumentUI.Settings.PeptideSettings.BackgroundProteome.IsNone)
            {
                MessageDlg.Show(this,
                                TextUtil.LineSeparate(Resources.SkylineWindow_ShowUniquePeptidesDlg_Inspecting_peptide_uniqueness_requires_a_background_proteome,
                                                      Resources.SkylineWindow_ShowUniquePeptidesDlg_Choose_a_background_proteome_in_the_Digestions_tab_of_the_Peptide_Settings));
                return;
            }

            // Need at least one node selected in tree
            var peptideGroupTreeNodes = UniquePeptidesDlg.PeptideSelection(SequenceTree);
            if (!peptideGroupTreeNodes.Any())
            {
                return;  // No selection
            }
            if (!ViewLibraryDlg.EnsureDigested(this, DocumentUI.Settings.PeptideSettings.BackgroundProteome))
            {
                return;
            }
            using (UniquePeptidesDlg uniquePeptidesDlg = new UniquePeptidesDlg(this)
            {
                PeptideGroupTreeNodes = peptideGroupTreeNodes
            })
            {
                uniquePeptidesDlg.ShowDialog(this);
            }
        }

        private void insertFASTAToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowPasteFastaDlg();
        }

        public void ShowPasteFastaDlg()  // Expose for test access
        {
            using (var pasteDlg = new PasteDlg(this)
            {
                SelectedPath = SelectedPath,
                PasteFormat = PasteFormat.fasta
            })
            {
                if (pasteDlg.ShowDialog(this) == DialogResult.OK)
                    SelectedPath = pasteDlg.SelectedPath;
            }
        }

        private void insertPeptidesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowPastePeptidesDlg();
        }

       public void ShowPastePeptidesDlg()
       {
           using (var pasteDlg = new PasteDlg(this)
           {
               SelectedPath = SelectedPath,
               PasteFormat = PasteFormat.peptide_list
           })
           {
               if (pasteDlg.ShowDialog(this) == DialogResult.OK)
                   SelectedPath = pasteDlg.SelectedPath;
           }
       }

        private void insertProteinsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowPasteProteinsDlg();
        }

        public void ShowPasteProteinsDlg()
        {
            using (var pasteDlg = new PasteDlg(this)
            {
                SelectedPath = SelectedPath,
                PasteFormat = PasteFormat.protein_list
            })
            {
                if (pasteDlg.ShowDialog(this) == DialogResult.OK)
                    SelectedPath = pasteDlg.SelectedPath;
            }
        }

        private void insertTransitionListMenuItem_Click(object sender, EventArgs e)
        {
            ShowPasteTransitionListDlg();
        }
       
        public void ShowPasteTransitionListDlg()
        {
            using (var pasteDlg = new PasteDlg(this)
            {
                SelectedPath = SelectedPath,
                PasteFormat = PasteFormat.transition_list
            })
            {
                if (pasteDlg.ShowDialog(this) == DialogResult.OK)
                    SelectedPath = pasteDlg.SelectedPath;
            }
        }

        private void refineMenuItem_Click(object sender, EventArgs e)
        {
            ShowRefineDlg();
        }

        public void ShowRefineDlg()
        {
            using (var refineDlg = new RefineDlg(DocumentUI))
            {
                if (refineDlg.ShowDialog(this) == DialogResult.OK)
                {
                    ModifyDocument(Resources.SkylineWindow_ShowRefineDlg_Refine, doc => refineDlg.RefinementSettings.Refine(doc));
                }
            }
        }

        private void removeEmptyProteinsMenuItem_Click(object sender, EventArgs e)
        {
            var refinementSettings = new RefinementSettings { MinPeptidesPerProtein = 1 };
            ModifyDocument(Resources.SkylineWindow_removeEmptyProteinsMenuItem_Click_Remove_empty_proteins, refinementSettings.Refine); 
        }

        private void removeEmptyPeptidesMenuItem_Click(object sender, EventArgs e)
        {
            var refinementSettings = new RefinementSettings { MinPrecursorsPerPeptide = 1 };
            ModifyDocument(Resources.SkylineWindow_removeEmptyPeptidesMenuItem_Click_Remove_empty_peptides, refinementSettings.Refine);
        }

        private void removeDuplicatePeptidesMenuItem_Click(object sender, EventArgs e)
        {
            var refinementSettings = new RefinementSettings { RemoveDuplicatePeptides = true };
            ModifyDocument(Resources.SkylineWindow_removeDuplicatePeptidesMenuItem_Click_Remove_duplicate_peptides, refinementSettings.Refine);
        }

        private void removeRepeatedPeptidesMenuItem_Click(object sender, EventArgs e)
        {
            var refinementSettings = new RefinementSettings { RemoveRepeatedPeptides = true };
            ModifyDocument(Resources.SkylineWindow_removeRepeatedPeptidesMenuItem_Click_Remove_repeated_peptides, refinementSettings.Refine);
        }

        private void renameProteinsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowRenameProteinsDlg();
        }

        public void ShowRenameProteinsDlg()
        {
            using (RenameProteinsDlg dlg = new RenameProteinsDlg(DocumentUI))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    ModifyDocument(Resources.SkylineWindow_ShowRenameProteinsDlg_Rename_proteins, doc => RenameProtein(doc, dlg));
                }
            }
        }

        private SrmDocument RenameProtein(SrmDocument doc, RenameProteinsDlg dlg)
        {
            foreach (var name in dlg.DictNameToName.Keys)
            {
                PeptideGroupDocNode node = Document.MoleculeGroups.FirstOrDefault(peptideGroup => Equals(name, peptideGroup.Name));
                if (node != null)
                    doc = (SrmDocument) doc.ReplaceChild(node.ChangeName(dlg.DictNameToName[name]));
            }
            return doc;
        }

        private void acceptPeptidesMenuItem_Click(object sender, EventArgs e)
        {
            AcceptPeptides();
        }

        public void AcceptPeptides()
        {
            using (var dlg = new RefineListDlg(DocumentUI))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    var refinementSettings = new RefinementSettings
                    {
                        AcceptedPeptides = dlg.AcceptedPeptides,
                        AcceptModified = dlg.MatchModified
                    };
                    if (dlg.RemoveEmptyProteins)
                        refinementSettings.MinPeptidesPerProtein = 1;

                    ModifyDocument(Resources.SkylineWindow_acceptPeptidesMenuItem_Click_Accept_peptides, refinementSettings.Refine);
                }
            }
        }

        private void acceptProteinsMenuItem_Click(object sender, EventArgs e)
        {
            AcceptProteins();
        }

        public void AcceptProteins()
        {
            using (var dlg = new RefineProteinListDlg(DocumentUI))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    var refinementSettings = new RefinementSettings
                    {
                        AcceptedProteins = dlg.AcceptedProteins,
                        AcceptProteinType = dlg.ProteinSpecType
                    };

                    ModifyDocument(Resources.SkylineWindow_acceptPeptidesMenuItem_Click_Accept_peptides, refinementSettings.Refine);
                }
            }
        }

        private void removeMissingResultsMenuItem_Click(object sender, EventArgs e)
        {
            RemoveMissingResults();
        }

        public void RemoveMissingResults()
        {
            var refinementSettings = new RefinementSettings { RemoveMissingResults = true };
            ModifyDocument(Resources.SkylineWindow_RemoveMissingResults_Remove_missing_results, refinementSettings.Refine);
        }

        private void generateDecoysMenuItem_Click(object sender, EventArgs e)
        {
            if (DocumentUI.PeptideCount == 0)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_generateDecoysMenuItem_Click_The_document_must_contain_peptides_to_generate_decoys_);
                return;
            }
            if (DocumentUI.PeptideGroups.Any(nodePeptideGroup => nodePeptideGroup.IsDecoy))
            {
                var message = TextUtil.LineSeparate(Resources.SkylineWindow_generateDecoysMenuItem_Click_This_operation_will_replace_the_existing_decoys,
                                                    Resources.SkylineWindow_generateDecoysMenuItem_Click_Are_you_sure_you_want_to_continue);
                // Warn about removing existing decoys
                var result = MessageBox.Show(this, message, Program.Name, MessageBoxButtons.OKCancel);
                if (result == DialogResult.Cancel)
                    return;
            }

            ShowGenerateDecoysDlg();
        }

        public void ShowGenerateDecoysDlg()
        {
            using (var decoysDlg = new GenerateDecoysDlg(DocumentUI))
            {
                if (decoysDlg.ShowDialog(this) == DialogResult.OK)
                {
                    var refinementSettings = new RefinementSettings { NumberOfDecoys = decoysDlg.NumDecoys, DecoysMethod = decoysDlg.DecoysMethod };
                    ModifyDocument(Resources.SkylineWindow_ShowGenerateDecoysDlg_Generate_Decoys, refinementSettings.GenerateDecoys);

                    var nodePepGroup = DocumentUI.PeptideGroups.First(nodePeptideGroup => nodePeptideGroup.IsDecoy);
                    SelectedPath = DocumentUI.GetPathTo((int)SrmDocument.Level.MoleculeGroups, DocumentUI.FindNodeIndex(nodePepGroup.Id));
                }
            }
        }

        #endregion // Edit menu

        #region Context menu

        private void contextMenuTreeNode_Opening(object sender, CancelEventArgs e)
        {
            var treeNode = SequenceTree.SelectedNode as TreeNodeMS;
            bool enabled = (SequenceTree.SelectedNode is IClipboardDataProvider && treeNode != null
                && treeNode.IsInSelection);
            copyContextMenuItem.Enabled = enabled;
            cutContextMenuItem.Enabled = enabled;
            deleteContextMenuItem.Enabled = enabled;
            pickChildrenContextMenuItem.Enabled = SequenceTree.CanPickChildren(SequenceTree.SelectedNode) && enabled;
            editNoteContextMenuItem.Enabled = (SequenceTree.SelectedNode is SrmTreeNode && enabled);
            removePeakContextMenuItem.Visible = (SequenceTree.SelectedNode is TransitionTreeNode && enabled);
            bool enabledModify = SequenceTree.GetNodeOfType<PeptideTreeNode>() != null;
            var transitionTreeNode = SequenceTree.SelectedNode as TransitionTreeNode;
            if (transitionTreeNode != null && transitionTreeNode.DocNode.Transition.IsPrecursor() && transitionTreeNode.DocNode.Transition.IsCustom())
                enabledModify = false; // Don't offer to modify generated custom precursor nodes
            modifyPeptideContextMenuItem.Visible = enabledModify && enabled;
            setStandardTypeContextMenuItem.Visible = (HasSelectedTargetPeptides() && enabled);
            // Custom molecule support
            var nodePepGroupTree = SequenceTree.SelectedNode as PeptideGroupTreeNode;
            var nodePepTree = SequenceTree.SelectedNode as PeptideTreeNode;
            addMoleculeContextMenuItem.Visible = enabled && nodePepGroupTree != null && 
                (nodePepGroupTree.DocNode.IsEmpty || nodePepGroupTree.DocNode.IsNonProteomic);
            addSmallMoleculePrecursorContextMenuItem.Visible = enabledModify && nodePepTree != null && !nodePepTree.DocNode.IsProteomic;
            var nodeTranGroupTree = SequenceTree.SelectedNode as TransitionGroupTreeNode;
            addTransitionMoleculeContextMenuItem.Visible = enabled && nodeTranGroupTree != null &&
                nodeTranGroupTree.PepNode.Peptide.IsCustomMolecule;
        }

        private void pickChildrenContextMenuItem_Click(object sender, EventArgs e) { ShowPickChildrenInternal(true); }

        public void ShowPickChildrenInternal(bool okOnDeactivate)
        {
            SequenceTree.ShowPickList(okOnDeactivate);
        }

        /// <summary>
        /// Shows pop-up pick list for tests, with no automatic OK on deactivation of the pick list,
        /// since this can cause failures, if the test computer is in use during the tests.
        /// </summary>
        public void ShowPickChildrenInTest()
        {
            ShowPickChildrenInternal(false);
        }

        private void singleReplicateTreeContextMenuItem_Click(object sender, EventArgs e)
        {
            SequenceTree.ShowReplicate = ReplicateDisplay.single;
        }

        private void bestReplicateTreeContextMenuItem_Click(object sender, EventArgs e)
        {
            SequenceTree.ShowReplicate = ReplicateDisplay.best;

            // Make sure the best result index is active for the current peptide.
            var nodePepTree = SequenceTree.GetNodeOfType<PeptideTreeNode>();
            if (nodePepTree != null)
            {
                int iBest = nodePepTree.DocNode.BestResult;
                if (iBest != -1)
                    SelectedResultsIndex = iBest;
            }
        }

        private void replicatesTreeContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            ReplicateDisplay replicate = SequenceTree.ShowReplicate;
            singleReplicateTreeContextMenuItem.Checked = (replicate == ReplicateDisplay.single);
            bestReplicateTreeContextMenuItem.Checked = (replicate == ReplicateDisplay.best);
        }

        #endregion

        #region View menu

        // See SkylineGraphs.cs

        private void spectralLibrariesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ViewSpectralLibraries();
        }

        public void ViewSpectralLibraries()
        {
            if (Settings.Default.SpectralLibraryList.Count == 0)
            {
                var result = MessageBox.Show(this,
                                             Resources.
                                                 SkylineWindow_ViewSpectralLibraries_No_libraries_to_show_Would_you_like_to_add_a_library,
                                             Program.Name, MessageBoxButtons.OKCancel);
                if (result == DialogResult.Cancel)
                    return;
                ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Library);
            }
            else
            {
                var index = OwnedForms.IndexOf(form => form is ViewLibraryDlg);
                if (index != -1)
                    OwnedForms[index].Activate();
                else
                {
                    var libraries = Document.Settings.PeptideSettings.Libraries;
                    string libraryName = string.Empty;
                    for (int i = 0; i < libraries.Libraries.Count && string.IsNullOrEmpty(libraryName); i++)
                    {
                        if (libraries.Libraries[i] != null)
                            libraryName = libraries.Libraries[i].Name;
                        else if (libraries.LibrarySpecs[i] != null)
                            libraryName = libraries.LibrarySpecs[i].Name;
                    }
                    OpenLibraryExplorer(libraryName);
                }
            }
        }

        private void defaultToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeTextSize(TreeViewMS.DEFAULT_TEXT_FACTOR);
        }

        private void largeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeTextSize(TreeViewMS.LRG_TEXT_FACTOR);
        }

        private void extraLargeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeTextSize(TreeViewMS.XLRG_TEXT_FACTOR);
        }

        public double TargetsTextFactor
        {
            get { return Settings.Default.TextZoom; }
            set
            {
                Settings.Default.TextZoom = value;
                largeToolStripMenuItem.Checked = (value == TreeViewMS.LRG_TEXT_FACTOR);
                extraLargeToolStripMenuItem.Checked = (value == TreeViewMS.XLRG_TEXT_FACTOR);
                defaultTextToolStripMenuItem.Checked = (!largeToolStripMenuItem.Checked &&
                                                        !extraLargeToolStripMenuItem.Checked);
            }
        }

        public void ChangeTextSize(double textFactor)
        {
            TargetsTextFactor = textFactor;
            SequenceTree.OnTextZoomChanged();
        }

        public void ChangeColorScheme()
        {
            UpdateGraphPanes();
            // Because changing text size already did everything we needed to update
            // the Targets view text.
            ChangeTextSize(Program.MainWindow.TargetsTextFactor);            
        }

        public void OpenLibraryExplorer(string libraryName)
        {
            var viewLibraryDlg = new ViewLibraryDlg(_libraryManager, libraryName, this) { Owner = this };
            viewLibraryDlg.Show(this);
        }

        private void statusToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bool show = statusToolStripMenuItem.Checked;
            Settings.Default.ShowStatusBar = show;
            statusStrip.Visible = show;
        }

        private void toolBarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bool show = toolBarToolStripMenuItem.Checked;
            Settings.Default.RTPredictorVisible = show;
            mainToolStrip.Visible = show;
        }

        private void addFoldChangeMenuItem_Click(object sender, EventArgs e)
        {
            AddGroupComparison();
        }

        public void AddGroupComparison()
        {
            using (var editDlg = new EditGroupComparisonDlg(this, GroupComparisonDef.EMPTY,
                Settings.Default.GroupComparisonDefList))
            {
                if (editDlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.GroupComparisonDefList.Add(editDlg.GroupComparisonDef);
                    ModifyDocument(Resources.SkylineWindow_AddGroupComparison_Add_Fold_Change,
                        doc => doc.ChangeSettings(
                            doc.Settings.ChangeDataSettings(
                                doc.Settings.DataSettings.AddGroupComparisonDef(
                                editDlg.GroupComparisonDef))), SettingsLogFunction);
                }
            }
        }

        public void ChangeDocPanoramaUri(Uri uri)
        {
                    ModifyDocument(Resources.SkylineWindow_ChangeDocPanoramaUri_Store_Panorama_publish_location,
                        doc => doc.ChangeSettings(
                            doc.Settings.ChangeDataSettings(
                                doc.Settings.DataSettings.ChangePanoramaPublishUri(
                                uri))), SettingsLogFunction);
        }

        private void editGroupComparisonListMenuItem_Click(object sender, EventArgs e)
        {
            DisplayDocumentSettingsDialogPage(1);
        }

        private void groupComparisonsMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            groupComparisonsMenuItem.DropDownItems.Clear();
            if (DocumentUI.Settings.DataSettings.GroupComparisonDefs.Any())
            {
                foreach (var groupComparisonDef in DocumentUI.Settings.DataSettings.GroupComparisonDefs)
                {
                    groupComparisonsMenuItem.DropDownItems.Add(MakeToolStripMenuItem(groupComparisonDef));
                }
                groupComparisonsMenuItem.DropDownItems.Add(new ToolStripSeparator());
            }
            groupComparisonsMenuItem.DropDownItems.AddRange(new ToolStripItem[]
            {
                addGroupComparisonMenuItem,
                editGroupComparisonListMenuItem
            });
        }

        private ToolStripMenuItem MakeToolStripMenuItem(GroupComparisonDef groupComparisonDef)
        {
            return new ToolStripMenuItem(groupComparisonDef.Name, null, (sender, args) =>
            {
                ShowGroupComparisonWindow(groupComparisonDef.Name);
            });
        }

        public void ShowGroupComparisonWindow(string groupComparisonName)
        {
            FoldChangeGrid.ShowFoldChangeGrid(dockPanel, GetFloatingRectangleForNewWindow(), this, groupComparisonName);
        }
        
        public void UpdateTargetsDisplayMode(ProteinMetadataManager.ProteinDisplayMode mode)
        {
            Settings.Default.ShowPeptidesDisplayMode = mode.ToString();
            Settings.Default.ShowPeptides = true;
            ShowSequenceTreeForm(true, true);

            FormUtil.OpenForms.OfType<FoldChangeBarGraph>().ForEach(b => b.QueueUpdateGraph());
            FormUtil.OpenForms.OfType<FoldChangeVolcanoPlot>().ForEach(v => v.QueueUpdateGraph());
        }

        private void showTargetsByNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            UpdateTargetsDisplayMode(ProteinMetadataManager.ProteinDisplayMode.ByName);
        }

        private void showTargetsByAccessionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            UpdateTargetsDisplayMode(ProteinMetadataManager.ProteinDisplayMode.ByAccession);
        }

        private void showTargetsByPreferredNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            UpdateTargetsDisplayMode(ProteinMetadataManager.ProteinDisplayMode.ByPreferredName);
        }

        private void showTargetsByGeneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            UpdateTargetsDisplayMode(ProteinMetadataManager.ProteinDisplayMode.ByGene);
        }

        private void peptidesMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            showTargetsByNameToolStripMenuItem.Checked =
                (Settings.Default.ShowPeptidesDisplayMode == ProteinMetadataManager.ProteinDisplayMode.ByName.ToString());
            showTargetsByAccessionToolStripMenuItem.Checked =
                (Settings.Default.ShowPeptidesDisplayMode == ProteinMetadataManager.ProteinDisplayMode.ByAccession.ToString());
            showTargetsByPreferredNameToolStripMenuItem.Checked =
                (Settings.Default.ShowPeptidesDisplayMode == ProteinMetadataManager.ProteinDisplayMode.ByPreferredName.ToString());
            showTargetsByGeneToolStripMenuItem.Checked =
                (Settings.Default.ShowPeptidesDisplayMode == ProteinMetadataManager.ProteinDisplayMode.ByGene.ToString());
        }

        private void PerformSort(string title, Comparison<PeptideGroupDocNode> comparison)
        {
            ModifyDocument(title, doc =>
            {
                var listIrt = new List<PeptideGroupDocNode>();
                var listProteins = new List<PeptideGroupDocNode>(doc.Children.Count);
                var listDecoy = new List<PeptideGroupDocNode>();
                foreach (var nodePepGroup in doc.MoleculeGroups)
                {
                    if (nodePepGroup.IsDecoy)
                    {
                        listDecoy.Add(nodePepGroup);
                    }
                    else if (nodePepGroup.PeptideCount > 0 && nodePepGroup.Peptides.All(nodePep => nodePep.GlobalStandardType == StandardType.IRT))
                    {
                        listIrt.Add(nodePepGroup);
                    }
                    else
                    {
                        listProteins.Add(nodePepGroup);
                    }
                }
                listIrt.Sort(comparison);
                listProteins.Sort(comparison);
                listDecoy.Sort(comparison);
                return (SrmDocument)doc.ChangeChildrenChecked(listIrt.Concat(listProteins).Concat(listDecoy).ToArray());
            });
        }

        public void sortProteinsByNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PerformSort(Resources.SkylineWindow_sortProteinsMenuItem_Click_Sort_proteins_by_name, PeptideGroupDocNode.CompareNames);
        }

        public void sortProteinsByAccessionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PerformSort(Resources.SkylineWindow_sortProteinsByAccessionToolStripMenuItem_Click_Sort_proteins_by_accession, PeptideGroupDocNode.CompareAccessions);
        }

        public void sortProteinsByPreferredNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PerformSort(Resources.SkylineWindow_sortProteinsByPreferredNameToolStripMenuItem_Click_Sort_proteins_by_preferred_name, PeptideGroupDocNode.ComparePreferredNames);
        }

        public void sortProteinsByGeneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PerformSort(Resources.SkylineWindow_sortProteinsByGeneToolStripMenuItem_Click_Sort_proteins_by_gene, PeptideGroupDocNode.CompareGenes);
        }

        private void addMoleculeContextMenuItem_Click(object sender, EventArgs e)
        {
            AddSmallMolecule();
        }

        private void addSmallMoleculePrecursorContextMenuItem_Click(object sender, EventArgs e)
        {
            AddSmallMolecule();
        }

        private void addTransitionMoleculeContextMenuItem_Click(object sender, EventArgs e)
        {
            AddSmallMolecule();
        }

        private TransitionDocNode[] GetDefaultPrecursorTransitions(SrmDocument doc, TransitionGroup tranGroup)
        {
            // CONSIDER(bspratt):
            // You might want to prepoluate one or more precursor transitions, if full scan MS1 "Isotope peaks include"=="count" 
            // (in which case you'd want more than one), or Transition Settings ion types filter includes "p"
            // var transition = new Transition(tranGroup, tranGroup.PrecursorAdduct, null, tranGroup.CustomMolecule);
            // var massType = doc.Settings.TransitionSettings.Prediction.FragmentMassType;
            // double mass = transition.CustomIon.GetMass(massType);
            // var nodeTran = new TransitionDocNode(transition, null, mass, null, null); // Precursor transition
            // return new[] { nodeTran };
            return new TransitionDocNode[0];
        }

        public void AddSmallMolecule()
        {
            var nodeGroupTree = SequenceTree.SelectedNode as TransitionGroupTreeNode;
            var nodePepGroupTree = SequenceTree.SelectedNode as PeptideGroupTreeNode;
            var nodePepTree = SequenceTree.SelectedNode as PeptideTreeNode;
            if (nodeGroupTree != null)
            {
                // Adding a transition - just want charge and formula/mz
                var nodeGroup = nodeGroupTree.DocNode;
                var groupPath = nodeGroupTree.Path;
                // List of existing transitions to avoid duplication
                var existingIons = nodeGroup.Transitions.Select(child => child.Transition)
                                                        .Where(c => c.IsNonReporterCustomIon()).ToArray();
                using (var dlg = new EditCustomMoleculeDlg(this,
                    EditCustomMoleculeDlg.UsageMode.fragment,
                    Resources.SkylineWindow_AddMolecule_Add_Transition, null, existingIons,
                    Transition.MIN_PRODUCT_CHARGE,
                    Transition.MAX_PRODUCT_CHARGE,
                    Document.Settings, nodeGroup.CustomMolecule, nodeGroup.Transitions.Any() ? nodeGroup.Transitions.Last().Transition.Adduct : Adduct.SINGLY_PROTONATED, null, null, null))
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        ModifyDocument(string.Format(Resources.SkylineWindow_AddMolecule_Add_custom_product_ion__0_, dlg.ResultCustomMolecule.DisplayName), doc =>
                        {
                            // Okay to use TransitionGroup identity object, if it has been removed from the 
                            // tree, then the Add below with the path will throw
                            var transitionGroup = nodeGroup.TransitionGroup;
                            var transition = new Transition(transitionGroup, dlg.Adduct, null, dlg.ResultCustomMolecule);
                            var massType = doc.Settings.TransitionSettings.Prediction.FragmentMassType;
                            var mass = transition.CustomIon.GetMass(massType);
                            var nodeTran = new TransitionDocNode(transition, null, mass, TransitionDocNode.TransitionQuantInfo.DEFAULT);
                            return (SrmDocument)doc.Add(groupPath, nodeTran);
                        });
                    }
                }
            }
            else if (nodePepTree != null)
            {
                // Adding a precursor - appends a transition group to the current peptide's existing list
                var nodePep = nodePepTree.DocNode;
                var pepPath = nodePepTree.Path;
                var notFirst = nodePep.TransitionGroups.Any();
                // Get a list of existing precursors - likely basis for adding a heavy version
                var existingPrecursors = nodePep.TransitionGroups.Select(child => child.TransitionGroup).Where(c => c.IsCustomIon).ToArray();
                using (var dlg = new EditCustomMoleculeDlg(this,
                    EditCustomMoleculeDlg.UsageMode.precursor,
                    Resources.SkylineWindow_AddSmallMolecule_Add_Precursor,
                    null, existingPrecursors,
                    TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE, Document.Settings,
                    nodePep.Peptide.CustomMolecule,
                    notFirst ? nodePep.TransitionGroups.First().TransitionGroup.PrecursorAdduct : Adduct.SINGLY_PROTONATED,
                    notFirst ? nodePep.TransitionGroups.First().ExplicitValues : ExplicitTransitionGroupValues.EMPTY,
                    null,
                    notFirst ? nodePep.TransitionGroups.First().TransitionGroup.LabelType : IsotopeLabelType.light))
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        ModifyDocument(string.Format(Resources.SkylineWindow_AddSmallMolecule_Add_small_molecule_precursor__0_, dlg.ResultCustomMolecule.DisplayName), doc =>
                        {
                            var tranGroup = new TransitionGroup(nodePep.Peptide, dlg.Adduct, dlg.IsotopeLabelType);
                            var tranGroupDocNode = new TransitionGroupDocNode(tranGroup, Annotations.EMPTY,
                                doc.Settings, null, null, dlg.ResultExplicitTransitionGroupValues, null, GetDefaultPrecursorTransitions(doc, tranGroup), true);
                            return (SrmDocument)doc.Add(pepPath, tranGroupDocNode);
                        });
                    }
                }
            }
            else if (nodePepGroupTree != null)
            {
                var nodePepGroup = nodePepGroupTree.DocNode;
                if (!nodePepGroup.IsPeptideList)
                {
                    MessageDlg.Show(this, Resources.SkylineWindow_AddMolecule_Custom_molecules_cannot_be_added_to_a_protein_);
                    return;
                }
                else if (!nodePepGroup.IsEmpty && nodePepGroup.IsProteomic) // N.B. : An empty PeptideGroup will return True for IsProteomic, the assumption being that it's in the early stages of being populated from a protein
                {
                    MessageDlg.Show(this, Resources.SkylineWindow_AddMolecule_Custom_molecules_cannot_be_added_to_a_peptide_list_);
                    return;
                }

                var pepGroupPath = nodePepGroupTree.Path;
                using (var dlg = new EditCustomMoleculeDlg(this,
                    EditCustomMoleculeDlg.UsageMode.moleculeNew,
                    Resources.SkylineWindow_AddSmallMolecule_Add_Small_Molecule_and_Precursor, null, null,
                    TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE, Document.Settings, null, Adduct.NonProteomicProtonatedFromCharge(1), 
                    ExplicitTransitionGroupValues.EMPTY, ExplicitRetentionTimeInfo.EMPTY, IsotopeLabelType.light))
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        ModifyDocument(string.Format(Resources.SkylineWindow_AddSmallMolecule_Add_small_molecule__0_, dlg.ResultCustomMolecule.DisplayName), doc =>
                        {
                            // If ion was described as having an adduct, leave that off for the parent "peptide" molecular formula
                            var peptideMolecule = dlg.ResultCustomMolecule;
                            var peptide = new Peptide(peptideMolecule);
                            var tranGroup = new TransitionGroup(peptide, dlg.Adduct, dlg.IsotopeLabelType, true, null);
                            var tranGroupDocNode = new TransitionGroupDocNode(tranGroup, Annotations.EMPTY,
                                doc.Settings, null, null, dlg.ResultExplicitTransitionGroupValues, null,
                                GetDefaultPrecursorTransitions(doc, tranGroup), true);
                            var nodePepNew = new PeptideDocNode(peptide, Document.Settings, null, null,
                                dlg.ResultRetentionTimeInfo, new[] { tranGroupDocNode }, true);
                            return (SrmDocument)doc.Add(pepGroupPath, nodePepNew);
                        });
                    }
                }
            }
        }

        #endregion

        #region Settings menu

        private void saveCurrentMenuItem_Click(object sender, EventArgs e)
        {
            SaveSettings();
        }

        private void editSettingsMenuItem_Click(object sender, EventArgs e)
        {
            IEnumerable<SrmSettings> listNew = Settings.Default.SrmSettingsList.EditList(this, null);
            if (listNew != null)
            {
                SrmSettingsList list = Settings.Default.SrmSettingsList;
                SrmSettings settingsDefault = list[0];
                SrmSettings settingsCurrent = DocumentUI.Settings;
                list.Clear();
                list.Add(settingsDefault); // Add back default settings.
                list.AddRange(listNew);
                SrmSettings settings;
                if (!list.TryGetValue(settingsCurrent.GetKey(), out settings))
                {
                    // If the current settings were removed, then make
                    // them the default, and use them to avoid a shift
                    // to some random settings values.
                    list[0] = settingsCurrent.MakeSavable(SrmSettingsList.DefaultName);
                }
            }
        }

        private void shareSettingsMenuItem_Click(object sender, EventArgs e)
        {
            ShareSettings();
        }

        public void ShareSettings()
        {
            using (var dlg = new ShareListDlg<SrmSettingsList, SrmSettings>(Settings.Default.SrmSettingsList))
            {
                dlg.ShowDialog(this);
            }
        }

        private void importSettingsMenuItem1_Click(object sender, EventArgs e)
        {
            ImportSettings();
        }

        public void ImportSettings()
        {
            ShareListDlg<SrmSettingsList, SrmSettings>.Import(this,
                Settings.Default.SrmSettingsList);
        }

        private void peptideSettingsMenuItem_Click(object sender, EventArgs e)
        {
            ShowPeptideSettingsUI();
        }

        public void ShowPeptideSettingsUI()
        {
            ShowPeptideSettingsUI(this);
        }

        public void ShowPeptideSettingsUI(IWin32Window parent)
        {
            ShowPeptideSettingsUI(parent, null);
        }

        public void ShowPeptideSettingsUI(PeptideSettingsUI.TABS? tab)
        {
            ShowPeptideSettingsUI(this, tab);
        }

        private void ShowPeptideSettingsUI(IWin32Window parent, PeptideSettingsUI.TABS? tab)
        {
            using (PeptideSettingsUI ps = new PeptideSettingsUI(this, _libraryManager) { TabControlSel = tab })
            {
                var oldStandard = RCalcIrtStandard();
                
                if (ps.ShowDialog(parent) == DialogResult.OK)
                {
                    if (ps.IsShowLibraryExplorer)
                    {
                        int libraryExpIndex = OwnedForms.IndexOf(form => form is ViewLibraryDlg);
                        if (libraryExpIndex != -1)
                            OwnedForms[libraryExpIndex].Activate();
                    }

                    HashSet<Target> missingPeptides;
                    var newStandard = RCalcIrtStandard(out missingPeptides);
                    if (oldStandard != newStandard)
                        AddStandardsToDocument(newStandard, missingPeptides);
                }
            }

            // In case user shows/hides things via the Spectral Library 
            // Explorer's spectrum graph pane.
            UpdateGraphPanes();
        }

        private void AddStandardsToDocument(IrtStandard standard, ICollection<Target> missingPeptides)
        {
            var standardDocReader = standard.DocumentReader;
            if (standardDocReader == null)
                return;

            // Check if document already has the standards
            var docPeptides = Document.Peptides.Select(nodePep => nodePep.Peptide.Target);
            var standardPeptides = standard.Peptides.Select(pep => pep.Target).Except(missingPeptides);
            if (!standardPeptides.Except(docPeptides).Any())
                return;

            using (var dlg = new AddIrtStandardsToDocumentDlg())
            {
                if (dlg.ShowDialog(this) == DialogResult.Yes)
                {
                    ModifyDocument(Resources.SkylineWindow_AddStandardsToDocument_Add_standard_peptides, doc =>
                    {
                        IdentityPath firstAdded, nextAdd;
                        doc = doc.ImportDocumentXml(standardDocReader,
                                                    string.Empty,
                                                    MeasuredResults.MergeAction.remove,
                                                    false,
                                                    FindSpectralLibrary,
                                                    Settings.Default.StaticModList,
                                                    Settings.Default.HeavyModList,
                                                    doc.Children.Any() ? new IdentityPath(doc.Children.First().Id) : null,
                                                    out firstAdded,
                                                    out nextAdd,
                                                    false);

                        var standardPepGroup = doc.PeptideGroups.First(nodePepGroup => new IdentityPath(nodePepGroup.Id).Equals(firstAdded));
                        var pepList = new List<DocNode>();
                        foreach (var nodePep in standardPepGroup.Peptides.Where(pep => !missingPeptides.Contains(pep.ModifiedTarget)))
                        {
                            var tranGroupList = new List<DocNode>();
                            foreach (TransitionGroupDocNode nodeTranGroup in nodePep.Children)
                            {
                                var transitions = nodeTranGroup.Transitions.Take(dlg.NumTransitions).ToArray();
                                Array.Sort(transitions, TransitionGroup.CompareTransitions);
                                tranGroupList.Add(nodeTranGroup.ChangeChildren(transitions.Cast<DocNode>().ToList()));
                            }
                            pepList.Add(nodePep.ChangeChildren(tranGroupList));
                        }
                        var newStandardPepGroup = standardPepGroup.ChangeChildren(pepList);
                        return (SrmDocument) doc.ReplaceChild(newStandardPepGroup);
                    });
                }
            }
        }

        private IrtStandard RCalcIrtStandard()
        {
            HashSet<Target> missingPeptides;
            return RCalcIrtStandard(out missingPeptides);
        }

        private IrtStandard RCalcIrtStandard(out HashSet<Target> missingPeptides)
        {
            missingPeptides = new HashSet<Target>();
            var calcPeptides = RCalcIrt.IrtPeptides(Document).ToArray();
            return calcPeptides.Any() ? IrtStandard.WhichStandard(calcPeptides, out missingPeptides) : IrtStandard.NULL;
        }

        private void transitionSettingsMenuItem_Click(object sender, EventArgs e)
        {
            ShowTransitionSettingsUI();
        }

        public void ShowTransitionSettingsUI()
        {
            ShowTransitionSettingsUI(this);
        }

        public void ShowTransitionSettingsUI(IWin32Window parent)
        {
            ShowTransitionSettingsUI(parent, null);
        }

        public void ShowTransitionSettingsUI(TransitionSettingsUI.TABS? tab)
        {
            ShowTransitionSettingsUI(this, tab);
        }

        private void ShowTransitionSettingsUI(IWin32Window parent, TransitionSettingsUI.TABS? tab)
        {
            using (TransitionSettingsUI ts = new TransitionSettingsUI(this) { TabControlSel = tab })
            {
                if (ts.ShowDialog(parent) == DialogResult.OK)
                {
                    // At this point the dialog does everything by itself.
                }
            }
        }

        private void settingsMenu_DropDownOpening(object sender, EventArgs e)
        {
            ToolStripMenuItem menu = settingsToolStripMenuItem;
            SrmSettingsList list = Settings.Default.SrmSettingsList;
            string selected = DocumentUI.Settings.Name;

            // No point in saving the settings, if a saved instance is selected.
            saveCurrentMenuItem.Enabled = (selected == SrmSettingsList.DefaultName);

            // Only edit or share, if more than default settings.
            bool enable = (list.Count > 1);
            editSettingsMenuItem.Enabled = enable;
            shareSettingsMenuItem.Enabled = enable;

            // Add the true default settings, as these can be useful for getting everyone using the
            // same settings in an instructional context
            var listItems = new List<SrmSettings> { SrmSettingsList.GetDefault() };
            listItems.AddRange(list.Skip(1));
            int i = 0;
            foreach (var settings in listItems)
            {
                ToolStripMenuItem item = menu.DropDownItems[i] as ToolStripMenuItem;
                if (item == null || settings.Name != item.Name)
                {
                    // Remove the rest until the separator is reached
                    while (!ReferenceEquals(menu.DropDownItems[i], toolStripSeparatorSettings))
                        menu.DropDownItems.RemoveAt(i);

                    SelectSettingsHandler handler = new SelectSettingsHandler(this, settings);
                    item = new ToolStripMenuItem(settings.Name, null,
                        handler.ToolStripMenuItemClick);
                    menu.DropDownItems.Insert(i, item);
                }

                if (Equals(settings, DocumentUI.Settings))
                    item.Checked = true;
                i++;
            }

            // Remove the rest until the separator is reached
            while (!ReferenceEquals(menu.DropDownItems[i], toolStripSeparatorSettings))
                menu.DropDownItems.RemoveAt(i);

            toolStripSeparatorSettings.Visible = (i > 0);
        }

        public bool SaveSettings()
        {
            using (SaveSettingsDlg ss = new SaveSettingsDlg())
            {
                if (ss.ShowDialog(this) != DialogResult.OK)
                    return false;

                SrmSettings settingsNew = null;

                ModifyDocument(Resources.SkylineWindow_SaveSettings_Name_settings, doc =>
                                                    {
                                                        settingsNew = (SrmSettings) doc.Settings.ChangeName(ss.SaveName);
                                                        return doc.ChangeSettings(settingsNew);
                                                    }, SettingsLogFunction);

                if (settingsNew != null)
                    Settings.Default.SrmSettingsList.Add(settingsNew.MakeSavable(ss.SaveName));

                return true;
            }
        }

        private class SelectSettingsHandler
        {
            private readonly SkylineWindow _skyline;
            private readonly SrmSettings _settings;

            public SelectSettingsHandler(SkylineWindow skyline, SrmSettings settings)
            {
                _skyline = skyline;
                _settings = settings;
            }

            public void ToolStripMenuItemClick(object sender, EventArgs e)
            {
                // If the current settings are not in a saved set, then ask to save
                // before overriting them.
                if (_skyline.DocumentUI.Settings.Name == SrmSettingsList.DefaultName)
                {
                    DialogResult result =
                        MessageBox.Show(
                            Resources.
                                SelectSettingsHandler_ToolStripMenuItemClick_Do_you_want_to_save_your_current_settings_before_switching,
                        Program.Name, MessageBoxButtons.YesNoCancel);
                    switch (result)
                    {
                        case DialogResult.Cancel:
                            return;
                        case DialogResult.Yes:
                            if (!_skyline.SaveSettings())
                                return;
                            break;
                    }
                }
                // For extra safety, make sure the settings do not contain Library
                // instances.  Saved settings should always have null Libraries, and
                // use the LibraryManager to get the right libraries for the library
                // spec's.
                var settingsNew = _settings;
                var lib = _settings.PeptideSettings.Libraries;
                if (lib != null)
                {
                    foreach (var library in lib.Libraries)
                    {
                        if (library != null)
                        {
                            settingsNew = _settings.ChangePeptideSettings(_settings.PeptideSettings.ChangeLibraries(
                                _settings.PeptideSettings.Libraries.ChangeLibraries(new Library[lib.Libraries.Count])));
                            break;
                        }
                    }
                }
                if (_skyline.ChangeSettings(settingsNew, false))
                    settingsNew.UpdateLists(_skyline.DocumentFilePath);
            }
        }

        public void ResetDefaultSettings()
        {
            var defaultSettings = SrmSettingsList.GetDefault();
            if (!Equals(defaultSettings, DocumentUI.Settings))
                ChangeSettings(defaultSettings, false, Resources.SkylineWindow_ResetDefaultSettings_Reset_default_settings);
        }

        public bool ChangeSettingsMonitored(Control parent, string message, Func<SrmSettings, SrmSettings> changeSettings)
        {
            bool documentChanged;
            do
            {
                documentChanged = false;
                try
                {
                    var newSettings = changeSettings(Document.Settings);
                    bool success = false;
                    using (var longWaitDlg = new LongWaitDlg(this)
                    {
                        Text = Text,    // Same as dialog box
                        Message = message,
                        ProgressValue = 0
                    })
                    {
                        var undoState = GetUndoState();
                        longWaitDlg.PerformWork(parent, 800, progressMonitor =>
                        {
                            using (var settingsChangeMonitor = new SrmSettingsChangeMonitor(progressMonitor, message, this))
                            {
                                // If background proteome lacks the needed protein metadata for uniqueness checks, force loading now
                                var diff = new SrmSettingsDiff(newSettings, Document.Settings);
                                if (diff.DiffPeptides || newSettings.PeptideSettings.NeedsBackgroundProteomeUniquenessCheckProcessing)
                                {
                                    if (progressMonitor.IsCanceled)
                                    {
                                        return;
                                    }
                                    // Looping here in case some other agent interrupts us with a change to Document
                                    while (newSettings.PeptideSettings.NeedsBackgroundProteomeUniquenessCheckProcessing)
                                    {
                                        BackgroundProteomeManager.BeginForegroundLoad();  // Signal the background task to stay out of our way
                                        var manager = BackgroundProteomeManager; // Use the background loader logic, but in this thread
                                        var withMetaData = manager.LoadForeground(newSettings.PeptideSettings, settingsChangeMonitor);
                                        if (withMetaData == null)
                                        {
                                            return; // Cancelled
                                        }
                                        newSettings = newSettings.ChangePeptideSettings(s => s.ChangeBackgroundProteome(withMetaData));
                                    }
                                }
                                success = ChangeSettings(newSettings, true, null, undoState, settingsChangeMonitor,
                                    () => longWaitDlg.EnableCancelOption(true),
                                    () => longWaitDlg.EnableCancelOption(false));
                            }
                        });
                        if (!success || longWaitDlg.IsCanceled)
                        {
                            return false;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Canceled mid-change due to background document change
                    documentChanged = true;
                }
                finally
                {
                    BackgroundProteomeManager.EndForegroundLoad(); // Done overriding the background loader
                }
            }
            while (documentChanged);

            return true;
        }

        public bool ChangeSettings(SrmSettings newSettings, bool store, string message = null, IUndoState undoState = null,
            SrmSettingsChangeMonitor monitor = null, Action onModifyingAction = null, Action onModifiedAction = null)
        {
            if (store)
            {
                // Edited settings always use the default name.  Saved settings
                // by nature have never been changed.  The way to store settings
                // other than to the default name is SaveSettings().
                string defaultName = SrmSettingsList.DefaultName;
                // MakeSavable will also remove any results information
                Settings.Default.SrmSettingsList[0] = newSettings.MakeSavable(defaultName);
                // Document must have the same name as the saved version.
                if (!Equals(newSettings.Name, defaultName))
                    newSettings = (SrmSettings)newSettings.ChangeName(defaultName);
            }

            ModifyDocument(message ?? Resources.SkylineWindow_ChangeSettings_Change_settings, undoState,
                doc => doc.ChangeSettings(newSettings, monitor), onModifyingAction, onModifiedAction, SettingsLogFunction);
            return true;
        }

        private void documentSettingsMenuItem_Click(object sender, EventArgs e)
        {
            ShowDocumentSettingsDialog();
        }

        public void ShowDocumentSettingsDialog()
        {
            DisplayDocumentSettingsDialogPage(0);
        }

        public void DisplayDocumentSettingsDialogPage(int tabPageIndex)
        {
            using (var dlg = new DocumentSettingsDlg(this))
            {
                dlg.GetTabControl().SelectedIndex = tabPageIndex;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    ModifyDocument(Resources.SkylineWindow_ShowDocumentSettingsDialog_Change_document_settings,
                        doc => doc.ChangeSettings(
                            doc.Settings.ChangeDataSettings(
                                dlg.GetDataSettings(doc.Settings.DataSettings))), SettingsLogFunction);
                }
            }
        }

        private void integrateAllMenuItem_Click(object sender, EventArgs e)
        {
            ToggleIntegrateAll();
        }

        public void ToggleIntegrateAll()
        {
            SetIntegrateAll(!DocumentUI.Settings.TransitionSettings.Integration.IsIntegrateAll);
        }

        public void SetIntegrateAll(bool integrateAll)
        {
            if (integrateAll != DocumentUI.Settings.TransitionSettings.Integration.IsIntegrateAll)
            {
                ModifyDocument(integrateAll ? Resources.SkylineWindow_IntegrateAll_Set_integrate_all : Resources.SkylineWindow_IntegrateAll_Clear_integrate_all,
                    doc => doc.ChangeSettings(doc.Settings.ChangeTransitionIntegration(i => i.ChangeIntegrateAll(integrateAll))), SettingsLogFunction);
            }
        }

        #endregion // Settings menu

        #region Tools Menu

        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowToolOptionsUI();
        }

        public void ShowToolOptionsUI()
        {
            using (var dlg = new ToolOptionsUI())
            {
                dlg.ShowDialog(this);
            }
        }

        private void updatesToolsMenuItem_Click(object sender, EventArgs e)
        {
            ShowToolUpdatesDlg();
        }

        public void ShowToolUpdatesDlg()
        {
            ShowToolUpdatesDlg(new ToolUpdateHelper());
        }

        public void ShowToolUpdatesDlg(IToolUpdateHelper updateHelper)
        {
            using (var dlg = new ToolUpdatesDlg(this,
                                                Settings.Default.ToolList.Where(tool => tool.UpdateAvailable).ToList(),
                                                updateHelper))
            {
                dlg.ShowDialog(this);
            }
        }

        private void toolStoreMenuItem_Click(object sender, EventArgs e)
        {
            ShowToolStoreDlg();
        }

        public void ShowToolStoreDlg()
        {
            ToolInstallUI.InstallZipFromWeb(this, InstallProgram);
        }

        private void configureToolsMenuItem_Click(object sender, EventArgs e)
        {
            ShowConfigureToolsDlg();
        }

        public void ShowConfigureToolsDlg()
        {
            using (var dlg = new ConfigureToolsDlg(this))
            {
                dlg.ShowDialog(this);
            }
        }

        private void toolsMenu_DropDownOpening(object sender, EventArgs e)
        {
            PopulateToolsMenu();
        }

        public void PopulateToolsMenu()
        {
            // Remove all tools from the toolToolStripMenuItem.
            while (!ReferenceEquals(toolsMenu.DropDownItems[0], toolStripSeparatorTools))
            {
                toolsMenu.DropDownItems.RemoveAt(0);
                //Consider: (danny) When we remove menu items do we have to dispose of them?
            }

            int lastInsertIndex = 0;
            var toolList = Settings.Default.ToolList;
            foreach (ToolDescription tool in toolList)
            {
                if (tool.Title.Contains('\\'))  // Not L10N
                {
                    ToolStripMenuItem current = toolsMenu;
                    string[] spliced = tool.Title.Split('\\');  // Not L10N
                    for (int i = 0; i < spliced.Length-1; i++)
                    {
                        ToolStripMenuItem item;
                        int index = toolExists(current, spliced[i]);
                        if (index >= 0)
                        {
                            item = (ToolStripMenuItem) current.DropDownItems[index];
                        }
                        else
                        {
                            item = new ToolStripMenuItem(spliced[i])
                                {
                                    Image = tool.UpdateAvailable ? Resources.ToolUpdateAvailable : null,
                                    ImageTransparentColor = Color.Fuchsia
                                };
                            if (current == toolsMenu)
                            {
                                current.DropDownItems.Insert(lastInsertIndex++, item);
                            }
                            else
                            {
                                current.DropDownItems.Add(item);
                            }    
                        }
                        
                        current = item;
                    }
                    ToolMenuItem final = new ToolMenuItem(tool, this) { Text = spliced.Last() };
                    current.DropDownItems.Add(final);
                }
                else
                {
                    ToolMenuItem menuItem = new ToolMenuItem(tool, this)
                        {
                            Text = tool.Title, 
                            Image = tool.UpdateAvailable ? Resources.ToolUpdateAvailable : null,
                            ImageTransparentColor = Color.Fuchsia
                        };
                    toolsMenu.DropDownItems.Insert(lastInsertIndex++ , menuItem);
                }
            }
            toolStripSeparatorTools.Visible = (lastInsertIndex != 0);
            updatesToolsMenuItem.Enabled = updatesToolsMenuItem.Visible = toolList.Contains(tool => tool.UpdateAvailable);
        }
        /// <summary>
        /// Helper Function that determines if a tool exists on a menu by it's title. 
        /// </summary>
        /// <param name="menu">Menu we are looking in</param>
        /// <param name="toolTitle">Title we are looking for</param>
        /// <returns>Index into menu if found, -1 if not found.</returns>
        private int toolExists(ToolStripMenuItem menu, string toolTitle)
        {
            for (int i = 0; i < menu.DropDownItems.Count; i++)
            {
                if (menu.DropDownItems[i] == configureToolsMenuItem)
                    return -1;
                if (menu.DropDownItems[i].Text == toolTitle)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Runs a tool by index from the tools menu. (for testing) make sure to SkylineWindow.PopulateToolsMenu() first
        /// Does not work with nested tools. 
        /// </summary>
        /// <param name="i">Index of tool in the menu. Zero indexed.</param>
        public void RunTool(int i)
        {
            GetToolMenuItem(i).DoClick();
        }

        /// <summary>
        /// Returns the title of a tool by index from the tools menu. (for testing) make sure to run SkylineWindow.PopulateToolsMenu() first
        /// </summary>
        /// <param name="i">Index of tool in the menu. Zero indexed.</param>
        /// <returns></returns>
        public string GetToolText(int i)
        {
            return GetToolMenuItem(i).Text;
        }

        public string GetTextByIndex(int i)
        {
            return toolsMenu.DropDownItems[i].Text;
        }

        public bool UpdatesMenuEnabled()
        {
            return updatesToolsMenuItem.Enabled;
        }

        public bool ConfigMenuPresent()
        {
            return toolsMenu.DropDownItems.Contains(configureToolsMenuItem);
        }

        public ToolStripMenuItem GetMenuItem(int index)
        {
            return (ToolStripMenuItem) toolsMenu.DropDownItems[index];
        }

        public ToolMenuItem GetToolMenuItem(int i)
        {
            return GetToolMenuItemRecursive(toolsMenu.DropDownItems, ref i);
        }

        public ToolMenuItem GetToolMenuItemRecursive(ToolStripItemCollection items, ref int i)
        {
            foreach (var item in items)
            {
                // first check to see if it is a valid tool
                var toolMenuItem = item as ToolMenuItem;
                if (toolMenuItem != null && i-- == 0)
                    return toolMenuItem;
                var toolStripDropDownItem = item as ToolStripDropDownItem;
                if (toolStripDropDownItem != null)
                {
                    // recurse to find nested tools if possible
                    var tool = GetToolMenuItemRecursive(toolStripDropDownItem.DropDownItems, ref i);
                    if (tool != null)
                        return tool;
                }
            }
            return null;
        }

        public class ToolMenuItem : ToolStripMenuItem
        {
            public readonly ToolDescription _tool;

            public ToolMenuItem(ToolDescription tool, SkylineWindow parent)
            {
                _tool = tool;
                Click += HandleClick;
                _parent = parent;
            }

            private readonly SkylineWindow _parent;
            public string Title { get { return _tool.Title; } }
            public string Command { get { return _tool.Command; } }

            private void HandleClick(object sender, EventArgs e)
            {
                DoClick();
            }

            public void DoClick()
            {
                // Run the tool and catch all errors.
                try
                {
                    if (_tool.OutputToImmediateWindow)
                    {
                        _parent.ShowImmediateWindow();
                        _tool.RunTool(_parent.Document, _parent, _skylineTextBoxStreamWriterHelper, _parent, _parent);
                    }
                    else
                    {
                        _tool.RunTool(_parent.Document, _parent, null, _parent, _parent);
                    }
                }
                catch (WebToolException e)
                {
                    WebHelpers.ShowLinkFailure(_parent, e.Link);
                }
                catch (ToolExecutionException e)
                {
                    MessageDlg.ShowException(_parent, e);
                }
            }
        }

        private void immediateWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowImmediateWindow();
        }

        public void ShowImmediateWindow()
        {
            if (_immediateWindow != null)
            {
                _immediateWindow.Activate();
            }
            else
            {
                _immediateWindow = CreateImmediateWindow();
                _immediateWindow.Activate();
                _immediateWindow.Focus();
                _immediateWindow.Show(dockPanel, DockState.DockBottom);
                //                ActiveDocumentChanged();
            }
        }

        public static TextBoxStreamWriterHelper _skylineTextBoxStreamWriterHelper;
        private readonly Alarms _removeStatusAlarms = new Alarms();

        private ImmediateWindow CreateImmediateWindow()
        {
            if (_skylineTextBoxStreamWriterHelper == null)
                _skylineTextBoxStreamWriterHelper = new TextBoxStreamWriterHelper();
            _immediateWindow = new ImmediateWindow(this, _skylineTextBoxStreamWriterHelper);
            return _immediateWindow;
        }

        private void DestroyImmediateWindow()
        {
            if (_immediateWindow != null)
            {
                _immediateWindow.Cleanup();
                _immediateWindow.Close();
                _immediateWindow = null;
            }
        }

        #endregion

        #region Help menu

        private void homeMenuItem_Click(object sender, EventArgs e)
        {
            WebHelpers.OpenLink(this, "http://proteome.gs.washington.edu/software/skyline/"); // Not L10N
        }

        private void videosMenuItem_Click(object sender, EventArgs e)
        {
            WebHelpers.OpenLink(this, "http://proteome.gs.washington.edu/software/Skyline/videos.html"); // Not L10N
        }

        private void tutorialsMenuItem_Click(object sender, EventArgs e)
        {
            OpenStartPageTutorial();
        }

        public void OpenStartPageTutorial()
        {
            if (!CheckSaveDocument())
                return;

            using (var startupForm = new StartPage())
            {
                startupForm.SelectedTab = StartPage.TABS.Tutorial;
                if (startupForm.ShowDialog(this) == DialogResult.OK)
                {
                    startupForm.Action(this);
                }
            }
        }

        private void supportMenuItem_Click(object sender, EventArgs e)
        {
            WebHelpers.OpenLink(this, "http://proteome.gs.washington.edu/software/Skyline/support.html"); // Not L10N
        }

        private void issuesMenuItem_Click(object sender, EventArgs e)
        {
            WebHelpers.OpenLink(this, "http://proteome.gs.washington.edu/software/Skyline/issues.html"); // Not L10N
        }

        private void checkForUpdatesMenuItem_Click(object sender, EventArgs e)
        {
            CheckForUpdate();
        }

        public void CheckForUpdate()
        {
            // Make sure the document is saved before doing this since it could
            // restart the application
            if (Dirty)
                SaveDocument();

            UpgradeManager.CheckForUpdateAsync(this, false);
        }

        private void aboutMenuItem_Click(object sender, EventArgs e)
        {
            using (var about = new AboutDlg())
            {
                about.ShowDialog(this);
            }
            
        }

        #endregion

        #region SequenceTree events

        public void ShowSequenceTreeForm(bool show, bool forceUpdate = false)
        {
            if (show)
            {
                if (_sequenceTreeForm != null)
                {
                    if (forceUpdate)
                    {
                        _sequenceTreeForm.UpdateTitle();
                        _sequenceTreeForm.SequenceTree.OnShowPeptidesDisplayModeChanged();
                    }
                    _sequenceTreeForm.Activate();
                    _sequenceTreeForm.Focus();
                }
                else
                {
                    _sequenceTreeForm = CreateSequenceTreeForm(null);
                    _sequenceTreeForm.Show(dockPanel, DockState.DockLeft);
                }
                // Make sure ComboResults has the right selection
                ActiveDocumentChanged();
            }
            else if (_sequenceTreeForm != null)
                {
                // Save current setting for showing spectra
                show = Settings.Default.ShowPeptides;
                // Close the spectrum graph window
                _sequenceTreeForm.Hide();
                // Restore setting and menuitem from saved value
                Settings.Default.ShowPeptides = show;
                }
            }

        private SequenceTreeForm CreateSequenceTreeForm(string persistentString)
        {
            // Initialize sequence tree control
            string expansionAndSelection = null;
            if (persistentString != null)
            {
                int sepIndex = persistentString.IndexOf('|');
                if (sepIndex != -1)
                    expansionAndSelection = persistentString.Substring(sepIndex + 1);
            }             
            _sequenceTreeForm = new SequenceTreeForm(this, expansionAndSelection != null);
            _sequenceTreeForm.FormClosed += sequenceTreeForm_FormClosed;
            _sequenceTreeForm.VisibleChanged += sequenceTreeForm_VisibleChanged;
            _sequenceTreeForm.SequenceTree.SelectedNodeChanged += sequenceTree_SelectedNodeChanged;
            _sequenceTreeForm.SequenceTree.AfterSelect += sequenceTree_AfterSelect;
            _sequenceTreeForm.SequenceTree.BeforeNodeEdit += sequenceTree_BeforeNodeEdit;
            _sequenceTreeForm.SequenceTree.AfterNodeEdit += sequenceTree_AfterNodeEdit;
            _sequenceTreeForm.SequenceTree.MouseUp += sequenceTree_MouseUp;
            _sequenceTreeForm.SequenceTree.PickedChildrenEvent += sequenceTree_PickedChildrenEvent;
            _sequenceTreeForm.SequenceTree.ItemDrag += sequenceTree_ItemDrag;
            _sequenceTreeForm.SequenceTree.DragEnter += sequenceTree_DragEnter;
            _sequenceTreeForm.SequenceTree.DragOver += sequenceTree_DragOver;
            _sequenceTreeForm.SequenceTree.DragDrop += sequenceTree_DragDrop;
            _sequenceTreeForm.SequenceTree.UseKeysOverride = _useKeysOverride;
            _sequenceTreeForm.ComboResults.SelectedIndexChanged += comboResults_SelectedIndexChanged;
            if (expansionAndSelection != null)
                _sequenceTreeForm.SequenceTree.RestoreExpansionAndSelection(expansionAndSelection);
            _sequenceTreeForm.UpdateTitle();
            return _sequenceTreeForm;
        }

        private void DestroySequenceTreeForm()
        {
            if (_sequenceTreeForm != null)
            {
                _sequenceTreeForm.FormClosed -= sequenceTreeForm_FormClosed;
                _sequenceTreeForm.VisibleChanged -= sequenceTreeForm_VisibleChanged;
                _sequenceTreeForm.SequenceTree.SelectedNodeChanged -= sequenceTree_SelectedNodeChanged;
                _sequenceTreeForm.SequenceTree.AfterSelect -= sequenceTree_AfterSelect;
                _sequenceTreeForm.SequenceTree.BeforeNodeEdit -= sequenceTree_BeforeNodeEdit;
                _sequenceTreeForm.SequenceTree.AfterNodeEdit -= sequenceTree_AfterNodeEdit;
                _sequenceTreeForm.SequenceTree.MouseUp -= sequenceTree_MouseUp;
                _sequenceTreeForm.SequenceTree.PickedChildrenEvent -= sequenceTree_PickedChildrenEvent;
                _sequenceTreeForm.SequenceTree.ItemDrag -= sequenceTree_ItemDrag;
                _sequenceTreeForm.SequenceTree.DragEnter -= sequenceTree_DragEnter;
                _sequenceTreeForm.SequenceTree.DragOver -= sequenceTree_DragOver;
                _sequenceTreeForm.SequenceTree.DragEnter -= sequenceTree_DragDrop;
                _sequenceTreeForm.ComboResults.SelectedIndexChanged -= comboResults_SelectedIndexChanged;
                _sequenceTreeForm.Close();
                _sequenceTreeForm = null;
            }
        }

        private void sequenceTreeForm_VisibleChanged(object sender, EventArgs e)
        {
            if (_sequenceTreeForm != null)
                Settings.Default.ShowPeptides = _sequenceTreeForm.Visible;
        }

        private void sequenceTreeForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            // Update settings and menu check
            Settings.Default.ShowPeptides = false;
            _sequenceTreeForm = null;
        }

        private void sequenceTree_BeforeNodeEdit(object sender, NodeLabelEditEventArgs e)
        {
            if (e.Node is EmptyNode)
                e.Node.Text = string.Empty;
            else
                e.CancelEdit = !SequenceTree.IsEditableNode(e.Node);
            ClipboardControlGotFocus(SequenceTree);
        }

        private void sequenceTree_AfterNodeEdit(object sender, NodeLabelEditEventArgs e)
        {
            ClipboardControlLostFocus(SequenceTree);
            if (e.Node is EmptyNode)
            {
                string labelText = (!e.CancelEdit ? e.Label.Trim() : null);
                // Keep the empty node around always
                if (!string.IsNullOrEmpty(labelText))
                {
                    // CONSIDER: Careful with document access outside ModifyDocument delegate
                    var document = DocumentUI;
                    var settings = document.Settings;
                    var backgroundProteome = settings.PeptideSettings.BackgroundProteome;
                    FastaSequence fastaSequence = null;
                    Target peptideSequence = null;
                    if (!backgroundProteome.IsNone)
                    {
                        int ichPeptideSeparator = labelText.IndexOf(FastaSequence.PEPTIDE_SEQUENCE_SEPARATOR,
                                                                    StringComparison.Ordinal);
                        string proteinName;
                        if (ichPeptideSeparator >= 0)
                        {
                            // TODO(nicksh): If they've selected a single peptide, then see if the protein has already
                            // been added, and, if so, just add the single peptide to the existing protein.
                            peptideSequence = new Target(labelText.Substring(0, ichPeptideSeparator));
                            proteinName = labelText.Substring(ichPeptideSeparator +
                                                              FastaSequence.PEPTIDE_SEQUENCE_SEPARATOR.Length);
                        }
                        else
                        {
                            proteinName = labelText;
                        }
                        fastaSequence = backgroundProteome.GetFastaSequence(proteinName);
                    }
                    string peptideGroupName = null;
                    string modifyMessage;
                    PeptideGroupDocNode oldPeptideGroupDocNode = null;
                    PeptideGroup peptideGroup = null;
                    List<PeptideDocNode> peptideDocNodes = new List<PeptideDocNode>();
                    ModificationMatcher matcher = null;
                    if (fastaSequence != null)
                    {
                        if (peptideSequence == null)
                            modifyMessage = string.Format(Resources.SkylineWindow_sequenceTree_AfterNodeEdit_Add__0__, fastaSequence.Name);
                        else
                        {
                            modifyMessage = string.Format(Resources.SkylineWindow_sequenceTree_AfterNodeEdit_Add__0__, peptideSequence);
                            oldPeptideGroupDocNode = document.FindPeptideGroup(fastaSequence);
                            if (oldPeptideGroupDocNode != null)
                            {
                                // Use the FastaSequence already in the document.
                                fastaSequence = (FastaSequence)oldPeptideGroupDocNode.Id;
                                foreach (PeptideDocNode peptideDocNode in oldPeptideGroupDocNode.Children)
                                {
                                    // If the peptide has already been added to this protein, there
                                    // is nothing to do.
                                    // CONSIDER: Should statement completion not show already added peptides?
                                    if (Equals(peptideDocNode.Peptide.Target, peptideSequence))
                                    {
                                        e.Node.Text = EmptyNode.TEXT_EMPTY;
                                        SequenceTree.Focus();
                                        return;
                                    }
                                    peptideDocNodes.Add(peptideDocNode);
                                }
                            }
                        }
                        peptideGroupName = fastaSequence.Name;
                        peptideGroup = fastaSequence;
                        if (peptideSequence == null)
                        {
                            peptideDocNodes.AddRange(fastaSequence.CreateFullPeptideDocNodes(settings, true, null));
                        }
                        else
                        {
                            peptideDocNodes.Add(fastaSequence.CreateFullPeptideDocNode(settings, peptideSequence));
                        }
                        peptideDocNodes.Sort(FastaSequence.ComparePeptides);
                    }
                    else
                    {
                        modifyMessage = string.Format(Resources.SkylineWindow_sequenceTree_AfterNodeEdit_Add__0__,labelText); // Not L10N
                        bool isExSequence = FastaSequence.IsExSequence(labelText) &&
                                            FastaSequence.StripModifications(labelText).Length >= 
                                            settings.PeptideSettings.Filter.MinPeptideLength;
                        if (isExSequence)
                        {
                            int countGroups = document.Children.Count;
                            if (countGroups > 0)
                            {
                                oldPeptideGroupDocNode = (PeptideGroupDocNode)document.Children[countGroups - 1];
                                if (oldPeptideGroupDocNode.IsNonProteomic || // Don't add a peptide to a small molecule list
                                    !oldPeptideGroupDocNode.IsPeptideList)   // Only add peptides to peptide lists, not proteins
                                    oldPeptideGroupDocNode = null;
                            }

                            if (oldPeptideGroupDocNode == null)
                            {
                                peptideGroupName = document.GetPeptideGroupId(true);
                                peptideGroup = new PeptideGroup();
                            }
                            else
                            {
                                peptideGroupName = oldPeptideGroupDocNode.Name;
                                peptideGroup = oldPeptideGroupDocNode.PeptideGroup;
                                foreach (PeptideDocNode peptideDocNode in oldPeptideGroupDocNode.Children)
                                    peptideDocNodes.Add(peptideDocNode);
                            }
                            try
                            {
                                matcher = new ModificationMatcher();
                                matcher.CreateMatches(settings, new List<string> { labelText }, Settings.Default.StaticModList, Settings.Default.HeavyModList);
                                           var strNameMatches = matcher.FoundMatches;
                                if (!string.IsNullOrEmpty(strNameMatches))
                                {
                                    if (DialogResult.Cancel == MultiButtonMsgDlg.Show(
                                        this,
                                        string.Format(TextUtil.LineSeparate(Resources.SkylineWindow_sequenceTree_AfterLabelEdit_Would_you_like_to_use_the_Unimod_definitions_for_the_following_modifications,string.Empty,
                                            strNameMatches)), Resources.OK))
                                    {
                                        e.Node.Text = EmptyNode.TEXT_EMPTY;
                                        e.Node.EnsureVisible();
                                        return;
                                    }
                                }
                                var peptideGroupDocNode = new PeptideGroupDocNode(peptideGroup, Annotations.EMPTY, peptideGroupName, null,
                                    new[] {matcher.GetModifiedNode(labelText)}, peptideSequence == null);
                                peptideDocNodes.AddRange(peptideGroupDocNode.ChangeSettings(settings, SrmSettingsDiff.ALL).Molecules);
                            }
                            catch (FormatException)
                            {
                                isExSequence = false;
                                matcher = null;
                            }
                        }
                        if(!isExSequence)
                        {
                            peptideGroupName = labelText;
                            peptideGroup = new PeptideGroup();
                        }
                    }
                    PeptideGroupDocNode newPeptideGroupDocNode;
                    if (oldPeptideGroupDocNode == null)
                    {
                        // Add a new peptide list or protein to the end of the document
                        newPeptideGroupDocNode = new PeptideGroupDocNode(peptideGroup, Annotations.EMPTY, peptideGroupName, null,
                            peptideDocNodes.ToArray(), peptideSequence == null);
                        ModifyDocument(modifyMessage, doc =>
                        {
                            var docNew = (SrmDocument) doc.Add(newPeptideGroupDocNode.ChangeSettings(doc.Settings, SrmSettingsDiff.ALL));
                            if (matcher != null)
                            {
                                var pepModsNew = matcher.GetDocModifications(docNew);
                                docNew = docNew.ChangeSettings(docNew.Settings.ChangePeptideModifications(mods => pepModsNew));
                                docNew.Settings.UpdateDefaultModifications(false);
                            }
                            return docNew;
                        });
                    }
                    else
                    {
                        // Add peptide to existing protein
                        newPeptideGroupDocNode = new PeptideGroupDocNode(oldPeptideGroupDocNode.PeptideGroup, oldPeptideGroupDocNode.Annotations, oldPeptideGroupDocNode.Name,
                            oldPeptideGroupDocNode.Description, peptideDocNodes.ToArray(), false);
                        ModifyDocument(modifyMessage, doc =>
                        {
                            var docNew = (SrmDocument) doc.ReplaceChild(newPeptideGroupDocNode);
                            if (matcher != null)
                            {
                                var pepModsNew = matcher.GetDocModifications(docNew);
                                docNew = docNew.ChangeSettings(docNew.Settings.ChangePeptideModifications(mods => pepModsNew));
                                docNew.Settings.UpdateDefaultModifications(false);
                            }
                            return docNew;
                        });
                    }
                }
                e.Node.Text = EmptyNode.TEXT_EMPTY;
                e.Node.EnsureVisible();
            }
            else if (!e.CancelEdit)
            {
                // Edit text on existing peptide list
                PeptideGroupTreeNode nodeTree = e.Node as PeptideGroupTreeNode;
                if (nodeTree != null && e.Label != null && !Equals(nodeTree.Text, e.Label))
                {
                    ModifyDocument(string.Format(Resources.SkylineWindow_sequenceTree_AfterNodeEdit_Edit_name__0__, e.Label), doc => (SrmDocument) // Not L10N
                        doc.ReplaceChild(nodeTree.DocNode.ChangeName(e.Label)));
                }
            }
            // Put the focus back on the sequence tree
            SequenceTree.Focus();
        }

        private void sequenceTree_MouseUp(object sender, MouseEventArgs e)
        {
            // Show context menu on right-click of SrmTreeNode.
            if (e.Button == MouseButtons.Right)
            {
                Point pt = e.Location;
                TreeNode nodeTree = SequenceTree.GetNodeAt(pt);
                SequenceTree.SelectedNode = nodeTree;

                ShowTreeNodeContextMenu(pt);
            }
        }

        public ContextMenuStrip ContextMenuTreeNode { get { return contextMenuTreeNode; } }
        public ToolStripMenuItem SetStandardTypeContextMenuItem { get { return setStandardTypeContextMenuItem; } }
        public ToolStripMenuItem IrtStandardContextMenuItem { get { return irtStandardContextMenuItem; } }

        public void ShowTreeNodeContextMenu(Point pt)
        {
            SequenceTree.HideEffects();
            var settings = DocumentUI.Settings;
            // Show the ratios sub-menu when there are results and a choice of
            // internal standard types.
            ratiosContextMenuItem.Visible =
                settings.HasResults &&
                    (settings.HasGlobalStandardArea ||
                    (settings.PeptideSettings.Modifications.RatioInternalStandardTypes.Count > 1 &&
                     settings.PeptideSettings.Modifications.HasHeavyModifications));
            contextMenuTreeNode.Show(SequenceTree, pt);
        }

        private void ratiosContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            ToolStripMenuItem menu = ratiosContextMenuItem;
            menu.DropDownItems.Clear();
            var standardTypes = DocumentUI.Settings.PeptideSettings.Modifications.RatioInternalStandardTypes;
            for (int i = 0; i < standardTypes.Count; i++)
            {
                SelectRatioHandler.Create(this, menu, standardTypes[i].Title, i);
            }
            if (DocumentUI.Settings.HasGlobalStandardArea)
            {
                SelectRatioHandler.Create(this, menu, ratiosToGlobalStandardsMenuItem.Text,
                    ChromInfo.RATIO_INDEX_GLOBAL_STANDARDS);
            }
        }

        private class SelectRatioHandler
        {
            protected readonly SkylineWindow _skyline;
            private readonly int _ratioIndex;

            public SelectRatioHandler(SkylineWindow skyline, int ratioIndex)
            {
                _skyline = skyline;
                _ratioIndex = ratioIndex;
            }

            public void ToolStripMenuItemClick(object sender, EventArgs e)
            {
                OnMenuItemClick();
            }

            public void Select()
            {
                OnMenuItemClick();
            }

            protected virtual void OnMenuItemClick()
            {
                _skyline.SequenceTree.RatioIndex = _ratioIndex;
                _skyline._listGraphPeakArea.ForEach(g => g.RatioIndex = _ratioIndex);
            }

            public static void Create(SkylineWindow skylineWindow, ToolStripMenuItem menu, string text, int i)
            {
                var handler = new SelectRatioHandler(skylineWindow, i);
                var item = new ToolStripMenuItem(text, null, handler.ToolStripMenuItemClick)
                { Checked = (skylineWindow.SequenceTree.RatioIndex == i) };
                menu.DropDownItems.Add(item);
            }
        }

        public void SetRatioIndex(int index)
        {
            new SelectRatioHandler(this, index).Select();
        }

        private void sequenceTree_PickedChildrenEvent(object sender, PickedChildrenEventArgs e)
        {
            SrmTreeNodeParent node = e.Node;
            ModifyDocument(string.Format(Resources.SkylineWindow_sequenceTree_PickedChildrenEvent_Pick__0__, node.ChildUndoHeading), // Not L10N
                doc => (SrmDocument)doc.PickChildren(doc.Settings, node.Path, e.PickedList, e.IsSynchSiblings));
        }

        private void sequenceTree_ItemDrag(object sender, ItemDragEventArgs e)
        {
            var listDragNodes = new List<SrmTreeNode>();

            foreach (var node in SequenceTree.SelectedNodes)
            {
                SrmTreeNode srmNode = node as SrmTreeNode;
                if (srmNode != null)
                {
                    // Only sequence nodes and peptides in peptide lists may be dragged.
                    bool allow = srmNode is PeptideGroupTreeNode;
                    if (!allow && srmNode.Model.Id is Peptide)
                    {
                        Peptide peptide = (Peptide)srmNode.Model.Id;
                        allow = (peptide.FastaSequence == null && !peptide.IsDecoy);
                    }
                    if (!allow || (listDragNodes.Count > 0 && srmNode.GetType() != listDragNodes[0].GetType()))
                        return;

                    listDragNodes.Add(srmNode);
                }
            }

            if (listDragNodes.Count != 0)
            {
                var dataObj = new DataObject();
                if (listDragNodes.First() is PeptideTreeNode)
                    dataObj.SetData(typeof(PeptideTreeNode), listDragNodes);
                else
                    dataObj.SetData(typeof(PeptideGroupTreeNode), listDragNodes);
                
                DoDragDrop(dataObj, DragDropEffects.Move);              
            }
        }

        private void sequenceTree_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = (GetDropTarget(e) != null ? DragDropEffects.Move : DragDropEffects.None);
        }

        private void sequenceTree_DragOver(object sender, DragEventArgs e)
        {
            TreeNode node = GetDropTarget(e);
            if (node == null)
                e.Effect = DragDropEffects.None;
            else
            {
                e.Effect = DragDropEffects.Move;
                SequenceTree.SelectedNode = node;
            }

            // Auto-scroll if near the top or bottom edge.
            Point ptView = SequenceTree.PointToClient(new Point(e.X, e.Y));
            if (ptView.Y < 10)
            {
                TreeNode nodeTop = SequenceTree.TopNode;
                if (nodeTop != null && nodeTop.PrevVisibleNode != null)
                    SequenceTree.TopNode = nodeTop.PrevVisibleNode;
            }
            if (ptView.Y > SequenceTree.Bottom - 10)
            {
                TreeNode nodeTop = SequenceTree.TopNode;
                if (nodeTop != null && nodeTop.NextVisibleNode != null)
                    SequenceTree.TopNode = nodeTop.NextVisibleNode;
            }
        }

        private void sequenceTree_DragDrop(object sender, DragEventArgs e)
        {
            List<SrmTreeNode> nodeSources = (List<SrmTreeNode>) e.Data.GetData(typeof(PeptideGroupTreeNode)) ??
                (List<SrmTreeNode>) e.Data.GetData(typeof(PeptideTreeNode));

            if (nodeSources == null)
                return;

            var nodeSourcesArray = nodeSources.ToArray();

            var selectedPaths = new List<IdentityPath>();
            var sourcePaths = new IdentityPath[nodeSourcesArray.Length];

            SrmTreeNode nodeDrop = GetSrmTreeNodeAt(e.X, e.Y);
            IdentityPath pathTarget = SrmTreeNode.GetSafePath(nodeDrop);
            
            for (int i = 0; i < nodeSourcesArray.Length; i++)
            {
                var nodeSource = nodeSourcesArray[i];
                // No work for dropping on the start node.
                if (ReferenceEquals(nodeDrop, nodeSource))
                    return;

                IdentityPath pathSource = SrmTreeNode.GetSafePath(nodeSource);
                
                // Dropping inside self also requires no work.
                if (pathSource.Length < pathTarget.Length &&
                    Equals(pathSource, pathTarget.GetPathTo(pathSource.Length - 1)))
                    return;

                sourcePaths[i] = pathSource;
            }

            // Reselect the original paths, so they will be stored on the undo bufferS
            SequenceTree.SelectedPath = sourcePaths.First();
            SequenceTree.SelectedPaths = sourcePaths;

            ModifyDocument(Resources.SkylineWindow_sequenceTree_DragDrop_Drag_and_drop, doc =>
                                                {
                                                    foreach (IdentityPath pathSource in sourcePaths)
                                                    {
                                                        IdentityPath selectPath;
                                                        doc = doc.MoveNode(pathSource, pathTarget, out selectPath);
                                                        selectedPaths.Add(selectPath);
                                                    }
                                                    return doc;
                                                });

            SequenceTree.SelectedPath = selectedPaths.First();
            SequenceTree.SelectedPaths = selectedPaths;
            SequenceTree.Invalidate();
        }

        private TreeNode GetDropTarget(DragEventArgs e)
        {
            bool isGroup = e.Data.GetDataPresent(typeof(PeptideGroupTreeNode).FullName);
            bool isPeptide = e.Data.GetDataPresent(typeof(PeptideTreeNode).FullName);
            if (isGroup)
            {
                TreeNode node = GetTreeNodeAt(e.X, e.Y);
                // If already at the root, then drop on this node.
                if (node == null || node.Parent == null)
                    return node;
                // Otherwise, walk to root, and drop on next sibling of
                // containing node.
                while (node.Parent != null)
                    node = node.Parent;
                return node.NextNode;
            }
            if (isPeptide)
            {
                SrmTreeNode nodeTree = GetSrmTreeNodeAt(e.X, e.Y);
                // Allow drop of peptide on peptide list node itself
                var nodePepGroupTree = nodeTree as PeptideGroupTreeNode;
                if (nodePepGroupTree != null)
                {
                    var nodePeptideGroup = nodePepGroupTree.DocNode;
                    return nodePeptideGroup.Id is FastaSequence || nodePeptideGroup.IsDecoy
                        ? null
                        : nodeTree;
                }

                // Allow drop on a peptide in a peptide list
                var nodePepTree = nodeTree as PeptideTreeNode;
                if (nodePepTree != null)
                {
                    var nodePep = nodePepTree.DocNode;
                    return (nodePep.Peptide.FastaSequence != null || nodePep.IsDecoy
                        ? null
                        : nodePepTree);
                }

                // Otherwise allow drop on children of peptides in peptide lists
                while (nodeTree != null)
                {
                    nodePepTree = nodeTree as PeptideTreeNode;
                    if (nodePepTree != null)
                    {
                        var nodePep = nodePepTree.DocNode;
                        if (nodePep.Peptide.FastaSequence != null || nodePep.IsDecoy)
                            return null;

                        return nodePepTree.NextNode ?? nodePepTree.Parent.NextNode;
                    }
                    nodeTree = nodeTree.Parent as SrmTreeNode;
                }
            }
            return null;
        }

        private SrmTreeNode GetSrmTreeNodeAt(int x, int y)
        {
            return GetTreeNodeAt(x, y) as SrmTreeNode;
        }

        private TreeNode GetTreeNodeAt(int x, int y)
        {
            Point ptView = SequenceTree.PointToClient(new Point(x, y));
            return SequenceTree.GetNodeAt(ptView);
        }

        private void SetResultIndexOnGraphs(IList<GraphSummary> graphs, bool useOriginalIndex)
        {
            foreach (var g in graphs.Where(g => g.ResultsIndex != ComboResults.SelectedIndex))
            {
                int origIndex = useOriginalIndex ? g.OriginalResultsIndex : -1;
                g.SetResultIndexes(ComboResults.SelectedIndex, origIndex);
            }
        }

        private void comboResults_SelectedIndexChanged(object sender, EventArgs e)
        {
            string name = SelectedGraphChromName;
            if (name == null)
                return;

            // Update the summary graphs if necessary.
            SetResultIndexOnGraphs(_listGraphRetentionTime, true);
            SetResultIndexOnGraphs(_listGraphPeakArea, false);
            SetResultIndexOnGraphs(_listGraphMassError, false);

            var liveResultsGrid = (LiveResultsGrid)_resultsGridForm;
            if (null != liveResultsGrid)
            {
                liveResultsGrid.SetReplicateIndex(ComboResults.SelectedIndex);
            }
            if (null != _calibrationForm)
            {
                _calibrationForm.UpdateUI(true);
            }

            if (SequenceTree.ResultsIndex != ComboResults.SelectedIndex)
            {
                // Show the right result set in the tree view.
                SequenceTree.ResultsIndex = ComboResults.SelectedIndex;

                // Make sure the graphs for the result set are visible.
                if (GetGraphChrom(name) != null)
                {
                    bool focus = ComboResults.Focused;

                    ShowGraphChrom(name, true);

                    if (focus)
                        // Keep focus on the combo box
                        ComboResults.Focus();
                }

                if (Program.MainToolService != null)
                    Program.MainToolService.SendSelectionChange();

//                UpdateReplicateMenuItems(DocumentUI.Settings.HasResults);
            }
        }

        #endregion // SequenceTree events

        #region Status bar

        private void UpdateNodeCountStatus()
        {
            var selectedPath = SelectedPath;
            int[] positions;
            if (selectedPath != null &&
                SequenceTree != null &&
                !SequenceTree.IsInUpdateDoc &&
                !SequenceTree.IsInsertPath(selectedPath))
            {
                positions = DocumentUI.GetNodePositions(SelectedPath);
            }
            else
            {
                positions = new int[DocumentUI.Depth];
                for (int i = 0; i < positions.Length; i++)
                    positions[i] = -1;
            }

            bool isProtOnly = DocumentUI.DocumentType == SrmDocument.DOCUMENT_TYPE.proteomic;
            UpdateStatusCounter(statusSequences, positions, SrmDocument.Level.MoleculeGroups, isProtOnly ? "prot" : "list"); // Not L10N
            UpdateStatusCounter(statusPeptides, positions, SrmDocument.Level.Molecules, isProtOnly ? "pep" : "mol"); // Not L10N
            UpdateStatusCounter(statusPrecursors, positions, SrmDocument.Level.TransitionGroups, "prec"); // Not L10N
            UpdateStatusCounter(statusIons, positions, SrmDocument.Level.Transitions, "tran"); // Not L10N
        }

        private void UpdateStatusCounter(ToolStripItem label, int[] positions, SrmDocument.Level level, string text)
        {
            int l = (int)level;
            int count = DocumentUI.GetCount(l);
            string tag;
            if (count == 0)
                tag = count.ToString(CultureInfo.InvariantCulture);
            else
            {
                int pos = 0;
                if (positions != null && l < positions.Length)
                    pos = positions[l];

                if (pos != -1)
                    pos++;
                else
                    pos = count;
                tag = string.Format("{0:#,0}", pos) + "/" + string.Format("{0:#,0}", count); // Not L10N
            }

            if (!Equals(label.Tag, tag))
            {
                label.Text = TextUtil.SpaceSeparate(tag, text);
                label.Tag = tag;
            }
        }

        bool IProgressMonitor.IsCanceled
        {
            get
            {
                // CONSIDER: Add a generic cancel button to the status bar that allows cancelling operations with progress?
                return _closing;    // Once the main window is closing tell anything listening for progress to cancel
            }
        }

        UpdateProgressResponse IProgressMonitor.UpdateProgress(IProgressStatus status)
        {
            var args = new ProgressUpdateEventArgs(status);
            UpdateProgress(this, args);
            return args.Response;
        }

        public bool HasUI { get { return true; } }

        private void UpdateProgress(object sender, ProgressUpdateEventArgs e)
        {
            var status = e.Progress;
            var multiStatus = status as MultiProgressStatus;
            if (multiStatus != null && multiStatus.IsEmpty)
            {
                Assume.Fail("Setting empty multi-status");  // Not L10N
            }
            var final = status.IsFinal;

            bool first;
            lock (_listProgress)
            {
                // Find the status being updated in the list
                Assume.IsNotNull(status);
                Assume.IsFalse(_listProgress.Any(s => s == null));
                int i = _listProgress.IndexOf(s => ReferenceEquals(s.Id, status.Id));
                // If final, remove the status if present
                if (final)
                {
                    if (i != -1)
                        _listProgress.RemoveAt(i);
                }
                // Otherwise, if present update the status
                else if (i != -1)
                {
                    _listProgress[i] = status;
                }
                // Or add it if not
                else
                {
                    i = _listProgress.Count;
                    _listProgress.Add(status);
                }
                first = i == 0;
            }

            // If the status is first in the queue and it is beginning, initialize
            // the progress UI.
            bool begin = status.IsBegin || (!final && !_timerProgress.Enabled);
            if (first && begin)
            {
                RunUIAction(BeginProgressUI, e);
            }
            // If it is a final state, and it is being shown, or there was an error
            // make sure user sees the change.
            else if (final)
            {
                // Import progress needs to know about this status immediately.  It might be gone by
                // the time the update progress interval comes around next time.
                if (ImportingResultsWindow != null && status is MultiProgressStatus)
                    RunUIAction(() => UpdateImportProgress(status as MultiProgressStatus));

                // Only wait for an error, since it is expected that e
                // may be modified by return of this function call
                if (status.IsError)
                    RunUIAction(CompleteProgressUI, e);
                else if (first)
                    RunUIActionAsync(CompleteProgressUI, e);
            }
        }

        private void BeginProgressUI(ProgressUpdateEventArgs e)
        {
            _timerProgress.Start();
            UpdateProgressUI();
        }

        private void CompleteProgressUI(ProgressUpdateEventArgs e)
        {
            // If completed successfully, make sure the user sees 100% by setting
            // 100 and then waiting for the next timer tick to clear the progress
            // indicator.
            var status = e.Progress; 
            if (status.IsComplete)
            {
                statusProgress.Value = 100;
            }
            else
            {
                // If an error, show the message before removing status
                if (status.IsError)
                    ShowProgressErrorUI(e);

                // Update the progress UI immediately
                UpdateProgressUI();
            }
        }

        private void ShowProgressErrorUI(ProgressUpdateEventArgs e)
        {
            var x = e.Progress.ErrorException;

            var multiException = x as MultiException;
            if (multiException != null)
            {
                // The next update to the UI will display errors.
                if (ImportingResultsWindow == null)
                    ImportingResultsWindow = new AllChromatogramsGraph { Owner = this, ChromatogramManager = _chromatogramManager };
                // Add the error to the ImportingResultsWindow before calling "ShowAllChromatogramsGraph" 
                // because "ShowAllChromatogramsGraph" may destroy the window if the job is done and there are
                // no errors yet.
                ImportingResultsWindow.UpdateStatus((MultiProgressStatus) e.Progress);
                ShowAllChromatogramsGraph();
                return;
            }

            var message = ExceptionUtil.GetMessage(x);

            // Drill down to see if the innermost exception was an out-of-memory exception.
            var innerException = x;
            while (innerException.InnerException != null)
                innerException = innerException.InnerException;
            if (innerException is OutOfMemoryException)
            {
                message = string.Format(Resources.SkylineWindow_CompleteProgressUI_Ran_Out_Of_Memory, Program.Name);
                if (!Install.Is64Bit && Environment.Is64BitOperatingSystem)
                {
                    message += string.Format(Resources.SkylineWindow_CompleteProgressUI_version_issue, Program.Name);
                }
            }

            // Try to show the error message, but the SkylineWindow may be disposed by the test thread, so ignore the exception.
            try
            {
                // TODO: Get topmost window
                MessageDlg.ShowWithException(this, message, x);
            }
            catch
            {
                // ignored
            }
        }

        private void UpdateProgressUI(object sender = null, EventArgs e = null)
        {
            if (statusStrip.IsDisposed)
                return;

            IProgressStatus status = null;
            MultiProgressStatus multiStatus = null;
            lock (_listProgress)
            {
                if (_listProgress.Count != 0)
                {
                    status = _listProgress[0];
                    multiStatus = _listProgress.LastOrDefault(s => s is MultiProgressStatus) as MultiProgressStatus;
                }
            }

            // First deal with AllChromatogramsGraph window
            if (!Program.NoAllChromatogramsGraph)
            {
                // Update chromatogram graph if we are importing a data file.
                if (multiStatus != null)
                {
                    UpdateImportProgress(multiStatus);
                }
                else if (ImportingResultsWindow != null)
                {
                    if (!ImportingResultsWindow.IsUserCanceled)
                        Settings.Default.AutoShowAllChromatogramsGraph = ImportingResultsWindow.Visible;
                    ImportingResultsWindow.Finish();
                    if (!ImportingResultsWindow.HasErrors && Settings.Default.ImportResultsAutoCloseWindow)
                        DestroyAllChromatogramsGraph();
                }
            }

            // Next deal with status bar, which may also show status for MultiProgressStatus objects
            if (status == null)
            {
                statusProgress.Visible = false;
                UpdateTaskbarProgress(TaskbarProgress.TaskbarStates.NoProgress, null);
                buttonShowAllChromatograms.Visible = false;
                statusGeneral.Text = Resources.SkylineWindow_UpdateProgressUI_Ready;
                _timerProgress.Stop();
            }
            else
            {
                // Update the status bar with the first progress status.
                statusProgress.Value = status.PercentComplete;
                statusProgress.Visible = true;
                UpdateTaskbarProgress(TaskbarProgress.TaskbarStates.Normal, status.PercentComplete);
                statusGeneral.Text = status.Message;
            }
        }

        public void UpdateTaskbarProgress(TaskbarProgress.TaskbarStates state, int? percentComplete)
        {
            _taskbarProgress.SetState(Handle, state);
            if (percentComplete.HasValue)
            {
                _taskbarProgress.SetValue(Handle, percentComplete.Value, 100);
            }
        }

        private void UpdateImportProgress(MultiProgressStatus multiStatus)
        {
            bool showable = !multiStatus.IsFinal || multiStatus.IsError || multiStatus.HasWarnings;
            buttonShowAllChromatograms.Visible = statusProgress.Visible = showable;
            if (ImportingResultsWindow == null && showable)
            {
                Assume.IsFalse(multiStatus.IsEmpty);    // Should never be starting results window with empty status
                ImportingResultsWindow = new AllChromatogramsGraph { Owner = this, ChromatogramManager = _chromatogramManager };
                if (Settings.Default.AutoShowAllChromatogramsGraph)
                    ImportingResultsWindow.Show(this);
            }
            if (ImportingResultsWindow != null)
                ImportingResultsWindow.UpdateStatus(multiStatus);
        }

        public void ShowAllChromatogramsGraph()
        {
            if (ImportingResultsWindow != null)
            {
                if (ImportingResultsWindow.Visible)
                    ImportingResultsWindow.Activate();
                else
                    ImportingResultsWindow.Show(this);
                UpdateProgressUI(); // Sets selected control
            }
        }

        private void buttonShowAllChromatograms_ButtonClick(object sender, EventArgs e)
        {
            ShowAllChromatogramsGraph();
        }

        Point INotificationContainer.NotificationAnchor
        {
            get { return new Point(Left, statusStrip.Visible ? Bottom - statusStrip.Height : Bottom); }
        }

        LibraryManager ILibraryBuildNotificationContainer.LibraryManager
        {
            get { return _libraryManager; }
        }

        public Action<LibraryManager.BuildState, bool> LibraryBuildCompleteCallback
        {
            get { return _libraryBuildNotificationHandler.LibraryBuildCompleteCallback; }
        }

        public void RemoveLibraryBuildNotification()
        {
            _libraryBuildNotificationHandler.RemoveLibraryBuildNotification();
        }

        public bool StatusContains(string format)
        {
            // Since status is updated on a timer, first check if there is any progress status
            // and use the latest, if there is. Otherwise, use the status bar text.
            string start = format.Split('{').First();
            string end = format.Split('}').Last();
            lock (_listProgress)
            {
                foreach (var progressStatus in _listProgress)
                {
                    if (progressStatus.Message.Contains(start) && progressStatus.Message.Contains(end))
                        return true;
                }
            }
            return statusGeneral.Text.Contains(start) && statusGeneral.Text.Contains(end);
        }

        public int StatusBarHeight { get { return statusGeneral.Height; } }

        #endregion

        private void SkylineWindow_Move(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Normal)
                Settings.Default.MainWindowLocation = Location;
            Settings.Default.MainWindowMaximized =
                (WindowState == FormWindowState.Maximized);
        }

        private void SkylineWindow_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Normal)
                Settings.Default.MainWindowSize = Size;
            Settings.Default.MainWindowMaximized =
                (WindowState == FormWindowState.Maximized);
        }

        private void RunUIActionAsync(Action act)
        {
            if (InvokeRequired)
                BeginInvoke(act);
            else
                act();
        }

        private void RunUIAction(Action act)
        {
            if (InvokeRequired)
                Invoke(act);
            else
                act();
        }

        private void RunUIActionAsync<TArg>(Action<TArg> act, TArg arg)
        {
            if (InvokeRequired)
                BeginInvoke(act, arg);
            else
                act(arg);
        }

        private void RunUIAction<TArg>(Action<TArg> act, TArg arg)
        {
            if (InvokeRequired)
                Invoke(act, arg);
            else
                act(arg);
        }

        #region Implementation of IToolMacroProvider

        public string SelectedPrecursor
        {
            get
            {
                var selprec = SequenceTree.GetNodeOfType<TransitionGroupTreeNode>();
                if (selprec != null)
                {
                    return selprec.ModifiedSequence +
                           Transition.GetChargeIndicator(selprec.DocNode.TransitionGroup.PrecursorAdduct);
                }
                return null;                
            }            
        }

        public string ResultNameCurrent
        {
            get
            {
                return ComboResults != null && ComboResults.SelectedItem != null
                           ? ComboResults.SelectedItem.ToString()
                           : null;
        }
        }



        public string SelectedPeptideSequence
        {
            get
            {
                var peptTreeNode = SequenceTree.GetNodeOfType<PeptideTreeNode>(); 
                return peptTreeNode != null ? peptTreeNode.DocNode.Peptide.Target.ToString() : null;
            }
        }

        public string SelectedProteinName
        {
            get
            {
                var protTreeNode = SequenceTree.GetNodeOfType<PeptideGroupTreeNode>();
                return protTreeNode != null ? protTreeNode.DocNode.Name : null;
            }
        }

        public string FindProgramPath(ProgramPathContainer programPathContainer)
        {
            AutoResetEvent wh = new AutoResetEvent(false);
            DialogResult result = DialogResult.No;
            RunUIAction(() =>
                {
                    using (var dlg = new LocateFileDlg(programPathContainer))
                    {
                        result = dlg.ShowDialog(this);
                    }
                    wh.Set();
                });
            wh.WaitOne();
            wh.Dispose();
            if (result == DialogResult.OK)
            {
                return Settings.Default.ToolFilePaths.ContainsKey(programPathContainer) ? Settings.Default.ToolFilePaths[programPathContainer] : string.Empty;
            }
            else
            {
                return null;
            }
        }

        // currently a work-around to get an R-installer
        public string InstallProgram(ProgramPathContainer programPathContainer, ICollection<ToolPackage> packages, string pathToPackageInstallScript)
        {
            if (programPathContainer.ProgramName.Equals("R")) // Not L10N
            {
                bool installed = RUtil.CheckInstalled(programPathContainer.ProgramVersion);
                if (!installed || packages.Count != 0)
                {
                    ICollection<ToolPackage> PackagesToInstall;
                    if (!installed)
                    {
                        PackagesToInstall = packages;
                    }
                    else
                    {
                        PackagesToInstall = RUtil.WhichPackagesToInstall(packages, RUtil.FindRProgramPath(programPathContainer.ProgramVersion));
                    }
                    if (installed && PackagesToInstall.Count == 0)
                        return RUtil.FindRProgramPath(programPathContainer.ProgramVersion);
                    
                    // we will need the immediate window to show output for package installation
                    if (PackagesToInstall.Count != 0)
                    {
                        ShowImmediateWindow();
                    }
                    
                    using (var dlg = new RInstaller(programPathContainer, PackagesToInstall, _skylineTextBoxStreamWriterHelper, pathToPackageInstallScript))
                    {
                        var result = dlg.ShowDialog(this);
                        if (result == DialogResult.Cancel || result == DialogResult.No)
                            return null;
                    }
                }
                return RUtil.FindRProgramPath(programPathContainer.ProgramVersion);
            }
            else if (programPathContainer.ProgramName.Equals("Python")) // Not L10N
            {
                if (!PythonUtil.CheckInstalled(programPathContainer.ProgramVersion) || packages.Count != 0)
                {
                    if (packages.Count != 0)
                    {
                        ShowImmediateWindow();
                    }
                    
                    // No versioning of packages for Python yet. 
                    // Here we just ignore all the versions attached to packages. 
                    IEnumerable<string> pythonPackages = packages.Select(p => p.Name);

                    using (var dlg = new PythonInstaller(programPathContainer, pythonPackages, _skylineTextBoxStreamWriterHelper))
                    {
                        if (dlg.ShowDialog(this) == DialogResult.Cancel)
                            return null;
                    }
                }
                return PythonUtil.GetProgramPath(programPathContainer.ProgramVersion);
            } 
            else 
            {
                return FindProgramPath(programPathContainer);
            }
        }

        #endregion

        public bool IsPasteKeys(Keys keys)
        {
            return Equals(pasteMenuItem.ShortcutKeys, keys);
        }
        
        private void associateFASTAMenuItem_Click(object sender, EventArgs e)
        {
            ShowAssociateProteinsDlg();
        }

        public void ShowAssociateProteinsDlg()
        {
            using (var associateFasta = new AssociateProteinsDlg(this))
            {
                associateFasta.ShowDialog(this);
            }
        }

        private string _originalProteinsText;
        private string _originalPeptidesText;

        private void expandAllMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            _originalProteinsText = _originalProteinsText ?? expandProteinsMenuItem.Text;
            _originalPeptidesText = _originalPeptidesText ?? expandPeptidesMenuItem.Text;

            if (DocumentUI.DocumentType == SrmDocument.DOCUMENT_TYPE.proteomic)
            {
                expandProteinsMenuItem.Text = _originalProteinsText;
                expandPeptidesMenuItem.Text = _originalPeptidesText;
            }
            else
            {
                expandProteinsMenuItem.Text = Resources.SkylineWindow_expandAllMenuItem_DropDownOpening__Lists;
                expandPeptidesMenuItem.Text = Resources.SkylineWindow_expandAllMenuItem_DropDownOpening__Molecules;
            }
        }

        private void collapseAllToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            _originalProteinsText = _originalProteinsText ?? expandProteinsMenuItem.Text;
            _originalPeptidesText = _originalPeptidesText ?? expandPeptidesMenuItem.Text;

            if (DocumentUI.DocumentType == SrmDocument.DOCUMENT_TYPE.proteomic)
            {
                collapseProteinsMenuItem.Text = _originalProteinsText;
                collapsePeptidesMenuItem.Text = _originalPeptidesText;
            }
            else
            {
                collapseProteinsMenuItem.Text = Resources.SkylineWindow_expandAllMenuItem_DropDownOpening__Lists;
                collapsePeptidesMenuItem.Text = Resources.SkylineWindow_expandAllMenuItem_DropDownOpening__Molecules;
            }
        }

        public void SelectElement(ElementRef elementRef)
        {
            var document = Document;
            var measuredResults = document.Settings.MeasuredResults;
            var resultFileRef = elementRef as ResultFileRef;
            if (resultFileRef != null)
            {
                elementRef = resultFileRef.Parent;
            }
            var replicateRef = elementRef as ReplicateRef;
            if (replicateRef != null)
            {
                if (measuredResults == null)
                {
                    return;
                }
                var index = measuredResults.Chromatograms.IndexOf(chromSet => chromSet.Name == replicateRef.Name);
                if (index >= 0)
                {
                    SelectedResultsIndex = index;
                }
                return;
            }
            var bookmark = new Bookmark();
            var resultRef = elementRef as ResultRef;
            if (resultRef != null)
            {
                var chromFileInfo = resultRef.FindChromFileInfo(document);
                if (chromFileInfo != null)
                {
                    bookmark = bookmark.ChangeChromFileInfoId(chromFileInfo.FileId)
                        .ChangeOptStep(resultRef.OptimizationStep);
                }
                elementRef = elementRef.Parent;
            }
            var nodeRef = elementRef as NodeRef;
            if (nodeRef != null)
            {
                var identityPath = nodeRef.ToIdentityPath(document);
                if (identityPath == null)
                {
                    return;
                }
                bookmark = bookmark.ChangeIdentityPath(identityPath);
                NavigateToBookmark(bookmark);
            }
        }
    }
}

