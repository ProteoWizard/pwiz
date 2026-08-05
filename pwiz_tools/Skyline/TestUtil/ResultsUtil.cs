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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// One transition's results: the precursor which holds them and the index which addresses
    /// them. Everything about them is asked of <see cref="TransitionGroupResults"/> with the
    /// index, since the results object itself is the precursor's own business.
    /// </summary>
    public struct TransitionResultsRef
    {
        public TransitionResultsRef(TransitionGroupResults results, int transitionIndex)
        {
            Results = results;
            TransitionIndex = transitionIndex;
        }

        public TransitionGroupResults Results { get; }
        public int TransitionIndex { get; }

        public bool HasResults
        {
            get { return Results?.HasTransitionResults(TransitionIndex) ?? false; }
        }

        public ChromFileIds ChromFileIds
        {
            get { return Results?.GetTransitionChromFileIds(TransitionIndex); }
        }

        /// <summary>
        /// Every file the transition has a peak for, with the replicate it belongs to, in position
        /// order. Everything about a peak is found by those two, so this is what walking a
        /// transition's results comes to: none of the values is stored per position, and two
        /// documents' positions are the only thing they have in common.
        /// </summary>
        public IEnumerable<KeyValuePair<int, ChromFileInfoId>> Files
        {
            get
            {
                var chromFileIds = ChromFileIds;
                if (chromFileIds == null)
                {
                    yield break;
                }

                for (int replicateIndex = 0;
                     replicateIndex < chromFileIds.ReplicatePositions.ReplicateCount;
                     replicateIndex++)
                {
                    foreach (var fileId in chromFileIds.GetFileIds(replicateIndex))
                    {
                        yield return new KeyValuePair<int, ChromFileInfoId>(replicateIndex, fileId);
                    }
                }
            }
        }

        public int PositionCount
        {
            get { return ChromFileIds?.FileIds.Count ?? 0; }
        }

        public TransitionPeak GetPeak(int replicateIndex, ChromFileInfoId fileId)
        {
            Assert.IsTrue(Results.TryGetTransitionPeak(TransitionIndex, replicateIndex, fileId, out var peak));
            return peak;
        }

        // A struct, so a lambda cannot reach "this": each of these copies it to a local first.
        public IEnumerable<TransitionPeak> Peaks
        {
            get
            {
                var self = this;
                return Files.Select(file => self.GetPeak(file.Key, file.Value));
            }
        }

        public IEnumerable<float> Areas
        {
            get { return Peaks.Select(peak => peak.Area); }
        }

        public IEnumerable<UserSet> UserSets
        {
            get { return Peaks.Select(peak => peak.UserSet); }
        }

        public IEnumerable<Annotations> AnnotationsList
        {
            get
            {
                var self = this;
                return Files.Select(file =>
                    self.Results.FindTransitionAnnotations(self.TransitionIndex, file.Key, file.Value));
            }
        }

        /// <summary>
        /// One entry per file, null where the transition's peak was integrated between the same
        /// boundaries as the rest of the precursor's - which is nearly every peak.
        /// </summary>
        public IEnumerable<CustomPeakBounds?> CustomPeakBounds
        {
            get
            {
                var self = this;
                return Files.Select(file =>
                    self.Results.FindTransitionCustomPeakBounds(self.TransitionIndex, file.Key, file.Value));
            }
        }

        /// <summary>
        /// One entry per file, null where the peak is one of the candidate peaks in the .skyd and
        /// so has nothing to keep for itself.
        /// </summary>
        public IEnumerable<CustomPeakMetrics> CustomPeakMetrics
        {
            get
            {
                var self = this;
                return Files.Select(file =>
                    self.Results.FindTransitionCustomPeakMetrics(self.TransitionIndex, file.Key, file.Value));
            }
        }
    }

    public static class ResultsUtil
    {
        /// <summary>
        /// Every transition's results, in document order, each as the precursor which owns them
        /// and the index which addresses them. A transition's results belong to its precursor now,
        /// and nothing hands the results object itself out, so this is what a test which used to
        /// walk MoleculeTransitions and ask each node for them does instead.
        /// </summary>
        public static IEnumerable<TransitionResultsRef> EnumerateTransitionResults(SrmDocument document)
        {
            foreach (var nodeGroup in document.MoleculeTransitionGroups)
            {
                for (int iTran = 0; iTran < nodeGroup.Children.Count; iTran++)
                {
                    yield return new TransitionResultsRef(nodeGroup.AbbreviatedResults, iTran);
                }
            }
        }

        public static SrmDocument DeserializeDocument(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open))
                {
                    // Wrap stream in XmlReader so that BaseUri is known
                    var xmlReader = XmlReader.Create(stream,
                        new XmlReaderSettings() { IgnoreWhitespace = true },
                        path);
                    return DeserializeDocument(xmlReader);
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
                    Assert.IsNotNull(stream);
                    // Wrap stream in XmlReader so that BaseUri is known
                    var xmlReader = XmlReader.Create(stream,
                        new XmlReaderSettings() { IgnoreWhitespace = true },
                        fileName);
                    return DeserializeDocument(xmlReader);
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

        public static SrmDocument DeserializeDocument(XmlReader reader)
        {
            Assert.IsNotNull(reader);

            XmlSerializer xmlSerializer = new XmlSerializer(typeof(SrmDocument));
            try
            {
                SrmDocument result = (SrmDocument)xmlSerializer.Deserialize(reader);
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
            cacheSize += CacheHeaderStruct.GetStructSize(CacheFormatVersion.CURRENT) -
                         CacheHeaderStruct.GetStructSize(CacheFormatVersion.Three);
            int fileCachedCount = docInitial.Settings.MeasuredResults.MSDataFileInfos.Count();
            cacheSize += fileCachedCount *
                         (CachedFileHeaderStruct.GetStructSize(CacheFormatVersion.CURRENT) -
                          CachedFileHeaderStruct.GetStructSize(CacheFormatVersion.Three));

            cacheSize += groupCount * (ChromGroupHeaderInfo.GetStructSize(CacheFormatVersion.CURRENT) -
                                       ChromGroupHeaderInfo.GetStructSize(CacheFormatVersion.Three));

            cacheSize += tranCount * (ChromTransition.GetStructSize(CacheFormatVersion.CURRENT) -
                                      ChromTransition.GetStructSize(CacheFormatVersion.Three));

            cacheSize += peakCount * (ChromPeak.GetStructSize(CacheFormatVersion.CURRENT) -
                                      ChromPeak.GetStructSize(CacheFormatVersion.Three));
            return cacheSize;
        }

        /// <summary>
        /// Set all of ImportTime values in all of the ChromFileInfos to null.
        /// </summary>
        public static SrmDocument ClearFileImportTimes(SrmDocument document)
        {
            var newMeasuredResults = document.MeasuredResults?.ClearImportTimes();
            if (Equals(newMeasuredResults, document.MeasuredResults))
            {
                return document;
            }
            return document.ChangeSettingsNoDiff(document.Settings.ChangeMeasuredResults(newMeasuredResults));
        }

        /// <summary>
        /// Set all of FileWriteTime values in all of the ChromFileInfos to null.
        /// </summary>
        public static SrmDocument ClearFileWriteTimes(SrmDocument document)
        {
            var newMeasuredResults = document.MeasuredResults?.ClearFileWriteTimes();
            if (Equals(newMeasuredResults, document.MeasuredResults))
            {
                return document;
            }
            return document.ChangeSettingsNoDiff(document.Settings.ChangeMeasuredResults(newMeasuredResults));
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
                // The files a molecule has results for, from the precursors' columnar results: the
                // molecule level chrom infos are not stored any more.
                PeptideResults += Enumerable
                    .Range(0, document.Settings.MeasuredResults?.Chromatograms.Count ?? 0)
                    .Sum(replicateIndex => nodePep.GetResultFileIds(replicateIndex).Count());

                foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
                {
                    // A peak per position of the columnar results, and its annotations from the
                    // precursor's annotation map.
                    var groupResults = nodeGroup.AbbreviatedResults;
                    if (groupResults != null)
                    {
                        TransitionGroupResults += groupResults.ChromFileIds.ReplicatePositions.TotalCount;
                        foreach (var position in Enumerable.Range(0,
                                     groupResults.ChromFileIds.ReplicatePositions.TotalCount))
                        {
                            var annotations = groupResults.GetAnnotations(position);
                            if (annotations.Note != null)
                                NoteCount++;
                            if (annotations.ListAnnotations().Length > 0)
                                AnnotationCount++;
                        }
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
            document.Settings.MeasuredResults.TryGetChromatogramSet(replicateName, out _, out index);
            Assert.AreNotEqual(-1, index, string.Format("Replicate {0} not found among -> {1} <-", replicateName,
                TextUtil.LineSeparate(document.Settings.MeasuredResults.Chromatograms.Select(c => c.Name))));
            int peptidesActual = 0;
            int transitionsActual = 0;
            int transitionsHeavyActual = 0;
            int tranGroupsActual = 0;
            int tranGroupsHeavyActual = 0;

            // The chrom infos are not stored any more, so they are rebuilt from the .skyd. That is
            // what keeps the counts these are asserted against: MoleculeResults gives one per file
            // and per optimization step, the same as the doc nodes used to hold, while the columnar
            // results keep only step zero and would count an optimized peak once instead of once
            // per step.
            bool integrateAll = document.Settings.TransitionSettings.Integration.IsIntegrateAll;
            foreach (var nodePep in document.Molecules)
            {
                if (nodePep.GetPeakCountRatio(index, integrateAll) >= 0.5)
                    peptidesActual++;

                // One read for the whole molecule, which is what makes asking about every
                // precursor and transition of it affordable.
                var moleculeResults = new MoleculeResults(document.Settings, nodePep);
                foreach (var nodeGroup in nodePep.TransitionGroups)
                {
                    bool isLight = nodeGroup.TransitionGroup.LabelType.IsLight;
                    foreach (var chromInfo in moleculeResults.GetTransitionGroupChromInfos(
                                 nodeGroup.TransitionGroup, index))
                    {
                        if (chromInfo.PeakCountRatio < 0.5)
                            continue;

                        if (isLight)
                            tranGroupsActual++;
                        else
                            tranGroupsHeavyActual++;
                    }

                    foreach (var nodeTran in nodeGroup.Transitions)
                    {
                        foreach (var chromInfo in moleculeResults.GetTransitionChromInfos(
                                     nodeGroup.TransitionGroup, nodeTran.Transition, index))
                        {
                            if (!chromInfo.IsGoodPeak(integrateAll))
                                continue;

                            if (isLight)
                                transitionsActual++;
                            else
                                transitionsHeavyActual++;
                        }
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
                    tolerance, out chromGroupInfo1));
                Assert.AreEqual(1, chromGroupInfo1.Length);
                ChromatogramGroupInfo[] chromGroupInfo2;
                Assert.IsTrue(results.TryLoadChromatogram(iChrom2, pair.NodePep, pair.NodeGroup,
                    tolerance, out chromGroupInfo2));
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

        // Debug aid for comparing chromatogram results before and after re-extraction
        public static void CompareResultsText(SrmDocument doc, string skyFileText, string comparisonFileName, string expectedValuesFileName, double? maxExpectedHeight)
        {
            GetResultsTextForComparison(doc, skyFileText, comparisonFileName, out var maxHeight);
            var rtErrors = new List<string>();

            using var actual = File.ReadLines(comparisonFileName).GetEnumerator();
            using var expected = File.ReadLines(expectedValuesFileName).GetEnumerator();

            while (true)
            {
                var hasActual = actual.MoveNext();
                var hasExpected = expected.MoveNext();
                if (!hasActual && !hasExpected)
                {
                    break; // both ended
                }
                if (!hasActual)
                {
                    rtErrors.Add($@"Expected:{Environment.NewLine}{expected.Current}{Environment.NewLine}got:{Environment.NewLine}nothing");
                }
                else if (!hasExpected)
                {
                    rtErrors.Add($@"Expected:{Environment.NewLine}nothing{Environment.NewLine}got:{Environment.NewLine}{actual.Current}");

                }
                else if (!string.Equals(expected.Current, actual.Current))
                {
                    rtErrors.Add($@"Expected:{Environment.NewLine}{expected.Current}{Environment.NewLine}got:{Environment.NewLine}{actual.Current}");
                }
            }
            Assert.AreEqual(0, rtErrors.Count, string.Join(Environment.NewLine, rtErrors));
            if (maxExpectedHeight.HasValue)
            {
                AssertEx.AreEqual(maxExpectedHeight, maxHeight, 1, @"max height");
            }
        }

        public static void GetResultsTextForComparison(SrmDocument doc, string skyFileContents,
            string comparisonFileName, out double maxObservedHeight)
        {
            var tolerance = (float)doc.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            var results = doc.Settings.MeasuredResults;
            maxObservedHeight = 0.0;

            using var streamWriter = new StreamWriter(comparisonFileName);

            var fileNames = doc.MeasuredResults.MSDataFilePaths.ToArray();
            for (var index = 0; index < fileNames.Length; index++)
            {
                var fileName = fileNames[index].GetFileName();
                if (!skyFileContents.Contains(fileName))
                {
                    continue; // Replicate was removed for debug purposes
                }
                streamWriter.WriteLine($@"f{index}: {fileName}");
                foreach (var chrom in doc.MeasuredResults.Chromatograms)
                {
                    foreach (var pair in doc.PeptidePrecursorPairs)
                    {
                        if (!results.TryLoadChromatogram(chrom, pair.NodePep, pair.NodeGroup,
                                tolerance, out var chromGroupInfo) ||
                            !(chrom.MSDataFilePaths.Any(p=>Equals(fileName, p.GetFileName()))))
                        {
                            var observation =
                                $@"f{index} {pair.NodePep.RawTextIdDisplay} no chromatogram ";
                            streamWriter.WriteLine(observation);
                            continue;
                        }

                        foreach (var chromGroup in chromGroupInfo.Where(cg => Equals(cg.FilePath.GetFileName(), fileName)))
                        {
                            foreach (var tranInfo in chromGroup.TransitionPointSets)
                            {
                                maxObservedHeight = Math.Max(maxObservedHeight, tranInfo.MaxIntensity);
                                foreach (var peak in tranInfo.Peaks)
                                {
                                    var observation =
                                        $@"f{index} {chromGroup.ChromatogramGroupId} tran {tranInfo.PrecursorMz:F4}/{tranInfo.ProductMz:F4} peak {peak}";
                                    streamWriter.WriteLine(observation);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
