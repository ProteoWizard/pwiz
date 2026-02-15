/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.API;
using pwiz.ProteomeDatabase.DataModel;
using pwiz.ProteomeDatabase.Fasta;
using pwiz.SkylineTestUtil;

namespace CommonTest
{

    public class FastaHeaderReaderResult : IEquatable<FastaHeaderReaderResult>
    {
        /// <summary>
        /// Holds what we can learn from a FASTA header line, along with the expected sequence length
        /// Sequence length is interesting for its role in disambiguating results from web service lookups
        /// </summary>
        /// <param name="accession">accession info</param>
        /// <param name="preferredname">human readable name</param>
        /// <param name="name">human readable name as parsed from fasta file with basic name-space-description pattern </param>
        /// <param name="description">human readable description as parsed from fasta file with basic name-space-description pattern</param>
        /// <param name="species">species, when known</param>
        /// <param name="gene">gene, when known</param>
        /// <param name="seqlen">expected sequence length</param>
        /// <param name="websearchcode">hint for faking up web search response</param>
        /// <param name="wellformed">if true, expect that result can be arrived at without web access</param>
        public FastaHeaderReaderResult(string accession, string preferredname, string name,
            string description,
            string species, 
            string gene, 
            int seqlen,
            char websearchcode = WebEnabledFastaImporter.UNIPROTKB_TAG,
            bool wellformed = false)
        {
            Protein = new ProteinMetadata(name, description, preferredname, accession, gene, species, websearchcode.ToString(CultureInfo.InvariantCulture));
            Wellformed = wellformed;
            SeqLen = seqlen;
        }

        public ProteinMetadata Protein { get; set; }
        public bool Wellformed { get; private set; }
        public int SeqLen { get; set; }

        private static string FindTerm(string str, string splitter)
        {
            if ((str!=null) && str.Contains(splitter.Replace(@"\",""))) // Not L10N
            {
                var splits = Regex.Split(str, splitter, RegexOptions.IgnoreCase|RegexOptions.CultureInvariant); // Not L10N
                var after = Regex.Split(splits[1],@"[ |\.]")[0]; // Not L10N
                return after;
            }
            return null;
        }

        public static string FindTerm(ProteinMetadata protein, string splitter)
        {
            return FindTerm(protein.Name, splitter) ?? FindTerm(protein.Description, splitter);
        }

        public string GetIntermediateSearchterm(string initialSearchTerm)
        {
            // get the intermediate step - what entrez would return that we would take to uniprot
            if (initialSearchTerm != null)
            {
                var term = char.IsDigit(initialSearchTerm, 1) ? (FindTerm(Protein, @"ref\|") ?? FindTerm(Protein, @"SW\:") ?? FindTerm(Protein, @"pir\|\|")) : null; // hopefully go from GI to ref
                if (String.IsNullOrEmpty(term)) // xp_nnnn
                {
                     term = Protein.Accession; // no obvious hints in our test sets, just use the expected accession
                }
                return term;
            }
            return null;
        }

        public bool Equals(FastaHeaderReaderResult other)
        {            
            return other != null && Equals(Protein, other.Protein);
        }
    }

    /// <summary>
    /// tests our ability to import various wildtype FASTA header lines, including some that need web services for full extraction
    /// </summary>
    [TestClass]
    public class FastaImporterTest : AbstractUnitTestEx
    {
        private const string FASTA_EXPECTED_JSON_RELATIVE_PATH = @"CommonTest\FastaImporterTestExpected.json";

        private FastaImporterExpectedData _cachedExpectedData;

        private const string NEGTEST_NAME = @"Q9090909090"; // For use in negative test
        private const string NEGTEST_DESCRIPTION = @"this is meant to fail"; // For use in negative test
        private const string novalue = null;

        private static List<FastaHeaderParserTest> MergeExpectedRecords(List<FastaHeaderParserTest> tests,
            IReadOnlyDictionary<string, FastaImporterExpectedRecord> expectedRecords)
        {
            if (expectedRecords == null || expectedRecords.Count == 0)
                return tests;

            var merged = new List<FastaHeaderParserTest>(tests.Count);
            foreach (var test in tests)
            {
                if (!ShouldUseRecordedExpectations(test.Header) ||
                    !expectedRecords.TryGetValue(test.Header, out var record) ||
                    record?.Results == null ||
                    record.Results.Count == 0)
                {
                    merged.Add(test);
                    continue;
                }

                var expectedResults = record.Results
                    .Select(ConvertExpectedResult)
                    .ToArray();
                merged.Add(new FastaHeaderParserTest(test.Header, expectedResults));
            }

            return merged;
        }

        private static bool ShouldUseRecordedExpectations(string header)
        {
            if (string.IsNullOrEmpty(header))
                return true;
            if (header.Contains(NEGTEST_NAME))
                return false;
            return true;
        }

        private static FastaHeaderReaderResult ConvertExpectedResult(FastaImporterExpectedResult result)
        {
            return new FastaHeaderReaderResult(
                accession: result.Accession ?? string.Empty,
                preferredname: result.PreferredName ?? string.Empty,
                name: result.Name ?? string.Empty,
                description: result.Description ?? string.Empty,
                species: result.Species ?? string.Empty,
                gene: result.Gene ?? string.Empty,
                seqlen: Math.Max(result.SequenceLength, 0),
                websearchcode: WebEnabledFastaImporter.UNIPROTKB_TAG,
                wellformed: result.WellFormed);
        }
 
 
        public class FastaHeaderParserTest
        {
            public FastaHeaderParserTest(string header, FastaHeaderReaderResult[] expectedResults)
            {
                Header = header;
                ExpectedResults = expectedResults;
                // one or more sets of parsed parts (more than one with SOH-seperated header)
            }

            public string Header { get; private set; }
            public FastaHeaderReaderResult[] ExpectedResults { get; private set; }
        }

        /// <summary>
        /// Return a list of fasta entries, the first bunch of which can't possibly work, to 
        /// exercise the logic that keeps us from trying against hopeless data and bogging down the user.
        /// </summary>
        private static List<FastaHeaderParserTest> GetNegativeTests()
        {
            var result = new List<FastaHeaderParserTest>();

            for (int i = 0; i < 2 * WebEnabledFastaImporter.MAX_CONSECUTIVE_PROTEIN_METATDATA_LOOKUP_FAILURES; i++) 
            {
                var nonsense = string.Format("Q999999{0}", i);
                result.Add(new FastaHeaderParserTest(@">" + nonsense,
                     new[]
                    {
                        new FastaHeaderReaderResult(name: nonsense, accession:novalue, preferredname: novalue,
                            description: novalue, species: novalue, gene: novalue, 
                            seqlen:123+i%(WebEnabledFastaImporter.MAX_CONSECUTIVE_PROTEIN_METATDATA_LOOKUP_FAILURES/2))
                    }));
            }

            // This search target should not be attempted due to those above already failing
            // So if it comes up with these metadata values, we aren't working properly
            result.Add(new FastaHeaderParserTest(
                @">"+NEGTEST_NAME+@" "+NEGTEST_DESCRIPTION,
                new[]
                {
                    new FastaHeaderReaderResult(name: NEGTEST_NAME, accession: "fish",
                        preferredname: "badnews", description:NEGTEST_DESCRIPTION, species: "sandwich", gene: "baseball", seqlen: 234)
                }));

            return result;
        }

        private static List<FastaHeaderParserTest> GetTests(int minSequenceLength)
        {

            var list = new List<FastaHeaderParserTest>
            {

                new FastaHeaderParserTest(
                    // Test handling peptide list name - unknown sequence length, and multiple uniprot results with some common attribures like species
                    // NOTE: this MUST be the 0th test in this list because it has no sequence
                    @">Q08446",  // Returns several records in Unitprot search, but all with same species
                    new[]
                    {
                        new FastaHeaderReaderResult(accession: "",
                            name: "Q08446",
                            preferredname: "",
                            description: "",
                            species: "Saccharomyces cerevisiae (strain ATCC 204508 / S288c) (Baker's yeast)", gene: "", seqlen:0) // No sequence - MUST be first in list
                    }),

                new FastaHeaderParserTest(
                    // Test handling of OS= and GN= but nothing afterward (this was formerly unsupported by the regex)
                    @">sp|P10636-3|TAU_HUMAN Isoform Tau-A of Microtubule-associated protein tau OS=Homo sapiens GN=MAPT",
                    new[]
                    {
                        new FastaHeaderReaderResult(accession: "P10636-3",
                            name: "sp|P10636-3|TAU_HUMAN",
                            preferredname: "TAU_HUMAN",
                            description: "Isoform Tau-A of Microtubule-associated protein tau OS=Homo sapiens GN=MAPT",
                            species: "Homo sapiens", gene: "MAPT", seqlen:316,
                            wellformed:true) // Should not require web access for complete metadata
                    }),

                new FastaHeaderParserTest(
                    // Test handling of OS= but nothing afterward, and also isoform handling
                    @">sp|P10636-2|TAU_HUMAN Isoform Fetal-tau of Microtubule-associated protein tau OS=Homo sapiens",
                    new[]
                    {
                        new FastaHeaderReaderResult(accession: "P10636-2",
                            name: "sp|P10636-2|TAU_HUMAN",
                            preferredname: "TAU_HUMAN",
                            description: "Isoform Fetal-tau of Microtubule-associated protein tau OS=Homo sapiens",
                            species: "Homo sapiens", gene: "", seqlen:352) // Uniprot won't return isoforms so we won't learn the gene
                    }),

                new FastaHeaderParserTest(
                    @">IPI:IPI00197700.1 Tax_Id=10116 Gene_Symbol=Apoa2 Apolipoprotein A-II",
                    new[]
                    {
                        new FastaHeaderReaderResult(accession: "P04638",
                            name: "IPI:IPI00197700.1",
                            preferredname: "APOA2_RAT",
                            description: "Tax_Id=10116 Gene_Symbol=Apoa2 Apolipoprotein A-II",
                            species: "Rattus norvegicus (Rat)", gene: "Apoa2", seqlen:102)
                    }),

                // If you follow xp_915497.1 to NP_035634 and follow that to uniprot, the protein length changes from 566 to 592, so we don't pursue to Uniprot
                new FastaHeaderParserTest(
                    ">ref|xp_915497.1| PREDICTED: similar to Syntaxin binding protein 3 (UNC-18 homolog 3) (UNC-18C) (MUNC-18-3) [Mus musculus].  Id=ref|XP_915497.1|gi|82891194| hash=28566A6F69346EB3",
                    new[]
                    {
                        new FastaHeaderReaderResult(accession: "NP_035634", name: "ref|xp_915497.1|",
                            preferredname: "",
                            description:
                                "PREDICTED: similar to Syntaxin binding protein 3 (UNC-18 homolog 3) (UNC-18C) (MUNC-18-3) [Mus musculus].  Id=ref|XP_915497.1|gi|82891194| hash=28566A6F69346EB3",
                            species: "", gene: "", seqlen:566)
                    }),
                new FastaHeaderParserTest(
                    @">IPI:IPI00197700.1|SWISS-PROT:P04638|ENSEMBL:ENSRNOP00000004662|REFSEQ:NP_037244 Tax_Id=10116 Gene_Symbol=Apoa2 Apolipoprotein A-II",
                    new[]
                    {
                        new FastaHeaderReaderResult(accession: "P04638",
                            name: "IPI:IPI00197700.1|SWISS-PROT:P04638|ENSEMBL:ENSRNOP00000004662|REFSEQ:NP_037244",
                            preferredname: "APOA2_RAT",
                            description: "Tax_Id=10116 Gene_Symbol=Apoa2 Apolipoprotein A-II",
                            species: "Rattus norvegicus (Rat)", gene: "Apoa2", seqlen:102)
                    }),

                new FastaHeaderParserTest(
                    @">"+WebEnabledFastaImporter.KNOWNGOOD_UNIPROT_SEARCH_TARGET,
                    new[]
                    {
                        new FastaHeaderReaderResult(accession: WebEnabledFastaImporter.KNOWNGOOD_UNIPROT_SEARCH_TARGET,
                            name: WebEnabledFastaImporter.KNOWNGOOD_UNIPROT_SEARCH_TARGET,
                            preferredname: "AB140_YEAST",
                            description: "tRNA(Thr) (cytosine(32)-N(3))-methyltransferase (EC 2.1.1.268) (Actin-binding protein of 140 kDa) (tRNA methyltransferase of 140 kDa)",
                            species: "Saccharomyces cerevisiae (strain ATCC 204508 / S288c) (Baker's yeast)", gene: "ABP140 TRM140 YOR239W YOR240W", seqlen:WebEnabledFastaImporter.KNOWNGOOD_UNIPROT_SEARCH_TARGET_SEQLEN)
                    }),

                new FastaHeaderParserTest(
                    ">SYHC Histidyl-tRNA synthetase, cytoplasmic OS=Homo sapiens GN=HARS PE=1 SV=2",
                    new[]
                    {
                        new FastaHeaderReaderResult(accession: novalue, name: "SYHC", preferredname: novalue,
                            description: "Histidyl-tRNA synthetase, cytoplasmic OS=Homo sapiens GN=HARS PE=1 SV=2",
                            species: "Homo sapiens", gene: "HARS", seqlen:20)
                    }),

                new FastaHeaderParserTest(
                    ">YOR242C SSP2 SGDID:S000005768, Chr XV from 789857-788742, reverse complement, Verified ORF, \"Sporulation specific protein that localizes to the spore wall; required for sporulation at a point after meiosis II and during spore wall formation; SSP2 expression is induced midway in meiosis\"",
                    new[]
                    {
                        new FastaHeaderReaderResult(accession: "Q08646", name: "YOR242C",
                            preferredname: "SSP2_YEAST",
                            description:
                                "SSP2 SGDID:S000005768, Chr XV from 789857-788742, reverse complement, Verified ORF, \"Sporulation specific protein that localizes to the spore wall; required for sporulation at a point after meiosis II and during spore wall formation; SSP2 expression is induced midway in meiosis\"",
                            species: "Saccharomyces cerevisiae (strain ATCC 204508 / S288c) (Baker's yeast)", gene: "SSP2 YOR242C O5251", seqlen:371)
                    }),
                new FastaHeaderParserTest(
                    ">F26D10.3	CE09682 WBGene00002005 locus:hsp-1 HSP-1 heat shock 70kd protein A status:Confirmed SW:P09446 protein_id:CAB02319.1",
                    new[]
                    {
                        new FastaHeaderReaderResult(name: "F26D10.3", accession: "P09446",
                            preferredname: "HSP7A_CAEEL",
                            description:
                                "CE09682 WBGene00002005 locus:hsp-1 HSP-1 heat shock 70kd protein A status:Confirmed SW:P09446 protein_id:CAB02319.1",
                            species: "Caenorhabditis elegans", gene: "hsp-1 hsp70a F26D10.3", seqlen:640)
                    }),
                new FastaHeaderParserTest(
                    ">UniRef100_A5DI11 Elongation factor 2 n=1 Tax=Pichia guilliermondii RepID=EF2_PICGU",
                    new[]
                    {
                        new FastaHeaderReaderResult(name: "UniRef100_A5DI11", accession: "A5DI11",
                            preferredname: "EF2_PICGU",
                            description: "Elongation factor 2 n=1 Tax=Pichia guilliermondii RepID=EF2_PICGU",
                            species: "Meyerozyma guilliermondii (strain ATCC 6260 / CBS 566 / DSM 6381 / JCM 1539 / NBRC 10279 / NRRL Y-324) (Yeast) (Candida guilliermondii)", 
                            gene: "EFT2 PGUG_02912", seqlen:842)
                    }),

                new FastaHeaderParserTest(
                    @">gi|"+WebEnabledFastaImporter.KNOWNGOOD_GENINFO_SEARCH_TARGET+"|30S_ribosomal_sub gi|15834432|ref|NP_313205.1| 30S ribosomal subunit protein S18 [Escherichia coli O157:H7]",
                    new[]
                    {
                        new FastaHeaderReaderResult(accession: "P0A7T9",
                            name: "gi|15834432|30S_ribosomal_sub", preferredname: "RS18_ECO57",
                            description:
                                "gi|15834432|ref|NP_313205.1| 30S ribosomal subunit protein S18 [Escherichia coli O157:H7]",
                            species: "Escherichia coli O157:H7", gene: "rpsR Z5811 ECs5178", seqlen:75)
                    }),

                new FastaHeaderParserTest(
                    @">NP_313205 gi|15834432|ref|NP_313205.1| 30S ribosomal subunit protein S18 [Escherichia coli O157:H7]",
                    new[]
                    {
                        new FastaHeaderReaderResult(accession: "P0A7T9",
                            name: "NP_313205", preferredname: "RS18_ECO57",
                            description:
                                "gi|15834432|ref|NP_313205.1| 30S ribosomal subunit protein S18 [Escherichia coli O157:H7]",
                            species: "Escherichia coli O157:H7", gene: "rpsR Z5811 ECs5178", seqlen:75)
                    }),

                new FastaHeaderParserTest(
                    @">sp|P01222|TSHB_HUMAN Thyrotropin subunit beta OS=Homo sapiens GN=TSHB PE=1 SV=2",
                    new[]
                    {
                        new FastaHeaderReaderResult(accession: "P01222", name: "sp|P01222|TSHB_HUMAN",
                            preferredname: "TSHB_HUMAN",
                            description: "Thyrotropin subunit beta OS=Homo sapiens GN=TSHB PE=1 SV=2",
                            species: "Homo sapiens", gene: "TSHB", seqlen:138,
                            wellformed: true) // Well-formed, should require no search
                    }),
                new FastaHeaderParserTest(
                    ">Y62E10A.1	CE22694 WBGene00004410 locus:rpa-2 status:Confirmed TR:Q9U1X9 protein_id:CAB60595.1",
                    new[]
                    {
                        new FastaHeaderReaderResult( 
                            name:"Y62E10A.1", accession:"Q9U1X9", preferredname:"Q9U1X9_CAEEL", 
                            description:"CE22694 WBGene00004410 locus:rpa-2 status:Confirmed TR:Q9U1X9 protein_id:CAB60595.1", 
                            gene:"rla-2 CELE_Y62E10A.1 Y62E10A.1", species:"Caenorhabditis elegans", seqlen:110)
                    }),
                new FastaHeaderParserTest(">CGI_10000780",
                    new[]
                    {
                        new FastaHeaderReaderResult(accession: "K1QN71", name: "CGI_10000780",
                            preferredname: "K1QN71_CRAGI",
                            description: "Uncharacterized protein", species: "Crassostrea gigas (Pacific oyster) (Crassostrea angulata)", gene: "CGI_10000780", seqlen:449)
                    }),
                new FastaHeaderParserTest(
                    ">ENSMUSP00000100344 pep:known chromosome:GRCm38:14:52427928:52428874:1 gene:ENSMUSG00000076758 transcript:ENSMUST00000103567 gene_biotype:TR_V_gene transcript_biotype:TR_V_gene",
                    new[]
                    {
                        new FastaHeaderReaderResult(accession: "A0A075B5Z4", name: "ENSMUSP00000100344",
                            preferredname: "A0A075B5Z4_MOUSE",
                            description:
                                "pep:known chromosome:GRCm38:14:52427928:52428874:1 gene:ENSMUSG00000076758 transcript:ENSMUST00000103567 gene_biotype:TR_V_gene transcript_biotype:TR_V_gene",
                            species: "Mus musculus (Mouse)", gene: "Trav1", seqlen:110)
                    }),

                new FastaHeaderParserTest(">AARS.IPI00027442 IPI:IPI00027442.4|SWISS-PROT:P49588|ENSEMBL:ENSP00000261772|REFSEQ:NP_001596|H-INV:HIT000035254|VEGA:OTTHUMP00000080084 Tax_Id=9606 Gene_Symbol=AARS Alanyl-tRNA synthetase, cytoplasmic",
                    new[]
                    {
                        new FastaHeaderReaderResult(accession: "P49588", name: "AARS.IPI00027442",
                            preferredname: "SYAC_HUMAN",
                            description: "IPI:IPI00027442.4|SWISS-PROT:P49588|ENSEMBL:ENSP00000261772|REFSEQ:NP_001596|H-INV:HIT000035254|VEGA:OTTHUMP00000080084 Tax_Id=9606 Gene_Symbol=AARS Alanyl-tRNA synthetase, cytoplasmic", 
                            species: "Homo sapiens (Human)", gene: "AARS", seqlen:968)
                    }),

                new FastaHeaderParserTest(
                    // they may not show in the IDE but there are SOH (ASCII 0x001) characters in here
                    @">gi|15834432|30S_ribosomal_sub| gi|15834432|ref|NP_313205.1| 30S ribosomal subunit protein S18 [Escherichia coli O157:H7]gi|16132024|ref|NP_418623.1| 30S ribosomal subunit protein S18 [Escherichia coli K12]gi|16763210|ref|NP_458827.1| 30s ribosomal subunit protein S18 [Salmonella enterica subsp. enterica serovar Typhi]gi|24115555|ref|NP_710065.1| 30S ribosomal subunit protein S18 [Shigella flexneri 2a str. 301]gi|26251099|ref|NP_757139.1| 30S ribosomal protein S18 [Escherichia coli CFT073]gi|29144689|ref|NP_808031.1| 30s ribosomal subunit protein S18 [Salmonella enterica subsp. enterica serovar Typhi Ty2]gi|30065573|ref|NP_839744.1| 30S ribosomal subunit protein S18 [Shigella flexneri 2a str. 2457T]gi|133836|sp|P02374|RS18_ECOLI 30S ribosomal protein S18gi|2144767|pir||R3EC18 ribosomal protein S18 [validated] - Escherichia coli (strain K-12)gi|25294828|pir||AI1052 30s ribosomal chain protein S18 [imported] - Salmonella enterica subsp. enterica serovar Typhi (strain CT18)gi|25294838|pir||B91276 30S ribosomal subunit protein S18 [imported] - Escherichia coli (strain O157:H7, substrain RIMD 0509952)gi|42847|emb|CAA27654.1| unnamed protein product [Escherichia coli]gi|537043|gb|AAA97098.1| 30S ribosomal subunit protein S18 [Escherichia coli]gi|1790646|gb|AAC77159.1| 30S ribosomal subunit protein S18 [Escherichia coli K12]gi|13364655|dbj|BAB38601.1| 30S ribosomal subunit protein S18 [Escherichia coli O157:H7]gi|16505518|emb|CAD06870.1| 30s ribosomal subunit protein S18 [Salmonella enterica subsp. enterica serovar Typhi]gi|24054886|gb|AAN45772.1|AE015442_2 30S ribosomal subunit protein S18 [Shigella flexneri 2a str. 301]gi|26111531|gb|AAN83713.1|AE016771_224 30S ribosomal protein S18 [Escherichia coli CFT073]gi|29140328|gb|AAO71891.1| 30s ribosomal subunit protein S18 [Salmonella enterica subsp. enterica serovar Typhi Ty2]gi|30043837|gb|AAP19556.1| 30S ribosomal subunit protein S18 [Shigella flexneri 2a str. 2457T] [MASS=8986]",
                    new[]
                    {
                        new FastaHeaderReaderResult(
                            name:"gi|15834432|30S_ribosomal_sub|", accession:"P0A7T9", preferredname:"RS18_ECO57", description:"gi|15834432|ref|NP_313205.1| 30S ribosomal subunit protein S18 [Escherichia coli O157:H7]", gene:"rpsR Z5811 ECs5178", species:"Escherichia coli O157:H7", seqlen:75),
                        new FastaHeaderReaderResult(
                            name:"gi|16132024|ref|NP_418623.1|", accession:"P0A7T7", preferredname:"RS18_ECOLI", description:"30S ribosomal subunit protein S18 [Escherichia coli K12]", gene:"rpsR b4202 JW4160", species:"Escherichia coli (strain K12)", seqlen:75),
                        new FastaHeaderReaderResult(
                            name:"gi|16763210|ref|NP_458827.1|", accession:"P0A7U1", preferredname:"RS18_SALTI", description:"30s ribosomal subunit protein S18 [Salmonella enterica subsp. enterica serovar Typhi]", gene:"rpsR STY4749 t4444", species:"Salmonella typhi", seqlen:75),
                        new FastaHeaderReaderResult(
                            name:"gi|24115555|ref|NP_710065.1|", accession:"P0A7U2", preferredname:"RS18_SHIFL", description:"30S ribosomal subunit protein S18 [Shigella flexneri 2a str. 301]", gene:"rpsR SF4355 S4627", species:"Shigella flexneri", seqlen:75),
                        new FastaHeaderReaderResult( // Note: entrez now returns comment "This RefSeq genome was suppressed because updated RefSeq validation criteria identified problems with the assembly or annotation."
                            name:"gi|26251099|ref|NP_757139.1|", accession:"WP_000135199.1", preferredname:"ref|NP_757139.1|", description:"30S ribosomal protein S18 [Escherichia coli CFT073]", gene:novalue, species:novalue, seqlen:75),
                        new FastaHeaderReaderResult( // Note: entrez now returns comment "This RefSeq genome was suppressed because updated RefSeq validation criteria identified problems with the assembly or annotation."
                            name:"gi|29144689|ref|NP_808031.1|", accession:"WP_000135199.1", preferredname:"ref|NP_808031.1|", description:"30s ribosomal subunit protein S18 [Salmonella enterica subsp. enterica serovar Typhi Ty2]", gene:novalue, species:novalue, seqlen:75),
                        new FastaHeaderReaderResult( // Note: entrez now returns comment "This RefSeq genome was suppressed because updated RefSeq validation criteria identified problems with the assembly or annotation."
                            name:"gi|30065573|ref|NP_839744.1|", accession:"WP_000135199.1", preferredname:"ref|NP_839744.1|", description:"30S ribosomal subunit protein S18 [Shigella flexneri 2a str. 2457T]", gene:novalue, species:novalue, seqlen:75),
                        new FastaHeaderReaderResult(
                            name:"gi|133836|sp|P02374|RS18_ECOLI", accession:"P0A7T7", preferredname:"RS18_ECOLI", description:"30S ribosomal protein S18", gene:"rpsR b4202 JW4160", species:"Escherichia coli (strain K12)", seqlen:75),
                        new FastaHeaderReaderResult( // gi2144767 points to P02374 which was demerged into P0A7T7, P0A7T8, P0A7T9, P0A7U0, P0A7U1 and P0A7U2 and thus ambiguous
                            name:"gi|2144767|pir||R3EC18", accession:"P02374", preferredname:"pir||R3EC18", description:"ribosomal protein S18 [validated] - Escherichia coli (strain K-12)", gene:novalue, species:novalue, seqlen:75),
                        new FastaHeaderReaderResult( // entrez gives AI1052 as accession from gi|25294828 but UniprotKB doesn't recognize that accession, so no gene or species
                            name:"gi|25294828|pir||AI1052", accession:"AI1052", preferredname:"pir||AI1052", description:"30s ribosomal chain protein S18 [imported] - Salmonella enterica subsp. enterica serovar Typhi (strain CT18)", gene:novalue, species:novalue, seqlen:75),
                        new FastaHeaderReaderResult( // gi25294838 points to P02374 which was demerged into P0A7T7, P0A7T8, P0A7T9, P0A7U0, P0A7U1 and P0A7U2 and thus ambiguous
                            name:"gi|25294838|pir||B91276", accession:"P02374", preferredname:"pir||B91276", description:"30S ribosomal subunit protein S18 [imported] - Escherichia coli (strain O157:H7, substrain RIMD 0509952)",  species:novalue, gene:novalue, seqlen:75),
                        new FastaHeaderReaderResult(
                            name:"gi|42847|emb|CAA27654.1|", accession:"P0A7T7", preferredname:"RS18_ECOLI", description:"unnamed protein product [Escherichia coli]", gene:"rpsR b4202 JW4160", species:"Escherichia coli (strain K12)", seqlen:75),
                        new FastaHeaderReaderResult(
                            name:"gi|537043|gb|AAA97098.1|", accession:"P0A7T7", preferredname:"RS18_ECOLI", description:"30S ribosomal subunit protein S18 [Escherichia coli]", gene:"rpsR b4202 JW4160", species:"Escherichia coli (strain K12)", seqlen:75),
                        new FastaHeaderReaderResult(
                            name:"gi|1790646|gb|AAC77159.1|", accession:"P0A7T7", preferredname:"RS18_ECOLI", description:"30S ribosomal subunit protein S18 [Escherichia coli K12]", gene:"rpsR b4202 JW4160", species:"Escherichia coli (strain K12)", seqlen:75),
                        new FastaHeaderReaderResult(
                            name:"gi|13364655|dbj|BAB38601.1|", accession:"P0A7T9", preferredname:"RS18_ECO57", description:"30S ribosomal subunit protein S18 [Escherichia coli O157:H7]", gene:"rpsR Z5811 ECs5178", species:"Escherichia coli O157:H7", seqlen:75),
                        new FastaHeaderReaderResult(
                            name:"gi|16505518|emb|CAD06870.1|", accession:"P0A7U1", preferredname:"RS18_SALTI", description:"30s ribosomal subunit protein S18 [Salmonella enterica subsp. enterica serovar Typhi]", gene:"rpsR STY4749 t4444", species:"Salmonella typhi", seqlen:75),
                        new FastaHeaderReaderResult(
                            name:"gi|24054886|gb|AAN45772.1|AE015442_2", accession:"P0A7U2", preferredname:"RS18_SHIFL", description:"30S ribosomal subunit protein S18 [Shigella flexneri 2a str. 301]", gene:"rpsR SF4355 S4627", species:"Shigella flexneri", seqlen:75),
                        new FastaHeaderReaderResult(
                            name:"gi|26111531|gb|AAN83713.1|AE016771_224", accession:"P0A7T8", preferredname:"RS18_ECOL6", description:"30S ribosomal protein S18 [Escherichia coli CFT073]", gene:"rpsR c5292", species:"Escherichia coli O6:H1 (strain CFT073 / ATCC 700928 / UPEC)", seqlen:75),
                        new FastaHeaderReaderResult(
                            name:"gi|29140328|gb|AAO71891.1|", accession:"P0A7U1", preferredname:"RS18_SALTI", description:"30s ribosomal subunit protein S18 [Salmonella enterica subsp. enterica serovar Typhi Ty2]", gene:"rpsR STY4749 t4444", species:"Salmonella typhi", seqlen:75),
                        new FastaHeaderReaderResult(
                            name:"gi|30043837|gb|AAP19556.1|", accession:"P0A7U2", preferredname:"RS18_SHIFL", description:"30S ribosomal subunit protein S18 [Shigella flexneri 2a str. 2457T] [MASS=8986]", gene:"rpsR SF4355 S4627", species:"Shigella flexneri", seqlen:75)
                    }),

                // keep this one negative test here in the middle, it ensures more code coverage in the retry code
                new FastaHeaderParserTest(">scoogly doodly abeebopboom",
                    new[]
                    {
                        new FastaHeaderReaderResult(accession: novalue, name: "scoogly", preferredname: novalue,
                            description: "doodly abeebopboom", species: novalue, gene: novalue, seqlen:340)
                    }),
                new FastaHeaderParserTest(
                    ">AAS51520 pep:known chromosome:ASM9102v1:IV:2278:3450:1 gene:AGOS_ADL400W transcript:AAS51520 description:\"ADL400WpAFR758Cp\"",
                    new[]
                    {
                        new FastaHeaderReaderResult(accession: "Q75BG2", name: "AAS51520",
                            preferredname: "Q75BG2_ASHGO",
                            description:
                                "pep:known chromosome:ASM9102v1:IV:2278:3450:1 gene:AGOS_ADL400W transcript:AAS51520 description:\"ADL400WpAFR758Cp\"",
                            species: "Ashbya gossypii (strain ATCC 10895 / CBS 109.51 / FGSC 9923 / NRRL Y-1056) (Yeast) (Eremothecium gossypii)",
                            gene: "AGOS_ADL400W AGOS_AFR758C", seqlen:390)
                    }),

                new FastaHeaderParserTest( // some kind of homegrown numbering scheme - don't even go to the web for that
                    ">000001 ENSBTAP00000055373 pep:known scaffold:UMD3.1:GJ059509.1:3535:6470:-1 gene:ENSBTAG00000047958 transcript:ENSBTAT00000064726 gene_biotype:protein_coding transcript_biotype:protein_coding",
                    new[]
                    {
                        new FastaHeaderReaderResult(accession: novalue, name: "000001",
                            preferredname: novalue,
                            description: "ENSBTAP00000055373 pep:known scaffold:UMD3.1:GJ059509.1:3535:6470:-1 gene:ENSBTAG00000047958 transcript:ENSBTAT00000064726 gene_biotype:protein_coding transcript_biotype:protein_coding", 
                            species: novalue, gene: novalue, seqlen:330, websearchcode: WebEnabledFastaImporter.SEARCHDONE_TAG)
                    }),

               // keep these negative tests at end, it ensures more code coverage in the retry code
                new FastaHeaderParserTest( // this one is a negative test
                    @">"+NEGTEST_NAME+@" "+NEGTEST_DESCRIPTION,
                    new[]
                    {
                        // no, this is not the right answer - it's a negative test
                        new FastaHeaderReaderResult(accession: "Happymeal", preferredname: "fish",
                            name: "grackle", description: "cat",
                            species: "Cleveland", gene: "France", seqlen:130)
                    }),

                new FastaHeaderParserTest( // Deal with inputs that are too short - search would be huge
                    ">ABC", 
                    new[]
                    {
                        new FastaHeaderReaderResult(accession: novalue, name: "ABC",
                            preferredname: novalue,
                            description: novalue, species: novalue, gene: novalue, seqlen:110, websearchcode: WebEnabledFastaImporter.SEARCHDONE_TAG)
                    }),

                new FastaHeaderParserTest( // Deal with inputs that contain characters that look like search directives
                    ">eat[19]:fish(b)", 
                    new[]
                    {
                        new FastaHeaderReaderResult(accession: novalue, name: "eat[19]:fish(b)",
                            preferredname: novalue,
                            description: novalue, species: novalue, gene: novalue, seqlen:10, websearchcode: WebEnabledFastaImporter.SEARCHDONE_TAG)
                    }),

                new FastaHeaderParserTest( // failure is expected with uniprot service
                    ">CGI_99999999", // no such thing
                    new[]
                    {
                        new FastaHeaderReaderResult(accession: novalue, name: "CGI_99999999",
                            preferredname: novalue,
                            description: novalue, species: novalue, gene: novalue, seqlen:120, websearchcode: WebEnabledFastaImporter.SEARCHDONE_TAG)
                    }),

                new FastaHeaderParserTest( // doesn't exist, lookup failure is expected
                    ">ENSMUSP99999999999 pep:known chromosome:GRCm83:14:52742928:52824874:1 gene:ENSMUSG99999999999999 transcript:ENSMUST9999999999999999 gene_biotype:TR_Z_gene transcript_biotype:TR_Z_gene",
                    new[]
                    {
                        new FastaHeaderReaderResult(accession: novalue,
                            name: "ENSMUSP99999999999", preferredname: novalue,
                            description:
                                "pep:known chromosome:GRCm83:14:52742928:52824874:1 gene:ENSMUSG99999999999999 transcript:ENSMUST9999999999999999 gene_biotype:TR_Z_gene transcript_biotype:TR_Z_gene",
                            species: novalue, gene: novalue, seqlen:230, websearchcode: WebEnabledFastaImporter.SEARCHDONE_TAG)
                    }),
               // keep these negative tests at end, it ensures more code coverage in the retry code
            };
            return list.Where(t => t.ExpectedResults[0].SeqLen > minSequenceLength).ToList();
        }

        private static List<ExpectedMetadataItem> GetExpectedMetadata(FastaHeaderParserTest test,
            IReadOnlyDictionary<string, FastaImporterExpectedRecord> expectedRecords)
        {
            if (!ShouldUseRecordedExpectations(test.Header))
                expectedRecords = null;
            if (expectedRecords != null &&
                expectedRecords.TryGetValue(test.Header, out var record) &&
                record?.Results != null &&
                record.Results.Count > 0)
            {
                return record.Results
                    .Select(ToExpectedMetadata)
                    .ToList();
            }

            return test.ExpectedResults
                .Select(ToExpectedMetadata)
                .ToList();
        }

        private static ExpectedMetadataItem ToExpectedMetadata(FastaImporterExpectedResult result)
        {
            var metadata = new ProteinMetadata(result.Name, result.Description, result.PreferredName,
                result.Accession, result.Gene, result.Species, result.WebSearchInfo);
            return new ExpectedMetadataItem(metadata, result.WellFormed, result.SequenceLength);
        }

        private static ExpectedMetadataItem ToExpectedMetadata(FastaHeaderReaderResult result)
        {
            var metadata = new ProteinMetadata(result.Protein.Name, result.Protein.Description,
                result.Protein.PreferredName, result.Protein.Accession, result.Protein.Gene,
                result.Protein.Species, result.Protein.WebSearchInfo.ToString());
            return new ExpectedMetadataItem(metadata, result.Wellformed, result.SeqLen);
        }

        private class ExpectedMetadataItem
        {
            public ExpectedMetadataItem(ProteinMetadata metadata, bool wellFormed, int sequenceLength)
            {
                Metadata = metadata;
                WellFormed = wellFormed;
                SequenceLength = sequenceLength;
            }

            public ProteinMetadata Metadata { get; }
            public bool WellFormed { get; }
            public int SequenceLength { get; }
        }

        private void RecordExpectedData(List<DbProtein> dbProteins, List<FastaHeaderParserTest> tests)
        {
            var jsonPath = TestContext.GetProjectDirectory(FASTA_EXPECTED_JSON_RELATIVE_PATH);
            Assert.IsNotNull(jsonPath, @"Unable to locate project-relative path for FASTA importer expectations.");

            var metadataByTest = new Dictionary<int, List<ProteinMetadata>>();
            var webSearchInfoByTest = new Dictionary<int, List<string>>();
            var sequenceLengths = new Dictionary<int, int>();

            foreach (var dbProtein in dbProteins)
            {
                var testnum = DecodeTestNumberFromSequence(dbProtein.Sequence);
                if (testnum < 0)
                    continue;

                if (!metadataByTest.TryGetValue(testnum, out var metadataList))
                {
                    metadataList = new List<ProteinMetadata>();
                    metadataByTest[testnum] = metadataList;
                }

                if (!webSearchInfoByTest.TryGetValue(testnum, out var webSearchList))
                {
                    webSearchList = new List<string>();
                    webSearchInfoByTest[testnum] = webSearchList;
                }

                sequenceLengths[testnum] = dbProtein.Sequence?.Length ?? 0;

                foreach (var name in dbProtein.Names)
                {
                    var metadata = name.GetProteinMetadata();
                    webSearchList.Add(metadata?.WebSearchInfo.ToString());
                    metadataList.Add(metadata?.ClearWebSearchInfo() ?? ProteinMetadata.EMPTY);
                }
            }

            var records = new List<FastaImporterExpectedRecord>();
            for (int i = 0; i < tests.Count; i++)
            {
                metadataByTest.TryGetValue(i, out var metadataList);
                metadataList ??= new List<ProteinMetadata>();
                webSearchInfoByTest.TryGetValue(i, out var webSearchList);
                webSearchList ??= new List<string>();
                var expectedResults = tests[i].ExpectedResults ?? Array.Empty<FastaHeaderReaderResult>();

                var record = new FastaImporterExpectedRecord
                {
                    Header = tests[i].Header,
                    Results = new List<FastaImporterExpectedResult>()
                };

                for (int j = 0; j < metadataList.Count; j++)
                {
                    var metadata = metadataList[j] ?? ProteinMetadata.EMPTY;
                    var expected = j < expectedResults.Length ? expectedResults[j] : null;
                    var sequenceLength = sequenceLengths.TryGetValue(i, out var length)
                        ? length
                        : expected?.SeqLen ?? 0;

                    record.Results.Add(new FastaImporterExpectedResult
                    {
                        Name = metadata.Name,
                        Description = metadata.Description,
                        PreferredName = metadata.PreferredName,
                        Accession = metadata.Accession,
                        Gene = metadata.Gene,
                        Species = metadata.Species,
                        SequenceLength = sequenceLength,
                        WellFormed = expected?.Wellformed ?? false,
                        WebSearchInfo = j < webSearchList.Count ? webSearchList[j] : null
                    });
                }

                if (metadataList.Count < expectedResults.Length)
                {
                    for (int j = metadataList.Count; j < expectedResults.Length; j++)
                    {
                        var expected = expectedResults[j];
                        if (expected == null)
                            continue;

                        record.Results.Add(new FastaImporterExpectedResult
                        {
                            Name = expected.Protein.Name,
                            Description = expected.Protein.Description,
                            PreferredName = expected.Protein.PreferredName,
                            Accession = expected.Protein.Accession,
                            Gene = expected.Protein.Gene,
                            Species = expected.Protein.Species,
                            SequenceLength = expected.SeqLen,
                            WellFormed = expected.Wellformed,
                            WebSearchInfo = expected.Protein.WebSearchInfo.ToString()
                        });
                    }
                }

                records.Add(record);
            }

            var data = new FastaImporterExpectedData
            {
                Records = records
            };
            data.RecordMap = data.Records.ToDictionary(r => r.Header, StringComparer.Ordinal);

            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(jsonPath, json, new UTF8Encoding(false));

            _cachedExpectedData = data;

            Console.Out.WriteLine(@"Recorded FASTA importer expectations to: " + jsonPath);
        }

        private FastaImporterExpectedData LoadExpectedData()
        {
            if (_cachedExpectedData != null)
                return _cachedExpectedData;

            var jsonPath = TestContext.GetProjectDirectory(FASTA_EXPECTED_JSON_RELATIVE_PATH);
            if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath))
            {
                _cachedExpectedData = new FastaImporterExpectedData();
                return _cachedExpectedData;
            }

            var json = File.ReadAllText(jsonPath, Encoding.UTF8);
            var data = JsonConvert.DeserializeObject<FastaImporterExpectedData>(json) ?? new FastaImporterExpectedData();
            data.Records ??= new List<FastaImporterExpectedRecord>();
            data.RecordMap = data.Records.ToDictionary(r => r.Header, StringComparer.Ordinal);

            _cachedExpectedData = data;
            return _cachedExpectedData;
        }

        private static Stream CreateResponseStream(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            return new MemoryStream(bytes);
        }

        private static HttpClientTestHelper BeginLegacyPlayback(IList<FastaHeaderParserTest> tests)
        {
            if (tests == null)
                throw new ArgumentNullException(nameof(tests));

            var testsCopy = tests.ToList();
            var playbackBehavior = new HttpClientTestBehavior
            {
                ResponseFactory = uri =>
                {
                    var url = uri.ToString();
                    if (TryBuildPlaybackResponse(url, testsCopy, out var text))
                        return CreateResponseStream(text);
                    throw new InvalidOperationException($"Unexpected URL during playback: {url}");
                }
            };

            return new HttpClientTestHelper(playbackBehavior);
        }

        public static IDisposable BeginPlaybackForFunctionalTests()
        {
            var tests = GetTests(1);

            // Load HTTP interactions from the standardized file (FastaImportTestWebData.json)
            var httpInteractions = LoadHttpInteractionsForType(typeof(FastaImporterTest), ExtensionTestContext.GetProjectDirectory);
            if (httpInteractions != null && httpInteractions.Count > 0)
            {
                return HttpClientTestHelper.PlaybackFromInteractions(httpInteractions);
            }

            return BeginLegacyPlayback(tests);
        }

        private static bool TryBuildPlaybackResponse(string url, IList<FastaHeaderParserTest> tests, out string response)
        {
            if (url.Contains(".gov"))
            {
                response = url.Contains("rettype=docsum")
                    ? BuildEntrezSummaryResponse(url, tests)
                    : BuildEntrezDetailResponse(url, tests);
                return true;
            }

            if (url.Contains(".org"))
            {
                response = BuildUniprotResponse(url, tests);
                return true;
            }

            response = null;
            return false;
        }

        private static string BuildEntrezSummaryResponse(string url, IList<FastaHeaderParserTest> tests)
        {
            var searches = ParseEntrezSearchTerms(url);
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\"?>\n<eSummaryResult>\n"); // Not L10N

            foreach (var search in searches)
            {
                var result = FindTest(tests, search);
                if (result == null || string.IsNullOrEmpty(result.Protein.Accession))
                    continue;

                var intermediateSearchTerm = result.GetIntermediateSearchterm(search);
                sb.AppendFormat("<Id>{0}</Id>", search);
                sb.Append("<DocSum> ");
                sb.AppendFormat("<Item Name=\"Caption\" Type=\"String\">{0}</Item>", intermediateSearchTerm);
                sb.AppendFormat("<Item Name=\"ReplacedBy\" Type=\"String\">{0}</Item>", intermediateSearchTerm);
                sb.AppendFormat("<Item Name=\"Length\" Type=\"Integer\">{0}</Item>", result.SeqLen);
                sb.Append("</DocSum>\n"); // Not L10N
            }

            sb.Append("</eSummaryResult>\n"); // Not L10N
            return sb.ToString();
        }

        private static string BuildEntrezDetailResponse(string url, IList<FastaHeaderParserTest> tests)
        {
            var searches = ParseEntrezSearchTerms(url);
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\"?>\n<GBSet>\n"); // Not L10N

            foreach (var search in searches)
            {
                var result = FindTest(tests, search);
                if (result == null || string.IsNullOrEmpty(result.Protein.Accession))
                    continue;

                sb.Append("<GBSeq> ");
                sb.AppendFormat("<GBSeq_length>{0}</GBSeq_length> ", result.SeqLen);
                if (result.Protein.PreferredName != null)
                    sb.AppendFormat("<GBSeq_locus>{0}</GBSeq_locus>", result.Protein.PreferredName); // Not L10N
                if (result.Protein.Description != null)
                    sb.AppendFormat(" <GBSeq_definition>{0}</GBSeq_definition> ", result.Protein.Description); // Not L10N
               if (result.Protein.Accession != null)
                    sb.AppendFormat("<GBSeq_primary-accession>{0}</GBSeq_primary-accession>", result.Protein.Accession); // Not L10N
                if (result.Protein.Species != null)
                    sb.AppendFormat("<GBSeq_organism>{0}</GBSeq_organism> ", result.Protein.Species); // Not L10N
                if (result.Protein.Gene != null)
                    sb.AppendFormat("<GBQualifier> <GBQualifier_name>gene</GBQualifier_name> <GBQualifier_value>{0}</GBQualifier_value> </GBQualifier> ", result.Protein.Gene); // Not L10N
                sb.Append("</GBSeq>\n"); // Not L10N
            }

            sb.Append("</GBSet>");
            return sb.ToString();
        }

        private static string BuildUniprotResponse(string url, IList<FastaHeaderParserTest> tests)
        {
            var searches = ParseUniprotSearchTerms(url);
            var sb = new StringBuilder();
            sb.Append("Entry\tEntry name\tProtein names\tGene names\tOrganism\tLength\tStatus\n"); // Not L10N

            foreach (var search in searches)
            {
                var result = FindTest(tests, search);
                if (result == null || string.IsNullOrEmpty(result.Protein.Accession))
                    continue;

                sb.AppendFormat("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\n", // Not L10N
                    result.Protein.Accession,
                    result.Protein.PreferredName ?? string.Empty,
                    result.Protein.Description ?? string.Empty,
                    result.Protein.Gene ?? string.Empty,
                    result.Protein.Species ?? string.Empty,
                    result.SeqLen,
                    "reviewed");
            }

            return sb.ToString();
        }

        private static string[] ParseEntrezSearchTerms(string url)
        {
            var parts = url.Split('=');
            if (parts.Length < 3)
                return Array.Empty<string>();
            var queryPart = parts[2].Split('&')[0];
            return queryPart.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static IEnumerable<string> ParseUniprotSearchTerms(string url)
        {
            var start = url.IndexOf("query=(", StringComparison.Ordinal);
            if (start < 0)
                return Array.Empty<string>();
            start += "query=(".Length;
            var end = url.IndexOf(')', start);
            if (end < 0)
                return Array.Empty<string>();
            var query = url.Substring(start, end - start);
            return query.Split(new[] { "+OR+" }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static FastaHeaderReaderResult FindTest(IEnumerable<FastaHeaderParserTest> tests, string keyword)
        {
            if (string.IsNullOrEmpty(keyword))
                return null;

            var upperKeyword = keyword.ToUpperInvariant();
            foreach (var test in tests)
            {
                var header = test.Header?.ToUpperInvariant();
                foreach (var expected in test.ExpectedResults ?? Array.Empty<FastaHeaderReaderResult>())
                {
                    var expectedProtein = expected.Protein;
                    if (expectedProtein == null)
                        continue;

                    var accession = expectedProtein.Accession?.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(accession) &&
                        (accession.StartsWith(upperKeyword, StringComparison.Ordinal) ||
                         upperKeyword.StartsWith(accession, StringComparison.Ordinal)))
                    {
                        return expected;
                    }

                    var name = expectedProtein.Name?.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(name) && name.Contains(upperKeyword))
                        return expected;
                }

                if (!string.IsNullOrEmpty(header) && header.Contains(upperKeyword))
                {
                    var firstResult = test.ExpectedResults?.FirstOrDefault();
                    if (firstResult != null)
                        return firstResult;
                }
            }

            return null;
        }

        private class FastaImporterExpectedData
        {
            public List<FastaImporterExpectedRecord> Records { get; set; } = new List<FastaImporterExpectedRecord>();

            [JsonIgnore]
            public Dictionary<string, FastaImporterExpectedRecord> RecordMap { get; set; } = new Dictionary<string, FastaImporterExpectedRecord>(StringComparer.Ordinal);
        }

        private class FastaImporterExpectedRecord
        {
            public string Header { get; set; }
            public List<FastaImporterExpectedResult> Results { get; set; }
        }

        private class FastaImporterExpectedResult
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string PreferredName { get; set; }
            public string Accession { get; set; }
            public string Gene { get; set; }
            public string Species { get; set; }
            public int SequenceLength { get; set; }
            public bool WellFormed { get; set; }
            public string WebSearchInfo { get; set; }
        }

        /// <summary>
        /// Test the basic parsing, no attempt at protein metadata resolution
        /// </summary>
        [TestMethod]
        public void TestBasicFastaImport()
        {
            List<FastaHeaderParserTest> tests = GetTests(0);
            var expectedData = LoadExpectedData();
            var recorded = expectedData?.RecordMap;
            if (!IsRecordMode && recorded?.Count > 0)
                tests = MergeExpectedRecords(tests, recorded);
            var dbProteins = new List<DbProtein>();
            // ReSharper disable once CollectionNeverQueried.Local
            // ReSharper disable once UnusedVariable
            var dbProteinNames = new List<DbProteinName>(); // Convenient for debugging
            var fastaImporter = new WebEnabledFastaImporter(new WebEnabledFastaImporter.FakeWebSearchProvider());
            int fakeID = 0;
            foreach (var dbProtein in fastaImporter.Import(new StringReader(GetFastaTestText())))
            {
                dbProtein.Id = fakeID++;
                foreach (var name in dbProtein.Names)
                {
                    name.Id = fakeID++;
                }
                dbProteins.Add(dbProtein);
            }
            foreach (var dbProtein in dbProteins)
            {
                // Decode test number in the sequence (doesn't have to make biological sense)
                int testnum = DecodeTestNumberFromSequence(dbProtein.Sequence);
                Assert.AreEqual(dbProtein.Names.Count, tests[testnum].ExpectedResults.Length);
                int n = 0;
                foreach (var name in dbProtein.Names)
                {
                    var actual = new DbProteinName(null, name.GetProteinMetadata());
                    if (tests[testnum].ExpectedResults[n].Wellformed)
                    {
                        Assert.AreEqual(WebEnabledFastaImporter.SEARCHDONE_TAG.ToString(), actual.WebSearchInfo.ToString()); // No search needed
                    }
                    var expected = new DbProteinName(null, tests[testnum].ExpectedResults[n++].Protein);
                    if (tests[testnum].Header.Contains(NEGTEST_NAME))
                    {
                        Assert.AreNotEqual(expected.Name, actual.Name);
                        Assert.AreNotEqual(expected.Description, actual.Description);
                    }
                    else
                    {
                        Assert.AreEqual(expected.Name, actual.Name);
                        if (actual.Description!=null)
                            Assert.AreEqual(expected.Description, actual.Description);
                    }
                }
            }
        }

        // Encode test number in a bogus sequence, where 
        // K=0, L=1 etc so test 0001 is KKKL, test 4003 is NKKM etc
        // and the sequence just repeats that as long as needed eg NKKMNKKMNKKMNK
        // Of course this only works for seqlen >= 4 
        private static void GetFakeFastaSequenceEncodingForTestNumber(StringBuilder fastaLines, FastaHeaderParserTest t, int testnum)
        {
            fastaLines.Append(t.Header);
            var buf = new char[4];
            int val = testnum;
            int i = 0;
            for (var place = 1000; place > 0; place /= 10)
            {
                buf[i++] = (char)('K' + (val / place));  // We just want a sequence of the right length, doesn't have to make biological sense
                val -= (val / place) * place;
            }
            fastaLines.Append("\n");
            for (int mm = t.ExpectedResults[0].SeqLen; mm > 0; mm--)
                fastaLines.Append(buf[i++ % 4]);
            fastaLines.Append("\n");
        }

        public static int DecodeTestNumberFromSequence(string sequence)
        {
            if (string.IsNullOrEmpty(sequence))
                return 0;
            int testnum = 0;
            int i = 0;
            for (var place = 1000; place > 0; place /= 10)
                testnum += (sequence[i++]-'K')*place;
            return testnum;
        }

        public static string GetFastaTestText(int maxEntries = -1)
        {
            var fastaLines = new StringBuilder();
            int testnum = 0;
            var tests = GetTests(1); // Ignore test sets intended to Peptide Group checking
            foreach (var t in tests)
            {
                GetFakeFastaSequenceEncodingForTestNumber(fastaLines, t, testnum++);
                if ((maxEntries >= 0) && (testnum >= maxEntries))
                    break;
            }
            return fastaLines.ToString();
        }

        //
        // Unit test for ProteinSearchInfo.Intersection
        //
        private void TestProteinSearchInfoIntersection()
        {
            var a = new DbProteinName
            {
                Name = "aName",
                Description = "aDesc",
                PreferredName = "aPref",
                Accession = "aAcc",
                Gene = "aGen",
                Species = "aSpec"
            };
            var aa = new DbProteinName(a.Protein, a.GetProteinMetadata());
            var b = new DbProteinName(a.Protein, a.GetProteinMetadata())
            {
                Name = "bName"
            };
            var bb = new DbProteinName(b.Protein, b.GetProteinMetadata())
            {
                Species = "bSpecies"
            };
            var bbb = new DbProteinName(bb.Protein, bb.GetProteinMetadata())
            {
                Gene = "bGene",
                PreferredName = "bPref",
                Accession = "bAcc",
                Description = "bDesc",
            };
            var n = new DbProteinName
            {
                Name = null,
                Description = null,
                PreferredName = null,
                Accession = null,
                Gene = null,
                Species = null
            };
            Assert.AreEqual(a.GetProteinMetadata(), 
                ProteinSearchInfo.Intersection(new List<ProteinSearchInfo> { new ProteinSearchInfo(a, 0) }).GetProteinMetadata());
            Assert.AreEqual(a.GetProteinMetadata(), 
                ProteinSearchInfo.Intersection(new List<ProteinSearchInfo> { new ProteinSearchInfo(a, 0), new ProteinSearchInfo(aa, 0) }).GetProteinMetadata());
            Assert.AreEqual(a.GetProteinMetadata().ChangeName(null),
                ProteinSearchInfo.Intersection(new List<ProteinSearchInfo> { new ProteinSearchInfo(a, 0), new ProteinSearchInfo(b, 0) }).GetProteinMetadata());
            Assert.AreEqual(a.GetProteinMetadata().ChangeName(null).ChangeSpecies(null),
                ProteinSearchInfo.Intersection(new List<ProteinSearchInfo> { new ProteinSearchInfo(a, 0), new ProteinSearchInfo(b, 0), new ProteinSearchInfo(bb, 0) }).GetProteinMetadata());
            Assert.AreNotEqual(a.GetProteinMetadata(),
                ProteinSearchInfo.Intersection(new List<ProteinSearchInfo> { new ProteinSearchInfo(a, 0), new ProteinSearchInfo(b, 0), new ProteinSearchInfo(bb, 0), new ProteinSearchInfo(bbb, 0) }).GetProteinMetadata());
            Assert.AreEqual(n.GetProteinMetadata(),
                ProteinSearchInfo.Intersection(new List<ProteinSearchInfo> { new ProteinSearchInfo(a, 0), new ProteinSearchInfo(b, 0), new ProteinSearchInfo(bb, 0), new ProteinSearchInfo(bbb, 0) }).GetProteinMetadata());
        }

        [Flags]
        private enum DiagnosticMode
        {
            none = 0,
            results = 1,
            requests = 2
        }

        private DiagnosticMode CurrentDiagnosticMode => DiagnosticMode.none;

        private bool CaptureResults => (CurrentDiagnosticMode & DiagnosticMode.results) != 0;
        private bool CaptureRequests => (CurrentDiagnosticMode & DiagnosticMode.requests) != 0;

        protected override bool IsRecordMode => false;

        [TestMethod]
        public void TestFastaImport()
        {
            if (CaptureResults)
                TestContext.EnsureTestResultsDir();

            TestProteinSearchInfoIntersection();

            DoTestFastaImport(false, false);  // Run with simulated web access
            DoTestFastaImport(false, true); // Run with simulated web access, using negative tests
        }

        [TestMethod]
        public void TestFastaImportWeb()
        {
            if (CaptureResults)
                TestContext.EnsureTestResultsDir();

            // Only run this if SkylineTester has enabled web access or web responses are being recorded
            if (AllowInternetAccess || IsRecordMode || CurrentDiagnosticMode != DiagnosticMode.none)
            {
                TestProteinSearchInfoIntersection();
                DoTestFastaImport(true, false); // run with actual web access
                DoTestFastaImport(true, true); // run with actual web access, using negative tests
            }

            CheckTestFlags();
        }
        private void CheckTestFlags()
        {
            CheckRecordMode();
            Assert.AreEqual(DiagnosticMode.none, CurrentDiagnosticMode, "Set DiagnosticMode to none before commit");
        }

        public void DoTestFastaImport(bool useActualWebAccess, bool doNegTests) // call with useActualWebAccess==true from perf test
        {

            var fastaLines = new StringBuilder();
            int testnum = 0;
            var tests = doNegTests ? GetNegativeTests() : GetTests(1); // Avoid test sets intended for checking peptide group handling
            var expectedData = LoadExpectedData();
            if (!IsRecordMode && expectedData?.RecordMap?.Count > 0)
                tests = MergeExpectedRecords(tests, expectedData.RecordMap);
            foreach (var t in tests)
            {
                GetFakeFastaSequenceEncodingForTestNumber(fastaLines, t, testnum++);
            }

            var dbProteins = new List<DbProtein>();
            var originalMetadata = new Dictionary<DbProteinName, ProteinMetadata>();
            var originalSequenceLengths = new Dictionary<DbProteinName, int>();
            WebEnabledFastaImporter fastaImporter = new WebEnabledFastaImporter(new WebEnabledFastaImporter.DelayedWebSearchProvider());
            int fakeID = 0;
            foreach (var dbProtein in fastaImporter.Import(new StringReader(fastaLines.ToString())))
            {
                dbProtein.Id = fakeID++;
                foreach (var name in dbProtein.Names)
                {
                    name.Id = fakeID++;
                    originalMetadata[name] = name.GetProteinMetadata();
                    originalSequenceLengths[name] = dbProtein.Sequence.Length;
                }
                dbProteins.Add(dbProtein);
            }

            IList<ProteinSearchInfo> CreateProteinSearchInfos()
            {
                var proteins = new List<ProteinSearchInfo>();
                foreach (var dbProtein in dbProteins)
                {
                    foreach (var name in dbProtein.Names)
                    {
                        if (originalMetadata.TryGetValue(name, out var metadata))
                            name.ChangeProteinMetadata(metadata);
                        proteins.Add(new ProteinSearchInfo(name, originalSequenceLengths[name]));
                    }
                }
                return proteins;
            }

            if (!useActualWebAccess && !doNegTests)
            {
                var failureModes = new[]
                {
                    LookupTestMode.http404,
                    LookupTestMode.no_network,
                    LookupTestMode.cancellation
                };

                foreach (var mode in failureModes)
                {
                    var proteinsToSearch = CreateProteinSearchInfos();
                    ExecuteLookupScenario(false, tests, proteinsToSearch, mode, expectedData);
                }
            }

            DoActualWebAccess = useActualWebAccess;
            using var scope = new ConditionalHttpRecordingScope(this, !doNegTests);
            var finalProteinsToSearch = CreateProteinSearchInfos();
            ExecuteLookupScenario(useActualWebAccess, tests, finalProteinsToSearch, LookupTestMode.normal,
                expectedData, skipExtraValidation: doNegTests, allowRecordedPlayback: !doNegTests, scope);

            if (IsRecordMode && useActualWebAccess && !doNegTests)
            {
                RecordExpectedData(dbProteins, tests);
                return;
            }

            Assert.AreEqual(tests.Count, dbProteins.Count);

            var errStringE = new StringBuilder();
            var errStringA = new StringBuilder();
            var expectedRecords = expectedData?.RecordMap;
            foreach (var dbProtein in dbProteins)
            {
                // note that fastaImporter doesn't always present proteins in file order, due to 
                // batching webserver lookups - but we can discern the test number by the
                // goofy sequence we created
                testnum = DecodeTestNumberFromSequence(dbProtein.Sequence);
                var expectedItems = GetExpectedMetadata(tests[testnum], expectedRecords);
                Assert.AreEqual(dbProtein.Names.Count, expectedItems.Count);

                int n = 0;
                var errors = new List<Tuple<String, String>>();
                foreach (var name in dbProtein.Names)
                {
                    var expectedItem = expectedItems[n++];
                    var actual = new DbProteinName(null, name.GetProteinMetadata());

                    if (expectedItem.WellFormed)
                        Assert.AreEqual(WebEnabledFastaImporter.SEARCHDONE_TAG.ToString(), actual.WebSearchInfo.ToString());

                    if (expectedItem.SequenceLength > 0)
                        Assert.AreEqual(expectedItem.SequenceLength, dbProtein.Sequence.Length);

                    actual.ClearWebSearchInfo();

                    var expected = new DbProteinName(null, expectedItem.Metadata);
                    expected.ClearWebSearchInfo(); // this is not a comparison we care about

                    if (tests[testnum].Header.Contains(NEGTEST_NAME))
                    {
                        if (Equals(expected.GetProteinMetadata(), actual.GetProteinMetadata()))
                        {
                            errors.Add(new Tuple<string, string>(@"anything but " + expected.GetProteinMetadata(),
                                actual.GetProteinMetadata().ToString()));
                        }
                    }
                    else
                    {
                        if (!Equals(expected.GetProteinMetadata(), actual.GetProteinMetadata()))
                        {
                            errors.Add(new Tuple<string, string>(expected.GetProteinMetadata().ToString(),
                                actual.GetProteinMetadata().ToString()));
                        }
                    }
                }

                foreach (var e in errors)
                {
                    if (!e.Item1.Equals(e.Item2))
                    {
                        errStringE.AppendLine(e.Item1);
                        errStringA.AppendLine(e.Item2);
                    }
                }
            }
            Assert.AreEqual(errStringE.ToString(), errStringA.ToString());
        }


        private enum LookupTestMode
        {
            normal,
            http404,
            no_network,
            cancellation
        }

        private void ExecuteLookupScenario(bool useNetAccess, List<FastaHeaderParserTest> testList,
            IList<ProteinSearchInfo> proteins, LookupTestMode mode, FastaImporterExpectedData expectedData,
            bool skipExtraValidation = false, bool allowRecordedPlayback = true, ConditionalHttpRecordingScope scope = null)
        {
            var captureInteractions = CaptureRequests && mode == LookupTestMode.normal && !skipExtraValidation;
            HttpInteractionRecorder diagnosticsRecorder = null;
            List<HttpInteraction> playbackDiagnostics = null;
            if (captureInteractions)
            {
                if (useNetAccess)
                {
                    diagnosticsRecorder = scope?.Helper == null ? new HttpInteractionRecorder() : null;
                }
                else
                {
                    playbackDiagnostics = new List<HttpInteraction>();
                }
            }

            using var helper = mode switch
            {
                LookupTestMode.http404 => HttpClientTestHelper.SimulateHttp404(),
                LookupTestMode.no_network => HttpClientTestHelper.SimulateNoNetworkInterface(),
                LookupTestMode.cancellation => HttpClientTestHelper.SimulateCancellation(),
                _ => CreateNormalHelper()
            };

            HttpClientTestHelper CreateNormalHelper()
            {
                // Use HttpRecordingScope if provided (always provided for normal tests)
                if (scope != null)
                {
                    return scope.Helper; // May be null for real network access when DoActualWebAccess=true and IsRecordMode=false
                }

                // Fallback for negative tests or other cases where scope is not provided
                if (useNetAccess)
                {
                    var activeRecorder = diagnosticsRecorder;
                    if (activeRecorder != null)
                        return HttpClientTestHelper.BeginRecording(activeRecorder);
                    return null; // use real network access when no recorder is supplied
                }
                
                // Load HTTP interactions from the standardized file for playback
                if (allowRecordedPlayback)
                {
                    var httpInteractions = LoadHttpInteractions();
                    if (httpInteractions != null && httpInteractions.Count > 0)
                    {
                        return HttpClientTestHelper.PlaybackFromInteractions(httpInteractions, playbackDiagnostics);
                    }
                }

                return BeginLegacyPlayback(testList);
            }


            var initialSnapshot = CaptureResults
                ? proteins.Select((p, i) => CreateDiagnosticEntry(p, i, includeHistory: false)).ToList()
                : null;

            // Use FastWebSearchProvider for playback to skip politeness delays
            // Check if we're using playback (scope.Helper is not null and not recording)
            bool useFastProvider = !useNetAccess && allowRecordedPlayback && scope?.Helper != null;
            IList<ProteinSearchInfo> results = RunLookup(proteins, mode, useFastProvider);

            if (!skipExtraValidation)
            {
                ValidateLookupResult(mode, proteins, results, helper, initialSnapshot);
            }

            if (captureInteractions)
            {
                IReadOnlyCollection<HttpInteraction> interactions =
                    diagnosticsRecorder?.Interactions ??
                    playbackDiagnostics;
                if (interactions != null && interactions.Count > 0)
                    WriteInteractionDiagnostics(mode, interactions);
            }
        }

        private void WriteInteractionDiagnostics(LookupTestMode mode, IEnumerable<HttpInteraction> interactions)
        {
            var path = TestContext.GetTestResultsPath($@"Interactions_{mode}.json");
            var indexedInteractions = interactions?
                .Select((interaction, index) => new
                {
                    Index = index,
                    interaction.Url,
                    interaction.Method,
                    interaction.StatusCode,
                    interaction.ContentType,
                    interaction.ResponseBody,
                    interaction.ExceptionType,
                    interaction.ExceptionMessage,
                    interaction.FailureType
                }).ToList();
            var json = JsonConvert.SerializeObject(indexedInteractions, Formatting.Indented);
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }

        private void ValidateLookupResult(LookupTestMode mode,
            IList<ProteinSearchInfo> proteins, IList<ProteinSearchInfo> results,
            HttpClientTestHelper helper, IList<object> initialSnapshot)
        {
            const int noSearchNeededCount = 7;  // Seven proteins will return successfully without any web access
            const int failOnSearch = 8; // 3 failures expected, and 5 just can't be resolved live on web
            int searchCount = proteins.Count - noSearchNeededCount;

            Assert.AreNotEqual(0, results.Count);

            if (CaptureResults)
            {
                WriteDiagnostics(mode, initialSnapshot, proteins, results, helper);
            }

            var failureException = proteins.FirstOrDefault(p => p.FailureException != null)?.FailureException;
            if (mode == LookupTestMode.normal)
            {
                Assert.IsNull(failureException, $"Unexpected exception thrown: {failureException}");
                // We should have results for everything
                Assert.AreEqual(proteins.Count, results.Count);
                // There should still be the same number of proteins that were resolved without a web search
                Assert.AreEqual(noSearchNeededCount, proteins.Count(p =>
                    p.Status == ProteinSearchInfo.SearchStatus.unsearched && p.GetProteinMetadata().NeedsSearch() == false));
                var failureProteins = proteins.Where(p => p.Status == ProteinSearchInfo.SearchStatus.failure).ToList();
                Assert.AreEqual(failOnSearch, failureProteins.Count);
                Assert.AreEqual(failOnSearch - 1,
                    failureProteins.Count(p => p.FailureReason == WebSearchFailureReason.no_response));
                Assert.AreEqual(1,
                    failureProteins.Count(p => p.FailureReason == WebSearchFailureReason.sequence_mismatch));
                Assert.AreEqual(searchCount - failOnSearch, results.Count(p =>
                    p.Status == ProteinSearchInfo.SearchStatus.success));
                return;
            }

            Assert.IsNotNull(failureException);
            if (mode == LookupTestMode.http404)
            {
                // We should have results for everything
                Assert.AreEqual(proteins.Count, results.Count);
                // There should still be the same number of proteins that were resolved without a web search
                Assert.AreEqual(noSearchNeededCount, proteins.Count(p =>
                    p.Status == ProteinSearchInfo.SearchStatus.unsearched && p.GetProteinMetadata().NeedsSearch() == false));

                // All searched proteins should have failed
                var failedProteins = proteins.Where(p => p.Status == ProteinSearchInfo.SearchStatus.failure).ToList();
                Assert.AreEqual(searchCount, failedProteins.Count);

                // The failure reason should always be a NetworkRequestException with HttpStatusCode.NotFound
                Assert.IsTrue(failedProteins.All(p => p.FailureReason == WebSearchFailureReason.http_not_found));
                Assert.IsTrue(failedProteins.All(p => p.FailureException is NetworkRequestException));

                // Check that the failing searches exception message matches by prefix, because it will contain the URI requested
                var expectedPrefix = GetExpectedMessagePrefix(helper);
                var expectedSuffix = GetExpectedMessageSuffix(helper);
                Assert.IsTrue(expectedPrefix.Length != 0 || expectedSuffix.Length != 0);
                foreach (var failedProtein in failedProteins)
                {
                    if (failedProtein.FailureDetail != null)
                    {
                        StringAssert.StartsWith(failedProtein.FailureDetail, expectedPrefix);
                        StringAssert.EndsWith(failedProtein.FailureDetail, expectedSuffix);
                    }
                }
            }
            else
            {
                // Only the proteins that do not need a search should have completed
                Assert.AreEqual(noSearchNeededCount, results.Count);
                // Everything else should still be waiting to be searched
                Assert.AreEqual(searchCount, proteins.Count(p => p.GetProteinMetadata().NeedsSearch() &&
                                                                 p.Status == ProteinSearchInfo.SearchStatus.unsearched));
                // Only one protein should have a failure reason recorded
                Assert.AreEqual(1, proteins.Count(p => p.FailureReason != WebSearchFailureReason.none));
                // No network and cancellation are URI-free messages. So, exact match is expected
                Assert.AreEqual(helper.GetExpectedMessage(), failureException.Message);
            }
        }

        private static string GetExpectedMessagePrefix(HttpClientTestHelper helper)
        {
            const string urlText = "https://test.com/";
            string expectedMessage = helper.GetExpectedMessage(new Uri(urlText));
            int iMatch = expectedMessage.IndexOf(urlText, StringComparison.Ordinal);
            if (iMatch == -1)
                return expectedMessage;
            return expectedMessage.Substring(0, iMatch);
        }

        private static string GetExpectedMessageSuffix(HttpClientTestHelper helper)
        {
            const string urlText = "https://test.com/";
            string expectedMessage = helper.GetExpectedMessage(new Uri(urlText));
            int iMatch = expectedMessage.LastIndexOf(urlText, StringComparison.Ordinal);
            if (iMatch == -1)
                return expectedMessage;
            return expectedMessage.Substring(iMatch + urlText.Length);
        }

        private void WriteDiagnostics(LookupTestMode mode, IList<ProteinSearchInfo> proteins,
            IList<ProteinSearchInfo> results, HttpClientTestHelper helper)
        {
            WriteDiagnostics(mode, null, proteins, results, helper);
        }

        private void WriteDiagnostics(LookupTestMode mode, IList<object> initialSnapshot,
            IList<ProteinSearchInfo> proteins, IList<ProteinSearchInfo> results, HttpClientTestHelper helper)
        {
            var diagnosticsPath = TestContext.GetTestResultsPath($@"Validate_{mode}.json");
            var diagnostics = new
            {
                Mode = mode.ToString(),
                TimestampUtc = DateTime.UtcNow,
                HelperExpectedMessage = helper?.GetExpectedMessage(),
                InitialProteins = initialSnapshot,
                Proteins = proteins.Select((p, i) => CreateDiagnosticEntry(p, i, includeHistory: true)).ToList(),
                Results = results.Select((p, i) => CreateDiagnosticEntry(p, i, includeHistory: true)).ToList()
            };
            var json = JsonConvert.SerializeObject(diagnostics, Formatting.Indented);
            File.WriteAllText(diagnosticsPath, json, new UTF8Encoding(false));
        }

        private static object CreateDiagnosticEntry(ProteinSearchInfo info, int index, bool includeHistory)
        {
            var metadata = info.GetProteinMetadata();
            var webSearchInfo = metadata?.WebSearchInfo;
            return new
            {
                Index = index,
                Status = info.Status.ToString(),
                FailureReason = info.FailureReason.ToString(),
                info.FailureDetail,
                FailureException = info.FailureException?.GetType().FullName,
                SearchState = DetermineSearchState(info, metadata),
                metadata?.Name,
                metadata?.Description,
                metadata?.PreferredName,
                metadata?.Accession,
                metadata?.Gene,
                metadata?.Species,
                NeedsSearch = metadata?.NeedsSearch() ?? false,
                WebSearchInfo = webSearchInfo?.ToString(),
                SearchUrlHistory = includeHistory ? info.SearchUrlHistory : Array.Empty<string>()
            };
        }

        private static string DetermineSearchState(ProteinSearchInfo info, ProteinMetadata metadata)
        {
            if (info.Status == ProteinSearchInfo.SearchStatus.failure)
                return $"failure:{info.FailureReason}";

            if (metadata != null && !metadata.NeedsSearch())
            {
                if (info.Status == ProteinSearchInfo.SearchStatus.success)
                    return "success";
                if (info.Status == ProteinSearchInfo.SearchStatus.unsearched)
                    return "completed_without_web";
                return $"completed:{info.Status}";
            }

            return metadata != null && metadata.NeedsSearch()
                ? "pending"
                : info.Status.ToString();
        }

        private class QuickFailWebSearchProvider : WebEnabledFastaImporter.WebSearchProvider
        {
            public override int WebRetryCount()
            {
                return 1; // try only once
            }
        }

        /// <summary>
        /// WebSearchProvider optimized for playback scenarios (no politeness delays)
        /// Use this when using HttpClientTestHelper.PlaybackFromInteractions to speed up tests
        /// </summary>
        private class FastWebSearchProvider : WebEnabledFastaImporter.WebSearchProvider
        {
            public override bool IsPolite
            {
                get { return false; } // Skip Thread.Sleep delays for in-memory playback
            }
        }

        private IList<ProteinSearchInfo> RunLookup(IList<ProteinSearchInfo> proteins, LookupTestMode mode, bool useFastProvider = false)
        {
            WebEnabledFastaImporter.WebSearchProvider provider = null;
            if (mode != LookupTestMode.normal)
            {
                provider = new QuickFailWebSearchProvider();
            }
            else if (useFastProvider)
            {
                provider = new FastWebSearchProvider(); // Skip politeness delays during playback
            }
            // CONSIDER(brendanx): Test progress monitor cancellation by linking a CancellationToken to HttpClientTestBehavior with a ResponseFactory function that triggers cancellation.
            var progressMonitor = new SilentProgressMonitor();
            var importer = new WebEnabledFastaImporter(provider);
            var results = importer.DoWebserviceLookup(proteins, progressMonitor, false).ToList();
            return results;
        }

        /// <summary>
        /// Wrapper around HttpRecordingScope that conditionally creates the scope based on test requirements.
        /// For negative tests, the scope is not created (no-op). For normal tests, it always creates the scope.
        /// </summary>
        private class ConditionalHttpRecordingScope : IDisposable
        {
            private readonly HttpRecordingScope _scope;

            public HttpClientTestHelper Helper => _scope?.Helper;

            public ConditionalHttpRecordingScope(FastaImporterTest test, bool createScope)
            {
                if (createScope)
                {
                    _scope = test.GetHttpRecordingScope();
                }
            }

            public void Dispose()
            {
                _scope?.Dispose();
            }
        }
    }
}
