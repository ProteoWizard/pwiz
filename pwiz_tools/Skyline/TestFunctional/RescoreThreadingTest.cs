using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class RescoreThreadingTest : AbstractFunctionalTest
    {
        const int REPLICATE_COUNT = 100;
        const int PEPTIDE_COUNT = 100;
        [TestMethod]
        public void TestRescoreThreading()
        {
            TestFilesZip = @"TestFunctional\RescoreThreadingTest.zip";
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
