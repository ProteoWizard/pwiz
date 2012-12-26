/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class EditSettingsTypesTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestEditSettingsTypes()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestEditEnzymeDlg();

            // TODO: Write more tests to cover uncovered Edit*Dlg in SettingsUI
        }

        private static void TestEditEnzymeDlg()
        {
            var enzymeList = Settings.Default.EnzymeList;
            Enzyme enzymeExpect = new Enzyme("Trypsin Test", "KR", "P");
            Enzyme enzyme = null;
            RunDlg<EditEnzymeDlg>(() => { enzyme = enzymeList.EditItem(SkylineWindow, null, enzymeList, null); },
                editEnzymeDlg =>
                    {
                        editEnzymeDlg.EnzymeName = enzymeExpect.Name;
                        editEnzymeDlg.Cleavage = enzymeExpect.CleavageC;
                        editEnzymeDlg.Restrict = enzymeExpect.RestrictC;
                        editEnzymeDlg.OkDialog();
                    });
            Assert.AreEqual(enzymeExpect, enzyme);
            
            RunDlg<EditEnzymeDlg>(() => { enzyme = enzymeList.EditItem(SkylineWindow, null, enzymeList, null); },
                editEnzymeDlg =>
                {
                    editEnzymeDlg.EnzymeName = enzymeExpect.Name;
                    editEnzymeDlg.Type = SequenceTerminus.N;
                    editEnzymeDlg.Cleavage = enzymeExpect.CleavageC;
                    editEnzymeDlg.Restrict = enzymeExpect.RestrictC;
                    editEnzymeDlg.OkDialog();
                });
            Assert.AreNotEqual(enzymeExpect, enzyme);
            Assert.AreEqual(enzymeExpect.CleavageC, enzyme.CleavageN);
            Assert.AreEqual(enzymeExpect.RestrictC, enzyme.RestrictN);
            Assert.IsTrue(enzyme.IsNTerm);

            Enzyme enzymeN = null;
            RunDlg<EditEnzymeDlg>(() => { enzymeN = enzymeList.EditItem(SkylineWindow, enzyme, enzymeList, null); },
                editEnzymeDlg =>
                {
                    Assert.AreEqual(enzyme.Name, editEnzymeDlg.EnzymeName);
                    Assert.AreEqual(enzyme.CleavageN, editEnzymeDlg.Cleavage);
                    Assert.AreEqual(enzyme.RestrictN, editEnzymeDlg.Restrict);
                    Assert.AreEqual(SequenceTerminus.N, editEnzymeDlg.Type);
                    editEnzymeDlg.OkDialog();
                });
            Assert.AreEqual(enzyme, enzymeN);

            RunDlg<EditEnzymeDlg>(() => { enzyme = enzymeList.EditItem(SkylineWindow, enzymeList.CopyItem(enzymeExpect), enzymeList, null); },
                editEnzymeDlg =>
                {
                    Assert.AreEqual(string.Empty, editEnzymeDlg.EnzymeName);
                    Assert.AreEqual(enzymeExpect.CleavageC, editEnzymeDlg.Cleavage);
                    Assert.AreEqual(enzymeExpect.RestrictC, editEnzymeDlg.Restrict);
                    Assert.AreEqual(SequenceTerminus.C, editEnzymeDlg.Type);
                    editEnzymeDlg.EnzymeName = enzymeExpect.Name;
                    editEnzymeDlg.OkDialog();
                });
            Assert.AreEqual(enzymeExpect, enzyme);

            Enzyme enzymeBoth = new Enzyme("Bidirectional", "M", null, "K", null);
            RunDlg<EditEnzymeDlg>(() => { enzyme = enzymeList.EditItem(SkylineWindow, null, enzymeList, null); },
                editEnzymeDlg =>
                {
                    editEnzymeDlg.EnzymeName = enzymeBoth.Name;
                    editEnzymeDlg.Type = null;
                    editEnzymeDlg.Cleavage = enzymeBoth.CleavageC;
                    editEnzymeDlg.CleavageN = enzymeBoth.CleavageN;
                    editEnzymeDlg.OkDialog();
                });
            Assert.AreEqual(enzymeBoth, enzyme);
            RunDlg<EditEnzymeDlg>(() => { enzyme = enzymeList.EditItem(SkylineWindow, enzymeBoth, enzymeList, null); },
                editEnzymeDlg =>
                {
                    Assert.IsNull(editEnzymeDlg.Type);
                    Assert.AreEqual(enzymeBoth.CleavageC, editEnzymeDlg.Cleavage);
                    Assert.AreEqual(string.Empty, editEnzymeDlg.Restrict);
                    Assert.AreEqual(enzymeBoth.CleavageN, editEnzymeDlg.CleavageN);
                    Assert.AreEqual(string.Empty, editEnzymeDlg.RestrictN);
                    editEnzymeDlg.OkDialog();
                });
            Assert.AreEqual(enzymeBoth, enzyme);

            // Test error messages
            {
                var editEnzymeDlg = ShowDialog<EditEnzymeDlg>(() => enzymeList.EditItem(SkylineWindow, null, enzymeList, null));
                RunDlg<MessageDlg>(editEnzymeDlg.OkDialog, messageDlg =>
                {
                    AssertEx.AreComparableStrings(Resources.MessageBoxHelper_ValidateNameTextBox__0__cannot_be_empty,
                        messageDlg.Message, 1);
                    messageDlg.OkDialog();
                });

                enzyme = enzymeList.First();
                RunUI(() => editEnzymeDlg.EnzymeName = enzyme.Name);
                RunDlg<MessageDlg>(editEnzymeDlg.OkDialog, messageDlg =>
                {
                    AssertEx.AreComparableStrings(Resources.EditEnzymeDlg_ValidateAATextBox__0__must_contain_at_least_one_amino_acid,
                        messageDlg.Message, 1);
                    messageDlg.OkDialog();
                });

                const string badAA = "Z";
                RunUI(() => editEnzymeDlg.Cleavage = enzyme.CleavageC + badAA);
                RunDlg<MessageDlg>(editEnzymeDlg.OkDialog, messageDlg =>
                {
                    Assert.AreEqual(string.Format(Resources.EditEnzymeDlg_ValidateAATextBox_The_character__0__is_not_a_valid_amino_acid, badAA),
                        messageDlg.Message);
                    messageDlg.OkDialog();
                });
                RunUI(() =>
                {
                    editEnzymeDlg.Cleavage = enzyme.CleavageC;
                    editEnzymeDlg.Restrict = badAA;
                });
                RunDlg<MessageDlg>(editEnzymeDlg.OkDialog, messageDlg =>
                {
                    Assert.AreEqual(string.Format(Resources.EditEnzymeDlg_ValidateAATextBox_The_character__0__is_not_a_valid_amino_acid, badAA),
                        messageDlg.Message);
                    messageDlg.OkDialog();
                });
                RunUI(() =>
                {
                    editEnzymeDlg.Type = null;
                    editEnzymeDlg.Restrict = enzyme.RestrictC;
                });
                RunDlg<MessageDlg>(editEnzymeDlg.OkDialog, messageDlg =>
                {
                    AssertEx.AreComparableStrings(Resources.EditEnzymeDlg_ValidateAATextBox__0__must_contain_at_least_one_amino_acid,
                        messageDlg.Message, 1);
                    messageDlg.OkDialog();
                });
                RunUI(() =>
                {
                    editEnzymeDlg.CleavageN = enzyme.CleavageC;
                    editEnzymeDlg.RestrictN = badAA;
                });
                RunDlg<MessageDlg>(editEnzymeDlg.OkDialog, messageDlg =>
                {
                    Assert.AreEqual(string.Format(Resources.EditEnzymeDlg_ValidateAATextBox_The_character__0__is_not_a_valid_amino_acid, badAA),
                        messageDlg.Message);
                    messageDlg.OkDialog();
                });

                RunUI(() => editEnzymeDlg.Type = SequenceTerminus.C);
                RunDlg<MessageDlg>(editEnzymeDlg.OkDialog, messageDlg =>
                {
                    AssertEx.AreComparableStrings(Resources.EditEnzymeDlg_OnClosing_The_enzyme__0__already_exists,
                        messageDlg.Message, 1);
                    messageDlg.AcceptButton.PerformClick();
                });

                RunUI(editEnzymeDlg.CancelButton.PerformClick);
                WaitForClosedForm(editEnzymeDlg);
            }
        }
    }
}