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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ProgressStatusChangeSegmentsEndPercentageTest : AbstractUnitTest
    {
        [TestMethod]
        public void ChangeSegments_NullSegmentPercentageEnds_ThrowsArgumentException()
        {
            var progress = new ProgressStatus();
            AssertEx.ThrowsException<NullReferenceException>(() => progress.ChangeSegments(0, null));
        }

        [TestMethod]
        public void ChangeSegments_EmptySegmentPercentageEnds_ThrowsArgumentException()
        {
            var progress = new ProgressStatus();
            AssertEx.ThrowsException<ArgumentException>(() =>
                progress.ChangeSegments(0, new int[0]), "ChangeSegments was passed an empty array of segment ends.");
        }

        [TestMethod]
        public void ChangeSegments_NonStrictlyIncreasingArray_ThrowsArgumentException()
        {
            var progress = new ProgressStatus();
            AssertEx.ThrowsException<ArgumentException>(() =>
                progress.ChangeSegments(0, new[] { 20, 40, 40, 80, 100 }), "ChangeSegments was passed an array of segment ends that is not strictly increasing.");
        }

        [TestMethod]
        public void ChangeSegments_OutOfRangeFirstElement_ThrowsArgumentException()
        {
            var progress = new ProgressStatus();
            AssertEx.ThrowsException<ArgumentException>(() =>
                    progress.ChangeSegments(0, new[] { 0, 50, 100 }),
                "ChangeSegments was passed an array of segment ends that contains values out of the expected range [1,100].");
        }

        [TestMethod]
        public void ChangeSegments_OutOfRangeLastElement_ThrowsArgumentException()
        {
            var progress = new ProgressStatus();
            AssertEx.ThrowsException<ArgumentException>(() =>
                    progress.ChangeSegments(0, new[] { 0, 50, 100 }),
                "ChangeSegments was passed an array of segment ends that contains values out of the expected range [1,100].");
        }

        [TestMethod]
        public void ChangeSegments_NegativeSegment_ThrowsArgumentException()
        {
            var progress = new ProgressStatus();
            AssertEx.ThrowsException<ArgumentException>(() => 
                progress.ChangeSegments(-1, new[] { 20, 40, 60 }), "ChangeSegments was passed a negative segment.");
        }

        [TestMethod]
        public void ChangeSegments_ValidInput_DoesNotThrowAndUpdatesProperties()
        {
            var progress = new ProgressStatus();
            var segmentPercentageEnds = new[] { 20, 40, 60, 80, 100 };
            int segment = 2;

            var result = progress.ChangeSegments(segment, segmentPercentageEnds);

            Assert.AreNotEqual(result, null);
            Assert.AreEqual(40, result.PercentComplete); // segment - 1 = 40
            Assert.AreEqual(40, result.PercentZoomStart); // segment - 1 = 40  
            Assert.AreEqual(60, result.PercentZoomEnd); // segment = 60
            Assert.AreEqual(segmentPercentageEnds, result.SegmentPercentEnds);
            Assert.AreEqual(5, result.SegmentCount);
            Assert.AreEqual(2, result.Segment);
        }

        [TestMethod]
        public void ChangeSegments_SegmentBeyondCount_DoesNotThrowAndSetsMaxValues()
        {
            var progress = new ProgressStatus();
            var segmentPercentageEnds = new[] { 20, 40, 60 };
            int segment = 5; // Beyond segmentCount (3)

            var result = progress.ChangeSegments(segment, segmentPercentageEnds);

            Assert.AreNotEqual(result, null);
            Assert.AreEqual(60, result.PercentComplete); // Last end
            Assert.AreEqual(60, result.PercentZoomStart); // Last end
            Assert.AreEqual(100, result.PercentZoomEnd); // 100
            Assert.AreEqual(segmentPercentageEnds, result.SegmentPercentEnds);
            Assert.AreEqual(3, result.SegmentCount);
            Assert.AreEqual(5, result.Segment);
        }
    }
}

