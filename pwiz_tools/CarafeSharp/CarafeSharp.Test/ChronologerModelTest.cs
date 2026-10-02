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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.Models;
using static TorchSharp.torch;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// The Chronologer retention-time model against jchronologer's golden parity cases
    /// (src/test/resources/data/golden/chronologer_parity_cases.*.tsv at searlelab/jchronologer b64a23363d,
    /// produced by the Python Chronologer): the coded sequence of each accepted peptide exactly, its
    /// hydrophobic index to <see cref="HI_TOLERANCE"/>, and the rejected peptides. Also the conversion of
    /// CarafeSharp's alphabase-named forms to Chronologer's mass-annotated sequences, the iRT fit, and the
    /// pinned files.
    /// </summary>
    [TestClass]
    public class ChronologerModelTest
    {
        /// <summary>The golden values are float32 from Python on another machine.</summary>
        private const double HI_TOLERANCE = 2e-5;

        /// <summary>Mass-annotated sequence, coded sequence, hydrophobic index.</summary>
        private static readonly (string ModifiedSequence, string Coded, double Hi)[] GOLDEN_ACCEPTED =
        {
            (@"VATVSLPR", @"-VATVSLPR_", 8.394585609436035),
            (@"LAADDFR", @"-LAADDFR_", 6.878472328186035),
            (@"FLEQQNK", @"-FLEQQNK_", 3.7536282539367676),
            (@"LGEHNIDVLEGNEQFINAAK", @"-LGEHNIDVLEGNEQFINAAK_", 14.286270141601562),
            (@"SHC[+57.021464]IAEVENDEMPADLPSLAADFVESK", @"-SHcIAEVENDEMPADLPSLAADFVESK_", 19.93527603149414),
            (@"C[+57.021464]C[+57.021464]TESLVNR", @"-ccTESLVNR_", 5.078381538391113),
            (@"ETYGEMADC[+57.021464]C[+57.021464]AK", @"-ETYGEMADccAK_", 5.957462310791016),
            (@"MPC[+57.021464]AEDYLSVVLNQLC[+57.021464]VLHEK", @"-MPcAEDYLSVVLNQLcVLHEK_", 24.174541473388672),
            (@"M[+15.994915]PEPTIDEK", @"-mPEPTIDEK_", 4.6299147605896),
            (@"Q[-17.026549]NQEYQVLLDVR", @"(eNQEYQVLLDVR_", 17.283842086791992),
            (@"E[-18.0105647]ATTESTK", @"(eATTESTK_", 2.180934429168701),
            (@"C[+39.99491463]PEPTIDEK", @")dPEPTIDEK_", 8.86428165435791),
            (@"[+42.010565]ACDEFGHIK", @"^ACDEFGHIK_", 9.192350387573242),
            (@"ACDEFG", @"-ACDEFG_", 5.981589317321777),
            (@"S[+79.966331]PEPTIDEK", @"-sPEPTIDEK_", 5.557801246643066),
            (@"[+42.010565]KPEPTIDEK", @"^KPEPTIDEK_", 5.410096168518066),
            (@"R[+14.01565]PEPTIDEK", @"-qPEPTIDEK_", 5.6915154457092285),
            (@"K[+224.152478]PEPTIDEK", @"-zPEPTIDEK_", 4.773852348327637),
            (@"[+229.162932]PEPTIDEK", @"*PEPTIDEK_", 7.6735029220581055),
        };

        /// <summary>An N-terminal mass without its sign, an unknown modification, 5 and 51 residues.</summary>
        private static readonly string[] GOLDEN_REJECTED =
        {
            @"[42.010565]ACDEFGHIK",
            @"ACDE[+123.456]FGHIK",
            @"ACDEF",
            new string('A', 51),
        };

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestChronologerGoldenParity()
        {
            using (var model = ChronologerModel.FromFiles(ChronologerFiles.Open(), CPU))
            {
                foreach (var golden in GOLDEN_ACCEPTED)
                    Assert.AreEqual(golden.Coded, model.Encoding.ToCodedSequence(golden.ModifiedSequence), golden.ModifiedSequence);
                var tokens = GOLDEN_ACCEPTED.Select(g => model.Encoding.EncodeModifiedSequence(g.ModifiedSequence)).ToArray();
                double[] predicted = model.PredictTokens(tokens);
                for (int i = 0; i < GOLDEN_ACCEPTED.Length; i++)
                    Assert.AreEqual(GOLDEN_ACCEPTED[i].Hi, predicted[i], HI_TOLERANCE, GOLDEN_ACCEPTED[i].ModifiedSequence);

                foreach (string rejected in GOLDEN_REJECTED)
                    Assert.IsNull(model.Encoding.EncodeModifiedSequence(rejected), rejected);
                // A rejected peptide predicts NaN without disturbing the others in its batch.
                double[] mixed = model.PredictTokens(new[] { tokens[0], null, tokens[1] });
                Assert.AreEqual(GOLDEN_ACCEPTED[0].Hi, mixed[0], HI_TOLERANCE);
                Assert.IsTrue(double.IsNaN(mixed[1]));
                Assert.AreEqual(GOLDEN_ACCEPTED[1].Hi, mixed[2], HI_TOLERANCE);
            }
        }

        /// <summary>
        /// CarafeSharp's forms carry alphabase names and sites; they reach Chronologer as the mass-annotated
        /// sequences its rules match, and predict as the golden cases do.
        /// </summary>
        [TestMethod]
        public void TestChronologerPeptideForms()
        {
            var cases = new[]
            {
                (new PeptideForm(@"SHCIAEVENDEMPADLPSLAADFVESK", new[] { @"Carbamidomethyl@C" }, new[] { 3 }),
                    @"SHC[+57.021464]IAEVENDEMPADLPSLAADFVESK", 4),
                (new PeptideForm(@"MPEPTIDEK", new[] { @"Oxidation@M" }, new[] { 1 }), @"M[+15.994915]PEPTIDEK", 8),
                (new PeptideForm(@"QNQEYQVLLDVR", new[] { @"Gln->pyro-Glu@Q^Any_N-term" }, new[] { 0 }),
                    @"Q[-17.026549]NQEYQVLLDVR", 9),
                (new PeptideForm(@"ACDEFGHIK", new[] { @"Acetyl@Protein_N-term" }, new[] { 0 }), @"[+42.010565]ACDEFGHIK", 12),
                (new PeptideForm(@"SPEPTIDEK", new[] { @"Phospho@S" }, new[] { 1 }), @"S[+79.966331]PEPTIDEK", 14),
            };
            using (var model = ChronologerModel.FromFiles(ChronologerFiles.Open(), CPU))
            {
                foreach (var (form, modifiedSequence, _) in cases)
                    Assert.AreEqual(modifiedSequence, ChronologerEncoding.ToModifiedSequence(form), form.ToString());
                double[] predicted = model.Predict(cases.Select(c => c.Item1).ToArray());
                for (int i = 0; i < cases.Length; i++)
                    Assert.AreEqual(GOLDEN_ACCEPTED[cases[i].Item3].Hi, predicted[i], HI_TOLERANCE, cases[i].Item2);

                // Chronologer has no C-terminal modifications, and nothing for an unknown name.
                var amidated = new PeptideForm(@"PEPTIDEK", new[] { @"Amidated@Any_C-term" }, new[] { -1 });
                Assert.IsNull(ChronologerEncoding.ToModifiedSequence(amidated));
                Assert.IsTrue(double.IsNaN(model.Predict(new[] { amidated }).Single()));
            }
        }

        /// <summary>
        /// Library prediction's Chronologer: Chronologer's own value for a peptide it encodes, and for one it rejects
        /// the pretrained AlphaPeptDeep prediction, carried onto the hydrophobic-index scale so that on iRT it is
        /// AlphaPeptDeep's own iRT.
        /// </summary>
        [TestMethod]
        public void TestChronologerRtPredictorFallback()
        {
            var plain = new PeptideForm(@"VATVSLPR");
            var amidated = new PeptideForm(@"VATVSLPR", new[] { @"Amidated@Any_C-term" }, new[] { -1 });
            var pretrained = PretrainedModels.Open();
            using (var predictor = new ChronologerRtPredictor(ChronologerModel.FromFiles(ChronologerFiles.Open(), CPU),
                       RtModel.FromPretrained(pretrained, CPU)))
            using (var alphaPeptDeep = RtModel.FromPretrained(pretrained, CPU))
            {
                double[] predicted = predictor.Predict(new[] { plain, amidated });
                Assert.AreEqual(GOLDEN_ACCEPTED[0].Hi, predicted[0], HI_TOLERANCE);
                Assert.AreEqual(1, predictor.FallbackCount);
                var chronologerIrt = predictor.FitIrtCalibration();
                var alphaPeptDeepIrt = alphaPeptDeep.FitIrtCalibration();
                double fallback = alphaPeptDeep.Predict(new[] { amidated }).Single();
                Assert.AreEqual(alphaPeptDeepIrt.Slope * fallback + alphaPeptDeepIrt.Intercept,
                    chronologerIrt.Slope * predicted[1] + chronologerIrt.Intercept, 1e-6);
            }
        }

        /// <summary>The iRT kit peptides map the hydrophobic index onto iRT with a positive slope.</summary>
        [TestMethod]
        public void TestChronologerIrtCalibration()
        {
            using (var model = ChronologerModel.FromFiles(ChronologerFiles.Open(), CPU))
            {
                var (slope, intercept) = model.FitIrtCalibration();
                Assert.IsTrue(slope > 0, @"slope " + slope);
                double[] hi = model.Predict(new[] { new PeptideForm(@"LGGNEQVTR"), new PeptideForm(@"LFLQFGAQGSPFLK") });
                Assert.AreEqual(-24.92, slope * hi[0] + intercept, 10);
                Assert.AreEqual(100.0, slope * hi[1] + intercept, 10);
            }
        }

        /// <summary>
        /// The build puts the committed files beside the test assembly and they match their pins; a missing
        /// file and a changed one are both refused.
        /// </summary>
        [TestMethod]
        public void TestChronologerFilesLocation()
        {
            string bundled = Path.Combine(AppContext.BaseDirectory, ChronologerFiles.BUNDLED_RELATIVE_DIRECTORY);
            var files = ChronologerFiles.Open(bundled);
            Assert.IsTrue(File.Exists(files.WeightsPath));
            Assert.IsTrue(File.Exists(files.EncodingPath));

            string folder = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"Chronologer_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(folder);
            try
            {
                Assert.ThrowsException<FileNotFoundException>(() => ChronologerFiles.Open(folder));
                File.Copy(files.WeightsPath, Path.Combine(folder, ChronologerFiles.WEIGHTS_FILE));
                File.WriteAllText(Path.Combine(folder, ChronologerFiles.ENCODING_FILE), @"{}");
                Assert.ThrowsException<InvalidDataException>(() => ChronologerFiles.Open(folder));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
