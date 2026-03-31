/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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

using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Lib;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that peak integration is blocked with a user-friendly message when
    /// document libraries have not finished loading. Regression test for issue #3949.
    /// </summary>
    [TestClass]
    public class PeakIntegrationLibraryGuardTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPeakIntegrationLibraryGuard()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // With no libraries configured, the guard should pass (nothing to load)
            bool result = false;
            RunUI(() => result = SkylineWindow.EnsureLibrariesLoadedForPeakIntegration());
            Assert.IsTrue(result);

            // Add a library spec with a null library entry to simulate libraries not yet loaded
            var badLibSpec = new BiblioSpecLiteSpec("NonExistentLibrary",
                Path.Combine(Path.GetTempPath(), "nonexistent_library.blib"));
            RunUI(() => SkylineWindow.ModifyDocument(
                "Add unloadable library for testing",
                doc => doc.ChangeSettings(doc.Settings.ChangePeptideSettings(
                    doc.Settings.PeptideSettings.ChangeLibraries(
                        doc.Settings.PeptideSettings.Libraries.ChangeLibraries(
                            new List<LibrarySpec> { badLibSpec },
                            new List<Library> { null }))))));

            // Verify libraries are not loaded
            RunUI(() =>
            {
                Assert.IsTrue(SkylineWindow.DocumentUI.Settings.PeptideSettings.Libraries.HasLibraries);
                Assert.IsFalse(SkylineWindow.DocumentUI.Settings.PeptideSettings.Libraries.IsLoaded);
            });

            // Guard should block and show a message dialog
            RunDlg<MessageDlg>(
                () => SkylineWindow.EnsureLibrariesLoadedForPeakIntegration(),
                dlg =>
                {
                    Assert.AreEqual(
                        SkylineResources.SkylineWindow_graphChromatogram_PickedPeak_Libraries_must_be_loaded,
                        dlg.Message);
                    dlg.OkDialog();
                });

            // Remove library spec and verify the guard passes again
            RunUI(() => SkylineWindow.ModifyDocument(
                "Remove library",
                doc => doc.ChangeSettings(doc.Settings.ChangePeptideSettings(
                    doc.Settings.PeptideSettings.ChangeLibraries(
                        doc.Settings.PeptideSettings.Libraries.ChangeLibraries(
                            new List<LibrarySpec>(),
                            new List<Library>()))))));
            WaitForDocumentLoaded();
            RunUI(() => result = SkylineWindow.EnsureLibrariesLoadedForPeakIntegration());
            Assert.IsTrue(result);
        }
    }
}
