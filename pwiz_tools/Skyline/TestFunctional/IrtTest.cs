﻿/*
 * Original author: John Chilton <jchilton .at. u.washington.edu>,
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
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class IrtTest : AbstractFunctionalTestEx
    {
        [TestMethod,
         NoLeakTesting(TestExclusionReason.EXCESSIVE_TIME)] // Don't leak test this - it takes a long time to run even once
        public void IrtFunctionalTest()
        {
            TestFilesZip = @"TestFunctional\IrtTest.zip";
            RunFunctionalTest();
        }
        private MeasuredPeptide BuildMeasuredPeptide(string seq, double rt)
        {
            return new MeasuredPeptide(new Target(seq), rt);
        }

        protected override void DoTest()
        {
            RunIrtTest();
            RunCalibrationTest();
        }

        private void RunIrtTest()
        {
            const int numStandardPeps = 11;
            const int numLibraryPeps = 18;
            const string irtCalc = "iRT-C18";
            const string ssrCalc = "SSRCalc 3.0 (300A)";

            var testFilesDir = new TestFilesDir(TestContext, TestFilesZip);
            string databasePath = testFilesDir.GetTestPath("irt-c18.irtdb");

            string documentPath = testFilesDir.GetTestPath("iRT Test.sky");
            RunUI(() => SkylineWindow.OpenFile(documentPath));

            var peptideSettingsDlg1 = ShowDialog<PeptideSettingsUI>(
                () => SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Prediction));
            var editRT1 = ShowDialog<EditRTDlg>(peptideSettingsDlg1.AddRTRegression);
            var irtDlg1 = ShowDialog<EditIrtCalcDlg>(editRT1.AddCalculator);
            
            RunUI(() =>
                {
                    irtDlg1.CalcName = irtCalc;
                    irtDlg1.CreateDatabase(databasePath);
                });

            RunUI(() =>
            {
                SetClipboardText(BuildStandardText(new[]
                {
                    BuildMeasuredPeptide("AAA", -10.00),
                    BuildMeasuredPeptide("CCC", 0.00),
                    BuildMeasuredPeptide("DDD", 10.00),
                }, seq => seq.Substring(0, seq.Length - 1)));
                irtDlg1.DoPasteStandard();
            });
            var calcToStandardDlg = ShowDialog<UseCurrentCalculatorDlg>(irtDlg1.AddStandard);
            const string newStandardName = "testCalcToStandard";
            RunUI(() => calcToStandardDlg.StandardNameText = newStandardName);
            OkDialog(calcToStandardDlg, calcToStandardDlg.OkDialog);
            RunUI(() =>
            {
                Assert.AreEqual(newStandardName, irtDlg1.IrtStandards.Name);
                // set back to none and make sure standards are cleared
                irtDlg1.IrtStandards = null;
                Assert.IsFalse(irtDlg1.StandardPeptides.Any());
            });

            /*
             * Check several error handling cases
             * Check the peptide choosing algorithm for sanity (correct # peptides)
             * Check that the info dialog comes up when too many are requested
             * Check the peptide linear transformation for sanity
             * Check that the peptides get passed back to EditIrtCalcDlg
             */
            var calibrateDlg = ShowDialog<CalibrateIrtDlg>(irtDlg1.Calibrate);

            //Check the peptide choosing algorithm
            int peptideCount = SkylineWindow.Document.PeptideCount; //29 peptides in the document

            //Use the dialog box UI
            var countDlg = ShowDialog<AddIrtStandardsDlg>(calibrateDlg.UseResults);
            RunUI(() => countDlg.StandardCount = CalibrateIrtDlg.MIN_STANDARD_PEPTIDES - 1);
            RunDlg<MessageDlg>(countDlg.OkDialog, messageDlg => messageDlg.OkDialog());
            RunUI(() => countDlg.StandardCount = peptideCount);
            OkDialog(countDlg, countDlg.OkDialog);
            TryWaitForConditionUI(() => peptideCount == calibrateDlg.StandardPeptideCount);
            RunUI(() => Assert.AreEqual(peptideCount, calibrateDlg.StandardPeptideCount));

            //Bypass the UI
            foreach (int i in new[]
                                  {
                                      peptideCount,
                                      peptideCount/2,
                                      CalibrateIrtDlg.MIN_STANDARD_PEPTIDES*2,
                                      CalibrateIrtDlg.MIN_STANDARD_PEPTIDES
                                  })
            {
                int j = i;
                RunUI(() =>
                          {
                              var calibratePeptides = calibrateDlg.Recalculate(SkylineWindow.Document, j);
                              Assert.AreEqual(calibratePeptides.Count, j);
                              var messageDlg = FindOpenForm<MessageDlg>();
                              if (messageDlg != null)
                                  Assert.Fail($@"Found open alert with the message '{messageDlg.Message}'");
                          });
            }

            RunUI(() =>
                      {
                          calibrateDlg.Recalculate(SkylineWindow.Document, 11);
                          //After closing this dialog, there should be 3 iRT values below 0
                          //and 3 above 100
                          calibrateDlg.SetFixedPoints(3, 7);
                          calibrateDlg.StandardName = "Document1";
                      });
            OkDialog(calibrateDlg, calibrateDlg.OkDialog);

            //Now check that the peptides were passed to the EditIrtCalcDlg
            RunUI(() =>
                      {
                          Assert.AreEqual(numStandardPeps, irtDlg1.StandardPeptideCount);
                          //And that there are 3 below 0 and 3 above 100
                          Assert.AreEqual(3, irtDlg1.StandardPeptides.Count(pep => Math.Round(pep.Irt, 2) < 0));
                          Assert.AreEqual(3, irtDlg1.StandardPeptides.Count(pep => Math.Round(pep.Irt, 2) > 100));
                          irtDlg1.ClearStandardPeptides();
                      });

            /*
             * Test pasting into EditIrtCalcDlg
             * Test that the dialog requires the whole standard to be in the document 
             * Test that add results gets everything in the document besides the standard
             * Test that there were no errors along the way
             */

            //Now paste in iRT with each peptide truncated by one amino acid
            var standard = new[]
                               {
                                   BuildMeasuredPeptide("LGGNEQVTR", -24.92),
                                   BuildMeasuredPeptide("GAGSSEPVTGLDAK", 0.00),
                                   BuildMeasuredPeptide("VEATFGVDESNAK", 12.39),
                                   BuildMeasuredPeptide("YILAGVENSK", 19.79),
                                   BuildMeasuredPeptide("TPVISGGPYEYR", 28.71),
                                   BuildMeasuredPeptide("TPVITGAPYEYR", 33.38),
                                   BuildMeasuredPeptide("DGLDAASYYAPVR", 42.26),
                                   BuildMeasuredPeptide("ADVTPADFSEWSK", 54.62),
                                   BuildMeasuredPeptide("GTFIIDPGGVIR", 70.52),
                                   BuildMeasuredPeptide("GTFIIDPAAVIR", 87.23),
                                   BuildMeasuredPeptide("LFLQFGAQGSPFLK", 100.00),
                               };

            RunUI(() =>
                      {
                          string standardText = BuildStandardText(standard, seq => seq.Substring(0, seq.Length - 1));
                          SetClipboardText(standardText);
                          irtDlg1.DoPasteStandard();
                      });

            // Cannot add results because standard peptides are not in the document
            RunDlg<AddIrtPeptidesDlg>(irtDlg1.AddResults, messageDlg => messageDlg.OkDialog());

            // Paste Biognosys-provided values
            RunUI(() =>
                      {
                          string standardText = BuildStandardText(standard);
                          SetClipboardText(standardText);
                          irtDlg1.ClearStandardPeptides();
                          irtDlg1.DoPasteStandard();

                          //Check count
                          Assert.AreEqual(numStandardPeps, irtDlg1.StandardPeptideCount);
                      });

            //Add results
            var addPeptidesDlg1 = ShowDialog<AddIrtPeptidesDlg>(irtDlg1.AddResults);
            var recalibrateDlg1 = ShowDialog<MultiButtonMsgDlg>(addPeptidesDlg1.OkDialog);
            OkDialog(recalibrateDlg1, recalibrateDlg1.Btn1Click);

            RunUI(() => Assert.AreEqual(numLibraryPeps, irtDlg1.LibraryPeptideCount));

            // Recalibrate
            const int shift = 100;
            const int skew = 10;
            var recalcDlg1 = ShowDialog<CalibrateIrtDlg>(irtDlg1.Calibrate);
            RunUI(() =>
            {
                recalcDlg1.SetIrtRange(standard[1].RetentionTime + shift, standard[standard.Length - 1].RetentionTime * skew + shift);
                recalcDlg1.SetFixedPoints(1, standard.Length - 1);
            });
            OkDialog(recalcDlg1, recalcDlg1.OkDialog);
            RunUI(() =>
            {
                for (int i = 0; i < irtDlg1.StandardPeptideCount; i++)
                {
                    Assert.AreEqual(standard[i].RetentionTime*skew + shift,
                                    irtDlg1.StandardPeptides.Skip(i).First().Irt);
                }
            });
            var recalcDlg2 = ShowDialog<CalibrateIrtDlg>(irtDlg1.Calibrate);
            RunUI(() =>
            {
                recalcDlg2.SetIrtRange(standard[2].RetentionTime, standard[standard.Length - 2].RetentionTime);
                recalcDlg2.SetFixedPoints(2, standard.Length - 2);
            });
            OkDialog(recalcDlg2, recalcDlg2.OkDialog);

            // Change peptides
            var changePeptides = irtDlg1.LibraryPeptides.Where((p, i) => i%2 == 0).ToArray();
            var resetPeptides = irtDlg1.StandardPeptides.ToArray();
            var changeDlg1 = ShowDialog<ChangeIrtPeptidesDlg>(irtDlg1.ChangeStandardPeptides);
            RunUI(() =>
            {
                // Check that the dialog detected that all of the standards are in a protein and selected it
                var standards = new TargetMap<bool>(irtDlg1.StandardPeptides.Select(pep => new KeyValuePair<Target, bool>(pep.ModifiedTarget, true)));
                Assert.IsTrue(changeDlg1.SelectedProtein.Peptides.All(pep => standards.ContainsKey(pep.ModifiedTarget)));

                // Check that selecting each protein correctly sets the text
                Assert.IsTrue(ArrayUtil.ReferencesEqual(SkylineWindow.DocumentUI.MoleculeGroups.ToArray(), changeDlg1.Proteins.ToArray()));
                foreach (var protein in changeDlg1.Proteins)
                {
                    changeDlg1.SelectedProtein = protein;
                    CollectionAssert.AreEqual(protein.Molecules.Select(pep => pep.ModifiedSequenceDisplay).ToArray(), changeDlg1.PeptideLines);
                }

                changeDlg1.SelectedProtein = null;
                Assert.IsTrue(string.IsNullOrEmpty(changeDlg1.PeptidesText));
            });
            const int useResultsCount = 12;
            RunDlg<AddIrtStandardsDlg>(changeDlg1.UseResults, dlg =>
            {
                dlg.StandardCount = useResultsCount;
                dlg.OkDialog();
            });
            RunUI(() => {
                Assert.AreEqual(useResultsCount, changeDlg1.PeptideLines.Length);
                changeDlg1.Peptides = changePeptides;
            });
            OkDialog(changeDlg1, changeDlg1.OkDialog);
            Assert.IsTrue(ArrayUtil.EqualsDeep(changePeptides.Select(p => p.Target).ToArray(),
                irtDlg1.StandardPeptides.Select(p => p.Target).ToArray()));
            Assert.IsTrue(ArrayUtil.EqualsDeep(changePeptides.Select(p => p.Irt).ToArray(),
                irtDlg1.StandardPeptides.Select(p => p.Irt).ToArray()));

            RunDlg<ChangeIrtPeptidesDlg>(irtDlg1.ChangeStandardPeptides, changeDlg =>
            {
                changeDlg.Peptides = resetPeptides;
                changeDlg.OkDialog();
            });

            OkDialog(irtDlg1, irtDlg1.OkDialog);

            Assert.IsNull(FindOpenForm<MessageDlg>());

            /*
             * Check that the database was created successfully
             * Check that it has the correct numbers of standard and library peptides
             */
            IrtDb db = IrtDb.GetIrtDb(databasePath, null);

            Assert.AreEqual(numStandardPeps, db.StandardPeptideCount);
            Assert.AreEqual(numLibraryPeps, db.LibraryPeptideCount);

            /*
             * Make sure that loading the database brings back up the right numbers of peptides
             */

            //Rather than rigging SettingsListComboDriver, just create a new one and load
            var irtDlg1A = ShowDialog<EditIrtCalcDlg>(editRT1.AddCalculator);

            RunDlg<MessageDlg>(() => irtDlg1A.OpenDatabase(testFilesDir.GetTestPath("bogus.irtdb")),
                messageDlg => messageDlg.OkDialog());

            //There was a _bug_ where opening a path and then clicking OK would save all the peptides
            //twice, doubling the size of the database. So check that that is fixed.
            EditIrtCalcDlgPepCountTest(irtDlg1A, numStandardPeps, numLibraryPeps, databasePath, false);
            EditIrtCalcDlgPepCountTest(ShowDialog<EditIrtCalcDlg>(editRT1.AddCalculator),
                numStandardPeps, numLibraryPeps, databasePath, false);
            EditIrtCalcDlgPepCountTest(ShowDialog<EditIrtCalcDlg>(editRT1.AddCalculator),
                numStandardPeps, numLibraryPeps, databasePath, true);
            EditIrtCalcDlgPepCountTest(ShowDialog<EditIrtCalcDlg>(editRT1.AddCalculator),
                numStandardPeps, numLibraryPeps, databasePath, false);

            /* 
             * Create a regression based on the new calculator
             * Create a regression based on SSRCalc
             * Open the graph
             * Switch to new calculator, verify r = 1.00 and graph is labeled iRT-C18
             * Switch to SSRCalc, verify graph label changes
             */

            RunUI(() =>
                      {
                          editRT1.SetRegressionName("iRT Regression");
                          editRT1.AddResults();
                          editRT1.ChooseCalculator(irtCalc);
                      });
            OkDialog(editRT1, editRT1.OkDialog);

            var editRT1A = ShowDialog<EditRTDlg>(peptideSettingsDlg1.AddRTRegression);

            RunUI(() =>
                      {
                          editRT1A.SetRegressionName("SSRCalc Regression");
                          editRT1A.AddResults();
                          editRT1A.ChooseCalculator(ssrCalc);
                      });

            RunDlg<RTDetails>(editRT1A.ShowDetails, detailsDlg => detailsDlg.Close());

            OkDialog(editRT1A, editRT1A.OkDialog);
            OkDialog(peptideSettingsDlg1, peptideSettingsDlg1.CancelButton.PerformClick);

            var docPeptides = new List<MeasuredRetentionTime>();
            RunUI(() =>
                      {
                          var document = Program.ActiveDocumentUI;
                          foreach (var docPepNode in document.Peptides)
                          {
                              docPeptides.Add(new MeasuredRetentionTime(document.Settings.GetModifiedSequence(docPepNode),
                                                                        docPepNode.AverageMeasuredRetentionTime ?? 0));
                          }
                      });

            RetentionTimeStatistics stats = null;
            RegressionLineElement line = null;
            RunUI(() => SkylineWindow.ShowRTRegressionGraphScoreToRun());
            WaitForRegression();
            RunUI(() =>
            {
                SkylineWindow.SetupCalculatorChooser();
                SkylineWindow.ChooseCalculator(irtCalc);
            });
            WaitForRegression();
            RunUI(() =>
            {
                stats = SkylineWindow.RTGraphController.RegressionRefined.CalcStatistics(docPeptides, null);
                line = SkylineWindow.RTGraphController.RegressionRefined.Conversion as RegressionLineElement;
            });
            Assert.IsNotNull(stats);
            Assert.AreEqual(1.0, stats.R, 0.001);
            Assert.IsNotNull(line);
            //These values were taken from the graph, which shows values to 2 decimal places, so the real values must
            //be +/- 0.01 from these values
            Assert.AreEqual(14.17, line.Intercept, 0.01);
            Assert.AreEqual(0.15, line.Slope, 0.01);

            RunUI(() => SkylineWindow.ChooseCalculator(ssrCalc));
            WaitForRegression();
            RunUI(() => stats = SkylineWindow.RTGraphController.RegressionRefined.CalcStatistics(docPeptides, null));

            Assert.IsNotNull(stats);
            Assert.AreEqual(0.97, stats.R, 0.01);

            /*
             * Delete all peptides except the standard
             * Create a new calculator with the document standard (instead of pasting)
             * Create a regression with that calculator
             * Export a transition list using measured retention times
             * Export a method using predicted retention times
             * Test that they are identical
             */

            //Delete from the document all but the last protein, which has all the standard peptides
            RunUI(() =>
                      {
                          SkylineWindow.CollapseProteins();
                          var seqTree = SkylineWindow.SequenceTree;
                          seqTree.SelectedNode = seqTree.Nodes[0];
                          seqTree.KeysOverride = Keys.Shift;
                          seqTree.SelectedNode = seqTree.Nodes[17];
                          SkylineWindow.EditDelete();
                      });

            //Peptide settings dialog -> Add a regression
            var peptideSettingsDlg2 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var editRT2 = ShowDialog<EditRTDlg>(peptideSettingsDlg2.AddRTRegression);

            //Set regression name
            RunUI(() => editRT2.SetRegressionName("iRT Document Regression"));

            //Regression dialog -> add a calculator
            var irtDlg2 = ShowDialog<EditIrtCalcDlg>(editRT2.AddCalculator);

            //Set calc name, database
            RunUI(() =>
                      {
                          irtDlg2.CalcName = "iRT Document Calculator";
                          irtDlg2.CreateDatabase(testFilesDir.GetTestPath("irt-doc.irtdb"));
                      });

            //Calc dialog -> calibrate standard
            RunDlg<CalibrateIrtDlg>(irtDlg2.Calibrate, calibrateDlg2 =>
            {
                //Get 11 peptides from the document (all of them) and go back to calculator dialog
                calibrateDlg2.Recalculate(SkylineWindow.Document, 11);
                calibrateDlg2.StandardName = "Document2";
                calibrateDlg2.OkDialog();
            });

            Assert.AreEqual(11, irtDlg2.StandardPeptideCount);
            Assert.AreEqual(0, irtDlg2.LibraryPeptideCount);

            // Close dialog to save calculator
            OkDialog(irtDlg2, irtDlg2.OkDialog);

            // Test adding irt calculator
            {
                var irtDlgAdd = ShowDialog<EditIrtCalcDlg>(editRT2.EditCurrentCalculator);
                var addDlg = ShowDialog<AddIrtCalculatorDlg>(irtDlgAdd.AddIrtDatabase);

                // Check error messages
                RunUI(() => addDlg.Source = IrtCalculatorSource.file);
                RunDlg<MessageDlg>(addDlg.OkDialog, messageDlg =>
                {
                    Assert.AreEqual(Resources.AddIrtCalculatorDlg_OkDialog_Please_specify_a_path_to_an_existing_iRT_database, messageDlg.Message);
                    messageDlg.OkDialog();
                });
                RunUI(() => addDlg.FilePath = "not_irtdb.docx");
                RunDlg<MessageDlg>(addDlg.OkDialog, messageDlg =>
                {
                    AssertEx.AreComparableStrings(Resources.AddIrtCalculatorDlg_OkDialog_The_file__0__is_not_an_iRT_database, messageDlg.Message);
                    messageDlg.OkDialog();
                });
                RunUI(() => addDlg.FilePath = "not_exist.irtdb");
                RunDlg<MessageDlg>(addDlg.OkDialog, messageDlg =>
                {
                    AssertEx.AreComparableStrings(TextUtil.LineSeparate(
                        Resources.AddIrtCalculatorDlgOkDialogThe_file__0__does_not_exist,
                        Resources.AddIrtCalculatorDlg_OkDialog_Please_specify_a_path_to_an_existing_iRT_database), messageDlg.Message);
                    messageDlg.OkDialog();
                });
                RunUI(() => addDlg.CalculatorName = "noexist");
                PauseForScreenShot();
                RunDlg<MessageDlg>(addDlg.OkDialog, messageDlg =>
                {
                    Assert.AreEqual(Resources.AddIrtCalculatorDlg_OkDialog_Please_choose_the_iRT_calculator_you_would_like_to_add, messageDlg.Message);
                    messageDlg.OkDialog();
                });

                RunUI(() => addDlg.FilePath = databasePath);
                var addPepDlg = ShowDialog<AddIrtPeptidesDlg>(addDlg.OkDialog);
                var recalibrateDlg = ShowDialog<MultiButtonMsgDlg>(addPepDlg.OkDialog);
                OkDialog(recalibrateDlg, recalibrateDlg.Btn1Click);
                Assert.AreEqual(18, irtDlgAdd.LibraryPeptideCount);

                OkDialog(irtDlgAdd, irtDlgAdd.CancelButton.PerformClick);
            }

            // Add from existing calculator
            {
                var irtDlgAdd = ShowDialog<EditIrtCalcDlg>(editRT2.EditCurrentCalculator);
                var addDlg = ShowDialog<AddIrtCalculatorDlg>(irtDlgAdd.AddIrtDatabase);
                RunUI(() => addDlg.CalculatorName = irtCalc);
                var addPepDlg = ShowDialog<AddIrtPeptidesDlg>(addDlg.OkDialog);
                var recalibrateDlg = ShowDialog<MultiButtonMsgDlg>(addPepDlg.OkDialog);
                OkDialog(recalibrateDlg, recalibrateDlg.Btn1Click);
                Assert.AreEqual(18, irtDlgAdd.LibraryPeptideCount);

                OkDialog(irtDlgAdd, irtDlgAdd.CancelButton.PerformClick);
            }


            var docIrtBefore = SkylineWindow.Document;

            OkDialog(editRT2, editRT2.OkDialog);
            OkDialog(peptideSettingsDlg2, peptideSettingsDlg2.OkDialog);
            WaitForClosedForm(peptideSettingsDlg2);

            var docIrt = VerifyIrtStandards(docIrtBefore, true);

            RunUI(() =>
            {
                // Select 3 of the standards and delete them
                SkylineWindow.SequenceTree.KeysOverride = Keys.None;
                SkylineWindow.SelectedPath = SkylineWindow.DocumentUI.GetPathTo((int)SrmDocument.Level.Molecules, 0);
                SkylineWindow.SequenceTree.KeysOverride = Keys.Shift;
                SkylineWindow.SelectedPath = SkylineWindow.DocumentUI.GetPathTo((int)SrmDocument.Level.Molecules, 2);
                SkylineWindow.SequenceTree.KeysOverride = Keys.None;
                SkylineWindow.EditDelete();
            });

            // Should still have iRT peptides
            docIrt = VerifyIrtStandards(docIrt, true);             
            // Remove one more and standards should be cleared
            RunUI(SkylineWindow.Cut);
            docIrt = VerifyIrtStandards(docIrt, false);
            RunUI(SkylineWindow.Paste);
            docIrt = VerifyIrtStandards(docIrt, true);             
            
            // Repeat without results
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, dlg =>
            {
                dlg.RemoveAllReplicates();
                dlg.OkDialog();
            });

            docIrt = VerifyIrtStandards(docIrt, true);
            RunUI(SkylineWindow.EditDelete);
            docIrt = VerifyIrtStandards(docIrt, false);
            RunUI(SkylineWindow.Paste);
            VerifyIrtStandards(docIrt, true);             

            //Restore the document to contain all 29 peptides
            RunUI(() => SkylineWindow.UndoRestore(7));

            //Open peptide settings
            var peptideSettingsDlg3 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunDlg<EditListDlg<SettingsListBase<RetentionTimeRegression>, RetentionTimeRegression>>(peptideSettingsDlg3.EditRegressionList,
                dlg =>
                {
                    dlg.SelectLastItem();
                    dlg.RemoveItem();
                    dlg.OkDialog();
                });

            //Add a new regression
            var editRT3 = ShowDialog<EditRTDlg>(peptideSettingsDlg3.AddRTRegression);

            RunUI(() => editRT3.SetRegressionName("iRT Document Regression"));

            //Edit the calculator list
            var editCalculator =
                ShowDialog<EditListDlg<SettingsListBase<RetentionScoreCalculatorSpec>, RetentionScoreCalculatorSpec>>(
                    editRT3.EditCalculatorList);

            RunUI(() => editCalculator.SelectItem("iRT Document Calculator"));

            //Edit the document-based calculator
            var irtDlg3 = ShowDialog<EditIrtCalcDlg>(editCalculator.EditItem);

            //Add the 18 non-standard peptides to the calculator, then OkDialog back to Skyline
            var addPeptidesDlg = ShowDialog<AddIrtPeptidesDlg>(irtDlg3.AddResults);
            var recalibrateDlg2 = ShowDialog<MultiButtonMsgDlg>(addPeptidesDlg.OkDialog);
            OkDialog(recalibrateDlg2, recalibrateDlg2.Btn1Click);

            RunUI(() => Assert.AreEqual(18, irtDlg3.LibraryPeptideCount));
            OkDialog(irtDlg3, irtDlg3.OkDialog);
            
            OkDialog(editCalculator, editCalculator.OkDialog);

            RunUI(() =>
                      {
                          editRT3.AddResults();
                          editRT3.ChooseCalculator("iRT Document Calculator");
                          editRT3.SetTimeWindow(2.0);
                      });
            OkDialog(editRT3, editRT3.OkDialog);

            //Then choose the new, document-based regression and turn off prediction
            RunUI(() =>
                      {
                          peptideSettingsDlg3.ChooseRegression("iRT Document Regression");
                          peptideSettingsDlg3.IsUseMeasuredRT = true;
                      });
            
            OkDialog(peptideSettingsDlg3, peptideSettingsDlg3.OkDialog);

            Assert.IsNull(FindOpenForm<MessageDlg>());

            //Export the measurement-based transition list
            ExportMethod(testFilesDir.GetTestPath("EmpiricalTL.csv"));

            //Turn on prediction for scheduling
            var peptideSettingsDlg4 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsDlg4.IsUseMeasuredRT = false);
            OkDialog(peptideSettingsDlg4, peptideSettingsDlg4.OkDialog);

            //Export the prediction-based transition list 
            ExportMethod(testFilesDir.GetTestPath("PredictionTL.csv"));

            //Now open both files and compare
            AssertEx.NoDiff(File.ReadAllText(testFilesDir.GetTestPath("EmpiricalTL.csv")),
                            File.ReadAllText(testFilesDir.GetTestPath("PredictionTL.csv")));

            // Close and re-open, and try again
            RunUI(() =>
                      {
                          Assert.IsTrue(SkylineWindow.SaveDocument());
                          SkylineWindow.NewDocument();
                          Assert.IsTrue(SkylineWindow.OpenFile(documentPath));
                      });

            WaitForDocumentLoaded();

            ExportMethod(testFilesDir.GetTestPath("PredictionTL_reopen.csv"));

            AssertEx.NoDiff(File.ReadAllText(testFilesDir.GetTestPath("EmpiricalTL.csv")),
                            File.ReadAllText(testFilesDir.GetTestPath("PredictionTL_reopen.csv")));
            
            /*
             * Rename the database
             * Switch to the calculator in EditRTDlg and check for an error
             * Go to edit the calculator and check for an error
             * Try to export a transition list and check for an error
             * Switch to the calculator in the graph and check for an error
             */
            var stream = File.Create(testFilesDir.GetTestPath("irt-c18-copy.irtdb"));
            stream.Close();
            File.Replace(databasePath, testFilesDir.GetTestPath("irt-c18-copy.irtdb"),
                         testFilesDir.GetTestPath("backup.irtdb"));

            // The database renaming doesn't break usage anymore, since
            // the iRT databases are loaded into memory during initialization.
            // So, create a new calculator with a bogus path.
            const string irtCalcMissing = "iRT-C18-missing";
            Settings.Default.RTScoreCalculatorList.SetValue(new RCalcIrt(irtCalcMissing, testFilesDir.GetTestPath("irt-c18-missing.irtdb")));

            var peptideSettingsDlg5 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var editRT5 = ShowDialog<EditRTDlg>(peptideSettingsDlg5.AddRTRegression);

            //Add results and switch to a calculator whose database is not connected: error
            RunDlg<MessageDlg>(() =>
                                   {
                                       editRT5.AddResults();
                                       editRT5.ChooseCalculator(irtCalcMissing);
                                   },
                                   errorMessage => errorMessage.OkDialog());

            //Go to add a new calculator
            var irtDlg5 = ShowDialog<EditIrtCalcDlg>(editRT5.AddCalculator);

            //Try to open a file that does not exist: error
            RunDlg<MessageDlg>(() => irtDlg5.OpenDatabase(databasePath), messageDlg => messageDlg.OkDialog());

            OkDialog(irtDlg5, irtDlg5.CancelDialog);

            //In order to export a transition list, we have to set the RT regression to have the iRT Calc.
            //This means that the iRT calc must have its database connected - else the dialog will not let
            //the user choose it. So here we will restore the file so we can choose the calculator.
            //Then once that dialog is closed and the regression is saved, switch the file back again.
            stream = File.Create(databasePath);
            stream.Close();
            File.Replace(testFilesDir.GetTestPath("irt-c18-copy.irtdb"), databasePath,
                         testFilesDir.GetTestPath("backup.irtdb"));

            RunUI(() =>
                      {
                          editRT5.SetRegressionName("iRT Test Regression");
                          editRT5.AddResults();
                          editRT5.ChooseCalculator(irtCalc);
                      });
            OkDialog(editRT5, editRT5.OkDialog);

            RunUI(() =>
                      {
                          peptideSettingsDlg5.ChooseRegression("iRT Test Regression");
                          peptideSettingsDlg5.IsUseMeasuredRT = false; //Use prediction
                      });
            OkDialog(peptideSettingsDlg5, peptideSettingsDlg5.OkDialog);

            RunUI(() => SkylineWindow.SaveDocument());

            //Switch the file back to the copy, destroying the original
            stream = File.Create(testFilesDir.GetTestPath("irt-c18-copy.irtdb"));
            stream.Close();
            File.Replace(databasePath, testFilesDir.GetTestPath("irt-c18-copy.irtdb"),
                         testFilesDir.GetTestPath("backup.irtdb"));

            var exportTransList = ShowDialog<ExportMethodDlg>(SkylineWindow.ShowExportTransitionListDlg);

            // Used to cause a message box, but should work now, because iRT databases get loaded once
            RunUI(() => exportTransList.SetMethodType(ExportMethodType.Scheduled));

            OkDialog(exportTransList, exportTransList.CancelDialog);

            // Used to cause a message box, but should work now, because iRT databases get loaded once
            RunUI(() => SkylineWindow.ChooseCalculator(irtCalc));

            /*
             * Now clean up by deleting all these calculators. If we don't, then the next functional test
             * will fail because it will try to use a calculator from settings which will not have its
             * database.
             */
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsDlg6 =>
                                                            {
                                                                peptideSettingsDlg6.ChooseRegression(Resources.SettingsList_ELEMENT_NONE_None);
                                                                peptideSettingsDlg6.OkDialog();
                                                            });

            // Finally, close and re-open the document to see MissingFileDlg
            int pepCount = SkylineWindow.Document.PeptideCount;
            RunUI(() => SkylineWindow.NewDocument(true));
            Assert.AreEqual(0, SkylineWindow.Document.PeptideCount);
            RunDlg<MissingFileDlg>(() => SkylineWindow.OpenFile(documentPath),
                dlg => dlg.OkDialog());
            Assert.AreEqual(pepCount, SkylineWindow.Document.PeptideCount);
            RunUI(() => SkylineWindow.NewDocument(true));
            RunDlg<MissingFileDlg>(() => SkylineWindow.OpenFile(documentPath),
                dlg => dlg.CancelDialog());
            Assert.AreEqual(0, SkylineWindow.Document.PeptideCount);

            // Make sure no message boxes are left open
            Assert.IsNull(FindOpenForm<MessageDlg>());
        }

        private void RunCalibrationTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, TestFilesZip);

            RunUI(() => SkylineWindow.OpenFile(testFilesDir.GetTestPath("RePLiCal data for Skyline team - Pierce.sky")));
            var peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var editIrtCalcDlg = ShowDialog<EditIrtCalcDlg>(peptideSettingsDlg.AddCalculator);

            var calcPath = testFilesDir.GetTestPath("calibration_test_calculator.irtdb");
            const string calcName = "Calibration test calculator";
            RunUI(() =>
            {
                editIrtCalcDlg.CalcName = calcName;
                editIrtCalcDlg.CreateDatabase(calcPath);
            });

            var calibrateIrtDlg = ShowDialog<CalibrateIrtDlg>(editIrtCalcDlg.Calibrate);
            RunUI(() =>
            {
                calibrateIrtDlg.StandardName = "Test standard";
                var regressionOptions = calibrateIrtDlg.RegressionOptions;
                Assert.AreEqual(4, regressionOptions.Length);
                Assert.IsTrue(regressionOptions[0].Name.Equals(Resources.RegressionOption_All_Fixed_points__linear_));
                Assert.IsTrue(regressionOptions[1].Name.Equals(Resources.RegressionOption_All_Fixed_points__logarithmic_));
                Assert.IsTrue(regressionOptions.Select(opt => opt.Name).Contains(IrtStandard.REPLICAL.Name));
                Assert.IsTrue(regressionOptions.Select(opt => opt.Name).Contains(IrtStandard.PIERCE.Name));
                Assert.IsTrue(ReferenceEquals(calibrateIrtDlg.SelectedRegressionOption, regressionOptions[0]));
            });
            ShowAndCancelDlg<MessageDlg>(() => calibrateIrtDlg.GraphRegression());
            ShowAndCancelDlg<MessageDlg>(() => calibrateIrtDlg.GraphIrts());
            var addIrtDlg = ShowDialog<AddIrtStandardsDlg>(calibrateIrtDlg.UseResults);
            ShowAndCancelDlg<MessageDlg>(() => addIrtDlg.OkDialog()); // try empty textbox
            RunUI(() => addIrtDlg.StandardCount = CalibrateIrtDlg.MIN_STANDARD_PEPTIDES - 1);
            ShowAndCancelDlg<MessageDlg>(() => addIrtDlg.OkDialog()); // try below minimum
            RunUI(() => addIrtDlg.StandardCount = SkylineWindow.Document.PeptideCount + 1);
            ShowAndCancelDlg<MessageDlg>(() => addIrtDlg.OkDialog()); // try above maximum
            RunUI(() => addIrtDlg.StandardCount = 10);
            OkDialog(addIrtDlg, addIrtDlg.OkDialog);
            RunUI(() => Assert.AreEqual(10, calibrateIrtDlg.StandardPeptideCount));
            RunDlg<GraphRegression>(() => calibrateIrtDlg.GraphRegression(), dlg =>
            {
                Assert.AreEqual(1, dlg.RegressionGraphDatas.Count);
                Assert.AreEqual(2, dlg.RegressionGraphDatas.First().RegularPoints.Count);
                dlg.CloseDialog();
            });
            RunDlg<GraphRegression>(() => calibrateIrtDlg.GraphIrts(), dlg =>
            {
                Assert.AreEqual(1, dlg.RegressionGraphDatas.Count);
                Assert.AreEqual(10, dlg.RegressionGraphDatas.First().RegularPoints.Count);
                dlg.CloseDialog();
            });
            RunUI(() => calibrateIrtDlg.SelectedRegressionOption = calibrateIrtDlg.RegressionOptions.First(opt => opt.Name.Equals(IrtStandard.PIERCE.Name)));
            RunDlg<AddIrtStandardsDlg>(() => calibrateIrtDlg.UseResults(), dlg =>
            {
                dlg.StandardCount = 10;
                dlg.OkDialog();
            });
            var standardPeptides = new List<Target>();
            RunUI(() =>
            {
                Assert.AreEqual(10, calibrateIrtDlg.StandardPeptideCount);
                calibrateIrtDlg.SelectedRegressionOption = calibrateIrtDlg.RegressionOptions.First(opt => opt.Name.Equals(IrtStandard.REPLICAL.Name));
                calibrateIrtDlg.UseResults();
                Assert.AreEqual(15, calibrateIrtDlg.StandardPeptideCount);
                standardPeptides.AddRange(calibrateIrtDlg.StandardPeptideList.Select(pep => pep.Target));
            });
            OkDialog(calibrateIrtDlg, calibrateIrtDlg.OkDialog);
            OkDialog(editIrtCalcDlg, editIrtCalcDlg.OkDialog);
            OkDialog(peptideSettingsDlg, peptideSettingsDlg.OkDialog);

            RunUI(() =>
            {
                Assert.IsTrue(SkylineWindow.SaveDocument());
                SkylineWindow.NewDocument();
            });
            // The created irtdb should have document XML for the standard peptides
            var irtDb = IrtDb.GetIrtDb(calcPath, null);
            Assert.IsFalse(string.IsNullOrEmpty(irtDb.DocumentXml));
            // Set RT regression to None
            RunDlg<PeptideSettingsUI>(() => SkylineWindow.ShowPeptideSettingsUI(), dlg =>
            {
                dlg.ChooseRegression(Resources.SettingsList_ELEMENT_NONE_None);
                dlg.OkDialog();
            });
            // Change to the RT regression using the calculator with document XML for prompt to add standards to document
            var peptideSettingsDlg2 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsDlg2.ChooseRegression(calcName));
            var addStandardsDlg = ShowDialog<AddIrtStandardsToDocumentDlg>(peptideSettingsDlg2.OkDialog);
            RunUI(() => addStandardsDlg.NumTransitions = 3);
            OkDialog(addStandardsDlg, addStandardsDlg.BtnYesClick);
            WaitForCondition(() => SkylineWindow.Document.PeptideCount == standardPeptides.Count);
            Assert.AreEqual(standardPeptides.Count, SkylineWindow.Document.PeptideCount);
            Assert.IsTrue(SkylineWindow.Document.Peptides.All(pep => standardPeptides.Contains(pep.Target)));

            // CiRT calibration test (use predefined values)
            RunUI(() => SkylineWindow.OpenFile(testFilesDir.GetTestPath("Bruker_diaPASEF_HYE-cirtonly.sky")));
            var peptideSettingsDlg3 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var editIrtCalcDlg2 = ShowDialog<EditIrtCalcDlg>(peptideSettingsDlg3.AddCalculator);
            var calibrateIrtDlg2 = ShowDialog<CalibrateIrtDlg>(editIrtCalcDlg2.Calibrate);
            RunUI(() =>
            {
                calibrateIrtDlg2.StandardName = "CiRT test standard 1";
                var regressionOptions = calibrateIrtDlg2.RegressionOptions;
                Assert.AreEqual(3, regressionOptions.Length);
                Assert.IsTrue(regressionOptions[0].Name.Equals(Resources.RegressionOption_All_Fixed_points__linear_));
                Assert.IsTrue(regressionOptions[1].Name.Equals(Resources.RegressionOption_All_Fixed_points__logarithmic_));
                Assert.IsTrue(regressionOptions[2].Name.Equals(IrtStandard.CIRT_SHORT.Name));
                Assert.IsTrue(ReferenceEquals(calibrateIrtDlg2.SelectedRegressionOption, regressionOptions[0]));
            });
            var addIrtDlg2 = ShowDialog<AddIrtStandardsDlg>(calibrateIrtDlg2.UseResults);
            RunUI(() => addIrtDlg2.StandardCount = 10);

            // found CiRT peptides, ask user if they want to use them, click yes
            var cirtDlg = ShowDialog<MultiButtonMsgDlg>(addIrtDlg2.OkDialog);
            OkDialog(cirtDlg, cirtDlg.BtnYesClick);
            WaitForClosedForm(cirtDlg);
            // ask user if they want to use predefined values, click yes
            var cirtPredefinedDlg = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(cirtPredefinedDlg, cirtPredefinedDlg.BtnYesClick);
            var predefinedIrts = IrtStandard.CIRT.Peptides.ToDictionary(pep => pep.ModifiedTarget, pep => pep.Irt);
            RunUI(() =>
            {
                Assert.AreEqual(Resources.CalibrationGridViewDriver_CiRT_option_name, calibrateIrtDlg2.SelectedRegressionOption.Name);
                Assert.AreEqual(10, calibrateIrtDlg2.StandardPeptideCount);
                foreach (var pep in calibrateIrtDlg2.StandardPeptideList)
                {
                    Assert.IsTrue(predefinedIrts.ContainsKey(pep.Target), $@"calibrateIrtDlg2.StandardPeptideList entry ""{pep.Target}"" not found in predefinedIrts");
                    Assert.AreEqual(predefinedIrts[pep.Target], pep.Irt);
                }
            });
            RunDlg<GraphRegression>(() => calibrateIrtDlg2.GraphRegression(), dlg =>
            {
                Assert.AreEqual(1, dlg.RegressionGraphDatas.Count);
                var data = dlg.RegressionGraphDatas.First();
                Assert.AreEqual(73, data.RegularPoints.Count);
                Assert.AreEqual(0, data.MissingPoints.Count);
                Assert.AreEqual(0, data.OutlierPoints.Count);
                Assert.IsTrue(data.R >= RCalcIrt.MIN_IRT_TO_TIME_CORRELATION);
                dlg.CloseDialog();
            });
            RunDlg<GraphRegression>(() => calibrateIrtDlg2.GraphIrts(), dlg =>
            {
                Assert.AreEqual(1, dlg.RegressionGraphDatas.Count);
                Assert.AreEqual(10, dlg.RegressionGraphDatas.First().RegularPoints.Count);
                dlg.CloseDialog();
            });
            OkDialog(calibrateIrtDlg2, calibrateIrtDlg2.OkDialog);
            RunUI(() =>
            {
                Assert.AreEqual(10, editIrtCalcDlg2.StandardPeptideCount);
                foreach (var pep in editIrtCalcDlg2.StandardPeptides)
                {
                    Assert.IsTrue(predefinedIrts.ContainsKey(pep.ModifiedTarget));
                    Assert.AreEqual(predefinedIrts[pep.ModifiedTarget], pep.Irt);
                }
            });

            // CiRT calibration test (don't use predefined values)
            var calibrateIrtDlg3 = ShowDialog<CalibrateIrtDlg>(editIrtCalcDlg2.Calibrate);
            RunUI(() => calibrateIrtDlg3.StandardName = "CiRT test standard 2");
            var addIrtDlg3 = ShowDialog<AddIrtStandardsDlg>(calibrateIrtDlg3.UseResults);
            RunUI(() => addIrtDlg3.StandardCount = 10);

            // found CiRT peptides, ask user if they want to use them, click yes
            var cirtDlg2 = ShowDialog<MultiButtonMsgDlg>(addIrtDlg3.OkDialog);
            OkDialog(cirtDlg2, cirtDlg2.BtnYesClick);
            WaitForClosedForm(cirtDlg2);
            // ask user if they want to use predefined values, click no
            var cirtPredefinedDlg2 = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(cirtPredefinedDlg2, cirtPredefinedDlg2.Btn1Click);
            var cirtIrts = new Dictionary<Target, double>();
            RunUI(() =>
            {
                Assert.AreEqual(Resources.CalibrationGridViewDriver_CiRT_option_name, calibrateIrtDlg3.SelectedRegressionOption.Name);
                Assert.AreEqual(10, calibrateIrtDlg3.StandardPeptideCount);
                foreach (var pep in calibrateIrtDlg3.StandardPeptideList)
                {
                    Assert.IsTrue(predefinedIrts.ContainsKey(pep.Target));
                    var expectedIrt = calibrateIrtDlg3.SelectedRegressionOption.Regression.GetY(pep.RetentionTime);
                    cirtIrts[pep.Target] = expectedIrt;
                    Assert.AreEqual(expectedIrt, pep.Irt);
                }
            });
            RunDlg<GraphRegression>(() => calibrateIrtDlg3.GraphRegression(), dlg =>
            {
                Assert.AreEqual(1, dlg.RegressionGraphDatas.Count);
                var data = dlg.RegressionGraphDatas.First();
                Assert.AreEqual(73, data.RegularPoints.Count);
                Assert.AreEqual(0, data.MissingPoints.Count);
                Assert.AreEqual(0, data.OutlierPoints.Count);
                Assert.IsTrue(data.R >= RCalcIrt.MIN_IRT_TO_TIME_CORRELATION);
                dlg.CloseDialog();
            });
            RunDlg<GraphRegression>(() => calibrateIrtDlg3.GraphIrts(), dlg =>
            {
                Assert.AreEqual(1, dlg.RegressionGraphDatas.Count);
                Assert.AreEqual(10, dlg.RegressionGraphDatas.First().RegularPoints.Count);
                dlg.CloseDialog();
            });
            OkDialog(calibrateIrtDlg3, calibrateIrtDlg3.OkDialog);
            RunUI(() =>
            {
                Assert.AreEqual(10, editIrtCalcDlg2.StandardPeptideCount);
                foreach (var pep in editIrtCalcDlg2.StandardPeptides)
                {
                    Assert.IsTrue(predefinedIrts.ContainsKey(pep.ModifiedTarget));
                    Assert.AreEqual(cirtIrts[pep.Target], pep.Irt);
                }
            });
            OkDialog(editIrtCalcDlg2, editIrtCalcDlg2.CancelDialog);
            OkDialog(peptideSettingsDlg3, peptideSettingsDlg3.CancelDialog);
        }

        private SrmDocument VerifyIrtStandards(SrmDocument docBefore, bool expectStandards)
        {
            var doc = WaitForDocumentChangeLoaded(docBefore);

            // Either all standards or all not standards
            foreach (var nodePep in doc.Peptides)
            {
                if (expectStandards)
                {
                    Assert.IsTrue(nodePep.GlobalStandardType == StandardType.IRT,
                        string.Format("{0} expected marked as iRT standard", nodePep.Peptide.Target));
                }
                else
                {
                    Assert.IsFalse(nodePep.GlobalStandardType == StandardType.IRT,
                        string.Format("{0} expected cleared of iRT standard", nodePep.Peptide.Target));
                }
            }

            return doc;
        }

        private static string BuildStandardText(IEnumerable<MeasuredPeptide> standard, Func<string, string> adjustSeq = null)
        {
            var standardBuilder = new StringBuilder();
            foreach (var peptide in standard)
            {
                standardBuilder.Append(adjustSeq != null ? adjustSeq(peptide.Sequence) : peptide.Sequence)
                    .Append('\t')
                    .Append(peptide.RetentionTime)
                    .AppendLine();
            }

            return standardBuilder.ToString();
        }

        private static void ExportMethod(string exportPath)
        {
            var expMethodDlg = ShowDialog<ExportMethodDlg>(SkylineWindow.ShowExportTransitionListDlg);
            RunUI(() =>
                      {
                          expMethodDlg.SetInstrument(ExportInstrumentType.THERMO);
                          expMethodDlg.SetMethodType(ExportMethodType.Scheduled);
                          expMethodDlg.OkDialog(exportPath);
                      });

            WaitForClosedForm(expMethodDlg);
        }

        public void EditIrtCalcDlgPepCountTest(EditIrtCalcDlg dlg, int numStandardPeps, int numLibraryPeps, string path, bool add)
        {
            RunUI(() =>
                      {
                          Assert.AreEqual(0, dlg.StandardPeptideCount);
                          Assert.AreEqual(0, dlg.LibraryPeptideCount);

                          dlg.CalcName = "Testing";
                          dlg.OpenDatabase(path);

                          Assert.AreEqual(numStandardPeps, dlg.StandardPeptideCount);
                          Assert.AreEqual(numLibraryPeps, dlg.LibraryPeptideCount);
                      });

            if(add)
            {
                var addDlg = ShowDialog<AddIrtPeptidesDlg>(dlg.AddResults);
                RunUI(() => addDlg.Action = AddIrtPeptidesAction.skip);
                var recalibrateDlg = ShowDialog<MultiButtonMsgDlg>(addDlg.OkDialog);
                OkDialog(recalibrateDlg, recalibrateDlg.Btn1Click);
            }

            OkDialog(dlg, dlg.OkDialog);
        }
    }

    [TestClass]
    public class IrtImportResultsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void IrtImportResultsFunctionalTest()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var peptideSettings = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var irtCalc = ShowDialog<EditIrtCalcDlg>(peptideSettings.AddCalculator);
            RunUI(() =>
            {
                irtCalc.CalcName = "Biognosys-10";
                irtCalc.CreateDatabase(TestContext.GetTestResultsPath("test.irtdb"));

            });
            // Test choosing an iRT standard with many rows before switching to a standard with fewer rows
            RunUI(() =>
            {
                irtCalc.IrtStandards = IrtStandard.REPLICAL;
                var standardPeptideCount = IrtStandard.REPLICAL.Peptides.Count;
                var gridView = irtCalc.GridViewStandard;
                Assert.AreEqual(standardPeptideCount, gridView.Rows.Count);
                // Put the focus in the last row, and make sure nothing bad happens when we switch to a shorter
                // iRT standard
                irtCalc.GridViewStandard.CurrentCell = gridView.Rows[standardPeptideCount - 1].Cells[0];
                irtCalc.IrtStandards = IrtStandard.BIOGNOSYS_10;
            });

            OkDialog(irtCalc, irtCalc.OkDialog);
            var addPeptides = ShowDialog<AddIrtStandardsToDocumentDlg>(peptideSettings.OkDialog);
            RunUI(() => addPeptides.NumTransitions = 3);
            OkDialog(addPeptides, addPeptides.BtnYesClick);
            RunUI(() => SkylineWindow.SaveDocument(TestContext.GetTestResultsPath("test.sky")));
            
            // Test opening ImportResultsDlg with all 10 iRT standard peptides in the document
            removePeptidesAndImport(IrtStandard.BIOGNOSYS_10, 0);

            // Test with 1 missing
            removePeptidesAndImport(IrtStandard.BIOGNOSYS_10, 1);

            // Test with 2 missing (minimum)
            removePeptidesAndImport(IrtStandard.BIOGNOSYS_10, 1);

            // Test with 3 missing (below minimum)
            removePeptidesAndImport(IrtStandard.BIOGNOSYS_10, 1);

            // Test with all missing
            WaitForDocumentLoaded();    // Changes from background loaders can cause the paste below to fail
            RunUI(() => SkylineWindow.Paste("PEPTIDER")); // Put in a single peptide so we don't get an error on import results
            removePeptidesAndImport(IrtStandard.BIOGNOSYS_10, 7);
        }

        private static void removePeptidesAndImport(IrtStandard standard, int numPeptides)
        {
            Target[] standardPeptides = null;
            List<Target> removedPeptides = null;
            RunUI(() =>
            {
                standardPeptides = standard.Peptides.Select(pep => pep.Target).ToArray();
                removedPeptides = standardPeptides.Except(SkylineWindow.DocumentUI.Peptides.Select(nodePep => nodePep.ModifiedTarget)).ToList();
            }); 
            var toRemove = standardPeptides.Except(removedPeptides).ToArray();
            if (numPeptides > toRemove.Length)
                Assert.Fail("Not enough peptides to remove");

            for (var i = 0; i < numPeptides; i++)
            {
                RemovePeptide(toRemove[i]);
                removedPeptides.Add(toRemove[i]);
            }

            if (!removedPeptides.Any())
            {
                var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
                OkDialog(importResultsDlg, importResultsDlg.CancelDialog);
                return;
            }

            var docCount = standardPeptides.Length - removedPeptides.Count;
            if (docCount >= RCalcIrt.MinStandardCount(standardPeptides.Length))
            {
                var allowedMissing = docCount - RCalcIrt.MinStandardCount(standardPeptides.Length);
                var warningDlg = ShowDialog<MultiButtonMsgDlg>(SkylineWindow.ImportResults);
                RunUI(() =>
                {
                    foreach (var pep in removedPeptides)
                    {
                        Assert.IsTrue(warningDlg.Message.Contains(pep.ToString()));
                    }
                    Assert.IsTrue(warningDlg.Message.Contains(
                        string.Format(Resources.SkylineWindow_ImportResults_The_document_contains__0__of_these_iRT_standard_peptides_, docCount)));
                    Assert.IsTrue(warningDlg.Message.Contains(
                        allowedMissing > 0
                            ? string.Format(Resources.SkylineWindow_ImportResults_A_maximum_of__0__may_be_missing_and_or_outliers_for_a_successful_import_, allowedMissing)
                            : Resources.SkylineWindow_ImportResults_None_may_be_missing_or_outliers_for_a_successful_import_));
                });
                OkDialog(warningDlg, warningDlg.BtnCancelClick);
            }
            else
            {
                var errorDlg = ShowDialog<MessageDlg>(SkylineWindow.ImportResults);
                RunUI(() =>
                {
                    foreach (var pep in removedPeptides)
                    {
                        Assert.IsTrue(errorDlg.Message.Contains(pep.ToString()));
                    }
                    Assert.IsTrue(errorDlg.Message.Contains(
                        docCount > 0
                            ? string.Format(Resources.SkylineWindow_ImportResults_The_document_only_contains__0__of_these_iRT_standard_peptides_, docCount)
                            : Resources.SkylineWindow_ImportResults_The_document_does_not_contain_any_of_these_iRT_standard_peptides_));
                    Assert.IsTrue(errorDlg.Message.Contains(Resources.SkylineWindow_ImportResults_Add_missing_iRT_standard_peptides_to_your_document_or_change_the_retention_time_predictor_));
                });
                OkDialog(errorDlg, errorDlg.OkDialog);
            }
        }
    }

    [TestClass]
    public class IrtRemoveDuplicatesTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void IrtRemoveDuplicatesFunctionalTest()
        {
            TestFilesZip = @"TestFunctional\IrtTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, TestFilesZip);
            var dbPath = testFilesDir.GetTestPath("MPDS_1_Peptides.irtdb");
            var dbBytes = File.ReadAllBytes(dbPath);

            const int numStandards = 19;
            const int numLibrary = 127;
            const int numOverlap = 18;

            void CheckIrtDbFile(bool expectDuplicates, out DbIrtPeptide[] arrStandards, out DbIrtPeptide[] arrLibrary, out Target[] arrOverlap)
            {
                IrtDb.GetIrtDb(dbPath, null, out var dbPeptides);
                arrStandards = dbPeptides.Where(pep => pep.Standard).ToArray();
                arrLibrary = dbPeptides.Where(pep => !pep.Standard).ToArray();
                arrOverlap = arrStandards.Select(pep => pep.ModifiedTarget).Intersect(arrLibrary.Select(pep => pep.ModifiedTarget)).ToArray();
                Assert.AreEqual(numStandards, arrStandards.Length);
                Assert.AreEqual(expectDuplicates ? numLibrary : numLibrary - numOverlap, arrLibrary.Length);
                Assert.AreEqual(expectDuplicates ? numOverlap : 0, arrOverlap.Length);
            }

            CheckIrtDbFile(true, out var standards, out var library, out var overlap);

            const string calcName = "Duplicate test";

            var peptideSettings = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunDlg<EditIrtCalcDlg>(peptideSettings.EditCalculator, dlg =>
            {
                dlg.CalcName = calcName;
                dlg.OpenDatabase(dbPath);
                // Check that the duplicates were removed from the list of library peptides
                Assert.AreEqual(numStandards, dlg.StandardPeptideCount);
                Assert.AreEqual(numLibrary - numOverlap, dlg.LibraryPeptideCount);
                // Reset lists to original values (i.e. containing duplicates)
                dlg.StandardPeptides = standards;
                dlg.LibraryPeptides = library;
                Assert.AreEqual(numStandards, dlg.StandardPeptideCount);
                Assert.AreEqual(numLibrary, dlg.LibraryPeptideCount);
                dlg.OkDialog();
            });

            // Check that the database got saved without duplicates
            CheckIrtDbFile(false, out _, out _, out _);

            // Add RT predictor with the new calculator
            RunDlg<EditRTDlg>(peptideSettings.AddRTRegression, dlg =>
            {
                dlg.ChooseCalculator(calcName);
                dlg.OkDialog();
            });
            RunDlg<AddIrtStandardsToDocumentDlg>(peptideSettings.OkDialog, dlg => dlg.BtnNoClick());

            var docPath = testFilesDir.GetTestPath("duplicate-test.sky");
            RunUI(() =>
            {
                SkylineWindow.SaveDocument(docPath);
                SkylineWindow.NewDocument();
            });

            // Reset irtdb file to contain duplicates
            TryHelper.TryTwice(() => File.WriteAllBytes(dbPath, dbBytes));
            CheckIrtDbFile(true, out _, out _, out _);

            // Open file and check that the duplicates get removed
            RunUI(() => SkylineWindow.OpenFile(docPath));
            WaitForDocumentLoaded();
            CheckIrtDbFile(false, out _, out _, out _);
        }
    }

    [TestClass]
    public class IrtRedundantDbTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void IrtRedundantDbFunctionalTest()
        {
            TestFilesZip = @"TestFunctional\IrtTest.zip";
            RunFunctionalTest();
        }

        private const string CALC_NAME = "History test";
        private string _dbPath;
        private readonly IList<DbIrtPeptide> _standards = IrtStandard.BIOGNOSYS_10.Peptides;
        private bool _redundant;
        private readonly Dictionary<string, List<double>> _peps = new Dictionary<string, List<double>>();

        protected override void DoTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, TestFilesZip);
            _dbPath = testFilesDir.GetTestPath("history-test.irtdb");

            // Create initial calculator
            var peptideSettings = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunDlg<EditIrtCalcDlg>(peptideSettings.EditCalculator, dlg =>
            {
                dlg.CalcName = CALC_NAME;
                dlg.CreateDatabase(_dbPath);
                dlg.StandardPeptides = _standards;
                CheckIrtCalcDlg(dlg);
                ChangeRedundant(dlg, true);
                AddIrt(dlg, "P", -100);
                AddIrt(dlg, "PE", -50);
                AddIrt(dlg, "PEP", -25);
                AddIrt(dlg, "PEPT", 0);
                AddIrt(dlg, "PEPTI", 25);
                AddIrt(dlg, "PEPTID", 50);
                AddIrt(dlg, "PEPTIDE", 100);
                dlg.OkDialog();
            });
            CheckIrtDbFile();

            // Add RT predictor with the new calculator
            RunDlg<EditRTDlg>(peptideSettings.AddRTRegression, dlg =>
            {
                dlg.ChooseCalculator(CALC_NAME);
                dlg.OkDialog();
            });

            // Change some iRT values and check database
            RunDlg<EditIrtCalcDlg>(peptideSettings.EditCalculator, dlg =>
            {
                CheckIrtCalcDlg(dlg);
                AddIrt(dlg, "PEPTIDEP", 30);
                AddIrt(dlg, "PEPTIDEPE", 35);
                AddIrt(dlg, "PEPTIDEPEP", 40);
                EditIrt(dlg, "PE", -75);
                EditIrt(dlg, "PEPTID", 75);
                DeleteIrt(dlg, "P");
                dlg.OkDialog();
            });
            CheckIrtDbFile();

            // Change some more iRT values and check database
            RunDlg<EditIrtCalcDlg>(peptideSettings.EditCalculator, dlg =>
            {
                CheckIrtCalcDlg(dlg);
                EditIrt(dlg, "PE", -150);
                EditIrt(dlg, "PEP", -10);
                EditIrt(dlg, "PEPTI", 10);
                EditIrt(dlg, "PEPTIDEPE", 38);
                DeleteIrt(dlg, "PEPTID");
                dlg.OkDialog();
            });
            CheckIrtDbFile();

            // Set database to non-redundant
            RunDlg<EditIrtCalcDlg>(peptideSettings.EditCalculator, dlg =>
            {
                CheckIrtCalcDlg(dlg);
                ChangeRedundant(dlg, false);
                dlg.OkDialog();
            });
            CheckIrtDbFile();

            RunDlg<AddIrtStandardsToDocumentDlg>(peptideSettings.OkDialog, dlg => dlg.BtnNoClick());

            RunUI(() => SkylineWindow.SaveDocument(testFilesDir.GetTestPath("history-test.sky")));
        }

        private double GetMedianIrt(string target)
        {
            Assert.IsTrue(_peps.TryGetValue(target, out var irts),
                $"Missing peptide {target} from [{string.Join(", ", _peps.Keys)}]");
            return new Statistics(irts).Median();
        }

        private void ChangeRedundant(EditIrtCalcDlg dlg, bool redundant)
        {
            dlg.IsRedundant = _redundant = redundant;
            if (!redundant)
            {
                var dlgPeps = dlg.LibraryPeptides.ToDictionary(pep => pep.ModifiedTarget.ToString());
                foreach (var pep in _peps)
                {
                    Assert.IsTrue(dlgPeps.ContainsKey(pep.Key));
                    pep.Value.Clear();
                    pep.Value.Add(dlgPeps[pep.Key].Irt);
                }
            }
        }

        private void AddIrt(EditIrtCalcDlg dlg, string target, double irt)
        {
            var dlgPeps = dlg.LibraryPeptides.ToList();
            Assert.IsNull(dlgPeps.FirstOrDefault(pep => Equals(pep.ModifiedTarget.ToString(), target)));
            var newPep = new DbIrtPeptide(new Target(target), irt, false, TimeSource.peak);
            _peps.Add(target, new List<double> { irt });
            dlg.LibraryPeptides = dlgPeps.Append(newPep);
        }

        private void EditIrt(EditIrtCalcDlg dlg, string target, double irt)
        {
            var dlgPepIdx = dlg.LibraryPeptides.ToArray().IndexOf(pep => Equals(pep.ModifiedTarget.ToString(), target));
            Assert.AreNotEqual(-1, dlgPepIdx);
            dlg.AddLibraryIrt(dlgPepIdx, irt);
            Assert.IsTrue(_peps.TryGetValue(target, out var histories));
            histories.Add(irt);
            Assert.AreEqual(GetMedianIrt(target), dlg.LibraryPeptides.Skip(dlgPepIdx).First().Irt);
        }

        private void DeleteIrt(EditIrtCalcDlg dlg, string target)
        {
            Assert.IsTrue(_peps.Remove(target));
            var dlgPeps = dlg.LibraryPeptides.ToList();
            var i = dlgPeps.IndexOf(pep => Equals(pep.ModifiedTarget.ToString(), target));
            Assert.AreNotEqual(-1, i);
            dlgPeps.RemoveAt(i);
            dlg.LibraryPeptides = dlgPeps;
        }

        private void CheckIrtCalcDlg(EditIrtCalcDlg dlg)
        {
            Assert.AreEqual(CALC_NAME, dlg.CalcName);
            Assert.AreEqual(_dbPath, dlg.CalcPath);
            Assert.AreEqual(dlg.SelectedRegressionType, IrtRegressionType.DEFAULT);
            Assert.AreEqual(_redundant, dlg.IsRedundant);

            Assert.AreEqual(_standards.Count, dlg.StandardPeptideCount);
            foreach (var pep in dlg.StandardPeptides.Select((dlgPep, i) =>
                         new KeyValuePair<int, DbIrtPeptide>(i, dlgPep)))
            {
                Assert.AreEqual(_standards[pep.Key].PeptideModSeq, pep.Value.PeptideModSeq);
                Assert.AreEqual(_standards[pep.Key].Irt, pep.Value.Irt);
            }

            var dlgPeps = dlg.LibraryPeptides.ToDictionary(pep => pep.PeptideModSeq, pep => pep);
            Assert.AreEqual(_peps.Count, dlg.LibraryPeptideCount);
            foreach (var pep in _peps)
            {
                Assert.IsTrue(dlgPeps.TryGetValue(pep.Key, out var dlgPep));
                var expectedIrt = GetMedianIrt(pep.Key);
                Assert.AreEqual(expectedIrt, dlgPep.Irt,
                    $"Peptide {pep.Key} differs (iRT expected = {expectedIrt}, actual = {dlgPep.Irt})");
            }
        }

        private void CheckIrtDbFile()
        {
            var db = IrtDb.GetIrtDb(_dbPath, null, out var dbPeptides);
            Assert.AreEqual(_redundant, db.Redundant);

            var dbHistories = new Dictionary<long, List<double>>();
            foreach (var history in db.ReadHistories() ?? Enumerable.Empty<DbIrtHistory>())
            {
                if (!dbHistories.TryGetValue(history.PeptideId, out var list))
                    dbHistories.Add(history.PeptideId, new List<double> { history.Irt });
                else
                    list.Add(history.Irt);
            }

            var expectedStandards = _standards.Select(pep => pep.ModifiedTarget).ToHashSet();
            var expectedLibrary = _peps.ToDictionary(pep => pep.Key, pep => new List<double>(pep.Value));
            foreach (var pep in dbPeptides)
            {
                if (pep.Standard)
                {
                    Assert.IsTrue(expectedStandards.Remove(pep.ModifiedTarget));
                    Assert.IsFalse(dbHistories.ContainsKey(pep.Id.Value)); // standards should not have histories
                    Assert.AreEqual(pep.Irt, db.ScoreSequence(pep.ModifiedTarget).Value);
                    continue;
                }

                // Verify iRT value
                Assert.AreEqual(GetMedianIrt(pep.ModifiedTarget.ToString()), pep.Irt);

                // Verify history
                if (_redundant)
                {
                    Assert.IsTrue(expectedLibrary.TryGetValue(pep.ModifiedTarget.ToString(), out var expectedHistory));
                    Assert.IsTrue(dbHistories.TryGetValue(pep.Id.Value, out var dbHistory));
                    foreach (var i in dbHistory.Select(history => expectedHistory.FindIndex(irt => Math.Abs(irt - history) < 0.01)))
                    {
                        Assert.AreNotEqual(-1, i);
                        expectedHistory.RemoveAt(i);
                    }
                    Assert.AreEqual(new Statistics(dbHistory.Append(pep.Irt)).Median(), db.ScoreSequence(pep.ModifiedTarget));
                    Assert.AreEqual(0, expectedHistory.Count);
                }
                Assert.IsTrue(expectedLibrary.Remove(pep.PeptideModSeq));
            }

            Assert.AreEqual(0, expectedStandards.Count);
            Assert.AreEqual(0, expectedLibrary.Count);
        }
    }

    [TestClass]
    public class IrtDocumentTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void IrtDocumentFunctionalTest()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            foreach (var standard in IrtStandard.ALL.Where(standard => standard.HasDocument))
            {
                if (ReferenceEquals(standard, IrtStandard.REPLICAL))
                    continue; // TODO: Fix neutral loss transition bug and remove this.

                TestStandardDocument(standard);
            }

            // Test full CiRT with first 20 peptides not found in short CiRT list.
            TestStandardDocument(IrtStandard.CIRT,
                IrtStandard.CIRT.Peptides.Where(pep => !IrtStandard.CIRT_SHORT.Contains(pep.ModifiedTarget)).Take(20));

            RunUI(() => SkylineWindow.SaveDocument(TestContext.GetTestResultsPath("test.sky")));
        }

        private void TestStandardDocument(IrtStandard standard, IEnumerable<DbIrtPeptide> overrideStandards = null, int numTransitions = 3)
        {
            RunUI(() => SkylineWindow.NewDocument(true));
            var peptideSettings = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);

            var standards = (overrideStandards ?? standard.Peptides).ToArray();

            // Add iRT calculator.
            RunDlg<EditIrtCalcDlg>(peptideSettings.AddCalculator, dlg =>
            {
                dlg.CalcName = string.Format("Test {0}", standard.Name);
                dlg.CreateDatabase(TestContext.GetTestResultsPath("test.irtdb"));
                dlg.StandardPeptides = standards;
                dlg.OkDialog();
            });

            // Add the iRT standards to the document.
            RunDlg<AddIrtStandardsToDocumentDlg>(peptideSettings.OkDialog, dlg =>
            {
                dlg.NumTransitions = numTransitions;
                dlg.BtnYesClick();
            });

            WaitForCondition(() => SkylineWindow.Document.Peptides.Any() && SkylineWindow.Document.IsLoaded);

            // Check that the document contains the standards.
            var doc = SkylineWindow.Document;
            Assert.AreEqual(standards.Length, doc.PeptideCount,
                $"{standard.Name}: have {doc.PeptideCount} peptides in document but want {standards.Length}");
            var docTargets = new TargetMap<PeptideDocNode>(doc.Peptides.Select(pep =>
                new KeyValuePair<Target, PeptideDocNode>(pep.ModifiedTarget, pep)));
            Assert.IsTrue(standards.All(pep => docTargets.ContainsKey(pep.ModifiedTarget)),
                $"{standard.Name}: have document peptides {string.Join(", ", docTargets.Keys)} but want {string.Join(",", standards.Select(pep => pep.ModifiedTarget))}");

            // Compare the added standards to the reference document.
            var refDoc = (SrmDocument)(new XmlSerializer(typeof(SrmDocument))).Deserialize(standard.GetReader());
            Assert.AreEqual(standard.Peptides.Count, refDoc.PeptideCount,
                $"{standard.Name}: have {refDoc.PeptideCount} peptides in reference document but want {standard.Peptides.Count}");
            var refDocTargets = new TargetMap<PeptideDocNode>(refDoc.Peptides.Select(pep =>
                new KeyValuePair<Target, PeptideDocNode>(pep.ModifiedTarget, pep)));
            foreach (var pep in docTargets)
            {
                var target = pep.Key;
                var nodePep = pep.Value;

                Assert.IsTrue(refDocTargets.TryGetValue(target, out var refNodePep),
                    $"{standard.Name}: reference document does not contain added target {target}");

                // Check precursors.
                var nodeTranGroups = nodePep.TransitionGroups.OrderBy(nodeTranGroup => nodeTranGroup.PrecursorMz).ToArray();
                var refNodeTranGroups = refNodePep.TransitionGroups.OrderBy(nodeTranGroup => nodeTranGroup.PrecursorMz).ToArray();
                Assert.AreEqual(refNodeTranGroups.Length, nodeTranGroups.Length,
                    $"{standard.Name}: have {nodePep.TransitionGroupCount} precursors but want {refNodePep.TransitionGroupCount} for {target}");
                for (var i = 0; i < nodeTranGroups.Length; i++)
                {
                    // Check transitions.
                    var nodeTranGroup = nodeTranGroups[i];
                    var refNodeTranGroup = refNodeTranGroups[i];
                    Assert.AreEqual(refNodeTranGroup.PrecursorMz, nodeTranGroup.PrecursorMz,
                        $"{standard.Name}: have precursor m/z {nodeTranGroup.PrecursorMz} for {target} but want {refNodeTranGroup.PrecursorMz}");

                    foreach (var nodeTran in refNodeTranGroup.Transitions)
                        Assert.IsTrue(nodeTran.HasLibInfo,
                            $"{standard.Name}: reference document missing LibInfo for {target}, {refNodeTranGroup.PrecursorMz}, {nodeTran.FragmentIonName}");

                    foreach (var nodeTran in nodeTranGroup.Transitions)
                        Assert.IsTrue(nodeTran.HasLibInfo,
                            $"{standard.Name}: missing LibInfo for added target {target}, {nodeTranGroup.PrecursorMz}, {nodeTran.FragmentIonName}");

                    var expectedTransitions = refNodeTranGroup.Transitions.OrderBy(nodeTran => nodeTran.LibInfo.Rank).Take(numTransitions).ToArray();
                    var actualTransitions = nodeTranGroup.Transitions.OrderBy(nodeTran => nodeTran.LibInfo.Rank).ToArray();
                    Assert.AreEqual(expectedTransitions.Length, actualTransitions.Length,
                        $"{standard.Name}: have {actualTransitions.Length} transitions for {target}, {nodeTranGroup.PrecursorMz} but want {expectedTransitions.Length}");

                    for (var j = 0; j < actualTransitions.Length; j++)
                    {
                        var actualTransition = actualTransitions[j];
                        var expectedTransition = expectedTransitions[j];
                        Assert.AreEqual(expectedTransition, actualTransition,
                            $"{standard.Name}: {target}, {nodeTranGroup.PrecursorMz} transition {actualTransition.FragmentIonName} does not equal expected transition");
                    }
                }
            }
        }
    }
}
