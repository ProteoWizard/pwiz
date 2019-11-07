/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.IO;
using System.Runtime.InteropServices;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Results
{
    public enum CacheFormatVersion
    {
        Two = 2,
        Three = 3,
        Four = 4,
        Five = 5,
        Six = 6,
        Seven = 7, // Introduces UTF8 character support
        Eight = 8, // Introduces ion mobility data
        Nine = 9, // Introduces abbreviated scan ids
        Ten = 10, // Introduces waters lockmass correction in MSDataFileUri syntax
        Eleven = 11, // Adds chromatogram start, stop times, and uncompressed size info, and new flag bit for SignedMz
        Twelve = 12, // Adds structure sizes to CacheHeaderStruct
        Thirteen = 13,
        Fourteen = 14,
        CURRENT = Fourteen,
    }
    
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct CacheHeaderStruct
    {
        public int chromPeakSize;
        public int chromTransitionSize;
        public int chromGroupHeaderSize;
        public int cachedFileSize;
        public CacheFormatVersion versionRequired;
        // Version 9 fields after this point
        public long locationScanIds;
        // Version 5 fields after this point
        public int numScoreTypes;
        public int numScores;
        public long locationScores;
        public int numTextIdBytes;
        public long locationTextIdBytes;
        // Version 2 fields after this point
        public CacheFormatVersion formatVersion;
        public int numPeaks;
        public long locationPeaks;
        public int numTransitions;
        public long locationTransitions;
        public int numChromatograms;
        public long locationHeaders;
        public int numFiles;
        public long locationFiles;

        public static int GetStructSize(CacheFormatVersion cacheFormatVersion)
        {
            if (cacheFormatVersion >= WithStructSizes)
            {
                return Marshal.SizeOf<CacheHeaderStruct>();
            }
            if (cacheFormatVersion >= CacheFormatVersion.Nine)
            {
                return Marshal.SizeOf<CacheHeaderStruct>() - (int) Marshal.OffsetOf<CacheHeaderStruct>(@"locationScanIds");
            }
            if (cacheFormatVersion >= CacheFormatVersion.Five)
            {
                return Marshal.SizeOf<CacheHeaderStruct>() - (int) Marshal.OffsetOf<CacheHeaderStruct>(@"numScoreTypes");
            }
            return Marshal.SizeOf<CacheHeaderStruct>() - (int) Marshal.OffsetOf<CacheHeaderStruct>(@"formatVersion");
        }

        public static CacheHeaderStruct Read(Stream stream)
        {
            StructSerializer<CacheHeaderStruct> minimumStructSerializer = GetStructSerializer(CacheFormatVersion.Two);
            stream.Seek(-minimumStructSerializer.ItemSizeOnDisk, SeekOrigin.End);
            CacheHeaderStruct minimumStruct = minimumStructSerializer.ReadArray(stream, 1)[0];
            StructSerializer<CacheHeaderStruct> actualStructSerializer =
                GetStructSerializer(minimumStruct.formatVersion);
            if (actualStructSerializer.ItemSizeOnDisk == minimumStructSerializer.ItemSizeOnDisk)
            {
                return minimumStruct;
            }
            stream.Seek(-actualStructSerializer.ItemSizeOnDisk, SeekOrigin.End);
            return actualStructSerializer.ReadArray(stream, 1)[0];
        }

        public void Write(Stream stream)
        {
            GetStructSerializer(formatVersion).WriteItems(stream, new []{this});
        }

        public static StructSerializer<CacheHeaderStruct> GetStructSerializer(CacheFormatVersion cacheFormatVersion)
        {
            return new StructSerializer<CacheHeaderStruct>()
            {
                ItemSizeOnDisk = GetStructSize(cacheFormatVersion),
                PadFromStart = true
            };
        }

        public const CacheFormatVersion WithStructSizes = CacheFormatVersion.Twelve;

        public CacheHeaderStruct(CacheFormat cacheFormat) : this()
        {
            chromPeakSize = cacheFormat.ChromPeakSize;
            chromTransitionSize = cacheFormat.ChromTransitionSize;
            chromGroupHeaderSize = cacheFormat.ChromGroupHeaderSize;
            cachedFileSize = cacheFormat.CachedFileSize;
            formatVersion = cacheFormat.FormatVersion;
            versionRequired = cacheFormat.VersionRequired;
        }
    }

    /// <summary>
    /// Holds information about the format of a .skyd file. The format includes 
    /// which version number the file is, plus the sizes of the various structures
    /// persisted in the file.
    /// </summary>
    public class CacheFormat
    {
        public static readonly CacheFormat EMPTY = FromCacheHeader(new CacheHeaderStruct());
        public static readonly CacheFormat CURRENT = new CacheFormat
        {
            FormatVersion = CacheFormatVersion.CURRENT,
            VersionRequired = CacheFormatVersion.Fourteen,
            CachedFileSize = Marshal.SizeOf<CachedFileHeaderStruct>(),
            ChromGroupHeaderSize = Marshal.SizeOf<ChromGroupHeaderInfo>(),
            ChromPeakSize = Marshal.SizeOf<ChromPeak>(),
            ChromTransitionSize = Marshal.SizeOf<ChromTransition>()
        };

        private CacheFormat()
        {
        }

        public static CacheFormat FromVersion(CacheFormatVersion formatVersion)
        {
            if (formatVersion > CacheFormatVersion.CURRENT)
            {
                throw new NotSupportedException();
            }
            CacheFormatVersion versionRequired;
            if (formatVersion.CompareTo(CacheHeaderStruct.WithStructSizes) >= 0)
            {
                versionRequired = CacheHeaderStruct.WithStructSizes;
            }
            else
            {
                versionRequired = formatVersion;
            }
            return new CacheFormat
            {
                FormatVersion = formatVersion,
                VersionRequired = versionRequired,
                ChromPeakSize = ChromPeak.GetStructSize(formatVersion),
                ChromTransitionSize = ChromTransition.GetStructSize(formatVersion),
                CachedFileSize = CachedFileHeaderStruct.GetStructSize(formatVersion),
                ChromGroupHeaderSize = ChromGroupHeaderInfo.GetStructSize(formatVersion)
            };
        }


        public static CacheFormat FromCacheHeader(CacheHeaderStruct cacheHeader)
        {
            var formatVersion = cacheHeader.formatVersion;
            if (formatVersion >= CacheHeaderStruct.WithStructSizes)
            {
                return new CacheFormat()
                {
                    FormatVersion = formatVersion,
                    CachedFileSize = cacheHeader.cachedFileSize,
                    ChromGroupHeaderSize = cacheHeader.chromGroupHeaderSize,
                    ChromPeakSize = cacheHeader.chromPeakSize,
                    ChromTransitionSize = cacheHeader.chromTransitionSize,
                    VersionRequired = cacheHeader.versionRequired
                };
            }
            return FromVersion(formatVersion);
        }

        public CacheFormatVersion FormatVersion { get; private set; }
        /// <summary>
        /// Minimum version required to be able to read this file (always less than or equal to <see cref="FormatVersion"/>).
        /// </summary>
        public CacheFormatVersion VersionRequired { get; private set; }
        public int ChromPeakSize { get; private set; }
        public int ChromTransitionSize { get; private set; }
        public int ChromGroupHeaderSize { get; private set; }
        public int CachedFileSize { get; private set; }

        public IItemSerializer<CachedFileHeaderStruct> CachedFileSerializer()
        {
            return new StructSerializer<CachedFileHeaderStruct>
            {
                ItemSizeOnDisk = CachedFileSize
            };
        }

        public IItemSerializer<ChromGroupHeaderInfo> ChromGroupHeaderInfoSerializer()
        {
            if (FormatVersion >= CacheFormatVersion.Five)
            {
                return ChromGroupHeaderInfo.ItemSerializer(ChromGroupHeaderSize);
            }

            var v4Reader = ChromGroupHeaderInfo4.StructSerializer();
            return ConvertedItemSerializer.Create(v4Reader, v4Header => new ChromGroupHeaderInfo(v4Header), header=>new ChromGroupHeaderInfo4(header));
        }

        public IItemSerializer<ChromTransition> ChromTransitionSerializer()
        {
            if (FormatVersion > CacheFormatVersion.Six)
            {
                return ChromTransition.StructSerializer(ChromTransitionSize);
            }
            if (FormatVersion > CacheFormatVersion.Four)
            {
                return ConvertedItemSerializer.Create(
                    ChromTransition5.StructSerializer(),
                    chromTransition5 => new ChromTransition(chromTransition5),
                    chromTransition => new ChromTransition5(chromTransition));
            }
            return ConvertedItemSerializer.Create(ChromTransition4.StructSerializer(),
                chromTransition4 => new ChromTransition(chromTransition4),
                chromTransiton => new ChromTransition4(chromTransiton));
        }

        public IItemSerializer<ChromPeak> ChromPeakSerializer()
        {
            return ChromPeak.StructSerializer(ChromPeakSize);
        }
    }
}
