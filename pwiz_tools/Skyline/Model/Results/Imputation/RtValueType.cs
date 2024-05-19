using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public abstract class RtValueType
    {
        public static readonly RtValueType PEAK_APEXES = new PeakApexes();
        public static readonly RtValueType PSM_TIMES = new PsmTimes();

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

        private class PsmTimes : RtValueType
        {
            protected override IEnumerable<KeyValuePair<Target, IEnumerable<double>>> GetAllRetentionTimes(SrmDocument document, ReplicateFileInfo fileInfo)
            {
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
