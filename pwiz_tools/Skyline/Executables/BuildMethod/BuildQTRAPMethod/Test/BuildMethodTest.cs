using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MSMethodSvrLib;



namespace BuildQTRAPMethod.Test
{
    [TestClass]
    public class BuildMethodTest
    {
        private const string METHOD_FILE = "bovine_std2.dam";
       
        [TestMethod]
        public void TestMRMMethod()
        {
            string projectDirectory = GetProjectDirectory();

            var args = new[] { projectDirectory+METHOD_FILE, projectDirectory+"Study 7 unsched.csv" };
            var builder = new BuildQtrapMethod();
            builder.ParseCommandArgs(args);
            builder.build();

            string methodFilePath = Path.GetFullPath(projectDirectory+"Study 7 unsched.dam");

            // read the updated method and make sure that the
            // transition list has been included
            MassSpecMethod method = GetMethod(methodFilePath);

            var period = (Period)method.GetPeriod(0);
            var msExperiment = (Experiment)period.GetExperiment(0);

            Assert.AreEqual(60, msExperiment.MassRangesCount);

            // Check the first one
            // 744.839753,1087.49894,19.49,APR.AGLCQTFVYGGCR.y9.light,85.4,38.2
            var massRange = (MassRange) msExperiment.GetMassRange(0);
            Assert.AreEqual(19.49, massRange.DwellTime, 0.000001);
            Assert.AreEqual(744.839753, massRange.QstartMass, 0.00005);
            Assert.AreEqual(0, massRange.QstopMass);
            Assert.AreEqual(1087.49894, massRange.QstepMass, 0.00005);

            var msMassRange3 = (IMassRange3)massRange;
            Assert.AreEqual("APR.AGLCQTFVYGGCR.y9.light", msMassRange3.CompoundID);
          
            // Check the last one
            // 572.791863,724.375566,20.48,CRP.GYSIFSYATK.y6.heavy,72.6,28.2
            massRange = (MassRange)msExperiment.GetMassRange(59);
            Assert.AreEqual(20.48, massRange.DwellTime, 0.000001);
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
