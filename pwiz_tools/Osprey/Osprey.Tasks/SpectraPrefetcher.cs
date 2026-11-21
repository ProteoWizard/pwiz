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
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using Pwiz.Analysis;
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// Reads a per-file stage's input files that have no spectra cache in full, in input order,
    /// ahead of the lanes that score them, so a file's read overlaps the scoring of the files before
    /// it. A forward read completes no isolation window until it reaches the end of the file, so the
    /// overlap is across files, never within one. In per-file scoring it also writes each file's
    /// <c>.spectra.bin</c> from the spectra it read, before handing them over, so scoring never
    /// reads the cache back (<c>--spectra-cache off</c> writes none).
    ///
    /// <para>Files are read by <see cref="OspreyEnvironment.ReadThreads"/> threads at
    /// above-normal priority: one decode thread competing with every scoring thread fell behind
    /// four scoring lanes (77-120 s per 6 GB Astral file, against 37-41 s on an idle machine).</para>
    ///
    /// <para>Memory is bounded by a byte budget over every file's resident peaks: (lanes +
    /// <see cref="OspreyEnvironment.ReadAhead"/>) times the first file's, which is unknown
    /// until that file is read, so the first files read unbudgeted. A read holds back, spectrum by
    /// spectrum, while the next spectrum would exceed the budget and memory is held that will come
    /// back without it: by files a lane has taken, which scoring frees, or, for any file but the
    /// oldest still being read, by any other file. The oldest read therefore never waits on a read
    /// behind it, and no read ever waits on itself. Memory comes back as scoring takes each window for the last time
    /// (<see cref="SpectraWindowIndex.ReleaseWindowsAsLoaded"/>) and, for whatever remains, when
    /// the lane finishes the file (<see cref="Release"/>).</para>
    ///
    /// <para>The lanes hand out files in input order (<see cref="OrderedFileLanes"/>), the order
    /// this reads them in, so a lane never waits on a file the reader is not yet heading for. A
    /// file whose lane needs no spectra (its outputs are current) is still read; the cost is the
    /// read, not correctness.</para>
    ///
    /// <para>The reader runs on a <see cref="QueueWorker{TItem}"/>, whose producer threads read the
    /// files; each publishes its own finished file to the lane waiting for it.</para>
    /// </summary>
    internal sealed class SpectraPrefetcher : IDisposable
    {
        private readonly List<string> _inputs;
        private readonly double _budgetFiles;
        private readonly PipelineContext _ctx;
        private readonly SpectrumListDemux.Params _demultiplex;
        private readonly bool _writeCache;
        private readonly object _lock = new object();
        private readonly Dictionary<string, PrefetchedFile> _files = new Dictionary<string, PrefetchedFile>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _skipped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private QueueWorker<PrefetchedFile> _worker;
        private int _next;
        private readonly SortedSet<int> _reading = new SortedSet<int>();
        private long _takenBytes;
        private bool _readerDone;
        private bool _disposed;
        private long _residentBytes;
        private long _peakResidentBytes;
        private long _budgetBytes;

        /// <param name="inputs">The stage's input files, in the order its lanes take them.</param>
        /// <param name="lanes">How many files the stage works on at once.</param>
        /// <param name="demultiplex">The demultiplexing settings the stage's own read would use, so
        /// a run is read here exactly as it would be there.</param>
        /// <param name="writeCache">Write each file's <c>.spectra.bin</c> from the spectra read,
        /// before handing them over, so a later stage can read the cache.</param>
        /// <param name="ctx">For the read-ahead log lines.</param>
        public SpectraPrefetcher(IEnumerable<string> inputs, int lanes, SpectrumListDemux.Params demultiplex,
            bool writeCache, PipelineContext ctx)
        {
            _inputs = new List<string>(inputs);
            _demultiplex = demultiplex;
            _writeCache = writeCache;
            _budgetFiles = Math.Max(1, lanes) + Math.Max(0, OspreyEnvironment.ReadAhead);
            _ctx = ctx;
        }

        /// <summary>The most peak memory every file held at once, in bytes.</summary>
        public long PeakResidentBytes
        {
            get { lock (_lock) { return _peakResidentBytes; } }
        }

        /// <summary>
        /// The spectra of <paramref name="inputFile"/>, waiting for the reader to finish them. The
        /// reader starts with the first call, so a stage that reads nothing reads no file. Null
        /// when the file is not one this reads, or was released unread; the caller then reads it
        /// itself. Rethrows the read's own failure.
        /// </summary>
        public SpectraWindowIndex Take(string inputFile, bool loadMs1, out int unsortedCount)
        {
            unsortedCount = 0;
            if (!_inputs.Contains(inputFile))
                return null;
            var swWait = Stopwatch.StartNew();
            PrefetchedFile file;
            lock (_lock)
            {
                if (_worker == null && !_disposed)
                {
                    _worker = new QueueWorker<PrefetchedFile>(ReadNext, Publish);
                    _worker.RunAsync(1, @"Spectra handoff", Math.Max(1, OspreyEnvironment.ReadThreads), @"Spectra read");
                }
                while (true)
                {
                    if (_files.TryGetValue(inputFile, out file) && file.Read)
                        break;
                    if (_disposed || _skipped.Contains(inputFile) || (_readerDone && file == null))
                        return null;
                    Monitor.Wait(_lock);
                }
                // A second reader of the same file in the same stage reads it itself.
                if (file.Taken || file.Abandoned)
                    return null;
                file.Taken = true;
                _takenBytes += file.Bytes - file.ReleasedBytes;
            }
            swWait.Stop();
            if (file.Error != null)
                ExceptionDispatchInfo.Capture(file.Error).Throw();

            var result = file.Result;
            file.Result = null;
            // A multiplexed run goes through the stage's own read, which keeps its demultiplexed
            // cache.
            if (result.DemultiplexScheme != null)
            {
                Release(inputFile);
                return null;
            }
            unsortedCount = result.UnsortedSpectrumCount;
            var index = SpectraWindowIndex.FromSpectra(result.Ms2Spectra, result.Ms1Spectra, loadMs1);
            if (!loadMs1)
            {
                // The index keeps no MS1, so its bytes are free as of now.
                long ms1Bytes = 0;
                foreach (var s in result.Ms1Spectra)
                    ms1Bytes += SpectrumFileReader.PeakBytes(s.Count);
                ReleaseBytes(file, ms1Bytes);
            }
            index.PeakBytesReleased = bytes => ReleaseBytes(file, bytes);
            _ctx.LogInfo(LogTag.TIMING, @"Read ahead {0}: read in {1:F1}s, waited {2:F1}s; {3:F2} GB resident of {4:F2} GB budget; cache write {5:F1}s",
                Path.GetFileName(inputFile), file.ReadSeconds, swWait.Elapsed.TotalSeconds,
                ToGb(Volatile.Read(ref _residentBytes)), ToGb(Volatile.Read(ref _budgetBytes)), file.WriteSeconds);
            return index;
        }

        /// <summary>
        /// Ends this stage's hold on <paramref name="inputFile"/>'s spectra, whether or not it took
        /// them: their remaining bytes go back to the budget, and a file not yet read is skipped.
        /// </summary>
        public void Release(string inputFile)
        {
            lock (_lock)
            {
                if (_files.TryGetValue(inputFile, out var file))
                {
                    // A file still being read stops at its next spectrum (see Reserve).
                    file.Abandoned = true;
                    if (file.Taken)
                        _takenBytes -= file.Bytes - file.ReleasedBytes;
                    _residentBytes -= file.Bytes - file.ReleasedBytes;
                    file.ReleasedBytes = file.Bytes;
                    file.Result = null;
                }
                else
                {
                    _skipped.Add(inputFile);
                }
                Monitor.PulseAll(_lock);
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _disposed = true;
                Monitor.PulseAll(_lock);
            }
            _worker?.Dispose();
            _ctx.LogInfo(LogTag.TIMING, @"Read ahead: peak {0:F2} GB of spectra resident, budget {1:F2} GB",
                ToGb(PeakResidentBytes), ToGb(_budgetBytes));
        }

        // Producer: read the next file not skipped, in full. Null ends the reading.
        private PrefetchedFile ReadNext(int threadIndex)
        {
            Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
            PrefetchedFile file;
            lock (_lock)
            {
                while (_next < _inputs.Count && _skipped.Contains(_inputs[_next]))
                    _next++;
                if (_disposed || _next >= _inputs.Count)
                {
                    _readerDone = true;
                    Monitor.PulseAll(_lock);
                    return null;
                }
                file = new PrefetchedFile(_inputs[_next], _next);
                _next++;
                _files[file.Path] = file;
                _reading.Add(file.Order);
            }
            var sw = Stopwatch.StartNew();
            try
            {
                file.Result = SpectrumFileReader.LoadAllSpectra(file.Path, _demultiplex, bytes => Reserve(file, bytes), false);
                if (_writeCache && file.Result.DemultiplexScheme == null)
                {
                    double readSeconds = sw.Elapsed.TotalSeconds;
                    SpectraCache.SaveSpectraCache(SpectraCache.GetCachePath(file.Path), file.Result.Ms2Spectra,
                        file.Result.Ms1Spectra, file.Path, null, false);
                    file.WriteSeconds = sw.Elapsed.TotalSeconds - readSeconds;
                }
            }
            catch (AbandonedReadException)
            {
                file.Result = null;
            }
            catch (OperationCanceledException)
            {
                file.Result = null;
                lock (_lock)
                {
                    _reading.Remove(file.Order);
                    _readerDone = true;
                    Monitor.PulseAll(_lock);
                }
                return null;
            }
            catch (Exception ex)
            {
                file.Error = ex;
                file.Result = null;
            }
            file.ReadSeconds = sw.Elapsed.TotalSeconds;
            lock (_lock)
            {
                _reading.Remove(file.Order);
                // Published here, not by the worker's consumer: with more than one producer the
                // worker stops its consumer when the FIRST producer runs out of files, which would
                // strand a file another producer is still reading.
                file.Read = true;
                Monitor.PulseAll(_lock);
                // The first file read sets the budget.
                if (_budgetBytes == 0 && file.Error == null)
                    _budgetBytes = (long)(_budgetFiles * file.Bytes);
                // Released while it was being read: nothing will take it.
                if (file.Abandoned)
                    file.Result = null;
            }
            return file;
        }

        // Consumer: the reader has already published the file (see ReadNext).
        private static void Publish(PrefetchedFile file, int threadIndex)
        {
        }

        // Called by the reader before it reads the next spectrum: hold back while that would
        // exceed the budget and another file still holds memory.
        private void Reserve(PrefetchedFile file, long bytes)
        {
            lock (_lock)
            {
                while (!_disposed && !file.Abandoned && _budgetBytes > 0 && _residentBytes + bytes > _budgetBytes &&
                       (_takenBytes > 0 || (_reading.Min != file.Order && _residentBytes - (file.Bytes - file.ReleasedBytes) > 0)))
                {
                    Monitor.Wait(_lock);
                }
                if (_disposed)
                    throw new OperationCanceledException();
                if (file.Abandoned)
                    throw new AbandonedReadException();
                file.Bytes += bytes;
                _residentBytes += bytes;
                _peakResidentBytes = Math.Max(_peakResidentBytes, _residentBytes);
            }
        }

        private void ReleaseBytes(PrefetchedFile file, long bytes)
        {
            lock (_lock)
            {
                bytes = Math.Max(0, Math.Min(bytes, file.Bytes - file.ReleasedBytes));
                file.ReleasedBytes += bytes;
                if (file.Taken)
                    _takenBytes -= bytes;
                _residentBytes -= bytes;
                Monitor.PulseAll(_lock);
            }
        }

        private static double ToGb(long bytes)
        {
            return bytes / 1024.0 / 1024.0 / 1024.0;
        }

        private sealed class PrefetchedFile
        {
            public PrefetchedFile(string path, int order)
            {
                Path = path;
                Order = order;
            }

            public string Path { get; }
            public int Order { get; }
            public SpectrumFileResult Result { get; set; }
            public Exception Error { get; set; }
            public double ReadSeconds { get; set; }
            public double WriteSeconds { get; set; }
            // Guarded by the prefetcher's lock.
            public long Bytes { get; set; }
            public long ReleasedBytes { get; set; }
            public bool Read { get; set; }
            public bool Taken { get; set; }
            public bool Abandoned { get; set; }
        }

        private sealed class AbandonedReadException : Exception
        {
        }
    }
}
