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
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Lib
{
    [XmlRoot("hunter_lib_spec")]
    public sealed class XHunterLibSpec : LibrarySpec
    {
        public const string EXT = ".hlf";

        public static readonly PeptideRankId PEP_RANK_EXPECT = new PeptideRankId("Expect");
        public static readonly PeptideRankId PEP_RANK_PROCESSED_INTENSITY = new PeptideRankId("Processed intensity");

        private static readonly PeptideRankId[] RANK_IDS = new[] { PEP_RANK_EXPECT, PEP_RANK_PROCESSED_INTENSITY };

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
            else if (ReferenceEquals(rankId, XHunterLibSpec.PEP_RANK_PROCESSED_INTENSITY))
                return ProcessedIntensity;
            return base.GetRankValue(rankId);
        }

        public override IEnumerable<KeyValuePair<PeptideRankId, string>> RankValues
        {
            get
            {
                yield return new KeyValuePair<PeptideRankId, string>(XHunterLibSpec.PEP_RANK_EXPECT,
                    Expect.ToString());
                yield return new KeyValuePair<PeptideRankId, string>(XHunterLibSpec.PEP_RANK_PROCESSED_INTENSITY,
                    ProcessedIntensity.ToString());
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
    public sealed class XHunterLibrary : Library
    {
        public const string DEFAULT_AUTHORITY = "thegpm.org";

        private static readonly Regex REGEX_HEADER = new Regex(@"HLF v=(\d+) s=([^ ]+) d=(\d\d\d\d\.\d\d.\d\d)");
        private Dictionary<LibKey, XHunterSpectrumInfo> _dictLibrary;
        private HashSet<LibSeqKey> _setSequences;
        private IPooledStream _readStream;

        public static XHunterLibrary Load(XHunterLibSpec spec, ILoadMonitor loader)
        {
            var library = new XHunterLibrary(spec) { FilePath = spec.FilePath };
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
        }

        public override LibrarySpec CreateSpec(string path)
        {
            return new XHunterLibSpec(Name, path);
        }

        /// <summary>
        /// A date string (yyyy.mm.dd) associate with the library.
        /// </summary>
        public string Revision { get; private set; }

        /// <summary>
        /// The name assigned to this library by the X! Hunter library builder.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Path to the file on disk from which this library was loaded.  This value
        /// may be null, if the library was deserialized from XML and has not yet
        /// been loaded.
        /// </summary>
        public string FilePath { get; private set; }

        public override bool IsLoaded
        {
            get { return _dictLibrary != null; }
        }

        public override IPooledStream ReadStream { get { return _readStream; } }

        private Stream CreateStream(ILoadMonitor loader)
        {
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
            return string.Compare(Revision, ((XHunterLibrary)library).Revision);
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
        // ReSharper restore UnusedMember.Local

        private bool Load(ILoadMonitor loader)
        {
            ProgressStatus status = new ProgressStatus(string.Format("Loading {0} library", Path.GetFileName(FilePath)));
            loader.UpdateProgress(status);

            try
            {
                // Use a buffered stream for initial read
                BufferedStream stream = new BufferedStream(CreateStream(loader), 32 * 1024);

                int version = 1;
                int size = ReadSize(stream);
                int i;
                if (size == 0)
                {
                    version = 2;
                    size = ReadSize(stream);

                    byte[] libHeader = new byte[256 - 8];
                    if (stream.Read(libHeader, 0, libHeader.Length) != libHeader.Length)
                        throw new InvalidDataException("Data truncation in library header. File may be corrupted.");

                    for (i = 0; i < libHeader.Length; i++)
                    {
                        if (libHeader[i] == 0)
                            break;
                    }

                    string header = Encoding.Default.GetString(libHeader, 0, i);
                    Match match = REGEX_HEADER.Match(header);
                    if (match.Success)
                    {
                        version = int.Parse(match.Groups[1].Value);
                        Id = match.Groups[2].Value;
                        Revision = match.Groups[3].Value;
                    }
                }

                var dictLibrary = new Dictionary<LibKey, XHunterSpectrumInfo>(size);
                var setSequences = new HashSet<LibSeqKey>();

                int countHeader = (version == 1 ? (int) SpectrumHeaders1.count : (int) SpectrumHeaders2.count)*4;
                byte[] specHeader = new byte[1024];
                byte[] specSequence = new byte[1024];
                i = 0;
                while (stream.Read(specHeader, 0, countHeader) == countHeader)
                {
                    int percent = i++ * 100 / size;
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

                    int charge = GetInt32(specHeader, (int)SpectrumHeaders1.charge);
                    if (charge == 0 || charge > 10)
                        throw new InvalidDataException("Invalid precursor charge found. File may be corrupted.");

                    float i2 = GetSingle(specHeader, (int)SpectrumHeaders1.i2);
                    float expect = (version == 1 ? 0.001f : GetSingle(specHeader, (int) SpectrumHeaders2.expect));
                    int seqLength = GetInt32(specHeader, (version == 1 ?
                        (int)SpectrumHeaders1.seq_len : (int)SpectrumHeaders2.seq_len));

                    // Read sequence information
                    ReadComplete(stream, specSequence, seqLength);
                    specSequence[seqLength] = 0;

                    short numPeaks = (short) ReadSize(stream);

                    // Save spectrum location
                    long location = stream.Position;

                    // Read over spectrum
                    int countPeaks = (sizeof (byte) + sizeof (float))*numPeaks;
                    stream.Seek(countPeaks, SeekOrigin.Current);    // Skip spectrum
                    
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
                                sb.Append(Encoding.Default.GetString(specSequence, iLast, iPos - iLast));

                                modTotal = 0;
                            }
                            modTotal += mod;
                            iLast = iPos;
                        }
                        if (modTotal != 0)
                            sb.Append(SequenceMassCalc.GetModDiffDescription(modTotal));
                        sb.Append(Encoding.Default.GetString(specSequence, iLast, seqLength - iLast));
                        sequence = Encoding.Default.GetBytes(sb.ToString());
                        seqLength = sb.Length;
                    }

                    // Skip over homologs (list of protein IDs and start positions from a FASTA
                    // file used to generate the library)
                    int numHomologs = ReadSize(stream);
                    for (int j = 0; j < numHomologs; j++)
                        stream.Seek(ReadSize(stream) + 4, SeekOrigin.Current);

                    // These libraries should not have duplicates, but just in case.
                    // CONSIDER: Emit error about redundancy?
                    // These legacy libraries assume [+57.0] modified Cysteine
                    LibKey key = new LibKey(sequence, 0, seqLength, charge);
                    if (!dictLibrary.ContainsKey(key))
                        dictLibrary.Add(key, new XHunterSpectrumInfo(i2, expect, numPeaks, location));
                    setSequences.Add(new LibSeqKey(key));
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
                x = new Exception(string.Format("Failed loading library '{0}'.", FilePath), x);
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
                    catch (IOException) { }
                }
            }
        }

        private static int GetInt32(byte[] bytes, int index)
        {
            int ibyte = index*4;
            return bytes[ibyte] | bytes[ibyte + 1] << 8 | bytes[ibyte + 2] << 16 | bytes[ibyte + 3] << 24;
        }

        private static float GetSingle(byte[] bytes, int index)
        {
            return BitConverter.ToSingle(bytes, index*4);
        }

        private static int ReadSize(Stream stream)
        {
            byte[] libSize = new byte[4];
            ReadComplete(stream, libSize, libSize.Length);
            return GetInt32(libSize, 0);            
        }

        private static void ReadComplete(Stream stream, byte[] buffer, int size)
        {
            if (stream.Read(buffer, 0, size) != size)
                throw new InvalidDataException("Data truncation in library header. File may be corrupted.");            
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
            XHunterSpectrumInfo info;
            if (_dictLibrary != null && _dictLibrary.TryGetValue(key, out info))
            {
                libInfo = new XHunterSpectrumHeaderInfo(Name, info.Expect, info.ProcessedIntensity);
                return true;
            }
            libInfo = null;
            return false;
        }

        public override bool TryLoadSpectrum(LibKey key, out SpectrumPeaksInfo spectrum)
        {
            XHunterSpectrumInfo info;
            if (_dictLibrary != null && _dictLibrary.TryGetValue(key, out info))
            {
                spectrum = new SpectrumPeaksInfo(ReadSpectrum(info));
                return true;
            }

            spectrum = null;
            return false;
        }

        public override int Count
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

        private SpectrumPeaksInfo.MI[] ReadSpectrum(XHunterSpectrumInfo info)
        {
            Stream fs = ReadStream.Stream;

            // Seek to stored location
            fs.Seek(info.Location, SeekOrigin.Begin);

            // Single read to get all the peaks
            const int lenPair = sizeof(byte) + sizeof(float);
            byte[] peaks = new byte[info.NumPeaks * lenPair];
            if (fs.Read(peaks, 0, peaks.Length) < peaks.Length)
                throw new IOException("Failure trying to read peaks");

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
                outStream.Write(BitConverter.GetBytes(library.Count), 0, sizeof(int));
                
                byte[] header = new byte[256 - 8];
                const string headerText = @"HLF v=2 s=test.hlf d=2009.02.04";
                Encoding.Default.GetBytes(headerText, 0, headerText.Length, header, 0);

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
                    Encoding.Default.GetBytes(sequence, 0, sequence.Length, seqBuffer, 0);
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
            writer.WriteAttribute(ATTR.id, Id);
            writer.WriteAttribute(ATTR.revision, Revision);
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
                result = (result*397) ^ Id.GetHashCode();
                result = (result*397) ^ Revision.GetHashCode();
                result = (result*397) ^ (FilePath != null ? FilePath.GetHashCode() : 0);
                return result;
            }
        }

        #endregion
    }

    internal struct XHunterSpectrumInfo
    {
        private readonly float _processedIntensity;
        private readonly float _expect;
        private readonly short _numPeaks;
        private readonly long _location;

        public XHunterSpectrumInfo(float processedIntensity, float expect, short numPeaks, long location)
        {
            _processedIntensity = processedIntensity;
            _expect = expect;
            _numPeaks = numPeaks;
            _location = location;
        }

        public float ProcessedIntensity { get { return _processedIntensity; } }
        public float Expect { get { return _expect; } }
        public short NumPeaks { get { return _numPeaks; } }
        public long Location { get { return _location; } }
    }
}
