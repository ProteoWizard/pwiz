/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Verify operation of UI when working with large protdb files that may need 
    /// processing before the settings can be changed.  In particular verify the 
    /// interaction of the UI with the background loader, and behaviour on cancellation.
    /// </summary>
    [TestClass]
    public class PerfUniquePeptidesTest : AbstractFunctionalTestEx
    {
        private PeptideFilter.PeptideUniquenessConstraint _cancellationCheckType;
        private string _initialBackgroundProteome;
        private string _newBackgroundProteome;
        private string _skyfile;
        private bool _quickexit;
        private bool _verbose;

        // Scenarios to test:
        // 0) Background proteome nicely digested and metatdata resolved
        // 1)  No current background proteome
        // 2)  Current background proteome same as in new settings, needs digest and protein metadata search
        // 3)  Current background proteome same as in new settings, needs protein metadata search
        // 4)  Current background proteome not same as in new settings

        void scenario(PeptideFilter.PeptideUniquenessConstraint cancellationCheckType, string initialBackgroundProteome, string newBackgroundProteome = null, bool verbose = false)
        {
            AllowInternetAccess = true; // Testing cancellation of web lookup is integral to this test
            TestFilesZip = GetPerfTestDataURL(@"PerfUniquePeptidesTest.zip");
            _skyfile = "lots_of_human_proteins.sky";
            _cancellationCheckType = cancellationCheckType;
            _initialBackgroundProteome = initialBackgroundProteome;
            _newBackgroundProteome = newBackgroundProteome;
            _quickexit = false;
            _verbose = verbose;
            RunFunctionalTest();
        }

        /// <summary>
        /// For tracking down some hanging tests
        /// </summary>
        void LogToConsole(string message, [CallerLineNumber] int lineNumber = 0)
        {
            if (_verbose)
            {
                Console.WriteLine($@"{new TimeStampISO8601()} at line {lineNumber}: {message}");
            }
        }

        [TestMethod]
        public void UniquePeptides0PerfTest()
        {
            // Scenarios to test:
            // 0) Background proteome nicely digested and metatdata resolved
            scenario(PeptideFilter.PeptideUniquenessConstraint.none, "human_and_yeast.protdb");
        }

        [TestMethod]
        public void UniquePeptides1PerfTest()
        {
            // 1)  No current background proteome
            scenario(PeptideFilter.PeptideUniquenessConstraint.gene, null, "human_and_yeast_no_metadata.protdb", true);
        }

        [TestMethod]
        public void UniquePeptides2PerfTest()
        {
            // 2)  Current background proteome same as in new settings, needs digest and protein metadata search
            scenario(PeptideFilter.PeptideUniquenessConstraint.protein, "human_and_yeast_no_digest.protdb");
        }

        [TestMethod]
        public void UniquePeptides3PerfTest()
        {
            // 3)  Current background proteome same as in new settings, needs protein metadata search
            scenario(PeptideFilter.PeptideUniquenessConstraint.gene, "human_and_yeast_no_metadata_too.protdb");
        }

        [TestMethod]
        public void UniquePeptides4PerfTest()
        {
            // 4)  Current background proteome not same as in new settings
            scenario(PeptideFilter.PeptideUniquenessConstraint.species, "human_and_yeast.protdb", "human_and_yeast_no_metadata.protdb");
        }

        [TestMethod]
        public void UniquePeptides5PerfTest()
        {
            // Just verify that we've fixed a problem with opening files with uniqueness mode already turned on
            AllowInternetAccess = true; // Testing cancellation of web lookup is integral to this test
            TestFilesZip = GetPerfTestDataURL(@"PerfUniquePeptidesTest5.zip");
            _skyfile = "minimal.sky";
            _quickexit = true;
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            runScenario(_cancellationCheckType, 
                (_initialBackgroundProteome == null) ? null : TestFilesDir.GetTestPath(_initialBackgroundProteome),
                (_newBackgroundProteome == null) ? null : TestFilesDir.GetTestPath(_newBackgroundProteome));
            LogToConsole("test complete");
        }

        void runScenario(PeptideFilter.PeptideUniquenessConstraint cancellationCheckType, string initialBackgroundProteome, string newBackgroundProteome = null)
        {
            // In each scenario we want to test:
            //  a) Cancellation while protdb processing in is progress
            //  b) Proper changes to document when we eventually don't cancel
            LogToConsole("Begin debug output...");
            LogToConsole($@"load {_skyfile}");
            OpenDocument(_skyfile);
            LogToConsole("loaded");
            if (_quickexit)
            {
                // Just wanted to see if it loads OK
                LogToConsole("attempting quick exit (calling NewDocument to shut down protdb load)");
                RunUI(() => SkylineWindow.NewDocument(true)); // Force protdb shutdown
                return;
            }
            LogToConsole("Collapse proteins...");
            RunUI(() => { SkylineWindow.CollapseProteins(); }); // Things get pretty slow when trying to show tens of thousands of peptides

            if (initialBackgroundProteome != null)
            {
                LogToConsole("Set the background loader to work...");
                // Set the background loader to work
                ChooseBackgroundProteomeAndUniquenessFilter(initialBackgroundProteome, 
                    PeptideFilter.PeptideUniquenessConstraint.none, 
                    false);  // Don't attempt cancellation
            }
            var protdbPath = newBackgroundProteome ?? initialBackgroundProteome;
            const int PROTEIN_COUNT = 1149;
            if (cancellationCheckType != PeptideFilter.PeptideUniquenessConstraint.none)
            {
                // Test cancellation
                var cancelChecks = new List<PeptideFilter.PeptideUniquenessConstraint>
                { 
                    PeptideFilter.PeptideUniquenessConstraint.none, // May need to wait for digest
                    PeptideFilter.PeptideUniquenessConstraint.protein, // Should not need to wait for protein metadata, might need to wait for digest
                    PeptideFilter.PeptideUniquenessConstraint.gene
                };
                if (!cancelChecks.Contains(cancellationCheckType))
                {
                    cancelChecks.Add(cancellationCheckType);
                }
                foreach (var mode in cancelChecks) 
                {
                    var doCancel = mode != PeptideFilter.PeptideUniquenessConstraint.none;
                    LogToConsole($@"cancelChecks: ChooseBackgroundProteomeAndUniquenessFilter({protdbPath}, {mode}, {doCancel})...");
                    ChooseBackgroundProteomeAndUniquenessFilter(protdbPath, mode, doCancel);
                    LogToConsole(@"done");
                    // Should have cancelled out of any longwait, so no changes to document nodes yet
                    AssertEx.IsDocumentState(SkylineWindow.Document, null, PROTEIN_COUNT, 8323, null, null, mode + "(cancelled): ");
                }
            }
            // Now properly set filter
            var expectedPeptides = new Dictionary<PeptideFilter.PeptideUniquenessConstraint, int>()
            {
                {PeptideFilter.PeptideUniquenessConstraint.protein, 17878},
                {PeptideFilter.PeptideUniquenessConstraint.gene, 114054},
                {PeptideFilter.PeptideUniquenessConstraint.none, 184853},
                {PeptideFilter.PeptideUniquenessConstraint.species, 184450},
            };
            foreach (var mode in expectedPeptides.Keys)
            {
                LogToConsole($@"uniqueness types: ChooseBackgroundProteomeAndUniquenessFilter({protdbPath}, {mode}, false)...");
                ChooseBackgroundProteomeAndUniquenessFilter(protdbPath, mode, false);
                LogToConsole($@"WaitForDocumentLoaded...");
                WaitForDocumentLoaded();
                LogToConsole(@"done");
                AssertEx.IsDocumentState(SkylineWindow.Document, null, PROTEIN_COUNT,
                    expectedPeptides[mode], null, null, mode + ": ");
            }
            LogToConsole(@"test complete, set new document...");
            RunUI(() => SkylineWindow.NewDocument(true)); // Force protdb shutdown
            LogToConsole(@"done");
        }

        private void ChooseBackgroundProteomeAndUniquenessFilter(string protdbPath, 
            PeptideFilter.PeptideUniquenessConstraint constraint, 
            bool doCancel, 
            string fastaFilePath = null)
        {
            var basename = Path.GetFileNameWithoutExtension(protdbPath);
            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            if (!peptideSettingsUI.ListBackgroundProteomes.ToList().Contains(basename))
            {
                LogToConsole(@"BuildBackgroundProteomeDlg...");
                // Skyline doesn't know about this background proteome yet, use the Edit Background Proeteome dialog to load it
                var buildBackgroundProteomeDlg = ShowDialog<BuildBackgroundProteomeDlg>(peptideSettingsUI.ShowBuildBackgroundProteomeDlg);
                RunUI(() =>
                {
                    buildBackgroundProteomeDlg.BackgroundProteomeName = basename;
                    buildBackgroundProteomeDlg.BackgroundProteomePath = protdbPath;
                    if (!string.IsNullOrEmpty(fastaFilePath))
                        buildBackgroundProteomeDlg.AddFastaFile(fastaFilePath);
                });
                OkDialog(buildBackgroundProteomeDlg, buildBackgroundProteomeDlg.OkDialog);
            }
            else
            {
                LogToConsole(@"directly set peptideSettingsUI.SelectedBackgroundProteome");
                RunUI(() => { peptideSettingsUI.SelectedBackgroundProteome = basename; });
            }
            RunUI(() => { peptideSettingsUI.ComboPeptideUniquenessConstraintSelected = constraint; });
            if (doCancel)
            {
                // Expect a longwait dialog as a result of hitting OK button - cancel out of it
                string selected=null;
                RunUI(() => selected = peptideSettingsUI.SelectedBackgroundProteome);
                var longWaitDlg = ShowDialog<LongWaitDlg>(peptideSettingsUI.OkDialog);
                if (SkylineWindow.Document.Settings.HasBackgroundProteome &&
                    Equals(SkylineWindow.Document.Settings.PeptideSettings.BackgroundProteome.Name, selected) &&
                    (constraint == PeptideFilter.PeptideUniquenessConstraint.gene || constraint == PeptideFilter.PeptideUniquenessConstraint.species))
                {
                    LogToConsole(@"wait for SkylineWindow.BackgroundProteomeManager.ForegroundLoadRequested...");
                    WaitForCondition(() => SkylineWindow.BackgroundProteomeManager.ForegroundLoadRequested); // Verify that background loader is made to wait for us
                }
                LogToConsole("longWaitDlg.CancelButton.PerformClick...");
                OkDialog(longWaitDlg, longWaitDlg.CancelButton.PerformClick);
                LogToConsole("peptideSettingsUI.CancelDialog...");
                OkDialog(peptideSettingsUI, peptideSettingsUI.CancelDialog);
            }
            else
            {
                bool settingsChanged = false;
                RunUI(() => settingsChanged = peptideSettingsUI.IsSettingsChanged);
                if (!settingsChanged)
                {
                    LogToConsole("peptideSettingsUI.OkDialog");
                    // Click the OK button but it is not expected to change anything
                    OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
                }
                else
                {
                    LogToConsole("WaitDocumentChange");
                    using (new WaitDocumentChange(null, true))
                    {
                        LogToConsole("peptideSettingsUI.OkDialog");
                        OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
                    }
                }
            }
        }

    }
}
