/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
{
    /// <summary>
    /// Summary description for ResultsUtilTest
    /// </summary>
    [TestClass]
    public class ResultsUtilTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestBaseNameMatch()
        {
            Assert.IsTrue(MeasuredResults.IsBaseNameMatch("BaseName123", "BaseName123.wiff"));
            Assert.IsTrue(MeasuredResults.IsBaseNameMatch("BaseName123.c", "BaseName123"));
            Assert.IsTrue(MeasuredResults.IsBaseNameMatch("BaseName123.c", "BASENAME123"));
            Assert.IsTrue(MeasuredResults.IsBaseNameMatch("BASENAME123", "basename123"));
            Assert.IsFalse(MeasuredResults.IsBaseNameMatch("BaseName123", "BaseName123-wiff"));
            Assert.IsFalse(MeasuredResults.IsBaseNameMatch("BaseName123", "BaseName123_wiff"));
            Assert.IsFalse(MeasuredResults.IsBaseNameMatch("BaseName123", "BaseName1234"));
            Assert.IsFalse(MeasuredResults.IsBaseNameMatch("BaseName1234", "BaseName123"));
        }

        [TestMethod]
        public void TestChromInfoListConstructor()
        {
            var chromInfoList = new ChromInfoList<TransitionChromInfo>(null);
            Assert.AreEqual(0, chromInfoList.Count);
            chromInfoList = new ChromInfoList<TransitionChromInfo>(new TransitionChromInfo[] {null, null});
            Assert.AreEqual(0, chromInfoList.Count);
            var transitionChromInfo = new TransitionChromInfo(new ChromFileInfoId(), 0, null, 1, 0, 2, null, 1, 0, 1, 1,
                false, false, 0, PeakIdentification.FALSE, 0, 0, Annotations.EMPTY, UserSet.FALSE, false);
            chromInfoList = new ChromInfoList<TransitionChromInfo>(new[] {transitionChromInfo});
            Assert.AreEqual(1, chromInfoList.Count);
            chromInfoList = new ChromInfoList<TransitionChromInfo>(new[] {transitionChromInfo, null});
            Assert.AreEqual(1, chromInfoList.Count);
            chromInfoList = new ChromInfoList<TransitionChromInfo>(new[] { transitionChromInfo, null, transitionChromInfo});
            Assert.AreEqual(2, chromInfoList.Count);
        }
    }
}