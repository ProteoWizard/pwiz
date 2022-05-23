/*
 * Original author: Alana Killeen <killea .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Summary description for EditNoteTest
    /// </summary>
    [TestClass]
    public class EditNoteTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestEditNote()
        {
            TestFilesZip = @"TestFunctional\EditNoteTest.zip";
            RunFunctionalTest();
        }

        private const string PROTEINS_AND_PEPTIDES = "Proteins and Peptides";

        protected override void DoTest()
        {

            // Open the .sky file
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("CE_Vantage_15mTorr_scheduled_mini.sky")));

            // Select the first transition group.
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedPath =
                    SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.TransitionGroups, 0);
            });
            RunDlg<EditNoteDlg>(SkylineWindow.EditNote, editNoteDlg3 =>
            {
                // Since no annotation has been set yet, color index should be set to the default
                // value.
                Assert.AreEqual(Settings.Default.AnnotationColor, editNoteDlg3.ColorIndex);
                editNoteDlg3.OkDialog();
            });
            RunUI(() =>
                Assert.AreEqual(-1, ((SrmTreeNode) SkylineWindow.SequenceTree.SelectedNode).Model.Annotations.ColorIndex));
            // Select the first protein.
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedPaths = new List<IdentityPath>
                {
                    SkylineWindow.SequenceTree.SelectedPath,
                    SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.MoleculeGroups, 0)
                };
            });
            var editNoteDlg = ShowDialog<EditNoteDlg>(SkylineWindow.EditNote);
            RunUI(() =>
            {
                Assert.AreEqual(Settings.Default.AnnotationColor, editNoteDlg.ColorIndex);
                editNoteDlg.OkDialog();
            });
            WaitForClosedForm(editNoteDlg);
            // Select just the first protein.
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedPath = 
                    SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.MoleculeGroups, 0);
            });
            var editNoteDlg4 = ShowDialog<EditNoteDlg>(SkylineWindow.EditNote);
            RunUI(() =>
            {
                Assert.IsFalse(editNoteDlg4.GetAnnotations().Contains(annotation => !string.IsNullOrEmpty(annotation.Value)));
                Assert.IsTrue(string.IsNullOrEmpty(editNoteDlg4.GetText()));
            });
        
            // Set annotations.
            Assert.IsTrue(SetAnnotationValue(editNoteDlg4, PROTEINS_AND_PEPTIDES, true));
            RunUI(() =>
            {
                editNoteDlg4.ColorIndex = 3;
                editNoteDlg4.NoteText = "Text";
                editNoteDlg4.OkDialog();
            });
            WaitForClosedForm(editNoteDlg4);
            RunDlg<EditNoteDlg>(SkylineWindow.EditNote, editNoteDlg0 =>
            {
                // Test annotations set correctly.
                Assert.AreEqual(PROTEINS_AND_PEPTIDES, editNoteDlg0.GetAnnotations()[0].Key);
                Assert.IsNull(editNoteDlg0.GetChangedAnnotations());
                Assert.AreEqual(3, editNoteDlg0.ColorIndex);
                Assert.IsNull(editNoteDlg0.GetText());
                // Change annotation.
                editNoteDlg0.NoteText = "New Text";
                editNoteDlg0.OkDialog();
            });
            // Test unchanged fields keep their original values.
            RunUI(() =>
            {
                var selNode = (SrmTreeNode) SkylineWindow.SequenceTree.SelectedNode;
                var annotations = selNode.Model.Annotations;
                Assert.AreEqual(PROTEINS_AND_PEPTIDES, annotations.ListAnnotations()[0].Key);
                Assert.IsFalse(string.IsNullOrEmpty(annotations.ListAnnotations()[0].Value));
                Assert.AreEqual(3, annotations.ColorIndex);
                Assert.AreEqual("New Text", annotations.Note);
            });
          
            // Select the first peptide.
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedPath =
                    SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
            });
            // Set matching annotations to match the protein, except for the noteText which is different.
            var doc = SkylineWindow.Document;
            var editNoteDlg1 = ShowDialog<EditNoteDlg>(SkylineWindow.EditNote);
            Assert.IsTrue(SetAnnotationValue(editNoteDlg1, PROTEINS_AND_PEPTIDES, true));
            RunUI(() =>
            {
                editNoteDlg1.ColorIndex = 3;
                editNoteDlg1.NoteText = "Text";
                editNoteDlg1.OkDialog();
                // Select the first protein again, also keeping the peptide selected.
                SkylineWindow.SequenceTree.SelectedPaths = new List<IdentityPath>
                {
                    SkylineWindow.SequenceTree.SelectedPath, 
                    SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.MoleculeGroups, 0)
                };
            });
            WaitForClosedForm(editNoteDlg1);
            WaitForDocumentChange(doc);
            RunDlg<EditNoteDlg>(SkylineWindow.EditNote, editNoteDlg2 =>
            {
                // Test annotation values for multiple nodes merge correctly.
                Assert.AreEqual(3, editNoteDlg2.ColorIndex);
                Assert.IsTrue(string.IsNullOrEmpty(editNoteDlg2.NoteText));
                // Should only be showing the annotation that matches peptides and proteins, not the annotation
                // that matches both proteins and peptides.
                Assert.AreEqual(1, editNoteDlg2.DataGridView.Rows.Count);
                Assert.IsTrue(!string.IsNullOrEmpty(editNoteDlg2.GetAnnotations()[0].Value));
                editNoteDlg2.OkDialog();
            });
        }

        private static bool SetAnnotationValue(EditNoteDlg editNoteDlg, String annotationName, Object value)
        {
            for (int i = 0; i < editNoteDlg.DataGridView.Rows.Count; i++)
            {
                var row = editNoteDlg.DataGridView.Rows[i];
                if (!annotationName.Equals(row.Cells[0].Value))
                {
                    continue;
                }
                row.Cells[1].Value = value;
                return true;
            }
            return false;
        }
    }
}
