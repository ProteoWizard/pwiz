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
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Proteome;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// Checks digestion, FASTA reading and header parsing against Carafe. Expected digests were
    /// printed by Carafe's own DBGear.digest_protein (compomics-utilities 5.0.39P2) under Java 23.
    /// </summary>
    [TestClass]
    public class ProteinDigestTest
    {
        /// <summary>Human serum albumin precursor, 609 residues.</summary>
        private const string ALBUMIN =
            @"MKWVTFISLLFLFSSAYSRGVFRRDAHKSEVAHRFKDLGEENFKALVLIAFAQYLQQCPFEDHVKLVNEVTEFAKTCVADESAENCDKSLHTLFGDK" +
            @"LCTVATLRETYGEMADCCAKQEPERNECFLQHKDDNPNLPRLVRPEVDVMCTAFHDNEETFLKKYLYEIARRHPYFYAPELLFFAKRYKAAFTECCQ" +
            @"AADKAACLLPKLDELRDEGKASSAKQRLKCASLQKFGERAFKAWAVARLSQRFPKAEFAEVSKLVTDLTKVHTECCHGDLLECADDRADLAKYICEN" +
            @"QDSISSKLKECCEKPLLEKSHCIAEVENDEMPADLPSLAADFVESKDVCKNYAEAKDVFLGMFLYEYARRHPDYSVVLLLRLAKTYETTLEKCCAAA" +
            @"DPHECYAKVFDEFKPLVEEPQNLIKQNCELFEQLGEYKFQNALLVRYTKKVPQVSTPTLVEVSRNLGKVGSKCCKHPEAKRMPCAEDYLSVVLNQLC" +
            @"VLHEKTPVSDRVTKCCTESLVNRRPCFSALEVDETYVPKEFNAETFTFHADICTLSEKERQIKKQTALVELVKHKPKATKEQLKAVMDDFAAFVEKC" +
            @"CKADDKETCFAEEGKKLVAASQAALGL";

        /// <summary>Every ambiguity code, P after K and R, and a leading M.</summary>
        private const string AMBIGUOUS = @"MXKBZPRKPJUOAKRPPK";

        [TestMethod]
        public void TestEnzymeDigests()
        {
            // Trypsin: X-K cleaves (X may be K or R), K-B cleaves, K-P and R-P do not.
            AssertDigest(1, 1, 7, 35, false, 110, @"9df6a91c7624fce0b0cd0effcd07a218da34af17ab6863b54af91209bde72950",
                @"BZPRKPJUOAK", @"KPJUOAK", @"KPJUOAKRPPK");
            AssertDigest(2, 1, 7, 35, false, 113, @"f3dfd13f26bab34e5cdeb3d9be4295c32b75a2ec8e50f0b2488ce82e9233b89a",
                @"KPJUOAK", @"PJUOAKR");
            // Clipping adds XKBZPR, the initiator-methionine clip of MXKBZPR.
            AssertDigest(2, 2, 6, 30, true, 192, @"cce6c72abafcd8e128d397e7e4fb4109373df51e4d3208a66e3c823d74ee7841",
                @"BZPRKPJUOAK", @"KBZPRK", @"KPJUOAK", @"KPJUOAKR", @"MXKBZPR", @"PJUOAK", @"PJUOAKR", @"PJUOAKRPPK", @"XKBZPR");
            AssertDigest(3, 1, 7, 35, false, 26, @"e31981da3473bff3e877685a813779741e0b8a176e591774b864492132b0fdaf",
                @"KBZPRKPJUOAKRPPK", @"KPJUOAKRPPK", @"MXKBZPR");
            AssertDigest(5, 1, 7, 35, false, 29, @"1511433c7fd112314c52c2762e7706ef8f967329f42e9ad7e5eafa880024ba84",
                @"RKPJUOAK", @"RKPJUOAKRPPK", @"XKBZPRKPJUOAK");
            // NoCut never cleaves and never clips.
            AssertDigest(18, 1, 7, 35, true, 0, @"e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                @"MXKBZPRKPJUOAKRPPK");
            AssertDigest(1, 0, 1, 50, false, 74, @"fb8bb3780fef3ba141dbe27abe059761c53dbcf074a5c73fcaf2ebb8877510f5",
                @"BZPR", @"K", @"KPJUOAK", @"MX", @"RPPK");
            // Non-specific: every residue but J, O and Z cleaves.
            AssertDigest(0, 100, 16, 18, false, 1779, @"ca69d357b389e45b8c11dfa651525876e479da6b31c0e0d9ceabcc60a13566bf",
                @"KBZPRKPJUOAKRPPK", @"MXKBZPRKPJUOAKRP", @"MXKBZPRKPJUOAKRPP", @"MXKBZPRKPJUOAKRPPK",
                @"XKBZPRKPJUOAKRPP", @"XKBZPRKPJUOAKRPPK");
            AssertDigest(17, 2, 4, 12, true, 105, @"ceff987d7fe3a3172ee68047c18e6508b19fe153e43ffb14ff22ab1384bf0851",
                @"KBZP", @"KBZPR", @"KBZPRKPJUOA", @"KPJUOA", @"KPJUOAK", @"KPJUOAKRPP", @"KRPP", @"KRPPK", @"MXKBZP",
                @"RKPJUOA", @"RKPJUOAK", @"RPPK", @"XKBZP", @"XKBZPR");
        }

        [TestMethod]
        public void TestCleavageRules()
        {
            var trypsin = EnzymeTable.GetByIndex(1);
            Assert.IsTrue(trypsin.IsCleavageSite('K', 'A'));
            Assert.IsFalse(trypsin.IsCleavageSite('K', 'P'));
            Assert.IsFalse(trypsin.IsCleavageSite('K', 'X'));
            Assert.IsTrue(trypsin.IsCleavageSite('X', 'A'));
            Assert.IsTrue(trypsin.IsCleavageSite('r', 'a'));
            Assert.IsTrue(EnzymeTable.GetByIndex(2).IsCleavageSite('K', 'X'));
            Assert.IsFalse(EnzymeTable.GetByIndex(0).IsCleavageSite('O', 'A'));
            Assert.IsTrue(EnzymeTable.GetByIndex(0).IsCleavageSite('J', 'A'));
            Assert.ThrowsException<ArgumentException>(() => trypsin.IsCleavageSite('K', '*'));
            Assert.ThrowsException<ArgumentException>(() => trypsin.Digest(@"PEP1TIDEK", 1, 1, 50));
            // A one-residue sequence never tests a site, so even a non-letter survives.
            Assert.AreEqual(@"*", trypsin.Digest(@"*", 1, 1, 50).Single());

            Assert.AreEqual(19, EnzymeTable.All.Count);
            Assert.AreEqual(@"Trypsin (no P rule)", EnzymeTable.GetByIndex(2).Name);
            Assert.AreEqual(18, EnzymeTable.GetIndexByName(@"nocut"));
            Assert.IsTrue(EnzymeTable.IsNoCut(EnzymeTable.GetByIndex(18)));
            Assert.IsTrue(EnzymeTable.IsNonSpecific(EnzymeTable.GetByIndex(0)));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => EnzymeTable.GetByIndex(19));
            Assert.ThrowsException<ArgumentException>(() => EnzymeTable.GetIndexByName(@"Trypsin/P"));

            // Carafe upper-cases and strips one terminal asterisk before digesting; an empty
            // result cannot be digested.
            var digester = new Digester(new DigestSettings { EnzymeIndex = 2, MaxMissedCleavages = 0, MinLength = 1 });
            CollectionAssert.AreEquivalent(new[] { @"PEPK", @"TIDER" }, digester.Digest(@"*pepktider*").ToArray());
            Assert.ThrowsException<IndexOutOfRangeException>(() => digester.Digest(@"**"));
            CollectionAssert.AreEquivalent(new[] { @"PEPK" }, digester.ProteinNTermPeptides.ToArray());
        }

        [TestMethod]
        public void TestFastaReader()
        {
            // Whitespace anywhere in a sequence is dropped, blank lines included.
            var records = Read(">sp|A|B desc \r\nPEP TI\tDEK\r\n\r\n  >second\nAC\n\nDK\n");
            Assert.AreEqual(2, records.Length);
            Assert.AreEqual(@"sp|A|B desc", records[0].Header);
            Assert.AreEqual(@"PEPTIDEK", records[0].Sequence);
            Assert.AreEqual(@"second", records[1].Header);
            Assert.AreEqual(@"ACDK", records[1].Sequence);

            // A '>' ends the sequence even mid-line, so the rest of the line is the next header.
            records = Read(">one\nPEPK>two x\nACDK\n");
            CollectionAssert.AreEqual(new[] { @"one", @"two x" }, records.Select(r => r.Header).ToArray());
            CollectionAssert.AreEqual(new[] { @"PEPK", @"ACDK" }, records.Select(r => r.Sequence).ToArray());

            // An entry without a sequence ends the file, with a warning.
            string warning = null;
            records = FastaReader.Read(new StringReader(">one\nPEPK\n>empty\n>three\nACDK\n"), w => warning = w).ToArray();
            Assert.AreEqual(1, records.Length);
            Assert.AreEqual(@"invalid fasta element [empty]", warning);

            // Anything before the first header, including a byte-order mark, is a format error.
            Assert.ThrowsException<InvalidDataException>(() => Read("\n>one\nPEPK\n"));
            Assert.ThrowsException<InvalidDataException>(() => Read("\uFEFF>one\nPEPK\n"));
            Assert.AreEqual(0, Read(string.Empty).Length);
        }

        [TestMethod]
        public void TestProteinRecords()
        {
            AssertRecord(@"sp|P12345|ALBU_HUMAN Serum albumin OS=Homo sapiens", @"sp", @"P12345", @"ALBU_HUMAN");
            AssertRecord(@"tr|Q1|Q1_MOUSE|extra fields", @"tr", @"Q1", @"Q1_MOUSE");
            AssertRecord(@"db2|ACC5 two fields", @"db2", @"ACC5", @"ACC5");
            AssertRecord(@"sp|ACC7| trailing empty field", @"sp", @"ACC7", @"ACC7");
            AssertRecord(@"|ACC6|ENTRY6", string.Empty, @"ACC6", @"ENTRY6");
            AssertRecord(@"plainident a header without bars", @"sp", @"plainident", @"plainident");
            AssertRecord(@">>nested", @"sp", @">nested", @">nested");
            AssertRecord(@"||", @"sp", @"||", @"||");

            var record = ProteinRecord.Parse(@"sp|A|B", @"*acdK**");
            Assert.AreEqual(@"ACDK*", record.Sequence);
            Assert.IsNull(ProteinRecord.Parse(@"sp|A|B", @"**"));
            Assert.IsNull(ProteinRecord.Parse(@"sp|A|B", @"*"));
        }

        private static void AssertDigest(int enzymeIndex, int missedCleavages, int minLength, int maxLength, bool clip,
            int albuminCount, string albuminSha256, params string[] ambiguous)
        {
            var settings = new DigestSettings
            {
                EnzymeIndex = enzymeIndex,
                MaxMissedCleavages = missedCleavages,
                MinLength = minLength,
                MaxLength = maxLength,
                ClipNTermMethionine = clip,
            };
            string label = string.Format(@"enzyme {0}, mc {1}, {2}-{3}, clip {4}", enzymeIndex, missedCleavages, minLength, maxLength, clip);
            var albumin = new Digester(settings).Digest(ALBUMIN).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            Assert.AreEqual(albuminCount, albumin.Length, label);
            Assert.AreEqual(albuminSha256, Sha256(string.Join(@",", albumin)), label);
            var peptides = new Digester(settings).Digest(AMBIGUOUS).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(ambiguous, peptides, label);
        }

        private static void AssertRecord(string header, string db, string accession, string entry)
        {
            var record = ProteinRecord.Parse(header, @"PEPTIDEK");
            Assert.AreEqual(db, record.Db, header);
            Assert.AreEqual(accession, record.Accession, header);
            Assert.AreEqual(entry, record.EntryName, header);
        }

        private static FastaRecord[] Read(string text)
        {
            return FastaReader.Read(new StringReader(text)).ToArray();
        }

        private static string Sha256(string text)
        {
            return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        }
    }
}
