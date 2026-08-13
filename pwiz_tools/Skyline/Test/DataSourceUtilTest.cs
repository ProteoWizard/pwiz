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
            AssertDataSource(DataSourceUtil.TYPE_BRUKER, fidSource);
            AssertDataSource(DataSourceUtil.TYPE_BRUKER, spotDir);

            // The folder holding the acquisition has to stay navigable, or there would be
            // no way to reach the acquisition in the first place.
            AssertNotDataSource(root);

            // A fid is a file, as is every other vendor file the reader looks for. A
            // directory of one of those names must not make the folder holding it look like
            // an acquisition - lower case is what separates testing for a file from testing
            // for mere existence, and upper case additionally covers Windows, where paths
            // are compared case insensitively.
            AssertFolderHoldingVendorDirectory(root, @"HoldsLowerCaseFid", @"fid");
            AssertFolderHoldingVendorDirectory(root, @"HoldsUpperCaseFid", @"FID");
            AssertFolderHoldingVendorDirectory(root, @"HoldsTdfDirectory", @"analysis.tdf");
            AssertFolderHoldingVendorDirectory(root, @"HoldsBafDirectory", @"analysis.baf");

            // An ordinary folder is still an ordinary folder
            var plainFolder = Path.Combine(root, @"PlainFolder");
            CreateDataFile(Path.Combine(plainFolder, @"Subfolder", @"1", @"notes.txt"));
            AssertNotDataSource(plainFolder);

            // The formats that are a flat directory of files are decided by a file name, but
            // only within a directory named as an acquisition. A flattened copy of one, holding
            // the same files under an ordinary name, has to stay a folder - otherwise it is
            // offered as a source and there is no way to navigate into it.
            var bafSource = Path.Combine(root, @"BafSource.d");
            CreateDataFile(Path.Combine(bafSource, @"analysis.baf"));
            AssertDataSource(DataSourceUtil.TYPE_BRUKER, bafSource);

            var bafCopy = Path.Combine(root, @"FlatCopyOfBaf");
            CreateDataFile(Path.Combine(bafCopy, @"analysis.baf"));
            AssertNotDataSource(bafCopy);

            var u2Source = Path.Combine(root, @"U2Source.d");
            CreateDataFile(Path.Combine(u2Source, @"U2Source.u2"));
            AssertDataSource(DataSourceUtil.TYPE_BRUKER, u2Source);

            // The same for the directory formats the naming rules recognize on their own: a
            // directory holding loose _FUNC files, or an AcqData, is not an acquisition
            var watersCopy = Path.Combine(root, @"FlatCopyOfWaters");
            CreateDataFile(Path.Combine(watersCopy, @"_FUNC001.DAT"));
            AssertNotDataSource(watersCopy);

            var agilentCopy = Path.Combine(root, @"FlatCopyOfAgilent");
            CreateDataFile(Path.Combine(agilentCopy, @"AcqData", @"mspeak.bin"));
            AssertNotDataSource(agilentCopy);

            // A folder of ordinary files, with nothing below it, is not a data source
            var leafFolder = Path.Combine(root, @"LeafFolder");
            CreateDataFile(Path.Combine(leafFolder, @"notes.txt"));
            AssertNotDataSource(leafFolder);

            TestSourceTypeFromNames();
        }

        /// <summary>
        /// The overload deciding a directory from names alone, which is how sharing a document
        /// reads a zip archive, where there is no directory to look in and no reader to ask.
        /// Vendors are not consistent about the case of these names, and neither the filesystem
        /// they usually come from nor the reader distinguishes it, so this must not either.
        /// </summary>
        private void TestSourceTypeFromNames()
        {
            var noNames = new string[0];
            AssertEx.AreEqual(DataSourceUtil.TYPE_BRUKER,
                DataSourceUtil.GetSourceType(@"Acquisition.d", new[] { @"Analysis.baf" }, noNames));
            AssertEx.AreEqual(DataSourceUtil.TYPE_BRUKER,
                DataSourceUtil.GetSourceType(@"Acquisition.d", new[] { @"analysis.tdf" }, noNames));
            AssertEx.AreEqual(DataSourceUtil.TYPE_AGILENT,
                DataSourceUtil.GetSourceType(@"Acquisition.d", noNames, new[] { @"ACQDATA" }));
            AssertEx.AreEqual(DataSourceUtil.TYPE_WATERS_RAW,
                DataSourceUtil.GetSourceType(@"Acquisition.raw", new[] { @"_func001.dat" }, noNames));

            // A directory holding none of them is a folder, whatever its extension
            AssertEx.AreEqual(DataSourceUtil.FOLDER_TYPE,
                DataSourceUtil.GetSourceType(@"Acquisition.d", new[] { @"notes.txt" }, new[] { @"Subfolder" }));
        }

        private void AssertFolderHoldingVendorDirectory(string root, string folderName, string vendorDirectoryName)
        {
            var holdsVendorFolder = Path.Combine(root, folderName);
            Directory.CreateDirectory(Path.Combine(holdsVendorFolder, vendorDirectoryName));
            AssertNotDataSource(holdsVendorFolder);
        }

        /// <summary>
        /// Checks both that the directory can be picked as a data source and that it is
        /// reported as one of the types the dialogs know. A type they do not know reads as
        /// neither a known format nor a folder, and their "Sources of type" filter then
        /// drops the directory from the list altogether.
        /// </summary>
        private void AssertDataSource(string expectedType, string path)
        {
            AssertEx.AreEqual(expectedType, DataSourceUtil.GetSourceType(path), path);
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
