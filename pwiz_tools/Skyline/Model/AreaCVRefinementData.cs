﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using pwiz.Common.Collections;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{
    public class AreaCVRefinementData
    {
        private readonly AreaCVRefinementSettings _settings;
        private IList<InternalData> _internalData;

        public IList<CVData> Data { get; private set; }

        public double CalcMedianCV()
        {
            return new Statistics(_internalData.Select(d => d.CV)).Median();
        }

        public AreaCVRefinementData(NormalizedValueCalculator normalizedValueCalculator, AreaCVRefinementSettings settings,
            CancellationToken token, SrmSettingsChangeMonitor progressMonitor = null)
        {
            _settings = settings;
            var document = normalizedValueCalculator.Document;
            if (document == null || !document.Settings.HasResults)
                return;
            var replicates = document.MeasuredResults.Chromatograms.Count;
            var areas = new List<AreaInfo>(replicates);
            var annotations = AnnotationHelper.GetPossibleAnnotations(document, settings.Group).ToArray();
            if (!annotations.Any() && settings.Group == null)
                annotations = new string[] { null };

            _internalData = new List<InternalData>();
            var hasHeavyMods = document.Settings.PeptideSettings.Modifications.HasHeavyModifications;
            var hasGlobalStandards = document.Settings.HasGlobalStandardArea;
            var ms1 = settings.MsLevel == AreaCVMsLevel.precursors;
            // Avoid using not-MS1 with a document that is only MS1
            if (!ms1 && document.MoleculeTransitions.All(t => t.IsMs1))
                ms1 = true;
            double? qvalueCutoff = null;
            if (ShouldUseQValues(document))
                qvalueCutoff = _settings.QValueCutoff;

            int? minDetections = null;
            if (_settings.MinimumDetections != -1)
                minDetections = _settings.MinimumDetections;

            MedianInfo medianInfo = null;
            if (_settings.NormalizeOption.Is(NormalizationMethod.EQUALIZE_MEDIANS))
                medianInfo = CalculateMedianAreas(document);

            foreach (var peptideGroup in document.MoleculeGroups)
            {
                foreach (var peptide in peptideGroup.Molecules)
                {
                    progressMonitor?.ProcessMolecule(peptide);

                    if (_settings.PointsType == PointsTypePeakArea.decoys != peptide.IsDecoy)
                        continue;

                    CalibrationCurveFitter calibrationCurveFitter = null;
                    CalibrationCurve calibrationCurve = null;
                    IEnumerable<TransitionGroupDocNode> transitionGroups;
                    if (_settings.NormalizeOption == NormalizeOption.CALIBRATED ||
                        _settings.NormalizeOption == NormalizeOption.DEFAULT)
                    {
                        if (!peptide.TransitionGroups.Any())
                        {
                            continue;
                        }

                        calibrationCurveFitter =
                            CalibrationCurveFitter.GetCalibrationCurveFitter(document, peptideGroup, peptide);
                        transitionGroups = new[] {peptide.TransitionGroups.First()};
                        if (_settings.NormalizeOption == NormalizeOption.CALIBRATED)
                        {
                            calibrationCurve = calibrationCurveFitter.GetCalibrationCurve();
                            if (calibrationCurve == null)
                            {
                                continue;
                            }
                        }
                    }
                    else
                    {
                        transitionGroups = peptide.TransitionGroups;
                    }
                    foreach (var transitionGroupDocNode in transitionGroups)
                    {
                        foreach (var a in annotations)
                        {
                            areas.Clear();

                            if (!Equals(a, _settings.Annotation) && (_settings.Group == null || _settings.Annotation != null))
                                continue;

                            foreach (var replicateIndex in AnnotationHelper.GetReplicateIndices(document, _settings.Group, a))
                            {
                                if (true == progressMonitor?.IsCanceled())
                                    throw new OperationCanceledException();

                                token.ThrowIfCancellationRequested();
                                var groupChromInfo = transitionGroupDocNode.GetSafeChromInfo(replicateIndex)
                                    .FirstOrDefault(c => c.OptimizationStep == 0);
                                if (groupChromInfo == null)
                                    continue;

                                if (qvalueCutoff.HasValue)
                                {
                                    if (!(groupChromInfo.QValue.HasValue &&
                                          groupChromInfo.QValue.Value < qvalueCutoff.Value))
                                        continue;
                                }

                                double sumArea, normalizedArea;
                                if (calibrationCurveFitter != null)
                                {
                                    double? value;
                                    if (calibrationCurve != null)
                                    {
                                        value = calibrationCurveFitter.GetCalculatedConcentration(calibrationCurve,
                                            replicateIndex);
                                    }
                                    else
                                    {
                                        value = calibrationCurveFitter.GetNormalizedPeakArea(
                                            new CalibrationPoint(replicateIndex, null));
                                    }
                                    if (!value.HasValue)
                                    {
                                        continue;
                                    }

                                    sumArea = value.Value;
                                    normalizedArea = value.Value;
                                }
                                else
                                {
                                    if (!groupChromInfo.Area.HasValue)
                                        continue;
                                    var index = replicateIndex;
                                    sumArea = transitionGroupDocNode.Transitions.Where(t =>
                                    {
                                        if (ms1 != t.IsMs1 || !t.ExplicitQuantitative)
                                            return false;

                                        var chromInfo = t.GetSafeChromInfo(index)
                                            .FirstOrDefault(c => c.OptimizationStep == 0);
                                        if (chromInfo == null)
                                            return false;
                                        if (_settings.Transitions == AreaCVTransitions.best)
                                            return chromInfo.RankByLevel == 1;
                                        if (_settings.Transitions == AreaCVTransitions.all)
                                            return true;

                                        return chromInfo.RankByLevel <= _settings.CountTransitions;
                                        // ReSharper disable once PossibleNullReferenceException
                                    }).Sum(t => (double) t.GetSafeChromInfo(index)
                                        .FirstOrDefault(c => c.OptimizationStep == 0).Area);

                                    normalizedArea = sumArea;
                                    if (_settings.NormalizeOption.Is(NormalizationMethod.EQUALIZE_MEDIANS))
                                    {
                                        normalizedArea /= medianInfo.Medians[replicateIndex] / medianInfo.MedianMedian;
                                    }
                                    else if (_settings.NormalizeOption.Is(NormalizationMethod.GLOBAL_STANDARDS) &&
                                             hasGlobalStandards)
                                    {
                                        normalizedArea =
                                            NormalizeToGlobalStandard(document, transitionGroupDocNode, replicateIndex,
                                                sumArea);
                                    }
                                    else if (_settings.NormalizeOption.Is(NormalizationMethod.TIC))
                                    {
                                        var denominator = document.Settings.GetTicNormalizationDenominator(
                                            replicateIndex, groupChromInfo.FileId);
                                        if (!denominator.HasValue)
                                        {
                                            continue;
                                        }

                                        normalizedArea /= denominator.Value;
                                    }
                                    else if (hasHeavyMods &&
                                             _settings.NormalizeOption.NormalizationMethod is NormalizationMethod
                                                 .RatioToLabel ratioToLabel)
                                    {
                                        var ci = transitionGroupDocNode.GetSafeChromInfo(replicateIndex)
                                            .FirstOrDefault(c => c.OptimizationStep == 0);
                                        var ratioValue =
                                            normalizedValueCalculator.GetTransitionGroupRatioValue(ratioToLabel,
                                                peptide, transitionGroupDocNode, ci);
                                        if (ratioValue == null)
                                        {
                                            continue;
                                        }

                                        normalizedArea = ratioValue.Ratio;
                                    }
                                }
                                areas.Add(new AreaInfo(sumArea, normalizedArea));
                            }

                            if (qvalueCutoff.HasValue && minDetections.HasValue && areas.Count < minDetections.Value)
                                continue;

                            _settings.AddToInternalData(_internalData, areas, peptideGroup, peptide, transitionGroupDocNode, a);
                        }
                    }
                }
            }
            Data = ImmutableList<CVData>.ValueOf(_internalData.GroupBy(i => i, (key, grouped) =>
            {
                var groupedArray = grouped.ToArray();
                return new CVData(
                    groupedArray.Select(idata => new PeptideAnnotationPair(idata.PeptideGroup, idata.Peptide, idata.TransitionGroup, idata.Annotation, idata.CV)),
                    key.CVBucketed, key.Area, groupedArray.Length);
            }).OrderBy(d => d.CV));
        }

        public SrmDocument RemoveAboveCVCutoff(SrmDocument document)
        {
            var filterByCutoff = Data;

            if (!double.IsNaN(_settings.CVCutoff))
            {
                var cutoff = _settings.CVCutoff / 100.0;

                filterByCutoff = Data.Where(d => d.CV < cutoff).ToList();
            }

            var ids = new HashSet<int>(filterByCutoff
                .SelectMany(d => d.PeptideAnnotationPairs)
                .Select(pair => pair.TransitionGroup.Id.GlobalIndex));
            var setRemove = IndicesToRemove(document, ids);

            return (SrmDocument)document.RemoveAll(setRemove, null, (int)SrmDocument.Level.Molecules);
        }

        public static HashSet<int> IndicesToRemove(SrmDocument document, HashSet<int> ids)
        {
            var setRemove = new HashSet<int>();
            foreach (var nodeMolecule in document.Molecules)
            {
                if (nodeMolecule.GlobalStandardType != null)
                    continue;
                foreach (var nodeGroup in nodeMolecule.TransitionGroups)
                {
                    if (!ids.Contains(nodeGroup.Id.GlobalIndex) || nodeGroup.AveragePeakArea == null)
                        setRemove.Add(nodeGroup.Id.GlobalIndex);
                    foreach (var trans in nodeGroup.Transitions)
                    {
                        if (trans.AveragePeakArea == null)
                            setRemove.Add(trans.Id.GlobalIndex);
                    }
                }
            }

            return setRemove;
        }

        private MedianInfo CalculateMedianAreas(SrmDocument document)
        {
            double? qvalueCutoff = null;
            if (ShouldUseQValues(document))
                qvalueCutoff = _settings.QValueCutoff;
            var replicates = document.MeasuredResults.Chromatograms.Count;
            var allAreas = new List<List<double?>>(document.MoleculeTransitionGroupCount);

            foreach (var transitionGroupDocNode in document.MoleculeTransitionGroups)
            {
                if ((_settings.PointsType == PointsTypePeakArea.decoys) != transitionGroupDocNode.IsDecoy)
                    continue;

                var detections = 0;
                var areas = new List<double?>(replicates);

                for (var i = 0; i < replicates; ++i)
                {
                    double? area = transitionGroupDocNode.GetPeakArea(i, qvalueCutoff);
                    if (area.HasValue)
                        ++detections;
                    areas.Add(area);
                }

                if (qvalueCutoff.HasValue && _settings.MinimumDetections != -1 && detections < _settings.MinimumDetections)
                    continue;

                allAreas.Add(areas);
            }

            var medians = new List<double>(replicates);
            for (var i = 0; i < replicates; ++i)
                medians.Add(new Statistics(GetReplicateAreas(allAreas, i)).Median());

            return new MedianInfo(medians, new Statistics(medians).Median());
        }

        private bool ShouldUseQValues(SrmDocument document)
        {
            return _settings.PointsType == PointsTypePeakArea.targets &&
                   document.Settings.PeptideSettings.Integration.PeakScoringModel.IsTrained &&
                   !double.IsNaN(_settings.QValueCutoff) && _settings.QValueCutoff < 1.0;
        }

        private IEnumerable<double> GetReplicateAreas(List<List<double?>> allAreas, int replicateIndex)
        {
            foreach (var areas in allAreas)
            {
                var area = areas[replicateIndex];
                if (area.HasValue)
                    yield return area.Value;
            }
        }

        private static double NormalizeToGlobalStandard(SrmDocument document, TransitionGroupDocNode transitionGroupDocNode, int replicateIndex, double area)
        {
            var chromSet = document.MeasuredResults.Chromatograms[replicateIndex];
            var groupChromSet = transitionGroupDocNode.Results[replicateIndex];
            var fileInfoFirst = chromSet.GetFileInfo(groupChromSet.First(c => c.OptimizationStep == 0).FileId);
            var globalStandard = document.Settings.CalcGlobalStandardArea(replicateIndex, fileInfoFirst);
            if (globalStandard != 0.0)
                area /= globalStandard;
            return area;
        }
    }

    public class InternalData
    {
        public PeptideGroupDocNode PeptideGroup;
        public PeptideDocNode Peptide;
        public TransitionGroupDocNode TransitionGroup;
        public object Annotation;
        public double CV;
        public double CVBucketed;
        public double Area;

        #region object overrides

        protected bool Equals(InternalData other)
        {
            return CVBucketed.Equals(other.CVBucketed) && Area.Equals(other.Area);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((InternalData)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (CVBucketed.GetHashCode() * 397) ^ Area.GetHashCode();
            }
        }

        #endregion
    }

    public class AreaCVRefinementSettings
    {
        public AreaCVRefinementSettings(double cvCutoff, double qValueCutoff, int minimumDetections, NormalizeOption normalizeOption,
            AreaCVTransitions transitions, int countTransitions, AreaCVMsLevel msLevel)
        {
            CVCutoff = cvCutoff;
            QValueCutoff = qValueCutoff;
            MinimumDetections = minimumDetections;
            NormalizeOption = normalizeOption;
            MsLevel = msLevel;
            Transitions = transitions;
            CountTransitions = countTransitions;
            Annotation = null;
            Group = null;
        }

        public virtual void AddToInternalData(ICollection<InternalData> data, List<AreaInfo> areas,
            PeptideGroupDocNode peptideGroup, PeptideDocNode peptide, TransitionGroupDocNode tranGroup, object annotation)
        {
            var normalizedStatistics = new Statistics(areas.Select(a => a.NormalizedArea));
            var normalizedMean = normalizedStatistics.Mean();
            var normalizedStdDev = normalizedStatistics.StdDev();

            // If the mean is 0 or NaN or the standard deviaiton is NaN the cv would also be NaN
            if (normalizedMean == 0.0 || double.IsNaN(normalizedMean) || double.IsNaN(normalizedStdDev))
                return;

            // Round cvs so that the smallest difference between two cv's is BinWidth
            var cv = normalizedStdDev / normalizedMean;
            data.Add(new InternalData
            {
                Peptide = peptide,
                PeptideGroup = peptideGroup,
                TransitionGroup = tranGroup,
                Annotation = annotation,
                CV = cv,
                CVBucketed = cv,
                Area = 0.0
            });
        }
        protected AreaCVRefinementSettings() {}

        public AreaCVMsLevel MsLevel { get; protected set; }
        public AreaCVTransitions Transitions { get; protected set; }
        public int CountTransitions { get; protected set; }
        public NormalizeOption NormalizeOption { get; protected set; }
        public ReplicateValue Group { get; protected set; }
        public object Annotation { get; protected set; }
        public PointsTypePeakArea PointsType { get; protected set; }
        public double QValueCutoff { get; protected set; }
        public double CVCutoff { get; protected set; }
        public int MinimumDetections { get; protected set; }

        protected bool Equals(AreaCVRefinementSettings other)
        {
            return MsLevel == other.MsLevel &&
                   Transitions == other.Transitions && CountTransitions == other.CountTransitions &&
                   NormalizeOption == other.NormalizeOption && Equals(Group, other.Group) &&
                   Equals(Annotation, other.Annotation) && PointsType == other.PointsType &&
                   QValueCutoff.Equals(other.QValueCutoff) && CVCutoff.Equals(other.CVCutoff) &&
                   MinimumDetections == other.MinimumDetections;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((AreaCVRefinementSettings) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = MsLevel.GetHashCode();
                hashCode = (hashCode * 397) ^ (int) Transitions;
                hashCode = (hashCode * 397) ^ CountTransitions;
                hashCode = (hashCode * 397) ^ NormalizeOption.GetHashCode();
                hashCode = (hashCode * 397) ^ (Group != null ? Group.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Annotation != null ? Annotation.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (int) PointsType;
                hashCode = (hashCode * 397) ^ QValueCutoff.GetHashCode();
                hashCode = (hashCode * 397) ^ CVCutoff.GetHashCode();
                hashCode = (hashCode * 397) ^ MinimumDetections;
                return hashCode;
            }
        }
    }

    public class AreaInfo
    {
        public AreaInfo(double area, double normalizedArea)
        {
            Area = area;
            NormalizedArea = normalizedArea;
        }

        public double Area { get; private set; }
        public double NormalizedArea { get; private set; }
    }

    public class MedianInfo
    {
        public MedianInfo(List<double> medians, double medianMedian)
        {
            Medians = medians;
            MedianMedian = medianMedian;
        }

        public IList<double> Medians { get; private set; }
        public double MedianMedian { get; private set; }
    }

    public class PeptideAnnotationPair
    {
        public PeptideAnnotationPair(PeptideGroupDocNode peptideGroup, PeptideDocNode peptide, TransitionGroupDocNode tranGroup, object annotation, double cvRaw)
        {
            PeptideGroup = peptideGroup;
            Peptide = peptide;
            TransitionGroup = tranGroup;
            Annotation = annotation;
            CVRaw = cvRaw;
        }

        public PeptideGroupDocNode PeptideGroup { get; private set; }
        public PeptideDocNode Peptide { get; private set; }
        public TransitionGroupDocNode TransitionGroup { get; private set; }
        public object Annotation { get; private set; }
        public double CVRaw { get; private set; }

    }

    public class CVData
    {
        public CVData(IEnumerable<PeptideAnnotationPair> peptideAnnotationPairs, double cv, double meanArea, int frequency)
        {
            PeptideAnnotationPairs = peptideAnnotationPairs;
            CV = cv;
            MeanArea = meanArea;
            Frequency = frequency;
        }

        public IEnumerable<PeptideAnnotationPair> PeptideAnnotationPairs { get; private set; }
        public double CV { get; private set; }
        public double MeanArea { get; private set; }
        public int Frequency { get; private set; }

        public override string ToString()
        {
            return CV + TextUtil.LineSeparate(PeptideAnnotationPairs.Select(p => @" " + p.Peptide.TextId));
        }
    }
}
