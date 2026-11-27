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
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Quasar.Core.Output;

namespace pwiz.Quasar.Test
{
    /// <summary>
    /// The pseudo-spectrum mzML (spec Stage 4) reads back as written: spectrum ids and times,
    /// isolation windows, precursor candidates (or none, for a tier 3 cluster), userParams, and
    /// the peak arrays sorted by m/z.
    /// </summary>
    [TestClass]
    public class MzmlWriterTest
    {
        private const string MZML_NS = @"http://psi.hupo.org/ms/mzml";

        [TestMethod]
        public void TestMzmlRoundTrip()
        {
            var tier3 = new PseudoSpectrum(@"run=r window=12 cluster=3", 21.25, 650.5, 1.5, 1.5,
                Array.Empty<PrecursorCandidate>(), new[] { 500.25, 175.119, 300.5 }, new[] { 10.0, 30.0, 20.0 },
                new[] { new KeyValuePair<string, double>(@"start_rt", 21.2) });
            var tier1 = new PseudoSpectrum(@"run=r window=12 cluster=4", 21.5, 650.5, 1.5, 1.5,
                new[] { new PrecursorCandidate(650.8123, 2, 1), new PrecursorCandidate(651.1, 3, 2) },
                new[] { 200.1 }, new[] { 5.5 }, Array.Empty<KeyValuePair<string, double>>());

            string path = Path.Combine(Path.GetTempPath(), @"quasar-mzml-" + Guid.NewGuid().ToString(@"N") + @".mzML");
            try
            {
                PseudoSpectrumMzmlWriter.Write(path, new[] { tier3, tier1 }, @"C:\data\run.raw", @"26.1.1.0",
                    @"Quasar -i run.raw");
                var doc = new XmlDocument();
                doc.Load(path);
                var ns = new XmlNamespaceManager(doc.NameTable);
                ns.AddNamespace(@"m", MZML_NS);

                Assert.AreEqual(@"2", doc.SelectSingleNode(@"//m:spectrumList/@count", ns)?.Value);
                var spectra = doc.SelectNodes(@"//m:spectrum", ns);
                Assert.IsNotNull(spectra);
                Assert.AreEqual(2, spectra.Count);

                // Tier 3: one precursor with the isolation window only, peaks sorted by m/z.
                var first = spectra[0];
                Assert.IsNotNull(first);
                Assert.AreEqual(tier3.NativeId, first.Attributes?[@"id"]?.Value);
                Assert.AreEqual(21.25, CvValue(first, ns, @"MS:1000016"));
                Assert.AreEqual(650.5, CvValue(first, ns, @"MS:1000827"));
                Assert.AreEqual(1.5, CvValue(first, ns, @"MS:1000828"));
                Assert.AreEqual(1, first.SelectNodes(@"m:precursorList/m:precursor", ns)?.Count);
                Assert.AreEqual(0, first.SelectNodes(@".//m:selectedIon", ns)?.Count);
                Assert.AreEqual(@"21.2", first.SelectSingleNode(@"m:userParam[@name='start_rt']/@value", ns)?.Value);
                CollectionAssert.AreEqual(new[] { 175.119, 300.5, 500.25 }, DecodeDoubles(first, ns, 0));
                CollectionAssert.AreEqual(new[] { 30f, 20f, 10f }, DecodeFloats(first, ns, 1));

                // Tier 1 and 2 candidates: one precursor element each, with m/z and charge.
                var second = spectra[1];
                Assert.IsNotNull(second);
                var selected = second.SelectNodes(@".//m:selectedIon", ns);
                Assert.IsNotNull(selected);
                Assert.AreEqual(2, selected.Count);
                Assert.AreEqual(650.8123, CvValue(selected[0], ns, @"MS:1000744"));
                Assert.AreEqual(2.0, CvValue(selected[0], ns, @"MS:1000041"));
                Assert.AreEqual(3.0, CvValue(selected[1], ns, @"MS:1000041"));

                // A source given as a bare file name (Quasar -i run.raw) is resolved, not rejected.
                PseudoSpectrumMzmlWriter.Write(path, new[] { tier3 }, @"run.raw", @"26.1.1.0", string.Empty);
                doc.Load(path);
                Assert.AreEqual(@"run.raw", doc.SelectSingleNode(@"//m:sourceFile/@name", ns)?.Value);
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static double CvValue(XmlNode node, XmlNamespaceManager ns, string accession)
        {
            var value = node.SelectSingleNode(@".//m:cvParam[@accession='" + accession + @"']/@value", ns)?.Value;
            Assert.IsNotNull(value, accession);
            return double.Parse(value, CultureInfo.InvariantCulture);
        }

        private static double[] DecodeDoubles(XmlNode spectrum, XmlNamespaceManager ns, int arrayIndex)
        {
            var bytes = Binary(spectrum, ns, arrayIndex);
            return Enumerable.Range(0, bytes.Length / 8).Select(i => BitConverter.ToDouble(bytes, i * 8)).ToArray();
        }

        private static float[] DecodeFloats(XmlNode spectrum, XmlNamespaceManager ns, int arrayIndex)
        {
            var bytes = Binary(spectrum, ns, arrayIndex);
            return Enumerable.Range(0, bytes.Length / 4).Select(i => BitConverter.ToSingle(bytes, i * 4)).ToArray();
        }

        private static byte[] Binary(XmlNode spectrum, XmlNamespaceManager ns, int arrayIndex)
        {
            var binaries = spectrum.SelectNodes(@".//m:binary", ns);
            Assert.IsNotNull(binaries);
            var binary = binaries[arrayIndex];
            Assert.IsNotNull(binary);
            return Convert.FromBase64String(binary.InnerText);
        }
    }
}
