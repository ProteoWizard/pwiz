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
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
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
            AssertForReportsEveryConcurrentFailure();
            AssertBoundsLookAhead();
            AssertForDoesNotWaitOnASlowFile();
            AssertLaneStartFailureStopsStartedLanes();
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
        /// The unordered form still visits every index exactly once, and a single failure comes
        /// back as the original exception with the stack it was thrown from.
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
                    if (i == 3)
                        ThrowForFile(i);
                });
                Assert.Fail(@"no exception");
            }
            catch (InvalidOperationException ex)
            {
                Assert.AreEqual(@"3", ex.Message);
                Assert.IsNotNull(ex.StackTrace);
                StringAssert.Contains(ex.StackTrace, nameof(ThrowForFile));
            }
        }

        /// <summary>
        /// Two files fail while both are running: neither is dropped. They come back together in
        /// file order, as the Parallel.For the unordered form replaced reported them, so a defect
        /// beside a user error still reads as a defect.
        /// </summary>
        private static void AssertForReportsEveryConcurrentFailure()
        {
            var userError = new IOException(@"cannot open file 0");
            var defect = new NullReferenceException();
            // Files 0 and 1 go to different lanes - the one that took 0 is held until 1 has
            // started - so both are in flight before either fails.
            using (var bothStarted = new CountdownEvent(2))
            {
                try
                {
                    OrderedFileLanes.For(10, 4, i =>
                    {
                        if (i > 1)
                            return;
                        bothStarted.Signal();
                        Assert.IsTrue(bothStarted.Wait(TimeSpan.FromSeconds(60)));
                        // The higher index fails first, so the report order cannot be the clock's.
                        if (i == 0)
                        {
                            Thread.Sleep(20);
                            throw userError;
                        }
                        throw defect;
                    });
                    Assert.Fail(@"no exception");
                }
                catch (AggregateException ex)
                {
                    Assert.AreEqual(2, ex.InnerExceptions.Count);
                    Assert.AreSame(userError, ex.InnerExceptions[0]);
                    Assert.AreSame(defect, ex.InnerExceptions[1]);
                    // The mixed aggregate is the defect report, not the user error's message.
                    Assert.AreSame(ex, CommonExceptionUtil.UnwrapUserException(ex));
                }
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
        /// lanes from taking every remaining file: file 0 does not finish until all the others
        /// have, which would never happen if they waited on it.
        /// </summary>
        private static void AssertForDoesNotWaitOnASlowFile()
        {
            const int count = 50;
            bool othersFinishedFirst = false;
            using (var othersDone = new CountdownEvent(count - 1))
            {
                OrderedFileLanes.For(count, 4, i =>
                {
                    if (i == 0)
                        othersFinishedFirst = othersDone.Wait(TimeSpan.FromSeconds(60));
                    else
                        othersDone.Signal();
                });
            }
            Assert.IsTrue(othersFinishedFirst);
        }

        /// <summary>
        /// A lane whose thread cannot be started - as Thread.Start fails when the process is out
        /// of threads - fails the call with that exception, and the lanes already started are
        /// stopped and waited for first: none is still running once the call returns. Run, Enumerate
        /// and For each start their own lanes, so each is checked.
        /// </summary>
        private static void AssertLaneStartFailureStopsStartedLanes()
        {
            const int count = 200;
            const int failingLane = 2;
            AssertLaneStartFailure(failingLane, produce =>
                OrderedFileLanes.Run(count, 4, produce, (i, result) => { }));
            AssertLaneStartFailure(failingLane, produce =>
            {
                foreach (int result in OrderedFileLanes.Enumerate(count, 4, produce))
                    Assert.IsTrue(result >= 0);
            });
            AssertLaneStartFailure(failingLane, produce =>
                OrderedFileLanes.For(count, 4, i => produce(i)));
        }

        /// <summary>
        /// Fails the start of lane <paramref name="failingLane"/> once a started lane is at work,
        /// runs <paramref name="runLanes"/> with a produce that records its thread, and checks the
        /// start failure comes back and every recorded thread has ended.
        /// </summary>
        private static void AssertLaneStartFailure(int failingLane, Action<Func<int, int>> runLanes)
        {
            var startFailure = new OutOfMemoryException(@"lane start failure");
            var laneThreads = new List<Thread>();
            using (var working = new ManualResetEventSlim())
            {
                OrderedFileLanes.LaneStartFailure = lane =>
                {
                    if (lane != failingLane)
                        return null;
                    // Not vacuous: an earlier lane is producing when the start fails.
                    Assert.IsTrue(working.Wait(TimeSpan.FromSeconds(60)));
                    return startFailure;
                };
                try
                {
                    runLanes(i =>
                    {
                        lock (laneThreads)
                        {
                            if (!laneThreads.Contains(Thread.CurrentThread))
                                laneThreads.Add(Thread.CurrentThread);
                        }
                        working.Set();
                        Thread.Sleep(5);
                        return i;
                    });
                    Assert.Fail(@"no exception");
                }
                catch (OutOfMemoryException ex)
                {
                    Assert.AreSame(startFailure, ex);
                }
                finally
                {
                    OrderedFileLanes.LaneStartFailure = null;
                }
            }
            lock (laneThreads)
            {
                Assert.IsTrue(laneThreads.Count > 0);
                foreach (var thread in laneThreads)
                    Assert.IsFalse(thread.IsAlive);
            }
        }

        private static void ThrowForFile(int i)
        {
            throw new InvalidOperationException(i.ToString());
        }
    }
}
