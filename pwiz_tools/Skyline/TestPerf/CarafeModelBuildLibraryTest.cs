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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.Proteome;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.AlphaPeptDeep;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    /// <summary>
    /// Builds an AlphaPeptDeep library with a model CarafeSharp fine-tuned on one Orbitrap Astral run
    /// (EXP25162_2025us0285X1_A, a 24 minute gradient), whose RT model is a fine-tuned Chronologer,
    /// CarafeSharp's default. A fine-tuned RT model predicts on its training run's gradient, so the
    /// library holds that run's retention times in minutes, not iRT.
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
            // Osprey predicts the library: a local build, or the latest TeamCity build downloaded
            OspreyTestUtil.RegisterOsprey(OspreyTestUtil.EnsureOsprey(TestContext));
            RunUI(() => SkylineWindow.Paste(TextUtil.LineSeparate(PEPTIDES.Select(p => p.Sequence))));

            const string libraryName = "CarafeModelLibrary";
            string libraryPath = TestFilesDir.GetTestPath(libraryName + BiblioSpecLiteSpec.EXT);
            string modelPath = TestFilesDir.GetTestPath("AstralFineTuned.carafemodel");
            Assert.AreEqual(RtModelType.chronologer, CarafeModelFile.Open(modelPath).RtModel);

            const string modelName = "Astral 24 min";
            var peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = libraryName;
                buildLibraryDlg.LibraryPath = libraryPath;
                // Predict on the CPU, whatever GPU this computer has
                buildLibraryDlg.TorchRuntimes = new TorchRuntimes { CudaDriver = null };
                buildLibraryDlg.AlphaPeptDeep = true;
                // The list starts with the pretrained models alone
                Assert.IsTrue(buildLibraryDlg.CarafeModel.IsDefault);
            });

            // Edit list... shows the grid of models, empty; its Add... takes a name and a file and
            // shows what the file was trained on
            var listDlg = ShowDialog<CarafeModelListDlg>(buildLibraryDlg.EditCarafeModelList);
            RunUI(() => Assert.AreEqual(0, listDlg.Models.Count));
            var editDlg = ShowDialog<EditCarafeModelDlg>(listDlg.AddItem);
            RunUI(() =>
            {
                editDlg.ModelName = modelName;
                editDlg.ModelPath = modelPath;
                AssertEx.Contains(editDlg.Details, @"Thermo Orbitrap Astral");
            });
            // A name already in the list is refused
            OkDialog(editDlg, editDlg.OkDialog);
            RunUI(() =>
            {
                var model = listDlg.Models.Single();
                Assert.AreEqual(modelName, model.Name);
                Assert.AreEqual(modelPath, model.FilePath);
            });
            var editAgainDlg = ShowDialog<EditCarafeModelDlg>(listDlg.AddItem);
            RunUI(() =>
            {
                editAgainDlg.ModelName = modelName;
                editAgainDlg.ModelPath = modelPath;
            });
            RunDlg<MessageDlg>(editAgainDlg.OkDialog, dlg =>
            {
                AssertEx.AreComparableStrings(SettingsUIResources.EditCarafeModelDlg_OkDialog_The_model__0__already_exists_, dlg.Message);
                dlg.OkDialog();
            });
            OkDialog(editAgainDlg, editAgainDlg.CancelDialog);
            OkDialog(listDlg, listDlg.OkDialog);

            // The list's one model is offered and chosen, with what it was trained on in its name
            RunUI(() =>
            {
                var choice = CarafeModelChoice.Read(modelPath);
                Assert.IsNotNull(choice);
                Assert.AreEqual(@"Thermo Orbitrap Astral", choice.Instrument);
                Assert.AreEqual(24, choice.GradientMinutes.Value, 0.1);
                Assert.AreEqual(25, choice.Nce);
                Assert.AreEqual(1, choice.TrainingRunCount);
                Assert.IsTrue(choice.RtR2 > 0.99);
                var models = Settings.Default.CarafeModelList;
                Assert.AreEqual(2, models.Count);
                buildLibraryDlg.CarafeModel = models[1];
                Assert.AreEqual(modelPath, buildLibraryDlg.CarafeModelPath);
            });

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

            // The next library build offers the model again, by name, and the combo's own Add...
            // adds a second one, which Edit list... can remove
            peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);
            RunUI(() =>
            {
                buildLibraryDlg.TorchRuntimes = new TorchRuntimes { CudaDriver = null };
                buildLibraryDlg.AlphaPeptDeep = true;
                var models = Settings.Default.CarafeModelList;
                Assert.AreEqual(modelName, models[1].Name);
                AssertEx.Contains(models.GetDisplayName(models[1]), modelName, @"Thermo Orbitrap Astral",
                    string.Format(SettingsUIResources.CarafeModelChoice_GetDetails__0__min_gradient, 24.ToString(LocalizationHelper.CurrentCulture)));
                buildLibraryDlg.CarafeModel = models[1];
                Assert.AreEqual(modelPath, buildLibraryDlg.CarafeModelPath);
            });
            const string secondName = "Astral copy";
            RunDlg<EditCarafeModelDlg>(buildLibraryDlg.AddCarafeModel, dlg =>
            {
                dlg.ModelName = secondName;
                dlg.ModelPath = modelPath;
                dlg.OkDialog();
            });
            RunUI(() =>
            {
                Assert.AreEqual(secondName, buildLibraryDlg.CarafeModel.Name);
                Assert.AreEqual(3, Settings.Default.CarafeModelList.Count);
            });
            RunDlg<CarafeModelListDlg>(buildLibraryDlg.EditCarafeModelList, dlg =>
            {
                Assert.AreEqual(2, dlg.Models.Count);
                dlg.SelectItem(secondName);
                dlg.RemoveItem();
                Assert.AreEqual(modelName, dlg.Models.Single().Name);
                dlg.OkDialog();
            });
            RunUI(() =>
            {
                var models = Settings.Default.CarafeModelList;
                Assert.AreEqual(2, models.Count);
                // The chosen model was removed, so the choice falls back to the default
                Assert.IsTrue(buildLibraryDlg.CarafeModel.IsDefault);
                buildLibraryDlg.CarafeModel = models[1];
                Assert.AreEqual(modelName, buildLibraryDlg.CarafeModel.Name);
            });
            OkDialog(buildLibraryDlg, buildLibraryDlg.CancelDialog);
            OkDialog(peptideSettings, peptideSettings.CancelDialog);

            // Release the library before checking that nothing still holds the test files
            RunUI(() => SkylineWindow.NewDocument(true));
            WaitForDocumentLoaded();
            TestFilesDir.CheckForFileLocks(TestFilesDir.FullPath);
        }
    }
}
