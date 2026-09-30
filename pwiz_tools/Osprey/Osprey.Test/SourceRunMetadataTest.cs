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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// What a run's data file says about its acquisition, for the training export's footer
    /// (<see cref="SpectrumFileReader.TryReadSourceMetadata"/>): the instrument vendor and model,
    /// and histograms of the dissociation method and collision energy over the first MS2
    /// spectra, capped at <see cref="SourceRunMetadata.MAX_MS2_SPECTRA"/>. A file that is not
    /// there or cannot be read gives null, never an exception, since the footer is descriptive.
    /// </summary>
    [TestClass]
    public class SourceRunMetadataTest
    {
        [TestMethod]
        public void TestSourceRunMetadata()
        {
            string dir = Path.Combine(Path.GetTempPath(), @"osprey_source_" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            try
            {
                AssertReadsInstrumentAndActivation(dir);
                AssertSamplesOnlyTheFirstMs2Spectra(dir);
                AssertCountsMs2Analyzers(dir);
                AssertUnreadableSourceIsUnknown(dir);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        /// <summary>
        /// A Thermo file from a Q Exactive, one MS1 and four MS2 spectra: three HCD at two
        /// energies and one ETD with no energy. The MS1 is not counted.
        /// </summary>
        private static void AssertReadsInstrumentAndActivation(string dir)
        {
            string path = Path.Combine(dir, @"thermo.mzML");
            var spectra = new List<string>
            {
                Spectrum(0, 1, null, null),
                Spectrum(1, 2, HCD, 27.0),
                Spectrum(2, 2, HCD, 27.0),
                Spectrum(3, 2, HCD, 30.0),
                Spectrum(4, 2, ETD, null),
            };
            File.WriteAllText(path, Mzml(spectra, true));

            var metadata = SpectrumFileReader.TryReadSourceMetadata(path);
            Assert.IsNotNull(metadata);
            Assert.AreEqual(@"Thermo", metadata.InstrumentVendor);
            Assert.AreEqual(@"Q Exactive", metadata.InstrumentModel);
            Assert.AreEqual(4, metadata.NMs2Sampled);
            AssertHistogram(metadata.DissociationMethods, (@"ETD", 1), (@"HCD", 3));
            AssertHistogram(metadata.CollisionEnergies, (@"27", 2), (@"30", 1), (SourceRunMetadata.NONE_KEY, 1));
            AssertHistogram(metadata.MassAnalyzers, (SourceRunMetadata.NONE_KEY, 4));
        }

        /// <summary>
        /// A Tribrid file whose MS2 spectra are read out in both analyzers, HCD in the Orbitrap and
        /// resonance CID in the ion trap: each spectrum's analyzer is the one its scan's instrument
        /// configuration names, and the activation is pwiz's short name (Thermo's HCD and CID).
        /// </summary>
        private static void AssertCountsMs2Analyzers(string dir)
        {
            string path = Path.Combine(dir, @"tribrid.mzML");
            var spectra = new List<string>
            {
                Spectrum(0, 1, null, null),
                Spectrum(1, 2, HCD, 30.0),
                Spectrum(2, 2, HCD, 30.0),
                Spectrum(3, 2, CID, 35.0, @"IC2"),
            };
            File.WriteAllText(path, Mzml(spectra, true, ORBITRAP, ION_TRAP));

            var metadata = SpectrumFileReader.TryReadSourceMetadata(path);
            Assert.IsNotNull(metadata);
            Assert.AreEqual(3, metadata.NMs2Sampled);
            AssertHistogram(metadata.MassAnalyzers, (@"orbitrap", 2), (@"radial ejection linear ion trap", 1));
            AssertHistogram(metadata.DissociationMethods, (@"CID", 1), (@"HCD", 2));
        }

        /// <summary>
        /// A file with more MS2 spectra than the sample reads counts only the first
        /// <see cref="SourceRunMetadata.MAX_MS2_SPECTRA"/>; a file that declares no instrument and
        /// no activation leaves the instrument unknown and counts every spectrum as none.
        /// </summary>
        private static void AssertSamplesOnlyTheFirstMs2Spectra(string dir)
        {
            string path = Path.Combine(dir, @"plain.mzML");
            int max = SourceRunMetadata.MAX_MS2_SPECTRA;
            var spectra = Enumerable.Range(0, max + 5).Select(i => Spectrum(i, 2, null, null)).ToList();
            File.WriteAllText(path, Mzml(spectra, false));

            var metadata = SpectrumFileReader.TryReadSourceMetadata(path);
            Assert.IsNotNull(metadata);
            Assert.IsTrue(string.IsNullOrEmpty(metadata.InstrumentVendor), metadata.InstrumentVendor);
            Assert.IsTrue(string.IsNullOrEmpty(metadata.InstrumentModel), metadata.InstrumentModel);
            Assert.AreEqual(max, metadata.NMs2Sampled);
            AssertHistogram(metadata.DissociationMethods, (SourceRunMetadata.NONE_KEY, max));
            AssertHistogram(metadata.CollisionEnergies, (SourceRunMetadata.NONE_KEY, max));
        }

        private static void AssertUnreadableSourceIsUnknown(string dir)
        {
            Assert.IsNull(SpectrumFileReader.TryReadSourceMetadata(null));
            Assert.IsNull(SpectrumFileReader.TryReadSourceMetadata(string.Empty));
            Assert.IsNull(SpectrumFileReader.TryReadSourceMetadata(Path.Combine(dir, @"missing.mzML")));
            string garbage = Path.Combine(dir, @"garbage.mzML");
            File.WriteAllText(garbage, @"not a spectrum file");
            Assert.IsNull(SpectrumFileReader.TryReadSourceMetadata(garbage));
        }

        private static void AssertHistogram(SortedDictionary<string, int> actual, params (string Key, int Count)[] expected)
        {
            string text = string.Join(@", ", actual.Select(p => p.Key + @"=" + p.Value));
            Assert.AreEqual(expected.Length, actual.Count, text);
            foreach (var (key, count) in expected)
            {
                Assert.IsTrue(actual.TryGetValue(key, out int n), key + @" missing from " + text);
                Assert.AreEqual(count, n, text);
            }
        }

        // ---- fixtures ------------------------------------------------------------------------

        private const string HCD = @"<cvParam cvRef=""MS"" accession=""MS:1000422"" name=""beam-type collision-induced dissociation"" />";
        private const string ETD = @"<cvParam cvRef=""MS"" accession=""MS:1000598"" name=""electron transfer dissociation"" />";
        private const string CID = @"<cvParam cvRef=""MS"" accession=""MS:1000133"" name=""collision-induced dissociation"" />";
        private const string ORBITRAP = @"<cvParam cvRef=""MS"" accession=""MS:1000484"" name=""orbitrap"" />";
        private const string ION_TRAP = @"<cvParam cvRef=""MS"" accession=""MS:1000083"" name=""radial ejection linear ion trap"" />";

        /// <summary>
        /// A minimal mzML document. <paramref name="thermo"/> declares a Thermo RAW source file
        /// and a Q Exactive instrument configuration, which is where ProteoWizard reads the
        /// vendor and model from; otherwise the file declares neither.
        /// </summary>
        /// <param name="spectra">The spectrum elements.</param>
        /// <param name="thermo">A Q Exactive Thermo file, else one that declares no instrument.</param>
        /// <param name="analyzers">One instrument configuration (IC1, IC2, ...) per analyzer term; none gives one bare IC1.</param>
        private static string Mzml(IReadOnlyList<string> spectra, bool thermo, params string[] analyzers)
        {
            var sb = new StringBuilder();
            sb.Append(@"<?xml version=""1.0"" encoding=""utf-8""?>
<mzML xmlns=""http://psi.hupo.org/ms/mzml"" version=""1.1.0"">
  <cvList count=""2"">
    <cv id=""MS"" fullName=""Proteomics Standards Initiative Mass Spectrometry Ontology"" />
    <cv id=""UO"" fullName=""Unit Ontology"" />
  </cvList>
  <fileDescription>
    <fileContent>
      <cvParam cvRef=""MS"" accession=""MS:1000580"" name=""MSn spectrum"" />
    </fileContent>");
            if (thermo)
            {
                sb.Append(@"
    <sourceFileList count=""1"">
      <sourceFile id=""RAW1"" name=""thermo.raw"" location=""file:///data"">
        <cvParam cvRef=""MS"" accession=""MS:1000563"" name=""Thermo RAW format"" />
      </sourceFile>
    </sourceFileList>");
            }
            sb.Append(@"
  </fileDescription>
  <instrumentConfigurationList count=""");
            int configurations = Math.Max(1, analyzers.Length);
            sb.Append(configurations.ToString(CultureInfo.InvariantCulture));
            sb.Append(@""">");
            for (int i = 0; i < configurations; i++)
            {
                sb.Append(string.Format(CultureInfo.InvariantCulture, @"
    <instrumentConfiguration id=""IC{0}"">", i + 1));
                if (thermo)
                {
                    sb.Append(@"
      <cvParam cvRef=""MS"" accession=""MS:1001911"" name=""Q Exactive"" />");
                }
                if (i < analyzers.Length)
                {
                    sb.Append(@"
      <componentList count=""1"">
        <analyzer order=""1"">" + analyzers[i] + @"</analyzer>
      </componentList>");
                }
                sb.Append(@"
    </instrumentConfiguration>");
            }
            sb.Append(@"
  </instrumentConfigurationList>
  <run id=""run"" defaultInstrumentConfigurationRef=""IC1"">
    <spectrumList count=""");
            sb.Append(spectra.Count.ToString(CultureInfo.InvariantCulture));
            sb.Append(@""" defaultDataProcessingRef=""dp"">");
            foreach (string spectrum in spectra)
                sb.Append(spectrum);
            sb.Append(@"
    </spectrumList>
  </run>
</mzML>");
            return sb.ToString();
        }

        /// <summary>One centroided spectrum of two peaks; an MS2 carries a precursor.</summary>
        private static string Spectrum(int index, int msLevel, string dissociation, double? collisionEnergy, string configuration = @"IC1")
        {
            string precursor = string.Empty;
            if (msLevel == 2)
            {
                string activation = (dissociation ?? string.Empty) +
                                    (collisionEnergy.HasValue
                                        ? string.Format(CultureInfo.InvariantCulture,
                                            @"<cvParam cvRef=""MS"" accession=""MS:1000045"" name=""collision energy"" value=""{0}"" />",
                                            collisionEnergy.Value)
                                        : string.Empty);
                precursor = @"
        <precursorList count=""1"">
          <precursor>
            <isolationWindow>
              <cvParam cvRef=""MS"" accession=""MS:1000827"" value=""500.0"" />
              <cvParam cvRef=""MS"" accession=""MS:1000828"" value=""2.0"" />
              <cvParam cvRef=""MS"" accession=""MS:1000829"" value=""2.0"" />
            </isolationWindow>
            <selectedIonList count=""1"">
              <selectedIon>
                <cvParam cvRef=""MS"" accession=""MS:1000744"" value=""500.0"" />
              </selectedIon>
            </selectedIonList>
            <activation>" + activation + @"</activation>
          </precursor>
        </precursorList>";
            }
            return string.Format(CultureInfo.InvariantCulture, @"
      <spectrum index=""{0}"" defaultArrayLength=""2"" id=""scan={1}"">
        <cvParam cvRef=""MS"" accession=""MS:1000511"" value=""{2}"" />
        <cvParam cvRef=""MS"" accession=""MS:1000127"" name=""centroid spectrum"" />
        <scanList count=""1"">
          <scan instrumentConfigurationRef=""{7}"">
            <cvParam cvRef=""MS"" accession=""MS:1000016"" value=""{3}"" unitCvRef=""UO"" unitAccession=""UO:0000031"" unitName=""minute"" />
          </scan>
        </scanList>{4}
        <binaryDataArrayList count=""2"">
          <binaryDataArray>
            <cvParam cvRef=""MS"" accession=""MS:1000514"" />
            <cvParam cvRef=""MS"" accession=""MS:1000523"" />
            <cvParam cvRef=""MS"" accession=""MS:1000576"" />
            <binary>{5}</binary>
          </binaryDataArray>
          <binaryDataArray>
            <cvParam cvRef=""MS"" accession=""MS:1000515"" />
            <cvParam cvRef=""MS"" accession=""MS:1000521"" />
            <cvParam cvRef=""MS"" accession=""MS:1000576"" />
            <binary>{6}</binary>
          </binaryDataArray>
        </binaryDataArrayList>
      </spectrum>",
                index, index + 1, msLevel, 1.0 + 0.01 * index, precursor,
                Base64(new[] { 200.0, 300.0 }), Base64(new[] { 100f, 200f }), configuration);
        }

        private static string Base64(double[] values)
        {
            var bytes = new byte[values.Length * sizeof(double)];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            return Convert.ToBase64String(bytes);
        }

        private static string Base64(float[] values)
        {
            var bytes = new byte[values.Length * sizeof(float)];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            return Convert.ToBase64String(bytes);
        }
    }
}
