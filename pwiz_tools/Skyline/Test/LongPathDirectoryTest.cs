/*
 * Original author: David Shteynberg <dshteyn .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class LongPathDirectoryTest : AbstractUnitTest
    {
        [TestMethod]
        public void LongPathExTest()
        {
            string fullyQualifiedDrivePath = @"C:\Folder\Subfolder";
            // Try simple first
            PathIsFullyQualifiedTest(fullyQualifiedDrivePath);
            // Try all valid drive letters
            for (char c = 'a'; c < 'z'; c++)
            {
                string drivePath = c + fullyQualifiedDrivePath.Substring(1);
                PathIsFullyQualifiedTest(drivePath);
                drivePath = char.ToUpperInvariant(c) + fullyQualifiedDrivePath.Substring(1);
                PathIsFullyQualifiedTest(drivePath);
            }
            // Try UNC path
            PathIsFullyQualifiedTest(@"\\server\share\folder");
            // Try a valid long path
            PathIsFullyQualifiedTest(@"C:\Folder".ToLongPath());
            // Test failures
            PathIsNotFullyQualifiedTest(@"Folder\Subfolder");
            PathIsNotFullyQualifiedTest(@"C:Folder\Subfolder");
            PathIsNotFullyQualifiedTest("A");
            PathIsNotFullyQualifiedTest("B:");
            PathIsNotFullyQualifiedTest(string.Empty);
            PathIsNotFullyQualifiedTest(null);

            ToLongPathTest(@"C:\Folder\Subfolder");
            ToLongPathTest(@"\\server\share\folder");
            ToLongPathTest(@"C:\Folder\Subfolder".ToLongPath(), false);
            
            AssertEx.ThrowsException<ArgumentException>(() => @"Folder\Subfolder".ToLongPath());
            AssertEx.ThrowsException<ArgumentException>(() => @"C:Folder\Subfolder".ToLongPath());
            AssertEx.ThrowsException<ArgumentException>(() => string.Empty.ToLongPath());
            AssertEx.ThrowsException<ArgumentException>(() => ((string)null).ToLongPath());
        }

        private void PathIsFullyQualifiedTest(string path)
        {
            string failureMessage = "Expected '{0}' to be fully qualified.";
            Assert.IsTrue(PathEx.IsPathFullyQualified(path), failureMessage, path);
            path = path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Assert.IsTrue(PathEx.IsPathFullyQualified(path), failureMessage, path);
        }
        private void PathIsNotFullyQualifiedTest(string path)
        {
            string failureMessage = "Expected '{0}' not to be fully qualified.";
            Assert.IsFalse(PathEx.IsPathFullyQualified(path), failureMessage, path);
        }

        private void ToLongPathTest(string path, bool addPrefix = true)
        {
            string failureMessage = addPrefix
                ? "Expected long-path prefix to be added to '{0}'"
                : "Expected long-path prefix not to be added to '{0}'";
            string prefix = addPrefix ? PathEx.PREFIX_LONG_PATH : string.Empty;
            Assert.AreEqual(prefix + path, path.ToLongPath(), failureMessage, path);
            path = path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            // Fix the long path prefix if it is not added and therefore just got
            // switched to AltDirectorySeparatorChar which is not valid.
            if (!addPrefix)
                path = path.Substring(PathEx.PREFIX_LONG_PATH.Length).ToLongPath();
            Assert.AreEqual(prefix + path, path.ToLongPath(), failureMessage, path);
        }

        /// <summary>
        /// When true console output is added to clarify what the test has accomplished
        /// </summary>
        public bool IsVerboseMode => false;

        /// <summary>
        /// Test of long path functions in <see cref="Directory"/>. Because the test cannot
        /// turn on long path support in the registry, this does not actually test long paths
        /// if the feature is not enabled in the registry. Since the feature is required for
        /// installing Python and testing AlphaPeptDeep and Carafe, we expect the majority
        /// of systems on the Skyline dev team to eventually have this enabled.
        /// </summary>
        [TestMethod]
        public void DirectoryWithLongPathTest()
        {
            string inputPath = TestContext.GetTestResultsPath();    // Start in the normal test directory that gets tested for file locking

            if (PythonInstaller.ValidateEnableLongpaths())
            {
                // Make the directory path longer than 256 characters, but only when the registry has LongPathsEnabled set
                for (int i = 0; i < 12; i++)
                {
                    inputPath = Path.Combine(inputPath, @"LongPathDirectoryTest");
                }
                Assert.IsTrue(inputPath.Length > 256);
            }
            else
            {
                // Can't actually test long paths if the feature is not enabled.
                Assert.IsFalse(inputPath.Length > 256);
            }

            if (IsVerboseMode)
                Console.WriteLine($@"Creating directory with path ""{inputPath}"" that has length {inputPath.Length} characters");

            Directory.CreateDirectory(inputPath);
            while (!Equals(inputPath, TestContext.GetTestResultsPath()))
            {
                Assert.IsTrue(Directory.Exists(inputPath));
                Directory.Delete(inputPath);
                Assert.IsFalse(Directory.Exists(inputPath));
                inputPath = Path.GetDirectoryName(inputPath);
            }
            
            // ToLongPath tests
            AssertEx.ThrowsException<ArgumentException>(() => @"path\to\file.txt".ToLongPath());
            const string validPath = @"C:\path\to\file.txt";
            AssertEx.NoExceptionThrown<Exception>(() => validPath.ToLongPath());
            // Make sure calling multiple times does not cause multiple long-path prefixes
            string testPath = validPath;
            for (int i = 0; i < 3; i++)
            {
                string longPath = testPath.ToLongPath();
                if (i > 0)
                    Assert.AreEqual(testPath, longPath);
                testPath = longPath;
            }
        }

        /// <summary>
        /// Test of long path functions in <see cref="DirectoryEx"/>. Because the test cannot
        /// turn on long path support in the registry, this does not actually test long paths
        /// if the feature is not enabled in the registry. Since the feature is required for
        /// installing Python and testing AlphaPeptDeep and Carafe, we expect the majority
        /// of systems on the Skyline dev team to eventually have this enabled.
        /// </summary>
        [TestMethod]
        public void DirectoryExWithLongPathTest()
        {
            string inputPath = TestContext.GetTestResultsPath();    // Start in the normal test directory that gets tested for file locking

            if (PythonInstaller.ValidateEnableLongpaths())
            {
                // Make the directory path longer than 256 characters, but only when the registry has LongPathsEnabled set
                for (int i = 0; i < 12; i++)
                {
                    inputPath = Path.Combine(inputPath, @"LongPathDirectoryTest");
                }
                Assert.IsTrue(inputPath.Length > 256);
            }
            else
            {
                // Can't actually test long paths if the feature is not enabled.
                Assert.IsFalse(inputPath.Length > 256);
            }

            if (IsVerboseMode)
                Console.WriteLine($@"Creating directory with path ""{inputPath}"" that has length {inputPath.Length} characters");

            DirectoryEx.CreateLongPath(inputPath);
            while (!Equals(inputPath, TestContext.GetTestResultsPath()))
            {
                Assert.IsTrue(Directory.Exists(inputPath));
                Directory.Delete(inputPath);
                Assert.IsFalse(Directory.Exists(inputPath));
                inputPath = Path.GetDirectoryName(inputPath);
            }

            // ToLongPath tests
            AssertEx.ThrowsException<ArgumentException>(() => @"path\to\file.txt".ToLongPath());
            const string validPath = @"C:\path\to\file.txt";
            AssertEx.NoExceptionThrown<Exception>(() => validPath.ToLongPath());
            // Make sure calling multiple times does not cause multiple long-path prefixes
            string testPath = validPath;
            for (int i = 0; i < 3; i++)
            {
                string longPath = testPath.ToLongPath();
                if (i > 0)
                    Assert.AreEqual(testPath, longPath);
                testPath = longPath;
            }
        }
    }
}