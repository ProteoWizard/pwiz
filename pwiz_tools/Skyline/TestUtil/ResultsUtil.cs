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
using pwiz.Common.Collections;
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
    /// One transition's results: the precursor which holds them and the <see cref="Transition"/>
    /// which addresses them. Everything about them is asked of <see cref="TransitionGroupResults"/>
    /// with that transition, since the results object itself is the precursor's own business.
    /// </summary>
    public struct TransitionResultsRef
    {
        public TransitionResultsRef(TransitionGroupResults results, Transition transition)
        {
            Results = results;
            Transition = transition;
        }

        public TransitionGroupResults Results { get; }
        public Transition Transition { get; }

        public bool HasResults
        {
            get { return Results?.HasTransitionResults(Transition) ?? false; }
        }

        public ChromFileIds ChromFileIds
        {
            get { return Results?.GetTransitionChromFileIds(Transition); }
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
            Assert.IsTrue(Results.TryGetTransitionPeak(Transition, replicateIndex, fileId, out var peak));
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

        /// <summary>
        /// The mean area of every peak the transition has, or null when it has none. What
        /// TransitionDocNode.AveragePeakArea used to answer.
        /// </summary>
        public float? AverageArea
        {
            get
            {
                var areas = Areas.ToArray();
                return areas.Length == 0 ? (float?) null : areas.Average();
            }
        }

        /// <summary>
        /// Whether the transition's peak in one replicate is a good one, which is what a peak count
        /// ratio of 1 used to say about a single transition. The first file of the replicate, which
        /// is the one the chrom info accessors this replaced answered about.
        /// </summary>
        public bool HasGoodPeak(int replicateIndex, bool integrateAll)
        {
            var chromFileIds = ChromFileIds;
            if (chromFileIds == null)
            {
                return false;
            }

            foreach (var fileId in chromFileIds.GetFileIds(replicateIndex))
            {
                return GetPeak(replicateIndex, fileId).IsGoodPeak(integrateAll);
            }

            return false;
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
                    self.Results.FindTransitionAnnotations(self.Transition, file.Key, file.Value));
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
                    self.Results.FindTransitionCustomPeakBounds(self.Transition, file.Key, file.Value));
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
                    self.Results.FindTransitionCustomPeakMetrics(self.Transition, file.Key, file.Value));
            }
        }
    }

    /// <summary>
    /// One transition, the precursor and molecule it belongs to, and its chrom infos as
    /// <see cref="MoleculeResults"/> rebuilt them. See
    /// <see cref="ResultsUtil.EnumerateTransitionChromInfos"/>.
    /// </summary>
    public struct TransitionChromInfosRef
    {
        public TransitionChromInfosRef(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup,
            TransitionDocNode nodeTran, Results<TransitionChromInfo> chromInfos)
        {
            NodePep = nodePep;
            NodeGroup = nodeGroup;
            NodeTran = nodeTran;
            ChromInfos = chromInfos;
        }

        public PeptideDocNode NodePep { get; }
        public TransitionGroupDocNode NodeGroup { get; }
        public TransitionDocNode NodeTran { get; }

        /// <summary>
        /// Null when the transition has no results at all, the same as the property a transition
        /// used to hold.
        /// </summary>
        public Results<TransitionChromInfo> ChromInfos { get; }

        public ChromInfoList<TransitionChromInfo> this[int replicateIndex]
        {
            get { return ChromInfos[replicateIndex]; }
        }
    }

    /// <summary>
    /// One precursor, the molecule it belongs to, and its chrom infos as
    /// <see cref="MoleculeResults"/> rebuilt them. See
    /// <see cref="ResultsUtil.EnumerateTransitionGroupChromInfos"/>.
    /// </summary>
    public struct TransitionGroupChromInfosRef
    {
        public TransitionGroupChromInfosRef(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup,
            Results<TransitionGroupChromInfo> chromInfos)
        {
            NodePep = nodePep;
            NodeGroup = nodeGroup;
            ChromInfos = chromInfos;
        }

        public PeptideDocNode NodePep { get; }
        public TransitionGroupDocNode NodeGroup { get; }

        /// <summary>
        /// Null when the precursor has no results at all, the same as the property a precursor
        /// used to hold.
        /// </summary>
        public Results<TransitionGroupChromInfo> ChromInfos { get; }

        public ChromInfoList<TransitionGroupChromInfo> this[int replicateIndex]
        {
            get { return ChromInfos[replicateIndex]; }
        }
    }

    public static class ResultsUtil
    {
        /// <summary>
        /// Every transition's results, in document order, each as the precursor which owns them
        /// and the transition which addresses them. A transition's results belong to its precursor
        /// now, and nothing hands the results object itself out, so this is what a test which used
        /// to walk MoleculeTransitions and ask each node for them does instead.
        /// </summary>
        public static IEnumerable<TransitionResultsRef> EnumerateTransitionResults(SrmDocument document)
        {
            foreach (var nodeGroup in document.MoleculeTransitionGroups)
            {
                foreach (TransitionDocNode nodeTran in nodeGroup.Children)
                {
                    yield return new TransitionResultsRef(nodeGroup.AbbreviatedResults, nodeTran.Transition);
                }
            }
        }

        /// <summary>
        /// Every transition of a document with its chrom infos rebuilt from the .skyd, in document
        /// order. This is what a test which used to walk MoleculeTransitions and index each node's
        /// Results does instead, when it needs values the columnar results do not keep - the ranks,
        /// the optimization steps, whether a peak was forced.
        /// <para>
        /// One <see cref="MoleculeResults"/> per molecule, so a molecule's chromatograms are read
        /// once however many transitions it has, and only one molecule's peaks are held at a time.
        /// </para>
        /// </summary>
        public static IEnumerable<TransitionChromInfosRef> EnumerateTransitionChromInfos(SrmDocument document)
        {
            foreach (var nodePep in document.Molecules)
            {
                var moleculeResults = new MoleculeResults(document.Settings, nodePep);
                foreach (var nodeGroup in nodePep.TransitionGroups)
                {
                    foreach (var nodeTran in nodeGroup.Transitions)
                    {
                        yield return new TransitionChromInfosRef(nodePep, nodeGroup, nodeTran,
                            moleculeResults.GetTransitionChromInfos(nodeGroup.TransitionGroup, nodeTran.Transition));
                    }
                }
            }
        }

        /// <summary>
        /// Every precursor of a document with its chrom infos rebuilt from the .skyd, in document
        /// order. The precursor level counterpart of <see cref="EnumerateTransitionChromInfos"/>,
        /// and what a test which used to index a precursor node's Results walks instead.
        /// <para>
        /// One <see cref="MoleculeResults"/> per molecule, so a molecule's chromatograms are read
        /// once however many precursors it has.
        /// </para>
        /// </summary>
        public static IEnumerable<TransitionGroupChromInfosRef> EnumerateTransitionGroupChromInfos(SrmDocument document)
        {
            foreach (var nodePep in document.Molecules)
            {
                var moleculeResults = new MoleculeResults(document.Settings, nodePep);
                foreach (var nodeGroup in nodePep.TransitionGroups)
                {
                    yield return new TransitionGroupChromInfosRef(nodePep, nodeGroup,
                        moleculeResults.GetTransitionGroupChromInfos(nodeGroup.TransitionGroup));
                }
            }
        }

        /// <summary>
        /// One precursor's chrom infos, rebuilt from the .skyd. The molecule which owns it is
        /// searched for, because a test which holds only the precursor cannot say - so a test
        /// asking about more than one should walk
        /// <see cref="EnumerateTransitionGroupChromInfos"/> instead, which reads each molecule once.
        /// </summary>
        public static Results<TransitionGroupChromInfo> GetTransitionGroupChromInfos(SrmDocument document,
            TransitionGroupDocNode nodeGroup)
        {
            var nodePep = FindMolecule(document, nodeGroup);
            return new MoleculeResults(document.Settings, nodePep)
                .GetTransitionGroupChromInfos(nodeGroup.TransitionGroup);
        }

        /// <summary>
        /// One precursor's chrom infos in one replicate. See
        /// <see cref="GetTransitionGroupChromInfos(SrmDocument,TransitionGroupDocNode)"/>.
        /// </summary>
        public static ChromInfoList<TransitionGroupChromInfo> GetTransitionGroupChromInfos(SrmDocument document,
            TransitionGroupDocNode nodeGroup, int replicateIndex)
        {
            var nodePep = FindMolecule(document, nodeGroup);
            return new MoleculeResults(document.Settings, nodePep)
                .GetTransitionGroupChromInfos(nodeGroup.TransitionGroup, replicateIndex);
        }

        /// <summary>
        /// The molecule of a document which has the given precursor among its children, matched on
        /// the <see cref="TransitionGroup"/> rather than the node, so that a node a test picked up
        /// from an earlier revision of the document still finds it - an identity outlives the nodes
        /// which carry it.
        /// </summary>
        public static PeptideDocNode FindMolecule(SrmDocument document, TransitionGroupDocNode nodeGroup)
        {
            foreach (var nodePep in document.Molecules)
            {
                if (nodePep.TransitionGroups.Any(nodeGroupChild =>
                        ReferenceEquals(nodeGroupChild.TransitionGroup, nodeGroup.TransitionGroup)))
                    return nodePep;
            }

            throw new ArgumentException(string.Format(@"Precursor {0} is not in this document",
                nodeGroup.TransitionGroup));
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
                            {
                                NoteCount++;
                                PrecursorNoteCount++;
                            }
                            if (annotations.ListAnnotations().Length > 0)
                                AnnotationCount++;
                        }
                    }

                    // A transition's peaks are the precursor's columnar results too, one per file
                    // it was found in.
                    if (groupResults == null)
                        continue;

                    foreach (var nodeTran in nodeGroup.Children.Cast<TransitionDocNode>())
                    {
                        for (int replicateIndex = 0;
                             replicateIndex < groupResults.ChromFileIds.ReplicatePositions.ReplicateCount;
                             replicateIndex++)
                        {
                            foreach (var entry in groupResults.GetTransitionPeaks(nodeTran.Transition, replicateIndex))
                            {
                                TransitionResults++;
                                var annotations = groupResults.FindTransitionAnnotations(nodeTran.Transition,
                                    replicateIndex, entry.Key);
                                if (annotations.Note != null)
                                {
                                    NoteCount++;
                                    TransitionNoteCount++;
                                }
                                if (annotations.ListAnnotations().Length > 0)
                                    AnnotationCount++;
                                if (entry.Value.UserSet == UserSet.TRUE)
                                    UserSetCount++;
                            }
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

        /// <summary>
        /// The <see cref="NoteCount"/> split by the level the note is on, so that a document
        /// which has lost notes says which kind it lost.
        /// </summary>
        public int PrecursorNoteCount { get; private set; }
        public int TransitionNoteCount { get; private set; }
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

            // Every count here is per file, not per replicate: a replicate imported with
            // --import-append has two files and every count doubles. That is what the chrom infos
            // these are asserted against used to give - one per file, and one per optimization step
            // as well, which is why the counts come from the .skyd wherever it can be read.
            bool integrateAll = document.Settings.TransitionSettings.Integration.IsIntegrateAll;
            foreach (var nodePep in document.Molecules)
            {
                // One read for the whole molecule, which is what makes asking about every
                // precursor and transition of it affordable. A document which has not been
                // connected to its .skyd - one a test deserialized to look at, say - can say
                // nothing this way, and is counted from its columnar results instead.
                var moleculeResults = new MoleculeResults(document.Settings, nodePep);
                bool fromChromatograms = moleculeResults.HasChromatograms;
                if (fromChromatograms)
                {
                    peptidesActual += moleculeResults.GetPeptideChromInfos(index)
                        .Count(chromInfo => chromInfo.PeakCountRatio >= 0.5);
                }
                else
                {
                    peptidesActual += CountGoodMoleculePeaks(nodePep, index, integrateAll);
                }

                foreach (var nodeGroup in nodePep.TransitionGroups)
                {
                    bool isLight = nodeGroup.TransitionGroup.LabelType.IsLight;
                    int groupCount, transitionCount;
                    if (fromChromatograms)
                        CountGoodPeaks(moleculeResults, nodeGroup, index, integrateAll, out groupCount, out transitionCount);
                    else
                        CountGoodPeaks(nodeGroup, index, integrateAll, out groupCount, out transitionCount);

                    if (isLight)
                    {
                        tranGroupsActual += groupCount;
                        transitionsActual += transitionCount;
                    }
                    else
                    {
                        tranGroupsHeavyActual += groupCount;
                        transitionsHeavyActual += transitionCount;
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

        /// <summary>
        /// How many of one precursor's peaks in a replicate are good ones, rebuilt from the .skyd.
        /// This is what the counts were originally written against: one chrom info per file and per
        /// optimization step, which is what the doc nodes used to hold.
        /// </summary>
        private static void CountGoodPeaks(MoleculeResults moleculeResults, TransitionGroupDocNode nodeGroup,
            int replicateIndex, bool integrateAll, out int groupCount, out int transitionCount)
        {
            groupCount = moleculeResults
                .GetTransitionGroupChromInfos(nodeGroup.TransitionGroup, replicateIndex)
                .Count(chromInfo => chromInfo.PeakCountRatio >= 0.5);
            transitionCount = nodeGroup.Transitions.Sum(nodeTran => moleculeResults
                .GetTransitionChromInfos(nodeGroup.TransitionGroup, nodeTran.Transition, replicateIndex)
                .Count(chromInfo => chromInfo.IsGoodPeak(integrateAll)));
        }

        /// <summary>
        /// The same counts taken from the columnar results, for a document whose chromatograms
        /// cannot be read. Those hold optimization step zero only, so a document with an
        /// optimization function counts a peak once here and once per step above. No caller
        /// combines the two - a test either has its .skyd or it does not.
        /// </summary>
        private static void CountGoodPeaks(TransitionGroupDocNode nodeGroup, int replicateIndex,
            bool integrateAll, out int groupCount, out int transitionCount)
        {
            var ratios = GetPeakCountRatiosByFile(nodeGroup, replicateIndex, integrateAll, out transitionCount);
            groupCount = ratios.Values.Count(ratio => ratio >= 0.5);
        }

        /// <summary>
        /// How many of one molecule's peaks in a replicate are good ones, taken from the columnar
        /// results. A molecule's peak count ratio in a file is the mean of its precursors' there,
        /// over every precursor it has - which is what <see cref="PeptideChromInfo.PeakCountRatio"/>
        /// held.
        /// </summary>
        private static int CountGoodMoleculePeaks(PeptideDocNode nodePep, int replicateIndex, bool integrateAll)
        {
            int transitionGroupCount = nodePep.TransitionGroupCount;
            if (transitionGroupCount == 0)
                return 0;

            var totalByFile = new Dictionary<ReferenceValue<ChromFileInfoId>, double>();
            foreach (var nodeGroup in nodePep.TransitionGroups)
            {
                foreach (var entry in GetPeakCountRatiosByFile(nodeGroup, replicateIndex, integrateAll, out _))
                {
                    totalByFile.TryGetValue(entry.Key, out double total);
                    totalByFile[entry.Key] = total + entry.Value;
                }
            }

            return totalByFile.Values.Count(total => total / transitionGroupCount >= 0.5);
        }

        /// <summary>
        /// One precursor's peak count ratio in each file of a replicate - the fraction of its
        /// transitions with a good peak there - plus how many good transition peaks there were
        /// altogether. A replicate almost always has one file; a multi injection replicate has
        /// several, and every count these helpers produce is per file.
        /// </summary>
        private static Dictionary<ReferenceValue<ChromFileInfoId>, double> GetPeakCountRatiosByFile(
            TransitionGroupDocNode nodeGroup, int replicateIndex, bool integrateAll, out int transitionCount)
        {
            transitionCount = 0;
            var ratios = new Dictionary<ReferenceValue<ChromFileInfoId>, double>();
            var results = nodeGroup.AbbreviatedResults;
            if (results == null)
                return ratios;

            var goodByFile = new Dictionary<ReferenceValue<ChromFileInfoId>, int>();
            var totalByFile = new Dictionary<ReferenceValue<ChromFileInfoId>, int>();
            foreach (var nodeTran in nodeGroup.Transitions)
            {
                if (!results.HasTransitionResults(nodeTran.Transition))
                    continue;
                foreach (var entry in results.GetTransitionPeaks(nodeTran.Transition, replicateIndex))
                {
                    var key = ReferenceValue.Of(entry.Key);
                    totalByFile.TryGetValue(key, out int total);
                    totalByFile[key] = total + 1;
                    if (!entry.Value.IsGoodPeak(integrateAll))
                        continue;
                    transitionCount++;
                    goodByFile.TryGetValue(key, out int good);
                    goodByFile[key] = good + 1;
                }
            }

            foreach (var entry in totalByFile)
            {
                goodByFile.TryGetValue(entry.Key, out int good);
                ratios.Add(entry.Key, (double) good / entry.Value);
            }

            return ratios;
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
