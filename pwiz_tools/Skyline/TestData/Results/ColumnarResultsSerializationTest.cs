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

using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
{
    /// <summary>
    /// A document written without the chrom infos has to come back with its columnar results
    /// intact, because those are all it has: the areas, the retention times, which candidate peak
    /// each peak is, and the boundaries of the ones which are not candidate peaks.
    /// </summary>
    [TestClass]
    public class ColumnarResultsSerializationTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestColumnarResultsRoundTrip()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\Results\AgilentMix.zip");
            string docPath = TestFilesDir.GetTestPath("Bovine_std_curated_seq_small2.sky");
            var doc = ResultsUtil.DeserializeDocument(docPath);
            using (var docContainer = new ResultsTestDocumentContainer(doc, docPath))
            {
                var rawPath = new MsDataFilePath(TestFilesDir.GetTestPath(
                    "081809_100fmol-MichromMix-05" + ExtensionTestContext.ExtAgilentRaw));
                var chromSets = new[]
                {
                    new ChromatogramSet(@"AgilentTest", new[] {rawPath}),
                    new ChromatogramSet(@"AgilentTest2", new[] {rawPath})
                };
                var docResults = doc.ChangeMeasuredResults(new MeasuredResults(chromSets));
                Assert.IsTrue(docContainer.SetDocument(docResults, doc, true));
                docContainer.AssertComplete();
                docResults = docContainer.Document;

                var docRoundTrip = RoundTrip(docResults, false);
                int transitionsChecked = 0;
                int precursorsChecked = 0;
                int chosenPeakIndexesChecked = 0;
                using (var expected = docResults.MoleculeTransitions.GetEnumerator())
                using (var actual = docRoundTrip.MoleculeTransitions.GetEnumerator())
                {
                    while (expected.MoveNext() && actual.MoveNext())
                    {
                        var expectedResults = expected.Current.AbbreviatedResults;
                        var actualResults = actual.Current.AbbreviatedResults;
                        Assert.AreEqual(expectedResults.ChromFileIds.ReplicatePositions,
                            actualResults.ChromFileIds.ReplicatePositions,
                            string.Format(@"replicate positions: expected {0} replicates {1} total, actual {2} replicates {3} total",
                                expectedResults.ChromFileIds.ReplicatePositions.ReplicateCount,
                                expectedResults.ChromFileIds.ReplicatePositions.TotalCount,
                                actualResults.ChromFileIds.ReplicatePositions.ReplicateCount,
                                actualResults.ChromFileIds.ReplicatePositions.TotalCount));
                        Assert.AreEqual(expectedResults.ChromFileIds.FileIds.Count,
                            actualResults.ChromFileIds.FileIds.Count, @"file count");
                        CollectionAssert.AreEqual(expectedResults.Areas.ToArray(),
                            actualResults.Areas.ToArray(), @"areas");
                        CollectionAssert.AreEqual(expectedResults.UserSets?.ToArray(),
                            actualResults.UserSets?.ToArray(), @"user sets");
                        CollectionAssert.AreEqual(expectedResults.CustomPeaks?.ToArray(),
                            actualResults.CustomPeaks?.ToArray(), @"custom peaks");
                        AssertSameFiles(docResults, expectedResults.ChromFileIds, docRoundTrip,
                            actualResults.ChromFileIds);
                        transitionsChecked++;
                    }
                }

                using (var expected = docResults.MoleculeTransitionGroups.GetEnumerator())
                using (var actual = docRoundTrip.MoleculeTransitionGroups.GetEnumerator())
                {
                    while (expected.MoveNext() && actual.MoveNext())
                    {
                        var expectedResults = expected.Current.AbbreviatedResults;
                        var actualResults = actual.Current.AbbreviatedResults;
                        Assert.AreEqual(expectedResults.ChromFileIds.ReplicatePositions,
                            actualResults.ChromFileIds.ReplicatePositions);
                        CollectionAssert.AreEqual(expectedResults.Areas.ToArray(),
                            actualResults.Areas.ToArray(), @"areas");
                        CollectionAssert.AreEqual(expectedResults.RetentionTimes.ToArray(),
                            actualResults.RetentionTimes.ToArray(), @"retention times");
                        CollectionAssert.AreEqual(expectedResults.ChosenPeakIndexes?.ToArray(),
                            actualResults.ChosenPeakIndexes?.ToArray(), @"chosen peak indexes");
                        CollectionAssert.AreEqual(expectedResults.QValues?.ToArray(),
                            actualResults.QValues?.ToArray(), @"q values");
                        CollectionAssert.AreEqual(expectedResults.ZScores?.ToArray(),
                            actualResults.ZScores?.ToArray(), @"z scores");
                        CollectionAssert.AreEqual(expectedResults.UserSets?.ToArray(),
                            actualResults.UserSets?.ToArray(), @"user sets");
                        CollectionAssert.AreEqual(expectedResults.CustomPeaks?.ToArray(),
                            actualResults.CustomPeaks?.ToArray(), @"custom peaks");
                        AssertSameFiles(docResults, expectedResults.ChromFileIds, docRoundTrip,
                            actualResults.ChromFileIds);
                        for (int position = 0; position < expectedResults.Areas.Count; position++)
                        {
                            if (expectedResults.GetChosenPeakIndex(position).HasValue)
                            {
                                chosenPeakIndexesChecked++;
                            }
                        }

                        precursorsChecked++;
                    }
                }

                Assert.AreNotEqual(0, transitionsChecked);
                Assert.AreNotEqual(0, precursorsChecked);

                // Without these the comparisons above would agree about nothing worth agreeing on.
                Assert.AreNotEqual(0, chosenPeakIndexesChecked);

                // The chrom infos are not written at all, so what comes back has no peaks in them
                // until the chromatograms are loaded again. Everything said about those peaks is
                // in the columnar results compared above.
                foreach (var nodeTran in docRoundTrip.MoleculeTransitions)
                {
                    Assert.IsTrue(nodeTran.Results.All(chromInfoList => chromInfoList.IsEmpty));
                }

                // Sharing writes them, which is what the Panorama website reads.
                var docShared = RoundTrip(docResults, true);
                foreach (var nodeTran in docShared.MoleculeTransitions)
                {
                    Assert.IsNotNull(nodeTran.Results);
                }
            }
        }

        /// <summary>
        /// The file ids themselves cannot be compared: a document which has been read again has
        /// its own, and <see cref="ChromFileIds"/> matches them by reference. What has to agree is
        /// which file each position belongs to.
        /// </summary>
        private static void AssertSameFiles(SrmDocument expectedDocument, ChromFileIds expected,
            SrmDocument actualDocument, ChromFileIds actual)
        {
            Assert.AreEqual(expected.FileIds.Count, actual.FileIds.Count);
            for (int position = 0; position < expected.FileIds.Count; position++)
            {
                Assert.AreEqual(GetFilePath(expectedDocument, expected.FileIds[position]),
                    GetFilePath(actualDocument, actual.FileIds[position]));
            }
        }

        private static MsDataFileUri GetFilePath(SrmDocument document, ChromFileInfoId fileId)
        {
            var fileInfo = document.Settings.MeasuredResults.Chromatograms
                .SelectMany(chromatogramSet => chromatogramSet.MSDataFileInfos)
                .FirstOrDefault(info => ReferenceEquals(info.FileId, fileId));
            Assert.IsNotNull(fileInfo);
            return fileInfo.FilePath;
        }

        private SrmDocument RoundTrip(SrmDocument document, bool writeChromInfos)
        {
            var stringBuilder = new StringBuilder();
            using (var writer = new XmlTextWriter(new StringWriter(stringBuilder)))
            {
                writer.Formatting = Formatting.Indented;
                document.Serialize(writer, null, SkylineVersion.CURRENT, null, writeChromInfos);
            }

            using (var reader = new StringReader(stringBuilder.ToString()))
            {
                return (SrmDocument) new XmlSerializer(typeof(SrmDocument)).Deserialize(reader);
            }
        }
    }
}
