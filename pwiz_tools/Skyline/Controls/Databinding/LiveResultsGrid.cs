/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Find;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;
using Transition = pwiz.Skyline.Model.Databinding.Entities.Transition;
// ReSharper disable NonLocalizedString
//TODO: Should this be localized?
namespace pwiz.Skyline.Controls.Databinding
{
    public partial class LiveResultsGrid : DataboundGridForm
    {
        private readonly SkylineDataSchema _dataSchema;
        private IList<IdentityPath> _selectedIdentityPaths = ImmutableList.Empty<IdentityPath>();
        private SequenceTree _sequenceTree;
        private IList<AnnotationDef> _annotations;
        private FindResult _pendingFindResult;

        public LiveResultsGrid(SkylineWindow skylineWindow)
        {
            InitializeComponent();
            BindingListSource = bindingListSource;
            DataGridView = boundDataGridView;
            NavBar = navBar;

            Icon = Resources.Skyline;
            SkylineWindow = skylineWindow;
            _dataSchema = new SkylineDataSchema(skylineWindow, SkylineDataSchema.GetLocalizedSchemaLocalizer());
            DataGridViewPasteHandler.Attach(skylineWindow, boundDataGridView);
        }

        // TODO(nicksh): replace this with ResultsGrid.IStateProvider
        public SkylineWindow SkylineWindow { get; private set; }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SkylineWindow.DocumentUIChangedEvent += SkylineWindow_DocumentUIChangedEvent;
            OnDocumentChanged();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            SkylineWindow.DocumentUIChangedEvent -= SkylineWindow_DocumentUIChangedEvent;
            SetSequenceTree(null);
            base.OnHandleDestroyed(e);
        }

        private void SkylineWindow_DocumentUIChangedEvent(object sender, DocumentChangedEventArgs e)
        {
            OnDocumentChanged();
        }

        private void OnDocumentChanged()
        {
            SetSequenceTree(SkylineWindow.SequenceTree);
            var newAnnotations = ImmutableList.ValueOf(SkylineWindow.DocumentUI.Settings.DataSettings.AnnotationDefs);
            if (!Equals(newAnnotations, _annotations))
            {
                _annotations = newAnnotations;
                UpdateViewContext();
            }
        }

        private void SetSequenceTree(SequenceTree sequenceTree)
        {
            if (ReferenceEquals(_sequenceTree, sequenceTree))
            {
                return;
            }
            if (null != _sequenceTree)
            {
                _sequenceTree.AfterSelect -= SequenceTreeOnAfterSelect;
            }
            _sequenceTree = sequenceTree;
            if (null != _sequenceTree)
            {
                _sequenceTree.AfterSelect += SequenceTreeOnAfterSelect;
                SelectedIdentityPaths = _sequenceTree.SelectedPaths;
            }
        }

        private void SequenceTreeOnAfterSelect(object sender, TreeViewEventArgs args)
        {
            SelectedIdentityPaths = _sequenceTree.SelectedPaths;
        }

        public IList<IdentityPath> SelectedIdentityPaths
        {
            get { return _selectedIdentityPaths; }
            set
            {
                if (_selectedIdentityPaths.SequenceEqual(value))
                {
                    return;
                }
                _selectedIdentityPaths = ImmutableList.ValueOf(value);
                UpdateViewContext();
            }
        }

        public int? GetReplicateIndex()
        {
            return GetReplicateIndex(bindingListSource.Current as RowItem);
        }

        public void SetReplicateIndex(int replicateIndex)
        {
            _pendingFindResult = null;
            if (replicateIndex == GetReplicateIndex())
            {
                return;
            }
            for (int iRow = 0; iRow < bindingListSource.Count; iRow++)
            {
                if (replicateIndex == GetReplicateIndex(bindingListSource[iRow] as RowItem))
                {
                    bindingListSource.Position = iRow;
                }
            }
        }

        private int? GetReplicateIndex(RowItem rowItem)
        {
            if (rowItem == null)
            {
                return null;
            }
            var replicate = rowItem.Value as Replicate;
            if (null != replicate)
            {
                return replicate.ReplicateIndex;
            }
            var result = rowItem.Value as Result;
            if (null != result)
            {
                return result.GetResultFile().Replicate.ReplicateIndex;
            }
            return null;
        }

        public ChromFileInfoId GetCurrentChromFileInfoId()
        {
            return GetChromFileInfoId(bindingListSource.Current as RowItem);
        }

        private ChromFileInfoId GetChromFileInfoId(RowItem rowItem)
        {
            if (null == rowItem)
            {
                return null;
            }
            var result = rowItem.Value as Result;
            if (null != result)
            {
                return result.GetResultFile().ChromFileInfoId;
            }
            return null;
        }

        private void UpdateViewContext()
        {
            RememberActiveView();
            IList rowSource = null;
            Type rowType = null;
            string builtInViewName = null;
            if (_selectedIdentityPaths.Count == 1)
            {
                var identityPath = _selectedIdentityPaths[0];
                if (identityPath.Length == 2)
                {
                    rowSource = new PeptideResultList(new Peptide(_dataSchema, identityPath));
                    rowType = typeof (PeptideResult);
                    builtInViewName = "Peptide Results";
                }
                else if (identityPath.Length == 3)
                {
                    rowSource = new PrecursorResultList(new Precursor(_dataSchema, identityPath));
                    rowType = typeof (PrecursorResult);
                    builtInViewName = "Precursor Results";
                }
                else if (identityPath.Length == 4)
                {
                    rowSource = new TransitionResultList(new Transition(_dataSchema, identityPath));
                    rowType = typeof (TransitionResult);
                    builtInViewName = "Transition Results";
                }
            }
            else
            {
                var pathLengths = _selectedIdentityPaths.Select(path => path.Length).Distinct().ToArray();
                if (pathLengths.Length == 1)
                {
                    var pathLength = pathLengths[0];
                    if (pathLength == 3)
                    {
                        rowSource = new MultiPrecursorResultList(_dataSchema,
                            _selectedIdentityPaths.Select(idPath => new Precursor(_dataSchema, idPath)));
                        rowType = typeof (MultiPrecursorResult);
                        builtInViewName = "Multiple Precursor Results";
                    }
                    if (pathLength == 4)
                    {
                        rowSource = new MultiTransitionResultList(_dataSchema,
                            _selectedIdentityPaths.Select(idPath => new Transition(_dataSchema, idPath)));
                        rowType = typeof (MultiTransitionResult);
                        builtInViewName = "Multiple Transition Results";
                    }
                }
            }
            if (rowSource == null)
            {
                rowSource = new ReplicateList(_dataSchema);
                rowType = typeof (Replicate);
                builtInViewName = "Replicates";
            }
            var parentColumn = ColumnDescriptor.RootColumn(_dataSchema, rowType);
            var builtInViewSpec = SkylineViewContext.GetDefaultViewInfo(parentColumn).GetViewSpec()
                .SetName(builtInViewName).SetRowType(rowType);
            if (null == bindingListSource.ViewContext ||
                !bindingListSource.ViewContext.BuiltInViews.Contains(builtInViewSpec))
            {
                var oldViewContext = bindingListSource.ViewContext as ResultsGridViewContext;
                if (null != oldViewContext)
                {
                    oldViewContext.RememberColumnWidths(boundDataGridView);
                }
                Debug.Assert(null != builtInViewName);
                var builtInView = new ViewInfo(parentColumn, builtInViewSpec);
                var rowSourceInfo = new RowSourceInfo(rowSource, builtInView);
                var viewContext = new ResultsGridViewContext(_dataSchema,
                    new[] {rowSourceInfo});
                ViewInfo activeView = null;
                string activeViewName;
                if (Settings.Default.ResultsGridActiveViews.TryGetValue(rowSourceInfo.Name, out activeViewName))
                {
                    var activeViewSpec = viewContext.CustomViews.FirstOrDefault(view => view.Name == activeViewName);
                    if (null != activeViewSpec)
                    {
                        activeView = viewContext.GetViewInfo(activeViewSpec);
                    }
                }
                activeView = activeView ?? builtInView;
                bindingListSource.SetViewContext(viewContext, activeView);
            }
            bindingListSource.RowSource = rowSource;
        }

        private void RememberActiveView()
        {
            var viewInfo = bindingListSource.ViewInfo;
            if (null != viewInfo)
            {
                var activeViews = Settings.Default.ResultsGridActiveViews;
                activeViews[viewInfo.RowSourceName] = viewInfo.Name;
                Settings.Default.ResultsGridActiveViews = activeViews;
            }
        }

        public void HighlightFindResult(FindResult findResult)
        {
            if (!IsComplete)
            {
                _pendingFindResult = findResult;
                return;
            }
            var bookmarkEnumerator = BookmarkEnumerator.TryGet(_dataSchema.Document, findResult.Bookmark);
            if (bookmarkEnumerator == null)
            {
                return;
            }
            var chromInfo = bookmarkEnumerator.CurrentChromInfo;
            if (chromInfo == null)
            {
                return;
            }
            int? iRowMatch = null;

            for (int iRow = 0; iRow < bindingListSource.Count; iRow++)
            {
                var rowItem = bindingListSource[iRow] as RowItem;
                if (rowItem == null)
                {
                    continue;
                }
                var replicate = rowItem.Value as Replicate;
                if (replicate != null)
                {
                    if (replicate.Files.Any(file => ReferenceEquals(file.ChromFileInfoId, chromInfo.FileId)))
                    {
                        iRowMatch = iRow;
                        break;
                    }
                }
                var result = rowItem.Value as Result;
                if (null != result)
                {
                    if (ReferenceEquals(result.GetResultFile().ChromFileInfoId, chromInfo.FileId))
                    {
                        iRowMatch = iRow;
                        break;
                    }
                }
            }
            if (!iRowMatch.HasValue)
            {
                return;
            }
            bindingListSource.Position = iRowMatch.Value;
            DataGridViewColumn column;
            if (findResult.FindMatch.Note)
            {
                column = FindColumn(PropertyPath.Root.Property("Note"));
            }
            else if (findResult.FindMatch.AnnotationName != null)
            {
                column = FindColumn(PropertyPath.Root.Property(
                    AnnotationDef.ANNOTATION_PREFIX + findResult.FindMatch.AnnotationName));
            }
            else
            {
                return;
            }
            if (null != column && null != DataGridView.CurrentRow)
            {
                DataGridView.CurrentCell = DataGridView.CurrentRow.Cells[column.Index];
            }
        }

        private void bindingListSource_ListChanged(object sender, ListChangedEventArgs e)
        {
            if (e.ListChangedType == ListChangedType.Reset)
            {
                if (null != SkylineWindow.SequenceTree)
                {
                    SetReplicateIndex(SkylineWindow.SequenceTree.ResultsIndex);
                }
            }
        }

        private void boundDataGridView_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            if (null != _pendingFindResult)
            {
                var pendingFindResult = _pendingFindResult;
                _pendingFindResult = null;
                BeginInvoke(new Action<FindResult>(HighlightFindResult), pendingFindResult);
            }
        }

        private void synchronizeSelectionContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ResultsGridSynchSelection = synchronizeSelectionContextMenuItem.Checked;
        }

        private void chooseColumnsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            navBar.CustomizeView();
        }


        private bool _inReplicateChange;
        private void bindingListSource_CurrentChanged(object sender, EventArgs e)
        {
            if (!Settings.Default.ResultsGridSynchSelection || _inReplicateChange || !bindingListSource.IsComplete)
            {
                return;
            }
            int? replicateIndex = GetReplicateIndex(bindingListSource.Current as RowItem);
            try
            {
                _inReplicateChange = true;
                // ReSharper disable once RedundantCheckBeforeAssignment
                if (replicateIndex.HasValue && replicateIndex != SkylineWindow.SelectedResultsIndex)
                {
                    SkylineWindow.SelectedResultsIndex = replicateIndex.Value;
                }
            }
            finally
            {
                _inReplicateChange = false;
            }
        }

        private void contextMenuResultsGrid_Opening(object sender, CancelEventArgs e)
        {
            synchronizeSelectionContextMenuItem.Checked = Settings.Default.ResultsGridSynchSelection;
        }
    }
}
