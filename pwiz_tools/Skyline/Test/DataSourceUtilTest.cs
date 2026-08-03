/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Tests the directory recognition behind the file open dialogs. Bruker FID is the
    /// case that matters here: a directory tree with no distinguishing extension on any
    /// of its levels, which the dialogs offered no way to select (issue #4510). Skeleton
    /// trees are enough, since only the names and locations of the files decide this.
    /// </summary>
    [TestClass]
    public class DataSourceUtilTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestGetSourceType()
        {
            TestContext.EnsureTestResultsDir();
            var root = TestContext.GetTestResultsPath();

            // A MALDI acquisition, with one spot directory per sample. The reader takes any
            // level of this, rolling up every spot below whichever one it is given.
            var fidSource = Path.Combine(root, @"DSM_105335_FID_File");
            var spotDir = Path.Combine(fidSource, @"0_A5");
            CreateDataFile(Path.Combine(spotDir, @"1", @"1SLin", @"fid"));
            CreateDataFile(Path.Combine(spotDir, @"1", @"1SLin", @"acqus"));
            AssertDataSource(fidSource);
            AssertDataSource(spotDir);

            // The folder holding the acquisition has to stay navigable, or there would be
            // no way to reach the acquisition in the first place.
            AssertNotDataSource(root);

            // A fid is a file. A directory named "fid" must not make the folder holding it
            // look like an acquisition - lower case is what separates testing for a file
            // from testing for existence, upper case additionally covers Windows, where
            // paths are compared case insensitively.
            AssertFolderHoldingFidDirectory(root, @"HoldsLowerCaseFid", @"fid");
            AssertFolderHoldingFidDirectory(root, @"HoldsUpperCaseFid", @"FID");

            // An ordinary folder is still an ordinary folder
            var plainFolder = Path.Combine(root, @"PlainFolder");
            CreateDataFile(Path.Combine(plainFolder, @"Subfolder", @"1", @"notes.txt"));
            AssertNotDataSource(plainFolder);
        }

        private void AssertFolderHoldingFidDirectory(string root, string folderName, string fidDirectoryName)
        {
            var holdsFidFolder = Path.Combine(root, folderName);
            Directory.CreateDirectory(Path.Combine(holdsFidFolder, fidDirectoryName));
            AssertNotDataSource(holdsFidFolder);
        }

        /// <summary>
        /// The reader names the format, so rather than pin the exact string, check what the
        /// dialogs actually act on - whether the directory can be picked as a data source.
        /// </summary>
        private void AssertDataSource(string path)
        {
            AssertEx.IsFalse(DataSourceUtil.IsFolderType(DataSourceUtil.GetSourceType(path)), path);
            AssertEx.IsTrue(DataSourceUtil.IsDataSource(path), path);
        }

        private void AssertNotDataSource(string path)
        {
            AssertEx.AreEqual(DataSourceUtil.FOLDER_TYPE, DataSourceUtil.GetSourceType(path), path);
            AssertEx.IsFalse(DataSourceUtil.IsDataSource(path), path);
        }

        private void CreateDataFile(string path)
        {
            var directoryPath = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directoryPath))
                Directory.CreateDirectory(directoryPath);
            File.WriteAllText(path, string.Empty);
        }
    }
}
