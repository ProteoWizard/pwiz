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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.IO;
using pwiz.CarafeSharp.Proteome;

namespace pwiz.CarafeSharp
{
    /// <summary>
    /// Writes a library's chunks of spectra on one dedicated thread, in the order they are
    /// added, so one chunk is written while the next is predicted. Each chunk goes to the TSV
    /// and/or the .blib, and, when DecoyPairs are planned, its precursors and their RefSpectra
    /// ids go to the pairing list. At most <see cref="QUEUE_CAPACITY"/> chunk waits behind the
    /// one being written; <see cref="Add"/> blocks while the queue is full.
    /// <para>
    /// A failure on the writer thread ends the writing, and the adding thread gets the
    /// exception, with its own type and stack trace, from the next <see cref="Add"/>,
    /// <see cref="ThrowIfFailed"/> or <see cref="Finish"/>. <see cref="Dispose"/> without
    /// <see cref="Finish"/> (the adding thread failed) stops the writer after the chunk it is
    /// writing and waits for it, so the TSV and .blib can then be disposed, which discards
    /// their partial files. Neither is ever completed here: the caller completes them only
    /// after <see cref="Finish"/> returns.
    /// </para>
    /// </summary>
    internal sealed class LibraryChunkWriter : IDisposable
    {
        /// <summary>
        /// Chunks waiting behind the one being written. With that one and the one being
        /// predicted, at most three chunks are held at once.
        /// </summary>
        public const int QUEUE_CAPACITY = 1;

        private readonly CarafeLibraryTsvWriter _tsv;
        private readonly BlibLibraryWriter _blib;
        private readonly List<DecoyPairPlanner.Precursor> _pairingPrecursors;
        private readonly Action<int> _beforeWrite;
        private readonly BlockingCollection<List<LibrarySpectrum>> _queue =
            new BlockingCollection<List<LibrarySpectrum>>(new ConcurrentQueue<List<LibrarySpectrum>>(), QUEUE_CAPACITY);
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private readonly Stopwatch _queueWaitClock = new Stopwatch();
        private readonly Thread _thread;
        private ExceptionDispatchInfo _failure;
        private long _writeTicks;
        private int _written;

        /// <param name="tsv">The TSV to write, or null.</param>
        /// <param name="blib">The .blib to write, or null.</param>
        /// <param name="pairingPrecursors">Receives each precursor written to the .blib, for DecoyPairs, or null.</param>
        /// <param name="beforeWrite">A test hook the writer thread calls with each chunk's index before writing it, or null.</param>
        public LibraryChunkWriter(CarafeLibraryTsvWriter tsv, BlibLibraryWriter blib, List<DecoyPairPlanner.Precursor> pairingPrecursors,
            Action<int> beforeWrite = null)
        {
            _tsv = tsv;
            _blib = blib;
            _pairingPrecursors = pairingPrecursors;
            _beforeWrite = beforeWrite;
            _thread = new Thread(WriteChunks) { Name = @"CarafeSharp library writer", IsBackground = true };
            _thread.Start();
        }

        /// <summary>Precursors written so far.</summary>
        public int Written
        {
            get { return Volatile.Read(ref _written); }
        }

        /// <summary>Time the writer thread has spent writing the chunks finished so far.</summary>
        public TimeSpan WriteTime
        {
            get { return TimeSpan.FromTicks(Interlocked.Read(ref _writeTicks)); }
        }

        /// <summary>Time <see cref="Add"/> has spent waiting for room in the queue.</summary>
        public TimeSpan QueueWaitTime
        {
            get { return _queueWaitClock.Elapsed; }
        }

        /// <summary>
        /// Queues a chunk to be written after those added before it, waiting while the queue is
        /// full. Throws the writer thread's exception if writing has failed.
        /// </summary>
        public void Add(List<LibrarySpectrum> spectra)
        {
            ThrowIfFailed();
            _queueWaitClock.Start();
            try
            {
                _queue.Add(spectra, _stop.Token);
            }
            catch (OperationCanceledException)
            {
                // The writer thread failed while this waited for room.
                ThrowIfFailed();
                throw;
            }
            finally
            {
                _queueWaitClock.Stop();
            }
        }

        /// <summary>Throws the writer thread's exception, with its original stack trace, if writing has failed.</summary>
        public void ThrowIfFailed()
        {
            Volatile.Read(ref _failure)?.Throw();
        }

        /// <summary>
        /// Waits until every chunk added has been written, then throws the writer thread's
        /// exception if writing failed. Only after this returns may the TSV and .blib be
        /// completed.
        /// </summary>
        public void Finish()
        {
            _queue.CompleteAdding();
            _thread.Join();
            ThrowIfFailed();
        }

        /// <summary>
        /// Stops the writer thread after the chunk it is writing, unless <see cref="Finish"/>
        /// already waited for it, and waits for it to end, so the TSV and .blib are no longer
        /// in use.
        /// </summary>
        public void Dispose()
        {
            _stop.Cancel();
            _queue.CompleteAdding();
            _thread.Join();
            _stop.Dispose();
            _queue.Dispose();
        }

        private void WriteChunks()
        {
            try
            {
                int chunkIndex = 0;
                foreach (var spectra in _queue.GetConsumingEnumerable(_stop.Token))
                    WriteChunk(chunkIndex++, spectra);
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
                // Stopped by Dispose because the adding thread failed.
            }
            catch (Exception e)
            {
                // Nothing may escape this thread, which would end the process; the adding thread rethrows it.
                Volatile.Write(ref _failure, ExceptionDispatchInfo.Capture(e));
                _stop.Cancel();
            }
        }

        private void WriteChunk(int chunkIndex, List<LibrarySpectrum> spectra)
        {
            _beforeWrite?.Invoke(chunkIndex);
            long start = Stopwatch.GetTimestamp();
            int threads = WriteThreads(_queue.Count, Environment.ProcessorCount);
            if (_tsv != null)
            {
                var rows = new string[spectra.Count];
                Parallel.For(0, spectra.Count, new ParallelOptions { MaxDegreeOfParallelism = threads },
                    i => rows[i] = CarafeLibraryTsvWriter.FormatRows(spectra[i]));
                foreach (string text in rows)
                    _tsv.WriteRows(text);
            }
            if (_blib != null)
            {
                int firstId = _blib.WriteBatch(spectra, threads);
                if (_pairingPrecursors != null)
                {
                    for (int i = 0; i < spectra.Count; i++)
                    {
                        var peptide = spectra[i].Precursor.Peptide;
                        _pairingPrecursors.Add(new DecoyPairPlanner.Precursor(firstId + i, peptide.Sequence,
                            spectra[i].Charge, DecoyPairPlanner.ModKey(peptide.ModNames)));
                    }
                }
            }
            Interlocked.Add(ref _writeTicks, Stopwatch.GetElapsedTime(start).Ticks);
            Interlocked.Add(ref _written, spectra.Count);
        }

        /// <summary>
        /// Threads for formatting and compressing a chunk. While the writer keeps up, no chunk
        /// waits (<paramref name="backlog"/> 0), and a quarter of the processors are enough and
        /// leave the rest to the prediction thread that feeds the GPU. On a shared 16-thread machine
        /// with a GTX 1650, writing on every thread beside prediction slowed MS2 prediction by 23% on
        /// Stellar (26% on Astral), and on a quarter of them by 10%. When a chunk is waiting,
        /// prediction is ahead, and writing uses them all.
        /// </summary>
        internal static int WriteThreads(int backlog, int processorCount)
        {
            return backlog > 0 ? -1 : Math.Max(1, processorCount / 4);
        }
    }
}
