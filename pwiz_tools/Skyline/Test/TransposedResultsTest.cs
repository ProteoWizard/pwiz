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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class TransposedResultsTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestTransposedResults()
        {
            var transitionChromInfo1 = new TransitionChromInfo(new ChromFileInfoId(), 1, 2, 3, 4, 5,
                IonMobilityFilter.EMPTY, 6, 7, 8, 9, false, true, 10, PeakIdentification.ALIGNED, 11, 12,
                Annotations.EMPTY, UserSet.FALSE, false, null);
            var transitionChromInfo2 = new TransitionChromInfo(new ChromFileInfoId(), 13, 14, 15, 16, 17,
                IonMobilityFilter.EMPTY, 18, 19, 20, 21, true, false, 22, PeakIdentification.TRUE, 23, 24,
                Annotations.EMPTY, UserSet.IMPORTED, true, null);
            var transposedResults = (TransposedTransitionChromInfos) TransposedTransitionChromInfos.EMPTY.ChangeColumns(
                TransitionChromInfo.TRANSPOSER.ToColumns(new[] { transitionChromInfo1, transitionChromInfo2 }));
            AssertEx.AreEqual(transitionChromInfo1, transposedResults.GetRow(0));
            AssertEx.AreEqual(transitionChromInfo2, transposedResults.GetRow(1));
            var array = new[] { transposedResults };
            TransitionChromInfo.TRANSPOSER.EfficientlyStore(null, array);
            AssertEx.AreEqual(transitionChromInfo1, array[0].ToRows(0, 1).GetValue(0));
            AssertEx.AreEqual(transitionChromInfo2, array[0].ToRows(1, 1).GetValue(0));
        }
    }
}
