/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ProfileImport : AbstractFunctionalTest
    {
        private string _skyFile;
        private object _dataFile;

        //[TestMethod]
        public void ProfileImportMS1()
        {
            //if (!IsProfiling) return;

            // From TestMs1Tutorial:
            _skyFile = @"c:\users\donmarsh\documents\perf\importMS1\Template_MS1 Filtering_1118_2011_3.sky";
            _dataFile = @"100803_0005b_MCF7_TiTip3.wiff";
            
            RunFunctionalTest();
        }

        //[TestMethod]
        public void ProfileImportTargetedMSMS()
        {
            //if (!IsProfiling) return;

            // From TestTargetedMSMSTutorial.LowResTest:
            _skyFile = @"C:\Users\donmarsh\Documents\perf\importTargetedMSMS\Low Res\BSA_Protea_label_free_meth3.sky";
            _dataFile = new[]
                {
                    @"Low Res\klc_20100329v_Protea_Peptide_Curve_20fmol_uL_tech1.raw",
                    @"Low Res\klc_20100329v_Protea_Peptide_Curve_80fmol_uL_tech1.raw"
                };
            
            RunFunctionalTest();
        }

        //[TestMethod]
        public void ProfileImportBigWiff()
        {
            //if (!IsProfiling) return;

            // From Brendanx/data/MacCoss/FullScan/20111031_Bigit_buckmemory/Birgit(Dylan):
            _skyFile = @"C:\Users\donmarsh\Documents\perf\importBigWiff\Birgit(Dylan)\mito_throughput_1003_2011_v14f.sky";
            _dataFile = @"110914_0038_A0Mito_rep1_.wiff";
            
            RunFunctionalTest();
        }

        //[TestMethod]
        public void ProfileImportBigRaw()
        {
            //if (!IsProfiling) return;

            // From Brendanx/data/Issues/20130311_Don_stress:
            _skyFile = @"C:\Users\donmarsh\Documents\20130311_Don_stress\Skyline - all - 2.sky";
            _dataFile = @"20120821_103B_242_01.raw";
            
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(_skyFile));

            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, dlg =>
            {
                dlg.RemoveAllReplicates();
                dlg.OkDialog();
            });

            using (new DotTraceProfile())
            {
                string fileName = _dataFile as string;
                if (fileName != null)
                    ImportResultsFile(fileName, 60*60);    // Allow 60 minutes for loading.
                else
                    ImportResultsFiles(((string[])_dataFile).Select(MsDataFileUri.Parse), 60*60); // Allow 60 minutes for loading.
            }

            //PauseAndContinue();
        }
    }
}
