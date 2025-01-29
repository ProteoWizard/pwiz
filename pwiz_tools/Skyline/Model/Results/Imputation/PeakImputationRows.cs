﻿/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
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
using System.Linq;
using System.Runtime.CompilerServices;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Results.Scoring;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class PeakImputationRows
    {
        public static readonly Producer<Parameters, PeakImputationRows> PRODUCER = new Producer();

        public PeakImputationRows(AlignmentData alignmentData, IEnumerable<MoleculePeaks> moleculePeaksList)
        {
            AlignmentData = alignmentData;
            MoleculePeaks = ImmutableList.ValueOf(moleculePeaksList);
        }

        public ImmutableList<MoleculePeaks> MoleculePeaks { get; }

        public AlignmentData AlignmentData { get; }

        public class Parameters : Immutable
        {
            public Parameters(SrmDocument document)
            {
                Document = document;
            }

            public SrmDocument Document { get; }

            public bool OverwriteManualPeaks { get; private set; }

            public Parameters ChangeOverwriteManualPeaks(bool value)
            {
                return ChangeProp(ImClone(this), im => im.OverwriteManualPeaks = value);
            }

            public ImmutableList<IdentityPath> PeptideIdentityPaths { get; private set; }

            public Parameters ChangePeptideIdentityPaths(ImmutableList<IdentityPath> value)
            {
                return ChangeProp(ImClone(this), im => im.PeptideIdentityPaths = value);
            }
        }

        private class Producer : Producer<Parameters, PeakImputationRows>
        {
            public override PeakImputationRows ProduceResult(ProductionMonitor productionMonitor, Parameters parameter, IDictionary<WorkOrder, object> inputs)
            {
                var peakImputationData = (AlignmentData) inputs.Values.First();
                var rowProducer = new RowProducer(productionMonitor, parameter, peakImputationData);
                return new PeakImputationRows(peakImputationData, rowProducer.GetRows());
            }

            public override IEnumerable<WorkOrder> GetInputs(Parameters parameter)
            {
                yield return AlignmentData.PRODUCER.MakeWorkOrder(
                    new AlignmentData.Parameters(parameter.Document));
            }

            public override string GetDescription(object workParameter)
            {
                return ImputationResources.Producer_GetDescription_Peak_Imputation_Results;
            }
        }

        private class RowProducer
        {
            public RowProducer(ProductionMonitor productionMonitor, Parameters parameters, AlignmentData alignmentData)
            {
                ProductionMonitor = productionMonitor;
                Parameters = parameters;
                AlignmentData = alignmentData;
            }

            public ProductionMonitor ProductionMonitor { get; }
            public Parameters Parameters { get; }
            public AlignmentData AlignmentData { get; }
            private MoleculePeaks RatePeaks(MoleculePeaks moleculePeaks)
            {
                var peaks = new List<RatedPeak>(MarkExemplaryPeaks(moleculePeaks.Peaks));
                var exemplaryPeaks = peaks.Where(peak => peak.PeakVerdict == RatedPeak.Verdict.Exemplary).ToList();
                if (exemplaryPeaks.Count == 0)
                {
                    return moleculePeaks.ChangePeaks(peaks.Select(peak => peak.ChangeVerdict(RatedPeak.Verdict.Unknown, ImputationResources.RowProducer_RatePeaks_No_exemplary_peaks)), null, null);
                }

                var bestPeak = exemplaryPeaks.First();
                var exemplaryPeakBounds = GetExemplaryPeakBounds(exemplaryPeaks);
                var peptideDocNode = (PeptideDocNode)Parameters.Document.FindNode(moleculePeaks.PeptideIdentityPath);
                peaks = peaks.Select(peak => MarkAcceptedPeak(peptideDocNode, exemplaryPeakBounds.ReverseAlignPreservingWidth(peak.AlignmentFunction), peak)).ToList();
                var peaksByFile = peaks.ToLookup(peak => peak.ReplicateFileInfo.ReplicateFileId);
                var peaksInOriginalOrder = moleculePeaks.Peaks.GroupBy(peak => peak.ReplicateFileInfo.ReplicateFileId)
                    .SelectMany(group => peaksByFile[group.Key]);
                return moleculePeaks.ChangePeaks(peaksInOriginalOrder, bestPeak, exemplaryPeakBounds);
            }
            private IEnumerable<RatedPeak> MarkExemplaryPeaks(IList<RatedPeak> peaks)
            {
                bool firstExemplary = true;
                IEnumerable<RatedPeak> orderedPeaks;
                orderedPeaks = peaks.OrderByDescending(peak => peak.Score);
                foreach (var peak in orderedPeaks)
                {
                    if (peak.AlignedPeakBounds == null || !peak.Score.HasValue)
                    {
                        yield return peak;
                        continue;
                    }

                    if (firstExemplary)
                    {
                        firstExemplary = false;
                        string opinion = string.Format(ImputationResources.RowProducer_MarkExemplaryPeaks_Peak_score__0__is_higher_than_all_other_replicates,
                            peak.Score?.ToString(Formats.PEAK_SCORE));
                        yield return peak.ChangeVerdict(RatedPeak.Verdict.Exemplary, opinion);
                        continue;
                    }

                    yield return peak;
                }
            }
            private FormattablePeakBounds GetExemplaryPeakBounds(IList<RatedPeak> exemplaryPeaks)
            {
                if (exemplaryPeaks.Count == 0)
                {
                    return null;
                }

                var alignedPeakBounds =
                    exemplaryPeaks.Select(peak => peak.RawPeakBounds.AlignPreservingWidth(peak.AlignmentFunction)).ToList();
                if (alignedPeakBounds.Count == 0)
                {
                    return null;
                }

                return new FormattablePeakBounds(alignedPeakBounds.Average(peak => peak.StartTime),
                    alignedPeakBounds.Average(peak => peak.EndTime));
            }
            [MethodImpl(MethodImplOptions.NoOptimization)]
            private RatedPeak MarkAcceptedPeak(PeptideDocNode peptideDocNode, FormattablePeakBounds exemplaryPeakBounds, RatedPeak peak)
            {
                if (exemplaryPeakBounds == null)
                {
                    return peak.ChangeVerdict(RatedPeak.Verdict.Accepted,
                        ImputationResources.RowProducer_MarkAcceptedPeak_Adjustment_not_possible_because_no_exemplary_peaks_found_);
                }

                peak = peak.ChangeRtShift(peak.RawPeakBounds?.MidTime - exemplaryPeakBounds.MidTime);
                if (peak.PeakVerdict != RatedPeak.Verdict.Unknown)
                {
                    return peak;
                }

                bool needsMoving = !peak.RtShift.HasValue || Math.Abs(peak.RtShift.Value) > AllowableRtShift;
                bool needsWidthChanged =
                    !needsMoving && MaxPeakWidthVariation.HasValue &&
                    Math.Abs(exemplaryPeakBounds.Width - peak.RawPeakBounds.Width) >
                    MaxPeakWidthVariation * exemplaryPeakBounds.Width;
                if (!needsMoving && !needsWidthChanged)
                {
                    return peak.ChangeVerdict(RatedPeak.Verdict.Accepted,
                        string.Format(ImputationResources.RowProducer_MarkAcceptedPeak_Retention_time__0__is_within__1__of__2_, peak.RawPeakBounds.MidTime.ToString(Formats.RETENTION_TIME),
                            AllowableRtShift, exemplaryPeakBounds.MidTime.ToString(Formats.RETENTION_TIME)));
                }

                if (false == peak.TimeIntervals?.ContainsTime((float)exemplaryPeakBounds.MidTime))
                {
                    var opinion = string.Format(ImputationResources.RowProducer_MarkAcceptedPeak_Imputed_retention_time__0__is_outside_the_chromatogram_, exemplaryPeakBounds.MidTime.ToString(Formats.RETENTION_TIME));
                    if (peak.RawPeakBounds == null)
                    {
                        return peak.ChangeVerdict(RatedPeak.Verdict.Accepted, opinion);
                    }

                    return peak.ChangeVerdict(RatedPeak.Verdict.NeedsRemoval, opinion);
                }

                if (needsWidthChanged)
                {
                    var opinion = string.Format(ImputationResources.RowProducer_MarkAcceptedPeak_Width__0__should_be_changed_because_more_than__1__different_from__2_,
                        peak.RawPeakBounds.Width.ToString(Formats.RETENTION_TIME),
                        MaxPeakWidthVariation.Value.ToString(Formats.Percent),
                        exemplaryPeakBounds.Width.ToString(Formats.RETENTION_TIME));
                    return peak.ChangeVerdict(RatedPeak.Verdict.NeedsAdjustment, opinion);
                }
                return peak.ChangeVerdict(RatedPeak.Verdict.NeedsAdjustment,
                    string.Format(ImputationResources.RowProducer_MarkAcceptedPeak_Peak_should_be_moved_to__0_, exemplaryPeakBounds.MidTime.ToString(Formats.RETENTION_TIME)));
            }


            public double? AllowableRtShift
            {
                get
                {
                    return Parameters.Document.Settings.PeptideSettings.Imputation.MaxRtShift;
                }
            }

            public double? MaxPeakWidthVariation
            {
                get
                {
                    return Parameters.Document.Settings.PeptideSettings.Imputation.MaxPeakWidthVariation;
                }
            }

            public IEnumerable<MoleculePeaks> GetRows()
            {
                return GetUnratedRows().Select(RatePeaks);
            }

            private IEnumerable<MoleculePeaks> GetUnratedRows()
            {
                var peptideIdentityPaths = Parameters.PeptideIdentityPaths?.ToHashSet();
                var document = Parameters.Document;
                var measuredResults = document.MeasuredResults;
                if (measuredResults == null)
                {
                    return Array.Empty<MoleculePeaks>();
                }

                var alignments = AlignmentData.Alignments;
                var chromatogramTimeRanges = AlignmentData.ChromatogramTimeRanges;
                Dictionary<Target, double> standardTimes = null;
                if (alignments?.StandardTimes != null)
                {
                    standardTimes = CollectionUtil.SafeToDictionary(alignments.StandardTimes);
                }

                var resultFileInfos = ReplicateFileInfo.List(document.MeasuredResults);
                var resultFileInfoDict =
                    resultFileInfos.ToDictionary(resultFileInfo =>
                        ReferenceValue.Of(resultFileInfo.ReplicateFileId.FileId));
                var molecules = document.MoleculeGroups.SelectMany(moleculeGroup =>
                    moleculeGroup.Molecules.Where(mol => mol.GlobalStandardType == null && mol.Children.Count != 0)
                        .Select(mol => Tuple.Create(moleculeGroup, mol))).ToList();
                if (peptideIdentityPaths != null)
                {
                    molecules = molecules.Where(tuple =>
                            peptideIdentityPaths.Contains(new IdentityPath(tuple.Item1.PeptideGroup,
                                tuple.Item2.Peptide)))
                        .ToList();
                }

                var scoringModel = document.Settings.PeptideSettings.Integration.PeakScoringModel;
                if (true != scoringModel.IsTrained)
                {
                    scoringModel = LegacyScoringModel.DEFAULT_MODEL;
                }

                var moleculePeaksArray = new MoleculePeaks[molecules.Count];
                int progressCount = 0;
                ParallelEx.For(0, molecules.Count, iMolecule =>
                {
                    if (ProductionMonitor.CancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    lock (moleculePeaksArray)
                    {
                        progressCount++;
                        ProductionMonitor.SetProgress(progressCount * 100 / molecules.Count);
                    }

                    var moleculeGroup = molecules[iMolecule].Item1;
                    var molecule = molecules[iMolecule].Item2;
                    var timeRanges = chromatogramTimeRanges?.GetTimeRanges(molecule);

                    var peptideIdentityPath = new IdentityPath(moleculeGroup.PeptideGroup, molecule.Peptide);
                    var peaks = new List<RatedPeak>();
                    for (int replicateIndex = 0; replicateIndex < measuredResults.Chromatograms.Count; replicateIndex++)
                    {
                        foreach (var peptideChromInfo in molecule.GetSafeChromInfo(replicateIndex))
                        {
                            ProductionMonitor.CancellationToken.ThrowIfCancellationRequested();
                            if (!resultFileInfoDict.TryGetValue(peptideChromInfo.FileId, out var peakResultFile))
                            {
                                // Shouldn't happen
                                continue;
                            }

                            var chromFileInfo = document.MeasuredResults.Chromatograms[replicateIndex]
                                .GetFileInfo(peptideChromInfo.FileId);
                            PeptideDocNode scoredMolecule = molecule;
                            bool manuallyIntegrated =
                                AlignmentData.IsManualIntegrated(molecule, replicateIndex, peptideChromInfo.FileId);
                            if (manuallyIntegrated)
                            {
                                if (!Parameters.OverwriteManualPeaks)
                                {
                                    continue;
                                }
                            }

                            var rawPeakBounds = AlignmentData.GetRawPeakBounds(scoredMolecule,
                                replicateIndex,
                                peptideChromInfo.FileId);
                            var onDemandFeatureCalculator = new OnDemandFeatureCalculator(
                                scoringModel.PeakFeatureCalculators, document.Settings,
                                molecule, replicateIndex, chromFileInfo);
                            var candidatePeakGroups = molecule.TransitionGroups.SelectMany(tg =>
                                onDemandFeatureCalculator.GetCandidatePeakGroups(tg.TransitionGroup)).ToList();
                            CandidatePeakGroupData matchingPeakGroup = null;
                            if (manuallyIntegrated)
                            {
                                matchingPeakGroup = candidatePeakGroups
                                    .OrderByDescending(group => group.Score.ModelScore).FirstOrDefault();
                            }
                            else
                            {
                                if (rawPeakBounds != null)
                                {
                                    matchingPeakGroup = candidatePeakGroups.FirstOrDefault(peakGroupData =>
                                        peakGroupData.MinStartTime == rawPeakBounds.StartTime &&
                                        peakGroupData.MaxEndTime == rawPeakBounds.EndTime);
                                }

                                if (matchingPeakGroup == null)
                                {
                                    matchingPeakGroup =
                                        onDemandFeatureCalculator.GetChosenPeakGroupData(molecule.TransitionGroups
                                            .First().TransitionGroup);
                                }
                            }

                            double? score = matchingPeakGroup?.Score.ModelScore;
                            var timeIntervals = timeRanges?.GetTimeIntervals(peakResultFile.MsDataFileUri);
                            var peak = new RatedPeak(peakResultFile,
                                alignments?.GetAlignment(peakResultFile.ReplicateFileId), timeIntervals, rawPeakBounds,
                                score,
                                manuallyIntegrated);
                            peaks.Add(peak);
                        }
                    }

                    var moleculePeaks = new MoleculePeaks(peptideIdentityPath, peaks);
                    if (true == standardTimes?.TryGetValue(molecule.ModifiedTarget, out var standardTime))
                    {
                        moleculePeaks = moleculePeaks.ChangeAlignmentStandardTime(standardTime);
                    }

                    moleculePeaksArray[iMolecule] = moleculePeaks;
                });
                return moleculePeaksArray.Where(p => p != null);
            }
        }
    }
}
