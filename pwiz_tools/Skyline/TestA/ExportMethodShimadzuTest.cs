/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    [TestClass]
    public class ExportMethodShimadzuTest : AbstractUnitTest
    {
        private const string ZIP_FILE = @"TestA\ExportMethodShimadzuTest.zip";

        [TestMethod]
        public void TestExportMethodShimadzu()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            var docPath = testFilesDir.GetTestPath("bgal.sky");
            var doc = ResultsUtil.DeserializeDocument(docPath);

            var outPath = testFilesDir.GetTestPath("out.lcm");
            var templatePath = testFilesDir.GetTestPath("40.lcm");
            var exporter = new ShimadzuMethodExporter(doc) {RunLength = 30};
            exporter.ExportMethod(outPath, templatePath, null);

            Assert.AreEqual(540672, new FileInfo(outPath).Length);
        }
    }
}
