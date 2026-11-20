/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace pwiz.Osprey.Demux
{
    /// <summary>
    /// The transmission of a scanning quadrupole: how much of a precursor at m/z <c>m</c> an
    /// encoded bin centered at <c>c</c> records, as a function of the offset <c>c - m</c> in Th.
    /// </summary>
    /// <remarks>
    /// Measured from the data rather than assumed: the surviving unfragmented precursor ion
    /// traces it directly, bin by bin across the sweep. The measured shape on a ZenoTOF 8600
    /// ZT Scan run is a trapezoid - a flat top about 4 Th wide and linear edges about 6 Th long
    /// on each side - so the kernel is kept as a table and interpolated linearly, with no
    /// parametric form imposed. The peak is normalized to 1.
    /// </remarks>
    public sealed class ScanningKernel
    {
        /// <summary>Kernel values below this fraction of the peak are treated as zero.</summary>
        public const double SUPPORT_FLOOR = 0.005;

        private readonly double[] _offsets;
        private readonly double[] _values;

        private ScanningKernel(double[] offsets, double[] values)
        {
            _offsets = offsets;
            _values = values;
        }

        /// <summary>
        /// A kernel from sampled offsets (Th) and relative transmissions. Samples need not be
        /// sorted; negative and missing values are dropped, the peak is scaled to 1, and the
        /// support is trimmed to where the kernel is above <see cref="SUPPORT_FLOOR"/>.
        /// </summary>
        public static ScanningKernel FromSamples(IReadOnlyList<double> offsets, IReadOnlyList<double> values)
        {
            if (offsets == null)
                throw new ArgumentNullException(nameof(offsets));
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            if (offsets.Count != values.Count)
                throw new ArgumentException(@"Kernel offsets and values differ in length.");

            var samples = new List<(double Offset, double Value)>();
            for (int i = 0; i < offsets.Count; i++)
            {
                if (!double.IsNaN(values[i]) && !double.IsInfinity(values[i]))
                    samples.Add((offsets[i], Math.Max(0, values[i])));
            }
            samples.Sort((a, b) => a.Offset.CompareTo(b.Offset)); // Array.Sort OK: offsets are distinct sample positions
            double peak = samples.Count > 0 ? samples.Max(s => s.Value) : 0;
            if (peak <= 0)
                throw new ArgumentException(@"A kernel needs at least one positive sample.");

            int first = samples.FindIndex(s => s.Value / peak >= SUPPORT_FLOOR);
            int last = samples.FindLastIndex(s => s.Value / peak >= SUPPORT_FLOOR);
            // Keep one sample outside the support on each side, so the edges ramp to zero.
            first = Math.Max(0, first - 1);
            last = Math.Min(samples.Count - 1, last + 1);
            int count = last - first + 1;
            var sampleOffsets = new double[count];
            var sampleValues = new double[count];
            for (int i = 0; i < count; i++)
            {
                var sample = samples[first + i];
                sampleOffsets[i] = sample.Offset;
                sampleValues[i] = sample.Value / peak < SUPPORT_FLOOR ? 0 : sample.Value / peak;
            }
            return new ScanningKernel(sampleOffsets, sampleValues);
        }

        /// <summary>
        /// Reads a kernel from a tab-separated profile: a header line, then rows of offset and
        /// value. The value is read from <paramref name="column"/>, the first data column by default.
        /// </summary>
        public static ScanningKernel Load(string path, int column = 1)
        {
            var offsets = new List<double>();
            var values = new List<double>();
            bool header = true;
            foreach (string line in File.ReadLines(path))
            {
                if (header)
                {
                    header = false;
                    continue;
                }
                if (line.Length == 0)
                    continue;
                string[] fields = line.Split('\t');
                if (fields.Length <= column)
                    continue;
                offsets.Add(double.Parse(fields[0], CultureInfo.InvariantCulture));
                values.Add(double.TryParse(fields[column], NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
                    ? v : double.NaN);
            }
            return FromSamples(offsets, values);
        }

        /// <summary>Most negative offset with nonzero transmission, in Th.</summary>
        public double MinOffset { get { return _offsets[0]; } }

        /// <summary>Most positive offset with nonzero transmission, in Th.</summary>
        public double MaxOffset { get { return _offsets[_offsets.Length - 1]; } }

        /// <summary>Full width at half maximum, in Th.</summary>
        public double Fwhm
        {
            get
            {
                double lo = double.NaN, hi = double.NaN;
                for (double d = MinOffset; d <= MaxOffset; d += 0.01)
                {
                    if (Evaluate(d) >= 0.5)
                    {
                        if (double.IsNaN(lo))
                            lo = d;
                        hi = d;
                    }
                }
                return hi - lo;
            }
        }

        /// <summary>The transmission at an offset <c>c - m</c>, in Th; zero outside the support.</summary>
        public double Evaluate(double offset)
        {
            if (offset <= _offsets[0] || offset >= _offsets[_offsets.Length - 1])
                return 0;
            int hi = Array.BinarySearch(_offsets, offset);
            if (hi >= 0)
                return _values[hi];
            hi = ~hi;
            int lo = hi - 1;
            double f = (offset - _offsets[lo]) / (_offsets[hi] - _offsets[lo]);
            return _values[lo] + f * (_values[hi] - _values[lo]);
        }

        /// <summary>
        /// A stable description of the kernel for provenance: its support, width and a hash of
        /// its samples.
        /// </summary>
        public string Descriptor
        {
            get
            {
                unchecked
                {
                    long hash = 17;
                    for (int i = 0; i < _offsets.Length; i++)
                    {
                        hash = hash * 31 + BitConverter.DoubleToInt64Bits(_offsets[i]);
                        hash = hash * 31 + BitConverter.DoubleToInt64Bits(_values[i]);
                    }
                    return string.Format(CultureInfo.InvariantCulture,
                        @"kernel=[{0:F2},{1:F2}]Th,fwhm={2:F2},samples={3},hash={4:x16}",
                        MinOffset, MaxOffset, Fwhm, _offsets.Length, hash);
                }
            }
        }
    }
}
