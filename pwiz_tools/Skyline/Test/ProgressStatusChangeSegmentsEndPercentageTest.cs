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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ProgressStatusChangeSegmentsEndPercentageTest : AbstractUnitTest
    {
        [TestMethod]
        public void ProgressStatusChangeSegmentsTest()
        {
            var progress = new ProgressStatus();
            AssertEx.ThrowsException<NullReferenceException>(() => progress.ChangeSegments(0, null));

            var emptyPercents = Array.Empty<int>();
            AssertEx.ThrowsException<ArgumentException>(() =>
                    progress.ChangeSegments(0, ImmutableList<int>.ValueOf(emptyPercents)),
                "ChangeSegments was passed an empty array of segment ends.");

            var evenPercents = new[] { 20, 40, 40, 80, 100 };
            AssertEx.ThrowsException<ArgumentException>(() =>
                    progress.ChangeSegments(0, ImmutableList<int>.ValueOf(evenPercents)),
                "ChangeSegments was passed an array of segment ends that is not strictly increasing.");

            var outOfRangePercents = new[] { 0, 50, 100 };
            AssertEx.ThrowsException<ArgumentException>(() =>
                    progress.ChangeSegments(0, ImmutableList<int>.ValueOf(outOfRangePercents)),
                "ChangeSegments was passed an array of segment ends that contains values out of the expected range [1,100].");

            var outOfRangeEndPercents = new[] { 10, 50, 101 };
            AssertEx.ThrowsException<ArgumentException>(() =>
                    progress.ChangeSegments(0, ImmutableList<int>.ValueOf(outOfRangeEndPercents)),
                "ChangeSegments was passed an array of segment ends that contains values out of the expected range [1,100].");

            var negativeIndexPercents = new[] { 20, 40, 60 };
            AssertEx.ThrowsException<ArgumentException>(() =>
                    progress.ChangeSegments(-1, ImmutableList<int>.ValueOf(negativeIndexPercents)),
                "ChangeSegments was passed a negative segment.");

            ChangeSegments_ValidInput_DoesNotThrowAndUpdatesProperties();
            ChangeSegments_SegmentBeyondCount_DoesNotThrowAndSetsMaxValues();
        }

        public void ChangeSegments_ValidInput_DoesNotThrowAndUpdatesProperties()
        {
            var progress = new ProgressStatus();
            
            // Test with valid input that properties get updated as expected
            var segmentPercentageEnds = new[] { 20, 40, 60, 80, 100 };
            int segment = 2;

            var result = progress.ChangeSegments(segment, ImmutableList<int>.ValueOf(segmentPercentageEnds));
            Assert.AreNotEqual(result, null);
            Assert.AreEqual(40, result.PercentComplete); // segment - 1 = 40
            Assert.AreEqual(40, result.PercentZoomStart); // segment - 1 = 40  
            Assert.AreEqual(60, result.PercentZoomEnd); // segment = 60
            CollectionAssert.AreEqual(segmentPercentageEnds, result.SegmentPercentEnds.ToArray());
            Assert.AreEqual(5, result.SegmentCount);
            Assert.AreEqual(2, result.Segment);
        }

        public void ChangeSegments_SegmentBeyondCount_DoesNotThrowAndSetsMaxValues()
        {
            var progress = new ProgressStatus();
            var segmentPercentageEnds = new[] { 20, 40, 60 };
            int segment = 5; // Beyond segmentCount (3)
           
            var result = progress.ChangeSegments(segment, ImmutableList<int>.ValueOf(segmentPercentageEnds));
            Assert.AreNotEqual(result, null);
            Assert.AreEqual(60, result.PercentComplete); // Last end
            Assert.AreEqual(60, result.PercentZoomStart); // Last end
            Assert.AreEqual(100, result.PercentZoomEnd); // 100
            CollectionAssert.AreEqual(segmentPercentageEnds, result.SegmentPercentEnds.ToArray());
            Assert.AreEqual(3, result.SegmentCount);
            Assert.AreEqual(5, result.Segment);
        }
    }
}

