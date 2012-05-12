/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009-2010 University of Washington - Seattle, WA
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
using System;
using System.Globalization;
using System.IO;
using Ionic.Zip;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;

namespace pwiz.SkylineTestUtil
{
    public static class ExtensionTestContext
    {
        public static string GetTestPath(this TestContext testContext, string relativePath)
        {
            return Path.Combine(testContext.TestDir, relativePath);
        }

        public static String GetProjectDirectory(this TestContext testContext, string relativePath)
        {
            for (String directory = testContext.TestDir;
                    directory != null && directory.Length > 10;
                    directory = Path.GetDirectoryName(directory))
            {
                if (File.Exists(Path.Combine(directory, Program.Name + ".sln")))
                    return Path.Combine(directory, relativePath);
            }
            return null;
        }

        public static void ExtractTestFiles(this TestContext testContext, string relativePathZip)
        {
            testContext.ExtractTestFiles(relativePathZip, testContext.TestDir);
        }

        public static void ExtractTestFiles(this TestContext testContext, string relativePathZip, string destDir)
        {
            string pathZip = testContext.GetProjectDirectory(relativePathZip);
            using (ZipFile zipFile = ZipFile.Read(pathZip))
            {
                foreach (ZipEntry zipEntry in zipFile)
                    zipEntry.Extract(destDir, ExtractExistingFileAction.OverwriteSilently);
            }
        }

        public static bool CanImportThermoRaw
        {
            get
            {
                return !Program.NoVendorReaders &&
                    Equals(".", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator) &&
                    Equals(",", CultureInfo.CurrentCulture.TextInfo.ListSeparator);
            }
        }

        public static string ExtThermoRaw
        {
            get { return CanImportThermoRaw ? ".RAW" : ".mzML"; }
        }
        
        public static bool CanImportAgilentRaw
        {
            get
            {
                // return false to import mzML
                return !Program.NoVendorReaders;
            }
        }

        public static string ExtAbWiff
        {
            get { return CanImportAbWiff ? ".wiff" : ".mzML"; }
        }

        public static bool CanImportAbWiff
        {
            get
            {
                // return false to import mzML
                return !Program.NoVendorReaders;
            }
        }

        public static string ExtAgilentRaw
        {
            get { return CanImportAgilentRaw ? ".d" : ".mzML"; }
        }

        public static bool CanImportWatersRaw
        {
            get
            {
                // return false to import mzML
                return false; // !Program.NoVendorReaders && !IsDebugMode;  // no waters library for debug
            }
        }

        public static string ExtWatersRaw
        {
            get { return CanImportWatersRaw ? ".raw" : ".mzML"; }
        }

        public static bool IsDebugMode
        {
            get
            {
#if DEBUG
                return true;
#else
                return false;
#endif
            }
        }
    }
}