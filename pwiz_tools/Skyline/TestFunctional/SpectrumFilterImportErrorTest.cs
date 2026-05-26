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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// A transition list whose Spectrum Filter column contains text that cannot be parsed must be
    /// reported as an ordinary row error in the import dialog, not crash the import. The small
    /// molecule transition list reader previously let the parse exception escape unhandled.
    /// </summary>
    [TestClass]
    public class SpectrumFilterImportErrorTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSpectrumFilterImportError()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.small_molecules));

            // One otherwise-valid precursor-only molecule whose Spectrum Filter text cannot be parsed
            const string badFilter = "CollisionEnergy=17";
            var text =
                "Molecule List Name\tPrecursor Name\tPrecursor Formula\tPrecursor Adduct\tSpectrumFilter\n" +
                "Lipid\tL1\tC41H74NO8P\t[M+H]\t" + badFilter + "\n";
            SetClipboardText(text);

            // Paste into the targets area; the column-select dialog opens because the list has headers
            var transitionDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());

            // Before the fix this threw an unhandled FormatException; now it surfaces as a row error
            var errDlg = ShowDialog<ImportTransitionListErrorDlg>(() => transitionDlg.buttonCheckForErrors.PerformClick());
            var expectedMessage = string.Format(
                SpectraResources.SpectrumClassFilter_ParseFilterString_Invalid_spectrum_filter_format,
                badFilter);
            RunUI(() =>
            {
                Assert.AreEqual(1, errDlg.ErrorList.Count);
                AssertEx.Contains(errDlg.ErrorList[0].ErrorMessage, expectedMessage);
            });
            OkDialog(errDlg, errDlg.OkDialog);
            OkDialog(transitionDlg, transitionDlg.CancelDialog);
        }
    }
}
