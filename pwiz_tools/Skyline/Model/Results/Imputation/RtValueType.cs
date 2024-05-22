using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;
using pwiz.Common.Collections;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public abstract class RtValueType
    {
        public static readonly RtValueType PEAK_APEXES = new PeakApexes();
        public static readonly RtValueType PSM_TIMES = new PsmTimes();
        public static readonly RtValueType HIGH_SCORING_PEAK_APEXES = new HighScoringPeakApexes();


        public static readonly ImmutableList<RtValueType> All = ImmutableList.ValueOf(new []{PEAK_APEXES, PSM_TIMES});

        public virtual bool IsValidFor(SrmDocument document)
        {
            return true;
        }

        public Dictionary<Target, double> GetRetentionTimes(SrmDocument document,
            ReplicateFileId replicateFileId)
        {
            var dictionary = new Dictionary<Target, double>();
            var fileInfo = replicateFileId.FindInfo(document.MeasuredResults);
            if (fileInfo == null)
            {
                return dictionary;
            }

            foreach (var entry in GetAllRetentionTimes(document, fileInfo))
            {
                dictionary.Add(entry.Key, entry.Value.Median());
            }

            return dictionary;
        }

        protected abstract IEnumerable<KeyValuePair<Target, IEnumerable<double>>> GetAllRetentionTimes(
            SrmDocument document, ReplicateFileInfo fileInfo);

        class PeakApexes : RtValueType
        {
            protected override IEnumerable<KeyValuePair<Target, IEnumerable<double>>> GetAllRetentionTimes(SrmDocument document, ReplicateFileInfo fileInfo)
            {
                foreach (var peptideGroup in document.Molecules.GroupBy(peptideDocNode => peptideDocNode.ModifiedTarget))
                {
                    var times = new List<double>();
                    foreach (var peptideDocNode in peptideGroup)
                    {
                        foreach (var peptideChromInfo in peptideDocNode.GetSafeChromInfo(fileInfo.ReplicateIndex))
                        {
                            if (ReferenceEquals(peptideChromInfo.FileId, fileInfo.ReplicateFileId.FileId))
                            {
                                if (peptideChromInfo.RetentionTime.HasValue)
                                {
                                    times.Add(peptideChromInfo.RetentionTime.Value);
                                }
                            }
                        }
                    }

                    if (times.Count > 0)
                    {
                        yield return new KeyValuePair<Target, IEnumerable<double>>(peptideGroup.Key, times);
                    }
                }
            }

            public override string ToString()
            {
                return "Peak Apexes";
            }
        }

        class HighScoringPeakApexes : RtValueType
        {
            protected override IEnumerable<KeyValuePair<Target, IEnumerable<double>>> GetAllRetentionTimes(SrmDocument document, ReplicateFileInfo fileInfo)
            {
                var scoreCutoff = GetScoreAtPercentile(document, .5);
                foreach (var peptideGroup in document.Molecules.GroupBy(peptideDocNode => peptideDocNode.ModifiedTarget))
                {
                    var times = new List<double>();
                    foreach (var peptideDocNode in peptideGroup)
                    {
                        if (GetScore(peptideDocNode, fileInfo.ReplicateIndex, fileInfo.ReplicateFileId.FileId) >=
                            scoreCutoff)
                        {
                            foreach (var peptideChromInfo in peptideDocNode.GetSafeChromInfo(fileInfo.ReplicateIndex))
                            {
                                if (ReferenceEquals(peptideChromInfo.FileId, fileInfo.ReplicateFileId.FileId))
                                {
                                    if (peptideChromInfo.RetentionTime.HasValue)
                                    {
                                        times.Add(peptideChromInfo.RetentionTime.Value);
                                    }
                                }
                            }
                        }
                    }

                    if (times.Count > 0)
                    {
                        yield return new KeyValuePair<Target, IEnumerable<double>>(peptideGroup.Key, times);
                    }
                }
            }

            private float? GetScore(PeptideDocNode peptideDocNode, int replicateIndex, ChromFileInfoId fileId)
            {
                foreach (var transitionGroup in peptideDocNode.TransitionGroups)
                {
                    foreach (var chromInfo in transitionGroup.GetSafeChromInfo(replicateIndex))
                    {
                        if (ReferenceEquals(chromInfo.FileId, fileId))
                        {
                            return chromInfo.ZScore;
                        }
                    }
                }

                return null;
            }

            public override string ToString()
            {
                return "High Scoring Peak Apexes";
            }

            public double GetScoreAtPercentile(SrmDocument document, double percentile)
            {
                int replicateCount = document.MeasuredResults?.Chromatograms.Count ?? 0;
                var allScores = document.MoleculeTransitionGroups
                    .SelectMany(tg => Enumerable.Range(0, replicateCount).SelectMany(i=>tg.GetSafeChromInfo(i)))
                    .Select(chromInfo => (double?) chromInfo.ZScore).OfType<double>();
                var statistics = new pwiz.Skyline.Util.Statistics(allScores);
                return statistics.Percentile(percentile);
            }


        }

        private class PsmTimes : RtValueType
        {
            protected override IEnumerable<KeyValuePair<Target, IEnumerable<double>>> GetAllRetentionTimes(SrmDocument document, ReplicateFileInfo fileInfo)
            {
                if (!document.Settings.PeptideSettings.Libraries.IsLoaded)
                {
                    return Array.Empty<KeyValuePair<Target, IEnumerable<double>>>();
                }
                var retentionTimes = document.Settings.GetRetentionTimes(fileInfo.MsDataFileUri);
                if (retentionTimes == null)
                {
                    return Array.Empty<KeyValuePair<Target, IEnumerable<double>>>();
                }

                return retentionTimes.GetFirstRetentionTimes().Select(kvp =>
                    new KeyValuePair<Target, IEnumerable<double>>(kvp.Key, new[] { kvp.Value }));
            }

            public override string ToString()
            {
                return "PSM Times";
            }

            public override bool IsValidFor(SrmDocument document)
            {
                var measuredResults = document.MeasuredResults;
                if (measuredResults == null)
                {
                    return true;
                }

                if (!document.Settings.PeptideSettings.Libraries.IsLoaded)
                {
                    return true;
                }
                foreach (var chromatogramSet in measuredResults.Chromatograms)
                {
                    foreach (var chromFileInfo in chromatogramSet.MSDataFileInfos)
                    {
                        if (document.Settings.PeptideSettings.Libraries.TryGetRetentionTimes(chromFileInfo.FilePath,
                                out var libraryRetentionTimes))
                        {
                            if (libraryRetentionTimes.PeptideRetentionTimes.Any())
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
        }
    }
}
