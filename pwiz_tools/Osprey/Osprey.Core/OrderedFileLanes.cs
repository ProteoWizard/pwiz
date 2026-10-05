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
using System.Runtime.ExceptionServices;
using System.Threading;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Runs per-file work on a few dedicated lanes while keeping every cross-file effect in file
    /// order.
    ///
    /// <para>The FDR stages walk the input files one at a time because each file feeds state
    /// shared across files - a competition, a min-reduction, an output sink - whose result
    /// depends on the order rows arrive in. Most of a file's cost is not that shared state, though:
    /// it is decoding the file's parquet, scoring its rows, sorting them. <see cref="Run{T}"/>
    /// splits the two. <c>produce</c> does the file-local part and may run for several files at
    /// once; <c>consume</c> applies a file's result to the shared state, and is called on the
    /// caller's thread for file 0, 1, 2, ... exactly as the sequential loop would. The output is
    /// therefore the sequential loop's output by construction, rather than by an argument about
    /// whether some merge is associative.</para>
    ///
    /// <para>Lanes take files from one shared counter in order instead of being dealt fixed
    /// buckets up front, so a lane that finishes early takes the next file rather than idling, and
    /// files start in roughly the order they are consumed. At most <c>2 * lanes</c> files are
    /// produced and not yet consumed at any moment, which bounds memory to a small constant number
    /// of files whatever the cohort size.</para>
    ///
    /// <para>A failure is reported for the first file, in file order, whose work failed - the file
    /// the sequential loop would have stopped at - and is rethrown as the original exception
    /// rather than inside an <see cref="AggregateException"/>, so its message reaches the
    /// user. A later file that also failed while produced ahead is not reported: the sequential
    /// loop would never have run it. The unordered <see cref="For"/> has no such loop to
    /// match, and reports every failure - see there.</para>
    ///
    /// <para>If a lane's thread cannot be started - the process is out of threads - the lanes
    /// already started are stopped and waited for before that failure is rethrown, so no lane
    /// outlives the call.</para>
    /// </summary>
    public static class OrderedFileLanes
    {
        [ThreadStatic]
        private static Func<int, Exception> _laneStartFailure;

        /// <summary>
        /// Test seam: when set, consulted on the calling thread before each lane's thread is
        /// started, with the lane number; an exception it returns is thrown in place of starting
        /// that lane, as <see cref="Thread.Start()"/> throws when no thread can be created.
        /// </summary>
        internal static Func<int, Exception> LaneStartFailure
        {
            get { return _laneStartFailure; }
            set { _laneStartFailure = value; }
        }

        /// <summary>
        /// Calls <paramref name="produce"/> for every index in [0, <paramref name="count"/>) on up
        /// to <paramref name="lanes"/> threads, and <paramref name="consume"/> with each result on
        /// the calling thread in strictly ascending index order. With one lane this is exactly
        /// <c>for (i...) consume(i, produce(i))</c> on the calling thread.
        /// </summary>
        public static void Run<T>(int count, int lanes, Func<int, T> produce, Action<int, T> consume)
        {
            if (consume == null)
                throw new ArgumentNullException(nameof(consume));
            RunWhile(count, lanes, produce, (i, result) =>
            {
                consume(i, result);
                return true;
            });
        }

        /// <summary>
        /// <see cref="Run{T}"/> for a consumer that can stop the walk: once <paramref name="consume"/>
        /// returns false no later file is consumed, the lanes take no further file, and this
        /// returns false - where the sequential loop would have broken out. Files already being
        /// produced when it stops are finished and discarded, so <paramref name="produce"/> must
        /// leave no effect a later file's absence would contradict; report through the result,
        /// and let the consumer act on it in order.
        /// </summary>
        public static bool RunWhile<T>(int count, int lanes, Func<int, T> produce, Func<int, T, bool> consume)
        {
            if (produce == null)
                throw new ArgumentNullException(nameof(produce));
            if (consume == null)
                throw new ArgumentNullException(nameof(consume));
            if (count <= 0)
                return true;
            if (lanes <= 1 || count == 1)
            {
                for (int i = 0; i < count; i++)
                {
                    if (!consume(i, produce(i)))
                        return false;
                }
                return true;
            }
            return new Pipeline<T>(count, Math.Min(lanes, count), produce).Drain(consume);
        }

        /// <summary>
        /// <see cref="Run{T}"/> for a caller that pulls: yields <paramref name="produce"/>'s result
        /// for index 0, 1, 2, ... in order while up to <paramref name="lanes"/> threads produce the
        /// files ahead of it, within the same 2 x lanes look-ahead. Each enumeration starts its own
        /// lanes, and disposing the enumerator early - a break, an exception - stops them and waits
        /// for any file still being produced before it returns. With one lane each file is
        /// produced only when it is asked for, exactly like a plain iterator.
        /// </summary>
        public static IEnumerable<T> Enumerate<T>(int count, int lanes, Func<int, T> produce)
        {
            if (produce == null)
                throw new ArgumentNullException(nameof(produce));
            return lanes <= 1 || count <= 1
                ? EnumerateInline(count, produce)
                : EnumerateOnLanes(count, Math.Min(lanes, count), produce);
        }

        /// <summary>
        /// Calls <paramref name="body"/> for every index in [0, <paramref name="count"/>) on up to
        /// <paramref name="lanes"/> threads, for per-file work whose effects are disjoint - each
        /// file writes only its own slot or its own output - so no order needs restoring.
        ///
        /// <para>Files are handed out one at a time, in order, from a shared counter: a lane that
        /// finishes takes the next file. That is the queue a reader of the per-file progress
        /// expects, and unlike the coarse contiguous chunks a <c>Parallel.For</c> over a range
        /// deals out up front, it leaves at most one file's worth of tail however unevenly the
        /// files cost. There is no look-ahead bound - nothing waits to be consumed in order, so a
        /// lane holds only the file it is working on, and one slow file never idles the
        /// others.</para>
        ///
        /// <para>After a failure no lane starts another file, but files already running on other
        /// lanes finish, and may fail too. A single failure is rethrown as the original
        /// exception, stack intact. More than one is thrown as an <see cref="AggregateException"/>
        /// of them all in file order - what the <c>Parallel.For</c> this replaces threw - so a
        /// defect that failed beside a user error is reported as the defect it is, not hidden
        /// behind the user error's message.</para>
        /// </summary>
        public static void For(int count, int lanes, Action<int> body)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));
            if (count <= 0)
                return;
            if (lanes <= 1 || count == 1)
            {
                for (int i = 0; i < count; i++)
                    body(i);
                return;
            }

            var errors = new Exception[count];
            int next = -1;
            using (var stop = new CancellationTokenSource())
            {
                var threads = new Thread[Math.Min(lanes, count)];
                for (int t = 0; t < threads.Length; t++)
                {
                    threads[t] = new Thread(() =>
                    {
                        while (!stop.IsCancellationRequested)
                        {
                            int i = Interlocked.Increment(ref next);
                            if (i >= count)
                                return;
                            try
                            {
                                body(i);
                            }
                            catch (Exception ex)
                            {
                                errors[i] = ex;
                                stop.Cancel();
                                return;
                            }
                        }
                    })
                    {
                        IsBackground = true,
                        Name = @"OspreyFileLane"
                    };
                }
                int started = 0;
                try
                {
                    for (; started < threads.Length; started++)
                        StartLane(threads[started], started);
                }
                catch
                {
                    // No lane takes another file, and none outlives this call.
                    stop.Cancel();
                    JoinLanes(threads, started);
                    throw;
                }
                JoinLanes(threads, started);
            }
            ThrowFailures(errors);
        }

        private static IEnumerable<T> EnumerateInline<T>(int count, Func<int, T> produce)
        {
            for (int i = 0; i < count; i++)
                yield return produce(i);
        }

        private static IEnumerable<T> EnumerateOnLanes<T>(int count, int lanes, Func<int, T> produce)
        {
            // A pipeline per enumeration, so enumerating twice starts fresh lanes rather than
            // reusing ones that have already been stopped.
            foreach (var result in new Pipeline<T>(count, lanes, produce).Pull())
                yield return result;
        }

        private static void StartLane(Thread thread, int lane)
        {
            var failure = _laneStartFailure?.Invoke(lane);
            if (failure != null)
                throw failure;
            thread.Start();
        }

        /// <summary>
        /// Waits for the first <paramref name="started"/> lanes - the ones whose threads were
        /// started; joining a thread never started would throw.
        /// </summary>
        private static void JoinLanes(Thread[] threads, int started)
        {
            for (int t = 0; t < started; t++)
                threads[t].Join();
        }

        /// <summary>
        /// Rethrows the one failure as itself, or several as an <see cref="AggregateException"/>
        /// in file order.
        /// </summary>
        private static void ThrowFailures(Exception[] errors)
        {
            var failures = new List<Exception>();
            foreach (var error in errors)
            {
                if (error != null)
                    failures.Add(error);
            }
            if (failures.Count == 1)
                ExceptionDispatchInfo.Capture(failures[0]).Throw();
            if (failures.Count > 1)
                throw new AggregateException(failures);
        }

        private sealed class Pipeline<T>
        {
            private readonly int _count;
            private readonly Func<int, T> _produce;
            private readonly Thread[] _threads;
            private readonly object _gate = new object();
            private readonly T[] _results;
            private readonly Exception[] _errors;
            private readonly bool[] _ready;
            // One slot per file a lane may hold ahead of the consumer. A lane waits for a slot
            // BEFORE it takes an index, so produced-or-in-flight-but-unconsumed never exceeds the
            // slot count.
            private readonly SemaphoreSlim _slots;
            private readonly CancellationTokenSource _cancel = new CancellationTokenSource();
            private int _next = -1;
            private int _started;

            public Pipeline(int count, int lanes, Func<int, T> produce)
            {
                _count = count;
                _produce = produce;
                _results = new T[count];
                _errors = new Exception[count];
                _ready = new bool[count];
                _slots = new SemaphoreSlim(2 * lanes, int.MaxValue);
                _threads = new Thread[lanes];
                for (int t = 0; t < lanes; t++)
                {
                    _threads[t] = new Thread(Lane)
                    {
                        IsBackground = true,
                        Name = @"OspreyFileLane"
                    };
                }
            }

            public bool Drain(Func<int, T, bool> consume)
            {
                try
                {
                    Start();
                    for (int i = 0; i < _count; i++)
                    {
                        if (!consume(i, Take(i)))
                            return false;
                    }
                    return true;
                }
                finally
                {
                    Stop();
                }
            }

            public IEnumerable<T> Pull()
            {
                try
                {
                    Start();
                    for (int i = 0; i < _count; i++)
                        yield return Take(i);
                }
                finally
                {
                    Stop();
                }
            }

            /// <summary>
            /// Starts the lanes, counting each as it starts, so a failure to start one leaves
            /// <see cref="Stop"/> joining exactly the lanes that are running.
            /// </summary>
            private void Start()
            {
                for (; _started < _threads.Length; _started++)
                    StartLane(_threads[_started], _started);
            }

            /// <summary>
            /// Waits for file <paramref name="i"/>, rethrows its failure if it had one, and frees
            /// its slot - so one more file may be produced while the caller works on this one.
            /// </summary>
            private T Take(int i)
            {
                T result;
                Exception error;
                lock (_gate)
                {
                    while (!_ready[i])
                        Monitor.Wait(_gate);
                    result = _results[i];
                    error = _errors[i];
                    // Released as soon as it is handed on, so a consumed file's result is
                    // collectable while later files are still being produced.
                    _results[i] = default(T);
                }
                if (error != null)
                    ExceptionDispatchInfo.Capture(error).Throw();
                _slots.Release();
                return result;
            }

            /// <summary>
            /// Normal completion and failure alike: wake any lane waiting for a slot, and do not
            /// return while a lane is still running work the caller's state may be about to be
            /// torn down under.
            /// </summary>
            private void Stop()
            {
                _cancel.Cancel();
                JoinLanes(_threads, _started);
                _cancel.Dispose();
                _slots.Dispose();
            }

            private void Lane()
            {
                while (true)
                {
                    try
                    {
                        _slots.Wait(_cancel.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    int i = Interlocked.Increment(ref _next);
                    if (i >= _count)
                        return;
                    T result = default(T);
                    Exception error = null;
                    try
                    {
                        result = _produce(i);
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                    }
                    lock (_gate)
                    {
                        _results[i] = result;
                        _errors[i] = error;
                        _ready[i] = true;
                        Monitor.PulseAll(_gate);
                    }
                    // A failed file ends this lane. The consumer reports it on reaching it, and
                    // every index below it was taken by a lane that will mark it ready, so the
                    // consumer cannot wait on an index nobody holds.
                    if (error != null)
                        return;
                }
            }
        }
    }
}
