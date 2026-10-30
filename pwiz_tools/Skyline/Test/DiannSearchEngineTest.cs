/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using pwiz.Skyline.Model.DdaSearch;
using pwiz.Skyline.Model.Lib;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class DiannSearchEngineTest : AbstractUnitTest
    {
        /// <summary>
        /// DIA-NN reads only some vendor formats; the rest are converted to mzML before it searches them.
        /// </summary>
        [TestMethod]
        public void TestDiannReadsFormat()
        {
            var cases = new (string SourceType, bool IsTimsTof, bool IsOneSampleOfSeveral, bool Reads)[]
            {
                (DataSourceUtil.TYPE_THERMO_RAW, false, false, true),
                (DataSourceUtil.TYPE_MZML, false, false, true),
                (DataSourceUtil.TYPE_WIFF, false, false, true),
                (DataSourceUtil.TYPE_WIFF, false, true, false), // DIA-NN cannot search just one sample of a file
                (DataSourceUtil.TYPE_WIFF2, false, false, true),
                (DataSourceUtil.TYPE_WIFF2, false, true, false),
                (DataSourceUtil.TYPE_BRUKER, true, false, true),
                (DataSourceUtil.TYPE_BRUKER, false, false, false), // BAF or TSF
                (DataSourceUtil.TYPE_WATERS_RAW, false, false, false),
                (DataSourceUtil.TYPE_AGILENT, false, false, false),
                (DataSourceUtil.TYPE_SHIMADZU, false, false, false),
                (DataSourceUtil.TYPE_MZXML, false, false, false),
                (DataSourceUtil.TYPE_MZ5, false, false, false),
                (DataSourceUtil.TYPE_MZMLB, false, false, false),
                (DataSourceUtil.TYPE_UIMF, false, false, false),
                (DataSourceUtil.TYPE_MBI, false, false, false),
            };
            foreach (var c in cases)
                AssertEx.AreEqual(c.Reads, DiannHelpers.ReadsFormat(c.SourceType, c.IsTimsTof, c.IsOneSampleOfSeveral), c.ToString());

            // The same decision from the files themselves, where ".raw" names Thermo and Waters
            // data, and ".d" names Bruker timsTOF, Bruker BAF and Agilent data
            var dir = TestContext.GetTestResultsPath(@"DiannFormats");
            Directory.CreateDirectory(dir);
            string MakeFile(string name)
            {
                var path = Path.Combine(dir, name);
                File.WriteAllText(path, string.Empty);
                return path;
            }
            string MakeFolder(string name, string fileInside, string folderInside = null)
            {
                var path = Path.Combine(dir, name);
                Directory.CreateDirectory(path);
                if (fileInside != null)
                    File.WriteAllText(Path.Combine(path, fileInside), string.Empty);
                if (folderInside != null)
                    Directory.CreateDirectory(Path.Combine(path, folderInside));
                return path;
            }

            var engine = new DiannSearchEngine(dir);
            var files = new (MsDataFileUri File, bool NeedsConversion)[]
            {
                (new MsDataFilePath(MakeFile(@"thermo.raw")), false),
                (new MsDataFilePath(MakeFolder(@"waters.raw", @"_FUNC001.DAT")), true),
                (new MsDataFilePath(MakeFolder(@"timstof.d", DiannHelpers.TIMSTOF_ANALYSIS_FILE)), false),
                (new MsDataFilePath(MakeFolder(@"baf.d", @"analysis.baf")), true),
                (new MsDataFilePath(MakeFolder(@"agilent.d", null, @"AcqData")), true),
                (new MsDataFilePath(MakeFile(@"run.mzML")), false),
                (new MsDataFilePath(MakeFile(@"run.mzXML")), true),
                (new MsDataFilePath(MakeFile(@"single.wiff")), false),
                (new MsDataFilePath(MakeFile(@"multiple.wiff"), @"sample2", 1), true),
            };
            foreach (var f in files)
            {
                AssertEx.AreEqual(f.NeedsConversion, engine.GetSearchFileNeedsConversion(f.File, out _), f.File.ToString());
            }

            // Every run's results are in the one library DIA-NN writes beside the document
            AssertEx.AreEqual(Path.Combine(dir, DiannHelpers.OUTPUT_LIB_NAME), engine.GetSearchResultFilepath(files[0].File));
            AssertEx.AreEqual(Path.Combine(dir, DiannHelpers.STDOUT_LOG_NAME), engine.LogFilePath);
        }
    }
}
