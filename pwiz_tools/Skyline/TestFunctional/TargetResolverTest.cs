/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class TargetResolverTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestTargetResolver()
        {
            TestFilesZip = @"TestFunctional\TargetResolverTest.zip";
            RunFunctionalTest();
        }


        protected override void DoTest()
        {
            var documentPath = TestFilesDir.GetTestPath("TargetResolverTest.sky");
            TargetResolver resolver;
            RunUI(() =>
            {
                SkylineWindow.OpenFile(documentPath);
                resolver = TargetResolver.MakeTargetResolver(SkylineWindow.DocumentUI);
                var target = resolver.ResolveTarget("Glc(04)"); // Find by name
                Assert.AreEqual("Glc(04)", target.DisplayName);
                target = resolver.ResolveTarget("ZXPLRDFHBYIQOX-BTBVOZEKSA-N"); // Find by InChiKey
                Assert.AreEqual("Glc(04)", target.DisplayName);
                target = resolver.ResolveTarget("PEPTIDER"); // Find by sequence
                Assert.AreEqual("PEPTIDER", target.DisplayName);
            });
        }
    }
}
