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
using Pwiz.Analysis;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;

namespace pwiz.Osprey.DemuxTool
{
    /// <summary>
    /// A staggered acquisition synthesized from a narrow-window one, so demultiplexing can be scored against the narrow
    /// spectra it should recover: in each cycle, adjacent narrow windows are summed into windows of k, the grouping
    /// shifted by one narrow window per cycle (k-fold staggered, the offset sets in successive cycles).
    /// </summary>
    /// <remarks>
    /// Intensities are rates (an ion trap's, normalized by fill time), so members add as ions: the sum is
    /// sum(intensity x fill time) over the summed fill time, which is the spectrum's injection time. Its time is the
    /// members' mean; MS1 spectra pass through. Members' m/z values that agree within 1e-6 are one point, so profile
    /// spectra on a common grid stay on it.
    /// </remarks>
    internal sealed class RemultiplexSpectrumList : SpectrumListWrapper
    {
        private const double SAME_MZ = 1e-6;

        private readonly List<int[]> _members = new List<int[]>();   // per output: inner indices (one for MS1)
        private readonly List<int> _group = new List<int>();          // per output: group within its cycle, -1 for MS1

        public RemultiplexSpectrumList(ISpectrumList inner, int k, TextWriter log)
            : base(inner)
        {
            var cycle = new List<(int Index, double Center)>();
            int cycles = 0;
            for (int i = 0; i < inner.Count; i++)
            {
                var spectrum = inner.GetSpectrum(i);
                if (spectrum.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0) == 1)
                {
                    cycles += Flush(cycle, cycles, k);
                    _members.Add(new[] { i });
                    _group.Add(-1);
                    continue;
                }
                double center = spectrum.Precursors[0].IsolationWindow.CvParamValueOrDefault(CVID.MS_isolation_window_target_m_z, 0.0);
                if (cycle.Any(c => Math.Abs(c.Center - center) < 1e-3))
                    cycles += Flush(cycle, cycles, k);
                cycle.Add((i, center));
            }
            cycles += Flush(cycle, cycles, k);
            log.WriteLine(@"Remultiplexed {0} cycles into {1}-fold staggered windows: {2} spectra", cycles, k, _members.Count);
        }

        public override int Count
        {
            get { return _members.Count; }
        }

        public override SpectrumIdentity SpectrumIdentity(int index)
        {
            var identity = Inner.SpectrumIdentity(_members[index][0]);
            return new SpectrumIdentity
            {
                Index = index,
                Id = OutputId(identity.Id, index),
                SpotId = identity.SpotId,
                SourceFilePosition = identity.SourceFilePosition,
            };
        }

        public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
        {
            var members = _members[index];
            var first = Inner.GetSpectrum(members[0], getBinaryData);
            first.Index = index;
            first.Id = OutputId(first.Id, index);
            if (_group[index] < 0)
                return first;

            double lower = double.MaxValue, upper = double.MinValue, timeSum = 0, fillSum = 0;
            var spectra = members.Select(m => m == members[0] ? first : Inner.GetSpectrum(m, getBinaryData)).ToArray();
            foreach (var spectrum in spectra)
            {
                var window = spectrum.Precursors[0].IsolationWindow;
                double target = window.CvParamValueOrDefault(CVID.MS_isolation_window_target_m_z, 0.0);
                lower = Math.Min(lower, target - window.CvParamValueOrDefault(CVID.MS_isolation_window_lower_offset, 0.0));
                upper = Math.Max(upper, target + window.CvParamValueOrDefault(CVID.MS_isolation_window_upper_offset, 0.0));
                var scan = spectrum.ScanList.Scans[0];
                timeSum += scan.CvParamValueOrDefault(CVID.MS_scan_start_time, 0.0);
                fillSum += scan.CvParamValueOrDefault(CVID.MS_ion_injection_time, 0.0);
            }
            var isolation = first.Precursors[0].IsolationWindow;
            isolation.Set(CVID.MS_isolation_window_target_m_z, (lower + upper) / 2, CVID.MS_m_z);
            isolation.Set(CVID.MS_isolation_window_lower_offset, (upper - lower) / 2, CVID.MS_m_z);
            isolation.Set(CVID.MS_isolation_window_upper_offset, (upper - lower) / 2, CVID.MS_m_z);
            var firstScan = first.ScanList.Scans[0];
            firstScan.Set(CVID.MS_scan_start_time, timeSum / spectra.Length, CVID.UO_minute);
            firstScan.Set(CVID.MS_ion_injection_time, fillSum, CVID.UO_millisecond);
            if (!getBinaryData)
                return first;

            // Every member's points as ions (intensity x fill time), merged by m/z, back to a rate over the summed fill.
            var points = new List<(double Mz, double Ions)>();
            foreach (var spectrum in spectra)
            {
                double fill = spectrum.ScanList.Scans[0].CvParamValueOrDefault(CVID.MS_ion_injection_time, 0.0);
                var mz = spectrum.GetMZArray()?.Data;
                var intensity = spectrum.GetIntensityArray()?.Data;
                if (mz == null || intensity == null)
                    continue;
                for (int i = 0; i < Math.Min(mz.Count, intensity.Count); i++)
                    points.Add((mz[i], intensity[i] * fill));
            }
            points.Sort((a, b) => a.Mz.CompareTo(b.Mz)); // Array.Sort OK: equal m/z are summed, so their order does not matter
            var outMz = new List<double>();
            var outIntensity = new List<double>();
            foreach (var point in points)
            {
                int last = outMz.Count - 1;
                if (last >= 0 && point.Mz - outMz[last] < SAME_MZ)
                {
                    outIntensity[last] += point.Ions;
                    continue;
                }
                outMz.Add(point.Mz);
                outIntensity.Add(point.Ions);
            }
            for (int i = 0; i < outIntensity.Count; i++)
                outIntensity[i] /= fillSum > 0 ? fillSum : 1;
            first.SetMZIntensityArrays(outMz.ToArray(), outIntensity.ToArray(), CVID.MS_number_of_detector_counts);
            return first;
        }

        /// <summary>
        /// Writes one cycle's narrow MS2 spectra as summed groups of k adjacent windows, the grouping shifted by the cycle
        /// number (so the first group of a cycle holds k - offset windows). Returns 1 if the cycle had spectra.
        /// </summary>
        private int Flush(List<(int Index, double Center)> cycle, int number, int k)
        {
            if (cycle.Count == 0)
                return 0;
            var ordered = cycle.OrderBy(c => c.Center).ToArray();
            int offset = number % k;
            var groups = new SortedDictionary<int, List<int>>();
            for (int j = 0; j < ordered.Length; j++)
            {
                int g = (j + offset) / k;
                if (!groups.TryGetValue(g, out var list))
                {
                    list = new List<int>();
                    groups[g] = list;
                }
                list.Add(ordered[j].Index);
            }
            foreach (var group in groups)
            {
                _members.Add(group.Value.ToArray());
                _group.Add(group.Key);
            }
            cycle.Clear();
            return 1;
        }

        private string OutputId(string id, int index)
        {
            return _group[index] < 0 ? id : id + @" remux=" + _group[index].ToString(CultureInfo.InvariantCulture);
        }
    }
}
