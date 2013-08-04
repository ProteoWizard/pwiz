/*
 * Original author: Dario Amodei <damodei .at. stanford.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using pwiz.Skyline.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    /// <summary>
    /// Tests the chromatogram export feature.
    /// Does not attempt to test the (very simple) UI.
    ///</summary>
    [TestClass]
    public class ChromatogramExporterTest : AbstractUnitTest
    {
        const string TEST_ZIP_PATH = @"TestA\ChromatogramExporterTest.zip";
        private const string FILE_1 = "Q_2012_1121_RJ_56.mzXML";
        private const string FILE_2 = "Q_2012_1121_RJ_58.mzXML";
        private const string FILE_3 = "Q_2012_1121_RJ_59.mzXML";
        private static readonly string[] FILE_NAMES_ALL = new[] { FILE_1, FILE_2, FILE_3 };
        private static readonly string[] FILE_NAMES_2 = new[] { FILE_1, FILE_2 };
        private static readonly string[] FILE_NAMES_1 = new[] { FILE_1 };
        private static readonly ChromExtractor[] EXTRACTOR_ALL = new[] { ChromExtractor.summed, ChromExtractor.base_peak };
        private static readonly ChromExtractor[] EXTRACTOR_2 = new[] { ChromExtractor.summed };
        private static readonly ChromExtractor[] EXTRACTOR_1 = new[] { ChromExtractor.base_peak };
        private static readonly ChromSource[] SOURCES_ALL = new[] { ChromSource.ms1 };
        private static readonly ChromSource[] SOURCES_2 = new[] { ChromSource.ms1, ChromSource.fragment };
        private static readonly ChromSource[] SOURCES_1 = new[] { ChromSource.fragment };

        [TestMethod]
        public void ChromatogramExportTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, TEST_ZIP_PATH);
            string chromExportDoc = testFilesDir.GetTestPath("ChromToExport.sky");
            string fileExpectedUs1 = testFilesDir.GetTestPath("ChromToExport1.tsv");
            string fileExpectedIntl1 = testFilesDir.GetTestPath("ChromToExport1_Intl.tsv");
            string fileExpectedUs2 = testFilesDir.GetTestPath("ChromToExport2.tsv");
            string fileExpectedIntl2 = testFilesDir.GetTestPath("ChromToExport2_Intl.tsv");
            string fileExpectedUsAll = testFilesDir.GetTestPath("ChromToExportAll.tsv");
            string fileExpectedIntlAll = testFilesDir.GetTestPath("ChromToExportAll_Intl.tsv");
            string fileActual = testFilesDir.GetTestPath("ExportActual.tsv");
            bool isIntl = (TextUtil.CsvSeparator != TextUtil.SEPARATOR_CSV);
            
            SrmDocument doc = ResultsUtil.DeserializeDocument(chromExportDoc);
            // Load an empty doc, so that we can make a change and 
            // cause the .skyd to be loaded
            var docContainer = new ResultsTestDocumentContainer(null, chromExportDoc);
            docContainer.SetDocument(doc, null, true);
            docContainer.AssertComplete();
            SrmDocument docResults = docContainer.Document;
            SaveChrom(docResults, fileActual, FILE_NAMES_1.ToList(), CultureInfo.CurrentCulture, EXTRACTOR_1, SOURCES_1);
            Assert.IsTrue(FileEquals(isIntl ? fileExpectedIntl1 : fileExpectedUs1, fileActual));
            SaveChrom(docResults, fileActual, FILE_NAMES_2.ToList(), CultureInfo.CurrentCulture, EXTRACTOR_2, SOURCES_2);
            Assert.IsTrue(FileEquals(isIntl ? fileExpectedIntl2 : fileExpectedUs2, fileActual));
            SaveChrom(docResults, fileActual, FILE_NAMES_ALL.ToList(), CultureInfo.CurrentCulture, EXTRACTOR_ALL, SOURCES_ALL);
            Assert.IsTrue(FileEquals(isIntl ? fileExpectedIntlAll : fileExpectedUsAll, fileActual));

        }

        private static void SaveChrom(SrmDocument docResults,
                                      string fileToSave, 
                                      IList<string> fileNames,
                                      CultureInfo cultureInfo,
                                      IList<ChromExtractor> extractors,
                                      IList<ChromSource> sources)
        {
            var chromExporter = new ChromatogramExporter(docResults);
            using (var saver = new FileSaver(fileToSave))
            using (var writer = new StreamWriter(saver.SafeName))
            {
                chromExporter.Export(writer, null, fileNames, CultureInfo.CurrentCulture, extractors, sources);
                writer.Flush();
                writer.Close();
                saver.Commit();
            }
        }

        static bool FileEquals(string path1, string path2)
        {
            byte[] file1 = File.ReadAllBytes(path1);
            byte[] file2 = File.ReadAllBytes(path2);
            if (file1.Length == file2.Length)
                return !file1.Where((t, i) => t != file2[i]).Any();
            return false;
        }
    }
}
