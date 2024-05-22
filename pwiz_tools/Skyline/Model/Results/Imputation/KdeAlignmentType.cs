using System.Collections.Generic;
using System.Linq;
using System.Threading;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model.RetentionTimes;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class KdeAlignmentType
    {
        public static Dictionary<ReplicateFileId, AlignmentFunction> PerformAlignment(ProductionMonitor productionMonitor,
            IDictionary<ReplicateFileId, Dictionary<Target, double>> fileTimesDictionaries)
        {
            var averageTimes = new Dictionary<Target, double>();
            foreach (var target in fileTimesDictionaries.Values.SelectMany(dictionary => dictionary.Keys).Distinct())
            {
                var times = new List<double>();
                foreach (var dictionary in fileTimesDictionaries.Values)
                {
                    if (dictionary.TryGetValue(target, out var time))
                    {
                        times.Add(time);
                    }
                }

                if (times.Count > 0)
                {
                    averageTimes.Add(target, times.Average());
                }
            }

            var alignmentFunctions = new Dictionary<ReplicateFileId, AlignmentFunction>();
            int completedCount = 0;
            foreach (var fileEntry in fileTimesDictionaries)
            {
                productionMonitor.CancellationToken.ThrowIfCancellationRequested();
                productionMonitor.SetProgress(completedCount * 100 / fileTimesDictionaries.Count);
                var kdeAligner =
                    PerformKdeAlignment(productionMonitor.CancellationToken, fileEntry.Value, averageTimes);
                if (kdeAligner != null)
                {
                    var alignmentFunction = AlignmentFunction.Define(kdeAligner.GetValue, kdeAligner.GetValueReversed);
                    alignmentFunctions.Add(fileEntry.Key, alignmentFunction);
                }
                completedCount++;
            }

            return alignmentFunctions;
        }

        private static KdeAligner PerformKdeAlignment(CancellationToken cancellationToken,
            Dictionary<Target, double> sourceTimes, Dictionary<Target, double> targetTimes)
        {
            var xValues = new List<double>();
            var yValues = new List<double>();
            foreach (var sourceEntry in sourceTimes)
            {
                if (targetTimes.TryGetValue(sourceEntry.Key, out var targetTime))
                {
                    xValues.Add(sourceEntry.Value);
                    yValues.Add(targetTime);
                }
            }

            var kdeAligner = new KdeAligner(-1, -1);
            kdeAligner.Train(xValues.ToArray(), yValues.ToArray(), cancellationToken);
            return kdeAligner;
        }

    }
}
