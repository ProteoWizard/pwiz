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
using System.IO;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{

    /// <summary>
    /// This class stores chromatogram intensity (and optional time) values for one transition.
    /// Memory is allocated in linked lists of blocks, which are written to disk "spill files" to
    /// reduce memory load (which makes everything slow, and risks out of memory exceptions).
    /// </summary>
    public sealed class ChromCollector
    {
        public int StatusId { get; private set; }
        private BlockedList<float> Intensities { get; set; }
        private SortedBlockedList<float> Times { get; set; }
        private BlockedList<float> MassErrors { get; set; }
        private BlockedList<int> Scans { get; set; }
        // True when this collector owns its own time array (single-time mode).
        // Distinct from "Times != null", which also becomes true after SetTimes
        // attaches a shared time list in grouped/shared mode.
        private readonly bool _ownsTimes;

        public ChromCollector(int statusId, bool hasTimes, bool hasMassErrors)
        {
            StatusId = statusId;
            _ownsTimes = hasTimes;
            Intensities = new BlockedList<float>();
            if (hasTimes)
                Times = new SortedBlockedList<float>();
            if (hasMassErrors)
                MassErrors = new BlockedList<float>();
        }

        public bool IsSetTimes { get { return Times != null; } }

        /// <summary>
        /// The chromatogram index whose spill file the (possibly shared) Times list was
        /// written to, or null if the Times list has not spilled to disk. Used to verify that
        /// this chromatogram is being released from the spill file its shared times live in.
        /// </summary>
        public int? TimesSpillChromatogramIndex
        {
            get { return Times != null && Times.HasSpilledToDisk ? Times.SpillChromatogramIndex : (int?) null; }
        }

        /// <summary>
        /// The chromatogram index whose spill file the (possibly shared) Scans list was
        /// written to, or null if the Scans list has not spilled to disk.
        /// </summary>
        public int? ScansSpillChromatogramIndex
        {
            get { return Scans != null && Scans.HasSpilledToDisk ? Scans.SpillChromatogramIndex : (int?) null; }
        }

        // DIAGNOSTIC (issue #4287): spill indexes for the product ion's own lists.
        public int? IntensitiesSpillChromatogramIndex
        {
            get { return Intensities != null && Intensities.HasSpilledToDisk ? Intensities.SpillChromatogramIndex : (int?) null; }
        }

        public int? MassErrorsSpillChromatogramIndex
        {
            get { return MassErrors != null && MassErrors.HasSpilledToDisk ? MassErrors.SpillChromatogramIndex : (int?) null; }
        }

        // DIAGNOSTIC (issue #4287): describe each list's spill state for logging.
        public string DescribeSpillState()
        {
            return string.Format(@"Times[{0}] Intensities[{1}] MassErrors[{2}] Scans[{3}]",
                DescribeList(Times), DescribeList(Intensities), DescribeList(MassErrors), DescribeList(Scans));
        }

        private static string DescribeList<TData>(BlockedList<TData> list)
        {
            if (list == null)
                return @"null";
            return string.Format(@"count={0},onDisk={1},spillIdx={2}",
                list.Count, list.HasSpilledToDisk, list.SpillChromatogramIndex);
        }

        /// <summary>
        /// Set a shared reference to a list of Times that is allocated independently.
        /// </summary>
        public void SetTimes(SortedBlockedList<float> times)
        {
            Times = times;
        }

        /// <summary>
        /// Set optional list of scans.
        /// </summary>
        public void SetScans(BlockedList<int> scans)
        {
            Scans = scans;
        }

        /// <summary>
        /// Add intensity and mass error (if needed) to the given chromatogram.
        /// When this collector owns its time array (single-time mode), the
        /// caller is expected to have called AddTime first; if it didn't,
        /// silently ignore so a "fill missing scan with zero" call from a
        /// caller that doesn't know about single-time mode can't desync the
        /// times and intensities arrays.
        /// </summary>
        public void AddPoint(int chromatogramIndex, float intensity, float? massError, BlockWriter writer)
        {
            if (_ownsTimes && Intensities.Count >= Times.Count)
                return;
            if (MassErrors != null)
                // ReSharper disable once PossibleInvalidOperationException
                MassErrors.Add(chromatogramIndex, massError.Value, writer); // If massError is required, this won't be null (and if it is, we want to hear about it)
            Intensities.Add(chromatogramIndex, intensity, writer);
        }

        /// <summary>
        /// Fill a number of intensity and mass error values for the given chromatogram with zeroes.
        /// In single-time mode this collector owns its own time array and back-filling
        /// only intensities would desync the arrays, so the operation is ignored — the
        /// caller is responsible for keeping times and intensities aligned via AddTime.
        /// </summary>
        public void FillZeroes(int chromatogramIndex, int count, BlockWriter writer)
        {
            if (_ownsTimes)
                return;
            if (MassErrors != null)
                MassErrors.FillZeroes(chromatogramIndex, count, writer);
            Intensities.FillZeroes(chromatogramIndex, count, writer);
        }

        /// <summary>
        /// Add a time value to the given chromatogram.
        /// </summary>
        public void AddTime(int chromatogramIndex, float time, BlockWriter writer)
        {
            Times.Add(chromatogramIndex, time, writer);
        }

        public int Count { get { return Intensities.Count; } }

        public int? MassErrorsCount { get { return MassErrors == null ? (int?)null : MassErrors.Count; } }

        // DIAGNOSTIC (issue #4287): call ToArray but annotate the exception with the list's role.
        private static TData[] ToArrayDiag<TData>(string role, BlockedList<TData> list, byte[] bytesFromDisk)
        {
            try
            {
                return list.ToArray(bytesFromDisk);
            }
            catch (Exception e)
            {
                throw new InvalidDataException(role + @": " + e.Message, e);
            }
        }

        /// <summary>
        /// Get a chromatogram with properly sorted time values.
        /// </summary>
        public void ReleaseChromatogram(byte[] bytesFromDisk, out TimeIntensities timeIntensities)
        {
            // DIAGNOSTIC (issue #4287): annotate which list fails so we know whether the corrupt
            // back-link chain is in a shared list (Times/Scans) or a per-product list (Intensities/MassErrors).
            var times = ToArrayDiag(@"Times", Times, bytesFromDisk);
            var intensities = ToArrayDiag(@"Intensities", Intensities, bytesFromDisk);
            var massErrors = MassErrors != null
                ? ToArrayDiag(@"MassErrors", MassErrors, bytesFromDisk)
                : null;
            var scanIds = Scans != null
                ? ToArrayDiag(@"Scans", Scans, bytesFromDisk)
                : null;

            // Filter out NaN intensities from narrow scan window coverage.
            // NaN marks time points where the target's product m/z was outside the
            // spectrum's scan window and should not contribute to the chromatogram.
            int nanCount = 0;
            for (int i = 0; i < intensities.Length; i++)
            {
                if (float.IsNaN(intensities[i]))
                    nanCount++;
            }
            if (nanCount > 0)
            {
                int validCount = intensities.Length - nanCount;
                var filteredTimes = new float[validCount];
                var filteredIntensities = new float[validCount];
                var filteredMassErrors = massErrors != null ? new float[validCount] : null;
                var filteredScanIds = scanIds != null ? new int[validCount] : null;
                int j = 0;
                for (int i = 0; i < intensities.Length; i++)
                {
                    if (!float.IsNaN(intensities[i]))
                    {
                        filteredTimes[j] = times[i];
                        filteredIntensities[j] = intensities[i];
                        if (filteredMassErrors != null)
                            filteredMassErrors[j] = massErrors[i];
                        if (filteredScanIds != null)
                            filteredScanIds[j] = scanIds[i];
                        j++;
                    }
                }
                times = filteredTimes;
                intensities = filteredIntensities;
                massErrors = filteredMassErrors;
                scanIds = filteredScanIds;
            }

            // Make sure times and intensities match in length.
            if (times.Length != intensities.Length)
            {
                throw new InvalidDataException(
                    string.Format(ResultsResources.ChromCollected_ChromCollected_Times__0__and_intensities__1__disagree_in_point_count,
                    times.Length, intensities.Length));
            }
            if (massErrors != null && massErrors.Length != intensities.Length)
            {
                throw new InvalidDataException(
                    string.Format(ResultsResources.ChromCollector_ReleaseChromatogram_Intensities___0___and_mass_errors___1___disagree_in_point_count_,
                    intensities.Length, massErrors.Length));
            }
            timeIntensities = new TimeIntensities(times, intensities, massErrors, scanIds);
            // Release memory.
            Times = null;
            Intensities = null;
            MassErrors = null;
            Scans = null;
        }
    }

    /// <summary>
    /// Generic interface for BlockedList templates for different data types.
    /// </summary>
    public interface IBlockedList
    {
        void WriteBlock(Stream fileStream);
    }

    /// <summary>
    /// A list of data values stored in small arrays that are linked together in memory (and possibly on disk).
    /// </summary>
    /// <typeparam name="TData"></typeparam>
    public class BlockedList<TData> : IBlockedList
    {
        // Different block sizes could be used for 32- and 64-bit OS.
        private const int BLOCK_SIZE_FOR_64_BIT = 16;
        private const int BLOCK_SIZE_FOR_32_BIT = 16;

        protected Block _block;
        protected int _blockIndex;
        private readonly int _blockSize;
        private int _blocksInMemory;
        private int _blocksOnDisk;
        private int _filePosition;
        // The chromatogram index that selects the spill file this list's blocks were
        // written to. A list is always spilled to a single spill file (chosen by
        // chromatogram index), so this stays constant once set. -1 until the first block
        // spills. Used to verify at read time that we are reconstructing this list from
        // the same spill file its data was written to (see issue #4287).
        private int _spillChromatogramIndex = -1;

        public BlockedList()
        {
            _blockSize = Environment.Is64BitProcess ? BLOCK_SIZE_FOR_64_BIT : BLOCK_SIZE_FOR_32_BIT;
        }

        /// <summary>
        /// The chromatogram index whose spill file this list's on-disk blocks live in
        /// (only meaningful when <see cref="HasSpilledToDisk"/> is true).
        /// </summary>
        public int SpillChromatogramIndex { get { return _spillChromatogramIndex; } }

        /// <summary>
        /// True if any of this list's blocks have been written to a spill file on disk.
        /// </summary>
        public bool HasSpilledToDisk { get { return _blocksOnDisk > 0; } }

        /// <summary>
        /// Remember the chromatogram index that selects the spill file this list is being
        /// written to. Every block of a given list must go to the same spill file, so a
        /// change here means the list is being split across spill files, which is itself a bug.
        /// </summary>
        private void RecordSpillChromatogramIndex(int chromatogramIndex)
        {
            if (_spillChromatogramIndex == -1)
                _spillChromatogramIndex = chromatogramIndex;
            else if (_spillChromatogramIndex != chromatogramIndex)
                throw new InvalidDataException(string.Format(
                    @"A spilled data list was written under two different chromatogram indexes ({0} and {1}), which would split it across spill files.",
                    _spillChromatogramIndex, chromatogramIndex));
        }

        /// <summary>
        /// A Block is just a fixed-size array of data and a link to a previous Block.
        /// </summary>
        protected class Block
        {
            public readonly Block _previousBlock;
            public readonly TData[] _data;

            public Block(TData[] data, Block previousBlock)
            {
                _data = data;
                _previousBlock = previousBlock;
            }
        }

        private void NewBlock()
        {
            _block = new Block(new TData[_blockSize], _block);
            _blocksInMemory++;
        }

        /// <summary>
        /// Add a data element to the list for a given chromatogram, and write the block
        /// to disk if it is complete.
        /// </summary>
        public void Add(int chromatogramIndex, TData data, BlockWriter writer)
        {
            // Spill data to disk at block boundaries, if necessary.
            if (_blockIndex == 0)
            {
                if (_blocksInMemory == 0 || writer == null)
                    NewBlock();
                else
                {
                    RecordSpillChromatogramIndex(chromatogramIndex);
                    writer.WriteBlock(chromatogramIndex, this);
                }
            }

            // Store data.
            _block._data[_blockIndex] = data;

            // Wrap index to next block.
            if (++_blockIndex == _blockSize)
                _blockIndex = 0;
        }

        /// <summary>
        /// Add a data element to the list, no disk write.
        /// </summary>
        public void AddShared(TData data)
        {
            // Create a new block, if necessary.
            if (_blockIndex == 0)
                NewBlock();

            // Store data.
            _block._data[_blockIndex] = data;

            // Wrap index to next block.
            if (++_blockIndex == _blockSize)
                _blockIndex = 0;
        }

        /// <summary>
        /// Add a specified number of zero intensities to the given chromatogram, possibly
        /// writing finished blocks to disk.
        /// </summary>
        public void FillZeroes(int chromatogramIndex, int count, BlockWriter writer)
        {
            if (count < 1)
                return;

            // Fill remainder of current block.
            if (_blockIndex > 0)
            {
                while (count > 0 && _blockIndex < _blockSize)
                {
                    _block._data[_blockIndex++] = default(TData);
                    count--;
                }
                if (_blockIndex == _blockSize)
                    _blockIndex = 0;
                if (count == 0)
                    return;
            }

            if (writer != null)
            {
                if (_block == null)
                {
                    NewBlock();
                }
                else
                {
                    // Clear out re-used block.
                    for (int i = 0; i < _blockSize; i++)
                        _block._data[i] = default(TData);
                }

                // Write zeroed blocks to disk.
                if (count >= _blockSize)
                    RecordSpillChromatogramIndex(chromatogramIndex);
                while (count >= _blockSize)
                {
                    writer.WriteBlock(chromatogramIndex, this);
                    count -= _blockSize;
                }
            }
            else
            {
                // Create new pre-zeroed blocks.
                while (count >= _blockSize)
                {
                    NewBlock();
                    count -= _blockSize;
                }
            }

            _blockIndex = count;
        }

        /// <summary>
        /// Write finished block to disk.
        /// </summary>
        public void WriteBlock(Stream fileStream)
        {
            Assume.IsTrue(_blocksInMemory == 1);
            WriteData(_block, fileStream);
            _blocksOnDisk++;
        }

        private void WriteData(Block block, Stream fileStream)
        {
            // DIAGNOSTIC (issue #4287): an append-only writer should always write at the end of the
            // stream. If Position is before Length, the stream was seeked backward (e.g. by a
            // concurrent release reading the whole file) and this write will overwrite an earlier
            // block, corrupting back links.
            if (fileStream.Position < fileStream.Length)
                Console.Error.WriteLine(
                    @"[#4287] WRITE-INTO-MIDDLE pos={0} len={1} thread={2} spillIdx={3}",
                    fileStream.Position, fileStream.Length,
                    System.Threading.Thread.CurrentThread.ManagedThreadId, _spillChromatogramIndex);

            // Create back link to previous spilled block.
            var lastFilePosition = _filePosition;
            _filePosition = (int) fileStream.Position;
            PrimitiveArrays.WriteOneValue(fileStream, lastFilePosition);
            // Tag the block with the chromatogram index that selects its spill file, so that
            // at read time we can detect having followed a back link into the wrong spill file.
            PrimitiveArrays.WriteOneValue(fileStream, _spillChromatogramIndex);

            PrimitiveArrays.Write(fileStream, block._data);
        }

        /// <summary>
        /// Return count of all data items (in memory and on disk).
        /// </summary>
        public int Count
        {
            get
            {
                if (_blockIndex < 0)
                    return _block._data.Length;
                int count = (_blocksOnDisk + _blocksInMemory)*_blockSize;
                if (_blockIndex > 0)
                    count -= _blockSize - _blockIndex;
                return count;
            }
        }

        /// <summary>
        /// Reconstitute data from memory and disk.
        /// </summary>
        public TData[] ToArray(byte[] bytes)
        {
            // Return final cached array.
            if (_blockIndex < 0)
                return _block._data;

            // Allocate final array.
            int dest = Count;
            var array = new TData[dest];
            if (dest == 0)
                return array;
            int length = (_blockIndex > 0) ? _blockIndex : _blockSize;

            // Copy data from memory blocks.
            while (_block != null)
            {
                dest -= length;
                Array.Copy(_block._data, 0, array, dest, length);
                _block = _block._previousBlock;
                length = _blockSize;
            }

            Assume.IsTrue(bytes != null || _blocksOnDisk == 0);

            // Copy data that was spilled.
            while (_blocksOnDisk > 0)
            {
                dest -= _blockSize;
                int blockStart = _filePosition;
                int source = _filePosition;
                // ReSharper disable once AssignNullToNotNullAttribute
                _filePosition = BitConverter.ToInt32(bytes, source);
                source += sizeof (int);
                // Verify this block was written to the spill file we are reading from. A
                // mismatch means we followed a back link into a different chromatogram's spill
                // file (issue #4287), which otherwise corrupts times/scans silently whenever the
                // wrong file happens to be long enough not to trigger an out-of-range crash.
                int blockChromatogramIndex = BitConverter.ToInt32(bytes, source);
                source += sizeof (int);
                if (blockChromatogramIndex != _spillChromatogramIndex)
                    throw new InvalidDataException(string.Format(
                        @"Read a spilled data block for chromatogram {0} while reconstructing chromatogram {1} at fileOffset {2} (blocksOnDisk remaining {3}, nextBackLink {4}, bytesLen {5}); the wrong spill file was read.",
                        blockChromatogramIndex, _spillChromatogramIndex, blockStart, _blocksOnDisk, _filePosition, bytes.Length));
                int dest2 = dest;

                // Convert byte data.
                if (typeof (TData) == typeof (short))
                {
                    for (int j = 0; j < _blockSize; j++)
                    {
                        // ReSharper disable once AssignNullToNotNullAttribute
                        array[dest2++] = (TData) (object) BitConverter.ToInt16(bytes, source);
                        source += sizeof (short);
                    }
                }
                else if (typeof (TData) == typeof (int))
                {
                    for (int j = 0; j < _blockSize; j++)
                    {
                        // ReSharper disable once AssignNullToNotNullAttribute
                        array[dest2++] = (TData)(object)BitConverter.ToInt32(bytes, source);
                        source += sizeof (int);
                    }
                }
                else if (typeof (TData) == typeof (float))
                {
                    for (int j = 0; j < _blockSize; j++)
                    {
                        // ReSharper disable once AssignNullToNotNullAttribute
                        array[dest2++] = (TData)(object)BitConverter.ToSingle(bytes, source);
                        source += sizeof (float);
                    }
                }
                else
                {
                    Assume.Fail();
                }
                _blocksOnDisk--;
            }

            // Cache the final array in case this collector is shared (i.e., shared times).
            _block = new Block(array, null);
            _blockIndex = -1;
            
            return array;
        }
    }

    /// <summary>
    /// Do fast (but not necessarily complete) checks to make sure data items
    /// are added in sort order.
    /// </summary>
    /// <typeparam name="TData"></typeparam>
    public class SortedBlockedList<TData> : BlockedList<TData>
    {
        public new void Add(int chromatogramIndex, TData data, BlockWriter writer)
        {
            // Check sort ordering within blocks.  Checking across blocks is too time consuming.
            if (_blockIndex > 0 && Comparer<TData>.Default.Compare(_block._data[_blockIndex-1], data) > 0)
                throw new InvalidDataException(ResultsResources.Block_VerifySort_Expected_sorted_data);
            base.Add(chromatogramIndex, data, writer);
        }

        public new void AddShared(TData data)
        {
            // Check sort ordering within blocks.  Checking across blocks is too time consuming.
            if (_blockIndex > 0 && Comparer<TData>.Default.Compare(_block._data[_blockIndex - 1], data) > 0)
                throw new InvalidDataException(ResultsResources.Block_VerifySort_Expected_sorted_data);
            base.AddShared(data);
        }
    }

    /// <summary>
    /// Optional object to write blocks to disk.
    /// </summary>
    public class BlockWriter
    {
        private readonly ChromGroups _chromGroups;

        public BlockWriter(ChromGroups chromGroups)
        {
            _chromGroups = chromGroups;
        }

        /// <summary>
        /// Write a block from a blocked list to disk for the given chromatogram.
        /// </summary>
        public void WriteBlock(int chromatogramIndex, IBlockedList blockedList)
        {
            // Serialize with the release-time whole-file read so the write position is never
            // computed from a stream another thread has seeked to 0 (issue #4287).
            lock (_chromGroups.SpillStreamLock)
            {
                var fileStream = _chromGroups.GetFileStream(chromatogramIndex);
                blockedList.WriteBlock(fileStream);
            }
        }
    }

    /// <summary>
    /// Keep track of groups of chromatograms that the reader wants to read together.
    /// These are kept together in spill files that are written to disk and then
    /// read back into memory in one big gulp.
    /// </summary>
    public class ChromGroups : IDisposable
    {
        // We try to make our spill files approximately 200MB
        private const long TARGET_SPILL_FILE_SIZE = 200 * 1024 * 1024;
        // Errors occur if the spill file is more than 2GB, so we make sure that the
        // most pessimistic estimate of the data size is less than this
        private const long MAX_SPILL_FILE_SIZE = 1L << 32 - 1;

        private readonly IList<ChromKey> _chromKeys;
        private readonly float _maxRetentionTime;
        private readonly int _cycleCount;
        private readonly string _cachePath;
        private readonly SpillFile[] _spillFiles;
        private readonly int[] _idToGroupId;
        private SpillFile _cachedSpillFile;
        private byte[] _bytesFromSpillFile;
        // Leaf lock serializing all spill-file stream I/O (block writes on the extraction thread
        // and the whole-file read on the reader thread). Issue #4287: without this, the reader's
        // Seek(0) to read a spill file races with a concurrent block write to the same shared
        // stream (files are shared across request-order groups), so the writer computes its next
        // file position from the seeked-back stream and overwrites earlier blocks, corrupting the
        // back-link chains. Acquired last and released before any Monitor.Wait, so it can never
        // participate in a deadlock with the _blockWriter / Collectors ("this") locks.
        private readonly object _spillStreamLock = new object();

        /// <summary>
        /// Lock guarding all reads and writes of the spill-file streams (see <see cref="_spillStreamLock"/>).
        /// </summary>
        public object SpillStreamLock { get { return _spillStreamLock; } }

        public ChromGroups(
            IList<IList<int>> chromatogramRequestOrder,
            IList<ChromKey> chromKeys,
            float maxRetentionTime,
            int cycleCount,
            string cachePath)
        {
            RequestOrder = chromatogramRequestOrder;
            _chromKeys = chromKeys;
            _maxRetentionTime = maxRetentionTime;
            _cycleCount = cycleCount;
            _cachePath = cachePath;
            if (RequestOrder == null)
                return;

            // Sanity check.
            foreach (var group in RequestOrder)
            {
                foreach (var chromIndex in group)
                    Assume.IsTrue(chromIndex >= 0 && chromIndex < _chromKeys.Count);
            }

            // Create array to map a provider id back to the peptide group that contains it.
            _idToGroupId = new int[_chromKeys.Count];
            for (int groupId = 0; groupId < chromatogramRequestOrder.Count; groupId++)
            {
                foreach (var id in chromatogramRequestOrder[groupId])
                    _idToGroupId[id] = groupId;
            }

            // Decide how groups will be allocated to spill files.
            long maxSpillFileSize = 0;
            long estimateSpillFileSize = 0;
            SpillFile spillFile = new SpillFile();
            _spillFiles = new SpillFile[chromatogramRequestOrder.Count];
            for (int groupId = 0; groupId < _spillFiles.Length; groupId++)
            {
                long maxGroupSize = GetMaxSize(groupId);
                long estimateGroupSize = EstimateGroupSize(groupId);
                maxSpillFileSize += maxGroupSize;
                estimateSpillFileSize += estimateGroupSize;
                if (maxSpillFileSize > MAX_SPILL_FILE_SIZE || estimateSpillFileSize > TARGET_SPILL_FILE_SIZE)
                {
                    spillFile = new SpillFile();
                    maxSpillFileSize = maxGroupSize;
                    estimateSpillFileSize = estimateGroupSize;
                }
                _spillFiles[groupId] = spillFile;
                spillFile.MaxTime = Math.Max(spillFile.MaxTime, GetMaxTime(groupId));
            }
        }

        /// <summary>
        /// This is the order and grouping that the reader will use when reading
        /// chromatograms.  We exploit this information for speed and memory use.
        /// </summary>
        public IList<IList<int>> RequestOrder { get; private set; }

        public void Dispose()
        {
            if (_spillFiles != null)
            {
                string cachePath = null;
                for (int i = 0; i < _spillFiles.Length; i++)
                {
                    if (_spillFiles[i].FileName != null)
                    {
                        cachePath = _spillFiles[i].FileName;
                    }
                    _spillFiles[i].CloseStream();
                }

                if (cachePath != null)
                    DirectoryEx.SafeDelete(Path.GetDirectoryName(cachePath));
            }
        }

        /// <summary>
        /// Return the group that a given chromatogram belongs to.
        /// </summary>
        public int GetGroupIndex(int chromatogramIndex)
        {
            return chromatogramIndex < _idToGroupId.Length ? _idToGroupId[chromatogramIndex] : 0;
        }

        /// <summary>
        /// Get the maximum retention time for all the chromatograms in a given group.
        /// </summary>
        public float GetMaxTime(int groupIndex)
        {
            float maxTime = 0;
            foreach (var index in RequestOrder[groupIndex])
            {
                var key = _chromKeys[index];
                if (!key.OptionalMaxTime.HasValue)
                    return float.MaxValue;
                maxTime = Math.Max(maxTime, (float) key.OptionalMaxTime.Value);
            }
            return maxTime;
        }

        /// <summary>
        /// Return (or possibly create) a spill file stream for the given group.
        /// </summary>
        public Stream GetFileStream(int chromIndex)
        {
            int groupIndex = GetGroupIndex(chromIndex);
            return _spillFiles[groupIndex].CreateFileStream(_cachePath, groupIndex);
        }

        /// <summary>
        /// Release a chromatogram if its collector is complete (indicated by retention time).
        /// </summary>
        /// <returns>-1 if chromatogram is not finished yet.</returns>
        public int ReleaseChromatogram(
            int chromatogramIndex, float retentionTime, ChromCollector collector,
            out TimeIntensities timeIntensities)
        {
            int groupIndex = GetGroupIndex(chromatogramIndex);
            var spillFile = _spillFiles[groupIndex];

            // Not done reading yet.
            if (retentionTime < spillFile.MaxTime || (collector != null && !collector.IsSetTimes))
            {
                timeIntensities = null;
                return -1;
            }

            // No chromatogram information collected.
            if (collector == null)
            {
                timeIntensities = TimeIntensities.EMPTY;
                return 0;
            }

            // Fail fast if the shared time/scan lists this chromatogram uses were spilled to a
            // different spill file than the one selected by this chromatogram index. Reading the
            // wrong spill file is the root cause of issue #4287; catch it here with a clear message
            // instead of a later out-of-range crash or (worse) silently wrong retention times.
            AssertSharedListSpillFile(collector.TimesSpillChromatogramIndex, chromatogramIndex, groupIndex);
            AssertSharedListSpillFile(collector.ScansSpillChromatogramIndex, chromatogramIndex, groupIndex);

            // Serialize the whole-file read with concurrent block writes to the same shared spill
            // stream (issue #4287). The lock is released before returning, and this method never
            // waits, so it cannot deadlock with the extraction thread's locks.
            lock (_spillStreamLock)
            {
                if (ReferenceEquals(_cachedSpillFile, spillFile))
                {
                    if (spillFile.Stream != null)
                    {
                        if (_bytesFromSpillFile == null || spillFile.Stream.Length != _bytesFromSpillFile.Length)
                        {
                            // Need to reread spill file if more bytes were written since the time it was cached.
                            _cachedSpillFile = null;
                        }
                    }
                }

                if (!ReferenceEquals(_cachedSpillFile, spillFile))
                {
                    _cachedSpillFile = spillFile;
                    _bytesFromSpillFile = null;
                    var fileStream = spillFile.Stream;
                    if (fileStream != null)
                    {
                        // DIAGNOSTIC (issue #4287): reading seeks the shared write stream to 0. Log the
                        // thread and file so we can see whether this races with WriteData on the same file.
                        Console.Error.WriteLine(@"[#4287] SEEK-READ file={0} len={1} thread={2}",
                            spillFile, fileStream.Length, System.Threading.Thread.CurrentThread.ManagedThreadId);
                        fileStream.Seek(0, SeekOrigin.Begin);
                        _bytesFromSpillFile = new byte[fileStream.Length];
                        int bytesRead = fileStream.Read(_bytesFromSpillFile, 0, _bytesFromSpillFile.Length);
                        Assume.IsTrue(bytesRead == _bytesFromSpillFile.Length);
                        // TODO: Would be nice to have something that releases spill files progressively, as they are
                        //       no longer needed.
                        // spillFile.CloseStream();
                    }
                }
            }
            // DIAGNOSTIC (issue #4287): on any release failure, dump the full spill state so we can
            // see which list mismatches, its write group, and whether that group's spill file is the
            // same object as the one selected by the release chromatogram index.
            try
            {
                collector.ReleaseChromatogram(_bytesFromSpillFile, out timeIntensities);
            }
            catch (Exception)
            {
                Console.Error.WriteLine(
                    @"[#4287] FAIL release chromIndex={0} releaseGroup={1} releaseFile={2} streamNull={3} bytesNull={4} bytesLen={5} | {6} | {7} {8} {9} {10}",
                    chromatogramIndex, groupIndex, spillFile, spillFile.Stream == null,
                    _bytesFromSpillFile == null, _bytesFromSpillFile?.Length ?? -1,
                    collector.DescribeSpillState(),
                    DescribeListFile(@"Times", collector.TimesSpillChromatogramIndex, spillFile),
                    DescribeListFile(@"Scans", collector.ScansSpillChromatogramIndex, spillFile),
                    DescribeListFile(@"Intens", collector.IntensitiesSpillChromatogramIndex, spillFile),
                    DescribeListFile(@"MassErr", collector.MassErrorsSpillChromatogramIndex, spillFile));
                throw;
            }

            return collector.StatusId;
        }

        // DIAGNOSTIC (issue #4287): describe where a list was spilled relative to the release file.
        private string DescribeListFile(string name, int? spillIdx, SpillFile releaseFile)
        {
            if (!spillIdx.HasValue)
                return name + @"=-";
            int g = GetGroupIndex(spillIdx.Value);
            return string.Format(@"{0}[idx={1},grp={2},sameFile={3},file={4}]",
                name, spillIdx.Value, g, ReferenceEquals(_spillFiles[g], releaseFile), _spillFiles[g]);
        }

        /// <summary>
        /// Throw if a shared time/scan list was spilled to a different spill file group than the
        /// one this chromatogram is being released from. When they differ, the bytes handed to
        /// <see cref="ChromCollector.ReleaseChromatogram"/> come from the wrong file (issue #4287).
        /// </summary>
        private void AssertSharedListSpillFile(int? spillChromatogramIndex, int releaseChromatogramIndex, int releaseGroupIndex)
        {
            if (!spillChromatogramIndex.HasValue)
                return;
            int writeGroupIndex = GetGroupIndex(spillChromatogramIndex.Value);
            if (writeGroupIndex != releaseGroupIndex)
                throw new InvalidDataException(string.Format(
                    @"Chromatogram {0} uses a shared time/scan list written to spill file group {1} (via chromatogram {2}), but is being released from spill file group {3}.",
                    releaseChromatogramIndex, writeGroupIndex, spillChromatogramIndex.Value, releaseGroupIndex));
        }

        /// <summary>
        /// Get the maximum possible size (in bytes) of a group, including all the chromatograms that are
        /// included in the group.
        /// </summary>
        private long GetMaxSize(int groupIndex)
        {
            int recordSize = sizeof(float) + sizeof(float) + sizeof(float) + sizeof(int); // time, intensity, mass error, scan index
            return _cycleCount * RequestOrder[groupIndex].Count * recordSize;
        }

        /// <summary>
        /// Returns the most likely size that the data for this group will take on disk.
        /// </summary>
        private long EstimateGroupSize(int groupIndex)
        {
            const int SPILL_FILE_OVERESTIMATION_FACTOR = 4;
            long maxSize = 0;
            foreach (var index in RequestOrder[groupIndex])
            {
                var key = _chromKeys[index];
                maxSize += EstimateSpectrumCount(key.OptionalMinTime, key.OptionalMaxTime);
            }
            maxSize *= sizeof(float) + sizeof(float) + sizeof(int); // Size of intensity, mass error, scan id
            maxSize /= SPILL_FILE_OVERESTIMATION_FACTOR;
            return maxSize;
        }

        private int EstimateSpectrumCount(double? minTime, double? maxTime)
        {
            if (!minTime.HasValue || !maxTime.HasValue)
            {
                return _cycleCount;
            }
            double duration = maxTime.Value - minTime.Value;
            if (duration >= _maxRetentionTime || duration <= 0)
            {
                return _cycleCount;
            }
            return (int)Math.Ceiling(duration * _cycleCount / _maxRetentionTime);
        }

        /// <summary>
        /// A file that contains "spilled" chromatogram data. We can't keep all the
        /// data resident in memory for very large raw data files, so we group chromatograms
        /// together (respecting an order defined by the reader) and store their data in
        /// a file that can then be read back into memory in largish chunks.
        /// </summary>
        private class SpillFile
        {
            private FileStream _fileStream;
            public BufferedStream Stream { get; private set; }
            public float MaxTime { get; set; }
            
            public BufferedStream CreateFileStream(string cachePath, int groupIndex)
            {
                if (Stream == null)
                {
                    // We make some effort to generate unique file names so that more than one instance
                    // of Skyline can load the same raw data file simultaneously.
                    var xicDir = GetSpillDirectory(cachePath);
                    TryHelper.Try<Exception>(() =>
                    {
                        string fileName = FileStreamManager.Default.GetTempFileName(xicDir, string.Format(@"{0:X03}", groupIndex & 0xFFF));    // Need uniquifying groupId because GetTempFileName is limited to 65,535 files with the same prefix in a folder
                        // Create the FileStream with a buffer size of 1 so that it never buffers, and therefore
                        // never tries to FlushWrite in its finalizer (errors thrown in finalizers can kill Skyline)
                        _fileStream = File.Create(fileName, 1, FileOptions.DeleteOnClose);
                        // Wrap the FileStream in a BufferedStream. BufferedStream does not have a finalizer
                        Stream = new BufferedStream(_fileStream, ushort.MaxValue);
                        FileName = fileName;
                    },
                    2, 100);
                }
                return Stream;
            }

            public void CloseStream()
            {
                if (_fileStream != null)
                {
                    _fileStream.Dispose();
                    _fileStream = null;
                }

                Stream = null;
                FileName = null;
            }

            private static string GetSpillDirectory(string cachePath)
            {
                string cacheDir = Path.GetDirectoryName(cachePath) ?? string.Empty;
                return Path.Combine(cacheDir, @"xic");
            }

            public override string ToString()
            {
                return FileName == null
                    ? @"(none)"
                    : FileName.Substring(FileName.Length - 8);
            }

            public string FileName { get; private set; }
        }
    }
}
