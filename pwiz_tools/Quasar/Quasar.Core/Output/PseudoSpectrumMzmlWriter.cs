/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Text;
using System.Xml;

namespace pwiz.Quasar.Core.Output
{
    /// <summary>
    /// Writes pseudo-spectra as an mzML 1.1 file (spec Stage 4) that Sage, SeeMS and Glimpse can
    /// read: centroided MS2 spectra with m/z as 64-bit and intensities as 32-bit floats,
    /// uncompressed, scan start time in minutes, one precursor element per candidate (each with
    /// the isolation window), and one precursor with only the isolation window for a tier 3
    /// cluster. The spectra are few and small, so the file is written in one pass from a list.
    /// </summary>
    public static class PseudoSpectrumMzmlWriter
    {
        private const string MS_CV = @"MS";
        private const string UO_CV = @"UO";
        private const string MZ_UNIT = @"MS:1000040";

        /// <summary>
        /// Writes <paramref name="spectra"/> to <paramref name="path"/>, recording
        /// <paramref name="sourcePath"/> as the source file and the Quasar version and command line
        /// as the processing method.
        /// </summary>
        public static void Write(string path, IReadOnlyList<PseudoSpectrum> spectra, string sourcePath,
            string softwareVersion, string commandLine)
        {
            var settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = new UTF8Encoding(false)
            };
            string runId = Path.GetFileNameWithoutExtension(path);
            using (var writer = XmlWriter.Create(path, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement(@"mzML", @"http://psi.hupo.org/ms/mzml");
                writer.WriteAttributeString(@"id", runId);
                writer.WriteAttributeString(@"version", @"1.1.0");
                WriteHeader(writer, sourcePath, softwareVersion, commandLine);

                writer.WriteStartElement(@"run");
                writer.WriteAttributeString(@"id", runId);
                writer.WriteAttributeString(@"defaultInstrumentConfigurationRef", @"IC1");
                writer.WriteStartElement(@"spectrumList");
                writer.WriteAttributeString(@"count", Format(spectra.Count));
                writer.WriteAttributeString(@"defaultDataProcessingRef", @"Quasar_processing");
                for (int i = 0; i < spectra.Count; i++)
                    WriteSpectrum(writer, i, spectra[i]);
                writer.WriteEndElement(); // spectrumList
                writer.WriteEndElement(); // run
                writer.WriteEndElement(); // mzML
                writer.WriteEndDocument();
            }
        }

        private static void WriteHeader(XmlWriter writer, string sourcePath, string softwareVersion, string commandLine)
        {
            writer.WriteStartElement(@"cvList");
            writer.WriteAttributeString(@"count", @"2");
            WriteCv(writer, MS_CV, @"Proteomics Standards Initiative Mass Spectrometry Ontology",
                @"https://raw.githubusercontent.com/HUPO-PSI/psi-ms-CV/master/psi-ms.obo");
            WriteCv(writer, UO_CV, @"Unit Ontology",
                @"https://raw.githubusercontent.com/bio-ontology-research-group/unit-ontology/master/unit.obo");
            writer.WriteEndElement();

            writer.WriteStartElement(@"fileDescription");
            writer.WriteStartElement(@"fileContent");
            WriteCvParam(writer, @"MS:1000580", @"MSn spectrum");
            WriteCvParam(writer, @"MS:1000127", @"centroid spectrum");
            writer.WriteEndElement();
            writer.WriteStartElement(@"sourceFileList");
            writer.WriteAttributeString(@"count", @"1");
            writer.WriteStartElement(@"sourceFile");
            writer.WriteAttributeString(@"id", @"SF1");
            writer.WriteAttributeString(@"name", Path.GetFileName(sourcePath));
            // Resolve first: a bare file name has an empty, not null, directory.
            string directory = Path.GetDirectoryName(Path.GetFullPath(sourcePath)) ?? Path.GetFullPath(@".");
            writer.WriteAttributeString(@"location", new Uri(directory + Path.DirectorySeparatorChar).AbsoluteUri);
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement(); // fileDescription

            writer.WriteStartElement(@"softwareList");
            writer.WriteAttributeString(@"count", @"1");
            writer.WriteStartElement(@"software");
            writer.WriteAttributeString(@"id", @"Quasar");
            writer.WriteAttributeString(@"version", softwareVersion);
            WriteCvParam(writer, @"MS:1000799", @"custom unreleased software tool", @"Quasar");
            writer.WriteEndElement();
            writer.WriteEndElement();

            writer.WriteStartElement(@"instrumentConfigurationList");
            writer.WriteAttributeString(@"count", @"1");
            writer.WriteStartElement(@"instrumentConfiguration");
            writer.WriteAttributeString(@"id", @"IC1");
            WriteCvParam(writer, @"MS:1000031", @"instrument model");
            writer.WriteEndElement();
            writer.WriteEndElement();

            writer.WriteStartElement(@"dataProcessingList");
            writer.WriteAttributeString(@"count", @"1");
            writer.WriteStartElement(@"dataProcessing");
            writer.WriteAttributeString(@"id", @"Quasar_processing");
            writer.WriteStartElement(@"processingMethod");
            writer.WriteAttributeString(@"order", @"0");
            writer.WriteAttributeString(@"softwareRef", @"Quasar");
            WriteCvParam(writer, @"MS:1000033", @"deisotoping");
            WriteCvParam(writer, @"MS:1000034", @"charge deconvolution");
            WriteUserParam(writer, @"command line", commandLine, null);
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteSpectrum(XmlWriter writer, int index, PseudoSpectrum spectrum)
        {
            var order = Enumerable.Range(0, spectrum.Mzs.Length).OrderBy(i => spectrum.Mzs[i]).ToArray();
            var mzs = order.Select(i => spectrum.Mzs[i]).ToArray();
            var intensities = order.Select(i => (float)spectrum.Intensities[i]).ToArray();

            writer.WriteStartElement(@"spectrum");
            writer.WriteAttributeString(@"index", Format(index));
            writer.WriteAttributeString(@"id", spectrum.NativeId);
            writer.WriteAttributeString(@"defaultArrayLength", Format(mzs.Length));
            WriteCvParam(writer, @"MS:1000511", @"ms level", @"2");
            WriteCvParam(writer, @"MS:1000580", @"MSn spectrum");
            WriteCvParam(writer, @"MS:1000127", @"centroid spectrum");
            WriteCvParam(writer, @"MS:1000285", @"total ion current", Format(intensities.Sum(v => (double)v)));
            foreach (var userParam in spectrum.UserParams)
                WriteUserParam(writer, userParam.Key, Format(userParam.Value), @"xsd:double");

            writer.WriteStartElement(@"scanList");
            writer.WriteAttributeString(@"count", @"1");
            WriteCvParam(writer, @"MS:1000795", @"no combination");
            writer.WriteStartElement(@"scan");
            writer.WriteStartElement(@"cvParam");
            writer.WriteAttributeString(@"cvRef", MS_CV);
            writer.WriteAttributeString(@"accession", @"MS:1000016");
            writer.WriteAttributeString(@"name", @"scan start time");
            writer.WriteAttributeString(@"value", Format(spectrum.RetentionTime));
            writer.WriteAttributeString(@"unitCvRef", UO_CV);
            writer.WriteAttributeString(@"unitAccession", @"UO:0000031");
            writer.WriteAttributeString(@"unitName", @"minute");
            writer.WriteEndElement();
            writer.WriteEndElement(); // scan
            writer.WriteEndElement(); // scanList

            var precursors = spectrum.Precursors.Count > 0
                ? spectrum.Precursors.ToList()
                : new List<PrecursorCandidate> { null };
            writer.WriteStartElement(@"precursorList");
            writer.WriteAttributeString(@"count", Format(precursors.Count));
            foreach (var candidate in precursors)
                WritePrecursor(writer, spectrum, candidate);
            writer.WriteEndElement();

            writer.WriteStartElement(@"binaryDataArrayList");
            writer.WriteAttributeString(@"count", @"2");
            WriteBinary(writer, ToBytes(mzs), @"MS:1000523", @"64-bit float", @"MS:1000514", @"m/z array",
                MZ_UNIT, @"m/z");
            WriteBinary(writer, ToBytes(intensities), @"MS:1000521", @"32-bit float", @"MS:1000515",
                @"intensity array", @"MS:1000131", @"number of detector counts");
            writer.WriteEndElement();

            writer.WriteEndElement(); // spectrum
        }

        /// <summary>One precursor element: the isolation window, and the candidate when there is one.</summary>
        private static void WritePrecursor(XmlWriter writer, PseudoSpectrum spectrum, PrecursorCandidate candidate)
        {
            writer.WriteStartElement(@"precursor");
            writer.WriteStartElement(@"isolationWindow");
            WriteMzParam(writer, @"MS:1000827", @"isolation window target m/z", spectrum.IsolationTarget);
            WriteMzParam(writer, @"MS:1000828", @"isolation window lower offset", spectrum.IsolationLowerOffset);
            WriteMzParam(writer, @"MS:1000829", @"isolation window upper offset", spectrum.IsolationUpperOffset);
            writer.WriteEndElement();
            if (candidate != null)
            {
                writer.WriteStartElement(@"selectedIonList");
                writer.WriteAttributeString(@"count", @"1");
                writer.WriteStartElement(@"selectedIon");
                WriteMzParam(writer, @"MS:1000744", @"selected ion m/z", candidate.Mz);
                if (candidate.Charge > 0)
                    WriteCvParam(writer, @"MS:1000041", @"charge state", Format(candidate.Charge));
                WriteUserParam(writer, @"quasar precursor tier", Format(candidate.Tier), @"xsd:int");
                writer.WriteEndElement();
                writer.WriteEndElement();
            }

            writer.WriteStartElement(@"activation");
            WriteCvParam(writer, @"MS:1000422", @"beam-type collision-induced dissociation");
            writer.WriteEndElement();
            writer.WriteEndElement(); // precursor
        }

        private static void WriteBinary(XmlWriter writer, byte[] bytes, string typeAccession, string typeName,
            string arrayAccession, string arrayName, string unitAccession, string unitName)
        {
            string encoded = Convert.ToBase64String(bytes);
            writer.WriteStartElement(@"binaryDataArray");
            writer.WriteAttributeString(@"encodedLength", Format(encoded.Length));
            WriteCvParam(writer, typeAccession, typeName);
            WriteCvParam(writer, @"MS:1000576", @"no compression");
            writer.WriteStartElement(@"cvParam");
            writer.WriteAttributeString(@"cvRef", MS_CV);
            writer.WriteAttributeString(@"accession", arrayAccession);
            writer.WriteAttributeString(@"name", arrayName);
            writer.WriteAttributeString(@"value", string.Empty);
            writer.WriteAttributeString(@"unitCvRef", MS_CV);
            writer.WriteAttributeString(@"unitAccession", unitAccession);
            writer.WriteAttributeString(@"unitName", unitName);
            writer.WriteEndElement();
            writer.WriteElementString(@"binary", encoded);
            writer.WriteEndElement();
        }

        private static void WriteCv(XmlWriter writer, string id, string fullName, string uri)
        {
            writer.WriteStartElement(@"cv");
            writer.WriteAttributeString(@"id", id);
            writer.WriteAttributeString(@"fullName", fullName);
            writer.WriteAttributeString(@"URI", uri);
            writer.WriteEndElement();
        }

        private static void WriteCvParam(XmlWriter writer, string accession, string name, string value = null)
        {
            writer.WriteStartElement(@"cvParam");
            writer.WriteAttributeString(@"cvRef", MS_CV);
            writer.WriteAttributeString(@"accession", accession);
            writer.WriteAttributeString(@"name", name);
            writer.WriteAttributeString(@"value", value ?? string.Empty);
            writer.WriteEndElement();
        }

        private static void WriteMzParam(XmlWriter writer, string accession, string name, double value)
        {
            writer.WriteStartElement(@"cvParam");
            writer.WriteAttributeString(@"cvRef", MS_CV);
            writer.WriteAttributeString(@"accession", accession);
            writer.WriteAttributeString(@"name", name);
            writer.WriteAttributeString(@"value", Format(value));
            writer.WriteAttributeString(@"unitCvRef", MS_CV);
            writer.WriteAttributeString(@"unitAccession", MZ_UNIT);
            writer.WriteAttributeString(@"unitName", @"m/z");
            writer.WriteEndElement();
        }

        private static void WriteUserParam(XmlWriter writer, string name, string value, string type)
        {
            writer.WriteStartElement(@"userParam");
            writer.WriteAttributeString(@"name", name);
            writer.WriteAttributeString(@"value", value);
            if (type != null)
                writer.WriteAttributeString(@"type", type);
            writer.WriteEndElement();
        }

        private static byte[] ToBytes(double[] values)
        {
            var bytes = new byte[values.Length * sizeof(double)];
            for (int i = 0; i < values.Length; i++)
                BitConverter.TryWriteBytes(new Span<byte>(bytes, i * sizeof(double), sizeof(double)), values[i]);
            return bytes;
        }

        private static byte[] ToBytes(float[] values)
        {
            var bytes = new byte[values.Length * sizeof(float)];
            for (int i = 0; i < values.Length; i++)
                BitConverter.TryWriteBytes(new Span<byte>(bytes, i * sizeof(float), sizeof(float)), values[i]);
            return bytes;
        }

        private static string Format(double value)
        {
            return value.ToString(@"R", CultureInfo.InvariantCulture);
        }

        private static string Format(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
