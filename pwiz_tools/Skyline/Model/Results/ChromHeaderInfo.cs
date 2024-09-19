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
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.PeakFinding;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Crawdad;
using pwiz.Skyline.Model.Results.Legacy;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using static pwiz.Skyline.Util.ValueChecking;

namespace pwiz.Skyline.Model.Results
{
    // This format was in use prior to Feb 2013, when the peak scoring work was added
    [StructLayout(LayoutKind.Sequential, Pack=4)]
    public struct ChromGroupHeaderInfo4
    {
        public ChromGroupHeaderInfo4(ChromGroupHeaderInfo16 header) : this()
        {
            Precursor = (float) header._precursor;
            FileIndex = header._fileIndex;
            NumTransitions = header._numTransitions;
            StartTransitionIndex = header._startTransitionIndex;
            NumPeaks = header._numPeaks;
            StartPeakIndex = header._startPeakIndex;
            MaxPeakIndex = header._maxPeakIndex;
            NumPoints = header._numPoints;
            CompressedSize = header._compressedSize;
            LocationPoints = header._locationPoints;
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


    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct ChromGroupHeaderInfo : IComparable<ChromGroupHeaderInfo>
    {
        /////////////////////////////////////////////////////////////////////
        // CAREFUL: This ordering determines the layout of this struct on
        //          disk from which it gets loaded directly into memory.
        //          The order and size of each element has been very carefully
        //          considered to avoid wasted space due to alignment.
        private SignedMz _precursor;
        private long _locationPoints;
        private int _uncompressedSize;
        private int _startTransitionIndex;
        private int _textIdIndex;
        private int _startPeakIndex;
        private int _startScoreIndex;
        private int _numPoints;
        private int _compressedSize;
        private float _startTime;
        private float _endTime;
        private float _collisionalCrossSection;
        private ushort _numTransitions;
        private FlagValues _flagBits;
        private ushort _fileIndex;
        private eIonMobilityUnits _ionMobilityUnits;
        private byte _numPeaks;
        private byte _maxPeakIndex;
        /////////////////////////////////////////////////////////////////////

        [Flags]
        public enum FlagValues : ushort
        {
            has_mass_errors = 0x01,
            extracted_base_peak = 0x02,
            has_ms1_scan_ids = 0x04,
            has_sim_scan_ids = 0x08,
            has_frag_scan_ids = 0x10,
            raw_chromatograms = 0x20,
            dda_acquisition_method = 0x40,
            extracted_qc_trace = 0x80
        }

        /// <summary>
        /// Allow a little fewer points than the data structure can actually hold.
        /// </summary>
        public const int MAX_POINTS = ushort.MaxValue - 1000;

        private const byte NO_MAX_PEAK = 0xFF;

        public ChromGroupHeaderInfo(SignedMz precursor, int numTransitions, int numPeaks, int maxPeakIndex,
            int compressedSize, int uncompressedSize,
            int numPoints, FlagValues flags, float? startTime, float? endTime,
            double? collisionalCrossSection, eIonMobilityUnits ionMobilityUnits)
            : this(precursor, 0, numTransitions, 0, numPeaks, 0, 0, maxPeakIndex, numPoints, compressedSize, uncompressedSize, 0, flags,
                startTime, endTime, collisionalCrossSection, ionMobilityUnits)
        {
        }

        /// <summary>
        /// Constructs header struct with all values populated
        /// </summary>
        public ChromGroupHeaderInfo(SignedMz precursor, int fileIndex,
                                     int numTransitions, int startTransitionIndex,
                                     int numPeaks, int startPeakIndex, int startScoreIndex, int maxPeakIndex,
                                     int numPoints, int compressedSize, int uncompressedSize, long location, FlagValues flags,
                                     float? startTime, float? endTime,
                                     double? collisionalCrossSection, 
                                     eIonMobilityUnits ionMobilityUnits) : this()
        {
            if (0 != (flags & FlagValues.raw_chromatograms) && compressedSize != -1)
            {
                Assume.AreNotEqual(-1, uncompressedSize);
            }
            _precursor = precursor;
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
            _flagBits = flags;
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

            _textIdIndex = -1;
            _ionMobilityUnits = ionMobilityUnits;
        }
        
        public ChromGroupHeaderInfo(ChromGroupHeaderInfo16 headerInfo16, int textIdIndex) : this()
        {
            _textIdIndex = textIdIndex;
            _startTransitionIndex = headerInfo16._startTransitionIndex;
            _startPeakIndex = headerInfo16._startPeakIndex;
            _startScoreIndex = headerInfo16._startScoreIndex;
            _numPoints = headerInfo16._numPoints;
            _compressedSize = headerInfo16._compressedSize;
            _ionMobilityUnits = headerInfo16.IonMobilityUnits;
            if (headerInfo16.HasMassErrors)
            {
                _flagBits |= FlagValues.has_mass_errors;
            }
            if (headerInfo16.HasMs1ScanIds)
            {
                _flagBits |= FlagValues.has_ms1_scan_ids;
            }
            if (headerInfo16.HasSimScanIds)
            {
                _flagBits |= FlagValues.has_sim_scan_ids;
            }
            if (headerInfo16.HasFragScanIds)
            {
                _flagBits |= FlagValues.has_frag_scan_ids;
            }
            if (headerInfo16.RawChromatograms)
            {
                _flagBits |= FlagValues.raw_chromatograms;
                Assume.AreNotEqual(-1, headerInfo16._uncompressedSize);
            }
            if (headerInfo16.IsDda)
            {
                _flagBits |= FlagValues.dda_acquisition_method;
            }
            if (headerInfo16.ExtractedBasePeak)
            {
                _flagBits |= FlagValues.extracted_base_peak;
            }
            if (headerInfo16.ExtractedQcTrace)
            {
                _flagBits |= FlagValues.extracted_qc_trace;
            }
            _fileIndex = headerInfo16._fileIndex;
            _numTransitions = headerInfo16._numTransitions;
            _numPeaks = headerInfo16._numPeaks;
            _maxPeakIndex = headerInfo16._maxPeakIndex;
            _precursor = headerInfo16.Precursor;
            _locationPoints = headerInfo16._locationPoints;
            _uncompressedSize = headerInfo16._uncompressedSize;
            _startTime = headerInfo16._startTime;
            _endTime = headerInfo16._endTime;
            _collisionalCrossSection = headerInfo16._collisionalCrossSection;
        }

        public int TextIdIndex { get { return _textIdIndex; } }
        public int StartTransitionIndex { get { return _startTransitionIndex; } }
        public int StartPeakIndex { get { return _startPeakIndex; } }
        public int StartScoreIndex { get { return _startScoreIndex; } }
        public int NumPoints { get { return _numPoints; } }
        public int CompressedSize { get { return _compressedSize; } }
        public ushort FileIndex { get {return _fileIndex;} }
        public ushort NumTransitions { get { return _numTransitions; } }
        public byte NumPeaks { get { return _numPeaks; } }        // The number of peaks stored per chrom should be well under 128
        public long LocationPoints { get{return _locationPoints;} }
        public int UncompressedSize { get{return _uncompressedSize;} }

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

        public FlagValues Flags { get { return _flagBits; } }

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
            get { return _precursor.IsNegative; }
        }

        public SignedMz Precursor
        {
            get { return _precursor; }
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
                return _ionMobilityUnits;
            }
        }

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

        public ChromGroupHeaderInfo ChangeTextIdIndex(int index)
        {
            var headerInfo = this;
            headerInfo._textIdIndex = index;
            return headerInfo;
        }

        public ChromGroupHeaderInfo ChangeChargeToNegative()
        {
            // For dealing with pre-V11 caches where we didn't record chromatogram polarity
            var chromGroupHeaderInfo = this;
            chromGroupHeaderInfo._precursor = new SignedMz(_precursor, true);
            return chromGroupHeaderInfo;
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

            if (cacheFormatVersion < CacheFormatVersion.Seventeen)
            {
                return 72;
            }
            return 68;
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
        private short _optimizationStep;
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

        public ChromTransition(double product, float extractionWidth, float ionMobilityValue,
            float ionMobilityExtractionWidth, ChromSource source, short optimizationStep) : this()
        {
            _product = product;
            _extractionWidth = extractionWidth;
            _ionMobilityValue = ionMobilityValue;
            _ionMobilityExtractionWidth = ionMobilityExtractionWidth;
            Source = source;
            _optimizationStep = optimizationStep;
        }

        public ChromTransition(ChromTransition5 chromTransition5) : this(chromTransition5.Product,
            // There was an issue with Manage Results > Rescore, which made it possible to corrupt
            // the chromatogram source until a commit by Nick in March 2014, and Brian introduced
            // the next version of this struct in the May, 2014. So considering the source unknown
            // for these older files seems safest, since we are moving to paying attention to the
            // source for chromatogram to transition matching.
            chromTransition5.ExtractionWidth, 0, 0, ChromSource.unknown, 0)
        {            
        }

        public ChromTransition(ChromTransition4 chromTransition4)
            : this(chromTransition4.Product, 0, 0, 0, ChromSource.unknown, 0)
        {
        }

        public double Product { get { return _product; } }
        public float ExtractionWidth { get { return _extractionWidth; }}  // In m/z
        public float IonMobilityValue { get { return _ionMobilityValue; } } // Units depend on ion mobility type
        public float IonMobilityExtractionWidth { get { return _ionMobilityExtractionWidth; } } // Units depend on ion mobility type
        public short OptimizationStep { get { return _optimizationStep; } }

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

        public ChromTransition ChangeProduct(double product)
        {
            var chromTransition = this;
            chromTransition._product = product;
            return chromTransition;
        }

        public ChromTransition ChangeOptimizationStep(short step, double productMz)
        {
            var chromTransition = this;
            chromTransition._optimizationStep = step;
            chromTransition._product = productMz;
            return chromTransition;
        }

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
        private FlagValues _flagValues;
        private short _massError;
        private readonly short _pointsAcross;
        private readonly float _stdDev;
        private readonly float _skewness;
        private readonly float _kurtosis;
        private readonly float _shapeCorrelation;

        [Flags]
        public enum FlagValues : ushort
        {
            degenerate_fwhm =       0x0001,
            forced_integration =    0x0002,
            time_normalized =       0x0004,
            peak_truncation_known = 0x0008,
            peak_truncated =        0x0010,
            contains_id =           0x0020,
            used_id_alignment =     0x0040,
            has_peak_shape = 0x0080,

            // This is the last available flag
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

        public ChromPeak(float retentionTime, float startTime, float endTime, float area, float backgroundArea,
            float height, float fwhm, FlagValues flagValues, double? massError, int? pointsAcross, PeakShapeValues? peakShapeValues)
        {
            _retentionTime = retentionTime;
            _startTime = startTime;
            _endTime = endTime;
            _area = area;
            _backgroundArea = backgroundArea;
            _height = height;
            _fwhm = fwhm;
            if (massError.HasValue)
            {
                flagValues |= FlagValues.mass_error_known;
                _massError = To10x(massError.Value);
            }
            else
            {
                flagValues &= ~FlagValues.mass_error_known;
                _massError = 0;
            }

            _pointsAcross = (short) Math.Min(pointsAcross.GetValueOrDefault(), ushort.MaxValue);
            if (peakShapeValues.HasValue)
            {
                flagValues |= FlagValues.has_peak_shape;
                _skewness = peakShapeValues.Value.Skewness;
                _kurtosis = peakShapeValues.Value.Kurtosis;
                _stdDev = peakShapeValues.Value.StdDev;
                _shapeCorrelation = peakShapeValues.Value.ShapeCorrelation;
            }
            else
            {
                flagValues &= ~FlagValues.has_peak_shape;
                _shapeCorrelation = _stdDev = _skewness = _kurtosis = 0;
            }
            _flagValues = flagValues;
        }

        public ChromPeak(IPeakFinder finder,
                         IFoundPeak peak,
                         FlagValues flags,
                         TimeIntensities timeIntensities,
                         IList<float> rawTimes,
                         MedianPeakShape medianPeakShape)
            : this()
        {
            var times = timeIntensities.Times;
            if (times.Count == 0)
            {
                return;
            }
            var intensities = timeIntensities.Intensities;
            var massErrors = timeIntensities.MassErrors;

            _retentionTime = times[peak.TimeIndex];
            _startTime = times[peak.StartIndex];
            _endTime = times[peak.EndIndex];
            // Get the interval being used to convert from Crawdad index based numbers
            // to numbers that are normalized with respect to time.
            double interval;
            if (peak.EndIndex > peak.StartIndex)
            {
                interval = (_endTime - _startTime) / (peak.EndIndex - peak.StartIndex);
            }
            else
            {
                interval = 0;
            }

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
            _flagValues = flags;
            if (peak.FwhmDegenerate)
                _flagValues |= FlagValues.degenerate_fwhm;

            // Calculate peak truncation as a peak extent at either end of the
            // recorded values, where the intensity is higher than the other extent
            // by more than 1% of the peak height.
            _flagValues |= FlagValues.peak_truncation_known;
            const double truncationTolerance = 0.01;
            double deltaIntensityExtents = (intensities[peak.EndIndex] - intensities[peak.StartIndex]) / Height;
            if ((peak.StartIndex == 0 && deltaIntensityExtents < -truncationTolerance) ||
                (peak.EndIndex == times.Count - 1 && deltaIntensityExtents > truncationTolerance))
            {
                _flagValues |= FlagValues.peak_truncated;
            }

            _massError = 0;
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
                    _flagValues |= FlagValues.mass_error_known;
                    _massError = To10x(massError);
                }
            }
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

            var backgroundLevel = Math.Min(timeIntensities.Intensities[peak.StartIndex],
                timeIntensities.Intensities[peak.EndIndex]);
            var peakShapeStatistics = PeakShapeStatistics.CalculateFromTimeIntensities(timeIntensities, peak.StartIndex,
                peak.EndIndex, backgroundLevel);
            if (peakShapeStatistics != null)
            {
                _flagValues |= FlagValues.has_peak_shape;
                _stdDev = (float) peakShapeStatistics.StdDevTime;
                _skewness = (float) peakShapeStatistics.Skewness;
                _kurtosis = (float) peakShapeStatistics.Kurtosis;
                _shapeCorrelation = (float?) medianPeakShape?.GetCorrelation(timeIntensities) ?? 1f;
            }
            else
            {
                _flagValues &= ~FlagValues.has_peak_shape;
            }
        }

        public float RetentionTime { get { return _retentionTime; } }
        public float StartTime { get { return _startTime; } }
        public float EndTime { get { return _endTime; } }
        public float Area { get { return _area; } }
        public float BackgroundArea { get { return _backgroundArea; } }
        public float Height { get { return _height; } }
        public float Fwhm { get { return _fwhm; } }
        public short? PointsAcross { get { return _pointsAcross == 0 ? (short?)null : _pointsAcross; } }

        public override string ToString()
        {
            return string.Format(@"rt={0:F02}, area={1}", RetentionTime, Area);
        }

        public FlagValues Flags
        {
            get
            {
                return _flagValues;
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
                if ((_flagValues & FlagValues.mass_error_known) == 0)
                    return null;
                // Mass error is stored as 10x the calculated mass error in PPM.
                return _massError / 10f;
            }
        }

        public PeakShapeValues? PeakShapeValues
        {
            get
            {
                if ((_flagValues & FlagValues.has_peak_shape) == 0)
                {
                    return null;
                }

                return new PeakShapeValues(_stdDev, _skewness, _kurtosis, _shapeCorrelation);
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
            copy._flagValues = Flags & ~FlagValues.mass_error_known;
            copy._massError = 0;
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
            if (formatVersion < CacheFormatVersion.Sixteen)
            {
                return 36;
            }
            return 52;
        }

        public static StructSerializer<ChromPeak> StructSerializer(int chromPeakSize)
        {
            return new StructSerializer<ChromPeak>
            {
                ItemSizeOnDisk = chromPeakSize,
                DirectSerializer = DirectSerializer.Create(ReadArray, WriteArray)
            };
        }

        public static ChromPeak IntegrateWithoutBackground(TimeIntensities timeIntensities, float startTime,
            float endTime, FlagValues flags, MedianPeakShape medianPeakShape)
        {
            int pointsAcrossPeak = 0;
            int startIndex = CollectionUtil.BinarySearch(timeIntensities.Times, startTime);
            if (startIndex < 0)
            {
                startIndex = ~startIndex;
                timeIntensities = timeIntensities.InterpolateTime(startTime);
                Assume.AreEqual(startTime, timeIntensities.Times[startIndex]);
                pointsAcrossPeak--;
            }

            int endIndex = CollectionUtil.BinarySearch(timeIntensities.Times, endTime);
            if (endIndex < 0)
            {
                endIndex = ~endIndex;
                timeIntensities = timeIntensities.InterpolateTime(endTime);
                Assume.AreEqual(endTime, timeIntensities.Times[endIndex]);
                pointsAcrossPeak--;
            }

            var peakShapeStatistics =
                PeakShapeStatistics.CalculateFromTimeIntensities(timeIntensities, startIndex, endIndex, 0);
            pointsAcrossPeak += endIndex - startIndex + 1;
            double totalArea = 0;
            double totalMassError = 0;
            for (int i = startIndex; i < endIndex; i++)
            {
                double width = (timeIntensities.Times[i + 1] - timeIntensities.Times[i]) * 60;
                double intensity1 = timeIntensities.Intensities[i];
                double intensity2 = timeIntensities.Intensities[i + 1];
                totalArea += (intensity1 + intensity2) * width / 2;
                if (timeIntensities.MassErrors != null)
                {
                    totalMassError +=
                        (intensity1 * timeIntensities.MassErrors[i] + intensity2 * timeIntensities.MassErrors[i + 1]) *
                        width / 2;
                }
            }

            double apexTime, apexHeight;
            if (totalArea == 0)
            {
                apexTime = (startTime + endTime) / 2;
                apexHeight = 0;
            }
            else
            {
                apexHeight = timeIntensities.Intensities[startIndex];
                apexTime = timeIntensities.Times[startIndex];
                for (int i = startIndex + 1; i <= endIndex; i++)
                {
                    if (timeIntensities.Intensities[i] > apexHeight)
                    {
                        apexTime = timeIntensities.Times[i];
                        apexHeight = timeIntensities.Intensities[i];
                    }
                }
            }
            // Determine the full width at half max
            bool fwhmDegenerate = false;
            double? halfMaxStart = null;
            double? halfMaxEnd = null;
            var halfHeight = apexHeight / 2;
            for (int index = startIndex; index <= endIndex; index++)
            {
                double time = timeIntensities.Times[index];
                double intensity = timeIntensities.Intensities[index];
                if (intensity >= halfHeight)
                {
                    if (!halfMaxStart.HasValue)
                    {
                        halfMaxStart = time;
                        if (index > startIndex)
                        {
                            if (intensity > halfHeight)
                            {
                                double prevTime = timeIntensities.Times[index - 1];
                                double prevIntensity = timeIntensities.Intensities[index - 1];
                                if (prevIntensity < halfHeight)
                                {
                                    var fraction = (intensity - halfHeight) / (intensity - prevIntensity);
                                    halfMaxStart = (1 - fraction) * time + fraction * prevTime;
                                }
                            }
                        }
                        else
                        {
                            fwhmDegenerate = true;
                        }
                    }

                    halfMaxEnd = time;
                    if (index < timeIntensities.NumPoints - 1)
                    {
                        double nextTime = timeIntensities.Times[index + 1];
                        if (nextTime <= endTime)
                        {
                            double nextIntensity = timeIntensities.Intensities[index + 1];
                            if (nextIntensity < halfHeight)
                            {
                                var fraction = (intensity - halfHeight) / (intensity - nextIntensity);
                                halfMaxEnd = (1 - fraction) * time + fraction * nextTime;
                            }
                        }
                        else
                        {
                            fwhmDegenerate = true;
                        }
                    }
                    else
                    {
                        fwhmDegenerate = true;
                    }
                }
                if (time >= endTime)
                {
                    break;
                }
            }

            if (fwhmDegenerate)
            {
                flags |= FlagValues.degenerate_fwhm;
            }
            else
            {
                flags &= ~FlagValues.degenerate_fwhm;
            }

            float fwhm;
            if (halfMaxStart.HasValue)
            {
                fwhm = (float)(halfMaxEnd - halfMaxStart);
            }
            else
            {
                fwhm = 0;
            }

            double? massError = null;
            if (timeIntensities.MassErrors != null && totalArea > 0)
            {
                massError = totalMassError / totalArea;
            }

            float correlation;
            if (medianPeakShape == null)
            {
                correlation = 1;
            }
            else
            {
                correlation = (float) medianPeakShape.GetCorrelation(timeIntensities);
            }

            PeakShapeValues? peakShapeValues = null;
            if (peakShapeStatistics != null)
            {
                peakShapeValues = new PeakShapeValues((float)peakShapeStatistics.StdDevTime, (float)peakShapeStatistics.Skewness,
                    (float)peakShapeStatistics.Kurtosis, correlation);
            }

            return new ChromPeak((float) apexTime, startTime, endTime, (float) totalArea, 0, (float) apexHeight, fwhm, flags, massError,
                pointsAcrossPeak, peakShapeValues);
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
            is_srm = 0x80,
            used_ms1_centroids = 0x100,
            used_ms2_centroids = 0x200,
            result_file_data = 0x400,
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

        public static eIonMobilityUnits IonMobilityUnitsFromFlags(FlagValues flags)
        {
            var ionMobilityBits = flags & FlagValues.ion_mobility_type_bitmask;
            if (ionMobilityBits == FlagValues.ion_mobility_type_bitmask)
            {
                return (eIonMobilityUnits)(-1);
            }
            return (eIonMobilityUnits)((int)ionMobilityBits >> 4);
        }

        public ChromCachedFile(ChromFileInfo fileInfo)
            : this(fileInfo.FilePath, 0, fileInfo.FileWriteTime ?? DateTime.FromBinary(0), fileInfo.FileWriteTime, null, 
                (float) fileInfo.MaxRetentionTime, (float) fileInfo.MaxIntensity, 0, 0, default(float?), fileInfo.IonMobilityUnits, fileInfo.SampleId, fileInfo.SampleId, fileInfo.InstrumentInfoList)
        {
            if (fileInfo.IsSrm)
            {
                Flags |= FlagValues.is_srm;
            }
        }

        public ChromCachedFile(MsDataFileUri fileUri,
                               FlagValues flags,
                               DateTime fileWriteTime,
                               DateTime? runStartTime,
                               DateTime? importTime,
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
            Flags = (flags & ~FlagValues.ion_mobility_type_bitmask) | ((FlagValues)((int)ionMobilityUnits << 4) & FlagValues.ion_mobility_type_bitmask);
            FileWriteTime = fileWriteTime;
            RunStartTime = runStartTime;
            ImportTime = importTime;
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
        public DateTime? ImportTime { get; private set; }
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
            get { return (Flags & FlagValues.has_midas_spectra) != 0; }
        }

        public bool HasCombinedIonMobility
        {
            get { return (Flags & FlagValues.has_combined_ion_mobility) != 0; }
        }

        public bool UsedMs1Centroids
        {
            get { return (Flags & FlagValues.used_ms1_centroids) != 0; }
        }

        public bool UsedMs2Centroids
        {
            get { return (Flags & FlagValues.used_ms2_centroids) != 0; }
        }

        public bool IsSrm
        {
            get { return (Flags & FlagValues.is_srm) != 0; }
        }

        public bool HasResultFileData
        {
            get { return 0 != (Flags & FlagValues.result_file_data); }
        }

        public ChromCachedFile RelocateScanIds(long locationScanIds)
        {
            return ResizeScanIds(locationScanIds, SizeScanIds, HasResultFileData);
        }

        /// <summary>
        /// Returns a new ChromCachedFile where the scan ids have been relocated to the given location.
        /// Also, sets the result_file_data flag to indicate whether the scan ids are in the new ResultFileMetaData
        /// format or in the old MsDataFileScanIds format.
        /// </summary>
        public ChromCachedFile ResizeScanIds(long locationScanIds, int sizeScanIds, bool resultFileData)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.LocationScanIds = locationScanIds;
                im.SizeScanIds = sizeScanIds;
                if (resultFileData)
                    im.Flags |= FlagValues.result_file_data;
                else
                    im.Flags &= ~FlagValues.result_file_data;
            });
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
            if (f1 == null || f2 == null)
            {
                return ReferenceEquals(f1, f2);
            }
            return Equals(f1.FilePath, f2.FilePath);
        }

        public int GetHashCode(TItem f)
        {
            return f.FilePath.GetHashCode();
        }
    }

    public enum ChromSource : byte { fragment, sim, ms1, unknown  }

    public enum ChromExtractor : byte { summed, base_peak, qc }

    public class ChromKey : Immutable, IComparable<ChromKey>
    {
        public static readonly ChromKey EMPTY = new ChromKey(null, SignedMz.ZERO, null,
            SignedMz.ZERO, 0, 0, 0, ChromSource.unknown, ChromExtractor.summed);

        private double _optionalMinTime;
        private double _optionalMaxTime;
        private bool _hasOptionalTimes;

        public ChromKey(ChromatogramGroupId chromatogramGroupId,
                        SignedMz precursor,
                        IonMobilityFilter ionMobilityFilter,
                        SignedMz product,
                        int optimizationStep,
                        double ceValue,
                        double extractionWidth,
                        ChromSource source,
                        ChromExtractor extractor)
        {
            ChromatogramGroupId = chromatogramGroupId;
            Precursor = precursor;
            IonMobilityFilter = ionMobilityFilter ?? IonMobilityFilter.EMPTY;
            Product = product;
            OptimizationStep = optimizationStep;
            CollisionEnergy = (float) ceValue;
            ExtractionWidth = (float) extractionWidth;
            Source = source;
            Extractor = extractor;
            // Calculating these values on the fly shows up in a profiler in the CompareTo function
            // So, probably not worth the space saved in this class
            IsEmpty = Precursor == 0 && Product == 0 && source == ChromSource.unknown;
        }

        public ChromatogramGroupId ChromatogramGroupId { get; }
        public Target Target
        {
            get { return ChromatogramGroupId?.Target; }
        }  // Modified sequence or custom ion id
        public SignedMz Precursor { get; private set; }
        public double? CollisionalCrossSectionSqA { get { return IonMobilityFilter.CollisionalCrossSectionSqA; }  }
        public eIonMobilityUnits IonMobilityUnits { get { return IonMobilityFilter.IonMobilityUnits; } }
        public IonMobilityFilter IonMobilityFilter { get; private set; }
        public SignedMz Product { get; private set; }
        public int OptimizationStep { get; private set; }
        public float CollisionEnergy { get; private set; }
        public float ExtractionWidth { get; private set; }
        public ChromSource Source { get; private set; }
        public ChromExtractor Extractor { get; private set; }
        public bool HasCalculatedMzs { get; private set; }
        public bool IsEmpty { get; private set; }

        public double? OptionalMinTime
        {
            get { return _hasOptionalTimes ? (double?) _optionalMinTime : null; }
        }

        public double? OptionalMaxTime
        {
            get { return _hasOptionalTimes ? (double?) _optionalMaxTime : null; }
        }

        public ChromKey ChangeOptimizationStep(int step, SignedMz? newProductMz)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.OptimizationStep = step;
                im.Product = newProductMz ?? im.Product;
            });
        }

        public ChromKey ChangeOptionalTimes(double start, double end)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im._optionalMinTime = start;
                im._optionalMaxTime = end;
                im._hasOptionalTimes = true;
            });
        }

        public ChromKey ChangeCollisionEnergy(float collisionEnergy)
        {
            return ChangeProp(ImClone(this), im => im.CollisionEnergy = collisionEnergy);
        }

        /// <summary>
        /// For debugging only
        /// </summary>
        public override string ToString()
        {
            if (ChromatogramGroupId != null)
                return string.Format(@"{0:F04}, {1:F04} {4} - {2} - {3}", Precursor.RawValue, Product.RawValue, Source, ChromatogramGroupId, IonMobilityFilter);
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
            c = OptimizationStep.CompareTo(key.OptimizationStep);
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
            c = ValueTuple.Create(ChromatogramGroupId).CompareTo(ValueTuple.Create(key.ChromatogramGroupId));
            if (c != 0)
                return c;
            return Extractor.CompareTo(key.Extractor);
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
                    var str = id.Substring(MsDataFileImpl.PREFIX_PRECURSOR.Length);
                    if (str.StartsWith(@"Q1="))
                    {
                        // Agilent format e.g. "SIM SIC Q1=215.15 start=5.087066667 end=14.497416667"
                        str = str.Substring(3).Split(' ')[0];
                    }
                    precursor = double.Parse(str, CultureInfo.InvariantCulture);
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
                                string.Format(ResultsResources.ChromKey_FromId_Invalid_chromatogram_ID__0__found_The_ID_must_include_both_precursor_and_product_mz_values,
                                              id));
                        }
                    }

                    precursor = double.Parse(mzs[0], CultureInfo.InvariantCulture);
                    product = double.Parse(mzs[1], CultureInfo.InvariantCulture);
                }
                else
                {
                    throw new ArgumentException(string.Format(ResultsResources.ChromKey_FromId_The_value__0__is_not_a_valid_chromatogram_ID, id));
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
                return new ChromKey(null, new SignedMz(precursor, isNegativeCharge), null, new SignedMz(product, isNegativeCharge), 0, ceValue, 0, source, extractor);
            }
            catch (FormatException)
            {
                throw new InvalidDataException(string.Format(ResultsResources.ChromKey_FromId_Invalid_chromatogram_ID__0__found_Failure_parsing_mz_values, idIn));
            }
        }

        public static ChromKey FromQcTrace(MsDataFileImpl.QcTrace qcTrace)
        {
            var chromatogramGroupId = ChromatogramGroupId.ForQcTraceName(qcTrace.Name);
            return new ChromKey(chromatogramGroupId, SignedMz.ZERO, null, SignedMz.ZERO, 0, 0, 0, ChromSource.unknown, ChromExtractor.qc);
        }

        #region object overrides

        public bool Equals(ChromKey other)
        {
            return Equals(ChromatogramGroupId, other.ChromatogramGroupId) &&
                Precursor.Equals(other.Precursor) &&
                IonMobilityFilter.Equals(other.IonMobilityFilter) &&
                Product.Equals(other.Product) &&
                CollisionEnergy.Equals(other.CollisionEnergy) &&
                ExtractionWidth.Equals(other.ExtractionWidth) &&
                Source == other.Source &&
                Extractor == other.Extractor &&
                HasCalculatedMzs.Equals(other.HasCalculatedMzs) &&
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
                var hashCode = (ChromatogramGroupId != null ? ChromatogramGroupId.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ Precursor.GetHashCode();
                hashCode = (hashCode*397) ^ IonMobilityFilter.GetHashCode();
                hashCode = (hashCode*397) ^ Product.GetHashCode();
                hashCode = (hashCode*397) ^ CollisionEnergy.GetHashCode();
                hashCode = (hashCode*397) ^ ExtractionWidth.GetHashCode();
                hashCode = (hashCode*397) ^ (int) Source;
                hashCode = (hashCode*397) ^ (int) Extractor;
                hashCode = (hashCode*397) ^ HasCalculatedMzs.GetHashCode();
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
        protected readonly FeatureNames _scoreTypeIndices;
        protected readonly IList<ChromCachedFile> _allFiles;
        protected readonly IReadOnlyList<ChromTransition> _allTransitions;
        [CanBeNull]
        protected ChromatogramCache _chromatogramCache;
        protected IList<ChromPeak> _chromPeaks;
        protected IList<float> _scores;
        private TimeIntensitiesGroup _timeIntensitiesGroup;

        public ChromatogramGroupInfo(ChromGroupHeaderInfo groupHeaderInfo,
                                     FeatureNames scoreTypeIndices,
                                     ChromatogramGroupId chromatogramGroupId,
                                     IList<ChromCachedFile> allFiles,
                                     IReadOnlyList<ChromTransition> allTransitions,
                                     ChromatogramCache chromatogramCache)
        {
            _groupHeaderInfo = groupHeaderInfo;
            _scoreTypeIndices = scoreTypeIndices;
            ChromatogramGroupId = chromatogramGroupId;
            _allFiles = allFiles;
            _allTransitions = allTransitions;
            _chromatogramCache = chromatogramCache;
        }

        public ChromatogramGroupInfo(ChromGroupHeaderInfo groupHeaderInfo,
            ChromCachedFile chromCachedFile,
            IReadOnlyList<ChromTransition> allTransitions, IList<ChromPeak> peaks, 
            TimeIntensitiesGroup timeIntensitiesGroup) 
            : this(groupHeaderInfo, chromCachedFile, allTransitions, peaks, timeIntensitiesGroup, FeatureNames.EMPTY, Array.Empty<float>())
        {
        }

        public ChromatogramGroupInfo(ChromGroupHeaderInfo groupHeaderInfo,
            ChromCachedFile chromCachedFile,
            IReadOnlyList<ChromTransition> allTransitions, IList<ChromPeak> peaks,
            TimeIntensitiesGroup timeIntensitiesGroup, FeatureNames scoreTypeIndices, float[] scores)
        {
            _groupHeaderInfo = groupHeaderInfo;
            _allFiles = new[]{chromCachedFile};
            _allTransitions = allTransitions;
            _chromPeaks = peaks;
            _timeIntensitiesGroup = timeIntensitiesGroup;
            _scoreTypeIndices = scoreTypeIndices;
            _scores = scores;
        }


        internal ChromGroupHeaderInfo Header { get { return _groupHeaderInfo; } }
        public SignedMz PrecursorMz { get { return new SignedMz(_groupHeaderInfo.Precursor, _groupHeaderInfo.NegativeCharge); } }
        [CanBeNull]
        public ChromatogramGroupId ChromatogramGroupId { get; private set; }
        public string QcTraceName { get { return ChromatogramGroupId?.QcTraceName; } }
        public double? PrecursorCollisionalCrossSection { get { return _groupHeaderInfo.CollisionalCrossSection; } }
        public ChromCachedFile CachedFile { get { return _allFiles[_groupHeaderInfo.FileIndex]; } }
        public MsDataFileUri FilePath { get { return _allFiles[_groupHeaderInfo.FileIndex].FilePath; } }
        public DateTime FileWriteTime { get { return _allFiles[_groupHeaderInfo.FileIndex].FileWriteTime; } }
        public DateTime? RunStartTime { get { return _allFiles[_groupHeaderInfo.FileIndex].RunStartTime; } }
        public virtual int NumTransitions { get { return _groupHeaderInfo.NumTransitions; } }
        public int NumPeaks { get { return _groupHeaderInfo.NumPeaks; } }
        public int MaxPeakIndex { get { return _groupHeaderInfo.MaxPeakIndex; } }
        public int BestPeakIndex { get { return MaxPeakIndex; } }

        [Browsable(false)]
        public TimeIntensitiesGroup TimeIntensitiesGroup
        {
            get
            {
                if (_timeIntensitiesGroup == null)
                {
                    _timeIntensitiesGroup = _chromatogramCache?.ReadTimeIntensities(Header);
                }
                return _timeIntensitiesGroup;
            }
        }

        public bool HasScore(Type scoreType)
        {
            return _scoreTypeIndices.IndexOf(scoreType) >= 0;
        }

        protected IList<float> ReadScores()
        {
            if (_scores == null)
            {
                _scores = _chromatogramCache?.ReadScores(Header);
            }

            return _scores;
        }

        protected IList<ChromPeak> ReadPeaks()
        {
            if (_chromPeaks == null)
            {
                _chromPeaks = _chromatogramCache?.ReadPeaks(Header);
            }

            return _chromPeaks;
        }

        public float GetScore(Type scoreType, int peakIndex)
        {
            int scoreIndex = _scoreTypeIndices.IndexOf(scoreType);
            if (scoreIndex < 0)
            {
                return float.NaN;
            }
            var scores = ReadScores();
            return scores[peakIndex*_scoreTypeIndices.Count + scoreIndex];
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
            var peaks = ReadPeaks();
            int startPeak = transitionIndex * _groupHeaderInfo.NumPeaks;
            int endPeak = startPeak + _groupHeaderInfo.NumPeaks;
            for (int i = startPeak; i < endPeak; i++)
                yield return peaks[i];
        }

        public IList<ChromPeak> GetPeakGroup(int peakIndex)
        {
            var peaks = ReadPeaks();
            return ReadOnlyList.Create(_groupHeaderInfo.NumTransitions,
                transitionIndex => peaks[transitionIndex * _groupHeaderInfo.NumPeaks + peakIndex]);
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
            // Don't allow fragment ions to match data from MS1
            if (isMs1Chromatogram && nodeTran != null && !nodeTran.IsMs1)
            {
                return false;
            }
            var globalMz = GetProductGlobal(index);
            var tranMz = nodeTran != null ? nodeTran.Mz : SignedMz.ZERO;
            return tranMz.CompareTolerant(globalMz, tolerance) == 0;
        }

        public SignedMz GetProductLocal(int transitionIndex)
        {
            return new SignedMz(_allTransitions[_groupHeaderInfo.StartTransitionIndex + transitionIndex].Product, _groupHeaderInfo.NegativeCharge);
        }

        public ChromTransition GetChromTransitionLocal(int transitionIndex)
        {
            return _allTransitions[_groupHeaderInfo.StartTransitionIndex + transitionIndex];
        }

        public ChromatogramInfo GetTransitionInfo(TransitionDocNode nodeTran, float tolerance)
        {
            return GetTransitionInfo(nodeTran, tolerance, TransformChrom.interpolated);
        }

        public virtual ChromatogramInfo GetTransitionInfo(TransitionDocNode nodeTran, float tolerance, TransformChrom transform)
        {
            var iBest = GetBestProductIndex(nodeTran, tolerance);
            return iBest.HasValue
                ? GetTransitionInfo(iBest.Value - _groupHeaderInfo.StartTransitionIndex, transform)
                       : null;
        }

        public OptStepChromatograms GetAllTransitionInfo(TransitionDocNode nodeTran, float tolerance, OptimizableRegression regression, TransformChrom transform)
        {
            if (regression == null)
            {
                var info = GetTransitionInfo(nodeTran, tolerance, transform);
                return info != null ? OptStepChromatograms.FromChromatogram(info) : OptStepChromatograms.EMPTY;
                }

            var listChromInfo = new List<ChromatogramInfo>();
            var iBest = GetBestProductIndex(nodeTran, tolerance);
            if (iBest.HasValue)
            {
                var startTran = _groupHeaderInfo.StartTransitionIndex;
                var endTran = startTran + _groupHeaderInfo.NumTransitions;

                // First back up to find the beginning
                var i = iBest.Value;
                while (i - 1 >= startTran &&
                       _allTransitions[i - 1].OptimizationStep == _allTransitions[i].OptimizationStep - 1)
                {
                    i--;
                }

                // Walk forward until the end
                do
                {
                    listChromInfo.Add(GetTransitionInfo(i - startTran, transform));
                    i++;
                } while (i < endTran && _allTransitions[i].OptimizationStep == _allTransitions[i - 1].OptimizationStep + 1);
            }

            return new OptStepChromatograms(listChromInfo, regression.StepCount);
        }

        private int? GetBestProductIndex(TransitionDocNode nodeTran, float tolerance)
        {
            var productMz = nodeTran?.Mz ?? SignedMz.ZERO;
            var startTran = _groupHeaderInfo.StartTransitionIndex;
            var endTran = startTran + _groupHeaderInfo.NumTransitions;

            int? iNearest = null;
            var deltaNearestMz = double.MaxValue;
            for (var i = startTran; i < endTran; i++)
            {
                if (!IsProductGlobalMatch(i, nodeTran, tolerance) || _allTransitions[i].OptimizationStep != 0)
                    continue;

                var deltaMz = Math.Abs(productMz - GetProductGlobal(i));
                if (deltaMz < deltaNearestMz)
            {
                    iNearest = i;
                    deltaNearestMz = deltaMz;
            }
        }

            return iNearest;
        }

        public ChromPeak GetTransitionPeak(int transitionIndex, int peakIndex)
        {
            return ReadPeaks()[transitionIndex*_groupHeaderInfo.NumPeaks + peakIndex];
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

        /// <summary>
        /// Throw away chromatogram and peaks, and disconnect from the ChromatogramCache.
        /// This object be able to provide information about the ChromGroupHeaderInfo and ChromTransition's,
        /// but will not have any chromatogram or peak data.
        /// </summary>
        public void DiscardData()
        {
            _chromatogramCache = null;
            _timeIntensitiesGroup = null;
            _chromPeaks = null;
        }

        public static PathEqualityComparer PathComparer { get; private set; }

        static ChromatogramGroupInfo()
        {
            PathComparer = new PathEqualityComparer();
        }

        /// <summary>
        /// Loads the peaks (and optionally the scores as well) for a list of ChromatogramGroupInfos.
        /// 
        /// </summary>
        public static void LoadPeaksForAll(IEnumerable<ChromatogramGroupInfo> chromatogramGroupInfos, bool loadScoresToo)
        {
            foreach (var grouping in chromatogramGroupInfos.Distinct()
                .GroupBy(chromatogramGroupInfo => chromatogramGroupInfo._chromatogramCache))
            {
                if (grouping.Key == null)
                {
                    continue;
                }
                var groupInfos = grouping.ToList();
                var headers = groupInfos.Select(group => group.Header).ToList();
                var peaksArray = new IList<ChromPeak>[headers.Count];
                var scoresArray = loadScoresToo ? new IList<float>[headers.Count] : null;
                grouping.Key.ReadDataForAll(headers, peaksArray, scoresArray);
                for (int i = 0; i < headers.Count; i++)
                {
                    groupInfos[i]._chromPeaks = peaksArray[i];
                    if (scoresArray != null)
                    {
                        groupInfos[i]._scores = scoresArray[i];
                    }
                }
            }
        }
    }

// ReSharper disable InconsistentNaming
    public enum TransformChrom { raw, interpolated, craw2d, craw1d, savitzky_golay }

    public static class TransformChromExtension
    {
        public static bool IsDerivative(this TransformChrom transformChrom)
        {
            return transformChrom == TransformChrom.craw1d || transformChrom == TransformChrom.craw2d;
        }
    }
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
        private TimeIntensities _timeIntensities;
        private TransformChrom _transformChrom;

        public ChromatogramInfo(ChromatogramGroupInfo groupInfo, int transitionIndex)
        {
            if (transitionIndex >= groupInfo.NumTransitions)
            {
                throw new IndexOutOfRangeException(
                    string.Format(ResultsResources.ChromatogramInfo_ChromatogramInfo_The_index__0__must_be_between_0_and__1__,
                                  transitionIndex, groupInfo.NumTransitions));
            }
            _groupInfo = groupInfo;
            _transitionIndex = transitionIndex;
        }

        public ChromatogramInfo(float[] times, float[] intensities) : this(new TimeIntensities(times, intensities, null, null))
        {
        }
        public ChromatogramInfo(TimeIntensities timeIntensities)
        {
            _timeIntensities = timeIntensities;
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

        public int OptimizationStep
        {
            get
            {
                return ChromTransition.OptimizationStep;
            }
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
            return IonMobilityFilter.GetIonMobilityFilter(
                IonMobilityAndCCS.GetIonMobilityAndCCS(
                    IonMobilityValue.GetIonMobilityValue(IonMobility, Header.IonMobilityUnits),
                    _groupInfo.PrecursorCollisionalCrossSection, null), IonMobilityExtractionWidth);
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

        [Browsable(false)]
        private TimeIntensities RawTimeIntensities
        {
            get
            {
                if (!Header.HasRawTimes())
                {
                    return null;
                }

                return _groupInfo?.TimeIntensitiesGroup?.TransitionTimeIntensities[TransitionIndex];
            }
        }
        [Browsable(false)]
        public IList<float> RawTimes
        {
            get { return RawTimeIntensities?.Times; }
        }

        [Browsable(false)]
        public TimeIntensities TimeIntensities
        {
            get
            {
                if (_timeIntensities == null)
                {
                    _timeIntensities = GetTransformedTimeIntensities(_transformChrom);
                }

                return _timeIntensities;
            }
        }

        public TimeIntensities GetTransformedTimeIntensities(TransformChrom transformChrom)
        {
            var timeIntensitiesGroup = _groupInfo?.TimeIntensitiesGroup;
            if (timeIntensitiesGroup == null)
            {
                return null;
            }

            var timeIntensities = timeIntensitiesGroup.TransitionTimeIntensities[TransitionIndex];
            if (transformChrom == TransformChrom.raw)
            {
                return timeIntensities;
            }
            timeIntensities = InterpolateTimeIntensities(timeIntensities);
            return TransformInterpolatedTimeIntensities(timeIntensities, transformChrom);
        }

        private TimeIntensities InterpolateTimeIntensities(TimeIntensities timeIntensities)
        {
            var rawTimeIntensitiesGroup = _groupInfo?.TimeIntensitiesGroup as RawTimeIntensities;
            if (rawTimeIntensitiesGroup == null)
            {
                return timeIntensities;
            }

            return timeIntensities.Interpolate(rawTimeIntensitiesGroup.GetInterpolatedTimes(),
                rawTimeIntensitiesGroup.InferZeroes);
        }

        public static TimeIntensities TransformInterpolatedTimeIntensities(TimeIntensities interpolatedTimeIntensities, TransformChrom transformChrom)
        {
            switch (transformChrom)
            {
                default:
                    return interpolatedTimeIntensities;
                case TransformChrom.craw1d:
                {
                    var peakFinder = Crawdads.NewCrawdadPeakFinder();
                    peakFinder.SetChromatogram(interpolatedTimeIntensities.Times, interpolatedTimeIntensities.Intensities);
                    return interpolatedTimeIntensities.ChangeIntensities(peakFinder.Intensities1d.ToArray());
                }
                case TransformChrom.craw2d:
                {
                    var peakFinder = Crawdads.NewCrawdadPeakFinder();
                    peakFinder.SetChromatogram(interpolatedTimeIntensities.Times, interpolatedTimeIntensities.Intensities);
                    return interpolatedTimeIntensities.ChangeIntensities(peakFinder.Intensities2d.ToArray());
                }
                case TransformChrom.savitzky_golay:
                {
                    return interpolatedTimeIntensities.ChangeIntensities(
                        SavitzkyGolaySmooth(interpolatedTimeIntensities.Intensities.ToArray()));
                }
            }
        }

        [Browsable(false)]
        public IList<float> Times { get { return TimeIntensities == null ? null : TimeIntensities.Times; } }
        [Browsable(false)]
        public IList<int> ScanIndexes { get { return TimeIntensities == null ? null : TimeIntensities.ScanIds; } }

        [Browsable(false)]
        public IList<float> Intensities
        {
            get { return TimeIntensities == null ? null : TimeIntensities.Intensities; }
        }

        [Browsable(false)]
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
                    string.Format(ResultsResources.ChromatogramInfo_ChromatogramInfo_The_index__0__must_be_between_0_and__1__,
                                  peakIndex, NumPeaks));
            }
            return _groupInfo.GetTransitionPeak(_transitionIndex, peakIndex);
        }

        public ChromPeak CalcPeak(PeakGroupIntegrator peakGroupIntegrator, float startTime, float endTime, ChromPeak.FlagValues flags)
        {
            var peakIntegrator = MakePeakIntegrator(peakGroupIntegrator);
            return peakIntegrator.IntegratePeak(startTime, endTime, flags);
        }

        public PeakIntegrator MakePeakIntegrator(PeakGroupIntegrator peakGroupIntegrator)
        {
            var rawTimeIntensities = RawTimeIntensities;
            var interpolatedTimeIntensities = GetTransformedTimeIntensities(TransformChrom.interpolated);
            return new PeakIntegrator(peakGroupIntegrator, ChromTransition.Source, rawTimeIntensities,
                interpolatedTimeIntensities, null);
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
                if (TimeIntervals == null)
                {
                    times = TimeIntensities.Times.Select(time => (double)time).ToArray();
                    intensities = TimeIntensities.Intensities.Select(intensity => (double)intensity).ToArray();
                }
                else
                {
                    var points = TimeIntervals.ReplaceExternalPointsWithNaN(Enumerable
                            .Range(0, TimeIntensities.NumPoints).Select(i =>
                                new KeyValuePair<float, float>(TimeIntensities.Times[i],
                                    TimeIntensities.Intensities[i])))
                        .ToList();
                    times = points.Select(p => (double) p.Key).ToArray();
                    intensities = points.Select(p => (double) p.Value).ToArray();
                }
            }
        }

        /// <summary>
        /// If this chromatogram came from a triggered acquisition then TimeIntervals represents the time
        /// over which data actually was collected.
        /// The TimeIntervals will be null if this chromatogram was not from a triggered acquisition.
        /// </summary>
        [Browsable(false)]
        public TimeIntervals TimeIntervals
        {
            get { return (GroupInfo?.TimeIntensitiesGroup as RawTimeIntensities)?.TimeIntervals; }
        }

        [Browsable(false)]
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
                _timeIntensities = TimeIntensities.MergeTimesAndAddIntensities(chromatogramInfo.TimeIntensities);
            }
        }

        public TransformChrom TransformChrom
        {
            get
            {
                return _transformChrom;
            }
        }

        public void Transform(TransformChrom transformChrom)
        {
            if (_transformChrom == transformChrom)
            {
                return;
            }

            var timeIntensities = _timeIntensities;
            if (timeIntensities != null)
            {
                if (transformChrom == TransformChrom.raw || _transformChrom == TransformChrom.craw1d || _transformChrom == TransformChrom.craw2d || _transformChrom == TransformChrom.savitzky_golay)
                {
                    throw new InvalidOperationException(string.Format(@"Cannot go from {0} to {1}", _transformChrom, transformChrom));
                }

                if (_transformChrom == TransformChrom.raw)
                {
                    timeIntensities = InterpolateTimeIntensities(timeIntensities);
                }

                timeIntensities = TransformInterpolatedTimeIntensities(timeIntensities, transformChrom);
            }
            _transformChrom = transformChrom;
            _timeIntensities = timeIntensities;
        }

        public TimeIntensities GetInterpolatedTimeIntensities()
        {
            var rawTimeIntensities = _groupInfo?.TimeIntensitiesGroup as RawTimeIntensities;
            if (rawTimeIntensities == null)
            {
                return TimeIntensities;
            }

            return rawTimeIntensities.TransitionTimeIntensities[TransitionIndex]
                .Interpolate(rawTimeIntensities.GetInterpolatedTimes(), rawTimeIntensities.InferZeroes);
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

        public float? GetBackgroundLevel(float peakStart, float peakEnd)
        {
            TimeIntensities interpolatedTimeIntensities = GetTransformedTimeIntensities(TransformChrom.interpolated);
            // Make sure that the TimeIntensities has an entry for both the start and end time
            interpolatedTimeIntensities =
                interpolatedTimeIntensities.InterpolateTime(peakStart).InterpolateTime(peakEnd);
            float startIntensity = interpolatedTimeIntensities.Intensities[interpolatedTimeIntensities.IndexOfNearestTime(peakStart)];
            float endIntensity =
                interpolatedTimeIntensities.Intensities[interpolatedTimeIntensities.IndexOfNearestTime(peakEnd)];
            return Math.Min(startIntensity, endIntensity);
        }
    }

    public class BulkReadException : IOException
    {
        public BulkReadException()
            : base(ResultsResources.BulkReadException_BulkReadException_Failed_reading_block_from_file)
        {
        }
    }
}
