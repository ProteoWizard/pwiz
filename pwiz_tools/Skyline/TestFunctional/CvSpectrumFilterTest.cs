/*
 * Original author: Brian Pratt <bspratt .at. uw.edu>,
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Filtering;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// End-to-end test that a <see cref="SpectrumClassFilter"/> on an uninterpreted mzML CV/user
    /// parameter (the "Phase 2" feature) actually restricts the spectra used for chromatogram
    /// extraction. Reuses the Ms1SpectrumFilterTest data, whose spectra carry base peak intensity
    /// (MS:1000505) and the Thermo filter string (MS:1000512) - terms Skyline does not interpret into
    /// its own fields, so they exercise the dynamic-column path rather than a typed property.
    /// </summary>
    [TestClass]
    public class CvSpectrumFilterTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestCvSpectrumFilter()
        {
            TestFilesZip = @"TestFunctional\Ms1SpectrumFilterTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Ms1SpectrumFilterTest.sky")));
            Assert.AreEqual(1, SkylineWindow.Document.MoleculeTransitionGroupCount);
            Assert.IsTrue(SkylineWindow.Document.MoleculeTransitions.All(t => t.IsMs1));

            var precursorPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.TransitionGroups, 0);

            // The Thermo filter string (MS:1000512) carries the FAIMS compensation voltage as text
            // ("cv=-50.00" / "cv=-70.00"), so two "contains" filters on it partition the MS1 spectra the
            // same way the interpreted CompensationVoltage does. A numeric filter on base peak intensity
            // (MS:1000505, reported in scientific notation, e.g. "5.49898375e05") that admits every
            // positive value matches them all - exercising the numeric path and the invariant value parse.
            var filterCv50 = StringCvFilter(@"cv=-50");
            var filterCv70 = StringCvFilter(@"cv=-70");
            var filterBpiAll = NumericBpiFilter(FilterOperations.OP_IS_GREATER_THAN, @"0");

            RunUI(() =>
            {
                SkylineWindow.EditMenu.ChangeSpectrumFilter(new[] { precursorPath }, filterCv50, true);
                SkylineWindow.EditMenu.ChangeSpectrumFilter(new[] { precursorPath }, filterCv70, true);
                SkylineWindow.EditMenu.ChangeSpectrumFilter(new[] { precursorPath }, filterBpiAll, true);
            });
            Assert.AreEqual(4, SkylineWindow.Document.MoleculeTransitionGroupCount);

            ImportResultsFile(TestFilesDir.GetTestPath("Ms1SpectrumFilterTest.mzML"));

            var document = SkylineWindow.Document;
            var peptideDocNode = document.Molecules.First();
            Assert.AreEqual(4, peptideDocNode.TransitionGroupCount);

            int Points(SpectrumClassFilter filter)
            {
                var transitionGroup = peptideDocNode.TransitionGroups.First(tg => Equals(tg.SpectrumClassFilter, filter));
                Assert.IsTrue(document.Settings.MeasuredResults.TryLoadChromatogram(0, peptideDocNode, transitionGroup,
                    (float)document.Settings.TransitionSettings.Instrument.MzMatchTolerance, out var infoSet));
                Assert.AreEqual(1, infoSet.Length);
                return infoSet[0].GetRawTransitionInfo(0).RawTimes.Count;
            }

            int unfilteredPoints = Points(default);
            int cv50Points = Points(filterCv50);
            int cv70Points = Points(filterCv70);
            Assert.AreNotEqual(0, cv50Points);
            Assert.AreNotEqual(0, cv70Points);
            // The two string CV filters partition the MS1 spectra: together they match exactly the
            // unfiltered set. This only holds if the CV terms were captured during extraction (they are
            // otherwise dropped) and the string predicate evaluated them.
            Assert.AreEqual(unfilteredPoints, cv50Points + cv70Points);
            // A numeric CV filter admitting every base peak intensity matches all spectra.
            Assert.AreEqual(unfilteredPoints, Points(filterBpiAll));
        }

        private static SpectrumClassFilter StringCvFilter(string containsText)
        {
            var column = SpectrumClassColumn.CvParam(@"MS:1000512", @"filter string", null, false);
            return new SpectrumClassFilter(new FilterClause(new[]
                { new FilterSpec(column.PropertyPath, FilterOperations.OP_CONTAINS, containsText) }));
        }

        private static SpectrumClassFilter NumericBpiFilter(IFilterOperation op, string operand)
        {
            var column = SpectrumClassColumn.CvParam(@"MS:1000505", @"base peak intensity",
                @"number of detector counts", true);
            return new SpectrumClassFilter(new FilterClause(new[]
                { new FilterSpec(column.PropertyPath, op, operand) }));
        }
    }
}
