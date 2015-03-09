/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    /// <summary>
    /// Test of precursor transition selection for full-scan MS1 cases
    /// </summary>
    [TestClass]
    public class FullScanPrecursorTest : AbstractUnitTest
    {
        [TestMethod]
        public void FullScanPrecursorTransitionsTest()
        {
            TestFilesDir testFilesDir = new TestFilesDir(TestContext, @"TestA\FullScanPrecursor.zip");

            string docPath = testFilesDir.GetTestPath("FullScanPrecursor.sky");
            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
            AssertEx.IsDocumentState(doc, 0, 1, 4, 5, 52);
            var docContainer = new ResultsTestDocumentContainer(doc, docPath);
            var mitoLibSpec = new BiblioSpecLiteSpec("mito2", testFilesDir.GetTestPath("mito2.blib"));
            doc = docContainer.ChangeLibSpecs(new [] { mitoLibSpec });
            Assert.IsTrue(doc.IsLoaded);            

            // Switch to only precursor ions
            var docPrecOnly = doc.ChangeSettings(doc.Settings.ChangeTransitionFilter(filter =>
                filter.ChangeIonTypes(new[] { IonType.precursor })));
            // All precursors should have 3 precursor transitions (M, M+1 and M+2)
            AssertEx.IsDocumentState(docPrecOnly, 3, 1, 4, 5, 15);
            Assert.IsFalse(docPrecOnly.PeptideTransitions.Any(nodeTran => nodeTran.Transition.IonType != IonType.precursor));

            // Use low resolution MS1 filtering
            var docLowMs1 = docPrecOnly.ChangeSettings(docPrecOnly.Settings.ChangeTransitionFullScan(fs =>
                fs.ChangePrecursorIsotopes(FullScanPrecursorIsotopes.Count, 1, null)
                  .ChangePrecursorResolution(FullScanMassAnalyzerType.qit, 0.5, null)));
            // All precursors should have one precursor transition
            AssertEx.IsDocumentState(docLowMs1, 4, 1, 4, 5, 5);

            // Add y-ions to low resolution filtering
            var docLowMs1Y = docLowMs1.ChangeSettings(docLowMs1.Settings.ChangeTransitionFilter(filter =>
                filter.ChangeIonTypes(new[] { IonType.precursor, IonType.y })));
            AssertEx.IsDocumentState(docLowMs1Y, 5, 1, 4, 5, 33);

            // Turn off MS1 filtering
            var docNoMs1 = docPrecOnly.ChangeSettings(docPrecOnly.Settings.ChangeTransitionFullScan(fs =>
                fs.ChangePrecursorIsotopes(FullScanPrecursorIsotopes.None, null, null)));
            // One of the precursors should have no transitions, since its spectrum has no precursor match
            AssertEx.IsDocumentState(docNoMs1, 4, 1, 4, 5, 4);

            // Turn off MS/MS library matching
            var docNoLibMatch = docNoMs1.ChangeSettings(docNoMs1.Settings.ChangeTransitionLibraries(lib =>
                lib.ChangePick(TransitionLibraryPick.none)));
            // All precursors should have a single precursor transition
            AssertEx.IsDocumentState(docNoLibMatch, 5, 1, 4, 5, 5);

            // Use library plus filter matching
            var docLibPlusMatch = docNoMs1.ChangeSettings(docNoMs1.Settings.ChangeTransitionLibraries(lib =>
                lib.ChangePick(TransitionLibraryPick.all_plus)));
            // All precursors should have a single precursor transition
            AssertEx.IsDocumentState(docLibPlusMatch, 5, 1, 4, 5, 5);

            // Release the library stream, and dispose of the directory
            docContainer.ChangeLibSpecs(new LibrarySpec[0]);
            testFilesDir.Dispose();
        }
    }
}