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
using pwiz.Common.SystemUtil;
using pwiz.Crawdad;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results
{
    public struct ChromGroupHeaderInfo : IComparable<ChromGroupHeaderInfo>
    {
        public ChromGroupHeaderInfo(float precursor, int fileIndex, int numTransitions, int startTransitionIndex,
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

        public int CompareTo(ChromGroupHeaderInfo info)
        {
            // Sort by key, and then file index.
            int keyCompare = Precursor.CompareTo(info.Precursor);
            if (keyCompare != 0)
                return keyCompare;
            return FileIndex - info.FileIndex;
        }

        #region Fast file I/O

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
        public static unsafe ChromGroupHeaderInfo[] ReadArray(Stream stream, int count)
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
                    // Fall through and attempt to read the slow way.
                }
            }

            ChromGroupHeaderInfo[] results = new ChromGroupHeaderInfo[count];
            int size = sizeof(ChromGroupHeaderInfo);
            byte[] buffer = new byte[size];
            for (int i = 0; i < count; ++i)
            {
                if (stream.Read(buffer, 0, size) != size)
                    throw new InvalidDataException();

                fixed (byte* pBuffer = buffer)
                {
                    results[i] = *(ChromGroupHeaderInfo*)pBuffer;
                }
            }

            return results;
        }

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
    }

    public struct ChromGroupHeaderInfo5 : IComparable<ChromGroupHeaderInfo5>
    {
        [Flags]
        public enum FlagValues
        {
            has_mass_errors = 0x01,
            has_calculated_mzs = 0x02,
            extracted_base_peak = 0x04,
            has_ms1_scan_ids = 0x08,
            has_sim_scan_ids = 0x10,
            has_frag_scan_ids = 0x20,
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
        public ChromGroupHeaderInfo5(double precursor, int fileIndex,
                                     int numTransitions, int startTransitionIndex,
                                     int numPeaks, int startPeakIndex, int startScoreIndex, int maxPeakIndex,
                                     int numPoints, int compressedSize, long location, FlagValues flags,
                                     int statusId, int statusRank)
            : this(precursor, -1, 0, fileIndex, numTransitions, startTransitionIndex,
                   numPeaks, startPeakIndex, startScoreIndex, maxPeakIndex, numPoints,
                   compressedSize, location, flags, statusId, statusRank)
        {            
        }

        /// <summary>
        /// Cunstructs header struct with all values populated.
        /// </summary>
        public ChromGroupHeaderInfo5(double precursor, int textIdIndex, int textIdLen, int fileIndex,
                                     int numTransitions, int startTransitionIndex,
                                     int numPeaks, int startPeakIndex, int startScoreIndex, int maxPeakIndex,
                                     int numPoints, int compressedSize, long location, FlagValues flags,
                                     int statusId, int statusRank)
            : this()
        {
            Precursor = precursor;
            TextIdIndex = textIdIndex;
            TextIdLen = CheckUShort(textIdLen);
            FileIndex = CheckUShort(fileIndex);
            NumTransitions = CheckUShort(numTransitions);
            StartTransitionIndex = startTransitionIndex;
            NumPeaks = CheckByte(numPeaks);
            StartPeakIndex = startPeakIndex;
            StartScoreIndex = startScoreIndex;
            MaxPeakIndexInternal = maxPeakIndex != -1 ? CheckByte(maxPeakIndex, byte.MaxValue - 1) : NO_MAX_PEAK;
            NumPoints = CheckUShort(numPoints);
            CompressedSize = compressedSize;
            LocationPoints = location;
            FlagBits = (ushort) flags;
            StatusId = CheckUShort(statusId, true);
            StatusRank = CheckUShort(statusRank, true);
            Align1 = 0;
        }

        public ChromGroupHeaderInfo5(ChromGroupHeaderInfo headerInfo)
            : this(headerInfo.Precursor,
            headerInfo.FileIndex,
            headerInfo.NumTransitions,
            headerInfo.StartTransitionIndex,
            headerInfo.NumPeaks,
            headerInfo.StartPeakIndex,
            -1,
            headerInfo.MaxPeakIndex,
            headerInfo.NumPoints,
            headerInfo.CompressedSize,
            headerInfo.LocationPoints,
            0, -1, -1)
        {
        }

        private static ushort CheckUShort(int value, bool allowNegativeOne = false)
        {
            return (ushort) CheckValue(value, ushort.MinValue, ushort.MaxValue, allowNegativeOne);
        }

        private static byte CheckByte(int value, int maxValue = byte.MaxValue)
        {
            return (byte) CheckValue(value, byte.MinValue, maxValue);
        }

        private static int CheckValue(int value, int min, int max, bool allowNegativeOne = false)
        {
            if (min > value || value > max)
            {
                if (!allowNegativeOne || value != -1)
                    throw new ArgumentOutOfRangeException(string.Format("The value {0} must be between {1} and {2}.", value, min, max)); // Not L10N?  Does user see this?
            }
            return value;
        }

        /////////////////////////////////////////////////////////////////////
        // CAREFUL: This ordering determines the layout of this struct on
        //          disk from which it gets loaded directly into memory.
        //          The order and size of each element has been very carefully
        //          considered to avoid wasted space due to alignment.
        public int TextIdIndex { get; private set; }
        public int StartTransitionIndex { get; private set; }
        public int StartPeakIndex { get; private set; }
        public int StartScoreIndex { get; private set; }
        public int NumPoints { get; private set; }
        public int CompressedSize { get; private set; }
        public ushort FlagBits { get; private set; }
        public ushort FileIndex { get; private set; }
        public ushort TextIdLen { get; private set; }
        public ushort NumTransitions { get; private set; }
        public byte NumPeaks { get; private set; }        // The number of peaks stored per chrom should be well under 128
        public byte MaxPeakIndexInternal { get; private set; }    // and MaxPeakIndex needs to be allowed to be -1 or 0xFF
        public ushort Align1 { get; private set; }
        public ushort StatusId { get; private set; }
        public ushort StatusRank { get; private set; }
        public double Precursor { get; private set; }
        public long LocationPoints { get; private set; }
        /////////////////////////////////////////////////////////////////////

        public short MaxPeakIndex
        {
            get
            {
                if (MaxPeakIndexInternal == NO_MAX_PEAK)
                    return -1;
                return MaxPeakIndexInternal;
            }
        }

        public FlagValues Flags { get { return (FlagValues) FlagBits; } }

        public bool HasCalculatedMzs { get { return (Flags & FlagValues.has_calculated_mzs) != 0; } }
        public bool HasMassErrors { get { return (Flags & FlagValues.has_mass_errors) != 0; } }
        public bool HasMs1ScanIds { get { return (Flags & FlagValues.has_ms1_scan_ids) != 0; } }
        public bool HasFragmentScanIds { get { return (Flags & FlagValues.has_frag_scan_ids) != 0; } }
        public bool HasSimScanIds { get { return (Flags & FlagValues.has_sim_scan_ids) != 0; } }

        public bool HasStatusId { get { return ((short)StatusId) != -1; } }
        public bool HasStatusRank { get { return ((short)StatusRank) != -1; } }
        
        public ChromExtractor Extractor
        {
            get
            {
                return (Flags & FlagValues.extracted_base_peak) != 0
                           ? ChromExtractor.base_peak
                           : ChromExtractor.summed;
            }
        }

        public void Offset(int offsetFiles, int offsetTransitions, int offsetPeaks, int offsetScores, long offsetPoints)
        {
            FileIndex += (ushort) offsetFiles;
            StartTransitionIndex += offsetTransitions;
            StartPeakIndex += offsetPeaks;
            if (StartScoreIndex != -1)
                StartScoreIndex += offsetScores;
            LocationPoints += offsetPoints;
        }

        public void ClearScores()
        {
            StartScoreIndex = -1;
        }

        public void CalcTextIdIndex(string textId,
            Dictionary<string, int> dictTextIdToByteIndex,
            List<byte> listTextIdBytes)
        {
            if (textId == null)
            {
                TextIdIndex = -1;
                TextIdLen = 0;
            }
            else
            {
                int textIdIndex;
                var textIdBytes = Encoding.UTF8.GetBytes(textId);
                if (!dictTextIdToByteIndex.TryGetValue(textId, out textIdIndex))
                {
                    textIdIndex = listTextIdBytes.Count;
                    listTextIdBytes.AddRange(textIdBytes);
                    dictTextIdToByteIndex.Add(textId, textIdIndex);
                }
                TextIdIndex = textIdIndex;
                TextIdLen = (ushort)textIdBytes.Length;
            }
        }

        public int CompareTo(ChromGroupHeaderInfo5 info)
        {
            // Sort by key, and then file index.
            int keyCompare = Precursor.CompareTo(info.Precursor);
            if (keyCompare != 0)
                return keyCompare;
            return FileIndex - info.FileIndex;
        }

        #region Fast file I/O

        public static ChromGroupHeaderInfo5[] ReadArray(Stream stream, int count, int formatVersion)
        {
            if (formatVersion > ChromatogramCache.FORMAT_VERSION_CACHE_4)
                return ReadArray(stream, count);

            var chrom4HeaderEntries = ChromGroupHeaderInfo.ReadArray(stream, count);
            var chromHeaderEntries = new ChromGroupHeaderInfo5[chrom4HeaderEntries.Length];
            for (int i = 0; i < chrom4HeaderEntries.Length; i++)
            {
                chromHeaderEntries[i] = new ChromGroupHeaderInfo5(chrom4HeaderEntries[i]);
            }
            return chromHeaderEntries;
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
        public static unsafe ChromGroupHeaderInfo5[] ReadArray(Stream stream, int count)
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
                    // Fall through and attempt to read the slow way.
                }
            }

            ChromGroupHeaderInfo5[] results = new ChromGroupHeaderInfo5[count];
            int size = sizeof(ChromGroupHeaderInfo5);
            byte[] buffer = new byte[size];
            for (int i = 0; i < count; ++i)
            {
                if (stream.Read(buffer, 0, size) != size)
                    throw new InvalidDataException();

                fixed (byte* pBuffer = buffer)
                {
                    results[i] = *(ChromGroupHeaderInfo5*)pBuffer;
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
        public static unsafe ChromGroupHeaderInfo5[] ReadArray(SafeHandle file, int count)
        {
            ChromGroupHeaderInfo5[] results = new ChromGroupHeaderInfo5[count];
            fixed (ChromGroupHeaderInfo5* p = results)
            {
                FastRead.ReadBytes(file, (byte*)p, sizeof(ChromGroupHeaderInfo5) * count);
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
        public static unsafe void WriteArray(SafeHandle file, ChromGroupHeaderInfo5[] groupHeaders)
        {
            fixed (ChromGroupHeaderInfo5* p = groupHeaders)
            {
                FastWrite.WriteBytes(file, (byte*)p, sizeof(ChromGroupHeaderInfo5) * groupHeaders.Length);
            }
        }

        #endregion

        public static unsafe int SizeOf
        {
            get { return sizeof (ChromGroupHeaderInfo5); }
        }

        public static unsafe int DeltaSize5 
        {
            get { return sizeof (ChromGroupHeaderInfo5) - sizeof (ChromGroupHeaderInfo); }
        }
    }


    public struct ChromTransition4
    {
        public ChromTransition4(float product) : this()
        {
            Product = product;
        }

        public float Product { get; private set; }

        #region Fast file I/O

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
        public static unsafe ChromTransition4[] ReadArray(Stream stream, int count)
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
            ChromTransition4[] results = new ChromTransition4[count];
            int size = sizeof (ChromTransition4);
            byte[] buffer = new byte[size];
            for (int i = 0; i < count; ++i)
            {
                if (stream.Read(buffer, 0, size) != size)
                    throw new InvalidDataException();

                fixed (byte* pBuffer = buffer)
                {
                    results[i] = *(ChromTransition4*) pBuffer;
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
        public static unsafe ChromTransition4[] ReadArray(SafeHandle file, int count)
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

        public static ChromTransition5[] ReadArray(Stream stream, int count, int formatVersion)
        {
            if (formatVersion > ChromatogramCache.FORMAT_VERSION_CACHE_4)
                return ReadArray(stream, count);

            var chrom4Transitions = ChromTransition4.ReadArray(stream, count);
            var chromTransitions = new ChromTransition5[chrom4Transitions.Length];
            for (int i = 0; i < chrom4Transitions.Length; i++)
            {
                chromTransitions[i] = new ChromTransition5(chrom4Transitions[i]);
            }
            return chromTransitions;
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
        public static unsafe ChromTransition5[] ReadArray(SafeHandle file, int count)
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
            return string.Format("{0:F04} - {1}", Product, Source); // Not L10N
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
    public struct ChromTransition 
    {
        [Flags]
        public enum FlagValues
        {
            source1 =       0x01,   // unknown = 00, fragment = 01
            source2 =       0x02,   // ms1     = 10, sim      = 11
        }

        public ChromTransition(double product, float extractionWidth, float ionMobilityValue, float ionMobilityExtractionWidth, ChromSource source) : this()
        {
            Product = product;
            ExtractionWidth = extractionWidth;
            IonMobilityValue = ionMobilityValue;
            IonMobilityExtractionWidth = ionMobilityExtractionWidth;
            Source = source;
            Align1 = 0;
        }

        public ChromTransition(ChromTransition5 chromTransition5) : this(chromTransition5.Product, 
            chromTransition5.ExtractionWidth, 0, 0, chromTransition5.Source)
        {            
        }

        public ChromTransition(ChromTransition4 chromTransition4)
            : this(chromTransition4.Product, 0, 0, 0, ChromSource.unknown)
        {            
        }

        public double Product { get; private set; }
        public float ExtractionWidth { get; private set; }  // In m/z
        public float IonMobilityValue { get; private set; } // Get the units (msec or area) from the source files
        public float IonMobilityExtractionWidth { get; private set; } // Get the units (msec or area) from the source files
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

        public static ChromTransition[] ReadArray(Stream stream, int count, int formatVersion)
        {
            if (formatVersion > ChromatogramCache.FORMAT_VERSION_CACHE_6)
                return ReadArray(stream, count);

            var chrom5Transitions = ChromTransition5.ReadArray(stream, count, formatVersion); // Falls through to v4 reader as needed
            var chromTransitions = new ChromTransition[chrom5Transitions.Length];
            for (int i = 0; i < chrom5Transitions.Length; i++)
            {
                chromTransitions[i] = new ChromTransition(chrom5Transitions[i]);
            }
            return chromTransitions;
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
        public static unsafe ChromTransition[] ReadArray(Stream stream, int count)
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
            ChromTransition[] results = new ChromTransition[count];
            int size = sizeof (ChromTransition);
            byte[] buffer = new byte[size];
            for (int i = 0; i < count; ++i)
            {
                if (stream.Read(buffer, 0, size) != size)
                    throw new InvalidDataException();

                fixed (byte* pBuffer = buffer)
                {
                    results[i] = *(ChromTransition*) pBuffer;
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
        public static unsafe ChromTransition[] ReadArray(SafeHandle file, int count)
        {
            ChromTransition[] results = new ChromTransition[count];
            fixed (ChromTransition* p = results)
            {
                FastRead.ReadBytes(file, (byte*)p, sizeof(ChromTransition) * count);
            }

            return results;
        }

        ///
        /// NOTE: writing is handled by ChromatogramCache::WriteStructs, so any members
        /// added here need to be added there - and in the proper order!
        /// 

        #endregion

        #region object overrides

        /// <summary>
        /// For debugging only
        /// </summary>
        public override string ToString()
        {
            return string.Format("{0:F04} {1:F04} - {2}", Product, IonMobilityValue, Source); // Not L10N
        }

        #endregion

    }

    public struct ChromPeak : ISummaryPeakData
    {
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

        public ChromPeak(CrawdadPeakFinder finder,
                         CrawdadPeak peak,
                         FlagValues flags,
                         IList<float> times,
                         IList<float> intensities,
                         IList<short> massErrors10X)
            : this()
        {
            // Get the interval being used to convert from Crawdad index based numbers
            // to numbers that are normalized with respect to time.
            double interval = times[peak.StartIndex + 1] - times[peak.StartIndex];

            RetentionTime = times[peak.TimeIndex];
            StartTime = times[peak.StartIndex];
            EndTime = times[peak.EndIndex];

            if ((flags & FlagValues.time_normalized) == 0 || finder.IsHeightAsArea)
            {
                Area = peak.Area;
                BackgroundArea = peak.BackgroundArea;
            }
            else
            {
                // Normalize area numbers by time in seconds, since this will be the least
                // dramatic change from Skyline v0.5, when the Crawdad index based areas
                // were used directly.
                double intervalSeconds = interval * 60;

                Area = (float)(peak.Area * intervalSeconds);
                BackgroundArea = (float) (peak.BackgroundArea * intervalSeconds);
            }
            Height = peak.Height;
            Fwhm = (float) (peak.Fwhm * interval);
            if (float.IsNaN(Fwhm))
                Fwhm = 0;
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
            if (massErrors10X != null)
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

                    double massErrorLocal = massErrors10X[i] / 10.0;
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
        }

        public float RetentionTime { get; private set; }
        public float StartTime { get; private set; }
        public float EndTime { get; private set; }
        public float Area { get; private set; }
        public float BackgroundArea { get; private set; }
        public float Height { get; private set; }
        public float Fwhm { get; private set; }
        public uint FlagBits { get; private set; }

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
            FlagBits = (uint) (Flags & ~FlagValues.mass_error_known);
            return this;
        }

        public static float Intersect(ChromPeak peak1, ChromPeak peak2)
        {
            return Intersect(peak1.StartTime, peak1.EndTime, peak2.StartTime, peak2.EndTime);
        }

        public static float Intersect(float startTime1, float endTime1, float startTime2, float endTime2)
        {
            return Math.Min(endTime1, endTime2) - Math.Max(startTime1, startTime2);
        }

        #region Fast file I/O

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
        public static unsafe ChromPeak[] ReadArray(Stream stream, int count)
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
                    // Fall through and attempt to read the slow way.
                }
            }

            ChromPeak[] results = new ChromPeak[count];
            int size = sizeof(ChromPeak);
            byte[] buffer = new byte[size];
            for (int i = 0; i < count; ++i)
            {
                if (stream.Read(buffer, 0, size) != size)
                    throw new InvalidDataException();

                fixed (byte* pBuffer = buffer)
                {
                    results[i] = *(ChromPeak*)pBuffer;
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
        public static unsafe ChromPeak[] ReadArray(SafeHandle file, int count)
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

        public static unsafe void WriteArray(SafeHandle file, ChromPeak[] headers, int startIndex, int count)
        {
            fixed (ChromPeak* p = &headers[startIndex])
            {
                FastWrite.WriteBytes(file, (byte*)p, sizeof(ChromPeak) * count);
            }
        }

        #endregion
    }

    public struct ChromCachedFile : IPathContainer
    {
        [Flags]
        public enum FlagValues
        {
            single_match_mz_known = 0x01,
            single_match_mz = 0x02,
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

        public ChromCachedFile(MsDataFileUri filePath, FlagValues flags, DateTime fileWriteTime, DateTime? runStartTime,
                               float maxRT, float maxIntensity, IEnumerable<MsInstrumentConfigInfo> instrumentInfoList)
            : this(filePath, flags, fileWriteTime, runStartTime, maxRT, maxIntensity, 0, 0, instrumentInfoList)
        {
        }

        public ChromCachedFile(MsDataFileUri filePath,
                               FlagValues flags,
                               DateTime fileWriteTime,
                               DateTime? runStartTime,
                               float maxRT,
                               float maxIntensity,
                               int sizeScanIds,
                               long locationScanIds,
                               IEnumerable<MsInstrumentConfigInfo> instrumentInfoList)
            : this()
        {
            FilePath = filePath;
            Flags = flags;
            FileWriteTime = fileWriteTime;
            RunStartTime = runStartTime;
            MaxRetentionTime = maxRT;
            MaxIntensity = maxIntensity;
            SizeScanIds = sizeScanIds;
            LocationScanIds = locationScanIds;
            InstrumentInfoList = instrumentInfoList;
        }

        public MsDataFileUri FilePath { get; private set; }
        public FlagValues Flags { get; private set; }
        public DateTime FileWriteTime { get; private set; }
        public DateTime? RunStartTime { get; private set; }
        public float MaxRetentionTime { get; private set; }
        public float MaxIntensity { get; private set; }
        public int SizeScanIds { get; private set; }
        public long LocationScanIds { get; private set; }
        public IEnumerable<MsInstrumentConfigInfo> InstrumentInfoList { get; private set; } 

        public bool IsCurrent
        {
            get { return Equals(FileWriteTime, GetLastWriteTime(FilePath)); }
        }

        public bool? IsSingleMatchMz
        {
            get { return IsSingleMatchMzFlags(Flags); }
        }

        public ChromCachedFile RelocateScanIds(long locationScanIds)
        {
            return new ChromCachedFile(FilePath, Flags, FileWriteTime, RunStartTime, MaxRetentionTime, MaxIntensity,
                SizeScanIds, locationScanIds, InstrumentInfoList);
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
        // Not L10N: Used for cache and testing
        public const string MODEL = "MODEL:"; // Not L10N
        public const string ANALYZER = "ANALYZER:"; // Not L10N
        public const string DETECTOR = "DETECTOR:"; // Not L10N
        public const string IONIZATION = "IONIZATION:"; // Not L10N

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
	                infoString.Append('\n'); // Not L10N

                // instrument model
                if(!string.IsNullOrWhiteSpace(configInfo.Model))
                {
                    infoString.Append(MODEL).Append(configInfo.Model).Append('\n'); // Not L10N
                }

                // ionization type
                if(!string.IsNullOrWhiteSpace(configInfo.Ionization))
                {
                    infoString.Append(IONIZATION).Append(configInfo.Ionization).Append('\n'); // Not L10N
                }

                // analyzer
                if (!string.IsNullOrWhiteSpace(configInfo.Analyzer))
                {
                    infoString.Append(ANALYZER).Append(configInfo.Analyzer).Append('\n'); // Not L10N
                }

                // detector
                if(!string.IsNullOrWhiteSpace(configInfo.Detector))
                {
                    infoString.Append(DETECTOR).Append(configInfo.Detector).Append('\n'); // Not L10N
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
            return Equals(f1.FilePath, f2.FilePath);
        }

        public int GetHashCode(TItem f)
        {
            return f.FilePath.GetHashCode();
        }
    }

    public enum ChromSource { fragment, sim, ms1, unknown  }

    public enum ChromExtractor { summed, base_peak }

    public class ChromKey : IComparable<ChromKey>
    {
        public static readonly ChromKey EMPTY = new ChromKey(null, 0, 0, 0,
            0, 0, 0, ChromSource.unknown, ChromExtractor.summed, false, false, null, null);

        public ChromKey(byte[] textIdBytes,
                        int textIdIndex,
                        int textIdLen,
                        double precursor,
                        double product,
                        double extractionWidth,
                        double ionMobilityValue,
                        double ionMobilityExtractionWidth,
                        ChromSource source,
                        ChromExtractor extractor,
                        bool calculatedMzs,
                        bool hasScanIds,
                        double? optionalMinTime,
                        double? optionalMaxTime)
            : this(textIdIndex != -1 ? Encoding.UTF8.GetString(textIdBytes, textIdIndex, textIdLen) : null,
                   precursor,
                   ionMobilityValue,
                   ionMobilityExtractionWidth,
                   product,
                   0,
                   extractionWidth,
                   source,
                   extractor,
                   calculatedMzs,
                   hasScanIds,
                   optionalMinTime,
                   optionalMaxTime)
        {
        }

        public ChromKey(string textId,
                        double precursor,
                        double? ionMobilityValue,
                        double ionMobilityExtractionWidth,
                        double product,
                        double ceValue,
                        double extractionWidth,
                        ChromSource source,
                        ChromExtractor extractor,
                        bool calculatedMzs,
                        bool hasScanIds,
                        double? optionalMinTime,
                        double? optionalMaxTime)
        {
            TextId = textId;
            Precursor = precursor;
            IonMobilityValue = (float)(ionMobilityValue ?? 0);
            IonMobilityExtractionWidth = (float)ionMobilityExtractionWidth;
            Product = product;
            CollisionEnergy = (float) ceValue;
            ExtractionWidth = (float) extractionWidth;
            Source = source;
            Extractor = extractor;
            HasCalculatedMzs = calculatedMzs;
            HasScanIds = hasScanIds;
            OptionalMinTime = optionalMinTime;
            OptionalMaxTime = optionalMaxTime;
        }

        public string TextId { get; private set; }  // Modified sequence or custom ion id
        public double Precursor { get; private set; }
        public float IonMobilityValue { get; private set; } // Dimensionless here - depends on source files
        public float IonMobilityExtractionWidth { get; private set; } // Dimensionless here - depends on source files
        public double Product { get; private set; }
        public float CollisionEnergy { get; private set; }
        public float ExtractionWidth { get; private set; }
        public ChromSource Source { get; private set; }
        public ChromExtractor Extractor { get; private set; }
        public bool HasCalculatedMzs { get; private set; }
        public bool HasScanIds { get; private set; }
        public double? OptionalMinTime { get; private set; }
        public double? OptionalMaxTime { get; private set; }

        public bool IsEmpty { get { return Precursor == 0 && Product == 0 && Source == ChromSource.unknown; } }

        /// <summary>
        /// Adjust the product m/z to look like it does for vendors that allow
        /// product m/z shifting for parameter optimization.
        /// </summary>
        /// <param name="step">The step from the central predicted parameter value</param>
        /// <returns>A new ChromKey with adjusted product m/z and cleared CE value</returns>
        public ChromKey ChangeOptimizationStep(int step)
        {
            return new ChromKey(TextId,
                                Precursor,
                                IonMobilityValue,
                                IonMobilityExtractionWidth,
                                Product + step*ChromatogramInfo.OPTIMIZE_SHIFT_SIZE,
                                0,
                                ExtractionWidth,
                                Source,
                                Extractor,
                                HasCalculatedMzs,
                                HasScanIds,
                                OptionalMinTime,
                                OptionalMaxTime);
        }

        /// <summary>
        /// For debugging only
        /// </summary>
        public override string ToString()
        {
            if (TextId != null)
                return string.Format("{0:F04}, {1:F04} im{4:F04} - {2} - {3}", Precursor, Product, Source, TextId, IonMobilityValue); // Not L10N
            return string.Format("{0:F04}, {1:F04} im{3:F04} - {2}", Precursor, Product, Source, IonMobilityValue); // Not L10N
        }

        public int CompareTo(ChromKey key)
        {
            return CompareTo(key, (mz1, mz2) => mz1.CompareTo(mz2));
        }

        public int CompareTolerant(ChromKey key, float tolerance)
        {
            return CompareTo(key, (mz1, mz2) => CompareTolerant(mz1, mz2, tolerance));
        }

        private int CompareTo(ChromKey key, Func<double, double, int> compareMz)
        {
            int c;

            // First deal with empty keys sorting to the end
            if (IsEmpty && key.IsEmpty)
                return 0;
            if (IsEmpty)
                return 1;
            if (key.IsEmpty)
                return -1;

            // Order by maximum retention time.
            if (OptionalMaxTime.HasValue != key.OptionalMaxTime.HasValue)
                return OptionalMaxTime.HasValue ? -1 : 1;
            if (OptionalMaxTime.HasValue && key.OptionalMaxTime.HasValue)
            {
                c = OptionalMaxTime.Value.CompareTo(key.OptionalMaxTime.Value);
                if (c != 0)
                    return c;
            }

            // Order by precursor values
            c = ComparePrecursors(key, compareMz);
            if (c != 0)
                return c;
            // Order by scan-type source, product m/z, extraction width
            c = CompareSource(key);
            if (c != 0)
                return c;
            c = compareMz(Product, key.Product);
            if (c != 0)
                return c;
            c = CollisionEnergy.CompareTo(key.CollisionEnergy);
            if (c != 0)
                return c;
            return ExtractionWidth.CompareTo(key.ExtractionWidth);
        }

        public int ComparePrecursors(ChromKey key)
        {
            return ComparePrecursors(key, (mz1, mz2) => mz1.CompareTo(mz2));
        }

        private int ComparePrecursors(ChromKey key, Func<double, double, int> compareMz)
        {
            // Order by precursor m/z, peptide sequence/custom ion id, extraction method
            int c = compareMz(Precursor, key.Precursor);
            if (c != 0)
                return c;
            c = CompareTextId(key);
            if (c != 0)
                return c;
            return Extractor - key.Extractor;
        }

        private int CompareTextId(ChromKey key)
        {
            if (TextId != null && key.TextId != null)
            {
                int c = string.CompareOrdinal(TextId, key.TextId);
                if (c != 0)
                    return c;
            }
            else if (TextId != null)
                return 1;
            else if (key.TextId != null)
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

        public static int CompareTolerant(double f1, double f2, float tolerance)
        {
            if (Math.Abs(f1 - f2) <= tolerance)
                return 0;
            return (f1 > f2 ? 1 : -1);
        }

        private const string SUFFIX_CE = "CE="; // Not L10N

        private static readonly Regex REGEX_ABI = new Regex(@"Q1=([^ ]+) Q3=([^ ]+) "); // Not L10N

        public static bool IsKeyId(string id)
        {
            return MsDataFileImpl.IsSingleIonCurrentId(id); // || id.StartsWith(PREFIX_TOTAL); Skip the TICs, since Skyline calculates these
        }

        public static ChromKey FromId(string id, bool parseCE)
        {
            try
            {
                if (MsDataFileImpl.IsNegativeChargeId(id).HasValue) // We currently ignore chromatogram polarity, but we may encounter it in mzML so must at least be able to recognize it
                    id = id.Substring(2);

                double precursor, product;
                if (id.StartsWith(MsDataFileImpl.PREFIX_TOTAL))
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
                        mzs = mzPart.Split(new[] { ',' }); // Not L10N
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
                return new ChromKey(null, precursor, 0, 0, product, ceValue, 0, ChromSource.fragment, ChromExtractor.summed, false, true, null, null);
            }
            catch (FormatException)
            {
                throw new InvalidDataException(string.Format(Resources.ChromKey_FromId_Invalid_chromatogram_ID__0__found_Failure_parsing_mz_values, id));
            }
        }

        #region object overrides

        public bool Equals(ChromKey other)
        {
            return string.Equals(TextId, other.TextId) &&
                Precursor.Equals(other.Precursor) &&
                IonMobilityValue.Equals(other.IonMobilityValue) &&
                IonMobilityExtractionWidth.Equals(other.IonMobilityExtractionWidth) &&
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
                var hashCode = (TextId != null ? TextId.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ Precursor.GetHashCode();
                hashCode = (hashCode*397) ^ IonMobilityValue.GetHashCode();
                hashCode = (hashCode*397) ^ IonMobilityExtractionWidth.GetHashCode();
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
        protected readonly ChromGroupHeaderInfo5 _groupHeaderInfo;
        protected readonly IDictionary<Type, int> _scoreTypeIndices;
        protected readonly IList<ChromCachedFile> _allFiles;
        protected readonly ChromTransition[] _allTransitions;
        protected readonly IReadOnlyList<ChromPeak> _allPeaks;
        protected readonly IReadOnlyList<float> _allScores;

        public ChromatogramGroupInfo(ChromGroupHeaderInfo5 groupHeaderInfo,
                                     IDictionary<Type, int> scoreTypeIndices,
                                     IList<ChromCachedFile> allFiles,
                                     ChromTransition[] allTransitions,
                                     IReadOnlyList<ChromPeak> allPeaks,
                                     IReadOnlyList<float> allScores)
        {
            _groupHeaderInfo = groupHeaderInfo;
            _scoreTypeIndices = scoreTypeIndices;
            _allFiles = allFiles;
            _allTransitions = allTransitions;
            _allPeaks = allPeaks;
            _allScores = allScores;
        }

        protected ChromatogramGroupInfo()
        {
        }

        internal ChromGroupHeaderInfo5 Header { get { return _groupHeaderInfo; } }
        public double PrecursorMz { get { return _groupHeaderInfo.Precursor; } }
        public MsDataFileUri FilePath { get { return _allFiles[_groupHeaderInfo.FileIndex].FilePath; } }
        public DateTime FileWriteTime { get { return _allFiles[_groupHeaderInfo.FileIndex].FileWriteTime; } }
        public DateTime? RunStartTime { get { return _allFiles[_groupHeaderInfo.FileIndex].RunStartTime; } }
        public int NumTransitions { get { return _groupHeaderInfo.NumTransitions; } }
        public int NumPeaks { get { return _groupHeaderInfo.NumPeaks; } }
        public int MaxPeakIndex { get { return _groupHeaderInfo.MaxPeakIndex; } }
        public int BestPeakIndex { get { return MaxPeakIndex; } }

        public float[] Times { get; set; }
        public float[][] IntensityArray { get; set; }
        public short[][] MassError10XArray { get; set; }
        public int[][] ScanIndexes { get; set; }

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
                for (int i = 0; i < _groupHeaderInfo.NumTransitions; i++)
                    yield return GetTransitionInfo(i);
            }
        }

        public ChromatogramInfo GetTransitionInfo(int index)
        {
            return new ChromatogramInfo(_groupHeaderInfo,
                                        _scoreTypeIndices,
                                        index,
                                        _allFiles,
                                        _allTransitions,
                                        _allPeaks,
                                        _allScores,
                                        Times,
                                        IntensityArray,
                                        MassError10XArray,
                                        ScanIndexes);
        }

        protected double GetProduct(int index)
        {
            return _allTransitions[index].Product;
        }

        protected ChromTransition GetChromTransition(int index)
        {
            return _allTransitions[index];
        }

        public ChromatogramInfo GetTransitionInfo(float productMz, float tolerance)
        {
            int startTran = _groupHeaderInfo.StartTransitionIndex;
            int endTran = startTran + _groupHeaderInfo.NumTransitions;
            int? iNearest = null;
            double deltaNearestMz = double.MaxValue;
            for (int i = startTran; i < endTran; i++)
            {
                if (ChromKey.CompareTolerant(productMz, GetProduct(i), tolerance) == 0)
                {
                    // If there is optimization data, return only the middle value, which
                    // was the regression value.
                    int iBegin = i;
                    while (i < endTran - 1 &&
                        ChromatogramInfo.IsOptimizationSpacing(GetProduct(i), GetProduct(i+1)))
                    {
                        i++;
                    }
                    int iMiddle = iBegin + (i - iBegin)/2;

                    double deltaMz = Math.Abs(productMz - GetProduct(iMiddle));
                    if (deltaMz < deltaNearestMz)
                    {
                        iNearest = iMiddle;
                        deltaNearestMz = deltaMz;
                    }
                }
            }
            return iNearest.HasValue
                       ? GetTransitionInfo(iNearest.Value - startTran)
                       : null;
        }

        public ChromatogramInfo[] GetAllTransitionInfo(float productMz, float tolerance, OptimizableRegression regression)
        {
            if (regression == null)
            {
                var info = GetTransitionInfo(productMz, tolerance);
                return info != null ? new[] { info } : new ChromatogramInfo[0];                
            }

            var listInfo = new List<ChromatogramInfo>();

            int startTran = _groupHeaderInfo.StartTransitionIndex;
            int endTran = startTran + _groupHeaderInfo.NumTransitions;
            for (int i = startTran; i < endTran; i++)
            {
                if (ChromKey.CompareTolerant(productMz, GetProduct(i), tolerance) == 0)
                {
                    // If there is optimization data, add it to the list
                    while (i < endTran - 1 &&
                        ChromatogramInfo.IsOptimizationSpacing(GetProduct(i), GetProduct(i+1)))
                    {
                        listInfo.Add(GetTransitionInfo(i - startTran));
                        i++;
                    }
                    // Add the last value, which may be the only value
                    listInfo.Add(GetTransitionInfo(i - startTran));
                }
            }

            return listInfo.ToArray();
        }

        public int IndexOfNearestTime(float time)
        {
            int iTime = Array.BinarySearch(Times, time);
            if (iTime < 0)
            {
                // Get index of first time greater than time argument
                iTime = ~iTime;
                // If the value before it was closer, then use that time
                if (iTime == Times.Length || (iTime > 0 && Times[iTime] - time > time - Times[iTime - 1]))
                    iTime--;
            }
            return iTime;
        }

        // ReSharper disable SuggestBaseTypeForParameter
        public int MatchTransitions(TransitionGroupDocNode nodeGroup, float tolerance, bool multiMatch)
        // ReSharper restore SuggestBaseTypeForParameter
        {
            int match = 0;
            foreach (TransitionDocNode nodeTran in nodeGroup.Children)
            {
                int start = _groupHeaderInfo.StartTransitionIndex;
                int end = start + _groupHeaderInfo.NumTransitions;
                for (int i = start; i < end; i++)
                {
                    if (ChromKey.CompareTolerant((float)nodeTran.Mz, GetProduct(i), tolerance) == 0)
                    {
                        match++;
                        if (!multiMatch)
                            break;  // only one match per transition
                    }
                }
            }
            return match;
        }

        public void ReadChromatogram(ChromatogramCache cache)
        {
            Stream stream = cache.ReadStream.Stream;
            byte[] pointsCompressed = new byte[_groupHeaderInfo.CompressedSize];
            lock(stream)
            {
                // Seek to stored location
                stream.Seek(_groupHeaderInfo.LocationPoints, SeekOrigin.Begin);

                // Single read to get all the points
                if (stream.Read(pointsCompressed, 0, pointsCompressed.Length) < pointsCompressed.Length)
                    throw new IOException(Resources.ChromatogramGroupInfo_ReadChromatogram_Failure_trying_to_read_points);
            }

            int numPoints = _groupHeaderInfo.NumPoints;
            int numTrans = _groupHeaderInfo.NumTransitions;
            bool hasErrors = _groupHeaderInfo.HasMassErrors;
            bool hasMs1ScanIds = _groupHeaderInfo.HasMs1ScanIds;
            bool hasFragmentScanIds = _groupHeaderInfo.HasFragmentScanIds;
            bool hasSimScanIds = _groupHeaderInfo.HasSimScanIds;

            int size = ChromatogramCache.GetChromatogramsByteCount(
                numTrans, numPoints, hasErrors,
                hasMs1ScanIds, hasFragmentScanIds, hasSimScanIds);
            byte[] peaks = pointsCompressed.Uncompress(size);

            float[] times;
            float[][] intensities;
            short[][] massErrors;
            int[][] scanIndexes;

            ChromatogramCache.BytesToTimeIntensities(
                peaks, numPoints, numTrans, hasErrors, hasMs1ScanIds, hasFragmentScanIds, hasSimScanIds,
                out times, out intensities, out massErrors, out scanIndexes);

            Times = times;
            IntensityArray = intensities;
            MassError10XArray = massErrors;
            ScanIndexes = scanIndexes;
        }

        public class PathEqualityComparer : IEqualityComparer<ChromatogramGroupInfo>
        {
            public bool Equals(ChromatogramGroupInfo x, ChromatogramGroupInfo y)
            {
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
    public enum TransformChrom { none, craw2d, craw1d, savitzky_golay }
// ReSharper restore InconsistentNaming

    public class ChromatogramInfo : ChromatogramGroupInfo
    {
        public const double OPTIMIZE_SHIFT_SIZE = 0.01;
        private const double OPTIMIZE_SHIFT_THRESHOLD = 0.001;

        public static bool IsOptimizationSpacing(double mz1, double mz2)
        {
            // Must be ordered correctly to be optimization spacing
            if (mz1 > mz2)
                return false;

            double delta = Math.Abs(Math.Abs(mz2 - mz1) - OPTIMIZE_SHIFT_SIZE);
            return delta < OPTIMIZE_SHIFT_THRESHOLD;
        }

        protected readonly int _transitionIndex;

        public ChromatogramInfo(ChromGroupHeaderInfo5 groupHeaderInfo,
                                IDictionary<Type, int> scoreTypeIndices,
                                int transitionIndex,
                                IList<ChromCachedFile> allFiles,
                                ChromTransition[] allTransitions,
                                IReadOnlyList<ChromPeak> allPeaks,
                                IReadOnlyList<float> allScores,
                                float[] times,
                                float[][] intensities,
                                short[][] massError10Xs,
                                int[][] scanIndexes)
            : base(groupHeaderInfo, scoreTypeIndices, allFiles, allTransitions, allPeaks, allScores)
        {
            if (transitionIndex >= _groupHeaderInfo.NumTransitions)
            {
                throw new IndexOutOfRangeException(
                    string.Format(Resources.ChromatogramInfo_ChromatogramInfo_The_index__0__must_be_between_0_and__1__,
                                  transitionIndex, _groupHeaderInfo.NumTransitions));
            }
            _transitionIndex = transitionIndex;

            Times = times;
            IntensityArray = intensities;
            if (intensities != null)
                Intensities = intensities[transitionIndex];
            MassError10XArray = massError10Xs;
            if (MassError10XArray != null)
                MassError10Xs = massError10Xs[transitionIndex];
            ScanIndexes = scanIndexes;
        }

        public ChromatogramInfo(float[] times, float[] intensities)
        {
            Times = times;
            Intensities = intensities;
        }

        public double ProductMz
        {
            get { return GetProduct(_groupHeaderInfo.StartTransitionIndex + _transitionIndex); }
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

        public double? IonMobilityValue
        {
            get { return FloatToNullableDouble(ChromTransition.IonMobilityValue); }
        }

        public double? IonMobilityExtractionWidth
        {
            get { return FloatToNullableDouble(ChromTransition.IonMobilityExtractionWidth); }
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
            get { return GetChromTransition(_groupHeaderInfo.StartTransitionIndex + _transitionIndex); }
        }

        public float[] Intensities { get; private set; }
        public short[] MassError10Xs { get; private set; }

        public IEnumerable<ChromPeak> Peaks
        {
            get
            {
                int startPeak = _groupHeaderInfo.StartPeakIndex + (_transitionIndex * _groupHeaderInfo.NumPeaks);
                int endPeak = startPeak + _groupHeaderInfo.NumPeaks;
                for (int i = startPeak; i < endPeak; i++)
                    yield return _allPeaks[i];
            }
        }

        public ChromPeak GetPeak(int peakIndex)
        {
            if (0 > peakIndex || peakIndex > _groupHeaderInfo.NumPeaks)
            {
                throw new IndexOutOfRangeException(
                    string.Format(Resources.ChromatogramInfo_ChromatogramInfo_The_index__0__must_be_between_0_and__1__,
                                  peakIndex, (int) _groupHeaderInfo.NumPeaks));
            }
            return _allPeaks[_groupHeaderInfo.StartPeakIndex + peakIndex + (_transitionIndex * _groupHeaderInfo.NumPeaks)];
        }

        public ChromPeak CalcPeak(int startIndex, int endIndex, ChromPeak.FlagValues flags)
        {
            if (startIndex == endIndex)
                return ChromPeak.EMPTY;

            CrawdadPeakFinder finder = new CrawdadPeakFinder();
            finder.SetChromatogram(Times, Intensities);
            var peak = finder.GetPeak(startIndex, endIndex);
            return new ChromPeak(finder, peak, flags, Times, Intensities, MassError10Xs);
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
            int len = Times.Length;
            times = new double[len];
            intensities = new double[len];
            for (int i = 0; i < len; i++)
            {
                times[i] = Times[i];
                intensities[i] = Intensities[i];
            }
        }

        public double MaxIntensity
        {
            get
            {
                double max = 0;
                foreach (float intensity in Intensities)
                    max = Math.Max(max, intensity);
                return max;
            }
        }

        public void SumIntensities(IList<ChromatogramInfo> listInfo)
        {
            var intensitiesNew = new float[Intensities.Length];
            foreach (var info in listInfo)
            {
                if (info == null)
                    continue;

                var intensitiesAdd = info.Intensities;
                for (int i = 0; i < intensitiesAdd.Length; i++)
                {
                    // Avoid arithmetic overflow
                    double intensitySum = intensitiesNew[i] + intensitiesAdd[i];
                    intensitiesNew[i] = intensitySum < float.MaxValue ? (float) intensitySum : float.MaxValue;
                }
            }
            Intensities = intensitiesNew;
        }

        public void Transform(TransformChrom transformChrom)
        {
            switch (transformChrom)
            {
                case TransformChrom.craw2d:
                    Crawdad2DTransform();
                    break;
                case TransformChrom.craw1d:
                    Crawdad1DTransform();
                    break;
                case TransformChrom.savitzky_golay:
                    SavitzkyGolaySmooth();
                    break;
            }
        }

        public void Crawdad2DTransform()
        {
            if (Intensities == null)
                return;
            var peakFinder = new CrawdadPeakFinder();
            peakFinder.SetChromatogram(Times, Intensities);
            Intensities = peakFinder.Intensities2d.ToArray();
        }

        public void Crawdad1DTransform()
        {
            if (Intensities == null)
                return;
            var peakFinder = new CrawdadPeakFinder();
            peakFinder.SetChromatogram(Times, Intensities);
            Intensities = peakFinder.Intensities1d.ToArray();
        }

        public void SavitzkyGolaySmooth()
        {
            Intensities = SavitzkyGolaySmooth(Intensities);
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
    }

    public class BulkReadException : IOException
    {
        public BulkReadException()
            : base(Resources.BulkReadException_BulkReadException_Failed_reading_block_from_file)
        {
        }
    }
}
