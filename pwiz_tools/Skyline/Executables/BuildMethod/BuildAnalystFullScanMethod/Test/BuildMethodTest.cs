using System;
using System.IO;
using AcqMethodSvrLib;
using Analyst;
using BuildAnalystMethod;
using Interop.DDEMethodSvr;
using MSMethodSvrLib;
using NUnit.Framework;




namespace BuildAnalystFullScanMethod.Test
{
    [TestFixture]
    class BuildMethodTest
    {
        private const string METHOD_FILE_IDA_5600 = "IDA-5600.dam";
        private const string METHOD_FILE_IDA_QSTAR = "IDA-QSTAR.dam";
        private const string METHOD_FILE_5600 = "5600.dam";
        private const string METHOD_FILE_QSTAR = "QSTAR.dam";

        [Test]
        public void TestIDAUnscheduled()
        {
            TestIDAUnscheduled(IsQSTAR() ? METHOD_FILE_IDA_QSTAR : METHOD_FILE_IDA_5600);
        }

        [Test]
        public void TestIDAScheduled()
        {
            TestIDAScheduled(IsQSTAR() ? METHOD_FILE_IDA_QSTAR : METHOD_FILE_IDA_5600);
        }

        [Test]
        public void TestTargetedMsmsNoTOFMs()
        {
            TestTargetedMsmsNoTOFMs(IsQSTAR() ? METHOD_FILE_QSTAR : METHOD_FILE_5600);
        }
        
        [Test]
        public void TestTargetedMsmsTOFMs()
        {
            TestTargetedMsmsTOFMs(IsQSTAR() ? METHOD_FILE_QSTAR : METHOD_FILE_5600);
        }

        private static Boolean IsQSTAR()
        {
           
            var analyst = new ApplicationClass();

            // Make sure that Analyst is fully started
            var acqMethodDir = (IAcqMethodDirConfig)analyst.Acquire();
            if (acqMethodDir == null)
                throw new IOException("Failed to initialize.  Analyst may need to be started.");


            string methodFilePath = Path.GetFullPath(GetProjectDirectory() + METHOD_FILE_IDA_QSTAR);

            object acqMethodObj;
            acqMethodDir.LoadNonUIMethod(methodFilePath, out acqMethodObj);
            var acqMethod = (IAcqMethod)acqMethodObj;


            var builder = new BuildAnalystFullScanMethod();
            var method = builder.ExtractMsMethod(acqMethod);

            // TODO there must be a better way to figure out which version of Analyst we have
            return method != null;


//            var period = (Period)method.GetPeriod(0);
//
//            var srcParamsTbl = (ParamDataColl)((Experiment)period.GetExperiment(0)).SourceParamsTbl;
//            
//            const string ionSprayVoltageParamName = "ISVF";
//            
//            try
//            {
//                short s;
//                var parameterData = ((ParameterData)srcParamsTbl.FindParameter(ionSprayVoltageParamName, out s));
//                return false;
//            }
//            catch (Exception)
//            {
//                return true;
//            }
        }

        private static void TestIDAUnscheduled(string templateMethodFile)
        {

            string projectDirectory = GetProjectDirectory();

            var args = new[] { "-i", projectDirectory+templateMethodFile, projectDirectory+"Study 7 unsched.csv" };
            Program.Main(args);

            string methodFilePath = Path.GetFullPath(projectDirectory+"Study 7 unsched.dam");
            TestIDACommon(methodFilePath, false);
            
        }

        private static void TestIDAScheduled(string templateMethodFile)
        {

            string projectDirectory = GetProjectDirectory();

            var args = new[] { "-i", "-r", projectDirectory + templateMethodFile, projectDirectory + "Study 7 sched.csv" };
            Program.Main(args);

            string methodFilePath = Path.GetFullPath(projectDirectory+"Study 7 sched.dam");
            
            TestIDACommon(methodFilePath, true);


        }

        private static string GetProjectDirectory()
        {
            string projectDirectory = Directory.GetCurrentDirectory();
            int idx = projectDirectory.IndexOf("bin");
            if (idx != -1)
            {
                projectDirectory = projectDirectory.Substring(0, idx);
            }
            return projectDirectory;
        }

        private static MassSpecMethod GetMethod(string methodFilePath)
        {

            var analyst = new ApplicationClass();

            // Make sure that Analyst is fully started
            var acqMethodDir = (IAcqMethodDirConfig)analyst.Acquire();
            if (acqMethodDir == null)
                throw new IOException("Failed to initialize.  Analyst may need to be started.");



            object acqMethodObj;
            acqMethodDir.LoadNonUIMethod(methodFilePath, out acqMethodObj);
            var acqMethod = (IAcqMethod)acqMethodObj;


            var builder = new BuildAnalystFullScanMethod();
            var method = builder.ExtractMsMethod(acqMethod);

            return method;
        }


        // read the updated method and make sure that the inclusion list is in the method
        private static void TestIDACommon(string methodFilePath, Boolean isScheduled)
        {

            MassSpecMethod method = GetMethod(methodFilePath);

            // The method should have an inclusion list
            object idaServer;
            ((IMassSpecMethod2)method).GetDataDependSvr(out idaServer);
            var useInclusionList = 0;
            ((IDDEMethodObj)idaServer).getUseIncludeList(ref useInclusionList);
            Assert.AreEqual(1, useInclusionList);

            // The inclusion list should have 20 entries.
            int inclusionListSize = 0;
            ((IDDEMethodObj)idaServer).GetIonListSize(1, ref inclusionListSize);
            Assert.AreEqual(20, inclusionListSize);

            
            // test m/z for the first entry in the inclusion list
            double mz = 0;
            ((IDDEMethodObj)idaServer).GetIncludeIonEntry(0, ref mz);
            Assert.AreEqual(744.839753, mz);

            // test m/z the last entry in the inclusion list
            ((IDDEMethodObj)idaServer).GetIncludeIonEntry(19, ref mz);
            Assert.AreEqual(572.791863, mz);

            // test retention time
            if(isScheduled)
            {
              
                // Since this is an scheduled method, the retention time of each entry should be a non-zero value
                double rt = -1;
                ((IDDEMethodObj3)idaServer).GetIncludeRetTimeEntry(0, ref rt);
                Assert.AreEqual(19.49, rt);

                
                ((IDDEMethodObj3)idaServer).GetIncludeRetTimeEntry(19, ref rt);
                Assert.AreEqual(20.48, rt);

            }
            else
            {
                // Since this is an unscheduled method the retention time of each entry should
                // be set to 0
                double rt = -1;
                ((IDDEMethodObj3) idaServer).GetIncludeRetTimeEntry(0, ref rt);
                Assert.AreEqual(0, rt);

            
                ((IDDEMethodObj3)idaServer).GetIncludeRetTimeEntry(19, ref rt);
                Assert.AreEqual(0, rt);
            }
            
        }

        private static void TestTargetedMsmsNoTOFMs(string templateMethodFile)
        {
            string projectDirectory = GetProjectDirectory();

            var args = new[] { projectDirectory + templateMethodFile, projectDirectory + "Study 7 sched.csv" };
            Program.Main(args);

            string methodFilePath = Path.GetFullPath(projectDirectory + "Study 7 sched.dam");
            TestTargetedMsmsCommon(methodFilePath, (templateMethodFile == METHOD_FILE_QSTAR), false);
        }

        private static void TestTargetedMsmsTOFMs(string templateMethodFile)
        {
            string projectDirectory = GetProjectDirectory();

            var args = new[] {"-1", projectDirectory + templateMethodFile, projectDirectory + "Study 7 sched.csv" };
            Program.Main(args);

            string methodFilePath = Path.GetFullPath(projectDirectory + "Study 7 sched.dam");

            TestTargetedMsmsCommon(methodFilePath, (templateMethodFile == METHOD_FILE_QSTAR), true);
        }

        private static void TestTargetedMsmsCommon(string methodFilePath, Boolean isQstar, Boolean doTOFMs)
        {

            var method = GetMethod(methodFilePath);
            Assert.AreEqual(1, method.PeriodCount);

            var period = (Period) method.GetPeriod(0);


            if (doTOFMs)
            {
                // We should have 1 "TOF MS" experiments and 20 "Product Ion" experiments
                Assert.AreEqual(21, period.ExperimCount);

                // first experiment should be a TOF experiment
                var experiment = (Experiment) period.GetExperiment(0);
                Assert.AreEqual(8, experiment.ScanType);

                experiment = (Experiment)period.GetExperiment(1);
                Assert.AreEqual((isQstar ? 6 : 9), experiment.ScanType);
                
            }
            else
            {
                // We should have 20 "Product Ion" experiments
                Assert.AreEqual(20, period.ExperimCount);

                var experiment = (Experiment)period.GetExperiment(0);
                Assert.AreEqual( (isQstar ? 6 : 9), experiment.ScanType);
            }
        }
        
    }
}
