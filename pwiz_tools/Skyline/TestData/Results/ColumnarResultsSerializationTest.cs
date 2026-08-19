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
using System.Text.RegularExpressions;
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
                StringAssert.Contains(compactXml, @"chosen_peak_index=");
                Assert.IsFalse(compactXml.Contains(@"<transition_results"));
                int transitionsChecked = 0;
                int precursorsChecked = 0;
                int chosenPeakIndexesChecked = 0;
                using (var expected = ResultsUtil.EnumerateTransitionResults(docResults).GetEnumerator())
                using (var actual = ResultsUtil.EnumerateTransitionResults(docRoundTrip).GetEnumerator())
                {
                    while (expected.MoveNext() && actual.MoveNext())
                    {
                        var expectedResults = expected.Current;
                        var actualResults = actual.Current;
                        Assert.AreEqual(expectedResults.ChromFileIds.ReplicatePositions,
                            actualResults.ChromFileIds.ReplicatePositions,
                            string.Format(@"replicate positions: expected {0} replicates {1} total, actual {2} replicates {3} total",
                                expectedResults.ChromFileIds.ReplicatePositions.ReplicateCount,
                                expectedResults.ChromFileIds.ReplicatePositions.TotalCount,
                                actualResults.ChromFileIds.ReplicatePositions.ReplicateCount,
                                actualResults.ChromFileIds.ReplicatePositions.TotalCount));
                        Assert.AreEqual(expectedResults.ChromFileIds.FileIds.Count,
                            actualResults.ChromFileIds.FileIds.Count, @"file count");
                        // Only the area and the user set: the rest of a TransitionPeak - truncated,
                        // empty, identified, forced integration - is deliberately not written,
                        // because it is worked out again from the .skyd when that is read.
                        CollectionAssert.AreEqual(expectedResults.Areas.ToArray(),
                            actualResults.Areas.ToArray(), @"areas");
                        CollectionAssert.AreEqual(expectedResults.UserSets.ToArray(),
                            actualResults.UserSets.ToArray(), @"user sets");
                        AssertSameCustomPeaks(expectedResults, actualResults);
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
                        // One struct per peak now, so this compares the times and the chosen index
                        // together rather than as four lists.
                        CollectionAssert.AreEqual(expectedResults.Peaks.FlatValues.ToArray(),
                            actualResults.Peaks.FlatValues.ToArray(), @"peaks");
                        CollectionAssert.AreEqual(expectedResults.QValues?.FlatValues.ToArray(),
                            actualResults.QValues?.FlatValues.ToArray(), @"q values");
                        CollectionAssert.AreEqual(expectedResults.ZScores?.FlatValues.ToArray(),
                            actualResults.ZScores?.FlatValues.ToArray(), @"z scores");
                        CollectionAssert.AreEqual(expectedResults.UserSets?.FlatValues.ToArray(),
                            actualResults.UserSets?.FlatValues.ToArray(), @"user sets");
                        CollectionAssert.AreEqual(expectedResults.Annotations?.FlatValues.ToArray(),
                            actualResults.Annotations?.FlatValues.ToArray(), @"annotations");
                        AssertSameFiles(docResults, expectedResults.ChromFileIds, docRoundTrip,
                            actualResults.ChromFileIds);
                        for (int position = 0; position < expectedResults.Peaks.FlatValues.Count; position++)
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
                    Assert.IsTrue(nodeTran.EmptyResults == null ||
                                  nodeTran.EmptyResults.All(chromInfoList => chromInfoList.IsEmpty));
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
                using (var expected = ResultsUtil.EnumerateTransitionResults(docResults).GetEnumerator())
                using (var actual = ResultsUtil.EnumerateTransitionResults(docShared).GetEnumerator())
                {
                    while (expected.MoveNext() && actual.MoveNext())
                    {
                        var expectedAreas = expected.Current.Areas.ToArray();
                        var actualAreas = actual.Current.Areas.ToArray();
                        CollectionAssert.AreEqual(expectedAreas, actualAreas,
                            string.Format(@"shared areas: expected {0} actual {1}", expectedAreas.Length,
                                actualAreas.Length));
                        sharedAreasChecked += expectedAreas.Length;
                    }
                }

                Assert.AreNotEqual(0, sharedAreasChecked);

                CheckUserSetPeakStillWritten(docResults);
                CheckSharedTransitionPeakFlags(docResults);

                // What this does NOT cover: opening the saved document and loading its
                // chromatograms again. Whether the peaks come back depends on UpdateResults
                // rebuilding them from the columnar results rather than picking them afresh,
                // which needs a document opened the way the application opens one.
            }
        }

        /// <summary>
        /// The two flags a peak has beyond its area are carried by its precursor, so that the
        /// transitions which agree with what most of them say are not written at all. Every peak of
        /// the first precursor is made truncated and forced except one, which is left disagreeing so
        /// that the majority is what gets carried and the odd one out still writes itself.
        /// </summary>
        private void CheckSharedTransitionPeakFlags(SrmDocument docResults)
        {
            var peptideGroup = docResults.MoleculeGroups.First();
            var nodePep = peptideGroup.Molecules.First();
            var nodeGroup = nodePep.TransitionGroups.First();
            var transitions = nodeGroup.Transitions.Select(nodeTran => nodeTran.Transition).ToArray();
            Assert.IsTrue(transitions.Length > 1, @"the first precursor has nothing to disagree with");

            var results = nodeGroup.AbbreviatedResults;
            int peaksFlagged = 0;
            // All but the last, so that truncated and forced is what most of them say.
            for (int iTran = 0; iTran < transitions.Length - 1; iTran++)
            {
                var peaks = results.GetAllTransitionPeaks(transitions[iTran]).ToArray();
                peaksFlagged += peaks.Length;
                results = results.ChangeTransitionResults(transitions[iTran],
                    results.GetTransitionChromFileIds(transitions[iTran]),
                    peaks.Select(peak => new TransitionPeak(peak.Area, peak.UserSet, true, peak.IsEmpty,
                        peak.Identified, true)), null, null, null);
            }

            Assert.AreNotEqual(0, peaksFlagged, @"no peak to flag");
            var docFlagged = (SrmDocument) docResults.ReplaceChild(new IdentityPath(peptideGroup.Id, nodePep.Id),
                nodeGroup.ChangeAbbreviatedResults(results));

            var docRoundTrip = RoundTrip(docFlagged, false, out string compactXml);
            // What the majority said, carried once by the precursor instead of by every one of them.
            StringAssert.Contains(compactXml, @"truncated=""true"" forced_integration=""true""");
            // The one which disagrees is still written, and only it.
            StringAssert.Contains(compactXml, @"<transition_results");
            Assert.AreEqual(peaksFlagged / (transitions.Length - 1),
                Regex.Matches(compactXml, @"<transition_peak").Count,
                @"a transition which agreed with its precursor was written anyway");

            var expectedGroup = docFlagged.MoleculeTransitionGroups.First().AbbreviatedResults;
            var actualNodeGroup = docRoundTrip.MoleculeTransitionGroups.First();
            var actualGroup = actualNodeGroup.AbbreviatedResults;
            // Each document's own transitions: the round trip made new identity objects, and results
            // are looked up by the identity the precursor holds.
            var actualTransitions = actualNodeGroup.Transitions.Select(nodeTran => nodeTran.Transition).ToArray();
            Assert.AreEqual(transitions.Length, actualTransitions.Length, @"transition count");
            for (int iTran = 0; iTran < transitions.Length; iTran++)
            {
                var expectedPeaks = expectedGroup.GetAllTransitionPeaks(transitions[iTran]).ToArray();
                var actualPeaks = actualGroup.GetAllTransitionPeaks(actualTransitions[iTran]).ToArray();
                CollectionAssert.AreEqual(expectedPeaks, actualPeaks,
                    string.Format(@"peaks of transition {0} of {1} ({2}): expected {3}, actual {4}",
                        iTran, transitions.Length, transitions[iTran], expectedPeaks.Length, actualPeaks.Length));
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
            var chromatograms = docResults.Settings.MeasuredResults.Chromatograms[0];
            var fileInfo = chromatograms.MSDataFileInfos[0];
            // Where the peak is now, from the columnar results. EmptyResults holds nothing: a
            // precursor does not keep its chrom infos any more.
            var peakBounds = nodeGroup.AbbreviatedResults.FindPrecursorPeakBounds(0, fileInfo.FileId);
            Assert.IsTrue(peakBounds.HasValue, @"the first precursor has no peak to move");
            double width = peakBounds.Value.EndTime - peakBounds.Value.StartTime;
            var docMoved = docResults.ChangePeak(
                new IdentityPath(peptideGroup.Id, nodePep.Id, nodeGroup.Id), chromatograms.Name,
                fileInfo.FilePath, null,
                peakBounds.Value.StartTime + width / 10, peakBounds.Value.EndTime - width / 10,
                UserSet.TRUE, PeakIdentification.FALSE, false);
            Assert.AreNotSame(docResults, docMoved);
            var movedBounds = docMoved.MoleculeTransitionGroups.First().AbbreviatedResults
                .FindPrecursorPeakBounds(0, fileInfo.FileId);
            Assert.AreNotEqual(peakBounds, movedBounds,
                string.Format(@"the peak did not move: still {0}-{1}", peakBounds.Value.StartTime,
                    peakBounds.Value.EndTime));

            var docRoundTrip = RoundTrip(docMoved, false, out string compactXml);
            StringAssert.Contains(compactXml, @"<transition_results");

            var expectedResults = ResultsUtil.EnumerateTransitionResults(docMoved).First();
            var actualResults = ResultsUtil.EnumerateTransitionResults(docRoundTrip).First();
            // The area and the user set are what the format carries. See above.
            CollectionAssert.AreEqual(expectedResults.Areas.ToArray(), actualResults.Areas.ToArray(), @"areas");
            CollectionAssert.AreEqual(expectedResults.UserSets.ToArray(), actualResults.UserSets.ToArray(),
                @"user sets");
            AssertSameCustomPeaks(expectedResults, actualResults);

            // The whole peak group moved together, so no transition kept boundaries of its own:
            // the ones to integrate between are the precursor's, and those have to come back.
            var expectedGroup = docMoved.MoleculeTransitionGroups.First().AbbreviatedResults;
            var actualGroup = docRoundTrip.MoleculeTransitionGroups.First().AbbreviatedResults;
            CollectionAssert.AreEqual(expectedGroup.Peaks.FlatValues.ToArray(),
                actualGroup.Peaks.FlatValues.ToArray(), @"moved precursor peaks");
            Assert.IsTrue(actualResults.CustomPeakBounds.All(peakBounds => !peakBounds.HasValue),
                @"a transition kept the precursor's own boundaries");
        }

        /// <summary>
        /// The three sparse values of a transition's peaks, each its own map: the annotations, the
        /// boundaries a transition kept because they are not the precursor's, and what integrating
        /// between them again could not find.
        /// </summary>
        private static void AssertSameCustomPeaks(TransitionResultsRef expectedResults,
            TransitionResultsRef actualResults)
        {
            CollectionAssert.AreEqual(expectedResults.AnnotationsList.ToArray(),
                actualResults.AnnotationsList.ToArray(), @"annotations");
            CollectionAssert.AreEqual(expectedResults.CustomPeakBounds.ToArray(),
                actualResults.CustomPeakBounds.ToArray(), @"custom peak bounds");
            CollectionAssert.AreEqual(expectedResults.CustomPeakMetrics.ToArray(),
                actualResults.CustomPeakMetrics.ToArray(), @"custom peak metrics");
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
