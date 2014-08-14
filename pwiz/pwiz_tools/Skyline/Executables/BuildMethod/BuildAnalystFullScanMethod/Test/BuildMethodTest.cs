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
using Interop.AcqMethodSvr;
using Interop.Analyst;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Interop.MSMethodSvr;


namespace BuildAnalystFullScanMethod.Test
{
    [TestClass]
    public class BuildMethodTest
    {
        protected const string METHOD_FILE_IDA_5600 = @"Test\IDA-5600.dam";
        protected const string METHOD_FILE_IDA_QSTAR = @"Test\IDA-QSTAR.dam";
        protected const string METHOD_FILE_5600 = @"Test\5600.dam";
        protected const string METHOD_FILE_5600_MS1_MS2 = @"Test\5600_MS1MS2.dam";
        protected const string METHOD_FILE_QSTAR_MS1_MS2 = @"Test\QSTAR_MS1MS2.dam";
        protected const string METHOD_FILE_QSTAR = @"Test\QSTAR.dam";

        private const string TRANS_LIST_UNSCHED = @"Test\Study 7 unsched.csv";
        private const string TRANS_LIST_SCHED = @"Test\Study 7 sched.csv";
        private const string METHOD_UNSCHED = @"Test\Study 7 unsched.dam";
        private const string METHOD_SCHED = @"Test\Study 7 sched.dam";

        private readonly string PROJECT_DIR;

        public BuildMethodTest()
        {
            PROJECT_DIR = GetProjectDirectory();
        }

        [TestMethod]
        [ExpectedException(typeof(IOException))]
        public void TestFailOnIncorrectInstrumentTemplate()
        {
            string projectDirectory = GetProjectDirectory();

            var template = IsQSTAR() ? METHOD_FILE_5600 : METHOD_FILE_QSTAR;
            var args = new[] { Path.Combine(projectDirectory, template), 
                               Path.Combine(projectDirectory, TRANS_LIST_UNSCHED) };

            var builder = new BuildAnalystFullScanMethod();
            builder.ParseCommandArgs(args);

            // This should throw an exception
            builder.build();

        }

        [TestMethod]
        [ExpectedException(typeof(IOException))]
        public void TestFailOnIncorrectTemplateTypeForIDA()
        {
            string projectDirectory = GetProjectDirectory();

            var template = IsQSTAR() ? METHOD_FILE_QSTAR : METHOD_FILE_5600;
            var args = new[] { "-i", Path.Combine(projectDirectory, template), 
                                     Path.Combine(projectDirectory, TRANS_LIST_UNSCHED) };

            var builder = new BuildAnalystFullScanMethod();
            builder.ParseCommandArgs(args);

            // This should throw an exception
            builder.build();
        }


        [TestMethod]
        [ExpectedException(typeof(IOException))]
        public void TestFailOnIncorrectTemplateTypeForTargetedMSMS()
        {
            string projectDirectory = GetProjectDirectory();

            var template = IsQSTAR() ? METHOD_FILE_IDA_QSTAR : METHOD_FILE_IDA_5600;
            var args = new[] { Path.Combine(projectDirectory, template),
                               Path.Combine(projectDirectory, TRANS_LIST_UNSCHED) };

            var builder = new BuildAnalystFullScanMethod();
            builder.ParseCommandArgs(args);

            // This should throw an exception
            builder.build();
        }

        protected static bool IsQstarTemplate(string templateFile)
        {
            return templateFile.Contains("QSTAR");
        }

        protected bool IsQSTAR()
        {

            var analyst = new ApplicationClass();

            // Make sure that Analyst is fully started
            var acqMethodDir = (IAcqMethodDirConfig)analyst.Acquire();
            if (acqMethodDir == null)
                throw new IOException("Failed to initialize.  Analyst may need to be started.");


            string methodFilePath = Path.GetFullPath(Path.Combine(GetProjectDirectory(), METHOD_FILE_IDA_QSTAR));

            object acqMethodObj;
            acqMethodDir.LoadNonUIMethod(methodFilePath, out acqMethodObj);
            var acqMethod = (IAcqMethod)acqMethodObj;


            var method = BuildAnalystFullScanMethod.ExtractMsMethod(acqMethod);

            // there must be a better way to figure out which version of Analyst we have
            return method != null;
        }

        protected MassSpecMethod GetMethod(string methodFilePath)
        {
            MassSpecMethod method;

            BuildAnalystFullScanMethod.GetAcqMethod(methodFilePath, out method);

            return method;
        }

        protected string GetTemplateFilePath(string templateMethodFile)
        {
            return GetFullPath(templateMethodFile);
        }

        protected string GetMethodUnschedPath()
        {
            return GetFullPath(METHOD_UNSCHED);
        }

        protected string GetMethodSchedPath()
        {
            return GetFullPath(METHOD_SCHED);
        }

        protected string GetTransListUnschedPath()
        {
            return GetFullPath(TRANS_LIST_UNSCHED);
        }

        protected string GetTransListSchedPath()
        {
            return GetFullPath(TRANS_LIST_SCHED); 
        }

        private string GetFullPath(string fileName)
        {
            return Path.GetFullPath(Path.Combine(PROJECT_DIR, fileName));
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

        protected void DeleteOutput(string methodFilePath)
        {
            try
            {
                File.Delete(methodFilePath);
                if (File.Exists(methodFilePath))
                {
                    Assert.Fail(string.Format("Could not delete file: {0}", methodFilePath));
                }
            }
            catch (FileNotFoundException)
            {
                Assert.Fail(string.Format("Could not find file: {0}", methodFilePath));
            }
        }       
    }
}
