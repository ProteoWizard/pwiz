/*
 * Original author: Alex MacLean <brendanx .at. u.washington.edu>,
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

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class RefineProteinListDlgTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestRefineProteinListDlg()
        {
            TestFilesZip = @"TestFunctional\RetentionTimeFilterTest.zip";
            RunFunctionalTest();
        }

        private const string FirstProteinName = "sp|P02769|ALBU_BOVIN";
        private const string SecondProteinName = "sp|P41520|CCKN_BOVIN";
        private const string FirstProteinAccestion = "P02769";
        private const string SecondProteinPreferd = "ALBU_BOVIN";
        private const string SecondProteinAccestion = "P41520";
        private const string FirstProteinPreferd = "CCKN_BOVIN";

        protected override void DoTest()
        {
            //import some data
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("RetentionTimeFilterTest.sky")));
            WaitForDocumentLoaded();
            var docOriginal = SkylineWindow.Document;

            TestErrorMessages();
            TestName();

            SkylineWindow.SetDocument(docOriginal, SkylineWindow.Document);
            TestAccession();
            SkylineWindow.SetDocument(docOriginal, SkylineWindow.Document);
            TestPreferred();
        }

        private static void TestErrorMessages()
        {
            var document = SkylineWindow.Document;
            var dlgRefineList = ShowDialog<RefineProteinListDlg>(SkylineWindow.AcceptProteins);
           
            // None of the proteins are in the document
            RunUI(() => dlgRefineList.ProteinsText = TextUtil.LineSeparate("PEPTIDER", "ONCELER"));
            RunDlg<MessageDlg>(dlgRefineList.OkDialog, dlg =>
            {
                Assert.AreEqual(Resources.RefineListDlgProtein_OkDialog_None_of_the_specified_proteins_are_in_the_document_, dlg.Message);
                dlg.OkDialog();
            });
            // One proteins not found
            RunUI(() => dlgRefineList.ProteinsText = TextUtil.LineSeparate(FirstProteinName, "ONCELER"));
            RunDlg<MultiButtonMsgDlg>(dlgRefineList.OkDialog, dlg =>
            {
                AssertEx.AreComparableStrings(
                    Resources.RefineListDlgProtein_OkDialog_The_protein___0___is_not_in_the_document__Do_you_want_to_continue_, dlg.Message);
                dlg.CancelDialog();
            });
            // Multiple proteins not found
            RunUI(() => dlgRefineList.ProteinsText = TextUtil.LineSeparate(FirstProteinName, "ONCELER", "TICK"));
            RunDlg<MultiButtonMsgDlg>(dlgRefineList.OkDialog, dlg =>
            {
                Assert.IsTrue(
                    dlg.Message.StartsWith(Resources.RefineListDlgProtein_OkDialog_The_following_proteins_are_not_in_the_document_));
                Assert.IsTrue(dlg.Message.EndsWith(Resources.RefineListDlgProtein_OkDialog_Do_you_want_to_continue));
                dlg.CancelDialog();
            });
            // Many proteins not found
            var listPeptides = new List<string>();
            const string ProteinRand = "ONLOOKER";
            const int notFoundCount = 20;
            for (int i = 0; i < notFoundCount; i++)
            {
                listPeptides.Add(string.Join(string.Empty, ProteinRand.ToArray().RandomOrder().ToArray()));
            }
            listPeptides.Add(FirstProteinName);
            listPeptides.Add(SecondProteinName);
            RunUI(() => dlgRefineList.ProteinsText = TextUtil.LineSeparate(listPeptides));
            RunDlg<MultiButtonMsgDlg>(dlgRefineList.OkDialog, dlg =>
            {
                Assert.AreEqual(
                    string.Format(
                        Resources
                            .RefineListDlgProtein_OkDialog_Of_the_specified__0__proteins__1__are_not_in_the_document__Do_you_want_to_continue_,
                        listPeptides.Count, notFoundCount), dlg.Message);
                dlg.BtnCancelClick();
                dlgRefineList.Close();
            });
            WaitForClosedForm(dlgRefineList);
            // By the time we can get back on the UI thread, the form processing should have completed
            // without changing anything
            RunUI(() => Assert.AreSame(document, SkylineWindow.Document));
        }

        private static void TestName()
        {
            RunDlg<RefineProteinListDlg>(SkylineWindow.AcceptProteins, true,
                dlgRefineList =>
                {
                    dlgRefineList.ProteinsText = TextUtil.LineSeparate(FirstProteinName, SecondProteinName);
                    dlgRefineList.OkDialog();
                });
            Assert.AreEqual(SkylineWindow.Document.PeptideGroupCount, 2);
            foreach (var nodePep in SkylineWindow.Document.PeptideGroups)
            {
                Assert.IsTrue((nodePep.Name.Equals(FirstProteinName)|| nodePep.Name.Equals(SecondProteinName)));
            }
        }

        private static void TestAccession()
        {
            RunDlg<RefineProteinListDlg>(SkylineWindow.AcceptProteins, true,
               dlgRefineList =>
               {
                   dlgRefineList.Accession = true;
                   dlgRefineList.ProteinsText = TextUtil.LineSeparate(FirstProteinAccestion, SecondProteinAccestion);
                   dlgRefineList.OkDialog();
               });

            Assert.AreEqual(SkylineWindow.Document.PeptideGroupCount, 2);
            foreach (var nodePep in SkylineWindow.Document.PeptideGroups)
            {
                Assert.IsTrue((nodePep.ProteinMetadata.Accession.Equals(FirstProteinAccestion) || nodePep.ProteinMetadata.Accession.Equals(SecondProteinAccestion)));
            }
        }

        private static void TestPreferred()
        {
            RunDlg<RefineProteinListDlg>(SkylineWindow.AcceptProteins, true,
               dlgRefineList =>
               {
                   dlgRefineList.Preferred = true;
                   dlgRefineList.ProteinsText = TextUtil.LineSeparate(FirstProteinPreferd, SecondProteinPreferd);
                   dlgRefineList.OkDialog();
               });

            Assert.AreEqual(SkylineWindow.Document.PeptideGroupCount, 2);
            foreach (var nodePep in SkylineWindow.Document.PeptideGroups)
            {
                Assert.IsTrue((nodePep.ProteinMetadata.PreferredName.Equals(FirstProteinPreferd) || nodePep.ProteinMetadata.PreferredName.Equals(SecondProteinPreferd)));
            }
        }
    }
}