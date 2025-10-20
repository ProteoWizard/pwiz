/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.SkylineTestUtil;
using System.Globalization;
using System.Linq;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class DissociationMethodTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestDissociationMethod()
        {
            TestFilesZip = @"TestFunctional\DissociationMethodTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("DissociationMethodTest.sky"));
            });
            RunUI(() =>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 1);
            });
            RunDlg<EditSpectrumFilterDlg>(SkylineWindow.EditMenu.EditSpectrumFilter, dlg =>
            {
                var row = dlg.RowBindingList.AddNew();
                Assert.IsNotNull(row);
                row.Property = SpectrumClassColumn.DissociationMethod.GetLocalizedColumnName(CultureInfo.CurrentCulture);
                row.SetOperation(FilterOperations.OP_EQUALS);
                row.SetValue("CID");
                dlg.OkDialog();
            });
            RunUI(() =>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 2);
            });
            RunDlg<EditSpectrumFilterDlg>(SkylineWindow.EditMenu.EditSpectrumFilter, dlg =>
            {
                var row = dlg.RowBindingList.AddNew();
                Assert.IsNotNull(row);
                row.Property =
                    SpectrumClassColumn.DissociationMethod.GetLocalizedColumnName(CultureInfo.CurrentCulture);
                row.SetOperation(FilterOperations.OP_EQUALS);
                row.SetValue("HCD");
                dlg.OkDialog();
            });
            ImportResultsFile(TestFilesDir.GetTestPath("DissociationMethodTest.mzML"));
            var chromatogramPointCounts = new List<int>();
            var document = SkylineWindow.Document;
            var tolerance = (float)document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            foreach (var molecule in document.Molecules)
            {
                Assert.IsTrue(document.MeasuredResults.TryLoadChromatogram(0, molecule, molecule.TransitionGroups.First(), tolerance,
                    out var chromatogramGroupInfos));
                Assert.AreEqual(1, chromatogramGroupInfos.Length);
                var numPoints = chromatogramGroupInfos[0].TimeIntensitiesGroup.TransitionTimeIntensities[0].NumPoints;
                Assert.AreNotEqual(0, numPoints);
                chromatogramPointCounts.Add(numPoints);
            }
            Assert.AreEqual(chromatogramPointCounts[0], chromatogramPointCounts[1] + chromatogramPointCounts[2]);
        }
    }
}
