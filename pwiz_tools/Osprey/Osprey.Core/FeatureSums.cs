/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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
using System.IO;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// One run's share of the first-pass model's feature report: the class counts, the per-feature
    /// sums of the standardized feature vectors, and the per-feature class histograms over fixed
    /// bins, all over that run's rows. The report's mean gaps and distributions are these shares
    /// added in run order, so a run that resumes from its first-pass FDR sidecar contributes from
    /// disk exactly as a re-scored one does, and a report asked for after the analysis needs no
    /// re-scoring (doc 00, P16).
    ///
    /// <para>Binary form, little-endian: u32 feature count F, u32 bin count B, i64 target count,
    /// i64 decoy count, F f64 target sums, F f64 decoy sums, F*B i32 target histogram counts
    /// (feature-major), F*B i32 decoy histogram counts.</para>
    /// </summary>
    public sealed class FeatureSums
    {
        public FeatureSums(int featureCount, int binCount)
        {
            if (featureCount < 0)
                throw new ArgumentOutOfRangeException(nameof(featureCount));
            if (binCount < 0)
                throw new ArgumentOutOfRangeException(nameof(binCount));
            BinCount = binCount;
            TargetSums = new double[featureCount];
            DecoySums = new double[featureCount];
            TargetHistograms = NewHistograms(featureCount, binCount);
            DecoyHistograms = NewHistograms(featureCount, binCount);
        }

        public int FeatureCount => TargetSums.Length;
        public int BinCount { get; }
        public long TargetCount { get; set; }
        public long DecoyCount { get; set; }
        public double[] TargetSums { get; }
        public double[] DecoySums { get; }
        /// <summary>Per feature, the target rows' count in each bin.</summary>
        public int[][] TargetHistograms { get; }
        /// <summary>Per feature, the decoy rows' count in each bin.</summary>
        public int[][] DecoyHistograms { get; }

        /// <summary>The length of <see cref="Write"/>'s output.</summary>
        public int ByteLength => ByteLengthFor(FeatureCount, BinCount);

        private static int ByteLengthFor(int featureCount, int binCount)
        {
            return checked((int)LongByteLengthFor(featureCount, binCount));
        }

        private static long LongByteLengthFor(long featureCount, long binCount)
        {
            return 2 * sizeof(uint) + 2 * sizeof(long) + 2 * featureCount * sizeof(double) +
                   2 * featureCount * binCount * sizeof(int);
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write((uint)FeatureCount);
            writer.Write((uint)BinCount);
            writer.Write(TargetCount);
            writer.Write(DecoyCount);
            foreach (double sum in TargetSums)
                writer.Write(sum);
            foreach (double sum in DecoySums)
                writer.Write(sum);
            WriteHistograms(writer, TargetHistograms);
            WriteHistograms(writer, DecoyHistograms);
        }

        /// <summary>
        /// Read a share written by <see cref="Write"/> that occupies exactly
        /// <paramref name="length"/> bytes, or null when the bytes do not describe one.
        /// </summary>
        public static FeatureSums Read(BinaryReader reader, int length)
        {
            if (length < 2 * sizeof(uint))
                return null;
            uint featureCount = reader.ReadUInt32();
            uint binCount = reader.ReadUInt32();
            // Long arithmetic: corrupt counts must fail this comparison, not overflow it.
            if (featureCount > short.MaxValue || binCount > short.MaxValue ||
                LongByteLengthFor(featureCount, binCount) != length)
            {
                return null;
            }
            var sums = new FeatureSums((int)featureCount, (int)binCount)
            {
                TargetCount = reader.ReadInt64(),
                DecoyCount = reader.ReadInt64()
            };
            for (int j = 0; j < featureCount; j++)
                sums.TargetSums[j] = reader.ReadDouble();
            for (int j = 0; j < featureCount; j++)
                sums.DecoySums[j] = reader.ReadDouble();
            ReadHistograms(reader, sums.TargetHistograms);
            ReadHistograms(reader, sums.DecoyHistograms);
            return sums;
        }

        private static int[][] NewHistograms(int featureCount, int binCount)
        {
            var histograms = new int[featureCount][];
            for (int j = 0; j < featureCount; j++)
                histograms[j] = new int[binCount];
            return histograms;
        }

        private static void WriteHistograms(BinaryWriter writer, int[][] histograms)
        {
            foreach (int[] histogram in histograms)
            {
                foreach (int count in histogram)
                    writer.Write(count);
            }
        }

        private static void ReadHistograms(BinaryReader reader, int[][] histograms)
        {
            foreach (int[] histogram in histograms)
            {
                for (int b = 0; b < histogram.Length; b++)
                    histogram[b] = reader.ReadInt32();
            }
        }
    }
}
