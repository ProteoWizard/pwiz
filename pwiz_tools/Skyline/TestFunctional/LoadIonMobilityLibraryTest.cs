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
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Controls.Startup;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class LoadIonMobilityLibraryTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestLoadIonMobilityLibrary()
        {
            TestFilesZip = @"TestFunctional\LoadIonMobilityLibraryTest.zip";
            RunFunctionalTest();
        }

        protected override bool ShowStartPage => true;

        protected override void DoTest()
        {
            // Make it so that default SrmSettings object has an ion mobility library
            var settings = SrmSettingsList.GetDefault();
            var ionMobilityLibrary =
                new IonMobilityLibrary("MyLibrary", TestFilesDir.GetTestPath("testPO.imsdb"), null);
            Settings.Default.IonMobilityLibraryList.Add(ionMobilityLibrary);
            Assert.IsFalse(ionMobilityLibrary.IsUsable);
            settings = settings.ChangeTransitionSettings(
                settings.TransitionSettings.ChangeIonMobilityFiltering(
                    settings.TransitionSettings.IonMobilityFiltering.ChangeLibrary(ionMobilityLibrary)));
            Settings.Default.SrmSettingsList.Clear();
            Settings.Default.SrmSettingsList.Add(settings);
            var startPage = WaitForOpenForm<StartPage>();
            // Push the "Blank Document" button on the Start Page.
            // The Blank Document button causes an extra call to "SkylineWindow.NewDocument", which is
            // necessary to reproduce the problem that this code is verifying is fixed
            RunUI(() =>
            {
                ActionBoxControl newDocumentControl = RecurseControlsOfType<ActionBoxControl>(startPage)
                    .Single(control => control.Caption == Resources.SkylineStartup_SkylineStartup_Blank_Document);
                newDocumentControl.EventAction();
            });
            WaitForOpenForm<SkylineWindow>();
            WaitForDocumentLoaded();

            // Make sure that the ion mobility library has been loaded
            var libraryLoaded = SkylineWindow.Document.Settings.TransitionSettings.IonMobilityFiltering
                .IonMobilityLibrary;
            Assert.IsNotNull(libraryLoaded);
            Assert.AreEqual(ionMobilityLibrary.FilePath, libraryLoaded.FilePath);
            Assert.IsTrue(libraryLoaded.IsUsable);
        }

        private IEnumerable<T> RecurseControlsOfType<T>(Control parent)
        {
            var result = parent.Controls.OfType<Control>().SelectMany(RecurseControlsOfType<T>);
            if (parent is T)
            {
                result = result.Prepend((T) (object) parent);
            }
            return result;
        }
    }
}
