/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Demux
{
    /// <summary>What a demultiplexing pipeline needs to know of one acquired spectrum, without its points.</summary>
    public readonly struct DemuxSpectrumInfo
    {
        public DemuxSpectrumInfo(int msLevel, bool hasWindow, IsolationWindow window, double time)
        {
            MsLevel = msLevel;
            HasWindow = hasWindow;
            Window = window;
            Time = time;
        }

        public int MsLevel { get; }

        /// <summary>Whether the spectrum reports an isolation window (an MS2 spectrum's precursor).</summary>
        public bool HasWindow { get; }

        /// <summary>The isolation window the spectrum reports, if it <see cref="HasWindow"/>.</summary>
        public IsolationWindow Window { get; }

        /// <summary>Scan start time, in minutes.</summary>
        public double Time { get; }

        /// <summary>The isolation window's target m/z; 0 without a window.</summary>
        public double Target
        {
            get { return HasWindow ? Window.Center : 0.0; }
        }
    }

    /// <summary>
    /// An acquisition as the demultiplexing pipeline reads it: its spectra in acquisition order, described by
    /// index, their points read on request.
    /// </summary>
    public interface IDemuxSource
    {
        /// <summary>Spectra in acquisition order.</summary>
        int Count { get; }

        /// <summary>A spectrum's native id (SCIEX: "sample=1 period=1 cycle=N experiment=E").</summary>
        string NativeId(int index);

        /// <summary>A spectrum's MS level, isolation window and time.</summary>
        DemuxSpectrumInfo Describe(int index);

        /// <summary>
        /// A spectrum's points as the file reports them, zeros included; null lists if it has none. The lists stay
        /// valid until <see cref="Release"/> is called for the spectrum. Called from several threads at once when
        /// <see cref="ScanningDemuxOptions.ReadThreads"/> is above 1.
        /// </summary>
        void Read(int index, out IReadOnlyList<double> mz, out IReadOnlyList<double> intensity);

        /// <summary>The points read for a spectrum are no longer needed; the source may keep the rest of it.</summary>
        void Release(int index);

        /// <summary>A spectrum's cycle is no longer needed: the source may drop anything it kept of it.</summary>
        void Forget(int index);
    }

    /// <summary>One solve of a batch, and its size, so the largest start first.</summary>
    public readonly struct DemuxWork
    {
        public DemuxWork(Func<ScanningUnitResult> solve, long size)
        {
            Solve = solve;
            Size = size;
        }

        public Func<ScanningUnitResult> Solve { get; }
        public long Size { get; }
    }

    /// <summary>
    /// A scheme's part of <see cref="DemuxPipeline"/>: its output spectra, which cycles are demultiplexed together,
    /// and for a batch of cycles its solves and its output peaks.
    /// </summary>
    public abstract class DemuxPlan
    {
        /// <summary>Output spectra, in the order they are written.</summary>
        public abstract int OutputCount { get; }

        /// <summary>The last cycle with output.</summary>
        public abstract int LastCycle { get; }

        /// <summary>Cycles demultiplexed together, from a batch's first.</summary>
        public abstract int BatchCycles { get; }

        /// <summary>The cycle whose batch builds an output spectrum's peaks; -1 for one passed through as read.</summary>
        public abstract int CycleOfOutput(int output);

        /// <summary>What the pipeline logs when a batch is written.</summary>
        public abstract string DescribeBatch(int firstCycle, int lastCycle);

        /// <summary>Before the batch from this cycle is built: forgets the input no batch from it on needs.</summary>
        public abstract void Forget(int firstCycle);

        /// <summary>
        /// Reads a batch's input and makes its solves, on the pipeline's thread; they run on the solve threads while
        /// it goes on. <paramref name="readSeconds"/> is the time spent reading.
        /// </summary>
        public abstract IReadOnlyList<DemuxWork> MakeWork(int firstCycle, int lastCycle, out double readSeconds);

        /// <summary>A solved batch's output peaks, in ions, by output index; the results in the order of the solves.</summary>
        public abstract void LayOut(int firstCycle, int lastCycle, ScanningUnitResult[] results,
            IDictionary<int, (double[] Mz, double[] Ions)> peaks);
    }

    /// <summary>
    /// Demultiplexes an acquisition a batch of cycles at a time, as its <see cref="DemuxPlan"/> says, and serves the
    /// output spectra's peaks by index.
    /// </summary>
    /// <remarks>
    /// Each batch is read and queued before the one before it is waited for, so the solve threads go on to it while
    /// that batch is laid out and written; a batch's solves start largest first. The caller asks for output spectra
    /// in index order, so only the current batch and the next one's input are resident. A solve's result depends only
    /// on its own input, so the output does not depend on the thread count or the order the solves run in.
    /// </remarks>
    public class DemuxPipeline
    {
        private readonly DemuxPlan _plan;
        private readonly ScanningDemuxOptions _options;
        private readonly TextWriter _log;
        private readonly Dictionary<int, (double[] Mz, double[] Ions)> _built = new Dictionary<int, (double[], double[])>();
        private readonly object _lock = new object();
        private DemuxBatch _nextBatch;                        // queued while the batch before it is laid out and written
        private TaskScheduler _solveScheduler;

        public DemuxPipeline(DemuxPlan plan, ScanningDemuxOptions options, TextWriter log)
        {
            _plan = plan;
            _options = options;
            _log = log;
        }

        public long Channels { get; private set; }
        public long ChannelsSolved { get; private set; }
        public double IonsIn { get; private set; }
        public double IonsPassedThrough { get; private set; }

        /// <summary>Output spectra, in the order they are written.</summary>
        public int OutputCount
        {
            get { return _plan.OutputCount; }
        }

        /// <summary>An output spectrum's peaks, in ions, demultiplexing its batch if needed.</summary>
        public (double[] Mz, double[] Ions) Peaks(int index)
        {
            lock (_lock)
            {
                if (_built.TryGetValue(index, out var cached))
                    return cached;
                int cycle = _plan.CycleOfOutput(index);
                foreach (int old in _built.Keys.Where(o => _plan.CycleOfOutput(o) < cycle).ToList())
                    _built.Remove(old);
                int batchLast = Math.Min(_plan.LastCycle, cycle + _plan.BatchCycles - 1);
                Build(cycle, batchLast);
                _log.WriteLine(_plan.DescribeBatch(cycle, batchLast));
                return _built[index];
            }
        }

        private void Build(int firstCycle, int lastCycle)
        {
            _plan.Forget(firstCycle);
            var batch = _nextBatch != null && _nextBatch.FirstCycle == firstCycle && _nextBatch.LastCycle == lastCycle
                ? _nextBatch
                : QueueBatch(firstCycle, lastCycle);
            _nextBatch = null;
            // The next batch is read and queued before this one is waited for: the solve threads move on to it as
            // this batch's solves run out, and solve it while this batch is laid out and written.
            if (lastCycle < _plan.LastCycle)
                _nextBatch = QueueBatch(lastCycle + 1, Math.Min(_plan.LastCycle, lastCycle + _plan.BatchCycles));
            var wait = Stopwatch.StartNew();
            try
            {
                Task.WaitAll(batch.Solves);
            }
            catch (AggregateException e)
            {
                throw new AggregateException(@"Exception while demultiplexing sweeps", e.InnerExceptions);
            }
            double waitSeconds = wait.Elapsed.TotalSeconds;
            var layoutClock = Stopwatch.StartNew();
            foreach (var result in batch.Results)
            {
                Channels += result.Channels;
                ChannelsSolved += result.ChannelsSolved;
                IonsIn += result.IonsIn;
                IonsPassedThrough += result.IonsPassedThrough;
            }
            _plan.LayOut(firstCycle, lastCycle, batch.Results, _built);
            if (batch.Results.Length > 0)
            {
                _log.WriteLine(@"  read {0:F1} s, units {1:F1} s, solve {2:F1} s (waited {3:F1} s), layout {4:F1} s ({5} units)",
                    batch.ReadSeconds, batch.UnitSeconds, batch.SolveSeconds, waitSeconds, layoutClock.Elapsed.TotalSeconds,
                    batch.Results.Length);
            }
        }

        /// <summary>
        /// Reads a batch's input, makes its solves and queues them on the solve threads, largest first: a batch's
        /// slowest solves then start first. Returns without waiting for them.
        /// </summary>
        private DemuxBatch QueueBatch(int firstCycle, int lastCycle)
        {
            var clock = Stopwatch.StartNew();
            var work = _plan.MakeWork(firstCycle, lastCycle, out double readSeconds);
            double unitSeconds = clock.Elapsed.TotalSeconds - readSeconds;
            var batch = new DemuxBatch(firstCycle, lastCycle, work.Count, readSeconds, unitSeconds);
            var order = Enumerable.Range(0, work.Count).OrderByDescending(i => work[i].Size).ToArray();
            for (int n = 0; n < order.Length; n++)
            {
                int i = order[n];
                batch.Solves[n] = Task.Factory.StartNew(() =>
                {
                    batch.Results[i] = work[i].Solve();
                    batch.Solved();
                }, CancellationToken.None, TaskCreationOptions.None, SolveScheduler);
            }
            return batch;
        }

        /// <summary>
        /// At most <see cref="ScanningDemuxOptions.Threads"/> solves at once, across batches, in the order they were
        /// queued.
        /// </summary>
        private TaskScheduler SolveScheduler
        {
            get
            {
                return _solveScheduler ??= new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default,
                    Math.Max(1, _options.Threads)).ConcurrentScheduler;
            }
        }

        /// <summary>A batch of cycles queued on the solve threads: its solves' results and its timings.</summary>
        private sealed class DemuxBatch
        {
            private readonly Stopwatch _clock = Stopwatch.StartNew();
            private int _remaining;

            public DemuxBatch(int firstCycle, int lastCycle, int solves, double readSeconds, double unitSeconds)
            {
                FirstCycle = firstCycle;
                LastCycle = lastCycle;
                ReadSeconds = readSeconds;
                UnitSeconds = unitSeconds;
                Results = new ScanningUnitResult[solves];
                Solves = new Task[solves];
                _remaining = solves;
            }

            public int FirstCycle { get; }
            public int LastCycle { get; }
            public double ReadSeconds { get; }
            public double UnitSeconds { get; }
            public ScanningUnitResult[] Results { get; }
            public Task[] Solves { get; }

            /// <summary>From queueing the batch to its last solve done.</summary>
            public double SolveSeconds { get; private set; }

            /// <summary>Called as each solve is done; the last records the batch's solve time.</summary>
            public void Solved()
            {
                if (Interlocked.Decrement(ref _remaining) == 0)
                    SolveSeconds = _clock.Elapsed.TotalSeconds;
            }
        }
    }
}
