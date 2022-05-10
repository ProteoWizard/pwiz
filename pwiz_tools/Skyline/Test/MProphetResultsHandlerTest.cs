/*
 * Original author: Dario Amodei <damodei .at. standard.edu>,
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

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class MProphetResultsHandlerTest : AbstractUnitTest
    {
        private const string ZIP_FILE = @"Test\MProphetResultsHandlerTest.zip"; // Not L10N
        private const double Q_CUTOFF = 0.005;
        private const double Q_CUTOFF_HIGH = 0.20;
        private const string REPORT_EXPECTED = "ReportExpected.csv";
        private const string REPORT_ACTUAL = "ReportActual.csv";
        private const string MPROPHET_EXPECTED = "MProphetExpected.csv";
        private const string MPROPHET_ACTUAL = "MProphetActual.csv";

        /// <summary>
        /// Set to true to regenerate the comparison files
        /// </summary>
        private bool IsSaveAll { get { return false; } }

        [TestMethod]
        public void TestMProphetResultsHandler()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            var documentFile = testFilesDir.GetTestPath("MProphetGold-trained.sky");
            SrmDocument doc = ResultsUtil.DeserializeDocument(documentFile);
            // Load libraries
            doc = doc.ChangeSettings(doc.Settings.ChangePeptideLibraries(libraries =>
                {
                    var lib = libraries.Libraries[0];
                    return libraries.ChangeLibrarySpecs(new LibrarySpec[]
                        {
                            new BiblioSpecLiteSpec(lib.Name, testFilesDir.GetTestPath(lib.FileNameHint))
                        });
                }));
            // Load an empty doc, so that we can make a change and 
            // cause the .skyd to be loaded
            using (var docContainer = new ResultsTestDocumentContainer(null, documentFile))
            {
                docContainer.SetDocument(doc, null, true);
                docContainer.AssertComplete();
                SrmDocument docOriginal = docContainer.Document;
                var peakScoringModel = docOriginal.Settings.PeptideSettings.Integration.PeakScoringModel;
                var resultsHandler = new MProphetResultsHandler(docOriginal, peakScoringModel) { QValueCutoff = Q_CUTOFF };

                // 1. Reintegrate and export report produces expected file
                resultsHandler.ScoreFeatures();
                var docNew = resultsHandler.ChangePeaks();
                var reportSpec = MakeReportSpec();
                if (IsSaveAll)
                {
                    // For regenerating expected files if things change
                    ReportToCsv(reportSpec, docNew, testFilesDir.GetTestPath(REPORT_EXPECTED), CultureInfo.GetCultureInfo("en-US"));
                    ReportToCsv(reportSpec, docNew, testFilesDir.GetTestPathIntl(REPORT_EXPECTED), CultureInfo.GetCultureInfo("fr-FR"));
                }
                string docNewActual = testFilesDir.GetTestPath(REPORT_ACTUAL);
                string docNewExpected = testFilesDir.GetTestPathLocale(REPORT_EXPECTED);
                ReportToCsv(reportSpec, docNew, docNewActual, CultureInfo.CurrentCulture);
                AssertEx.FileEquals(docNewExpected, docNewActual);

                // 2. Reintegrating again gives no change in document
                var resultsHandlerRepeat = new MProphetResultsHandler(docNew, peakScoringModel) { QValueCutoff = Q_CUTOFF };
                resultsHandlerRepeat.ScoreFeatures();
                var docRepeat = resultsHandlerRepeat.ChangePeaks();
                Assert.AreSame(docRepeat, docNew);
                Assert.AreNotSame(docOriginal, docNew);

                // 3. Export mProphet results gives expected file
                var calcs = peakScoringModel.PeakFeatureCalculators;
                var mProphetActual = testFilesDir.GetTestPath(MPROPHET_ACTUAL);
                var mProphetExpected = testFilesDir.GetTestPathLocale(MPROPHET_EXPECTED);
                if (IsSaveAll)
                {
                    // For regenerating files
                    SaveMProphetFeatures(resultsHandler, testFilesDir.GetTestPath(MPROPHET_EXPECTED), CultureInfo.GetCultureInfo("en-US"), calcs);
                    SaveMProphetFeatures(resultsHandler, testFilesDir.GetTestPathIntl(MPROPHET_EXPECTED), CultureInfo.GetCultureInfo("fr-FR"), calcs);
                }
                SaveMProphetFeatures(resultsHandler, mProphetActual, CultureInfo.CurrentCulture, calcs);
                AssertEx.FileEquals(mProphetExpected, mProphetActual);

                // 4. Export mProphet -> Import Peak Boundaries leads to same result as reintegrate
                var resultsHandlerQAll = new MProphetResultsHandler(docOriginal, peakScoringModel) { QValueCutoff = 1.0 };
                resultsHandlerQAll.ScoreFeatures();
                var docNewQAll = resultsHandlerQAll.ChangePeaks();
                var peakBoundaryImporter = new PeakBoundaryImporter(docNewQAll);
                long lineCount = Helpers.CountLinesInFile(mProphetActual);
                peakBoundaryImporter.Import(mProphetActual, null, lineCount);
                var docImport = peakBoundaryImporter.Document;
                // Serialized documents are easier to debug when something is different
                string strDocNew = SerializeDoc(docNewQAll);
                string strDocImport = SerializeDoc(docImport);
                AssertEx.NoDiff(strDocNew, strDocImport);
                Assert.AreSame(docNewQAll, docImport);

                // 5. Reintegration with q value cutoff of <0 causes all peaks set to null
                var handlerAllNull = new MProphetResultsHandler(docOriginal, peakScoringModel) { QValueCutoff = -0.001 };
                handlerAllNull.ScoreFeatures();
                var docNull = handlerAllNull.ChangePeaks();
                foreach (var transitionNode in docNull.PeptideTransitions)
                    foreach (var chromInfo in transitionNode.ChromInfos)
                        Assert.IsTrue(chromInfo.IsEmpty || transitionNode.IsDecoy);

                // 6. Reintegration adjusts example peak to null at q=0.005 cutoff, but adjusts it to a non-null peak at q=0.20
                const int groupNum = 11;
                var midQNode = resultsHandler.Document.PeptideTransitionGroups.ToList()[groupNum];
                foreach (var chromInfo in midQNode.Transitions.SelectMany(transition => transition.ChromInfos))
                    Assert.IsTrue(chromInfo.IsEmpty);
                resultsHandler.QValueCutoff = Q_CUTOFF_HIGH;
                resultsHandler.ChangePeaks();
                var midQNodeNew = resultsHandler.Document.PeptideTransitionGroups.ToList()[groupNum];
                foreach (var chromInfo in midQNodeNew.Transitions.SelectMany(transition => transition.ChromInfos))
                    Assert.IsFalse(chromInfo.IsEmpty);

                // 7. Labeled peptide pairs still have matching peaks
                foreach (var peptideNode in resultsHandler.Document.Peptides)
                {
                    Assert.AreEqual(2, peptideNode.TransitionGroupCount);
                    var groupList = peptideNode.TransitionGroups.ToList();
                    var lightGroup = groupList[0];
                    var heavyGroup = groupList[0];
                    var lightChromInfo = lightGroup.ChromInfos.ToList()[0];
                    var heavyChromInfo = heavyGroup.ChromInfos.ToList()[0];
                    Assert.AreEqual(lightChromInfo.StartRetentionTime, heavyChromInfo.StartRetentionTime);
                    Assert.AreEqual(lightChromInfo.EndRetentionTime, heavyChromInfo.EndRetentionTime);
                    Assert.AreEqual(lightChromInfo.RetentionTime, heavyChromInfo.RetentionTime);
                }

                // 8. Verify that chosen peaks and q values are the same as those in mProphet paper: 
                // http://www.nature.com/nmeth/journal/v8/n5/full/nmeth.1584.html#/supplementary-information
                // TODO: Grab this data from the mProphet paper
            }
        }

        private string SerializeDoc(SrmDocument doc)
        {
            XmlSerializer ser = new XmlSerializer(typeof(SrmDocument));
            StringBuilder sb = new StringBuilder();
            using (XmlTextWriter writer = new XmlTextWriter(new StringWriter(sb)))
            {
                writer.Formatting = Formatting.Indented;
                ser.Serialize(writer, doc);
            }
            return sb.ToString();
        }

        private static ReportSpec MakeReportSpec()
        {
            Type tableTran = typeof (DbTransition);
            Type tableTranRes = typeof (DbTransitionResult);
            return new ReportSpec("PeakBoundaries", new QueryDef
                {
                    Select = new[]
                        {
                            new ReportColumn(tableTran, "Precursor", "Charge"),
                            new ReportColumn(tableTranRes, "ResultFile", "FileName"),
                            new ReportColumn(tableTranRes, "PrecursorResult", "MinStartTime"),
                            new ReportColumn(tableTranRes, "PrecursorResult", "MaxEndTime"),
                            new ReportColumn(tableTran, "Precursor", "Peptide", "ModifiedSequence"),
                        }
                });

        }

        public void ReportToCsv(ReportSpec reportSpec, SrmDocument doc, string fileName, CultureInfo cultureInfo)
        {
            CheckReportCompatibility.ReportToCsv(reportSpec, doc, fileName, cultureInfo);
        }

        private static void SaveMProphetFeatures(MProphetResultsHandler resultsHandler, 
                                          string saveFile, 
                                          CultureInfo cultureInfo,
                                          FeatureCalculators calcs)
        {
            using (var saver = new FileSaver(saveFile))
            using (var writer = new StreamWriter(saver.SafeName))
            {
                resultsHandler.WriteScores(writer, cultureInfo, calcs);
                writer.Flush();
                writer.Close();
                saver.Commit();
            }
        }
    }
}
