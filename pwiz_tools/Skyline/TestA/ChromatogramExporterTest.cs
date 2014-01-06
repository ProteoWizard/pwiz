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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
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
        private static readonly string[] FILE_NAMES_ALL = { FILE_1, FILE_2, FILE_3 };
        private static readonly string[] FILE_NAMES_2 = { FILE_1, FILE_2 };
        private static readonly string[] FILE_NAMES_1 = { FILE_1 };
        private static readonly ChromExtractor[] EXTRACTOR_ALL = { ChromExtractor.summed, ChromExtractor.base_peak };
        private static readonly ChromExtractor[] EXTRACTOR_2 = { ChromExtractor.summed };
        private static readonly ChromExtractor[] EXTRACTOR_1 = { ChromExtractor.base_peak };
        private static readonly ChromSource[] SOURCES_ALL = { ChromSource.ms1 };
        private static readonly ChromSource[] SOURCES_2 = { ChromSource.ms1, ChromSource.fragment };
        private static readonly ChromSource[] SOURCES_1 = { ChromSource.fragment };
        private const string EXPORT_1 = "ChromToExport1.tsv";
        private const string EXPORT_2 = "ChromToExport2.tsv";
        private const string EXPORT_ALL = "ChromToExportAll.tsv";

        /// <summary>
        /// Set to true to regenerate the comparison files
        /// </summary>
        private bool IsSaveAll { get { return false; } }

        [TestMethod]
        public void ChromatogramExportTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, TEST_ZIP_PATH);
            string chromExportDoc = testFilesDir.GetTestPath("ChromToExport.sky");
            string fileExpected1 = testFilesDir.GetTestPathLocale(EXPORT_1);
            string fileActual1 = GetActualName(fileExpected1);
            string fileExpected2 = testFilesDir.GetTestPathLocale(EXPORT_2);
            string fileActual2 = GetActualName(fileExpected2);
            string fileExpectedAll = testFilesDir.GetTestPathLocale(EXPORT_ALL);
            string fileActualAll = GetActualName(fileExpectedAll);
            
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
                SaveChrom(docResults, testFilesDir.GetTestPath(EXPORT_1), FILE_NAMES_1.ToList(), CultureInfo.GetCultureInfo("en-US"), EXTRACTOR_1, SOURCES_1);
                SaveChrom(docResults, testFilesDir.GetTestPath(EXPORT_2), FILE_NAMES_2.ToList(), CultureInfo.GetCultureInfo("en-US"), EXTRACTOR_2, SOURCES_2);
                SaveChrom(docResults, testFilesDir.GetTestPath(EXPORT_ALL), FILE_NAMES_ALL.ToList(), CultureInfo.GetCultureInfo("en-US"), EXTRACTOR_ALL, SOURCES_ALL);
                SaveChrom(docResults, testFilesDir.GetTestPathIntl(EXPORT_1), FILE_NAMES_1.ToList(), CultureInfo.GetCultureInfo("fr-FR"), EXTRACTOR_1, SOURCES_1);
                SaveChrom(docResults, testFilesDir.GetTestPathIntl(EXPORT_2), FILE_NAMES_2.ToList(), CultureInfo.GetCultureInfo("fr-FR"), EXTRACTOR_2, SOURCES_2);
                SaveChrom(docResults, testFilesDir.GetTestPathIntl(EXPORT_ALL), FILE_NAMES_ALL.ToList(), CultureInfo.GetCultureInfo("fr-FR"), EXTRACTOR_ALL, SOURCES_ALL);
            }

            SaveChrom(docResults, fileActual1, FILE_NAMES_1.ToList(), LocalizationHelper.CurrentCulture, EXTRACTOR_1, SOURCES_1);
            SaveChrom(docResults, fileActual2, FILE_NAMES_2.ToList(), LocalizationHelper.CurrentCulture, EXTRACTOR_2, SOURCES_2);
            SaveChrom(docResults, fileActualAll, FILE_NAMES_ALL.ToList(), LocalizationHelper.CurrentCulture, EXTRACTOR_ALL, SOURCES_ALL);

            AssertEx.FileEquals(fileExpected1, fileActual1);
            AssertEx.FileEquals(fileExpected2, fileActual2);
            AssertEx.FileEquals(fileExpectedAll, fileActualAll);

            // Close the .skyd file
            docContainer.Release();
        }

        private static string GetActualName(string fileExpected)
        {
            return Path.Combine(Path.GetDirectoryName(fileExpected) ?? "",
                "Actual_" + Path.GetFileName(fileExpected));
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
    }
}
