/*
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Files;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.IonMobility;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.Skyline.Util;
using static pwiz.Skyline.Model.Files.FileModel;
using Debugger = System.Diagnostics.Debugger;
using Process = System.Diagnostics.Process;

// CONSIDER: using IdentityPath (and DocNode.ReplaceChild) to simplify replicate name changes
//           But replicates do not have IdentityPath support because ChromatogramSet does not
//           inherit from DocNode. Will revisit later.
// ReSharper disable WrongIndentSize
namespace pwiz.Skyline.Controls.FilesTree
{
    public partial class FilesTreeForm : DockableFormEx, ITipDisplayer
    {
        private NodeTip _nodeTip;
        private Panel _dropTargetRemove;
        private readonly MoveThreshold _moveThreshold = new MoveThreshold(5, 5);
        private IDragDropHandler _dragDropHandler = DefaultDragDropHandler.Instance;

        public FilesTreeForm()
        {
            InitializeComponent();
        }

        public FilesTreeForm(SkylineWindow skylineWindow) : this()
        {
            SkylineWindow = skylineWindow;

            // Initialize ShowFileNames from settings
            SkylineFile.ShowFileNames = Settings.Default.FilesViewShowFileNames;

            // FilesTree => context menu
            filesTree.ContextMenuStrip = filesTreeContextMenu;

            // FilesTree => tooltips
            _nodeTip = new NodeTip(this) { Parent = TopLevelControl };

            // FilesTree => trash can drop target that appears when dragging tree nodes
            _dropTargetRemove = CreateDropTarget(FilesTreeResources.Trash, DockStyle.None);
            _dropTargetRemove.Visible = false;
            _dropTargetRemove.AllowDrop = true;

            filesTree.Controls.Add(_dropTargetRemove);

            filesTree.InitializeTree(SkylineWindow);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            // FilesTree custom events (not available in designer)
            filesTree.LostFocus += FilesTree_LostFocus;
            filesTree.AfterNodeEdit += FilesTree_AfterLabelEdit;

            // SkylineWindow events
            SkylineWindow.DocumentSavedEvent += OnDocumentSavedEvent;
            SkylineWindow.DocumentUIChangedEvent += OnDocumentUIChangedEvent;

            // Drop target events
            _dropTargetRemove.DragEnter += DropTargetRemove_DragEnter;
            _dropTargetRemove.DragDrop += DropTargetRemove_DragDrop;
            _dropTargetRemove.QueryContinueDrag += FilesTree_QueryContinueDrag;
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            // Drop target events
            _dropTargetRemove.QueryContinueDrag -= FilesTree_QueryContinueDrag;
            _dropTargetRemove.DragDrop -= DropTargetRemove_DragDrop;
            _dropTargetRemove.DragEnter -= DropTargetRemove_DragEnter;

            // SkylineWindow events
            SkylineWindow.DocumentUIChangedEvent -= OnDocumentUIChangedEvent;
            SkylineWindow.DocumentSavedEvent -= OnDocumentSavedEvent;

            // FilesTree custom events
            filesTree.AfterNodeEdit -= FilesTree_AfterLabelEdit;
            filesTree.LostFocus -= FilesTree_LostFocus;

            base.OnHandleDestroyed(e);
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public FilesTree FilesTree => filesTree;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SkylineWindow SkylineWindow { get; }

        /// <summary>
        /// Wrapper for SkylineWindow.ModifyDocument that skips debouncing in FilesTree.
        /// Use this for document modifications initiated by FilesTree/FilesTreeForm to ensure
        /// immediate UI feedback (e.g., after drag-drop, delete, rename operations).
        /// </summary>
        private void ModifyDocument(string description, IDocumentModifier documentModifier)
        {
            FilesTree.SkipNextDebounce();
            SkylineWindow.ModifyDocument(description, documentModifier);
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DockPane ParentDockPane => Parent as DockPane;

        private void OnDocumentSavedEvent(object sender, DocumentSavedEventArgs args)
        {
            filesTree.OnDocumentSaved(sender, args);
        }

        private void OnDocumentUIChangedEvent(object sender, DocumentChangedEventArgs e)
        {
            filesTree.OnDocumentChanged(sender, e);
        }

        protected override string GetPersistentString()
        {
            return base.GetPersistentString() + @"|" + FilesTree.GetPersistentString();
        }

        public void OpenAuditLog()
        {
            SkylineWindow.ShowAuditLog();
        }

        public void OpenContainingFolder(TreeNode selectedNode)
        {
            if (FilesTree.SelectedNode is FilesTreeNode filesTreeNode)
            {
                Process.Start(@"explorer.exe", $@"/select,""{filesTreeNode.LocalFilePath}""");
            }
        }

        public void OpenLibraryExplorerDialog(TreeNode selectedNode)
        {
            var model = (SpectralLibrary)((FilesTreeNode)selectedNode).Model;
            var libraryName = model.Name;

            var index = SkylineWindow.OwnedForms.IndexOf(form => form is ViewLibraryDlg);

            // Change the selected library if Library Explorer is already available
            if (index != -1)
            {
                var viewLibraryDlg = SkylineWindow.OwnedForms[index] as ViewLibraryDlg;
                viewLibraryDlg?.Activate();
                viewLibraryDlg?.ChangeSelectedLibrary(libraryName);
            }
            // If not, open a new Library Explorer.
            else
            {
                SkylineWindow.OpenLibraryExplorer(libraryName);
            }
        }

        /// <summary>
        /// Activate a replicate by name. This handler runs when a replicate sample file is clicked, so
        /// obtain the replicate's name from its parent tree node, which represents the replicate.
        /// </summary>
        /// <param name="selectedNode">Replicate sample file</param>
        public void ActivateReplicate(TreeNode selectedNode)
        {
            var filesTreeNode = (FilesTreeNode)selectedNode;
            Replicate model;
            switch (filesTreeNode.Model)
            {
                case ReplicateSampleFile _:
                    model = (Replicate)((FilesTreeNode)filesTreeNode.Parent).Model;
                    break;
                case Replicate replicate:
                    model = replicate;
                    break;
                default:
                    return;
            }

            SkylineWindow.ActivateReplicate(model.Name);
        }

        public void OpenManageResultsDialog()
        {
            SkylineWindow.ManageResults();
        }

        public void OpenLibraryExplorerDialog()
        {
            SkylineWindow.ViewSpectralLibraries();
        }

        public void OpenEditBackgroundProteomeDialog(FilesTreeNode treeNode)
        {
            var docBgProteome = SkylineWindow.DocumentUI.Settings.PeptideSettings.BackgroundProteome;
            if (!ReferenceEquals(docBgProteome.Id, treeNode.Model.IdentityPath.GetIdentity(0)))
                return;

            using var editBackgroundProteomeDlg = new BuildBackgroundProteomeDlg(new[] {docBgProteome});
            editBackgroundProteomeDlg.BackgroundProteomeSpec = docBgProteome;
            if (editBackgroundProteomeDlg.ShowDialog(this) == DialogResult.OK)
            {
                var newBgProteome = editBackgroundProteomeDlg.BackgroundProteomeSpec;
                var modifier = DocumentModifier.Create(doc => BackgroundProteome.Edit(doc, newBgProteome));

                ModifyDocument(FilesTreeResources.FilesTreeForm_Update_BackgroundProteome, modifier);

                // Update the settings list to persist the change
                Settings.Default.BackgroundProteomeList.ReplaceValue(docBgProteome, newBgProteome);
            }
        }

        public void OpenEditRTCalculatorDialog(FilesTreeNode filesTreeNode)
        {
            var docCalc = SkylineWindow.DocumentUI.Settings.PeptideSettings.Prediction.RetentionTime?.Calculator as RCalcIrt;
            if (docCalc == null)
                return;
            if (!ReferenceEquals(docCalc.Id, filesTreeNode.Model.IdentityPath.GetIdentity(0)))
                return;

            var existingCalcs = Settings.Default.RTScoreCalculatorList.Where(calc => !ReferenceEquals(calc, docCalc));
            using var editIrtCalcDlg = new EditIrtCalcDlg(docCalc, existingCalcs);
            if (editIrtCalcDlg.ShowDialog(this) == DialogResult.OK)
            {
                var newCalc = editIrtCalcDlg.Calculator;
                var modifier = DocumentModifier.Create(doc => RTCalc.Edit(doc, newCalc));

                ModifyDocument(FilesTreeResources.FilesTreeForm_Update_RTCalculator, modifier);

                // Update the settings list to persist the change
                Settings.Default.RTScoreCalculatorList.ReplaceValue(docCalc, newCalc);
                // Also need to update the RetentionTimeRegression that uses this calculator (name change impossible)
                Settings.Default.RetentionTimeList.SetValue(
                    SkylineWindow.DocumentUI.Settings.PeptideSettings.Prediction.RetentionTime);
            }
        }

        public void OpenEditOptimizationLibraryDialog(FilesTreeNode filesTreeNode)
        {
            var docSettings = SkylineWindow.DocumentUI.Settings;
            var docOptLib = docSettings.TransitionSettings.Prediction.OptimizedLibrary;
            if (docOptLib == null || docOptLib.IsNone)
                return;
            if (!ReferenceEquals(docOptLib.Id, filesTreeNode.Model.IdentityPath.GetIdentity(0)))
                return;

            var existingLibs = Settings.Default.OptimizationLibraryList.Where(lib => !ReferenceEquals(lib, docOptLib));
            using var editOptLibDlg = new EditOptimizationLibraryDlg(SkylineWindow.DocumentUI, docOptLib, existingLibs);
            if (editOptLibDlg.ShowDialog(this) == DialogResult.OK)
            {
                var newLib = editOptLibDlg.Library;
                var modifier = DocumentModifier.Create(doc => OptimizationLibrary.Edit(doc, newLib));

                ModifyDocument(FilesTreeResources.FilesTreeForm_Update_OptimizationLibrary, modifier);

                // Update the settings list to persist the change
                Settings.Default.OptimizationLibraryList.ReplaceValue(docOptLib, newLib);
            }
        }

        public void OpenEditIonMobilityLibraryDialog(FilesTreeNode filesTreeNode)
        {
            var docSettings = SkylineWindow.DocumentUI.Settings;
            var docIonMobilityLib = docSettings.TransitionSettings.IonMobilityFiltering.IonMobilityLibrary;
            if (docIonMobilityLib == null || docIonMobilityLib.IsNone)
                return;
            if (!ReferenceEquals(docIonMobilityLib.Id, filesTreeNode.Model.IdentityPath.GetIdentity(0)))
                return;

            var existingLibs = Settings.Default.IonMobilityLibraryList.Where(lib => !ReferenceEquals(lib, docIonMobilityLib));
            var ionMobilityWindowWidthCalculator = docSettings.TransitionSettings.IonMobilityFiltering.FilterWindowWidthCalculator;
            using var editIonMobilityLibDlg = new EditIonMobilityLibraryDlg(docIonMobilityLib, existingLibs, ionMobilityWindowWidthCalculator);
            if (editIonMobilityLibDlg.ShowDialog(this) == DialogResult.OK)
            {
                var newLib = editIonMobilityLibDlg.IonMobilityLibrary;
                var modifier = DocumentModifier.Create(doc => IonMobilityLibrary.Edit(doc, newLib));

                ModifyDocument(FilesTreeResources.FilesTreeForm_Update_IonMobilityLibrary, modifier);

                // Update the settings list to persist the change
                Settings.Default.IonMobilityLibraryList.ReplaceValue(docIonMobilityLib, newLib);
            }
        }

        public void OpenEditSpectralLibraryDialog(FilesTreeNode filesTreeNode)
        {
            var docSettings = SkylineWindow.DocumentUI.Settings;
            var docLibSpec = docSettings.PeptideSettings.Libraries.FindLibrarySpec(filesTreeNode.Model.IdentityPath.GetIdentity(0));
            if (docLibSpec == null)
                return;

            using var editLibDlg = new EditLibraryDlg(Settings.Default.SpectralLibraryList);
            editLibDlg.LibrarySpec = docLibSpec;
            if (editLibDlg.ShowDialog(this) == DialogResult.OK)
            {
                var newLibSpec = editLibDlg.LibrarySpec;
                var modifier = DocumentModifier.Create(doc => SpectralLibrary.Edit(doc, docLibSpec, newLibSpec));

                ModifyDocument(FilesTreeResources.FilesTreeForm_Update_SpectralLibrary, modifier);

                // Update the settings list to persist the change
                Settings.Default.SpectralLibraryList.ReplaceValue(docLibSpec, newLibSpec);
            }
        }

        // Remove all child nodes from this folder.
        public void RemoveAll(FilesTreeNode folderNode)
        {
            if (folderNode == null || !folderNode.SupportsRemoveAllItems())
                return;

            var messages = folderNode.Model is ReplicatesFolder ? 
                ValueTuple.Create(FilesTreeResources.FilesTreeForm_Confirm_Remove_Replicates, FilesTreeResources.Remove_All_Replicates) : 
                ValueTuple.Create(FilesTreeResources.FilesTreeForm_Confirm_Remove_Spectral_Libraries, FilesTreeResources.Remove_All_Spectral_Libraries);

            if (ConfirmDelete(messages.Item1) != DialogResult.Yes)
                return;

            lock (SkylineWindow.GetDocumentChangeLock())
            {
                var originalDoc = SkylineWindow.Document;
                ModifiedDocument modifiedDoc = null;

                using var longWaitDlg = new LongWaitDlg();
                longWaitDlg.PerformWork(this, 750, progressMonitor =>
                {
                    using var monitor = new SrmSettingsChangeMonitor(progressMonitor, longWaitDlg.Text, SkylineWindow);

                    if (folderNode.Model is ReplicatesFolder replicates)
                    {
                        modifiedDoc = replicates.DeleteAll(originalDoc, monitor);
                    }
                    else if (folderNode.Model is SpectralLibrariesFolder libraries)
                    {
                        modifiedDoc = libraries.DeleteAll(originalDoc, monitor);
                    }
                });

                if (modifiedDoc == null) 
                    return;

                ModifyDocument(messages.Item2, DocumentModifier.FromResult(originalDoc, modifiedDoc));
            }
        }

        public void RemoveSelected(IList<FilesTreeNode> nodes)
        {
            if (nodes == null || nodes.Count == 0)
                return;

            var model = nodes.First().Model;
            Assume.IsTrue(model is Replicate || model is SpectralLibrary);

            var messages = model is Replicate ? 
                ValueTuple.Create(FilesTreeResources.FilesTreeForm_Confirm_Remove_Replicate, FilesTreeResources.FilesTreeForm_Confirm_Remove_Replicates, FilesTreeResources.Remove_Replicate) :
                ValueTuple.Create(FilesTreeResources.FilesTreeForm_Confirm_Remove_Spectral_Library, FilesTreeResources.FilesTreeForm_Confirm_Remove_Spectral_Libraries, FilesTreeResources.Remove_Spectral_Library);

            if(ConfirmDelete(nodes.Count, messages.Item1, messages.Item2) != DialogResult.Yes)
                return;

            lock (SkylineWindow.GetDocumentChangeLock())
            {
                var originalDoc = SkylineWindow.Document;
                ModifiedDocument modifiedDoc = null;

                using var longWaitDlg = new LongWaitDlg();
                longWaitDlg.PerformWork(this, 750, progressMonitor =>
                {
                    using var monitor = new SrmSettingsChangeMonitor(progressMonitor, longWaitDlg.Text, SkylineWindow);

                    if (model is Replicate)
                    {
                        // The selected nodes could include sample files. If so, remove them so the list is only Replicates, which
                        // makes the Audit Log messages more consistent.
                        var deletedModels = nodes.Select(item => item.Model).OfType<Replicate>().Cast<FileModel>().ToList();
                        modifiedDoc = Replicate.Delete(originalDoc, monitor, deletedModels);
                    }
                    else if (model is SpectralLibrary)
                    {
                        var deletedModels = nodes.Select(item => item.Model).ToList();
                        modifiedDoc = SpectralLibrary.Delete(originalDoc, monitor, deletedModels);
                    }
                });

                if (modifiedDoc == null)
                    return;

                ModifyDocument(messages.Item3, DocumentModifier.FromResult(originalDoc, modifiedDoc));
            }
        }

        /// <summary>
        /// Set the name of the given tree node to a new value.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="newLabel"></param>
        /// <returns>true if the caller should cancel the edit. false otherwise.</returns>
        public bool EditTreeNodeLabel(FilesTreeNode node, string newLabel)
        {
            if (node == null || string.IsNullOrEmpty(newLabel) || !node.SupportsRename())
                return true;

            if (!(node.Model is IFileRenameable renameable))
                return true;

            var oldLabel = node.Name;

            // Validate input - cancel event if the old and new names match
            if (string.Compare(oldLabel, newLabel, StringComparison.CurrentCulture) == 0)
            {
                return true;
            }

            // Validate the new name
            if (!renameable.ValidateNewName(SkylineWindow.DocumentUI, newLabel, out string errorMessage))
            {
                MultiButtonMsgDlg.Show(this, errorMessage, MessageBoxButtons.OK);
                return true;
            }

            lock (SkylineWindow.GetDocumentChangeLock())
            {
                var originalDoc = SkylineWindow.Document;
                ModifiedDocument modifiedDoc = null;

                using var longWaitDlg = new LongWaitDlg();
                longWaitDlg.PerformWork(this, 750, progressMonitor =>
                {
                    using var monitor = new SrmSettingsChangeMonitor(progressMonitor, longWaitDlg.Text, SkylineWindow);
                    modifiedDoc = renameable.PerformRename(originalDoc, monitor, newLabel);
                });

                ModifyDocument(renameable.AuditLogMessageResource, DocumentModifier.FromResult(originalDoc, modifiedDoc));
            }

            return false;
        }

        public void DropNodes(IList<FilesTreeNode> draggedNodes, FilesTreeNode primaryDraggedNode, FilesTreeNode dropNode, MoveType moveType, DragDropEffects effect)
        {
            try
            {
                if (effect != DragDropEffects.Move || dropNode == null || !dropNode.IsDroppable() || draggedNodes.Count == 0 || draggedNodes.Contains(dropNode))
                    return;

                var draggedModels = draggedNodes.Select(item => item.Model).ToList();

                // Adjust moveType if dragged nodes were dropped on the parent folder in which case
                // dropped nodes should move to the bottom of the list
                if (dropNode.Model is ReplicatesFolder || dropNode.Model is SpectralLibrariesFolder)
                    moveType = MoveType.move_last;

                lock (SkylineWindow.GetDocumentChangeLock())
                {
                    var originalDoc = SkylineWindow.Document;
                    ModifiedDocument modifiedDoc = null;

                    using var longWaitDlg = new LongWaitDlg();
                    longWaitDlg.PerformWork(this, 750, progressMonitor =>
                    {
                        using var monitor = new SrmSettingsChangeMonitor(progressMonitor, longWaitDlg.Text, SkylineWindow);

                        if (primaryDraggedNode.Model is Replicate)
                        {
                            modifiedDoc = Replicate.Rearrange(originalDoc, monitor, draggedModels, dropNode.Model, moveType);
                        }
                        else if (primaryDraggedNode.Model is SpectralLibrary)
                        {
                            modifiedDoc = SpectralLibrary.Rearrange(originalDoc, monitor, draggedModels, dropNode.Model, moveType);
                        }
                    });

                    if (modifiedDoc == null)
                        return;

                    ModifyDocument(FilesTreeResources.Drag_and_Drop, DocumentModifier.FromResult(originalDoc, modifiedDoc));
                }

                // After the drop, re-select the dragged nodes to paint the nodes blue and maintain the user's selection.
                // This is a tricky process which must be done in a specific order.
                filesTree.SelectedNodes.Clear();

                foreach (var node in draggedNodes)
                {
                    filesTree.SelectNode(node, true);
                }

                // Do not move.
                //
                // Set the SelectedNode without clearing already selected items. This trickery is necessary
                // because the usual way of setting SelectedNode on a TreeView clears other selected items and
                // setting SelectedNode prior to calling SelectNode(...) mis-orders dropped items.
                filesTree.SelectNodeWithoutResettingSelection(primaryDraggedNode);

                filesTree.Invalidate();
                filesTree.Focus();
            }
            finally
            {
                HideDragAndDropEffects(false);
            }
        }

        #region ITipDisplayer implementation

        public Rectangle ScreenRect => Screen.GetBounds(FilesTree);

        public bool AllowDisplayTip => IgnoreFocus || FilesTree.Focused;

        public Rectangle RectToScreen(Rectangle r)
        {
            return FilesTree.RectangleToScreen(r);
        }

        #endregion

        #region Test helpers for tooltip testing

        /// <summary>
        /// Test helper property to determine if tooltip is visible
        /// </summary>
        public bool IsTipVisible => _nodeTip.Visible;

        public string NodeTipText => _nodeTip?.TipText;

        /// <summary>
        /// Test helper property to control focus behavior during tooltip testing
        /// </summary>
        public bool IgnoreFocus { get; set; }

        /// <summary>
        /// Test helper method to simulate mouse movement for tooltip testing.
        /// Takes a point in FilesTree coordinates and transforms it appropriately.
        /// </summary>
        public void MoveMouseOnTree(Point pt)
        {
            // Point is already in FilesTree coordinates, which is what FilesTree_MouseMove expects
            FilesTree_MouseMove(pt, MouseButtons.None);
        }

        #endregion

        #region Test helpers for context menu testing

        /// <summary>
        /// Test helper property to access the context menu for validation
        /// </summary>
        public ContextMenuStrip TreeContextMenu => filesTreeContextMenu;

        /// <summary>
        /// Test helper properties to access menu items for localization-safe validation
        /// </summary>
        public ToolStripMenuItem ShowFileNamesMenuItem => showFileNamesMenuItem;
        public ToolStripMenuItem LibraryExplorerMenuItem => libraryExplorerMenuItem;
        public ToolStripMenuItem ManageResultsMenuItem => manageResultsMenuItem;
        public ToolStripMenuItem OpenAuditLogMenuItem => openAuditLogMenuItem;
        public ToolStripMenuItem EditMenuItem => editMenuItem;
        public ToolStripMenuItem OpenLibraryInLibraryExplorerMenuItem => openLibraryInLibraryExplorerMenuItem;
        public ToolStripMenuItem OpenContainingFolderMenuItem => openContainingFolderMenuItem;
        public ToolStripMenuItem SelectReplicateMenuItem => selectReplicateMenuItem;
        public ToolStripMenuItem RemoveAllMenuItem => removeAllMenuItem;
        public ToolStripMenuItem RemoveMenuItem => removeMenuItem;
        public ToolStripMenuItem DebugRefreshTreeMenuItem => debugRefreshTreeMenuItem;

        public bool IsDebugMenu(ToolStripMenuItem item)
        {
            return ReferenceEquals(item, DebugRefreshTreeMenuItem);
        }

        /// <summary>
        /// Test helper flag to signal when the context menu Opening event has completed
        /// </summary>
        public bool? ContextMenuShown { get; set; }

        /// <summary>
        /// Test helper method to programmatically open the context menu for a node.
        /// This triggers the Opening event which populates the menu items based on the node type.
        /// </summary>
        public void ShowContextMenuForNode(FilesTreeNode node)
        {
            if (node == null)
                return;

            ContextMenuShown = null;

            // Select the node first (required by FilesTree_ContextMenuStrip_Opening)
            FilesTree.SelectedNode = node;

            // Show the context menu - triggers the Opening event naturally
            var rect = node.Bounds;
            var pt = new Point((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
            filesTreeContextMenu.Show(FilesTree, pt);
        }

        #endregion

        #region Test helpers for drag-drop simulation

        public IDragDropHandler DragDropHandler
        {
            get => _dragDropHandler;
            set => _dragDropHandler = value ?? DefaultDragDropHandler.Instance;
        }

        public bool IsRemoveDropTargetVisible => _dropTargetRemove.Visible;

        public void SimulateMouseDown(Point treePoint)
        {
            FilesTree_MouseDown(null, new MouseEventArgs(MouseButtons.Left, 1, treePoint.X, treePoint.Y, 0));
        }

        public void SimulateMouseMoveWithLeftButton(Point treePoint)
        {
            FilesTree_MouseMove(treePoint, MouseButtons.Left);
        }

        public void RaiseDragEnter(DragEventArgs e)
        {
            FilesTree_DragEnter(filesTree, e);
        }

        public void RaiseDragOver(DragEventArgs e)
        {
            FilesTree_DragOver(filesTree, e);
        }

        public void RaiseDragLeave()
        {
            FilesTree_DragLeave(filesTree, EventArgs.Empty);
        }

        public void RaiseDragDrop(DragEventArgs e)
        {
            FilesTree_DragDrop(filesTree, e);
        }

        public void RaiseRemoveTargetDragEnter(DragEventArgs e)
        {
            DropTargetRemove_DragEnter(_dropTargetRemove, e);
        }

        public void RaiseRemoveTargetDragDrop(DragEventArgs e)
        {
            DropTargetRemove_DragDrop(_dropTargetRemove, e);
        }

        public void RaiseQueryContinueDrag(bool escapePressed)
        {
            var args = new QueryContinueDragEventArgs(0, escapePressed, DragAction.Continue);
            FilesTree_QueryContinueDrag(filesTree, args);
        }

        #endregion

        // TreeNode => Open Context Menu
        private void FilesTree_ContextMenuStrip_Opening(object sender, CancelEventArgs e)
        {
            var filesTreeNode = (FilesTreeNode)FilesTree.SelectedNode;

            _nodeTip.HideTip();

            // Node-specific options - start hidden
            libraryExplorerMenuItem.Visible = false;
            manageResultsMenuItem.Visible = false;
            openAuditLogMenuItem.Visible = false;
            editMenuItem.Visible = false;
            openLibraryInLibraryExplorerMenuItem.Visible = false;
            openContainingFolderMenuItem.Visible = false;
            selectReplicateMenuItem.Visible = false;
            removeAllMenuItem.Visible = false;
            removeMenuItem.Visible = false;
            debugRefreshTreeMenuItem.Visible = false;

            // Track which items we make visible for debugging and to determine if separator should show
            var visibleItems = new List<ToolStripMenuItem>();

            // Local function to make menu item visible and track it
            void SetMenuItemVisible(ToolStripMenuItem item, bool enabled = true)
            {
                item.Visible = true;
                item.Enabled = enabled;
                visibleItems.Add(item);
            }

            // Only offer "Open Containing Folder" if (1) supported by this tree node
            // and (2) the file's state is "available"
            if (filesTreeNode.SupportsOpenContainingFolder())
                SetMenuItemVisible(openContainingFolderMenuItem, filesTreeNode.LocalFileIsAvailable());

            // Show Remove and Remove All options if supported by the selected node. But only enable them if
            // all selected nodes are of the same type.
            var allSameType = FilesTree.SelectedNodes.Cast<FilesTreeNode>().Select(node => node.Model.GetType()).Distinct().Count() == 1;
            if (filesTreeNode.SupportsRemoveItem())
                SetMenuItemVisible(removeMenuItem, allSameType);

            if (filesTreeNode.SupportsRemoveAllItems())
                SetMenuItemVisible(removeAllMenuItem, allSameType);

            switch (filesTreeNode.Model)
            {
                case ReplicatesFolder _:
                    SetMenuItemVisible(manageResultsMenuItem);
                    break;
                case Replicate _:
                case ReplicateSampleFile _:
                    SetMenuItemVisible(selectReplicateMenuItem);
                    break;
                case SpectralLibrariesFolder _:
                    SetMenuItemVisible(libraryExplorerMenuItem);
                    break;
                case SpectralLibrary _:
                    SetMenuItemVisible(openLibraryInLibraryExplorerMenuItem);
                    SetMenuItemVisible(editMenuItem);
                    break;
                case SkylineAuditLog _:
                    SetMenuItemVisible(openAuditLogMenuItem);
                    break;
                case BackgroundProteome _:
                case RTCalc _:
                case OptimizationLibrary _:
                case IonMobilityLibrary _:
                    SetMenuItemVisible(editMenuItem);
                    break;
                case SkylineFile _:
                    if(Debugger.IsAttached)
                        SetMenuItemVisible(debugRefreshTreeMenuItem);
                    break;
            }

            // Show separator only if there are node-specific items
            contextMenuSeparator.Visible = visibleItems.Count > 0;

            // Form-wide options - always visible at the bottom
            showFileNamesMenuItem.Visible = true;
            showFileNamesMenuItem.Checked = SkylineFile.ShowFileNames;

            // Always show the menu since we have at least the form-wide "Show File Names" option
            ContextMenuShown = true;
            e.Cancel = false;
        }

        private void FilesTree_ShowFileNamesMenuItem(object sender, EventArgs e)
        {
            DoShowFileNames(!SkylineFile.ShowFileNames);
        }

        /// <summary>
        /// Sets the ShowFileNames preference and refreshes the tree to update all node display text.
        /// </summary>
        /// <param name="show">true to show file names (e.g., "Rat_mini.protdb"), false to show resource names (e.g., "Rat mini")</param>
        public void DoShowFileNames(bool show)
        {
            SkylineFile.ShowFileNames = show;
            Settings.Default.FilesViewShowFileNames = show;
            FilesTree.UpdateNodeStates();
        }

        /// <summary>
        /// Context menu used for debugging to force a refresh of the entire tree. This method is also a convenient
        /// place to set a breakpoint to debug internal tree state.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FilesTree_DebugRefreshTreeMenuItem(object sender, EventArgs e)
        {
            FilesTree.UpdateNodeStates();
        }

        private void FilesTree_TreeNodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            DoubleClickNode(e.Node as FilesTreeNode);
        }

        public void DoubleClickNode(FilesTreeNode filesTreeNode)
        {
            switch (filesTreeNode?.Model)
            {
                case SkylineAuditLog _:
                    OpenAuditLog();
                    break;
                case SpectralLibrary _:
                    OpenLibraryExplorerDialog(filesTreeNode); 
                    break;
                case ReplicateSampleFile _:
                    ActivateReplicate(filesTreeNode);
                    break;
                case BackgroundProteome _:
                    OpenEditBackgroundProteomeDialog(filesTreeNode);
                    break;
                case RTCalc _:
                    OpenEditRTCalculatorDialog(filesTreeNode);
                    break;
                case OptimizationLibrary _:
                    OpenEditOptimizationLibraryDialog(filesTreeNode);
                    break;
                case IonMobilityLibrary _:
                    OpenEditIonMobilityLibraryDialog(filesTreeNode);
                    break;
            }
        }

        private void FilesTree_BeforeCollapse(object sender, TreeViewCancelEventArgs e)
        {
            // CONSIDER: change FilesTree UI so the root node is a label and not
            //           actually a TreeNode. Akin to VisualStudio's Solution Explorer.
            // Prevent collapse of root node. 
            if (e.Node == filesTree.Root)
            {
                e.Cancel = true;
            }
        }

        private void FilesTree_OpenContainingFolderMenuItem(object sender, EventArgs e)
        {
            OpenContainingFolder(FilesTree.SelectedNode);
        }

        private void FilesTree_ManageResultsMenuItem(object sender, EventArgs e)
        {
            OpenManageResultsDialog();
        }

        private void FilesTree_OpenLibraryExplorerMenuItem(object sender, EventArgs e) 
        {
            OpenLibraryExplorerDialog();
        }

        private void FilesTree_OpenAuditLogMenuItem(object sender, EventArgs e)
        {
            OpenAuditLog();
        }

        private void FilesTree_ActivateReplicateMenuItem(object sender, EventArgs e)
        {
            ActivateReplicate(FilesTree.SelectedNode);
        }

        private void FilesTree_OpenLibraryInLibraryExplorerMenuItem(object sender, EventArgs e)
        {
            OpenLibraryExplorerDialog(FilesTree.SelectedNode);
        }

        private void FilesTree_RemoveAllMenuItem(object sender, EventArgs e)
        {
            var selection = (FilesTreeNode)FilesTree.SelectedNode;
            RemoveAll(selection);
        }

        private void FilesTree_RemoveMenuItem(object sender, EventArgs e)
        {
            var selection = FilesTree.SelectedNodes.Cast<FilesTreeNode>().ToList();
            RemoveSelected(selection);
        }

        private void FilesTree_EditMenuItem(object sender, EventArgs e)
        {
            EditNode(FilesTree.SelectedNode as FilesTreeNode);
        }

        public void EditNode(FilesTreeNode filesTreeNode)
        {
            switch (filesTreeNode?.Model)
            {
                case BackgroundProteome _:
                    OpenEditBackgroundProteomeDialog(filesTreeNode);
                    break;
                case RTCalc _:
                    OpenEditRTCalculatorDialog(filesTreeNode);
                    break;
                case OptimizationLibrary _:
                    OpenEditOptimizationLibraryDialog(filesTreeNode);
                    break;
                case IonMobilityLibrary _:
                    OpenEditIonMobilityLibraryDialog(filesTreeNode);
                    break;
                case SpectralLibrary _:
                    OpenEditSpectralLibraryDialog(filesTreeNode);
                    break;
            }
        }

        // FilesTree => initiate drag-and-drop, hide tooltips 
        private void FilesTree_MouseMove(object sender, MouseEventArgs e)
        {
            FilesTree_MouseMove(e.Location, e.Button);
        }

        private void FilesTree_MouseDown(object sender, MouseEventArgs e)
        {
            _moveThreshold.Location = e.Location;
        }

        private void FilesTree_LostFocus(object sender, EventArgs e)
        {
            _nodeTip?.HideTip();
        }

        private void FilesTree_MouseLeave(object sender, EventArgs e)
        {
            _nodeTip?.HideTip();
        }

        private void FilesTree_MouseMove(Point location, MouseButtons button)
        {
            // Skip if the mouse hasn't moved enough to do stuff
            if (!_moveThreshold.Moved(location))
                return;

            // Ok, mouse moved enough - reset previous location
            _moveThreshold.Location = null;

            var node = FilesTree.GetNodeAt(location) as TreeNodeMS;
            var tipProvider = node as ITipProvider;

            if (tipProvider != null && !tipProvider.HasTip)
                tipProvider = null;

            if (tipProvider != null)
            {
                var rectCapture = node.BoundsMS;
                if (!rectCapture.Contains(location))
                    _nodeTip.HideTip();
                else
                    _nodeTip.SetTipProvider(tipProvider, rectCapture, location);
            }
            else
            {
                _nodeTip?.HideTip();
            }

            // Trigger drag-and-drop. The advanced approach is needed because the ItemDrag event does not trigger correctly
            // when users set a custom font - despite calculating the custom width for a given font size on FilesTreeNode.
            if (button == MouseButtons.Left)
            {
                var selectedNode = FilesTree.SelectedNode;
                var selectedNodes = FilesTree.SelectedNodes.Cast<FilesTreeNode>().ToList();
            
                if (selectedNode == null || selectedNodes.Count == 0)
                    return;
            
                _nodeTip?.HideTip();
            
                // Warning - order matters! Do this first to either short-circuit if attempting to
                // select an un-draggable node or collect nodes to drag
                var draggedNodes = new List<FilesTreeNode>();
                foreach (var item in selectedNodes)
                {
                    if (item.IsDraggable())
                    {
                        draggedNodes.Add(item);
                    }
                    // Cannot drag the .sky file and need to prevent running the next check on Root
                    else if (ReferenceEquals(item, FilesTree.Root))
                    {
                        return;
                    }
                    else if (((FilesTreeNode)item.Parent).IsDraggable() && selectedNodes.Contains(item.Parent))
                    {
                        /* ignore node and keep going - node's parent is selected and will be added to draggedNodes */
                    }
                    else return;
                }
            
                // Clear selection of dragged nodes, which omits the light blue selection color
                filesTree.SelectedNodes.Clear();
            
                // Set the location for the remove drop target based on the tree's SelectedNode. This
                // deliberately ignore window resize events. They cancel an active drag-and-drop operation.
                // No need to do extra work handling them.
                if (!_dropTargetRemove.Visible)
                {
                    // Vertically center the drop target on the SelectNode's midline
                    var x = FilesTree.Bounds.X + FilesTree.ClientSize.Width - _dropTargetRemove.Width - 10 /* extra padding */;
                    var y = FilesTree.SelectedNode.Bounds.Y + FilesTree.SelectedNode.Bounds.Height / 2 - _dropTargetRemove.Height / 2;
            
                    _dropTargetRemove.Location = new Point(x, y);
                }
            
                ShowRemoveDropTarget();
            
                var dataObj = new DataObject();
                dataObj.SetData(typeof(PrimarySelectedNode), selectedNode);
                dataObj.SetData(typeof(FilesTreeNode), draggedNodes);
            
                _dragDropHandler.DoDragDrop(filesTree, dataObj, DragDropEffects.Move);
            }
        }

        private void FilesTree_BeforeLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            var filesTreeNode = (FilesTreeNode)e.Node;

            // Only allow edit on Replicate tree nodes
            if (!filesTreeNode.SupportsRename())
            {
                e.CancelEdit = true;
            }
        }

        private void FilesTree_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            var cancel = EditTreeNodeLabel((FilesTreeNode)e.Node, e.Label);
            if (cancel)
            {
                e.CancelEdit = true;
            }
        }

        private void FilesTree_KeyDown(object sender, KeyEventArgs e)
        {
            _nodeTip.HideTip();
        }

        // Select the expected drop location
        private void FilesTree_DragOver(object sender, DragEventArgs e)
        {
            var point = filesTree.PointToClient(new Point(e.X, e.Y));
            var node = (FilesTreeNode)filesTree.GetNodeAt(point);
            var primaryDraggedNode = (FilesTreeNode)e.Data.GetData(typeof(PrimarySelectedNode));

            if (node == null)
                return;

            // Select node at the drop target
            if (PrimarySelectedNode.DoesDropTargetAcceptDraggedNode(node, primaryDraggedNode))
            {
                e.Effect = DragDropEffects.Move;
                filesTree.SelectedNode = node;

                ShowRemoveDropTarget();
            }
            else
            {
                e.Effect = DragDropEffects.None;

                HideDragAndDropEffects();
            }

            // Re-paint "current" and surrounding nodes as the mouse moves so
            // selection and the insert line repaint correctly
            filesTree.InvalidateNode((FilesTreeNode)node.PrevVisibleNode);
            filesTree.InvalidateNode(node);

            // CONSIDER: does setting AutoScroll on the form negate the need for this code?
            // Scroll the tree if near the top or bottom edge
            var ptView = FilesTree.PointToClient(new Point(e.X, e.Y));
            if (ptView.Y < 10)
            {
                var nodeTop = FilesTree.TopNode;
                if (nodeTop != null && nodeTop.PrevVisibleNode != null)
                    FilesTree.TopNode = nodeTop.PrevVisibleNode;
            }
            if (ptView.Y > FilesTree.Bottom - 10)
            {
                var nodeTop = FilesTree.TopNode;
                if (nodeTop != null && nodeTop.NextVisibleNode != null)
                    FilesTree.TopNode = nodeTop.NextVisibleNode;
            }
        }

        private void FilesTree_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(FilesTreeNode)))
                e.Effect = DragDropEffects.Move;

            ShowRemoveDropTarget();
        }

        private void FilesTree_DragDrop(object sender, DragEventArgs e)
        {
            var dropPoint = filesTree.PointToClient(new Point(e.X, e.Y));
            var dropNode = (FilesTreeNode)filesTree.GetNodeAt(dropPoint);

            if (dropNode == null)
            {
                HideDragAndDropEffects();
                return;
            }

            var primaryDraggedNode = (FilesTreeNode)e.Data.GetData(typeof(PrimarySelectedNode));
            var dragNodeList = (IList<FilesTreeNode>)e.Data.GetData(typeof(FilesTreeNode));

            // Do not move.
            //
            // This check reads the location of the mouse. If dropping on the last node, it
            // checks whether the drop occurred in the upper half or lower half of that TreeNode
            // to decide whether to insert nodes above or below the last node.
            //
            // Automated tests will fail unpredictably if this check moves into DropNodes because
            // the test will read a mouse location that has nothing to do with the test.
            var dropLocation = 
                dropNode.IsLastNodeInFolder() && dropNode.IsMouseInLowerHalf() ? MoveType.move_last : MoveType.move_to;

            DropNodes(dragNodeList, primaryDraggedNode, dropNode, dropLocation, e.Effect);
        }

        // CONSIDER: when drag operation canceled, would be nice to re-select the original dragged nodes.
        //           But it's not obvious how to get the list of dragged nodes. TreeView doesn't seem to
        //           make them available during DragLeave events, so do they need to be stored separately?
        private void FilesTree_QueryContinueDrag(object sender, QueryContinueDragEventArgs e)
        {
            if (e.EscapePressed)
            {
                e.Action = DragAction.Cancel;
                HideDragAndDropEffects();
            }
        }

        private void DropTargetRemove_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.AllowedEffect;

            // Save a ref to the currently selected node so it can repaint
            // once selection is cleared
            var tmp = filesTree.SelectedNode;

            filesTree.SelectedNode = null;
            filesTree.SelectedNodes.Clear();

            filesTree.InvalidateNode((FilesTreeNode)tmp);

            ShowRemoveDropTarget(true);
        }

        private void DropTargetRemove_DragDrop(object sender, DragEventArgs e)
        {
            var nodes = (IList<FilesTreeNode>)e.Data.GetData(typeof(FilesTreeNode));

            try
            {
                RemoveSelected(nodes);
            }
            finally
            {
                HideDragAndDropEffects();
            }
        }

        private void FilesTree_DragLeave(object sender, EventArgs e)
        {
            var mouseLocation = MousePosition;
            var inside = IsPointInRectangle(mouseLocation, Bounds);

            if (!inside)
            {
                HideDragAndDropEffects();
            }
        }

        private void ShowRemoveDropTarget(bool highlight = false)
        {
            FilesTree.IsDuringDragAndDrop = true;

            _dropTargetRemove.Visible = true;
            _dropTargetRemove.BackColor = highlight ? Color.LightYellow : Color.WhiteSmoke;
        }

        private void HideDragAndDropEffects(bool clearSelection = true)
        {
            FilesTree.IsDuringDragAndDrop = false;

            // Clear selection so the insert highlight line does not appear when mouse
            // is not over a valid drop target
            if (clearSelection)
            {
                filesTree.SelectedNode = null;
                filesTree.SelectedNodes.Clear();
            }

            FilesTree.Invalidate();

            _dropTargetRemove.Visible = false;
            _dropTargetRemove.BackColor = Color.WhiteSmoke;
        }

        private DialogResult ConfirmDelete(int items, string oneItem, string manyItems)
        {
            var msg = items == 1 ? oneItem : manyItems;
            return ConfirmDelete(msg);
        }

        private DialogResult ConfirmDelete(string confirmMsg)
        {
            var confirmDlg = new MultiButtonMsgDlg(confirmMsg, MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false);

            // Activate the "No" button so a keystroke cancels the delete action
            confirmDlg.ActiveControl = confirmDlg.VisibleButtons.Last();
            confirmDlg.KeyUp += (sender, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    confirmDlg.BtnCancelClick();
                }
            };

            return confirmDlg.ShowAndDispose(this);
        }

        private static Panel CreateDropTarget(Image icon, DockStyle dockStyle)
        {
            var dropTarget = new Panel
            {
                Dock = dockStyle,
                Padding = new Padding(2),
                Size = new Size { Height = 30, Width = 30 },
                BorderStyle = BorderStyle.FixedSingle
            };

            var pictureBox = new PictureBox
            {
                Image = icon,
                SizeMode = PictureBoxSizeMode.CenterImage,
                Dock = DockStyle.Fill,
            };

            dropTarget.Controls.Add(pictureBox);

            return dropTarget;
        }

        private static bool IsPointInRectangle(Point point, Rectangle rectangle)
        {
            return point.X >= rectangle.X &&
                   point.X <= rectangle.X + rectangle.Width &&
                   point.Y >= rectangle.Y &&
                   point.Y <= rectangle.Y + rectangle.Height;
        }
    }

    // Used as a key for passing drag-and-drop data between event handlers
    public sealed class PrimarySelectedNode
    {
        public static bool DoesDropTargetAcceptDraggedNode(FilesTreeNode possibleDropTarget, FilesTreeNode primaryDraggedNode)
        {
            if (primaryDraggedNode == null)
                return false;
            else return possibleDropTarget.Model.GetType() == primaryDraggedNode.Model.GetType() || 
                        possibleDropTarget.Model.GetType() == primaryDraggedNode.ParentFTN.Model.GetType();
        }
    }
}
