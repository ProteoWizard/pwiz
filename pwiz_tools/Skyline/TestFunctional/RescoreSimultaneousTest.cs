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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests re-scoring a document when <see cref="Settings.ImportResultsSimultaneousFiles"/> is set to "many".
    /// There used to be a bug where <see cref="ChromatogramCache.ReadStream"/> would get closed while another
    /// thread was using it resulting in ObjectDisposedException.
    /// </summary>
    [TestClass]
    public class RescoreSimultaneousTest : AbstractFunctionalTest
    {
        const int REPLICATE_COUNT = 10;
        const int PEPTIDE_COUNT = 5;
        [TestMethod]
        public void TestRescoreSimultaneous()
        {
            TestFilesZip = @"TestFunctional\RescoreSimultaneousTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            int waitTime = 60 * REPLICATE_COUNT * PEPTIDE_COUNT;
            Settings.Default.ImportResultsSimultaneousFiles = (int) MultiFileLoader.ImportResultsSimultaneousFileOptions.many;
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Document.sky")));
            // Add many permutations of the peptide GNPTVEVELTTEK to the document.
            var peptideSequences = RescoreInPlaceTest.PermuteString("GNPTVEVELTTE").Distinct().Select(s => s + "K")
                .Take(PEPTIDE_COUNT);
            RunUI(() => SkylineWindow.Paste(TextUtil.LineSeparate(peptideSequences)));
            var filesToImport = new List<MsDataFileUri>();
            // Import the file "S_1.mzML" into the document multiple times,
            // copying it to a new name each time
            for (int iFile = 1; iFile <= REPLICATE_COUNT; iFile++)
            {
                var filePath = TestFilesDir.GetTestPath("S_" + iFile + ".mzML");
                if (iFile != 1)
                {
                    File.Copy(TestFilesDir.GetTestPath("S_1.mzML"), filePath);
                }
                filesToImport.Add(new MsDataFilePath(filePath));
            }
            ImportResultsFiles(filesToImport, waitTime);
            var manageResultsDlg = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
            RunDlg<RescoreResultsDlg>(manageResultsDlg.Rescore, dlg=>dlg.Rescore(false));
            WaitForDocumentLoaded(waitTime);
            manageResultsDlg = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
            RunDlg<RescoreResultsDlg>(manageResultsDlg.Rescore, dlg=>dlg.RescoreToFile(TestFilesDir.GetTestPath("RescoredDocument.sky")));
            WaitForDocumentLoaded(waitTime);
        }
    }
}
