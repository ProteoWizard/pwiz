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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;


namespace pwiz.Skyline.Model.Lib
{
    [XmlRoot("hunter_lib_spec")]
    public sealed class XHunterLibSpec : LibrarySpec
    {
        public const string EXT = ".hlf"; // Not L10N

        public static readonly PeptideRankId PEP_RANK_EXPECT =
            new PeptideRankId("Expect", Resources.XHunterLibSpec_PEP_RANK_EXPECT_Expect); // Not L10N

        public static readonly PeptideRankId PEP_RANK_PROCESSED_INTENSITY =
            new PeptideRankId("Processed intensity", Resources.XHunterLibSpec_PEP_RANK_PROCESSED_INTENSITY_Processed_intensity); // Not L10N

        private static readonly PeptideRankId[] RANK_IDS = { PEP_RANK_EXPECT, PEP_RANK_PROCESSED_INTENSITY };

        public XHunterLibSpec(string name, string path)
            : base(name, path)
        {
        }

        public override Library LoadLibrary(ILoadMonitor loader)
        {
            return XHunterLibrary.Load(this, loader);
        }

        public override IEnumerable<PeptideRankId> PeptideRankIds
        {
            get { return RANK_IDS; }
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private XHunterLibSpec()
        {
        }

        public static XHunterLibSpec Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new XHunterLibSpec());
        }

        #endregion
    }

    [XmlRoot("hunter_spectrum_info")]
    public sealed class XHunterSpectrumHeaderInfo : SpectrumHeaderInfo
    {
        public XHunterSpectrumHeaderInfo(string libraryName, float expect, float processedIntensity)
            : base(libraryName)
        {
            Expect = expect;
            ProcessedIntensity = processedIntensity;
        }

        public float ProcessedIntensity { get; private set; }
        public float Expect { get; private set; }

        public override float GetRankValue(PeptideRankId rankId)
        {
            if (ReferenceEquals(rankId, XHunterLibSpec.PEP_RANK_EXPECT))
                return -Expect;
            if (ReferenceEquals(rankId, XHunterLibSpec.PEP_RANK_PROCESSED_INTENSITY))
                return ProcessedIntensity;
            return base.GetRankValue(rankId);
        }

        public override IEnumerable<KeyValuePair<PeptideRankId, string>> RankValues
        {
            get
            {
                yield return new KeyValuePair<PeptideRankId, string>(XHunterLibSpec.PEP_RANK_EXPECT,
                    Expect.ToString(LocalizationHelper.CurrentCulture));
                yield return new KeyValuePair<PeptideRankId, string>(XHunterLibSpec.PEP_RANK_PROCESSED_INTENSITY,
                    ProcessedIntensity.ToString(LocalizationHelper.CurrentCulture));
            }
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For XML serialization
        /// </summary>
        private XHunterSpectrumHeaderInfo()
        {
        }

        private enum ATTR
        {
            expect,
            processed_intensity
        }

        public static XHunterSpectrumHeaderInfo Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new XHunterSpectrumHeaderInfo());
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);
            Expect = reader.GetFloatAttribute(ATTR.expect);
            ProcessedIntensity = reader.GetFloatAttribute(ATTR.processed_intensity);
            // Consume tag
            reader.Read();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);
            writer.WriteAttribute(ATTR.expect, Expect);
            writer.WriteAttribute(ATTR.processed_intensity, ProcessedIntensity);
        }

        #endregion

        #region object overrides

        public bool Equals(XHunterSpectrumHeaderInfo obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) && obj.ProcessedIntensity == ProcessedIntensity && obj.Expect == Expect;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as XHunterSpectrumHeaderInfo);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ ProcessedIntensity.GetHashCode();
                result = (result*397) ^ Expect.GetHashCode();
                return result;
            }
        }

        #endregion
    }

    [XmlRoot("hunter_library")]
    public sealed class XHunterLibrary : CachedLibrary<XHunterSpectrumInfo>
    {
        private const int FORMAT_VERSION_CACHE = 3;

        public const string DEFAULT_AUTHORITY = "thegpm.org"; // Not L10N

        public const string EXT_CACHE = ".slc"; // Not L10N 

        private static readonly Regex REGEX_HEADER = new Regex(@"HLF v=(\d+) s=([^ ]+) d=(.*\d\d\d\d\.\d\d.\d\d)"); // Not L10N
        private IPooledStream _readStream;

        public static XHunterLibrary Load(XHunterLibSpec spec, ILoadMonitor loader)
        {
            var library = new XHunterLibrary(spec);
            if (library.Load(loader))
                return library;
            return null;            
        }

        /// <summary>
        /// Controlled access to this <see cref="Immutable"/> class, which should be
        /// created through <see cref="Load(XHunterLibSpec,ILoadMonitor)"/>.
        /// </summary>
        private XHunterLibrary(LibrarySpec spec)
            : base(spec)
        {
            FilePath = spec.FilePath;
            
            string baseName = Path.GetFileNameWithoutExtension(FilePath) ?? string.Empty; // ReSharper
            CachePath = Path.Combine(Path.GetDirectoryName(FilePath) ?? string.Empty, baseName + EXT_CACHE);

        }

        public override LibrarySpec CreateSpec(string path)
        {
            return new XHunterLibSpec(Name, path);
        }

        public override string SpecFilter
        {
            get { return TextUtil.FileDialogFilterAll(Resources.XHunterLibrary_SpecFilter_GPM_Spectral_Library, XHunterLibSpec.EXT); }
        }

        /// <summary>
        /// A date string (yyyy.mm.dd) associate with the library.
        /// </summary>
        public string Revision { get; private set; }

        /// <summary>
        /// The name assigned to this library by the X! Hunter library builder.
        /// </summary>
        public string Id { get; private set; }

        public override LibraryDetails LibraryDetails
        {
            get
            {
                LibraryDetails details = new LibraryDetails { Format = "X!Hunter", PeptideCount = SpectrumCount }; // Not L10N

                if (!string.IsNullOrEmpty(Id))
                {
                    details.Id = Id;
                }
                if (!string.IsNullOrEmpty(Revision))
                {
                    details.Revision = Revision;
                }

                details.AddLink(LibraryLink.GPM);

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

        public override bool IsSameLibrary(Library library)
        {
            XHunterLibrary xlib = library as XHunterLibrary;
            if (xlib != null)
                return Equals(Id, xlib.Id);
            return false;
        }

        public override int CompareRevisions(Library library)
        {
            // Not a valid request, if the two libraries are not the same.
            Debug.Assert(IsSameLibrary(library));
            return string.CompareOrdinal(Revision, ((XHunterLibrary)library).Revision);
        }

        // ReSharper disable UnusedMember.Local
        private enum SpectrumHeaders1
        {
            precursor_mh_hi,
            precursor_mh_lo,
            charge,
            i2, // intensity?
            seq_len,

            count
        }

        private enum SpectrumHeaders2
        {
            precursor_mh_hi,
            precursor_mh_lo,
            charge,
            i2, // intensity?
            expect,
            seq_len,

            count
        }

        private enum LibHeaders
        {
            revision_byte_count,
            id_byte_count,
            format_version,
            num_spectra,
            location_headers_lo,
            location_headers_hi,

            count
        }

        private enum SpectrumCacheHeader
        {
            seq_key_hash,
            seq_key_length, 
            charge,
            i2,
            location_lo,
            location_hi,
            num_peaks,
            expect,
            seq_len,

            count
        }
       
        // ReSharper restore UnusedMember.Local

        private bool CreateCache(ILoadMonitor loader, ProgressStatus status, int percent)
        {
            var sm = loader.StreamManager;

            BufferedStream stream = new BufferedStream(CreateStream(loader), 32*1024);

            int version = 1;
            string id = string.Empty, revision = string.Empty;
            int size = ReadSize(stream);
            int i;
            if (size == 0)
            {
                version = 2;
                size = ReadSize(stream);

                const int countLibHeader = 256 - 8;
                byte[] libHeader = new byte[countLibHeader];
                if (stream.Read(libHeader, 0, libHeader.Length) != libHeader.Length)
                    throw new InvalidDataException(Resources.XHunterLibrary_CreateCache_Data_truncation_in_library_header_File_may_be_corrupted);

                for (i = 0; i < libHeader.Length; i++)
                {
                    if (libHeader[i] == 0)
                        break;
                }

                string header = Encoding.UTF8.GetString(libHeader, 0, i);
                Match match = REGEX_HEADER.Match(header);
                if (match.Success)
                {
                    version = int.Parse(match.Groups[1].Value);
                    id = match.Groups[2].Value;
                    revision = match.Groups[3].Value;
                }
            }
            var setLibKeys = new Dictionary<LibKey, bool>(size);
            var setSequences = new Dictionary<LibSeqKey, bool>(size);
            var libraryEntries = new List<XHunterSpectrumInfo>(size);

            const int countHeader = ((int) SpectrumHeaders2.count)*sizeof (int);
            byte[] specHeader = new byte[1024];
            byte[] specSequence = new byte[1024];
            i = 0;

            while (stream.Read(specHeader, 0, countHeader) == countHeader)
            {
                
                int percentComplete = (i++*percent/size);
                if (status.PercentComplete != percentComplete)
                {
                    // Check for cancellation after each integer change in percent loaded.
                    if (loader.IsCanceled)
                    {
                        loader.UpdateProgress(status.Cancel());
                        return false;
                    }

                    // If not cancelled, update progress.
                    loader.UpdateProgress(status = status.ChangePercentComplete(percentComplete));
                }

                int charge = (version == 1
                                  ? GetInt32(specHeader, ((int) SpectrumHeaders1.charge))
                                  : GetInt32(specHeader, ((int) SpectrumHeaders2.charge)));
                
                float i2 = (version == 1
                                ? GetSingle(specHeader, ((int) SpectrumHeaders1.i2))
                                : GetSingle(specHeader, ((int) SpectrumHeaders2.i2)));
                int seqLength = (version == 1
                                     ? GetInt32(specHeader, ((int) SpectrumHeaders1.seq_len))
                                     : GetInt32(specHeader, ((int) SpectrumHeaders2.seq_len)));
                
                float expect = (version == 1 ? 0.001f : GetSingle(specHeader, (int) SpectrumHeaders2.expect));
                

                // Read sequence information
                ReadComplete(stream, specSequence, seqLength);
                specSequence[seqLength] = 0;

                short numPeaks = (short) ReadSize(stream);

                // Save spectrum location
                long location = stream.Position;

                // Read over spectrum
                int countPeaks = (sizeof (byte) + sizeof (float))*numPeaks;
                stream.Seek(countPeaks, SeekOrigin.Current); // Skip spectrum

                // Read modifications
                int numMods = ReadSize(stream);

                byte[] sequence = specSequence;

                if (numMods > 0)
                {
                    StringBuilder sb = new StringBuilder();

                    ReadComplete(stream, specHeader, (4 + sizeof (double))*numMods);
                    int iLast = 0;
                    double modTotal = 0;
                    for (int j = 0; j < numMods; j++)
                    {
                        int iPos = GetInt32(specHeader, j*3);
                        double mod = BitConverter.ToDouble(specHeader, (j*3 + 1)*4);

                        // X! Hunter allows multiple modifications on the same
                        // residue.  So, they need to be totaled, and assigned to a
                        // single residue to allow them to match Skyline modification
                        // settings.
                        if (iPos > iLast)
                        {
                            if (modTotal != 0)
                                sb.Append(SequenceMassCalc.GetModDiffDescription(modTotal));
                            sb.Append(Encoding.UTF8.GetString(specSequence, iLast, iPos - iLast));

                            modTotal = 0;
                        }
                        modTotal += mod;
                        iLast = iPos;
                    }
                    if (modTotal != 0)
                        sb.Append(SequenceMassCalc.GetModDiffDescription(modTotal));
                    sb.Append(Encoding.UTF8.GetString(specSequence, iLast, seqLength - iLast));
                    sequence = Encoding.UTF8.GetBytes(sb.ToString());
                    seqLength = sb.Length;
                }

                // Skip over homologs (list of protein IDs and start positions from a FASTA
                // file used to generate the library)
                int numHomologs = ReadSize(stream);
                for (int j = 0; j < numHomologs; j++)
                    stream.Seek(ReadSize(stream) + 4, SeekOrigin.Current);

                // These libraries should not have duplicates, but just in case.
                // Apparently, GPM libraries do contain redundancies, as we found
                // when a revision lost this test.
                var key = new LibKey(sequence, 0, seqLength, charge);
                if (!setLibKeys.ContainsKey(key))
                {
                    setLibKeys.Add(key, true);
                    libraryEntries.Add(new XHunterSpectrumInfo(key, i2, expect, numPeaks, location));
                }
            }
           
            libraryEntries.Sort(CompareSpectrumInfo);

            using (FileSaver fs = new FileSaver(CachePath, sm))
            using (Stream outStream = sm.CreateStream(fs.SafeName, FileMode.Create, true))
            {

                foreach (var info in libraryEntries)
                {
                    LibSeqKey seqKey = new LibSeqKey(info.Key);
                    if (setSequences.ContainsKey(seqKey))
                    {
                        outStream.Write(BitConverter.GetBytes(0), 0, sizeof(int));
                        outStream.Write(BitConverter.GetBytes(-1), 0, sizeof(int));
                    }
                    else
                    {
                        // If it is unique, it will need to be added at cache load time.
                        setSequences.Add(seqKey, true);
                        outStream.Write(BitConverter.GetBytes(seqKey.GetHashCode()), 0, sizeof(int));
                        outStream.Write(BitConverter.GetBytes(seqKey.Length), 0, sizeof(int));
                    }
                    outStream.Write(BitConverter.GetBytes(info.Key.Charge), 0, sizeof (int));
                    outStream.Write(BitConverter.GetBytes(info.ProcessedIntensity), 0, sizeof (float));
                    outStream.Write(BitConverter.GetBytes(info.Location), 0, sizeof (long));
                    outStream.Write(BitConverter.GetBytes(info.NumPeaks), 0, sizeof (int));
                    outStream.Write(BitConverter.GetBytes(info.Expect), 0, sizeof (float));
                    info.Key.WriteSequence(outStream);
                }

                byte[] revisionBytes = Encoding.UTF8.GetBytes(revision);
                outStream.Write(revisionBytes, 0, revisionBytes.Length);
                byte[] idBytes = Encoding.UTF8.GetBytes(id);
                outStream.Write(idBytes, 0, idBytes.Length);
                outStream.Write(BitConverter.GetBytes(revisionBytes.Length), 0, sizeof(int));
                outStream.Write(BitConverter.GetBytes(idBytes.Length), 0, sizeof(int));
                outStream.Write(BitConverter.GetBytes(FORMAT_VERSION_CACHE), 0, sizeof(int));
                outStream.Write(BitConverter.GetBytes(libraryEntries.Count), 0, sizeof (int));
                outStream.Write(BitConverter.GetBytes((long) 0), 0, sizeof (long));

                sm.Finish(outStream);
                fs.Commit();
                sm.SetCache(FilePath, CachePath);
            }

            loader.UpdateProgress(status.Complete());

            return true;
        }

        private bool Load(ILoadMonitor loader)
        {
            ProgressStatus status = new ProgressStatus(string.Empty);
            loader.UpdateProgress(status);

            bool cached = loader.StreamManager.IsCached(FilePath, CachePath);
            if (Load(loader, status, cached))
                return true;

            // If loading from the cache failed, rebuild it.
            if (cached)
            {
                // Reset readStream so we don't read corrupt file.
                _readStream = null;
                if (Load(loader, status, false))
                    return true;
            }

            return false;
        }

        private bool Load(ILoadMonitor loader, ProgressStatus status, bool cached)
        {
            try
            {
                int loadPercent = 100;

                if (!cached)
                {
                    // Building the cache will take 95% of the load time.
                    loadPercent = 5;

                    status = status.ChangeMessage(string.Format(Resources.XHunterLibrary_Load_Building_binary_cache_for__0__library, Path.GetFileName(FilePath)));
                    status = status.ChangePercentComplete(0);

                    loader.UpdateProgress(status);

                    if (!CreateCache(loader, status, 100 - loadPercent))
                        return false;
                }

                status = status.ChangeMessage(string.Format(Resources.XHunterLibrary_Load_Loading__0__library, Path.GetFileName(FilePath)));
                loader.UpdateProgress(status);

                var sm = loader.StreamManager;
                using (Stream stream = sm.CreateStream(CachePath, FileMode.Open, true))
                {

                    // Read library header from the end of the cache
                    int countHeader = (int) LibHeaders.count*sizeof (int);
                    stream.Seek(-countHeader, SeekOrigin.End);

                    byte[] libHeader = new byte[countHeader];
                    ReadComplete(stream, libHeader, countHeader);

                    int version = GetInt32(libHeader, (int) LibHeaders.format_version);
                    if (version != FORMAT_VERSION_CACHE)
                        return false;

                    int countRevisionBytes = GetInt32(libHeader, (int)LibHeaders.revision_byte_count);
                    int countIdBytes = GetInt32(libHeader, (int)LibHeaders.id_byte_count);
                    stream.Seek(-countHeader - countRevisionBytes - countIdBytes, SeekOrigin.End);
                    Revision = ReadString(stream, countRevisionBytes);
                    Id = ReadString(stream, countIdBytes);

                    int numSpectra = GetInt32(libHeader, (int)LibHeaders.num_spectra);

                    var setSequences = new Dictionary<LibSeqKey, bool>(numSpectra);
                    var libraryEntries = new XHunterSpectrumInfo[numSpectra];

                    // Seek to beginning of spectrum headers
                    long locationHeaders = BitConverter.ToInt64(libHeader,
                                                                ((int) LibHeaders.location_headers_lo)*sizeof (int));
                    stream.Seek(locationHeaders, SeekOrigin.Begin);

                    byte[] specSequence = new byte[1024];
                    byte[] specHeader = new byte[1024];
                    countHeader = (int) SpectrumCacheHeader.count*4;

                    for (int i = 0; i < numSpectra; i++)
                    {
                        int percent = (100 - loadPercent) + (i*loadPercent/numSpectra);
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
                        ReadComplete(stream, specHeader, countHeader);

                        int seqKeyHash = GetInt32(specHeader, ((int) SpectrumCacheHeader.seq_key_hash));
                        int seqKeyLength = GetInt32(specHeader, ((int) SpectrumCacheHeader.seq_key_length));
                        int charge = GetInt32(specHeader, ((int)SpectrumCacheHeader.charge));
                        if (charge == 0 || charge > TransitionGroup.MAX_PRECURSOR_CHARGE)
                            throw new InvalidDataException(Resources.XHunterLibrary_Load_Invalid_precursor_charge_found_File_may_be_corrupted);
                        float i2 = BitConverter.ToSingle(specHeader, ((int) SpectrumCacheHeader.i2)*4);
                        long location = BitConverter.ToInt64(specHeader, ((int) SpectrumCacheHeader.location_lo)*4);
                        int numPeaks = GetInt32(specHeader, ((int) SpectrumCacheHeader.num_peaks));
                        float expect = BitConverter.ToSingle(specHeader, ((int) SpectrumCacheHeader.expect)*4);
                        int seqLength = GetInt32(specHeader, (int) SpectrumCacheHeader.seq_len);

                        // Read sequence information
                        ReadComplete(stream, specSequence, seqLength);
                        
                        LibKey key = new LibKey(specSequence, 0, seqLength, charge);
                        libraryEntries[i] = new XHunterSpectrumInfo(key, i2, expect, (short)numPeaks, location);
                        
                        if (seqKeyLength > 0)
                        {
                            LibSeqKey seqKey = new LibSeqKey(key, seqKeyHash, seqKeyLength);
                            setSequences.Add(seqKey, true);
                        }
                    }

                    // Checksum = checksum.ChecksumValue;
                    _libraryEntries = libraryEntries;
                    _setSequences = setSequences;
                    
                    loader.UpdateProgress(status.Complete());

                    // Create the stream from which the spectra will be read
                    CreateStream(loader);
                }

                return true;
            }
            catch (InvalidDataException x)
            {
                if (!cached)
                    loader.UpdateProgress(status.ChangeErrorException(x));
                return false;
            }
            catch (IOException x)
            {
                if (!cached)
                    loader.UpdateProgress(status.ChangeErrorException(x));
                return false;
            }
            catch (Exception x)
            {
                if (!cached)
                {
                    x = new Exception(string.Format(Resources.XHunterLibrary_Load_Failed_loading_library__0__, FilePath), x);
                    loader.UpdateProgress(status.ChangeErrorException(x));
                }
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
                    catch (IOException) { }
                }
            }
        }

        protected override SpectrumHeaderInfo CreateSpectrumHeaderInfo(XHunterSpectrumInfo info)
        {
            return new XHunterSpectrumHeaderInfo(Name, info.Expect, info.ProcessedIntensity);
        }

        protected override SpectrumPeaksInfo.MI[] ReadSpectrum(XHunterSpectrumInfo info)
        {
            const int lenPair = sizeof(byte) + sizeof(float);
            byte[] peaks = new byte[info.NumPeaks * lenPair];

            lock (ReadStream)
            {
                Stream fs = ReadStream.Stream;

                // Seek to stored location
                fs.Seek(info.Location, SeekOrigin.Begin);

                // Single read to get all the peaks
                if (fs.Read(peaks, 0, peaks.Length) < peaks.Length)
                    throw new IOException(Resources.XHunterLibrary_ReadSpectrum_Failure_trying_to_read_peaks);
            }

            // Build the list
            var arrayMI = new SpectrumPeaksInfo.MI[info.NumPeaks];

            // Read intensities
            for (int i = 0; i < info.NumPeaks; i++)
                arrayMI[i].Intensity = peaks[i];
            // Read m/z values
            for (int i = info.NumPeaks, iNext = 0; i < peaks.Length; i += sizeof(float))
                arrayMI[iNext++].Mz = BitConverter.ToSingle(peaks, i);

            return arrayMI;
        }

        #region Test functions

        /// <summary>
        /// Test only method for creating a <see cref="XHunterLibrary"/> file
        /// from another loaded <see cref="Library"/>.  Should this move into test project?
        /// </summary>
        /// <param name="streamManager">Provides access to the file system</param>
        /// <param name="path">Path to write to</param>
        /// <param name="library">The loaded library to use as a data source</param>
        /// <param name="lowIntensity">True to use 20 lowest intensity peaks for bad spectrum</param>
        public static void Write(IStreamManager streamManager, string path, Library library, bool lowIntensity)
        {
            using (FileSaver fs = new FileSaver(path, streamManager))
            using (Stream outStream = streamManager.CreateStream(fs.SafeName, FileMode.Create, true))
            {
                outStream.Write(BitConverter.GetBytes(0), 0, sizeof(int));
                outStream.Write(BitConverter.GetBytes(library.SpectrumCount), 0, sizeof(int));
                
                byte[] header = new byte[256 - 8];
                const string headerText = @"HLF v=2 s=test.hlf d=2009.02.04"; // Not L10N
                Encoding.UTF8.GetBytes(headerText, 0, headerText.Length, header, 0);

                outStream.Write(header, 0, header.Length);

                SequenceMassCalc calc = new SequenceMassCalc(MassType.Monoisotopic);

                byte[] seqBuffer = new byte[1024];

                foreach (var key in library.Keys)
                {
                    SpectrumPeaksInfo peaksInfo;
                    if (!library.TryLoadSpectrum(key, out peaksInfo))
                        continue;

                    // Fake X! Hunter filtering by choosing just the to 20 peaks
                    SpectrumPeaksInfo.MI[] peaks = peaksInfo.Peaks.ToArray();
                    // Sort by intensity
                    if (lowIntensity)
                        Array.Sort(peaks, (p1, p2) => Comparer.Default.Compare(p1.Intensity, p2.Intensity)); // ascending
                    else
                        Array.Sort(peaks, (p1, p2) => Comparer.Default.Compare(p2.Intensity, p1.Intensity)); // descending
                    float maxI = peaks.Length == 0 ? 0 : peaks[0].Intensity;
                    // Take 20 most intense peaks
                    SpectrumPeaksInfo.MI[] peaksFiltered = new SpectrumPeaksInfo.MI[Math.Min(20, peaks.Length)];
                    Array.Copy(peaks, peaksFiltered, peaksFiltered.Length);
                    // Resort by m/z (ineffient, but this is test code)
                    Array.Sort(peaksFiltered, (p1, p2) => Comparer.Default.Compare(p1.Mz, p2.Mz));

                    double totalI = 0;
                    byte[] peakBytes = new byte[(sizeof(byte) + sizeof(float))*peaksFiltered.Length];
                    for (int i = 0; i < peaksFiltered.Length; i++)
                    {
                        var mi = peaksFiltered[i];

                        // Calculate the X! Hunter processed intensity value
                        float intensity = 100f*mi.Intensity/maxI;
                        totalI += intensity;

                        // Fill the peaks buffer
                        peakBytes[i] = (byte)(int)intensity;
                        Array.Copy(BitConverter.GetBytes((float) mi.Mz), 0, peakBytes, peaksFiltered.Length + i*4, sizeof(float));
                    }
                        
                    string sequence = key.Sequence;
                    // Only works for unmodified sequence
                    Debug.Assert(!key.IsModified);
                    double precursorMH = calc.GetPrecursorMass(sequence);
                    outStream.Write(BitConverter.GetBytes(precursorMH), 0, sizeof(double));
                    outStream.Write(BitConverter.GetBytes(key.Charge), 0, sizeof(int));
                    // Value rounded for consistent serialization round-tripping
                    float i2 = (float) Math.Round(Math.Sqrt(totalI), 4);
                    outStream.Write(BitConverter.GetBytes(i2), 0, sizeof(float));
                    outStream.Write(BitConverter.GetBytes(0.0001f), 0, sizeof(float));
                    outStream.Write(BitConverter.GetBytes(sequence.Length), 0, sizeof(int));

                    // Sequence
                    Encoding.UTF8.GetBytes(sequence, 0, sequence.Length, seqBuffer, 0);
                    outStream.Write(seqBuffer, 0, sequence.Length);

                    // Peaks
                    outStream.Write(BitConverter.GetBytes(peaksFiltered.Length), 0, sizeof(int));
                    outStream.Write(peakBytes, 0, peakBytes.Length);

                    // Modifications
                    outStream.Write(BitConverter.GetBytes(0), 0, sizeof(int));
                    // Homologs
                    outStream.Write(BitConverter.GetBytes(0), 0, sizeof(int));
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
        private XHunterLibrary()
        {
        }

        private enum ATTR
        {
            id,
            revision
        }

        public static XHunterLibrary Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new XHunterLibrary());
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);
            Id = reader.GetAttribute(ATTR.id);
            Revision = reader.GetAttribute(ATTR.revision);
            // Consume tag
            reader.Read();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);
            writer.WriteAttributeIfString(ATTR.id, Id);
            writer.WriteAttributeIfString(ATTR.revision, Revision);
        }

        #endregion

        #region object overrides

        public bool Equals(XHunterLibrary obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) &&
                Equals(obj.Id, Id) &&
                Equals(obj.Revision, Revision) &&
                Equals(obj.FilePath, FilePath);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as XHunterLibrary);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ (Id != null ? Id.GetHashCode() : 0);
                result = (result*397) ^ (Revision != null ? Revision.GetHashCode() : 0);
                result = (result*397) ^ (FilePath != null ? FilePath.GetHashCode() : 0);
                return result;
            }
        }

        #endregion
    }

    public struct XHunterSpectrumInfo : ICachedSpectrumInfo
    {
        private readonly LibKey _key;
        private readonly float _processedIntensity;
        private readonly float _expect;
        private readonly short _numPeaks;
        private readonly long _location;

        public XHunterSpectrumInfo(LibKey key, float processedIntensity, float expect, short numPeaks, long location)
        {
            _key = key;
            _processedIntensity = processedIntensity;
            _expect = expect;
            _numPeaks = numPeaks;
            _location = location;
        }

        public LibKey Key { get { return _key; } }
        public float ProcessedIntensity { get { return _processedIntensity; } }
        public float Expect { get { return _expect; } }
        public int NumPeaks { get { return _numPeaks; } }
        public long Location { get { return _location; } }
    }
}

