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
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Contracts;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.Proteome;
using pwiz.CarafeSharp.Training;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// Reads an Osprey training export of the search Carafe trained on and compares the
    /// training rows CarafeSharp builds from it with Carafe's own (its <c>osprey_new_library</c>
    /// folder): the RT normalizer and collision energy from the footer, how many spectra each
    /// keeps, and the per-ion masking decisions on the spectra both have. Carafe reads its own
    /// XICs and CarafeSharp reads Osprey's evidence, so this is agreement, not identity.
    /// <see cref="TestData"/> finds the export (<c>CARAFESHARP_OSPREY_TRAINING_EXPORT</c>) and the
    /// Carafe folder, the first fine-tuning folder (<c>CARAFESHARP_CARAFE_FINETUNED</c>). The subset
    /// test runs the same checks on the committed <c>TrainingParitySubset.zip</c>, whose export keeps
    /// the rows of a hash sample of sequences, and compares Carafe's spectra of the same sequences.
    /// </summary>
    [TestClass]
    public class OspreyMaskingParityTest
    {
        // Agreement measured when the policy was ported (Stellar _21, Osprey 0a0b74432a):
        // 85.0% of slots, 12,404 spectra kept by both. On the format 2 export Osprey #4708 (a5d15e6a4f)
        // wrote from the .raw: 85.0%, 12,378 of 14,109. The floors leave room for Osprey changes.
        private const double MIN_SLOT_AGREEMENT = 0.83;
        private const double MIN_SHARED_KEPT_FRACTION = 0.80;

        // The footer key of a subset export's sample: the rows whose sequence IsSampled with this modulus.
        private const string SUBSET_MODULUS_KEY = @"carafesharp.subset.sequence_modulus";

        // 64-bit FNV-1a, as the subset builder samples sequences.
        private const ulong FNV_OFFSET_BASIS = 14695981039346656037UL;
        private const ulong FNV_PRIME = 1099511628211UL;

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestMaskingAgreesWithCarafe()
        {
            CompareMasking();
        }

        [TestMethod]
        public void TestMaskingAgreesWithCarafeSubset()
        {
            using (TestData.UseSubset(TestContext, CarafeFineTuneParityTest.SUBSET_ZIP))
                CompareMasking();
        }

        private void CompareMasking()
        {
            // Both packages in one call: with either one present, a missing other one fails.
            TestData.InconclusiveUnlessAvailable(TestData.TrainingExports, TestData.FineTunedLibraries);
            string carafeFolder = TestData.FineTunedLibraries.Resolve().First();
            TestData.RequireFiles(carafeFolder, CarafeTrainingDirectory.PSM_FILE, CarafeModelDirectory.META_FILE);
            // What the Carafe run trained with, from its meta.json: the run's NCE, its instrument (else
            // Carafe's default) and its rt_max, the last MS2 RT + 0.1.
            var carafeSettings = CarafeReferenceRun.Open(carafeFolder).Settings;
            foreach (string exportPath in TestData.TrainingExports.Resolve())
                CompareMasking(TestData.RequireFile(exportPath), carafeFolder, carafeSettings);
        }

        private void CompareMasking(string exportPath, string carafeFolder, LibrarySettings carafeSettings)
        {
            TestContext.WriteLine(@"{0} against {1}", exportPath, carafeFolder);
            var export = TrainingExport.Read(exportPath);
            TestContext.WriteLine(@"{0} rows, rt_max {1}, NCE {2}, instrument {3}", export.Records.Count, export.RtMax,
                export.DominantCollisionEnergy, export.InstrumentModel);
            var options = new OspreyTrainingSetOptions();
            Assert.AreEqual(carafeSettings.RtMax, export.RtMax + options.RtMaxPadding, 1e-9);
            Assert.AreEqual(carafeSettings.Nce, export.DominantCollisionEnergy);
            Assert.IsTrue(export.Records.All(r => r.NSlots == 4 * (r.Sequence.Length - 1)));

            var trainingSet = OspreyTrainingSet.Build(new[] { export }, options);
            TestContext.WriteLine(trainingSet.Stats.ToString());
            TestContext.WriteLine(@"Masked by: " + string.Join(@", ", trainingSet.Stats.MaskedBy.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => p.Key + @" " + p.Value)));

            // Spectra by modified form and charge; a repeated key keeps its first row.
            var carafeRows = CarafeTrainingDirectory.ReadMs2(carafeFolder, carafeSettings.Nce, carafeSettings.Instrument);
            if (TestData.IsSubset)
            {
                // The subset export has the rows of a sample of sequences; compare Carafe's of the same.
                if (!export.Metadata.TryGetValue(SUBSET_MODULUS_KEY, out string modulusText))
                    Assert.Fail(@"No {0} in {1}", SUBSET_MODULUS_KEY, exportPath);
                int modulus = int.Parse(modulusText, CultureInfo.InvariantCulture);
                carafeRows = carafeRows.Where(e => IsSampled(e.Sequence, modulus)).ToArray();
                TestContext.WriteLine(@"Subset: Carafe's spectra of the sequences sampled modulo {0}", modulus);
            }
            var carafe = FirstByKey(carafeRows, e => Key(e.Precursor.Peptide, e.Precursor.Charge), out int carafeRepeats);
            var records = FirstByKey(export.Records.Where(r => r.RunPrecursorQ <= options.MaxRunQ), RecordKey, out int exportRepeats);
            TestContext.WriteLine(@"Repeated keys: Carafe {0}, export {1}", carafeRepeats, exportRepeats);
            var policy = new OspreyMaskingPolicy(options.Masking);
            int common = 0, keptByBoth = 0;
            long slots = 0, agree = 0;
            foreach (var pair in carafe)
            {
                if (!records.TryGetValue(pair.Key, out var record))
                    continue;
                common++;
                var masked = policy.Apply(record);
                if (masked.RejectReason == null)
                    keptByBoth++;
                for (int slot = 0; slot < masked.SlotCount; slot++)
                {
                    slots++;
                    if ((masked.Invalid[slot] == 0) == (pair.Value.Invalid[slot] <= 0))
                        agree++;
                }
            }
            double agreement = (double)agree / slots;
            TestContext.WriteLine(@"Carafe kept {0} spectra; {1} are in the export and {2} of those are kept here. " +
                                  @"Kept here: {3}. Slot agreement {4:P2} over {5} slots.",
                carafe.Count, common, keptByBoth, trainingSet.Ms2.Count, agreement, slots);
            Assert.IsTrue(common > 0.9 * carafe.Count, @"Too few of Carafe's spectra are in the export");
            Assert.IsTrue(agreement >= MIN_SLOT_AGREEMENT, string.Format(@"Slot agreement {0:P2}", agreement));
            Assert.IsTrue(keptByBoth >= MIN_SHARED_KEPT_FRACTION * common, string.Format(@"Kept by both {0} of {1}", keptByBoth, common));
        }

        /// <summary>A modified form and charge, its modifications in site order whatever order the source lists them in.</summary>
        private static string Key(PeptideForm peptide, int charge)
        {
            var modifications = Enumerable.Range(0, peptide.ModNames.Count)
                .Select(i => peptide.ModSites[i].ToString(CultureInfo.InvariantCulture) + @":" + peptide.ModNames[i])
                .OrderBy(m => m, StringComparer.Ordinal);
            return peptide.Sequence + @"|" + string.Join(@";", modifications) + @"/" + charge.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The key of a record's mapped form, or null when its modifications do not map.</summary>
        private static string RecordKey(TrainingRecord record)
        {
            return OspreyModificationMapper.TryMap(record.Sequence, record.ModifiedSequence, record.ModPositions, record.ModMasses,
                record.ModUnimodIds, out var peptide, out _)
                ? Key(peptide, record.Charge)
                : null;
        }

        /// <summary>True when the 64-bit FNV-1a hash of <paramref name="sequence"/>, shifted right 32 bits, is 0 modulo <paramref name="modulus"/>.</summary>
        private static bool IsSampled(string sequence, int modulus)
        {
            ulong hash = FNV_OFFSET_BASIS;
            foreach (byte b in Encoding.UTF8.GetBytes(sequence))
                hash = (hash ^ b) * FNV_PRIME;
            return (hash >> 32) % (ulong)modulus == 0;
        }

        private static Dictionary<string, T> FirstByKey<T>(IEnumerable<T> items, Func<T, string> getKey, out int repeats)
        {
            var byKey = new Dictionary<string, T>(StringComparer.Ordinal);
            repeats = 0;
            foreach (var item in items)
            {
                string key = getKey(item);
                if (key == null)
                    continue;
                if (!byKey.TryAdd(key, item))
                    repeats++;
            }
            return byKey;
        }
    }
}
