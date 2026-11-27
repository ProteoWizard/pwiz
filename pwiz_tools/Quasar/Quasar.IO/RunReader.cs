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
using System.Linq;
using pwiz.Osprey.IO;
using pwiz.ProteowizardWrapper;
using pwiz.Quasar.Features.Data;

namespace pwiz.Quasar.IO
{
    /// <summary>The MS2 scans of one isolation window, in time order.</summary>
    public sealed class WindowScans
    {
        public WindowScans(int index, double target, double lowerOffset, double upperOffset,
            IReadOnlyList<SpectrumData> scans)
        {
            Index = index;
            Target = target;
            LowerOffset = lowerOffset;
            UpperOffset = upperOffset;
            Scans = scans;
        }

        /// <summary>Position of the window in increasing target m/z.</summary>
        public int Index { get; }

        public double Target { get; }
        public double LowerOffset { get; }
        public double UpperOffset { get; }
        public IReadOnlyList<SpectrumData> Scans { get; }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, @"Window[{0}, {1:F3} -{2:F3}/+{3:F3}, {4} scans]",
                Index, Target, LowerOffset, UpperOffset, Scans.Count);
        }
    }

    /// <summary>One run's spectra: MS1 scans and MS2 scans grouped by isolation window.</summary>
    public sealed class RunData
    {
        public RunData(string sourcePath, string instrumentModel, IReadOnlyList<SpectrumData> ms1,
            IReadOnlyList<WindowScans> windows)
        {
            SourcePath = sourcePath;
            InstrumentModel = instrumentModel;
            Ms1 = ms1;
            Windows = windows;
        }

        public string SourcePath { get; }

        /// <summary>The instrument model the file declares, or null.</summary>
        public string InstrumentModel { get; }

        public IReadOnlyList<SpectrumData> Ms1 { get; }
        public IReadOnlyList<WindowScans> Windows { get; }
    }

    /// <summary>
    /// Reads a run, vendor raw file or mzML, through ProteoWizard as Osprey does (vendor
    /// centroiding for raw files, SIM scans as spectra, ion mobility not combined), and keeps
    /// what Quasar's Stage 1 needs and Osprey's reader drops: each scan's injection time and the
    /// analyzer that recorded it, for the ion conversion and Hardklor's peak width.
    /// </summary>
    public static class RunReader
    {
        /// <summary>Window targets closer than this are the same window (staggered schemes are demultiplexed first).</summary>
        private const double WINDOW_TARGET_TOLERANCE = 0.01;

        /// <summary>Spectra read from the start of a run to learn its isolation scheme: several DIA cycles.</summary>
        private const int SCHEME_SPECTRA = 2000;

        /// <summary>
        /// The run's isolation window targets, in increasing m/z, from its first
        /// <see cref="SCHEME_SPECTRA"/> spectra. Window indices everywhere else follow this order.
        /// </summary>
        public static IReadOnlyList<double> ReadWindowTargets(string path)
        {
            var targets = new List<double>();
            using (var msData = Open(path))
            {
                int count = Math.Min(msData.SpectrumCount, SCHEME_SPECTRA);
                for (int i = 0; i < count; i++)
                {
                    var spectrum = msData.GetSpectrum(i);
                    if (spectrum == null || spectrum.Level != 2 || spectrum.Precursors.Count == 0)
                        continue;
                    var target = spectrum.Precursors[0].IsolationWindowTargetMz;
                    if (target.HasValue && targets.All(t => Math.Abs(t - target.Value) >= WINDOW_TARGET_TOLERANCE))
                        targets.Add(target.Value);
                }
            }

            targets.Sort();
            return targets;
        }

        /// <summary>
        /// Reads the MS2 spectra of <paramref name="path"/> whose isolation target
        /// <paramref name="keepTarget"/> accepts (null for all), and its MS1 spectra when
        /// <paramref name="keepMs1"/> is true. Spectra not kept are never held, which is most of
        /// the memory when only a few windows are processed. <paramref name="progress"/> receives
        /// the number of spectra read so far and the total.
        /// </summary>
        public static RunData Read(string path, Func<double, bool> keepTarget = null, bool keepMs1 = true,
            Action<int, int> progress = null)
        {
            var ms1 = new List<SpectrumData>();
            var ms2 = new List<(double Target, double Lower, double Upper, SpectrumData Data)>();
            string model = null;
            using (var msData = Open(path))
            {
                model = msData.GetInstrumentConfigInfoList().Select(i => i.Model).FirstOrDefault(m => !string.IsNullOrEmpty(m));
                int count = msData.SpectrumCount;
                for (int i = 0; i < count; i++)
                {
                    var spectrum = msData.GetSpectrum(i);
                    progress?.Invoke(i + 1, count);
                    if (spectrum?.Mzs == null || spectrum.Intensities == null || spectrum.Mzs.Length != spectrum.Intensities.Length)
                        continue;
                    if (spectrum.Level == 1)
                    {
                        if (keepMs1)
                            ms1.Add(ToSpectrumData(spectrum, i, double.NaN, double.NaN, double.NaN));
                        continue;
                    }

                    if (spectrum.Level != 2 || spectrum.Precursors.Count == 0)
                        continue;
                    var precursor = spectrum.Precursors[0];
                    if (!precursor.IsolationWindowTargetMz.HasValue)
                        continue;
                    double target = precursor.IsolationWindowTargetMz.Value;
                    if (keepTarget != null && !keepTarget(target))
                        continue;
                    double lower = precursor.IsolationWindowLower ?? 0.0;
                    double upper = precursor.IsolationWindowUpper ?? 0.0;
                    ms2.Add((target, lower, upper, ToSpectrumData(spectrum, i, target, lower, upper)));
                }
            }

            return new RunData(path, model, ms1, GroupByWindow(ms2));
        }

        /// <summary>
        /// Opens a run the way Osprey reads one: vendor centroiding for raw files, SIM scans as
        /// spectra, ion mobility not combined.
        /// </summary>
        private static MsDataFileImpl Open(string path)
        {
            bool vendorFormat = SpectrumFileReader.IsVendorFormat(path);
            return new MsDataFileImpl(path,
                simAsSpectra: true,
                requireVendorCentroidedMS1: vendorFormat,
                requireVendorCentroidedMS2: vendorFormat,
                combineIonMobilitySpectra: false);
        }

        /// <summary>The analyzer an instrument configuration names: Astral, Orbitrap or ion trap.</summary>
        public static MassAnalyzer AnalyzerOf(string analyzerName)
        {
            if (string.IsNullOrEmpty(analyzerName))
                return MassAnalyzer.unknown;
            string name = analyzerName.ToLowerInvariant();
            if (name.Contains(@"astral"))
                return MassAnalyzer.astral;
            if (name.Contains(@"orbitrap"))
                return MassAnalyzer.orbitrap;
            if (name.Contains(@"ion trap") || name.Contains(@"iontrap"))
                return MassAnalyzer.ion_trap;
            return MassAnalyzer.other;
        }

        private static SpectrumData ToSpectrumData(MsDataSpectrum spectrum, int index, double target, double lower,
            double upper)
        {
            var mzs = spectrum.Mzs;
            var intensities = spectrum.Intensities;
            if (!IsSorted(mzs))
            {
                var order = Enumerable.Range(0, mzs.Length).OrderBy(i => mzs[i]).ToArray();
                mzs = order.Select(i => spectrum.Mzs[i]).ToArray();
                intensities = order.Select(i => spectrum.Intensities[i]).ToArray();
            }

            int basePeak = 0;
            double tic = 0.0;
            for (int i = 0; i < intensities.Length; i++)
            {
                tic += intensities[i];
                if (intensities[i] > intensities[basePeak])
                    basePeak = i;
            }

            double? injectionTime = spectrum.Metadata?.InjectionTime;
            var header = new ScanHeader(index, (byte)spectrum.Level, spectrum.RetentionTime ?? 0.0,
                injectionTime.HasValue ? (float)injectionTime.Value : float.NaN,
                target, double.IsNaN(target) ? double.NaN : target - lower, double.IsNaN(target) ? double.NaN : target + upper,
                tic, mzs.Length > 0 ? mzs[basePeak] : 0.0, mzs.Length > 0 ? (float)intensities[basePeak] : 0f,
                mzs.Length, spectrum.NegativeCharge ? Polarity.negative : Polarity.positive, spectrum.Centroided,
                AnalyzerOf(spectrum.InstrumentInfo?.Analyzer));
            return new SpectrumData(header, mzs, intensities, new SpectrumDetails { NativeId = spectrum.Id });
        }

        private static bool IsSorted(double[] values)
        {
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] < values[i - 1])
                    return false;
            }

            return true;
        }

        private static List<WindowScans> GroupByWindow(List<(double Target, double Lower, double Upper, SpectrumData Data)> ms2)
        {
            var windows = new List<WindowScans>();
            var byTarget = ms2.OrderBy(s => s.Target).ToList();
            int start = 0;
            while (start < byTarget.Count)
            {
                int end = start + 1;
                while (end < byTarget.Count && byTarget[end].Target - byTarget[start].Target < WINDOW_TARGET_TOLERANCE)
                    end++;
                var group = byTarget.GetRange(start, end - start);
                var first = group[0];
                var scans = group.Select(s => s.Data).OrderBy(d => d.Header.RetentionTime).ToList();
                windows.Add(new WindowScans(windows.Count, first.Target, first.Lower, first.Upper, scans));
                start = end;
            }

            return windows;
        }
    }
}
