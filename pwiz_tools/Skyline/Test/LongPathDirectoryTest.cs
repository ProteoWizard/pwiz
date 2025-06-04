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
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class LongPathDirectoryTest : AbstractUnitTest
    {
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
        }
    }
}