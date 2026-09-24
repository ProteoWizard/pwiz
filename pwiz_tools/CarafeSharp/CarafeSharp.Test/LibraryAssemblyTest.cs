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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.IO;
using pwiz.CarafeSharp.Proteome;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// Checks the pieces of Carafe's library assembly against values printed by the programs
    /// Carafe runs: alphabase 1.2.1 in Carafe's Python for fragment m/z, Java 23 for number
    /// text and string hashes, and Carafe's own compiled PeptideUtils for peptidoforms.
    /// </summary>
    [TestClass]
    public class LibraryAssemblyTest
    {
        [TestMethod]
        public void TestFragmentMz()
        {
            // alphabase create_fragment_mz_dataframe, float32 bits of b_z1, b_z2, y_z1, y_z2.
            AssertFragmentRow(@"PEPTIDEK", @"", @"", 2, 0, 1120149182, 1111892599, 1146083892, 1137711787);
            AssertFragmentRow(@"PEPTIDEK", @"", @"", 2, 6, 1145280212, 1136908107, 1125326049, 1117003454);
            AssertFragmentRow(@"ACDMK", @"Carbamidomethyl@C;Oxidation@M", @"2;4", 3, 0, 1116739258, 1108482676, 1141787948, 1133415843);
            AssertFragmentRow(@"ACDMK", @"Carbamidomethyl@C;Oxidation@M", @"2;4", 3, 3, 1140265366, 1131909764, 1125326049, 1117003454);
            // Site 0 adds to the first residue, and a charge 1 precursor has no charge 2 fragments.
            AssertFragmentRow(@"MCMCK", @"Acetyl@Protein_N-term;Oxidation@M;Carbamidomethyl@C", @"0;1;2", 1, 0, 1128140193, 0, 1141328988, 0);
            AssertFragmentRow(@"MCMCK", @"Acetyl@Protein_N-term;Oxidation@M;Carbamidomethyl@C", @"0;1;2", 1, 3, 1142032524, 0, 1125326049, 0);
        }

        [TestMethod]
        public void TestJavaNumberFormat()
        {
            // Double.toString, then String.format %.2f and %.4f, from Java 23.
            AssertJavaDouble(400.182674629482, @"400.182674629482", @"400.18", @"400.1827");
            AssertJavaDouble(400.0, @"400.0", @"400.00", @"400.0000");
            AssertJavaDouble(0.001, @"0.001", @"0.00", @"0.0010");
            AssertJavaDouble(1e-4, @"1.0E-4", @"0.00", @"0.0001");
            AssertJavaDouble(12340000.0, @"1.234E7", @"12340000.00", @"12340000.0000");
            AssertJavaDouble(1e16, @"1.0E16", @"10000000000000000.00", @"10000000000000000.0000");
            AssertJavaDouble(-35.214714, @"-35.214714", @"-35.21", @"-35.2147");
            AssertJavaDouble(0.1 + 0.2, @"0.30000000000000004", @"0.30", @"0.3000");
            AssertJavaDouble(123456789.123, @"1.23456789123E8", @"123456789.12", @"123456789.1230");
            // %.2f rounds the shortest decimal half up: 9.995 is 9.99499... in binary.
            AssertJavaDouble(9.995, @"9.995", @"10.00", @"9.9950");
            AssertJavaDouble(0.125, @"0.125", @"0.13", @"0.1250");
            AssertJavaDouble(-0.001, @"-0.001", @"-0.00", @"-0.0010");
            AssertJavaDouble(0.0, @"0.0", @"0.00", @"0.0000");
            AssertJavaDouble(-0.0, @"-0.0", @"-0.00", @"-0.0000");
            // Float.toString, then %.4f of the float widened to double.
            AssertJavaFloat(434.23578f, @"434.23578", @"434.2358");
            AssertJavaFloat(200.0f, @"200.0", @"200.0000");
            AssertJavaFloat(0.5421f, @"0.5421", @"0.5421");
            AssertJavaFloat(0.03125f, @"0.03125", @"0.0313");
            AssertJavaFloat(0.99995f, @"0.99995", @"0.9999");
            AssertJavaFloat(1e-4f, @"1.0E-4", @"0.0001");
            AssertJavaFloat(1.5e7f, @"1.5E7", @"15000000.0000");
            AssertJavaFloat((float)(0.123 / 0.456), @"0.26973686", @"0.2697");
            AssertJavaFloat(0.00005f, @"5.0E-5", @"0.0000");
        }

        [TestMethod]
        public void TestJavaHashOrder()
        {
            Assert.AreEqual(0, JavaHashOrder.StringHash(string.Empty));
            Assert.AreEqual(99162322, JavaHashOrder.StringHash(@"hello"));
            Assert.AreEqual(1645517421, JavaHashOrder.StringHash(@"sp|P55011_pep00001|S12A2_HUMAN"));
            Assert.AreEqual(-2017539908, JavaHashOrder.StringHash(@"decoy_sp|Q9NP61_pep00017|ARFG3_HUMAN"));
            // Default maps resize at 13, 25 and 49 keys; HashMap(10000) starts at 16384.
            Assert.AreEqual(16, JavaHashOrder.TableSize(12));
            Assert.AreEqual(32, JavaHashOrder.TableSize(13));
            Assert.AreEqual(128, JavaHashOrder.TableSize(49));
            Assert.AreEqual(16384, JavaHashOrder.TableSize(12288, 10000));
            Assert.AreEqual(32768, JavaHashOrder.TableSize(12289, 10000));
            // Keys iterate by spread hash modulo the table size, not insertion order.
            CollectionAssert.AreEqual(new[] { @"a", @"b" }, JavaHashOrder.OrderStringKeys(new[] { @"b", @"a" }));
        }

        [TestMethod]
        public void TestPeptideIsoforms()
        {
            // Carafe's PeptideUtils.calcPeptideIsoforms with -fixMod 1 -varMod 2,5 -maxVar 2.
            var settings = new ModificationSettings { FixedModifications = @"1", VariableModifications = @"2,5", MaxVariableModifications = 2 };
            var generator = new PeptideIsoformGenerator(settings, new HashSet<string> { @"MKDNLCK" });
            AssertIsoforms(generator, @"MCMCK",
                (@"Oxidation@M;Carbamidomethyl@C;Carbamidomethyl@C", @"1;2;4", 4649757643919798294),
                (@"Oxidation@M;Carbamidomethyl@C;Carbamidomethyl@C", @"3;2;4", 4649757643919798294),
                (@"Oxidation@M;Oxidation@M;Carbamidomethyl@C;Carbamidomethyl@C", @"1;3;2;4", 4649898336676674218),
                (@"Carbamidomethyl@C;Carbamidomethyl@C", @"2;4", 4649616951162922370));
            AssertIsoforms(generator, @"MKDNLCK",
                (@"Oxidation@M;Carbamidomethyl@C", @"1;6", 4651333708199786282),
                (@"Acetyl@Protein_N-term;Carbamidomethyl@C", @"0;6", 4651562544280565873),
                (@"Oxidation@M;Acetyl@Protein_N-term;Carbamidomethyl@C", @"1;0;6", 4651703237037441798),
                (@"Carbamidomethyl@C", @"6", 4651193015442910357));
            AssertIsoforms(generator, @"ACDEFGHIKLMNPQRSTVWY",
                (@"Oxidation@M;Carbamidomethyl@C", @"11;2", 4657643716134533223),
                (@"Carbamidomethyl@C", @"2", 4657608542945314242));

            // Library order is by mass, then sequence.
            var forms = LibraryPeptideForms.Enumerate(new[] { @"MCMCK", @"MKDNLCK" }, generator);
            CollectionAssert.AreEqual(forms.Select(f => f.Mass).OrderBy(m => m).ToArray(), forms.Select(f => f.Mass).ToArray());
            Assert.AreEqual(8, forms.Count);
            CollectionAssert.AreEqual(new[] { 2 }, LibraryPeptideForms.GetCharges(forms[0], new[] { 1, 2, 3 }, 300, 400));

            // A modification Carafe's library prediction has no alphabase name for is not supported.
            var tmt = new PeptideIsoformGenerator(new ModificationSettings { FixedModifications = @"11", VariableModifications = @"0" },
                new HashSet<string>());
            Assert.ThrowsException<NotSupportedException>(() => tmt.Enumerate(@"PEPTIDEK").First().ToAlphabase());
        }

        [TestMethod]
        public void TestModifiedPeptideNotation()
        {
            var settings = new ModificationSettings { FixedModifications = @"1", VariableModifications = @"2,3,5,10", MaxVariableModifications = 4 };
            var generator = new PeptideIsoformGenerator(settings, new HashSet<string> { @"MCNKR" });
            var isoform = generator.Enumerate(@"MCNKR").Single(f => f.Modifications.Count == 5);
            Assert.AreEqual(@"_[UniMod:1]M[UniMod:35]C[UniMod:4]N(UniMod:7)K(UniMod:121)R_",
                ModifiedPeptideNotation.Format(isoform, ModifiedPeptideStyle.dia_nn));
            // Skyline sums the top_modifications.tsv masses on a residue, exactly, and moves the
            // protein N-term acetyl onto residue 1.
            Assert.AreEqual(@"M[+58.00547930326]C[+57.02146372057]N[+0.9840155826899988]K[+114.04292744114]R",
                ModifiedPeptideNotation.FormatSkyline(isoform, out var skylineMods));
            CollectionAssert.AreEqual(new[] { 1, 3, 1, 4, 2 }, skylineMods.Select(m => m.Position).ToArray());
            Assert.AreEqual(42.0105646837, skylineMods[2].Mass);
            // Carafe cannot write protein N-term acetyl in the EncyclopeDIA or generic notations.
            Assert.ThrowsException<NotSupportedException>(() => ModifiedPeptideNotation.Format(isoform, ModifiedPeptideStyle.encyclopedia));

            var residueOnly = generator.Enumerate(@"MCNKR").Single(f => f.Modifications.Count == 4 && f.Modifications.All(m => m.Position > 0));
            Assert.AreEqual(@"_M[Oxidation (M)]C[Carbamidomethyl (C)]N[Deamidated (N)]K[GG (K)]R_",
                ModifiedPeptideNotation.Format(residueOnly, ModifiedPeptideStyle.encyclopedia));
            Assert.AreEqual(@"_M[Oxidation]C[Carbamidomethyl]DeamidatedGGR_",
                ModifiedPeptideNotation.Format(residueOnly, ModifiedPeptideStyle.generic));
            var unmodified = generator.Enumerate(@"PEPTIDER").Single();
            Assert.AreEqual(@"_PEPTIDER_", ModifiedPeptideNotation.Format(unmodified, ModifiedPeptideStyle.dia_nn));
            Assert.AreEqual(@"PEPTIDER", ModifiedPeptideNotation.FormatSkyline(unmodified, out _));

            Assert.AreEqual(ModifiedPeptideStyle.dia_nn, ModifiedPeptideNotation.GetStyle(@"diann"));
            Assert.AreEqual(ModifiedPeptideStyle.encyclopedia, ModifiedPeptideNotation.GetStyle(@"EncyclopeDIA"));
            Assert.AreEqual(ModifiedPeptideStyle.generic, ModifiedPeptideNotation.GetStyle(@" DIA-NN"));
        }

        [TestMethod]
        public void TestFragmentSelection()
        {
            // Two rows (a 3-residue peptide): b1/y2 then b2/y1.
            var mz = new[] { 250.0, 125.6, 300.0, 150.6, 350.0, 175.6, 110.0, 100.5 };
            var intensities = new[] { 0.5f, 0.5f, 1.0f, 0.0f, 0.25f, 0.5f, 0.9f, 0.3f };
            // m/z 120-400, ion number 1 allowed, top 3: y1 is out of range and y2^2 has no
            // intensity; the 0.5 tie (b1, b1^2, b2^2) is broken by HashMap order, which for five
            // candidates is position order.
            var fragments = new CarafeFragmentSelector(120, 400, 3, 1).Select(intensities, 4, mz);
            CollectionAssert.AreEqual(new[] { @"y2^1", @"b1^1", @"b1^2" }, fragments.Select(f => f.ToString()).ToArray());
            Assert.AreEqual(1.0f, fragments[0].RelativeIntensity);
            Assert.AreEqual(0.5f, fragments[1].RelativeIntensity);
            Assert.AreEqual((float)300.0, fragments[0].Mz);
            Assert.AreEqual(300.0, fragments[0].TheoreticalMz);

            // Ion number 2 and up only: b2 and y2 remain; renormalized to the largest candidate.
            fragments = new CarafeFragmentSelector(120, 400, 20, 2).Select(intensities, 4, mz);
            CollectionAssert.AreEqual(new[] { @"y2^1", @"b2^2", @"b2^1" }, fragments.Select(f => f.ToString()).ToArray());

            // Renormalization divides by the largest candidate, not the largest prediction.
            fragments = new CarafeFragmentSelector(120, 280, 20, 1).Select(intensities, 4, mz);
            CollectionAssert.AreEqual(new[] { @"b1^1", @"b1^2", @"b2^2" }, fragments.Select(f => f.ToString()).ToArray());
            Assert.AreEqual(1.0f, fragments[0].RelativeIntensity);
            Assert.AreEqual(1.0f, fragments[2].RelativeIntensity);

            // With 13 candidates Carafe's HashMap has 32 buckets, so position 33 (b9) iterates
            // first and wins an intensity tie against positions 2 to 13.
            var tieMz = Enumerable.Repeat(500.0, 36).ToArray();
            var tieIntensities = new float[36];
            foreach (int position in Enumerable.Range(2, 12).Append(33))
                tieIntensities[position - 1] = 0.5f;
            fragments = new CarafeFragmentSelector(120, 2000, 1, 1).Select(tieIntensities, 4, tieMz);
            CollectionAssert.AreEqual(new[] { @"b9^1" }, fragments.Select(f => f.ToString()).ToArray());
        }

        private static void AssertFragmentRow(string sequence, string mods, string sites, int charge, int row, params int[] expectedBits)
        {
            var precursor = new PrecursorForm(PeptideForm.FromAlphabase(sequence, mods, sites), charge);
            var mz = AlphabaseFragmentMz.ToFloat32(AlphabaseFragmentMz.Calculate(precursor));
            for (int k = 0; k < AlphabaseFragmentMz.COLUMN_COUNT; k++)
            {
                Assert.AreEqual(expectedBits[k], BitConverter.SingleToInt32Bits(mz[row * AlphabaseFragmentMz.COLUMN_COUNT + k]),
                    string.Format(@"{0}/{1} row {2} column {3}", sequence, charge, row, k));
            }
        }

        private static void AssertJavaDouble(double value, string toString, string fixed2, string fixed4)
        {
            Assert.AreEqual(toString, JavaNumberFormat.ToString(value));
            Assert.AreEqual(fixed2, JavaNumberFormat.FormatFixed(value, 2));
            Assert.AreEqual(fixed4, JavaNumberFormat.FormatFixed(value, 4));
        }

        private static void AssertJavaFloat(float value, string toString, string fixed4)
        {
            Assert.AreEqual(toString, JavaNumberFormat.ToString(value));
            Assert.AreEqual(fixed4, JavaNumberFormat.FormatFixed(value, 4));
        }

        private static void AssertIsoforms(PeptideIsoformGenerator generator, string sequence,
            params (string Mods, string Sites, long MassBits)[] expected)
        {
            var forms = generator.Enumerate(sequence).ToList();
            Assert.AreEqual(expected.Length, forms.Count, sequence);
            for (int i = 0; i < forms.Count; i++)
            {
                var alphabase = forms[i].ToAlphabase();
                Assert.AreEqual(expected[i].Mods, alphabase.ModsText, sequence);
                Assert.AreEqual(expected[i].Sites, alphabase.ModSitesText, sequence);
                Assert.AreEqual(expected[i].MassBits, BitConverter.DoubleToInt64Bits(forms[i].Mass), sequence);
            }
        }
    }
}
