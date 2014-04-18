/*
 * Original author: Dario Amodei <damodei .at. stanford.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford University
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    //[TestClass]
    public class PerfPeakTest : AbstractFunctionalTest
    {
        //[TestMethod]
        public void TestPeakPerf()
        {
            TestFilesZipPaths = new[]
            {
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/OpenSWATH_Water.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/OpenSWATH_Yeast.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/OpenSWATH_Human.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/Olga_srm_course_vantage.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/Olga_srm_course.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/HeartFailure.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/OvarianCancer.zip",
            };
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Import settings 
            var skylineSettings = TestFilesDir.GetTestPath("settings_test.skys");
            RunUI(() => ShareListDlg<SrmSettingsList, SrmSettings>.ImportFile(SkylineWindow, Settings.Default.SrmSettingsList, skylineSettings));
            // TODO: Add default settings to cover the models we want to use
        }

        protected class DataSetAnalyzer
        {
            public DataSetAnalyzer(string skylineDocument, IList<string> modelNames, IEnumerable<string> filePaths, string outDir)
            {
                RunUI(() => SkylineWindow.OpenFile(skylineDocument));
                WaitForDocumentLoaded();
                foreach (var modelName in modelNames)
                {
                    TrainModel(modelName);
                }
                var comparePeakPickingDlg = ShowDialog<ComparePeakPickingDlg>(SkylineWindow.ShowCompareModelsDlg);
                foreach (var modelName in modelNames)
                {
                    AddModel(comparePeakPickingDlg, modelName);
                }
                foreach (var filePath in filePaths)
                {
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    Assert.IsNotNull(fileName);
                    int stringSize = Math.Min(fileName.Length, 15);
                    fileName = fileName.Take(stringSize).ToString();
                    // TODO: this needs to handle the warning dialogs that pop up
                    AddFile(comparePeakPickingDlg, fileName, filePath);
                }
                int saveStringSize = Math.Min(skylineDocument.Length, 15);
                var sbRoc = new StringBuilder(skylineDocument.Take(saveStringSize).ToString());
                sbRoc.Append("-roc.bmp");
                comparePeakPickingDlg.ZedGraphRoc.MasterPane.GetImage().Save(Path.Combine(outDir, sbRoc.ToString()));
                var sbQq = new StringBuilder(skylineDocument.Take(saveStringSize).ToString());
                sbQq.Append("-qq.bmp");
                comparePeakPickingDlg.ZedGraphQq.MasterPane.GetImage().Save(Path.Combine(outDir, sbQq.ToString()));
                // TODO: How can I copy the data using commands?
                OkDialog(comparePeakPickingDlg, comparePeakPickingDlg.OkDialog);
            }

            public static void AddModel(ComparePeakPickingDlg dlg, string modelName)
            {
                var addPeakCompareDlg = ShowDialog<AddPeakCompareDlg>(dlg.Add);
                RunUI(() =>
                {
                    addPeakCompareDlg.IsModel = true;
                    addPeakCompareDlg.PeakScoringModelSelected = modelName;
                });
                OkDialog(addPeakCompareDlg, addPeakCompareDlg.OkDialog);
            }

            private static void AddFile(ComparePeakPickingDlg dlg, string fileName, string filePath)
            {
                var addPeakCompareDlg = ShowDialog<AddPeakCompareDlg>(dlg.Add);
                RunUI(() =>
                {
                    addPeakCompareDlg.IsModel = false;
                    addPeakCompareDlg.FileName = fileName;
                    addPeakCompareDlg.FilePath = filePath;
                });

                OkDialog(addPeakCompareDlg, addPeakCompareDlg.OkDialog);
            }

            public static void TrainModel(string modelName)
            {
                RunEditPeakScoringDlg(modelName, true, editPeakScoringModelDlg =>
                {
                    editPeakScoringModelDlg.TrainModel();
                    editPeakScoringModelDlg.OkDialog();
                });
            }

            protected static void RunEditPeakScoringDlg(string editName, bool cancelReintegrate, Action<EditPeakScoringModelDlg> act)
            {
                var reintegrateDlg = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
                if (editName != null)
                {
                    var editList = ShowDialog<EditListDlg<SettingsListBase<PeakScoringModelSpec>, PeakScoringModelSpec>>(
                        reintegrateDlg.EditPeakScoringModel);
                    RunUI(() => editList.SelectItem(editName)); // Not L10N
                    RunDlg(editList.EditItem, act);
                    OkDialog(editList, editList.OkDialog);
                }
                else
                {
                    RunDlg(reintegrateDlg.AddPeakScoringModel, act);
                }
                if (cancelReintegrate)
                {
                    OkDialog(reintegrateDlg, reintegrateDlg.CancelDialog);
                }
                else
                {
                    OkDialog(reintegrateDlg, reintegrateDlg.OkDialog);
                }
            }
        }
    }
}
