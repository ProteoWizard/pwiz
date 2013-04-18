using Microsoft.VisualStudio.TestTools.UnitTesting;
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

            using (new DotTraceProfile())
            {
                if (_dataFile is string)
                    ImportResultsFile((string)_dataFile, 60*60);    // Allow 60 minutes for loading.
                else
                    ImportResultsFiles((string[])_dataFile, 60*60); // Allow 60 minutes for loading.
            }

            PauseAndContinue();
        }
    }
}
