/*
 * Original author: MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Localization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Unit tests for <see cref="SiteDeterminingIonAnalyzer"/>, the analytic (no isomer
    /// enumeration) identifier of site-determining product ions for modification localization.
    /// </summary>
    [TestClass]
    public class SiteDeterminingIonTest : AbstractUnitTest
    {
        private static readonly StaticMod PHOSPHO = new StaticMod("Phospho", "S,T,Y", null, true, "PO3H",
            LabelAtoms.None, RelativeRT.Matching, null, null, null);
        private static readonly StaticMod OXIDATION = new StaticMod("Oxidation", "M", null, true, "O",
            LabelAtoms.None, RelativeRT.Matching, null, null, null);

        [TestMethod]
        public void TestSiteDeterminingIonAnalyzer()
        {
            TestPhosphoNearTerminusUnique();
            TestInteriorSerineNonUnique();
            TestInteriorSerineRunNoUnique();
            TestTwoSeparatedCandidatesUnique();
            TestTwoPhosphoAmongFour();
            TestTwoModTypes();
            TestNonLocalizable();
        }

        /// <summary>
        /// Mirrors the screenshot peptide: a phospho on an interior serine inside a contiguous run
        /// of candidate serines. Because a prefix ion reaching the modified serine necessarily also
        /// covers its neighbor in the run (and likewise for suffix ions), NO fragment ion is unique
        /// to the placement, even though the peptide is localizable. The site-determining filter
        /// therefore leaves an empty list (the empty-state hint scenario).
        /// </summary>
        private void TestInteriorSerineRunNoUnique()
        {
            // A0 A1 S2 S3 S4 A5 K6 - run of three candidate serines (2,3,4); phospho on the middle one.
            const string seq = "AASSSAK";
            var settings = SrmSettingsList.GetDefault();
            var peptide = new Peptide(seq);
            var nodePep = new PeptideDocNode(peptide, MakeMods(peptide, (PHOSPHO, 3)));
            var analyzer = new SiteDeterminingIonAnalyzer(settings, nodePep);
            var group = new TransitionGroup(peptide, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light);

            AssertEx.IsTrue(analyzer.CanLocalize);
            AssertEx.AreEqual(3L, analyzer.IsomerCount); // C(3,1)

            // For EVERY candidate fragment ion (b and y), the placement is not uniquely localized.
            int len = seq.Length;
            for (int offset = 0; offset <= len - 2; offset++)
            {
                foreach (var ion in new[] { B(group, offset), Y(group, offset) })
                    AssertEx.IsTrue(!analyzer.IsUniqueToPrecursor(ion));
            }
        }

        /// <summary>
        /// Two well-separated candidate residues - an N-terminal serine and a much later threonine -
        /// with the phospho on the N-terminal serine. A short prefix (b) ion that contains only the
        /// modified serine uniquely localizes the placement, while any fragment whose span covers
        /// BOTH candidates cannot.
        /// </summary>
        private void TestTwoSeparatedCandidatesUnique()
        {
            // S0 A1 A2 A3 A4 A5 T6 A7 A8 K9 - candidates: serine 0 and threonine 6; phospho on S0.
            const string seq = "SAAAAATAAK";
            var settings = SrmSettingsList.GetDefault();
            var peptide = new Peptide(seq);
            var nodePep = new PeptideDocNode(peptide, MakeMods(peptide, (PHOSPHO, 0)));
            var analyzer = new SiteDeterminingIonAnalyzer(settings, nodePep);
            var group = new TransitionGroup(peptide, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light);

            AssertEx.IsTrue(analyzer.CanLocalize);
            AssertEx.AreEqual(2L, analyzer.IsomerCount); // C(2,1)

            // b1 (offset 0) spans only the modified serine, not the later threonine -> unique.
            var b1 = B(group, 0);
            AssertEx.IsTrue(analyzer.IsUniqueToPrecursor(b1));
            AssertEx.AreEqual(1L, analyzer.GetProducingSetSize(b1));

            // b7 (offset 6) spans BOTH candidate residues -> can no longer localize, not unique.
            var b7 = B(group, 6);
            AssertEx.IsTrue(!analyzer.IsUniqueToPrecursor(b7));
            AssertEx.IsTrue(analyzer.GetProducingSetSize(b7) > 1);

            // At least one unique b-ion exists among the prefixes that stop before the threonine.
            bool anyUnique = false;
            for (int offset = 0; offset <= 5; offset++)
                anyUnique |= analyzer.IsUniqueToPrecursor(B(group, offset));
            AssertEx.IsTrue(anyUnique);
        }

        /// <summary>
        /// Case 1: a single phospho on the N-terminal serine of a peptide with two candidate
        /// serines produces a run of unique, site-determining N-terminal (b) ions. The b-ion whose
        /// span reaches the second candidate serine can no longer distinguish placements, and the
        /// analytic span logic is cross-checked against actual fragment m/z values.
        /// </summary>
        private void TestPhosphoNearTerminusUnique()
        {
            // S0 A1 A2 A3 S4 A5 A6 K7 - candidate serines at indices 0 and 4.
            const string seq = "SAAASAAK";
            var settings = SrmSettingsList.GetDefault();
            var peptide = new Peptide(seq);
            var modsAt0 = MakeMods(peptide, (PHOSPHO, 0));
            var nodePep = new PeptideDocNode(peptide, modsAt0);
            var analyzer = new SiteDeterminingIonAnalyzer(settings, nodePep);
            var group = new TransitionGroup(peptide, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light);

            AssertEx.IsTrue(analyzer.CanLocalize);
            AssertEx.AreEqual(2L, analyzer.IsomerCount); // C(2,1)
            Assert.IsNotNull(analyzer.LocalizationGroupKey);

            // b1..b4 (offsets 0..3) span only the first candidate serine - unique site-determining.
            for (int offset = 0; offset <= 3; offset++)
            {
                var b = B(group, offset);
                AssertEx.IsTrue(analyzer.IsSiteDetermining(b));
                AssertEx.AreEqual(1L, analyzer.GetProducingSetSize(b));
                AssertEx.IsTrue(analyzer.IsUniqueToPrecursor(b));
                Assert.AreSame(PHOSPHO, analyzer.GetResolvedModification(b));
            }
            // b5, b6 (offsets 4, 5) span both candidate serines - can no longer localize.
            for (int offset = 4; offset <= 5; offset++)
            {
                var b = B(group, offset);
                AssertEx.IsTrue(!analyzer.IsSiteDetermining(b));
                AssertEx.IsTrue(analyzer.GetProducingSetSize(b) > 1);
                AssertEx.IsTrue(!analyzer.IsUniqueToPrecursor(b));
            }
            // Precursor is never site-determining.
            var precursor = new Transition(group, 0, Adduct.SINGLY_PROTONATED);
            AssertEx.IsTrue(!analyzer.IsSiteDetermining(precursor));
            AssertEx.AreEqual(0L, analyzer.GetProducingSetSize(precursor));

            // Cross-check the span logic against real fragment m/z: compare phospho@0 vs phospho@4.
            var modsAt4 = MakeMods(peptide, (PHOSPHO, 4));
            var massesA = settings.GetFragmentCalc(IsotopeLabelType.light, modsAt0).GetFragmentIonMasses(peptide.Target);
            var massesB = settings.GetFragmentCalc(IsotopeLabelType.light, modsAt4).GetFragmentIonMasses(peptide.Target);
            for (int offset = 0; offset <= 3; offset++)
            {
                // Site-determining b-ions differ by roughly the phospho mass (~80 Da).
                double diff = massesA[IonType.b, offset].Value - massesB[IonType.b, offset].Value;
                AssertEx.IsTrue(diff > 50);
            }
            for (int offset = 4; offset <= 6; offset++)
            {
                // Non-site-determining b-ions carry exactly one phospho for either placement.
                AssertEx.AreEqual(massesA[IonType.b, offset].Value, massesB[IonType.b, offset].Value, 1e-6);
            }
        }

        /// <summary>
        /// Case 2: a phospho on an interior serine flanked by other candidate serines on both
        /// sides. Some ions are site-determining, but none is unique to the placement (every
        /// producing-set size exceeds 1).
        /// </summary>
        private void TestInteriorSerineNonUnique()
        {
            // S0 A1 S2 A3 S4 A5 K6 - candidate serines at 0, 2, 4; phospho on the middle one.
            const string seq = "SASASAK";
            var settings = SrmSettingsList.GetDefault();
            var peptide = new Peptide(seq);
            var nodePep = new PeptideDocNode(peptide, MakeMods(peptide, (PHOSPHO, 2)));
            var analyzer = new SiteDeterminingIonAnalyzer(settings, nodePep);
            var group = new TransitionGroup(peptide, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light);

            AssertEx.IsTrue(analyzer.CanLocalize);
            AssertEx.AreEqual(3L, analyzer.IsomerCount); // C(3,1)

            // b3 (offset 2) spans two of the three candidate serines - site-determining, not unique.
            var b3 = B(group, 2);
            AssertEx.IsTrue(analyzer.IsSiteDetermining(b3));
            AssertEx.AreEqual(2L, analyzer.GetProducingSetSize(b3));
            AssertEx.IsTrue(!analyzer.IsUniqueToPrecursor(b3));

            // No fragment ion (b or y) is unique to this placement, yet at least one is site-determining.
            bool anySiteDetermining = false;
            int len = seq.Length;
            for (int offset = 0; offset <= len - 2; offset++)
            {
                foreach (var ion in new[] { B(group, offset), Y(group, offset) })
                {
                    AssertEx.IsTrue(!analyzer.IsUniqueToPrecursor(ion));
                    anySiteDetermining |= analyzer.IsSiteDetermining(ion);
                }
            }
            AssertEx.IsTrue(anySiteDetermining);
        }

        /// <summary>
        /// Case 3: two phospho among four candidate serines. IsomerCount is C(4,2) = 6, and a b-ion
        /// whose span captures exactly the two modified serines is unique to the placement.
        /// </summary>
        private void TestTwoPhosphoAmongFour()
        {
            // S0 A1 S2 A3 S4 A5 S6 A7 K8 - four candidate serines; phospho on the first two.
            const string seq = "SASASASAK";
            var settings = SrmSettingsList.GetDefault();
            var peptide = new Peptide(seq);
            var nodePep = new PeptideDocNode(peptide, MakeMods(peptide, (PHOSPHO, 0), (PHOSPHO, 2)));
            var analyzer = new SiteDeterminingIonAnalyzer(settings, nodePep);
            var group = new TransitionGroup(peptide, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light);

            AssertEx.IsTrue(analyzer.CanLocalize);
            AssertEx.AreEqual(6L, analyzer.IsomerCount); // C(4,2)

            var b3 = B(group, 2); // span [0..2] captures exactly serines 0 and 2
            AssertEx.IsTrue(analyzer.IsSiteDetermining(b3));
            AssertEx.AreEqual(1L, analyzer.GetProducingSetSize(b3));
            AssertEx.IsTrue(analyzer.IsUniqueToPrecursor(b3));
        }

        /// <summary>
        /// Case 4: two simultaneously-ambiguous modification types (phospho over two serines and
        /// oxidation over two methionines). IsomerCount is the product of the per-type isomer
        /// counts, and an ion whose span crosses only the serine region resolves phospho (not
        /// oxidation) while an ion crossing only the methionine region resolves oxidation.
        /// </summary>
        private void TestTwoModTypes()
        {
            // S0 S1 A2 A3 M4 M5 K6 - two candidate serines, two candidate methionines.
            const string seq = "SSAAMMK";
            var settings = SrmSettingsList.GetDefault();
            var peptide = new Peptide(seq);
            var nodePep = new PeptideDocNode(peptide, MakeMods(peptide, (PHOSPHO, 0), (OXIDATION, 4)));
            var analyzer = new SiteDeterminingIonAnalyzer(settings, nodePep);
            var group = new TransitionGroup(peptide, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light);

            AssertEx.IsTrue(analyzer.CanLocalize);
            AssertEx.AreEqual(4L, analyzer.IsomerCount); // C(2,1) * C(2,1)

            // b1 span [0..0] separates the two serines but not the methionines -> resolves phospho.
            var b1 = B(group, 0);
            AssertEx.IsTrue(analyzer.IsSiteDetermining(b1));
            Assert.AreSame(PHOSPHO, analyzer.GetResolvedModification(b1));

            // b5 span [0..4] separates the two methionines but not the serines -> resolves oxidation.
            var b5 = B(group, 4);
            AssertEx.IsTrue(analyzer.IsSiteDetermining(b5));
            Assert.AreSame(OXIDATION, analyzer.GetResolvedModification(b5));
        }

        /// <summary>
        /// Case 5: an unmodified peptide and a peptide whose only modification is fully determined
        /// (a single candidate site) are both non-localizable, with a null group key.
        /// </summary>
        private void TestNonLocalizable()
        {
            var settings = SrmSettingsList.GetDefault();

            // Unmodified peptide.
            var pep1 = new Peptide("SASASAK");
            var analyzer1 = new SiteDeterminingIonAnalyzer(settings, new PeptideDocNode(pep1, (ExplicitMods) null));
            AssertEx.IsTrue(!analyzer1.CanLocalize);
            AssertEx.IsNull(analyzer1.LocalizationGroupKey);
            AssertEx.AreEqual(1L, analyzer1.IsomerCount);

            // Only one candidate serine, so the phospho is fully determined - not ambiguous.
            var pep2 = new Peptide("SAAAAK");
            var analyzer2 = new SiteDeterminingIonAnalyzer(settings, new PeptideDocNode(pep2, MakeMods(pep2, (PHOSPHO, 0))));
            AssertEx.IsTrue(!analyzer2.CanLocalize);
            AssertEx.IsNull(analyzer2.LocalizationGroupKey);
        }

        private static ExplicitMods MakeMods(Peptide peptide, params (StaticMod mod, int indexAA)[] placements)
        {
            var explicitMods = placements
                .OrderBy(p => p.indexAA)
                .Select(p => new ExplicitMod(p.indexAA, p.mod))
                .ToArray();
            return new ExplicitMods(peptide, explicitMods, new TypedExplicitModifications[0]);
        }

        private static Transition B(TransitionGroup group, int offset)
        {
            return new Transition(group, IonType.b, offset, 0, Adduct.SINGLY_PROTONATED);
        }

        private static Transition Y(TransitionGroup group, int offset)
        {
            return new Transition(group, IonType.y, offset, 0, Adduct.SINGLY_PROTONATED);
        }
    }
}
