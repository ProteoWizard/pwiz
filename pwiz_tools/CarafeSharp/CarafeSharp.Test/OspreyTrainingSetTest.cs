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
using pwiz.CarafeSharp.IO;
using pwiz.CarafeSharp.Proteome;
using pwiz.CarafeSharp.Training;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// Training on Osprey's training export: modification mapping to alphabase, the masking
    /// policy on hand-built records, the training command line and finding the exports.
    /// </summary>
    [TestClass]
    public class OspreyTrainingSetTest
    {
        [TestMethod]
        public void TestModificationMapper()
        {
            // Carbamidomethyl on C and Oxidation on M by UniMod id: residue k (0-based) is site k + 1.
            Assert.IsTrue(OspreyModificationMapper.TryMap(@"AMCK", @"AM(UniMod:35)C(UniMod:4)K", new[] { 1, 2 },
                new[] { 15.994915, 57.021464 }, new[] { 35, 4 }, out var peptide, out string reason), reason);
            Assert.AreEqual(@"Oxidation@M;Carbamidomethyl@C", peptide.ModsText);
            Assert.AreEqual(@"2;3", peptide.ModSitesText);

            // Without a UniMod id the mass decides; Osprey's Skyline-style masses round to 4 decimals.
            Assert.IsTrue(OspreyModificationMapper.TryMap(@"PEPCK", @"PEPC[+57.0215]K", new[] { 3 },
                new[] { 57.0215 }, new[] { -1 }, out peptide, out reason), reason);
            Assert.AreEqual(@"Carbamidomethyl@C", peptide.ModsText);
            Assert.AreEqual(@"4", peptide.ModSitesText);

            // A modification before the first residue is N-terminal (site 0), not on residue 1.
            Assert.IsTrue(OspreyModificationMapper.TryMap(@"SAMPLER", @"(UniMod:1)SAMPLER", new[] { 0 },
                new[] { 42.010565 }, new[] { 1 }, out peptide, out reason), reason);
            Assert.AreEqual(@"0", peptide.ModSitesText);
            StringAssert.EndsWith(peptide.ModsText, @"N-term");
            // The same mass written on the residue is the residue's modification.
            Assert.IsTrue(OspreyModificationMapper.TryMap(@"SAMPLER", @"S(UniMod:1)AMPLER", new[] { 0 },
                new[] { 42.010565 }, new[] { 1 }, out peptide, out reason), reason);
            Assert.AreEqual(@"Acetyl@S", peptide.ModsText);
            Assert.AreEqual(@"1", peptide.ModSitesText);

            // A mass alphabase does not know cannot be featurized.
            Assert.IsFalse(OspreyModificationMapper.TryMap(@"PEPCK", @"PEPC[+12.3456]K", new[] { 3 },
                new[] { 12.3456 }, new[] { -1 }, out peptide, out reason));
            Assert.IsNull(peptide);
            StringAssert.Contains(reason, @"12.3456");
        }

        [TestMethod]
        public void TestMaskingPolicy()
        {
            // PEPTIDEK, L = 8: 7 positions x (b+, b++, y+, y++) = 28 slots, all applicable at charge 2.
            // Every ion matched with a clean profile; slot 10 (y5+) is the top ion.
            var record = NewRecord(@"PEPTIDEK", 2);
            for (int slot = 0; slot < record.SlotCount; slot++)
                Match(record, slot, slot == 10 ? 1000 : 100 + slot, 0.95f);
            var settings = new OspreyMaskingSettings();
            var masked = Apply(settings, record);
            Assert.IsNull(masked.RejectReason);
            Assert.AreEqual(28, masked.MatchedCount);
            Assert.AreEqual(10, masked.TopSlot);
            // b1 (position 0, both charges) and y1 (position 6, both charges) are always masked.
            CollectionAssert.AreEqual(new[] { 0, 1, 26, 27 }, InvalidSlots(masked));
            Assert.AreEqual(24, masked.ValidMatchedCount);
            Assert.AreEqual(4L, masked.MaskedBy[OspreyMaskingPolicy.RULE_ORDINAL]);
            // Intensities are relative to the top ion.
            Assert.AreEqual(1.0, masked.Intensities[10]);
            Assert.AreEqual(0.102, masked.Intensities[2], 1e-12);

            // Poor correlation masks a matched ion; the threshold passes inclusively.
            record.CorrPolish[5] = 0.5f;
            record.CorrPolish[6] = 0.8f;
            // A peak another confident precursor matched at the same apex masks the ion.
            record.SharedApexCount[9] = 1;
            // Two ions of this precursor on one peak (same observed m/z) are both masked.
            record.IonMz[13] = record.IonMz[12] + 0.3;
            record.ApexMzError[13] = -0.3f;
            // An ion elevated at both boundaries (the others are 0 there) is skewed.
            record.XicStart[14] = record.XicEnd[14] = 0.5f * record.ApexIntensity[14];
            masked = Apply(settings, record);
            CollectionAssert.AreEqual(new[] { 0, 1, 5, 9, 12, 13, 14, 26, 27 }, InvalidSlots(masked));
            Assert.AreEqual(1L, masked.MaskedBy[OspreyMaskingPolicy.RULE_CORRELATION]);
            Assert.AreEqual(1L, masked.MaskedBy[OspreyMaskingPolicy.RULE_SHARED_APEX]);
            Assert.AreEqual(2L, masked.MaskedBy[OspreyMaskingPolicy.RULE_SELF_SHARED]);
            Assert.AreEqual(1L, masked.MaskedBy[OspreyMaskingPolicy.RULE_SKEW]);
            // A one-sided elevation is not a skew.
            record.XicEnd[14] = 0;
            Assert.AreEqual(0.0, Apply(settings, record).Invalid[14]);
            // Sharing with another precursor only masks when it is the better identification, if asked.
            settings.SharedOnlyWhenBetterClaimant = true;
            Assert.AreEqual(0.0, Apply(settings, record).Invalid[9]);
            record.IonFlags[9] |= OspreyIonFlags.BETTER_CLAIMANT_APEX;
            Assert.AreEqual(1.0, Apply(settings, record).Invalid[9]);

            // An intense b2 or y2 (at least half the top ion) needs a correlation above 0.9.
            record.ApexIntensity[4] = 600;
            record.CorrPolish[4] = 0.95f;
            Assert.AreEqual(0.0, Apply(settings, record).Invalid[4]);
            record.CorrPolish[4] = 0.85f;
            masked = Apply(settings, record);
            Assert.AreEqual(1.0, masked.Invalid[4]);
            Assert.AreEqual(1L, masked.MaskedBy[OspreyMaskingPolicy.RULE_LOW_ORDINAL]);
            settings.LowOrdinalB = 0;
            Assert.AreEqual(0.0, Apply(settings, record).Invalid[4]);

            // An unmatched ion trains as 0 and stays valid: the model learns it is absent.
            record.IonFlags[16] &= unchecked((byte)~OspreyIonFlags.MATCHED_AT_APEX);
            record.ApexIntensity[16] = 0;
            masked = Apply(settings, record);
            Assert.AreEqual(0.0, masked.Intensities[16]);
            Assert.AreEqual(0.0, masked.Invalid[16]);
            Assert.AreEqual(27, masked.MatchedCount);
            // So does an ion outside the scan window, as Carafe trains, unless asked to mask it.
            record.IonFlags[18] = OspreyIonFlags.APPLICABLE;
            record.ApexIntensity[18] = 0;
            Assert.AreEqual(0.0, Apply(settings, record).Invalid[18]);
            settings.OutOfRange = OutOfRangeIons.masked;
            masked = Apply(settings, record);
            Assert.AreEqual(1.0, masked.Invalid[18]);
            Assert.AreEqual(1L, masked.MaskedBy[OspreyMaskingPolicy.RULE_OUT_OF_RANGE]);

            // The top ion must be valid.
            record.CorrPolish[10] = 0.1f;
            Assert.AreEqual(OspreyMaskingPolicy.REJECT_TOP_ION_INVALID, Apply(settings, record).RejectReason);
            settings.RequireTopIonValid = false;
            masked = Apply(settings, record);
            Assert.IsNull(masked.RejectReason);
            Assert.AreEqual(1.0, masked.Intensities[10]);

            // Too few valid or matched ions reject the spectrum.
            settings.MinValidIons = 100;
            Assert.AreEqual(OspreyMaskingPolicy.REJECT_FEW_VALID, Apply(settings, record).RejectReason);
            settings.MinMatchedIons = 100;
            Assert.AreEqual(OspreyMaskingPolicy.REJECT_FEW_MATCHED, Apply(settings, record).RejectReason);

            // The polish's row effects give the intensities when asked, on one scale.
            var polished = NewRecord(@"PEPTIDEK", 2);
            polished.MedianPolishFitted = true;
            for (int slot = 0; slot < polished.SlotCount; slot++)
            {
                Match(polished, slot, 100 + slot, 0.95f);
                polished.PolishRowEffect[slot] = (float)Math.Log(slot + 1);
            }
            masked = Apply(new OspreyMaskingSettings { IntensitySource = TrainingIntensitySource.polish }, polished);
            // The top ion is still the most intense apex peak above the ordinal floor (slot 25).
            Assert.AreEqual(25, masked.TopSlot);
            Assert.AreEqual(3.0 / 26, masked.Intensities[2], 1e-6);

            // A charge 1 precursor has no charge 2 ions; they are masked.
            var singly = NewRecord(@"PEPTIDEK", 1);
            for (int slot = 0; slot < singly.SlotCount; slot += 2)
                Match(singly, slot, 100 + slot, 0.95f);
            Assert.AreEqual(14L, Apply(new OspreyMaskingSettings(), singly).MaskedBy[OspreyMaskingPolicy.RULE_NOT_APPLICABLE]);
        }

        [TestMethod]
        public void TestTrainingCommandLine()
        {
            // Carafe's own fine-tune command from its Osprey workflow (June Stellar run, parameter.txt).
            var commandLine = CarafeCommandLine.Parse((@"-db lib.fasta -i osprey_train\osprey.blib -ms run_21.mzML -o out " +
                @"-fdr 0.01 -ptm_site_prob 0.75 -ptm_site_qvalue 0.01 -itol 0.4 -itolu Da -rf -rf_rt_win auto -cor 0.8 " +
                @"-min_mz 200 -n_ion_min 2 -c_ion_min 2 -mode general -device gpu -enzyme NoCut -miss_c 1 -fixMod 1 " +
                @"-varMod 0 -maxVar 1 -clip_n_m -minLength 7 -maxLength 35 -min_pep_mz 400 -max_pep_mz 900 -min_pep_charge 2 " +
                @"-max_pep_charge 3 -lf_frag_mz_min 200 -lf_frag_mz_max 1960 -lf_top_n_frag 20 -lf_min_n_frag 2 " +
                @"-lf_frag_n_min 2 -lf_type DIA-NN -se Osprey -decoy_prefix decoy_ -tf all -nm -nf 4 -min_n 4 -valid " +
                @"-na 0 -ez -fast").Split(' '));
            Assert.AreEqual(CarafeCommandMode.train, commandLine.Mode);
            var settings = commandLine.TrainingSettings;
            Assert.AreEqual(@"osprey_train\osprey.blib", settings.Identifications);
            CollectionAssert.AreEqual(new[] { @"run_21.mzML" }, settings.MsFiles.ToArray());
            Assert.AreEqual(@"out", settings.OutputDirectory);
            Assert.AreEqual(0.8, settings.MinCorrelation);
            Assert.AreEqual(2, settings.LowOrdinalB);
            Assert.AreEqual(2, settings.LowOrdinalY);
            Assert.AreEqual(2, settings.MinFragmentOrdinal);
            Assert.AreEqual(4, settings.MinMatchedIons);
            Assert.AreEqual(4, settings.MinValidIons);
            Assert.IsTrue(settings.RequireTopIonValid);
            Assert.IsTrue(settings.TrainMs2 && settings.TrainRt);
            Assert.AreEqual(TrainingSettings.DEFAULT_SEED, settings.Seed);
            // The library follows, predicted from the output folder's models and training state.
            Assert.IsNotNull(settings.Library);
            Assert.IsTrue(settings.Library.ApplyTrainingRunMeta);
            Assert.IsFalse(settings.Library.ApplyModelDirectoryMeta);
            Assert.AreEqual(1960.0, settings.Library.MaxFragmentMz);
            // Carafe's XIC options are Osprey's to decide; they are reported, not silently dropped.
            Assert.AreEqual(2, commandLine.Warnings.Count);
            StringAssert.Contains(commandLine.Warnings[0], @"-itol -itolu -rf -rf_rt_win -min_mz");
            StringAssert.Contains(commandLine.Warnings[1], @"-ez");

            // Carafe's code defaults when the options are absent; training exports named directly
            // train without -ms, which Carafe could never have read.
            Assert.AreEqual(OspreyTrainingExport.FILE_SUFFIX, CarafeCommandLine.TRAINING_EXPORT_SUFFIX);
            settings = CarafeCommandLine.Parse(new[] { @"-i", @"a.training.parquet", @"-tf", @"ms2" }).TrainingSettings;
            Assert.AreEqual(TrainingSettings.DEFAULT_CORRELATION, settings.MinCorrelation);
            Assert.AreEqual(0, settings.LowOrdinalB);
            Assert.IsFalse(settings.RequireTopIonValid);
            Assert.IsTrue(settings.TrainMs2);
            Assert.IsFalse(settings.TrainRt);
            Assert.IsNull(settings.Library);
            Assert.AreEqual(CarafeCommandMode.train, CarafeCommandLine.Parse(new[] { @"-i", Path.GetTempPath() }).Mode);
            // Carafe trains only with -ms: -i without it is a library run, as the GUI's initial library is.
            Assert.AreEqual(CarafeCommandMode.predict_library,
                CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-i", @"osprey.blib" }).Mode);

            // -seed is Carafe's Integer.parseInt, and numpy rejects a negative seed.
            Assert.AreEqual(7u, ParseExport(@"-seed", @"7").TrainingSettings.Seed);
            AssertThrows<ArgumentException>(() => ParseExport(@"-seed", @"-1"));
            AssertThrows<ArgumentException>(() => ParseExport(@"-seed", @"4294967296"));
            // A flag given a value is an error, as it is to Carafe's option parser.
            AssertThrows<ArgumentException>(() => ParseExport(@"-valid=false"));
            AssertThrows<ArgumentException>(() => ParseExport(@"-device", @"tpu"));
            AssertThrows<NotSupportedException>(() => ParseExport(@"-ai_version", @"v1"));
            AssertThrows<NotSupportedException>(() => ParseExport(@"-user_var_mods", @"x"));
            AssertThrows<NotSupportedException>(() => ParseExport(@"-mod2mass", @"x"));

            AssertThrows<ArgumentException>(() => CarafeCommandLine.Parse(new[] { @"-ms", @"run.mzML" }));
            AssertThrows<NotSupportedException>(() => CarafeCommandLine.Parse(new[] { @"-i", @"report.tsv", @"-ms", @"run.mzML", @"-se", @"DIA-NN" }));
            AssertThrows<NotSupportedException>(() => CarafeCommandLine.Parse(new[] { @"-i", @"x.blib", @"-ms", @"run.mzML", @"-na", @"2" }));
            AssertThrows<NotSupportedException>(() => CarafeCommandLine.Parse(new[] { @"-i", @"x.blib", @"-ms", @"run.mzML", @"-tf", @"test" }));

            // Training checks the library FASTA, then opens the pretrained models, before it
            // reads any export.
            string folder = Path.Combine(Path.GetTempPath(), @"CarafeSharpTrainer_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(folder);
            try
            {
                string fasta = Path.Combine(folder, @"library.fasta");
                string zip = Path.Combine(folder, @"pretrained_models.zip");
                var trainer = new ModelTrainer(CarafeCommandLine.Parse(new[] { @"-i", Path.Combine(folder, @"osprey.blib"),
                    @"-ms", @"run.mzML", @"-o", folder, @"-db", fasta, @"-pretrained", zip }).TrainingSettings, null);
                Assert.AreEqual(fasta, Assert.ThrowsException<FileNotFoundException>(() => trainer.Run()).FileName);
                File.WriteAllText(fasta, ">P1\nPEPTIDEK\n");
                Assert.AreEqual(zip, Assert.ThrowsException<FileNotFoundException>(() => trainer.Run()).FileName);
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }

        [TestMethod]
        public void TestTrainingExportLocator()
        {
            string folder = Path.Combine(Path.GetTempPath(), @"CarafeSharpLocator_" + Guid.NewGuid().ToString(@"N"));
            try
            {
                string results = Path.Combine(folder, @"osprey");
                string raw = Path.Combine(folder, @"raw");
                Directory.CreateDirectory(results);
                Directory.CreateDirectory(raw);
                string blib = Touch(results, @"osprey.blib");
                string a = Touch(results, @"run_a" + OspreyTrainingExport.FILE_SUFFIX);
                string b = Touch(results, @"run_b" + OspreyTrainingExport.FILE_SUFFIX);
                string c = Touch(raw, @"run_c" + OspreyTrainingExport.FILE_SUFFIX);
                Touch(raw, @"run_a.mzML");
                Touch(raw, @"run_c.raw");

                // A folder of exports, all of them or the -ms runs among them.
                CollectionAssert.AreEqual(new[] { a, b }, TrainingExportLocator.Find(results, null).ToArray());
                CollectionAssert.AreEqual(new[] { a }, TrainingExportLocator.Find(results, new[] { Path.Combine(raw, @"run_a.mzML") }).ToArray());
                // Osprey's blib: every export beside it, or each -ms run's beside it, then beside the run.
                CollectionAssert.AreEqual(new[] { a, b }, TrainingExportLocator.Find(blib, null).ToArray());
                CollectionAssert.AreEqual(new[] { a, c }, TrainingExportLocator.Find(blib, new[] { raw }).ToArray());
                // Exports named directly.
                CollectionAssert.AreEqual(new[] { a, b }, TrainingExportLocator.Find(b + @"," + a, null).ToArray());
                Assert.AreEqual(@"run_b", TrainingExportLocator.RunStem(b));

                AssertThrows<FileNotFoundException>(() => TrainingExportLocator.Find(blib, new[] { Path.Combine(raw, @"run_d.mzML") }));
                AssertThrows<FileNotFoundException>(() => TrainingExportLocator.Find(results, new[] { @"run_d.mzML" }));
                AssertThrows<FileNotFoundException>(() => TrainingExportLocator.Find(raw + @"\missing.blib", null));
                // A folder without exports: Osprey was run without --training-export.
                string empty = Path.Combine(folder, @"empty");
                Directory.CreateDirectory(empty);
                AssertThrows<FileNotFoundException>(() => TrainingExportLocator.Find(empty, null));
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }

        private static string Touch(string folder, string name)
        {
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, string.Empty);
            return path;
        }

        /// <summary>A training command line on an export named directly, plus <paramref name="options"/>.</summary>
        private static CarafeCommandLine ParseExport(params string[] options)
        {
            return CarafeCommandLine.Parse(new[] { @"-i", @"a.training.parquet" }.Concat(options).ToArray());
        }

        private static void AssertThrows<T>(Action action) where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }
            Assert.Fail(@"Expected " + typeof(T).Name);
        }

        private static MaskedSpectrum Apply(OspreyMaskingSettings settings, OspreyTrainingRecord record)
        {
            return new OspreyMaskingPolicy(settings).Apply(record);
        }

        private static int[] InvalidSlots(MaskedSpectrum masked)
        {
            return Enumerable.Range(0, masked.SlotCount).Where(s => masked.Invalid[s] > 0).ToArray();
        }

        private static OspreyTrainingRecord NewRecord(string sequence, int charge)
        {
            int slots = 4 * (sequence.Length - 1);
            var record = new OspreyTrainingRecord
            {
                Sequence = sequence,
                ModifiedSequence = sequence,
                Charge = charge,
                ModPositions = Array.Empty<int>(),
                ModMasses = Array.Empty<double>(),
                ModUnimodIds = Array.Empty<int>(),
                IonMz = Enumerable.Range(0, slots).Select(s => 300.0 + s).ToArray(),
                IonFlags = new byte[slots],
                ApexIntensity = new float[slots],
                ApexMzError = new float[slots],
                LibraryRelIntensity = new float[slots],
                FiniteScanCount = new ushort[slots],
                XicStart = new float[slots],
                XicEnd = new float[slots],
                XicMax = new float[slots],
                CorrPolish = Enumerable.Repeat(float.NaN, slots).ToArray(),
                CorrReference = Enumerable.Repeat(float.NaN, slots).ToArray(),
                PolishRowEffect = new float[slots],
                PolishR2 = new float[slots],
                PolishPositiveResidualMax = new float[slots],
                PolishApexResidual = new float[slots],
                PolishOutlierZ = new float[slots],
                PolishApexRatio = new float[slots],
                PolishRelIntensity = new float[slots],
                SharedApexCount = new byte[slots],
                SharedCoeluteCount = new byte[slots],
                MinClaimantQ = new float[slots],
            };
            for (int slot = 0; slot < slots; slot++)
            {
                bool applicable = slot % 2 == 0 || charge >= 2;
                if (applicable)
                    record.IonFlags[slot] = OspreyIonFlags.APPLICABLE | OspreyIonFlags.IN_SCAN_RANGE;
                else
                    record.IonMz[slot] = double.NaN;
            }
            return record;
        }

        private static void Match(OspreyTrainingRecord record, int slot, float intensity, float correlation)
        {
            record.IonFlags[slot] |= OspreyIonFlags.MATCHED_AT_APEX;
            record.ApexIntensity[slot] = intensity;
            record.XicMax[slot] = intensity;
            record.CorrPolish[slot] = correlation;
            record.CorrReference[slot] = correlation;
        }
    }
}
