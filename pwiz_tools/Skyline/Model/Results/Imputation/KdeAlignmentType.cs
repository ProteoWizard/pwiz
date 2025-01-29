/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Threading;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model.RetentionTimes;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class KdeAlignmentType
    {
        public static ConsensusAlignmentResults PerformAlignment(ProductionMonitor productionMonitor,
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

            return new ConsensusAlignmentResults(alignmentFunctions, averageTimes);
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

            try
            {
                var kdeAligner = new KdeAligner(-1, -1);
                kdeAligner.Train(xValues.ToArray(), yValues.ToArray(), cancellationToken);
                return kdeAligner;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

    }
}
