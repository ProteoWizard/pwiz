/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.SkylineTestUtil
{
    public static class ResultsUtil
    {
        public static SrmDocument DeserializeDocument(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open))
                {
                    return DeserializeDocument(stream);
                }
            }
            catch (Exception x)
            {
                Assert.Fail("Exception thrown: " + x);
// ReSharper disable HeuristicUnreachableCode
                throw;  // Will never happen, but is necessary to compile
// ReSharper restore HeuristicUnreachableCode
            }
        }

        public static SrmDocument DeserializeDocument(string fileName, Type classType)
        {
            try
            {
                using (var stream = classType.Assembly.GetManifestResourceStream(classType.Namespace + "." + fileName))
                {
                    return DeserializeDocument(stream);
                }
            }
            catch (Exception x)
            {
                Assert.Fail("Exception thrown: " + x);
// ReSharper disable HeuristicUnreachableCode
                throw;  // Will never happen, but is necessary to compile
// ReSharper restore HeuristicUnreachableCode
            }
        }

        public static SrmDocument DeserializeDocument(Stream stream)
        {
            Assert.IsNotNull(stream);

            XmlSerializer xmlSerializer = new XmlSerializer(typeof(SrmDocument));
            try
            {
                SrmDocument result = (SrmDocument)xmlSerializer.Deserialize(stream);
                return result;
            }
            catch (Exception x)
            {
                Assert.Fail("Exception thrown: " + x);
// ReSharper disable HeuristicUnreachableCode
                throw;  // Will never happen, but is necessary to compile
// ReSharper restore HeuristicUnreachableCode
            }
        }

        public static long CacheSize(SrmDocument docInitial, long format3Size, int groupCount, int tranCount, int peakCount)
        {
            long cacheSize = format3Size;
            int fileCachedCount = docInitial.Settings.MeasuredResults.MSDataFileInfos.Count();
            if (ChromatogramCache.FORMAT_VERSION_CACHE > ChromatogramCache.FORMAT_VERSION_CACHE_3)
            {
                // Cache version 4 stores instrument information, and is bigger in size.
                cacheSize += sizeof(int) * fileCachedCount;
            }
            if (ChromatogramCache.FORMAT_VERSION_CACHE > ChromatogramCache.FORMAT_VERSION_CACHE_4)
            {
                // Cache version 5 adds an int for flags for each file
                // Allow for a difference in sizes due to the extra information.
                int fileFlagsSize = sizeof(int) * fileCachedCount;
                // And SeqIndex, SeqCount, StartScoreIndex and padding
                var deltaSize5 = ChromGroupHeaderInfo.GetStructSize(CacheFormatVersion.Five) -
                                 ChromGroupHeaderInfo.GetStructSize(CacheFormatVersion.Four);
                int groupHeadersSize = deltaSize5 * groupCount;
                // And flags for each transition
                int transitionFlagsSize = ChromTransition5.DeltaSize5 * tranCount;
                // And num seq byte count, seq location, score types, num scores and score location
                const int headerScoreSize = sizeof(int) + sizeof(long) + sizeof(int) + sizeof(int) + sizeof(long);
                cacheSize += groupHeadersSize + fileFlagsSize + transitionFlagsSize + headerScoreSize;
            }
            if (ChromatogramCache.FORMAT_VERSION_CACHE > ChromatogramCache.FORMAT_VERSION_CACHE_5)
            {
                // Cache version 6 adds status graph dimensions for every file
                cacheSize += sizeof(float) * 2 * fileCachedCount;
            }
            if (ChromatogramCache.FORMAT_VERSION_CACHE > ChromatogramCache.FORMAT_VERSION_CACHE_6)
            {
                // Cache version 7 adds ion mobility information
                cacheSize += sizeof(float) * 2 * tranCount;
            }
            if (ChromatogramCache.FORMAT_VERSION_CACHE > ChromatogramCache.FORMAT_VERSION_CACHE_8)
            {
                // Cache version 9 adds scan id values for every file
                cacheSize += (sizeof(int) + sizeof(long)) * fileCachedCount;
                // And scan ids location to global header
                cacheSize += sizeof(long);
            }
            if (ChromatogramCache.FORMAT_VERSION_CACHE >= ChromatogramCache.FORMAT_VERSION_CACHE_11)
            {
                // Version 11 adds uncompressed buffer size for convenience, and some time span metadata
                cacheSize += ChromGroupHeaderInfo.DeltaSize11 * groupCount;
            }
            if (ChromatogramCache.FORMAT_VERSION_CACHE >= CacheFormatVersion.Twelve)
            {
                cacheSize += peakCount * (ChromPeak.GetStructSize(CacheFormatVersion.Twelve) -
                                        ChromPeak.GetStructSize(CacheFormatVersion.Eleven));
                cacheSize += tranCount *
                             (ChromTransition.GetStructSize(CacheFormatVersion.Twelve) -
                              ChromTransition.GetStructSize(CacheFormatVersion.Eleven));
            }
            cacheSize += fileCachedCount *
                         (CachedFileHeaderStruct.GetStructSize(ChromatogramCache.FORMAT_VERSION_CACHE) -
                          CachedFileHeaderStruct.GetStructSize(CacheFormatVersion.Nine));
            cacheSize += CacheHeaderStruct.GetStructSize(ChromatogramCache.FORMAT_VERSION_CACHE) -
                         CacheHeaderStruct.GetStructSize(ChromatogramCache.FORMAT_VERSION_CACHE_11);
            return cacheSize;
        }
    }

    public class DocResultsState
    {
        public DocResultsState(SrmDocument document)
        {
            AddDocument(document);
        }

        private void AddDocument(SrmDocument document)
        {
            //                var fileIndices = document.Settings.HasResults ?
            //                    document.Settings.MeasuredResults.Chromatograms.SelectMany(set => set.MSDataFileInfos).Select(
            //                    info => info.FileIndex).ToArray() : new int[0];
            //                Console.WriteLine("--->");
            foreach (PeptideDocNode nodePep in document.Peptides)
            {
                if (nodePep.HasResults)
                {
                    PeptideResults += nodePep.Results.Where(result => !result.IsEmpty)
                        .SelectMany(info => info).Count();
                }

                foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
                {
                    if (nodeGroup.HasResults)
                    {
                        //                            int startSize = TransitionGroupResults;
                        foreach (var chromInfo in nodeGroup.Results.Where(result => !result.IsEmpty)
                            .SelectMany(info => info))
                        {
                            TransitionGroupResults++;
                            if (chromInfo.Annotations.Note != null)
                                NoteCount++;
                            if (chromInfo.Annotations.ListAnnotations().Length > 0)
                                AnnotationCount++;
                        }
                        //                            if (TransitionGroupResults - startSize < fileIndices.Length)
                        //                            {
                        //                                var listIds = fileIndices.ToList();
                        //                                foreach (var chromInfo in nodeGroup.Results.Where(result => result != null)
                        //                                                                           .SelectMany(info => info))
                        //                                {
                        //                                    listIds.Remove(chromInfo.FileIndex);
                        //                                }
                        //                                Console.WriteLine("{0} ({1})", nodePep.Peptide.Sequence, String.Join(", ", listIds.Select(i => i.ToString()).ToArray()));
                        //                            }
                    }

                    foreach (var nodeTran in
                        nodeGroup.Children.Cast<TransitionDocNode>().Where(nodeTran => nodeTran.HasResults))
                    {
                        foreach (var chromInfo in nodeTran.Results.Where(result => !result.IsEmpty)
                            .SelectMany(info => info))
                        {
                            TransitionResults++;
                            if (chromInfo.Annotations.Note != null)
                                NoteCount++;
                            if (chromInfo.Annotations.ListAnnotations().Length > 0)
                                AnnotationCount++;
                            if (chromInfo.IsUserSetManual)
                                UserSetCount++;
                        }
                    }
                }
            }
        }

        public void AreEqual(SrmDocument document)
        {
            var state = new DocResultsState(document);
            Assert.AreEqual(PeptideResults, state.PeptideResults);
            Assert.AreEqual(TransitionGroupResults, state.TransitionGroupResults);
            Assert.AreEqual(TransitionResults, state.TransitionResults);
            Assert.AreEqual(UserSetCount, state.UserSetCount);
            Assert.AreEqual(NoteCount, state.NoteCount);
            Assert.AreEqual(AnnotationCount, state.AnnotationCount);
        }

        public bool HasResults
        {
            get { return PeptideResults != 0 && TransitionGroupResults != 0 && TransitionResults != 0; }
        }

        public int PeptideResults { get; private set; }
        public int TransitionGroupResults { get; private set; }
        public int TransitionResults { get; private set; }
        public int UserSetCount { get; private set; }
        public int NoteCount { get; private set; }
        public int AnnotationCount { get; private set; }
    }

    public class ResultsTestDocumentContainer : ResultsMemoryDocumentContainer
    {
        public ResultsTestDocumentContainer(SrmDocument docInitial, string pathInitial)
            : base(docInitial, pathInitial)
        {
        }

        public ResultsTestDocumentContainer(SrmDocument docInitial, string pathInitial, bool wait)
            : base(docInitial, pathInitial, wait)
        {
        }

        private const int SLEEP_INTERVAL = 10;
        public const int WAIT_TIME = 5 * 1000;    // 5 seconds

        private static int GetWaitCycles(int millis = WAIT_TIME)
        {
            return millis / SLEEP_INTERVAL;
        }

        public void WaitForProcessing(int millis = WAIT_TIME)
        {
            int waitCycles = GetWaitCycles(millis);
            for (int i = 0; i < waitCycles; i++)
            {
                if (!AnyProcessing)
                    return;
                Thread.Sleep(SLEEP_INTERVAL);
            }
            Assert.Fail("Still processing after {0} seconds", waitCycles*SLEEP_INTERVAL/1000);
        }

        public bool AnyProcessing
        {
            get { return BackgroundLoaders.Any(l => l.AnyProcessing()); }
        }

        public void AssertComplete()
        {
            if (LastProgress == null || LastProgress.IsComplete) return;
            if (LastProgress.IsError)
                Assert.Fail(LastProgress.ErrorException.ToString());

            Assert.Fail(LastProgress.IsCanceled
                            ? "Loader cancelled"
                            : "Unexpected loader progress state \"" + LastProgress.State + "\"");
        }

        public void AssertError(string expectedError)
        {
            Assert.IsTrue(LastProgress.IsError);
            Assert.IsTrue(LastProgress.ErrorException.ToString().Contains(expectedError));
        }

        public SrmDocument ChangeMeasuredResults(MeasuredResults measuredResults,
            int peptides, int tranGroups, int transitions)
        {
            return ChangeMeasuredResults(measuredResults, peptides, tranGroups, 0, transitions, 0);
        }

        public SrmDocument ChangeMeasuredResults(MeasuredResults measuredResults,
            int peptides, int tranGroups, int tranGroupsHeavy, int transitions, int transitionsHeavy)
        {
            var doc = Document;
            var docResults = doc.ChangeMeasuredResults(measuredResults);
            ResetProgress();
            Assert.IsTrue(SetDocument(docResults, doc, true));
            AssertComplete();
            docResults = Document;

            // Check the result state of the most recently added chromatogram set.
            var chroms = measuredResults.Chromatograms;
            AssertResult.IsDocumentResultsState(docResults, chroms[chroms.Count - 1].Name,
                peptides, tranGroups, tranGroupsHeavy, transitions, transitionsHeavy);

            return docResults;
        }

        public SrmDocument ChangeLibSpecs(IList<LibrarySpec> libSpecs)
        {
            var doc = Document;
            var libraries = new Library[libSpecs.Count];
            var settings = Document.Settings.ChangePeptideLibraries(l => l.ChangeLibraries(libSpecs, libraries));
            var docLibraries = doc.ChangeSettings(settings);
            ResetProgress();
            Assert.IsTrue(SetDocument(docLibraries, doc, libSpecs.Count > 0));
            AssertComplete();
            return Document;
        }

        public override void Dispose()
        {
            base.Dispose();

            var docEmpty = new SrmDocument(SrmSettingsList.GetDefault());
            Assert.IsTrue(SetDocument(docEmpty, Document));            
        }
    }

    public static class AssertResult
    {

        private static string CompareValues(int expected, int actual, string description)
        {
            return expected != actual ? string.Format("Expected {0} count {1}, got {2} instead. ", description, expected, actual) : string.Empty;
        }

        public static void IsDocumentResultsState(SrmDocument document, string replicateName,
            int peptides, int tranGroups, int tranGroupsHeavy, int transitions, int transitionsHeavy)
        {
            Assert.IsTrue(document.Settings.HasResults,"Expected document to have results.");
            int index;
            ChromatogramSet chromatogramSet;
            document.Settings.MeasuredResults.TryGetChromatogramSet(replicateName, out chromatogramSet, out index);
            Assert.AreNotEqual(-1, index, string.Format("Replicate {0} not found among -> {1} <-", replicateName,
                TextUtil.LineSeparate(document.Settings.MeasuredResults.Chromatograms.Select(c => c.Name))));
            int peptidesActual = 0;

            foreach (var nodePep in document.Molecules.Where(nodePep => (nodePep.Results != null && !nodePep.Results[index].IsEmpty)))
            {
                peptidesActual += nodePep.Results[index].Sum(chromInfo => chromInfo.PeakCountRatio >= 0.5 ? 1 : 0);
            }
            int transitionsActual = 0;
            int transitionsHeavyActual = 0;
            int tranGroupsActual = 0;
            int tranGroupsHeavyActual = 0;
            foreach (var nodeGroup in document.MoleculeTransitionGroups.Where(nodeGroup => ( nodeGroup.Results != null && !nodeGroup.Results[index].IsEmpty)))
            {
                foreach (var chromInfo in nodeGroup.Results[index])
                {
                    if (chromInfo.PeakCountRatio < 0.5)
                        continue;

                    if (nodeGroup.TransitionGroup.LabelType.IsLight)
                        tranGroupsActual++;
                    else
                        tranGroupsHeavyActual++;
                }
                foreach (var nodeTran in nodeGroup.Children.Cast<TransitionDocNode>().Where(
                            nodeTran => (nodeTran.Results != null && !nodeTran.Results[index].IsEmpty)))
                {
                    foreach (var chromInfo in nodeTran.Results[index])
                    {
                        if (!chromInfo.IsGoodPeak(document.Settings.TransitionSettings.Integration.IsIntegrateAll))
                            continue;

                        if (nodeGroup.TransitionGroup.LabelType.IsLight)
                            transitionsActual++;
                        else
                            transitionsHeavyActual++;
                    }
                }
            }
            var failMessage = CompareValues(peptides, peptidesActual, "peptide");
            failMessage += CompareValues(tranGroups, tranGroupsActual, "transition group");
            failMessage += CompareValues(tranGroupsHeavy, tranGroupsHeavyActual,"heavy transition group");
            failMessage += CompareValues(transitions, transitionsActual, "transition");
            failMessage += CompareValues(transitionsHeavy, transitionsHeavyActual, "heavy transition");
            if (failMessage.Length > 0)
                Assert.Fail("IsDocumentResultsState failed for replicate " + replicateName + ": "+failMessage);
        }

        public static void MatchChromatograms(ResultsTestDocumentContainer docContainer,
            string path1, string path2, int delta, int missing, LockMassParameters lockMassParameters = null)
        {
            MatchChromatograms(docContainer, MsDataFileUri.Parse(path1), MsDataFileUri.Parse(path2), delta, missing, lockMassParameters);
        }

        public static void MatchChromatograms(ResultsTestDocumentContainer docContainer,
            MsDataFileUri path1, MsDataFileUri path2, int delta, int missing,
            LockMassParameters lockMassParameters = null)
        {
            var doc = docContainer.Document;
            var listChromatograms = new List<ChromatogramSet>();
            foreach (var path in new[] { path1, path2 })
            {
                var setAdd = FindChromatogramSet(doc, path);
                if (setAdd == null)
                {
                    string addName = (path.GetFileName() ?? "").Replace('.', '_');
                    addName = Helpers.GetUniqueName(addName, n => listChromatograms.All(set => n != set.Name));
                    setAdd = new ChromatogramSet(addName, new[] {path});
                }
                listChromatograms.Add(setAdd);
            }
            var docResults = doc.ChangeMeasuredResults(new MeasuredResults(listChromatograms));
            Assert.IsTrue(docContainer.SetDocument(docResults, doc, true));
            docContainer.AssertComplete();
            docResults = docContainer.Document;
            MatchChromatograms(docResults, 0, 1, delta, missing);
        }

        public static ChromatogramSet FindChromatogramSet(SrmDocument document, MsDataFileUri path)
        {
            if (document.Settings.HasResults)
            {
                foreach (var chromSet in document.Settings.MeasuredResults.Chromatograms)
                {
                    if (chromSet.MSDataFilePaths.Contains(path))
                        return chromSet;
                }
            }
            return null;
        }

        public static void MatchChromatograms(SrmDocument document, int iChrom1, int iChrom2, int delta, int missing)
        {
            float tolerance = (float)document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            var results = document.Settings.MeasuredResults;
            int missingPeaks = 0;
            foreach (var pair in document.MoleculePrecursorPairs)
            {
                ChromatogramGroupInfo[] chromGroupInfo1;
                Assert.IsTrue(results.TryLoadChromatogram(iChrom1, pair.NodePep, pair.NodeGroup,
                    tolerance, true, out chromGroupInfo1));
                Assert.AreEqual(1, chromGroupInfo1.Length);
                ChromatogramGroupInfo[] chromGroupInfo2;
                Assert.IsTrue(results.TryLoadChromatogram(iChrom2, pair.NodePep, pair.NodeGroup,
                    tolerance, true, out chromGroupInfo2));
                Assert.AreEqual(1, chromGroupInfo2.Length);
                if (delta != -1)
                {
                    if (chromGroupInfo1[0].NumPeaks != chromGroupInfo2[0].NumPeaks)
                        Assert.AreEqual(chromGroupInfo1[0].NumPeaks, chromGroupInfo2[0].NumPeaks, delta);
                    if (chromGroupInfo1[0].NumPeaks == chromGroupInfo2[0].NumPeaks)
                    {
                        if (chromGroupInfo1[0].MaxPeakIndex != chromGroupInfo2[0].MaxPeakIndex)
                            Assert.AreEqual(MaxPeakTime(chromGroupInfo1[0]), MaxPeakTime(chromGroupInfo2[0]), 0.1);
                    }
                }
                else
                {
                    Assert.IsTrue(chromGroupInfo1[0].NumPeaks >= 1);
                    Assert.IsTrue(chromGroupInfo2[0].NumPeaks >= 1);
                }
                if (chromGroupInfo1[0].MaxPeakIndex < 0 || chromGroupInfo2[0].MaxPeakIndex < 0)
                    missingPeaks++;
            }
            Assert.AreEqual(missing, missingPeaks);
        }

        private static double MaxPeakTime(ChromatogramGroupInfo chromGroupInfo)
        {
            double maxIntensity = 0;
            double maxTime = 0;
            int iBest = chromGroupInfo.BestPeakIndex;
            foreach (var chromInfo in chromGroupInfo.TransitionPointSets)
            {
                var peak = chromInfo.GetPeak(iBest);
                if (!peak.IsForcedIntegration && peak.Height > maxIntensity)
                {
                    maxIntensity = peak.Height;
                    maxTime = peak.RetentionTime;
                }
            }
            return maxTime;
        }        
    }
}
