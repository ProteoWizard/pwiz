/*
 * Original author: Yuval Boss <yuval .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Startup;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests for opening file from the Start Page.
    /// </summary>
    [TestClass]
    public class StartPageOpenFileTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestStartPageOpenFile()
        {
            TestFilesZip = @"TestFunctional\StartPageTest.zip";
            RunFunctionalTest();
        }

        protected override bool ShowStartPage
        {
            get { return true; }
        }

        protected override void DoTest()
        {
            var startPage = WaitForOpenForm<StartPage>();
            RunUI(() => startPage.OpenFile(TestFilesDir.GetTestPath("StartPageTest.sky")));
            WaitForOpenForm<SkylineWindow>();
        }
    }

    /// <summary>
    /// Tests that recent file panel is populated and that the recent file action works correctly.
    /// </summary>
    [TestClass]
    public class StartPageOpenRecentTest : AbstractFunctionalTest
    {
        private const string SKYLINE_FILENAME = "StartPageTest.sky";

        [TestMethod]
        public void TestStartPageOpenRecent()
        {
            TestFilesZip = @"TestFunctional\StartPageTest.zip";
            RunFunctionalTest();
        }

        protected override bool ShowStartPage
        {
            get { return true; }
        }

        protected override void DoTest()
        {
            var startPage = WaitForOpenForm<StartPage>();
            RunUI(() =>
            {
                var recentFileControls = startPage.GetRecentFileControls();
                Assert.IsTrue(recentFileControls.First().FileName.Equals(SKYLINE_FILENAME));
                startPage.OpenRecentFile(TestFilesDir.GetTestPath(SKYLINE_FILENAME));
            });
            WaitForOpenForm<SkylineWindow>();
        }

        protected override List<String> SetMru
        {
            get { return new List<string> { TestFilesDir.GetTestPath(SKYLINE_FILENAME) }; }
        }
    }

    /// <summary>
    /// Checks that the StartPage is populated with all the BoxPanels.
    /// </summary>
    [TestClass]
    public class StartPageCheckActıonBoxesTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestStartPageCheckActionBoxes()
        {
            RunFunctionalTest();
        }

        protected override bool ShowStartPage
        {
            get { return true; }
        }

        protected override void DoTest()
        {
            CheckBoxes();
        }

        private void CheckBoxes()
        {
            var startPage = WaitForOpenForm<StartPage>();
            RunUI(() => startPage.SelectedTab = StartPage.TABS.Tutorial);
            WaitForOpenForm<StartPage>();   // Wait for tutorial tab
            RunUI(() =>
            {
                startPage.SelectedTab = StartPage.TABS.Tutorial;

                Assert.IsTrue(AllBoxPanelsExist(startPage));
                startPage.DoAction(skylineWindow=>true);
            });
            WaitForOpenForm<SkylineWindow>();
        }

        private bool AllBoxPanelsExist(StartPage startPage)
        {
            var boxPanels = new List<ActionBoxControl>();
            GetControlsOfType(boxPanels, startPage);
            var openBoxPanels = startPage.GetVisibleBoxPanels();
            return boxPanels.Count == openBoxPanels.Count;
        }

        private void GetControlsOfType<T>(List<T> controls, Control root) where T :Control 
        {
            var tControl = root as T;
            if (null != tControl)
            {
                controls.Add(tControl);
            }
            foreach (Control child in root.Controls)
            {
                GetControlsOfType(controls, child);
            }
        } 
    }

    /// <summary>
    /// Checks that the NewDocument action works correctly.
    /// </summary>
    [TestClass]
    public class StartPageNewDocumentTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestStartPageNewDocument()
        {
            RunFunctionalTest();
        }

        protected override bool ShowStartPage
        {
            get { return true; }
        }

        protected override void DoTest()
        {
            var startPage = WaitForOpenForm<StartPage>();
            RunUI(() => startPage.DoAction(skylineWindow => true));
            WaitForOpenForm<SkylineWindow>();
        }
    }

    /// <summary>
    /// Checks that the Import Action works, randomly test either peptide, protein, transition list, or fasta import.
    /// </summary>
    [TestClass]
    public class StartPageImportTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestStartPageImport()
        {
            RunFunctionalTest();
        }

        protected override bool ShowStartPage
        {
            get { return true; }
        }

        protected override void DoTest()
        {
            Directory.CreateDirectory(TestContext.TestDir);
            {
                var types = new List<PasteDlgTab>
                {
                    new PasteDlgTab(ActionImport.DataType.fasta, typeof(PasteDlg.FastaTab)),
                    new PasteDlgTab(ActionImport.DataType.proteins, typeof(PasteDlg.ProteinListTab)),
                    new PasteDlgTab(ActionImport.DataType.transition_list, typeof(PasteDlg.TransitionListTab)),
                };
                var random = new Random(DateTime.UtcNow.Millisecond);
                int rand = random.Next(0, 3);
                var selectedImport = types[rand];
                var startPage = FindOpenForm<StartPage>();
                RunUI(() => startPage.TestImportAction(selectedImport.DType));
                WaitForOpenForm<SkylineWindow>();
                var settings = WaitForOpenForm<StartPageSettingsUI>();
                RunDlg<PasteDlg>(() => settings.AcceptButton.PerformClick(), paste =>
                {
                    Assert.AreEqual(selectedImport.TabType, paste.ShowingFormView.GetType());
                    paste.OkDialog();
                });
            }
        }
    }

    public class PasteDlgTab
    {
        public PasteDlgTab(ActionImport.DataType dataType, Type tabType)
        {
            DType = dataType;
            TabType = tabType;
        }

        public ActionImport.DataType DType { get; private set; }
        public Type TabType { get; private set; }
    }

    /// <summary>
    /// Checks that the Import Peptide Search action works correctly.
    /// </summary>
    [TestClass]
    public class StartPagePeptideSearchTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestStartPagePeptideSearch()
        {
            TestFilesZip = @"TestFunctional\StartPageTest.zip";
            RunFunctionalTest();
        }

        protected override bool ShowStartPage
        {
            get { return true; }
        }

        protected override void DoTest()
        {
            Directory.CreateDirectory(TestContext.TestDir);

            var startPage = WaitForOpenForm<StartPage>();
            RunUI(() => startPage.TestImportAction(ActionImport.DataType.peptide_search_dda,
                TestFilesDir.GetTestPath("StartPageTest.sky")));
            WaitForOpenForm<SkylineWindow>();
            var peptideSearchDlg = WaitForOpenForm<ImportPeptideSearchDlg>();
            OkDialog(peptideSearchDlg, peptideSearchDlg.CancelDialog);
        }
    }

    /// <summary>
    /// Checks that the Import Peptide Search action works correctly.
    /// </summary>
    [TestClass]
    public class StartPageShowPathChooser : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestStartPageShowPathChooser()
        {
            RunFunctionalTest();
        }

        protected override bool ShowStartPage
        {
            get { return true; }
        }

        protected override void DoTest()
        {
            var startPage = WaitForOpenForm<StartPage>();
            var pathChooser = ShowDialog<PathChooserDlg>(()=>startPage
                .TestTutorialAction(ActionTutorial.TutorialType.absolute_quantifiaction));
            OkDialog(pathChooser, pathChooser.Dispose);
            RunUI(() => startPage.DoAction(skylineWindow => true));
            WaitForOpenForm<SkylineWindow>();
        }
    }

    /// <summary>
    /// Checks that the Import Peptide Search action works correctly.
    /// </summary>
    [TestClass]
    public class StartPageShowFromSkyline : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestStartPageShowFromSkyline()
        {
            TestFilesZip = @"TestFunctional\StartPageTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            Directory.CreateDirectory(TestContext.TestDir);

            var skylineWindow = WaitForOpenForm<SkylineWindow>();
            var startPage = ShowDialog<StartPage>(skylineWindow.OpenStartPage);
            RunUI(() => Assert.AreEqual(startPage.SelectedTab, StartPage.TABS.Wizard));
            var peptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(() =>
                startPage.TestImportAction(ActionImport.DataType.peptide_search_dda,
                    TestFilesDir.GetTestPath("StartPageTest.sky")));
            OkDialog(peptideSearchDlg, peptideSearchDlg.CancelDialog);
            RunUI(() => SkylineWindow.SaveDocument());
            RunDlg<StartPage>(skylineWindow.OpenStartPageTutorial, start =>
            {
                Assert.IsNotNull(start);
                Assert.AreEqual(start.SelectedTab, StartPage.TABS.Tutorial);
                start.Close();
            });
        }
    }
}
