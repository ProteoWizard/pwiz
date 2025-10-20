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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.DocSettings;
using pwiz.SkylineTestUtil;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SequenceTreePropertyGridTest : AbstractFunctionalTest
    {
        private const int PEPTIDE_EXPECTED_PROP_NUM = 10;
        private const string SEQUENCE_PROP_NAME = "Sequence";
        private const string NOTE_PROP_NAME = "Note";

        [TestMethod]
        public void TestSequenceTreePropertyGrid()
        {
            TestFilesZipPaths = new[] { PropertyGridTestUtil.TEST_FILES_ZIP };

            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            VerifySetup();

            TestPeptideProperties();

            TestAnnotationProperties();

            TestEditProperty();

            CloseForms();
        }

        private void VerifySetup()
        {
            var documentPath = Path.Combine(TestFilesDirs[0].FullPath, @"Main", @"test.sky");
            RunUI(() => { SkylineWindow.OpenFile(documentPath); });

            Assert.IsNotNull(SkylineWindow.SequenceTree);
            Assert.IsTrue(SkylineWindow.SequenceTreeFormIsVisible);

            Assert.IsNull(SkylineWindow.PropertyGridForm);
            Assert.IsFalse(SkylineWindow.PropertyGridFormIsVisible);
            Assert.IsFalse(SkylineWindow.PropertyGridFormIsActivated);

            RunUI(() => { SkylineWindow.ShowPropertyGridForm(true); });
            WaitForConditionUI(() => SkylineWindow.PropertyGridFormIsVisible);
        }

        private static void TestPeptideProperties()
        {
            // select peptide node
            var peptideGroupTreeNode = SkylineWindow.SequenceTree.GetSequenceNodes().FirstOrDefault();
            Assert.IsNotNull(peptideGroupTreeNode);
            var peptideTreeNode = peptideGroupTreeNode.Nodes[0] as PeptideTreeNode;
            Assert.IsNotNull(peptideTreeNode);
            RunUI(() => { SkylineWindow.SequenceTree.SelectedNode = peptideTreeNode; });

            // get properties of selected node
            var selectedObject = SkylineWindow.PropertyGridForm.GetPropertyObject();
            Assert.IsTrue(selectedObject is Peptide);
            var props = TypeDescriptor.GetProperties(selectedObject, false);

            // Check that sequence property matches value in selected node
            Assert.AreEqual(peptideTreeNode.DocNode.Peptide.Sequence, props[SEQUENCE_PROP_NAME].GetValue(selectedObject));

            // smoke test check total number of properties - this failing could just mean another relevant property was added
            Assert.AreEqual(PEPTIDE_EXPECTED_PROP_NUM, props.Count);

            // Test localization of property grid property display names
            var nameProp = props[SEQUENCE_PROP_NAME];
            Assert.IsNotNull(nameProp);
            Assert.AreEqual(nameProp.Name, SEQUENCE_PROP_NAME);
            Assert.AreEqual(selectedObject.GetResourceManager().GetString(nameProp.Name), nameProp.DisplayName);
        }

        private static void TestAnnotationProperties()
        {
            // select peptide node
            var peptideGroupTreeNode = SkylineWindow.SequenceTree.GetSequenceNodes().FirstOrDefault();
            Assert.IsNotNull(peptideGroupTreeNode);
            var peptideTreeNode = peptideGroupTreeNode.Nodes[0] as PeptideTreeNode;
            Assert.IsNotNull(peptideTreeNode);
            RunUI(() => { SkylineWindow.SequenceTree.SelectedNode = peptideTreeNode; });
            
            // Get selected node and ensure it is a peptide
            var selectedObject = SkylineWindow.PropertyGridForm.GetPropertyObject();
            Assert.IsTrue(selectedObject is Peptide);

            // Add annotation definitions for peptides
            RunUI(() => { PropertyGridTestUtil.TestAddAnnotations(SkylineWindow, AnnotationDef.AnnotationTarget.peptide); });

            // Test if sum of original properties and new annotation definition properties appear
            selectedObject = SkylineWindow.PropertyGridForm.GetPropertyObject();
            var props = TypeDescriptor.GetProperties(selectedObject, false);
            Assert.AreEqual(PEPTIDE_EXPECTED_PROP_NUM + SkylineWindow.Document.Settings.DataSettings.AnnotationDefs.Count, props.Count);

            // Test editing the annotation properties
            RunUI(() => { PropertyGridTestUtil.TestEditAnnotations(SkylineWindow); });
        }

        private static void TestEditProperty()
        {
            // Select Peptide node
            var peptideGroupTreeNode = SkylineWindow.SequenceTree.GetSequenceNodes().FirstOrDefault();
            Assert.IsNotNull(peptideGroupTreeNode);
            var peptideTreeNode = peptideGroupTreeNode.Nodes[0] as PeptideTreeNode;
            Assert.IsNotNull(peptideTreeNode);
            RunUI(() => { SkylineWindow.SequenceTree.SelectedNode = peptideTreeNode; });
            var selectedObject = SkylineWindow.PropertyGridForm.GetPropertyObject();
            Assert.IsTrue(selectedObject is Peptide);

            // Edit the note property of the selected object (peptide) and verify the change was made on the object and UI
            RunUI(() => { PropertyGridTestUtil.TestEditProperty(SkylineWindow, NOTE_PROP_NAME, "EditedNote"); });
        }

        private static void CloseForms()
        {
            var sequenceTreeForm = FindOpenForm<SequenceTreeForm>();
            Assert.IsNotNull(sequenceTreeForm);
            OkDialog(sequenceTreeForm, sequenceTreeForm.Close);

            var propertyGridForm = FindOpenForm<PropertyGridForm>();
            Assert.IsNotNull(propertyGridForm);
            OkDialog(propertyGridForm, propertyGridForm.Close);
        }
    }
}
