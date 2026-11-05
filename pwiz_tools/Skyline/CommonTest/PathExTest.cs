/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.SkylineTestUtil;

namespace CommonTest
{
    [TestClass]
    public class PathExTest : AbstractUnitTest
    {
        /// <summary>
        /// The name a test's tools directory gets. Short names are used as they are; longer ones are
        /// shortened to their capitals and digits plus the original length, to keep command lines run
        /// against the directory under the length limit.
        /// </summary>
        [TestMethod]
        public void TestGetTestDirectoryName()
        {
            var cases = new[]
            {
                // Test name, culture, expected directory name
                new[] { @"Foo", @"ja", @"Foo_ja" },
                new[] { @"TestFooBar", @"en-US", @"TestFooBar_en-US" },          // Exactly 10, left alone
                new[] { @"TestFooBars", @"en-US", @"FB11_en-US" },               // 11, shortened
                new[] { @"Test7Widget", @"tr-TR", @"7W11_tr-TR" },               // Digits are kept
                new[] { @"TestDdaSearchDependencyErrors", @"fr-FR", @"DSDE29_fr-FR" },
                new[] { @"TestExportTestList", @"zh-CHS", @"EL18_zh-CHS" }       // Every "Test" comes out
            };

            foreach (var testCase in cases)
            {
                AssertEx.AreEqual(testCase[2], PathEx.GetTestDirectoryName(testCase[0], testCase[1]),
                    $@"unexpected tools directory name for {testCase[0]}");
            }

            // Shortening is lossy, so different tests can be given the same directory. The parallel test
            // runner therefore hands out work keyed on this name rather than on the test, so that two
            // tests which collide here are never run at the same time (issue 4447).
            AssertEx.AreEqual(PathEx.GetTestDirectoryName(@"TestDdaSearch", @"en-US"),
                PathEx.GetTestDirectoryName(@"TestDiaSearch", @"en-US"));
        }

        /// <summary>
        /// Paths shown to a user lose long-path syntax, and a test can show one folder as another.
        /// </summary>
        [TestMethod]
        public void TestDisplayPath()
        {
            Assert.IsNull(PathEx.DisplayPath(null));
            AssertEx.AreEqual(@"C:\proj\pwiz-work3\a.sky", PathEx.DisplayPathsInText(@"C:\proj\pwiz-work3\a.sky"));
            AssertEx.AreEqual(@"C:\Data\File.raw", PathEx.DisplayPath(@"C:\Data\File.raw"));
            AssertEx.AreEqual(@"C:\Data\File.raw", PathEx.DisplayPath(@"C:\Data\File.raw".ToLongPath()));
            AssertEx.AreEqual(@"\\server\share\File.raw", PathEx.DisplayPath(@"\\?\UNC\server\share\File.raw"));

            var root = PathEx.DisplayPathRoot;
            var rootShown = PathEx.DisplayPathRootShown;
            try
            {
                PathEx.DisplayPathRoot = @"C:\proj\pwiz-work3\";
                PathEx.DisplayPathRootShown = @"C:\proj\pwiz";
                AssertEx.AreEqual(@"C:\proj\pwiz\pwiz_tools\File.raw",
                    PathEx.DisplayPath(@"C:\proj\pwiz-work3\pwiz_tools\File.raw"));
                AssertEx.AreEqual(@"C:\proj\pwiz\pwiz_tools\File.raw",
                    PathEx.DisplayPath(@"c:\PROJ\pwiz-work3\pwiz_tools\File.raw".ToLongPath()));
                AssertEx.AreEqual(@"C:\proj\pwiz", PathEx.DisplayPath(@"C:\proj\pwiz-work3"));
                // Only whole folder names match.
                AssertEx.AreEqual(@"C:\proj\pwiz-work30\File.raw", PathEx.DisplayPath(@"C:\proj\pwiz-work30\File.raw"));
                AssertEx.AreEqual(@"D:\pwiz-work3\File.raw", PathEx.DisplayPath(@"D:\pwiz-work3\File.raw"));

                // Paths inside message text.
                AssertEx.AreEqual(@"Failed to open C:\proj\pwiz\a.sky and 'C:\proj\pwiz\b.sky'.",
                    PathEx.DisplayPathsInText(@"Failed to open C:\proj\pwiz-work3\a.sky and 'c:\PROJ\pwiz-work3\b.sky'."));
                AssertEx.AreEqual(@"Kept C:\proj\pwiz-work30\a.sky",
                    PathEx.DisplayPathsInText(@"Kept C:\proj\pwiz-work30\a.sky"));
            }
            finally
            {
                PathEx.DisplayPathRoot = root;
                PathEx.DisplayPathRootShown = rootShown;
            }
        }
    }
}
