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
        [Test]
        public void TestIDAUnscheduled()
        {

            // IDA-QSTAR.dam -i "Study 7 unsched.csv"
            var args = new [] { "-i", @"..\..\..\IDA-QSTAR.dam", @"..\..\..\Study 7 unsched.csv" };
            Program.Main(args);


            var builder = new BuildAnalystFullScanMethod();
            
            // read the updated method and make sure that the inclusion list is in the method
            var analyst = new ApplicationClass();

            // Make sure that Analyst is fully started
            var acqMethodDir = (IAcqMethodDirConfig)analyst.Acquire();
            if (acqMethodDir == null)
                throw new IOException("Failed to initialize.  Analyst may need to be started.");


            string methodFilePath = Path.GetFullPath(@"..\..\..\Study 7 unsched.dam");
            object acqMethodObj;
            acqMethodDir.LoadNonUIMethod(methodFilePath, out acqMethodObj);
            var acqMethod = (IAcqMethod)acqMethodObj;


            var method = builder.ExtractMsMethod(acqMethod);

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

            // Since this is an unscheduled method the retention time of each entry should
            // be set to 0
            // test for the first entry in the inclusion list
            double mz = 0;
            ((IDDEMethodObj)idaServer).GetIncludeIonEntry(0, ref mz);
            Assert.AreEqual(744.839753, mz);

            double rt = -1;
            ((IDDEMethodObj3) idaServer).GetIncludeRetTimeEntry(0, ref rt);
            Assert.AreEqual(0, rt);

            // test for the last entry in the inclusion list
            ((IDDEMethodObj)idaServer).GetIncludeIonEntry(19, ref mz);
            Assert.AreEqual(572.791863, mz);

            ((IDDEMethodObj3)idaServer).GetIncludeRetTimeEntry(0, ref rt);
            Assert.AreEqual(0, rt);

            
            
        }

        [Test]
        public void TestIDAScheduled()
        {

            // IDA-QSTAR.dam -i -r "Study 7 sched.csv"
            var args = new[] { "-i", "-r", @"..\..\..\IDA-QSTAR.dam", @"..\..\..\Study 7 sched.csv" };
            Program.Main(args);


            var builder = new BuildAnalystFullScanMethod();

            // read the updated method and make sure that the inclusion list is in the method
            var analyst = new ApplicationClass();

            // Make sure that Analyst is fully started
            var acqMethodDir = (IAcqMethodDirConfig)analyst.Acquire();
            if (acqMethodDir == null)
                throw new IOException("Failed to initialize.  Analyst may need to be started.");


            string methodFilePath = Path.GetFullPath(@"..\..\..\Study 7 sched.dam");
            object acqMethodObj;
            acqMethodDir.LoadNonUIMethod(methodFilePath, out acqMethodObj);
            var acqMethod = (IAcqMethod)acqMethodObj;


            var method = builder.ExtractMsMethod(acqMethod);

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

            // Since this is an scheduled method, the retention time of each entry should be a non-zero value
            // test for the first entry in the inclusion list
            double mz = 0;
            ((IDDEMethodObj)idaServer).GetIncludeIonEntry(0, ref mz);
            Assert.AreEqual(744.839753, mz);

            double rt = -1;
            ((IDDEMethodObj3)idaServer).GetIncludeRetTimeEntry(0, ref rt);
            Assert.AreEqual(19.49, rt);

            // test for the last entry in the inclusion list
            ((IDDEMethodObj)idaServer).GetIncludeIonEntry(19, ref mz);
            Assert.AreEqual(572.791863, mz);

            ((IDDEMethodObj3)idaServer).GetIncludeRetTimeEntry(19, ref rt);
            Assert.AreEqual(20.48, rt);



        }
    }
}
