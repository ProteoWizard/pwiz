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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
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
        [TestMethod]
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

            var calibratePeptides = new List<MeasuredPeptide>();

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
            RunUI(() =>
            {
                countDlg.StandardCount = peptideCount;
                countDlg.OkDialog();
            });
            WaitForClosedForm(countDlg);

            Assert.AreEqual(peptideCount, calibrateDlg.StandardPeptideCount);

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
                              calibratePeptides = calibrateDlg.Recalculate(SkylineWindow.Document, j);
                              Assert.AreEqual(calibratePeptides.Count, j);
                              Assert.IsNull(FindOpenForm<MessageDlg>());
                          });
            }

            RunUI(() =>
                      {
                          calibrateDlg.Recalculate(SkylineWindow.Document, 11);
                          //After closing this dialog, there should be 3 iRT values below 0
                          //and 3 above 100
                          calibrateDlg.SetFixedPoints(3, 7);

                          calibrateDlg.OkDialog();
                      });
            WaitForClosedForm(calibrateDlg);

            //Now check that the peptides were passed to the EditIrtCalcDlg
            RunUI(() =>
                      {
                          Assert.AreEqual(numStandardPeps, irtDlg1.StandardPeptideCount);
                          //And that there are 3 below 0 and 3 above 100
                          Assert.AreEqual(3, irtDlg1.StandardPeptides.Count(pep => pep.Irt < 0));
                          Assert.AreEqual(3, irtDlg1.StandardPeptides.Count(pep => pep.Irt > 100));
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
                          string standardText = BuildStandardText(standard, seq => seq);
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
            RunDlg<RecalibrateIrtDlg>(irtDlg1.Calibrate, recalDlg =>
            {
                recalDlg.MinIrt = standard[1].RetentionTime + shift;
                recalDlg.MaxIrt = standard[standard.Length - 1].RetentionTime*skew + shift;
                recalDlg.FixedPoint1 = standard[1].Sequence;
                recalDlg.FixedPoint2 = standard[standard.Length - 1].Sequence;
                recalDlg.OkDialog();
            });
            RunUI(() =>
            {
                for (int i = 0; i < irtDlg1.StandardPeptideCount; i++)
                {
                    Assert.AreEqual(standard[i].RetentionTime*skew + shift,
                                    irtDlg1.StandardPeptides.Skip(i).First().Irt);
                }
            });
            RunDlg<RecalibrateIrtDlg>(irtDlg1.Calibrate, recalDlg =>
            {
                recalDlg.FixedPoint1 = standard[2].Sequence;
                recalDlg.FixedPoint2 = standard[standard.Length - 2].Sequence;
                recalDlg.MinIrt = standard[2].RetentionTime;
                recalDlg.MaxIrt = standard[standard.Length - 2].RetentionTime;
                recalDlg.OkDialog();
            });

            // Change peptides
            var changePeptides = irtDlg1.LibraryPeptides.Where((p, i) => i%2 == 0).ToArray();
            var resetPeptides = irtDlg1.StandardPeptides.ToArray();
            RunDlg<ChangeIrtPeptidesDlg>(irtDlg1.ChangeStandardPeptides, changeDlg =>
            {
                changeDlg.Peptides = changePeptides;
                changeDlg.OkDialog();
            });
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
                          editRT1.OkDialog();
                      });
            WaitForClosedForm(editRT1);

            var editRT1A = ShowDialog<EditRTDlg>(peptideSettingsDlg1.AddRTRegression);

            RunUI(() =>
                      {
                          editRT1A.SetRegressionName("SSRCalc Regression");
                          editRT1A.AddResults();
                          editRT1A.ChooseCalculator(ssrCalc);
                      });

            RunDlg<RTDetails>(editRT1A.ShowDetails, detailsDlg => detailsDlg.Close());

            OkDialog(editRT1A, editRT1A.OkDialog);
            RunUI(peptideSettingsDlg1.CancelButton.PerformClick);
            WaitForClosedForm(peptideSettingsDlg1);

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
            Assert.IsTrue(stats.R > 0.999);
            Assert.IsNotNull(line);
            //These values were taken from the graph, which shows values to 2 decimal places, so the real values must
            //be +/- 0.01 from these values
            Assert.IsTrue(Math.Abs(line.Intercept - 14.17) < 0.01);
            Assert.IsTrue(Math.Abs(line.Slope - 0.15) < 0.01);

            RunUI(() => SkylineWindow.ChooseCalculator(ssrCalc));
            WaitForRegression();
            RunUI(() => stats = SkylineWindow.RTGraphController.RegressionRefined.CalcStatistics(docPeptides, null));

            Assert.IsNotNull(stats);
            Assert.IsTrue(Math.Abs(stats.R - 0.97) < 0.01);

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

            RunUI(() =>
                      {
                          Assert.AreEqual(18, irtDlg3.LibraryPeptideCount);
                          irtDlg3.OkDialog();
                      });
            WaitForClosedForm(irtDlg3);
            
            RunUI(editCalculator.OkDialog);
            WaitForClosedForm(editCalculator);

            RunUI(() =>
                      {
                          editRT3.AddResults();
                          editRT3.ChooseCalculator("iRT Document Calculator");
                          editRT3.SetTimeWindow(2.0);
                          editRT3.OkDialog();
                      });
            WaitForClosedForm(editRT3);

            //Then choose the new, document-based regression and turn off prediction
            RunUI(() =>
                      {
                          peptideSettingsDlg3.ChooseRegression("iRT Document Regression");
                          peptideSettingsDlg3.IsUseMeasuredRT = true;
                          peptideSettingsDlg3.OkDialog();

                      });
            
            WaitForClosedForm(peptideSettingsDlg3);

            Assert.IsNull(FindOpenForm<MessageDlg>());

            //Export the measurement-based transition list
            ExportMethod(testFilesDir.GetTestPath("EmpiricalTL.csv"));

            //Turn on prediction for scheduling
            var peptideSettingsDlg4 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
                                          {
                                              peptideSettingsDlg4.IsUseMeasuredRT = false;
                                              peptideSettingsDlg4.OkDialog();
                                          });
            WaitForClosedForm(peptideSettingsDlg4);

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

            RunUI(() => irtDlg5.CancelButton.PerformClick());
            WaitForClosedForm(irtDlg5);

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
                          editRT5.OkDialog();
                      });
            WaitForClosedForm(editRT5);

            RunUI(() =>
                      {
                          peptideSettingsDlg5.ChooseRegression("iRT Test Regression");
                          peptideSettingsDlg5.IsUseMeasuredRT = false; //Use prediction
                          peptideSettingsDlg5.OkDialog();
                      });
            WaitForClosedForm(peptideSettingsDlg5);

            RunUI(() => SkylineWindow.SaveDocument());

            //Switch the file back to the copy, destroying the original
            stream = File.Create(testFilesDir.GetTestPath("irt-c18-copy.irtdb"));
            stream.Close();
            File.Replace(databasePath, testFilesDir.GetTestPath("irt-c18-copy.irtdb"),
                         testFilesDir.GetTestPath("backup.irtdb"));

            var exportTransList = ShowDialog<ExportMethodDlg>(SkylineWindow.ShowExportTransitionListDlg);

            // Used to cause a message box, but should work now, because iRT databases get loaded once
            RunUI(() => exportTransList.SetMethodType(ExportMethodType.Scheduled));

            RunUI(() => exportTransList.CancelButton.PerformClick());
            WaitForClosedForm(exportTransList);

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

        private static string BuildStandardText(IEnumerable<MeasuredPeptide> standard, Func<string, string> adjustSeq)
        {
            var standardBuilder = new StringBuilder();
            foreach (var peptide in standard)
            {
                standardBuilder.Append(adjustSeq(peptide.Sequence))
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

            RunUI(dlg.OkDialog);
            WaitForClosedForm(dlg);
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
            var testDir = TestContext.TestDir;
            RunUI(() =>
            {
                irtCalc.CalcName = "Biognosys-10";
                irtCalc.CreateDatabase(Path.Combine(testDir, "test.irtdb"));
                irtCalc.IrtStandards = IrtStandard.BIOGNOSYS_10;
            });
            OkDialog(irtCalc, irtCalc.OkDialog);
            var addPeptides = ShowDialog<AddIrtStandardsToDocumentDlg>(peptideSettings.OkDialog);
            RunUI(() => addPeptides.NumTransitions = 3);
            OkDialog(addPeptides, addPeptides.BtnYesClick);
            RunUI(() => SkylineWindow.SaveDocument(Path.Combine(testDir, "test.sky")));
            
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
}
