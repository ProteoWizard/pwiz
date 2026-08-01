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
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
{
    /// <summary>
    /// A document written without the chrom infos has to come back with its columnar results
    /// intact, because those are all it has: the transition areas, the precursor peak times, which
    /// candidate peak each peak is, and the boundaries of the ones which are not candidate peaks.
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

                var docRoundTrip = RoundTrip(docResults, false, out string compactXml);

                // Nothing in this document has a user set peak, annotations or a missing area, so
                // every transition area rides on its precursor and no transition needs an element
                // of its own.
                StringAssert.Contains(compactXml, @"transition_areas=");
                Assert.IsFalse(compactXml.Contains(@"transition_results_columnar"));
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
                        CollectionAssert.AreEqual(expectedResults.Areas.Values.ToArray(),
                            actualResults.Areas.Values.ToArray(), @"areas");
                        CollectionAssert.AreEqual(expectedResults.UserSets?.Values.ToArray(),
                            actualResults.UserSets?.Values.ToArray(), @"user sets");
                        CollectionAssert.AreEqual(expectedResults.CustomPeaks?.Values.ToArray(),
                            actualResults.CustomPeaks?.Values.ToArray(), @"custom peaks");
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
                        CollectionAssert.AreEqual(expectedResults.RetentionTimes.ToArray(),
                            actualResults.RetentionTimes.ToArray(), @"retention times");
                        CollectionAssert.AreEqual(expectedResults.StartTimes.ToArray(),
                            actualResults.StartTimes.ToArray(), @"start times");
                        CollectionAssert.AreEqual(expectedResults.EndTimes.ToArray(),
                            actualResults.EndTimes.ToArray(), @"end times");
                        CollectionAssert.AreEqual(expectedResults.ChosenPeakIndexes?.ToArray(),
                            actualResults.ChosenPeakIndexes?.ToArray(), @"chosen peak indexes");
                        CollectionAssert.AreEqual(expectedResults.QValues?.ToArray(),
                            actualResults.QValues?.ToArray(), @"q values");
                        CollectionAssert.AreEqual(expectedResults.ZScores?.ToArray(),
                            actualResults.ZScores?.ToArray(), @"z scores");
                        CollectionAssert.AreEqual(expectedResults.UserSets?.ToArray(),
                            actualResults.UserSets?.ToArray(), @"user sets");
                        CollectionAssert.AreEqual(expectedResults.Annotations?.ToArray(),
                            actualResults.Annotations?.ToArray(), @"annotations");
                        AssertSameFiles(docResults, expectedResults.ChromFileIds, docRoundTrip,
                            actualResults.ChromFileIds);
                        for (int position = 0; position < expectedResults.RetentionTimes.Count; position++)
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

                // A transition keeps only its columnar results either way.
                foreach (var nodeTran in docRoundTrip.MoleculeTransitions)
                {
                    Assert.IsTrue(nodeTran.Results == null ||
                                  nodeTran.Results.All(chromInfoList => chromInfoList.IsEmpty));
                }

                // Sharing writes them, which is what the Panorama website reads. A transition does
                // not hold on to its chrom infos any more, so these are worked out again from the
                // chromatograms while writing.
                // What the writer has to work from when it writes the chrom infos.
                var firstPep = docResults.Peptides.First();
                var firstGroup = firstPep.TransitionGroups.First();
                var firstTran = firstGroup.Transitions.First();
                var restored = new MoleculeResults(docResults.Settings, firstPep)
                    .GetTransitionChromInfos(firstGroup.TransitionGroup, firstTran.Transition);
                Assert.IsNotNull(restored, @"MoleculeResults gave nothing back for the first transition");
                Assert.AreNotEqual(0, restored.Sum(chromInfoList => chromInfoList.Count),
                    @"MoleculeResults gave no chrom infos for the first transition");

                var docShared = RoundTrip(docResults, true, out string sharedXml);
                StringAssert.Contains(sharedXml, @"<transition_peak");

                // Reading them back turns them into the columnar results, which is what a document
                // read the old way now keeps, and the areas have to be the ones written.
                int sharedAreasChecked = 0;
                using (var expected = docResults.MoleculeTransitions.GetEnumerator())
                using (var actual = docShared.MoleculeTransitions.GetEnumerator())
                {
                    while (expected.MoveNext() && actual.MoveNext())
                    {
                        var expectedAreas = expected.Current.AbbreviatedResults.Areas;
                        var actualAreas = actual.Current.AbbreviatedResults.Areas;
                        CollectionAssert.AreEqual(expectedAreas.Values.ToArray(), actualAreas.Values.ToArray(),
                            string.Format(@"shared areas: expected {0} actual {1}", expectedAreas.Values.Count,
                                actualAreas.Values.Count));
                        sharedAreasChecked += expectedAreas.Values.Count;
                    }
                }

                Assert.AreNotEqual(0, sharedAreasChecked);

                CheckUserSetPeakStillWritten(docResults);

                // What this does NOT cover: opening the saved document and loading its
                // chromatograms again. Whether the peaks come back depends on UpdateResults
                // rebuilding them from the columnar results rather than picking them afresh,
                // which needs a document opened the way the application opens one.
            }
        }

        /// <summary>
        /// A peak whose boundaries the user set cannot ride on its precursor's transition areas:
        /// the boundaries have to be kept, and integrating between them is the only way it comes
        /// back. So those transitions are written out on their own after all.
        /// </summary>
        private void CheckUserSetPeakStillWritten(SrmDocument docResults)
        {
            var peptideGroup = docResults.MoleculeGroups.First();
            var nodePep = peptideGroup.Molecules.First();
            var nodeGroup = nodePep.TransitionGroups.First();
            var chromInfo = nodeGroup.EmptyResults[0].First();
            var chromatograms = docResults.Settings.MeasuredResults.Chromatograms[0];
            double width = chromInfo.EndRetentionTime.Value - chromInfo.StartRetentionTime.Value;
            var docMoved = docResults.ChangePeak(
                new IdentityPath(peptideGroup.Id, nodePep.Id, nodeGroup.Id), chromatograms.Name,
                chromatograms.MSDataFileInfos[0].FilePath, null,
                chromInfo.StartRetentionTime.Value + width / 10, chromInfo.EndRetentionTime.Value - width / 10,
                UserSet.TRUE, PeakIdentification.FALSE, false);
            Assert.AreNotSame(docResults, docMoved);

            var docRoundTrip = RoundTrip(docMoved, false, out string compactXml);
            StringAssert.Contains(compactXml, @"transition_results_columnar");

            var expectedResults = docMoved.MoleculeTransitions.First().AbbreviatedResults;
            var actualResults = docRoundTrip.MoleculeTransitions.First().AbbreviatedResults;
            CollectionAssert.AreEqual(expectedResults.Areas.Values.ToArray(), actualResults.Areas.Values.ToArray(), @"areas");
            CollectionAssert.AreEqual(expectedResults.UserSets.Values.ToArray(),
                actualResults.UserSets.Values.ToArray(), @"user sets");
            CollectionAssert.AreEqual(expectedResults.CustomPeaks?.Values.ToArray(),
                actualResults.CustomPeaks?.Values.ToArray(), @"custom peaks");
            Assert.IsNotNull(actualResults.CustomPeaks);
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

        private SrmDocument RoundTrip(SrmDocument document, bool writeChromInfos, out string xml)
        {
            var stringBuilder = new StringBuilder();
            using (var writer = new XmlTextWriter(new StringWriter(stringBuilder)))
            {
                writer.Formatting = Formatting.Indented;
                document.Serialize(writer, null, SkylineVersion.CURRENT, null, writeChromInfos);
            }

            xml = stringBuilder.ToString();
            using (var reader = new StringReader(stringBuilder.ToString()))
            {
                return (SrmDocument) new XmlSerializer(typeof(SrmDocument)).Deserialize(reader);
            }
        }
    }
}
