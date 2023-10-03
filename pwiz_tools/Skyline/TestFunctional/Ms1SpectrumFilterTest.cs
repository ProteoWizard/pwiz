/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests applying <see cref="SpectrumClassFilter"/>'s that restring the MS1 spectra
    /// in extracted ion chromatograms.
    /// </summary>
    [TestClass]
    public class Ms1SpectrumFilterTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestMs1SpectrumFilter()
        {
            TestFilesZip = @"TestFunctional\Ms1SpectrumFilterTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Ms1SpectrumFilterTest.sky"));
                SkylineWindow.SelectedPath =
                    SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.TransitionGroups, 0);
            });
            Assert.AreEqual(1, SkylineWindow.Document.MoleculeTransitionGroupCount);
            Assert.IsTrue(SkylineWindow.Document.MoleculeTransitions.All(transition=>transition.IsMs1));

            // Create a copy of the first transition group which only uses spectra with compensation voltage -50
            RunDlg<EditSpectrumFilterDlg>(SkylineWindow.EditMenu.EditSpectrumFilter, dlg =>
            {
                dlg.CreateCopy = true;
                var row = dlg.RowBindingList.AddNew();
                Assert.IsNotNull(row);
                row.Property = SpectrumClassColumn.CompensationVoltage.GetLocalizedColumnName(CultureInfo.CurrentCulture);
                row.SetOperation(FilterOperations.OP_EQUALS);
                row.SetValue(-50);
                dlg.OkDialog();
            });
            Assert.AreEqual(2, SkylineWindow.Document.MoleculeTransitionGroupCount);
            RunUI(() =>
            {
                SkylineWindow.SelectedPath =
                    SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.TransitionGroups, 0);
            });

            // Create a copy of the first transition group which only uses spectra with compensation voltage -70
            RunDlg<EditSpectrumFilterDlg>(SkylineWindow.EditMenu.EditSpectrumFilter, dlg =>
            {
                dlg.CreateCopy = true;
                var row = dlg.RowBindingList.AddNew();
                Assert.IsNotNull(row);
                row.Property = SpectrumClassColumn.CompensationVoltage.GetLocalizedColumnName(CultureInfo.CurrentCulture);
                row.SetOperation(FilterOperations.OP_EQUALS);
                row.SetValue(-70);
                dlg.OkDialog();
            });
            Assert.AreEqual(3, SkylineWindow.Document.MoleculeTransitionGroupCount);

            ImportResultsFile(TestFilesDir.GetTestPath("Ms1SpectrumFilterTest.mzML"));

            var document = SkylineWindow.Document;
            Assert.AreEqual(1, document.MoleculeCount);
            var peptideDocNode = document.Molecules.First();
            Assert.AreEqual(3, peptideDocNode.TransitionGroupCount);

            // The unfiltered transition group is the first in the document because of SpectrumClassFilter sort order
            var unfilteredTransitionGroup = peptideDocNode.TransitionGroups.First();
            var unfilteredChromatogramGroup =
                LoadChromatogram(document, peptideDocNode, unfilteredTransitionGroup);
            Assert.IsTrue(unfilteredTransitionGroup.SpectrumClassFilter.IsEmpty);
            int unfilteredChromatogramPointCount = unfilteredChromatogramGroup.GetRawTransitionInfo(0).RawTimes.Count;

            int totalFilteredChromatogramPointCount = 0;
            foreach (var filteredTransitionGroup in peptideDocNode.TransitionGroups.Skip(1))
            {
                Assert.IsFalse(filteredTransitionGroup.SpectrumClassFilter.IsEmpty);
                var filteredChromatogramGroup = LoadChromatogram(document, peptideDocNode, filteredTransitionGroup);
                int chromatogramPointCount = filteredChromatogramGroup.GetRawTransitionInfo(0).RawTimes.Count;
                Assert.AreNotEqual(0, chromatogramPointCount);
                totalFilteredChromatogramPointCount += chromatogramPointCount;
            }

            Assert.AreEqual(unfilteredChromatogramPointCount, totalFilteredChromatogramPointCount);
        }

        private ChromatogramGroupInfo LoadChromatogram(SrmDocument document, PeptideDocNode peptideDocNode,
            TransitionGroupDocNode transitionGroupDocNode)
        {
            Assert.IsTrue(document.Settings.MeasuredResults.TryLoadChromatogram(0, peptideDocNode, transitionGroupDocNode,
                (float)document.Settings.TransitionSettings.Instrument.MzMatchTolerance, out var infoSet));
            Assert.AreEqual(1, infoSet.Length);
            return infoSet[0];
        }
    }
}
