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
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Chromatography;

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

        /// <summary>
        /// The unit-resolution MS2 m/z calibration on real data with a large offset. The Stellar
        /// subset's MS2 sit only about 0.06 Th low, which the mean calibration sees, so its own
        /// search keeps the mean. Its MS2 m/z moved down by <see cref="AppliedMs2Shift"/> (-0.30 Th at
        /// m/z 200 to -0.40 Th above 600), so they sit -0.35 to -0.55 Th low in all, as a 2026 Stellar
        /// run's profile apex does, must be calibrated by the m/z offset curve: each run's curve
        /// recovers the shift, and the search reports nearly what the unshifted subset reports.
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
            var shiftedInputs = DataInputs().Select(input => WriteShiftedMs2(input, shiftedData)).ToArray();
            string shiftedDir = CreateDir(@"ms2-offset");
            RunOsprey(Ms2OffsetArgs(shiftedInputs, shiftedDir), Verifier(false));
            int shifted = BlibComparer.CountRows(Path.Combine(shiftedDir, BLIB_FILE), @"RefSpectra");
            Assert.IsTrue(shifted >= MIN_SHIFTED_FRACTION * baseline,
                string.Format(@"{0} precursors reported shifted, {1} unshifted", shifted, baseline));

            for (int i = 0; i < shiftedInputs.Length; i++)
            {
                var own = LoadMs2Calibration(DataInputs().ElementAt(i), baselineDir);
                Assert.IsFalse(own.HasOffsetCurve, @"the subset's own offset is within half the tolerance");
                var curve = LoadMs2Calibration(shiftedInputs[i], shiftedDir);
                Assert.IsTrue(curve.HasOffsetCurve, shiftedInputs[i]);
                foreach (double mz in new[] { 400.0, 700.0, 1000.0 })
                {
                    Assert.AreEqual(AppliedMs2Shift(mz) + own.Mean, curve.OffsetAt(mz), OFFSET_CURVE_TOLERANCE_TH,
                        string.Format(@"{0} at m/z {1}", Path.GetFileName(shiftedInputs[i]), mz));
                }
            }
        }

        /// <summary>The offset added to the shifted subset's MS2 m/z: -0.30 Th at m/z 200 to -0.40 Th above 600.</summary>
        private static double AppliedMs2Shift(double mz)
        {
            return -0.30 - 0.1 * Math.Min(1.0, Math.Max(0.0, (mz - 200.0) / 400.0));
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
        /// <see cref="AppliedMs2Shift"/>; MS1 and everything else as they were. The subset's arrays
        /// are zlib-compressed 32-bit floats, and the copy keeps them so.
        /// </summary>
        private static string WriteShiftedMs2(string input, string dir)
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
                        mzs[i] = (float)(mzs[i] + AppliedMs2Shift(mzs[i]));
                    binary.InnerText = EncodeFloats(mzs);
                    array.SetAttribute(@"encodedLength", binary.InnerText.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
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
