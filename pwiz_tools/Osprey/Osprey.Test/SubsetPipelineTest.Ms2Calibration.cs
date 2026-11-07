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
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Scoring;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    public partial class SubsetPipelineTest
    {
        private const string MZML_NAMESPACE = @"http://psi.hupo.org/ms/mzml";
        private const string CV_MS_LEVEL = @"MS:1000511";
        private const string CV_MZ_ARRAY = @"MS:1000514";
        private const string CV_FLOAT_32 = @"MS:1000521";
        private const string CV_ZLIB = @"MS:1000574";

        // The shifted subset reports at least this share of what the unshifted subset reports.
        private const double MIN_SHIFTED_FRACTION = 0.9;
        // The curve recovers the applied shift, on top of the subset's own offset, within this.
        private const double OFFSET_CURVE_TOLERANCE_TH = 0.1;
        // The mean, on the same shifted runs, misses the offset by more than this (it finds -0.29 Th of
        // about -0.42), and needs a window more than twice the curve's (about 1.1 Th against 0.3).
        private const double MEAN_MISS_TH = 0.08;
        private const double MEAN_WINDOW_RATIO = 2.0;
        private const double FRAGMENT_TOLERANCE_TH = 0.5;
        // The spectra test's shift the others are compared with, and how closely they follow it (the scan's step).
        private const double REFERENCE_SHIFT_TH = -0.30;
        private const double SHIFT_SCAN_TOLERANCE_TH = 0.02;
        private static readonly double[] OFFSET_CHECK_MZ = { 400.0, 700.0, 1000.0 };

        /// <summary>
        /// The unit-resolution MS2 m/z calibration on real data with a large offset. The Stellar
        /// subset's MS2 sit only about 0.06 Th low, which the mean calibration sees, so its own
        /// search keeps the mean. Its MS2 m/z moved down by <see cref="AppliedMs2Shift"/> (-0.30 Th at
        /// m/z 200 to -0.40 Th above 600), so they sit -0.35 to -0.55 Th low in all, as a 2026 Stellar
        /// run's profile apex does, must be calibrated by the m/z offset curve: each run's curve
        /// recovers the shift, and the search reports nearly what the unshifted subset reports. The
        /// same shifted runs calibrated by the mean (OSPREY_MS2_CAL_MEAN=1, as Rust osprey does) miss
        /// part of the offset, need a window over twice as wide, and report fewer precursors.
        /// (Much further off and this small subset finds too few calibration peptides within the
        /// uncorrected 0.5 Th tolerance to calibrate at all; full runs have thousands.)
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestSubsetLargeMs2Offset()
        {
            string baselineDir = CreateDir(@"ms2-offset-baseline");
            RunOsprey(Ms2OffsetArgs(DataInputs().ToArray(), baselineDir), Verifier(false));
            int baseline = BlibComparer.CountRows(Path.Combine(baselineDir, BLIB_FILE), @"RefSpectra");
            Assert.IsTrue(baseline >= MIN_PRECURSORS, string.Format(@"{0} precursors reported unshifted", baseline));

            string shiftedData = CreateDir(@"ms2-offset-data");
            var shiftedInputs = DataInputs().Select(input => WriteShiftedMs2(input, shiftedData, AppliedMs2Shift)).ToArray();
            string shiftedDir = CreateDir(@"ms2-offset");
            RunOsprey(Ms2OffsetArgs(shiftedInputs, shiftedDir), Verifier(false));
            int shifted = BlibComparer.CountRows(Path.Combine(shiftedDir, BLIB_FILE), @"RefSpectra");
            Assert.IsTrue(shifted >= MIN_SHIFTED_FRACTION * baseline,
                string.Format(@"{0} precursors reported shifted, {1} unshifted", shifted, baseline));

            string meanDir = CreateDir(@"ms2-offset-mean");
            var mean = new Dictionary<string, string>(Verifier(false)) { [@"OSPREY_MS2_CAL_MEAN"] = @"1" };
            RunOsprey(Ms2OffsetArgs(shiftedInputs, meanDir), mean);
            int meanShifted = BlibComparer.CountRows(Path.Combine(meanDir, BLIB_FILE), @"RefSpectra");
            Assert.IsTrue(shifted > meanShifted,
                string.Format(@"{0} precursors reported with the curve, {1} with the mean", shifted, meanShifted));

            for (int i = 0; i < shiftedInputs.Length; i++)
            {
                string run = Path.GetFileName(shiftedInputs[i]);
                var own = LoadMs2Calibration(DataInputs().ElementAt(i), baselineDir);
                Assert.IsFalse(own.HasOffsetCurve, @"the subset's own offset is within half the tolerance");
                var curve = LoadMs2Calibration(shiftedInputs[i], shiftedDir);
                Assert.IsTrue(curve.HasOffsetCurve, run);
                foreach (double mz in OFFSET_CHECK_MZ)
                {
                    Assert.AreEqual(AppliedMs2Shift(mz) + own.Mean, curve.OffsetAt(mz), OFFSET_CURVE_TOLERANCE_TH,
                        string.Format(@"{0} at m/z {1}", run, mz));
                }
                var meanOnly = LoadMs2Calibration(shiftedInputs[i], meanDir);
                Assert.IsFalse(meanOnly.HasOffsetCurve, run);
                Assert.IsTrue(meanOnly.Mean - (AppliedMs2Shift(700) + own.Mean) > MEAN_MISS_TH,
                    string.Format(@"{0}: the mean finds {1:F3} Th", run, meanOnly.Mean));
                Assert.IsTrue(meanOnly.SD > MEAN_WINDOW_RATIO * curve.SD,
                    string.Format(@"{0}: SD {1:F3} with the mean, {2:F3} with the curve", run, meanOnly.SD, curve.SD));
            }
        }

        /// <summary>
        /// The search across the offsets this subset can discover: within about 0.2 Th the mean
        /// stays, and it sees the offset; further off, on either side, each run's curve recovers it.
        /// Each reports nearly what the unshifted subset reports. Beyond that, at +0.75 Th, too few
        /// calibration peptides match within the uncorrected tolerance on a subset this small, and
        /// the search refuses cleanly rather than reporting from a calibration it could not make.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestSubsetMs2OffsetAcrossRange()
        {
            string baselineDir = CreateDir(@"ms2-range-baseline");
            RunOsprey(Ms2OffsetArgs(DataInputs().ToArray(), baselineDir), Verifier(false));
            int baseline = BlibComparer.CountRows(Path.Combine(baselineDir, BLIB_FILE), @"RefSpectra");

            foreach (var (shift, expectCurve) in new[] { (-0.10, false), (0.20, false), (-0.40, true), (0.40, true) })
            {
                string name = string.Format(CultureInfo.InvariantCulture, @"ms2-range-{0:F2}", shift);
                var inputs = WriteShiftedInputs(name + @"-data", _ => shift);
                string workDir = CreateDir(name);
                RunOsprey(Ms2OffsetArgs(inputs, workDir), Verifier(false));
                int count = BlibComparer.CountRows(Path.Combine(workDir, BLIB_FILE), @"RefSpectra");
                Assert.IsTrue(count >= MIN_SHIFTED_FRACTION * baseline,
                    string.Format(@"{0}: {1} precursors reported, {2} unshifted", name, count, baseline));
                for (int i = 0; i < inputs.Length; i++)
                {
                    double expected = shift + LoadMs2Calibration(DataInputs().ElementAt(i), baselineDir).Mean;
                    var calibration = LoadMs2Calibration(inputs[i], workDir);
                    Assert.AreEqual(expectCurve, calibration.HasOffsetCurve, name);
                    foreach (double mz in OFFSET_CHECK_MZ)
                    {
                        Assert.AreEqual(expected, calibration.OffsetAt(mz), OFFSET_CURVE_TOLERANCE_TH,
                            string.Format(@"{0} at m/z {1}", name, mz));
                    }
                }
            }

            var beyond = WriteShiftedInputs(@"ms2-range-beyond-data", _ => 0.75);
            string beyondDir = CreateDir(@"ms2-range-beyond");
            RunOspreyExpecting(Ms2OffsetArgs(beyond, beyondDir), Verifier(false), Program.EXIT_CODE_FAILURE_TO_START);
            Assert.IsFalse(File.Exists(Path.Combine(beyondDir, BLIB_FILE)));
        }

        /// <summary>
        /// Calibration is per run: one run of three far off takes the curve while the others keep
        /// their mean, and the experiment reports nearly what it reports with none shifted.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestSubsetMs2OffsetInOneRun()
        {
            string baselineDir = CreateDir(@"ms2-onerun-baseline");
            RunOsprey(Ms2OffsetArgs(DataInputs().ToArray(), baselineDir), Verifier(false));
            int baseline = BlibComparer.CountRows(Path.Combine(baselineDir, BLIB_FILE), @"RefSpectra");

            const int shiftedRun = 1;
            var inputs = DataInputs().ToArray();
            inputs[shiftedRun] = WriteShiftedMs2(inputs[shiftedRun], CreateDir(@"ms2-onerun-data"), _ => -0.40);
            string workDir = CreateDir(@"ms2-onerun");
            RunOsprey(Ms2OffsetArgs(inputs, workDir), Verifier(false));
            for (int i = 0; i < inputs.Length; i++)
                Assert.AreEqual(i == shiftedRun, LoadMs2Calibration(inputs[i], workDir).HasOffsetCurve, inputs[i]);
            int count = BlibComparer.CountRows(Path.Combine(workDir, BLIB_FILE), @"RefSpectra");
            Assert.IsTrue(count >= MIN_SHIFTED_FRACTION * baseline,
                string.Format(@"{0} precursors reported, {1} unshifted", count, baseline));
        }

        /// <summary>
        /// The curve across the HPC chain's task boundaries: it is fitted on the scoring worker, and
        /// the rescoring worker has it only from the shipped calibration file, so the chain must
        /// report the straight-through library. Then a re-run of the straight directory, which
        /// reads the curve back, and a search at another thread count, each byte for byte.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestSubsetLargeMs2OffsetRoutes()
        {
            string shiftedData = CreateDir(@"ms2-routes-data");
            var inputs = DataInputs().Select(input => WriteShiftedMs2(input, shiftedData, AppliedMs2Shift)).ToArray();
            File.Copy(Path.Combine(_dataDir, LIBRARY_FILE), Path.Combine(shiftedData, LIBRARY_FILE));
            AssertHpcChainMatchesStraight(new Subset(shiftedData, LIBRARY_FILE, @"unit", RUN_NAMES), @"ms2-routes-");
            foreach (string run in RUN_NAMES)
            {
                string phaseDir = Path.Combine(_testDir, @"ms2-routes-phase1_" + run);
                Assert.IsTrue(LoadMs2Calibration(Path.Combine(phaseDir, run + MZML_EXTENSION), phaseDir).HasOffsetCurve, run);
            }

            string firstDir = CreateDir(@"ms2-routes-first");
            RunOsprey(Ms2OffsetArgs(inputs, firstDir), Verifier(false));
            string firstBlib = Path.Combine(_testDir, @"ms2-routes-first.blib");
            File.Copy(Path.Combine(firstDir, BLIB_FILE), firstBlib);
            RunOsprey(Ms2OffsetArgs(inputs, firstDir), Verifier(false));
            AssertBlibsEqual(firstBlib, Path.Combine(firstDir, BLIB_FILE));

            string threadsDir = CreateDir(@"ms2-routes-threads");
            var oneThread = Ms2OffsetArgs(inputs, threadsDir).ToArray();
            oneThread[Array.IndexOf(oneThread, OspreyCommandArgs.ARG_THREADS.ArgumentText) + 1] = @"1";
            RunOsprey(oneThread, Verifier(false));
            AssertBlibsEqual(firstBlib, Path.Combine(threadsDir, BLIB_FILE));
        }

        /// <summary>
        /// The curve with the decoys the library supplies, where <c>DecoyGenerator</c> never runs:
        /// the shifted search reports nearly what the unshifted one does.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestSubsetLargeMs2OffsetLibraryDecoys()
        {
            var args = new[]
            {
                OspreyCommandArgs.ARG_DECOYS_IN_LIBRARY.ArgumentText,
                OspreyCommandArgs.ARG_DECOY_PAIRING_MANIFEST.ArgumentText, Path.Combine(_dataDir, LIBDECOY_PAIRING_FILE)
            };
            string baselineDir = CreateDir(@"ms2-libdecoy-baseline");
            RunAnalysis(baselineDir, DataInputs(), LIBDECOY_FILE, Verifier(false), args);
            int baseline = BlibComparer.CountRows(Path.Combine(baselineDir, BLIB_FILE), @"RefSpectra");

            var inputs = WriteShiftedInputs(@"ms2-libdecoy-data", _ => -0.30);
            string workDir = CreateDir(@"ms2-libdecoy");
            RunAnalysis(workDir, inputs, LIBDECOY_FILE, Verifier(false), args);
            foreach (string input in inputs)
                Assert.IsTrue(LoadMs2Calibration(input, workDir).HasOffsetCurve, input);
            int count = BlibComparer.CountRows(Path.Combine(workDir, BLIB_FILE), @"RefSpectra");
            Assert.IsTrue(count >= MIN_SHIFTED_FRACTION * baseline,
                string.Format(@"{0} precursors reported, {1} unshifted", count, baseline));
        }

        /// <summary>
        /// The curve on the subset's real centroids over the whole range of the shift scan, past what
        /// so small a subset can discover in a search: the peaks the calibration collects at the apex
        /// of each precursor its unshifted search reported, with the spectrum moved first. A shift
        /// of the data only moves the curve, so at any shift the mean gives way at the same place
        /// and the curve is the one at any other shift, moved by the difference: no shift lands on
        /// a neighbouring isotope, including those a whole isotope spacing from zero. A shift that
        /// changes with m/z is followed. (Not one as steep as 0.001 per Th about -0.65 Th, which the
        /// first association about a single shift gets wrong: see the discussion on the PR.)
        /// With too few fragments there is no curve and no shift.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestMs2OffsetCurveOnSubsetSpectra()
        {
            string workDir = CreateDir(@"ms2-spectra");
            RunOsprey(Ms2OffsetArgs(DataInputs().ToArray(), workDir), Verifier(false));
            var apexes = SubsetApexSpectra(workDir);

            Assert.IsNull(Ms2OffsetCurve.Calculate(ShiftedFragmentPeaks(apexes, _ => 0), FRAGMENT_TOLERANCE_TH, out double ownShift));
            foreach (double applied in new[] { -0.10, 0.10, 0.30 })
            {
                Assert.IsNull(Ms2OffsetCurve.Calculate(ShiftedFragmentPeaks(apexes, _ => applied), FRAGMENT_TOLERANCE_TH, out double shift),
                    applied.ToString(CultureInfo.InvariantCulture));
                Assert.AreEqual(ownShift + applied, shift, SHIFT_SCAN_TOLERANCE_TH, applied.ToString(CultureInfo.InvariantCulture));
            }

            var reference = CurveAt(apexes, _ => REFERENCE_SHIFT_TH, out _);
            foreach (double applied in new[] { -0.50, -0.65, -0.80, -0.95, -1.15, 0.50, 0.75, 1.05 })
            {
                string message = applied.ToString(CultureInfo.InvariantCulture);
                var curve = CurveAt(apexes, _ => applied, out double shift);
                Assert.AreEqual(ownShift + applied, shift, SHIFT_SCAN_TOLERANCE_TH, message);
                foreach (double mz in OFFSET_CHECK_MZ)
                    Assert.AreEqual(reference.OffsetAt(mz) - REFERENCE_SHIFT_TH + applied, curve.OffsetAt(mz), SHIFT_SCAN_TOLERANCE_TH, message);
            }

            Func<double, double> tilted = mz => -0.65 + 0.0005 * (mz - 700.0);
            var tiltedCurve = CurveAt(apexes, tilted, out _);
            foreach (double mz in OFFSET_CHECK_MZ)
            {
                Assert.AreEqual(reference.OffsetAt(mz) - REFERENCE_SHIFT_TH + tilted(mz), tiltedCurve.OffsetAt(mz),
                    OFFSET_CURVE_TOLERANCE_TH, mz.ToString(CultureInfo.InvariantCulture));
            }

            int tooFew = Ms2OffsetCurve.MIN_FRAGMENTS / TopFragmentExtractor.CAL_TOP_N_FRAGMENTS;
            Assert.IsNull(Ms2OffsetCurve.Calculate(ShiftedFragmentPeaks(apexes.Take(tooFew), _ => -0.65), FRAGMENT_TOLERANCE_TH, out double thinShift));
            Assert.IsTrue(double.IsNaN(thinShift));
        }

        /// <summary>
        /// An offset that changes with m/z, -0.20 Th at m/z 400 to -0.55 Th at 1100, which no single
        /// offset fits: each run's curve follows it, the search reports nearly what the unshifted
        /// subset reports, and a search calibrated by the mean reports fewer.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestSubsetMs2OffsetVaryingWithMz()
        {
            string baselineDir = CreateDir(@"ms2-tilt-baseline");
            RunOsprey(Ms2OffsetArgs(DataInputs().ToArray(), baselineDir), Verifier(false));
            int baseline = BlibComparer.CountRows(Path.Combine(baselineDir, BLIB_FILE), @"RefSpectra");

            Func<double, double> shiftAt = mz => -0.20 - 0.0005 * (mz - 400.0);
            var inputs = WriteShiftedInputs(@"ms2-tilt-data", shiftAt);
            string workDir = CreateDir(@"ms2-tilt");
            RunOsprey(Ms2OffsetArgs(inputs, workDir), Verifier(false));
            int count = BlibComparer.CountRows(Path.Combine(workDir, BLIB_FILE), @"RefSpectra");
            Assert.IsTrue(count >= MIN_SHIFTED_FRACTION * baseline,
                string.Format(@"{0} precursors reported, {1} unshifted", count, baseline));
            for (int i = 0; i < inputs.Length; i++)
            {
                double own = LoadMs2Calibration(DataInputs().ElementAt(i), baselineDir).Mean;
                var calibration = LoadMs2Calibration(inputs[i], workDir);
                Assert.IsTrue(calibration.HasOffsetCurve, inputs[i]);
                foreach (double mz in OFFSET_CHECK_MZ)
                    Assert.AreEqual(shiftAt(mz) + own, calibration.OffsetAt(mz), OFFSET_CURVE_TOLERANCE_TH, inputs[i]);
            }

            string meanDir = CreateDir(@"ms2-tilt-mean");
            var mean = new Dictionary<string, string>(Verifier(false)) { [@"OSPREY_MS2_CAL_MEAN"] = @"1" };
            RunOsprey(Ms2OffsetArgs(inputs, meanDir), mean);
            int meanCount = BlibComparer.CountRows(Path.Combine(meanDir, BLIB_FILE), @"RefSpectra");
            Assert.IsTrue(count > meanCount, string.Format(@"{0} precursors with the curve, {1} with the mean", count, meanCount));
        }

        /// <summary>The offset added to the shifted subset's MS2 m/z: -0.30 Th at m/z 200 to -0.40 Th above 600.</summary>
        private static double AppliedMs2Shift(double mz)
        {
            return -0.30 - 0.1 * Math.Min(1.0, Math.Max(0.0, (mz - 200.0) / 400.0));
        }

        private string[] WriteShiftedInputs(string dirName, Func<double, double> shiftAt)
        {
            string dir = CreateDir(dirName);
            return DataInputs().Select(input => WriteShiftedMs2(input, dir, shiftAt)).ToArray();
        }

        /// <summary>
        /// What the calibration would be given, for each precursor of a search of the unshifted
        /// subset in each run that reported it: its library entry and that run's MS2 spectrum
        /// closest to its retention time. Unlike a shifted search, it needs no discovery first.
        /// </summary>
        private List<(LibraryEntry Entry, Spectrum Apex)> SubsetApexSpectra(string workDir)
        {
            var entries = new DiannTsvLoader(FragmentToleranceConfig.UnitResolution(FRAGMENT_TOLERANCE_TH))
                .Load(Path.Combine(_dataDir, LIBRARY_FILE))
                .GroupBy(e => (e.Sequence, (int)e.Charge)).ToDictionary(g => g.Key, g => g.First());
            var spectra = new Dictionary<string, List<Spectrum>>();
            foreach (string input in DataInputs())
            {
                var runSpectra = new List<Spectrum>();
                SpectrumFileReader.VisitMs2Spectra(input, s =>
                {
                    runSpectra.Add(s);
                    return true;
                });
                spectra[Path.GetFileNameWithoutExtension(input)] = runSpectra;
            }

            var apexes = new List<(LibraryEntry, Spectrum)>();
            using var conn = new SQLiteConnection(@"Data Source=" + Path.Combine(workDir, BLIB_FILE) + @";Version=3;Read Only=True;Pooling=False");
            conn.Open();
            using var cmd = new SQLiteCommand(@"SELECT r.peptideSeq, r.precursorCharge, f.fileName, t.retentionTime " +
                @"FROM RetentionTimes t JOIN RefSpectra r ON t.RefSpectraID = r.id JOIN SpectrumSourceFiles f ON t.SpectrumSourceID = f.id " +
                @"WHERE t.retentionTime IS NOT NULL", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var key = (reader.GetString(0), reader.GetInt32(1));
                string run = Path.GetFileNameWithoutExtension(reader.GetString(2));
                if (!entries.TryGetValue(key, out var entry) || !spectra.TryGetValue(run, out var runSpectra))
                    continue;
                double rt = reader.GetDouble(3);
                var apex = runSpectra.Where(s => s.ContainsPrecursor(entry.PrecursorMz))
                    .OrderBy(s => Math.Abs(s.RetentionTime - rt)).FirstOrDefault();
                if (apex != null)
                    apexes.Add((entry, apex));
            }
            return apexes;
        }

        /// <summary>The peaks the calibration collects from <paramref name="apexes"/>, with their MS2 m/z moved by <paramref name="shiftAt"/>.</summary>
        private static List<Ms2FragmentPeaks> ShiftedFragmentPeaks(IEnumerable<(LibraryEntry Entry, Spectrum Apex)> apexes,
            Func<double, double> shiftAt)
        {
            var config = new OspreyConfig { FragmentTolerance = FragmentToleranceConfig.UnitResolution(FRAGMENT_TOLERANCE_TH) };
            var peaks = new List<Ms2FragmentPeaks>();
            foreach (var (entry, apex) in apexes)
            {
                var shifted = new Spectrum
                {
                    Mzs = apex.Mzs.Select(mz => mz + shiftAt(mz)).ToArray(),
                    Intensities = apex.Intensities
                };
                peaks.AddRange(Calibrator.CollectMs2FragmentPeaks(entry, shifted, config));
            }
            return peaks;
        }

        private static MzCalibrationResult CurveAt(IEnumerable<(LibraryEntry Entry, Spectrum Apex)> apexes,
            Func<double, double> shiftAt, out double shift)
        {
            var curve = Ms2OffsetCurve.Calculate(ShiftedFragmentPeaks(apexes, shiftAt), FRAGMENT_TOLERANCE_TH, out shift);
            Assert.IsNotNull(curve);
            Assert.IsTrue(curve.HasOffsetCurve);
            return curve;
        }

        private string[] Ms2OffsetArgs(string[] inputs, string workDir)
        {
            return InputArgs(inputs).Concat(new[]
            {
                OspreyCommandArgs.ARG_LIBRARY.ArgumentText, Path.Combine(_dataDir, LIBRARY_FILE),
                OspreyCommandArgs.ARG_OUTPUT.ArgumentText, Path.Combine(workDir, BLIB_FILE),
                OspreyCommandArgs.ARG_WORK_DIR.ArgumentText, workDir
            }).Concat(CommonArgs()).ToArray();
        }

        private static MzCalibrationResult LoadMs2Calibration(string input, string workDir)
        {
            var calibration = CalibrationIO.LoadCalibration(CalibrationIO.CalibrationPathForInput(input, workDir));
            var ms2 = calibration.Ms2Calibration.ToResult();
            Assert.IsNotNull(ms2, input);
            return ms2;
        }

        /// <summary>
        /// A copy of <paramref name="input"/> in <paramref name="dir"/> with every MS2 m/z moved by
        /// <paramref name="shiftAt"/> of that m/z; MS1 and everything else as they were. The subset's arrays
        /// are zlib-compressed 32-bit floats, and the copy keeps them so.
        /// </summary>
        private static string WriteShiftedMs2(string input, string dir, Func<double, double> shiftAt)
        {
            var doc = new XmlDocument { PreserveWhitespace = true };
            doc.Load(input);
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace(@"m", MZML_NAMESPACE);
            int shifted = 0;
            var spectra = doc.SelectNodes(@"//m:spectrum", ns);
            Assert.IsNotNull(spectra, input);
            foreach (XmlElement spectrum in spectra)
            {
                var level = (XmlElement)spectrum.SelectSingleNode(@"m:cvParam[@accession='" + CV_MS_LEVEL + @"']", ns);
                if (level == null || level.GetAttribute(@"value") != @"2")
                    continue;
                var arrays = spectrum.SelectNodes(@".//m:binaryDataArray", ns);
                Assert.IsNotNull(arrays, input);
                foreach (XmlElement array in arrays)
                {
                    if (array.SelectSingleNode(@"m:cvParam[@accession='" + CV_MZ_ARRAY + @"']", ns) == null)
                        continue;
                    Assert.IsNotNull(array.SelectSingleNode(@"m:cvParam[@accession='" + CV_FLOAT_32 + @"']", ns), input);
                    Assert.IsNotNull(array.SelectSingleNode(@"m:cvParam[@accession='" + CV_ZLIB + @"']", ns), input);
                    var binary = (XmlElement)array.SelectSingleNode(@"m:binary", ns);
                    Assert.IsNotNull(binary, input);
                    var mzs = DecodeFloats(binary.InnerText);
                    for (int i = 0; i < mzs.Length; i++)
                        mzs[i] = (float)(mzs[i] + shiftAt(mzs[i]));
                    binary.InnerText = EncodeFloats(mzs);
                    array.SetAttribute(@"encodedLength", binary.InnerText.Length.ToString(CultureInfo.InvariantCulture));
                    shifted++;
                }
            }
            Assert.IsTrue(shifted > 0, input);
            string output = Path.Combine(dir, Path.GetFileName(input));
            doc.Save(output);
            return output;
        }

        private static float[] DecodeFloats(string base64)
        {
            using var compressed = new MemoryStream(Convert.FromBase64String(base64));
            using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
            using var bytes = new MemoryStream();
            zlib.CopyTo(bytes);
            var raw = bytes.ToArray();
            var values = new float[raw.Length / sizeof(float)];
            Buffer.BlockCopy(raw, 0, values, 0, values.Length * sizeof(float));
            return values;
        }

        private static string EncodeFloats(float[] values)
        {
            var raw = new byte[values.Length * sizeof(float)];
            Buffer.BlockCopy(values, 0, raw, 0, raw.Length);
            using var bytes = new MemoryStream();
            using (var zlib = new ZLibStream(bytes, CompressionLevel.Optimal, true))
                zlib.Write(raw, 0, raw.Length);
            return Convert.ToBase64String(bytes.ToArray());
        }
    }
}
