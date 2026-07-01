/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Nomenclature;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Unit tests for the small-molecule nomenclature derivation framework
    /// (<see cref="NomenclatureRegistry"/>) and its interpreters: <see cref="InChiFormulaInterpreter"/>
    /// (InChI formula layer) and <see cref="GlycanCompositionInterpreter"/> (glycan composition).
    /// </summary>
    [TestClass]
    public class NomenclatureTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestNomenclatureDerivation()
        {
            TestInChiDerivation();
            TestGlycanDerivation();
            TestRegistryDispatch();
        }

        private static void TestInChiDerivation()
        {
            // Single-component InChI (D-glucose) -> C6H12O6, via Auto detection, and the originating
            // InChI is retained as an accession number.
            const string glucose =
                @"InChI=1S/C6H12O6/c7-1-3(9)5(11)6(12)4(10)2-8/h3-12H,1-2H2/t3-,4+,5-,6?/m1/s1";
            Assert.AreEqual(glucose, AssertDerives(glucose, @"C6H12O6", null).GetInChI());

            // Multi-component formula layer with an integer multiplier plus water:
            // 2 x C6H12O6 (= C12H24O12) + H2O = C12H26O13. Exercises the "2C6H12O6.H2O" grammar.
            AssertDerives(@"InChI=1S/2C6H12O6.H2O/h1H2", @"C12H26O13", null);

            // Mobile-proton layer: sulfuric acid H2O4S with /p-2 removes two protons -> O4S (sulfate).
            AssertDerives(@"InChI=1S/H2O4S/c1-5(2,3)4/p-2", @"O4S", null);

            // Explicit mode selection works without relying on auto-detection.
            AssertDerives(@"InChI=1S/CH4/h1H4", @"CH4", InChiFormulaInterpreter.MODE_KEY);

            // Shape test.
            var interpreter = new InChiFormulaInterpreter();
            Assert.IsTrue(interpreter.CanHandle(@"InChI=1S/CH4/h1H4"));
            Assert.IsFalse(interpreter.CanHandle(@"CH4"));
            Assert.IsFalse(interpreter.CanHandle(null));
        }

        private static void TestGlycanDerivation()
        {
            // HexNAc2Hex3 = 2 x C8H13NO5 + 3 x C6H10O5 + H2O = C34H58N2O26 (validated reference value).
            AssertDerives(@"HexNAc2Hex3", @"C34H58N2O26", null);

            // The parenthesized building-block(count) grammar and reordering yield the same formula.
            AssertDerives(@"Hex(3)HexNAc(2)", @"C34H58N2O26", null);

            // A single monosaccharide: fucose residue C6H10O4 + H2O = C6H12O5.
            AssertDerives(@"Fuc", @"C6H12O5", null);

            // Explicit mode selection; a sialylated, core-fucosylated biantennary N-glycan composition.
            AssertDerives(@"Hex5HexNAc4NeuAc2Fuc1", @"C90H148N6O66", GlycanCompositionInterpreter.MODE_KEY);

            // Shape test: a glycan composition is claimed, an ordinary formula is not.
            var interpreter = new GlycanCompositionInterpreter();
            Assert.IsTrue(interpreter.CanHandle(@"HexNAc2Hex3"));
            Assert.IsFalse(interpreter.CanHandle(@"C6H12O6"));
            Assert.IsFalse(interpreter.CanHandle(@"NotAGlycan"));
            Assert.IsFalse(interpreter.CanHandle(null));
        }

        private static void TestRegistryDispatch()
        {
            // Auto mode: an ordinary formula is claimed by no interpreter -> false, with no error.
            Assert.IsFalse(NomenclatureRegistry.TryDeriveFormula(@"C6H12O6", null,
                out _, out _, out var noMatchError));
            Assert.IsNull(noMatchError);

            // Explicit InChI mode on a malformed InChI (empty formula layer): claimed but not
            // derivable -> false, with the offending text surfaced for the caller to localize.
            const string malformed = @"InChI=1S//c1";
            Assert.IsFalse(NomenclatureRegistry.TryDeriveFormula(malformed, InChiFormulaInterpreter.MODE_KEY,
                out _, out _, out var malformedError));
            AssertEx.Contains(malformedError, malformed);
        }

        private static MoleculeAccessionNumbers AssertDerives(string text, string expectedFormula, string modeKey)
        {
            Assert.IsTrue(NomenclatureRegistry.TryDeriveFormula(text, modeKey,
                out var formula, out var idsFound, out var error), text);
            Assert.IsNull(error, text);
            Assert.AreEqual(ParsedMolecule.Create(expectedFormula).Molecule, formula.Molecule, text);
            return idsFound;
        }
    }
}
