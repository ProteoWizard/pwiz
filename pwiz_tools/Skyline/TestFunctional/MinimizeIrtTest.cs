/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Irt;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that when doing "File > Share > Minimal", the minimized iRT database contains the
    /// peptides that it is supposed to.
    /// </summary>
    [TestClass]
    public class MinimizeIrtTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMinimizeIrt()
        {
            TestFilesZip = @"TestFunctional\MinimizeIrtTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("MinimizeIrtTest.sky")));
            var irtDatabasePath = TestFilesDir.GetTestPath("MinimizeIrtTestIrtdb.irtdb");
            Assert.IsTrue(File.Exists(irtDatabasePath), irtDatabasePath);

            WaitForDocumentLoaded();

            // Delete two peptides from the document.
            var peptidesToDelete = new[] {"AQPPEAGPQGLHDLGR", "GALQAVDQLSLFRPLCK"};
            var allPeptideSequences =
                SkylineWindow.Document.Molecules.Select(molecule => molecule.Peptide.Sequence).ToList();
            foreach (var peptide in peptidesToDelete)
            {
                CollectionAssert.Contains(allPeptideSequences, peptide);
                DeletePeptide(peptide);
            }

            // Verify that the iRT database contains all of the peptides that were in the document, including the two
            // that were deleted
            var rCalcIrt =
                SkylineWindow.Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator as RCalcIrt;
            Assert.IsNotNull(rCalcIrt);
            var irtPeptideSequences = rCalcIrt.GetDbIrtPeptides()
                .Select(peptide => FastaSequence.StripModifications(peptide.ModifiedTarget.Sequence)).ToHashSet();
            
            foreach (var peptide in allPeptideSequences)
            { 
                Assert.IsTrue(irtPeptideSequences.Contains(peptide), "{0} not found", peptide);
            }

            // File > Share > Minimal
            var skyZipPath = TestFilesDir.GetTestPath("SharedFile.sky.zip");
            RunUI(()=>
            {
                SkylineWindow.ShareDocument(skyZipPath, ShareType.MINIMAL);
            });

            // New document, say no to saving current document
            RunDlg<MultiButtonMsgDlg>(SkylineWindow.NewDocument, dlg=>dlg.ClickNo());
            
            // Open the .sky.zip that was just created
            RunUI(() =>
            {
                SkylineWindow.OpenSharedFile(skyZipPath);
            });
            WaitForDocumentLoaded();

            var minimizedDocumentFolder = Path.GetDirectoryName(SkylineWindow.DocumentFilePath);
            Assert.IsNotNull(minimizedDocumentFolder);
            var minimizedIrtPath = Path.Combine(minimizedDocumentFolder, "MinimizeIrtTestIrtdb.irtdb");
            Assert.IsTrue(File.Exists(minimizedIrtPath), "File {0} does not exist", minimizedIrtPath);
            
            // Read the peptides from the minimized iRT Database.
            // Note that this iRT db file is not actually being used by the current Skyline document. Because
            // of IrtDbManager._loadedCalculators, it is required that you exit Skyline if you want to change
            // the file location of a particular irt database.
            var minimizedIrtDb = IrtDb.GetIrtDb(minimizedIrtPath, null);
            irtPeptideSequences = minimizedIrtDb.GetPeptides()
                .Select(peptide => FastaSequence.StripModifications(peptide.ModifiedTarget.Sequence)).ToHashSet();
            // Verify that a peptide sequence can be found in the iRT database if and only if the peptide had not
            // been deleted from the document.
            foreach (var peptide in allPeptideSequences)
            {
                Assert.AreEqual(!peptidesToDelete.Contains(peptide), irtPeptideSequences.Contains(peptide), "Incorrect presence of peptide {0}", peptide);
            }
        }

        private void DeletePeptide(string sequence)
        {
            foreach (var protein in SkylineWindow.Document.MoleculeGroups)
            {
                foreach (var peptide in protein.Molecules)
                {
                    if (peptide.Peptide.Sequence == sequence)
                    {
                        RunUI(() =>
                        {
                            SkylineWindow.SelectedPath = new IdentityPath(protein.PeptideGroup, peptide.Peptide);
                            SkylineWindow.EditDelete();
                        });
                        return;
                    }
                }
            }
            Assert.Fail("Peptide {0} not found", sequence);
        }
    }
}
