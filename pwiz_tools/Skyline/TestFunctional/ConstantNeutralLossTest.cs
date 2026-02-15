/*
 * Original author: Brian Pratt <bspratt .at. uw.edu>,
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.SkylineTestUtil;
using System.IO;
using System.Linq;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Load a small Agilent Constant Neutral Loss (CNL) file  and check against curated results.
    /// Then provoke a failure by changing the expected CNL value in the document so that no scans match.
    /// </summary>
    [TestClass]
    public class AgilentConstantNeutralLossTest : AbstractFunctionalTestEx
    {

        [TestMethod]
        public void ConstantNeutralLossTest()
        {
            TestFilesZip = @"TestFunctional\ConstantNeutralLossTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            VerifyConstantNeutralLossScans(); // Has two different Neutral Loss spectrum filters on same precursor mz. Only one of them should hit.
            VerifyConstantNeutralLossScans(true); // Now do it again but with incorrect CNL value, so nothing matches.
        }

        private void VerifyConstantNeutralLossScans(bool provokeFailure = false)
        {
            var docPath = TestFilesDir.GetTestPath(@"ConstantNeutralLossTest.sky");
            if (provokeFailure)
            {
                var text = File.ReadAllText(docPath).Replace("161.", "165."); // Replace correct CNL value with incorrect one
                File.WriteAllText(docPath, text);
                // Remove audit log file so we don't get complaints about it being out of date
                var skylPath = TestFilesDir.GetTestPath(@"ConstantNeutralLossTest.skyl");
                FileEx.SafeDelete(skylPath); 
            }
            OpenDocument(docPath);
            var testData = Path.Combine(TestFilesDir.GetVendorTestData(TestFilesDir.VendorDir.Agilent), @"RS080806_NL_448.2_001.d");
            ImportResults(testData);
            var doc = WaitForDocumentLoaded();

            // If data has not been properly understood as Constant Neutral Loss scans, expected number of peaks will not be found
            var nPeaksFound = (from tg in SkylineWindow.Document.MoleculeTransitions 
                from r in tg.Results 
                from peak in r select peak).Count(peak => peak.Area > 0);
            AssertEx.AreEqual(provokeFailure ? 0 : 1, nPeaksFound);
            LoadNewDocument(true);
        }
    }
}