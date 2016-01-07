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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib
{
    [XmlRoot("bibliospec_lib_spec")]
    public sealed class BiblioSpecLibSpec : LibrarySpec
    {
        public const string EXT = ".lib"; // Not L10N

        private static readonly PeptideRankId[] RANK_IDS = { PEP_RANK_COPIES, PEP_RANK_PICKED_INTENSITY };

        public BiblioSpecLibSpec(string name, string path)
            : base(name, path)
        {
        }

        public override Library LoadLibrary(ILoadMonitor loader)
        {
            return BiblioSpecLibrary.Load(this, loader);
        }

        public override IEnumerable<PeptideRankId> PeptideRankIds
        {
            get { return RANK_IDS; }
        }

        #region Implementation of IXmlSerializable
        
        /// <summary>
        /// For serialization
        /// </summary>
        private BiblioSpecLibSpec()
        {
        }

        public static BiblioSpecLibSpec Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new BiblioSpecLibSpec());
        }

        #endregion
    }

    [XmlRoot("bibliospec_spectrum_info")]
    public sealed class BiblioSpecSpectrumHeaderInfo : SpectrumHeaderInfo
    {
        public BiblioSpecSpectrumHeaderInfo(string libraryName, int spectrumCount)
            : base(libraryName)
        {
            SpectrumCount = spectrumCount;
        }

        public int SpectrumCount { get; private set; }

        public override float GetRankValue(PeptideRankId rankId)
        {
            if (ReferenceEquals(rankId, LibrarySpec.PEP_RANK_COPIES))
                return SpectrumCount;
            return base.GetRankValue(rankId);
        }

        public override IEnumerable<KeyValuePair<PeptideRankId, string>> RankValues
        {
            get
            {
                yield return new KeyValuePair<PeptideRankId, string>(LibrarySpec.PEP_RANK_COPIES,
                    SpectrumCount.ToString(LocalizationHelper.CurrentCulture));
            }
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For XML serialization
        /// </summary>
        private BiblioSpecSpectrumHeaderInfo()
        {
        }

        private enum ATTR
        {
            count_measured
        }

        public static BiblioSpecSpectrumHeaderInfo Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new BiblioSpecSpectrumHeaderInfo());
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);
            SpectrumCount = reader.GetIntAttribute(ATTR.count_measured);
            // Consume tag
            reader.Read();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);
            writer.WriteAttribute(ATTR.count_measured, SpectrumCount);
        }

        #endregion

        #region object overrides

        public bool Equals(BiblioSpecSpectrumHeaderInfo obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) && obj.SpectrumCount == SpectrumCount;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as BiblioSpecSpectrumHeaderInfo);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode()*397) ^ SpectrumCount;
            }
        }

        #endregion
    }

    [XmlRoot("bibliospec_library")]
    public sealed class BiblioSpecLibrary : Library
    {
        public const string DEFAULT_AUTHORITY = "proteome.gs.washington.edu"; // Not L10N

        private bool _bigEndian;
        private bool _linuxFormat;
        private Dictionary<LibKey, BiblioSpectrumInfo> _dictLibrary;
        private HashSet<LibSeqKey> _setSequences;
        private IPooledStream _readStream;

        public static BiblioSpecLibrary Load(BiblioSpecLibSpec spec, ILoadMonitor loader)
        {
            var library = new BiblioSpecLibrary(spec) { FilePath = spec.FilePath };
            if (library.Load(loader))
                return library;
            return null;            
        }

        /// <summary>
        /// Controlled access to this <see cref="Immutable"/> class, which should be
        /// created through <see cref="Load(BiblioSpecLibSpec,ILoadMonitor)"/>.
        /// </summary>
        private BiblioSpecLibrary(LibrarySpec spec)
            : base(spec)
        {
        }

        public override LibrarySpec CreateSpec(string path)
        {
            return new BiblioSpecLibSpec(Name, path);
        }

        public override string SpecFilter
        {
            get { return TextUtil.FileDialogFilterAll(Resources.BiblioSpecLibrary_SpecFilter_Legacy_BiblioSpec_Library, BiblioSpecLibSpec.EXT); }
        }

        /// <summary>
        /// A monotonically increasing revision number associated with this library.
        /// </summary>
        public float Revision { get; private set; }

        public override LibraryDetails LibraryDetails
        {
            get
            {
                LibraryDetails details = new LibraryDetails
                {
                    Format = "BiblioSpec", // Not L10N
                    Revision = Revision.ToString(LocalizationHelper.CurrentCulture),
                    PeptideCount = SpectrumCount
                };

                return details;
            }
        }

        /// <summary>
        /// Path to the file on disk from which this library was loaded.  This value
        /// may be null, if the library was deserialized from XML and has not yet
        /// been loaded.
        /// </summary>
        public string FilePath { get; private set; }

        public override IPooledStream ReadStream { get { return _readStream; } }

        private Stream CreateStream(ILoadMonitor loader)
        {
            if (_readStream == null)
                _readStream = loader.StreamManager.CreatePooledStream(FilePath, false);
            return ReadStream.Stream;
        }

        public override string IsNotLoadedExplained
        {
            get { return (_dictLibrary != null) ? null : "BiblioSpec: no dictionary"; } // Not L10N
        }

        public override bool IsSameLibrary(Library library)
        {
            // Not really possible to tell with the old library format.
            return library is BiblioSpecLibrary;
        }

        public override int CompareRevisions(Library library)
        {
            // Not a valid request, if the two libraries are not the same.
            Debug.Assert(IsSameLibrary(library));
            float diff = Revision - ((BiblioSpecLibrary) library).Revision;
            return (diff == 0 ? 0 : (diff < 0 ? -1 : 1));
        }

        // ReSharper disable UnusedMember.Local
        private enum LibHeaders
        {
            num_spectra,
            filtered,
            version1,
            version2,
            next_id,

            count
        }

        private enum SpectrumHeaders
        {
            scan_num,
            scan_type,
            pre_mz,
            charge,
            r_time,
            num_peaks,
            peak_ptr,
            seq_len,
            annot,
            copies,
            lib_id,

            count
        }

        private enum SpectrumHeadersLinux
        {
            scan_num,
            scan_type,
            pre_mz,
            charge,
            r_time,
            num_peaks,
            peak_ptr,
            peak_ptr2,  // 64-bit pointer
            seq_len,
            annot,
            copies,
            lib_id,

            count
        }
        // ReSharper restore UnusedMember.Local

        private bool Load(ILoadMonitor loader)
        {
            ProgressStatus status =
                new ProgressStatus(string.Format(Resources.BiblioSpecLibrary_Load_Loading__0__library,
                                                 Path.GetFileName(FilePath)));
            loader.UpdateProgress(status);

            long lenRead = 0;
            // AdlerChecksum checksum = new AdlerChecksum();

            try
            {
                // Use a buffered stream for initial read
                BufferedStream stream = new BufferedStream(CreateStream(loader), 32 * 1024);

                int countHeader = (int) LibHeaders.count*4;
                byte[] libHeader = new byte[countHeader];
                if (stream.Read(libHeader, 0, countHeader) != countHeader)
                    throw new InvalidDataException(Resources.BiblioSpecLibrary_Load_Data_truncation_in_library_header_File_may_be_corrupted);
                lenRead += countHeader;
                // Check the first byte of the primary version number to determine
                // whether the format is little- or big-endian.  Little-endian will
                // have the version number in this byte, while big-endian will have zero.
                if (libHeader[(int) LibHeaders.version1 * 4] == 0)
                    _bigEndian = true;

                int numSpectra = GetInt32(libHeader, (int) LibHeaders.num_spectra);
                var dictLibrary = new Dictionary<LibKey, BiblioSpectrumInfo>(numSpectra);
                var setSequences = new HashSet<LibSeqKey>();

                string revStr = string.Format("{0}.{1}", // Not L10N
                                              GetInt32(libHeader, (int) LibHeaders.version1),
                                              GetInt32(libHeader, (int) LibHeaders.version2));
                Revision = float.Parse(revStr, CultureInfo.InvariantCulture);

                // checksum.MakeForBuff(libHeader, AdlerChecksum.ADLER_START);

                countHeader = (int) SpectrumHeaders.count*4;
                byte[] specHeader = new byte[1024];
                byte[] specSequence = new byte[1024];
                for (int i = 0; i < numSpectra; i++)
                {
                    int percent = i * 100 / numSpectra;
                    if (status.PercentComplete != percent)
                    {
                        // Check for cancellation after each integer change in percent loaded.
                        if (loader.IsCanceled)
                        {
                            loader.UpdateProgress(status.Cancel());
                            return false;
                        }

                        // If not cancelled, update progress.
                        loader.UpdateProgress(status = status.ChangePercentComplete(percent));                        
                    }

                    // Read spectrum header
                    int bytesRead = stream.Read(specHeader, 0, countHeader);
                    if (bytesRead != countHeader)
                        throw new InvalidDataException(Resources.BiblioSpecLibrary_Load_Data_truncation_in_spectrum_header_File_may_be_corrupted);

                    // If this is the first header, and the sequence length is zero,
                    // then this is a Linux format library.  Switch to linux format,
                    // and start over.
                    if (i == 0 && GetInt32(specHeader, (int)SpectrumHeaders.seq_len) == 0)
                    {
                        _linuxFormat = true;
                        stream.Seek(lenRead, SeekOrigin.Begin);

                        // Re-ead spectrum header
                        countHeader = (int)SpectrumHeadersLinux.count * 4;
                        bytesRead = stream.Read(specHeader, 0, countHeader);
                        if (bytesRead != countHeader)
                            throw new InvalidDataException(Resources.BiblioSpecLibrary_Load_Data_truncation_in_spectrum_header_File_may_be_corrupted);
                    }

                    lenRead += bytesRead;

                    // checksum.MakeForBuff(specHeader, checksum.ChecksumValue);
                    
                    int charge = GetInt32(specHeader, (int)SpectrumHeaders.charge);
                    if (charge > TransitionGroup.MAX_PRECURSOR_CHARGE)
                        throw new InvalidDataException(Resources.BiblioSpecLibrary_Load_Invalid_precursor_charge_found_File_may_be_corrupted);

                    int numPeaks = GetInt32(specHeader, (int)SpectrumHeaders.num_peaks);
                    int seqLength = GetInt32(specHeader, (_linuxFormat ?
                        (int)SpectrumHeadersLinux.seq_len : (int)SpectrumHeaders.seq_len));
                    int copies = GetInt32(specHeader, (_linuxFormat ?
                        (int)SpectrumHeadersLinux.copies : (int)SpectrumHeaders.copies));

                    // Read sequence information
                    int countSeq = (seqLength + 1)*2;
                    if (stream.Read(specSequence, 0, countSeq) != countSeq)
                        throw new InvalidDataException(Resources.BiblioSpecLibrary_Load_Data_truncation_in_spectrum_sequence_File_may_be_corrupted);

                    lenRead += countSeq;

                    // checksum.MakeForBuff(specSequence, checksum.ChecksumValue);

                    // Store in dictionary
                    if (IsUnmodified(specSequence, seqLength + 1, seqLength))
                    {
                        // These libraries should not have duplicates, but just in case.
                        // CONSIDER: Emit error about redundancy?
                        // These legacy libraries assume [+57.0] modified Cysteine
                        LibKey key = new LibKey(GetCModified(specSequence, ref seqLength), 0, seqLength, charge);
                        if (!dictLibrary.ContainsKey(key))
                            dictLibrary.Add(key, new BiblioSpectrumInfo((short)copies, (short)numPeaks, lenRead));
                        setSequences.Add(new LibSeqKey(key));
                    }

                    // Read over peaks
                    int countPeaks = 2*sizeof(Single)*numPeaks;
                    stream.Seek(countPeaks, SeekOrigin.Current);    // Skip spectrum
                    lenRead += countPeaks;

                    // checksum.MakeForBuff(specPeaks, checksum.ChecksumValue);
                }

                // Checksum = checksum.ChecksumValue;
                _dictLibrary = dictLibrary;
                _setSequences = setSequences;
                loader.UpdateProgress(status.Complete());
                return true;
            }
            catch (InvalidDataException x)
            {
                loader.UpdateProgress(status.ChangeErrorException(x));
                return false;
            }
            catch (IOException x)
            {
                loader.UpdateProgress(status.ChangeErrorException(x));
                return false;
            }
            catch (Exception x)
            {
                x = new Exception(string.Format(Resources.BiblioSpecLibrary_Load_Failed_loading_library__0__, FilePath), x);
                loader.UpdateProgress(status.ChangeErrorException(x));
                return false;
            }
            finally
            {
                if (ReadStream != null)
                {
                    // Close the read stream to ensure we never leak it.
                    // This only costs on extra open, the first time the
                    // active document tries to read.
                    try { ReadStream.CloseStream(); }
                    catch(IOException) {}
                }
            }
        }

        private static byte[] GetCModified(byte[] sequence, ref int len)
        {
            foreach (byte b in sequence)
            {
                if (b == 'C')
                {
                    // All C's in these libraries assume carbamidomehtyl cysteine
                    string seqString = Encoding.UTF8.GetString(sequence, 0, len);
                    byte[] bytes = Encoding.UTF8.GetBytes(seqString.Replace("C", "C[+57.0]")); // Not L10N
                    len = bytes.Length;
                    return bytes;
                }
            }
            return sequence;
        }

        private new int GetInt32(byte[] bytes, int index)
        {
            int ibyte = index*4;
            return _bigEndian ?
                bytes[ibyte] << 24 | bytes[ibyte + 1] << 16 | bytes[ibyte + 2] << 8 | bytes[ibyte + 3]
                :
                bytes[ibyte] | bytes[ibyte + 1] << 8 | bytes[ibyte + 2] << 16 | bytes[ibyte + 3] << 24;
        }

        private static bool IsUnmodified(byte[] mod, int start, int len)
        {
            for (int i = start; i < start + len; i++)
            {
                if (mod[i] != '0')
                    return false;
            }
            return true;
        }

        public override bool Contains(LibKey key)
        {
            return (_dictLibrary != null && _dictLibrary.ContainsKey(key));
        }

        public override bool ContainsAny(LibSeqKey key)
        {
            return (_setSequences != null && _setSequences.Contains(key));
        }

        public override bool TryGetLibInfo(LibKey key, out SpectrumHeaderInfo libInfo)
        {
            BiblioSpectrumInfo info;
            if (_dictLibrary != null && _dictLibrary.TryGetValue(key, out info))
            {
                libInfo = new BiblioSpecSpectrumHeaderInfo(Name, info.Copies);
                return true;
            }
            libInfo = null;
            return false;
        }

        public override bool TryLoadSpectrum(LibKey key, out SpectrumPeaksInfo spectrum)
        {
            BiblioSpectrumInfo info;
            if (_dictLibrary != null && _dictLibrary.TryGetValue(key, out info))
            {
                spectrum = new SpectrumPeaksInfo(ReadSpectrum(info));
                return true;
            }

            spectrum = null;
            return false;
        }

        public override SpectrumPeaksInfo LoadSpectrum(object spectrumKey)
        {
            return new SpectrumPeaksInfo(ReadSpectrum(_dictLibrary[(LibKey)spectrumKey]));
        }

        public override bool TryGetRetentionTimes(LibKey key, MsDataFileUri filePath, out double[] retentionTimes)
        {
            retentionTimes = null;
            return false;
        }

        public override bool TryGetRetentionTimes(MsDataFileUri filePath, out LibraryRetentionTimes retentionTimes)
        {
            retentionTimes = null;
            return false;
        }

        public override bool TryGetRetentionTimes(int fileIndex, out LibraryRetentionTimes retentionTimes)
        {
            retentionTimes = null;
            return false;
        }

        public override bool TryGetIrts(out LibraryRetentionTimes retentionTimes)
        {
            retentionTimes = null;
            return false;
        }

        // No ion mobility data in BiblioSpec libs (those are ancient - try BiblioSpecLite instead)
        public override bool TryGetIonMobilities(LibKey key, MsDataFileUri filePath, out IonMobilityInfo[] ionMobilities)
        {
            ionMobilities = null;
            return false;
        }

        public override bool TryGetIonMobilities(MsDataFileUri filePath, out LibraryIonMobilityInfo ionMobilities)
        {
            ionMobilities = null;
            return false;
        }

        public override bool TryGetIonMobilities(int fileIndex, out LibraryIonMobilityInfo ionMobilities)
        {
            ionMobilities = null;
            return false;
        }

        public override IEnumerable<SpectrumInfo> GetSpectra(LibKey key, IsotopeLabelType labelType, LibraryRedundancy redundancy)
        {
            // This base class only handles best match spectra
            SpectrumHeaderInfo libInfo;
            if (redundancy == LibraryRedundancy.best && TryGetLibInfo(key, out libInfo))
                yield return new SpectrumInfo(this, labelType, key) { SpectrumHeaderInfo = libInfo };
        }

        public override int? FileCount
        {
            get { return null; }
        }

        public override int SpectrumCount
        {
            get { return _dictLibrary == null ? 0 : _dictLibrary.Count; }
        }

        public override IEnumerable<LibKey> Keys
        {
            get
            {
                if (_dictLibrary != null)
                    foreach (var key in _dictLibrary.Keys)
                        yield return key;
            }
        }

        private SpectrumPeaksInfo.MI[] ReadSpectrum(BiblioSpectrumInfo info)
        {
            const int lenPair = sizeof(float) + sizeof(float);
            byte[] peaks = new byte[info.NumPeaks * lenPair];
            lock (ReadStream)
            {
                Stream fs = ReadStream.Stream;

                // Seek to stored location
                fs.Seek(info.Location, SeekOrigin.Begin);

                // Single read to get all the peaks
                if (fs.Read(peaks, 0, peaks.Length) < peaks.Length)
                    throw new IOException(Resources.BiblioSpecLibrary_ReadSpectrum_Failure_trying_to_read_peaks);
            }

            // Build the list
            var arrayMI = new SpectrumPeaksInfo.MI[info.NumPeaks];

            for (int i = 0, iNext = 0; i < peaks.Length; i += lenPair)
            {
                arrayMI[iNext].Intensity = BitConverter.ToSingle(peaks, i + sizeof (Single));
                arrayMI[iNext++].Mz = BitConverter.ToSingle(peaks, i);
            }

            return arrayMI;
        }

        #region Test functions

        /// <summary>
        /// Test only method for creating a <see cref="BiblioSpecLibrary"/> file
        /// from another loaded <see cref="Library"/>.  Should this move into test project?
        /// </summary>
        /// <param name="streamManager">Provides access to the file system</param>
        /// <param name="path">Path to write to</param>
        /// <param name="library">The loaded library to use as a data source</param>
        public static void Write(IStreamManager streamManager, string path, Library library)
        {
            using (FileSaver fs = new FileSaver(path, streamManager))
            using (Stream outStream = streamManager.CreateStream(fs.SafeName, FileMode.Create, true))
            {
                outStream.Write(BitConverter.GetBytes(library.SpectrumCount), 0, sizeof(int)); // num_spectra
                outStream.Write(BitConverter.GetBytes(0), 0, sizeof(int));             // filtered
                outStream.Write(BitConverter.GetBytes(1), 0, sizeof(int));             // version1
                outStream.Write(BitConverter.GetBytes(1), 0, sizeof(int));             // version2
                outStream.Write(BitConverter.GetBytes(0), 0, sizeof(int));             // next_id

                SequenceMassCalc calc = new SequenceMassCalc(MassType.Monoisotopic);

                byte[] seqBuffer = new byte[1024];

                int scanNum = 1;

                foreach (var key in library.Keys)
                {
                    SpectrumPeaksInfo peaksInfo;
                    if (!library.TryLoadSpectrum(key, out peaksInfo))
                        continue;

                    string sequence = key.Sequence;
                    // Only works for unmodified sequence
                    Debug.Assert(!key.IsModified);
                    double precursorMH = calc.GetPrecursorMass(sequence);
                    int charge = key.Charge;
                    float precursorMz = (float) SequenceMassCalc.GetMZ(precursorMH, charge);

                    outStream.Write(BitConverter.GetBytes(scanNum), 0, sizeof(int));      // scan_num
                    outStream.Write(BitConverter.GetBytes(2), 0, sizeof(int));              // scan_type
                    outStream.Write(BitConverter.GetBytes(precursorMz), 0, sizeof(float));  // pre_mz
                    outStream.Write(BitConverter.GetBytes(charge), 0, sizeof(int));         // scan_type
                    outStream.Write(BitConverter.GetBytes(0f), 0, sizeof(int));             // r_time
                    outStream.Write(BitConverter.GetBytes(peaksInfo.Peaks.Length), 0, sizeof(int)); // num_peaks
                    outStream.Write(BitConverter.GetBytes(0), 0, sizeof(int));               // 32-bit peak_ptr
                    outStream.Write(BitConverter.GetBytes(sequence.Length), 0, sizeof(int)); // seq_len
                    outStream.Write(BitConverter.GetBytes(0), 0, sizeof(int));               // annot
                    outStream.Write(BitConverter.GetBytes(scanNum), 0, sizeof(int));         // copies (bogus value for ranking)
                    outStream.Write(BitConverter.GetBytes(0), 0, sizeof(int));               // lib_id
                    scanNum++;

                    // Sequence
                    int len = sequence.Length;
                    seqBuffer[len] = 0;
                    Encoding.UTF8.GetBytes(sequence, 0, len, seqBuffer, 0);
                    outStream.Write(seqBuffer, 0, len + 1);
                    // Modifications
                    const string zeros = "000000000000000000000000000000000000000000000000000"; // Not L10N
                    Encoding.UTF8.GetBytes(zeros.Substring(0, len), 0, len, seqBuffer, 0);
                    outStream.Write(seqBuffer, 0, len + 1);
                    // Peaks
                    foreach (var mi in peaksInfo.Peaks)
                    {
                        outStream.Write(BitConverter.GetBytes((float)mi.Mz), 0, sizeof(float));
                        outStream.Write(BitConverter.GetBytes(mi.Intensity), 0, sizeof(float));
                    }
                }

                streamManager.Finish(outStream);
                fs.Commit();
            }
        }

        #endregion

        #region Implementation of IXmlSerializable
        
        /// <summary>
        /// For serialization
        /// </summary>
        private BiblioSpecLibrary()
        {
        }

        private enum ATTR
        {
        //    lsid,  old version has no unique identifier
            revision
        }

        public static BiblioSpecLibrary Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new BiblioSpecLibrary());
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);
            Revision = reader.GetFloatAttribute(ATTR.revision, 0);
            // Consume tag
            reader.Read();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);
            writer.WriteAttribute(ATTR.revision, Revision);
        }

        #endregion

        #region object overrides

        public bool Equals(BiblioSpecLibrary obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) && obj.Revision == Revision && Equals(obj.FilePath, FilePath);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as BiblioSpecLibrary);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ Revision.GetHashCode();
                result = (result*397) ^ (FilePath != null ? FilePath.GetHashCode() : 0);
                return result;
            }
        }

        #endregion
    }

    internal struct BiblioSpectrumInfo
    {
        private readonly short _copies;
        private readonly short _numPeaks;
        private readonly long _location;

        public BiblioSpectrumInfo(short copies, short numPeaks, long location)
        {
            _copies = copies;
            _numPeaks = numPeaks;
            _location = location;
        }

        public int Copies { get { return _copies; } }
        public int NumPeaks { get { return _numPeaks; } }
        public long Location { get { return _location; } }
    }
}
