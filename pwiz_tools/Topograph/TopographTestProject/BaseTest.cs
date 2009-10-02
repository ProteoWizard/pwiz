using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace pwiz.Topograph.Test
{
    public class BaseTest
    {
        private TestContext testContextInstance;
        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        public String FindDirectory(String name)
        {
            for (String directory = TestContext.TestDir; directory.Length > 10; directory = Path.GetDirectoryName(directory))
            {
                String testDataDirectory = Path.Combine(directory, name);
                if (Directory.Exists(testDataDirectory))
                {
                    return testDataDirectory;
                }
            }
            return null;
        }

        public String GetDataDirectory()
        {
            return FindDirectory("TestData");
        }
        public void CopyDirectory(String sourceDirectory, String destPath)
        {
            String destDirectory = Path.Combine(destPath, Path.GetFileName(sourceDirectory));
            Directory.CreateDirectory(destDirectory);
            foreach (var file in Directory.GetFiles(sourceDirectory))
            {
                File.Copy(file, Path.Combine(destDirectory, Path.GetFileName(file)));
            }
            foreach (var directory in Directory.GetDirectories(sourceDirectory))
            {
                CopyDirectory(directory, destDirectory);
            }
        }

        public void FreshenDirectory(String sourceDirectory, String destDirectory)
        {
            foreach (var file in Directory.GetFiles(sourceDirectory))
            {
                String destFile = Path.Combine(destDirectory, Path.GetFileName(file));
                if (File.Exists(destFile))
                {
                    continue;
                }
                File.Copy(file, destFile);
            }
            foreach (var directory in Directory.GetDirectories(sourceDirectory))
            {
                if (Directory.Exists(Path.Combine(destDirectory, Path.GetFileName(directory))))
                {
                    continue;
                }
                CopyDirectory(directory, destDirectory);
            }
        }

        public void SetupForPwiz()
        {
            String destDirectory = Path.Combine(FindDirectory("TopographTestProject"), "bin\\x86\\Debug");
            FreshenDirectory(Path.Combine(FindDirectory("turnover_lib"), "bin\\x86\\Debug"), destDirectory);
        }
    }
}
