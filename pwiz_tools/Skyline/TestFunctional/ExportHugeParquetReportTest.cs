/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using pwiz.CommonMsData;
using pwiz.Skyline.Controls.Databinding;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ExportHugeParquetReportTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestExportHugeParquetReport()
        {
            TestFilesZip = @"TestFunctional\ExportHugeParquetReportTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Document.sky")));
            // Add many permutations of the peptide GNPTVEVELTTEK to the document.
            var peptideSequences = RescoreInPlaceTest.PermuteString("GNPTVEVELTTE").Distinct().Select(s => s + "K").Take(2000);
            RunUI(() => SkylineWindow.Paste(TextUtil.LineSeparate(peptideSequences)));
            const int fileCount = 5;
            List<string> filesToImport = new List<string>();
            for (int iFile = 1; iFile <= fileCount; iFile++)
            {
                var filePath = TestFilesDir.GetTestPath("S_" + iFile + ".mzML");
                if (filesToImport.Count > 0)
                {
                    File.Copy(filesToImport[0], filePath);
                }
                filesToImport.Add(filePath);
            }
            ImportResultsFiles(filesToImport.Select(f=>new MsDataFilePath(f)));
            var parquetFilePath = TestFilesDir.GetTestPath("prism.parquet");
            RunDlg<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog, exportReportDlg =>
            {
                Assert.IsFalse(exportReportDlg.InvariantLanguage);
                exportReportDlg.ReportName = "PRISM";
                exportReportDlg.OkDialog(parquetFilePath, TextUtil.CsvSeparator);
            });

        }
    }
}
