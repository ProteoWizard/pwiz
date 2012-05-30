/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Find;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Controls
{
    /// <summary>
    /// Displays results in a grid
    /// </summary>
    public class ResultsGrid : DataGridViewEx
    {
        private readonly Dictionary<RowIdentifier, DataGridViewRow> _chromInfoRows 
            = new Dictionary<RowIdentifier, DataGridViewRow>();
        private readonly Stack<DataGridViewRow> _unassignedRows
            = new Stack<DataGridViewRow>();

        private Dictionary<string, DataGridViewColumn> _annotationColumns 
            = new Dictionary<string, DataGridViewColumn>();
        private Dictionary<string, DataGridViewColumn> _ratioColumns
            = new Dictionary<string, DataGridViewColumn>();

        private bool _inCommitEdit;
        private bool _inReplicateChange;

        public interface IStateProvider
        {
            TreeNodeMS SelectedNode { get; }
            IList<TreeNodeMS> SelectedNodes { get; }
            int SelectedResultsIndex { get; set; }
        }

        private class DefaultStateProvider : IStateProvider
        {
            public TreeNodeMS SelectedNode { get { return null; } }
            public IList<TreeNodeMS> SelectedNodes { get { return null; } }
            public int SelectedResultsIndex { get; set; }
        }

        public ResultsGrid()
        {
            // Replicate
            Columns.Add(ReplicateNameColumn
                = new DataGridViewTextBoxColumn
                    {
                        Name = "ReplicateName",
                        HeaderText = "Replicate Name",
                        ReadOnly = true,
                    });
            Columns.Add(FileNameColumn
                = new DataGridViewTextBoxColumn
                    {
                        Name = "FileName",
                        HeaderText = "File Name",
                        ReadOnly = true
                    });
            Columns.Add(SampleNameColumn
                = new DataGridViewTextBoxColumn
                    {
                        Name = "SampleName",
                        HeaderText = "Sample Name",
                        ReadOnly = true
                    });
            Columns.Add(ModifiedTimeColumn
                = new DataGridViewTextBoxColumn
                {
                    Name = "ModifiedTime",
                    HeaderText = "Modified Time",
                    ReadOnly = true
                });
            Columns.Add(AcquiredTimeColumn
                = new DataGridViewTextBoxColumn
                {
                    Name = "AcquiredTime",
                    HeaderText = "Acquired Time",
                    ReadOnly = true
                });
            Columns.Add(OptStepColumn
                = new DataGridViewTextBoxColumn
                    {
                        Name = "OptStep",
                        HeaderText = "Opt Step",
                        ReadOnly = true,
                    });

            // Peptide
            Columns.Add(PeptidePeakFoundRatioColumn
                = new DataGridViewTextBoxColumn
                    {
                        Name = "PeptidePeakFoundRatio",
                        HeaderText = "Peptide Peak Found Ratio",
                        ReadOnly = true,
                        DefaultCellStyle = {Format = Formats.PEAK_FOUND_RATIO}
                    });
            Columns.Add(PeptideRetentionTimeColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "PeptideRetentionTime",
                          HeaderText = "Peptide Retention Time",
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.RETENTION_TIME}
                      });
            Columns.Add(RatioToStandardColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "RatioToStandard",
                          HeaderText = "Ratio To Standard",
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.STANDARD_RATIO}
                      });

            // Precursor
            Columns.Add(PrecursorNoteColumn
                = new DataGridViewTextBoxColumn
                {
                    Name = "PrecursorReplicateNote",
                    HeaderText = "Precursor Replicate Note",
                });
            Columns.Add(PrecursorPeakFoundRatioColumn
                = new DataGridViewTextBoxColumn
                    {
                        Name = "PrecursorPeakFound",
                        HeaderText = "Precursor Peak Found Ratio",
                        ReadOnly = true,
                        DefaultCellStyle = {Format = Formats.PEAK_FOUND_RATIO}
                    });
            Columns.Add(BestRetentionTimeColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "BestRetentionTime",
                          HeaderText = "Best Retention Time",
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.RETENTION_TIME}
                      });
            Columns.Add(MaxFwhmColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "MaxFwhm",
                          HeaderText = "Max Fwhm",
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.RETENTION_TIME}
                      });
            Columns.Add(MinStartTimeColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "MinStartTime",
                          HeaderText = "Min Start Time",
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.RETENTION_TIME}
                      });
            Columns.Add(MaxEndTimeColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "MaxEndTime",
                          HeaderText = "Max End Time",
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.RETENTION_TIME}
                      });
            Columns.Add(TotalAreaColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "TotalArea",
                          HeaderText = "Total Area",
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.PEAK_AREA}
                      });
            Columns.Add(TotalBackgroundColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "TotalBackground",
                          HeaderText = "Total Background",
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.PEAK_AREA}
                      });
            Columns.Add(TotalAreaRatioColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "TotalAreaRatio",
                          HeaderText = "Total Area Ratio",
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.STANDARD_RATIO}
                      });
            Columns.Add(CountTruncatedColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "CountTruncated",
                          HeaderText = "Count Truncated",
                          ReadOnly = true
                      });
            Columns.Add(IdentifiedColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "Identified",
                          HeaderText = "Identified",
                          ReadOnly = true
                      });
            Columns.Add(UserSetTotalColumn
                = new DataGridViewTextBoxColumn
                {
                    Name = "UserSetTotal",
                    HeaderText = "User Set Total",
                    ReadOnly = true
                });
            Columns.Add(LibraryDotProductColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "LibraryDotProduct",
                          HeaderText = "Library Dot Product",
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.STANDARD_RATIO}
                      });
            Columns.Add(IsotopeDotProductColumn
                = new DataGridViewTextBoxColumn
                {
                    Name = "IsotopeDotProduct",
                    HeaderText = "Isotope Dot Product",
                    ReadOnly = true,
                    DefaultCellStyle = { Format = Formats.STANDARD_RATIO }
                });
            Columns.Add(OptCollisionEnergyColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "OptCollisionEnergy",
                          HeaderText = "Opt Collision Energy",
                          ReadOnly = true,
                          DefaultCellStyle = { Format = Formats.OPT_PARAMETER }
                      });
            Columns.Add(OptDeclusteringPotentialColumn
                = new DataGridViewTextBoxColumn
                {
                    Name = "OptDeclusteringPotential",
                    HeaderText = "Opt Declustering Potential",
                    ReadOnly = true,
                    DefaultCellStyle = { Format = Formats.OPT_PARAMETER }
                });
            
            // Transitions
            Columns.Add(TransitionNoteColumn
                = new DataGridViewTextBoxColumn
                {
                    Name = "TransitionReplicateNote",
                    HeaderText = "Transition Replicate Note"
                });
            Columns.Add(RetentionTimeColumn 
                = new DataGridViewTextBoxColumn
                  {
                      Name = "RetentionTime",
                      HeaderText = "Retention Time",
                      ReadOnly = true,
                      DefaultCellStyle = { Format = Formats.RETENTION_TIME },
                  });
            Columns.Add(FwhmColumn 
                = new DataGridViewTextBoxColumn
                    {
                        Name = "Fwhm",
                        HeaderText = "Fwhm",
                        ReadOnly = true,
                        DefaultCellStyle = { Format = Formats.RETENTION_TIME },
                    });
            Columns.Add(StartTimeColumn 
                = new DataGridViewTextBoxColumn
                    {
                        Name = "StartTime",
                        HeaderText = "Start Time",
                        ReadOnly = true,
                        DefaultCellStyle = {Format = Formats.RETENTION_TIME},
                    });
            Columns.Add(EndTimeColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "EndTime",
                          HeaderText = "End Time",
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.RETENTION_TIME}
                      });
            Columns.Add(AreaColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "Area",
                          HeaderText = "Area",
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.PEAK_AREA}
                      });
            Columns.Add(BackgroundColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "Background",
                          HeaderText = "Background",
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.PEAK_AREA}
                      });
            Columns.Add(AreaRatioColumn = new DataGridViewTextBoxColumn
                      {
                          Name = "AreaRatio",
                          HeaderText = "Area Ratio",
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.STANDARD_RATIO}
                      });
            Columns.Add(HeightColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "Height",
                          HeaderText = "Height",
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.PEAK_AREA}
                      });
            Columns.Add(TruncatedColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "Truncated",
                          HeaderText = "Truncated",
                          ReadOnly = true,
                      });
            Columns.Add(PeakRankColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "PeakRank",
                          HeaderText = "Peak Rank",
                          ReadOnly = true,
                      });
            Columns.Add(UserSetColumn
                = new DataGridViewTextBoxColumn
                {
                    Name = "UserSet",
                    HeaderText = "User Set",
                    ReadOnly = true,
                });
            KeyDown += ResultsGrid_KeyDown;
            CellEndEdit += ResultsGrid_CellEndEdit;
            CurrentCellChanged += ResultsGrid_CurrentCellChanged;
        }

        public void Init(IDocumentUIContainer documentUiContainer, SequenceTree sequenceTree)
        {
            DocumentUiContainer = documentUiContainer;
            StateProvider = documentUiContainer as IStateProvider ??
                            new DefaultStateProvider();
        }

        private void ResultsGrid_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    DocumentUiContainer.FocusDocument();
                    break;
            }
        }

        private void ResultsGrid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            _inCommitEdit = true;
            try
            {
                if (DocumentUiContainer.InUndoRedo)
                    return;
                Program.MainWindow.ModifyDocument("Edit note", doc => 
                    UpdateDocument(doc, SelectedPaths, Rows[e.RowIndex], Columns, e.ColumnIndex, 
                    PrecursorNoteColumn.Index, TransitionNoteColumn.Index ));
            }
            finally
            {
                _inCommitEdit = false;
            }
        }

        public static SrmDocument UpdateDocument(SrmDocument doc, List<IdentityPath> selectedPaths, DataGridViewRow row, 
            DataGridViewColumnCollection columns, int columnIndex, int precursorNoteColumnIndex, int transitionNoteColumnIndex)
        {
            var rowIdentifier = row.Tag as RowIdentifier;
            if (rowIdentifier == null)
            {
                return doc;
            }

            bool updateAllSteps = selectedPaths.Count > 1;
            bool isNote = columnIndex == precursorNoteColumnIndex || columnIndex == transitionNoteColumnIndex;
            string noteNew = Convert.ToString(row.Cells[columnIndex].Value);
            var annotationDef = columns[columnIndex].Tag as AnnotationDef;
            var value = row.Cells[columnIndex].Value;

            foreach (IdentityPath selPath in selectedPaths)
            {
                var path = selPath;
                var nodeDoc = doc.FindNode(path);
                var transitionGroupDocNode = nodeDoc as TransitionGroupDocNode;
                var transitionDocNode = nodeDoc as TransitionDocNode;
                if (isNote || annotationDef != null)
                {
                    IEnumerable<ChromInfo> chromInfos = new List<ChromInfo>();
                    if (transitionDocNode != null)
                        chromInfos = GetChromInfos(transitionDocNode.Results, rowIdentifier, updateAllSteps);
                    if (transitionGroupDocNode != null)
                        chromInfos = GetChromInfos(transitionGroupDocNode.Results, rowIdentifier, updateAllSteps);
                    foreach (var chromInfo in chromInfos)
                    {
                        TransitionChromInfo chromInfoTrans = chromInfo as TransitionChromInfo;
                        TransitionGroupChromInfo chromInfoTransGroup = chromInfo as TransitionGroupChromInfo;
                        if(chromInfoTrans != null && transitionDocNode != null)
                        {
                            Annotations newAnnotations = isNote
                                                 ? chromInfoTrans.Annotations.ChangeNote(noteNew)
                                                 : chromInfoTrans.Annotations.ChangeAnnotation(annotationDef, value);
                            var chromInfoNew = chromInfoTrans.ChangeAnnotations(newAnnotations);
                            transitionDocNode = transitionDocNode.ChangeResults(ChangeChromInfo(
                                transitionDocNode.Results, rowIdentifier, chromInfoTrans, chromInfoNew));
                            doc = (SrmDocument)doc.ReplaceChild(path.Parent, transitionDocNode);
                        }
                        else if(chromInfoTransGroup != null && transitionGroupDocNode != null)
                        {
                            Annotations newAnnotations = isNote
                                                 ? chromInfoTransGroup.Annotations.ChangeNote(noteNew)
                                                 : chromInfoTransGroup.Annotations.ChangeAnnotation(annotationDef, value);
                           var chromInfoNew = chromInfoTransGroup.ChangeAnnotations(newAnnotations);
                           transitionGroupDocNode = transitionGroupDocNode.ChangeResults(
                               ChangeChromInfo(transitionGroupDocNode.Results, rowIdentifier, chromInfoTransGroup,
                                   chromInfoNew));
                           doc = (SrmDocument)doc.ReplaceChild(path.Parent, transitionGroupDocNode);
                        }
                    }
                }
            }
            return doc;
        }

        private void ResultsGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            if (_inReplicateChange || !ResultsGridForm.SynchronizeSelection)
                return;

            if (CurrentRow != null && CurrentRow.Index != StateProvider.SelectedResultsIndex)
            {
                _inReplicateChange = true;
                try
                {
                    StateProvider.SelectedResultsIndex = ((RowIdentifier)CurrentRow.Tag).ReplicateIndex;
                }
                finally
                {
                    _inReplicateChange = false;
                }
            }
        }


        private static IEnumerable<ChromInfo> GetChromInfos(DocNode nodeDoc, AnnotationDef.AnnotationTarget annotationTarget, RowIdentifier rowIdentifier, bool allOptimizationSteps)
        {
            var transitionDocNode = nodeDoc as TransitionDocNode;
            var transitionGroupDocNode = nodeDoc as TransitionGroupDocNode;
            if (transitionDocNode != null && 0 != (annotationTarget & AnnotationDef.AnnotationTarget.transition_result))
            {
                return GetChromInfos(transitionDocNode.Results, rowIdentifier, allOptimizationSteps); 
            }
            if (transitionGroupDocNode != null && 0 != (annotationTarget & AnnotationDef.AnnotationTarget.precursor_result))
            {
                return GetChromInfos(transitionGroupDocNode.Results, rowIdentifier, allOptimizationSteps);
            }
            return new ChromInfo[0];
        }
        
// ReSharper disable SuggestBaseTypeForParameter
        private static IEnumerable<ChromInfo> GetChromInfos(Results<TransitionGroupChromInfo> results, RowIdentifier rowIdentifier, bool allOptimizationSteps)
// ReSharper restore SuggestBaseTypeForParameter
        {
            var chromInfoList = results[rowIdentifier.ReplicateIndex];
            if (chromInfoList == null)
                yield break;

            foreach (var chromInfo in chromInfoList)
            {
                if (chromInfo != null && chromInfo.FileIndex == rowIdentifier.FileIndex 
                    && (allOptimizationSteps || chromInfo.OptimizationStep == rowIdentifier.OptimizationStep))
                {
                    yield return chromInfo;
                }
            }
        }

// ReSharper disable SuggestBaseTypeForParameter
        private static IEnumerable<ChromInfo> GetChromInfos(Results<TransitionChromInfo> results, RowIdentifier rowIdentifier, bool allOptimizationSteps)
// ReSharper restore SuggestBaseTypeForParameter
        {
            var chromInfoList = results[rowIdentifier.ReplicateIndex];
            if (chromInfoList == null)
                yield break;

            foreach (var chromInfo in chromInfoList)
            {
                if (chromInfo != null && chromInfo.FileIndex == rowIdentifier.FileIndex 
                    && (allOptimizationSteps || chromInfo.OptimizationStep == rowIdentifier.OptimizationStep))
                {
                    yield return chromInfo;
                }
            }
        }

// ReSharper disable SuggestBaseTypeForParameter
        private static Results<TItem> ChangeChromInfo<TItem>(Results<TItem> results, RowIdentifier rowIdentifier, TItem chromInfoOld, TItem chromInfoNew) where TItem: ChromInfo
// ReSharper restore SuggestBaseTypeForParameter
        {
            var elements = new List<ChromInfoList<TItem>>();
            bool found = false;
            for (int iReplicate = 0; iReplicate < results.Count; iReplicate++)
            {
                var replicate = results[iReplicate];
                if (iReplicate != rowIdentifier.ReplicateIndex)
                {
                    elements.Add(replicate);
                }
                else
                {
                    var chromInfoList = new List<TItem>();
                    foreach (var chromInfo in replicate)
                    {
                        if (chromInfo != chromInfoOld)
                        {
                            chromInfoList.Add(chromInfo);
                        }
                        else
                        {
                            found = true;
                            chromInfoList.Add(chromInfoNew);
                        }
                    }
                    elements.Add(new ChromInfoList<TItem>(chromInfoList));
                }
            }
            if (!found)
            {
                throw new InvalidOperationException("Element not found");
            }
            return new Results<TItem>(elements);
        }

        /// <summary>
        /// Stores the Setting which controls the set and order of visible columns and their widths.
        /// </summary>
        private void SaveColumnState()
        {
            var key = GetGridColumnsKey();
            if (key == null)
            {
                return;
            }
            var gridColumns = new List<GridColumn>();
            foreach (var column in GetColumnsSortedByDisplayIndex())
            {
                gridColumns.Add(new GridColumn(column.Name, column.Visible, column.Width));
            }
            var gridColumnsList = Settings.Default.GridColumnsList;
            gridColumnsList.Add(new GridColumns(key, gridColumns));
            Settings.Default.GridColumnsList = gridColumnsList;
        }

// ReSharper disable ReturnTypeCanBeEnumerable.Local
        private IList<DataGridViewColumn> GetColumnsSortedByDisplayIndex()
        {
            var result = new DataGridViewColumn[Columns.Count];
            Columns.CopyTo(result, 0);
            Array.Sort(result, (a,b)=>a.DisplayIndex.CompareTo(b.DisplayIndex));
            return result;
        }

        private IList<DataGridViewColumn> TransitionColumns
        {
            get
            {
                return new DataGridViewColumn[]
                           {
                                TransitionNoteColumn,
                                RetentionTimeColumn,
                                FwhmColumn,
                                StartTimeColumn,
                                EndTimeColumn,
                                AreaColumn,
                                BackgroundColumn,
                                AreaRatioColumn,
                                HeightColumn,
                                TruncatedColumn,
                                UserSetColumn,
                                PeakRankColumn,
                           };
            }
        }

        IList<DataGridViewColumn> PrecursorColumns
        {
            get
            {
                return new DataGridViewColumn[]
                           {
                               PrecursorNoteColumn,
                               PrecursorPeakFoundRatioColumn,
                               BestRetentionTimeColumn,
                               MaxFwhmColumn,
                               MinStartTimeColumn,
                               MaxEndTimeColumn,
                               TotalAreaColumn,
                               TotalBackgroundColumn,
                               TotalAreaRatioColumn,
                               CountTruncatedColumn,
                               IdentifiedColumn,
                               UserSetTotalColumn,
                               LibraryDotProductColumn,
                               IsotopeDotProductColumn,
                               OptCollisionEnergyColumn,
                               OptDeclusteringPotentialColumn,
                           };
            }
        }

        IList<DataGridViewColumn> PeptideColumns
        {
            get
            {
                return new DataGridViewColumn[]
                           {
                               PeptidePeakFoundRatioColumn,
                               PeptideRetentionTimeColumn,
                               RatioToStandardColumn,
                           };
            }
        }

        IList<DataGridViewColumn> ReplicateColumns
        {
            get
            {
                return new DataGridViewColumn[]
                           {
                               ReplicateNameColumn,
                               FileNameColumn,
                               SampleNameColumn,
                               ModifiedTimeColumn,
                               AcquiredTimeColumn,
                               OptStepColumn,
                           };
            }
        }
        // ReSharper restore ReturnTypeCanBeEnumerable.Local

        DataGridViewRow EnsureRow(RowIdentifier rowIdentifier)
        {
            DataGridViewRow row;
            if (_chromInfoRows.TryGetValue(rowIdentifier, out row))
            {
                return row;
            }
            if (_unassignedRows.Count == 0)
                row = Rows[Rows.Add(new DataGridViewRow {Tag = rowIdentifier})];
            else
            {
                row = _unassignedRows.Pop();
                row.Tag = rowIdentifier;
                // To be extra safe, make sure the row is actually part of the grid.
                if (!ReferenceEquals(this, row.DataGridView))
                    Rows.Add(row);
            }
            row.Cells[OptStepColumn.Index].Value = rowIdentifier.OptimizationStep;
            _chromInfoRows.Add(rowIdentifier, row);
            return row;
        }

        DataGridViewRow EnsureRow(int iReplicate, int fileIndex, int step, ICollection<RowIdentifier> rowIds)
        {
            var rowId = new RowIdentifier(iReplicate, fileIndex, step);
            rowIds.Add(rowId);
            return EnsureRow(rowId);
        }

        DataGridViewRow EnsureRow(int iReplicate, MeasuredResults results, ICollection<RowIdentifier> rowIds)
        {
            int fileIndex = results.Chromatograms[iReplicate].MSDataFileInfos[0].FileIndex;
            return EnsureRow(iReplicate, fileIndex, 0, rowIds);
        }

        /// <summary>
        /// Clears row assignments leaving the actual rows in the grid for reassignment
        /// </summary>
        private void ClearAllRows()
        {
            for (int i = Rows.Count - 1; i >= 0; i--)
            {
                var row = Rows[i];

                row.Tag = null;
                _unassignedRows.Push(row);
            }
            _chromInfoRows.Clear();
        }

        private void HideColumns()
        {
            foreach (DataGridViewColumn column in Columns)
            {
                if (!ReferenceEquals(column, ReplicateNameColumn))
                    ShowColumn(column, false);
            }
        }

        private void ShowColumn(DataGridViewColumn column, bool show)
        {
            // If hiding the selected column, set the selected column back to the
            // replicate column.
            if (!show && CurrentCell != null && CurrentCell.ColumnIndex != -1 &&
                    ReferenceEquals(column, Columns[CurrentCell.ColumnIndex]))
            {
                int currentRowIndex = CurrentCell.RowIndex;
                ClearSelection();
                if (currentRowIndex != -1)
                    CurrentCell = Rows[currentRowIndex].Cells[0];
            }
            column.Visible = show;
        }

        /// <summary>
        /// Figures out which nodes in the document tree this control will be displaying data from.
        /// Remembers the selected IdentifyPath, TransitionDocNode, TransitionGroupDocNode and PeptideDocNode.
        /// </summary>
        private void UpdateSelectedTreeNode()
        {
            Document = DocumentUiContainer.DocumentUI;

            // Always clear all rows (except in a commit), and allow them to have their row
            // IDs reset.  It turns out that this produces a much more stable user experience
            // than trying to match up existing row ID.  Because part of the row ID is the file
            // index, row correspondence was getting completely messed up when switching between
            // nodes collected in different results files.
            if (_inCommitEdit)
                return;

            AnnotationTarget = AnnotationDef.AnnotationTarget.none;
            SelectedPaths = new List<IdentityPath>();

            ClearAllRows();

            var srmTreeNode = StateProvider.SelectedNodes.Count > 0 ?
                StateProvider.SelectedNodes[0] as SrmTreeNode
                : null;

            bool noResults = false;

            // Update the list of SelectedPaths and the AnnotationTarget.
            foreach (TreeNodeMS srmTreNode in StateProvider.SelectedNodes)
            {
                var nodeTree = srmTreNode as SrmTreeNode;
                if(nodeTree == null || srmTreNode.StateImageIndex == -1)
                {
                    noResults = true;
                    break;
                }
                SelectedPaths.Add(nodeTree.Path);
                if (srmTreNode is TransitionTreeNode)
                    AnnotationTarget = AnnotationTarget | AnnotationDef.AnnotationTarget.transition_result;
                else if (srmTreNode is TransitionGroupTreeNode)
                    AnnotationTarget = AnnotationTarget | AnnotationDef.AnnotationTarget.precursor_result;
                else
                    AnnotationTarget = AnnotationTarget | nodeTree.Model.AnnotationTarget;
            }

            if(noResults)
            {
                AnnotationTarget = AnnotationDef.AnnotationTarget.none;
                SelectedPaths = new List<IdentityPath>();
            }
           
            // If we have more than one node selected, or if the selected node is not a tree node, 
            // all of these values should be null. We only want to walk the tree if we have a single-selected
            // tree node. 
            if (noResults || StateProvider.SelectedNodes.Count != 1 || srmTreeNode == null)
            {
                SelectedTransitionDocNode = null;
                SelectedTransitionGroupDocNode = null;
                SelectedPeptideDocNode = null;
                HideColumns();
                return;
            }

            SelectedTransitionDocNode = srmTreeNode.Model as TransitionDocNode;
            if (SelectedTransitionDocNode != null)
            {
                srmTreeNode = srmTreeNode.SrmParent;
            }
            SelectedTransitionGroupDocNode = srmTreeNode.Model as TransitionGroupDocNode;
            if (SelectedTransitionGroupDocNode != null)
            {
                srmTreeNode = srmTreeNode.SrmParent;
            }
            SelectedPeptideDocNode = srmTreeNode.Model as PeptideDocNode;
        }

        private void UpdateAnnotationColumns()
        {
            var newAnnotationColumns = new Dictionary<string, DataGridViewColumn>();
            var listAnnotationDefs = Document.Settings.DataSettings.AnnotationDefs;
                                         
            foreach (var annotationDef in listAnnotationDefs)
            {
                if (0 == (annotationDef.AnnotationTargets 
                    & (AnnotationDef.AnnotationTarget.precursor_result | AnnotationDef.AnnotationTarget.transition_result)))
                {
                    continue;
                }
                var name = AnnotationDef.GetColumnName(annotationDef.Name);
                DataGridViewColumn column;
                _annotationColumns.TryGetValue(name, out column);
                if (column != null)
                {
                    bool canReuseColumn = false;
                    switch (annotationDef.Type)
                    {
                        case AnnotationDef.AnnotationType.true_false:
                            canReuseColumn = column is DataGridViewCheckBoxColumn;
                            break;
                        case AnnotationDef.AnnotationType.text:
                            canReuseColumn = column is DataGridViewTextBoxColumn;
                            break;
                        case AnnotationDef.AnnotationType.value_list:
                            var comboBoxColumn = column as DataGridViewComboBoxColumn;
                            if (comboBoxColumn != null)
                            {
                                if (comboBoxColumn.Items.Count == annotationDef.Items.Count + 1 && "".Equals(comboBoxColumn.Items[0]))
                                {
                                    canReuseColumn = true;
                                    for (int i = 0; i < annotationDef.Items.Count; i++)
                                    {
                                        if (!annotationDef.Items[i].Equals(comboBoxColumn.Items[i + 1]))
                                        {
                                            canReuseColumn = false;
                                            break;
                                        }
                                    }
                                    
                                }
                            }
                            break;
                    }
                    if (!canReuseColumn)
                    {
                        Columns.Remove(column);
                        _annotationColumns.Remove(column.Name);
                        column = null;
                    }
                }
                if (column == null)
                {
                    switch (annotationDef.Type)
                    {
                        case AnnotationDef.AnnotationType.true_false:
                            column = new DataGridViewCheckBoxColumn();
                            break;
                        case AnnotationDef.AnnotationType.value_list:
                            var comboBoxColumn = new DataGridViewComboBoxColumn();
                            comboBoxColumn.Items.Add("");
                            foreach (var item in annotationDef.Items)
                            {
                                comboBoxColumn.Items.Add(item);
                            }
                            column = comboBoxColumn;
                            break;
                        default:
                            column = new DataGridViewTextBoxColumn();
                            break;
                    }
                    column.Name = name;
                    column.HeaderText = annotationDef.Name;
                    Columns.Add(column);
                }
                column.Tag = annotationDef;
                newAnnotationColumns[name] = column;
            }
            SynchronizeColumns(newAnnotationColumns, ref _annotationColumns);
        }

        private void UpdateRatioColumns()
        {
            var newRatioColumns = new Dictionary<string, DataGridViewColumn>();
            var mods = DocumentUiContainer.DocumentUI.Settings.PeptideSettings.Modifications;
            var standardTypes = mods.InternalStandardTypes;
            var labelTypes = mods.GetModificationTypes().ToArray();
            if (labelTypes.Length > 2)
            {
                // Add special ratio columns in order after the default ratio columns
                // for their element type.
                int indexPeptideRatio = 0;
                int indexInsert = Columns.IndexOf(RatioToStandardColumn) + 1;
                foreach (var standardType in standardTypes)
                {
                    foreach (var labelType in labelTypes)
                    {
                        if (ReferenceEquals(standardType, labelType))
                            continue;

                        EnsureRatioColumn(RatioPropertyAccessor.GetPeptideColumnName(labelType, standardType),
                                          RatioPropertyAccessor.GetPeptideResultsHeader(labelType, standardType),
                                          new RatioColumnTag(RatioPropertyAccessor.RatioTarget.peptide_result, indexPeptideRatio++),
                                          indexInsert++,
                                          newRatioColumns);
                    }
                }                
                if (standardTypes.Count > 1)
                {
                    indexInsert = Columns.IndexOf(TotalAreaRatioColumn) + 1;
                    for (int i = 0; i < standardTypes.Count; i++)
                    {
                        EnsureRatioColumn(RatioPropertyAccessor.GetPrecursorColumnName(standardTypes[i]),
                                          RatioPropertyAccessor.GetPrecursorResultsHeader(standardTypes[i]),
                                          new RatioColumnTag(RatioPropertyAccessor.RatioTarget.precursor_result, i),
                                          indexInsert++,
                                          newRatioColumns);
                    }
                    indexInsert = Columns.IndexOf(AreaRatioColumn) + 1;
                    for (int i = 0; i < standardTypes.Count; i++)
                    {
                        EnsureRatioColumn(RatioPropertyAccessor.GetTransitionColumnName(standardTypes[i]),
                                          RatioPropertyAccessor.GetTransitionResultsHeader(standardTypes[i]),
                                          new RatioColumnTag(RatioPropertyAccessor.RatioTarget.transition_result, i),
                                          indexInsert++,
                                          newRatioColumns);                        
                    }
                }
            }
            SynchronizeColumns(newRatioColumns, ref _ratioColumns);
        }

        private void SynchronizeColumns(Dictionary<string, DataGridViewColumn> newColumns,
                                        ref Dictionary<string, DataGridViewColumn> oldColumns)
        {
            foreach (var entry in oldColumns)
            {
                if (!newColumns.ContainsKey(entry.Key))
                {
                    Columns.Remove(entry.Key);
                }
            }
            oldColumns = newColumns;
        }

        private sealed class RatioColumnTag
        {
            public RatioColumnTag(RatioPropertyAccessor.RatioTarget target, int indexRatio)
            {
                Target = target;
                IndexRatio = indexRatio;
            }

            public RatioPropertyAccessor.RatioTarget Target { get; private set; }
            public int IndexRatio { get; private set; }
        }

        private void EnsureRatioColumn(string name, string header, RatioColumnTag tag,
            int indexInsert, IDictionary<string, DataGridViewColumn> newRatioColumns)
        {
            DataGridViewColumn column;
            if (!_ratioColumns.TryGetValue(name, out column))
            {
                column = new DataGridViewTextBoxColumn
                {
                    Name = name,
                    HeaderText = header,
                    DefaultCellStyle = { Format = Formats.STANDARD_RATIO }
                };
                Columns.Insert(indexInsert, column);
            }
            column.Tag = tag;
            newRatioColumns[name] = column;
        }

        /// <summary>
        /// Updates all of the UI.  Called whenever the document is modified, or the selected tree node changes.
        /// </summary>
        public void UpdateGrid()
        {
            _inReplicateChange = true;
            try
            {
                UpdateGridNotReplicate();
            }
            finally
            {
                _inReplicateChange = false;
            }
        }

        private void UpdateGridNotReplicate()
        {
            EndEdit();
            SaveColumnState();
            UpdateSelectedTreeNode();
            if (_inCommitEdit)
            {
                return;
            }
            var settings = Document.Settings;
            var measuredResults = settings.MeasuredResults;
            if (measuredResults == null)
            {
                return;
            }
            UpdateRatioColumns();
            UpdateAnnotationColumns();
            // Remember the set of rowIds that have data in them.  After updating the data, rows
            // that are not in this set will be deleted from the grid
            var rowIds = new HashSet<RowIdentifier>();

            if (SelectedTransitionDocNode != null && SelectedTransitionDocNode.HasResults)
            {
                for (int iReplicate = 0; iReplicate < SelectedTransitionDocNode.Results.Count; iReplicate++) 
                {
                    var results = SelectedTransitionDocNode.Results[iReplicate];
                    if (results == null)
                    {
                        var row = EnsureRow(iReplicate, measuredResults, rowIds);
                        FillTransitionRow(row, null);
                        continue;
                    }

                    foreach (var chromInfo in results)
                    {
                        var row = EnsureRow(iReplicate, chromInfo.FileIndex, chromInfo.OptimizationStep, rowIds);
                        FillTransitionRow(row, chromInfo);
                    }
                }
            }

            if (SelectedTransitionGroupDocNode != null && SelectedTransitionGroupDocNode.HasResults)
            {
                for (int iReplicate = 0; iReplicate < SelectedTransitionGroupDocNode.Results.Count; iReplicate++)
                {
                    var optFunc = measuredResults.Chromatograms[iReplicate].OptimizationFunction;

                    var results = SelectedTransitionGroupDocNode.Results[iReplicate];
                    if (results == null)
                    {
                        var row = EnsureRow(iReplicate, measuredResults, rowIds);
                        FillTransitionGroupRow(row, settings, optFunc, null);
                        continue;
                    }

                    foreach (var chromInfo in results)
                    {
                        if (chromInfo == null)
                        {
                            continue;
                        }
                        var row = EnsureRow(iReplicate, chromInfo.FileIndex, chromInfo.OptimizationStep, rowIds);
                        FillTransitionGroupRow(row, settings, optFunc, chromInfo);
                    }
                }
            }

            if (rowIds.Count == 0)
            {
                // Make sure some rows exist
                if (SelectedPeptideDocNode == null || !SelectedPeptideDocNode.HasResults)
                {
                    // Just a blank set of replicate rows
                    for (int iReplicate = 0; iReplicate < settings.MeasuredResults.Chromatograms.Count; iReplicate++)
                    {
                        var row = EnsureRow(iReplicate, measuredResults, rowIds);
                        FillMultiSelectRow(row, iReplicate);
                    }
                }
                else
                {
                    // Need file indexes from a peptide
                    for (int iReplicate = 0; iReplicate < SelectedPeptideDocNode.Results.Count; iReplicate++)
                    {
                        var results = SelectedPeptideDocNode.Results[iReplicate];
                        if (results == null)
                        {
                            EnsureRow(iReplicate, measuredResults, rowIds);
                            continue;
                        }

                        foreach (var chromInfo in results)
                        {
                            EnsureRow(iReplicate, chromInfo.FileIndex, 0, rowIds);
                        }
                    }
                }
            }
            
            // Group all of the optimization step rows by file that they're in.
            var replicateRowDict = new Dictionary<RowIdentifier, ICollection<RowIdentifier>>();
            foreach (var rowId in _chromInfoRows.Keys)
            {
                var rowIdZero = new RowIdentifier(rowId.ReplicateIndex, rowId.FileIndex, 0);
                ICollection<RowIdentifier> optStepRowIds;
                if (!replicateRowDict.TryGetValue(rowIdZero, out optStepRowIds))
                {
                    optStepRowIds = new List<RowIdentifier>();
                    replicateRowDict.Add(rowIdZero, optStepRowIds);
                }
                optStepRowIds.Add(rowId);
            }

            // Update columns that do not depend on optimization step
            for (int iReplicate = 0; iReplicate < measuredResults.Chromatograms.Count; iReplicate++)
            {
                var results = settings.MeasuredResults.Chromatograms[iReplicate];
                for (int iFile = 0; iFile < results.FileCount; iFile++)
                {
                    var fileInfo = results.MSDataFileInfos[iFile];
                    var rowIdZero = new RowIdentifier(iReplicate, fileInfo.FileIndex, 0);
                    ICollection<RowIdentifier> optStepRowIds;
                    if (!replicateRowDict.TryGetValue(rowIdZero, out optStepRowIds))
                        continue;
                    var filePath = fileInfo.FilePath;
                    var fileName = SampleHelp.GetFileName(filePath);
                    var sampleName = SampleHelp.GetFileSampleName(filePath);
                    foreach (var rowId in optStepRowIds)
                    {
                        var row = _chromInfoRows[rowId];
                        row.Cells[ReplicateNameColumn.Index].Value = results.Name;
                        row.Cells[FileNameColumn.Index].Value = fileName;
                        row.Cells[SampleNameColumn.Index].Value = sampleName;
                        row.Cells[ModifiedTimeColumn.Index].Value = fileInfo.FileWriteTime;
                        row.Cells[AcquiredTimeColumn.Index].Value = fileInfo.RunStartTime;
                    }
                }
            }

            if (SelectedPeptideDocNode != null && SelectedPeptideDocNode.HasResults)
            {
                for (int iReplicate = 0; iReplicate < SelectedPeptideDocNode.Results.Count; iReplicate++)
                {
                    var results = SelectedPeptideDocNode.Results[iReplicate];
                    if (results == null)
                    {
                        var row = EnsureRow(iReplicate, measuredResults, rowIds);
                        FillPeptideRow(row, null);                        
                        continue;
                    }

                    foreach (var chromInfo in results)
                    {
                        var rowIdZero = new RowIdentifier(iReplicate, chromInfo.FileIndex, 0);
                        ICollection<RowIdentifier> optStepRowIds;
                        if (!replicateRowDict.TryGetValue(rowIdZero, out optStepRowIds))
                            continue;
                        foreach (var rowId in optStepRowIds)
                        {
                            var row = _chromInfoRows[rowId];
                            FillPeptideRow(row, chromInfo);
                        }
                    }
                }
            }

            // Delete unused rows from the grid
            var rowsToDelete = new HashSet<RowIdentifier>(_chromInfoRows.Keys);
            rowsToDelete.ExceptWith(rowIds);
            foreach (var rowId in rowsToDelete)
            {
                Rows.Remove(_chromInfoRows[rowId]);
                _chromInfoRows.Remove(rowId);
            }
            while (_unassignedRows.Count > 0)
            {
                var row = _unassignedRows.Pop();
                // To be extra safe, make sure the row is actually part of the grid before removing it.
                if (ReferenceEquals(this, row.DataGridView))
                    Rows.Remove(row);
            }

            // Update column visibility from the settings.  This has to be done
            // all in one step to avoid messing up scrolling of the grid.
            GridColumns gridColumns = null;
            var gridColumnsKey = GetGridColumnsKey();
            if (gridColumnsKey != null)
            {
                Settings.Default.GridColumnsList.TryGetValue(gridColumnsKey, out gridColumns);
            }
            var visibleColumnNameSet = new HashSet<string>(from col in GetDefaultColumns() select col.Name);
            if (gridColumns != null)
            {
                var availableColumnSet = new HashSet<DataGridViewColumn>(GetAvailableColumns());
                int displayIndex = 0;
                foreach (var gridColumn in gridColumns.Columns)
                {
                    var column = Columns[gridColumn.Name];
                    if (column == null) {
                        continue;
                    }
                    column.Width = gridColumn.Width;
                    column.DisplayIndex = displayIndex++;
                    if (gridColumn.Visible && availableColumnSet.Contains(column))
                        visibleColumnNameSet.Add(column.Name);
                    else 
                        visibleColumnNameSet.Remove(column.Name);
                }
            }
            // Visibility is decided as whether or not the saved columns make the
            // column visible, or any default column that was not part of the
            // saved set.
            foreach (DataGridViewColumn column in Columns)
            {
                ShowColumn(column, visibleColumnNameSet.Contains(column.Name));
            }
        }

        private void FillPeptideRow(DataGridViewRow row, PeptideChromInfo chromInfo)
        {
            if (chromInfo == null)
            {
                row.Cells[PeptidePeakFoundRatioColumn.Index].Value = 
                    row.Cells[PeptideRetentionTimeColumn.Index].Value = 
                    row.Cells[RatioToStandardColumn.Index].Value = null;
            }
            else
            {
                row.Cells[PeptidePeakFoundRatioColumn.Index].Value = chromInfo.PeakCountRatio;
                row.Cells[PeptideRetentionTimeColumn.Index].Value = chromInfo.RetentionTime;
                row.Cells[RatioToStandardColumn.Index].Value = chromInfo.LabelRatios[0].Ratio;
            }
            foreach (var column in _ratioColumns.Values)
            {
                var ratioTag = (RatioColumnTag)column.Tag;
                if (ratioTag.Target == RatioPropertyAccessor.RatioTarget.peptide_result)
                {
                    row.Cells[column.Index].Value =
                        chromInfo == null ? null :
                        chromInfo.LabelRatios[ratioTag.IndexRatio].Ratio;
                }
            }
        }

        private void FillTransitionGroupRow(DataGridViewRow row, SrmSettings settings,
            OptimizableRegression optFunc, TransitionGroupChromInfo chromInfo)
        {
            if (chromInfo == null)
            {
                row.Cells[PrecursorPeakFoundRatioColumn.Index].Value =
                    row.Cells[BestRetentionTimeColumn.Index].Value =
                    row.Cells[MaxFwhmColumn.Index].Value =
                    row.Cells[MinStartTimeColumn.Index].Value =
                    row.Cells[MaxEndTimeColumn.Index].Value =
                    row.Cells[TotalAreaColumn.Index].Value =
                    row.Cells[TotalBackgroundColumn.Index].Value =
                    row.Cells[TotalAreaRatioColumn.Index].Value =
                    row.Cells[LibraryDotProductColumn.Index].Value =
                    row.Cells[IsotopeDotProductColumn.Index].Value =
                    row.Cells[CountTruncatedColumn.Index].Value =
                    row.Cells[IdentifiedColumn.Index].Value =
                    row.Cells[UserSetTotalColumn.Index].Value =
                    row.Cells[PrecursorNoteColumn.Index].Value =
                    row.Cells[OptCollisionEnergyColumn.Index].Value =
                    row.Cells[OptDeclusteringPotentialColumn.Index].Value = null;
            }
            else
            {
                row.Cells[PrecursorPeakFoundRatioColumn.Index].Value = chromInfo.PeakCountRatio;
                row.Cells[BestRetentionTimeColumn.Index].Value = chromInfo.RetentionTime;
                row.Cells[MaxFwhmColumn.Index].Value = chromInfo.Fwhm;
                row.Cells[MinStartTimeColumn.Index].Value = chromInfo.StartRetentionTime;
                row.Cells[MaxEndTimeColumn.Index].Value = chromInfo.EndRetentionTime;
                row.Cells[TotalAreaColumn.Index].Value = chromInfo.Area;
                row.Cells[TotalBackgroundColumn.Index].Value = chromInfo.BackgroundArea;
                row.Cells[TotalAreaRatioColumn.Index].Value = chromInfo.Ratios[0];
                row.Cells[CountTruncatedColumn.Index].Value = chromInfo.Truncated;
                row.Cells[IdentifiedColumn.Index].Value = chromInfo.Identified;
                row.Cells[UserSetTotalColumn.Index].Value = chromInfo.UserSet;
                row.Cells[LibraryDotProductColumn.Index].Value = chromInfo.LibraryDotProduct;
                row.Cells[IsotopeDotProductColumn.Index].Value = chromInfo.IsotopeDotProduct;
                row.Cells[PrecursorNoteColumn.Index].Value = chromInfo.Annotations.Note;
                row.Cells[OptCollisionEnergyColumn.Index].Value = null;
                row.Cells[OptDeclusteringPotentialColumn.Index].Value = null;

                if (optFunc != null)
                {
                    double regressionMz = settings.GetRegressionMz(SelectedPeptideDocNode,
                        SelectedTransitionGroupDocNode);
                    var ceRegression = optFunc as CollisionEnergyRegression;
                    if (ceRegression != null)
                    {
                        int charge = SelectedTransitionGroupDocNode.TransitionGroup.PrecursorCharge;
                        row.Cells[OptCollisionEnergyColumn.Index].Value =
                            ceRegression.GetCollisionEnergy(
                                charge, regressionMz, chromInfo.OptimizationStep);
                    }
                    var dpRegression = optFunc as DeclusteringPotentialRegression;
                    if (dpRegression != null)
                    {
                        row.Cells[OptDeclusteringPotentialColumn.Index].Value =
                            dpRegression.GetDeclustringPotential(
                                regressionMz, chromInfo.OptimizationStep);
                    }
                }
            }
            foreach (var column in _ratioColumns.Values)
            {
                var ratioTag = (RatioColumnTag)column.Tag;
                if (ratioTag.Target == RatioPropertyAccessor.RatioTarget.precursor_result)
                {
                    row.Cells[column.Index].Value =
                        chromInfo == null ? null :
                        chromInfo.Ratios[ratioTag.IndexRatio];
                }
            }
            foreach (var column in _annotationColumns.Values)
            {
                var annotationDef = (AnnotationDef)column.Tag;
                var mask = AnnotationDef.AnnotationTarget.precursor_result;
                if (SelectedTransitionDocNode != null)
                {
                    mask |= AnnotationDef.AnnotationTarget.transition_result;
                }
                if (AnnotationDef.AnnotationTarget.precursor_result == (annotationDef.AnnotationTargets & mask))
                {
                    row.Cells[column.Index].Value = 
                        chromInfo == null ? null : 
                        chromInfo.Annotations.GetAnnotation(annotationDef);
                }
            }
        }

        private void FillTransitionRow(DataGridViewRow row, TransitionChromInfo chromInfo)
        {
            if (chromInfo == null)
            {
                row.Cells[RetentionTimeColumn.Index].Value =
                    row.Cells[FwhmColumn.Index].Value =
                    row.Cells[StartTimeColumn.Index].Value =
                    row.Cells[EndTimeColumn.Index].Value =
                    row.Cells[AreaColumn.Index].Value =
                    row.Cells[BackgroundColumn.Index].Value =
                    row.Cells[AreaRatioColumn.Index].Value =
                    row.Cells[HeightColumn.Index].Value =
                    row.Cells[TruncatedColumn.Index].Value =
                    row.Cells[PeakRankColumn.Index].Value =
                    row.Cells[UserSetColumn.Index].Value =
                    row.Cells[TransitionNoteColumn.Index].Value = null;
            }
            else
            {
                row.Cells[RetentionTimeColumn.Index].Value = chromInfo.RetentionTime;
                row.Cells[FwhmColumn.Index].Value = chromInfo.Fwhm;
                row.Cells[StartTimeColumn.Index].Value = chromInfo.StartRetentionTime;
                row.Cells[EndTimeColumn.Index].Value = chromInfo.EndRetentionTime;
                row.Cells[AreaColumn.Index].Value = chromInfo.Area;
                row.Cells[BackgroundColumn.Index].Value = chromInfo.BackgroundArea;
                row.Cells[AreaRatioColumn.Index].Value = chromInfo.Ratios[0];
                row.Cells[HeightColumn.Index].Value = chromInfo.Height;
                row.Cells[TruncatedColumn.Index].Value = chromInfo.IsTruncated;
                row.Cells[PeakRankColumn.Index].Value = chromInfo.Rank;
                row.Cells[UserSetColumn.Index].Value = chromInfo.UserSet;
                row.Cells[TransitionNoteColumn.Index].Value = chromInfo.Annotations.Note;
            }
            foreach (var column in _ratioColumns.Values)
            {
                var ratioTag = (RatioColumnTag)column.Tag;
                if (ratioTag.Target == RatioPropertyAccessor.RatioTarget.transition_result)
                {
                    row.Cells[column.Index].Value =
                        chromInfo == null ? null :
                        chromInfo.Ratios[ratioTag.IndexRatio];
                }
            }
            foreach (var column in _annotationColumns.Values)
            {
                var annotationDef = (AnnotationDef) column.Tag;
                if (0 != (annotationDef.AnnotationTargets & AnnotationDef.AnnotationTarget.transition_result))
                {
                    row.Cells[column.Index].Value = 
                        chromInfo == null ? null :
                        chromInfo.Annotations.GetAnnotation(annotationDef);
                }
            }
        }

        /// <summary>
        /// Sets the values for the given row using the given replicate index. Based on the nodes we selected, 
        /// annotation values must be merged. Only annotation values that match for all nodes will be displayed
        /// in the ResultsGrid.
        /// </summary>
        public void FillMultiSelectRow(DataGridViewRow row, int replicateIndex)
        {
            bool first  = true;
            foreach (IdentityPath selPath in SelectedPaths)
            {
                var nodeDoc = Document.FindNode(selPath);
                const AnnotationDef.AnnotationTarget annotationTarget =
                    AnnotationDef.AnnotationTarget.precursor_result |
                    AnnotationDef.AnnotationTarget.transition_result;

                IEnumerable<ChromInfo> chromInfos = GetChromInfos(nodeDoc, annotationTarget,
                    new RowIdentifier(replicateIndex, 0, null), true);

                foreach (var chromInfo in chromInfos)
                {
                    // All ChromInfos returned from GetChromInfos must be either TransitionChromInfos
                    // or TransitionGroupChromInfos.
                    var transitionChromInfo = chromInfo as TransitionChromInfo;
                    var transitionGroupChromInfo = chromInfo as TransitionGroupChromInfo;
                    bool transition = transitionChromInfo != null;
                    var target = transition
                                     ? AnnotationDef.AnnotationTarget.transition_result
                                     : AnnotationDef.AnnotationTarget.precursor_result;
                    var noteIndex = transition
                                        ? TransitionNoteColumn.Index
                                        : PrecursorNoteColumn.Index;
                     // Update all of the annotation columns.
// ReSharper disable PossibleNullReferenceException
                    Annotations annotations = transition ? transitionChromInfo.Annotations
                        : transitionGroupChromInfo.Annotations;
// ReSharper restore PossibleNullReferenceException

                    foreach (var column in _annotationColumns.Values)
                    {
                        var annotationDef = (AnnotationDef) column.Tag;
                        if (0 != (annotationDef.AnnotationTargets & target))
                        {
                            var annotation = annotations.GetAnnotation(annotationDef);
                            if (first)
                                row.Cells[column.Index].Value = annotation;
                            if (!Equals(row.Cells[column.Index].Value, annotation))
                                row.Cells[column.Index].Value = null;
                        }
                    }
                    // Update the note_ column.
                    if (first)
                        row.Cells[noteIndex].Value = annotations.Note;
                    if (!Equals(row.Cells[TransitionNoteColumn.Index].Value, annotations.Note))
                        row.Cells[TransitionNoteColumn.Index].Value = null;
                    first = false;
                }
            }
        }

        public void UpdateSelectedReplicate()
        {
            if (_inReplicateChange)
                return;

            EndEdit();
            if (_inCommitEdit || CurrentRow == null)
                return;

            var selectedRowId = (RowIdentifier) CurrentRow.Tag;
            if (selectedRowId == null)
                return;

            int selectedResultRow = selectedRowId.ReplicateIndex;
            int selectedResult = StateProvider.SelectedResultsIndex;

            int selectedColumn = 0;
            if (CurrentCell != null && CurrentCell.ColumnIndex != -1 && Columns[CurrentCell.ColumnIndex].Visible)
                selectedColumn = CurrentCell.ColumnIndex;
            if (selectedResult != selectedResultRow)
            {
                // For some reason GridView seems to store these in reverse order
                for (int iRow = 0; iRow < Rows.Count; iRow++)
                {
                    var rowId = (RowIdentifier) Rows[iRow].Tag;
                    if (rowId == null || rowId.ReplicateIndex != selectedResult)
                        continue;

                    ClearSelection();
                    SetSelectedCellCore(selectedColumn, iRow, true);
                    CurrentCell = Rows[iRow].Cells[selectedColumn];
                    break;
                }
            }
        }

        /// <summary>
        /// Returns the name to use when saving the column state.  This name is a key for the class
        /// <see cref="GridColumnsList" />
        /// </summary>
        String GetGridColumnsKey()
        {
            if(SelectedPaths == null || SelectedPaths.Count != 1)
            {
                return null;
            }
            if (SelectedTransitionDocNode != null)
            {
                return typeof (TransitionDocNode).Name;
            }
            if (SelectedTransitionGroupDocNode != null)
            {
                return typeof (TransitionGroupDocNode).Name;
            }
            if (SelectedPeptideDocNode != null)
            {
                return typeof (PeptideDocNode).Name;
            }
            return typeof(PeptideGroupDocNode).Name;
        }

        /// <summary>
        /// Returns the set of columns that should be displayed by default, based on
        /// the current tree node selected.
        /// The default order of the columns is controlled by GetAvailableColumns()
        /// </summary>
        public ICollection<DataGridViewColumn> GetDefaultColumns()
        {
            var result = new HashSet<DataGridViewColumn> {ReplicateNameColumn};
            if (SelectedTransitionDocNode != null)
            {
                result.UnionWith(from column in TransitionColumns
                                     where !ReferenceEquals(column, TruncatedColumn) &&
                                           !ReferenceEquals(column, UserSetColumn)
                                     select column);
            }
            // If only transition nodes are selected, the TransitionNoteColumn should be 
            // available.
            else if(Equals(AnnotationTarget, AnnotationDef.AnnotationTarget.transition_result))
                result.Add(TransitionColumns[0]);
            else if (SelectedTransitionGroupDocNode != null)
            {
                result.UnionWith(from column in PrecursorColumns
                                     where !ReferenceEquals(column, IsotopeDotProductColumn) &&
                                           !ReferenceEquals(column, CountTruncatedColumn) &&
                                           !ReferenceEquals(column, IdentifiedColumn) &&
                                           !ReferenceEquals(column, UserSetTotalColumn) &&
                                           !ReferenceEquals(column, OptCollisionEnergyColumn) &&
                                           !ReferenceEquals(column, OptDeclusteringPotentialColumn)
                                     select column);
            }
            // If only precursors are selected, the PrecursorNoteColumn should be available.
            else if (Equals(AnnotationTarget, AnnotationDef.AnnotationTarget.precursor_result))
                result.Add(PrecursorColumns[0]);
            else if (SelectedPeptideDocNode != null)
            {
                result.UnionWith(PeptideColumns);
            }
            // Only allow each annotation column if the annotation target applies to all nodes that are 
            // currently selected.
            foreach (var column in _annotationColumns.Values)
            {
                var annotationDef = (AnnotationDef) column.Tag;
                if (AnnotationTarget > 0 && AnnotationTarget == (annotationDef.AnnotationTargets & AnnotationTarget))
                {
                    result.Add(column);
                }
            }
            return result;
        }

        /// <summary>
        /// Returns the list of columns that the user is allowed to display based
        /// on the currently selected tree node.
        /// </summary>
        public IList<DataGridViewColumn> GetAvailableColumns()
        {
            var result = new List<DataGridViewColumn>();
            result.AddRange(ReplicateColumns);
            if (Equals(AnnotationTarget, AnnotationDef.AnnotationTarget.precursor_result))
                result.Add(PrecursorColumns[0]);
            if (Equals(AnnotationTarget, AnnotationDef.AnnotationTarget.transition_result))
                result.Add(TransitionColumns[0]);
            AnnotationDef.AnnotationTarget targets = 0;
            if (SelectedPeptideDocNode != null)
            {
                result.AddRange(PeptideColumns);
                InsertRatioColumns(result, RatioToStandardColumn,
                    RatioPropertyAccessor.RatioTarget.peptide_result);
            }
            if (SelectedTransitionGroupDocNode != null)
            {
                result.AddRange(PrecursorColumns);
                InsertRatioColumns(result, TotalAreaRatioColumn,
                    RatioPropertyAccessor.RatioTarget.precursor_result);
                targets |= AnnotationDef.AnnotationTarget.precursor_result;
            }
            if (SelectedTransitionDocNode != null)
            {
                result.AddRange(TransitionColumns);
                InsertRatioColumns(result, AreaRatioColumn,
                    RatioPropertyAccessor.RatioTarget.transition_result);
                targets |= AnnotationDef.AnnotationTarget.transition_result;
            }
            targets |= AnnotationTarget;
            foreach (var column in _annotationColumns.Values)
            {
                var annotationDef = (AnnotationDef) column.Tag;
                if (0 != (annotationDef.AnnotationTargets & targets))
                {
                    result.Add(column);
                }
            }
            return result;
        }

        private void InsertRatioColumns(IList<DataGridViewColumn> columns,
                                        DataGridViewColumn columnAfter,
                                        RatioPropertyAccessor.RatioTarget target)
        {
            int insertIndex = columns.IndexOf(columnAfter) + 1;
            foreach (var column in _ratioColumns.Values)
            {
                var ratioTag = (RatioColumnTag)column.Tag;
                if (ratioTag.Target == target)
                {
                    columns.Insert(insertIndex++, column);
                }
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (DocumentUiContainer == null)
            {
                return;
            }
            DocumentUiContainer.ListenUI(DocumentChanged);
            UpdateGrid();
            UpdateSelectedReplicate();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            if (DocumentUiContainer == null)
            {
                return;
            }
            DocumentUiContainer.UnlistenUI(DocumentChanged);
        }

        private void DocumentChanged(object sender, DocumentChangedEventArgs args)
        {
            UpdateGrid();
        }

        public void HighlightFindResult(FindResult findResult)
        {
            var bookmarkEnumerator = BookmarkEnumerator.TryGet(Document, findResult.Bookmark);
            if (bookmarkEnumerator == null)
            {
                return;
            }
            var chromInfo = bookmarkEnumerator.CurrentChromInfo;
            if (chromInfo == null)
            {
                return;
            }
            var replicateIndex = FindReplicateIndex(bookmarkEnumerator.CurrentDocNode, chromInfo);
            if (replicateIndex < 0)
            {
                return;
            }
            var rowIdentifier = new RowIdentifier(replicateIndex, chromInfo.FileIndex, GetOptStep(chromInfo));
            DataGridViewRow row;
            if (!_chromInfoRows.TryGetValue(rowIdentifier, out row))
            {
                return;
            }
            DataGridViewColumn column = null;
            if (findResult.FindMatch.Note)
            {
                var docNode = bookmarkEnumerator.CurrentDocNode;
                if (docNode is TransitionGroupDocNode)
                {
                    column = PrecursorNoteColumn;
                }
                else if (docNode is TransitionDocNode)
                {
                    column = TransitionNoteColumn;
                }
            }
            else if (findResult.FindMatch.AnnotationName != null)
            {
                _annotationColumns.TryGetValue(AnnotationDef.GetColumnName(findResult.FindMatch.AnnotationName), out column);
            }
            if (column == null)
            {
                return;
            }
            ShowColumn(column, true);
            CurrentCell = row.Cells[column.Index];
        }

        public ChromFileInfoId GetCurrentChromFileInfoId()
        {
            var currentRow = CurrentRow;
            if (currentRow == null)
            {
                return null;
            }
            var rowIdentifier = currentRow.Tag as RowIdentifier;
            if (rowIdentifier == null)
            {
                return null;
            }
            var currentTreeNode = StateProvider.SelectedNode as SrmTreeNode;
            if (currentTreeNode == null)
            {
                return null;
            }
            var chromInfo =
                GetChromInfos(currentTreeNode.Model, (AnnotationDef.AnnotationTarget) ~0, rowIdentifier, false)
                .FirstOrDefault();
            if (chromInfo == null)
            {
                return null;
            }
            return chromInfo.FileId;
        }

        private static int FindReplicateIndex(DocNode docNode, ChromInfo chromInfo)
        {
            var peptideDocNode = docNode as PeptideDocNode;
            if (peptideDocNode != null)
            {
                if (peptideDocNode.Results != null)
                {
                    for (int replicateIndex = 0; replicateIndex < peptideDocNode.Results.Count; replicateIndex++)
                    {
                        var chromInfoList = peptideDocNode.Results[replicateIndex];
                        if (chromInfoList.Count > chromInfo.FileIndex
                            && Equals(chromInfo, chromInfoList[chromInfo.FileIndex]))
                        {
                            return replicateIndex;
                        }
                    }
                }
                return -1;
            }
            var transitionGroupDocNode = docNode as TransitionGroupDocNode;
            if (transitionGroupDocNode != null)
            {
                if (transitionGroupDocNode.Results != null)
                {
                    for (int replicateIndex = 0;
                         replicateIndex < transitionGroupDocNode.Results.Count;
                         replicateIndex++)
                    {
                        if (transitionGroupDocNode.Results[replicateIndex].FirstOrDefault(
                            transitionGroupChromInfo=>chromInfo.FileId == transitionGroupChromInfo.FileId) != null)
                        {
                            return replicateIndex;
                        }
                    }
                }
                return -1;
            }
            var transitionDocNode = docNode as TransitionDocNode;
            if (transitionDocNode != null)
            {
                if (transitionDocNode.Results != null)
                {
                    for (int replicateIndex = 0;
                        replicateIndex < transitionDocNode.Results.Count;
                        replicateIndex++)
                    {
                        if (transitionDocNode.Results[replicateIndex].FirstOrDefault(
                            transitionChromInfo => chromInfo.FileId == transitionChromInfo.FileId) != null)
                        {
                            return replicateIndex;
                        }
                    }

                }
            }
            return -1;
        }

        private static int? GetOptStep(ChromInfo chromInfo)
        {
            var transitionGroupChromInfo = chromInfo as TransitionGroupChromInfo;
            if (transitionGroupChromInfo != null)
            {
                return transitionGroupChromInfo.OptimizationStep;
            }
            var transitionChromInfo = chromInfo as TransitionChromInfo;
            if (transitionChromInfo != null)
            {
                return transitionChromInfo.OptimizationStep;
            }
            return null;
        }


        public IDocumentUIContainer DocumentUiContainer { get; private set; }
        public IStateProvider StateProvider { get; private set; }

        // Replicate Columns
        public DataGridViewTextBoxColumn ReplicateNameColumn { get; private set; }
        public DataGridViewTextBoxColumn FileNameColumn { get; private set; }
        public DataGridViewTextBoxColumn SampleNameColumn { get; private set; }
        public DataGridViewTextBoxColumn ModifiedTimeColumn { get; private set; }
        public DataGridViewTextBoxColumn AcquiredTimeColumn { get; private set; }
        public DataGridViewTextBoxColumn OptStepColumn { get; private set; }
        // Peptide Columns
        public DataGridViewTextBoxColumn PeptidePeakFoundRatioColumn { get; private set; }
        public DataGridViewTextBoxColumn PeptideRetentionTimeColumn { get; private set; }
        public DataGridViewTextBoxColumn RatioToStandardColumn { get; private set; }
        // Precursor Columns
        public DataGridViewTextBoxColumn PrecursorPeakFoundRatioColumn { get; private set; }
        public DataGridViewTextBoxColumn BestRetentionTimeColumn { get; private set; }
        public DataGridViewTextBoxColumn MaxFwhmColumn { get; private set; }
        public DataGridViewTextBoxColumn MinStartTimeColumn { get; private set; }
        public DataGridViewTextBoxColumn MaxEndTimeColumn { get; private set; }
        public DataGridViewTextBoxColumn TotalAreaColumn { get; private set; }
        public DataGridViewTextBoxColumn TotalBackgroundColumn { get; private set; }
        public DataGridViewTextBoxColumn TotalAreaRatioColumn { get; private set; }
        public DataGridViewTextBoxColumn CountTruncatedColumn { get; private set; }
        public DataGridViewTextBoxColumn IdentifiedColumn { get; private set; }
        public DataGridViewTextBoxColumn UserSetTotalColumn { get; private set; }
        public DataGridViewTextBoxColumn LibraryDotProductColumn { get; private set; }
        public DataGridViewTextBoxColumn IsotopeDotProductColumn { get; private set; }
        public DataGridViewTextBoxColumn OptCollisionEnergyColumn { get; private set; }
        public DataGridViewTextBoxColumn OptDeclusteringPotentialColumn { get; private set; }
        public DataGridViewTextBoxColumn PrecursorNoteColumn { get; private set; }
        // Transition Columns
        public DataGridViewTextBoxColumn RetentionTimeColumn { get; private set; }
        public DataGridViewTextBoxColumn FwhmColumn { get; private set; }
        public DataGridViewTextBoxColumn StartTimeColumn { get; private set; }
        public DataGridViewTextBoxColumn EndTimeColumn { get; private set; }
        public DataGridViewTextBoxColumn AreaColumn { get; private set; }
        public DataGridViewTextBoxColumn BackgroundColumn { get; private set; }
        public DataGridViewTextBoxColumn AreaRatioColumn { get; private set; }
        public DataGridViewTextBoxColumn HeightColumn { get; private set; }
        public DataGridViewTextBoxColumn TruncatedColumn { get; private set; }
        public DataGridViewTextBoxColumn PeakRankColumn { get; private set; }
        public DataGridViewTextBoxColumn UserSetColumn { get; private set; }
        public DataGridViewTextBoxColumn TransitionNoteColumn { get; private set; }

        private SrmDocument Document { get; set; }
        private List<IdentityPath> SelectedPaths { get; set; }
        private TransitionDocNode SelectedTransitionDocNode { get; set; }
        private TransitionGroupDocNode SelectedTransitionGroupDocNode { get; set; }
        private PeptideDocNode SelectedPeptideDocNode { get; set; }
        private AnnotationDef.AnnotationTarget AnnotationTarget { get; set; }
        
        /// <summary>
        /// Identifies which result a row in the grid refers to.
        /// </summary>
        class RowIdentifier
        {
            public RowIdentifier(int replicateIndex, int fileIndex, int? optStep)
            {
                ReplicateIndex = replicateIndex;
                FileIndex = fileIndex;
                OptimizationStep = optStep;
            }

            public int ReplicateIndex { get; private set; }
            public int FileIndex { get; private set; }
            public int? OptimizationStep { get; private set; }
            public override bool Equals(object obj)
            {
                if (obj == this)
                {
                    return true;
                }
                var that = obj as RowIdentifier;
                if (that == null)
                {
                    return false;
                }
                return ReplicateIndex == that.ReplicateIndex && FileIndex == that.FileIndex && OptimizationStep == that.OptimizationStep;
            }
            public override int GetHashCode()
            {
                int result = ReplicateIndex.GetHashCode();
                result = result*31 + FileIndex.GetHashCode();
                result = result*31 + OptimizationStep.GetHashCode();
                return result;
            }

            public override string ToString()
            {
                return string.Format("{0}, {1}, {2}", ReplicateIndex, FileIndex, OptimizationStep);
            }
        }
    }
}
