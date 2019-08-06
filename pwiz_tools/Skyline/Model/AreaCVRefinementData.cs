using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class AreaCVRefinementData : Immutable
    {
        public readonly AreaCVRefinementSettings _settings;
        public AreaCVRefinementData(SrmDocument document, AreaCVRefinementSettings settings)
        {
            _settings = settings;
            if (document == null || !document.Settings.HasResults)
                return;

            var replicates = document.MeasuredResults.Chromatograms.Count;
            var areas = new List<CVAreaInfo>(replicates);
            var annotations = new string[] { null };
            var data = new List<InternalData>();
            var hasHeavyMods = document.Settings.PeptideSettings.Modifications.HasHeavyModifications;
            var hasGlobalStandards = document.Settings.HasGlobalStandardArea;
            var ms1 = false;
            var best = false;
            double? qvalueCutoff = null;
            if (document.Settings.PeptideSettings.Integration.PeakScoringModel.IsTrained &&
                !double.IsNaN(_settings.QValueCutoff) &&
                _settings.QValueCutoff < 1.0)
            {
                qvalueCutoff = _settings.QValueCutoff;
            }

            int? minDetections = null;
            if (_settings.MinimumDetections != -1)
            {
                minDetections = _settings.MinimumDetections;
            }

            MedianInfo medianInfo = null;
            if (_settings.NormalizationMethod == AreaCVNormalizationMethod.medians)
                medianInfo = CalculateMedianAreas(document);

            foreach (var peptideGroup in document.MoleculeGroups)
            {
                foreach (var peptide in peptideGroup.Molecules)
                {
                    foreach (var transitionGroupDocNode in peptide.TransitionGroups)
                    {
                        if (transitionGroupDocNode.IsDecoy)
                            continue;

                        foreach (var a in annotations)
                        {
                            areas.Clear();
                            
                            foreach (var i in AnnotationHelper.GetReplicateIndices(document.Settings, null, a))
                            {
                                var groupChromInfo = transitionGroupDocNode.GetSafeChromInfo(i)
                                    .FirstOrDefault(c => c.OptimizationStep == 0);
                                if (groupChromInfo == null)
                                    continue;

                                if (qvalueCutoff.HasValue)
                                {
                                    if (!(groupChromInfo.QValue.HasValue && groupChromInfo.QValue.Value < qvalueCutoff.Value))
                                        continue;
                                }

                                if (!groupChromInfo.Area.HasValue)
                                    continue;
                                var index = i;
                                var sumArea = transitionGroupDocNode.Transitions.Where(t =>
                                {
                                    if (ms1 != t.IsMs1 || !t.ExplicitQuantitative)
                                        return false;

                                    var chromInfo = t.GetSafeChromInfo(index)
                                        .FirstOrDefault(c => c.OptimizationStep == 0);
                                    return chromInfo != null && (!best || chromInfo.RankByLevel == 1);
                                }).Sum(t => (double) t.GetSafeChromInfo(index)
                                    .FirstOrDefault(c => c.OptimizationStep == 0).Area);

                                var normalizedArea = sumArea;
                                if (_settings.NormalizationMethod == AreaCVNormalizationMethod.medians)
                                    normalizedArea /= medianInfo.Medians[i] / medianInfo.MedianMedian;
                                else if (_settings.NormalizationMethod == AreaCVNormalizationMethod.global_standards &&
                                         hasGlobalStandards)
                                    normalizedArea =
                                        NormalizeToGlobalStandard(document, transitionGroupDocNode, i, sumArea);
                                else if (_settings.NormalizationMethod == AreaCVNormalizationMethod.ratio && hasHeavyMods && _settings.RatioIndex >= 0)
                                {
                                    var ci = transitionGroupDocNode.GetSafeChromInfo(i).FirstOrDefault(c => c.OptimizationStep == 0);
                                    if (ci != null)
                                    {
                                        var ratioValue = ci.GetRatio(_settings.RatioIndex);
                                        if (ratioValue != null)
                                            normalizedArea /= ratioValue.Ratio;
                                    }
                                }
                                areas.Add(new CVAreaInfo(sumArea, normalizedArea));
                            }

                            if (qvalueCutoff.HasValue && minDetections.HasValue && areas.Count < minDetections.Value)
                                continue;
                            AddToInternalData(data, areas, peptideGroup, peptide, transitionGroupDocNode);
                        }
                    }
                }
            }
            Data = ImmutableList<CVData>.ValueOf(data.GroupBy(i => i, (key, grouped) =>
            {
                var groupedArray = grouped.ToArray();
                return new CVData(
                    groupedArray.Select(idata => new PeptideAnnotationPair(idata.PeptideGroup, idata.Peptide, idata.TransitionGroup, idata.CV)),
                    key.CV, key.Area, groupedArray.Length);
            }).OrderBy(d => d.CV));

        }

        private void AddToInternalData(ICollection<InternalData> data, List<CVAreaInfo> areas,
            PeptideGroupDocNode peptideGroup, PeptideDocNode peptide, TransitionGroupDocNode tranGroup)
        {
            var normalizedStatistics = new Statistics(areas.Select(a => a.NormalizedArea));
            var normalizedMean = normalizedStatistics.Mean();
            var normalizedStdDev = normalizedStatistics.StdDev();

            if (normalizedMean == 0.0 || double.IsNaN(normalizedMean) || double.IsNaN(normalizedStdDev))
                return;

            var cv = normalizedStdDev / normalizedMean;
            data.Add(new InternalData
            {
                Peptide = peptide,
                PeptideGroup = peptideGroup,
                TransitionGroup = tranGroup,
                CV = cv,
                Area = 0.0
            });
        }

        public SrmDocument RemoveAboveCVCuttoff(SrmDocument document)
        {
            var cutoff = _settings.CVCutoff / 100.0;

            var ids = new HashSet<int>(Data.Where(d => d.CV < cutoff)
                .SelectMany(d => d.PeptideAnnotationPairs)
                .Select(pair => pair.TransitionGroup.Id.GlobalIndex));

            var nodeCount = 0;
            var setRemove = new HashSet<int>();
            foreach (var nodeMolecule in document.Molecules)
            {
                if (nodeMolecule.GlobalStandardType != null)
                    continue;
                foreach (var nodeGroup in nodeMolecule.TransitionGroups.Where(n => !ids.Contains(n.Id.GlobalIndex)))
                    setRemove.Add(nodeGroup.Id.GlobalIndex);
                nodeCount = setRemove.Count;
            }

            return (SrmDocument)document.RemoveAll(setRemove, (int) SrmDocument.Level.TransitionGroups,
                (int) SrmDocument.Level.Molecules);
        }

        private MedianInfo CalculateMedianAreas(SrmDocument document)
        {
            double? qvalueCutoff = null;
            if (!double.IsNaN(_settings.QValueCutoff))
                qvalueCutoff = _settings.QValueCutoff;
            var replicates = document.MeasuredResults.Chromatograms.Count;
            var allAreas = new List<List<double?>>(document.MoleculeTransitionGroupCount);

            foreach (var transitionGroupDocNode in document.MoleculeTransitionGroups)
            {
                if (transitionGroupDocNode.IsDecoy)
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

        public IList<CVData> Data { get; private set; }

        private class MedianInfo
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
            public PeptideAnnotationPair(PeptideGroupDocNode peptideGroup, PeptideDocNode peptide, TransitionGroupDocNode tranGroup, double cvRaw)
            {
                PeptideGroup = peptideGroup;
                Peptide = peptide;
                TransitionGroup = tranGroup;
                CVRaw = cvRaw;
            }

            public PeptideGroupDocNode PeptideGroup { get; private set; }
            public PeptideDocNode Peptide { get; private set; }
            public TransitionGroupDocNode TransitionGroup { get; private set; }
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
        }

        private class CVAreaInfo
        {
            public CVAreaInfo(double area, double normalizedArea)
            {
                Area = area;
                NormalizedArea = normalizedArea;
            }

            public double Area { get; private set; }
            public double NormalizedArea { get; private set; }
        }

        private class InternalData
        {
            public PeptideGroupDocNode PeptideGroup;
            public PeptideDocNode Peptide;
            public TransitionGroupDocNode TransitionGroup;
            public double CV;
            public double Area;

            #region object overrides

            protected bool Equals(InternalData other)
            {
                return CV.Equals(other.CV) && Area.Equals(other.Area);
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
                    return (CV.GetHashCode() * 397) ^ Area.GetHashCode();
                }
            }

            #endregion
        }

        public class AreaCVRefinementSettings
        {
            public AreaCVRefinementSettings(double cvCutoff)
            {
                CVCutoff = cvCutoff;
                QValueCutoff = double.NaN;
                MinimumDetections = -1;
                NormalizationMethod = AreaCVNormalizationMethod.none;
                RatioIndex = -1;
            }

            public AreaCVRefinementSettings(double cvCutoff, double qValueCutoff, int minimumDetections, AreaCVNormalizationMethod normalizationMethod)
            {
                CVCutoff = cvCutoff;
                QValueCutoff = qValueCutoff;
                MinimumDetections = minimumDetections;
                NormalizationMethod = normalizationMethod;
            }

            public double QValueCutoff { get; private set; }
            public double CVCutoff { get; private set; }
            public int MinimumDetections { get; private set; }
            public AreaCVNormalizationMethod NormalizationMethod { get; private set; }
            public int RatioIndex { get; private set; }
        }
    }
}