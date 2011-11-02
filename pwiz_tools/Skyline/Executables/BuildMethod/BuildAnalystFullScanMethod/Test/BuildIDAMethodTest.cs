/*
 * Original author: Vagisha Sharma <vsharma .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using Interop.DDEMethodSvr;
using Interop.MSMethodSvr;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BuildAnalystFullScanMethod.Test
{
    [TestClass]
    public class BuildIDAMethodTest : BuildMethodTest
    {       
        [TestMethod]
        public void TestIDAUnscheduled()
        {
            TestIDAUnscheduled(IsQSTAR() ? METHOD_FILE_IDA_QSTAR : METHOD_FILE_IDA_5600);
        }

        [TestMethod]
        public void TestIDAScheduled()
        {
            TestIDAScheduled(IsQSTAR() ? METHOD_FILE_IDA_QSTAR : METHOD_FILE_IDA_5600);
        }

        private void TestIDAUnscheduled(string templateMethodFile)
        {
            var args = new[] { 
                                "-i", 
                                GetTemplateFilePath(templateMethodFile),
                                GetTransListUnschedPath()
                             };

            var builder = new BuildAnalystFullScanMethod();
            builder.ParseCommandArgs(args);
            builder.build();


            TestIDACommon(GetMethodUnschedPath(), false);            
        }

        private void TestIDAScheduled(string templateMethodFile)
        {
            var args = new[] {
                                "-i", "-r",
                                GetTemplateFilePath(templateMethodFile),
                                GetTransListSchedPath()
                             };

            var builder = new BuildAnalystFullScanMethod();
            builder.ParseCommandArgs(args);
            builder.build();


            TestIDACommon(GetMethodSchedPath(), true);
        }

        // read the updated method and make sure that the inclusion list is in the method
        private void TestIDACommon(string methodFilePath, bool isScheduled)
        {
            MassSpecMethod myMethod = GetMethod(methodFilePath);

            // The method should have an inclusion list
            object idaServer;
            ((IMassSpecMethod2)myMethod).GetDataDependSvr(out idaServer);
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
            if (isScheduled)
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
                ((IDDEMethodObj3)idaServer).GetIncludeRetTimeEntry(0, ref rt);
                Assert.AreEqual(0, rt);


                ((IDDEMethodObj3)idaServer).GetIncludeRetTimeEntry(19, ref rt);
                Assert.AreEqual(0, rt);
            }

            DeleteOutput(methodFilePath);
        }
    }
}
