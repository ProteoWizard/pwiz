/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Skyline;
using pwiz.Skyline.Controls.Startup;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that libraries load correctly when a new blank document is created from the start page.
    /// </summary>
    [TestClass]
    public class NewDocumentLoadLibrariesTest : AbstractFunctionalTest
    {
        [TestMethod, NoParallelTesting]
        public void TestNewDocumentLoadLibraries()
        {
            TestFilesZip = @"TestFunctional\NewDocumentLoadLibrariesTest.zip";
            RunFunctionalTest();
        }

        protected override bool ShowStartPage => true;

        protected override void DoTest()
        {
            // Make it so that default SrmSettings object has an ion mobility library
            var settings = SrmSettingsList.GetDefault();
            var ionMobilityLibrary =
                new IonMobilityLibrary("MyIonMobilityLibrary", TestFilesDir.GetTestPath("testPO.imsdb"), null);
            Settings.Default.IonMobilityLibraryList.Add(ionMobilityLibrary);

            var irtCalculator = new RCalcIrt("MyIrtCalculator", TestFilesDir.GetTestPath("pierce.irtdb"));
            var retentionTimeRegression = new RetentionTimeRegression("MyRetentionTimeRegression", irtCalculator, null,
                RetentionTimeRegression.DEFAULT_WINDOW, ImmutableList<MeasuredRetentionTime>.EMPTY);
            Settings.Default.RTScoreCalculatorList.Add(irtCalculator);

            var librarySpec = new BiblioSpecLiteSpec("MyBlibLibrary", TestFilesDir.GetTestPath("rat_cmp_20.blib"));
            Settings.Default.SpectralLibraryList.Add(librarySpec);

            Assert.IsFalse(ionMobilityLibrary.IsUsable);
            settings = settings
                .ChangeTransitionSettings(
                    settings.TransitionSettings.ChangeIonMobilityFiltering(
                        settings.TransitionSettings.IonMobilityFiltering.ChangeLibrary(ionMobilityLibrary)))
                .ChangePeptideSettings(settings.PeptideSettings
                    .ChangePrediction(
                        settings.PeptideSettings.Prediction.ChangeRetentionTime(
                            retentionTimeRegression))
                    .ChangeLibraries(settings.PeptideSettings.Libraries.ChangeLibraries(
                        new List<LibrarySpec>{librarySpec},
                        new List<Library>{null})));

            Settings.Default.SrmSettingsList.Clear();
            Settings.Default.SrmSettingsList.Add(settings);
            var startPage = WaitForOpenForm<StartPage>();
            // Push the "Blank Document" button on the Start Page.
            // The Blank Document button causes an extra call to "SkylineWindow.NewDocument", which is
            // necessary to reproduce the problem that this code is verifying is fixed
            RunUI(() => startPage.ClickWizardAction(Resources.SkylineStartup_SkylineStartup_Blank_Document));
            WaitForOpenForm<SkylineWindow>();
            WaitForDocumentLoaded();

            // Make sure that the ion mobility library has been loaded
            var ionMobilityLibraryLoaded = SkylineWindow.Document.Settings.TransitionSettings.IonMobilityFiltering
                .IonMobilityLibrary;
            Assert.IsNotNull(ionMobilityLibraryLoaded);
            Assert.AreEqual(ionMobilityLibrary.FilePath, ionMobilityLibraryLoaded.FilePath);
            Assert.IsTrue(ionMobilityLibraryLoaded.IsUsable);

            var irtCalculatorLoaded =
                SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator as RCalcIrt;
            Assert.IsNotNull(irtCalculatorLoaded);
            Assert.AreEqual(irtCalculator.DatabasePath, irtCalculatorLoaded.DatabasePath);
            Assert.IsTrue(irtCalculatorLoaded.IsUsable);

            var spectralLibraryLoaded = SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.Single();
            Assert.IsNotNull(spectralLibraryLoaded);
            Assert.IsTrue(spectralLibraryLoaded.IsLoaded);
            var libarySpecLoaded = SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs.Single();
            Assert.IsNotNull(libarySpecLoaded);
            Assert.AreEqual(librarySpec.FilePath, libarySpecLoaded.FilePath);
        }
    }
}
