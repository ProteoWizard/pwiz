/*
 * Original author: Michael J. MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// The library cosine at every scan of the chosen peak, from its start to its end
    /// (the SG sweep's per-scan kernel, <see cref="SgWeightedSweep.ComputeCosineAtScan"/>),
    /// reduced to a mean and a standard deviation. Where sg_weighted_cosine looks at the
    /// apex +/- 2 scans, these see whether the library spectrum holds across the whole
    /// peak - a real elution matches it at every scan, an interfered or noise peak at a
    /// few. Scans outside the scored window are skipped, as the SG sweep skips them.
    /// </summary>
    internal sealed class PeakSpectrumSweep
    {
        public double CosineMean;
        public double CosineSd;

        public static PeakSpectrumSweep GetOrCompute(OspreyScoringContext context, IOspreyApexSpectraPeakData peakData)
        {
            if (context.TryGetInfo(out PeakSpectrumSweep sweep))
                return sweep;
            sweep = Compute(context, peakData);
            context.AddInfo(sweep);
            return sweep;
        }

        private static PeakSpectrumSweep Compute(OspreyScoringContext context, IOspreyApexSpectraPeakData peakData)
        {
            var sweep = new PeakSpectrumSweep();
            var bounds = peakData.PeakBounds;
            int first = bounds.StartIndex - bounds.ApexIndex;
            int last = bounds.EndIndex - bounds.ApexIndex;
            double sum = 0.0;
            double sumSq = 0.0;
            int n = 0;
            for (int offset = first; offset <= last; offset++)
            {
                if (!peakData.TryGetApexOffsetSpectrum(offset, out var spectrum, out _))
                    continue;
                double cosine = SgWeightedSweep.ComputeCosineAtScan(peakData.Candidate, spectrum, context.Config);
                sum += cosine;
                sumSq += cosine * cosine;
                n++;
            }
            if (n > 0)
            {
                sweep.CosineMean = sum / n;
                sweep.CosineSd = Math.Sqrt(Math.Max(0.0, sumSq / n - sweep.CosineMean * sweep.CosineMean));
            }
            return sweep;
        }
    }

    /// <summary>peak_spectrum_cosine_mean: the mean library cosine over the peak's scans (0 with none).</summary>
    internal sealed class PeakSpectrumCosineMeanCalc : ApexSpectraOspreyFeatureCalculator
    {
        public override string Name { get { return @"peak_spectrum_cosine_mean"; } }

        public override string DisplayName { get { return OspreyScoringResources.PeakSpectrumCosineMeanCalc_DisplayName_Library_cosine_across_peak__mean_; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        protected override double Calculate(OspreyScoringContext context, IOspreyApexSpectraPeakData peakData)
        {
            return PeakSpectrumSweep.GetOrCompute(context, peakData).CosineMean;
        }
    }

    /// <summary>peak_spectrum_cosine_sd: the standard deviation of the library cosine over the peak's scans.</summary>
    internal sealed class PeakSpectrumCosineSdCalc : ApexSpectraOspreyFeatureCalculator
    {
        public override string Name { get { return @"peak_spectrum_cosine_sd"; } }

        public override string DisplayName { get { return OspreyScoringResources.PeakSpectrumCosineSdCalc_DisplayName_Library_cosine_across_peak__SD_; } }

        public override bool IsReversedScore { get { return true; } }   // a steadier match is better

        protected override double Calculate(OspreyScoringContext context, IOspreyApexSpectraPeakData peakData)
        {
            return PeakSpectrumSweep.GetOrCompute(context, peakData).CosineSd;
        }
    }
}
