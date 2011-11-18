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
using System.IO;
using Interop.MSMethodSvr;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BuildQTRAPMethod.Test
{
    [TestClass]
    public class BuildMethodTest
    {
        private const string METHOD_FILE = @"Test\bovine_std2.dam";
        private const string LIST_UNSCHED = @"Test\Study 7 unsched.csv";
        private const string METHOD_UNSCHED = @"Test\Study 7 unsched.dam";
       
        [TestMethod]
        public void TestMRMMethod()
        {
            string projectDirectory = GetProjectDirectory();

            var args = new[] { Path.Combine(projectDirectory, METHOD_FILE), 
                               Path.Combine(projectDirectory, LIST_UNSCHED) };
            var builder = new BuildQtrapMethod();
            builder.ParseCommandArgs(args);
            builder.build();

            string methodFilePath = Path.GetFullPath(Path.Combine(projectDirectory, METHOD_UNSCHED));

            // read the updated method and make sure that the
            // transition list has been included
            MassSpecMethod method = GetMethod(methodFilePath);

            var period = (Period)method.GetPeriod(0);
            var msExperiment = (Experiment)period.GetExperiment(0);

            Assert.AreEqual(60, msExperiment.MassRangesCount);

            // Check the first one
            // 744.839753,1087.49894,20,APR.AGLCQTFVYGGCR.y9.light,85.4,38.2
            var massRange = (MassRange)msExperiment.GetMassRange(0);
            Assert.AreEqual(20, massRange.DwellTime);
            Assert.AreEqual(744.839753, massRange.QstartMass, 0.00005);
            Assert.AreEqual(0, massRange.QstopMass);
            Assert.AreEqual(1087.49894, massRange.QstepMass, 0.00005);

            var msMassRange3 = (IMassRange3)massRange;
            Assert.AreEqual("APR.AGLCQTFVYGGCR.y9.light", msMassRange3.CompoundID);

            // Check the last one
            // 572.791863,724.375566,20,CRP.GYSIFSYATK.y6.heavy,72.6,28.2
            massRange = (MassRange)msExperiment.GetMassRange(59);
            Assert.AreEqual(20, massRange.DwellTime);
            Assert.AreEqual(572.791863, massRange.QstartMass, 0.00005);
            Assert.AreEqual(0, massRange.QstopMass);
            Assert.AreEqual(724.375566, massRange.QstepMass, 0.00005);

            msMassRange3 = (IMassRange3)massRange;
            Assert.AreEqual("CRP.GYSIFSYATK.y6.heavy", msMassRange3.CompoundID);

            DeleteOutput(methodFilePath);
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

        protected MassSpecMethod GetMethod(string methodFilePath)
        {
            MassSpecMethod method;

            BuildQtrapMethod.GetAcqMethod(methodFilePath, out method);

            return method;
        }

        private static void DeleteOutput(string methodFilePath)
        {
            try
            {
                File.Delete(methodFilePath);
                if (File.Exists(methodFilePath))
                {
                    Assert.Fail("Could not delete file: " + methodFilePath);
                }
            }
            catch (FileNotFoundException)
            {
                Assert.Fail(string.Format("Could not find file: {0}", methodFilePath));
            }
        }
    }
}
