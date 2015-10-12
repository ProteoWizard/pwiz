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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// Creates and cleans up a directory containing the contents of a
    /// test ZIP file.
    /// </summary>
    public sealed class TestFilesDir : IDisposable
    {
        private TestContext TestContext { get; set; }

        /// <summary>
        /// Creates a sub-directory of the Test Results directory with the same
        /// basename as a ZIP file in the test project tree.
        /// </summary>
        /// <param name="testContext">The test context for the test creating the directory</param>
        /// <param name="relativePathZip">A root project relative path to the ZIP file</param>
        public TestFilesDir(TestContext testContext, string relativePathZip)
            : this(testContext, relativePathZip, null)
        {
            
        }

        /// <summary>
        /// Creates a sub-directory of the Test Results directory with extracted files
        /// from a ZIP file in the test project tree to a directory in TestResults.
        /// </summary>
        /// <param name="testContext">The test context for the test creating the directory</param>
        /// <param name="relativePathZip">A root project relative path to the ZIP file</param>
        /// <param name="directoryName">Name of directory to create in the test results</param>
        public TestFilesDir(TestContext testContext, string relativePathZip, string directoryName)
            : this(testContext, relativePathZip, directoryName, null)
        {

        }

        /// <summary>
        /// Creates a sub-directory of the Test Results directory with extracted files
        /// from a ZIP file in the test project tree to a directory in TestResults.
        /// </summary>
        /// <param name="testContext">The test context for the test creating the directory</param>
        /// <param name="relativePathZip">A root project relative path to the ZIP file</param>
        /// <param name="directoryName">Name of directory to create in the test results</param>
        /// <param name="persistentFiles">List of files we'd like to extract in the ZIP file's directory for (re)use</param>
        public TestFilesDir(TestContext testContext, string relativePathZip, string directoryName, string[] persistentFiles)
        {
            TestContext = testContext;
            string zipBaseName = Path.GetFileNameWithoutExtension(relativePathZip);
            if (zipBaseName == null)
                Assert.Fail("Null zip base name");  // Resharper
            directoryName = directoryName != null
                ? Path.Combine(directoryName, zipBaseName)
                : zipBaseName;
            FullPath = TestContext.GetTestPath(directoryName);
            if (Directory.Exists(FullPath))
            {
                Helpers.TryTwice(() => Directory.Delete(FullPath, true));
            }
            // where to place persistent (usually large, expensive to extract) files if any>
            PersistentFiles = persistentFiles;
            PersistentFilesDir = Path.GetDirectoryName(relativePathZip);
            PersistentFilesDir = PersistentFilesDir != null
                ? Path.Combine(PersistentFilesDir, zipBaseName)
                : zipBaseName;
            TestContext.ExtractTestFiles(relativePathZip, FullPath, PersistentFiles, PersistentFilesDir);
        }

        public string FullPath { get; private set; }

        public string PersistentFilesDir { get; private set; }

        public string[] PersistentFiles { get; private set; }

        /// <summary>
        /// Returns a full path to a file in the unzipped directory.
        /// </summary>
        /// <param name="relativePath">Relative path, as stored in the ZIP file, to the file</param>
        /// <returns>Absolute path to the file for use in tests</returns>
        public string GetTestPath(string relativePath)
        {
            if ((PersistentFiles != null) && (PersistentFiles.Contains(relativePath) || PersistentFiles.Any(relativePath.Contains)))
            {
                // persistent file - probably so because it's large. 
                return Path.Combine(PersistentFilesDir, relativePath);
            }
            if (!Directory.Exists(FullPath)) // can happen when all files are in the persistant area, but we're creating a new one as part of the test
                Directory.CreateDirectory(FullPath);
            return Path.Combine(FullPath, relativePath);
        }

        /// <summary>
        /// Returns a full path to a file in the unzipped directory, with "_Intl" appended.
        /// </summary>
        /// <param name="relativePath">Relative path, as stored in the ZIP file, to the file</param>
        /// <returns>Absolute path to the file for use in tests</returns>
        public string GetTestPathIntl(string relativePath)
        {
            string testPath = GetTestPath(relativePath);
            return Path.Combine(Path.GetDirectoryName(testPath) ?? "",
                    Path.GetFileNameWithoutExtension(testPath) + "_Intl" +
                    Path.GetExtension(testPath));
        }

        /// <summary>
        /// Returns a full path to a file in the unzipped directory, with "_Intl" appended
        /// if the current culture does not use a comma for .csv.
        /// </summary>
        /// <param name="relativePath">Relative path, as stored in the ZIP file, to the file</param>
        /// <returns>Absolute path to the file for use in tests</returns>
        public string GetTestPathLocale(string relativePath)
        {
            if (TextUtil.CsvSeparator == TextUtil.SEPARATOR_CSV)
                return GetTestPath(relativePath);
            return GetTestPathIntl(relativePath);
        }

        /// <summary>
        /// Attempts to move the directory to make sure no file handles are open.
        /// Used to delete the directory, but it can be useful to look at test
        /// artifacts, after the tests complete.
        /// </summary>
        public void Dispose()
        {
            // Move to a new name within the same directory
            string guidName = Guid.NewGuid().ToString();
            string parentPath = Path.GetDirectoryName(FullPath);
            if (parentPath != null)
                guidName = Path.Combine(parentPath, guidName);

            if (Directory.Exists(FullPath))  // Did test already clean up after itself?
            {
                try
                {
                    Helpers.TryTwice(() => Directory.Move(FullPath, guidName));
                }
                catch (IOException)
                {
                    // Useful for debugging. Exception names file that is locked.
                    Helpers.TryTwice(() => Directory.Delete(FullPath, true));
                }

                // Move the file back to where it was, and fail if this throws
                try
                {
                    Helpers.TryTwice(() => Directory.Move(guidName, FullPath));
                }
                catch (IOException)
                {
                    // Useful for debugging. Exception names file that is locked.
                    Helpers.TryTwice(() => Directory.Delete(guidName, true));
                }
            }
        }
    }
}