/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    [TestClass]
    public class ChromCollectorTest : AbstractUnitTest
    {
        /// <summary>
        /// Utility class to create a tiny allocator and return its block count and size.
        /// </summary>
        private class TestAllocator : IDisposable
        {
            public readonly int BlockCount;
            public readonly int BlockSize;
            private const int BUFFER_PARTS = 16;

            /// <summary>
            /// Create an chromatogram allocator with the given buffer size and initial block size (in floats).
            /// </summary>
            public TestAllocator(int bufferSize, int blockSize)
            {
                var allocator = new ChromCollector.Allocator(@".\ChromCollectorTest", bufferSize, BUFFER_PARTS, blockSize);
                Assert.AreSame(ChromCollector.Allocator.Instance, allocator);

                BlockSize = blockSize;
                BlockCount = bufferSize/BUFFER_PARTS/(blockSize*sizeof(float))*BUFFER_PARTS;
            }

            public void Dispose()
            {
                var allocator = ChromCollector.Allocator.Instance;
                allocator.Dispose();
                allocator.Dispose();    // extra Dispose for unit test coverage
            }

            /// <summary>
            /// Write a number of chromatograms (blockCount) to the allocator, with the given block size (in floats).
            /// </summary>
            public static void Test(
                int blockCount,
                int blockSize,
                int averageBytesPaged = -1,
                bool singleTimes = false,
                Action<ChromCollector, int> actionPerCollector = null)
            {
                // Create the requested number of chromatogram blocks.
                var blocks = new ChromCollector[blockCount];
                for (int i = 0; i < blockCount; i++)
                {
                    blocks[i] = new ChromCollector();
                    if (singleTimes)
                        blocks[i].TimesCollector = new ChromCollector();
                }

                // Add intensities (or times and intensities) to each chromatogram.
                for (int i = 0; i < blockSize; i++)
                {
                    for (int j = 0; j < blockCount; j++)
                    {
                        if (singleTimes)
                        {
                            blocks[j].AddTime(i*j);
                            blocks[j].Add(i + j);
                        }
                        else
                        {
                            blocks[j].Add(i + j);
                        }
                    }
                }

                // Verify that we get back what we wrote.
                for (int i = 0; i < blockCount; i++)
                {
                    var data = blocks[i].GetData();
                    Assert.AreEqual(data.Length, blockSize);
                    Assert.AreEqual(data.Length, blocks[i].Length);
                    for (int j = 0; j < data.Length; j++)
                    {
                        Assert.AreEqual(data[j], i + j);
                    }
                }

                // Optionally verify the average number of bytes written to disk per block.
                if (averageBytesPaged >= 0)
                    Assert.AreEqual(averageBytesPaged, ChromCollector.Allocator.Instance.AverageBytesPerPagedBlock);

                // Execute an optional action for each chromatogram.
                if (actionPerCollector != null)
                {
                    for (int i = 0; i < blockCount; i++)
                        actionPerCollector(blocks[i], i);
                }
            }
        }

        [TestMethod]
        public void ChromCollectorSimpleTest()
        {
            // Most basic test of Allocator: no block paging or subdivision.
            using (var testAllocator = new TestAllocator(100000, 100))
                TestAllocator.Test(testAllocator.BlockCount, testAllocator.BlockSize, 0);

            // Code coverage for badly parameterized Allocator.
            using (new TestAllocator(100000, 100000))
            {
            }
        }

        [TestMethod]
        public void ChromCollectorPageOnlyTest()
        {
            // Test Allocator paging (exceed block size slightly).
            using (var testAllocator = new TestAllocator(100000, 100))
                TestAllocator.Test(testAllocator.BlockCount, testAllocator.BlockSize + 1, testAllocator.BlockSize*sizeof(float));
        }

        [TestMethod]
        public void ChromCollectorSubdivideOnlyTest()
        {
            // Test Allocator subdivision (twice as many blocks as accomodated by initial settings).
            using (var testAllocator = new TestAllocator(100000, 100))
                TestAllocator.Test(testAllocator.BlockCount*2, testAllocator.BlockSize/2 - 1, 0);
        }

        [TestMethod]
        public void ChromCollectorPageAndSubdivideTest()
        {
            // Stress the Allocator (longer blocks and more of them than accomodated by default settings).
            using (var testAllocator = new TestAllocator(100000, 100))
                TestAllocator.Test(testAllocator.BlockCount * 5, testAllocator.BlockSize *3);
        }

        [TestMethod]
        public void ChromCollectorSingleTimeTest()
        {
            // Test Allocator storing both times and intensities.
            using (var testAllocator = new TestAllocator(100000, 100))
            {
                TestAllocator.Test(testAllocator.BlockCount, testAllocator.BlockSize, -1, true, (collector, index) =>
                    {
                        float[] times, intensities, massErrors;
                        int[] scanIds;
                        collector.ReleaseChromatogram(out times, out scanIds, out intensities, out massErrors);
                        Assert.AreEqual(testAllocator.BlockSize, times.Length);
                        Assert.AreEqual(testAllocator.BlockSize, intensities.Length);
                        for (int i = 0; i < testAllocator.BlockSize; i++)
                        {
                            Assert.AreEqual(index*i, times[i]);
                            Assert.AreEqual(index + i, intensities[i]);
                        }
                    });
            }
        }

        [TestMethod]
        public void ChromCollectorGroupedTimeTest()
        {
            // Test Allocator with grouped times.
            using (var testAllocator = new TestAllocator(100000, 100))
            {
                var groupedTimesCollector = new ChromCollector();
                for (int i = 0; i < testAllocator.BlockSize; i++)
                    groupedTimesCollector.Add(i * 10);
                TestAllocator.Test(testAllocator.BlockCount, testAllocator.BlockSize, -1, false, (collector, index) =>
                {
                    collector.TimesCollector = groupedTimesCollector;
                    float[] times, intensities, massErrors;
                    int[] scanIds;
                    collector.ReleaseChromatogram(out times, out scanIds, out intensities, out massErrors);
                    Assert.AreEqual(testAllocator.BlockSize, times.Length);
                    Assert.AreEqual(testAllocator.BlockSize, intensities.Length);
                    for (int i = 0; i < testAllocator.BlockSize; i++)
                    {
                        Assert.AreEqual(i * 10, times[i]);
                        Assert.AreEqual(index + i, intensities[i]);
                    }
                });
            }
        }

        [TestMethod]
        public void ChromCollectorRandomTest()
        {
            using (var testAllocator = new TestAllocator(100000, 100))
            {
                var blocks = new List<ChromCollector>();
                for (int i = 0; i < testAllocator.BlockCount; i++)
                    blocks.Add(new ChromCollector());
                for (int i = 0; i < testAllocator.BlockSize; i++)
                {
                    for (int j = 0; j < testAllocator.BlockCount; j++)
                        blocks[j].Add(i + j);
                }

                // Add blocks and data at random.
                var random = new Random(397);
                for (int i = 0; i < 1000; i++)
                {
                    if (random.Next(2) == 0)
                    {
                        var newCollector = new ChromCollector();
                        newCollector.Add(blocks.Count);
                        blocks.Add(newCollector);
                    }
                    else
                    {
                        var index = random.Next(blocks.Count);
                        blocks[index].Add(index + blocks[index].Length);
                    }
                }

                // Verify that we read back the expected values.
                for (int i = 0; i < blocks.Count; i++)
                {
                    var data = blocks[i].GetData();
                    for (int j = 0; j < data.Length; j++)
                    {
                        Assert.AreEqual(data[j], i + j);
                    }
                }
            }
        }

        [TestMethod]
        public void ChromCollectorSortTimesTest()
        {
            // Test Allocator with out of order grouped times.
            using (var testAllocator = new TestAllocator(100000, 100))
            {
                var groupedTimesCollector = new ChromCollector();
                // Reverse times.
                for (int i = 0; i < testAllocator.BlockSize; i++)
                    groupedTimesCollector.Add(testAllocator.BlockSize-1 - i);
                TestAllocator.Test(testAllocator.BlockCount, testAllocator.BlockSize, -1, false, (collector, index) =>
                {
                    collector.TimesCollector = groupedTimesCollector;
                    float[] times, intensities, massErrors;
                    int[] scanIds;
                    collector.ReleaseChromatogram(out times, out scanIds, out intensities, out massErrors);
                    Assert.AreEqual(testAllocator.BlockSize, times.Length);
                    Assert.AreEqual(testAllocator.BlockSize, intensities.Length);
                    // Times will be ordered, and data reversed.
                    for (int i = 0; i < testAllocator.BlockSize; i++)
                    {
                        Assert.AreEqual(i, times[i]);
                        Assert.AreEqual(index + testAllocator.BlockSize-1 - i, intensities[i]);
                    }

                    // Use ToString method for coverage.
                    Assert.AreNotEqual("", collector.ToString());
                });
            }
        }
    }
}
