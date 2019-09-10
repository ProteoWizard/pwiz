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
using pwiz.Skyline.Properties;
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

        public ChromCollector(int statusId, bool hasTimes, bool hasMassErrors)
        {
            StatusId = statusId;
            Intensities = new BlockedList<float>();
            if (hasTimes)
                Times = new SortedBlockedList<float>();
            if (hasMassErrors)
                MassErrors = new BlockedList<float>();
        }

        public bool IsSetTimes { get { return Times != null; } }

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
        /// </summary>
        public void AddPoint(int chromatogramIndex, float intensity, float? massError, BlockWriter writer)
        {
            if (MassErrors != null)
                // ReSharper disable once PossibleInvalidOperationException
                MassErrors.Add(chromatogramIndex, massError.Value, writer); // If massError is required, this won't be null (and if it is, we want to hear about it)
            Intensities.Add(chromatogramIndex, intensity, writer);
        }

        /// <summary>
        /// Fill a number of intensity and mass error values for the given chromatogram with zeroes.
        /// </summary>
        public void FillZeroes(int chromatogramIndex, int count, BlockWriter writer)
        {
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

        /// <summary>
        /// Get a chromatogram with properly sorted time values.
        /// </summary>
        public void ReleaseChromatogram(byte[] bytesFromDisk, out TimeIntensities timeIntensities)
        {
            var times = Times.ToArray(bytesFromDisk);
            var intensities = Intensities.ToArray(bytesFromDisk);
            var massErrors = MassErrors != null
                ? MassErrors.ToArray(bytesFromDisk)
                : null;
            var scanIds = Scans != null
                ? Scans.ToArray(bytesFromDisk)
                : null;
            // Make sure times and intensities match in length.
            if (times.Length != intensities.Length)
            {
                throw new InvalidDataException(
                    string.Format(Resources.ChromCollected_ChromCollected_Times__0__and_intensities__1__disagree_in_point_count,
                    times.Length, intensities.Length));
            }
            if (massErrors != null && massErrors.Length != intensities.Length)
            {
                throw new InvalidDataException(
                    string.Format(Resources.ChromCollector_ReleaseChromatogram_Intensities___0___and_mass_errors___1___disagree_in_point_count_,
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

        public BlockedList()
        {
            _blockSize = Environment.Is64BitProcess ? BLOCK_SIZE_FOR_64_BIT : BLOCK_SIZE_FOR_32_BIT;
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
                    writer.WriteBlock(chromatogramIndex, this);
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
            // Create back link to previous spilled block.
            var lastFilePosition = _filePosition;
            _filePosition = (int) fileStream.Position;
            PrimitiveArrays.WriteOneValue(fileStream, lastFilePosition);

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
                int source = _filePosition;
                // ReSharper disable once AssignNullToNotNullAttribute
                _filePosition = BitConverter.ToInt32(bytes, source);
                source += sizeof (int);
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
                throw new InvalidDataException(Resources.Block_VerifySort_Expected_sorted_data);
            base.Add(chromatogramIndex, data, writer);
        }

        public new void AddShared(TData data)
        {
            // Check sort ordering within blocks.  Checking across blocks is too time consuming.
            if (_blockIndex > 0 && Comparer<TData>.Default.Compare(_block._data[_blockIndex - 1], data) > 0)
                throw new InvalidDataException(Resources.Block_VerifySort_Expected_sorted_data);
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
            var fileStream = _chromGroups.GetFileStream(chromatogramIndex);
            blockedList.WriteBlock(fileStream);
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
        private readonly int _spectrumCount;
        private readonly string _cachePath;
        private readonly SpillFile[] _spillFiles;
        private readonly int[] _idToGroupId;
        private SpillFile _cachedSpillFile;
        private byte[] _bytesFromSpillFile;

        public ChromGroups(
            IList<IList<int>> chromatogramRequestOrder,
            IList<ChromKey> chromKeys,
            float maxRetentionTime,
            int spectrumCount,
            string cachePath)
        {
            RequestOrder = chromatogramRequestOrder;
            _chromKeys = chromKeys;
            _maxRetentionTime = maxRetentionTime;
            _spectrumCount = spectrumCount;
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
            return _spillFiles[groupIndex].CreateFileStream(_cachePath);
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
                    fileStream.Seek(0, SeekOrigin.Begin);
                    _bytesFromSpillFile = new byte[fileStream.Length];
                    int bytesRead = fileStream.Read(_bytesFromSpillFile, 0, _bytesFromSpillFile.Length);
                    Assume.IsTrue(bytesRead == _bytesFromSpillFile.Length);
                    // TODO: Would be nice to have something that releases spill files progressively, as they are
                    //       no longer needed.
                    // spillFile.CloseStream();
                }
            }
            collector.ReleaseChromatogram(_bytesFromSpillFile, out timeIntensities);
                
            return collector.StatusId;
        }

        /// <summary>
        /// Get the maximum possible size (in bytes) of a group, including all the chromatograms that are 
        /// included in the group.
        /// </summary>
        private long GetMaxSize(int groupIndex)
        {
            int recordSize = sizeof(float) + sizeof(float) + sizeof(float) + sizeof(int); // time, intensity, mass error, scan index
            return _spectrumCount * RequestOrder[groupIndex].Count * recordSize;
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
                return _spectrumCount;
            }
            double duration = maxTime.Value - minTime.Value;
            if (duration >= _maxRetentionTime || duration <= 0)
            {
                return _spectrumCount;
            }
            return (int)Math.Ceiling(duration * _spectrumCount / _maxRetentionTime);
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
            
            public BufferedStream CreateFileStream(string cachePath)
            {
                if (Stream == null)
                {
                    // We make some effort to generate unique file names so that more than one instance
                    // of Skyline can load the same raw data file simultaneously.
                    var xicDir = GetSpillDirectory(cachePath);
                    Helpers.Try<Exception>(() =>
                    {
                        string fileName = FileStreamManager.Default.GetTempFileName(xicDir, @"xic");
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
