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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
{
    /// <summary>
    /// Summary description for ThermoQuantTest
    /// </summary>
    [TestClass]
    public class ThermoQuantTest : AbstractUnitTest
    {
        private const string ZIP_FILE = @"TestData\Results\ThermoQuant.zip";

        [TestMethod]
        public void ThermoFileTypeTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            string extRaw = ExtensionTestContext.ExtThermoRaw;

            // Do file type checks
            using (var msData = new MsDataFileImpl(TestFilesDir.GetTestPath("Site20_STUDY9P_PHASEII_QC_03" + extRaw)))
            {
                Assert.IsTrue(msData.IsThermoFile);
            }

            using (var msData = new MsDataFileImpl(TestFilesDir.GetTestPath("Site20_STUDY9P_PHASEII_QC_03.mzXML")))
            {
                Assert.IsTrue(msData.IsThermoFile);
            }

            using (var msData = new MsDataFileImpl(TestFilesDir.GetTestPath("Site20_STUDY9P_PHASEII_QC_05" + extRaw)))
            {
                Assert.IsTrue(msData.IsThermoFile);
            }
        }

        /// <summary>
        /// Verifies that canceling an import cleans up correctly.
        /// </summary>
        [TestMethod]
        public void ThermoFormatsTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath;
            SrmDocument doc = InitThermoDocument(TestFilesDir, out docPath);
            using (var docContainer = new ResultsTestDocumentContainer(doc, docPath))
            {
                // Verify mzML and RAW contain same results
                string extRaw = ExtensionTestContext.ExtThermoRaw;
                AssertResult.MatchChromatograms(docContainer,
                                            TestFilesDir.GetTestPath("Site20_STUDY9P_PHASEII_QC_03" + extRaw),
                                            TestFilesDir.GetTestPath("Site20_STUDY9P_PHASEII_QC_03.mzML"),
                                            0, 0);
                // Verify mzXML and RAW contain same results (some small peaks are different)
                AssertResult.MatchChromatograms(docContainer,
                                            TestFilesDir.GetTestPath("Site20_STUDY9P_PHASEII_QC_03" + extRaw),
                                            TestFilesDir.GetTestPath("Site20_STUDY9P_PHASEII_QC_03.mzXML"),
                                            2, 0);
            }
        }

        /// <summary>
        /// Verifies that canceling an import cleans up correctly.
        /// </summary>
        [TestMethod]
        public void ThermoCancelImportTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string resultsPath = TestFilesDir.GetTestPath("Site20_STUDY9P_PHASEII_QC_03" +
                ExtensionTestContext.ExtThermoRaw);
            string dirPath = Path.GetDirectoryName(resultsPath) ?? "";
            string docPath;
            SrmDocument doc = InitThermoDocument(TestFilesDir, out docPath);
            // Retries are kept as a guard, but should no longer be needed: the cancel is issued
            // from the loader's own progress callback below, which removed the race that used to
            // make this test lose to a fast import. The retry conditions that remain describe
            // the one outcome that would still prove nothing - the import finishing before any
            // cancel could be issued.
            const int maxTries = 5;
            for (int tries = 0; tries < maxTries; tries++)
            {
                using (var docContainer = new ResultsTestDocumentContainer(doc, docPath))
                {
                    // Remove any existing temp and cache files
                    foreach (var path in Directory.GetFiles(dirPath))
                    {
                        if (IsCacheOrTempFile(path))
                            FileEx.SafeDelete(path);
                    }
                    string name = Path.GetFileNameWithoutExtension(resultsPath);
                    var listChromatograms = new List<ChromatogramSet> { new ChromatogramSet(name, new[] { MsDataFileUri.Parse(resultsPath) }) };
                    var docResults = doc.ChangeMeasuredResults(new MeasuredResults(listChromatograms));
                    // The cancel is issued from inside the loader's own progress report, on the
                    // loader's thread, rather than from here after polling for the cache file.
                    // Polling cannot win: measured on this import, the temp cache file exists
                    // before the loader's first progress report, and the import then reports
                    // progress 51 more times and finishes within a few milliseconds - well inside
                    // one 10 ms poll. Cold, the JIT slowed the loader enough for the poll to win;
                    // warm (test #1065 of pass 0 in the nightly, or after a handful of mzML imports
                    // in one process) the import finished first on every one of five tries, and
                    // the cancel's compare-and-swap failed against the already-loaded document.
                    // Cancelling from a progress callback lands while the loader is paused inside
                    // that report, so there is no race to win.
                    var canceler = new CancelFromProgressMonitor(docContainer, doc, docResults, dirPath);
                    docContainer.ProgressMonitor = canceler;
                    // Start cache load, but don't wait for completion
                    Assert.IsTrue(docContainer.SetDocument(docResults, doc));

                    // Wait up to 10 seconds for the loader to report progress with its cache file on disk
                    for (int i = 0; i < 1000 && canceler.Outcome == CancelOutcome.pending; i++)
                        Thread.Sleep(10);
                    if (canceler.Outcome == CancelOutcome.pending)
                    {
                        AssertEx.Fail(TextUtil.LineSeparate("Loader never reported progress with a cache file present. Found files:",
                            TextUtil.LineSeparate(Directory.GetFiles(dirPath))));
                    }
                    if (canceler.Outcome == CancelOutcome.import_finished_first)
                    {
                        // Should now be unreachable - the callback runs before the loader can finish
                        // its report - but if it ever is, it means the same thing the finished-cache
                        // check below retries on: nothing about cancellation was exercised.
                        if (tries < maxTries - 1)
                        {
                            FileEx.SafeDelete(docPath);
                            continue;   // Try again
                        }
                        AssertEx.Fail(string.Format("Import finished before the cancel could be issued on all {0} tries.", maxTries));
                    }
                    // Wait up to 10 seconds for cancel to occur
                    bool cancelOccurred = false;
                    for (int i = 0; i < 1000; i++)
                    {
                        if (docContainer.LastProgress != null && docContainer.LastProgress.IsCanceled)
                        {
                            cancelOccurred = true;
                            break;
                        }
                        Thread.Sleep(10);
                    }
                    // Wait up to 20 seconds for the cache to be removed
                    bool cacheRemoved = false;
                    for (int i = 0; i < 200; i++)
                    {
                        if (Directory.GetFiles(dirPath).IndexOf(IsCacheOrTempFile) == -1)
                        {
                            cacheRemoved = true;
                            break;
                        }
                        Thread.Sleep(100);
                    }
                    if (!cacheRemoved)
                    {
                        if (tries < maxTries - 1 && File.Exists(Path.ChangeExtension(docPath, ChromatogramCache.EXT)))
                        {
                            // Ending up with the final cache means the import finished before the
                            // cancel could take effect, so the cancellation this test exists to
                            // check never actually happened - the attempt proves nothing either
                            // way and is not a failure. Retrying that was already the intent here,
                            // but allowing it only on the first attempt is not enough where the
                            // import consistently wins the race: importing pre-converted mzML
                            // (pass 0) is fast enough that both attempts finished first.
                            FileEx.SafeDelete(docPath);
                            continue;   // Try again
                        }
                        if (!cancelOccurred)
                        {
                            Assert.Fail("Attempt to cancel results load failed on try {0}. {1}", tries + 1,
                                docContainer.LastProgress != null && docContainer.LastProgress.ErrorException != null
                                    ? docContainer.LastProgress.ErrorException.Message : string.Empty);
                        }
                        Assert.Fail(TextUtil.LineSeparate("Failed to remove cache file. Found files:", TextUtil.LineSeparate(Directory.GetFiles(dirPath))));
                    }
                    // The cache being gone is necessary but not sufficient: this is the assertion
                    // the test exists to make, so it is checked on the success path too rather
                    // than only when cleanup failed.
                    AssertEx.IsTrue(cancelOccurred, "Cache file was removed but the loader never reported a cancelled status.");
                    break;  // If we make it here then, successful
                }
            }
            // Cache file has been removed
        }

        private enum CancelOutcome { pending, issued, import_finished_first }

        /// <summary>
        /// Cancels a results import from inside the loader's own progress report, by reverting
        /// the container to the original document while the loader is paused in that report.
        /// Fires once, on the first running-state report made with a cache or temp file on
        /// disk, so the cancel lands mid-write - the case that has to clean up a partial cache.
        /// </summary>
        private class CancelFromProgressMonitor : IProgressMonitor
        {
            private readonly ResultsTestDocumentContainer _container;
            private readonly SrmDocument _docOriginal;
            private readonly SrmDocument _docLoading;
            private readonly string _dirPath;
            // Progress reports arrive on the loader's thread and, in later phases, on several
            // worker threads at once; the outcome is read from the test thread.
            private int _outcome;
            private int _claimed;

            public CancelFromProgressMonitor(ResultsTestDocumentContainer container, SrmDocument docOriginal,
                SrmDocument docLoading, string dirPath)
            {
                _container = container;
                _docOriginal = docOriginal;
                _docLoading = docLoading;
                _dirPath = dirPath;
            }

            public CancelOutcome Outcome { get { return (CancelOutcome) Volatile.Read(ref _outcome); } }

            public bool IsCanceled { get { return false; } }
            public bool HasUI { get { return false; } }

            public UpdateProgressResponse UpdateProgress(IProgressStatus status)
            {
                if (status.State != ProgressState.running || Directory.GetFiles(_dirPath).IndexOf(IsCacheOrTempFile) == -1)
                    return UpdateProgressResponse.normal;
                // First qualifying report claims the cancel; any concurrent report backs off.
                if (Interlocked.CompareExchange(ref _claimed, 1, 0) != 0)
                    return UpdateProgressResponse.normal;

                var outcome = _container.SetDocument(_docOriginal, _docLoading)
                    ? CancelOutcome.issued
                    : CancelOutcome.import_finished_first;
                Volatile.Write(ref _outcome, (int) outcome);
                return UpdateProgressResponse.normal;
            }
        }

        private static bool IsCacheOrTempFile(string path)
        {
            string fileName = Path.GetFileName(path);
            if (fileName != null && fileName.StartsWith(FileSaver.TEMP_PREFIX))
                return true;
            return PathEx.HasExtension(path, ChromatogramCache.EXT);
        }

        /// <summary>
        /// Verifies proper behavior of ratio calculations
        /// </summary>
        [TestMethod]
        public void ThermoRatioTest()
        {
            DoThermoRatioTest(RefinementSettings.ConvertToSmallMoleculesMode.none);
        }

        [TestMethod]
        public void ThermoRatioTestAsSmallMolecules()
        {
            // Ratio match by formula
            DoThermoRatioTest(RefinementSettings.ConvertToSmallMoleculesMode.formulas);
        }

        [TestMethod]
        public void ThermoRatioTestAsSmallMoleculeMassesAndNames()
        {
            // Ratio match by names
            DoThermoRatioTest(RefinementSettings.ConvertToSmallMoleculesMode.masses_and_names);
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void DoThermoRatioTest(RefinementSettings.ConvertToSmallMoleculesMode smallMoleculesTestMode)
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath;
            SrmDocument doc = InitThermoDocument(TestFilesDir, out docPath);
            SrmSettings settings = doc.Settings.ChangePeptideModifications(mods =>
                mods.ChangeInternalStandardTypes(new[]{IsotopeLabelType.light}));
            doc = doc.ChangeSettings(settings);
            if (smallMoleculesTestMode != RefinementSettings.ConvertToSmallMoleculesMode.none)
            {
                var docOrig = doc;
                var refine = new RefinementSettings();
                doc = refine.ConvertToSmallMolecules(doc, TestContext.ResultsDirectory, smallMoleculesTestMode);
                // This is our first example of a converted label doc - check roundtripping
                AssertEx.ConvertedSmallMoleculeDocumentIsSimilar(docOrig, doc, TestContext.GetTestResultsPath(), smallMoleculesTestMode);
                AssertEx.Serializable(doc);
            }
            using (var docContainer = new ResultsTestDocumentContainer(doc, docPath))
            {
                string extRaw = ExtensionTestContext.ExtThermoRaw;
                var listChromatograms = new List<ChromatogramSet>
                                        {
                                            new ChromatogramSet("rep03", new[]
                                                                             {
                                                                                 MsDataFileUri.Parse(TestFilesDir.GetTestPath(
                                                                                     "Site20_STUDY9P_PHASEII_QC_03" + extRaw))
                                                                             }),
                                            new ChromatogramSet("rep05", new[]
                                                                             {
                                                                                 MsDataFileUri.Parse(TestFilesDir.GetTestPath(
                                                                                     "Site20_STUDY9P_PHASEII_QC_05" + extRaw))
                                                                             })
                                        };
                var docResults = doc.ChangeMeasuredResults(new MeasuredResults(listChromatograms));
                Assert.IsTrue(docContainer.SetDocument(docResults, doc, true));
                docContainer.AssertComplete();
                docResults = docContainer.Document;

                AssertEx.AreEqualDeep(new[] { "TQU00490" },
                    docResults.MeasuredResults.CachedFileInfos.Select(fi => fi.InstrumentSerialNumber).Distinct().ToList());
                AssertEx.AreEqualDeep(new[] { "10 fmol/ul peptides in 3% ACN/0.1% Formic Acid" },
                    docResults.MeasuredResults.CachedFileInfos.Select(fi => fi.SampleId).Distinct().ToList());

                // Remove the first light transition, checking that this removes the ratio
                // from the corresponding heavy transition, but not the entire group, until
                // after all light transitions have been removed.
                IdentityPath pathFirstPep = docResults.GetPathTo((int)SrmDocument.Level.Molecules, 0);
                var nodePep = (PeptideDocNode)docResults.FindNode(pathFirstPep);
                Assert.AreEqual(2, nodePep.Children.Count);
                var nodeGroupLight = (TransitionGroupDocNode)nodePep.Children[0];
                IdentityPath pathGroupLight = new IdentityPath(pathFirstPep, nodeGroupLight.TransitionGroup);
                var nodeGroupHeavy = (TransitionGroupDocNode)nodePep.Children[1];
                IdentityPath pathGroupHeavy = new IdentityPath(pathFirstPep, nodeGroupHeavy.TransitionGroup);
                var expectedValues = new[] { 1.403414, 1.38697791, 1.34598482 };
                for (int i = 0; i < 3; i++)
                {
                    var pathLight = docResults.GetPathTo((int)SrmDocument.Level.Transitions, 0);
                    var pathHeavy = docResults.GetPathTo((int)SrmDocument.Level.Transitions, 3);
                    TransitionDocNode nodeTran = (TransitionDocNode)docResults.FindNode(pathHeavy);
                    docResults = (SrmDocument)docResults.RemoveChild(pathLight.Parent, docResults.FindNode(pathLight));
                    Assert.AreEqual(pathGroupHeavy, pathHeavy.Parent, "Transition found outside expected group");
                    //                nodePep = (PeptideDocNode) docResults.FindNode(pathFirstPep);
                    nodeGroupHeavy = (TransitionGroupDocNode)docResults.FindNode(pathGroupHeavy);
                    //                Assert.AreEqual(nodePep.Results[0][0].RatioToStandard, nodeGroupHeavy.Results[0][0].Ratio,
                    //                                "Peptide and group ratios not equal");
                }
                bool asSmallMolecules = (smallMoleculesTestMode != RefinementSettings.ConvertToSmallMoleculesMode.none);
                if (!asSmallMolecules) // GetTransitions() doesn't work the same way for small molecules - it only lists existing ones
                {
                    bool firstAdd = true;
                    var nodeGroupLightOrig = (TransitionGroupDocNode)doc.FindNode(pathGroupLight);
                    DocNode[] lightChildrenOrig = nodeGroupLightOrig.Children.ToArray();
                    foreach (var nodeTran in nodeGroupLightOrig.GetTransitions(docResults.Settings,
                        null, nodeGroupLightOrig.PrecursorMz, null, null, null, false))
                    {
                        var transition = nodeTran.Transition;
                        if (!firstAdd && lightChildrenOrig.IndexOf(node => Equals(node.Id, transition)) == -1)
                            continue;
                        // Add the first transition, and then the original transitions
                        docResults = (SrmDocument)docResults.Add(pathGroupLight, nodeTran);
                        nodeGroupHeavy = (TransitionGroupDocNode)docResults.FindNode(pathGroupHeavy);
                        firstAdd = false;
                    }
                }
            }
        }

        /// <summary>
        /// Verifies importing reference peptides with non-matching retention times
        /// works.
        /// </summary>
        [TestMethod]
        public void ThermoNonMatchingRTTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath;
            SrmDocument doc = InitThermoDocument(TestFilesDir, out docPath);
            string extRaw = ExtensionTestContext.ExtThermoRaw;
            var listChromatograms = new List<ChromatogramSet>
                                        {
                                            new ChromatogramSet("rep03", new[]
                                                                             {
                                                                                 MsDataFileUri.Parse(TestFilesDir.GetTestPath(
                                                                                     "Site20_STUDY9P_PHASEII_QC_03" + extRaw))
                                                                             }),
                                        };

            ValidateRelativeRT(RelativeRT.Preceding, doc, docPath, listChromatograms);
            ValidateRelativeRT(RelativeRT.Overlapping, doc, docPath, listChromatograms);
            ValidateRelativeRT(RelativeRT.Unknown, doc, docPath, listChromatograms);
        }

        private static void ValidateRelativeRT(RelativeRT relativeRT, SrmDocument doc, string docPath, List<ChromatogramSet> listChromatograms)
        {
            FileEx.SafeDelete(Path.ChangeExtension(docPath, ChromatogramCache.EXT));

            SrmSettings settings = doc.Settings.ChangePeptideModifications(mods =>
                mods.ChangeModifications(IsotopeLabelType.heavy, 
                    mods.AllHeavyModifications.Select(m => m.ChangeRelativeRT(relativeRT)).ToArray()));
            var docMods = doc.ChangeSettings(settings);
            var docResults = docMods.ChangeMeasuredResults(new MeasuredResults(listChromatograms));
            using (var docContainer = new ResultsTestDocumentContainer(docMods, docPath))
            {
                Assert.IsTrue(docContainer.SetDocument(docResults, docMods, true));
                docContainer.AssertComplete();
            }
        }

        /// <summary>
        /// Verifies import code for handling multiple instances of peptides in documents
        /// with various mixed up precursors and transitions
        /// </summary>
        [TestMethod]
        public void ThermoMixedPeptidesTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath;
            SrmDocument docMixed = InitMixedDocument(TestFilesDir, out docPath);
            FileEx.SafeDelete(Path.ChangeExtension(docPath, ChromatogramCache.EXT));
            SrmDocument docUnmixed = InitUnmixedDocument(TestFilesDir, out docPath);
            FileEx.SafeDelete(Path.ChangeExtension(docPath, ChromatogramCache.EXT));
            string extRaw = ExtensionTestContext.ExtThermoRaw;
            var listChromatograms = new List<ChromatogramSet>
                                        {
                                            new ChromatogramSet("rep03", new[]
                                                                             {
                                                                                 MsDataFileUri.Parse(TestFilesDir.GetTestPath(
                                                                                     "Site20_STUDY9P_PHASEII_QC_03" + extRaw))
                                                                             }),
                                            new ChromatogramSet("rep05", new[]
                                                                             {
                                                                                 MsDataFileUri.Parse(TestFilesDir.GetTestPath(
                                                                                     "Site20_STUDY9P_PHASEII_QC_05" + extRaw))
                                                                             })
                                        };
            var docResults = docMixed.ChangeMeasuredResults(new MeasuredResults(listChromatograms));
            SrmDocument docMixedUnmixed;
            using (var docContainerMixed = new ResultsTestDocumentContainer(docMixed, docPath))
            {
                Assert.IsTrue(docContainerMixed.SetDocument(docResults, docMixed, true));
                docContainerMixed.AssertComplete();
                docMixed = docContainerMixed.Document;
                docMixedUnmixed = (SrmDocument)docMixed.ChangeChildren(new DocNode[0]);
                docMixedUnmixed = docMixedUnmixed.AddPeptideGroups(docUnmixed.PeptideGroups, true, IdentityPath.ROOT,
                    out _, out _);
                docResults = docUnmixed.ChangeMeasuredResults(new MeasuredResults(listChromatograms));
            }

            using (var docContainerUnmixed = new ResultsTestDocumentContainer(docUnmixed, docPath))
            {
                Assert.IsTrue(docContainerUnmixed.SetDocument(docResults, docUnmixed, true));
                docContainerUnmixed.AssertComplete();
                docUnmixed = docContainerUnmixed.Document;
                AssertEx.DocumentCloned(docMixedUnmixed, docUnmixed);
            }
        }

        private static SrmDocument InitThermoDocument(TestFilesDir testFilesDir, out string docPath)
        {
            docPath = testFilesDir.GetTestPath("Site20_Study9p.sky");
            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
            AssertEx.IsDocumentState(doc, 0, 2, 10, 18, 54);
            return doc;
        }

        private static SrmDocument InitMixedDocument(TestFilesDir testFilesDir, out string docPath)
        {
            docPath = testFilesDir.GetTestPath("Site20_Study9p_mixed.sky");
            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
            AssertEx.IsDocumentState(doc, 0, 2, 8, 13, 31);
            return doc;
        }

        private static SrmDocument InitUnmixedDocument(TestFilesDir testFilesDir, out string docPath)
        {
            docPath = testFilesDir.GetTestPath("Site20_Study9p_unmixed.sky");
            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
            AssertEx.IsDocumentState(doc, 0, 1, 4, 8, 24);
            return doc;
        }
    }
}
