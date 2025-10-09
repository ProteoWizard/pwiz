/*
 * Original author: Aaron Banse <acbanse .at. icloud dot com>,
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

using System.ComponentModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SequenceTreePropertyGridTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSequenceTreePropertyGrid()
        {
            TestFilesZip = SequenceTreeRatioTest.TEST_FILES_ZIP;
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            VerifySetup();

            TestCurrentSelection();

            TestChangeSelection();

            TestEditProperty();

            // Destroy the property form to avoid test freezing
            RunUI(() =>
            {
                SkylineWindow.DestroyPropertyGridForm();
            });
        }

        private void VerifySetup()
        {
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath(SequenceTreeRatioTest.TEST_FILE_NAME));
            });

            Assert.IsNotNull(SkylineWindow.SequenceTree);
            Assert.IsTrue(SkylineWindow.SequenceTreeFormIsVisible);

            Assert.IsNull(SkylineWindow.PropertyGridForm);
            Assert.IsFalse(SkylineWindow.PropertyGridFormIsVisible);
            Assert.IsFalse(SkylineWindow.PropertyGridFormIsActivated);

            RunUI(() => { SkylineWindow.ShowPropertyGridForm(true); });
            WaitForConditionUI(() => SkylineWindow.PropertyGridFormIsVisible);
        }

        private static void TestCurrentSelection()
        {
            DocNode selectedDocNode = null;
            RunUI(() =>
            {
                selectedDocNode = ((SrmTreeNode)SkylineWindow.SequenceTree.SelectedNode).Model;
            });
            Assert.IsTrue(selectedDocNode is PeptideDocNode);

            var propertyGrid = SkylineWindow.PropertyGridForm;
            Assert.IsTrue(propertyGrid.GetPropertyObject() is Peptide);

            var props = TypeDescriptor.GetProperties(propertyGrid.GetPropertyObject());
            Assert.AreEqual(((PeptideDocNode)selectedDocNode).Peptide.Sequence, props["Sequence"]);


        }

        private static void TestChangeSelection()
        {

        }

        private static void TestEditProperty()
        {

        }
    }
}
