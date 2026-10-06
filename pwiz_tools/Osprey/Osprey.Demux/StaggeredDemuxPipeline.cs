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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Demux
{
    /// <summary>
    /// The stepped staggered scheme's part of <see cref="DemuxPipeline"/> (Orbitrap, Astral; see
    /// <see cref="StaggeredDemuxPipeline"/>): the same fragment channels and weighted per-channel solve as the
    /// scanning demultiplexer, the other windows interpolated to each spectrum's time. Each MS2 spectrum becomes one
    /// spectrum per narrow bin its window covers; MS1 spectra pass through.
    /// </summary>
    internal sealed class StaggeredDemuxPlan : DemuxPlan
    {
        private readonly IDemuxSource _source;
        private readonly ScanningDemuxOptions _options;
        private readonly int[] _ms2Spectra;           // input index of each MS2, in acquisition order
        private readonly double[] _ms2Time;           // its time, in minutes
        private readonly int[] _cycleOfMs2;           // the cycle it belongs to
        private readonly Dictionary<int, int> _ms2OfSpectrum = new Dictionary<int, int>();
        private readonly List<(int Spectrum, int Bin)> _output = new List<(int, int)>();
        private readonly Dictionary<(int, int), int> _outputOf = new Dictionary<(int, int), int>();
        private readonly int _cycles;

        public StaggeredDemuxPlan(IDemuxSource source, ScanningDemuxOptions options, TextWriter log, double minimumBinWidth)
        {
            _source = source;
            _options = options;
            var ms2 = new List<int>();
            var times = new List<double>();
            var windows = new List<IsolationWindow>();
            for (int k = 0; k < source.Count; k++)
            {
                var info = source.Describe(k);
                if (info.MsLevel == 1)
                    continue;
                windows.Add(info.Window);
                ms2.Add(k);
                times.Add(info.Time);
            }
            _ms2Spectra = ms2.ToArray();
            _ms2Time = times.ToArray();
            for (int i = 0; i < _ms2Spectra.Length; i++)
                _ms2OfSpectrum[_ms2Spectra[i]] = i;
            Scheme = DemuxSchemeDetector.Detect(windows, minimumBinWidth);

            // A cycle ends when a window repeats, so a staggered pair of window sets is one cycle
            // and a file without survey scans still has cycles.
            _cycleOfMs2 = new int[_ms2Spectra.Length];
            var seen = new HashSet<int>();
            int cycle = 0;
            for (int i = 0; i < _ms2Spectra.Length; i++)
            {
                if (!seen.Add(Scheme.WindowOfSpectrum[i]))
                {
                    cycle++;
                    seen.Clear();
                    seen.Add(Scheme.WindowOfSpectrum[i]);
                }
                _cycleOfMs2[i] = cycle;
            }
            _cycles = _ms2Spectra.Length > 0 ? cycle + 1 : 0;
            log.WriteLine(@"Scheme: {0}, {1} windows into {2} bins, {3}-fold", Scheme.Kind, Scheme.Windows.Count,
                Scheme.Bins.Count, Scheme.OverlapFactor);

            for (int k = 0; k < source.Count; k++)
            {
                if (!_ms2OfSpectrum.TryGetValue(k, out int i))
                {
                    _output.Add((k, -1));
                    continue;
                }
                var w = Scheme.Windows[Scheme.WindowOfSpectrum[i]];
                for (int b = w.FirstBin; b <= w.LastBin; b++)
                {
                    _outputOf[(k, b)] = _output.Count;
                    _output.Add((k, b));
                }
            }
        }

        public DemuxScheme Scheme { get; }

        public override int OutputCount
        {
            get { return _output.Count; }
        }

        public override int LastCycle
        {
            get { return _cycles - 1; }
        }

        public override int BatchCycles
        {
            get { return Math.Max(1, _options.Threads / 2) * _options.BlockCycles; }
        }

        /// <summary>The acquired spectrum an output spectrum is built on (for a demultiplexed one, its own MS2).</summary>
        public int BaseIndex(int output)
        {
            return _output[output].Spectrum;
        }

        /// <summary>Whether an output spectrum is passed through as read (an MS1 spectrum).</summary>
        public bool IsPassThrough(int output)
        {
            return _output[output].Bin < 0;
        }

        /// <summary>A demultiplexed spectrum's narrow bin.</summary>
        public DemuxBin BinOf(int output)
        {
            return Scheme.Bins[_output[output].Bin];
        }

        /// <summary>A demultiplexed spectrum's place among its window's bins, from 0.</summary>
        public int PartOf(int output)
        {
            var (k, bin) = _output[output];
            return bin - Scheme.Windows[Scheme.WindowOfSpectrum[_ms2OfSpectrum[k]]].FirstBin;
        }

        public override int CycleOfOutput(int output)
        {
            var (k, bin) = _output[output];
            return bin < 0 ? -1 : _cycleOfMs2[_ms2OfSpectrum[k]];
        }

        public override string DescribeBatch(int firstCycle, int lastCycle)
        {
            return string.Format(CultureInfo.InvariantCulture, @"  cycles {0}-{1} of {2} written", firstCycle, lastCycle, _cycles);
        }

        /// <summary>Nothing is kept between batches: each reads its own cycles and context.</summary>
        public override void Forget(int firstCycle)
        {
        }

        /// <summary>Reads every MS2 spectrum of a batch's padded cycles and makes one solve per block and group of bins.</summary>
        public override IReadOnlyList<DemuxWork> MakeWork(int firstCycle, int lastCycle, out double readSeconds)
        {
            var clock = Stopwatch.StartNew();
            int padLo = Math.Max(0, firstCycle - _options.CyclePad), padHi = Math.Min(_cycles - 1, lastCycle + _options.CyclePad);
            // Every MS2 spectrum of the padded cycles, its nonzero peaks in ions.
            var inBlock = Enumerable.Range(0, _ms2Spectra.Length)
                .Where(i => _cycleOfMs2[i] >= padLo && _cycleOfMs2[i] <= padHi).ToArray();
            var peaks = new Dictionary<int, (double[] Mz, double[] Ions)>();
            foreach (int i in inBlock)
                peaks[i] = ReadPeaks(_ms2Spectra[i]);
            readSeconds = clock.Elapsed.TotalSeconds;

            var work = new List<DemuxWork>();
            int binCount = Scheme.Bins.Count, contextBins = Math.Max(2, Scheme.OverlapFactor * 2);
            for (int k0 = firstCycle; k0 <= lastCycle; k0 += _options.BlockCycles)
            {
                int k1 = Math.Min(lastCycle, k0 + _options.BlockCycles - 1);
                int lo = Math.Max(padLo, k0 - _options.CyclePad), hi = Math.Min(padHi, k1 + _options.CyclePad);
                for (int g0 = 0; g0 < binCount; g0 += _options.GroupBins)
                {
                    int g1 = Math.Min(binCount - 1, g0 + _options.GroupBins - 1);
                    var unit = MakeUnit(inBlock, peaks, lo, hi, k0, k1, g0, g1, contextBins);
                    work.Add(new DemuxWork(() => ScanningDemultiplexer.DemuxInterpolatedUnit(unit, _options.Parameters),
                        unit.Mz.Length));
                }
            }
            return work;
        }

        /// <summary>Each MS2 spectrum of the batch as one spectrum per bin of its window.</summary>
        public override void LayOut(int firstCycle, int lastCycle, ScanningUnitResult[] results,
            IDictionary<int, (double[] Mz, double[] Ions)> peaks)
        {
            var demuxed = new Dictionary<(int, int), List<ScanningPeak>>();
            var through = new Dictionary<int, List<ScanningPeak>>();
            foreach (var result in results)
            {
                foreach (var p in result.Demultiplexed)
                    Add(demuxed, (p.Cycle, p.Bin), p);
                foreach (var p in result.PassedThrough)
                    Add(through, p.Cycle, p);
            }
            var none = new List<ScanningPeak>();
            for (int i = 0; i < _ms2Spectra.Length; i++)
            {
                if (_cycleOfMs2[i] < firstCycle || _cycleOfMs2[i] > lastCycle)
                    continue;
                int k = _ms2Spectra[i];
                var w = Scheme.Windows[Scheme.WindowOfSpectrum[i]];
                // A pass-through peak is shared equally among the spectrum's bins.
                var shared = (through.TryGetValue(k, out var list) ? list : none)
                    .Select(p => new ScanningPeak(p.Bin, p.Cycle, p.Mz, p.Ions / w.BinCount)).ToList();
                for (int b = w.FirstBin; b <= w.LastBin; b++)
                {
                    ScanningLayout.Assemble(shared, demuxed.TryGetValue((k, b), out var dem) ? dem : none,
                        out double[] mz, out double[] ions);
                    peaks[_outputOf[(k, b)]] = (mz, ions);
                }
            }
        }

        private InterpolatedUnit MakeUnit(int[] inBlock, Dictionary<int, (double[] Mz, double[] Ions)> peaks, int lo, int hi,
            int k0, int k1, int g0, int g1, int contextBins)
        {
            int col0 = Math.Max(0, g0 - contextBins), col1 = Math.Min(Scheme.Bins.Count - 1, g1 + contextBins);
            var columnBins = Enumerable.Range(col0, col1 - col0 + 1).ToArray();
            // Rows: the windows that cover any of the columns.
            var rowWindows = Enumerable.Range(0, Scheme.Windows.Count)
                .Where(w => Scheme.Windows[w].LastBin >= col0 && Scheme.Windows[w].FirstBin <= col1).ToArray();
            var rowOfWindow = new Dictionary<int, int>();
            for (int r = 0; r < rowWindows.Length; r++)
                rowOfWindow[rowWindows[r]] = r;
            var a = new double[rowWindows.Length, columnBins.Length];
            for (int r = 0; r < rowWindows.Length; r++)
            {
                for (int j = 0; j < columnBins.Length; j++)
                    a[r, j] = Scheme.Windows[rowWindows[r]].Covers(columnBins[j]) ? 1 : 0;
            }

            var acquisitions = rowWindows.Select(w => new List<int>()).ToArray();
            foreach (int i in inBlock)
            {
                if (_cycleOfMs2[i] < lo || _cycleOfMs2[i] > hi)
                    continue;
                if (rowOfWindow.TryGetValue(Scheme.WindowOfSpectrum[i], out int r))
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
                var w = Scheme.Windows[rowWindows[r]];
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
            _source.Read(k, out var mzData, out var intensityData);
            var mzs = new List<double>();
            var values = new List<double>();
            if (mzData != null && intensityData != null)
            {
                int count = Math.Min(mzData.Count, intensityData.Count);
                for (int i = 0; i < count; i++)
                {
                    if (intensityData[i] <= 0)
                        continue;
                    mzs.Add(mzData[i]);
                    values.Add(intensityData[i] / _options.CountsPerIon);
                }
            }
            _source.Release(k);
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

    /// <summary>
    /// Demultiplexes a stepped staggered acquisition (Orbitrap, Astral) on the shared <see cref="DemuxPipeline"/>
    /// with the same fragment channels and weighted per-channel solve as the scanning demultiplexer, the other
    /// windows interpolated to each spectrum's time. Each MS2 spectrum becomes one spectrum per narrow bin its
    /// window covers, under that bin's window, as msconvert's demultiplex filter writes them; MS1 spectra pass
    /// through.
    /// </summary>
    public sealed class StaggeredDemuxPipeline : DemuxPipeline
    {
        private readonly StaggeredDemuxPlan _staggered;

        /// <param name="source">The acquisition.</param>
        /// <param name="options">The solve's settings.</param>
        /// <param name="log">Where the scheme and each batch are logged.</param>
        /// <param name="minimumBinWidth">Window boundaries closer than this (Th) are one boundary.</param>
        public StaggeredDemuxPipeline(IDemuxSource source, ScanningDemuxOptions options, TextWriter log,
            double minimumBinWidth = DemuxSchemeDetector.DEFAULT_MINIMUM_BIN_WIDTH)
            : this(new StaggeredDemuxPlan(source, options, log, minimumBinWidth), options, log)
        {
        }

        private StaggeredDemuxPipeline(StaggeredDemuxPlan plan, ScanningDemuxOptions options, TextWriter log)
            : base(plan, options, log)
        {
            _staggered = plan;
        }

        public DemuxScheme Scheme
        {
            get { return _staggered.Scheme; }
        }

        /// <summary>The acquired spectrum an output spectrum is built on (for a demultiplexed one, its own MS2).</summary>
        public int BaseIndex(int index)
        {
            return _staggered.BaseIndex(index);
        }

        /// <summary>Whether an output spectrum is passed through as read (an MS1 spectrum).</summary>
        public bool IsPassThrough(int index)
        {
            return _staggered.IsPassThrough(index);
        }

        /// <summary>A demultiplexed spectrum's narrow bin: its isolation window.</summary>
        public DemuxBin BinOf(int index)
        {
            return _staggered.BinOf(index);
        }

        /// <summary>A demultiplexed spectrum's place among its window's bins, from 0 (its id's demux= suffix).</summary>
        public int PartOf(int index)
        {
            return _staggered.PartOf(index);
        }
    }
}
