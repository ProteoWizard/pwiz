/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Unit tests for <see cref="OrderedFileLanes"/>, the helper every FDR-stage file loop now
    /// runs through. The FDR stages rely on its ordering guarantee for byte-identical output, so
    /// that guarantee is tested here directly, under deliberately scrambled completion order,
    /// rather than inferred from a run that happened to finish files in order.
    /// </summary>
    [TestClass]
    public class OrderedFileLanesTest
    {
        [TestMethod]
        public void TestOrderedFileLanes()
        {
            foreach (int lanes in new[] { 1, 2, 4, 7 })
            {
                AssertConsumesInOrder(lanes);
                AssertReportsFirstFailureInOrder(lanes);
                AssertStopsWhenConsumerStops(lanes);
                AssertForVisitsEveryFileOnce(lanes);
            }
            AssertBoundsLookAhead();
            AssertForDoesNotWaitOnASlowFile();
        }

        /// <summary>
        /// Files finish in scrambled order - later files are made faster - yet the consumer sees
        /// 0, 1, 2, ... on the calling thread, each with its own result.
        /// </summary>
        private static void AssertConsumesInOrder(int lanes)
        {
            const int count = 23;
            int callingThread = Thread.CurrentThread.ManagedThreadId;
            var consumed = new List<int>();
            OrderedFileLanes.Run(count, lanes, i =>
            {
                Thread.Sleep((count - i) % 5);
                return i * 10;
            }, (i, result) =>
            {
                Assert.AreEqual(callingThread, Thread.CurrentThread.ManagedThreadId);
                Assert.AreEqual(i * 10, result);
                consumed.Add(i);
            });
            Assert.AreEqual(count, consumed.Count);
            for (int i = 0; i < count; i++)
                Assert.AreEqual(i, consumed[i]);
        }

        /// <summary>
        /// Two files fail; the one reported is the lower index - where the sequential loop would
        /// have stopped - as the original exception, not an AggregateException, and no file at or
        /// after it is consumed.
        /// </summary>
        private static void AssertReportsFirstFailureInOrder(int lanes)
        {
            var consumed = new List<int>();
            try
            {
                OrderedFileLanes.Run(12, lanes, i =>
                {
                    if (i == 9)
                        throw new InvalidOperationException(@"nine");
                    if (i == 5)
                    {
                        // Fails later in wall time than file 9 can, so the report must not follow
                        // the clock.
                        Thread.Sleep(30);
                        throw new ArgumentException(@"five");
                    }
                    return i;
                }, (i, result) => consumed.Add(i));
                Assert.Fail(@"no exception");
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual(@"five", ex.Message);
            }
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, consumed.ToArray());
        }

        /// <summary>
        /// A consumer that returns false ends the walk there, and RunWhile says so.
        /// </summary>
        private static void AssertStopsWhenConsumerStops(int lanes)
        {
            var consumed = new List<int>();
            bool completed = OrderedFileLanes.RunWhile(30, lanes, i => i, (i, result) =>
            {
                consumed.Add(i);
                return i < 6;
            });
            Assert.IsFalse(completed);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 6 }, consumed.ToArray());
            Assert.IsTrue(OrderedFileLanes.RunWhile(3, lanes, i => i, (i, result) => true));
        }

        /// <summary>
        /// The unordered form still visits every index exactly once, and reports its lowest
        /// failing index.
        /// </summary>
        private static void AssertForVisitsEveryFileOnce(int lanes)
        {
            const int count = 41;
            var visits = new int[count];
            OrderedFileLanes.For(count, lanes, i => Interlocked.Increment(ref visits[i]));
            foreach (int v in visits)
                Assert.AreEqual(1, v);

            try
            {
                OrderedFileLanes.For(count, lanes, i =>
                {
                    if (i == 3 || i == 17)
                        throw new InvalidOperationException(i.ToString());
                });
                Assert.Fail(@"no exception");
            }
            catch (InvalidOperationException ex)
            {
                // Lane timing decides whether 17 is reached before 3 stops the hand-out; when both
                // fail, 3 is the one reported.
                Assert.AreEqual(@"3", ex.Message);
            }
        }

        /// <summary>
        /// While the consumer is held on file 0, no lane may get more than 2 x lanes files ahead of
        /// it - file 0 itself has been handed over by then - which is what keeps a stage's memory
        /// to a few files whatever the cohort size.
        /// </summary>
        private static void AssertBoundsLookAhead()
        {
            const int lanes = 3;
            int produced = 0;
            int maxProducedWhileHeld = 0;
            OrderedFileLanes.Run(40, lanes, i =>
            {
                Interlocked.Increment(ref produced);
                return i;
            }, (i, result) =>
            {
                if (i == 0)
                {
                    Thread.Sleep(200);
                    maxProducedWhileHeld = Volatile.Read(ref produced);
                }
            });
            Assert.IsTrue(maxProducedWhileHeld <= 2 * lanes + 1,
                string.Format(@"{0} files produced while file 0 was held", maxProducedWhileHeld));
            Assert.AreEqual(40, produced);
        }

        /// <summary>
        /// The unordered form has no look-ahead bound, so one slow file does not stop the other
        /// lanes from taking every remaining file.
        /// </summary>
        private static void AssertForDoesNotWaitOnASlowFile()
        {
            const int count = 50;
            int fastDone = 0;
            int fastDoneBeforeSlowEnded = -1;
            OrderedFileLanes.For(count, 4, i =>
            {
                if (i == 0)
                {
                    Thread.Sleep(1000);
                    fastDoneBeforeSlowEnded = Volatile.Read(ref fastDone);
                    return;
                }
                Interlocked.Increment(ref fastDone);
            });
            Assert.AreEqual(count - 1, fastDoneBeforeSlowEnded);
        }
    }
}
