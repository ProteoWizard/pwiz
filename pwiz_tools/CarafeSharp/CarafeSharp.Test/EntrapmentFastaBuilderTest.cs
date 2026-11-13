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
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Contracts;
using pwiz.CarafeSharp.Proteome;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// Builds entrapment FASTAs from a small protein FASTA designed to reach Carafe's edge cases
    /// (shared and duplicate accessions, header forms, ambiguity codes, asterisks, lower case,
    /// repeats with no acceptable shuffle, palindromes, I/L twins, a foreign proteome too small
    /// for its targets) and compares the bytes with what Carafe origin/main (Java 23) wrote for
    /// the same command lines. Also checks the pairing validator, the manifest reconciler and
    /// the command-line interpretation.
    /// </summary>
    [TestClass]
    public class EntrapmentFastaBuilderTest
    {
        private const string FOREIGN_PLACEHOLDER = @"{foreign}";

        private static readonly string[] TARGET_FASTA =
        {
            @">sp|P10001|ALPHA_HUMAN Alpha protein OS=Homo sapiens OX=9606 GN=ALPHA PE=1 SV=1",
            @"MKWVTFISLLFLFSSAYSRGVFRRDAHKSEVAHRFKDLGEENFKALVLIAFAQYLQQCPF",
            @"EDHVKLVNEVTEFAKTCVADESAENCDKSLHTLFGDKLCTVATLRetygemadccakqep",
            @"ERNECFLQHKDDNPNLPRLVRPEVDVMCTAFHDNEETFLKK",
            @">sp|P10002|BETA_HUMAN Beta protein that shares peptides",
            @"MSDKPDMAEIEKFDKSKLKKTETQEKNPLPSKETIEQEKQAGESLVNEVTEFAKTCVADE",
            @"SAENCDKPEPTIDEKEDITPEPKLEVELKAAAAAAAAAAAAAAAGATCLERGSMMEEAPR",
            @"",
            @">tr|Q10003|GAMMA_MOUSE Gamma protein with ambiguity codes",
            @"MXKBZPRKPJUOAKRPPKGGSAVIDEKSLDEKLIDELIVESKELVISLIVESKELVLSLL",
            @"VESKLMDLIGDRIMDLLGDRK*",
            @">plainident a header without bars",
            @"GVFRRDAHKSEVAHRFKDLGEENFKALVLIAFAQYLQQCPFEDHVK",
            @">db2|ACC5 two fields",
            @"*MSTNPKPQRKTKRNTNRRPQDVKFPGGGQIVGGVYLLPRRGPRLGVRATRKTSERSQPR",
            @">|ACC6|ENTRY6 empty database field",
            @"MAEGEITTFTALTEKFNLPPGNYKKPKLLYCSNGGHFLRILPDGTVDGTRDRSDQHIQLQ",
            @"  LSAESVGEVYIKSTETGQYLAMDTDGLLYGSQTPNEECLFLERLEENHYNTYISK",
            @">sp|ACC7| trailing empty field",
            @"mqifvktltgktitlevepsdtienvkakiqdkegippdqqrlifagkqledgrtlsdyn",
            @"iqkestlhlvlrlrgg",
            @">tr|P10001|ALPHA_HUMAN Duplicate accession in another database",
            @"MKWVTFISLLFLFSSAYSRGVFRRDAHKSEVAHR",
            @">sp|P10009|STAR_HUMAN Only asterisks",
            @"**",
            @">sp|P10010|SINGLE_HUMAN",
            @"K",
            @">sp|P10011|MONO_HUMAN Mono residue repeats",
            @"MGGGGGGGGKAAAAAAAAKGGGGGGGGGRCCCCCCCCKEEEEEEEEEEKLLLLLLLLLLLR",
        };

        private static readonly string[] FOREIGN_FASTA =
        {
            @">sp|P00924|ENO1_YEAST Enolase 1 OS=Saccharomyces cerevisiae",
            @"MAVSKVYARSVYDSRGNPTVEVELTTEKGVFRSIVPSGASTGVHEALEMRDGDKSKWMGK",
            @"GVLHAVKNVNDVIAPAFVKANIDVKDQKAVDDFLISLDGTANKSKLGANAILGVSLAASR",
            @"AAAAEKNVPLYKHLADLSKSKTSPYVLPVPFLNVLNGGSHAGGALALQEFMIAPTGAKTF",
            @"AEALRIGSEVYHNLKSLTKKRYGASAGNVGDEGGVAPNIQTAEEALDLIVDAIKAAGHDG",
            @"KIKIGLDCASSEFFKDGKYDLDFKNPNSDKSKWLTGPQLADLYHSLMKRYPIVSIEDPFA",
            @"EDDWEAWSHFFKTAGIQIVADDLTVTNPKRIATAIEKKAADALLLKVNQIGTLSESIKAA",
            @"QDSFAAGWGVMVSHRSGETEDTFIADLVVGLRTGQIKTGAPARSERLAKLNQLLRIEEEL",
            @"GDNAVFAGENFHHGDKL",
            @">sp|F00002|HOMOLOG_ARATH Designed homologs of target peptides",
            @"MLVNEVTEFAKIVNEVTEFAKSLHTLFGDKSIHTLFGDKXXVAHRFKDLGEENFKBBR",
            @">sp|F00003|CDR2L_MOUSE Cerebellar degeneration-related protein 2-like",
            @"MRRAAGMEDYSAEEEESWYDHQDLEQDLHLAAELGKTLLERNKELEESLQQMYSTNEEQV",
            @"HEIEYLTKQLDTLRLVNEQHAKVYEQLDLTARDLELTNQRLVMESKAAQQKIHGLTETIE",
            @"RLQSQVEELQAQVEQLRGLEQLRIRREKRERRRTIHTFPCLKELCTSSRCEDAFRLHSSS",
        };

        private const string GUI = @"-enzyme 2 -miss_c 1 -minLength 7 -maxLength 35";

        /// <summary>
        /// Carafe command lines (after -build_entrapment_fasta, -db and -manifest) and the SHA-256
        /// of the peptide FASTA and manifest Carafe wrote for each.
        /// </summary>
        private static readonly FixtureBuild[] BUILDS =
        {
            new FixtureBuild(@"train", GUI,
                @"4e8ad7955a84ef5a63fcca33404606d695543a0c8b652ca492b65ec2683aeeb2", @"2b9af6cf63582f7fbbf228273dc18321b2631b675b57bd327e11a8ec7d7164fc"),
            new FixtureBuild(@"lib", GUI + @" -entrapment",
                @"12e1d6806352031085b3880adc1b8004c39abc88cca152b12ef36e9f9eea785b", @"519f654d50c3bdaa9e873ad283d986d09c2d61408fdc8def2ff190298ed765eb"),
            new FixtureBuild(@"lib-nogate", GUI + @" -entrapment -no_similarity_gate",
                @"90fda1b1e43b1e959a79979d61ebc0d8c03433094e83e950f7fac35adcc375d2", @"a8f85952fb79d4aedc2eade3ee1a251f44f09537f673321b4aa33df7db11b087"),
            new FixtureBuild(@"lib-r05", GUI + @" -entrapment -entrapment_ratio 0.5 -entrapment_seed 11",
                @"ac249be7c8113b159eac1d9e80daf0af690ecfa828e2a5d200a950cf647a809c", @"e259ea7f20dc5452d43f1a0df7c09969534cbdf17288d07b1bda65e3866b1138"),
            new FixtureBuild(@"foreign", GUI + @" -entrapment -entrapment_db " + FOREIGN_PLACEHOLDER,
                @"675b32e1ca27f12fe25833976605bf80da5ab6cac9a4d424cb7f1e82476ff329", @"3c8077c39011ca98308c16a45472b7f10e780f693300e54f43da34c30016550b"),
            new FixtureBuild(@"foreign-r05-mz", GUI + @" -entrapment -entrapment_db " + FOREIGN_PLACEHOLDER +
                                                 @" -entrapment_ratio 0.5 -mz_filter -min_pep_mz 400 -max_pep_mz 900",
                @"845d37260c295590d478f5bd2764b0f98413900a5d8edb622823784786dc4983", @"f0754959d5e25c85e31649cb46ab3b666a3932557e990810dd0f0944e628d957"),
            new FixtureBuild(@"trypsin-clip", @"-enzyme 1 -miss_c 2 -minLength 5 -maxLength 40 -clip_n_m -entrapment -decoy_prefix rev_",
                @"1ca18cacdcbb0b8f4ce71a4bb7e88cae2aa85f99e3381685d2e011192014b5a8", @"82a9c99d1b625b37efa898d5ae761a40b3f5a49642f56e96c0484aff43186582"),
            new FixtureBuild(@"nonspecific", @"-enzyme 0 -minLength 6 -maxLength 7 -no_decoys",
                @"6e4c3bcbc11edf07c657cca055e066e35f21f51e7aa9008e76b71578d2a8355c", @"d2e9d53619d503a1e6a7993145d2b12d928cac2ba64260fd25fd4dc9efc6e0e7"),
            new FixtureBuild(@"mods-mz", GUI + @" -clip_n_m -mz_filter -min_pep_mz 500 -max_pep_mz 800 -min_pep_charge 2 " +
                                          @"-max_pep_charge 2 -fixMod 1 -varMod 2,5,27 -maxVar 2 -entrapment",
                @"e8cb74820ea18d83bcbb64df36a57893893602363c2c4b7b43989662b8e63daf", @"b3dd8371ff6f45c446161b37c32b45ba7d207813a09616d08476dcbeabecd8a1"),
            new FixtureBuild(@"nocut", @"-enzyme NoCut -clip_n_m -minLength 1 -maxLength 200 -no_decoys",
                @"cdcb801e496e9668c0838b8316b54ff48a84414a8818eb513ca5f4789fabccde", @"e385d05a459a8418e646499beca733e2365e1a61234b61829c5e7930c71a171b"),
            new FixtureBuild(@"chymo", @"-enzyme 12 -miss_c 0 -minLength 4 -maxLength 25 -no_decoys",
                @"466c8fce8bb919c22740de080e091bd3552352bca2086d14b43a868fb463e6fc", @"4d520440ee397bc50144601782ef1be6535e704501cc4140a5a1ebcbfa06628d"),
            new FixtureBuild(@"argn-r01", @"-enzyme 5 -miss_c 1 -minLength 7 -maxLength 35 -entrapment -entrapment_ratio 0.1",
                @"863c288ee37158f6b671469d7afa823062d551c1d3a38986dd5caa324f76c9ce", @"a9f69e14594cc72003e864c39061ba794b42aa9d15542b9fd7d0b280cbe47663"),
            new FixtureBuild(@"nomods-mz", @"-enzyme 2 -mz_filter -fixMod 0 -varMod no -min_pep_mz 350 -max_pep_mz 600 -min_pep_charge 3 -max_pep_charge 1",
                @"c8d6ef737554d60b9bbbd6b6163193d78ce05d6fbc66eb4ed80600b67940c3bd", @"907b16248624e85ea84161a610ad904dd42a9b71835b3282c2f971303029c42d"),
        };

        /// <summary>SHA-256 of Carafe's -reconcile_manifest output for the "lib" manifest and <see cref="WriteLibrary"/>.</summary>
        private const string RECONCILED_SHA256 = @"145fa8c622b97b3f3064aed3379f66f22ada0073f97e7f4f1efdd46a11972431";

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestFixtureBuildsMatchCarafe()
        {
            string folder = CreateTestFolder();
            try
            {
                string targets = WriteLines(folder, @"targets.fasta", TARGET_FASTA);
                string foreign = WriteLines(folder, @"foreign.fasta", FOREIGN_FASTA);
                var mismatches = new List<string>();
                foreach (var build in BUILDS)
                {
                    string outputFolder = Path.Combine(folder, build.Name);
                    var result = Build(build.Arguments, targets, foreign, outputFolder);
                    Assert.IsTrue(result.KeptQuartets > 0, build.Name);
                    string fastaSha = Sha256File(Path.Combine(outputFolder, @"peptides.fasta"));
                    // Carafe wrote FDRBench's five columns; the entrapment_method column is cut first.
                    string manifestSha = Sha256File(TestData.FdrBenchColumnsOf(Path.Combine(outputFolder, @"pairing.tsv")));
                    TestContext.WriteLine(@"{0}: {1} quartets, FASTA {2}, manifest {3}", build.Name, result.KeptQuartets,
                        fastaSha == build.FastaSha256 ? @"match" : @"DIFFERS", manifestSha == build.ManifestSha256 ? @"match" : @"DIFFERS");
                    if (fastaSha != build.FastaSha256 || manifestSha != build.ManifestSha256)
                        mismatches.Add(build.Name);
                }
                Assert.AreEqual(0, mismatches.Count, @"Differs from Carafe: " + string.Join(@", ", mismatches));

                AssertNoCutOutput(Path.Combine(folder, @"nocut"));
                AssertTrainRows(Path.Combine(folder, @"train", @"pairing.tsv"));
                AssertReconciliation(folder, Path.Combine(folder, @"lib", @"pairing.tsv"));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [TestMethod]
        public void TestPairingValidator()
        {
            var sources = new ProteinRecord(@"P1", @"E1", @"sp", @"PEPTIDEK");
            var good = Quartet(@"PEPTIDEK", @"EEPPDTIK", @"EDITPEPK", @"ITDPPEEK", sources);
            var missing = Quartet(string.Empty, null, null, null, sources);
            var unselected = Quartet(@"ACDEFGK", null, @"FEDCAGK", null, sources);
            unselected.EntrapmentSelected = true;
            var selfEntrapment = Quartet(@"LLLLLK", @"LLLLLK", @"LLLLLK", null, sources);
            // Its decoy is the first quartet's target.
            var collision = Quartet(@"KPEPTIDE", null, @"PEPTIDEK", null, sources);
            var violations = EntrapmentPairingValidator.ValidateQuartets(new[] { good, missing, unselected, selfEntrapment, collision });
            CollectionAssert.AreEqual(new[]
            {
                EntrapmentPairingValidator.KIND_MISSING_TARGET, EntrapmentPairingValidator.KIND_SELECTED_WITHOUT_ENTRAPMENT,
                EntrapmentPairingValidator.KIND_ENTRAPMENT_EQUALS_TARGET, EntrapmentPairingValidator.KIND_GENERATED_EQUALS_TARGET,
            }, violations.Select(v => v.Kind).ToArray());
            CollectionAssert.AreEqual(new[] { @"pair_index 1" }, violations[0].Examples.ToArray());
            CollectionAssert.AreEqual(new[] { @"LLLLLK", @"PEPTIDEK" }, violations[3].Examples.ToArray());
            Assert.AreEqual(0, EntrapmentPairingValidator.ValidateQuartets(new[] { good }).Count);

            // Refused by default; with the escape hatch, written with a warning.
            Assert.ThrowsException<InvalidOperationException>(() => EntrapmentPairingValidator.Enforce(violations, true));
            var log = new StringWriter();
            EntrapmentPairingValidator.Enforce(violations, false, log);
            StringAssert.Contains(log.ToString(), EntrapmentPairingValidator.Describe(violations));
            // Counts are not capped with the examples.
            var many = Enumerable.Range(0, 7).Select(i => Quartet(string.Empty, null, null, null, sources)).ToArray();
            var capped = EntrapmentPairingValidator.ValidateQuartets(many).Single();
            Assert.AreEqual(7, capped.Count);
            Assert.AreEqual(5, capped.Examples.Count);

            // Library against manifest: a clipped entrapment pairs with the clip of its target.
            var pairs = new Dictionary<string, int> { { @"TARGETK", 0 }, { @"ENTRAPK", 0 }, { @"MTARGETR", 1 }, { @"MENTRAPR", 1 }, { @"LONEK", 2 } };
            var types = new Dictionary<string, string>
            {
                { @"TARGETK", PairingManifest.KIND_TARGET }, { @"ENTRAPK", PairingManifest.KIND_P_TARGET },
                { @"MTARGETR", PairingManifest.KIND_TARGET }, { @"MENTRAPR", PairingManifest.KIND_P_TARGET },
                { @"LONEK", PairingManifest.KIND_TARGET },
            };
            Assert.AreEqual(0, EntrapmentPairingValidator.ValidateLibraryAgainstManifest(
                new[] { @"ENTRAPK", @"ENTRAPR" }, new[] { @"TARGETK", @"TARGETR", @"LONEK" }, pairs, types).Count);
            var libraryViolations = EntrapmentPairingValidator.ValidateLibraryAgainstManifest(
                new[] { @"ENTRAPR", @"STRAYK" }, new[] { @"TARGETK", @"MTARGETR" }, pairs, types);
            Assert.AreEqual(EntrapmentPairingValidator.KIND_ORPHAN_ENTRAPMENT, libraryViolations[0].Kind);
            CollectionAssert.AreEqual(new[] { @"ENTRAPR", @"STRAYK" }, libraryViolations[0].Examples.ToArray());
            Assert.AreEqual(EntrapmentPairingValidator.KIND_UNCOVERED_TARGET, libraryViolations[1].Kind);
            CollectionAssert.AreEqual(new[] { @"TARGETK", @"MTARGETR" }, libraryViolations[1].Examples.ToArray());
        }

        [TestMethod]
        public void TestCommandLine()
        {
            // Carafe's effective defaults, not its help text's.
            var build = CarafeCommandLine.Parse(new[] { @"--build-entrapment-fasta", @"out.fasta", @"--db", @"in.fasta" });
            Assert.AreEqual(CarafeCommandMode.build_entrapment_fasta, build.Mode);
            var settings = build.BuildSettings;
            Assert.AreEqual(@"in.fasta", settings.InputFasta);
            Assert.AreEqual(@"out.fasta", settings.OutputFasta);
            Assert.IsNull(settings.Manifest);
            Assert.AreEqual(1, settings.Digest.EnzymeIndex);
            Assert.AreEqual(2, settings.Digest.MaxMissedCleavages);
            Assert.IsFalse(settings.Digest.ClipNTermMethionine);
            Assert.AreEqual(300.0, settings.MinMz);
            Assert.AreEqual(2000.0, settings.MaxMz);
            CollectionAssert.AreEqual(new[] { 2, 3 }, settings.Charges);
            Assert.IsTrue(settings.AddDecoys);
            Assert.IsFalse(settings.AddEntrapment);
            Assert.IsTrue(settings.SimilarityGate);
            Assert.IsTrue(settings.FailOnPairingViolation);
            Assert.AreEqual(@"decoy_", settings.DecoyPrefix);
            Assert.AreEqual(42L, settings.EntrapmentSeed);
            Assert.AreEqual(0.0, settings.EntrapmentInTargetsRatio);
            Assert.AreEqual(EntrapmentFastaSettings.DEFAULT_ENTRAPMENT_PROTEIN_SEED, settings.EntrapmentProteinSeed);

            // First value of a repeated option wins, "-name=value" works, a negative number is a
            // value, the non-specific enzyme forces 100 missed cleavages, a charge range upside
            // down collapses to its minimum, and options these modes do not read are ignored.
            settings = CarafeCommandLine.Parse(new[]
            {
                @"--build-entrapment-fasta", @"out.fasta", @"--db=in.fasta", @"--enzyme", @"0", @"--enzyme", @"2", @"--miss-c", @"1",
                @"--entrapment-seed", @"-5", @"--min-pep-charge", @"3", @"--max-pep-charge", @"2", @"--entrapment", @"--no-similarity-gate",
                @"--ignore-pairing-errors", @"--o", @"ignored", @"--i2l", @"stray", @"--entrapment-ratio", @" 0.5d ",
                @"--entrapment-in-targets", @"0.25",
            }).BuildSettings;
            Assert.AreEqual(@"in.fasta", settings.InputFasta);
            Assert.AreEqual(0, settings.Digest.EnzymeIndex);
            Assert.AreEqual(100, settings.Digest.MaxMissedCleavages);
            Assert.AreEqual(-5L, settings.EntrapmentSeed);
            CollectionAssert.AreEqual(new[] { 3 }, settings.Charges);
            Assert.IsFalse(settings.SimilarityGate);
            Assert.IsFalse(settings.FailOnPairingViolation);
            Assert.AreEqual(0.5, settings.EntrapmentRatio);
            Assert.AreEqual(0.25, settings.EntrapmentInTargetsRatio);
            // The floor on the ratio applies to the total of the two.
            settings = CarafeCommandLine.Parse(new[] { @"--build-entrapment-fasta", @"o", @"--db", @"i", @"--entrapment", @"--entrapment-ratio", @"0.05", @"--entrapment-in-targets", @"0.05" }).BuildSettings;
            Assert.AreEqual(0.05, settings.EntrapmentRatio);
            Assert.AreEqual(0.05, settings.EntrapmentInTargetsRatio);
            Assert.AreEqual(18, CarafeCommandLine.Parse(new[] { @"--build-entrapment-fasta", @"o", @"--db", @"i", @"--enzyme", @"nocut" })
                .BuildSettings.Digest.EnzymeIndex);

            var reconcile = CarafeCommandLine.Parse(new[] { @"--reconcile-manifest", @"out.tsv", @"--manifest", @"in.tsv", @"--predicted-library", @"lib.blib" });
            Assert.AreEqual(CarafeCommandMode.reconcile_manifest, reconcile.Mode);
            Assert.AreEqual(@"out.tsv", reconcile.ReconcileManifestOut);
            Assert.AreEqual(@"in.tsv", reconcile.ReconcileManifestIn);
            Assert.AreEqual(@"lib.blib", reconcile.ReconcileLibrary);
            Assert.AreEqual(CarafeCommandMode.help, CarafeCommandLine.Parse(new string[0]).Mode);
            Assert.AreEqual(CarafeCommandMode.help, CarafeCommandLine.Parse(new[] { @"-h" }).Mode);

            // Where Carafe stops with an error.
            AssertCommandLineError(@"-build_entrapment_fasta o");
            AssertCommandLineError(@"-build_entrapment_fasta o -db i -entrapment_ratio 0.5");
            AssertCommandLineError(@"-build_entrapment_fasta o -db i -entrapment_db f");
            AssertCommandLineError(@"-build_entrapment_fasta o -db i -entrapment -entrapment_ratio 1.5");
            AssertCommandLineError(@"-build_entrapment_fasta o -db i -entrapment -entrapment_ratio 0.05");
            // Entrapment on target proteins: needs entrapment, is above 0 and at most 1, and is
            // added to the ratio, whose limits apply to the total (the default ratio is 1).
            AssertCommandLineError(@"-build_entrapment_fasta o -db i -entrapment_in_targets 0.5");
            AssertCommandLineError(@"-build_entrapment_fasta o -db i -entrapment -entrapment_in_targets 0");
            AssertCommandLineError(@"-build_entrapment_fasta o -db i -entrapment -entrapment_in_targets 1.5");
            AssertCommandLineError(@"-build_entrapment_fasta o -db i -entrapment -entrapment_in_targets 0.1");
            AssertCommandLineError(@"-build_entrapment_fasta o -db i -entrapment -entrapment_ratio 0.04 -entrapment_in_targets 0.04");
            AssertCommandLineError(@"-build_entrapment_fasta o -db i -enzyme 19");
            // An enzyme name is accepted (CarafeSharp takes any name in the table, Carafe only NoCut);
            // one not in the table is refused.
            AssertCommandLineError(@"-build_entrapment_fasta o -db i -enzyme NotAnEnzyme");
            AssertCommandLineError(@"-build_entrapment_fasta o -db i -not_an_option");
            AssertCommandLineError(@"-build_entrapment_fasta o -db -entrapment");
            AssertCommandLineError(@"-reconcile_manifest o -manifest m");
            // -db on its own is library prediction, which takes a FASTA.
            Assert.AreEqual(CarafeCommandMode.predict_library, CarafeCommandLine.Parse(new[] { @"--db", @"i.fasta" }).Mode);
            Assert.ThrowsException<NotSupportedException>(() => CarafeCommandLine.Parse(new[] { @"--db", @"i" }));
        }

        /// <summary>
        /// Entrapment placed on target proteins: round((r1 + r2) * n) of the n targets carry
        /// entrapment and round(r2 * n) of those sit on a target protein, by shuffle and by
        /// foreign draw; each such p_target row carries exactly one unsuffixed label that is a
        /// target row's, and its p_decoy the decoy prefix on that label; the rest keep the suffix;
        /// the FASTA counters are coherent; the same seeds give the same files; and the in-process
        /// route marks both halves of every entrapment pair. A build without the option is
        /// unchanged, which the fixture hashes above prove.
        /// </summary>
        [TestMethod]
        public void TestEntrapmentOnTargetProteins()
        {
            string folder = CreateTestFolder();
            try
            {
                string targets = WriteLines(folder, @"targets.fasta", TARGET_FASTA);
                string foreign = WriteLines(folder, @"foreign.fasta", FOREIGN_FASTA);
                AssertEntrapmentOnTargetProteins(folder, @"shuffle", GUI + @" -entrapment -entrapment_ratio 0.4 -entrapment_in_targets 0.3",
                    0.4, 0.3, targets, foreign);
                AssertEntrapmentOnTargetProteins(folder, @"foreign", GUI + @" -entrapment -entrapment_db " + FOREIGN_PLACEHOLDER +
                                                                   @" -entrapment_ratio 0.5 -entrapment_in_targets 0.5", 0.5, 0.5, targets, foreign);
                // Every entrapment peptide on a target protein.
                AssertEntrapmentOnTargetProteins(folder, @"all", GUI + @" -entrapment -entrapment_ratio 0 -entrapment_in_targets 1",
                    0.0, 1.0, targets, foreign);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        private void AssertEntrapmentOnTargetProteins(string folder, string name, string arguments, double ratio, double inTargetsRatio,
            string targets, string foreign)
        {
            string outputFolder = Path.Combine(folder, name);
            var settings = BuildSettings(arguments, targets, foreign, outputFolder);
            Assert.AreEqual(inTargetsRatio, settings.EntrapmentInTargetsRatio, name);
            var result = new EntrapmentFastaBuilder(settings).Run();
            int n = result.QuartetsBuilt;
            Assert.AreEqual(JavaText.Round((ratio + inTargetsRatio) * n), n - result.EntrapmentNotSelected, name);
            var kept = new EntrapmentFastaBuilder(settings).Build(new EntrapmentFastaResult(), out _);
            int expectedOnTarget = (int)Math.Min(JavaText.Round(inTargetsRatio * n), kept.Count(q => q.PTarget != null));
            Assert.IsTrue(expectedOnTarget > 0, name);
            Assert.AreEqual(expectedOnTarget, result.EntrapmentOnTargetProteins, name);
            Assert.AreEqual(expectedOnTarget, kept.Count(q => q.PTargetSources != null), name);

            // The same seeds give the same files.
            string fasta = Path.Combine(outputFolder, @"peptides.fasta");
            string manifest = Path.Combine(outputFolder, @"pairing.tsv");
            string again = Path.Combine(folder, name + @"-again");
            new EntrapmentFastaBuilder(BuildSettings(arguments, targets, foreign, again)).Run();
            Assert.AreEqual(Sha256File(fasta), Sha256File(Path.Combine(again, @"peptides.fasta")), name);
            Assert.AreEqual(Sha256File(manifest), Sha256File(Path.Combine(again, @"pairing.tsv")), name);

            var rows = ReadLfLines(manifest).Skip(1).Select(line => line.Split('\t')).ToList();
            var targetLabels = new HashSet<string>(rows.Where(r => r[3] == PairingManifest.KIND_TARGET).SelectMany(r => r[2].Split(';')));
            var pDecoyProteins = rows.Where(r => r[3] == PairingManifest.KIND_P_DECOY).ToDictionary(r => r[4], r => r[2]);
            int onTarget = 0;
            foreach (var row in rows.Where(r => r[3] == PairingManifest.KIND_P_TARGET))
            {
                string[] labels = row[2].Split(';');
                if (labels.All(label => label.Contains(settings.EntrapmentSuffix)))
                    continue;
                onTarget++;
                Assert.AreEqual(1, labels.Length, row[2]);
                Assert.IsTrue(targetLabels.Contains(labels[0]), row[2]);
                Assert.AreEqual(settings.DecoyPrefix + labels[0], pDecoyProteins[row[4]], row[2]);
            }
            Assert.AreEqual(expectedOnTarget, onTarget, name);
            AssertCoherentCounters(fasta, settings);
            AssertEntrapmentPairs(settings, expectedOnTarget);
            TestContext.WriteLine(@"{0}: {1} targets, {2} with entrapment, {3} on target proteins", name, n, n - result.EntrapmentNotSelected, onTarget);
        }

        /// <summary>
        /// Every target-side header label is unique, and so is every decoy-side one (a decoy
        /// shares its target's counter by design): an entrapment peptide placed on a target
        /// protein took a counter of that protein's own.
        /// </summary>
        private static void AssertCoherentCounters(string fasta, EntrapmentFastaSettings settings)
        {
            var targetSide = new HashSet<string>();
            var decoySide = new HashSet<string>();
            foreach (string header in ReadLfLines(fasta).Where(line => line.StartsWith(">")))
            {
                foreach (string label in header.Substring(1).Split(';'))
                {
                    Assert.IsTrue(LibraryContract.PEPTIDE_SUFFIX.IsMatch(label), label);
                    var side = label.StartsWith(settings.DecoyPrefix) ? decoySide : targetSide;
                    Assert.IsTrue(side.Add(label), @"Duplicate header label: " + label);
                }
            }
        }

        /// <summary>
        /// The in-process route: the pairing marks both halves of every entrapment pair, and the
        /// decoy of an entrapment peptide on a target protein carries that protein's label, prefixed.
        /// </summary>
        private static void AssertEntrapmentPairs(EntrapmentFastaSettings settings, int expectedOnTarget)
        {
            var peptides = LibraryPeptides.Build(settings, TextWriter.Null);
            var ordered = peptides.Peptides.OrderBy(p => p, StringComparer.Ordinal).ToList();
            var precursors = ordered.Select((sequence, i) => new DecoyPairPlanner.Precursor(i + 1, sequence, 2, string.Empty)).ToList();
            var rows = DecoyPairPlanner.Plan(precursors, peptides.Pairing, out _);
            var entrapment = rows.Where(r => r.IsEntrapment).ToList();
            Assert.IsTrue(entrapment.Count > 0);
            Assert.AreEqual(entrapment.Count(r => r.IsDecoy), entrapment.Count(r => !r.IsDecoy));
            int onTarget = 0;
            foreach (var row in entrapment.Where(r => r.IsDecoy))
            {
                Assert.IsTrue(row.TargetRefSpectraId.HasValue);
                string targetProteins = peptides.PeptideToProteins[ordered[row.TargetRefSpectraId.Value - 1]];
                string decoyProteins = peptides.PeptideToProteins[ordered[row.RefSpectraId - 1]];
                if (targetProteins.Contains(settings.EntrapmentSuffix))
                    continue;
                onTarget++;
                // A decoy generated twice carries both records' proteins.
                CollectionAssert.Contains(decoyProteins.Split(';'), settings.DecoyPrefix + targetProteins, decoyProteins);
            }
            Assert.IsTrue(onTarget > 0 && onTarget <= expectedOnTarget, string.Format(@"{0} of {1} on target proteins", onTarget, expectedOnTarget));
        }

        private static EntrapmentFastaResult Build(string arguments, string targets, string foreign, string outputFolder)
        {
            return new EntrapmentFastaBuilder(BuildSettings(arguments, targets, foreign, outputFolder)).Run();
        }

        private static EntrapmentFastaSettings BuildSettings(string arguments, string targets, string foreign, string outputFolder)
        {
            var args = new List<string>
            {
                @"--build-entrapment-fasta", Path.Combine(outputFolder, @"peptides.fasta"),
                @"--db", targets,
                @"--manifest", Path.Combine(outputFolder, @"pairing.tsv"),
            };
            // Substituted after splitting: the test folder's path can contain spaces.
            args.AddRange(arguments.Split(' ').Select(arg => arg == FOREIGN_PLACEHOLDER ? foreign : arg));
            return CarafeCommandLine.Parse(args).BuildSettings;
        }

        /// <summary>NoCut takes each record whole, so its output can be read in full.</summary>
        private static void AssertNoCutOutput(string folder)
        {
            CollectionAssert.AreEqual(new[]
            {
                @">sp|plainident_pep00001|plainident", @"GVFRRDAHKSEVAHRFKDLGEENFKALVLIAFAQYLQQCPFEDHVK",
                @">sp|P10010_pep00001|SINGLE_HUMAN", @"K",
                @">|ACC6_pep00001|ENTRY6",
                @"MAEGEITTFTALTEKFNLPPGNYKKPKLLYCSNGGHFLRILPDGTVDGTRDRSDQHIQLQLSAESVGEVYIKSTETGQYLAMDTDGLLYGSQTPNEECLFLERLEENHYNTYISK",
                @">sp|P10011_pep00001|MONO_HUMAN", @"MGGGGGGGGKAAAAAAAAKGGGGGGGGGRCCCCCCCCKEEEEEEEEEEKLLLLLLLLLLLR",
                // One counter per accession, across databases.
                @">tr|P10001_pep00001|ALPHA_HUMAN", @"MKWVTFISLLFLFSSAYSRGVFRRDAHKSEVAHR",
                @">sp|P10001_pep00002|ALPHA_HUMAN",
                @"MKWVTFISLLFLFSSAYSRGVFRRDAHKSEVAHRFKDLGEENFKALVLIAFAQYLQQCPFEDHVKLVNEVTEFAKTCVADESAENCDKSLHTLFGDKLCTVATLR" +
                @"ETYGEMADCCAKQEPERNECFLQHKDDNPNLPRLVRPEVDVMCTAFHDNEETFLKK",
                @">sp|ACC7_pep00001|ACC7", @"MQIFVKTLTGKTITLEVEPSDTIENVKAKIQDKEGIPPDQQRLIFAGKQLEDGRTLSDYNIQKESTLHLVLRLRGG",
                @">sp|P10002_pep00001|BETA_HUMAN",
                @"MSDKPDMAEIEKFDKSKLKKTETQEKNPLPSKETIEQEKQAGESLVNEVTEFAKTCVADESAENCDKPEPTIDEKEDITPEPKLEVELKAAAAAAAAAAAAAAAGATCLERGSMMEEAPR",
                @">db2|ACC5_pep00001|ACC5", @"MSTNPKPQRKTKRNTNRRPQDVKFPGGGQIVGGVYLLPRRGPRLGVRATRKTSERSQPR",
            }, ReadLfLines(Path.Combine(folder, @"peptides.fasta")));
            var manifest = ReadLfLines(Path.Combine(folder, @"pairing.tsv"));
            Assert.AreEqual(PairingManifest.HEADER_WITH_ENTRAPMENT_METHOD, manifest[0]);
            Assert.AreEqual("K\tNo\tsp|P10010|SINGLE_HUMAN\ttarget\t1\t", manifest[2]);
            Assert.AreEqual(10, manifest.Length);
        }

        /// <summary>A shared peptide lists every source, sorted by accession then entry, stably.</summary>
        private static void AssertTrainRows(string manifestPath)
        {
            var manifest = ReadLfLines(manifestPath);
            CollectionAssert.Contains(manifest,
                "DAHKSEVAHR\tNo\tsp|P10001|ALPHA_HUMAN;tr|P10001|ALPHA_HUMAN;sp|plainident|plainident\ttarget\t6\t");
            CollectionAssert.Contains(manifest,
                "HAVESKHADR\tYes\tdecoy_sp|P10001|ALPHA_HUMAN;decoy_tr|P10001|ALPHA_HUMAN;decoy_sp|plainident|plainident\tdecoy\t6\t");
        }

        /// <summary>
        /// Reconciles the "lib" manifest against a library missing one target, one decoy and one
        /// p_decoy of the first 12 groups, with one I/L-swapped lower-case sequence, one peptide
        /// the manifest lacks and one duplicate; as a TSV and as a .blib.
        /// </summary>
        private void AssertReconciliation(string folder, string manifest)
        {
            var librarySequences = LibrarySequences(manifest);
            string tsv = Path.Combine(folder, @"library.tsv");
            WriteLibrary(tsv, librarySequences);
            string reconciled = Path.Combine(folder, @"reconciled.tsv");
            var result = PairingManifestReconciler.Run(manifest, tsv, reconciled);
            Assert.AreEqual(RECONCILED_SHA256, Sha256File(TestData.FdrBenchColumnsOf(reconciled)));
            // The entrapment_method column survives reconciliation: on the p_target rows alone.
            var reconciledLines = ReadLfLines(reconciled);
            Assert.AreEqual(PairingManifest.HEADER_WITH_ENTRAPMENT_METHOD, reconciledLines[0]);
            foreach (string line in reconciledLines.Skip(1))
            {
                string[] fields = line.Split('\t');
                Assert.AreEqual(6, fields.Length, line);
                Assert.AreEqual(fields[3] == PairingManifest.KIND_P_TARGET ? PairingManifest.ENTRAPMENT_METHOD_SHUFFLE : string.Empty, fields[5], line);
            }
            Assert.AreEqual(99, result.GroupsIn);
            Assert.AreEqual(11, result.GroupsKept);
            Assert.AreEqual(88, result.GroupsDropped);
            Assert.AreEqual(396, result.RowsIn);
            Assert.AreEqual(42, result.RowsKept);
            Assert.AreEqual(354, result.RowsDropped);
            Assert.AreEqual(46, result.LibraryPeptides);
            Assert.AreEqual(1, result.LibraryPeptidesNotInManifest);
            Assert.AreEqual(1, result.KeptTargetsWithoutDecoy);

            string blib = Path.Combine(folder, @"library.blib");
            WriteBlib(blib, librarySequences);
            string fromBlib = Path.Combine(folder, @"reconciled-blib.tsv");
            PairingManifestReconciler.Run(manifest, blib, fromBlib);
            Assert.AreEqual(RECONCILED_SHA256, Sha256File(TestData.FdrBenchColumnsOf(fromBlib)));
            TestContext.WriteLine(@"reconciled: {0}/{1} groups, {2}/{3} rows", result.GroupsKept, result.GroupsIn, result.RowsKept, result.RowsIn);
        }

        private static List<string> LibrarySequences(string manifest)
        {
            var sequences = new List<string>();
            foreach (string line in ReadLfLines(manifest).Skip(1))
            {
                string[] fields = line.Split('\t');
                string sequence = fields[0];
                string type = fields[3];
                int pair = int.Parse(fields[4]);
                if (pair >= 12 || (pair == 3 && type == PairingManifest.KIND_TARGET) ||
                    (pair == 5 && type == PairingManifest.KIND_DECOY) ||
                    (pair == 7 && type == PairingManifest.KIND_P_DECOY))
                {
                    continue;
                }
                if (pair == 9 && type == PairingManifest.KIND_TARGET)
                    sequence = sequence.ToLowerInvariant().Replace('l', 'i');
                sequences.Add(sequence);
            }
            sequences.Add(@"EXTRAPEPTIDEK");
            sequences.Add(sequences[0]);
            return sequences;
        }

        private static void WriteLibrary(string path, IEnumerable<string> sequences)
        {
            var lines = new List<string> { "ModifiedPeptide\tStrippedPeptide\tPrecursorCharge" };
            lines.AddRange(sequences.Select(s => @"_" + s + "_\t" + s + "\t2"));
            File.WriteAllText(path, string.Join("\n", lines) + "\n");
        }

        private static void WriteBlib(string path, IEnumerable<string> sequences)
        {
            using (var connection = new SQLiteConnection(new SQLiteConnectionStringBuilder { DataSource = path }.ToString()))
            {
                connection.Open();
                using (var create = new SQLiteCommand(@"CREATE TABLE RefSpectra (id INTEGER PRIMARY KEY, peptideSeq TEXT)", connection))
                    create.ExecuteNonQuery();
                foreach (string sequence in sequences)
                {
                    using (var insert = new SQLiteCommand(@"INSERT INTO RefSpectra (peptideSeq) VALUES (@seq)", connection))
                    {
                        insert.Parameters.AddWithValue(@"@seq", sequence);
                        insert.ExecuteNonQuery();
                    }
                }
            }
            SQLiteConnection.ClearAllPools();
        }

        /// <summary>An <see cref="ArgumentException"/> or a subclass, as Carafe exits with an error.</summary>
        private static void AssertCommandLineError(string commandLine)
        {
            try
            {
                CarafeCommandLine.Parse(commandLine.Split(' '));
            }
            catch (ArgumentException)
            {
                return;
            }
            Assert.Fail(@"No error for: " + commandLine);
        }

        private static EntrapmentQuartet Quartet(string target, string pTarget, string decoy, string pDecoy, ProteinRecord source)
        {
            var quartet = new EntrapmentQuartet(target) { PTarget = pTarget, Decoy = decoy, PDecoy = pDecoy };
            quartet.Sources.Add(source);
            return quartet;
        }

        private string CreateTestFolder()
        {
            string root = TestContext.TestRunDirectory ?? Path.GetTempPath();
            string folder = Path.Combine(root, @"EntrapmentFasta_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static string WriteLines(string folder, string name, IEnumerable<string> lines)
        {
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, string.Join("\n", lines) + "\n");
            return path;
        }

        private static string[] ReadLfLines(string path)
        {
            string text = File.ReadAllText(path);
            Assert.IsFalse(text.Contains('\r'), path);
            return text.TrimEnd('\n').Split('\n');
        }

        private static string Sha256File(string path)
        {
            using (var stream = File.OpenRead(path))
                return Convert.ToHexStringLower(SHA256.HashData(stream));
        }

        private sealed class FixtureBuild
        {
            public FixtureBuild(string name, string arguments, string fastaSha256, string manifestSha256)
            {
                Name = name;
                Arguments = arguments;
                FastaSha256 = fastaSha256;
                ManifestSha256 = manifestSha256;
            }

            public string Name { get; }
            public string Arguments { get; }
            public string FastaSha256 { get; }
            public string ManifestSha256 { get; }
        }
    }
}
