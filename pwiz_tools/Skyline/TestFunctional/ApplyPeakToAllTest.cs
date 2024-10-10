/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ApplyPeakToAllTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestApplyPeakToAll()
        {
            TestFilesZip = @"TestFunctional\ApplyPeakToAllTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ApplyPeakToAllTest.sky")));
            WaitForDocumentLoaded();
            RunUI(() =>
            {
                PeakMatcherTestUtil.SelectAndApplyPeak("GILAADESVGSMAK", null, "D_103_REP1", false, false, 19.0987);
                PeakMatcherTestUtil.VerifyPeaks(MakeVerificationDictionary(
                    19.09872, 18.38107,
                    19.10433, 18.73012, 18.35188));
            });
            RunUI(() =>
            {
                PeakMatcherTestUtil.SelectAndApplyPeak("LNDGSQITFEK", null, "D_138_REP1", false, false, 23.5299);
                PeakMatcherTestUtil.VerifyPeaks(MakeVerificationDictionary(
                    23.45410, 23.11210, 
                    23.52992, 22.89255, 23.04702));
            });
            // Apply to subsequent
            RunUI(() =>
            {
                PeakMatcherTestUtil.SelectAndApplyPeak("DLTGFPQGADQR", null, "H_159_REP2", true, false, 24.7955);
                PeakMatcherTestUtil.VerifyPeaks(MakeVerificationDictionary(
                    24.02537, 24.60460, 
                    23.99268, 23.08690, 24.79552));
            });
            RunUI(() =>
            {
                PeakMatcherTestUtil.SelectAndApplyPeak("GATYAFSGSHYWR", null, "D_103_REP3", true, false, 16.4223);
                PeakMatcherTestUtil.VerifyPeaks(MakeVerificationDictionary(
                    17.38182, 16.42228, 
                    17.23210, 17.15165, 16.88772));
            });

            // Test apply to all on a case with an obvious reference point
            // with two small (sometimes non-existent) peaks on either side
            RunUI(() =>
            {
                PeakMatcherTestUtil.SelectAndApplyPeak("LGGEEVSVAC[+57.0]K", null, "H_148_REP1", false, false, 13.1616);
                PeakMatcherTestUtil.VerifyPeaks(MakeVerificationDictionary(
                    13.16143, 13.12692, 
                    13.12773, 13.16158, 13.06082));
            });
            RunUI(() =>
            {
                PeakMatcherTestUtil.SelectAndApplyPeak("LGGEEVSVAC[+57.0]K", null, "H_148_REP1", false, false, 13.4631);
                PeakMatcherTestUtil.VerifyPeaks(MakeVerificationDictionary(
                    13.46293, 13.39492, 
                    13.42923, 13.46308, 13.36232));
            });
            RunUI(() =>
            {
                PeakMatcherTestUtil.SelectAndApplyPeak("LGGEEVSVAC[+57.0]K", null, "H_148_REP1", false, false, 13.6641);
                PeakMatcherTestUtil.VerifyPeaks(MakeVerificationDictionary(
                    14.30043, 13.79692, 
                    13.83123, 13.66408, 13.52982));
            });
            // Test "Apply Peak To All" with multiple selection
            RunUI(() =>
            {
                var identityPaths = new List<IdentityPath>();
                foreach ((string pep, double time) in new[]
                         {
                             Tuple.Create("GILAADESVGSMAK", 21.9337),
                             Tuple.Create("LGGEEVSVAC[+57.0]K", 11.1179),
                             Tuple.Create("LNDGSQITFEK", 27.5473)
                         })
                {
                    PeakMatcherTestUtil.SelectPeak(pep, null, "D_103_REP1", time);
                    identityPaths.Add(SkylineWindow.SelectedPath);
                }

                SkylineWindow.SequenceTree.SelectedPaths = identityPaths;
                SkylineWindow.ApplyPeak(false, false);
                SkylineWindow.SequenceTree.SelectedPath = identityPaths[0];
                PeakMatcherTestUtil.VerifyPeaks(MakeVerificationDictionary(
                    21.93372, 20.88247, 21.79523, 22.86122, 22.25558));
                SkylineWindow.SequenceTree.SelectedPath = identityPaths[1];
                PeakMatcherTestUtil.VerifyPeaks(MakeVerificationDictionary(
                    11.11793, 11.15042, 10.78273, 11.15158, 11.18482));
                SkylineWindow.SequenceTree.SelectedPath = identityPaths[2];
                PeakMatcherTestUtil.VerifyPeaks(MakeVerificationDictionary(
                    27.54730, 27.24320, 26.82722, 27.28895, 27.10232));
            });
        }

        private static ImmutableList<string> AllReplicates => ImmutableList.ValueOf(new[]
        {
            "D_103_REP1", "D_103_REP3",
            "D_138_REP1",
            "H_148_REP1",
            "H_159_REP2"
        });

        private static Dictionary<string, double> MakeVerificationDictionary(params double[] expected)
        {
            Assert.AreEqual(AllReplicates.Count, expected.Length);
            return AllReplicates.Zip(expected, (name, expect) => new { name, expect })
                .ToDictionary(x => x.name, x => x.expect);
        }

    }
}
