/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on skyline-glimpse (https://github.com/maccoss/skyline-glimpse)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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
using System.IO.Compression;
using System.Linq;
using System.Threading;
using pwiz.Quasar.Features;
using pwiz.Quasar.Features.Data;
using pwiz.Quasar.Features.Scans;

namespace pwiz.Quasar.Test
{
    /// <summary>
    /// An in-memory run: spectra built in the test (skyline-glimpse's FakeSource, without the reader interface
    /// Quasar does not have).
    /// </summary>
    internal sealed class FakeRun
    {
        private readonly List<SpectrumData> _spectra = new List<SpectrumData>();

        public IReadOnlyList<SpectrumData> Spectra => _spectra;

        public FakeRun Ms1(double rt, double[] mz, double[] intensity, float injectionMs = 10f, MassAnalyzer analyzer = MassAnalyzer.unknown)
        {
            _spectra.Add(WithAnalyzer(Spectrum(_spectra.Count, 1, rt, mz, intensity, injectionMs, double.NaN, double.NaN), analyzer));
            return this;
        }

        public FakeRun Ms2(double rt, double lower, double upper, double[] mz, double[] intensity, float injectionMs = 10f,
            MassAnalyzer analyzer = MassAnalyzer.unknown)
        {
            _spectra.Add(WithAnalyzer(Spectrum(_spectra.Count, 2, rt, mz, intensity, injectionMs, lower, upper), analyzer));
            return this;
        }

        public SpectrumData ReadSpectrum(int index) => _spectra[index];

        public static SpectrumData Spectrum(int index, int level, double rt, double[] mz, double[] intensity, float injectionMs, double lower, double upper)
        {
            var tic = intensity.Sum();
            var bp = intensity.Length == 0 ? -1 : Array.IndexOf(intensity, intensity.Max());
            var header = new ScanHeader(index, (byte)level, rt, injectionMs, (lower + upper) / 2, lower, upper, tic,
                bp < 0 ? double.NaN : mz[bp], bp < 0 ? 0 : (float)intensity[bp], mz.Length, Polarity.positive, true);
            return new SpectrumData(header, mz, intensity, new SpectrumDetails { NativeId = @"scan=" + (index + 1), ScanNumber = index + 1 });
        }

        private static SpectrumData WithAnalyzer(SpectrumData s, MassAnalyzer analyzer) =>
            analyzer == MassAnalyzer.unknown ? s : new SpectrumData(s.Header with { Analyzer = analyzer }, s.Mz, s.Intensity, s.Details);
    }

    /// <summary>
    /// The one sequential pass over a run, as skyline-glimpse's ScanIndexBuilder.Build drives its observers with
    /// a single reader: Begin with the first and last retention times, every spectrum in order, then End.
    /// </summary>
    internal static class ScanPass
    {
        /// <summary>Passes <paramref name="spectra"/> to <paramref name="observers"/>; returns their headers.</summary>
        public static ScanHeader[] Run(IReadOnlyList<SpectrumData> spectra, params IScanObserver[] observers)
        {
            var count = spectra.Count;
            var headers = new ScanHeader[count];
            var rtFirst = count > 0 ? spectra[0].Header.RetentionTime : double.NaN;
            var rtLast = count > 0 ? spectra[count - 1].Header.RetentionTime : double.NaN;
            foreach (var o in observers)
                o.Begin(count, rtFirst, rtLast);
            for (var i = 0; i < count; i++)
            {
                headers[i] = spectra[i].Header;
                foreach (var o in observers)
                    o.Observe(spectra[i]);
            }
            foreach (var o in observers)
                o.End();
            return headers;
        }

        /// <summary>Indices of the scans at <paramref name="level"/>, ascending (Glimpse's ScanIndex.IndicesOfLevel).</summary>
        public static int[] IndicesOfLevel(IReadOnlyList<ScanHeader> headers, int level) =>
            Enumerable.Range(0, headers.Count).Where(i => headers[i].MsLevel == level).ToArray();
    }

    /// <summary>
    /// Finds an envelope wherever a spectrum has a peak at one of the given m/z values: that peak and the next
    /// <c>Count - 1</c> peaks, at the given charge.
    /// </summary>
    internal sealed class PatternEnvelopeFinder : IEnvelopeFinder
    {
        private readonly (double FirstMz, int Charge, int Count)[] _patterns;
        private int _calls;

        public PatternEnvelopeFinder(params (double FirstMz, int Charge, int Count)[] patterns)
        {
            _patterns = patterns;
        }

        public int Calls => _calls;

        /// <summary>Analyzers it refuses, as the real finder refuses the ion trap.</summary>
        public MassAnalyzer[] Refused { get; init; } = Array.Empty<MassAnalyzer>();

        public bool CanSearch(MassAnalyzer analyzer) => !Refused.Contains(analyzer);

        public IReadOnlyList<IsotopeEnvelope> Find(double[] mz, double[] intensity, MassAnalyzer analyzer)
        {
            Interlocked.Increment(ref _calls);
            var found = new List<IsotopeEnvelope>();
            foreach (var (first, charge, count) in _patterns)
            {
                var i = Array.FindIndex(mz, m => Math.Abs(m - first) < 0.001);
                if (i < 0 || i + count > mz.Length)
                    continue;
                var peaks = Enumerable.Range(i, count).ToArray();
                found.Add(new IsotopeEnvelope((first - 1.00727649) * charge, charge, (float)peaks.Sum(p => intensity[p]),
                    mz[i], 0.99, mz[i], mz[i + count - 1], peaks));
            }
            return found;
        }
    }

    /// <summary>
    /// pwiz's example_data/small.RAW (LTQ-FT, 48 spectra), every spectrum as skyline-glimpse's
    /// PwizMsDataSource.ReadSpectrum(i, centroid: true) reads it: TestData/small-raw-centroided.tsv.gz
    /// (see TestData/Hardklor/README.md). Quasar has no raw-file reader yet, so the tests that ran on
    /// small.RAW in Glimpse run on this.
    /// </summary>
    internal static class SmallRaw
    {
        private static readonly Lazy<SpectrumData[]> SPECTRA = new Lazy<SpectrumData[]>(Load);

        /// <summary>Every spectrum of the run, in file order.</summary>
        public static IReadOnlyList<SpectrumData> Spectra => SPECTRA.Value;

        /// <summary>The headers of every spectrum.</summary>
        public static ScanHeader[] Headers => Spectra.Select(s => s.Header).ToArray();

        /// <summary>The MS1 spectra with their scan numbers: what Hardklor.exe read from msconvert's mzML.</summary>
        public static List<(int Scan, double[] Mz, double[] Intensity)> Ms1() =>
            Spectra.Where(s => s.Header.MsLevel == 1).Select(s => (s.Details.ScanNumber ?? -1, s.Mz, s.Intensity)).ToList();

        /// <summary>A file in the test data folder copied beside the test assembly.</summary>
        public static string TestDataPath(params string[] parts) =>
            Path.Combine(new[] { AppContext.BaseDirectory, @"TestData" }.Concat(parts).ToArray());

        private static SpectrumData[] Load()
        {
            var inv = CultureInfo.InvariantCulture;
            var spectra = new List<SpectrumData>();
            using (var file = File.OpenRead(TestDataPath(@"small-raw-centroided.tsv.gz")))
            using (var gzip = new GZipStream(file, CompressionMode.Decompress))
            using (var reader = new StreamReader(gzip))
            {
                string line;
                var haveHeader = false;
                ScanHeader header = default;
                var scanNumber = -1;
                var mz = new List<double>();
                var intensity = new List<double>();
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith(@"#"))
                        continue;
                    var f = line.Split('\t');
                    if (f[0] == @"S")
                    {
                        if (haveHeader)
                            spectra.Add(Spectrum(header, scanNumber, mz, intensity));
                        haveHeader = true;
                        header = new ScanHeader(int.Parse(f[1], inv), byte.Parse(f[3], inv), double.Parse(f[4], inv), float.Parse(f[5], inv),
                            double.Parse(f[6], inv), double.Parse(f[7], inv), double.Parse(f[8], inv), double.Parse(f[9], inv),
                            double.Parse(f[10], inv), float.Parse(f[11], inv), int.Parse(f[12], inv), (Polarity)int.Parse(f[13], inv),
                            f[14] == @"1", (MassAnalyzer)int.Parse(f[15], inv));
                        scanNumber = int.Parse(f[2], inv);
                        mz.Clear();
                        intensity.Clear();
                    }
                    else
                    {
                        mz.Add(double.Parse(f[0], inv));
                        intensity.Add(double.Parse(f[1], inv));
                    }
                }
                if (haveHeader)
                    spectra.Add(Spectrum(header, scanNumber, mz, intensity));
            }
            return spectra.ToArray();
        }

        private static SpectrumData Spectrum(ScanHeader header, int scanNumber, List<double> mz, List<double> intensity)
        {
            if (header.PeakCount != mz.Count)
                throw new InvalidDataException(@"Peak count mismatch in spectrum " + header.Index);
            return new SpectrumData(header, mz.ToArray(), intensity.ToArray(),
                new SpectrumDetails { ScanNumber = scanNumber < 0 ? null : scanNumber });
        }
    }
}
