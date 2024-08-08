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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using pwiz.Common.Collections;
using pwiz.Common.Spectra;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.RetentionTimes;

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

        private static DigestKey GetSpectrumDigestKey(
            SpectrumSummary spectrumSummary)
        {
            if (spectrumSummary.SummaryValueLength == 0 || spectrumSummary.SummaryValueArray.All(v=>0 == v))
            {
                return null;
            }

            if (!spectrumSummary.SpectrumMetadata.ScanWindowLowerLimit.HasValue ||
                !spectrumSummary.SpectrumMetadata.ScanWindowUpperLimit.HasValue)
            {
                return null;
            }

            var precursorsByMsLevel = ImmutableList.ValueOf(Enumerable
                .Range(1, spectrumSummary.SpectrumMetadata.MsLevel - 1)
                .Select(level => spectrumSummary.SpectrumMetadata.GetPrecursors(level)));
            return new DigestKey(spectrumSummary.SpectrumMetadata.ScanWindowLowerLimit.Value,
                spectrumSummary.SpectrumMetadata.ScanWindowUpperLimit.Value, spectrumSummary.SummaryValueLength,
                precursorsByMsLevel);
        }

        /// <summary>
        /// Minimum number of a spectra with the same key (i.e. same MS Level and Precursor)
        /// to do an alignment between. (That is, it does not make sense to try to align to
        /// DDA spectra which happen to have the same precursor m/z-- the same precursor
        /// needs to have been sampled several times)
        /// </summary>
        private const int MIN_SPECTRA_FOR_ALIGNMENT = 20;

        public IEnumerable<SpectrumMetadata> SpectrumMetadatas
        {
            get
            {
                return this.Select(summary => summary.SpectrumMetadata);
            }
        }

        protected bool Equals(SpectrumSummaryList other)
        {
            return _summaries.Equals(other._summaries);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SpectrumSummaryList)obj);
        }

        public override int GetHashCode()
        {
            return _summaries.GetHashCode();
        }

        private class DigestKey
        {
            public DigestKey(double scanWindowLowerLimit, double scanWindowUpperLimit, int summaryValueLength, ImmutableList<ImmutableList<SpectrumPrecursor>> precursors)
            {
                ScanWindowLowerLimit = scanWindowLowerLimit;
                ScanWindowUpperLimit = scanWindowUpperLimit;
                SummaryValueLength = summaryValueLength;
                Precursors = precursors;

            }

            public double ScanWindowLowerLimit { get; }
            public double ScanWindowUpperLimit { get; }
            public int SummaryValueLength { get; }
            public ImmutableList<ImmutableList<SpectrumPrecursor>> Precursors { get; }

            protected bool Equals(DigestKey other)
            {
                return ScanWindowLowerLimit.Equals(other.ScanWindowLowerLimit) &&
                       ScanWindowUpperLimit.Equals(other.ScanWindowUpperLimit) && Precursors.Equals(other.Precursors);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((DigestKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = ScanWindowLowerLimit.GetHashCode();
                    hashCode = (hashCode * 397) ^ ScanWindowUpperLimit.GetHashCode();
                    hashCode = (hashCode * 397) ^ Precursors.GetHashCode();
                    return hashCode;
                }
            }
        }

        /// <summary>
        /// Remove all of the spectra whose scan window or precursors are not common enough to do
        /// alignment on
        /// </summary>
        /// <returns></returns>
        public SpectrumSummaryList RemoveRareSpectra()
        {
            var digestKeysToKeep = this.GroupBy(GetSpectrumDigestKey)
                .Where(group => group.Count() >= MIN_SPECTRA_FOR_ALIGNMENT).Select(group => group.Key).ToHashSet();
            return new SpectrumSummaryList(this.Where(spectrum =>
                digestKeysToKeep.Contains(GetSpectrumDigestKey(spectrum))));
        }

        public SimilarityGrid GetSimilarityGrid(SpectrumSummaryList that)
        {
            var thisByDigestKey = this.ToLookup(GetSpectrumDigestKey);
            var thatByDigestKey = that.ToLookup(GetSpectrumDigestKey);
            int bestCount = 0;
            DigestKey bestDigestKey = null;
            foreach (var group in thisByDigestKey)
            {
                if (group.Key == null)
                {
                    continue;
                }

                var count = group.Count() * thatByDigestKey[group.Key].Count();
                if (count > bestCount)
                {
                    bestCount = count;
                    bestDigestKey = group.Key;
                }
            }

            if (bestCount == 0)
            {
                return null;
            }

            return new SimilarityGrid(thisByDigestKey[bestDigestKey], thatByDigestKey[bestDigestKey]);
        }

        public KdeAligner PerformAlignment(IProgressMonitor progressMonitor, SpectrumSummaryList spectra2, double? startingWindowSizeProportion, int? threadCount)
        {
            var similarityGrid = GetSimilarityGrid(spectra2);
            var candidatePoints = similarityGrid.GetBestPointCandidates(progressMonitor, threadCount);
            if (candidatePoints == null)
            {
                return null;
            }

            var bestPoints = SimilarityGrid.FilterBestPoints(candidatePoints);
            var kdeAligner = new KdeAligner();
            if (startingWindowSizeProportion.HasValue)
            {
                kdeAligner.StartingWindowSizeProportion = startingWindowSizeProportion.Value;
            }
            kdeAligner.Train(bestPoints.Select(pt => pt.XRetentionTime).ToArray(), bestPoints.Select(pt=>pt.YRetentionTime).ToArray(), CancellationToken.None);
            return kdeAligner;
        }
    }
}
