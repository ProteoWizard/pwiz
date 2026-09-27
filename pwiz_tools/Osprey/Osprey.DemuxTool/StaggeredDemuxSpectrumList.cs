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
using System.Threading.Tasks;
using Pwiz.Analysis;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;
using pwiz.Osprey.Demux;
using IsolationWindow = pwiz.Osprey.Core.IsolationWindow;

namespace pwiz.Osprey.DemuxTool
{
    /// <summary>
    /// A spectrum list that demultiplexes a stepped staggered acquisition (Orbitrap, Astral) with
    /// the same fragment channels and weighted per-channel solve as the scanning demultiplexer,
    /// the other windows interpolated to each spectrum's time. Each MS2 spectrum becomes one
    /// spectrum per narrow bin its window covers, under that bin's window, as msconvert's
    /// demultiplex filter writes them; MS1 spectra pass through.
    /// </summary>
    internal sealed class StaggeredDemuxSpectrumList : SpectrumListWrapper
    {
        private readonly ScanningDemuxOptions _options;
        private readonly TextWriter _log;
        private readonly DemuxScheme _scheme;
        private readonly int[] _ms2Spectra;           // input index of each MS2, in acquisition order
        private readonly double[] _ms2Time;           // its time, in minutes
        private readonly int[] _cycleOfMs2;           // the survey cycle it follows
        private readonly Dictionary<int, int> _ms2OfSpectrum = new Dictionary<int, int>();
        private readonly List<(int Spectrum, int Bin)> _output = new List<(int, int)>();
        private readonly Dictionary<(int, int), (double[] Mz, double[] Ions)> _built = new Dictionary<(int, int), (double[], double[])>();
        private readonly int _cycles;
        private readonly object _lock = new object();

        public StaggeredDemuxSpectrumList(ISpectrumList inner, ScanningDemuxOptions options, TextWriter log)
            : base(inner)
        {
            _options = options;
            _log = log;
            var ms2 = new List<int>();
            var times = new List<double>();
            var windows = new List<IsolationWindow>();
            for (int k = 0; k < inner.Count; k++)
            {
                var spectrum = inner.GetSpectrum(k);
                if (spectrum.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0) == 1)
                    continue;
                var window = spectrum.Precursors[0].IsolationWindow;
                windows.Add(new IsolationWindow(
                    window.CvParamValueOrDefault(CVID.MS_isolation_window_target_m_z, 0.0),
                    window.CvParamValueOrDefault(CVID.MS_isolation_window_lower_offset, 0.0),
                    window.CvParamValueOrDefault(CVID.MS_isolation_window_upper_offset, 0.0)));
                ms2.Add(k);
                times.Add(spectrum.ScanList.Scans[0].CvParamValueOrDefault(CVID.MS_scan_start_time, 0.0));
            }
            _ms2Spectra = ms2.ToArray();
            _ms2Time = times.ToArray();
            for (int i = 0; i < _ms2Spectra.Length; i++)
                _ms2OfSpectrum[_ms2Spectra[i]] = i;
            _scheme = DemuxSchemeDetector.Detect(windows);

            // A cycle ends when a window repeats, so a staggered pair of window sets is one cycle
            // and a file without survey scans still has cycles.
            _cycleOfMs2 = new int[_ms2Spectra.Length];
            var seen = new HashSet<int>();
            int cycle = 0;
            for (int i = 0; i < _ms2Spectra.Length; i++)
            {
                if (!seen.Add(_scheme.WindowOfSpectrum[i]))
                {
                    cycle++;
                    seen.Clear();
                    seen.Add(_scheme.WindowOfSpectrum[i]);
                }
                _cycleOfMs2[i] = cycle;
            }
            _cycles = _ms2Spectra.Length > 0 ? cycle + 1 : 0;
            _log.WriteLine(@"Scheme: {0}, {1} windows into {2} bins, {3}-fold", _scheme.Kind, _scheme.Windows.Count,
                _scheme.Bins.Count, _scheme.OverlapFactor);

            for (int k = 0; k < inner.Count; k++)
            {
                if (!_ms2OfSpectrum.TryGetValue(k, out int i))
                {
                    _output.Add((k, -1));
                    continue;
                }
                var w = _scheme.Windows[_scheme.WindowOfSpectrum[i]];
                for (int b = w.FirstBin; b <= w.LastBin; b++)
                    _output.Add((k, b));
            }
        }

        public long Channels { get; private set; }
        public long ChannelsSolved { get; private set; }
        public double IonsIn { get; private set; }
        public double IonsPassedThrough { get; private set; }

        public override int Count
        {
            get { return _output.Count; }
        }

        public override SpectrumIdentity SpectrumIdentity(int index)
        {
            var (k, bin) = _output[index];
            var identity = Inner.SpectrumIdentity(k);
            return new SpectrumIdentity
            {
                Index = index,
                Id = OutputId(identity.Id, k, bin),
                SpotId = identity.SpotId,
                SourceFilePosition = identity.SourceFilePosition,
            };
        }

        public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
        {
            var (k, bin) = _output[index];
            // A demultiplexed spectrum's peaks are replaced, so only a pass-through reads the acquired peaks.
            var spectrum = Inner.GetSpectrum(k, getBinaryData && bin < 0);
            spectrum.Index = index;
            if (bin < 0)
                return spectrum;
            spectrum.Id = OutputId(spectrum.Id, k, bin);
            var demuxBin = _scheme.Bins[bin];
            var window = spectrum.Precursors[0].IsolationWindow;
            window.Set(CVID.MS_isolation_window_target_m_z, demuxBin.Center, CVID.MS_m_z);
            window.Set(CVID.MS_isolation_window_lower_offset, demuxBin.Width / 2, CVID.MS_m_z);
            window.Set(CVID.MS_isolation_window_upper_offset, demuxBin.Width / 2, CVID.MS_m_z);
            if (!getBinaryData)
                return spectrum;
            var (mz, ions) = Built(k, bin);
            var counts = new double[ions.Length];
            for (int i = 0; i < ions.Length; i++)
                counts[i] = ions[i] * _options.CountsPerIon;
            spectrum.SetMZIntensityArrays(mz, counts, CVID.MS_number_of_detector_counts);
            return spectrum;
        }

        private string OutputId(string id, int k, int bin)
        {
            if (bin < 0)
                return id;
            var w = _scheme.Windows[_scheme.WindowOfSpectrum[_ms2OfSpectrum[k]]];
            return id + @" demux=" + (bin - w.FirstBin).ToString(CultureInfo.InvariantCulture);
        }

        private (double[] Mz, double[] Ions) Built(int k, int bin)
        {
            lock (_lock)
            {
                if (_built.TryGetValue((k, bin), out var cached))
                    return cached;
                int cycle = _cycleOfMs2[_ms2OfSpectrum[k]];
                foreach (var old in _built.Keys.Where(key => _cycleOfMs2[_ms2OfSpectrum[key.Item1]] < cycle).ToList())
                    _built.Remove(old);
                int blocksPerBatch = Math.Max(1, _options.Threads / 2);
                int last = Math.Min(_cycles - 1, cycle + blocksPerBatch * _options.BlockCycles - 1);
                BuildDemux(cycle, last);
                _log.WriteLine(@"  cycles {0}-{1} of {2} written", cycle, last, _cycles);
                return _built[(k, bin)];
            }
        }

        private void BuildDemux(int firstCycle, int lastCycle)
        {
            int padLo = Math.Max(0, firstCycle - _options.CyclePad), padHi = Math.Min(_cycles - 1, lastCycle + _options.CyclePad);
            // Every MS2 spectrum of the padded cycles, its nonzero peaks in ions.
            var inBlock = Enumerable.Range(0, _ms2Spectra.Length)
                .Where(i => _cycleOfMs2[i] >= padLo && _cycleOfMs2[i] <= padHi).ToArray();
            var peaks = new Dictionary<int, (double[] Mz, double[] Ions)>();
            foreach (int i in inBlock)
                peaks[i] = ReadPeaks(_ms2Spectra[i]);

            var units = new List<InterpolatedUnit>();
            int binCount = _scheme.Bins.Count, contextBins = Math.Max(2, _scheme.OverlapFactor * 2);
            for (int k0 = firstCycle; k0 <= lastCycle; k0 += _options.BlockCycles)
            {
                int k1 = Math.Min(lastCycle, k0 + _options.BlockCycles - 1);
                int lo = Math.Max(padLo, k0 - _options.CyclePad), hi = Math.Min(padHi, k1 + _options.CyclePad);
                for (int g0 = 0; g0 < binCount; g0 += _options.GroupBins)
                {
                    int g1 = Math.Min(binCount - 1, g0 + _options.GroupBins - 1);
                    units.Add(MakeUnit(inBlock, peaks, lo, hi, k0, k1, g0, g1, contextBins));
                }
            }

            var results = new ScanningUnitResult[units.Count];
            Parallel.For(0, units.Count, new ParallelOptions { MaxDegreeOfParallelism = _options.Threads },
                i => results[i] = ScanningDemultiplexer.DemuxInterpolatedUnit(units[i], _options.Parameters));

            var demuxed = new Dictionary<(int, int), List<ScanningPeak>>();
            var through = new Dictionary<int, List<ScanningPeak>>();
            foreach (var result in results)
            {
                Channels += result.Channels;
                ChannelsSolved += result.ChannelsSolved;
                IonsIn += result.IonsIn;
                IonsPassedThrough += result.IonsPassedThrough;
                foreach (var p in result.Demultiplexed)
                    Add(demuxed, (p.Cycle, p.Bin), p);
                foreach (var p in result.PassedThrough)
                    Add(through, p.Cycle, p);
            }
            var none = new List<ScanningPeak>();
            foreach (int i in inBlock.Where(i => _cycleOfMs2[i] >= firstCycle && _cycleOfMs2[i] <= lastCycle))
            {
                int k = _ms2Spectra[i];
                var w = _scheme.Windows[_scheme.WindowOfSpectrum[i]];
                // A pass-through peak is shared equally among the spectrum's bins.
                var shared = (through.TryGetValue(k, out var list) ? list : none)
                    .Select(p => new ScanningPeak(p.Bin, p.Cycle, p.Mz, p.Ions / w.BinCount)).ToList();
                for (int b = w.FirstBin; b <= w.LastBin; b++)
                {
                    ScanningLayout.Assemble(shared, demuxed.TryGetValue((k, b), out var dem) ? dem : none,
                        out double[] mz, out double[] ions);
                    _built[(k, b)] = (mz, ions);
                }
            }
        }

        private InterpolatedUnit MakeUnit(int[] inBlock, Dictionary<int, (double[] Mz, double[] Ions)> peaks, int lo, int hi,
            int k0, int k1, int g0, int g1, int contextBins)
        {
            int col0 = Math.Max(0, g0 - contextBins), col1 = Math.Min(_scheme.Bins.Count - 1, g1 + contextBins);
            var columnBins = Enumerable.Range(col0, col1 - col0 + 1).ToArray();
            // Rows: the windows that cover any of the columns.
            var rowWindows = Enumerable.Range(0, _scheme.Windows.Count)
                .Where(w => _scheme.Windows[w].LastBin >= col0 && _scheme.Windows[w].FirstBin <= col1).ToArray();
            var rowOfWindow = new Dictionary<int, int>();
            for (int r = 0; r < rowWindows.Length; r++)
                rowOfWindow[rowWindows[r]] = r;
            var a = new double[rowWindows.Length, columnBins.Length];
            for (int r = 0; r < rowWindows.Length; r++)
            {
                for (int j = 0; j < columnBins.Length; j++)
                    a[r, j] = _scheme.Windows[rowWindows[r]].Covers(columnBins[j]) ? 1 : 0;
            }

            var acquisitions = rowWindows.Select(w => new List<int>()).ToArray();
            foreach (int i in inBlock)
            {
                if (_cycleOfMs2[i] < lo || _cycleOfMs2[i] > hi)
                    continue;
                if (rowOfWindow.TryGetValue(_scheme.WindowOfSpectrum[i], out int r))
                    acquisitions[r].Add(i);
            }
            var rowTimes = acquisitions.Select(list => list.Select(i => _ms2Time[i]).ToArray()).ToArray();
            var rowSpectra = acquisitions.Select(list => list.Select(i => _ms2Spectra[i]).ToArray()).ToArray();

            var outputRow = new List<int>();
            var outputAcquisition = new List<int>();
            var outputTime = new List<double>();
            var outputSpectrum = new List<int>();
            var mz = new List<double>();
            var ions = new List<double>();
            var row = new List<int>();
            var acquisition = new List<int>();
            for (int r = 0; r < rowWindows.Length; r++)
            {
                var w = _scheme.Windows[rowWindows[r]];
                bool coversCore = w.LastBin >= g0 && w.FirstBin <= g1;
                for (int q = 0; q < acquisitions[r].Count; q++)
                {
                    int i = acquisitions[r][q];
                    if (coversCore && _cycleOfMs2[i] >= k0 && _cycleOfMs2[i] <= k1)
                    {
                        outputRow.Add(r);
                        outputAcquisition.Add(q);
                        outputTime.Add(_ms2Time[i]);
                        outputSpectrum.Add(_ms2Spectra[i]);
                    }
                    var (m, v) = peaks[i];
                    for (int p = 0; p < m.Length; p++)
                    {
                        mz.Add(m[p]);
                        ions.Add(v[p]);
                        row.Add(r);
                        acquisition.Add(q);
                    }
                }
            }
            return new InterpolatedUnit(a, columnBins, rowTimes, rowSpectra, outputRow.ToArray(), outputAcquisition.ToArray(),
                outputTime.ToArray(), outputSpectrum.ToArray(), g0, g1)
            {
                Mz = mz.ToArray(),
                Ions = ions.ToArray(),
                Row = row.ToArray(),
                Acquisition = acquisition.ToArray(),
            };
        }

        private (double[] Mz, double[] Ions) ReadPeaks(int k)
        {
            var spectrum = Inner.GetSpectrum(k, true);
            var mzArray = spectrum.GetMZArray();
            var intensityArray = spectrum.GetIntensityArray();
            var mzs = new List<double>();
            var values = new List<double>();
            if (mzArray != null && intensityArray != null)
            {
                int count = Math.Min(mzArray.Data.Count, intensityArray.Data.Count);
                for (int i = 0; i < count; i++)
                {
                    if (intensityArray.Data[i] <= 0)
                        continue;
                    mzs.Add(mzArray.Data[i]);
                    values.Add(intensityArray.Data[i] / _options.CountsPerIon);
                }
            }
            return (mzs.ToArray(), values.ToArray());
        }

        private static void Add<TKey>(Dictionary<TKey, List<ScanningPeak>> byKey, TKey key, ScanningPeak peak)
        {
            if (!byKey.TryGetValue(key, out var list))
            {
                list = new List<ScanningPeak>();
                byKey[key] = list;
            }
            list.Add(peak);
        }
    }
}
