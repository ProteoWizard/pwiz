/*
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
    public class IrtTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void IrtFunctionalTest()
        {
            TestFilesZip = @"TestFunctional\IrtTest.zip";
            RunFunctionalTest();
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
                                   new MeasuredPeptide("LGGNEQVTR", -24.92),
                                   new MeasuredPeptide("GAGSSEPVTGLDAK", 0.00),
                                   new MeasuredPeptide("VEATFGVDESNAK", 12.39),
                                   new MeasuredPeptide("YILAGVENSK", 19.79),
                                   new MeasuredPeptide("TPVISGGPYEYR", 28.71),
                                   new MeasuredPeptide("TPVITGAPYEYR", 33.38),
                                   new MeasuredPeptide("DGLDAASYYAPVR", 42.26),
                                   new MeasuredPeptide("ADVTPADFSEWSK", 54.62),
                                   new MeasuredPeptide("GTFIIDPGGVIR", 70.52),
                                   new MeasuredPeptide("GTFIIDPAAVIR", 87.23),
                                   new MeasuredPeptide("LFLQFGAQGSPFLK", 100.00),
                               };

            RunUI(() =>
                      {
                          string standardText = BuildStandardText(standard, seq => seq.Substring(0, seq.Length - 1));
                          SetClipboardText(standardText);
                          irtDlg1.DoPasteStandard();
                      });

            // Cannot add results because standard peptides are not in the document
            RunDlg<MessageDlg>(irtDlg1.AddResults, messageDlg => messageDlg.OkDialog());

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
            RunDlg<AddIrtPeptidesDlg>(irtDlg1.AddResults, addPeptidesDlg => addPeptidesDlg.OkDialog());

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
            Assert.IsTrue(ArrayUtil.EqualsDeep(changePeptides.Select(p => p.Sequence).ToArray(),
                irtDlg1.StandardPeptides.Select(p => p.Sequence).ToArray()));
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
                                                                        docPepNode.AverageMeasuredRetentionTime.HasValue
                                                                        ? docPepNode.AverageMeasuredRetentionTime.Value
                                                                        : 0));
                          }
                      });

            RetentionTimeStatistics stats = null;
            RegressionLineElement line = null;
            RunUI(() =>
                      {
                          SkylineWindow.ShowRTLinearRegressionGraph();
                          SkylineWindow.SetupCalculatorChooser();
                          SkylineWindow.ChooseCalculator(irtCalc);

                          stats = SkylineWindow.RTGraphController.RegressionRefined.CalcStatistics(docPeptides, null);
                          line = SkylineWindow.RTGraphController.RegressionRefined.Conversion;
                      });
            Assert.IsNotNull(stats);
            Assert.IsTrue(stats.R > 0.999);
            Assert.IsNotNull(line);
            //These values were taken from the graph, which shows values to 2 decimal places, so the real values must
            //be +/- 0.01 from these values
            Assert.IsTrue(Math.Abs(line.Intercept - 14.17) < 0.01);
            Assert.IsTrue(Math.Abs(line.Slope - 0.15) < 0.01);

            RunUI(() =>
                      {
                          SkylineWindow.ChooseCalculator(ssrCalc);

                          stats = SkylineWindow.RTGraphController.RegressionRefined.CalcStatistics(docPeptides, null);
                      });
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
                RunDlg<AddIrtPeptidesDlg>(addDlg.OkDialog, addPepDlg => addPepDlg.OkDialog());
                Assert.AreEqual(18, irtDlgAdd.LibraryPeptideCount);

                OkDialog(irtDlgAdd, irtDlgAdd.CancelButton.PerformClick);
            }

            // Add from existing calculator
            {
                var irtDlgAdd = ShowDialog<EditIrtCalcDlg>(editRT2.EditCurrentCalculator);
                var addDlg = ShowDialog<AddIrtCalculatorDlg>(irtDlgAdd.AddIrtDatabase);
                RunUI(() => addDlg.CalculatorName = irtCalc);
                RunDlg<AddIrtPeptidesDlg>(addDlg.OkDialog, addPepDlg => addPepDlg.OkDialog());
                Assert.AreEqual(18, irtDlgAdd.LibraryPeptideCount);

                OkDialog(irtDlgAdd, irtDlgAdd.CancelButton.PerformClick);
            }

            OkDialog(editRT2, editRT2.CancelButton.PerformClick);
            OkDialog(peptideSettingsDlg2, peptideSettingsDlg2.CancelButton.PerformClick);
            WaitForClosedForm(peptideSettingsDlg2);

            //Restore the document to contain all 29 peptides
            RunUI(SkylineWindow.Undo);


            //Open peptide settings
            var peptideSettingsDlg3 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);

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
            RunDlg<AddIrtPeptidesDlg>(irtDlg3.AddResults, addPeptidesDlg => addPeptidesDlg.OkDialog());

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
                RunDlg<AddIrtPeptidesDlg>(dlg.AddResults, addDlg =>
                                                              {
                                                                  addDlg.Action = AddIrtPeptidesAction.skip;
                                                                  addDlg.OkDialog();
                                                              });
            }

            RunUI(dlg.OkDialog);
            WaitForClosedForm(dlg);
        }
    }
}
