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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    /// <summary>
    /// Summary description for LibrarySettingsTest
    /// </summary>
    [TestClass]
    public class LibrarySettingsTest : AbstractUnitTest
    {
        /// <summary>
        /// List of <see cref="LibrarySpec"/> objects used for testing only,
        /// as a substitute for the one in <see cref="Settings.Default"/>.
        /// </summary>
        private static SpectralLibraryList TestLibraryList { get; set; }

        private static LibrarySpec FindLibrarySpec(Library library)
        {
            LibrarySpec spec;
            if (TestLibraryList.TryGetValue(library.Name, out spec))
                return spec;
            return null;
        }

        private static SrmDocument CreateNISTLibraryDocument(out LibraryManager libraryManager,
            out TestDocumentContainer docContainer, out int startRev)
        {
            SrmDocument docLoaded = CreateNISTLibraryDocument(ExampleText.TEXT_FASTA_YEAST_LIB,
                                                              false,
                                                              LibraryLoadTest.TEXT_LIB_YEAST_NIST,
                                                              out libraryManager,
                                                              out docContainer,
                                                              out startRev);
            AssertEx.IsDocumentState(docLoaded, startRev, 2, 4, 12);
            return docLoaded;
        }

        public static SrmDocument CreateNISTLibraryDocument(string textFasta, bool peptideList, string textLib,
            out LibraryManager libraryManager, out TestDocumentContainer docContainer, out int startRev)
        {
            var streamManager = new MemoryStreamManager();
            streamManager.TextFiles.Add(LibraryLoadTest.PATH_NIST_LIB, textLib);
            var librarySpec = new NistLibSpec("Yeast (NIST)", LibraryLoadTest.PATH_NIST_LIB);

            // For serialization, add the library spec to the settings
            TestLibraryList = new SpectralLibraryList { librarySpec };

            libraryManager = new LibraryManager { StreamManager = streamManager };
            docContainer = new TestDocumentContainer();

            SrmSettings settings = SrmSettingsList.GetDefault0_6();
            settings = settings.ChangePeptideLibraries(l => l.ChangeLibrarySpecs(new[] { librarySpec }));

            return CreateLibraryDocument(settings, textFasta, peptideList, docContainer, libraryManager, out startRev);            
        }

        private static SrmDocument CreateLibraryDocument(SrmSettings settings, string textFasta, bool peptideList,
            TestDocumentContainer docContainer, BackgroundLoader libraryManager, out int startRev)
        {
            startRev = 0;
            SrmDocument document = new SrmDocument(SrmSettingsList.GetDefault0_6());
            Assert.IsTrue(docContainer.SetDocument(document, null));

            // Register after first set document
            libraryManager.Register(docContainer);

            // Add libraries
            SrmDocument docLibraries = document.ChangeSettings(settings);
            ++startRev;

            // Add some FASTA
            IdentityPath path = IdentityPath.ROOT;
            SrmDocument docFasta = docLibraries.ImportFasta(new StringReader(textFasta), peptideList, path, out path);
            ++startRev;

            // Until libraries are loaded, only the sequences should appear            
            if (!peptideList)
                AssertEx.IsDocumentState(docFasta, startRev, textFasta.Count(c => c == '>'), 0, 0);

            // Run the library load
            Assert.IsTrue(docContainer.SetDocument(docFasta, document, true));
            ++startRev;

            // After library load completes peptides and transitions should have expected library info
            SrmDocument docLoaded = docContainer.Document;

            // Check expected library inforamation
            foreach (var nodePeptide in docLoaded.Peptides)
                Assert.IsNull(nodePeptide.Rank);
            foreach (var nodeTran in docLoaded.PeptideTransitions)
            {
                Assert.IsTrue(nodeTran.HasLibInfo);
                Assert.IsTrue(nodeTran.LibInfo.Rank <= 3);
            }

            return docLoaded;
        }

        [TestMethod]
        public void LibraryOnlyPeptidesTest()
        {
            LibraryManager libraryManager;
            TestDocumentContainer docContainer;
            int startRev;
            SrmDocument docLoaded = CreateNISTLibraryDocument(out libraryManager, out docContainer, out startRev);
            Assert.IsTrue(HasAllLibraryInfo(docLoaded));
            AssertEx.Serializable(docLoaded, (doc1, doc2) => ValidateLibraryDocs(doc1, doc2, libraryManager));

            SrmSettings settings = docLoaded.Settings.ChangePeptideFilter(f => f.ChangeMaxPeptideLength(14));
            settings = settings.ChangePeptideLibraries(l => l.ChangePick(PeptidePick.filter));
            SrmDocument docFilter = docLoaded.ChangeSettings(settings);
            Assert.IsFalse(HasAllLibraryInfo(docFilter));
            AssertEx.IsDocumentState(docFilter, ++startRev, 2, 14, 45);

            settings = settings.ChangePeptideLibraries(l => l.ChangePick(PeptidePick.either));
            docFilter = docFilter.ChangeSettings(settings);
            Assert.IsFalse(HasAllLibraryInfo(docFilter));
            AssertEx.IsDocumentState(docFilter, ++startRev, 2, 16, 51);
            AssertEx.Serializable(docFilter, (doc1, doc2) => ValidateLibraryDocs(doc1, doc2, libraryManager));

            settings = settings.ChangePeptideLibraries(l => l.ChangePick(PeptidePick.both));
            docFilter = docFilter.ChangeSettings(settings);
            Assert.IsTrue(HasAllLibraryInfo(docFilter));
            AssertEx.IsDocumentState(docFilter, ++startRev, 2, 2, 6);

            // Check all possible rankings
            settings = docLoaded.Settings.ChangePeptideLibraries(l => l.ChangeRankId(LibrarySpec.PEP_RANK_COPIES));
            SrmDocument docRank = docLoaded.ChangeSettings(settings);
            CheckRanks(docRank, null);
            settings = settings.ChangePeptideLibraries(l => l.ChangeRankId(LibrarySpec.PEP_RANK_TOTAL_INTENSITY));
            SrmDocument docRank2 = docRank.ChangeSettings(settings);
            CheckRanks(docRank2, docRank);
            settings = settings.ChangePeptideLibraries(l => l.ChangeRankId(LibrarySpec.PEP_RANK_PICKED_INTENSITY));
// ReSharper disable InconsistentNaming
            SrmDocument docRank3_1 = docRank2.ChangeSettings(settings);
// ReSharper restore InconsistentNaming
            CheckRanks(docRank3_1, docRank2);
            // Turns out TFRatio and picked intensity rank the same with these peptides
            settings = settings.ChangePeptideLibraries(l => l.ChangeRankId(NistLibSpecBase.PEP_RANK_TFRATIO));
// ReSharper disable InconsistentNaming
            SrmDocument docRank3_2 = docRank2.ChangeSettings(settings);
// ReSharper restore InconsistentNaming
            CheckRanks(docRank3_2, docRank2);
            AssertEx.Serializable(docRank3_2, (doc1, doc2) => ValidateLibraryDocs(doc1, doc2, libraryManager));

            SrmSettings setThrow1 = settings;
            AssertEx.ThrowsException<InvalidDataException>(() => setThrow1.ChangePeptideLibraries(l => l.ChangeRankId(XHunterLibSpec.PEP_RANK_EXPECT)));
            AssertEx.ThrowsException<InvalidDataException>(() => setThrow1.ChangePeptideLibraries(l => l.ChangePick(PeptidePick.filter)));
            AssertEx.ThrowsException<InvalidDataException>(() => setThrow1.ChangePeptideLibraries(l => l.ChangePick(PeptidePick.either)));

            // Check peptide limits based on rank
            settings = settings.ChangePeptideLibraries(l => l.ChangePeptideCount(1));
            SrmDocument docLimited = docRank3_1.ChangeSettings(settings);
            // Should now be 2 peptides with rank 1
            AssertEx.IsDocumentState(docLimited, ++startRev, 2, 2, 6);
            foreach (var nodePep in docLimited.Peptides)
                Assert.AreEqual(1, nodePep.Rank ?? 0);
            settings = settings.ChangePeptideLibraries(l => l.ChangePeptideCount(3));
            docLimited = docRank3_1.ChangeSettings(settings);
            Assert.IsTrue(ArrayUtil.ReferencesEqual(docRank3_1.Peptides.ToArray(), docLimited.Peptides.ToArray()));
            AssertEx.Serializable(docLimited, (doc1, doc2) => ValidateLibraryDocs(doc1, doc2, libraryManager));

            SrmSettings setThrow2 = settings;
            AssertEx.ThrowsException<InvalidDataException>(() => setThrow2.ChangePeptideLibraries(l => l.ChangePick(PeptidePick.filter)));
            AssertEx.ThrowsException<InvalidDataException>(() => setThrow2.ChangePeptideLibraries(l => l.ChangePick(PeptidePick.either)));
            AssertEx.NoExceptionThrown<InvalidDataException>(() => setThrow2.ChangePeptideLibraries(l => l.ChangeRankId(null)));

            settings = settings.ChangePeptideLibraries(l => l.ChangePeptideCount(null));

            SrmDocument docUnlimited = docLimited.ChangeSettings(settings);
            Assert.IsTrue(ArrayUtil.ReferencesEqual(docLimited.Peptides.ToArray(), docUnlimited.Peptides.ToArray()));

            SrmSettings setThrow3 = settings;
            AssertEx.ThrowsException<InvalidDataException>(() => setThrow3.ChangePeptideLibraries(l => l.ChangePeptideCount(PeptideLibraries.MIN_PEPTIDE_COUNT - 1)));
            AssertEx.ThrowsException<InvalidDataException>(() => setThrow3.ChangePeptideLibraries(l => l.ChangePeptideCount(PeptideLibraries.MAX_PEPTIDE_COUNT + 1)));
        }

        private static void ValidateLibraryDocs(SrmDocument docTarget, SrmDocument docActual, LibraryManager libraryManager)
        {
            var docContainer = new TestDocumentContainer();
            libraryManager.Register(docContainer);
            try
            {
                AssertEx.IsDocumentState(docActual, 0, docTarget.PeptideGroupCount, docTarget.PeptideCount,
                    docTarget.PeptideTransitionGroupCount, docTarget.PeptideTransitionCount);
                docActual = docActual.ChangeSettings(docActual.Settings.ConnectLibrarySpecs(FindLibrarySpec));
                Assert.IsTrue(docContainer.SetDocument(docActual, null, true));
                SrmDocument docLoaded = docContainer.Document;
                AssertEx.DocumentCloned(docTarget, docLoaded);
//                Assert.IsTrue(ArrayUtil.ReferencesEqual(docActual.Transitions.ToArray(), docLoaded.Transitions.ToArray()));
//                Assert.IsTrue(ArrayUtil.ReferencesEqual(docActual.TransitionGroups.ToArray(), docLoaded.TransitionGroups.ToArray()));
//                Assert.IsTrue(ArrayUtil.ReferencesEqual(docActual.Peptides.ToArray(), docLoaded.Peptides.ToArray()));
                Assert.IsTrue(ArrayUtil.ReferencesEqual(docActual.Children, docLoaded.Children));
            }
            finally
            {
                libraryManager.Unregister(docContainer);
            }
        }

        private static bool HasAllLibraryInfo(SrmDocument document)
        {
            foreach (var nodeGroup in document.PeptideTransitionGroups)
            {
                if (!nodeGroup.HasLibInfo)
                    return false;
            }
            foreach (var nodeTran in document.PeptideTransitions)
            {
                if (!nodeTran.HasLibInfo)
                    return false;
            }
            return true;
        }

        private static void CheckRanks(SrmDocument doc1, SrmDocument doc2)
        {
            IEnumerator<PeptideDocNode> it = null;
            bool equalRanks = (doc2 != null);
            if (equalRanks)
                it = doc2.Peptides.GetEnumerator();

            foreach (var nodePeptide in doc1.Peptides)
            {
                Assert.IsNotNull(nodePeptide.Rank);
                Assert.IsTrue(nodePeptide.Rank <= 3);

                if (it != null)
                {
                    if (!it.MoveNext() || it.Current == null)
                        Assert.Fail("Unexpected end of peptides.");
                    if (!Equals(nodePeptide.Rank, it.Current.Rank))
                        equalRanks = false;
                }
            }

            Assert.IsFalse(equalRanks);
        }

        [TestMethod]
        public void LibraryTransitionTest()
        {
            LibraryManager libraryManager;
            TestDocumentContainer docContainer;
            int startRev;
            SrmDocument docLoaded = CreateNISTLibraryDocument(out libraryManager, out docContainer, out startRev);

            // Test tolerance range
            SrmSettings settings = docLoaded.Settings.ChangeTransitionLibraries(l =>
                l.ChangeIonMatchTolerance(TransitionLibraries.MIN_MATCH_TOLERANCE));
            SrmDocument docLowTol = docLoaded.ChangeSettings(settings);
            // Use the original low tolerance for transition testing, since
            // the new low tolerance is for high accuracy data.
            docLowTol = docLowTol.ChangeSettings(settings.ChangeTransitionLibraries(l =>
                l.ChangeIonMatchTolerance(0.1)));
            settings = docLowTol.Settings.ChangeTransitionLibraries(l =>
                l.ChangeIonMatchTolerance(TransitionLibraries.MAX_MATCH_TOLERANCE));
            SrmDocument docHighTol = docLoaded.ChangeSettings(settings);

            Assert.AreEqual(docLowTol.PeptideTransitionCount, docHighTol.PeptideTransitionCount);

            var transLow = docLowTol.PeptideTransitions.ToArray();
            var transHigh = docHighTol.PeptideTransitions.ToArray();
            int diffCount = 0;
            for (int i = 0; i < transLow.Length; i++)
            {
                if (!Equals(transLow[i], transHigh[i]))
                    diffCount++;
            }
            Assert.AreEqual(2, diffCount);

            Assert.IsTrue(ArrayUtil.ReferencesEqual(docLoaded.PeptideTransitionGroups.ToArray(), docHighTol.PeptideTransitionGroups.ToArray()));
            Assert.IsTrue(HasMaxTransitionRank(docHighTol, 3));

            SrmSettings setThrow = settings;
            AssertEx.ThrowsException<InvalidDataException>(() =>
                setThrow.ChangeTransitionLibraries(l => l.ChangeIonMatchTolerance(TransitionLibraries.MAX_MATCH_TOLERANCE * 2)));
            AssertEx.ThrowsException<InvalidDataException>(() =>
                setThrow.ChangeTransitionLibraries(l => l.ChangeIonMatchTolerance(TransitionLibraries.MIN_MATCH_TOLERANCE / 2)));

            // Picked transition count
            settings = docLoaded.Settings.ChangeTransitionLibraries(l => l.ChangeIonCount(5));
            SrmDocument docHighIons = docLoaded.ChangeSettings(settings);
            AssertEx.IsDocumentState(docHighIons, ++startRev, 2, 4, 20);
            Assert.IsTrue(HasMaxTransitionRank(docHighIons, 5));
            Assert.IsFalse(HasMinTransitionOrdinal(docHighIons, 4));

            settings = settings.ChangeTransitionLibraries(l => l.ChangePick(TransitionLibraryPick.none));
            SrmDocument docFilteredIons = docHighIons.ChangeSettings(settings);
            AssertEx.IsDocumentState(docFilteredIons, ++startRev, 2, 4, 15); // Proline ions
            Assert.IsFalse(HasMaxTransitionRank(docFilteredIons, 5));

            settings = settings.ChangeTransitionFilter(f => f.ChangeFragmentRangeFirstName("ion 4")
                .ChangeFragmentRangeLastName("last ion").ChangeMeasuredIons(new MeasuredIon[0]));
            settings = settings.ChangeTransitionLibraries(l => l.ChangePick(TransitionLibraryPick.filter));
            SrmDocument docRankedFiltered = docFilteredIons.ChangeSettings(settings);
            AssertEx.IsDocumentState(docRankedFiltered, ++startRev, 2, 4, 20);
            Assert.IsTrue(HasMaxTransitionRank(docRankedFiltered, 5));
            Assert.IsTrue(HasMinTransitionOrdinal(docRankedFiltered, 4));
            AssertEx.Serializable(docRankedFiltered, (doc1, doc2) => ValidateLibraryDocs(doc1, doc2, libraryManager));
        }

        private static bool HasMaxTransitionRank(SrmDocument doc, int rank)
        {
            foreach (TransitionDocNode nodeTran in doc.PeptideTransitions)
            {
                if (!nodeTran.HasLibInfo || nodeTran.LibInfo.Rank > rank)
                    return false;
            }
            return true;
        }

        private static bool HasMinTransitionOrdinal(SrmDocument doc, int ordinal)
        {
            foreach (TransitionDocNode nodeTran in doc.PeptideTransitions)
            {
                if (nodeTran.Transition.Ordinal < ordinal)
                    return false;
            }
            return true;            
        }

        [TestMethod]
        public void LibraryMultipleTest()
        {
            var streamManager = new MemoryStreamManager();
            var loader = new LibraryLoadTest.TestLibraryLoader { StreamManager = streamManager };

            // Create library files
            const string hunterText = LibraryLoadTest.TEXT_LIB_YEAST_NIST1 + "\n" + LibraryLoadTest.TEXT_LIB_YEAST_NIST2;
            LibraryLoadTest.CreateHunterFile(streamManager, loader, hunterText);
            const string biblioText = LibraryLoadTest.TEXT_LIB_YEAST_NIST2 + "\n" + LibraryLoadTest.TEXT_LIB_YEAST_NIST3;
            LibraryLoadTest.CreateBiblioFile(streamManager, loader, biblioText);
            const string nistText = LibraryLoadTest.TEXT_LIB_YEAST_NIST3 + "\n" + LibraryLoadTest.TEXT_LIB_YEAST_NIST4;
            streamManager.TextFiles.Add(LibraryLoadTest.PATH_NIST_LIB, nistText);

            var hunterSpec = new XHunterLibSpec("Yeast (X!)", LibraryLoadTest.PATH_HUNTER_LIB);
            var bilbioSpec = new BiblioSpecLibSpec("Yeast (BS)", LibraryLoadTest.PATH_BIBLIOSPEC_LIB);
            var nistSpec = new NistLibSpec("Yeast (NIST)", LibraryLoadTest.PATH_NIST_LIB);

            // For serialization, add the library spec to the settings
            TestLibraryList = new SpectralLibraryList { hunterSpec, bilbioSpec, nistSpec };

            var libraryManager = new LibraryManager { StreamManager = streamManager };
            var docContainer = new TestDocumentContainer();

            SrmSettings settings = SrmSettingsList.GetDefault();
            settings = settings.ChangePeptideLibraries(l => l.ChangeLibrarySpecs(new LibrarySpec[] { hunterSpec, bilbioSpec, nistSpec }));

            int startRev;
            SrmDocument docLoaded = CreateLibraryDocument(settings, ExampleText.TEXT_FASTA_YEAST_LIB, false,
                docContainer, libraryManager, out startRev);
            AssertEx.IsDocumentState(docLoaded, startRev, 2, 4, 12);
            Assert.IsTrue(HasLibraryInfo(docLoaded, typeof(XHunterSpectrumHeaderInfo)));
            Assert.IsTrue(HasLibraryInfo(docLoaded, typeof(BiblioSpecSpectrumHeaderInfo)));
            Assert.IsTrue(HasLibraryInfo(docLoaded, typeof(NistSpectrumHeaderInfo)));

            // Remove the rank 1 transition from each transition group
            TransitionDocNode[] transitionNodes = docLoaded.PeptideTransitions.ToArray();
            for (int i = 0; i < transitionNodes.Length; i++)
            {
                var nodeTran = transitionNodes[i];
                if (nodeTran.LibInfo.Rank != 1)
                    continue;
                var path = docLoaded.GetPathTo((int) SrmDocument.Level.TransitionGroups, i/3);
                docLoaded = (SrmDocument) docLoaded.RemoveChild(path, nodeTran);
                ++startRev;
            }
            AssertEx.IsDocumentState(docLoaded, startRev, 2, 4, 8);
            // Make sure this can be serialized and deserialized without causing
            // a recalculation of the nodes in the tree.
            AssertEx.Serializable(docLoaded, (doc1, doc2) => ValidateLibraryDocs(doc1, doc2, libraryManager));
        }

        private static bool HasLibraryInfo(SrmDocument document, Type headerType)
        {
            foreach (var nodeGroup in document.PeptideTransitionGroups)
            {
                if (nodeGroup.HasLibInfo && headerType.IsInstanceOfType(nodeGroup.LibInfo))
                    return true;
            }
            return false;
        }

        [TestMethod]
        public void LibraryNoIonsTest()
        {
            var streamManager = new MemoryStreamManager();
            var loader = new LibraryLoadTest.TestLibraryLoader { StreamManager = streamManager };

            // Create X! Hunter library with 20 lowest intensity peaks in NIST library
            LibraryLoadTest.CreateHunterFile(streamManager, loader, LibraryLoadTest.TEXT_LIB_YEAST_NIST, true);

            var hunterSpec = new XHunterLibSpec("Yeast (X!)", LibraryLoadTest.PATH_HUNTER_LIB);

            // For serialization, add the library spec to the settings
            TestLibraryList = new SpectralLibraryList { hunterSpec };

            var libraryManager = new LibraryManager { StreamManager = streamManager };
            var docContainer = new TestDocumentContainer();

            SrmSettings settings = SrmSettingsList.GetDefault();
            settings = settings.ChangePeptideLibraries(l => l.ChangeLibrarySpecs(new LibrarySpec[] { hunterSpec }));

            int startRev;
            SrmDocument docLoaded = CreateLibraryDocument(settings, ExampleText.TEXT_FASTA_YEAST_LIB, false,
                docContainer, libraryManager, out startRev);
            // Peptides should have been chosen, but no transitions, since the spectra are garbage
            AssertEx.IsDocumentState(docLoaded, startRev, 2, 4, 0);
        }
    }
}