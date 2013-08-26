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
using pwiz.Skyline.Properties;
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

        /// <summary>
        /// Set to true to regenerate the comparison files
        /// </summary>
        private bool IsSaveAll { get { return false; } }

        [TestMethod]
        public void ChromatogramExportTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, TEST_ZIP_PATH);
            string chromExportDoc = testFilesDir.GetTestPath("ChromToExport.sky");
            string fileExpectedUs1 = testFilesDir.GetTestPath("ChromToExport1.tsv");
            string fileActualUs1 = GetActualName(fileExpectedUs1);
            string fileExpectedUs2 = testFilesDir.GetTestPath("ChromToExport2.tsv");
            string fileActualUs2 = GetActualName(fileExpectedUs2);
            string fileExpectedUsAll = testFilesDir.GetTestPath("ChromToExportAll.tsv");
            string fileActualUsAll = GetActualName(fileExpectedUsAll);
            
            SrmDocument doc = ResultsUtil.DeserializeDocument(chromExportDoc);
            // Load an empty doc, so that we can make a change and 
            // cause the .skyd to be loaded
            var docContainer = new ResultsTestDocumentContainer(null, chromExportDoc);
            docContainer.SetDocument(doc, null, true);
            docContainer.AssertComplete();
            SrmDocument docResults = docContainer.Document;
            if (IsSaveAll)
            {
                // For regenerating all of the required expected files, if things change
                SaveChrom(docResults, fileExpectedUs1, FILE_NAMES_1.ToList(), CultureInfo.GetCultureInfo("en-US"), EXTRACTOR_1, SOURCES_1);
                SaveChrom(docResults, fileExpectedUs2, FILE_NAMES_2.ToList(), CultureInfo.GetCultureInfo("en-US"), EXTRACTOR_2, SOURCES_2);
                SaveChrom(docResults, fileExpectedUsAll, FILE_NAMES_ALL.ToList(), CultureInfo.GetCultureInfo("en-US"), EXTRACTOR_ALL, SOURCES_ALL);
                SaveChrom(docResults, GetIntlName(fileExpectedUs1), FILE_NAMES_1.ToList(), CultureInfo.GetCultureInfo("fr-FR"), EXTRACTOR_1, SOURCES_1);
                SaveChrom(docResults, GetIntlName(fileExpectedUs2), FILE_NAMES_2.ToList(), CultureInfo.GetCultureInfo("fr-FR"), EXTRACTOR_2, SOURCES_2);
                SaveChrom(docResults, GetIntlName(fileExpectedUsAll), FILE_NAMES_ALL.ToList(), CultureInfo.GetCultureInfo("fr-FR"), EXTRACTOR_ALL, SOURCES_ALL);
            }

            SaveChrom(docResults, GetLocaleName(fileActualUs1), FILE_NAMES_1.ToList(), CultureInfo.CurrentCulture, EXTRACTOR_1, SOURCES_1);
            SaveChrom(docResults, GetLocaleName(fileActualUs2), FILE_NAMES_2.ToList(), CultureInfo.CurrentCulture, EXTRACTOR_2, SOURCES_2);
            SaveChrom(docResults, GetLocaleName(fileActualUsAll), FILE_NAMES_ALL.ToList(), CultureInfo.CurrentCulture, EXTRACTOR_ALL, SOURCES_ALL);

            AssertFileEquals(GetLocaleName(fileExpectedUs1), GetLocaleName(fileActualUs1));
            AssertFileEquals(GetLocaleName(fileExpectedUs2), GetLocaleName(fileActualUs2));
            AssertFileEquals(GetLocaleName(fileExpectedUsAll), GetLocaleName(fileActualUsAll));

            // Close the .skyd file
            var docEmpty = new SrmDocument(SrmSettingsList.GetDefault());
            Assert.IsTrue(docContainer.SetDocument(docEmpty, docContainer.Document));
        }

        private static string GetActualName(string fileExpected)
        {
            return Path.Combine(Path.GetDirectoryName(fileExpected) ?? "",
                "Actual_" + Path.GetFileName(fileExpected));
        }

        private static string GetLocaleName(string fileExpected)
        {
            if (TextUtil.CsvSeparator == TextUtil.SEPARATOR_CSV)
                return fileExpected;

            return GetIntlName(fileExpected);
        }

        private static string GetIntlName(string fileExpected)
        {
            return Path.Combine(Path.GetDirectoryName(fileExpected) ?? "",
                                Path.GetFileNameWithoutExtension(fileExpected) + "_Intl" +
                                Path.GetExtension(fileExpected));
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
                chromExporter.Export(writer, null, fileNames, cultureInfo, extractors, sources);
                writer.Flush();
                writer.Close();
                saver.Commit();
            }
        }

        private static void AssertFileEquals(string path1, string path2)
        {
            string file1 = File.ReadAllText(path1);
            string file2 = File.ReadAllText(path2);
            AssertEx.NoDiff(file1, file2);
        }
    }
}
