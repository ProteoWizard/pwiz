using System;
using System.Runtime.InteropServices;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Skyline.Util;
using static pwiz.Skyline.Util.ValueChecking;


namespace pwiz.Skyline.Model.Results.Legacy
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ChromGroupHeaderInfo16
    {
        private const byte NO_MAX_PEAK = 0xFF;

        public int _textIdIndex;
        public int _startTransitionIndex;
        public int _startPeakIndex;
        public int _startScoreIndex;
        public int _numPoints;
        public int _compressedSize;
        public FlagValues _flagBits;
        public ushort _fileIndex;
        public ushort _textIdLen;
        public ushort _numTransitions;
        public byte _numPeaks;
        public byte _maxPeakIndex;
        public byte _isProcessedScans;
        private readonly byte _align1;
        public ushort _statusId;
        public ushort _statusRank;
        public double _precursor;
        public long _locationPoints;
        // V11 fields
        public int _uncompressedSize;
        public float _startTime;
        public float _endTime;
        public float _collisionalCrossSection;
        
        [Flags]
        public enum FlagValues : ushort
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

        public ChromGroupHeaderInfo16(ChromGroupHeaderInfo4 headerInfo) : this()
        {
            _precursor = headerInfo.Precursor;
            _fileIndex = CheckUShort(headerInfo.FileIndex);
            _numTransitions = CheckUShort(headerInfo.NumTransitions);
            _startTransitionIndex = headerInfo.StartTransitionIndex;
            _numPeaks = CheckByte(headerInfo.NumPeaks);
            _startPeakIndex = headerInfo.StartPeakIndex;
            _startScoreIndex = -1;
            if (headerInfo.MaxPeakIndex == -1)
            {
                _maxPeakIndex = NO_MAX_PEAK;
            }
            else
            {
                _maxPeakIndex = CheckByte(headerInfo.MaxPeakIndex);
            }
            _numPoints = headerInfo.NumPoints;
            _compressedSize = headerInfo.CompressedSize;
            _uncompressedSize = -1;
            _locationPoints = headerInfo.LocationPoints;
            _flagBits = 0;
            unchecked
            {
                _statusId = (ushort)-1;
                _statusRank = (ushort)-1;
            }

            _textIdIndex = -1;
            _startTime = -1;
            _endTime = -1;
        }

        public ChromGroupHeaderInfo16(ChromGroupHeaderInfo chromGroupHeaderInfo, int textIdIndex, ushort textIdLen) : this()
        {
            _textIdIndex = textIdIndex;
            _startTransitionIndex = chromGroupHeaderInfo.StartTransitionIndex;
            _startPeakIndex = chromGroupHeaderInfo.StartPeakIndex;
            _startScoreIndex = chromGroupHeaderInfo.StartScoreIndex;
            _numPoints = chromGroupHeaderInfo.NumPoints;
            _compressedSize = chromGroupHeaderInfo.CompressedSize;
            _flagBits = GetFlagValues(chromGroupHeaderInfo);
            _fileIndex = chromGroupHeaderInfo.FileIndex;
            _textIdLen = textIdLen;
            _numTransitions = chromGroupHeaderInfo.NumTransitions;
            _numPeaks = chromGroupHeaderInfo.NumPeaks;
            _maxPeakIndex = CheckByte(chromGroupHeaderInfo.MaxPeakIndex);
            _statusId = CheckUShort(-1, true);
            _statusRank = CheckUShort(-1, true);
            _precursor = chromGroupHeaderInfo.Precursor;
            _locationPoints = chromGroupHeaderInfo.LocationPoints;
            _uncompressedSize = chromGroupHeaderInfo.UncompressedSize;
            _startTime = chromGroupHeaderInfo.StartTime ?? -1;
            _endTime = chromGroupHeaderInfo.EndTime ?? -1;
            _collisionalCrossSection = chromGroupHeaderInfo.CollisionalCrossSection ?? -1;
        }

        public static FlagValues GetFlagValues(ChromGroupHeaderInfo chromGroupHeaderInfo)
        {
            FlagValues flagValues = 0;
            if (chromGroupHeaderInfo.HasMassErrors)
            {
                flagValues |= FlagValues.has_mass_errors;
            }

            if (chromGroupHeaderInfo.HasCalculatedMzs)
            {
                flagValues |= FlagValues.has_calculated_mzs;
            }
            switch (chromGroupHeaderInfo.Extractor)
            {
                case ChromExtractor.base_peak:
                    flagValues |= FlagValues.extracted_base_peak;
                    break;
                case ChromExtractor.qc:
                    flagValues |= FlagValues.extracted_qc_trace;
                    break;
            }

            if (chromGroupHeaderInfo.HasMs1ScanIds)
            {
                flagValues |= FlagValues.has_ms1_scan_ids;
            }

            if (chromGroupHeaderInfo.HasSimScanIds)
            {
                flagValues |= FlagValues.has_sim_scan_ids;
            }

            if (chromGroupHeaderInfo.HasFragmentScanIds)
            {
                flagValues |= FlagValues.has_frag_scan_ids;
            }

            if (chromGroupHeaderInfo.Precursor.IsNegative)
            {
                flagValues |= FlagValues.polarity_negative;
            }

            if (chromGroupHeaderInfo.HasRawChromatograms)
            {
                flagValues |= FlagValues.raw_chromatograms;
            }

            if (chromGroupHeaderInfo.IsDda)
            {
                flagValues |= FlagValues.dda_acquisition_method;
            }

            if (chromGroupHeaderInfo.IonMobilityUnits < 0)
            {
                flagValues |= FlagValues.ion_mobility_type_bitmask;
            }
            else
            {
                flagValues |= (FlagValues)((ushort)chromGroupHeaderInfo.IonMobilityUnits << 8);
            }

            return flagValues;
        }

        public static IItemSerializer<ChromGroupHeaderInfo16> ItemSerializer(int itemSizeOnDisk)
        {
            StructSerializer<ChromGroupHeaderInfo16> structSerializer = new StructSerializer<ChromGroupHeaderInfo16>
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
                }, chromGroupHeaderInfo => chromGroupHeaderInfo);
            }
            return structSerializer;
        }

        private static unsafe ChromGroupHeaderInfo16[] ReadArray(SafeHandle file, int count)
        {
            ChromGroupHeaderInfo16[] results = new ChromGroupHeaderInfo16[count];
            fixed (ChromGroupHeaderInfo16* p = results)
            {
                FastRead.ReadBytes(file, (byte*)p, sizeof(ChromGroupHeaderInfo16) * count);
            }

            return results;
        }
        private static unsafe void WriteArray(SafeHandle file, ChromGroupHeaderInfo16[] groupHeaders)
        {
            fixed (ChromGroupHeaderInfo16* p = groupHeaders)
            {
                FastWrite.WriteBytes(file, (byte*)p, sizeof(ChromGroupHeaderInfo16) * groupHeaders.Length);
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

        public static unsafe int SizeOf
        {
            get { return sizeof(ChromGroupHeaderInfo16); }
        }

        public ChromGroupHeaderInfo16 ChangeChargeToNegative()
        {
            // For dealing with pre-V11 caches where we didn't record chromatogram polarity
            var chromGroupHeaderInfo = this;
            chromGroupHeaderInfo._flagBits |= FlagValues.polarity_negative;
            return chromGroupHeaderInfo;
        }

        public eIonMobilityUnits IonMobilityUnits
        {
            get
            {
                var ionMobilityBits = _flagBits & FlagValues.ion_mobility_type_bitmask;
                if (ionMobilityBits == FlagValues.ion_mobility_type_bitmask)
                {
                    return (eIonMobilityUnits)(-1);
                }
                return (eIonMobilityUnits)((int)ionMobilityBits >> 8);
            }
        }

        public bool HasMassErrors => 0 != (_flagBits & FlagValues.has_mass_errors);
        public bool HasCalculatedMzs => 0 != (_flagBits & FlagValues.has_calculated_mzs);
        public bool HasMs1ScanIds => 0 != (_flagBits & FlagValues.has_ms1_scan_ids);
        public bool HasSimScanIds => 0 != (_flagBits & FlagValues.has_sim_scan_ids);
        public bool HasFragScanIds => 0 != (_flagBits & FlagValues.has_frag_scan_ids);
        public bool RawChromatograms => 0 != (_flagBits & FlagValues.raw_chromatograms);
        public bool IsDda => 0 != (_flagBits & FlagValues.dda_acquisition_method);
        public bool ExtractedBasePeak => 0 != (_flagBits & FlagValues.extracted_base_peak);
        public bool ExtractedQcTrace => 0 != (_flagBits & FlagValues.extracted_qc_trace);

        public SignedMz Precursor
        {
            get
            {
                return new SignedMz(_precursor, 0 != (_flagBits & FlagValues.polarity_negative));
            }
        }
    }
}
