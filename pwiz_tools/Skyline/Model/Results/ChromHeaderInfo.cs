/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.PeakFinding;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Crawdad;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results
{
    // This format was in use prior to Feb 2013, when the peak scoring work was added
    [StructLayout(LayoutKind.Sequential, Pack=4)]
    public struct ChromGroupHeaderInfo4
    {
        public ChromGroupHeaderInfo4(float precursor, int fileIndex, int numTransitions, int startTransitionIndex,
                int numPeaks, int startPeakIndex, int maxPeakIndex,
                int numPoints, int compressedSize, long location)
            : this()
        {
            Precursor = precursor;
            FileIndex = fileIndex;
            NumTransitions = numTransitions;
            StartTransitionIndex = startTransitionIndex;
            NumPeaks = numPeaks;
            StartPeakIndex = startPeakIndex;
            MaxPeakIndex = maxPeakIndex;
            NumPoints = numPoints;
            CompressedSize = compressedSize;
            Align = 0;
            LocationPoints = location;
        }

        public ChromGroupHeaderInfo4(ChromGroupHeaderInfo header) : this()
        {
            Precursor = (float) header.Precursor;
            FileIndex = header.FileIndex;
            NumTransitions = header.NumTransitions;
            StartTransitionIndex = header.StartTransitionIndex;
            NumPeaks = header.NumPeaks;
            StartPeakIndex = header.StartPeakIndex;
            MaxPeakIndex = header.MaxPeakIndex;
            NumPoints = header.NumPoints;
            CompressedSize = header.CompressedSize;
            LocationPoints = header.LocationPoints;
        }

        public float Precursor { get; set; }
        public int FileIndex { get; private set; }
        public int NumTransitions { get; private set; }
        public int StartTransitionIndex { get; private set; }
        public int NumPeaks { get; private set; }
        public int StartPeakIndex { get; private set; }
        public int MaxPeakIndex { get; private set; }
        public int NumPoints { get; private set; }
        public int CompressedSize { get; private set; }
        public int Align { get; private set; }  // Need even number of 4-byte values
        public long LocationPoints { get; private set; }

        public void Offset(int offsetFiles, int offsetTransitions, int offsetPeaks, long offsetPoints)
        {
            FileIndex += offsetFiles;
            StartTransitionIndex += offsetTransitions;
            StartPeakIndex += offsetPeaks;
            LocationPoints += offsetPoints;
        }

        public static StructSerializer<ChromGroupHeaderInfo4> StructSerializer()
        {
            return new StructSerializer<ChromGroupHeaderInfo4>()
            {
                DirectSerializer = DirectSerializer.Create(ReadArray, WriteArray)
            };
        }

        #region Fast file I/O
        /// <summary>
        /// Direct read of an entire array using p-invoke of Win32 WriteFile.  This seems
        /// to coexist with FileStream reading that the write version, but its use case
        /// is tightly limited.
        /// <para>
        /// Contributed by Randy Kern.  See:
        /// http://randy.teamkern.net/2009/02/reading-arrays-from-files-in-c-without-extra-copy.html
        /// </para>
        /// </summary>
        /// <param name="file">File handler returned from <see cref="FileStream.SafeFileHandle"/></param>
        /// <param name="count">Number of elements to read</param>
        /// <returns>New array of elements</returns>
        private static unsafe ChromGroupHeaderInfo4[] ReadArray(SafeHandle file, int count)
        {
            ChromGroupHeaderInfo4[] results = new ChromGroupHeaderInfo4[count];
            fixed (ChromGroupHeaderInfo4* p = results)
            {
                FastRead.ReadBytes(file, (byte*)p, sizeof(ChromGroupHeaderInfo4) * count);
            }

            return results;
        }

        /// <summary>
        /// Direct write of an entire array throw p-invoke of Win32 WriteFile.  This cannot
        /// be mixed with standard writes to a FileStream, or .NET throws an exception
        /// about the file location not being what it expected.
        /// </summary>
        /// <param name="file">File handler returned from <see cref="FileStream.SafeFileHandle"/></param>
        /// <param name="groupHeaders">The array to write</param>
        public static unsafe void WriteArray(SafeHandle file, ChromGroupHeaderInfo4[] groupHeaders)
        {
            fixed (ChromGroupHeaderInfo4* p = groupHeaders)
            {
                FastWrite.WriteBytes(file, (byte*)p, sizeof(ChromGroupHeaderInfo4) * groupHeaders.Length);
            }
        }
        #endregion
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ChromGroupHeaderInfo : IComparable<ChromGroupHeaderInfo>
    {
        /////////////////////////////////////////////////////////////////////
        // CAREFUL: This ordering determines the layout of this struct on
        //          disk from which it gets loaded directly into memory.
        //          The order and size of each element has been very carefully
        //          considered to avoid wasted space due to alignment.
        // ALSO:    With any additions you need to tweak the writer code in 
        //          ChromatogramCache.WriteStructs since we write element by element.
        private int _textIdIndex;
        private int _startTransitionIndex;
        private int _startPeakIndex;
        private int _startScoreIndex;
        private int _numPoints;
        private int _compressedSize;
        private ushort _flagBits;
        private ushort _fileIndex;
        private ushort _textIdLen;
        private ushort _numTransitions;
        private byte _numPeaks;
        private byte _maxPeakIndex;
        private byte _isProcessedScans;
        private byte _align1;
        private ushort _statusId;
        private ushort _statusRank;
        private double _precursor;
        private long _locationPoints;
        // V11 fields
        private int _uncompressedSize;
        private float _startTime;
        private float _endTime;
        private float _collisionalCrossSection;
        /////////////////////////////////////////////////////////////////////

        [Flags]
        public enum FlagValues
        {
            has_mass_errors = 0x01,
            has_calculated_mzs = 0x02,
            extracted_base_peak = 0x04,
            has_ms1_scan_ids = 0x08,
            has_sim_scan_ids = 0x10,
            has_frag_scan_ids = 0x20,
            polarity_negative = 0x40, // When set, only use negative scans.
            raw_chromatograms = 0x80,
            ion_mobility_type_bitmask = 0x700, // 3 bits for ion mobility type none, drift, inverse_mobility, spares
            dda_acquisition_method = 0x800,
            extracted_qc_trace = 0x1000
        }

        /// <summary>
        /// Allow a little fewer points than the data structure can actually hold.
        /// </summary>
        public const int MAX_POINTS = ushort.MaxValue - 1000;

        private const byte NO_MAX_PEAK = 0xFF;

        /// <summary>
        /// Constructs header struct with TextIdIndex and TextIdCount left to be initialized
        /// in a subsequent call to <see cref="CalcTextIdIndex"/>.
        /// </summary>
        public ChromGroupHeaderInfo(SignedMz precursor, int fileIndex,
                                     int numTransitions, int startTransitionIndex,
                                     int numPeaks, int startPeakIndex, int startScoreIndex, int maxPeakIndex,
                                     int numPoints, int compressedSize, int uncompressedSize, long location, FlagValues flags,
                                     int statusId, int statusRank,
                                     float? startTime, float? endTime,
                                     double? collisionalCrossSection, 
                                     eIonMobilityUnits ionMobilityUnits)
            : this(precursor, -1, 0, fileIndex, numTransitions, startTransitionIndex,
                   numPeaks, startPeakIndex, startScoreIndex, maxPeakIndex, numPoints,
                   compressedSize, uncompressedSize, location, flags, statusId, statusRank,
                   startTime, endTime, collisionalCrossSection, ionMobilityUnits)
        {
        }

        /// <summary>
        /// Cunstructs header struct with all values populated.
        /// </summary>
        public ChromGroupHeaderInfo(SignedMz precursor, int textIdIndex, int textIdLen, int fileIndex,
                                     int numTransitions, int startTransitionIndex,
                                     int numPeaks, int startPeakIndex, int startScoreIndex, int maxPeakIndex,
                                     int numPoints, int compressedSize, int uncompressedSize, long location, FlagValues flags,
                                     int statusId, int statusRank,
                                     float? startTime, float? endTime,
                                     double? collisionalCrossSection, eIonMobilityUnits ionMobilityUnits)
            : this()
        {
            _precursor = precursor.Value;
            if (precursor.IsNegative)
            {
                flags |= FlagValues.polarity_negative;
            }
            else
            {
                flags &= ~FlagValues.polarity_negative;
            }
            flags = (flags & ~FlagValues.ion_mobility_type_bitmask) | (FlagValues) ((int) ionMobilityUnits << 8);
            _textIdIndex = textIdIndex;
            _textIdLen = CheckUShort(textIdLen);
            _fileIndex = CheckUShort(fileIndex);
            _numTransitions = CheckUShort(numTransitions);
            _startTransitionIndex = startTransitionIndex;
            _numPeaks = CheckByte(numPeaks);
            _startPeakIndex = startPeakIndex;
            _startScoreIndex = startScoreIndex;
            _maxPeakIndex = maxPeakIndex != -1 ? CheckByte(maxPeakIndex, byte.MaxValue - 1) : NO_MAX_PEAK;
            _numPoints = precursor == SignedMz.ZERO ? 0 : CheckUShort(numPoints);
            _compressedSize = compressedSize;
            _uncompressedSize = uncompressedSize;
            _locationPoints = location;
            _flagBits = (ushort)flags;
            _statusId = CheckUShort(statusId, true);
            _statusRank = CheckUShort(statusRank, true);
            _startTime = startTime ?? -1;
            _endTime = endTime ?? -1;
            _collisionalCrossSection = (float)(collisionalCrossSection ?? 0);
            if (_startTime < 0)
            {
                _startTime = -1;  // Unknown
            }
            if (_endTime < 0)
            {
                _endTime = -1;  // Unknown
            }
            if (_startTime >= _endTime)
            {
                _startTime = _endTime = -1; // Unknown
            }
        }

        public ChromGroupHeaderInfo(ChromGroupHeaderInfo4 headerInfo)
            : this(new SignedMz(headerInfo.Precursor),
            headerInfo.FileIndex,
            headerInfo.NumTransitions,
            headerInfo.StartTransitionIndex,
            headerInfo.NumPeaks,
            headerInfo.StartPeakIndex,
            -1,
            headerInfo.MaxPeakIndex,
            headerInfo.NumPoints,
            headerInfo.CompressedSize,
            -1,
            headerInfo.LocationPoints,
            0, -1, -1,
            null, null, null, eIonMobilityUnits.none)
        {
        }

        public ChromGroupHeaderInfo ChangeChargeToNegative()
        {
            // For dealing with pre-V11 caches where we didn't record chromatogram polarity
            var chromGroupHeaderInfo = this;
            chromGroupHeaderInfo._flagBits |= (ushort)FlagValues.polarity_negative;
            return chromGroupHeaderInfo;
        }

        private static ushort CheckUShort(int value, bool allowNegativeOne = false)
        {
            return (ushort)CheckValue(value, ushort.MinValue, ushort.MaxValue, allowNegativeOne);
        }

        private static byte CheckByte(int value, int maxValue = byte.MaxValue)
        {
            return (byte)CheckValue(value, byte.MinValue, maxValue);
        }

        private static int CheckValue(int value, int min, int max, bool allowNegativeOne = false)
        {
            if (min > value || value > max)
            {
                if (!allowNegativeOne || value != -1)
                    throw new ArgumentOutOfRangeException(string.Format(@"The value {0} must be between {1} and {2}.", value, min, max)); // CONSIDER: localize?  Does user see this?
            }
            return value;
        }

        public int TextIdIndex { get { return _textIdIndex; } }
        public int StartTransitionIndex { get { return _startTransitionIndex; } }
        public int StartPeakIndex { get { return _startPeakIndex; } }
        public int StartScoreIndex { get { return _startScoreIndex; } }
        public int NumPoints { get { return _numPoints; } }
        public int CompressedSize { get { return _compressedSize; } }
        public ushort FlagBits { get { return _flagBits; } }
        public ushort FileIndex { get {return _fileIndex;} }
        public ushort TextIdLen { get { return _textIdLen; } }
        public ushort NumTransitions { get { return _numTransitions; } }
        public byte NumPeaks { get { return _numPeaks; } }        // The number of peaks stored per chrom should be well under 128
        public ushort StatusId { get { return _statusId; } }
        public ushort StatusRank { get { return _statusRank; } }
        public long LocationPoints { get{return _locationPoints;} }
        public int UncompressedSize { get{return _uncompressedSize;} }
        public bool IsProcessedScans { get { return _isProcessedScans != 0; } }

        public override string ToString()
        {
            return string.Format(@"{0:F04}, {1}, {2}", Precursor, NumTransitions, FileIndex);
        }

        public short MaxPeakIndex
        {
            get
            {
                if (_maxPeakIndex == NO_MAX_PEAK)
                    return -1;
                return _maxPeakIndex;
            }
        }

        public FlagValues Flags { get { return (FlagValues)FlagBits; } }

        public bool HasCalculatedMzs { get { return (Flags & FlagValues.has_calculated_mzs) != 0; } }
        public bool HasMassErrors { get { return (Flags & FlagValues.has_mass_errors) != 0; } }
        public bool HasMs1ScanIds { get { return (Flags & FlagValues.has_ms1_scan_ids) != 0; } }
        public bool HasFragmentScanIds { get { return (Flags & FlagValues.has_frag_scan_ids) != 0; } }
        public bool HasSimScanIds { get { return (Flags & FlagValues.has_sim_scan_ids) != 0; } }
        public bool HasRawChromatograms { get { return (Flags & FlagValues.raw_chromatograms) != 0; } }
        public bool IsDda { get { return (Flags & FlagValues.dda_acquisition_method) != 0; } }

        public float? StartTime { get { return _startTime >= 0 ? _startTime : (float?) null; }  } // For SRM data with same precursor but different RT interval
        public float? EndTime { get { return _endTime >= 0 ? _endTime : (float?)null; } } // For SRM data with same precursor but different RT interval

        public bool HasRawTimes()
        {
            return 0 != (Flags & FlagValues.raw_chromatograms);
        }


        public bool IsNotIncludedTime(double retentionTime)
        {
            return StartTime.HasValue && EndTime.HasValue &&
                   (retentionTime < StartTime.Value || EndTime.Value < retentionTime);
        }

        public bool NegativeCharge
        {
            get { return (Flags & FlagValues.polarity_negative) != 0; }
        }

        public SignedMz Precursor
        {
            get { return new SignedMz(_precursor, NegativeCharge); }
        }

        public float? CollisionalCrossSection
        {
            get
            {
                if (_collisionalCrossSection <= 0)
                    return null;
                return _collisionalCrossSection;
            }
        }

        public eIonMobilityUnits IonMobilityUnits
        {
            get
            {
                return (eIonMobilityUnits)((int)(Flags & FlagValues.ion_mobility_type_bitmask) >> 8);
            }
        }

        public bool HasStatusId { get { return ((short)StatusId) != -1; } }
        public bool HasStatusRank { get { return ((short)StatusRank) != -1; } }

        public ChromExtractor Extractor
        {
            get
            {
                if (Flags.HasFlag(FlagValues.extracted_base_peak)) 
                    return ChromExtractor.base_peak;
                if (Flags.HasFlag(FlagValues.extracted_qc_trace)) 
                    return ChromExtractor.qc;
                return ChromExtractor.summed;
            }
        }

        public void Offset(int offsetFiles, int offsetTransitions, int offsetPeaks, int offsetScores, long offsetPoints)
        {
            _fileIndex += (ushort)offsetFiles;
            _startTransitionIndex += offsetTransitions;
            _startPeakIndex += offsetPeaks;
            if (_startScoreIndex != -1)
                _startScoreIndex += offsetScores;
            _locationPoints += offsetPoints;
        }

        public void ClearScores()
        {
            _startScoreIndex = -1;
        }

        public void CalcTextIdIndex(Target target,
            Dictionary<Target, int> dictTextIdToByteIndex,
            List<byte> listTextIdBytes)
        {
            if (target == null)
            {
                _textIdIndex = -1;
                _textIdLen = 0;
            }
            else
            {
                int textIdIndex;
                var textIdBytes = Encoding.UTF8.GetBytes(target.ToSerializableString());
                if (!dictTextIdToByteIndex.TryGetValue(target, out textIdIndex))
                {
                    textIdIndex = listTextIdBytes.Count;
                    listTextIdBytes.AddRange(textIdBytes);
                    dictTextIdToByteIndex.Add(target, textIdIndex);
                }
                _textIdIndex = textIdIndex;
                _textIdLen = (ushort)textIdBytes.Length;
            }
        }

        public int CompareTo(ChromGroupHeaderInfo info)
        {
            // Sort by key, and then file index.
            int keyCompare = Precursor.CompareTo(info.Precursor);
            if (keyCompare != 0)
                return keyCompare;
            return FileIndex - info.FileIndex;
        }

        #region Fast file I/O

        public static IItemSerializer<ChromGroupHeaderInfo> ItemSerializer(int itemSizeOnDisk)
        {
            StructSerializer<ChromGroupHeaderInfo> structSerializer = new StructSerializer<ChromGroupHeaderInfo>
            {
                ItemSizeOnDisk = itemSizeOnDisk,
                DirectSerializer = DirectSerializer.Create(ReadArray, WriteArray)
            };
            if (itemSizeOnDisk < GetStructSize(CacheFormatVersion.Eleven))
            {
                return ConvertedItemSerializer.Create(structSerializer, chromGroupHeaderInfo =>
                {
                    chromGroupHeaderInfo._uncompressedSize = -1;
                    return chromGroupHeaderInfo;
                }, chromGroupHeaderInfo=>chromGroupHeaderInfo);
            }
            return structSerializer;
        }
        /// <summary>
        /// Direct read of an entire array throw p-invoke of Win32 WriteFile.  This seems
        /// to coexist with FileStream reading that the write version, but its use case
        /// is tightly limited.
        /// <para>
        /// Contributed by Randy Kern.  See:
        /// http://randy.teamkern.net/2009/02/reading-arrays-from-files-in-c-without-extra-copy.html
        /// </para>
        /// </summary>
        /// <param name="file">File handler returned from <see cref="FileStream.SafeFileHandle"/></param>
        /// <param name="count">Number of elements to read</param>
        /// <returns>New array of elements</returns>
        private static unsafe ChromGroupHeaderInfo[] ReadArray(SafeHandle file, int count)
        {
            ChromGroupHeaderInfo[] results = new ChromGroupHeaderInfo[count];
            fixed (ChromGroupHeaderInfo* p = results)
            {
                FastRead.ReadBytes(file, (byte*)p, sizeof(ChromGroupHeaderInfo) * count);
            }

            return results;
        }

        /// <summary>
        /// Direct write of an entire array throw p-invoke of Win32 WriteFile.  This cannot
        /// be mixed with standard writes to a FileStream, or .NET throws an exception
        /// about the file location not being what it expected.
        /// </summary>
        /// <param name="file">File handler returned from <see cref="FileStream.SafeFileHandle"/></param>
        /// <param name="groupHeaders">The array to write</param>
        public static unsafe void WriteArray(SafeHandle file, ChromGroupHeaderInfo[] groupHeaders)
        {
            fixed (ChromGroupHeaderInfo* p = groupHeaders)
            {
                FastWrite.WriteBytes(file, (byte*)p, sizeof(ChromGroupHeaderInfo) * groupHeaders.Length);
            }
        }

        #endregion

        // Set default block size for BlockedArray<ChromTransition>
        public const int DEFAULT_BLOCK_SIZE = 100 * 1024 * 1024;  // 100 megabytes

        public static unsafe int SizeOf
        {
            get { return sizeof(ChromGroupHeaderInfo); }
        }

        // For test purposes
        public static int DeltaSize11
        {
            get
            {
                return Marshal.SizeOf<ChromGroupHeaderInfo>() -
                       (int) Marshal.OffsetOf<ChromGroupHeaderInfo>(@"_uncompressedSize");
            }
        }

        public static int GetStructSize(CacheFormatVersion cacheFormatVersion)
        {
            if (cacheFormatVersion <= CacheFormatVersion.Four)
            {
                return 48;
            }
            if (cacheFormatVersion < CacheFormatVersion.Eleven)
            {
                return 56;
            }
            return 72;
        }
    }

    /// <summary>
    /// Holds a ChromGroupHeaderInfo, and also remembers an index to disambiguate
    /// when two ChromGroupHeaderInfo's compare the same.
    /// </summary>
    public struct ChromGroupHeaderEntry : IComparable<ChromGroupHeaderEntry>
    {
        public ChromGroupHeaderEntry(int index, ChromGroupHeaderInfo chromGroupHeaderInfo) : this()
        {
            Index = index;
            ChromGroupHeaderInfo = chromGroupHeaderInfo;
        }

        public int Index { get; private set; }
        public ChromGroupHeaderInfo ChromGroupHeaderInfo { get; private set; }
        public int CompareTo(ChromGroupHeaderEntry other)
        {
            int result = ChromGroupHeaderInfo.CompareTo(other.ChromGroupHeaderInfo);
            if (result == 0)
            {
                result = Index.CompareTo(other.Index);
            }
            return result;
        }
    }

    public struct ChromTransition4
    {
        public ChromTransition4(float product) : this()
        {
            Product = product;
        }

        public ChromTransition4(ChromTransition chromTransition) : this((float) chromTransition.Product)
        {
            
        }

        public float Product { get; private set; }

        #region Fast file I/O

        public static IItemSerializer<ChromTransition4> StructSerializer()
        {
            return new StructSerializer<ChromTransition4>
            {
                DirectSerializer = DirectSerializer.Create(ReadArray, WriteArray),
            };
        }

        /// <summary>
        /// Direct read of an entire array throw p-invoke of Win32 WriteFile.  This seems
        /// to coexist with FileStream reading that the write version, but its use case
        /// is tightly limited.
        /// <para>
        /// Contributed by Randy Kern.  See:
        /// http://randy.teamkern.net/2009/02/reading-arrays-from-files-in-c-without-extra-copy.html
        /// </para>
        /// </summary>
        /// <param name="file">File handler returned from <see cref="FileStream.SafeFileHandle"/></param>
        /// <param name="count">Number of elements to read</param>
        /// <returns>New array of elements</returns>
        private static unsafe ChromTransition4[] ReadArray(SafeHandle file, int count)
        {
            ChromTransition4[] results = new ChromTransition4[count];
            fixed (ChromTransition4* p = results)
            {
                FastRead.ReadBytes(file, (byte*)p, sizeof(ChromTransition4) * count);
            }

            return results;
        }

        /// <summary>
        /// Direct write of an entire array throw p-invoke of Win32 WriteFile.  This cannot
        /// be mixed with standard writes to a FileStream, or .NET throws an exception
        /// about the file location not being what it expected.
        /// </summary>
        /// <param name="file">File handler returned from <see cref="FileStream.SafeFileHandle"/></param>
        /// <param name="setHeaders">The array to write</param>
        public static unsafe void WriteArray(SafeHandle file, ChromTransition4[] setHeaders)
        {
            fixed (ChromTransition4* p = &setHeaders[0])
            {
                FastWrite.WriteBytes(file, (byte*)p, sizeof(ChromTransition4) * setHeaders.Length);
            }
        }

        #endregion

        #region object overrides

        public override string ToString()
        {
            return Product.ToString(LocalizationHelper.CurrentCulture);
        }

        #endregion
    }

    public struct ChromTransition5
    {
        [Flags]
        public enum FlagValues
        {
            source1 =       0x01,   // unknown = 00, fragment = 01
            source2 =       0x02,   // ms1     = 10, sim      = 11
        }

        public ChromTransition5(double product, float extractionWidth, ChromSource source) : this()
        {
            Product = product;
            ExtractionWidth = extractionWidth;
            Source = source;
            Align1 = 0;
        }

        public ChromTransition5(ChromTransition4 chromTransition4)
            : this(chromTransition4.Product, 0, ChromSource.unknown)
        {            
        }

        public ChromTransition5(ChromTransition chromTransition) : this()
        {
            Product = chromTransition.Product;
            ExtractionWidth = chromTransition.ExtractionWidth;
            Source = chromTransition.Source;
        }

        public double Product { get; private set; }
        public float ExtractionWidth { get; private set; }  // In m/z
        public ushort FlagBits { get; private set; }
        public ushort Align1 { get; private set; }  // Explicitly declaring alignment padding the compiler will add anyway

        public FlagValues Flags { get { return (FlagValues) FlagBits; } }

        public ChromSource Source
        {
            get
            {
                // CONSIDER: Could just mask and cast
                switch (Flags & (FlagValues.source1 | FlagValues.source2))
                {
                    case 0:
                        return ChromSource.unknown;
                    case FlagValues.source2:
                        return ChromSource.fragment;
                    case FlagValues.source1:
                        return ChromSource.ms1;
                    default:
                        return ChromSource.sim;
                }
            }
            set
            {
                FlagBits = (ushort) GetFlags(value);
            }
        }

        public FlagValues GetFlags(ChromSource source)
        {
            // CONSIDER: Could just cast
            switch (source)
            {
                case ChromSource.unknown:
                    return 0;
                case ChromSource.fragment:
                    return FlagValues.source2;
                case ChromSource.ms1:
                    return FlagValues.source1;
                default:
                    return FlagValues.source1 | FlagValues.source2;
            }
        }

        #region Fast file I/O

        public static IItemSerializer<ChromTransition5> StructSerializer()
        {
            return new StructSerializer<ChromTransition5>()
            {
                DirectSerializer = DirectSerializer.Create(ReadArray, WriteArray),
            };
        }

        /// <summary>
        /// A 2x slower version of ReadArray than <see cref="ReadArray(SafeHandle,int)"/>
        /// that does not require a file handle.  This one is covered in Randy Kern's blog,
        /// but is originally from Eric Gunnerson:
        /// <para>
        /// http://blogs.msdn.com/ericgu/archive/2004/04/13/112297.aspx
        /// </para>
        /// </summary>
        /// <param name="stream">Stream to from which to read the elements</param>
        /// <param name="count">Number of elements to read</param>
        /// <returns>New array of elements</returns>
        public static unsafe ChromTransition5[] ReadArray(Stream stream, int count)
        {
            // Use fast version, if this is a file
            var fileStream = stream as FileStream;
            if (fileStream != null)
            {
                try
                {
                    return ReadArray(fileStream.SafeFileHandle, count);
                }
                catch (BulkReadException)
                {
                    // Fall through and attempt to read the slow way
                }
            }

            // CONSIDER: Probably faster in this case to read the entire block,
            //           and convert from bytes to single float values.
            ChromTransition5[] results = new ChromTransition5[count];
            int size = sizeof (ChromTransition5);
            byte[] buffer = new byte[size];
            for (int i = 0; i < count; ++i)
            {
                if (stream.Read(buffer, 0, size) != size)
                    throw new InvalidDataException();

                fixed (byte* pBuffer = buffer)
                {
                    results[i] = *(ChromTransition5*) pBuffer;
                }
            }

            return results;
        }

        /// <summary>
        /// Direct read of an entire array throw p-invoke of Win32 WriteFile.  This seems
        /// to coexist with FileStream reading that the write version, but its use case
        /// is tightly limited.
        /// <para>
        /// Contributed by Randy Kern.  See:
        /// http://randy.teamkern.net/2009/02/reading-arrays-from-files-in-c-without-extra-copy.html
        /// </para>
        /// </summary>
        /// <param name="file">File handler returned from <see cref="FileStream.SafeFileHandle"/></param>
        /// <param name="count">Number of elements to read</param>
        /// <returns>New array of elements</returns>
        private static unsafe ChromTransition5[] ReadArray(SafeHandle file, int count)
        {
            ChromTransition5[] results = new ChromTransition5[count];
            fixed (ChromTransition5* p = results)
            {
                FastRead.ReadBytes(file, (byte*)p, sizeof(ChromTransition5) * count);
            }

            return results;
        }

        /// <summary>
        /// Direct write of an entire array throw p-invoke of Win32 WriteFile.  This cannot
        /// be mixed with standard writes to a FileStream, or .NET throws an exception
        /// about the file location not being what it expected.
        /// </summary>
        /// <param name="file">File handler returned from <see cref="FileStream.SafeFileHandle"/></param>
        /// <param name="setHeaders">The array to write</param>
        public static unsafe void WriteArray(SafeHandle file, ChromTransition5[] setHeaders)
        {
            fixed (ChromTransition5* p = &setHeaders[0])
            {
                FastWrite.WriteBytes(file, (byte*)p, sizeof(ChromTransition5) * setHeaders.Length);
            }
        }

        #endregion

        #region object overrides

        /// <summary>
        /// For debugging only
        /// </summary>
        public override string ToString()
        {
            return string.Format(@"{0:F04} - {1}", Product, Source);
        }

        #endregion

        public static unsafe int DeltaSize5
        {
            get { return sizeof(ChromTransition5) - sizeof(ChromTransition4); }
        }
    }

    /// <summary>
    /// Version 8 of ChromTransition adds ion mobility information
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack=4)]
    public struct ChromTransition
    {
        private double _product;
        private float _extractionWidth;
        private float _ionMobilityValue;
        private float _ionMobilityExtractionWidth;
        private ushort _flagBits;
        private ushort _align1;
        [Flags]
        public enum FlagValues
        {
            unknown = 0x00,
            ms1 = 0x01,
            fragment = 0x02,
            sim = 0x03,

            missing_mass_errors = 0x04,
        }

        const FlagValues MASK_SOURCE = (FlagValues) 0x03;

        public ChromTransition(double product, float extractionWidth, float ionMobilityValue, float ionMobilityExtractionWidth, ChromSource source) : this()
        {
            _product = product;
            _extractionWidth = extractionWidth;
            _ionMobilityValue = ionMobilityValue;
            _ionMobilityExtractionWidth = ionMobilityExtractionWidth;
            Source = source;
        }

        public ChromTransition(ChromTransition5 chromTransition5) : this(chromTransition5.Product,
            // There was an issue with Manage Results > Rescore, which made it possible to corrupt
            // the chromatogram source until a commit by Nick in March 2014, and Brian introduced
            // the next version of this struct in the May, 2014. So considering the source unknown
            // for these older files seems safest, since we are moving to paying attention to the
            // source for chromatogram to transition matching.
            chromTransition5.ExtractionWidth, 0, 0, ChromSource.unknown)
        {            
        }

        public ChromTransition(ChromTransition4 chromTransition4)
            : this(chromTransition4.Product, 0, 0, 0, ChromSource.unknown)
        {
        }

        public double Product { get { return _product; } }
        public float ExtractionWidth { get { return _extractionWidth; }}  // In m/z
        public float IonMobilityValue { get { return _ionMobilityValue; } } // Units depend on ion mobility type
        public float IonMobilityExtractionWidth { get { return _ionMobilityExtractionWidth; } } // Units depend on ion mobility type

        public FlagValues Flags
        {
            get { return (FlagValues) _flagBits; }
            set { _flagBits = (ushort) value; }
        }

        public ChromSource Source
        {
            get
            {
                switch (Flags & MASK_SOURCE)
                {
                    case FlagValues.unknown:
                        return ChromSource.unknown;
                    case FlagValues.fragment:
                        return ChromSource.fragment;
                    case FlagValues.ms1:
                        return ChromSource.ms1;
                    default:
                        return ChromSource.sim;
                }
            }
            set
            {
                Flags = GetSourceFlags(value) | (Flags & ~MASK_SOURCE);
            }
        }

        public bool MissingMassErrors
        {
            get { return (Flags & FlagValues.missing_mass_errors) != 0; }
            set { Flags = (Flags & ~FlagValues.missing_mass_errors) | (value ? FlagValues.missing_mass_errors : 0); }
        }

        public static FlagValues GetSourceFlags(ChromSource source)
        {
            switch (source)
            {
                case ChromSource.unknown:
                    return FlagValues.unknown;
                case ChromSource.fragment:
                    return FlagValues.fragment;
                case ChromSource.ms1:
                    return FlagValues.ms1;
                default:
                    return FlagValues.sim;
            }
        }

        // Set default block size for BlockedArray<ChromTransition>
        public const int DEFAULT_BLOCK_SIZE = 100 * 1024 * 1024;  // 100 megabytes

        // sizeof(ChromPeak)
        public static int SizeOf
        {
            get { unsafe { return sizeof(ChromTransition); } }
        }

        #region Fast file I/O

        public static StructSerializer<ChromTransition> StructSerializer(int structSizeOnDisk)
        {
            return new StructSerializer<ChromTransition>()
            {
                DirectSerializer = DirectSerializer.Create(ReadArray, null),
                ItemSizeOnDisk = structSizeOnDisk,
            };
        }

        /// <summary>
        /// Direct read of an entire array throw p-invoke of Win32 WriteFile.  This seems
        /// to coexist with FileStream reading that the write version, but its use case
        /// is tightly limited.
        /// <para>
        /// Contributed by Randy Kern.  See:
        /// http://randy.teamkern.net/2009/02/reading-arrays-from-files-in-c-without-extra-copy.html
        /// </para>
        /// </summary>
        /// <param name="file">File handler returned from <see cref="FileStream.SafeFileHandle"/></param>
        /// <param name="count">Number of elements to read</param>
        /// <returns>New array of elements</returns>
        private static unsafe ChromTransition[] ReadArray(SafeHandle file, int count)
        {
            ChromTransition[] results = new ChromTransition[count];
            fixed (ChromTransition* p = results)
            {
                FastRead.ReadBytes(file, (byte*)p, sizeof(ChromTransition) * count);
            }

            return results;
        }

        public static ChromTransition[] ReadArray(Stream stream, int count)
        {
            return new StructSerializer<ChromTransition>().ReadArray(stream, count);
        }

        public static int GetStructSize(CacheFormatVersion cacheFormatVersion)
        {
            if (cacheFormatVersion < CacheFormatVersion.Five)
            {
                return 4;
            }
            if (cacheFormatVersion <= CacheFormatVersion.Six)
            {
                return 16;
            }
            return 24;
        }

        //
        // NOTE: writing is handled by ChromatogramCache::WriteStructs, so any members
        // added here need to be added there - and in the proper order!
        // 

        #endregion

        #region object overrides

        /// <summary>
        /// For debugging only
        /// </summary>
        public override string ToString()
        {
            return string.Format(@"{0:F04} {1:F04} - {2}", Product, IonMobilityValue, Source);
        }

        #endregion



    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct ChromPeak : ISummaryPeakData
    {
        private readonly float _retentionTime;
        private readonly float _startTime;
        private readonly float _endTime;
        private readonly float _area;
        private readonly float _backgroundArea;
        private readonly float _height;
        private readonly float _fwhm;
        private uint _flagBits;
        private readonly short _pointsAcross;

        [Flags]
        public enum FlagValues
        {
            degenerate_fwhm =       0x0001,
            forced_integration =    0x0002,
            time_normalized =       0x0004,
            peak_truncation_known = 0x0008,
            peak_truncated =        0x0010,
            contains_id =           0x0020,
            used_id_alignment =     0x0040,

            // This is the last available flag
            // The high word of the flags is reserved for delta-mass-error
            mass_error_known =      0x8000,
        }

// ReSharper disable InconsistentNaming
// ReSharper disable UnassignedField.Global
        public static ChromPeak EMPTY;  // Zero filled struct
// ReSharper restore UnassignedField.Global
// ReSharper restore InconsistentNaming

        // Set default block size for BlockedArray<ChromPeak>
        public const int DEFAULT_BLOCK_SIZE = 100*1024*1024;  // 100 megabytes

        // sizeof(ChromPeak)
        public static int SizeOf
        {
            get { unsafe { return sizeof (ChromPeak); } }
        }

        public static short To10x(double f)
        {
            return (short) Math.Round(f*10);
        }

        public ChromPeak(IPeakFinder finder,
                         IFoundPeak peak,
                         FlagValues flags,
                         TimeIntensities timeIntensities,
                         IList<float> rawTimes)
            : this()
        {
            var times = timeIntensities.Times;
            var intensities = timeIntensities.Intensities;
            var massErrors = timeIntensities.MassErrors;
            // Get the interval being used to convert from Crawdad index based numbers
            // to numbers that are normalized with respect to time.
            double interval;
            if (peak.StartIndex + 1 < timeIntensities.NumPoints)
            {
                interval = times[peak.StartIndex + 1] - times[peak.StartIndex];
            }
            else
            {
                interval = 0;
            }

            _retentionTime = times[peak.TimeIndex];
            _startTime = times[peak.StartIndex];
            _endTime = times[peak.EndIndex];

            if ((flags & FlagValues.time_normalized) == 0 || finder.IsHeightAsArea)
            {
                _area = peak.Area;
                _backgroundArea = peak.BackgroundArea;
            }
            else
            {
                // Normalize area numbers by time in seconds, since this will be the least
                // dramatic change from Skyline v0.5, when the Crawdad index based areas
                // were used directly.
                double intervalSeconds = interval * 60;

                _area = (float)(peak.Area * intervalSeconds);
                _backgroundArea = (float) (peak.BackgroundArea * intervalSeconds);
            }
            _height = peak.Height;
            _fwhm = (float) (peak.Fwhm * interval);
            if (float.IsNaN(Fwhm))
                _fwhm = 0;
            if (peak.FwhmDegenerate)
                flags |= FlagValues.degenerate_fwhm;

            // Calculate peak truncation as a peak extent at either end of the
            // recorded values, where the intensity is higher than the other extent
            // by more than 1% of the peak height.
            flags |= FlagValues.peak_truncation_known;
            const double truncationTolerance = 0.01;
            double deltaIntensityExtents = (intensities[peak.EndIndex] - intensities[peak.StartIndex]) / Height;
            if ((peak.StartIndex == 0 && deltaIntensityExtents < -truncationTolerance) ||
                (peak.EndIndex == times.Count - 1 && deltaIntensityExtents > truncationTolerance))
            {
                flags |= FlagValues.peak_truncated;
            }
            if (massErrors != null)
            {
                // Mass error is mean of mass errors in the peak, weighted by intensity
                double massError = 0;
                double totalIntensity = 0;
                // Subtract background intensity to reduce noise contribution to this mean value
                double backgroundIntensity = Math.Min(intensities[peak.StartIndex], intensities[peak.EndIndex]);
                for (int i = peak.StartIndex; i <= peak.EndIndex; i++)
                {
                    double intensity = intensities[i] - backgroundIntensity;
                    if (intensity <= 0)
                        continue;

                    double massErrorLocal = massErrors[i];
                    totalIntensity += intensity;
                    massError += (massErrorLocal - massError)*intensity/totalIntensity;
                }
                // Only if intensity exceded the background at least once
                if (totalIntensity > 0)
                {
                    flags |= FlagValues.mass_error_known;
                    FlagBits = ((uint)To10x(massError)) << 16;
                }
            }
            FlagBits |= (uint) flags;
            if (rawTimes != null)
            {
                int startIndex = CollectionUtil.BinarySearch(rawTimes, StartTime);
                if (startIndex < 0)
                {
                    startIndex = ~startIndex;
                }
                int endIndex = CollectionUtil.BinarySearch(rawTimes, EndTime);
                if (endIndex < 0)
                {
                    endIndex = ~endIndex - 1;
                }
                int pointsAcross = endIndex - startIndex + 1;
                if (pointsAcross >= 0)
                {
                    _pointsAcross = (short) Math.Min(pointsAcross, ushort.MaxValue);
                }
            }
        }

        public float RetentionTime { get { return _retentionTime; } }
        public float StartTime { get { return _startTime; } }
        public float EndTime { get { return _endTime; } }
        public float Area { get { return _area; } }
        public float BackgroundArea { get { return _backgroundArea; } }
        public float Height { get { return _height; } }
        public float Fwhm { get { return _fwhm; } }
        public uint FlagBits { get { return _flagBits; } private set { _flagBits = value; } }
        public short? PointsAcross { get { return _pointsAcross == 0 ? (short?)null : _pointsAcross; } }

        public override string ToString()
        {
            return string.Format(@"rt={0:F02}, area={1}", RetentionTime, Area);
        }

        public FlagValues Flags
        {
            get
            {
                // Mask off mass error bits
                return (FlagValues) (FlagBits & 0xFFFF);
            }
        }

        public bool IsEmpty { get { return EndTime == 0; } }

        public bool ContainsTime(float retentionTime)
        {
            return StartTime <= retentionTime && retentionTime <= EndTime;
        }

        public bool IsFwhmDegenerate
        {
            get { return (Flags & FlagValues.degenerate_fwhm) != 0; }
        }

        public bool IsForcedIntegration
        {
            get { return (Flags & FlagValues.forced_integration) != 0; }
        }

        public PeakIdentification Identified
        {
            get
            {
                if ((Flags & FlagValues.contains_id) == 0)
                    return PeakIdentification.FALSE;
                else if ((Flags & FlagValues.used_id_alignment) == 0)
                    return PeakIdentification.TRUE;
                return PeakIdentification.ALIGNED;
            }
        }

        public bool? IsTruncated
        {
            get
            {
                if ((Flags & FlagValues.peak_truncation_known) == 0)
                    return null;
                return (Flags & FlagValues.peak_truncated) != 0;
            }
        }

        public float? MassError
        {
            get
            {
                if ((FlagBits & (uint) FlagValues.mass_error_known) == 0)
                    return null;
                // Mass error is stored in the high 16 bits of the Flags
                // as 10x the calculated mass error in PPM.
                return ((short)(FlagBits >> 16))/10f;
            }
        }

        /// <summary>
        /// Removes the mass error bits from the upper 16 in order to keep
        /// from writing mass errors into older cache file formats until
        /// the v5 format version is ready.
        /// </summary>
        public ChromPeak RemoveMassError()
        {
            var copy = this;
            copy.FlagBits = (uint) (Flags & ~FlagValues.mass_error_known);
            return copy;
        }

        public static float Intersect(ChromPeak peak1, ChromPeak peak2)
        {
            return Intersect(peak1.StartTime, peak1.EndTime, peak2.StartTime, peak2.EndTime);
        }

        public static float Intersect(float startTime1, float endTime1, float startTime2, float endTime2)
        {
            return Math.Min(endTime1, endTime2) - Math.Max(startTime1, startTime2);
        }

        public static int GetStructSize(CacheFormatVersion formatVersion)
        {
            if (formatVersion < CacheFormatVersion.Twelve)
            {
                return 32;
            }
            return 36;
        }

        public static StructSerializer<ChromPeak> StructSerializer(int chromPeakSize)
        {
            return new StructSerializer<ChromPeak>
            {
                ItemSizeOnDisk = chromPeakSize,
                DirectSerializer = DirectSerializer.Create(ReadArray, WriteArray)
            };
        }

        #region Fast file I/O

        /// <summary>
        /// Direct read of an entire array throw p-invoke of Win32 WriteFile.  This seems
        /// to coexist with FileStream reading that the write version, but its use case
        /// is tightly limited.
        /// <para>
        /// Contributed by Randy Kern.  See:
        /// http://randy.teamkern.net/2009/02/reading-arrays-from-files-in-c-without-extra-copy.html
        /// </para>
        /// </summary>
        /// <param name="file">File handler returned from <see cref="FileStream.SafeFileHandle"/></param>
        /// <param name="count">Number of elements to read</param>
        /// <returns>New array of elements</returns>
        private static unsafe ChromPeak[] ReadArray(SafeHandle file, int count)
        {
            ChromPeak[] results = new ChromPeak[count];
            if (count > 0)
            {
                fixed (ChromPeak* p = results)
                {
                    FastRead.ReadBytes(file, (byte*)p, sizeof(ChromPeak) * count);
                }
            }

            return results;
        }

        /// <summary>
        /// Direct write of an entire array throw p-invoke of Win32 WriteFile.  This cannot
        /// be mixed with standard writes to a FileStream, or .NET throws an exception
        /// about the file location not being what it expected.
        /// </summary>
        /// <param name="file">File handler returned from <see cref="FileStream.SafeFileHandle"/></param>
        /// <param name="headers">The array to write</param>
        public static unsafe void WriteArray(SafeHandle file, ChromPeak[] headers)
        {
            fixed (ChromPeak* p = headers)
            {
                FastWrite.WriteBytes(file, (byte*)p, sizeof(ChromPeak) * headers.Length);
            }
        }

        public static byte[] GetBytes(ChromPeak p)
        {
            int size = Marshal.SizeOf(p);
            byte[] arr = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);

            Marshal.StructureToPtr(p, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);

            return arr;            
        }

        #endregion
    }

    public class ChromCachedFile : Immutable, IPathContainer
    {
        [Flags]
        public enum FlagValues
        {
            single_match_mz_known = 0x01,
            single_match_mz = 0x02,
            has_midas_spectra = 0x04,
            has_combined_ion_mobility = 0x08,
            ion_mobility_type_bitmask = 0x70, // 3 bits for ion mobility type drift, inverse_mobility, spares
            // 0x80 available
            used_ms1_centroids = 0x100,
            used_ms2_centroids = 0x200,
        }

        public static DateTime GetLastWriteTime(MsDataFileUri filePath)
        {
            return filePath.GetFileLastWriteTime();
        }

        public static bool? IsSingleMatchMzFlags(FlagValues flags)
        {
            if ((flags & FlagValues.single_match_mz_known) == 0)
                return null;
            return (flags & FlagValues.single_match_mz) != 0;            
        }

        private static bool HasMidasSpectraFlags(FlagValues flags)
        {
            return (flags & FlagValues.has_midas_spectra) != 0;
        }

        private static bool HasCombinedIonMobilityFlags(FlagValues flags)
        {
            return (flags & FlagValues.has_combined_ion_mobility) != 0;
        }

        public static eIonMobilityUnits IonMobilityUnitsFromFlags(FlagValues flags)
        {
            return (eIonMobilityUnits)((int)(flags & FlagValues.ion_mobility_type_bitmask) >> 4);
        }

        private static bool UsedMs1CentroidsFlags(FlagValues flags)
        {
            return (flags & FlagValues.used_ms1_centroids) != 0;
        }

        private static bool UsedMs2CentroidsFlags(FlagValues flags)
        {
            return (flags & FlagValues.used_ms2_centroids) != 0;
        }

        public ChromCachedFile(MsDataFileUri filePath, FlagValues flags, DateTime fileWriteTime, DateTime? runStartTime,
                               float maxRT, float maxIntensity, eIonMobilityUnits ionMobilityUnits, string sampleId, string serialNumber,
                               IEnumerable<MsInstrumentConfigInfo> instrumentInfoList)
            : this(filePath, flags, fileWriteTime, runStartTime, maxRT, maxIntensity, 0, 0, default(float?), ionMobilityUnits, sampleId, serialNumber, instrumentInfoList)
        {
        }

        public ChromCachedFile(MsDataFileUri fileUri,
                               FlagValues flags,
                               DateTime fileWriteTime,
                               DateTime? runStartTime,
                               float maxRT,
                               float maxIntensity,
                               int sizeScanIds,
                               long locationScanIds,
                               float? ticArea,
                               eIonMobilityUnits ionMobilityUnits,
                               string sampleId,
                               string instrumentSerialNumber,
                               IEnumerable<MsInstrumentConfigInfo> instrumentInfoList)
        {
            // BACKWARD COMPATIBILITY: Deal with legacy parameters which got stored on the file_path URI
            var filePath = fileUri as MsDataFilePath;
            if (filePath != null && filePath.LegacyCombineIonMobilitySpectra) // Skyline-daily 19.1.9.338 or 350
                flags |= FlagValues.has_combined_ion_mobility;
            // Centroiding for a much longer time
            if (fileUri.LegacyGetCentroidMs1())
                flags |= FlagValues.used_ms1_centroids;
            if (fileUri.LegacyGetCentroidMs2())
                flags |= FlagValues.used_ms2_centroids;
            FilePath = fileUri.RemoveLegacyParameters();
            Flags = (flags & ~FlagValues.ion_mobility_type_bitmask) | (FlagValues)((int)ionMobilityUnits << 4);
            FileWriteTime = fileWriteTime;
            RunStartTime = runStartTime;
            MaxRetentionTime = maxRT;
            MaxIntensity = maxIntensity;
            SizeScanIds = sizeScanIds;
            LocationScanIds = locationScanIds;
            TicArea = ticArea;
            SampleId = sampleId;
            InstrumentSerialNumber = instrumentSerialNumber;
            InstrumentInfoList = ImmutableList.ValueOf(instrumentInfoList) ?? ImmutableList<MsInstrumentConfigInfo>.EMPTY;
        }

        public MsDataFileUri FilePath { get; private set; }
        public FlagValues Flags { get; private set; }
        public DateTime FileWriteTime { get; private set; }
        public DateTime? RunStartTime { get; private set; }
        public float MaxRetentionTime { get; private set; }
        public float MaxIntensity { get; private set; }
        public int SizeScanIds { get; private set; }
        public long LocationScanIds { get; private set; }
        public ImmutableList<MsInstrumentConfigInfo> InstrumentInfoList { get; private set; }
        public float? TicArea { get; private set; }
        public eIonMobilityUnits IonMobilityUnits { get { return IonMobilityUnitsFromFlags(Flags); } }
        public string SampleId { get; private set; }
        public string InstrumentSerialNumber { get; private set; }

        public bool IsCurrent
        {
            get { return Equals(FileWriteTime, GetLastWriteTime(FilePath)); }
        }

        public bool? IsSingleMatchMz
        {
            get { return IsSingleMatchMzFlags(Flags); }
        }

        public bool HasMidasSpectra
        {
            get { return HasMidasSpectraFlags(Flags); }
        }

        public bool HasCombinedIonMobility
        {
            get { return HasCombinedIonMobilityFlags(Flags); }
        }

        public bool UsedMs1Centroids
        {
            get { return UsedMs1CentroidsFlags(Flags); }
        }

        public bool UsedMs2Centroids
        {
            get { return UsedMs2CentroidsFlags(Flags); }
        }

        public ChromCachedFile RelocateScanIds(long locationScanIds)
        {
            return ChangeProp(ImClone(this), im => im.LocationScanIds = locationScanIds);
        }

        public ChromCachedFile ChangeTicArea(float? ticArea)
        {
            return ChangeProp(ImClone(this), im => im.TicArea = ticArea);
        }

        public ChromCachedFile ChangeFilePath(MsDataFileUri filePath)
        {
            return ChangeProp(ImClone(this), im => im.FilePath = filePath);
        }

        public ChromCachedFile ChangeSampleId(string sampleId)
        {
            return ChangeProp(ImClone(this), im => im.SampleId = sampleId);
        }

        public ChromCachedFile ChangeSerialNumber(string serialNumber)
        {
            return ChangeProp(ImClone(this), im => im.InstrumentSerialNumber = serialNumber);
        }
    }

    /// <summary>
    /// A utility class that provides two methods. One for converting a collection of 
    /// MsInstrumentConfigInfo objects into a string representation that can be written
    /// to the chromatogram cache file.
    /// The second method takes the string representation and parses the instrument information.
    /// </summary>
    public static class InstrumentInfoUtil
    {
        // Used for cache and testing
        public const string MODEL = "MODEL:";
        public const string ANALYZER = "ANALYZER:";
        public const string DETECTOR = "DETECTOR:";
        public const string IONIZATION = "IONIZATION:";

        public static IEnumerable<MsInstrumentConfigInfo> GetInstrumentInfo(string infoString)
        {
            if (String.IsNullOrEmpty(infoString))
            {
                return Enumerable.Empty<MsInstrumentConfigInfo>();
            }

            IList<MsInstrumentConfigInfo> instrumentConfigList = new List<MsInstrumentConfigInfo>();

            using (StringReader reader = new StringReader(infoString))
            {
                MsInstrumentConfigInfo instrumentInfo;
                while (ReadInstrumentConfig(reader, out instrumentInfo))
                {
                    if(!instrumentInfo.IsEmpty)
                        instrumentConfigList.Add(instrumentInfo);
                }
            }
            return instrumentConfigList;
        }

        private static bool ReadInstrumentConfig(TextReader reader, out MsInstrumentConfigInfo instrumentInfo)
        {
            string model = null;
            string ionization = null;
            string analyzer = null;
            string detector = null;

            string line;
            bool readLine = false;
            while((line = reader.ReadLine()) != null)
            {
                readLine = true;

                if (Equals(string.Empty, line.Trim())) // We have come too far
                    break;

                if (line.StartsWith(MODEL))
                {
                    model =  line.Substring(MODEL.Length);
                }
                else if (line.StartsWith(IONIZATION))
                {
                    ionization = line.Substring(IONIZATION.Length);
                }
                else if (line.StartsWith(ANALYZER))
                {
                    analyzer = line.Substring(ANALYZER.Length);
                }
                else if (line.StartsWith(DETECTOR))
                {
                    detector = line.Substring(DETECTOR.Length);
                }
                else
                {
                    throw new IOException(string.Format(Resources.InstrumentInfoUtil_ReadInstrumentConfig_Unexpected_line_in_instrument_config__0__, line));
                }
            }

            if(readLine)
            {
                instrumentInfo = new MsInstrumentConfigInfo(model, ionization, analyzer, detector);
                return true;
            }
            instrumentInfo = null;
            return false;
        }

        public static string GetInstrumentInfoString(IEnumerable<MsInstrumentConfigInfo> instrumentConfigList)
        {
            if (instrumentConfigList == null)
                return string.Empty;

            StringBuilder infoString = new StringBuilder();

            foreach (var configInfo in instrumentConfigList)
            {
                if (configInfo == null || configInfo.IsEmpty)
                    continue;

				if (infoString.Length > 0)
	                infoString.Append('\n');

                // instrument model
                if(!string.IsNullOrWhiteSpace(configInfo.Model))
                {
                    infoString.Append(MODEL).Append(configInfo.Model).Append('\n');
                }

                // ionization type
                if(!string.IsNullOrWhiteSpace(configInfo.Ionization))
                {
                    infoString.Append(IONIZATION).Append(configInfo.Ionization).Append('\n');
                }

                // analyzer
                if (!string.IsNullOrWhiteSpace(configInfo.Analyzer))
                {
                    infoString.Append(ANALYZER).Append(configInfo.Analyzer).Append('\n');
                }

                // detector
                if(!string.IsNullOrWhiteSpace(configInfo.Detector))
                {
                    infoString.Append(DETECTOR).Append(configInfo.Detector).Append('\n');
                }
            }
            
            return infoString.ToString();
        }
    }

    public interface IPathContainer
    {
        MsDataFileUri FilePath { get; }
    }

    public class PathComparer<TItem> : IEqualityComparer<TItem>
        where TItem : IPathContainer
    {
        public bool Equals(TItem f1, TItem f2)
        {
            if (ReferenceEquals(f1, null) || ReferenceEquals(f2, null))
            {
                return ReferenceEquals(f1, null) && ReferenceEquals(f2, null);
            }
            return Equals(f1.FilePath, f2.FilePath);
        }

        public int GetHashCode(TItem f)
        {
            return f.FilePath.GetHashCode();
        }
    }

    public enum ChromSource { fragment, sim, ms1, unknown  }

    public enum ChromExtractor { summed, base_peak, qc }

    public class ChromKey : IComparable<ChromKey>
    {
        public static readonly ChromKey EMPTY = new ChromKey(null, SignedMz.ZERO, null,
            SignedMz.ZERO, 0, 0, ChromSource.unknown, ChromExtractor.summed, false, false, null, null);

        public ChromKey(byte[] textIdBytes,
                        int textIdIndex,
                        int textIdLen,
                        SignedMz precursor,
                        SignedMz product,
                        double extractionWidth,
                        IonMobilityFilter ionMobility,
                        ChromSource source,
                        ChromExtractor extractor,
                        bool calculatedMzs,
                        bool hasScanIds,
                        double? optionalMinTime,
                        double? optionalMaxTime,
                        double? optionalCenterOfGravityTime = null)
            : this(textIdIndex != -1 ? Target.FromSerializableString(Encoding.UTF8.GetString(textIdBytes, textIdIndex, textIdLen)) : null,
                   precursor,
                   ionMobility,
                   product,
                   0,
                   extractionWidth,
                   source,
                   extractor,
                   calculatedMzs,
                   hasScanIds,
                   optionalMinTime,
                   optionalMaxTime,
                   optionalCenterOfGravityTime)
        {
        }

        public ChromKey(Target target,
                        SignedMz precursor,
                        IonMobilityFilter ionMobilityFilter,
                        SignedMz product,
                        double ceValue,
                        double extractionWidth,
                        ChromSource source,
                        ChromExtractor extractor,
                        bool calculatedMzs,
                        bool hasScanIds,
                        double? optionalMinTime,
                        double? optionalMaxTime,
                        double? optionalCenterOfGravityTime = null)
        {
            Target = target;
            Precursor = precursor;
            IonMobilityFilter = ionMobilityFilter ?? IonMobilityFilter.EMPTY;
            Product = product;
            CollisionEnergy = (float) ceValue;
            ExtractionWidth = (float) extractionWidth;
            Source = source;
            Extractor = extractor;
            HasCalculatedMzs = calculatedMzs;
            HasScanIds = hasScanIds;
            OptionalMinTime = optionalMinTime;
            OptionalMaxTime = optionalMaxTime;
            OptionalCenterOfGravityTime = optionalCenterOfGravityTime;
            // Calculating these values on the fly shows up in a profiler in the CompareTo function
            // So, probably not worth the space saved in this class
            IsEmpty = Precursor == 0 && Product == 0 && source == ChromSource.unknown;
            if (OptionalMaxTime.HasValue && OptionalMinTime.HasValue && (OptionalMaxTime > OptionalMinTime))
                OptionalMidTime = (OptionalMaxTime.Value + OptionalMinTime.Value) / 2;
        }

        public Target Target { get; private set; }  // Modified sequence or custom ion id
        public SignedMz Precursor { get; private set; }
        public double? CollisionalCrossSectionSqA { get { return IonMobilityFilter == null ? null : IonMobilityFilter.CollisionalCrossSectionSqA; }  }
        public eIonMobilityUnits IonMobilityUnits { get { return IonMobilityFilter == null ? eIonMobilityUnits.none : IonMobilityFilter.IonMobility.Units; } }
        public IonMobilityFilter IonMobilityFilter { get; private set; }
        public SignedMz Product { get; private set; }
        public float CollisionEnergy { get; private set; }
        public float ExtractionWidth { get; private set; }
        public ChromSource Source { get; private set; }
        public ChromExtractor Extractor { get; private set; }
        public bool HasCalculatedMzs { get; private set; }
        public bool HasScanIds { get; private set; }
        public bool IsEmpty { get; private set; }
        public double? OptionalMinTime { get; private set; }
        public double? OptionalMaxTime { get; private set; }
        public double? OptionalCenterOfGravityTime { get; private set; } // Only used in SRM, to help disambiguate chromatograms with same Q1>Q3 but different retention time intervals
        public double? OptionalMidTime { get; private set; }

        /// <summary>
        /// Adjust the product m/z to look like it does for vendors that allow
        /// product m/z shifting for parameter optimization.
        /// </summary>
        /// <param name="step">The step from the central predicted parameter value</param>
        /// <returns>A new ChromKey with adjusted product m/z and cleared CE value</returns>
        public ChromKey ChangeOptimizationStep(int step)
        {
            return new ChromKey(Target,
                                Precursor,
                                IonMobilityFilter,
                                Product + step*ChromatogramInfo.OPTIMIZE_SHIFT_SIZE,
                                0,
                                ExtractionWidth,
                                Source,
                                Extractor,
                                HasCalculatedMzs,
                                HasScanIds,
                                OptionalMinTime,
                                OptionalMaxTime,
                                OptionalCenterOfGravityTime);
        }

        public ChromKey ChangeOptionalTimes(double? start, double? end, double? centerOfGravity)
        {
            return new ChromKey(Target,
                                Precursor,
                                IonMobilityFilter,
                                Product,
                                CollisionEnergy,
                                ExtractionWidth,
                                Source,
                                Extractor,
                                HasCalculatedMzs,
                                HasScanIds,
                                start,
                                end,
                                centerOfGravity);
        }

        /// <summary>
        /// For debugging only
        /// </summary>
        public override string ToString()
        {
            if (Target != null)
                return string.Format(@"{0:F04}, {1:F04} {4} - {2} - {3}", Precursor.RawValue, Product.RawValue, Source, Target, IonMobilityFilter);
            return string.Format(@"{0:F04}, {1:F04} {3} - {2}", Precursor.RawValue, Product.RawValue, Source, IonMobilityFilter);
        }

        public int CompareTo(ChromKey key)
        {
            // First deal with empty keys sorting to the end
            if (IsEmpty)
                return key.IsEmpty ? 0 : 1;
            if (key.IsEmpty)
                return -1;

            // Order by precursor values
            var c = ComparePrecursors(key);
            if (c != 0)
                return c;

            // Order by scan-type source, product m/z, extraction width
            c = CompareSource(key);
            if (c != 0)
                return c;
            c = Product.CompareTo(key.Product);
            if (c != 0)
                return c;
            c = CollisionEnergy.CompareTo(key.CollisionEnergy);
            if (c != 0)
                return c;
            return ExtractionWidth.CompareTo(key.ExtractionWidth);

            // CONSIDER(bspratt) - we're currently ignoring ion mobility for comparison
        }

        public int ComparePrecursors(ChromKey key)
        {
            // Order by precursor m/z, peptide sequence/custom ion id, extraction method
            // For SRM data, do not group discontiguous chromotagrams
            int c = Precursor.CompareTo(key.Precursor);
            if (c != 0)
                return c;
            c = CompareTarget(key);
            if (c != 0)
                return c;
            return Extractor.CompareTo(key.Extractor);
        }

        private int CompareTarget(ChromKey key)
        {
            if (Target != null && key.Target != null)
            {
                int c = Target.CompareTo(key.Target);
                if (c != 0)
                    return c;
            }
            else if (Target != null)
                return 1;
            else if (key.Target != null)
                return -1;
            return 0;   // both null
        }

        public int CompareSource(ChromKey key)
        {
            // Sort with all unknown sources after all known sources
            if (Source != ChromSource.unknown && key.Source != ChromSource.unknown)
                return Source.CompareTo(key.Source);
            // Flip comparison to put the known value first
            return key.Source.CompareTo(Source);
        }

        private const string SUFFIX_CE = "CE=";

        private static readonly Regex REGEX_ABI = new Regex(@"Q1=([^ ]+) Q3=([^ ]+) ");

        public static bool IsKeyId(string id)
        {
            return MsDataFileImpl.IsSingleIonCurrentId(id); // || id.StartsWith(PREFIX_TOTAL); Skip the TICs, since Skyline calculates these
        }

        public static ChromKey FromId(string idIn, bool parseCE)
        {
            try
            {
                double precursor, product;
                var isNegativeChargeNullable = MsDataFileImpl.IsNegativeChargeIdNullable(idIn);
                bool isNegativeCharge = isNegativeChargeNullable ?? false;
                var id = isNegativeChargeNullable.HasValue ? idIn.Substring(2) : idIn;
                var source = ChromSource.fragment;
                var extractor = ChromExtractor.summed;
                if (id == MsDataFileImpl.TIC)
                {
                    precursor = product = 0;
                    source = ChromSource.unknown;
                }
                else if (id == MsDataFileImpl.BPC)
                {
                    precursor = product = 0;
                    extractor = ChromExtractor.base_peak;
                    source = ChromSource.unknown;
                }
                else if (id.StartsWith(MsDataFileImpl.PREFIX_TOTAL))
                {
                    precursor = double.Parse(id.Substring(MsDataFileImpl.PREFIX_TOTAL.Length), CultureInfo.InvariantCulture);
                    product = 0;
                }
                else if (id.StartsWith(MsDataFileImpl.PREFIX_PRECURSOR))
                {
                    precursor = double.Parse(id.Substring(MsDataFileImpl.PREFIX_TOTAL.Length), CultureInfo.InvariantCulture);
                    product = precursor;
                }
                else if (id.StartsWith(MsDataFileImpl.PREFIX_SINGLE))
                {
                    // Remove the prefix
                    string mzPart = id.Substring(MsDataFileImpl.PREFIX_SINGLE.Length);

                    // Check of ABI id format match
                    string[] mzs;
                    Match match = REGEX_ABI.Match(mzPart);
                    if (match.Success)
                    {
                        mzs = new[] {match.Groups[1].Value, match.Groups[2].Value};
                    }
                    // Try simpler comma separated format (Thermo)
                    else
                    {
                        mzs = mzPart.Split(new[] { ',' });
                        if (mzs.Length != 2)
                        {
                            throw new InvalidDataException(
                                string.Format(Resources.ChromKey_FromId_Invalid_chromatogram_ID__0__found_The_ID_must_include_both_precursor_and_product_mz_values,
                                              id));
                        }
                    }

                    precursor = double.Parse(mzs[0], CultureInfo.InvariantCulture);
                    product = double.Parse(mzs[1], CultureInfo.InvariantCulture);
                }
                else
                {
                    throw new ArgumentException(string.Format(Resources.ChromKey_FromId_The_value__0__is_not_a_valid_chromatogram_ID, id));
                }
                float ceValue = 0;
                if (parseCE)
                {
                    int ceIndex = id.LastIndexOf(SUFFIX_CE, StringComparison.Ordinal);
                    float ceParsed;
                    if (ceIndex != -1 && float.TryParse(id.Substring(ceIndex + SUFFIX_CE.Length),
                                                        NumberStyles.AllowDecimalPoint | NumberStyles.Integer,
                                                        CultureInfo.InvariantCulture,
                                                        out ceParsed))
                    {
                        // Shimadzu uses negative CE values internally, but Skyline uses positive CE values
                        // Avoid sign confusion
                        ceValue = Math.Abs(ceParsed);
                    }
                }
                return new ChromKey(null, new SignedMz(precursor, isNegativeCharge), null, new SignedMz(product, isNegativeCharge), ceValue, 0, source, extractor, false, true, null, null);
            }
            catch (FormatException)
            {
                throw new InvalidDataException(string.Format(Resources.ChromKey_FromId_Invalid_chromatogram_ID__0__found_Failure_parsing_mz_values, idIn));
            }
        }

        public static ChromKey FromQcTrace(MsDataFileImpl.QcTrace qcTrace)
        {
            var qcTextBytes = Encoding.UTF8.GetBytes(qcTrace.Name);
            return new ChromKey(qcTextBytes, 0, qcTextBytes.Length, SignedMz.ZERO, SignedMz.ZERO, 0, null, ChromSource.unknown, ChromExtractor.qc, false, false, null, null);
        }

        #region object overrides

        public bool Equals(ChromKey other)
        {
            return Equals(Target, other.Target) &&
                Precursor.Equals(other.Precursor) &&
                IonMobilityFilter.Equals(other.IonMobilityFilter) &&
                Product.Equals(other.Product) &&
                CollisionEnergy.Equals(other.CollisionEnergy) &&
                ExtractionWidth.Equals(other.ExtractionWidth) &&
                Source == other.Source &&
                Extractor == other.Extractor &&
                HasCalculatedMzs.Equals(other.HasCalculatedMzs) &&
                HasScanIds.Equals(other.HasScanIds) &&
                OptionalMinTime.Equals(other.OptionalMinTime) &&
                OptionalMaxTime.Equals(other.OptionalMaxTime);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is ChromKey && Equals((ChromKey) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Target != null ? Target.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ Precursor.GetHashCode();
                hashCode = (hashCode*397) ^ IonMobilityFilter.GetHashCode();
                hashCode = (hashCode*397) ^ Product.GetHashCode();
                hashCode = (hashCode*397) ^ CollisionEnergy.GetHashCode();
                hashCode = (hashCode*397) ^ ExtractionWidth.GetHashCode();
                hashCode = (hashCode*397) ^ (int) Source;
                hashCode = (hashCode*397) ^ (int) Extractor;
                hashCode = (hashCode*397) ^ HasCalculatedMzs.GetHashCode();
                hashCode = (hashCode*397) ^ HasScanIds.GetHashCode();
                hashCode = (hashCode*397) ^ OptionalMinTime.GetHashCode();
                hashCode = (hashCode*397) ^ OptionalMaxTime.GetHashCode();
                return hashCode;
            }
        }

        #endregion
    }

    /// <summary>
    /// This exists to encourage more stable sorting of lists which were
    /// formerly lists of KeyValuePair(ChromKey,providerId) and were sorted on ChromKey only.
    /// In small molecule work, ChromKey collisions are common so this could be an unstable sort.  
    /// </summary>
    public struct ChromKeyProviderIdPair : IComparable<ChromKeyProviderIdPair>
    {
        public ChromKeyProviderIdPair(ChromKey key, int providerId)
        {
            Key = key;
            ProviderId = providerId;
        }

        public ChromKey Key;
        public int ProviderId;

        public int CompareTo(ChromKeyProviderIdPair other)
        {
            var result = Key.CompareTo(other.Key);
            if (result == 0)
                result = ProviderId.CompareTo(other.ProviderId);
            return result;
        }

        public override string ToString()
        {
            return Key + string.Format(@" ({0})", ProviderId);
        }
    }

    /// <summary>
    /// Extra information about a chromatogram, which does not belong in ChromKey
    /// CONSIDER: Move other values from ChromKey to this class?
    /// </summary>
    public class ChromExtra
    {
        public ChromExtra(int statusId, int statusRank)
        {
            StatusId = (ushort) statusId;
            StatusRank = (ushort) statusRank;
        }

        public ushort StatusId { get; private set; }
        public ushort StatusRank { get; private set; }
    }

    public class ChromatogramGroupInfo
    {
        protected readonly ChromGroupHeaderInfo _groupHeaderInfo;
        protected readonly IDictionary<Type, int> _scoreTypeIndices;
        protected readonly byte[] _textIdBytes;
        protected readonly IList<ChromCachedFile> _allFiles;
        protected readonly IReadOnlyList<ChromTransition> _allTransitions;
        protected readonly IReadOnlyList<ChromPeak> _allPeaks;
        protected readonly IReadOnlyList<float> _allScores;

        public ChromatogramGroupInfo(ChromGroupHeaderInfo groupHeaderInfo,
                                     IDictionary<Type, int> scoreTypeIndices,
                                     byte[] textIdBytes,
                                     IList<ChromCachedFile> allFiles,
                                     IReadOnlyList<ChromTransition> allTransitions,
                                     IReadOnlyList<ChromPeak> allPeaks,
                                     IReadOnlyList<float> allScores)
        {
            _groupHeaderInfo = groupHeaderInfo;
            _scoreTypeIndices = scoreTypeIndices;
            _textIdBytes = textIdBytes;
            _allFiles = allFiles;
            _allTransitions = allTransitions;
            _allPeaks = allPeaks;
            _allScores = allScores;
        }

        protected ChromatogramGroupInfo()
        {
        }

        protected ChromatogramGroupInfo(ChromGroupHeaderInfo header, ChromatogramGroupInfo copyFrom) 
            : this(header, copyFrom._scoreTypeIndices, copyFrom._textIdBytes, copyFrom._allFiles, copyFrom._allTransitions, copyFrom._allPeaks, copyFrom._allScores)
        {
        }

        internal ChromGroupHeaderInfo Header { get { return _groupHeaderInfo; } }
        public SignedMz PrecursorMz { get { return new SignedMz(_groupHeaderInfo.Precursor, _groupHeaderInfo.NegativeCharge); } }
        public string TextId
        {
            get
            {
                return _groupHeaderInfo.TextIdIndex != -1
                    ? Encoding.UTF8.GetString(_textIdBytes, _groupHeaderInfo.TextIdIndex, _groupHeaderInfo.TextIdLen)
                    : null;
            }
        }
        public double? PrecursorCollisionalCrossSection { get { return _groupHeaderInfo.CollisionalCrossSection; } }
        public ChromCachedFile CachedFile { get { return _allFiles[_groupHeaderInfo.FileIndex]; } }
        public MsDataFileUri FilePath { get { return _allFiles[_groupHeaderInfo.FileIndex].FilePath; } }
        public DateTime FileWriteTime { get { return _allFiles[_groupHeaderInfo.FileIndex].FileWriteTime; } }
        public DateTime? RunStartTime { get { return _allFiles[_groupHeaderInfo.FileIndex].RunStartTime; } }
        public virtual int NumTransitions { get { return _groupHeaderInfo.NumTransitions; } }
        public int NumPeaks { get { return _groupHeaderInfo.NumPeaks; } }
        public int MaxPeakIndex { get { return _groupHeaderInfo.MaxPeakIndex; } }
        public int BestPeakIndex { get { return MaxPeakIndex; } }

        private byte[] DeferedCompressedBytes { get; set; }

        public TimeIntensitiesGroup TimeIntensitiesGroup { get; set; }

        public bool HasScore(Type scoreType)
        {
            return _scoreTypeIndices.ContainsKey(scoreType);
        }

        public float GetScore(Type scoreType, int peakIndex)
        {
            int scoreIndex;
            if (!_scoreTypeIndices.TryGetValue(scoreType, out scoreIndex))
                return float.NaN;
            return _allScores[_groupHeaderInfo.StartScoreIndex + peakIndex*_scoreTypeIndices.Count + scoreIndex];
        }

        public IEnumerable<ChromatogramInfo> TransitionPointSets
        {
            get
            {
                for (int i = 0; i < NumTransitions; i++)
                {
                    yield return GetTransitionInfo(i);
                }
            }
        }

        public IEnumerable<ChromPeak> GetPeaks(int transitionIndex)
        {
            int startPeak = _groupHeaderInfo.StartPeakIndex + (transitionIndex * _groupHeaderInfo.NumPeaks);
            int endPeak = startPeak + _groupHeaderInfo.NumPeaks;
            for (int i = startPeak; i < endPeak; i++)
                yield return _allPeaks[i];
        }

        public ChromatogramInfo GetTransitionInfo(int index)
        {
            return GetTransitionInfo(index, TransformChrom.interpolated);
        }

        public ChromatogramInfo GetTransitionInfo(int index, TransformChrom transform)
        {
            var chromatogramInfo = GetRawTransitionInfo(index);
            chromatogramInfo.Transform(transform);
            return chromatogramInfo;
        }

        public virtual ChromatogramInfo GetRawTransitionInfo(int index)
        {
            return new ChromatogramInfo(this, index);
        }

        protected SignedMz GetProductGlobal(int index)
        {
            return new SignedMz(_allTransitions[index].Product, _groupHeaderInfo.NegativeCharge);
        }

        private bool IsProductGlobalMatch(int index, TransitionDocNode nodeTran, float tolerance)
        {
            var source = _allTransitions[index].Source;
            bool isMs1Chromatogram = source == ChromSource.ms1 || source == ChromSource.sim;
            bool isTranMs1 = nodeTran == null || nodeTran.IsMs1;
            // Don't allow fragment ions to match data from MS1
            if (!isTranMs1 && isMs1Chromatogram)
                return false;
            var globalMz = GetProductGlobal(index);
            var tranMz = nodeTran != null ? nodeTran.Mz : SignedMz.ZERO;
            return tranMz.CompareTolerant(globalMz, tolerance) == 0;
        }

        public SignedMz GetProductLocal(int transitionIndex)
        {
            return new SignedMz(_allTransitions[_groupHeaderInfo.StartTransitionIndex + transitionIndex].Product, _groupHeaderInfo.NegativeCharge);
        }

        protected ChromTransition GetChromTransitionGlobal(int index)
        {
            return _allTransitions[index];
        }

        public ChromTransition GetChromTransitionLocal(int transitionIndex)
        {
            return _allTransitions[_groupHeaderInfo.StartTransitionIndex + transitionIndex];
        }

        public ChromatogramInfo GetTransitionInfo(TransitionDocNode nodeTran, float tolerance, OptimizableRegression regression)
        {
            return GetTransitionInfo(nodeTran, tolerance, TransformChrom.interpolated, regression);
        }

        public virtual ChromatogramInfo GetTransitionInfo(TransitionDocNode nodeTran, float tolerance, TransformChrom transform, OptimizableRegression regression)
        {
            var productMz = nodeTran != null ? nodeTran.Mz : SignedMz.ZERO;
            int startTran = _groupHeaderInfo.StartTransitionIndex;
            int endTran = startTran + _groupHeaderInfo.NumTransitions;
            int? iNearest = null;
            double deltaNearestMz = double.MaxValue;
            for (int i = startTran; i < endTran; i++)
            {
                if (IsProductGlobalMatch(i, nodeTran, tolerance))
                {
                    int iMiddle;
                    if (regression == null)
                    {
                        iMiddle = i;
                    }
                    else
                    {
                        // If there is optimization data, return only the middle value, which
                        // was the regression value.
                        int startOptTran, endOptTran;
                        GetOptimizationBounds(productMz, i, startTran, endTran, out startOptTran, out endOptTran);
                        iMiddle = (startOptTran + endOptTran) / 2;
                    }

                    double deltaMz = Math.Abs(productMz - GetProductGlobal(iMiddle));
                    if (deltaMz < deltaNearestMz)
                    {
                        iNearest = iMiddle;
                        deltaNearestMz = deltaMz;
                    }
                }
            }
            return iNearest.HasValue
                       ? GetTransitionInfo(iNearest.Value - startTran, transform)
                       : null;
        }

        public ChromatogramInfo[] GetAllTransitionInfo(TransitionDocNode nodeTran, float tolerance, OptimizableRegression regression, TransformChrom transform)
        {
            var listChromInfo = new List<ChromatogramInfo>();
            GetAllTransitionInfo(nodeTran, tolerance, regression, listChromInfo, transform);
            return listChromInfo.ToArray();
        }

        public void GetAllTransitionInfo(TransitionDocNode nodeTran, float tolerance, OptimizableRegression regression, List<ChromatogramInfo> listChromInfo, TransformChrom transform)
        {
            listChromInfo.Clear();
            if (regression == null)
            {
                // ReSharper disable ExpressionIsAlwaysNull
                var info = GetTransitionInfo(nodeTran, tolerance, transform, regression);
                // ReSharper restore ExpressionIsAlwaysNull
                if (info != null)
                    listChromInfo.Add(info);
                return;
            }

            var productMz = nodeTran != null ? nodeTran.Mz : SignedMz.ZERO;
            int startTran = _groupHeaderInfo.StartTransitionIndex;
            int endTran = startTran + _groupHeaderInfo.NumTransitions;
            for (int i = startTran; i < endTran; i++)
            {
                if (IsProductGlobalMatch(i, nodeTran, tolerance))
                {
                    int startOptTran, endOptTran;
                    GetOptimizationBounds(productMz, i, startTran, endTran, out startOptTran, out endOptTran);
                    for (int j = startOptTran; j <= endOptTran; j++)
                        listChromInfo.Add(GetTransitionInfo(j - startTran));
                    i = Math.Max(i, endOptTran);
                }
            }
        }

        private void GetOptimizationBounds(SignedMz productMz, int i, int startTran, int endTran, out int startOptTran, out int endOptTran)
        {
            // CONSIDER: Tried to make this a little more fault tolerant, but that just caused
            //           more problems. So, decided to leave this close to the original implementation.
            var productMzCurrent = GetProductGlobal(i);

            // First back up to find the beginning
            while (i > startTran &&
                   ChromatogramInfo.IsOptimizationSpacing(GetProductGlobal(i - 1), productMzCurrent))
            {
                productMzCurrent = GetProductGlobal(--i);
            }
            startOptTran = i;
            // Walk forward until the end
            while (i < endTran - 1 &&
                ChromatogramInfo.IsOptimizationSpacing(productMzCurrent, GetProductGlobal(i + 1)))
            {
                productMzCurrent = GetProductGlobal(++i);
            }
            endOptTran = i;
        }

        public ChromPeak GetTransitionPeak(int transitionIndex, int peakIndex)
        {
            return _allPeaks[_groupHeaderInfo.StartPeakIndex + transitionIndex*_groupHeaderInfo.NumPeaks + peakIndex];
        }

        // ReSharper disable SuggestBaseTypeForParameter
        public virtual int MatchTransitions(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup, float tolerance, bool multiMatch)
        // ReSharper restore SuggestBaseTypeForParameter
        {
            int match = 0;
            ExplicitRetentionTimeInfo explicitRT = null;
            if (nodePep != null && nodePep.ExplicitRetentionTime != null)
            {
                // We have retention time info, use that in the match
                explicitRT = nodePep.ExplicitRetentionTime;
            }

            foreach (TransitionDocNode nodeTran in nodeGroup.Children)
            {
                int countMatches = CountTransitionMatches(nodeTran, tolerance, explicitRT);
                if (countMatches > 0)
                {
                    match += multiMatch ? countMatches : 1;
                }
            }
            return match;
        }

        public int CountTransitionMatches(TransitionDocNode nodeTran, float tolerance, ExplicitRetentionTimeInfo explicitRT)
        {
            int countMatches = 0;
            if (explicitRT != null && Header.IsNotIncludedTime(explicitRT.RetentionTime))
                return 0;

            for (int transitionNum = 0; transitionNum < NumTransitions; transitionNum++)
            {
                if (nodeTran.Mz.CompareTolerant(GetProductLocal(transitionNum), tolerance) == 0)
                {
                    countMatches++;
                }
            }
            return countMatches;
        }

        public virtual void ReadChromatogram(ChromatogramCache cache, bool deferDecompression = false)
        {
            var compressedBytes = DeferedCompressedBytes ?? ReadCompressedBytes(cache);

            if (deferDecompression)
                DeferedCompressedBytes = compressedBytes;
            else
            {
                CompressedBytesToTimeIntensities(compressedBytes);
                DeferedCompressedBytes = null;
            }
        }

        public void EnsureDecompressed()
        {
            if (DeferedCompressedBytes != null)
                CompressedBytesToTimeIntensities(DeferedCompressedBytes);
        }

        public byte[] ReadCompressedBytes(ChromatogramCache cache)
        {
            Stream stream = cache.ReadStream.Stream;
            byte[] pointsCompressed = new byte[_groupHeaderInfo.CompressedSize];
            lock (stream)
            {
                try
                {
                    // Seek to stored location
                    stream.Seek(_groupHeaderInfo.LocationPoints, SeekOrigin.Begin);

                    // Single read to get all the points
                    if (stream.Read(pointsCompressed, 0, pointsCompressed.Length) < pointsCompressed.Length)
                        throw new IOException(Resources.ChromatogramGroupInfo_ReadChromatogram_Failure_trying_to_read_points);
                }
                catch (Exception)
                {
                    // If an exception is thrown, close the stream in case the failure is something
                    // like a network failure that can be remedied by re-opening the stream.
                    cache.ReadStream.CloseStream();
                    throw;
                }
            }
            return pointsCompressed;
        }

        public void CompressedBytesToTimeIntensities(byte[] pointsCompressed)
        {
            int uncompressedSize = _groupHeaderInfo.UncompressedSize;
            if (uncompressedSize < 0) // Before version 11
            {
                int numPoints = _groupHeaderInfo.NumPoints;
                int numTrans = _groupHeaderInfo.NumTransitions;
                bool hasErrors = _groupHeaderInfo.HasMassErrors;
                bool hasMs1ScanIds = _groupHeaderInfo.HasMs1ScanIds;
                bool hasFragmentScanIds = _groupHeaderInfo.HasFragmentScanIds;
                bool hasSimScanIds = _groupHeaderInfo.HasSimScanIds;

                uncompressedSize = ChromatogramCache.GetChromatogramsByteCount(
                    numTrans, numPoints, hasErrors,
                    hasMs1ScanIds, hasFragmentScanIds, hasSimScanIds);
            }
            var uncompressedBytes = pointsCompressed.Uncompress(uncompressedSize);
            if (_groupHeaderInfo.HasRawChromatograms)
            {
                TimeIntensitiesGroup = RawTimeIntensities.ReadFromStream(new MemoryStream(uncompressedBytes));
            }
            else
            {
                var chromTransitions = Enumerable.Range(Header.StartTransitionIndex, Header.NumTransitions)
                    .Select(i => _allTransitions[i]).ToArray();
                TimeIntensitiesGroup = InterpolatedTimeIntensities.ReadFromStream(new MemoryStream(uncompressedBytes),
                    Header, chromTransitions);
            }
        }

        public class PathEqualityComparer : IEqualityComparer<ChromatogramGroupInfo>
        {
            public bool Equals(ChromatogramGroupInfo x, ChromatogramGroupInfo y)
            {
                if (ReferenceEquals(x, null) || ReferenceEquals(y, null))
                {
                    return ReferenceEquals(x, null) && ReferenceEquals(y, null);
                }
                return Equals(x.FilePath, y.FilePath);
            }

            public int GetHashCode(ChromatogramGroupInfo obj)
            {
                return obj.FilePath.GetHashCode();
            }
        }

        public static PathEqualityComparer PathComparer { get; private set; }

        static ChromatogramGroupInfo()
        {
            PathComparer = new PathEqualityComparer();
        }
    }

// ReSharper disable InconsistentNaming
    public enum TransformChrom { raw, interpolated, craw2d, craw1d, savitzky_golay }
// ReSharper restore InconsistentNaming

    public class ChromatogramInfo
    {
        public const double OPTIMIZE_SHIFT_SIZE = 0.01;
        private const double OPTIMIZE_SHIFT_THRESHOLD = 0.001;

        public static bool IsOptimizationSpacing(double mz1, double mz2)
        {
            // Must be ordered correctly to be optimization spacing
            if (mz1 > mz2)
                return false;

            double delta = Math.Abs(Math.Abs(mz2 - mz1) - OPTIMIZE_SHIFT_SIZE);
            return delta <= OPTIMIZE_SHIFT_THRESHOLD;
        }

        private readonly ChromatogramGroupInfo _groupInfo;
        protected readonly int _transitionIndex;

        public ChromatogramInfo(ChromatogramGroupInfo groupInfo, int transitionIndex)
        {
            if (transitionIndex >= groupInfo.NumTransitions)
            {
                throw new IndexOutOfRangeException(
                    string.Format(Resources.ChromatogramInfo_ChromatogramInfo_The_index__0__must_be_between_0_and__1__,
                                  transitionIndex, groupInfo.NumTransitions));
            }
            _groupInfo = groupInfo;
            _transitionIndex = transitionIndex;
            var timeIntensitiesGroup = _groupInfo.TimeIntensitiesGroup;
            if (timeIntensitiesGroup != null)
            {
                TimeIntensities = timeIntensitiesGroup.TransitionTimeIntensities[_transitionIndex];
                if (Header.HasRawTimes())
                {
                    RawTimes = TimeIntensities.Times;
                }
            }
        }

        public ChromatogramInfo(float[] times, float[] intensities)
        {
            TimeIntensities = new TimeIntensities(times, intensities, null, null);
        }

        public ChromatogramGroupInfo GroupInfo { get { return _groupInfo; } }

        public ChromGroupHeaderInfo Header { get { return _groupInfo.Header; } }

        public int NumPeaks { get { return _groupInfo != null ? _groupInfo.NumPeaks : 0; } }

        public int BestPeakIndex { get { return _groupInfo != null ? _groupInfo.BestPeakIndex : -1; } }

        public MsDataFileUri FilePath { get { return _groupInfo.FilePath; } }

        public SignedMz PrecursorMz
        {
            get { return _groupInfo.PrecursorMz; }
        }

        public SignedMz ProductMz
        {
            get { return _groupInfo.GetProductLocal(_transitionIndex); }
        }

        public ChromSource Source
        {
            get { return ChromTransition.Source; }
        }

        public double? ExtractionWidth
        {
            get
            {
                return FloatToNullableDouble(ChromTransition.ExtractionWidth);
            }
        }

        public double? IonMobility
        {
            get { return FloatToNullableDouble(ChromTransition.IonMobilityValue); }
        }

        public double? IonMobilityExtractionWidth
        {
            get { return FloatToNullableDouble(ChromTransition.IonMobilityExtractionWidth); }
        }

        public eIonMobilityUnits IonMobilityUnits
        {
            get { return Header.IonMobilityUnits; }
        }

        public IonMobilityFilter GetIonMobilityFilter()
        {
            return IonMobilityFilter.GetIonMobilityFilter(IonMobilityValue.GetIonMobilityValue(IonMobility, Header.IonMobilityUnits), IonMobilityExtractionWidth, _groupInfo.PrecursorCollisionalCrossSection);
        }

        private static double? FloatToNullableDouble(float value)
        {
            float extractionWidth = value;
            if (extractionWidth == 0)
                return null;
            return extractionWidth;
        }

        private ChromTransition ChromTransition
        {
            get { return _groupInfo.GetChromTransitionLocal(_transitionIndex); }
        }

        public IList<float> RawTimes { get; set; }
        public TimeIntensities TimeIntensities { get; set; }
        public IList<float> Times { get { return TimeIntensities == null ? null : TimeIntensities.Times; } }
        public IList<int> ScanIndexes { get { return TimeIntensities == null ? null : TimeIntensities.ScanIds; } }

        public IList<float> Intensities
        {
            get { return TimeIntensities == null ? null : TimeIntensities.Intensities; }

            set { TimeIntensities = TimeIntensities.ChangeIntensities(value); }
        }

        public IEnumerable<ChromPeak> Peaks
        {
            get
            {
                return _groupInfo.GetPeaks(_transitionIndex);
            }
        }

        /// <summary>
        /// Get the nth peak for this group (as opposed to the nth peak in the allPeaks list)
        /// </summary>
        public ChromPeak GetPeak(int peakIndex)
        {
            if (0 > peakIndex || peakIndex > NumPeaks)
            {
                throw new IndexOutOfRangeException(
                    string.Format(Resources.ChromatogramInfo_ChromatogramInfo_The_index__0__must_be_between_0_and__1__,
                                  peakIndex, NumPeaks));
            }
            return _groupInfo.GetTransitionPeak(_transitionIndex, peakIndex);
        }

        public ChromPeak CalcPeak(int startIndex, int endIndex, ChromPeak.FlagValues flags)
        {
            if (startIndex == endIndex)
                return ChromPeak.EMPTY;

            var finder = Crawdads.NewCrawdadPeakFinder();
            finder.SetChromatogram(TimeIntensities.Times, TimeIntensities.Intensities);
            var peak = finder.GetPeak(startIndex, endIndex);

            return new ChromPeak(finder, peak, flags, TimeIntensities, RawTimes);
        }

        public int IndexOfPeak(double retentionTime)
        {
            // Find the closest peak within a tolerance of 0.001 (near the precision of a float)
            int i = 0, iMin = -1;
            double minDelta = double.MaxValue;
            foreach (var peak in Peaks)
            {
                double delta = Math.Abs(peak.RetentionTime - retentionTime);
                if (delta < minDelta)
                {
                    minDelta = delta;
                    iMin = i;
                }
                i++;
            }
            return minDelta < 0.001 ? iMin : -1;
        }

        public void AsArrays(out double[] times, out double[] intensities)
        {
            if (TimeIntensities == null)
            {
                times = intensities = new double[0];
            }
            else
            {
                times = TimeIntensities.Times.Select(time => (double) time).ToArray();
                intensities = TimeIntensities.Intensities.Select(intensity => (double) intensity).ToArray();
            }
        }

        public double MaxIntensity
        {
            get
            {
                double max = 0;
                foreach (float intensity in TimeIntensities.Intensities)
                    max = Math.Max(max, intensity);
                return max;
            }
        }

        public void SumIntensities(IList<ChromatogramInfo> listInfo)
        {
            foreach (var chromatogramInfo in listInfo)
            {
                if (chromatogramInfo == null || ReferenceEquals(this, chromatogramInfo))
                {
                    continue;
                }
                TimeIntensities = TimeIntensities.MergeTimesAndAddIntensities(chromatogramInfo.TimeIntensities);
            }
        }

        public void Transform(TransformChrom transformChrom)
        {
            switch (transformChrom)
            {
                case TransformChrom.interpolated:
                    Interpolate();
                    break;
                case TransformChrom.craw2d:
                    Interpolate();
                    Crawdad2DTransform();
                    break;
                case TransformChrom.craw1d:
                    Interpolate();
                    Crawdad1DTransform();
                    break;
                case TransformChrom.savitzky_golay:
                    Interpolate();
                    SavitzkyGolaySmooth();
                    break;
            }
        }

        public void Crawdad2DTransform()
        {
            if (Intensities == null)
                return;
            var peakFinder = Crawdads.NewCrawdadPeakFinder();
            peakFinder.SetChromatogram(Times, Intensities);
            Intensities = peakFinder.Intensities2d.ToArray();
        }

        public void Interpolate()
        {
            if (_groupInfo == null)
                return;
            var rawTimeIntensities = _groupInfo.TimeIntensitiesGroup as RawTimeIntensities;
            if (rawTimeIntensities == null)
                return;
            TimeIntensities = TimeIntensities.Interpolate(rawTimeIntensities.GetInterpolatedTimes(), rawTimeIntensities.InferZeroes);
        }

        public void Crawdad1DTransform()
        {
            if (Intensities == null)
                return;
            var peakFinder = Crawdads.NewCrawdadPeakFinder();
            peakFinder.SetChromatogram(Times, Intensities);
            Intensities = peakFinder.Intensities1d.ToArray();
        }

        public void SavitzkyGolaySmooth()
        {
            Intensities = SavitzkyGolaySmooth(Intensities.ToArray());
        }

        public static float[] SavitzkyGolaySmooth(float[] intensities)
        {
            if (intensities == null || intensities.Length < 9)
                return intensities;
            var intRaw = intensities;
            var intSmooth = new float[intRaw.Length];
            Array.Copy(intensities, intSmooth, 4);
            for (int i = 4; i < intRaw.Length - 4; i++)
            {
                double sum = 59 * intRaw[i] +
                    54 * (intRaw[i - 1] + intRaw[i + 1]) +
                    39 * (intRaw[i - 2] + intRaw[i + 2]) +
                    14 * (intRaw[i - 3] + intRaw[i + 3]) -
                    21 * (intRaw[i - 4] + intRaw[i + 4]);
                intSmooth[i] = (float)(sum / 231);
            }
            Array.Copy(intRaw, intRaw.Length - 4, intSmooth, intSmooth.Length - 4, 4);
            return intSmooth;
        }

        public int IndexOfNearestTime(float time)
        {
            return TimeIntensities.IndexOfNearestTime(time);
        }

        public int TransitionIndex { get { return _transitionIndex; } }
    }

    public class BulkReadException : IOException
    {
        public BulkReadException()
            : base(Resources.BulkReadException_BulkReadException_Failed_reading_block_from_file)
        {
        }
    }
}
