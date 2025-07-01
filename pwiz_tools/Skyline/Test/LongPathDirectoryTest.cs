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
        private const string PREFIX_LONG_PATH = @"\\?\"; // Assuming this is the long-path prefix

        [TestMethod]
        public void IsPathFullyQualified_ValidDriveLetterPath_ReturnsTrue()
        {
            string path = @"C:\Folder\Subfolder";
            bool result = PathEx.IsPathFullyQualified(path);

            Assert.IsTrue(result, "Expected C:\\Folder\\Subfolder to be fully qualified.");
        }

        [TestMethod]
        public void IsPathFullyQualified_ValidUncPath_ReturnsTrue()
        {
            string path = @"\\server\share\folder";
            bool result = PathEx.IsPathFullyQualified(path);

            Assert.IsTrue(result, "Expected \\\\server\\share\\folder to be fully qualified.");
        }

        [TestMethod]
        public void IsPathFullyQualified_RelativePath_ReturnsFalse()
        {
            string path = @"Folder\Subfolder";
            bool result = PathEx.IsPathFullyQualified(path);

            Assert.IsFalse(result, "Expected Folder\\Subfolder to be not fully qualified.");
        }

        [TestMethod]
        public void IsPathFullyQualified_DriveRelativePath_ReturnsFalse()
        {
            string path = @"C:Folder\Subfolder";
            bool result = PathEx.IsPathFullyQualified(path);

            Assert.IsFalse(result, "Expected C:Folder\\Subfolder to be not fully qualified.");
        }

        [TestMethod]
        public void IsPathFullyQualified_LongPathPrefix_ReturnsFalse()
        {
            string path = @"\\?\C:\Folder";
            bool result = PathEx.IsPathFullyQualified(path);

            Assert.IsFalse(result, "Expected \\\\?\\C:\\Folder to be not fully qualified (special case).");
        }

        [TestMethod]
        public void IsPathFullyQualified_EmptyPath_ReturnsFalse()
        {
            string path = "";
            bool result = PathEx.IsPathFullyQualified(path);

            Assert.IsFalse(result, "Expected empty path to be not fully qualified.");
        }

        [TestMethod]
        public void IsPathFullyQualified_NullPath_ReturnsFalse()
        {
            bool result = PathEx.IsPathFullyQualified(null);

            Assert.IsFalse(result, "Expected null path to be not fully qualified.");
        }

        [TestMethod]
        public void ToLongPath_FullyQualifiedDriveLetterPath_ReturnsPrefixedPath()
        {
            string path = @"C:\Folder\Subfolder";
            string expected = PREFIX_LONG_PATH + path;
            string result = path.ToLongPath();

            Assert.AreEqual(expected, result, "Expected long-path prefix to be added.");
        }

        [TestMethod]
        public void ToLongPath_FullyQualifiedUncPath_ReturnsPrefixedPath()
        {
            string path = @"\\server\share\folder";
            string expected = PREFIX_LONG_PATH + path;
            string result = path.ToLongPath();

            Assert.AreEqual(expected, result, "Expected long-path prefix to be added to UNC path.");
        }

        [TestMethod]
        public void ToLongPath_AlreadyLongPath_ReturnsUnchangedPath()
        {
            string path = @"\\?\C:\Folder\Subfolder";
            string result = path.ToLongPath();

            Assert.AreEqual(path, result, "Expected path with long-path prefix to remain unchanged.");
        }

        [TestMethod]
        public void ToLongPath_RelativePath_ThrowsArgumentException()
        {
            string path = @"Folder\Subfolder";

            AssertEx.ThrowsException<ArgumentException>(() => path.ToLongPath());
        }

        [TestMethod]
        public void ToLongPath_DriveRelativePath_ThrowsArgumentException()
        {
            string path = @"C:Folder\Subfolder";

            AssertEx.ThrowsException<ArgumentException>(() => path.ToLongPath());
        }

        [TestMethod]
        public void ToLongPath_ForwardSlashPath_ReturnsPrefixedPath()
        {
            string path = @"C:/Folder/Subfolder";
            string expected = PREFIX_LONG_PATH + path;
            string result = path.ToLongPath();

            Assert.AreEqual(expected, result, "Expected long-path prefix for forward-slash path.");
        }

        [TestMethod]
        public void ToLongPath_EmptyPath_ThrowsArgumentException()
        {
            string path = "";

            AssertEx.ThrowsException<ArgumentException>(() => path.ToLongPath());
        }

        [TestMethod]
        public void ToLongPath_NullPath_ThrowsArgumentException()
        {
            string path = null;

            // ReSharper disable once ExpressionIsAlwaysNull
            AssertEx.ThrowsException<NullReferenceException>(() => path.ToLongPath());
        }

        /// <summary>
        /// When true console output is added to clarify what the test has accomplished
        /// </summary>
        public bool IsVerboseMode => false;

        /// <summary>
        /// Test of long path functions in <see cref="DirectoryEx"/>. Because the test cannot
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

            DirectoryEx.CreateLongPath(inputPath);

            while (!Equals(inputPath, TestContext.GetTestResultsPath()))
            {
                Assert.IsTrue(DirectoryEx.ExistsLongPath(inputPath));
                DirectoryEx.SafeDeleteLongPath(inputPath);
                Assert.IsFalse(DirectoryEx.ExistsLongPath(inputPath));
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