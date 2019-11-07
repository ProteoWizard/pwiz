/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataAnalysis;
using pwiz.Common.DataAnalysis.FoldChange;
using pwiz.Common.DataAnalysis.Matrices;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.Hibernate;

namespace pwiz.Skyline.Model.GroupComparison
{
    public class GroupComparer 
    {
        private readonly IList<KeyValuePair<int, ReplicateDetails>> _replicateIndexes;
        private QrFactorizationCache _qrFactorizationCache;
        private NormalizationData _normalizationData;
        public GroupComparer(GroupComparisonDef comparisonDef, SrmDocument document, QrFactorizationCache qrFactorizationCache)
        {
            SrmDocument = document;
            ComparisonDef = comparisonDef;
            var annotationCalculator = new AnnotationCalculator(document);
            _qrFactorizationCache = qrFactorizationCache;
            List<KeyValuePair<int, ReplicateDetails>> replicateIndexes = new List<KeyValuePair<int, ReplicateDetails>>();
            var controlGroupIdentifier = ComparisonDef.GetControlGroupIdentifier(SrmDocument.Settings);
            if (SrmDocument.Settings.HasResults)
            {
                var chromatograms = SrmDocument.Settings.MeasuredResults.Chromatograms;
                for (int i = 0; i < chromatograms.Count; i++)
                {
                    var chromatogramSet = chromatograms[i];
                    ReplicateDetails replicateDetails = new ReplicateDetails()
                    {
                        GroupIdentifier = comparisonDef.GetGroupIdentifier(annotationCalculator, chromatogramSet)
                    };
                    if (Equals(controlGroupIdentifier, replicateDetails.GroupIdentifier))
                    {
                        replicateDetails.IsControl = true;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(ComparisonDef.CaseValue))
                        {
                            var annotationValue = chromatogramSet.Annotations.GetAnnotation(ComparisonDef.ControlAnnotation);
                            if (!Equals(annotationValue, ComparisonDef.CaseValue))
                            {
                                continue;
                            }
                        }
                    }
                    if (null != ComparisonDef.IdentityAnnotation)
                    {
                        replicateDetails.BioReplicate =
                            chromatogramSet.Annotations.GetAnnotation(ComparisonDef.IdentityAnnotation);
                    }
                    replicateIndexes.Add(new KeyValuePair<int, ReplicateDetails>(i, replicateDetails));
                }
            }
            _replicateIndexes = ImmutableList.ValueOf(replicateIndexes);
            IsValid = _replicateIndexes.Any(keyValuePair => keyValuePair.Value.IsControl) &&
                      _replicateIndexes.Any(keyValuePair => !keyValuePair.Value.IsControl);
        }
        public GroupComparisonDef ComparisonDef { get; private set; }
        public SrmDocument SrmDocument { get; private set; }

        public bool IsValid { get; private set; }

        public IList<IsotopeLabelType> ListLabelTypes(PeptideGroupDocNode protein, PeptideDocNode peptideDocNode)
        {
            var labelTypes = new HashSet<IsotopeLabelType>();
            var peptides = peptideDocNode == null ? protein.Molecules : new[] {peptideDocNode};
            foreach (var peptide in peptides)
            {
                foreach (var precursor in peptide.TransitionGroups)
                {
                    if (NormalizationMethod.RatioToLabel.Matches(ComparisonDef.NormalizationMethod, precursor.TransitionGroup.LabelType))
                    {
                        continue;
                    }
                    labelTypes.Add(precursor.TransitionGroup.LabelType);
                }
            }
            var result = labelTypes.ToArray();
            Array.Sort(result);
            return result;
        }

        public IList<GroupIdentifier> ListGroupsToCompareTo()
        {
            var groupIdentifiers = _replicateIndexes.Select(entry => entry.Value.GroupIdentifier)
                .Distinct().ToArray();
            Array.Sort(groupIdentifiers);
            return groupIdentifiers;
        }

        public List<GroupComparisonResult> CalculateFoldChanges(PeptideGroupDocNode protein, PeptideDocNode peptide)
        {
            var result = new List<GroupComparisonResult>();
            var groupsToCompareTo = ListGroupsToCompareTo();
            foreach (var labelType in ListLabelTypes(protein, peptide))
            {
                for (int msLevel = 1; msLevel <= 2; msLevel++)
                {
                    foreach (var group in groupsToCompareTo)
                    {
                        var groupComparisonResult = CalculateFoldChange(new GroupComparisonSelector(protein, peptide, labelType, msLevel, group), null);
                        if (null != groupComparisonResult)
                        {
                            result.Add(groupComparisonResult);
                        }
                    }
                }
            }
            return result;
        }

        public GroupComparisonResult CalculateFoldChange(GroupComparisonSelector selector, List<RunAbundance> runAbundances)
        {
            if (Equals(ComparisonDef.SummarizationMethod, SummarizationMethod.REGRESSION))
            {
                return CalculateFoldChangeUsingRegression(selector, runAbundances);
            }
            if (Equals(ComparisonDef.SummarizationMethod, SummarizationMethod.MEDIANPOLISH))
            {
                return CalculateFoldChangeWithSummarization(selector, runAbundances, SummarizeDataRowsWithMedianPolish);
            }
            return CalculateFoldChangeWithSummarization(selector, runAbundances, SummarizeDataRowsByAveraging);
        }

        private GroupComparisonResult CalculateFoldChangeUsingRegression(
            GroupComparisonSelector selector, List<RunAbundance> runAbundances)
        {
            var detailRows = new List<DataRowDetails>();
            GetDataRows(selector, detailRows);
            if (detailRows.Count == 0)
            {
                return null;
            }
            var foldChangeDataRows = detailRows
                .Where(row=>!double.IsNaN(row.GetLog2Abundance()) && !double.IsInfinity(row.GetLog2Abundance()))
                .Select(row => new FoldChangeCalculator.DataRow
            {
                Abundance = row.GetLog2Abundance(),
                Control = row.Control,
                Feature = row.IdentityPath,
                Run = row.ReplicateIndex,
                Subject = row.BioReplicate,
            }).ToArray();
            FoldChangeDataSet runQuantificationDataSet = FoldChangeCalculator.MakeDataSet(foldChangeDataRows);
            var runNumberToReplicateIndex = FoldChangeCalculator.GetUniqueList(foldChangeDataRows.Select(row => row.Run));
            var runQuantificationDesignMatrix = DesignMatrix.GetRunQuantificationDesignMatrix(runQuantificationDataSet);
            var quantifiedRuns = runQuantificationDesignMatrix.PerformLinearFit(_qrFactorizationCache);
            var subjects = new List<int>();

            for (int run = 0; run < quantifiedRuns.Count; run++)
            {
                int iRow = runQuantificationDataSet.Runs.IndexOf(run);
                subjects.Add(runQuantificationDataSet.Subjects[iRow]);
                if (null != runAbundances)
                {
                    var replicateIndex = runNumberToReplicateIndex[run];
                    var replicateDetails = _replicateIndexes.First(kvp => kvp.Key == replicateIndex).Value;

                    runAbundances.Add(new RunAbundance
                    {
                        ReplicateIndex = replicateIndex,
                        Control = replicateDetails.IsControl,
                        BioReplicate = replicateDetails.BioReplicate,
                        Log2Abundance = quantifiedRuns[run].EstimatedValue
                    });
                }
            }
            var abundances = quantifiedRuns.Select(result => result.EstimatedValue).ToArray();
            var quantifiedDataSet = new FoldChangeDataSet(
                abundances,
                Enumerable.Repeat(0, quantifiedRuns.Count).ToArray(),
                Enumerable.Range(0, quantifiedRuns.Count).ToArray(),
                subjects,
                runQuantificationDataSet.SubjectControls);
            if (quantifiedDataSet.SubjectControls.Distinct().Count() < 2)
            {
                return null;
            }

            var foldChangeResult = DesignMatrix.GetDesignMatrix(quantifiedDataSet, false).PerformLinearFit(_qrFactorizationCache).First();
            return new GroupComparisonResult(selector, quantifiedRuns.Count, foldChangeResult);
        }

        private GroupComparisonResult CalculateFoldChangeWithSummarization(GroupComparisonSelector selector,
            List<RunAbundance> runAbundances, Func<IList<DataRowDetails>, IList<RunAbundance>> summarizationFunction)
        {
            var detailRows = new List<DataRowDetails>();
            GetDataRows(selector, detailRows);
            if (detailRows.Count == 0)
            {
                return null;
            }
            var replicateRows = summarizationFunction(detailRows);
            if (replicateRows.Count == 0)
            {
                return null;
            }
            if (null != runAbundances)
            {
                runAbundances.AddRange(replicateRows);
            }

            var summarizedRows = replicateRows;
            if (replicateRows.Any(row => null != row.BioReplicate))
            {
                var groupedByBioReplicate = replicateRows.ToLookup(
                    row => new KeyValuePair<string, bool>(row.BioReplicate, row.Control));
                summarizedRows = groupedByBioReplicate.Select(
                    grouping =>
                    {
                        return new RunAbundance()
                        {
                            BioReplicate = grouping.Key.Key,
                            Control = grouping.Key.Value,
                            ReplicateIndex = -1,
                            Log2Abundance = grouping.Average(row => row.Log2Abundance),
                        };
                    }).ToList();
            }

            var quantifiedDataSet = new FoldChangeDataSet(
                summarizedRows.Select(row => row.Log2Abundance).ToArray(),
                Enumerable.Repeat(0, summarizedRows.Count).ToArray(),
                Enumerable.Range(0, summarizedRows.Count).ToArray(),
                Enumerable.Range(0, summarizedRows.Count).ToArray(),
                summarizedRows.Select(row => row.Control).ToArray());

            if (quantifiedDataSet.SubjectControls.Distinct().Count() < 2)
            {
                return null;
            }
            var designMatrix = DesignMatrix.GetDesignMatrix(quantifiedDataSet, false);
            var foldChangeResult = designMatrix.PerformLinearFit(_qrFactorizationCache).First();
            // Note that because the design matrix has only two columns, this is equivalent to a simple linear
            // regression
            //            var statsAbundances = new Util.Statistics(summarizedRows.Select(row => row.Log2Abundance));
            //            var statsXValues = new Util.Statistics(summarizedRows.Select(row => row.Control ? 0.0 : 1));
            //            var slope = statsAbundances.Slope(statsXValues);

            return new GroupComparisonResult(selector, replicateRows.Count, foldChangeResult);
            
        }

        private IList<RunAbundance> SummarizeDataRowsByAveraging(IList<DataRowDetails> dataRows)
        {
            var result = new List<RunAbundance>();
            foreach (var grouping in RemoveIncompleteReplicates(dataRows))
            {
                var log2Abundance = Math.Log(grouping.Sum(row => row.Intensity) / grouping.Sum(row=>row.Denominator), 2.0);
                result.Add(new RunAbundance
                {
                    Control = grouping.First().Control,
                    BioReplicate = grouping.First().BioReplicate,
                    Log2Abundance = log2Abundance,
                    ReplicateIndex = grouping.Key
                });
            }
            return result;
        }

        private IList<RunAbundance> SummarizeDataRowsWithMedianPolish(IList<DataRowDetails> dataRows)
        {
            IList<IGrouping<IdentityPath, DataRowDetails>> dataRowsByFeature = 
                SumMs1Transitions(dataRows).ToLookup(row => row.IdentityPath)
                .Where(IncludeFeatureForMedianPolish)
                .ToArray();
#pragma warning disable 162
            // ReSharper disable HeuristicUnreachableCode
            if (false)
            {
                // For debugging purposes, we might want to order these features in the same order as MSstats R code
                Array.Sort((IGrouping<IdentityPath, DataRowDetails>[])dataRowsByFeature, (group1, group2) =>
                {
                    String s1 = GetFeatureKey(SrmDocument, group1.Key);
                    String s2 = GetFeatureKey(SrmDocument, group2.Key);
                    return String.Compare(s1.ToLowerInvariant(), s2.ToLowerInvariant(), StringComparison.Ordinal);
                });
            }
            // ReSharper restore HeuristicUnreachableCode
#pragma warning restore 162

            var featureCount = dataRowsByFeature.Count;
            if (featureCount == 0)
            {
                return ImmutableList.Empty<RunAbundance>();
            }
            var rows = new List<double?[]>();
            IDictionary<int, int> replicateRowIndexes = new Dictionary<int, int>();
            for (int iFeature = 0; iFeature < dataRowsByFeature.Count; iFeature++)
            {
                foreach (DataRowDetails dataRowDetails in dataRowsByFeature[iFeature])
                {
                    int rowIndex;
                    if (!replicateRowIndexes.TryGetValue(dataRowDetails.ReplicateIndex, out rowIndex))
                    {
                        rowIndex = rows.Count;
                        rows.Add(new double?[featureCount]);
                        replicateRowIndexes.Add(dataRowDetails.ReplicateIndex, rowIndex);
                    }
                    var row = rows[rowIndex];
                    row[iFeature] = dataRowDetails.GetLog2Abundance();
                }
            }
            var matrix = new double?[rows.Count, featureCount];
            for (int iRow = 0; iRow < rows.Count; iRow++)
            {
                for (int iCol = 0; iCol < featureCount; iCol++)
                {
                    matrix[iRow, iCol] = rows[iRow][iCol];
                }
            }
            var medianPolish = MedianPolish.GetMedianPolish(matrix);
            List<RunAbundance> runAbundances = new List<RunAbundance>();
            foreach (var replicateIndexDetails in _replicateIndexes)
            {
                int rowIndex;
                if (!replicateRowIndexes.TryGetValue(replicateIndexDetails.Key, out rowIndex))
                {
                    continue;
                }
                double value = medianPolish.OverallConstant + medianPolish.RowEffects[rowIndex];
                runAbundances.Add(new RunAbundance()
                {
                    ReplicateIndex = replicateIndexDetails.Key,
                    BioReplicate = replicateIndexDetails.Value.BioReplicate,
                    Control = replicateIndexDetails.Value.IsControl,
                    Log2Abundance = value
                });
            }
            return runAbundances;
        }

        private IList<DataRowDetails> SumMs1Transitions(IList<DataRowDetails> dataRows)
        {
            var dataRowsByReplicateIndexAndTransitionGroup =
                dataRows.ToLookup(row =>
                    new Tuple<int, IdentityPath>(row.ReplicateIndex,
                        row.IdentityPath.GetPathTo((int)SrmDocument.Level.TransitionGroups)));
            var newDataRows = new List<DataRowDetails>();
            foreach (var grouping in dataRowsByReplicateIndexAndTransitionGroup)
            {
                DataRowDetails ms1DataRow = null;
                var transitionGroup = (TransitionGroupDocNode) SrmDocument.FindNode(grouping.Key.Item2);
                foreach (var dataRow in grouping)
                {
                    var transition = (TransitionDocNode) transitionGroup.FindNode(dataRow.IdentityPath.Child);
                    if (transition.IsMs1)
                    {
                        if (ms1DataRow == null)
                        {
                            ms1DataRow = new DataRowDetails()
                            {
                                IdentityPath = grouping.Key.Item2,
                                BioReplicate = dataRow.BioReplicate,
                                Control = dataRow.Control,
                                ReplicateIndex = dataRow.ReplicateIndex
                            };
                        }
                        ms1DataRow.Intensity += dataRow.Intensity;
                        ms1DataRow.Denominator += dataRow.Denominator;
                    }
                    else
                    {
                        newDataRows.Add(dataRow);
                    }
                }
                if (ms1DataRow != null)
                {
                    newDataRows.Add(ms1DataRow);
                }
            }
            return newDataRows;
        }

        /// <summary>
        /// Given the set of data rows for a given feature, should this be included in the calculations.
        /// Requirements:
        /// A) There are at least two values that are greater than 1.
        /// This is almost the same as what MSstats does, but might be slightly different with median
        /// normalization.
        /// </summary>
        private bool IncludeFeatureForMedianPolish(IGrouping<IdentityPath, DataRowDetails> grouping)
        {
            return grouping.Count(dataRow => dataRow.Intensity > 1) > 1;
        }

        private IEnumerable<IGrouping<int, DataRowDetails>> RemoveIncompleteReplicates(IList<DataRowDetails> dataRows)
        {
            var rowsByReplicateIndex = dataRows.ToLookup(row => row.ReplicateIndex);
            if (ComparisonDef.NormalizationMethod is NormalizationMethod.RatioToLabel)
            {
                return rowsByReplicateIndex;
            }
            var allIdentityPaths = new HashSet<IdentityPath>();
            allIdentityPaths.UnionWith(dataRows.Select(row => row.IdentityPath));
            return rowsByReplicateIndex.Where(
                grouping => allIdentityPaths.SetEquals(grouping.Select(row => row.IdentityPath)));
        }

        private void GetDataRows(GroupComparisonSelector selector, IList<DataRowDetails> foldChangeDetails)
        {
            foreach (var replicateEntry in _replicateIndexes)
            {
                if (!replicateEntry.Value.IsControl &&
                    !Equals(selector.GroupIdentifier, replicateEntry.Value.GroupIdentifier))
                {
                    continue;
                }
                foreach (var peptide in selector.ListPeptides())
                {
                    QuantificationSettings quantificationSettings = QuantificationSettings.DEFAULT
                        .ChangeNormalizationMethod(ComparisonDef.NormalizationMethod)
                        .ChangeMsLevel(selector.MsLevel);
                    var peptideQuantifier = new PeptideQuantifier(GetNormalizationData, selector.Protein, peptide,
                        quantificationSettings)
                    {
                        QValueCutoff = ComparisonDef.QValueCutoff
                    };
                    if (null != selector.LabelType)
                    {
                        peptideQuantifier.MeasuredLabelTypes = ImmutableList.Singleton(selector.LabelType);
                    }
                    foreach (var quantityEntry in peptideQuantifier.GetTransitionIntensities(SrmDocument.Settings, 
                                replicateEntry.Key, ComparisonDef.UseZeroForMissingPeaks))
                    {
                        var dataRowDetails = new DataRowDetails
                        {
                            BioReplicate = replicateEntry.Value.BioReplicate,
                            Control = replicateEntry.Value.IsControl,
                            IdentityPath = quantityEntry.Key,
                            Intensity = Math.Max(1.0, quantityEntry.Value.Intensity),
                            Denominator = Math.Max(1.0, quantityEntry.Value.Denominator),
                            ReplicateIndex = replicateEntry.Key,
                        };
                        foldChangeDetails.Add(dataRowDetails);
                    }
                }
            }
        }

        public NormalizationData GetNormalizationData()
        {
            if (_normalizationData == null)
            {
                _normalizationData = NormalizationData.GetNormalizationData(SrmDocument, ComparisonDef.UseZeroForMissingPeaks, ComparisonDef.QValueCutoff);
            }
            return _normalizationData;
        }

        public struct RunAbundance
        {
            public int ReplicateIndex { get; set; }
            public bool Control { get; set; }
            public string BioReplicate { get; set; }
            public double Log2Abundance { get; set; }
        }
       
        private class DataRowDetails
        {
            public IdentityPath IdentityPath { get; set; }
            public int ReplicateIndex { get; set; }
            public bool Control { get; set; }
            public string BioReplicate { get; set; }
            public double Intensity;
            public double Denominator;

            public double GetLog2Abundance()
            {
                return Math.Log(Intensity/Denominator)/Math.Log(2.0);
            }
        }

        private struct ReplicateDetails
        {
            public bool IsControl { get; set; }
            public string BioReplicate { get; set; }
            public GroupIdentifier GroupIdentifier { get; set; }
        }

        private abstract class FoldChangeCalculator : FoldChangeCalculator<int, IdentityPath, string>
        {
        }

        /// <summary>
        /// Returns the string that MSstats code uses to identify a row of data in the MSstats Input report.
        /// </summary>
        private static string GetFeatureKey(SrmDocument document, IdentityPath identityPath)
        {
            PeptideGroupDocNode peptideGroup = (PeptideGroupDocNode) document.FindNode(identityPath.GetIdentity(0));
            PeptideDocNode peptide = (PeptideDocNode) peptideGroup.FindNode(identityPath.GetIdentity(1));
            TransitionGroupDocNode transitionGroup =
                (TransitionGroupDocNode) peptide.FindNode(identityPath.GetIdentity(2));
            TransitionDocNode transition = (TransitionDocNode) transitionGroup.FindNode(identityPath.GetIdentity(3));
            return peptide.ModifiedSequenceDisplay + '_' + transitionGroup.PrecursorCharge + '_' + GetFragmentIon(transition) +
                   '_' + transition.Transition.Charge;
        }

        private static string GetFragmentIon(TransitionDocNode transitionDocNode)
        {
            string fragmentIon = transitionDocNode.GetFragmentIonName(CultureInfo.InvariantCulture);
            if (transitionDocNode.Transition.IonType == IonType.precursor)
            {
                fragmentIon += Transition.GetMassIndexText(transitionDocNode.Transition.MassIndex);
            }
            return fragmentIon;
        }

        public static String MatrixToString(double?[,] matrix)
        {
            return string.Join(Environment.NewLine, Enumerable.Range(0, matrix.GetLength(0)).Select(iRow =>
            {
                return string.Join(@",",
                    Enumerable.Range(0, matrix.GetLength(1)).Select(iCol =>
                {
                    var value = matrix[iRow, iCol];
                    if (!value.HasValue)
                    {
                        return string.Empty;
                    }
                    return value.Value.ToString(Formats.RoundTrip, CultureInfo.InvariantCulture);
                }));
            }));
        }

    }
}
