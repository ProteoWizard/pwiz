/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.Spectra;
using pwiz.Common.SystemUtil;
using ZedGraph;

namespace pwiz.Skyline.Model.Results.Spectra.Alignment
{
    public class SpectrumSummaryList : IReadOnlyList<SpectrumSummary>
    {
        private ImmutableList<SpectrumSummary> _summaries;

        public SpectrumSummaryList(IEnumerable<SpectrumSummary> summaries)
        {
            _summaries = ImmutableList.ValueOfOrEmpty(summaries);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<SpectrumSummary> GetEnumerator()
        {
            return _summaries.GetEnumerator();
        }

        public int Count => _summaries.Count;

        public SpectrumSummary this[int index] => _summaries[index];

        private static Tuple<double, double, ImmutableList<ImmutableList<SpectrumPrecursor>>> GetSpectrumDigestKey(
            SpectrumSummary spectrumSummary)
        {
            if (spectrumSummary.SummaryValue == null)
            {
                return null;
            }

            var precursorsByMsLevel = ImmutableList.ValueOf(Enumerable
                .Range(1, spectrumSummary.SpectrumMetadata.MsLevel - 1)
                .Select(level => spectrumSummary.SpectrumMetadata.GetPrecursors(level)));
            return Tuple.Create(spectrumSummary.SpectrumMetadata.ScanWindowLowerLimit.Value,
                spectrumSummary.SpectrumMetadata.ScanWindowUpperLimit.Value, precursorsByMsLevel);
        }

        public SimilarityMatrix GetSimilarityMatrix(
            IProgressMonitor progressMonitor,
            IProgressStatus status,
            IEnumerable<SpectrumSummary> listS)
        {
            var byDigestKey = listS.ToLookup(GetSpectrumDigestKey);
            int completedCount = 0;
            var lists = new IList<PointPair>[Count];
            ParallelEx.For(0, Count, index =>
            {
                var spectrum = this[index];
                var key = GetSpectrumDigestKey(spectrum);
                if (key != null)
                {
                    var list = new List<PointPair>();
                    foreach (var otherSpectrum in byDigestKey[key])
                    {
                        if (true == progressMonitor?.IsCanceled)
                        {
                            break;
                        }

                        var score = CalculateSimilarityScore(spectrum.SummaryValue,
                            otherSpectrum.SummaryValue);
                        if (score.HasValue)
                        {
                            list.Add(new PointPair(spectrum.RetentionTime, otherSpectrum.RetentionTime, score.Value));
                        }
                    }

                    lists[index] = list;
                }

                if (progressMonitor != null)
                {
                    lock (progressMonitor)
                    {
                        completedCount++;
                        int progressValue = completedCount * 100 / lists.Length;
                        progressMonitor.UpdateProgress(status = status.ChangePercentComplete(progressValue));
                    }
                }
            });
            return new SimilarityMatrix(lists.SelectMany(v=> v ?? Array.Empty<PointPair>()));
        }
        public static double? CalculateSimilarityScore(IList<double> xList, IList<double> yList)
        {
            if (xList.Count != yList.Count)
            {
                return null;
            }
            double sumXX = 0;
            double sumXY = 0;
            double sumYY = 0;
            for (int i = 0; i < xList.Count; i++)
            {
                double x = xList[i];
                double y = yList[i];
                sumXX += x * x;
                sumXY += x * y;
                sumYY += y * y;
            }

            if (sumXX <= 0 || sumYY <= 0)
            {
                return null;
            }

            return sumXY / Math.Sqrt(sumXX * sumYY);
        }
    }
}
