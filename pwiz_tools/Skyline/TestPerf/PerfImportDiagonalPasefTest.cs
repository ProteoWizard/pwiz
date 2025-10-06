/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Verify consistent import of Bruker DiagonalPASEF.
    /// </summary>
    [TestClass]
    public class PerfImportBrukerDiagonalPasefTest : AbstractFunctionalTestEx
    {
        protected override bool IsRecordMode => false;

        // N.B. this test is currently excluded from TeamCity testing by mentioning it in pwiz\scripts\misc\tc-perftests-skiplist.txt

        [TestMethod, NoParallelTesting(TestExclusionReason.VENDOR_FILE_LOCKING)] // Bruker wants exclusive read access to raw data
        public void BrukerDiagonalPasefImportTest()
        {
            // RunPerfTests = true; // Uncomment this to force test to run in IDE
            Log.AddMemoryAppender();

            TestFilesZip = GetPerfTestDataURL(@"PerfImportBrukerDiagonalPasef.zip");
            TestFilesPersistent = new[] { ".d" }; // List of file basenames that we'd like to unzip alongside parent zipFile, and (re)use in place

            MsDataFileImpl.PerfUtilFactory.IssueDummyPerfUtils = false; // Turn on performance measurement

            RunFunctionalTest();

            var logs = Log.GetMemoryAppendedLogEvents();
            var stats = PerfUtilFactory.SummarizeLogs(logs, TestFilesPersistent); // Show summary
            var log = new Log("Summary");
            if (TestFilesDirs != null)
                log.Info(stats.Replace(TestFilesDir.PersistentFilesDir, "")); // Remove tempfile info from log
        }

        private KeyValuePair<string, string> GetChromInfoStrings(ChromatogramInfo tranInfo)
        {
            var chromGroup = tranInfo.GroupInfo;
            return new KeyValuePair<string, string>(
                $@"{chromGroup.FilePath.GetFileName()} {chromGroup.ChromatogramGroupId} {tranInfo.ChromTransition}",
                string.Join(" # ",
                    tranInfo.Peaks.Where(p => p.Area > 0).Select(p => $@"{p.ToString()} nI={tranInfo.TimeIntensities.NumPoints}")));
        }

        private bool altered; // For debug purposes we may remove some replicates
        
        protected override void DoTest()
        {
            // Note the expected values as saved in the test file
            var skyfile = TestFilesDir.GetTestPath("HELA_FASTA.sky");
            RunUI(() =>
            {
                SkylineWindow.OpenFile(skyfile);
            });
            var doc0 = WaitForDocumentLoaded();

            // For debug convenience
            //  RemoveReplicateReference(@"DiaPASEF"); // debug convenience - remove 20240619_HeLa_400ng_py12_IO25_35min_Slot2-25_1_8265
            //  RemoveReplicateReference(@"synchroPASEF"); // debug convenience - remove B_240304_IO25x75_HeLa_35min_vista_6S_200mz_70ms_Slot2-1_1_7799
            // RemoveReplicateReference(@"midiaPASEF"); // debug convenience - remove B_20231027_IO25x75_HeLa_800ng_35min_midiaPASEF_24x12_75ms_frag_Slot2-6_1_6699
            //RemoveAllPeptidesExcept( "AELSGPVYLDLNLQDIQEEIR"); // Look at just this peptide for debug ease
            //RemoveAllFragmentsExcept(902.4578); // Look at just this fragment for debug ease
            if (altered) // Did we clear out any replicates or peptides or fragments?
            {
                RunUI(() => SkylineWindow.SaveDocument());
                LoadNewDocument(true); // Close the current document to trim the .skyd
                RunUI(() => SkylineWindow.OpenFile(skyfile)); 
                WaitForDocumentLoaded();
                doc0 = SkylineWindow.Document;
            }

            var expected = GetResultPeaks(doc0, out var maxExpectedHeight);
            LoadNewDocument(true); // Close the current document so we can delete the .skyd

            // Load a .sky with mising .skyd, forcing re-import with existing parameters
            var skydFile = TestFilesDir.GetTestPath("HELA_FASTA.skyd");
            File.Delete(skydFile);

            var skylFile = TestFilesDir.GetTestPath("HELA_FASTA.skyl"); // Ignore audit log changes
            File.Delete(skylFile);
            
            // Update the paths to the .d files mentioned in the skyline doc
            var text = File.ReadAllText(skyfile);
            // Change the paths to match the test locations
            text = text.Replace(@"database_path=""C:\Dev\WinGroups\pwiz_tools\Skyline\SkylineTester Results\PerfImportBrukerDiagonalPasef\", $@"database_path=""{PathEx.EscapePathForXML(TestFilesDir.FullPath)}\");
            text = text.Replace(@"file_path=""c:\Skyline T&amp;est ^Data\Perftests\PerfImportBrukerDiagonalPasef\", $@"file_path=""{PathEx.EscapePathForXML(TestFilesDir.PersistentFilesDir)}\");
            text = text.Replace(@"D:\data\DiagonalPASEF\dia-PASEF", PathEx.EscapePathForXML(TestFilesDir.PersistentFilesDir));
            text = text.Replace(@"D:\data\DiagonalPASEF\midia-PASEF", PathEx.EscapePathForXML(TestFilesDir.PersistentFilesDir));
            text = text.Replace(@"D:\data\DiagonalPASEF\synchro-pasef", PathEx.EscapePathForXML(TestFilesDir.PersistentFilesDir));

            File.WriteAllText(skyfile, text);

            Stopwatch loadStopwatch = new Stopwatch();
            loadStopwatch.Start();
            var doc = SkylineWindow.Document;
            RunUI(() =>
            {
                Settings.Default.ImportResultsSimultaneousFiles = (int) MultiFileLoader.ImportResultsSimultaneousFileOptions.many;
                SkylineWindow.OpenFile(skyfile);
            });

            var doc1 = WaitForDocumentChangeLoaded(doc, 15 * 60 * 1000); // 15 minutes
            if (!altered)
            {
                AssertEx.IsDocumentState(doc1, null, 255, 578, 593, 30191);
            }
            loadStopwatch.Stop();
            DebugLog.Info("load time = {0}", loadStopwatch.ElapsedMilliseconds);

            var diffFileE = TestFilesDir.GetTestPath("expected.txt"); // For debug convenience e.g. WinMerge
            var diffFileA = TestFilesDir.GetTestPath("actual.txt");

            var actual = GetResultPeaks(doc1, out var maxActualHeight);

            var errors = new List<string>();
            var errorsE = new List<string>();
            var errorsA = new List<string>();
            foreach (var key in expected.Keys.Where(key => actual.ContainsKey(key)))
            {
                if (expected[key] != actual[key])
                {
                    errors.Add($@"{key} expected:{Environment.NewLine}""{expected[key]}""{Environment.NewLine}got:{Environment.NewLine}""{actual[key]}""{Environment.NewLine}");
                    errorsE.Add($@"{key}{Environment.NewLine}{expected[key]}");
                    errorsA.Add($@"{key}{Environment.NewLine}{actual[key]}");
                }
            }
            foreach (var key in expected.Keys.Where(key => !actual.ContainsKey(key)))
            {
                var msg = $@"no peak found for {key}";
                errors.Add(msg);
                errorsE.Add(msg);
                errorsA.Add(string.Empty);
            }
            foreach (var key in actual.Keys.Where(key => !expected.ContainsKey(key)))
            {
                var msg = $@"unexpected peak found for {key}";
                errors.Add(msg);
                errorsA.Add(msg);
                errorsE.Add(string.Empty);
            }
            if (errors.Count > 0)
            {
                // For debug convenience e.g. WinMerge
                File.WriteAllLines(diffFileE, errorsE.Select(e=> $@"{e}{Environment.NewLine}"));
                File.WriteAllLines(diffFileA, errorsA.Select(e => $@"{e}{Environment.NewLine}"));
                AssertEx.Fail(string.Join(Environment.NewLine, errors));
            }
            AssertEx.AreEqual(maxExpectedHeight, maxActualHeight, 1, @"max height");

            /*            TODO ?
                        // Test isolation scheme import (combined mode only)
                        if (!MsDataFileImpl.ForceUncombinedIonMobility)
                        {
                            var tranSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                            RunUI(() => tranSettings.TabControlSel = TransitionSettingsUI.TABS.FullScan);
                            var isoEditor = ShowDialog<EditIsolationSchemeDlg>(tranSettings.AddIsolationScheme);
                            RunUI(() => isoEditor.UseResults = false);
                            ValidateIsolationSchemeImport(isoEditor, "20240619_HeLa_400ng_py12_IO25_35min_Slot2-25_1_8265.d",
                                32, 25, null);
                            ValidateIsolationSchemeImport(isoEditor, "B_240304_IO25x75_HeLa_35min_vista_6S_200mz_70ms_Slot2-1_1_7799.d",
                                24, 25, 0.5);
                            ValidateIsolationSchemeImport(isoEditor, "B_20231027_IO25x75_HeLa_800ng_35min_midiaPASEF_24x12_75ms_frag_Slot2-6_1_6699.d",
                                24, 25, 0.5);
                            OkDialog(isoEditor, isoEditor.CancelDialog);
                            OkDialog(tranSettings, tranSettings.CancelDialog);
                        }

                    private void ValidateIsolationSchemeImport(EditIsolationSchemeDlg isoEditor, string fileName,
                        int windowCount, int windowWidth, double? margin)
                    {
                        RunDlg<OpenDataSourceDialog>(isoEditor.ImportRanges, openData =>
                        {
                            openData.SelectFile(TestFilesDir.GetTestPath(fileName));
                            openData.Open();
                        });
                        WaitForConditionUI(() => windowCount == (isoEditor.GetIsolationWindows()?.Count ?? 0));
                        RunUI(() =>
                        {
                            var listIsolationWindows = isoEditor.GetIsolationWindows();
                            AssertEx.AreEqual(windowCount, listIsolationWindows.Count);
                            foreach (var isolationWindow in listIsolationWindows)
                            {
                                AssertEx.AreEqual(windowWidth, isolationWindow.End - isolationWindow.Start, 
                                    string.Format("Range {0} to {1} does not have width {2}", isolationWindow.Start, isolationWindow.End, windowWidth));
                                AssertEx.AreEqual(margin, isolationWindow.StartMargin);
                                AssertEx.AreEqual(margin, isolationWindow.EndMargin);
                            }
                        });
                    }

            */
   
            Assert.IsFalse(altered, "Debug code was left uncommented!"); 
            return;
            
            
            Dictionary<string, string> GetResultPeaks(SrmDocument srmDocument, out double maxIntensity)
            {
                var dictionary = new Dictionary<string, string>();
                var results = srmDocument.Settings.MeasuredResults;
                var tolerance = (float)srmDocument.Settings.TransitionSettings.Instrument.MzMatchTolerance;
                maxIntensity = 0.0;
                for (var index = 0; index < srmDocument.MeasuredResults.MSDataFilePaths.Count(); index++)
                {
                    foreach (var pair in srmDocument.PeptidePrecursorPairs)
                    {
                        AssertEx.IsTrue(results.TryLoadChromatogram(index, pair.NodePep, pair.NodeGroup,
                            tolerance, out var chromGroupInfo));

                        foreach (var chromGroup in chromGroupInfo)
                        {
                            foreach (var tranInfo in chromGroup.TransitionPointSets.Where(t => !soleMz.HasValue || Math.Abs(soleMz.Value-t.ProductMz.Value) < .001))
                            {
                                maxIntensity = Math.Max(maxIntensity, tranInfo.MaxIntensity);
                                var kvp = GetChromInfoStrings(tranInfo);
                                if (dictionary.TryGetValue(kvp.Key, out var value))
                                {
                                    dictionary[kvp.Key] = value + Environment.NewLine + kvp.Value;
                                }
                                else
                                {
                                    dictionary.Add(kvp.Key, kvp.Value);
                                }
                            }
                        }
                    }
                }

                return dictionary;
            }
        }

        #region debug_stuff
        private void RemoveAllPeptidesExcept(string sequence)
        {
            var satisfied = false;
            foreach (var moleculeGroup in SkylineWindow.Document.MoleculeGroups.ToArray())
            {
                if (satisfied || moleculeGroup.Molecules.All(m => sequence != m.Target.ToString()))
                {
                    var doc = SkylineWindow.Document;
                    RunUI(() =>
                    {
                        SkylineWindow.SelectedPath = new IdentityPath(moleculeGroup.PeptideGroup);
                        SkylineWindow.EditDelete();
                    });
                    WaitForDocumentChange(doc);
                }
                else 
                {
                    foreach (var m in moleculeGroup.Molecules.Where(m => sequence != m.Target.ToString()))
                    {
                        var doc = SkylineWindow.Document;
                        RunUI(() =>
                        {
                            SkylineWindow.SelectedPath = new IdentityPath(moleculeGroup.PeptideGroup, m.Peptide);
                            SkylineWindow.EditDelete();
                        });
                        WaitForDocumentChange(doc);
                    }

                    satisfied = true; // Keep just the first seen
                }
            }
            
            altered = true;
        }

        private double? soleMz; // Debug aid, set by RemoveAllFragmentsExcept()
        private void RemoveAllFragmentsExcept(double mz)
        {
            soleMz = mz;
            foreach (var moleculeGroup in SkylineWindow.Document.MoleculeGroups.ToArray())
            {
                foreach (var molecule in moleculeGroup.Molecules)
                {
                    foreach (var tg in molecule.TransitionGroups)
                    {
                        foreach (var t in tg.Transitions)
                        {
                            if (Math.Abs(t.Mz  - mz) > .001)
                            {
                                var doc = SkylineWindow.Document;
                                RunUI(() =>
                                {
                                    SkylineWindow.SelectedPath = new IdentityPath(moleculeGroup.PeptideGroup,
                                        molecule.Peptide, tg.TransitionGroup, t.Transition);
                                    SkylineWindow.EditDelete();
                                });
                                WaitForDocumentChange(doc);
                            }
                        }
                    }
                }
            }
            altered = true;
        }

        private static string RemoveEmptyLines(char[] text)
        {
            var result = string.Join("\n", 
                new string(text).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Where(line => !string.IsNullOrWhiteSpace(line)));
            return result;
        }

        private void RemoveReplicateReference(string replicateName)
        {
            // Remove reference to replicate with file type that we don't need to handle at this time
            var manageResultsDlg = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
            RunUI(() =>
            {
                manageResultsDlg.SelectedChromatograms = new[]
                {
                    SkylineWindow.DocumentUI.Settings.MeasuredResults.Chromatograms.First(c => c.Name.Contains(replicateName))
                };
            });
            RunUI(manageResultsDlg.RemoveReplicates);
            OkDialog(manageResultsDlg, manageResultsDlg.OkDialog);
            altered = true;
        }
        #endregion
    }
}
