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
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
    public class FastaImporterTest : AbstractUnitTest
    {
        private const string NEGTEST_NAME = @"Q9090909090"; // For use in negative test
        private const string NEGTEST_DESCRIPTION = @"this is meant to fail"; // For use in negative test
        private const string novalue = null;


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

        /// <summary>
        /// for testing without requiring web access - returns the expected web responses for the tests herein.
        /// </summary>
        public class PlaybackProvider : WebEnabledFastaImporter.WebSearchProvider
        {
            private readonly List<FastaHeaderParserTest> _tests; // We mine this for mimicry of web response

            public override bool IsPolite
            {
                get { return false; }
            }

            public PlaybackProvider(List<FastaHeaderParserTest> tests)
            {
                _tests = tests;
            }

            public PlaybackProvider()
            {
                _tests = GetTests(0); // The default set of fasta header tests
            }

            public override XmlTextReader GetXmlTextReader(string url)
            {
                // should look something like "http://eutils.ncbi.nlm.nih.gov/entrez/eutils/efetch.fcgi?db=protein&id=15834432,15834432&tool=%22skyline%22&email=%22johnqdeveloper@proteinms.net%22&retmode=xml"
                var searches = url.Split('=')[2].Split('&')[0].Split(',');
                var sb = new StringBuilder();
                if (url.Contains(".gov")) // watch for deliberately malformed url in tests Not L10N
                {
                    if (url.Contains("rettype=docsum"))
                    {
                        sb.Append("<?xml version=\"1.0\"?>\n<eSummaryResult>\n"); // Not L10N
                        foreach (var search in searches)
                        {
                            var test = FindTest(search);
                            if ((null != test) && !String.IsNullOrEmpty(test.Protein.Accession))
                            {
                                var intermediateSearchTerm = test.GetIntermediateSearchterm(search);
                                sb.AppendFormat("<Id>{0}</Id>",search);
                                sb.AppendFormat("<DocSum> ");
                                sb.AppendFormat("<Item Name=\"Caption\" Type=\"String\">{0}</Item>",
                                    intermediateSearchTerm);
                                sb.AppendFormat("<Item Name=\"ReplacedBy\" Type=\"String\">{0}</Item>",
                                    intermediateSearchTerm);
                                sb.AppendFormat("<Item Name=\"Length\" Type=\"Integer\">{0}</Item>",
                                    test.SeqLen);
                                sb.AppendFormat("</DocSum>\n"); // Not L10N
                            }
                        }
                        sb.AppendFormat("</eSummaryResult>\n"); // Not L10N
                    }
                    else
                    {
                        sb.Append("<?xml version=\"1.0\"?>\n<GBSet>\n"); // Not L10N
                        foreach (var search in searches)
                        {
                            var test = FindTest(search);
                            if ((null != test) && !String.IsNullOrEmpty(test.Protein.Accession))
                            {
                                sb.AppendFormat("<GBSeq> ");
                                sb.AppendFormat("<GBSeq_length>{0}</GBSeq_length> ",
                                    test.SeqLen);
                                if (test.Protein.PreferredName != null)
                                    sb.AppendFormat("<GBSeq_locus>{0}</GBSeq_locus>", test.Protein.PreferredName);
                                        // Not L10N
                                if (test.Protein.Description != null)
                                    sb.AppendFormat(" <GBSeq_definition>{0}</GBSeq_definition> ",
                                        test.Protein.Description); // Not L10N
                                if (test.Protein.Accession != null)
                                    sb.AppendFormat("<GBSeq_primary-accession>{0}</GBSeq_primary-accession>",
                                        test.Protein.Accession); // Not L10N 
                                if (test.Protein.Species != null)
                                    sb.AppendFormat("<GBSeq_organism>{0}</GBSeq_organism> ", test.Protein.Species);
                                        // Not L10N
                                if (test.Protein.Gene != null)
                                    sb.AppendFormat(
                                        "<GBQualifier> <GBQualifier_name>gene</GBQualifier_name> <GBQualifier_value>{0}</GBQualifier_value> </GBQualifier> ",
                                        test.Protein.Gene); // Not L10N
                                sb.AppendFormat("</GBSeq>\n"); // Not L10N
                            }
                        }
                        sb.Append("</GBSet>"); // Not L10N
                    }
                    return new XmlTextReader(MakeStream(sb));
                }
                else
                {
                    throw new WebException("error 404"); // mimic bad url behavior Not L10N
                }
            }

            public override Stream GetWebResponseStream(string url, int timeout)
            {
                // should look something like "https://www.uniprot.xyzpdq/uniprot/?query=(P04638+OR+SGD_S000005768+OR+CAB02319.1)&format=tab&columns=id,genes,organism,length,entry name,protein names,reviewed"
                var searches = url.Split('(')[1].Split(')')[0].Split('+').Where(s => !Equals(s, "OR")).ToArray();
                var sb = new StringBuilder();
                if (url.Contains(".org")) // watch for deliberately malformed url in tests Not L10N
                {
                    sb.Append("Entry\tEntry name\tProtein names\tGene names\tOrganism\tLength\tStatus\n");
                    foreach (var search in searches)
                    {
                        var test = FindTest(search);
                        if ((null != test) && !String.IsNullOrEmpty(test.Protein.Accession))
                        {
                            sb.AppendFormat(
                                "{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\n", // Not L10N
                                test.Protein.Accession , test.Protein.PreferredName ?? String.Empty,
                                test.Protein.Description ?? String.Empty, test.Protein.Gene ?? String.Empty, test.Protein.Species ?? String.Empty,
                                test.SeqLen, "reviewed");
                        }
                    }
                    return MakeStream(sb);
                }
                else
                {
                    throw new WebException("error 404"); //  mimic bad url behavior Not L10N
                }
            }

            private Stream MakeStream(StringBuilder sb)
            {
                MemoryStream stream = new MemoryStream();
                StreamWriter writer = new StreamWriter(stream);
                writer.Write(sb.ToString());
                writer.Flush();
                stream.Position = 0;
                return stream;
            }

            private FastaHeaderReaderResult FindTest(string keyword)
            {
                if (!string.IsNullOrEmpty(keyword))
                {
                    foreach (var test in _tests)
                    {
                        foreach (var expectedResult in test.ExpectedResults)
                        {
                            if ((!String.IsNullOrEmpty(expectedResult.Protein.Accession) &&
                                    (expectedResult.Protein.Accession.ToUpperInvariant().StartsWith(keyword.ToUpperInvariant()) ||
                                    keyword.ToUpperInvariant().StartsWith(expectedResult.Protein.Accession.ToUpperInvariant()))) ||
                                (!String.IsNullOrEmpty(expectedResult.Protein.Name) &&
                                (expectedResult.Protein.Name.ToUpperInvariant().Contains(keyword.ToUpperInvariant()))))
                            {
                                return expectedResult;
                            }
                        }
                    }
                    // no joy yet - see if its buried in name or description, as in our GI->Uniprot scenario
                    keyword = keyword.Split('.')[0]; // drop .n from xp_mmmmmmm.n
                    foreach (var test in _tests)
                    {
                        foreach (var expectedResult in test.ExpectedResults)
                        {
                            if (Equals(keyword,FastaHeaderReaderResult.FindTerm(expectedResult.Protein, @"ref\|")) ||
                                Equals(keyword,FastaHeaderReaderResult.FindTerm(expectedResult.Protein, @"gi\|")))
                            {
                                return expectedResult;
                            }
                        }
                    }
                    // Possibly SGDID
                    if (keyword.StartsWith(WebEnabledFastaImporter.UNIPROTKB_PREFIX_SGD))
                    {
                        foreach (var test in _tests)
                        {
                            foreach (var expectedResult in test.ExpectedResults)
                            {
                                if (expectedResult.Protein.Description.Contains(keyword.Replace(WebEnabledFastaImporter.UNIPROTKB_PREFIX_SGD, "SGD:S")) ||
                                    expectedResult.Protein.Description.Contains(keyword.Replace(WebEnabledFastaImporter.UNIPROTKB_PREFIX_SGD, "SGDID:S")))
                                {
                                    return expectedResult;
                                }
                            }
                        }
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// like the actual  WebEnabledFastaImporter.WebSearchProvider,
        /// but intentionally generates bad URLs to test error handling
        /// </summary>
        public class DoomedWebSearchProvider : WebEnabledFastaImporter.WebSearchProvider
        {
            public override bool IsPolite
            {
                get { return false; }
            }

            public override int GetTimeoutMsec(int searchTermCount)
            {
                return 10 * (10 + (searchTermCount / 5));
            }

            public override int WebRetryCount()
            {
                return 1; // once is plenty
            }

            public override string ConstructEntrezURL(IEnumerable<string> searches, bool summary)
            {
                var result = base.ConstructEntrezURL(searches, summary).Replace("nlm.nih.gov", "nlm.nih.gummint"); // provoke a failure for test purposes Not L10N
                return result;
            }

            public override string ConstructUniprotURL(IEnumerable<string> searches)
            {
                var result = base.ConstructUniprotURL(searches).Replace("uniprot.org", "uniprot.xyzpdq"); // provoke a failure for test purposes Not L10N
                return result;
            }

        }

        /// <summary>
        /// Test the basic parsing, no attempt at protein metadata resolution
        /// </summary>
        [TestMethod]
        public void TestBasicFastaImport()
        {
            List<FastaHeaderParserTest> tests = GetTests(0);
            var dbProteins = new List<DbProtein>();
            // ReSharper disable once CollectionNeverQueried.Local
            // ReSharper disable once UnusedVariable
            var dbProteinNames = new List<DbProteinName>(); // Convenient for debugging
            WebEnabledFastaImporter fastaImporter = new WebEnabledFastaImporter(new WebEnabledFastaImporter.FakeWebSearchProvider());
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


        [TestMethod]
        public void TestFastaImport()
        {
            TestProteinSearchInfoIntersection();
            DoTestFastaImport(false, false);  // Run with simulated web access
            DoTestFastaImport(false, true); // Run with simulated web access, using negative tests
        }

        [TestMethod]
        public void WebTestFastaImport()
        {
            if (AllowInternetAccess) // Only run this if SkylineTester has enabled web access
            {
                TestProteinSearchInfoIntersection();
                DoTestFastaImport(true, false); // run with actual web access
                DoTestFastaImport(true, true); // run with actual web access, using negative tests
            }
        }


        public void DoTestFastaImport(bool useActualWebAccess, bool doNegTests) // call with useActualWebAccess==true from perf test
        {

            var fastaLines = new StringBuilder();
            int testnum = 0;
            var tests = doNegTests ? GetNegativeTests() : GetTests(1); // Avoid test sets intended for checking peptide group handling
            foreach (var t in tests)
            {
                GetFakeFastaSequenceEncodingForTestNumber(fastaLines, t, testnum++);
            }

            var dbProteins = new List<DbProtein>();
            var proteinsToSearch = new List<ProteinSearchInfo>();
            WebEnabledFastaImporter fastaImporter = new WebEnabledFastaImporter(new WebEnabledFastaImporter.DelayedWebSearchProvider());
            int fakeID = 0;
            foreach (var dbProtein in fastaImporter.Import(new StringReader(fastaLines.ToString())))
            {
                dbProtein.Id = fakeID++;
                foreach (var name in dbProtein.Names)
                {
                    name.Id = fakeID++;
                    proteinsToSearch.Add(new ProteinSearchInfo(name, dbProtein.Sequence.Length));
                }
                dbProteins.Add(dbProtein);
            }

            

            for (int test = 2; test-- > 0;)
            {
                if (test == 1) // first, test poor internet access
                    fastaImporter = new WebEnabledFastaImporter(new DoomedWebSearchProvider()); // intentionally messes up the URLs
                else  // then test web search code - either live in a perf test, or using playback object
                    fastaImporter = new WebEnabledFastaImporter(useActualWebAccess? new WebEnabledFastaImporter.WebSearchProvider() : new PlaybackProvider(tests));
                var results = fastaImporter.DoWebserviceLookup(proteinsToSearch, null, false).ToList(); // No progress monitor, and don't be polite get it all at once
                foreach (var result in results)
                {
                    if (result != null)
                    {
                        bool searchCompleted =
                            String.IsNullOrEmpty(result.GetProteinMetadata().GetPendingSearchTerm());
                        bool searchDelayed = (test==1); // first go round we simulate bad web access
                        if (!result.ProteinDbInfo.WebSearchInfo.ToString().StartsWith(WebEnabledFastaImporter.SEARCHDONE_TAG.ToString(CultureInfo.InvariantCulture))) // the 'no search possible' case
                            Assert.IsTrue(searchCompleted == !searchDelayed);
                    }
                }
            }
            Assert.AreEqual(tests.Count, dbProteins.Count);


            var errStringE = String.Empty;
            var errStringA = String.Empty;
            foreach (var dbProtein in dbProteins)
            {
                // note that fastaImporter doesn't always present proteins in file order, due to 
                // batching webserver lookups - but we can discern the test number by the
                // goofy sequence we created
                testnum = DecodeTestNumberFromSequence(dbProtein.Sequence);
                Assert.AreEqual(dbProtein.Names.Count, tests[testnum].ExpectedResults.Length);
                int n = 0;
                var errors = new List<Tuple<String, String>>();
                foreach (var name in dbProtein.Names)
                {
                    var actual = new DbProteinName(null, name.GetProteinMetadata());
                    var expected = new DbProteinName(null, tests[testnum].ExpectedResults[n++].Protein);

                    actual.ClearWebSearchInfo();
                    expected.ClearWebSearchInfo(); // this is not a comparison we care about

                    if (tests[testnum].Header.Contains(NEGTEST_NAME))
                    {
                        if (Equals(expected.GetProteinMetadata(), actual.GetProteinMetadata()))
                            // If we are working properly, this protein metadata should not be populated due to previous streak of failures.
                            errors.Add(new Tuple<string, string>(@"anything but "+expected.GetProteinMetadata(),
                                actual.GetProteinMetadata().ToString()));
                    }
                    else
                    {
                        if (!Equals(expected.GetProteinMetadata(), actual.GetProteinMetadata()))
                            errors.Add(new Tuple<string, string>(expected.GetProteinMetadata().ToString(),
                                actual.GetProteinMetadata().ToString()));
                    }
                }
                foreach (var e in errors)
                {
                    if (!e.Item1.Equals(e.Item2))
                    {
                        errStringE += "\n" + e.Item1;
                        errStringA += "\n" + e.Item2;
                    }
                }
            }
            Assert.AreEqual(errStringE + "\n", errStringA + "\n");
        }
    }
}
