/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.AlphaPeptDeep;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    /// <summary>
    /// Builds an AlphaPeptDeep library with a model CarafeSharp fine-tuned on one Orbitrap Astral run
    /// (EXP25162_2025us0285X1_A, a 24 minute gradient). A fine-tuned RT model predicts on its training
    /// run's gradient, so the library holds that run's retention times in minutes, not iRT.
    /// </summary>
    [TestClass]
    public class CarafeModelBuildLibraryTest : AbstractFunctionalTestEx
    {
        /// <summary>
        /// Peptides Osprey detected in the training run at q-value 0.0002, one from each 2.5 minutes of
        /// the gradient, with the retention times it measured.
        /// </summary>
        private static readonly (string Sequence, double RetentionTime)[] PEPTIDES =
        {
            (@"PQGPPPPGKPQGPPAQGGSK", 4.627),
            (@"DPGAAVPGAANASAQQPR", 5.892),
            (@"AGYPTGTGVGPQAAAAAAAK", 8.460),
            (@"VQNATLAVANITNADSATR", 11.562),
            (@"EVQLVESGGGLVQPGGSLR", 13.052),
            (@"AVLDVFEEGTEASAATAVK", 15.720),
            (@"LGGSLADSYLDEGFLLDK", 19.065),
            (@"LTVGAAQVPAQLLVGALR", 20.037),
        };

        /// <summary>
        /// How far a predicted retention time may be from the measured one, in minutes. The fine-tuned
        /// model's median error on held-out peptides is about 0.15 minutes; iRT values would be tens of
        /// units away.
        /// </summary>
        private const double RT_TOLERANCE = 1.0;

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestCarafeModelBuildLibrary()
        {
            TestFilesZip = "TestPerf/CarafeModelBuildLibraryTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.Paste(TextUtil.LineSeparate(PEPTIDES.Select(p => p.Sequence))));

            const string libraryName = "CarafeModelLibrary";
            string libraryPath = TestFilesDir.GetTestPath(libraryName + BiblioSpecLiteSpec.EXT);
            string modelPath = TestFilesDir.GetTestPath("AstralFineTuned.carafemodel");

            var peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = libraryName;
                buildLibraryDlg.LibraryPath = libraryPath;
                buildLibraryDlg.AlphaPeptDeep = true;
            });
            RunLongNativeDlg<NativeOpenFileDialog>(buildLibraryDlg.ShowCarafeModelDlg, dlg =>
            {
                dlg.EnterPath(modelPath);
                dlg.Accept();
            });
            RunUI(() => Assert.AreEqual(modelPath, buildLibraryDlg.CarafeModelPath));

            RunUI(buildLibraryDlg.OkWizardPage);
            WaitForCondition(() => buildLibraryDlg.Builder != null);
            Assert.AreEqual(modelPath, ((AlphapeptdeepLibraryBuilder)buildLibraryDlg.Builder).CarafeModelPath);
            WaitForClosedForm<BuildLibraryDlg>();
            WaitForCondition(() => peptideSettings.AvailableLibraries.Contains(libraryName));
            RunUI(() => peptideSettings.PickedLibraries = new[] { libraryName });
            OkDialog(peptideSettings, peptideSettings.OkDialog);
            WaitForDocumentLoaded();

            var library = SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.Single();
            foreach (var peptide in PEPTIDES)
            {
                var spectrum = library.GetSpectra(new LibKey(peptide.Sequence, 2), null, LibraryRedundancy.best).Single();
                Assert.IsNotNull(spectrum.RetentionTime, peptide.Sequence);
                double predicted = spectrum.RetentionTime.Value;
                Assert.IsTrue(Math.Abs(predicted - peptide.RetentionTime) <= RT_TOLERANCE,
                    string.Format(@"{0}: predicted {1:F2} min, measured {2:F2} min", peptide.Sequence, predicted, peptide.RetentionTime));
            }

            // Release the library before checking that nothing still holds the test files
            RunUI(() => SkylineWindow.NewDocument(true));
            WaitForDocumentLoaded();
            TestFilesDir.CheckForFileLocks(TestFilesDir.FullPath);
        }
    }
}
