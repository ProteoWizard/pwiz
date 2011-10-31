using System.IO;
using AcqMethodSvrLib;
using Analyst;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Interop.MSMethodSvr;


namespace BuildAnalystFullScanMethod.Test
{
    [TestClass]
    public class BuildMethodTest
    {
        protected const string METHOD_FILE_IDA_5600 = "IDA-5600.dam";
        protected const string METHOD_FILE_IDA_QSTAR = "IDA-QSTAR.dam";
        protected const string METHOD_FILE_5600 = "5600.dam";
        protected const string METHOD_FILE_5600_MS1_MS2 = "5600_MS1MS2.dam";
        protected const string METHOD_FILE_QSTAR_MS1_MS2 = "QSTAR_MS1MS2.dam";
        protected const string METHOD_FILE_QSTAR = "QSTAR.dam";


        [TestMethod]
        [ExpectedException(typeof(IOException))]
        public void TestFailOnIncorrectInstrumentTemplate()
        {
            string projectDirectory = GetProjectDirectory();

            var template = IsQSTAR() ? METHOD_FILE_5600 : METHOD_FILE_QSTAR;
            var args = new[] { projectDirectory + template, projectDirectory + "Study 7 unsched.csv" };
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
            var args = new[] { "-i", projectDirectory + template, projectDirectory + "Study 7 unsched.csv" };
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
            var args = new[] { projectDirectory + template, projectDirectory + "Study 7 unsched.csv" };
            var builder = new BuildAnalystFullScanMethod();
            builder.ParseCommandArgs(args);

            // This should throw an exception
            builder.build();
        }

        protected bool IsQSTAR()
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


            var method = BuildAnalystFullScanMethod.ExtractMsMethod(acqMethod);

            // TODO there must be a better way to figure out which version of Analyst we have
            return method != null;
        }

        protected MassSpecMethod GetMethod(string methodFilePath)
        {
            MassSpecMethod method;

            BuildAnalystFullScanMethod.GetAcqMethod(methodFilePath, out method);

            return method;

        }

        protected string GetProjectDirectory()
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
