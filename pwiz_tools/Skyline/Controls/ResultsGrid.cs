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
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
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

        private Dictionary<string, DataGridViewColumn> _annotationColumns 
            = new Dictionary<string, DataGridViewColumn>();

        private bool _inCommitEdit;
        private bool _inReplicateChange;

        public interface IStateProvider
        {
            TreeNode SelectedNode { get; }

            int SelectedResultsIndex { get; set; }
        }

        private class DefaultStateProvider : IStateProvider
        {
            public TreeNode SelectedNode { get { return null; } }
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
            Columns.Add(LibraryDotProductColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "LibraryDotProduct",
                          HeaderText = "Library Dot Product",
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.STANDARD_RATIO}
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
            Columns.Add(PeakRankColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "PeakRank",
                          HeaderText = "Peak Rank",
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

        void ResultsGrid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            _inCommitEdit = true;
            try
            {
                if (DocumentUiContainer.InUndoRedo)
                    return;

                var row = Rows[e.RowIndex];
                var rowIdentifier = row.Tag as RowIdentifier;
                if (rowIdentifier == null)
                {
                    return;
                }
                if (e.ColumnIndex == PrecursorNoteColumn.Index)
                {
                    var precursorDocNode = SelectedTransitionGroupDocNode;
                    if (precursorDocNode == null)
                        return;

                    string noteNew = Convert.ToString(row.Cells[e.ColumnIndex].Value);
                    var chromInfoOld = GetChromInfo(SelectedTransitionGroupDocNode.Results, rowIdentifier);
                    if (Equals(chromInfoOld.Annotations.Note, noteNew))
                        return;

                    var chromInfoNew = chromInfoOld.ChangeAnnotations(
                        chromInfoOld.Annotations.ChangeNote(Convert.ToString(row.Cells[e.ColumnIndex].Value)));
                    Debug.Assert(chromInfoNew.UserSet);
                    Program.MainWindow.ModifyDocument("Set Note",
                        doc => (SrmDocument)doc.ReplaceChild(
                            PathToSelectedNode(precursorDocNode).Parent, SelectedTransitionGroupDocNode.ChangeResults(
                            ChangeChromInfo(precursorDocNode.Results, rowIdentifier, chromInfoOld, chromInfoNew))));
                    return;
                }
                if (e.ColumnIndex == TransitionNoteColumn.Index)
                {
                    var transitionDocNode = SelectedTransitionDocNode;
                    if (transitionDocNode == null)
                        return;

                    string noteNew = Convert.ToString(row.Cells[e.ColumnIndex].Value);
                    var chromInfoOld = GetChromInfo(transitionDocNode.Results, rowIdentifier);
                    if (Equals(chromInfoOld.Annotations.Note, noteNew))
                        return;

                    var chromInfoNew = chromInfoOld.ChangeAnnotations(
                        chromInfoOld.Annotations.ChangeNote(Convert.ToString(row.Cells[e.ColumnIndex].Value)));
                    Debug.Assert(chromInfoNew.UserSet);
                    Program.MainWindow.ModifyDocument("Set Note",
                        doc => (SrmDocument)doc.ReplaceChild(
                            PathToSelectedNode(transitionDocNode).Parent, transitionDocNode.ChangeResults(
                            ChangeChromInfo(transitionDocNode.Results, rowIdentifier, chromInfoOld, chromInfoNew))));
                    return;
                }
                var column = Columns[e.ColumnIndex];
                var annotationDef = column.Tag as AnnotationDef;
                if (annotationDef != null)
                {
                    var chromInfo = GetChromInfo(annotationDef, rowIdentifier);
                    var transitionChromInfo = chromInfo as TransitionChromInfo;
                    var value = row.Cells[e.ColumnIndex].Value;
                    if (transitionChromInfo != null)
                    {
                        var chromInfoNew = transitionChromInfo.ChangeAnnotations(
                            transitionChromInfo.Annotations.ChangeAnnotation(annotationDef, value));
                        Program.MainWindow.ModifyDocument("Set " + annotationDef.Name, 
                            doc => (SrmDocument) doc.ReplaceChild(
                                PathToSelectedNode(SelectedTransitionDocNode).Parent,
                                SelectedTransitionDocNode.ChangeResults(
                                    ChangeChromInfo(SelectedTransitionDocNode.Results, rowIdentifier, transitionChromInfo, chromInfoNew))));
            }
                    var transitionGroupChromInfo = chromInfo as TransitionGroupChromInfo;
                    if (transitionGroupChromInfo != null)
                    {
                        var chromInfoNew = transitionGroupChromInfo.ChangeAnnotations(
                            transitionGroupChromInfo.Annotations.ChangeAnnotation(annotationDef, value));
                        Program.MainWindow.ModifyDocument("Set " + annotationDef.Name,
                            doc => (SrmDocument) doc.ReplaceChild(
                                PathToSelectedNode(SelectedTransitionGroupDocNode).Parent,
                                SelectedTransitionGroupDocNode.ChangeResults(
                                    ChangeChromInfo(SelectedTransitionGroupDocNode.Results, rowIdentifier, transitionGroupChromInfo, chromInfoNew))));
                    }
                }
            }
            finally
            {
                _inCommitEdit = false;
            }
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

        /// <summary>
        /// For a node which is either the currently selected node, or one of its
        /// ancestors, return the path to that node.
        /// </summary>
        private IdentityPath PathToSelectedNode(DocNode docNode)
        {
            var identityPath = SelectedPath;
            while (identityPath != null && !docNode.EqualsId(Document.FindNode(identityPath)))
            {
                identityPath = identityPath.Parent;
            }
            return identityPath;
        }

        static TransitionGroupChromInfo GetChromInfo(Results<TransitionGroupChromInfo> results, RowIdentifier rowIdentifier)
        {
            foreach (var chromInfo in results[rowIdentifier.ReplicateIndex])
            {
                if (chromInfo.FileIndex == rowIdentifier.FileIndex && chromInfo.OptimizationStep == rowIdentifier.OptimizationStep)
                {
                    return chromInfo;
                }
            }
            return null;
        }

        ChromInfo GetChromInfo(AnnotationDef annotationDef, RowIdentifier rowIdentifier)
        {
            if (SelectedTransitionDocNode != null && 0 != (annotationDef.AnnotationTargets & AnnotationDef.AnnotationTarget.transition_result))
            {
                return GetChromInfo(SelectedTransitionDocNode.Results, rowIdentifier);
            }
            if (SelectedTransitionGroupDocNode != null && 0 != (annotationDef.AnnotationTargets & AnnotationDef.AnnotationTarget.precursor_result))
            {
                return GetChromInfo(SelectedTransitionGroupDocNode.Results, rowIdentifier);
            }
            return null;
        }

        static TransitionChromInfo GetChromInfo(Results<TransitionChromInfo> results, RowIdentifier rowIdentifier)
        {
            foreach (var chromInfo in results[rowIdentifier.ReplicateIndex])
            {
                if (chromInfo.FileIndex == rowIdentifier.FileIndex && chromInfo.OptimizationStep == rowIdentifier.OptimizationStep)
                {
                    return chromInfo;
                }
            }
            return null;
        }

        static Results<T> ChangeChromInfo<T>(Results<T> results, RowIdentifier rowIdentifier, T chromInfoOld, T chromInfoNew) where T: ChromInfo
        {
            var elements = new List<ChromInfoList<T>>();
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
                    var chromInfoList = new List<T>();
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
                    elements.Add(new ChromInfoList<T>(chromInfoList));
                }
            }
            if (!found)
            {
                throw new InvalidOperationException("Element not found");
            }
            return new Results<T>(elements);
        }

        /// <summary>
        /// Stores the Setting which controls the set and order of visible columns and their widths.
        /// </summary>
        void SaveColumnState()
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
                               LibraryDotProductColumn,
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
                               OptStepColumn,
                           };
            }
        }

        DataGridViewRow EnsureRow(RowIdentifier rowIdentifier)
        {
            DataGridViewRow row;
            if (_chromInfoRows.TryGetValue(rowIdentifier, out row))
            {
                return row;
            }
            row = Rows[Rows.Add(new DataGridViewRow {Tag = rowIdentifier})];
            row.Cells[OptStepColumn.Index].Value = rowIdentifier.OptimizationStep;
            _chromInfoRows.Add(rowIdentifier, row);
            return row;
        }

        DataGridViewRow EnsureRow(int iReplicate, int iFile, int step, ICollection<RowIdentifier> rowIds)
        {
            var rowId = new RowIdentifier(iReplicate, iFile, step);
            rowIds.Add(rowId);
            return EnsureRow(rowId);
        }

        DataGridViewRow EnsureRow(int iReplicate, ICollection<RowIdentifier> rowIds)
        {
            return EnsureRow(iReplicate, 0, 0, rowIds);
        }

        private void ClearRows()
        {
            Rows.Clear();
            _chromInfoRows.Clear();
        }

        private void HideColumns()
        {
            foreach (DataGridViewColumn column in Columns)
            {
                if (!ReferenceEquals(column, ReplicateNameColumn))
                    column.Visible = false;
            }
        }

        /// <summary>
        /// Figures out which nodes in the document tree this control will be displaying data from.
        /// Remembers the selected IdentifyPath, TransitionDocNode, TransitionGroupDocNode and PeptideDocNode.
        /// </summary>
        private void UpdateSelectedTreeNode()
        {
            Document = DocumentUiContainer.DocumentUI;
            var srmTreeNode = StateProvider.SelectedNode as SrmTreeNode;
            if (srmTreeNode == null)
            {
                SelectedTransitionDocNode = null;
                SelectedTransitionGroupDocNode = null;
                SelectedPeptideDocNode = null;
                SelectedPath = null;
                ClearRows();
                HideColumns();
                return;
            }
            var newPath = srmTreeNode.Path;
            if (SelectedPath != null && SelectedPath.Depth != newPath.Depth)
                ClearRows();
            SelectedPath = srmTreeNode.Path;
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
            foreach (var annotationDef in Document.Settings.DataSettings.AnnotationDefs)
            {
                if (0 == (annotationDef.AnnotationTargets & (AnnotationDef.AnnotationTarget.precursor_result | AnnotationDef.AnnotationTarget.transition_result)))
                {
                    continue;
                }
                var name = AnnotationPropertyAccessor.AnnotationPrefix + annotationDef.Name;
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
            foreach (var entry in _annotationColumns)
            {
                if (!newAnnotationColumns.ContainsKey(entry.Key))
                {
                    Columns.Remove(entry.Key);
                }
            }
            _annotationColumns = newAnnotationColumns;
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
            if (SelectedPath == null || Document.Settings.MeasuredResults == null)
            {
                return;
            }
            UpdateAnnotationColumns();
            // Remember the set of rowIds that have data in them.  After updating the data, rows
            // that are not in this set will be deleted from the grid
            var rowIds = new HashSet<RowIdentifier>();
            var settings = Document.Settings;

            if (SelectedTransitionDocNode != null && SelectedTransitionDocNode.HasResults)
            {
                for (int iReplicate = 0; iReplicate < SelectedTransitionDocNode.Results.Count; iReplicate++) 
                {
                    var results = SelectedTransitionDocNode.Results[iReplicate];
                    if (results == null)
                    {
                        var row = EnsureRow(iReplicate, rowIds);
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
                    var optFunc = settings.MeasuredResults.Chromatograms[iReplicate].OptimizationFunction;

                    var results = SelectedTransitionGroupDocNode.Results[iReplicate];
                    if (results == null)
                    {
                        var row = EnsureRow(iReplicate, rowIds);
                        FillTransitionGroupRow(row, settings, optFunc, null);
                        continue;
                    }

                    foreach (var chromInfo in results)
                    {
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
                        EnsureRow(iReplicate, rowIds);
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
                            EnsureRow(iReplicate, rowIds);
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
            for (int iReplicate = 0; iReplicate < settings.MeasuredResults.Chromatograms.Count; iReplicate++)
            {
                var results = settings.MeasuredResults.Chromatograms[iReplicate];
                for (int iFile = 0; iFile < results.MSDataFilePaths.Count; iFile++)
                {
                    var rowIdZero = new RowIdentifier(iReplicate, iFile, 0);
                    ICollection<RowIdentifier> optStepRowIds;
                    if (!replicateRowDict.TryGetValue(rowIdZero, out optStepRowIds))
                        continue;
                    var filePath = results.MSDataFilePaths[iFile];
                    var fileName = SampleHelp.GetFileName(filePath);
                    var sampleName = SampleHelp.GetFileSampleName(filePath);
                    foreach (var rowId in optStepRowIds)
                    {
                        var row = _chromInfoRows[rowId];
                        row.Cells[ReplicateNameColumn.Index].Value = results.Name;
                        row.Cells[FileNameColumn.Index].Value = fileName;
                        row.Cells[SampleNameColumn.Index].Value = sampleName;
                    }
                }
            }

            if (SelectedPeptideDocNode != null)
            {
                for (int iReplicate = 0; iReplicate < SelectedPeptideDocNode.Results.Count; iReplicate++)
                {
                    var results = SelectedPeptideDocNode.Results[iReplicate];
                    if (results == null)
                    {
                        var row = EnsureRow(iReplicate, rowIds);
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

            // Set the visible columns to the default
            var defaultColumnSet = GetDefaultColumns();
            foreach (DataGridViewColumn column in Columns)
            {
                column.Visible = defaultColumnSet.Contains(column);
            }

            // Update column visibility from the settings
            GridColumns gridColumns = null;
            var gridColumnsKey = GetGridColumnsKey();
            if (gridColumnsKey != null)
            {
                Settings.Default.GridColumnsList.TryGetValue(gridColumnsKey, out gridColumns);
            }
            if (gridColumns != null)
            {
                var availableColumnSet = new HashSet<DataGridViewColumn>(GetAvailableColumns());
                int displayIndex = 0;
                for (int iColumn = 0; iColumn < gridColumns.Columns.Count; iColumn++)
                {
                    var gridColumn = gridColumns.Columns[iColumn];
                    var column = Columns[gridColumn.Name];
                    if (column == null) {
                        continue;
                    }
                    column.Width = gridColumn.Width;
                    column.DisplayIndex = displayIndex++;
                    column.Visible = gridColumn.Visible && availableColumnSet.Contains(column);
                }
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
                row.Cells[RatioToStandardColumn.Index].Value = chromInfo.RatioToStandard;
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
                    row.Cells[LibraryDotProductColumn.Index].Value =
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
                row.Cells[LibraryDotProductColumn.Index].Value = chromInfo.LibraryDotProduct;
                row.Cells[PrecursorNoteColumn.Index].Value = chromInfo.Annotations.Note;
                row.Cells[OptCollisionEnergyColumn.Index].Value = null;
                row.Cells[OptDeclusteringPotentialColumn.Index].Value = null;

                if (optFunc != null)
                {
                    double regressionMz = settings.GetRegressionMz(SelectedPeptideDocNode,
                        SelectedTransitionGroupDocNode);
                    if (optFunc is CollisionEnergyRegression)
                    {
                        int charge = SelectedTransitionGroupDocNode.TransitionGroup.PrecursorCharge;
                        row.Cells[OptCollisionEnergyColumn.Index].Value =
                            ((CollisionEnergyRegression)optFunc).GetCollisionEnergy(
                                charge, regressionMz, chromInfo.OptimizationStep);
                    }
                    if (optFunc is DeclusteringPotentialRegression)
                    {
                        row.Cells[OptDeclusteringPotentialColumn.Index].Value =
                            ((DeclusteringPotentialRegression)optFunc).GetDeclustringPotential(
                                regressionMz, chromInfo.OptimizationStep);
                    }
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
                    row.Cells[PeakRankColumn.Index].Value =
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
                row.Cells[AreaRatioColumn.Index].Value = chromInfo.Ratio;
                row.Cells[HeightColumn.Index].Value = chromInfo.Height;
                row.Cells[PeakRankColumn.Index].Value = chromInfo.Rank;
                row.Cells[TransitionNoteColumn.Index].Value = chromInfo.Annotations.Note;
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

        public void UpdateSelectedReplicate()
        {
            if (_inReplicateChange)
                return;

            EndEdit();
            if (_inCommitEdit || CurrentRow == null)
            {
                return;
            }
            int selectedResult = StateProvider.SelectedResultsIndex;
            int selectedResultRow = ((RowIdentifier)CurrentRow.Tag).ReplicateIndex;

            int selectedColumn = (SelectedCells.Count > 0 ? SelectedCells[0].ColumnIndex : 0);
            if (selectedResult != selectedResultRow)
            {
                // For some reason GridView seems to store these in reverse order
                for (int iRow = 0; iRow < Rows.Count; iRow++)
                {
                    if (((RowIdentifier)Rows[iRow].Tag).ReplicateIndex != selectedResult)
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
            if (SelectedPath == null)
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
            AnnotationDef.AnnotationTarget annotationTargets = 0;
            if (SelectedTransitionDocNode != null)
            {
                result.UnionWith(TransitionColumns);
                annotationTargets = AnnotationDef.AnnotationTarget.transition_result;
            }
            else if (SelectedTransitionGroupDocNode != null)
            {
                result.UnionWith(from column in PrecursorColumns
                                     where !ReferenceEquals(column, OptCollisionEnergyColumn) &&
                                           !ReferenceEquals(column, OptDeclusteringPotentialColumn)
                                     select column);
                annotationTargets = AnnotationDef.AnnotationTarget.precursor_result;
            }
            else if (SelectedPeptideDocNode != null)
            {
                result.UnionWith(PeptideColumns);
            }
            foreach (var column in _annotationColumns.Values)
            {
                var annotationDef = (AnnotationDef) column.Tag;
                if (0 != (annotationDef.AnnotationTargets & annotationTargets))
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
            AnnotationDef.AnnotationTarget targets = 0;
            if (SelectedPeptideDocNode != null)
            {
                result.AddRange(PeptideColumns);
            }
            if (SelectedTransitionGroupDocNode != null)
            {
                result.AddRange(PrecursorColumns);
                targets |= AnnotationDef.AnnotationTarget.precursor_result;
            }
            if (SelectedTransitionDocNode != null)
            {
                result.AddRange(TransitionColumns);
                targets |= AnnotationDef.AnnotationTarget.transition_result;
            }
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

        public IDocumentUIContainer DocumentUiContainer { get; private set; }
        public IStateProvider StateProvider { get; private set; }

        // Replicate Columns
        public DataGridViewTextBoxColumn ReplicateNameColumn { get; private set; }
        public DataGridViewTextBoxColumn FileNameColumn { get; private set; }
        public DataGridViewTextBoxColumn SampleNameColumn { get; private set; }
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
        public DataGridViewTextBoxColumn LibraryDotProductColumn { get; private set; }
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
        public DataGridViewTextBoxColumn PeakRankColumn { get; private set; }
        public DataGridViewTextBoxColumn TransitionNoteColumn { get; private set; }

        private SrmDocument Document { get; set; }
        private IdentityPath SelectedPath { get; set; }
        private TransitionDocNode SelectedTransitionDocNode { get; set; }
        private TransitionGroupDocNode SelectedTransitionGroupDocNode { get; set; }
        private PeptideDocNode SelectedPeptideDocNode { get; set; }
        
        /// <summary>
        /// Identifies which result a row in the grid refers to.
        /// </summary>
        class RowIdentifier
        {
            public RowIdentifier(int replicateIndex, int fileIndex, int optStep)
            {
                ReplicateIndex = replicateIndex;
                FileIndex = fileIndex;
                OptimizationStep = optStep;
            }

            public int ReplicateIndex { get; private set; }
            public int FileIndex { get; private set; }
            public int OptimizationStep { get; private set; }
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
