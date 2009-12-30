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
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib
{
    [XmlRoot("nist_lib_spec")]
    public sealed class NistLibSpec : NistLibSpecBase
    {
        public const string EXT = ".msp";

        public NistLibSpec(string name, string path)
            : base(name, path)
        {
        }

        public override Library LoadLibrary(ILoadMonitor loader)
        {
            return NistLibrary.Load(this, loader);
        }

        #region Implementation of IXmlSerializable
        
        /// <summary>
        /// For serialization
        /// </summary>
        private NistLibSpec()
        {
        }

        public static NistLibSpec Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new NistLibSpec());
        }

        #endregion

        #region object overrides

        public bool Equals(NistLibSpec other)
        {
            return base.Equals(other);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as NistLibSpec);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        #endregion
    }

    public abstract class NistLibSpecBase : LibrarySpec
    {
        public static readonly PeptideRankId PEP_RANK_TFRATIO = new PeptideRankId("TFRatio");

        private static readonly PeptideRankId[] RANK_IDS = new[]
            { PEP_RANK_COPIES, PEP_RANK_TOTAL_INTENSITY, PEP_RANK_PICKED_INTENSITY, PEP_RANK_TFRATIO};

        protected NistLibSpecBase(string name, string path)
            : base(name, path)
        {
        }

        public override IEnumerable<PeptideRankId> PeptideRankIds
        {
            get { return RANK_IDS; }
        }

        #region Implementation of IXmlSerializable
        
        /// <summary>
        /// For serialization
        /// </summary>
        protected NistLibSpecBase()
        {
        }

        #endregion
    }

    [XmlRoot("nist_spectrum_info")]
    public sealed class NistSpectrumHeaderInfo : NistSpectrumHeaderInfoBase
    {
        public NistSpectrumHeaderInfo(string libraryName, float tfRatio, float totalIntensity, int spectrumCount)
            : base(libraryName, tfRatio, totalIntensity, spectrumCount)
        {
        }

        #region Implementation of IXmlSerializable
        
        /// <summary>
        /// For serialization
        /// </summary>
        private NistSpectrumHeaderInfo()
        {
        }

        public static NistSpectrumHeaderInfo Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new NistSpectrumHeaderInfo());
        }

        #endregion
    }

    public class NistSpectrumHeaderInfoBase : SpectrumHeaderInfo
    {
        public NistSpectrumHeaderInfoBase(string libraryName, float tfRatio, float totalIntensity, int spectrumCount)
            : base(libraryName)
        {
            TFRatio = tfRatio;
            TotalIntensity = totalIntensity;
            SpectrumCount = spectrumCount;
        }

        public int SpectrumCount { get; private set; }
        public float TotalIntensity { get; private set; }
// ReSharper disable InconsistentNaming
        public float TFRatio { get; private set; }
// ReSharper restore InconsistentNaming

        public override float GetRankValue(PeptideRankId rankId)
        {
            if (ReferenceEquals(rankId, NistLibSpecBase.PEP_RANK_TFRATIO))
                return TFRatio;
            else if (ReferenceEquals(rankId, LibrarySpec.PEP_RANK_TOTAL_INTENSITY))
                return TotalIntensity;
            else if (ReferenceEquals(rankId, LibrarySpec.PEP_RANK_COPIES))
                return SpectrumCount;

            return base.GetRankValue(rankId);
        }

        public override IEnumerable<KeyValuePair<PeptideRankId, string>> RankValues
        {
            get
            {
                yield return new KeyValuePair<PeptideRankId, string>(NistLibSpecBase.PEP_RANK_TFRATIO,
                    TFRatio.ToString());
                yield return new KeyValuePair<PeptideRankId, string>(LibrarySpec.PEP_RANK_TOTAL_INTENSITY,
                    string.Format("{0:F0}", TotalIntensity));
                yield return new KeyValuePair<PeptideRankId, string>(LibrarySpec.PEP_RANK_COPIES,
                    SpectrumCount.ToString());
            }
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For XML serialization
        /// </summary>
        protected NistSpectrumHeaderInfoBase()
        {
        }

        private enum ATTR
        {
            count_measured,
            total_intensity,
            tfratio
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);
            SpectrumCount = reader.GetIntAttribute(ATTR.count_measured);
            TotalIntensity = reader.GetFloatAttribute(ATTR.total_intensity);
            TFRatio = reader.GetFloatAttribute(ATTR.tfratio);
            // Consume tag
            reader.Read();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);
            writer.WriteAttribute(ATTR.count_measured, SpectrumCount);
            writer.WriteAttribute(ATTR.total_intensity, TotalIntensity);
            writer.WriteAttribute(ATTR.tfratio, TFRatio);
        }

        #endregion

        #region object overrides

        public bool Equals(NistSpectrumHeaderInfoBase obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) && obj.SpectrumCount == SpectrumCount && obj.TotalIntensity == TotalIntensity && obj.TFRatio == TFRatio;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as NistSpectrumHeaderInfoBase);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ SpectrumCount;
                result = (result*397) ^ TotalIntensity.GetHashCode();
                result = (result*397) ^ TFRatio.GetHashCode();
                return result;
            }
        }

        #endregion
    }

    [XmlRoot("nist_library")]
    public sealed class NistLibrary : NistLibraryBase
    {
        public const string DEFAULT_AUTHORITY = "nist.gov";

        public const string EXT_CACHE = ".slc";

        public static NistLibrary Load(LibrarySpec spec, ILoadMonitor loader)
        {
            return (NistLibrary) Load(spec, new NistLibrary(spec), loader);            
        }

        /// <summary>
        /// Controlled access to this <see cref="Immutable"/> class, which should be
        /// created through <see cref="Load(LibrarySpec,ILoadMonitor)"/>.
        /// </summary>
        private NistLibrary(LibrarySpec spec)
            : base(spec, EXT_CACHE)
        {
        }

        protected override SpectrumHeaderInfo CreateSpectrumHeaderInfo(string libraryName,
            float tfRatio, float totalIntensity, int spectrumCount)
        {
            return new NistSpectrumHeaderInfo(libraryName, tfRatio, totalIntensity, spectrumCount);
        }

        public override LibrarySpec CreateSpec(string path)
        {
            return new NistLibSpec(Name, path);
        }

        #region Implementation of IXmlSerializable
        
        /// <summary>
        /// For serialization
        /// </summary>
        private NistLibrary()
        {
        }

        public static NistLibrary Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new NistLibrary());
        }

        #endregion

        #region object overrides

        public bool Equals(NistLibrary other)
        {
            return base.Equals(other);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as NistLibrary);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        #endregion
    }

    public abstract class NistLibraryBase : Library
    {
        private const int FORMAT_VERSION_CACHE = 1;

        private static readonly Regex REGEX_BASENAME = new Regex(@"NIST_(.*)_v(\d+\.\d+)_(\d\d\d\d\-\d\d-\d\d)");

        private static readonly Dictionary<string, string> MODIFICATION_MASSES = new Dictionary<string, string>
            {
                // Modification values taken from http://chemdata.nist.gov/mass-spc/ftp/mass-spc/PepLib.pdf
                {"Oxidation", SequenceMassCalc.GetModDiffDescription(15.994915)},
                {"Carbamidomethyl", SequenceMassCalc.GetModDiffDescription(57.02146)},            
                {"ICAT_light", SequenceMassCalc.GetModDiffDescription(227.12)},
                {"ICAT_heavy", SequenceMassCalc.GetModDiffDescription(236.12)},
                {"AB_old_ICATd0", SequenceMassCalc.GetModDiffDescription(442.20)},
                {"AB_old_ICATd8", SequenceMassCalc.GetModDiffDescription(450.20)},
                {"Acetyl", SequenceMassCalc.GetModDiffDescription(42.0106)},
                {"Deamidation", SequenceMassCalc.GetModDiffDescription(0.9840)},
                {"Pyro-cmC", SequenceMassCalc.GetModDiffDescription(-17.026549)},
                {"Pyro-glu", SequenceMassCalc.GetModDiffDescription(-17.026549)},
                {"Pyro_glu", SequenceMassCalc.GetModDiffDescription(-18.010565)},
                {"Amide", SequenceMassCalc.GetModDiffDescription(-0.984016)},
                {"Phospho", SequenceMassCalc.GetModDiffDescription(79.9663)},
                {"Methyl", SequenceMassCalc.GetModDiffDescription(14.0157)},
                {"Carbamyl", SequenceMassCalc.GetModDiffDescription(43.00581)},                                                                                         
            };

        private NistSpectrumInfo[] _libraryEntries;

        private IPooledStream _readStream;

        protected static Library Load(LibrarySpec spec, NistLibraryBase library, ILoadMonitor loader)
        {
            if (library.Load(loader))
                return library;
            return null;            
        }

        /// <summary>
        /// Controlled access to this <see cref="Immutable"/> class, which should be
        /// created through <see cref="Load(LibrarySpec,NistLibraryBase,ILoadMonitor)"/>.
        /// </summary>
        protected NistLibraryBase(LibrarySpec spec, string extCache)
            : base(spec)
        {
            FilePath = spec.FilePath;

            string baseName = Path.GetFileNameWithoutExtension(FilePath);
            CachePath = Path.Combine(Path.GetDirectoryName(FilePath), baseName + extCache);

            Match match = REGEX_BASENAME.Match(baseName);
            if (match.Success)
            {
                Id = match.Groups[1].Value;
                Revision = match.Groups[3].Value;
            }
        }

        protected abstract SpectrumHeaderInfo CreateSpectrumHeaderInfo(string name,
            float tfRatio, float intensity, int copies);

        /// <summary>
        /// A date string (yyyy-mm-dd) associate with the library.
        /// </summary>
        public string Revision { get; private set; }

        /// <summary>
        /// The ID name assigned to this library by NIST.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Path to the file on disk from which this library was loaded.  This value
        /// may be null, if the library was deserialized from XML and has not yet
        /// been loaded.
        /// </summary>
        public string FilePath { get; private set; }

        private string CachePath { get; set; }

        public override bool IsLoaded
        {
            get { return _libraryEntries != null; }
        }

        public override IPooledStream ReadStream { get { return _readStream; } }

        private Stream CreateStream(ILoadMonitor loader)
        {
            _readStream = loader.StreamManager.CreatePooledStream(CachePath, false);
            return ReadStream.Stream;
        }

        public override bool IsSameLibrary(Library library)
        {
            NistLibrary nlib = library as NistLibrary;
            if (nlib != null)
                return Equals(Id, nlib.Id);
            return false;
        }

        public override int CompareRevisions(Library library)
        {
            // Not a valid request, if the two libraries are not the same.
            Debug.Assert(IsSameLibrary(library));
            string libRevision = ((NistLibrary)library).Revision;
            if (Revision == null && libRevision == null)
                return 0;
            if (Revision == null)
                return -1;
            if (libRevision == null)
                return 1;
            return string.Compare(Revision, libRevision);
        }

        // ReSharper disable UnusedMember.Local
        private enum LibHeaders
        {
            format_version,
            num_spectra,
            location_headers_lo,
            location_headers_hi,

            count
        }

        private enum SpectrumHeaders
        {
//            pre_mz,
            charge,
            tf_ratio,
            total_intensity,
            copies,
            num_peaks,
            compressed_size,
            location_peaks_lo,
            location_peaks_hi,
            seq_len,

            count
        }
        // ReSharper restore UnusedMember.Local

        private bool Load(ILoadMonitor loader)
        {
            ProgressStatus status = new ProgressStatus("");
            loader.UpdateProgress(status);

            bool cached = loader.StreamManager.IsCached(FilePath, CachePath);
            if (Load(loader, status, cached))
                return true;

            // If loading from the cache failed, rebuild it.
            if (cached)
            {
                // TODO: Necessary?
                ReadStream.CloseStream();
                if (Load(loader, status, false))
                    return true;
                ReadStream.CloseStream();
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

                    status = status.ChangeMessage(string.Format("Building binary cache for {0} library", Path.GetFileName(FilePath)));
                    status = status.ChangePercentComplete(0);

                    loader.UpdateProgress(status);

                    if (!CreateCache(loader, status, 100 - loadPercent))
                        return false;
                }

                status = status.ChangeMessage(string.Format("Loading {0} library", Path.GetFileName(FilePath)));
                loader.UpdateProgress(status);

                // Use a buffered stream for initial read
                BufferedStream stream = new BufferedStream(CreateStream(loader), 32 * 1024);

                // Read library header from the end of the cache
                int countHeader = (int) LibHeaders.count*4;
                stream.Seek(-countHeader, SeekOrigin.End);

                byte[] libHeader = new byte[countHeader];
                ReadComplete(stream, libHeader, countHeader);

                int formatVersion = GetInt32(libHeader, (int)LibHeaders.format_version);
                if (formatVersion != FORMAT_VERSION_CACHE)
                    return false;

                int numSpectra = GetInt32(libHeader, (int) LibHeaders.num_spectra);
                var libraryEntries = new NistSpectrumInfo[numSpectra];

                // Seek to beginning of spectrum headers
                long locationHeaders = BitConverter.ToInt64(libHeader, ((int)LibHeaders.location_headers_lo)*4);
                stream.Seek(locationHeaders, SeekOrigin.Begin);

                countHeader = (int) SpectrumHeaders.count*4;
                byte[] specHeader = new byte[1024];
                byte[] specSequence = new byte[1024];
                for (int i = 0; i < numSpectra; i++)
                {
                    int percent = (100 - loadPercent) + (i * loadPercent / numSpectra);
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

                    int charge = GetInt32(specHeader, (int)SpectrumHeaders.charge);
                    if (charge == 0 || charge > 10)
                        throw new InvalidDataException("Invalid precursor charge found. File may be corrupted.");

                    float tfRatio = BitConverter.ToSingle(specHeader, ((int) SpectrumHeaders.tf_ratio)*4);
                    float totalIntensity = BitConverter.ToSingle(specHeader, ((int)SpectrumHeaders.total_intensity) * 4);
                    int copies = GetInt32(specHeader, (int)SpectrumHeaders.copies);                    
                    int numPeaks = GetInt32(specHeader, (int)SpectrumHeaders.num_peaks);
                    int compressedSize = GetInt32(specHeader, (int)SpectrumHeaders.compressed_size);
                    long location = BitConverter.ToInt64(specHeader, ((int) SpectrumHeaders.location_peaks_lo)*4);
                    int seqLength = GetInt32(specHeader, (int)SpectrumHeaders.seq_len);

                    // Read sequence information
                    ReadComplete(stream, specSequence, seqLength);

                    // Add new entry
                    LibKey key = new LibKey(specSequence, 0, seqLength, charge);
                    libraryEntries[i] = new NistSpectrumInfo(key, tfRatio, totalIntensity,
                                                              (short)copies, (short)numPeaks, (short)compressedSize, location);
                }

                // Checksum = checksum.ChecksumValue;
                _libraryEntries = libraryEntries;
                loader.UpdateProgress(status.Complete());
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
                    x = new Exception(string.Format("Failed loading library '{0}'.", FilePath), x);
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

        private static readonly Regex REGEX_NAME = new Regex(@"^Name: ([A-Z()\[\]0-9]+)/(\d)"); // NIST libraries can contain M(O) and SpectraST M[16]
        private static readonly Regex REGEX_NUM_PEAKS = new Regex(@"^Num ?[pP]eaks: (\d+)");  // NIST uses "Num peaks" and SpectraST "NumPeaks"
        private static readonly Regex REGEX_COMMENT = new Regex(@"^Comment: ");
        private static readonly Regex REGEX_MODS = new Regex(@" Mods=([^ ]+) ");
        private static readonly Regex REGEX_TF_RATIO = new Regex(@" Tfratio=([^ ]+) ");
        private static readonly Regex REGEX_SAMPLE = new Regex(@" Sample=(\d+)");
        private static readonly char[] MAJOR_SEP = new[] {'/'};
        private static readonly char[] MINOR_SEP = new[] {','};

        private bool CreateCache(ILoadMonitor loader, ProgressStatus status, int percent)
        {
            var sm = loader.StreamManager;
            long size = sm.GetLength(FilePath);
            long readChars = 0;

            using (TextReader reader = sm.CreateReader(FilePath))
            using (FileSaver fs = new FileSaver(CachePath, sm))
            using (Stream outStream = sm.CreateStream(fs.SafeName, FileMode.Create, true))
            {
                List<NistSpectrumInfo> listInfo = new List<NistSpectrumInfo>(10000);

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    // Update status trying to approximate position in the file
                    readChars += line.Length;
                    int percentComplete = (int) (readChars * percent / size);
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


                    // Read until name line
                    Match match = REGEX_NAME.Match(line);
                    if (!match.Success)
                        continue;

                    string sequence = match.Groups[1].Value;
                    int charge = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);

                    int numPeaks = 0;
                    float tfRatio = 1000;
                    int copies = 1;

                    // Process until the start of the peaks
                    while ((line = reader.ReadLine()) != null)
                    {
                        readChars += line.Length;

                        match = REGEX_NUM_PEAKS.Match(line);
                        if (match.Success)
                        {
                            numPeaks = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                            break;
                        }

                        match = REGEX_COMMENT.Match(line);
                        if (match.Success)
                        {
                            match = REGEX_MODS.Match(line);
                            if (match.Success)
                                sequence = Modify(sequence, match.Groups[1].Value);

                            match = REGEX_SAMPLE.Match(line);
                            if (match.Success)
                                copies = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);

                            match = REGEX_TF_RATIO.Match(line);
                            if (match.Success)
                                tfRatio = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                        }

                        if (line.StartsWith("_EOF_"))
                            throw new IOException("Unexpected end of file.");
                        else if (line.StartsWith("Name:"))
                            break;
                    }

                    if (numPeaks == 0)
                        throw new IOException(string.Format("No peaks found for peptide {0}.", sequence));

                    double totalIntensity = 0;

                    int mzBytes = sizeof(float)*numPeaks;
                    byte[] peaks = new byte[mzBytes*2];
                    for (int i = 0; i < numPeaks; i++)
                    {
                        line = reader.ReadLine();
                        if (line == null)
                            throw new IOException(string.Format("Unexpected end of file in peaks for {0}.", sequence));
                        readChars += line.Length;

                        // Parse out mass and intensity as quickly as possible, since
                        // this will be the most repeated parsing code.
                        int iTab1 = line.IndexOf('\t');
                        int iTab2 = (iTab1 == -1 ? -1 : line.IndexOf('\t', iTab1 + 1));
                        if (iTab1 == -1 || iTab2 == -1)
                            throw new IOException(string.Format("Invalid format at peak {0} for {1}.", i + 1, sequence));

                        string mzField = line.Substring(0, iTab1++);
                        string intensityField = line.Substring(iTab1, iTab2 - iTab1);

                        int offset = i*4;
                        Array.Copy(BitConverter.GetBytes(float.Parse(mzField, CultureInfo.InvariantCulture)), 0, peaks, offset, 4);
                        float intensity = float.Parse(intensityField, CultureInfo.InvariantCulture);
                        Array.Copy(BitConverter.GetBytes(intensity), 0, peaks, mzBytes + offset, 4);
                        totalIntensity += intensity;
                    }
                    // Peak list compression turns out to have a 4x impact on time to
                    // create the cache.  Using zero below turns it off, or 1 to turn
                    // it on, and take the performance hit for 40% less disk use, though
                    // because the text library files are so large, this difference
                    // represents only 5% of the cost of having the library on disk.
                    byte[] peaksCompressed = peaks.Compress(0);
                    int lenCompressed = peaksCompressed.Length;
                    long location = outStream.Position;
                    outStream.Write(peaksCompressed, 0, lenCompressed);
                    
                    LibKey key = new LibKey(sequence, charge);
                    listInfo.Add(new NistSpectrumInfo(key, tfRatio, Convert.ToSingle(totalIntensity),
                        (short) copies, (short) numPeaks, (short) lenCompressed, location));
                }

                listInfo.Sort(CompareSpectrumInfo);

                long locationHeaders = outStream.Position;
                foreach (NistSpectrumInfo info in listInfo)
                {
                    outStream.Write(BitConverter.GetBytes(info.Key.Charge), 0, sizeof(int));
                    outStream.Write(BitConverter.GetBytes(info.TFRatio), 0, sizeof(float));
                    outStream.Write(BitConverter.GetBytes(info.TotalIntensity), 0, sizeof(float));
                    outStream.Write(BitConverter.GetBytes(info.Copies), 0, sizeof(int));
                    outStream.Write(BitConverter.GetBytes(info.NumPeaks), 0, sizeof(int));
                    outStream.Write(BitConverter.GetBytes(info.CompressedSize), 0, sizeof(int));
                    outStream.Write(BitConverter.GetBytes(info.Location), 0, sizeof(long));
                    info.Key.WriteSequence(outStream);
                }

                outStream.Write(BitConverter.GetBytes(FORMAT_VERSION_CACHE), 0, sizeof(int));
                outStream.Write(BitConverter.GetBytes(listInfo.Count), 0, sizeof(int));
                outStream.Write(BitConverter.GetBytes(locationHeaders), 0, sizeof(long));

                sm.Finish(outStream);
                fs.Commit();
                sm.SetCache(FilePath, CachePath);
            }

            return true;
        }

        private static string Modify(string sequence, string mod)
        {
            // If no modifications, just return the input sequence
            bool clean = (sequence.IndexOfAny(new[] {'(', '['}) == -1);
            if (clean && Equals(mod, "0"))
                return sequence;

            // Parse the modification spec, and insert [+/-00.0] modifiers
            string[] mods = mod.Split(MAJOR_SEP);

            StringBuilder sb = new StringBuilder(sequence.Length);
            bool inMod = false;
            int i = 0, iMod = 1, iNextMod = -1;
            string massDiffDesc = null;
            foreach (char c in sequence)
            {
                while (iNextMod < i && iMod < mods.Length)
                    iNextMod = GetMod(mods[iMod++], out massDiffDesc);

                // At least for Oxidation the sequence already contains
                // inserted identifiers that look like M(O) for Methyonine
                // with oxidation.  So, these are removed.
                if (c == '(' || c == '[')
                    inMod = true;
                else if (inMod)
                {
                    if (c == ')' || c == ']')
                        inMod = false;
                }
                else
                {
                    sb.Append(c);
                    if (iNextMod == i)
                        sb.Append(massDiffDesc);
                    i++;
                }
            }
            return sb.ToString();
        }

        private static int GetMod(string mod, out string massDiff)
        {
            string[] parts = mod.Split(MINOR_SEP);
            if (parts.Length < 3)
            {
                massDiff = "";
                return -1;
            }
            int index = int.Parse(parts[0], CultureInfo.InvariantCulture);
            // If it is an unknown modification, insert a sequence modifier
            // that will cause this sequence never to match anything.  These
            // are rare, and can be viewed by placing a breakpoint on the
            // line where if is true.
            if (!MODIFICATION_MASSES.TryGetValue(parts[2], out massDiff))
                massDiff = "[?]";
            return index;
        }

        private static int GetInt32(byte[] bytes, int index)
        {
            int ibyte = index*4;
            return bytes[ibyte] | bytes[ibyte + 1] << 8 | bytes[ibyte + 2] << 16 | bytes[ibyte + 3] << 24;
        }

//        private static int ReadSize(Stream stream)
//        {
//            byte[] libSize = new byte[4];
//            ReadComplete(stream, libSize, libSize.Length);
//            return GetInt32(libSize, 0);            
//        }

        private static void ReadComplete(Stream stream, byte[] buffer, int size)
        {
            if (stream.Read(buffer, 0, size) != size)
                throw new InvalidDataException("Data truncation in library header. File may be corrupted.");            
        }

        private static int CompareSpectrumInfo(NistSpectrumInfo info1, NistSpectrumInfo info2)
        {
            return info1.Key.Compare(info2.Key);
        }

        public override bool Contains(LibKey key)
        {
            return FindEntry(key) != -1;
        }

        public override bool TryGetLibInfo(LibKey key, out SpectrumHeaderInfo libInfo)
        {
            int i = FindEntry(key);
            if (i != -1)
            {
                var entry = _libraryEntries[i];
                libInfo = CreateSpectrumHeaderInfo(Name, entry.TFRatio, entry.TotalIntensity , entry.Copies);
                return true;
            }
            libInfo = null;
            return false;
        }

        public override bool TryLoadSpectrum(LibKey key, out SpectrumPeaksInfo spectrum)
        {
            int i = FindEntry(key);
            if (i != -1)
            {
                spectrum = new SpectrumPeaksInfo(ReadSpectrum(_libraryEntries[i]));
                return true;
            }

            spectrum = null;
            return false;
        }

        public override int Count
        {
            get { return _libraryEntries == null ? 0 : _libraryEntries.Length; }
        }

        public override IEnumerable<LibKey> Keys
        {
            get
            {
                if (IsLoaded)
                    foreach (var entry in _libraryEntries)
                        yield return entry.Key;
            }
        }

        private SpectrumPeaksInfo.MI[] ReadSpectrum(NistSpectrumInfo info)
        {
            Stream fs = ReadStream.Stream;

            // Seek to stored location
            fs.Seek(info.Location, SeekOrigin.Begin);

            // Single read to get all the peaks
            byte[] peaksCompressed = new byte[info.CompressedSize];
            if (fs.Read(peaksCompressed, 0, peaksCompressed.Length) < peaksCompressed.Length)
                throw new IOException("Failure trying to read peaks");

            int mzBytes = sizeof(float)*info.NumPeaks;
            byte[] peaks = peaksCompressed.Uncompress(mzBytes*2);

            // Build the list
            var arrayMI = new SpectrumPeaksInfo.MI[info.NumPeaks];

            for (int i = 0; i < info.NumPeaks; i++)
            {
                int offset = i*4;
                arrayMI[i].Mz = BitConverter.ToSingle(peaks, offset);
                arrayMI[i].Intensity = BitConverter.ToSingle(peaks, mzBytes + offset);
            }

            return arrayMI;
        }

        private int FindEntry(LibKey key)
        {
            if (_libraryEntries == null)
                return -1;
            return FindEntry(key, 0, _libraryEntries.Length - 1);
        }

        private int FindEntry(LibKey key, int left, int right)
        {
            // Binary search for the right key
            if (left > right)
                return -1;
            int mid = (left + right)/2;
            int compare = key.Compare(_libraryEntries[mid].Key);
            if (compare < 0)
                return FindEntry(key, left, mid - 1);
            else if (compare > 0)
                return FindEntry(key, mid + 1, right);
            else
                return mid;
        }

        #region Implementation of IXmlSerializable
        
        /// <summary>
        /// For serialization
        /// </summary>
        protected NistLibraryBase()
        {
        }

        private enum ATTR
        {
            id,
            revision
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
                result = (result * 397) ^ Id.GetHashCode();
                result = (result * 397) ^ Revision.GetHashCode();
                result = (result * 397) ^ (FilePath != null ? FilePath.GetHashCode() : 0);
                return result;
            }
        }

        #endregion
    }

    internal struct NistSpectrumInfo
    {
        private readonly LibKey _key;
        private readonly float _tfRatio;
        private readonly float _totalIntensity;
        private readonly short _copies;
        private readonly short _numPeaks;
        private readonly short _compressedSize;
        private readonly long _location;

        public NistSpectrumInfo(LibKey key, float tfRatio, float totalIntensity,
            short copies, short numPeaks, short compressedSize, long location)
        {
            _key = key;
            _totalIntensity = totalIntensity;
            _tfRatio = tfRatio;
            _copies = copies;
            _numPeaks = numPeaks;
            _compressedSize = compressedSize;
            _location = location;
        }

        public LibKey Key { get { return _key; } }
// ReSharper disable InconsistentNaming
        public float TFRatio { get { return _tfRatio; } }
// ReSharper restore InconsistentNaming
        public float TotalIntensity { get { return _totalIntensity; } }
        public int Copies { get { return _copies; } }
        public int NumPeaks { get { return _numPeaks; } }
        public int CompressedSize { get { return _compressedSize; }}
        public long Location { get { return _location; } }
    }
}
