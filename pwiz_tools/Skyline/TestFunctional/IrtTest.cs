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
using System.Data.SQLite;
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

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion
        
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

            var peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var editRT = ShowDialog<EditRTDlg>(peptideSettingsDlg.AddRTRegression);
            var irtDlg = ShowDialog<EditIrtCalcDlg>(editRT.AddCalculator);
            EditIrtCalcDlg irtDlg1 = irtDlg;
            RunUI(() =>
                {
                    irtDlg1.SetStandardName(irtCalc);
                    irtDlg1.SetDatabasePath(databasePath);
                });

            List<MeasuredPeptide> calibratePeptides = new List<MeasuredPeptide>();

            /*
             * Check several error handling cases
             * Check the peptide choosing algorithm for sanity (correct # peptides)
             * Check that the info dialog comes up when too many are requested
             * Check the peptide linear transformation for sanity
             * Check that the peptides get passed back to EditIrtCalcDlg
             */
            var calibrateDlg = ShowDialog<CalibrateIrtDlg>(irtDlg.Calibrate);

            CalibrateIrtDlg calibrateDlg1 = calibrateDlg;
            RunUI(() => calibrateDlg1.SetNumPeptides(1));
            //Can't create a standard with < 1 peptide
            RunDlg<MessageDlg>(() => calibrateDlg1.Recalculate(), messageDlg => messageDlg.OkDialog());

            //Check the peptide choosing algorithm
            int peptideCount = SkylineWindow.Document.Peptides.Count();
            for (int i = peptideCount; i >= CalibrateIrtDlg.MIN_STANDARD_PEPTIDES; i--) //29 peptides in the document
            {
                int j = i;
                RunUI(() =>
                          {
                              calibrateDlg1.SetNumPeptides(j);
                              calibratePeptides = calibrateDlg1.Recalculate();
                              Assert.AreEqual(calibratePeptides.Count, j);
                          });
            }

            RunUI(() => calibrateDlg1.SetNumPeptides(peptideCount + 1));

            //Can't use more peptides than there are in the document
            RunDlg<MessageDlg>(() => calibratePeptides = calibrateDlg1.Recalculate(),
                messageDlg => messageDlg.OkDialog());

            Assert.AreEqual(peptideCount, calibratePeptides.Count);

            RunUI(() =>
                      {
                          calibrateDlg1.SetNumPeptides(11);
                          calibrateDlg1.Recalculate();
                          //After closing this dialog, there should be 3 iRT values below 0
                          //and 3 above 100
                          calibrateDlg1.SetBoxesChecked(4, 8);

                          calibrateDlg1.OkDialog();
                      });
            WaitForClosedForm(calibrateDlg);

            //Now check that the peptides were passed to the EditIrtCalcDlg
            EditIrtCalcDlg dlg = irtDlg;
            RunUI(() =>
                      {
                          Assert.IsTrue(dlg.GetStandard(out calibratePeptides));
                          Assert.AreEqual(numStandardPeps, calibratePeptides.Count);
                          //And that there are 3 below 0 and 3 above 100
                          Assert.AreEqual(3, calibratePeptides.FindAll(pep => pep.RetentionTimeOrIrt < 0).Count);
                          Assert.AreEqual(3, calibratePeptides.FindAll(pep => pep.RetentionTimeOrIrt > 100).Count);
                      });

            /*
             * Test pasting into EditIrtCalcDlg
             * Test that the dialog requires the whole standard to be in the document 
             * Test that add results gets everything in the document besides the standard
             * Test that there were no errors along the way
             */

            //Now paste in iRT with each peptide truncated by one amino acid
            string standard = new StringBuilder()
                .Append("LGGNEQVT").Append('\t').Append(-24.92).AppendLine()
                .Append("GAGSSEPVTGLDA").Append('\t').Append(0.00).AppendLine()
                .Append("VEATFGVDESNA").Append('\t').Append(12.39).AppendLine()
                .Append("YILAGVENS").Append('\t').Append(19.79).AppendLine()
                .Append("TPVISGGPYEY").Append('\t').Append(28.71).AppendLine()
                .Append("TPVITGAPYEY").Append('\t').Append(33.38).AppendLine()
                .Append("DGLDAASYYAPV").Append('\t').Append(42.26).AppendLine()
                .Append("ADVTPADFSEWS").Append('\t').Append(54.62).AppendLine()
                .Append("GTFIIDPGGVI").Append('\t').Append(70.52).AppendLine()
                .Append("GTFIIDPAAVI").Append('\t').Append(87.23).AppendLine()
                .Append("LFLQFGAQGSPFL").Append('\t').Append(100.00).AppendLine()
                .ToString();

            string standard1 = standard;
            EditIrtCalcDlg irtDlg2 = irtDlg;
            RunUI(() =>
                      {
                          SetClipboardText(standard1);
                          irtDlg2.DoPaste();
                      });

            // Cannot add results because standard peptides are not in the document
            RunDlg<MessageDlg>(irtDlg.AddResults, messageDlg => messageDlg.OkDialog());

            // Paste Biognosys-provided values
            standard = new StringBuilder()
                .Append("LGGNEQVTR").Append('\t').Append(-24.92).AppendLine()
                .Append("GAGSSEPVTGLDAK").Append('\t').Append(0.00).AppendLine()
                .Append("VEATFGVDESNAK").Append('\t').Append(12.39).AppendLine()
                .Append("YILAGVENSK").Append('\t').Append(19.79).AppendLine()
                .Append("TPVISGGPYEYR").Append('\t').Append(28.71).AppendLine()
                .Append("TPVITGAPYEYR").Append('\t').Append(33.38).AppendLine()
                .Append("DGLDAASYYAPVR").Append('\t').Append(42.26).AppendLine()
                .Append("ADVTPADFSEWSK").Append('\t').Append(54.62).AppendLine()
                .Append("GTFIIDPGGVIR").Append('\t').Append(70.52).AppendLine()
                .Append("GTFIIDPAAVIR").Append('\t').Append(87.23).AppendLine()
                .Append("LFLQFGAQGSPFLK").Append('\t').Append(100.00).AppendLine()
                .ToString();

            EditIrtCalcDlg irtDlg3 = irtDlg;
            RunUI(() =>
            {
                SetClipboardText(standard);
                irtDlg3.DoPaste();

                //Check count
                Assert.AreEqual(numStandardPeps, irtDlg3.GetNumStandardPeptides());

                //Add results
                irtDlg3.AddResults();
                Assert.AreEqual(numLibraryPeps, irtDlg3.UpdateNumPeptides());

                irtDlg3.OkDialog();
            });

            WaitForClosedForm(irtDlg3);

            Assert.IsNull(FindOpenForm<MessageDlg>());

            /*
             * Check that the database was created successfully
             * Check that it has the correct numbers of standard and library peptides
             */
            try
            {
                IrtDb db = IrtDb.OpenIrtDb(databasePath);

                Assert.AreEqual(numStandardPeps, db.GetStandard().Count);
                Assert.AreEqual(numLibraryPeps, db.GetLibrary().Count);
            }
            catch(SQLiteException)
            {
                Assert.Fail();
            }
            catch(Exception)
            {
                Assert.Fail();
            }

            /*
             * Make sure that loading the database brings back up the right numbers of peptides
             */

            //Rather than rigging SettingsListComboDriver, just create a new one and load
            irtDlg = ShowDialog<EditIrtCalcDlg>(editRT.AddCalculator);

            var irtDlg4 = irtDlg;
            RunUI(() => irtDlg4.SetDatabasePath(testFilesDir.GetTestPath("bogus.irtdb")));

            RunDlg<MessageDlg>(irtDlg.OpenDatabase, messageDlg => messageDlg.OkDialog());

            //There was a bu.g where opening a path and then clicking OK would save all the peptides
            //twice, doubling the size of the database. So check that that is fixed.
            EditIrtCalcDlgPepCountTest(irtDlg, numStandardPeps, numLibraryPeps, databasePath, false);
            irtDlg = ShowDialog<EditIrtCalcDlg>(editRT.AddCalculator);
            EditIrtCalcDlgPepCountTest(irtDlg, numStandardPeps, numLibraryPeps, databasePath, false);
            irtDlg = ShowDialog<EditIrtCalcDlg>(editRT.AddCalculator);
            EditIrtCalcDlgPepCountTest(irtDlg, numStandardPeps, numLibraryPeps, databasePath, true);
            irtDlg = ShowDialog<EditIrtCalcDlg>(editRT.AddCalculator);
            EditIrtCalcDlgPepCountTest(irtDlg, numStandardPeps, numLibraryPeps, databasePath, false);

            /* 
             * Create a regression based on the new calculator
             * Create a regression based on SSRCalc
             * Open the graph
             * Switch to new calculator, verify r = 1.00 and graph is labeled iRT-C18
             * Switch to SSRCalc, verify graph label changes
             */

            EditRTDlg editRT1 = editRT;
            RunUI(() =>
                      {
                          editRT1.SetRegressionName("iRT Regression");
                          editRT1.AddResults();
                          editRT1.ChooseCalculator(irtCalc);
                          editRT1.OkDialog();
                      });
            WaitForClosedForm(editRT);

            editRT = ShowDialog<EditRTDlg>(peptideSettingsDlg.AddRTRegression);

            EditRTDlg editRT2 = editRT;
            RunUI(() =>
                      {
                          editRT2.SetRegressionName("SSRCalc Regression");
                          editRT2.AddResults();
                          editRT2.ChooseCalculator(ssrCalc);
                          editRT2.OkDialog();
                      });

            WaitForClosedForm(editRT);
            RunUI(peptideSettingsDlg.CancelButton.PerformClick);
            WaitForClosedForm(peptideSettingsDlg);

            var docPeptides = new List<MeasuredRetentionTime>();
            RunUI(() =>
                      {
                          foreach (var docPepNode in Program.ActiveDocumentUI.Peptides)
                          {
                              docPeptides.Add(new MeasuredRetentionTime(docPepNode.Peptide.Sequence,
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
            peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            editRT = ShowDialog<EditRTDlg>(peptideSettingsDlg.AddRTRegression);

            //Set regression name
            EditRTDlg editRT3 = editRT;
            RunUI(() => editRT3.SetRegressionName("iRT Document Regression"));

            //Regression dialog -> add a calculator
            irtDlg = ShowDialog<EditIrtCalcDlg>(editRT.AddCalculator);

            //Set calc name, database
            var irtDlg5 = irtDlg;
            RunUI(() =>
                      {
                          irtDlg5.SetStandardName("iRT Document Calculator");
                          irtDlg5.CreateDatabase(testFilesDir.GetTestPath("irt-doc.irtdb"));
                      });

            //Calc dialog -> calibrate standard
            calibrateDlg = ShowDialog<CalibrateIrtDlg>(irtDlg.Calibrate);

            //Get 11 peptides from the document (all of them) and go back to calculator dialog
            RunUI(() =>
                      {
                          calibrateDlg.SetNumPeptides(11);
                          calibrateDlg.Recalculate();
                          calibrateDlg.OkDialog();
                      });
            WaitForClosedForm(calibrateDlg);

            //WaitForCondition(5000000, () => false);

            //Can't add results since the document doesn't have anything but the standard. Close dialogs to
            //get back to Skyline
            RunUI(irtDlg5.OkDialog);
            WaitForClosedForm(irtDlg5);
            RunUI(editRT.CancelButton.PerformClick);
            WaitForClosedForm(editRT);
            RunUI(peptideSettingsDlg.CancelButton.PerformClick);
            WaitForClosedForm(peptideSettingsDlg);

            //Restore the document to contain all 29 peptides
            RunUI(SkylineWindow.Undo);


            //Open peptide settings
            peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);

            //Add a new regression
            editRT = ShowDialog<EditRTDlg>(peptideSettingsDlg.AddRTRegression);

            EditRTDlg editRT5 = editRT;
            RunUI(() => editRT5.SetRegressionName("iRT Document Regression"));

            //Edit the calculator list
            var editCalculator =
                ShowDialog<EditListDlg<SettingsListBase<RetentionScoreCalculatorSpec>, RetentionScoreCalculatorSpec>>(
                    editRT.EditCalculatorList);

            RunUI(() => editCalculator.SelectItem("iRT Document Calculator"));

            //Edit the document-based calculator
            irtDlg = ShowDialog<EditIrtCalcDlg>(editCalculator.EditItem);

            //Add the 18 non-standard peptides to the calculator, then OkDialog back to Skyline
            EditIrtCalcDlg irtDlg6 = irtDlg;
            RunUI(() =>
                      {
                          irtDlg6.AddResults();
                          Assert.AreEqual(18, irtDlg6.UpdateNumPeptides());
                          irtDlg6.OkDialog();
                      });
            WaitForClosedForm(irtDlg6);
            
            RunUI(editCalculator.OkDialog);
            WaitForClosedForm(editCalculator);

            EditRTDlg editRT4 = editRT;
            RunUI(() =>
                      {
                          editRT4.AddResults();
                          editRT4.ChooseCalculator("iRT Document Calculator");
                          editRT4.SetTimeWindow(2.0);
                          editRT4.OkDialog();
                      });
            WaitForClosedForm(editRT);

            //Then choose the new, document-based regression and turn off prediction
            PeptideSettingsUI peptideSettingsDlg1 = peptideSettingsDlg;
            RunUI(() =>
                      {
                          peptideSettingsDlg1.ChooseRegression("iRT Document Regression");
                          peptideSettingsDlg1.UseMeasuredRT(true);
                          peptideSettingsDlg1.OkDialog();

                      });
            
            WaitForClosedForm(peptideSettingsDlg);

            Assert.IsNull(FindOpenForm<MessageDlg>());

            //Export the measurement-based transition list
            var expMethodDlg = ShowDialog<ExportMethodDlg>(SkylineWindow.ShowExportTransitionListDlg);
            ExportMethodDlg expMethodDlg1 = expMethodDlg;
            RunUI(() =>
                        {
                            expMethodDlg1.SetInstrument(ExportInstrumentType.Thermo);
                            expMethodDlg1.SetMethodType(ExportMethodType.Scheduled);
                            expMethodDlg1.OkDialog(testFilesDir.GetTestPath("EmpiricalTL.csv"));
                        });

            WaitForClosedForm(expMethodDlg1);

            //Turn on prediction for scheduling
            peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var peptideSettingsDlg2 = peptideSettingsDlg;
            RunUI(() =>
                                          {
                                              peptideSettingsDlg2.UseMeasuredRT(false);
                                              peptideSettingsDlg2.OkDialog();
                                          });
            WaitForClosedForm(peptideSettingsDlg);

            //Export the prediction-based transition list 
            expMethodDlg = ShowDialog<ExportMethodDlg>(SkylineWindow.ShowExportTransitionListDlg);
            RunUI(() =>
            {
                expMethodDlg.SetInstrument(ExportInstrumentType.Thermo);
                expMethodDlg.SetMethodType(ExportMethodType.Scheduled);
                expMethodDlg.OkDialog(testFilesDir.GetTestPath("PredictionTL.csv"));
            });

            WaitForClosedForm(expMethodDlg);

            //Now open both files and compare

            var expected = File.ReadAllText(testFilesDir.GetTestPath("EmpiricalTL.csv"));
            var actual = File.ReadAllText(testFilesDir.GetTestPath("PredictionTL.csv"));

            AssertEx.NoDiff(expected, actual);
            
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

            peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            editRT = ShowDialog<EditRTDlg>(peptideSettingsDlg.AddRTRegression);

            //Add results and switch to a calculator whose database is not connected: error
            RunDlg<MessageDlg>(() =>
                                   {
                                       editRT.AddResults();
                                       editRT.ChooseCalculator(irtCalc);
                                   },
                                   errorMessage => errorMessage.OkDialog());

            //Go to add a new calculator
            irtDlg = ShowDialog<EditIrtCalcDlg>(editRT.AddCalculator);

            //Try to open a file that does not exist: error
            RunDlg<MessageDlg>(() =>
                                   {
                                       irtDlg.SetDatabasePath(databasePath);
                                       irtDlg.OpenDatabase();
                                   }, messageDlg => messageDlg.OkDialog());

            RunUI(() => irtDlg.CancelButton.PerformClick());
            WaitForClosedForm(irtDlg);

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
                          editRT.SetRegressionName("iRT Test Regression");
                          editRT.AddResults();
                          editRT.ChooseCalculator(irtCalc);
                          editRT.OkDialog();
                      });

            WaitForClosedForm(editRT);
            RunUI(() =>
                      {
                          peptideSettingsDlg.ChooseRegression("iRT Test Regression");
                          peptideSettingsDlg.UseMeasuredRT(false); //Use prediction
                          peptideSettingsDlg.OkDialog();
                      });
            WaitForClosedForm(peptideSettingsDlg);

            //Switch the file back to the copy, destroying the original
            stream = File.Create(testFilesDir.GetTestPath("irt-c18-copy.irtdb"));
            stream.Close();
            File.Replace(databasePath, testFilesDir.GetTestPath("irt-c18-copy.irtdb"),
                         testFilesDir.GetTestPath("backup.irtdb"));

            var exportTransList = ShowDialog<ExportMethodDlg>(SkylineWindow.ShowExportTransitionListDlg);

            RunDlg<MessageDlg>(() => exportTransList.SetMethodType(ExportMethodType.Scheduled),
                               errorMessage => errorMessage.OkDialog());

            RunUI(() => exportTransList.CancelButton.PerformClick());
            WaitForClosedForm(exportTransList);

            RunDlg<MessageDlg>(() => SkylineWindow.ChooseCalculator(irtCalc),
                               errorMessage => errorMessage.OkDialog());

            /*
             * Now clean up by deleting all these calculators. If we don't, then the next functional test
             * will fail because it will try to use a calculator from settings which will not have its
             * database.
             */
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsDlg3 =>
                                                            {
                                                                peptideSettingsDlg3.ChooseRegression("None");
                                                                peptideSettingsDlg3.OkDialog();
                                                            });
        }

        public void EditIrtCalcDlgPepCountTest(EditIrtCalcDlg dlg, int numStandardPeps, int numLibraryPeps, string path, bool add)
        {
            RunUI(() =>
                      {
                          Assert.AreEqual(0, dlg.GetNumStandardPeptides());
                          Assert.AreEqual(0, dlg.UpdateNumPeptides());
                          dlg.SetStandardName("Testing");
                          dlg.SetDatabasePath(path);
                          dlg.OpenDatabase();

                          Assert.AreEqual(numStandardPeps, dlg.GetNumStandardPeptides());
                          Assert.AreEqual(numLibraryPeps, dlg.UpdateNumPeptides());

                          if(add)
                              dlg.AddResults();
                          
                          dlg.OkDialog();
                      });

            WaitForClosedForm(dlg);
        }
    }
}
