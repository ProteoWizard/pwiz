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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Find;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using Array = System.Array;

namespace pwiz.Skyline.Controls
{
    /// <summary>
    /// Displays results in a grid
    /// </summary>
    public class ResultsGrid : DataGridViewEx
    {
        private readonly Dictionary<RowIdentifier, DataGridViewRow> _chromInfoRows 
            = new Dictionary<RowIdentifier, DataGridViewRow>();
        private readonly IDictionary<RowIdentifier, DataGridViewRow> _unassignedRowsDict
            = new Dictionary<RowIdentifier, DataGridViewRow>();
        private readonly Stack<DataGridViewRow> _unassignedRowsStack
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

        // ReSharper disable NonLocalizedString
        public ResultsGrid()
        {
            // Replicate
            Columns.Add(ReplicateNameColumn
                = new DataGridViewTextBoxColumn
                    {
                        Name = "ReplicateName",
                        HeaderText = Resources.ResultsGrid_ResultsGrid_Replicate_Name,
                        ReadOnly = true,
                    });
            Columns.Add(FileNameColumn
                = new DataGridViewTextBoxColumn
                    {
                        Name = "FileName",
                        HeaderText = Resources.ResultsGrid_ResultsGrid_File_Name,
                        ReadOnly = true
                    });
            Columns.Add(SampleNameColumn
                = new DataGridViewTextBoxColumn
                    {
                        Name = "SampleName",
                        HeaderText = Resources.ResultsGrid_ResultsGrid_Sample_Name,
                        ReadOnly = true
                    });
            Columns.Add(ModifiedTimeColumn
                = new DataGridViewTextBoxColumn
                {
                    Name = "ModifiedTime",
                    HeaderText = Resources.ResultsGrid_ResultsGrid_Modified_Time,
                    ReadOnly = true
                });
            Columns.Add(AcquiredTimeColumn
                = new DataGridViewTextBoxColumn
                {
                    Name = "AcquiredTime",
                    HeaderText = Resources.ResultsGrid_ResultsGrid_Acquired_Time,
                    ReadOnly = true
                });
            Columns.Add(OptStepColumn
                = new DataGridViewTextBoxColumn
                    {
                        Name = "OptStep",
                        HeaderText = Resources.ResultsGrid_ResultsGrid_Opt_Step,
                        ReadOnly = true,
                    });

            // Peptide
            Columns.Add(PeptidePeakFoundRatioColumn
                = new DataGridViewTextBoxColumn
                    {
                        Name = "PeptidePeakFoundRatio",
                        HeaderText = Resources.ResultsGrid_ResultsGrid_Peptide_Peak_Found_Ratio,
                        ReadOnly = true,
                        DefaultCellStyle = {Format = Formats.PEAK_FOUND_RATIO}
                    });
            Columns.Add(PeptideRetentionTimeColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "PeptideRetentionTime",
                          HeaderText = Resources.ResultsGrid_ResultsGrid_Peptide_Retention_Time,
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.RETENTION_TIME}
                      });
            Columns.Add(RatioToStandardColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "RatioToStandard",
                          HeaderText = Resources.ResultsGrid_ResultsGrid_Ratio_To_Standard,
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.STANDARD_RATIO}
                      });
            // Precursor
            Columns.Add(PrecursorNoteColumn
                = new DataGridViewTextBoxColumn
                {
                    Name = "PrecursorReplicateNote",
                    HeaderText = Resources.ResultsGrid_ResultsGrid_Precursor_Replicate_Note,
                });
            Columns.Add(PrecursorPeakFoundRatioColumn
                = new DataGridViewTextBoxColumn
                    {
                        Name = "PrecursorPeakFound",
                        HeaderText = Resources.ResultsGrid_ResultsGrid_Precursor_Peak_Found_Ratio,
                        ReadOnly = true,
                        DefaultCellStyle = {Format = Formats.PEAK_FOUND_RATIO}
                    });
            Columns.Add(BestRetentionTimeColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "BestRetentionTime",
                          HeaderText = Resources.ResultsGrid_ResultsGrid_Best_Retention_Time,
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.RETENTION_TIME}
                      });
            Columns.Add(MaxFwhmColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "MaxFwhm",
                          HeaderText = Resources.ResultsGrid_ResultsGrid_Max_Fwhm,
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.RETENTION_TIME}
                      });
            Columns.Add(MinStartTimeColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "MinStartTime",
                          HeaderText = Resources.ResultsGrid_ResultsGrid_Min_Start_Time,
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.RETENTION_TIME}
                      });
            Columns.Add(MaxEndTimeColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "MaxEndTime",
                          HeaderText = Resources.ResultsGrid_ResultsGrid_Max_End_Time,
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.RETENTION_TIME}
                      });
            Columns.Add(TotalAreaColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "TotalArea",
                          HeaderText = Resources.ResultsGrid_ResultsGrid_Total_Area,
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.PEAK_AREA}
                      });
            Columns.Add(TotalBackgroundColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "TotalBackground",
                          HeaderText = Resources.ResultsGrid_ResultsGrid_Total_Background,
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.PEAK_AREA}
                      });
            Columns.Add(TotalAreaRatioColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "TotalAreaRatio",
                          HeaderText = Resources.ResultsGrid_ResultsGrid_Total_Area_Ratio,
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.STANDARD_RATIO}
                      });
            Columns.Add(RatioDotProductColumn
                = new DataGridViewTextBoxColumn
                    {
                        Name = "RatioDotProduct",
                        HeaderText = Resources.ResultsGrid_ResultsGrid_Ratio_Dot_Product,
                        ReadOnly = true,
                        DefaultCellStyle = {Format = Formats.STANDARD_RATIO}
                    });
            Columns.Add(MaxHeightColumn
                = new DataGridViewTextBoxColumn
                {
                    Name = "MaxHeight",
                    HeaderText = Resources.ResultsGrid_ResultsGrid_Max_Height,
                    ReadOnly = true,
                    DefaultCellStyle = { Format = Formats.PEAK_AREA }
                });
            Columns.Add(AverageMassErrorColumn
                = new DataGridViewTextBoxColumn
                {
                    Name = "AverageMassErrorPPM",
                    HeaderText = Resources.ResultsGrid_ResultsGrid_Average_Mass_Error_PPM,
                    ReadOnly = true,
                    DefaultCellStyle = {Format = Formats.MASS_ERROR}
                });
            Columns.Add(CountTruncatedColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "CountTruncated",
                          HeaderText = Resources.ResultsGrid_ResultsGrid_Count_Truncated,
                          ReadOnly = true
                      });
            Columns.Add(IdentifiedColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "Identified",
                          HeaderText = Resources.ResultsGrid_ResultsGrid_Identified,
                          ReadOnly = true
                      });
            Columns.Add(UserSetTotalColumn
                = new DataGridViewTextBoxColumn
                {
                    Name = "UserSetTotal",
                    HeaderText = Resources.ResultsGrid_ResultsGrid_User_Set_Total,
                    ReadOnly = true
                });
            Columns.Add(LibraryDotProductColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "LibraryDotProduct",
                          HeaderText = Resources.ResultsGrid_ResultsGrid_Library_Dot_Product,
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.STANDARD_RATIO}
                      });
            Columns.Add(IsotopeDotProductColumn
                = new DataGridViewTextBoxColumn
                {
                    Name = "IsotopeDotProduct",
                    HeaderText = Resources.ResultsGrid_ResultsGrid_Isotope_Dot_Product,
                    ReadOnly = true,
                    DefaultCellStyle = { Format = Formats.STANDARD_RATIO }
                });
            Columns.Add(OptCollisionEnergyColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "OptCollisionEnergy",
                          HeaderText = Resources.ResultsGrid_ResultsGrid_Opt_Collision_Energy,
                          ReadOnly = true,
                          DefaultCellStyle = { Format = Formats.OPT_PARAMETER }
                      });
            Columns.Add(OptDeclusteringPotentialColumn
                = new DataGridViewTextBoxColumn
                {
                    Name = "OptDeclusteringPotential",
                    HeaderText = Resources.ResultsGrid_ResultsGrid_Opt_Declustering_Potential,
                    ReadOnly = true,
                    DefaultCellStyle = { Format = Formats.OPT_PARAMETER }
                });
            Columns.Add(OptCompensationVoltageColumn
                = new DataGridViewTextBoxColumn
                {
                    Name = "OptCompensationVoltage",
                    HeaderText = Resources.ResultsGrid_ResultsGrid_Opt_Compensation_Voltage,
                    ReadOnly = true,
                    DefaultCellStyle = { Format = Formats.OPT_PARAMETER }
                });
            
            // Transitions
            Columns.Add(TransitionNoteColumn
                = new DataGridViewTextBoxColumn
                {
                    Name = "TransitionReplicateNote",
                    HeaderText = Resources.ResultsGrid_ResultsGrid_Transition_Replicate_Note
                });
            Columns.Add(RetentionTimeColumn 
                = new DataGridViewTextBoxColumn
                  {
                      Name = "RetentionTime",
                      HeaderText = Resources.ResultsGrid_ResultsGrid_Best_Retention_Time,
                      ReadOnly = true,
                      DefaultCellStyle = { Format = Formats.RETENTION_TIME },
                  });
            Columns.Add(FwhmColumn 
                = new DataGridViewTextBoxColumn
                    {
                        Name = "Fwhm",
                        HeaderText = Resources.ResultsGrid_ResultsGrid_Fwhm,
                        ReadOnly = true,
                        DefaultCellStyle = { Format = Formats.RETENTION_TIME },
                    });
            Columns.Add(StartTimeColumn 
                = new DataGridViewTextBoxColumn
                    {
                        Name = "StartTime",
                        HeaderText = Resources.ResultsGrid_ResultsGrid_Start_Time,
                        ReadOnly = true,
                        DefaultCellStyle = {Format = Formats.RETENTION_TIME},
                    });
            Columns.Add(EndTimeColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "EndTime",
                          HeaderText = Resources.ResultsGrid_ResultsGrid_End_Time,
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.RETENTION_TIME}
                      });
            Columns.Add(AreaColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "Area",
                          HeaderText = Resources.ResultsGrid_ResultsGrid_Area,
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.PEAK_AREA}
                      });
            Columns.Add(BackgroundColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "Background",
                          HeaderText = Resources.ResultsGrid_ResultsGrid_Background,
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.PEAK_AREA}
                      });
            Columns.Add(AreaRatioColumn = new DataGridViewTextBoxColumn
                      {
                          Name = "AreaRatio",
                          HeaderText = Resources.ResultsGrid_ResultsGrid_Area_Ratio,
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.STANDARD_RATIO}
                      });
            Columns.Add(HeightColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "Height",
                          HeaderText = Resources.ResultsGrid_ResultsGrid_Height,
                          ReadOnly = true,
                          DefaultCellStyle = {Format = Formats.PEAK_AREA}
                      });
            Columns.Add(MassErrorColumn
                = new DataGridViewTextBoxColumn
                {
                    Name = "MassErrorPPM",
                    HeaderText = Resources.ResultsGrid_ResultsGrid_Mass_Error_PPM,
                    ReadOnly = true,
                    DefaultCellStyle = {Format = Formats.MASS_ERROR}
                });
            Columns.Add(TruncatedColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "Truncated",
                          HeaderText = Resources.ResultsGrid_ResultsGrid_Truncated,
                          ReadOnly = true,
                      });
            Columns.Add(PeakRankColumn
                = new DataGridViewTextBoxColumn
                      {
                          Name = "PeakRank",
                          HeaderText = Resources.ResultsGrid_ResultsGrid_Peak_Rank,
                          ReadOnly = true,
                      });
            Columns.Add(UserSetColumn
                = new DataGridViewTextBoxColumn
                {
                    Name = "UserSet",
                    HeaderText = Resources.ResultsGrid_ResultsGrid_User_Set,
                    ReadOnly = true,
                });
            KeyDown += ResultsGrid_KeyDown;
            CellEndEdit += ResultsGrid_CellEndEdit;
            CurrentCellChanged += ResultsGrid_CurrentCellChanged;
            DataError += ResultsGrid_DataError;
        }
        // ReSharper restore NonLocalizedString

        public void Init(IDocumentUIContainer documentUiContainer)
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
                Program.MainWindow.ModifyDocument(Resources.ResultsGrid_ResultsGrid_CellEndEdit_Edit_note, doc =>
                    {
                        var newDocument = UpdateDocument(doc, SelectedPaths, Rows[e.RowIndex], Columns, e.ColumnIndex,
                                                         PrecursorNoteColumn.Index, TransitionNoteColumn.Index);
                        return newDocument.Equals(doc) ? doc : newDocument;
                    }
                );
            }
            finally
            {
                _inCommitEdit = false;
            }
        }

        private void ResultsGrid_DataError(object sender, DataGridViewDataErrorEventArgs dataGridViewDataErrorEventArgs)
        {
            var column = Columns[dataGridViewDataErrorEventArgs.ColumnIndex];
            var annotationDef = column.Tag as AnnotationDef;
            if (annotationDef != null)
            {
                if (0 != (dataGridViewDataErrorEventArgs.Context & DataGridViewDataErrorContexts.Commit))
                {
                    MessageDlg.Show(this, annotationDef.ValidationErrorMessage);
                }
                return;
            }
            dataGridViewDataErrorEventArgs.ThrowException = true;
        }

        public static SrmDocument UpdateDocument(SrmDocument doc, IList<IdentityPath> selectedPaths, DataGridViewRow row, 
            DataGridViewColumnCollection columns, int columnIndex, int precursorNoteColumnIndex, int transitionNoteColumnIndex)
        {
            var rowIdentifier = row.Tag as RowIdentifier;
            if (rowIdentifier == null)
            {
                return doc;
            }

            bool updateAllSteps = selectedPaths.Count > 1;
            AnnotationDef.AnnotationTarget? noteTarget = null;
            AnnotationDef annotationDef = null;
            if (columnIndex == precursorNoteColumnIndex)
            {
                noteTarget = AnnotationDef.AnnotationTarget.precursor_result;
            }
            else if (columnIndex == transitionNoteColumnIndex)
            {
                noteTarget = AnnotationDef.AnnotationTarget.transition_result;
            }
            else
            {
                annotationDef = columns[columnIndex].Tag as AnnotationDef;
            }
            bool isNote = noteTarget.HasValue;
            if (!isNote && null == annotationDef)
            {
                return doc;
            }
            var value = row.Cells[columnIndex].Value;
            foreach (IdentityPath selPath in selectedPaths)
            {
                var selTarget = noteTarget ?? GetAnnotationTargetForNode(annotationDef, doc, selPath);
                if (!selTarget.HasValue)
                {
                    continue;
                }
                if (AnnotationDef.AnnotationTarget.replicate == selTarget)
                {
                    var chromatograms = doc.Settings.MeasuredResults.Chromatograms.ToArray();
                    var chromatogramSet = chromatograms[rowIdentifier.ReplicateIndex];
                    chromatogramSet =
                        chromatogramSet.ChangeAnnotations(chromatogramSet.Annotations.ChangeAnnotation(annotationDef, value));
                    chromatograms[rowIdentifier.ReplicateIndex] = chromatogramSet;
                    doc = doc.ChangeMeasuredResults(doc.Settings.MeasuredResults.ChangeChromatograms(chromatograms));
                    continue;
                }
                var nodeArray = doc.ToNodeArray(selPath);
                if (AnnotationDef.AnnotationTarget.transition_result == selTarget)
                {
                    var transitionDocNode = nodeArray.OfType<TransitionDocNode>().FirstOrDefault();
                    if (null == transitionDocNode)
                    {
                        continue;
                    }
                    var nodePath = selPath.GetPathTo(Array.IndexOf(nodeArray, transitionDocNode));
                    foreach (var chromInfo in GetChromInfos(transitionDocNode.Results, rowIdentifier, updateAllSteps))
                    {
                        TransitionChromInfo chromInfoNew;
                        if (isNote)
                        {
                            chromInfoNew = chromInfo.ChangeAnnotations(chromInfo.Annotations.ChangeNote(Convert.ToString(value)));
                        }
                        else
                        {
                            chromInfoNew =
                                chromInfo.ChangeAnnotations(chromInfo.Annotations.ChangeAnnotation(annotationDef, value));
                        }
                        transitionDocNode = transitionDocNode.ChangeResults(
                            ChangeChromInfo(transitionDocNode.Results, rowIdentifier, chromInfo, chromInfoNew));
                    }
                    doc = (SrmDocument) doc.ReplaceChild(nodePath.Parent, transitionDocNode);
                }
                else if (AnnotationDef.AnnotationTarget.precursor_result == selTarget)
                {
                    var transitionGroupDocNode = nodeArray.OfType<TransitionGroupDocNode>().FirstOrDefault();
                    if (null == transitionGroupDocNode)
                    {
                        continue;
                    }
                    var nodePath = selPath.GetPathTo(Array.IndexOf(nodeArray, transitionGroupDocNode));
                    foreach (var chromInfo in GetChromInfos(transitionGroupDocNode.Results, rowIdentifier, updateAllSteps))
                    {
                        TransitionGroupChromInfo chromInfoNew;
                        if (isNote)
                        {
                            chromInfoNew = chromInfo.ChangeAnnotations(chromInfo.Annotations.ChangeNote(Convert.ToString(value)));
                        }
                        else
                        {
                            chromInfoNew =
                                chromInfo.ChangeAnnotations(chromInfo.Annotations.ChangeAnnotation(annotationDef, value));
                        }
                        transitionGroupDocNode =
                            transitionGroupDocNode.ChangeResults(ChangeChromInfo(transitionGroupDocNode.Results,
                                                                                 rowIdentifier, chromInfo, chromInfoNew));
                    }
                    doc = (SrmDocument) doc.ReplaceChild(nodePath.Parent, transitionGroupDocNode);
                }
            }
            return doc;
        }

        /// <summary>
        /// Returns the set of <see cref="AnnotationDef.AnnotationTarget"/> that the particular
        /// annotation column will be getting its values from.  If there is any node selected
        /// to which the annotation does not apply, then this returns an empty set.
        /// </summary>
        private AnnotationDef.AnnotationTargetSet GetAnnotationTargetSet(AnnotationDef annotationDef)
        {
            var targets = SelectedPaths
                .Select(path => GetAnnotationTargetForNode(annotationDef, path))
                .ToArray();
            if (Enumerable.Contains(targets, null))
            {
                return AnnotationDef.AnnotationTargetSet.EMPTY;
            }
            return AnnotationDef.AnnotationTargetSet.OfValues(targets.Cast<AnnotationDef.AnnotationTarget>());
        }
        
        private AnnotationDef.AnnotationTarget? GetAnnotationTargetForNode(AnnotationDef annotationDef, IdentityPath selectedPath)
        {
            return GetAnnotationTargetForNode(annotationDef, Document, selectedPath);
        }

        private static AnnotationDef.AnnotationTarget? GetAnnotationTargetForNode(AnnotationDef annotationDef, SrmDocument document, IdentityPath selectedPath)
        {
            var resultTargets = annotationDef.AnnotationTargets.Intersect(new[]
                                                                             {
                                                                                 AnnotationDef.AnnotationTarget.
                                                                                     transition_result,
                                                                                 AnnotationDef.AnnotationTarget.
                                                                                     precursor_result,
                                                                                 AnnotationDef.AnnotationTarget.
                                                                                     replicate
                                                                             });
            if (resultTargets.IsEmpty)
            {
                return null;
            }
            if (annotationDef.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.transition_result))
            {
                if (document.ToNodeArray(selectedPath).OfType<TransitionDocNode>().Any())
                {
                    return AnnotationDef.AnnotationTarget.transition_result;                    
                }  
            }
            if (annotationDef.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.precursor_result))
            {
                if (document.ToNodeArray(selectedPath).OfType<TransitionGroupDocNode>().Any())
                {
                    return AnnotationDef.AnnotationTarget.precursor_result;
                }
            }
            if (annotationDef.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.replicate))
            {
                return AnnotationDef.AnnotationTarget.replicate;
            }
            return null;
        }

        private bool AnyTransitionsSelected()
        {
            return SelectedPaths.Any(path => Document.ToNodeArray(path).OfType<TransitionDocNode>().Any());
        }

        private bool OnlyTransitionsSelected()
        {
            return OnlyTransitionsSelected(Document, SelectedPaths);
        }

        private static bool OnlyTransitionsSelected(SrmDocument document, IEnumerable<IdentityPath> selectedPaths)
        {
            return selectedPaths.All(
                path =>
                    {
                        var nodeArray = document.ToNodeArray(path);
                        return nodeArray.OfType<TransitionDocNode>().Any();
                    }
                );
        }

        private bool AnyPrecursorsSelected()
        {
            return SelectedPaths.Any(path => Document.ToNodeArray(path).OfType<TransitionGroupDocNode>().Any());
        }

        private bool OnlyPrecursorsSelected()
        {
            return OnlyPrecursorsSelected(Document, SelectedPaths);
        }
        
        private static bool OnlyPrecursorsSelected(SrmDocument document, IEnumerable<IdentityPath> selectedPaths)
        {
            return selectedPaths.All(
                path =>
                    {
                        var nodeArray = document.ToNodeArray(path);
                        return !nodeArray.OfType<TransitionDocNode>().Any() &&
                               nodeArray.OfType<TransitionGroupDocNode>().Any();
                    });
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


        private static IEnumerable<ChromInfo> GetChromInfos(DocNode nodeDoc, RowIdentifier rowIdentifier, bool allOptimizationSteps)
        {
            var transitionDocNode = nodeDoc as TransitionDocNode;
            var transitionGroupDocNode = nodeDoc as TransitionGroupDocNode;
            if (transitionDocNode != null)
            {
                return GetChromInfos(transitionDocNode.Results, rowIdentifier, allOptimizationSteps); 
            }
            if (transitionGroupDocNode != null)
            {
                return GetChromInfos(transitionGroupDocNode.Results, rowIdentifier, allOptimizationSteps);
            }
            return new ChromInfo[0];
        }
        
        private static IEnumerable<TransitionGroupChromInfo> GetChromInfos(Results<TransitionGroupChromInfo> results, RowIdentifier rowIdentifier, bool allOptimizationSteps)
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

        private static IEnumerable<TransitionChromInfo> GetChromInfos(Results<TransitionChromInfo> results, RowIdentifier rowIdentifier, bool allOptimizationSteps)
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

        private static Results<TItem> ChangeChromInfo<TItem>(Results<TItem> results, RowIdentifier rowIdentifier, TItem chromInfoOld, TItem chromInfoNew) where TItem: ChromInfo
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
                        if (!ReferenceEquals(chromInfo, chromInfoOld))
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
                throw new InvalidOperationException(Resources.ResultsGrid_ChangeChromInfo_Element_not_found);
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
                                MassErrorColumn,
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
                               RatioDotProductColumn,
                               MaxHeightColumn,
                               AverageMassErrorColumn,
                               CountTruncatedColumn,
                               IdentifiedColumn,
                               UserSetTotalColumn,
                               LibraryDotProductColumn,
                               IsotopeDotProductColumn,
                               OptCollisionEnergyColumn,
                               OptDeclusteringPotentialColumn,
                               OptCompensationVoltageColumn,
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
            if (!TryGetUnassignedRow(rowIdentifier, out row))
                row = Rows[Rows.Add(new DataGridViewRow {Tag = rowIdentifier})];
            else
            {
                row.Tag = rowIdentifier;
                // To be extra safe, make sure the row is actually part of the grid.
                if (!ReferenceEquals(this, row.DataGridView))
                    Rows.Add(row);
            }
            row.Cells[OptStepColumn.Index].Value = rowIdentifier.OptimizationStep;
            _chromInfoRows.Add(rowIdentifier, row);
            return row;
        }

        private bool TryGetUnassignedRow(RowIdentifier rowIdentifier, out DataGridViewRow row)
        {
            if (_unassignedRowsDict.Count > 0 && _unassignedRowsDict.TryGetValue(rowIdentifier, out row))
            {
                _unassignedRowsDict.Remove(rowIdentifier);
                return true;
            }
            if (_unassignedRowsStack.Count > 0)
            {
                row = _unassignedRowsStack.Pop();
                return true;
            }
            row = null;
            return false;
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
        private void ClearAllRows(bool selChanged)
        {
            for (int i = Rows.Count - 1; i >= 0; i--)
            {
                var row = Rows[i];
                var rowIdentifier = row.Tag as RowIdentifier;
                if (rowIdentifier == null || _unassignedRowsDict.ContainsKey(rowIdentifier))
                {
                    Rows.RemoveAt(i);
                }
                else
                {
                    row.Tag = null;
                    _unassignedRowsStack.Push(row);
                    _unassignedRowsDict.Add(rowIdentifier, row);
                }
            }

            // If selection changed farm out rows in the order they are needed (stack),
            // otherwise try to keep rows associated with the same row identifier (dict).
            if (selChanged)
                _unassignedRowsDict.Clear();
            else
                _unassignedRowsStack.Clear();

            _chromInfoRows.Clear();
        }

        private void HideColumns()
        {
            foreach (DataGridViewColumn column in Columns.Cast<DataGridViewColumn>().Except(GetAvailableColumns()))
            {
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
                {
                    var firstVisibleColumn = Columns.Cast<DataGridViewColumn>()
                        .FirstOrDefault(col => col != column && col.Visible);
                    if (null != firstVisibleColumn)
                    {
                        CurrentCell = Rows[currentRowIndex].Cells[firstVisibleColumn.Index];
                    }
                }
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
            var oldSelectedPaths = SelectedPaths;
            SelectedPaths = new List<IdentityPath>();

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
            }

            if(noResults)
            {
                SelectedPaths = new List<IdentityPath> {IdentityPath.ROOT};
            }

            bool selChanged = !ArrayUtil.EqualsDeep(SelectedPaths, oldSelectedPaths);
            ClearAllRows(selChanged);
            // TODO: If the selection changed then a column sort may no longer apply
//            if (selChanged)
//                ClearSortIndicator();
           
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

        /// <summary>
        /// Ensures that there is a DataGridViewColumn in the grid for each annotation whose value
        /// can be displayed in this grid.  An annotation can be displayed in this grid if, for
        /// each of the paths in <see cref="SelectedPaths"/>, an AnnotationTarget can be found
        /// in <see cref="AnnotationDef.AnnotationTargets"/> which is one of 
        /// <see cref="AnnotationDef.AnnotationTarget.replicate"/>,
        /// <see cref="AnnotationDef.AnnotationTarget.precursor_result"/>,
        /// <see cref="AnnotationDef.AnnotationTarget.transition_result"/>,
        /// and that AnnotationTarget applies to the selected node, or one of its ancestors.
        /// 
        /// 
        /// </summary>
        private void UpdateAnnotationColumns()
        {
            var newAnnotationColumns = new Dictionary<string, DataGridViewColumn>();
            var listAnnotationDefs = Document.Settings.DataSettings.AnnotationDefs;
                                         
            foreach (var annotationDef in listAnnotationDefs)
            {
                if (GetAnnotationTargetSet(annotationDef).IsEmpty)
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
                        default:
                            canReuseColumn = column is DataGridViewTextBoxColumn;
                            break;
                        case AnnotationDef.AnnotationType.true_false:
                            canReuseColumn = column is DataGridViewCheckBoxColumn && column.ValueType == annotationDef.ValueType;
                            break;
                        case AnnotationDef.AnnotationType.value_list:
                            var comboBoxColumn = column as DataGridViewComboBoxColumn;
                            if (comboBoxColumn != null)
                            {
                                if (comboBoxColumn.Items.Count == annotationDef.Items.Count + 1 && string.Empty.Equals(comboBoxColumn.Items[0]))
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
                            comboBoxColumn.Items.Add(string.Empty);
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
                    column.ValueType = annotationDef.ValueType;
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
            var settings = DocumentUiContainer.DocumentUI.Settings;
            var mods = settings.PeptideSettings.Modifications;
            var standardTypes = mods.RatioInternalStandardTypes;
            var labelTypes = mods.GetModificationTypes().ToArray();
            // Add special ratio columns in order after the default ratio columns
            // for their element type.
            int indexPeptideRatio = 0;
            int indexInsert = Columns.IndexOf(RatioToStandardColumn) + 1;
            if (labelTypes.Length == 2)
            {
                // Skip the light-to-heavy ratio, if it extists.  It will be handled by the
                // Ratio To Standard column, and does not need a custom column created for it.
                indexPeptideRatio++;
            }
            else if (labelTypes.Length > 2)
            {
                foreach (var standardType in standardTypes)
                {
                    foreach (var labelType in labelTypes)
                    {
                        if (ReferenceEquals(standardType, labelType))
                            continue;

                        indexInsert = EnsureRatioColumn(RatioPropertyAccessor.PeptideRatioProperty(labelType, standardType),
                                          new RatioColumnTag(RatioPropertyAccessor.RatioTarget.peptide_result, indexPeptideRatio++),
                                          indexInsert,
                                          RatioPropertyAccessor.PeptideRdotpProperty(labelType, standardType),
                                          newRatioColumns);
                    }
                }                
                if (standardTypes.Count > 1)
                {
                    indexInsert = Columns.IndexOf(RatioDotProductColumn) + 1;
                    for (int i = 0; i < standardTypes.Count; i++)
                    {
                        indexInsert = EnsureRatioColumn(RatioPropertyAccessor.PrecursorRatioProperty(standardTypes[i]),
                                          new RatioColumnTag(RatioPropertyAccessor.RatioTarget.precursor_result, i),
                                          indexInsert,
                                          RatioPropertyAccessor.PrecursorRdotpProperty(standardTypes[i]),
                                          newRatioColumns);
                    }
                    indexInsert = Columns.IndexOf(AreaRatioColumn) + 1;
                    for (int i = 0; i < standardTypes.Count; i++)
                    {
                        indexInsert = EnsureRatioColumn(RatioPropertyAccessor.TransitionRatioProperty(standardTypes[i]),
                                          new RatioColumnTag(RatioPropertyAccessor.RatioTarget.transition_result, i),
                                          indexInsert,
                                          null,
                                          newRatioColumns);                        
                    }
                }
            }
            if (settings.HasGlobalStandardArea)
            {
                foreach (var labelType in labelTypes)
                {
                    indexInsert = EnsureRatioColumn(RatioPropertyAccessor.PeptideRatioProperty(labelType, null),
                                        new RatioColumnTag(RatioPropertyAccessor.RatioTarget.peptide_result, indexPeptideRatio++),
                                        indexInsert,
                                        null,
                                        newRatioColumns,
                                        Formats.GLOBAL_STANDARD_RATIO);
                }
                indexInsert = EnsureRatioColumn(RatioPropertyAccessor.PrecursorRatioProperty(null),
                                  new RatioColumnTag(RatioPropertyAccessor.RatioTarget.precursor_result, standardTypes.Count),
                                  indexInsert,
                                  null,
                                  newRatioColumns,
                                  Formats.GLOBAL_STANDARD_RATIO);
                /* indexInsert = */ EnsureRatioColumn(RatioPropertyAccessor.TransitionRatioProperty(null),
                                  new RatioColumnTag(RatioPropertyAccessor.RatioTarget.transition_result, standardTypes.Count),
                                  indexInsert,
                                  null,
                                  newRatioColumns,
                                  Formats.GLOBAL_STANDARD_RATIO);
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

        private int EnsureRatioColumn(RatioPropertyAccessor.RatioPropertyName propertyName,
                                      RatioColumnTag tag,
                                      int indexInsert,
                                      RatioPropertyAccessor.RatioPropertyName rdotpPropertyName,
                                      IDictionary<string, DataGridViewColumn> newRatioColumns,
                                      string format = Formats.STANDARD_RATIO)
        {
            DataGridViewColumn ratioColumn;
            if (!_ratioColumns.TryGetValue(propertyName.ColumnName, out ratioColumn))
            {
                ratioColumn = new DataGridViewTextBoxColumn
                {
                    Name = propertyName.ColumnName,
                    HeaderText = propertyName.HeaderText,
                    DefaultCellStyle = { Format = format }
                };
                Columns.Insert(indexInsert++, ratioColumn);
            }
            ratioColumn.Tag = tag;
            newRatioColumns[ratioColumn.Name] = ratioColumn;
            if (null != rdotpPropertyName)
            {
                DataGridViewColumn dotProductColumn;
                if (!_ratioColumns.TryGetValue(rdotpPropertyName.ColumnName, out dotProductColumn))
                {
                    dotProductColumn = new DataGridViewTextBoxColumn
                    {
                        Name = rdotpPropertyName.ColumnName,
                        HeaderText = rdotpPropertyName.HeaderText,
                        DefaultCellStyle = {Format = format}
                    };
                    Columns.Insert(indexInsert++, dotProductColumn);
                }
                dotProductColumn.Tag = tag;
                newRatioColumns[dotProductColumn.Name] = dotProductColumn;
            }
            return indexInsert;
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
                    var sampleName = filePath.GetSampleOrFileName();
                    foreach (var rowId in optStepRowIds)
                    {
                        var row = _chromInfoRows[rowId];
                        row.Cells[ReplicateNameColumn.Index].Value = results.Name;
                        row.Cells[FileNameColumn.Index].Value = fileName;
                        row.Cells[SampleNameColumn.Index].Value = sampleName;
                        row.Cells[ModifiedTimeColumn.Index].Value = fileInfo.FileWriteTime;
                        row.Cells[AcquiredTimeColumn.Index].Value = fileInfo.RunStartTime;
                        foreach (var annotationColumn in _annotationColumns.Values)
                        {
                            var annotationDef = (AnnotationDef) annotationColumn.Tag;
                            if (GetAnnotationTargetSet(annotationDef).IsSingleton(AnnotationDef.AnnotationTarget.replicate))
                            {
                                row.Cells[annotationColumn.Index].Value =
                                    results.Annotations.GetAnnotation(annotationDef);
                            }
                        }
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

            // There should only ever be rows in one of the unassigned rows collections at this point
            Assume.IsTrue(_unassignedRowsDict.Count == 0 || _unassignedRowsStack.Count == 0);
            try
            {
                foreach (var row in _unassignedRowsDict.Values)
                {
                    // To be extra safe, make sure the row is actually part of the grid before removing it.
                    if (ReferenceEquals(this, row.DataGridView))
                        Rows.Remove(row);
                }
                while (_unassignedRowsStack.Count > 0)
                {
                    var row = _unassignedRowsStack.Pop();
                    // To be extra safe, make sure the row is actually part of the grid before removing it.
                    if (ReferenceEquals(this, row.DataGridView))
                        Rows.Remove(row);
                }
            }
            finally
            {
                _unassignedRowsDict.Clear();
                _unassignedRowsStack.Clear();
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
            // Visibility is decided as whether or not the saved columns make the
            // column visible, or any default column that was not part of the
            // saved set.
            if (gridColumns != null)
            {
                int displayIndex = 0;
                foreach (var gridColumn in gridColumns.Columns)
                {
                    var column = Columns[gridColumn.Name];
                    if (column == null) {
                        continue;
                    }
                    column.Width = gridColumn.Width;
                    column.DisplayIndex = displayIndex++;
                    if (gridColumn.Visible)
                        visibleColumnNameSet.Add(column.Name);
                    else 
                        visibleColumnNameSet.Remove(column.Name);
                }
            }
            var availableColumnSet = new HashSet<DataGridViewColumn>(GetAvailableColumns());
            foreach (DataGridViewColumn column in Columns)
            {
                ShowColumn(column, visibleColumnNameSet.Contains(column.Name) && availableColumnSet.Contains(column));
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
                row.Cells[RatioToStandardColumn.Index].Value = RatioValue.GetRatio(chromInfo.LabelRatios[0].Ratio);
            }
            foreach (var column in _ratioColumns.Values)
            {
                var ratioTag = (RatioColumnTag)column.Tag;
                if (ratioTag.Target == RatioPropertyAccessor.RatioTarget.peptide_result)
                {
                    RatioValue ratioValue = null;
                    if (chromInfo != null)
                    {
                        ratioValue = chromInfo.LabelRatios[ratioTag.IndexRatio].Ratio;
                    }
                    if (RatioPropertyAccessor.IsRatioProperty(column.Name))
                    {
                        row.Cells[column.Index].Value = RatioValue.GetRatio(ratioValue);
                    }
                    else if (RatioPropertyAccessor.IsRdotpProperty(column.Name))
                    {
                        row.Cells[column.Index].Value = RatioValue.GetDotProduct(ratioValue);
                    }
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
                    row.Cells[RatioDotProductColumn.Index].Value = 
                    row.Cells[LibraryDotProductColumn.Index].Value =
                    row.Cells[IsotopeDotProductColumn.Index].Value =
                    row.Cells[MassErrorColumn.Index].Value =
                    row.Cells[CountTruncatedColumn.Index].Value =
                    row.Cells[IdentifiedColumn.Index].Value =
                    row.Cells[UserSetTotalColumn.Index].Value =
                    row.Cells[PrecursorNoteColumn.Index].Value =
                    row.Cells[OptCollisionEnergyColumn.Index].Value =
                    row.Cells[OptDeclusteringPotentialColumn.Index].Value =
                    row.Cells[OptCompensationVoltageColumn.Index].Value = null;
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
                row.Cells[TotalAreaRatioColumn.Index].Value = RatioValue.GetRatio(chromInfo.Ratios[0]);
                row.Cells[RatioDotProductColumn.Index].Value = RatioValue.GetDotProduct(chromInfo.Ratios[0]);
                row.Cells[MaxHeightColumn.Index].Value = chromInfo.Height;
                row.Cells[AverageMassErrorColumn.Index].Value = chromInfo.MassError;
                row.Cells[CountTruncatedColumn.Index].Value = chromInfo.Truncated;
                row.Cells[IdentifiedColumn.Index].Value = chromInfo.Identified;
                row.Cells[UserSetTotalColumn.Index].Value = chromInfo.UserSet;
                row.Cells[LibraryDotProductColumn.Index].Value = chromInfo.LibraryDotProduct;
                row.Cells[IsotopeDotProductColumn.Index].Value = chromInfo.IsotopeDotProduct;
                row.Cells[PrecursorNoteColumn.Index].Value = chromInfo.Annotations.Note;
                row.Cells[OptCollisionEnergyColumn.Index].Value = null;
                row.Cells[OptDeclusteringPotentialColumn.Index].Value = null;
                row.Cells[OptCompensationVoltageColumn.Index].Value = null;

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
                    var covRegression = optFunc as CompensationVoltageParameters;
                    if (covRegression != null)
                    {
                        row.Cells[OptCompensationVoltageColumn.Index].Value =
                            Document.GetCompensationVoltage(SelectedPeptideDocNode, SelectedTransitionGroupDocNode,
                                chromInfo.OptimizationStep, covRegression.TuneLevel);
                    }
                }
            }
            foreach (var column in _ratioColumns.Values)
            {
                var ratioTag = (RatioColumnTag)column.Tag;
                if (ratioTag.Target == RatioPropertyAccessor.RatioTarget.precursor_result)
                {
                    RatioValue ratioValue = chromInfo == null ? null : chromInfo.Ratios[ratioTag.IndexRatio];
                    if (RatioPropertyAccessor.IsRatioProperty(column.Name))
                    {
                        row.Cells[column.Index].Value = RatioValue.GetRatio(ratioValue);
                    }
                    else if (RatioPropertyAccessor.IsRdotpProperty(column.Name))
                    {
                        row.Cells[column.Index].Value = RatioValue.GetDotProduct(ratioValue);
                    }
                }
            }
            foreach (var column in _annotationColumns.Values)
            {
                var annotationDef = (AnnotationDef)column.Tag;
                if (GetAnnotationTargetSet(annotationDef).IsSingleton(AnnotationDef.AnnotationTarget.precursor_result))
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
                    row.Cells[MassErrorColumn.Index].Value =
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
                row.Cells[MassErrorColumn.Index].Value = chromInfo.MassError;
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
                var annotationDef = (AnnotationDef)column.Tag;
                if (GetAnnotationTargetSet(annotationDef).IsSingleton(AnnotationDef.AnnotationTarget.transition_result))
                {
                    row.Cells[column.Index].Value =
                        chromInfo == null ? null :
                        chromInfo.Annotations.GetAnnotation(annotationDef);
                }
            }
        }

        /// <summary>
        /// Sets the values for the given row using the given replicate index. Based on the nodes we selected, 
        /// annotation values must be merged. Annotation values that do not match for all nodes will be displayed
        /// as null in the result grid.
        /// </summary>
        public void FillMultiSelectRow(DataGridViewRow row, int replicateIndex)
        {
            var transitionNotes = new List<object>();
            var precursorNotes = new List<object>();
            var rowIdentifier = new RowIdentifier(replicateIndex, 0, null);
            foreach (var selPath in SelectedPaths)
            {
                var nodePath = Document.ToNodeArray(selPath);
                var transitionDocNode = nodePath.OfType<TransitionDocNode>().FirstOrDefault();
                var precursorDocNode = nodePath.OfType<TransitionGroupDocNode>().FirstOrDefault();
                if (transitionDocNode == null)
                {
                    transitionNotes.Add(null);
                }
                else
                {
                    var chromInfos = GetChromInfos(transitionDocNode.Results, rowIdentifier, true);
                    transitionNotes.AddRange(chromInfos.Select(chromInfo=>chromInfo.Annotations.Note));
                }
                if (precursorDocNode == null)
                {
                    precursorNotes.Add(null);
                }
                else
                {
                    var chromInfos = GetChromInfos(precursorDocNode.Results, rowIdentifier, true);
                    precursorNotes.AddRange(chromInfos.Select(chromInfo=>chromInfo.Annotations.Note));
                }
            }
            var distinctTransitionNotes = transitionNotes.Distinct().ToArray();
            if (distinctTransitionNotes.Length == 1)
            {
                row.Cells[TransitionNoteColumn.Index].Value = distinctTransitionNotes[0];
            }
            else
            {
                row.Cells[TransitionNoteColumn.Index].Value = null;
            }
            var distinctPrecursorNotes = precursorNotes.Distinct().ToArray();
            if (distinctPrecursorNotes.Length == 1)
            {
                row.Cells[PrecursorNoteColumn.Index].Value = distinctPrecursorNotes[0];
            }
            else
            {
                row.Cells[PrecursorNoteColumn.Index].Value = null;
            }
            foreach (var annotationColumn in _annotationColumns.Values)
            {
                var annotationDef = (AnnotationDef) annotationColumn.Tag;
                var values = new List<object>();
                foreach (IdentityPath selPath in SelectedPaths)
                {
                    var annotationTarget = GetAnnotationTargetForNode(annotationDef, selPath);
                    if (AnnotationDef.AnnotationTarget.transition_result == annotationTarget)
                    {
                        var transitionDocNode =
                            Document.ToNodeArray(selPath).OfType<TransitionDocNode>().FirstOrDefault();
                        if (transitionDocNode != null)
                        {
                            values.AddRange(
                                GetChromInfos(transitionDocNode.Results, rowIdentifier, true).Select(
                                    chromInfo => chromInfo.Annotations.GetAnnotation(annotationDef)));
                        }
                    }
                    else if (AnnotationDef.AnnotationTarget.precursor_result == annotationTarget)
                    {
                        var transitionGroupDocNode =
                            Document.ToNodeArray(selPath).OfType<TransitionGroupDocNode>().FirstOrDefault();
                        if (transitionGroupDocNode != null)
                        {
                            values.AddRange(
                                GetChromInfos(transitionGroupDocNode.Results, rowIdentifier, true).Select(
                                    chromInfo => chromInfo.Annotations.GetAnnotation(annotationDef)));
                        }
                    }
                    else if (AnnotationDef.AnnotationTarget.replicate == annotationTarget)
                    {
                        values.Add(
                            Document.Settings.MeasuredResults.Chromatograms[replicateIndex].Annotations.GetAnnotation(
                                annotationDef));
                    }
                }
                var distinctValues = values.Distinct().ToArray();
                if (distinctValues.Length == 1)
                {
                    row.Cells[annotationColumn.Index].Value = distinctValues[0];
                }
                else
                {
                    row.Cells[annotationColumn.Index].Value = null;
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
        /// The default order of the columns is controlled by GetAvailableColumns().
        /// This list is also always intersected with GetAvailableColumns().
        /// </summary>
        public ICollection<DataGridViewColumn> GetDefaultColumns()
        {
            var result = new HashSet<DataGridViewColumn> { ReplicateNameColumn };
            if (SelectedTransitionDocNode != null)
            {
                result.UnionWith(TransitionColumns.Except(new[]
                                                              {
                                                                  MassErrorColumn,
                                                                  TruncatedColumn,
                                                                  UserSetColumn
                                                              }));
            }
            else if (OnlyTransitionsSelected())
            {
                // Every selected node is a TransitionDocNode. The TransitionNoteColumn should be 
                // available.
                result.Add(TransitionNoteColumn);
            }
            else if (SelectedTransitionGroupDocNode != null)
            {
                result.UnionWith(
                    PrecursorColumns.Except(new[]
                                                {
                                                    IsotopeDotProductColumn,
                                                    AverageMassErrorColumn,
                                                    CountTruncatedColumn,
                                                    IdentifiedColumn,
                                                    UserSetTotalColumn,
                                                    OptCollisionEnergyColumn,
                                                    OptDeclusteringPotentialColumn,
                                                    OptCompensationVoltageColumn
                                                }));
            }
            else if (OnlyPrecursorsSelected())
            {
                // Only precursors are selected. The PrecursorNoteColumn should be available.
                result.Add(PrecursorNoteColumn);
            }
            else 
            {
                if (SelectedPeptideDocNode != null)
                {
                    result.UnionWith(PeptideColumns);
                }
            }
            var mostSpecificAnnotationTarget = AnnotationDef.AnnotationTarget.replicate;
            if (AnyTransitionsSelected())
            {
                mostSpecificAnnotationTarget = AnnotationDef.AnnotationTarget.transition_result;
            } 
            else if (AnyPrecursorsSelected())
            {
                mostSpecificAnnotationTarget = AnnotationDef.AnnotationTarget.precursor_result;
            }
            result.UnionWith(_annotationColumns.Values.Where(col => ((AnnotationDef)col.Tag).AnnotationTargets.Contains(mostSpecificAnnotationTarget)));
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
            }
            else if (OnlyPrecursorsSelected())
            {
                result.Add(PrecursorNoteColumn);
            }

            if (SelectedTransitionDocNode != null)
            {
                result.AddRange(TransitionColumns);
                InsertRatioColumns(result, AreaRatioColumn,
                    RatioPropertyAccessor.RatioTarget.transition_result);
            }
            else if (OnlyTransitionsSelected())
            {
                result.Add(TransitionNoteColumn);
            }
            var comparator = StringComparer.CurrentCultureIgnoreCase;
            var sortedAnnotationColumns = _annotationColumns.Values.ToArray();
            Array.Sort(sortedAnnotationColumns, (col1, col2)=>comparator.Compare(col1.Name, col2.Name));
            result.AddRange(sortedAnnotationColumns);
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
            // Do not update, if selection update is locked, since it may not be
            // consisten with the current document, which may cause exceptions
            if (!args.IsInSelUpdateLock)
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
            var chromInfo = GetChromInfos(currentTreeNode.Model, rowIdentifier, false).FirstOrDefault();
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
                        var results = transitionGroupDocNode.Results[replicateIndex];
                        if (null != results && results.Any(transitionGroupChromInfo 
                            => ReferenceEquals(chromInfo.FileId, transitionGroupChromInfo.FileId)))
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
                        var results = transitionDocNode.Results[replicateIndex];
                        if (null != results && results.Any(transitionChromInfo
                            => ReferenceEquals(chromInfo.FileId, transitionChromInfo.FileId)))
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
        public DataGridViewTextBoxColumn RatioDotProductColumn { get; private set; }
        public DataGridViewTextBoxColumn MaxHeightColumn { get; private set; }
        public DataGridViewTextBoxColumn AverageMassErrorColumn { get; private set; }
        public DataGridViewTextBoxColumn CountTruncatedColumn { get; private set; }
        public DataGridViewTextBoxColumn IdentifiedColumn { get; private set; }
        public DataGridViewTextBoxColumn UserSetTotalColumn { get; private set; }
        public DataGridViewTextBoxColumn LibraryDotProductColumn { get; private set; }
        public DataGridViewTextBoxColumn IsotopeDotProductColumn { get; private set; }
        public DataGridViewTextBoxColumn OptCollisionEnergyColumn { get; private set; }
        public DataGridViewTextBoxColumn OptDeclusteringPotentialColumn { get; private set; }
        public DataGridViewTextBoxColumn OptCompensationVoltageColumn { get; private set; }
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
        public DataGridViewTextBoxColumn MassErrorColumn { get; private set; }
        public DataGridViewTextBoxColumn TruncatedColumn { get; private set; }
        public DataGridViewTextBoxColumn PeakRankColumn { get; private set; }
        public DataGridViewTextBoxColumn UserSetColumn { get; private set; }
        public DataGridViewTextBoxColumn TransitionNoteColumn { get; private set; }

        private SrmDocument Document { get; set; }
        private List<IdentityPath> SelectedPaths { get; set; }
        private TransitionDocNode SelectedTransitionDocNode { get; set; }
        private TransitionGroupDocNode SelectedTransitionGroupDocNode { get; set; }
        private PeptideDocNode SelectedPeptideDocNode { get; set; }
        
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
                return string.Format("{0}, {1}, {2}", ReplicateIndex, FileIndex, OptimizationStep); // Not L10N
            }
        }
    }
}
