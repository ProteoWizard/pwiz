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
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{

    /// <summary>
    /// This class stores chromatogram intensity (and optional time) values for one transition.
    /// Memory is allocated in large-ish chunks and shared between various instances of ChromCollector.
    /// Blocks are paged out to a disk file if the data grows too large to fit in pre-allocated
    /// slots.
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

        public void SetTimes(SortedBlockedList<float> times)
        {
            Times = times;
        }

        public void SetScans(BlockedList<int> scans)
        {
            Scans = scans;
        }

        public void AddIntensity(float intensity, BlockWriter writer, int chromatogramIndex)
        {
            Intensities.Add(intensity, writer, chromatogramIndex);
        }

        public void FillIntensities(int count, BlockWriter writer, int chromatogramIndex)
        {
            Intensities.FillZeroes(count, writer, chromatogramIndex);
        }

        public void AddTime(float time, BlockWriter writer, int chromatogramIndex)
        {
            Times.Add(time, writer, chromatogramIndex);
        }

        public void AddMassError(float massError, BlockWriter writer, int chromatogramIndex)
        {
            MassErrors.Add(massError, writer, chromatogramIndex);
        }

        public int Count { get { return Intensities.Count; } }

        /// <summary>
        /// Get a chromatogram with properly sorted time values.
        /// </summary>
        public void ReleaseChromatogram(byte[] bytesFromDisk, out float[] times, out float[] intensities, out float[] massErrors, out int[] scanIds)
        {
            times = Times.ToArray(bytesFromDisk);
            intensities = Intensities.ToArray(bytesFromDisk);
            massErrors = MassErrors != null
                ? MassErrors.ToArray(bytesFromDisk)
                : null;
            scanIds = Scans != null
                ? Scans.ToArray(bytesFromDisk)
                : null;
            
            // Release memory.
            Times = null;
            Intensities = null;
            MassErrors = null;
            Scans = null;

            // Make sure times and intensities match in length.
            if (times.Length != intensities.Length)
            {
                throw new InvalidDataException(
                    string.Format(Resources.ChromCollected_ChromCollected_Times__0__and_intensities__1__disagree_in_point_count,
                    times.Length, intensities.Length));
            }
        }
    }

    public interface IBlockedList
    {
        void WriteBlocks(FileStream fileStream);
    }

    public class BlockedList<TData> : IBlockedList
    {
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

        public void Add(TData data, BlockWriter writer, int chromatogramIndex)
        {
            // Spill data to disk at block boundaries, if necessary.
            if (_blockIndex == 0)
            {
                if (_blocksInMemory == 0 || !writer.WriteBlocks(this, chromatogramIndex))
                    NewBlock();
            }

            // Store data.
            _block._data[_blockIndex] = data;

            // Wrap index to next block.
            if (++_blockIndex == _blockSize)
                _blockIndex = 0;
        }

        public void AddShared(TData data)
        {
            // Spill data to disk at block boundaries, if necessary.
            if (_blockIndex == 0)
                NewBlock();

            // Store data.
            _block._data[_blockIndex] = data;

            // Wrap index to next block.
            if (++_blockIndex == _blockSize)
                _blockIndex = 0;
        }

        public void FillZeroes(int count, BlockWriter writer, int chromatogramIndex)
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

            // Create new pre-zeroed blocks.
            while (count >= _blockSize)
            {
                NewBlock();
                count -= _blockSize;
            }

            // Spill blocks to disk.
            writer.WriteBlocks(this, chromatogramIndex);

            // Final partial block.
            while (count > 0)
            {
                _block._data[_blockIndex++] = default(TData);
                count--;
            }
        }

        public void WriteBlocks(FileStream fileStream)
        {
            if (_blocksInMemory == 1)
            {
                WriteData(_block, fileStream);
                _blocksOnDisk++;
                return;
            }

            // Write each block to file stream.
            var blockList = new Block[_blocksInMemory];
            for (int i = _blocksInMemory - 1; i >= 0; i--)
            {
                blockList[i] = _block;
                _block = _block._previousBlock;
            }
            for (int i = 0; i < _blocksInMemory; i++)
            {
                WriteData(blockList[i], fileStream);
            }
            _blocksOnDisk += _blocksInMemory;

            // Reuse first block
            _block = blockList[0];
            _blocksInMemory = 1;
        }

        private void WriteData(Block block, FileStream fileStream)
        {
            // Create back link to previous spilled block.
            var lastFilePosition = new[] {_filePosition};
            _filePosition = (int) fileStream.Position;
            FastWrite.WriteInts(fileStream.SafeFileHandle, lastFilePosition, 0, 1);

            // Write one data block.
            if (typeof (TData) == typeof (short))
                FastWrite.WriteShorts(fileStream.SafeFileHandle, (short[]) (object) block._data, 0, _blockSize);
            else if (typeof (TData) == typeof (int))
                FastWrite.WriteInts(fileStream.SafeFileHandle, (int[])(object) block._data, 0, _blockSize);
            else if (typeof (TData) == typeof (float))
                FastWrite.WriteFloats(fileStream.SafeFileHandle, (float[])(object) block._data, 0, _blockSize);
            else
                Assume.Fail();
        }

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

    public class SortedBlockedList<TData> : BlockedList<TData>
    {
        public new void Add(TData data, BlockWriter writer, int groupIndex)
        {
            if (_blockIndex > 0 && Comparer<TData>.Default.Compare(_block._data[_blockIndex-1], data) > 0)
                throw new InvalidDataException(Resources.Block_VerifySort_Expected_sorted_data);
            base.Add(data, writer, groupIndex);
        }

        public new void AddShared(TData data)
        {
            if (_blockIndex > 0 && Comparer<TData>.Default.Compare(_block._data[_blockIndex-1], data) > 0)
                throw new InvalidDataException(Resources.Block_VerifySort_Expected_sorted_data);
            base.AddShared(data);
        }
    }

    public class BlockWriter
    {
        private readonly ChromGroups _chromGroups;

        public BlockWriter(ChromGroups chromGroups)
        {
            _chromGroups = chromGroups;
        }

        public float RetentionTime { get; set; }

        public bool WriteBlocks(IBlockedList blockedList, int chromatogramIndex)
        {
            if (_chromGroups != null)
            {
                var fileStream = _chromGroups.GetFileStream(chromatogramIndex);
                blockedList.WriteBlocks(fileStream);
                return true;
            }
            return false;
        }

        public int ReleaseChromatogram(int index, ChromCollector collector, out float[] times, out float[] intensities, out float[] massErrors,
            out int[] scanIds)
        {
            if (_chromGroups != null)
                return _chromGroups.ReleaseChromatogram(index, RetentionTime, collector, out times, out intensities, out massErrors, out scanIds);
            collector.ReleaseChromatogram(null, out times, out intensities, out massErrors, out scanIds);
            return collector.StatusId;
        }
    }

    public class ChromGroups : IDisposable
    {
        private const int MAX_SPILL_FILE_SIZE = 200 * 1024 * 1024;
        private const int SPILL_FILE_OVER_ESTIMATION_FACTOR = 4;
        private static readonly float[] EMPTY_FLOAT_ARRAY = new float[0];

        private readonly IList<ChromKey> _chromKeys;
        private readonly float _maxRetentionTime;
        private readonly string _cachePath;
        private readonly SpillFile[] _spillFiles;
        private readonly int[] _idToGroupId;
        private SpillFile _cachedSpillFile;
        private byte[] _bytesFromSpillFile;

        public ChromGroups(
            IList<IList<int>> chromatogramRequestOrder,
            IList<ChromKey> chromKeys,
            float maxRetentionTime,
            string cachePath)
        {
            RequestOrder = chromatogramRequestOrder;
            _chromKeys = chromKeys;
            _maxRetentionTime = maxRetentionTime;
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
                {
                    _idToGroupId[id] = groupId;
                }
            }

            // Decide how groups will be allocated to spill files.
            int spillFileSize = 0;
            SpillFile spillFile = new SpillFile();
            _spillFiles = new SpillFile[chromatogramRequestOrder.Count];
            for (int groupId = 0; groupId < _spillFiles.Length; groupId++)
            {
                int groupSize = GetMaxSize(groupId);
                spillFileSize += groupSize;
                if (spillFileSize > MAX_SPILL_FILE_SIZE * SPILL_FILE_OVER_ESTIMATION_FACTOR)
                {
                    spillFile = new SpillFile();
                    spillFileSize = groupSize;
                }
                _spillFiles[groupId] = spillFile;
                spillFile.MaxTime = Math.Max(spillFile.MaxTime, GetMaxTime(groupId));
            }
        }

        public IList<IList<int>> RequestOrder { get; private set; }

        public void Dispose()
        {
            if (_spillFiles != null)
            {
                string cachePath = null;
                for (int i = 0; i < _spillFiles.Length; i++)
                {
                    var stream = _spillFiles[i].Stream;
                    if (stream != null)
                    {
                        cachePath = stream.Name;
                        stream.Dispose();
                    }
                }

                if (cachePath != null)
                    DirectoryEx.SafeDelete(Path.GetDirectoryName(cachePath));
            }
        }

        public int GetGroupIndex(int chromatogramIndex)
        {
            return chromatogramIndex < _idToGroupId.Length ? _idToGroupId[chromatogramIndex] : 0;
        }

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

        public FileStream GetFileStream(int chromIndex)
        {
            int groupIndex = GetGroupIndex(chromIndex);
            return _spillFiles[groupIndex].CreateFileStream(_cachePath);
        }

        public int ReleaseChromatogram(
            int index, float retentionTime, ChromCollector collector,
            out float[] times, out float[] intensities, out float[] massErrors, out int[] scanIds)
        {
            int groupIndex = GetGroupIndex(index);
            var spillFile = _spillFiles[groupIndex];

            // Not done reading yet.
            if (retentionTime < spillFile.MaxTime)
            {
                times = null;
                intensities = null;
                massErrors = null;
                scanIds = null;
                return -1;
            }

            // No chromatogram information collected.
            if (collector == null)
            {
                times = EMPTY_FLOAT_ARRAY;
                intensities = EMPTY_FLOAT_ARRAY;
                massErrors = null;
                scanIds = null;
                return 0;
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
            collector.ReleaseChromatogram(_bytesFromSpillFile, out times, out intensities, out massErrors, out scanIds);
                
            return collector.StatusId;
        }

        public int GetMaxSize(int groupIndex)
        {
            int maxSize = 0;
            foreach (var index in RequestOrder[groupIndex])
            {
                var key = _chromKeys[index];
                double duration = key.OptionalMaxTime.HasValue && key.OptionalMinTime.HasValue
                    ? key.OptionalMaxTime.Value - key.OptionalMinTime.Value
                    : _maxRetentionTime;
                maxSize += (int) Math.Ceiling(duration/PeptideChromDataSets.TIME_MIN_DELTA);
            }
            maxSize *= sizeof (float) + sizeof (float) + sizeof (int); // Size of intensity, mass error, scan id
            return maxSize;
        }

        private class SpillFile
        {
            public FileStream Stream { get; private set; }
            public float MaxTime { get; set; }
            
            public FileStream CreateFileStream(string cachePath)
            {
                if (Stream == null)
                {
                    var xicDir = GetSpillDirectory(cachePath);
                    Directory.CreateDirectory(xicDir);
                    string fileName = Path.Combine(xicDir,
                        "xic" + "_" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + "_" + Guid.NewGuid() + ".tmp"); // Not L10N
                    Stream = File.Create(fileName, ushort.MaxValue, FileOptions.DeleteOnClose);
                }
                return Stream;
            }

            public void CloseStream()
            {
                Stream.Dispose();
                Stream = null;
            }

            private static string GetSpillDirectory(string cachePath)
            {
                string cacheDir = Path.GetDirectoryName(cachePath) ?? string.Empty;
                return Path.Combine(cacheDir, "xic"); // Not L10N
            }

            public override string ToString()
            {
                return Stream == null
                    ? "(none)" // Not L10N
                    : Stream.Name.Substring(Stream.Name.Length - 8);
            }
        }
    }
}