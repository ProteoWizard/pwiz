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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using DigitalRune.Windows.Docking;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Find;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Controls;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using PasteFormat = pwiz.Skyline.EditUI.PasteFormat;
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
        private SrmDocument _document;
        private SrmDocument _documentUI;
        private int _savedVersion;
        private bool _closing;
        private readonly UndoManager _undoManager;
        private readonly UndoRedoButtons _undoRedoButtons;
        private readonly LibraryManager _libraryManager;
        private readonly LibraryBuildNotificationHandler _libraryBuildNotificationHandler;
        private readonly BackgroundProteomeManager _backgroundProteomeManager;
        private readonly ChromatogramManager _chromatogramManager;
        private readonly IrtDbManager _irtDbManager;
        private readonly RetentionTimeManager _retentionTimeManager;

        public event EventHandler<DocumentChangedEventArgs> DocumentChangedEvent;
        public event EventHandler<DocumentChangedEventArgs> DocumentUIChangedEvent;

        private List<ProgressStatus> _listProgress;
        private readonly Timer _timerProgress;
        private readonly Timer _timerGraphs;

        /// <summary>
        /// Constructor for the main window of the Skyline program.
        /// </summary>
        public SkylineWindow()
        {
            InitializeComponent();

            _undoManager = new UndoManager(this);
            _undoRedoButtons = new UndoRedoButtons(_undoManager,
                undoMenuItem, undoToolBarButton,
                redoMenuItem, redoToolBarButton);
            _undoRedoButtons.AttachEventHandlers();

            _graphSpectrumSettings = new GraphSpectrumSettings(UpdateSpectrumGraph);

            _listProgress = new List<ProgressStatus>();
            _timerProgress = new Timer { Interval = 750 };
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
            _chromatogramManager = new ChromatogramManager();
            _chromatogramManager.ProgressUpdateEvent += UpdateProgress;
            _chromatogramManager.Register(this);
            _irtDbManager = new IrtDbManager();
            _irtDbManager.ProgressUpdateEvent += UpdateProgress;
            _irtDbManager.Register(this);
            _retentionTimeManager = new RetentionTimeManager();
            _retentionTimeManager.ProgressUpdateEvent += UpdateProgress;
            _retentionTimeManager.Register(this);

            // Get placement values before changing anything.
            Point location = Settings.Default.MainWindowLocation;
            Size size = Settings.Default.MainWindowSize;
            bool maximize = Settings.Default.MainWindowMaximized;

            // Restore window placement.
            if (!location.IsEmpty)
            {
                StartPosition = FormStartPosition.Manual;
                Location = location;
            }
            if (!size.IsEmpty)
                Size = size;
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
                throw new InvalidOperationException("Must have a window handle to begin processing.");

            // Load any file the user may have double-clicked on to run this application
            bool newFile = true;
            var activationArgs = AppDomain.CurrentDomain.SetupInformation.ActivationArguments;
            string[] args = (activationArgs != null ? activationArgs.ActivationData : null);
            if (args != null && args.Length != 0)
            {
                try
                {
                    Uri uri = new Uri(args[0]);
                    if (!uri.IsFile)
                        throw new UriFormatException("The URI " + uri + " is not a file.");

                    string pathOpen = Uri.UnescapeDataString(uri.AbsolutePath);

                    // If the file chosen was the cache file, open its associated document.)
                    if (Equals(Path.GetExtension(pathOpen), ChromatogramCache.EXT))
                        pathOpen = Path.ChangeExtension(pathOpen, SrmDocument.EXT);
                    // Handle direct open from UNC path names
                    if (!string.IsNullOrEmpty(uri.Host))
                        pathOpen = "//" + uri.Host + pathOpen;

                    newFile = !OpenFile(pathOpen);
                }
                catch (UriFormatException)
                {
                    MessageBox.Show(this, "Invalid file specified.", Program.Name);
                }
            }

            // If no file was loaded, create a new one.
            if (newFile)
            {
                // CONSIDER: Reload last document?
                NewDocument();
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            if (ApplicationDeployment.IsNetworkDeployed)
            {
                try
                {
                    var appDeployment = ApplicationDeployment.CurrentDeployment;
                    if (appDeployment != null)
                    {
                        appDeployment.CheckForUpdateAsync();
                    }
                }
                catch (DeploymentDownloadException)
                {
                }
                catch (InvalidDeploymentException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }

            base.OnHandleCreated(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _timerGraphs.Dispose();
            _timerProgress.Dispose();

            base.OnClosed(e);
        }

        void IDocumentContainer.Listen(EventHandler<DocumentChangedEventArgs> listener)
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
                    throw new InvalidOperationException("The DocumentUI property may only be accessed on the UI thread.");

                return _documentUI;
            }
        }

        /// <summary>
        /// The currently saved location of the document
        /// </summary>
        public string DocumentFilePath { get; set; }

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

        /// <summary>
        /// True if the active document has been modified.
        /// </summary>
        public bool Dirty
        {
            get
            {
                return _documentUI != null && _savedVersion != _documentUI.RevisionIndex;
            }
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
                // If this is not happening inside an undoable action, and the
                // document is not currently dirty, make sure it stays that way.
                // Otherwise, try to undo to a clean document will be impossible.
                // This should only happen when the new document represents the
                // fulfilling of an IOU on the current document (e.g. loading
                // spectral libraries)
                else if (!_undoManager.Recording && _savedVersion == documentPrevious.RevisionIndex)
                    _savedVersion = _documentUI.RevisionIndex;

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
            bool docIdChanged = false;
            if (documentPrevious != null)
            {
                settingsOld = documentPrevious.Settings;
                docIdChanged = !ReferenceEquals(DocumentUI.Id, documentPrevious.Id);
            }

            if (_alignToReplicate >= 0)
            {
                if (!settingsNew.HasResults || _alignToReplicate >= settingsNew.MeasuredResults.Chromatograms.Count)
                {
                    _alignToReplicate = -1;
                }
            }
            // Update results combo UI
            if (_sequenceTreeForm != null)
                _sequenceTreeForm.UpdateResultsUI(settingsNew, settingsOld);

            // Fire event to allow listeners to update.
            // This has to be done before the graph UI updates, since it updates
            // the tree, and the graph UI depends on the tree being up to date.
            if (DocumentUIChangedEvent != null)
                DocumentUIChangedEvent(this, new DocumentChangedEventArgs(documentPrevious));

            // Update graph pane UI
            UpdateGraphUI(settingsOld, docIdChanged);

            // Update title and status bar.
            UpdateTitle();
            UpdateNodeCountStatus();

            integrateAllMenuItem.Checked = settingsNew.TransitionSettings.Integration.IsIntegrateAll;
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

            var docResult = Interlocked.CompareExchange(ref _document, docNew, docOriginal);
            if (!ReferenceEquals(docResult, docOriginal))
                return false;

            if (DocumentChangedEvent != null)
                DocumentChangedEvent(this, new DocumentChangedEventArgs(docOriginal));

            RunUIAction(UpdateDocumentUI);

            return true;
        }

        public void ModifyDocument(string description, Func<SrmDocument, SrmDocument> act)
        {
            try
            {
                using (var undo = BeginUndo(description))
                {
                    SrmDocument docOriginal;
                    SrmDocument docNew;
                    do
                    {
                        docOriginal = Document;
                        docNew = act(docOriginal);

                        // If no change has been made, return without committing a
                        // new undo record to the undo stack.
                        if (ReferenceEquals(docOriginal, docNew))
                            return;
                    }
                    while (!SetDocument(docNew, docOriginal));
                    
                    undo.Commit();
                }
            }
            catch (IdentityNotFoundException)
            {
                MessageBox.Show(this, "Failure attempting to modify the document.", Program.Name);
            }
            catch (InvalidDataException x)
            {
                MessageDlg.Show(this, string.Format("Failure attempting to modify the document.\n{0}", x.Message));
            }
            catch (IOException x)
            {
                MessageDlg.Show(this, string.Format("Failure attempting to modify the document.\n{0}", x.Message));
            }
        }

        public void SwitchDocument(SrmDocument document, string pathOnDisk)
        {
            // Some hoops are jumped through here to make sure the
            // document path is correct for listeners on the Document
            // at the time the document change event notifications
            // are fired.

            // CONSIDER: This is not strictly synchronization safe, since
            //           it still leaves open the possibility that a thread
            //           will get the wrong path for the current document.
            //           It may really be necessary to synchronize access
            //           to DocumentFilePath.
            string pathPrevious = DocumentFilePath;
            DocumentFilePath = pathOnDisk;

            try
            {
                RestoreDocument(document);

                _savedVersion = document.RevisionIndex;

                SetActiveFile(pathOnDisk);
            }
            catch (Exception)
            {
                DocumentFilePath = pathPrevious;
                throw;
            }
        }

        public IUndoTransaction BeginUndo(string description)
        {
            return _undoManager.BeginTransaction(description);
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
                throw new InvalidOperationException("Failed to restore document");

            return docReplaced;
        }

        #region Implementation of IUndoable

        IUndoState IUndoable.GetUndoState()
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

            public string ResultNameCurrent
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
                string dirtyMark = (Dirty ? " *" : "");
                Text = string.Format("{0} - {1}{2}", Program.Name, Path.GetFileName(filePath), dirtyMark);
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
            if (!CheckSaveDocument())
            {
                e.Cancel = true;
                return;
            }

            if (!Program.NoSaveSettings)
            {
                try
                {
                    Settings.Default.Save();
                }
                catch (Exception)
                {
                    MessageDlg.Show(this, "An unexpected error has prevented global settings changes from this session from being saved.");
                }
            }

            _closing = true;

            base.OnClosing(e);
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
            manageUniquePeptidesMenuItem.Enabled = enabled;
            var nodePepTree = SequenceTree.GetNodeOfType<PeptideTreeNode>();
            modifyPeptideMenuItem.Enabled = nodePepTree != null;

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
        }

        private bool StatementCompletionAction(Action<TextBox> act)
        {
            var completionEditBox = SequenceTree.StatementCompletionEditBox;
            if (completionEditBox == null)
                return false;

            act(completionEditBox.TextBox);
            return true;
        }

        private void cutMenuItem_Click(object sender, EventArgs e)
        {
            if (StatementCompletionAction(textBox => textBox.Cut()))
                return;

            copyMenuItem_Click(sender, e);
            deleteMenuItem_Click(sender, e);
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
                        "<br>\r\n", "&nbsp;&nbsp;&nbsp;&nbsp;", levels, lineBreaks);
                string providerText = (string) data.GetData("Text");
                if (providerText != null)
                    AppendClipboardText(textSb, providerText, "\r\n", "    ", levels, lineBreaks);

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
            sbData.Append("proteins-selected=").Append(selectionContainsProteins).AppendLine();
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
                MessageDlg.Show(this, ClipboardHelper.GetOpenClipboardMessage("Failed setting data to the clipboard."));
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
                MessageDlg.Show(this, ClipboardHelper.GetOpenClipboardMessage("Failed getting data from the clipboard."));
                return;
            }
        
            if (dataObjectSkyline != null)
            {
                SrmTreeNode nodePaste = SequenceTree.SelectedNode as SrmTreeNode;

                bool pasteToPeptideList  = false;

                if (dataObjectSkyline.Substring(0, dataObjectSkyline.IndexOf('\r')).Equals("proteins-selected=False")) 
                {
                    if (nodePaste != null)
                        pasteToPeptideList = !(nodePaste.Path.GetIdentity((int) SrmDocument.Level.PeptideGroups) is FastaSequence);
                }

                IdentityPath selectPath = null, nextAdd;

                ModifyDocument("Paste " + (pasteToPeptideList ? "peptides" : "proteins"), doc =>
                    doc.ImportDocumentXml(new StringReader(dataObjectSkyline.Substring(dataObjectSkyline.IndexOf('<'))),
                        null,
                        MeasuredResults.MergeAction.remove,
                        false,
                        FindSpectralLibrary,
                        Settings.Default.StaticModList,
                        Settings.Default.HeavyModList,
                        nodePaste == null ? null : nodePaste.Path,
                        out selectPath,
                        out nextAdd,
                        pasteToPeptideList));

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
                catch (ExternalException)
                {
                    MessageDlg.Show(this, ClipboardHelper.GetOpenClipboardMessage("Failed getting data from the clipboard."));
                    return;
                }
                try
                {
                    Paste(string.IsNullOrEmpty(textCsv) ? text : textCsv);
                }
                catch (InvalidDataException x)
                {
                    MessageDlg.Show(this, x.Message);
                }
            }
        }

        public void Paste(string text)
        {
            bool peptideList = false;
            Type[] columnTypes;
            IFormatProvider provider;
            char separator;

            // Check for a FASTA header
            if (text.StartsWith(">"))
            {
                // Make sure there is sequence information
                string[] lines = text.Split('\n');
                int aa = 0;
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (line.StartsWith(">"))
                    {
                        if (i > 0 && aa == 0)
                        {
                            throw new InvalidDataException(string.Format("Empty sequence found at line {0}.", i + 1));
                        }
                        aa = 0;
                        continue;
                    }

                    foreach (char c in line)
                    {
                        if (AminoAcid.IsExAA(c))
                            aa++;
                        else if (!char.IsWhiteSpace(c) && c != '*')
                        {
                            throw new InvalidDataException(string.Format("Unexpected character '{0}' found on line {1}.", c, i + 1));
                        }
                    }
                }
            }
            // If the text contains numbers, see if it can be imported as a mass list.
            // It is definitly not a sequence, if it has numbers.  Whereas, sequences do
            // allow internal white space including tabs.
            else if (MassListImporter.IsColumnar(text, out provider, out separator, out columnTypes))
            {
                // If no numeric type is found, try the second line.  Ther first may be
                // a header row.
                if (!MassListImporter.HasNumericColumn(columnTypes))
                {
                    int endLine = text.IndexOf('\n');
                    if (endLine != -1)
                    {
                        MassListImporter.IsColumnar(text.Substring(endLine + 1),
                            out provider, out separator, out columnTypes);
                    }
                }

                if (MassListImporter.HasNumericColumn(columnTypes))
                    ImportMassList(new StringReader(text), provider, separator, "Paste transition list");
                else if (columnTypes[columnTypes.Length - 1] != typeof(FastaSequence))
                    throw new InvalidDataException("Protein sequence not found.\nThe protein sequence must be the last value in each line.");
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
                        false, "Paste proteins");
                }
                return;
            }
            // Otherwise, look for a list of peptides, or a bare sequence
            else
            {
                // First make sure it looks like a sequence.
                List<double> lineLengths = new List<double>();
                int lineLen = 0;
                var textNoMods = FastaSequence.StripModifications(text);
                foreach (char c in textNoMods)
                {
                    if (!AminoAcid.IsExAA(c) && !char.IsWhiteSpace(c) && c != '*' && c != '.')
                    {
                        MessageDlg.Show(this, string.Format("Unexpected character '{0}' found on line {1}.", c, lineLengths.Count + 1));
                        return;
                    }
                    if (c == '\n')
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
                else if (text.Contains("."))
                {
                    MessageBox.Show(this, "Unexpected character '.' found.");
                    return;
                }

                // Choose an unused ID
                string seqId = Document.GetPeptideGroupId(peptideList);

                // Construct valid FASTA format (with >> to indicate custom name)
                text = ">>" + seqId + "\n" + text;
            }

            string description = (peptideList ? "Paste peptide list" : "Paste FASTA");
            ImportFasta(new StringReader(text), Helpers.CountLinesInString(text),
                peptideList, description);
        }

        private string FilterPeptideList(string text)
        {
            SrmSettings settings = DocumentUI.Settings;
//            Enzyme enzyme = settings.PeptideSettings.Enzyme;

            // Check to see if any of the peptides would be filtered
            // by the current settings.
            string[] pepSequences = text.Split('\n');
            var setAdded = new HashSet<string>();
            var listAllPeptides = new List<string>();
            var listAcceptPeptides = new List<string>();
            var listFilterPeptides = new List<string>();
            for (int i = 0; i < pepSequences.Length; i++)
            {
                string pepSeqMod = CleanPeptideSequence(pepSequences[i]);
                string pepSeqClean = FastaSequence.StripModifications(pepSeqMod);
                if (string.IsNullOrEmpty(pepSeqMod))
                    continue;
                if (pepSeqClean.Contains("."))
                {
                    MessageBox.Show(this, string.Format("Unexpected character '.' found on line {0}.", i + 1));
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
                            return string.Join("\n", listAcceptPeptides.ToArray());
                    }
                }
            }
            return string.Join("\n", listAllPeptides.ToArray());
        }

        // CONSIDER: Probably should go someplace else
        private static string CleanPeptideSequence(string s)
        {
            s = s.Trim();
            if (s.IndexOfAny(new[] { '\n', '\r', '\t', ' ', '.' }) == -1)
                return s;
            // Internal whitespace
            var sb = new StringBuilder();
            bool inParen = false;
            foreach (char c in s)
            {
                if(c == '[' || c == '{')
                    inParen = true;
                if(c == ']' || c == '}')
                    inParen = false;
                // Preserve spaces inside brackets - modification names can have spaces.
                if (inParen || !char.IsWhiteSpace(c))
                    sb.Append(c);
            }
            // If the peptide is in the format K.PEPTIDER.C, then remove the periods
            // and the preceding and trailing amino acids.
            if (sb.Length > 4 && sb[1] == '.' && sb[sb.Length - 2] == '.')
            {
                sb.Remove(0, 2);
                sb.Remove(sb.Length - 2, 2);
            }
            return sb.ToString();
        }

        private void deleteMenuItem_Click(object sender, EventArgs e) { EditDelete(); }
        public void EditDelete()
        {
            string undoText = "items";
            if (SequenceTree.SelectedNodes.Count == 1)
            {
                SrmTreeNode node = SequenceTree.SelectedNode as SrmTreeNode;
                if (node != null)
                    undoText = node.Text;
            }
            ModifyDocument("Delete " + undoText, doc =>
                                                  {
                                                      foreach (TreeNodeMS nodeTree in SequenceTree.SelectedNodes)
                                                      {
                                                          var node = nodeTree as SrmTreeNode;
                                                          if (node == null)
                                                              continue;

                                                          IdentityPath path = node.Path;
                                                          if (doc.FindNode(path) != null)
                                                              doc = (SrmDocument)doc.RemoveChild(path.Parent, node.Model);
                                                      }
                                                      return doc;
                                                  });
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
            using (EditNoteDlg dlg = new EditNoteDlg
            {
                Text = selPaths.Count > 1 ? "Edit Note" :
                    string.Format("Edit Note {0} {1}", ((SrmTreeNode)SequenceTree.SelectedNode).Heading, SequenceTree.SelectedNode.Text)

            })
            {
                dlg.Init(((SrmTreeNode) SequenceTree.SelectedNode).Document, selPaths);

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    bool clearAll = dlg.ClearAll;
                    var resultAnnotations = dlg.GetChangedAnnotations();
                    var resultColorIndex = dlg.ColorIndex;
                    string resultText = dlg.GetText();
                    if (resultColorIndex != -1)
                        Settings.Default.AnnotationColor = dlg.ColorIndex;
                    ModifyDocument("Edit note", doc =>
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
                    node.Collapse();
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
                    foreach (TreeNode child in node.Nodes)
                        child.Collapse();
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
                    foreach (TreeNode child in node.Nodes)
                        foreach (TreeNode grandChild in child.Nodes)
                            grandChild.Collapse();
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
                bookmark = bookmark.ChangeChromFileInfoId(_resultsGridForm.ResultsGrid.GetCurrentChromFileInfoId());
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

        public void FindAll(Control parent)
        {
            var findOptions = FindOptions.ReadFromSettings(Settings.Default);
            var findPredicate = new FindPredicate(findOptions, SequenceTree.GetDisplaySettings(null));
            IList<FindResult> results = null;
            var longWaitDlg = new LongWaitDlg(this);
            longWaitDlg.PerformWork(parent, 2000, lwb => results = FindAll(lwb, findPredicate).ToArray());
            if (results.Count == 0)
            {
                if (!longWaitDlg.IsCanceled)
                {
                    MessageBox.Show(parent.TopLevelControl, findOptions.GetNotFoundMessage());
                }
                return;
            }
            // Consider(nicksh): if there is only one match, then perhaps just navigate to it instead
            // of displaying FindResults window
//            if (results.Count() == 1)
//            {
//                DisplayFindResult(results[0]);
//            }
            var findResultsForm = Application.OpenForms.OfType<FindResultsForm>().FirstOrDefault();
            if (findResultsForm == null)
            {
                findResultsForm = new FindResultsForm(this, results);
                findResultsForm.Show(dockPanel, DockState.DockBottom);
            }
            else
            {
                findResultsForm.ChangeResults(results);
                findResultsForm.Activate();
            }
        }

        public void HideFindResults()
        {
            var findResultsForm = Application.OpenForms.OfType<FindResultsForm>().FirstOrDefault();
            if (findResultsForm != null)
                findResultsForm.Close();            
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
            var bookmarkEnumerator = BookmarkEnumerator.TryGet(DocumentUI, findResult.Bookmark);
            if (bookmarkEnumerator == null)
            {
                return;
            }
            SequenceTree.SelectedPath = bookmarkEnumerator.IdentityPath;
            int resultsIndex = bookmarkEnumerator.ResultsIndex;
            if (resultsIndex >= 0)
            {
                ComboResults.SelectedIndex = resultsIndex;
            }
            bool isAnnotationOrNote = findResult.FindMatch.AnnotationName != null || findResult.FindMatch.Note;
            if (isAnnotationOrNote && bookmarkEnumerator.CurrentChromInfo != null)
            {
                ShowResultsGrid(true);
                _resultsGridForm.ResultsGrid.HighlightFindResult(findResult);
                return;
            }
            SequenceTree.HighlightFindMatch(owner, findResult.FindMatch);
        }

        private void modifyPeptideMenuItem_Click(object sender, EventArgs e)
        {
            ModifyPeptide();
        }

        public void ModifyPeptide()
        {
            PeptideTreeNode nodePeptideTree = SequenceTree.GetNodeOfType<PeptideTreeNode>();
            if (nodePeptideTree != null)
            {
                PeptideDocNode nodePeptide = nodePeptideTree.DocNode;
                using (EditPepModsDlg dlg = new EditPepModsDlg(DocumentUI.Settings, nodePeptide))
                {
                    dlg.Height = Math.Min(dlg.Height, Screen.FromControl(this).WorkingArea.Height);
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        var listStaticMods = Settings.Default.StaticModList;
                        var listHeavyMods = Settings.Default.HeavyModList;
                        ModifyDocument("Modify " + nodePeptideTree.Text, doc =>
                                                                         doc.ChangePeptideMods(nodePeptideTree.Path,
                                                                                               dlg.ExplicitMods,
                                                                                               dlg.IsCreateCopy,
                                                                                               listStaticMods,
                                                                                               listHeavyMods));
                    }
                }
            }
        }

        private void manageUniquePeptidesMenuItem_Click(object sender, EventArgs e)
        {
            ShowUniquePeptidesDlg();
        }

        public void ShowUniquePeptidesDlg()
        {
            if (DocumentUI.Settings.PeptideSettings.BackgroundProteome.IsNone)
            {
                MessageDlg.Show(this, "Inspecting peptide uniqueness requires a background proteome.\n" +
                    "Choose a background proteome in the Digestions tab of the Peptide Settings.");
                return;
            }
            var treeNode = SequenceTree.SelectedNode;
            while (treeNode != null && !(treeNode is PeptideGroupTreeNode))
            {
                treeNode = treeNode.Parent;
            }
            var peptideGroupTreeNode = treeNode as PeptideGroupTreeNode;
            if (peptideGroupTreeNode == null)
            {
                return;
            }
            using (UniquePeptidesDlg uniquePeptidesDlg = new UniquePeptidesDlg(this)
            {
                PeptideGroupTreeNode = peptideGroupTreeNode
            })
            {
                uniquePeptidesDlg.ShowDialog(this);
            }
        }

        private void insertFASTAToolStripMenuItem_Click(object sender, EventArgs e)
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
                    ModifyDocument("Refine", doc => refineDlg.RefinementSettings.Refine(doc));
                }
            }
        }

        private void removeEmptyProteinsMenuItem_Click(object sender, EventArgs e)
        {
            var refinementSettings = new RefinementSettings { MinPeptidesPerProtein = 1 };
            ModifyDocument("Remove empty proteins", refinementSettings.Refine);
        }

        private void removeDuplicatePeptidesMenuItem_Click(object sender, EventArgs e)
        {
            var refinementSettings = new RefinementSettings { RemoveDuplicatePeptides = true };
            ModifyDocument("Remove duplicate peptides", refinementSettings.Refine);
        }

        private void removeRepeatedPeptidesMenuItem_Click(object sender, EventArgs e)
        {
            var refinementSettings = new RefinementSettings { RemoveRepeatedPeptides = true };
            ModifyDocument("Remove repeated peptides", refinementSettings.Refine);
        }

        private void sortProteinsMenuItem_Click(object sender, EventArgs e)
        {
            ModifyDocument("Sort proteins by name", doc =>
                {
                    var listProteins = new List<PeptideGroupDocNode>(doc.PeptideGroups);
                    listProteins.Sort(PeptideGroupDocNode.CompareNames);
                    return (SrmDocument) doc.ChangeChildrenChecked(listProteins.Cast<DocNode>().ToArray());
                });
        }

        private void acceptPeptidesMenuItem_Click(object sender, EventArgs e)
        {
            using (var dlg = new RefineListDlg(DocumentUI))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    var refinementSettings = new RefinementSettings {AcceptedPeptides = dlg.AcceptedPeptides};
                    if (dlg.RemoveEmptyProteins)
                        refinementSettings.MinPeptidesPerProtein = 1;

                    ModifyDocument("Accept peptides", refinementSettings.Refine);
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
            ModifyDocument("Remove missing results", refinementSettings.Refine);
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
            modifyPeptideContextMenuItem.Visible = (SequenceTree.SelectedNode is PeptideTreeNode && enabled);
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
            using (var dlg = new ShareListDlg<SrmSettingsList, SrmSettings>(Settings.Default.SrmSettingsList))
            {
                dlg.ShowDialog(this);
            }
        }

        private void importSettingsMenuItem1_Click(object sender, EventArgs e)
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
            ShowPeptideSettingsUI(null);
        }

        public void ShowPeptideSettingsUI(PeptideSettingsUI.TABS? tab)
        {
            using (PeptideSettingsUI ps = new PeptideSettingsUI(this, _libraryManager) { TabControlSel = tab })
            {
                if (ps.ShowDialog(this) == DialogResult.OK)
                {
                    if (ps.IsShowLibraryExplorer)
                        OwnedForms[OwnedForms.IndexOf(form => form is ViewLibraryDlg)].Activate();
                }
            }

            // In case user shows/hides things via the Spectral Library 
            // Explorer's spectrum graph pane.
            UpdateGraphPanes();
        }

        private void transitionSettingsMenuItem_Click(object sender, EventArgs e)
        {
            ShowTransitionSettingsUI();
        }

        public void ShowTransitionSettingsUI()
        {
            using (TransitionSettingsUI ts = new TransitionSettingsUI(this))
            {
                if (ts.ShowDialog(this) == DialogResult.OK)
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

            int i = 0;
            foreach (SrmSettings settings in list)
            {
                if (settings.Name == SrmSettingsList.DefaultName)
                    continue;

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

                if (selected == item.Text)
                    item.Checked = true;
                i++;
            }

            // Remove the rest until the separator is reached
            while (!ReferenceEquals(menu.DropDownItems[i], toolStripSeparatorSettings))
                menu.DropDownItems.RemoveAt(i);

            toolStripSeparatorSettings.Visible = (i > 0);
        }

        private bool SaveSettings()
        {
            using (SaveSettingsDlg ss = new SaveSettingsDlg())
            {
                if (ss.ShowDialog(this) != DialogResult.OK)
                    return false;

                SrmSettings settingsNew = null;

                ModifyDocument("Name settings", doc =>
                                                    {
                                                        settingsNew = (SrmSettings) doc.Settings.ChangeName(ss.SaveName);
                                                        return doc.ChangeSettings(settingsNew);
                                                    });

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
                    DialogResult result = MessageBox.Show("Do you want to save your current settings before switching?",
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
                    settingsNew.UpdateLists();
            }
        }

        public bool ChangeSettings(SrmSettings newSettings, bool store)
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

            ModifyDocument("Change settings", doc => doc.ChangeSettings(newSettings));
            return true;
        }

        private void annotationsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowAnnotationsDialog();
        }

        public void ShowAnnotationsDialog()
        {
            using (var dlg = new ChooseAnnotationsDlg(this))
            {
                 dlg.ShowDialog(this);
            }
        }

        private void integrateAllMenuItem_Click(object sender, EventArgs e)
        {
            IntegrateAll();
        }

        public void IntegrateAll()
        {
            bool integrateAll = DocumentUI.Settings.TransitionSettings.Integration.IsIntegrateAll;
            ModifyDocument(integrateAll ? "Set integrate all" : "Clear integrate all",
                doc => doc.ChangeSettings(doc.Settings.ChangeTransitionIntegration(i => i.ChangeIntegrateAll(!integrateAll))));
        }

        #endregion // Settings menu

        #region Help menu

        private void homeMenuItem_Click(object sender, EventArgs e)
        {
            WebHelpers.OpenLink(this, "http://proteome.gs.washington.edu/software/skyline/");
        }

        private void videosMenuItem_Click(object sender, EventArgs e)
        {
            WebHelpers.OpenLink(this, "http://proteome.gs.washington.edu/software/Skyline/videos.html");
        }

        private void tutorialsMenuItem_Click(object sender, EventArgs e)
        {
            WebHelpers.OpenLink(this, "http://proteome.gs.washington.edu/software/Skyline/tutorials.html");
        }

        private void supportMenuItem_Click(object sender, EventArgs e)
        {
            WebHelpers.OpenLink(this, "http://proteome.gs.washington.edu/software/Skyline/support.html");
        }

        private void issuesMenuItem_Click(object sender, EventArgs e)
        {
            WebHelpers.OpenLink(this, "http://proteome.gs.washington.edu/software/Skyline/issues.html");
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

        private void peptidesMenuItem_Click(object sender, EventArgs e)
        {
            ShowSequenceTreeForm(Settings.Default.ShowPeptides = true);            
        }

        public void ShowSequenceTreeForm(bool show)
        {
            if (show)
            {
                if (_sequenceTreeForm != null)
        {
                    _sequenceTreeForm.Activate();
                    _sequenceTreeForm.Focus();
        }
                else
        {
                    _sequenceTreeForm = CreateSequenceTreeForm();
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

        private SequenceTreeForm CreateSequenceTreeForm()
        {
            // Initialize sequence tree control
            _sequenceTreeForm = new SequenceTreeForm(this);
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
            _sequenceTreeForm.SequenceTree.DragEnter += sequenceTree_DragDrop;
            _sequenceTreeForm.SequenceTree.UseKeysOverride = _useKeysOverride;
            _sequenceTreeForm.ComboResults.SelectedIndexChanged += comboResults_SelectedIndexChanged;
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
                e.Node.Text = "";
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
                    // TODO(brendanx) Move document access inside ModifyDocument delegate
                    var document = DocumentUI;
                    var settings = document.Settings;
                    var backgroundProteome = settings.PeptideSettings.BackgroundProteome;
                    FastaSequence fastaSequence = null;
                    string peptideSequence = null;
                    if (!backgroundProteome.IsNone)
                    {
                        int ichPeptideSeparator = labelText.IndexOf(FastaSequence.PEPTIDE_SEQUENCE_SEPARATOR,
                                                                    StringComparison.Ordinal);
                        string proteinName;
                        if (ichPeptideSeparator >= 0)
                        {
                            // TODO(nicksh) If they've selected a single peptide, then see if the protein has already
                            // been added, and, if so, just add the single peptide to the existing protein.
                            peptideSequence = labelText.Substring(0, ichPeptideSeparator);
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
                            modifyMessage = "Add " + fastaSequence.Name;
                        else
                        {
                            modifyMessage = "Add " + peptideSequence;
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
                                    if (Equals(peptideDocNode.Peptide.Sequence, peptideSequence))
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
                            peptideDocNodes.AddRange(fastaSequence.CreateFullPeptideDocNodes(settings, true));
                        }
                        else
                        {
                            peptideDocNodes.Add(fastaSequence.CreateFullPeptideDocNode(settings, peptideSequence));
                        }
                        peptideDocNodes.Sort(FastaSequence.ComparePeptides);
                    }
                    else
                    {
                        modifyMessage = "Add " + labelText;
                        bool isExSequence = FastaSequence.IsExSequence(labelText) &&
                                            FastaSequence.StripModifications(labelText).Length >= 
                                            settings.PeptideSettings.Filter.MinPeptideLength;
                        if (isExSequence)
                        {
                            int countGroups = document.Children.Count;
                            if (countGroups > 0)
                            {
                                oldPeptideGroupDocNode = (PeptideGroupDocNode)document.Children[countGroups - 1];
                                if (!oldPeptideGroupDocNode.IsPeptideList)
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
                                    using (var dlg = new MultiButtonMsgDlg(
                                        string.Format(
                                            "Would you like to use the Unimod definitions for the following modifications?\n\n{0}",
                                            strNameMatches), "OK"))
                                    {
                                        if (dlg.ShowDialog() == DialogResult.Cancel)
                                        {
                                            e.Node.Text = EmptyNode.TEXT_EMPTY;
                                            e.Node.EnsureVisible();
                                            return;
                                        }
                                    }
                                }
                                peptideDocNodes.Add(matcher.GetModifiedNode(labelText).ChangeSettings(settings, SrmSettingsDiff.ALL));
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
                            var docNew =
                            (SrmDocument) doc.ReplaceChild(newPeptideGroupDocNode);
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
                    ModifyDocument(string.Format("Edit name {0}", e.Label), doc => (SrmDocument)
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
                SequenceTree.HideEffects();
                var settings = DocumentUI.Settings;
                // Show the ratios sub-menu when there are results and a choice of
                // internal standard types.
                ratiosContextMenuItem.Visible =
                    settings.HasResults &&
                    settings.PeptideSettings.Modifications.InternalStandardTypes.Count > 1 &&
                    settings.PeptideSettings.Modifications.HasHeavyModifications;
                contextMenuTreeNode.Show(SequenceTree, pt);
            }
        }

        private void ratiosContextMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            ToolStripMenuItem menu = ratiosContextMenuItem;
            menu.DropDownItems.Clear();
            var standardTypes = DocumentUI.Settings.PeptideSettings.Modifications.InternalStandardTypes;
            for (int i = 0; i < standardTypes.Count; i++)
            {
                var handler = new SelectRatioHandler(this, i);
                var item = new ToolStripMenuItem(standardTypes[i].Title, null, handler.ToolStripMenuItemClick)
                    {Checked = (SequenceTree.RatioIndex == i)};
                menu.DropDownItems.Add(item);
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

            protected virtual void OnMenuItemClick()
            {
                _skyline.SequenceTree.RatioIndex = _ratioIndex;
                if (_skyline._graphPeakArea != null)
                    _skyline._graphPeakArea.RatioIndex = _ratioIndex;                
            }
        }

        private void sequenceTree_PickedChildrenEvent(object sender, PickedChildrenEventArgs e)
        {
            SrmTreeNodeParent node = e.Node;
            ModifyDocument(string.Format("Pick {0}", node.ChildUndoHeading),
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
            List<SrmTreeNode> nodeSources = (List<SrmTreeNode>) e.Data.GetData(typeof (PeptideGroupTreeNode).FullName) ??
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

            ModifyDocument("Drag and drop", doc =>
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

        private void comboResults_SelectedIndexChanged(object sender, EventArgs e)
        {
            string name = SelectedGraphChromName;
            if (name == null)
                return;

            // Update the summary graphs if necessary.
            if (_graphRetentionTime != null && _graphRetentionTime.ResultsIndex != ComboResults.SelectedIndex)
                _graphRetentionTime.ResultsIndex = ComboResults.SelectedIndex;
            if (_graphPeakArea != null && _graphPeakArea.ResultsIndex != ComboResults.SelectedIndex)
                _graphPeakArea.ResultsIndex = ComboResults.SelectedIndex;
            if (_resultsGridForm != null && _resultsGridForm.ResultsIndex != ComboResults.SelectedIndex)
                _resultsGridForm.ResultsIndex = ComboResults.SelectedIndex;
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

            UpdateStatusCounter(statusSequences, positions, SrmDocument.Level.PeptideGroups, "prot");
            UpdateStatusCounter(statusPeptides, positions, SrmDocument.Level.Peptides, "pep");
            UpdateStatusCounter(statusPrecursors, positions, SrmDocument.Level.TransitionGroups, "prec");
            UpdateStatusCounter(statusIons, positions, SrmDocument.Level.Transitions, "tran");
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
                tag = pos + "/" + count;
            }

            if (!Equals(label.Tag, tag))
            {
                label.Text = tag + " " + text;
                label.Tag = tag;
            }
        }

        private List<ProgressStatus> ListProgress { get { return _listProgress; } }

        private bool SetListProgress(List<ProgressStatus> listNew, List<ProgressStatus> listOriginal)
        {
            var listResult = Interlocked.CompareExchange(ref _listProgress, listNew, listOriginal);

            return ReferenceEquals(listResult, listOriginal);
        }

        // TODO: Something better after demoing library building
        bool IProgressMonitor.IsCanceled
        {
            get { return false; }
        }

        void IProgressMonitor.UpdateProgress(ProgressStatus status)
        {
            UpdateProgress(this, new ProgressUpdateEventArgs(status));
        }

        private void UpdateProgress(object sender, ProgressUpdateEventArgs e)
        {
            var status = e.Progress;
            var final = status.IsFinal;

            int i;
            List<ProgressStatus> listOriginal, listNew;
            do
            {
                listOriginal = ListProgress;
                listNew = new List<ProgressStatus>(listOriginal);

                // Replace existing status, if it is already being tracked.
                for (i = 0; i < listNew.Count; i++)
                {
                    if (ReferenceEquals(listNew[i].Id, status.Id))
                    {
                        if (final)
                            listNew.RemoveAt(i);
                        else
                            listNew[i] = status;
                        break;
                    }
                }
                // Or add this status, if it is not in the list.
                if (!final && i == listNew.Count)
                    listNew.Add(status);
            }
            while (!SetListProgress(listNew, listOriginal));

            // If the status is first in the queue and it is beginning, initialize
            // the progress UI.
            if (i == 0 && status.IsBegin)
                RunUIAction(BeginProgressUI, status);
            // If it is a final state, and it is being shown, or there was an error
            // make sure user sees the change.
            else if (final && (i == 0 || status.IsError))
                RunUIAction(CompleteProgressUI, status);
        }

        private void BeginProgressUI(ProgressStatus status)
        {
            _timerProgress.Start();
        }

        private void CompleteProgressUI(ProgressStatus status)
        {
            // If completed successfully, make sure the user sees 100% by setting
            // 100 and then waiting for the next timer tick to clear the progress
            // indicator.
            if (status.IsComplete)
            {
                if (statusProgress.Visible)
                    statusProgress.Value = status.PercentComplete;
            }
            else
            {
                // If an error, show the message before removing status
                if (status.IsError)
                {
                    var message = status.ErrorException.Message;

                    // Drill down to see if the innermost exception was an out-of-memory exception.
                    var innerException = status.ErrorException;
                    while (innerException.InnerException != null)
                        innerException = innerException.InnerException;
                    if (innerException is OutOfMemoryException)
                    {
                        message += string.Format("\n\n{0} ran out of memory.", Program.Name);
                        if (!Install.Is64Bit && Environment.Is64BitOperatingSystem)
                        {
                            message += string.Format("\n\nYou may be able to avoid this problem by installing a 64-bit version of {0}.", Program.Name);
                        }
                    }

                    // TODO: Get topmost window
                    MessageDlg.Show(this, message);
                }

                // Update the progress UI immediately
                UpdateProgressUI(this, new EventArgs());
            }
        }

        private void UpdateProgressUI(object sender, EventArgs e)
        {
            if (statusStrip.IsDisposed)
                return;

            var listProgress = ListProgress;
            if (listProgress.Count == 0)
            {
                statusProgress.Visible = false;
                statusGeneral.Text = "Ready";
                _timerProgress.Stop();
            }
            else
            {
                ProgressStatus status = listProgress[0];
                statusProgress.Value = status.PercentComplete;
                statusProgress.Visible = true;
                statusGeneral.Text = status.Message;
            }
        }

        Point INotificationContainer.NotificationAnchor
        {
            get { return new Point(Left, statusStrip.Visible ? Bottom - statusStrip.Height : Bottom); }
        }

        LibraryManager ILibraryBuildNotificationContainer.LibraryManager
        {
            get { return _libraryManager; }
        }

        public AsyncCallback LibraryBuildCompleteCallback
        {
            get { return _libraryBuildNotificationHandler.LibraryBuildCompleteCallback; }
        }

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

        private void RunUIAction(Action act)
        {
            if (InvokeRequired)
                BeginInvoke(act);
            else
                act();
        }

        private void RunUIAction<TArg>(Action<TArg> act, TArg arg)
        {
            if (InvokeRequired)
                BeginInvoke(act, arg);
            else
                act(arg);
        }

        private Control _activeClipboardControl;
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
                return;
            }
            int countSelected = SequenceTree.SelectedNodes.Count;
            SrmTreeNode nodeTree = (countSelected > 0 ? SequenceTree.SelectedNode as SrmTreeNode : null);

            // Allow deletion, copy/paste for a single selection that is not the insert node,
            // or any multiple selection.
            bool enabled = (nodeTree != null || SequenceTree.SelectedNodes.Count > 1);
            cutToolBarButton.Enabled = cutMenuItem.Enabled = enabled;
            copyToolBarButton.Enabled = copyMenuItem.Enabled = enabled;
            pasteToolBarButton.Enabled = pasteMenuItem.Enabled = true;
            deleteMenuItem.Enabled = enabled;
        }

        private void spectralLibrariesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ViewSpectralLibraries();
        }

        public void ViewSpectralLibraries()
        {
            if (Settings.Default.SpectralLibraryList.Count == 0)
            {
                var result = MessageBox.Show(this, "No libraries to show. Would you like to add a library?",
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
                    string libraryName = "";
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

        public void OpenLibraryExplorer(string libraryName)
        {
            var viewLibraryDlg = new ViewLibraryDlg(_libraryManager, libraryName, this) { Owner = this };
            viewLibraryDlg.Show();
        }

        private void defaultToolStripMenuItem_Click(object sender, EventArgs e)
        {
            defaultTextToolStripMenuItem.Checked = true;
            largeToolStripMenuItem.Checked = extraLargeToolStripMenuItem.Checked = false;
            ChangeTextSize();
        }

        private void largeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            largeToolStripMenuItem.Checked = true;
            defaultTextToolStripMenuItem.Checked = extraLargeToolStripMenuItem.Checked = false;
            ChangeTextSize();
        }

        private void extraLargeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            extraLargeToolStripMenuItem.Checked = true;
            defaultTextToolStripMenuItem.Checked = largeToolStripMenuItem.Checked = false;
            ChangeTextSize();
        }

        private void ChangeTextSize()
        {
            if (largeToolStripMenuItem.Checked)
                Settings.Default.TextZoom = TreeViewMS.LRG_TEXT_FACTOR;
            else if (extraLargeToolStripMenuItem.Checked)
                Settings.Default.TextZoom = TreeViewMS.XLRG_TEXT_FACTOR;
            else
            {
                defaultTextToolStripMenuItem.Checked = true;
                Settings.Default.TextZoom = TreeViewMS.DEFAULT_TEXT_FACTOR; 
            }
            SequenceTree.OnTextZoomChanged();
        }

        private void generateDecoysMenuItem_Click(object sender, EventArgs e)
        {
            if (DocumentUI.PeptideGroups.Any(nodePeptideGroup => nodePeptideGroup.IsDecoy))
            {
                // Warn about removing existing decoys
                var result = MessageBox.Show(this, "This operation will replace the existing decoys.\nAre you sure you want to continue?",
                                             Program.Name, MessageBoxButtons.OKCancel);
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
                    var refinementSettings = new RefinementSettings{ NumberOfDecoys = decoysDlg.NumDecoys, DecoysLabelUsed = decoysDlg.DecoysLabelType, DecoysMethod = decoysDlg.DecoysMethod };
                    ModifyDocument("Generate Decoys", refinementSettings.GenerateDecoys);

                    var nodePepGroup = DocumentUI.PeptideGroups.First(nodePeptideGroup => nodePeptideGroup.IsDecoy);
                    SelectedPath = DocumentUI.GetPathTo((int)SrmDocument.Level.PeptideGroups, DocumentUI.FindNodeIndex(nodePepGroup.Id));
                }
            }
        }

        private void retentionTimeAlignmentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowRetentionTimeAlignmentForm();
        }

        public AlignmentForm ShowRetentionTimeAlignmentForm()
        {
            var form = Application.OpenForms.OfType<AlignmentForm>().FirstOrDefault();
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

        #region Tools Menu

        public class SenderArgs : EventArgs
        {
            public SenderArgs(List<ToolDescription> tools )
            {
                Tools = tools;
            }
            public List<ToolDescription> Tools { get; set; }
        }
    
        public static void SaveEvent(object sender, SenderArgs args)
        {
            Settings.Default.ToolList = args.Tools;
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
                           Transition.GetChargeIndicator(selprec.DocNode.TransitionGroup.PrecursorCharge);
                }
                return null;                
            }            
        }

        //Todo: (danny) check with brendan this is correct for Active Replicate Name
        public string ResultNameCurrent
        {
            get { return (new UndoState(this).ResultNameCurrent); }
        }

        public string SelectedPeptideSequence
        {
            get
            {
                var peptTreeNode = SequenceTree.GetNodeOfType<PeptideTreeNode>(); 
                return peptTreeNode != null ? peptTreeNode.DocNode.Peptide.Sequence : null;
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

        #endregion

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
                // If one of the macros cannot be replaced a messageDlg is displayed and null is returned. 
                string args = _tool.GetArguments(_parent, _parent);
                string initDir = null;
                if (args != null)
                {
                    initDir = _tool.GetInitialDirectory(_parent, _parent);
                }
                if (args != null && initDir != null)
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo(Command, args) {WorkingDirectory = initDir};

                    try
                    {
                        Process.Start(startInfo);
                    }
                    catch (FileNotFoundException)
                    {
                        MessageDlg.Show(_parent,
                                        "File Not Found: \n\n Please check the command location is correct for this tool");
                    }
                    catch (Win32Exception)
                    {
                        MessageDlg.Show(_parent,
                                        "File Not Found: \n\n Please check the command location is correct for this tool");
                    }
                    catch (Exception)
                    {
                        MessageDlg.Show(_parent, "Please reconfigure that tool, it failed to execute. ");
                    }
                }
            }
        }

        private void configureToolsMenuItem_Click(object sender, EventArgs e)
        {
            ShowConfigureToolsDlg();
        }

        public void ShowToolOptionsUI()
        {
            using (ToolOptionsUI dlg = new ToolOptionsUI())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    // At this point the dialog does everything by itself.
                }
            }
        }

        public void ShowConfigureToolsDlg()
        {
            using (ConfigureToolsDlg dlg = new ConfigureToolsDlg(Copier(Settings.Default.ToolList)))
            {
                dlg.ShowDialog(this);
            }
        }

        public static List<ToolDescription> Copier(List<ToolDescription> list)
        {
            List<ToolDescription> output = new List<ToolDescription>();          
           
            foreach (var t in list)
            {
                if (!t.Equals( new ToolDescription("", "", "", "")))
                {
                    ToolDescription temp = new ToolDescription(t.Title, t.Command, t.Arguments, t.InitialDirectory);
                    output.Add(temp);
                }                
            }
            return output;
        }

        private void toolsMenu_DropDownOpening(object sender, EventArgs e)
        {
            PopulateToolsMenu();
        }

        public void PopulateToolsMenu()
        {
            // Remove all items from the toolToolStripMenuItem.
            while (!ReferenceEquals(toolsMenu.DropDownItems[0], toolStripSeparator46))
            {
                toolsMenu.DropDownItems.RemoveAt(0);
            }

            int lastInsertIndex = 0;
            List<ToolDescription> toolList = Settings.Default.ToolList;
            if (toolList.Count == 0)
            {
                toolStripSeparator46.Visible = false;
            }
            else
            {
                toolStripSeparator46.Visible = true;
                foreach (ToolMenuItem menuItem in toolList.Select(t => new ToolMenuItem(t, this) {Text = t.Title}))
            
                    toolsMenu.DropDownItems.Insert(lastInsertIndex++, menuItem);
            }            
        }

        /// <summary>
        /// Runs a tool by index from the tools menu. (for testing)
        /// </summary>
        /// <param name="i">Index of tool in the menu</param>
        public void RunTool(int i)
        {
            GetToolMenuItem(i).DoClick();
        }

        public string GetToolText(int i)
        {
            return GetToolMenuItem(i).Text;
        }

        public bool ConfigMenuPresent()
        {
            return toolsMenu.DropDownItems.Contains(toolStripSeparator46) && 
                    toolsMenu.DropDownItems.Contains(configureToolsMenuItem);
        }

        public ToolMenuItem GetToolMenuItem(int i)
        {
            foreach (var item in toolsMenu.DropDownItems)
            {
                var toolMenuItem = item as ToolMenuItem;
                if (toolMenuItem != null && i-- == 0)
                    return toolMenuItem;
            }
            return null;
        }

        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowToolOptionsUI();
        }

        #endregion 

    }
}

