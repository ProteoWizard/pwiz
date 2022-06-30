/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Extensive changes in 2014 by Brian Pratt <bspratt .at. proteinms.net>,
 * in case of trouble bother him before you bother Nick.
 * 
 * Copyright 2009-2014 University of Washington - Seattle, WA
 * 
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
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.API;
using pwiz.ProteomeDatabase.DataModel;
using pwiz.ProteomeDatabase.Properties;

namespace pwiz.ProteomeDatabase.Fasta
{

    /// <summary>
    /// Helper class for pulling protein metadata from the web
    /// Note how we carry sequence length around - this is useful in sifting out search results
    /// If we submit search batches with each protein having a unique sequence length, it's
    /// much easier to map the results back to the queries by matching reported sequence lengths.
    /// </summary>
    public class ProteinSearchInfo
    {
        public ProteinSearchInfo(DbProteinName dbProteinDbInfo, int sequenceLength)
        {
            ProteinDbInfo = dbProteinDbInfo;
            SeqLength = sequenceLength;
            Status = SearchStatus.unsearched;
        }

        public ProteinSearchInfo()
        {
            ProteinDbInfo = new DbProteinName();
            SeqLength = 0;
            Status = SearchStatus.unsearched;
        }

        public enum SearchStatus { unsearched, success, failure };

        public DbProteinName ProteinDbInfo { get; set; }
        public int SeqLength { get; set; }  // Useful for disambiguation of multiple responses
        public string ReviewStatus { get; set; } // Status reviewed or unreviewed in Uniprot
        public bool IsReviewed => string.Equals(ReviewStatus,@"reviewed", StringComparison.OrdinalIgnoreCase); // Uniprot reviewed status
        public SearchStatus Status { get; set; }

        public void NoteSearchFailure()
        {
            if (Status == SearchStatus.unsearched)
                Status = SearchStatus.failure;
        }

        public ProteinMetadata GetProteinMetadata()
        {
            return ProteinDbInfo.GetProteinMetadata();
        }

        public void ChangeProteinMetadata(ProteinMetadata other)
        {
            ProteinDbInfo.ChangeProteinMetadata(other);
        }

        public void SetWebSearchCompleted()
        {
            ProteinDbInfo.SetWebSearchCompleted();
        }

        public void SetWebSearchTerm(WebSearchTerm search)
        {
            ProteinDbInfo.SetWebSearchTerm(search);
        }

        public string Species { get { return ProteinDbInfo.Species; } set { ProteinDbInfo.Species = value; } }
        public string PreferredName { get { return ProteinDbInfo.PreferredName; } set { ProteinDbInfo.PreferredName = value; } }
        public string Accession { get { return ProteinDbInfo.Accession; } set { ProteinDbInfo.Accession = value; } }
        public string Description { get { return ProteinDbInfo.Description; } set { ProteinDbInfo.Description = value; } }
        public string Gene { get { return ProteinDbInfo.Gene; } set { ProteinDbInfo.Gene = value; } }
        public DbProtein Protein { get { return ProteinDbInfo.Protein; } set { ProteinDbInfo.Protein = value; } }

        //
        // Return a ProteinSearchInfo whose members are the same in every member of the list, or null when list members disagree
        //
        public static ProteinSearchInfo Intersection(IEnumerable<ProteinSearchInfo> list)
        {
            if (list == null)
                return null;
            var proteinSearchInfos = list as ProteinSearchInfo[] ?? list.ToArray();
            if (!proteinSearchInfos.Any())
                return null;
            var result = new ProteinSearchInfo(new DbProteinName(proteinSearchInfos[0].ProteinDbInfo.Protein, proteinSearchInfos[0].ProteinDbInfo.GetProteinMetadata().ClearWebSearchInfo()),0);
            var rdb = result.ProteinDbInfo;
            foreach (var p in proteinSearchInfos.Skip(1))
            {
                // Make sure all string properties in list agree, nulling out those that don't
                var pdb = p.ProteinDbInfo;
                foreach (var resultProperty in rdb.GetType().GetProperties().Where(prop => prop.PropertyType == typeof (string)))
                {
                    var pdbProperty = pdb.GetType().GetProperties().First(pprop => Equals(pprop.Name, resultProperty.Name));
                    if (!Equals(resultProperty.GetValue(rdb, null), pdbProperty.GetValue(pdb, null)))
                    {
                        resultProperty.SetValue(rdb, null);
                    }
                }
            }
            return result;
        }
    }

    /// <summary>
    /// Class for reading FASTA-formatted protein sequence file
    /// Note: proteins are not necessarily returned in the order they appear in the input,
    /// due to the need to batch up webservice requests when seeking extra data
    /// 
    /// Some notes on using webservices:
    /// Batching up and moderating request rate is important, or user could get booted.
    /// Groovy XML-based services all seem to want to return the sequence as well as the metadata,
    /// so we stick with relatively simple TSV-formatted http requests.
    /// 
    /// </summary>
    public class WebEnabledFastaImporter
    {
        private DbProtein _curProtein;
        private StringBuilder _curSequence;
        private readonly WebSearchProvider _webSearchProvider;
        private readonly List<RegexPair> _regexFasta;
        private IpiToUniprotMap _ipiMapper;
        private readonly Dictionary<char, int> _batchsize; // Managing for ambiguous responses: start with pessimistic batch size, then grow it with success.
        private readonly Dictionary<char, int> _successCountAtThisBatchsize;
        private readonly Dictionary<char, int> _maxBatchSize;
        private bool? _hasWebAccess;


        /// <summary>
        /// Class for reading in a Fasta file, possibly noting need for later webservice access to get extra information like accession, gene etc
        /// <param name="webSearchProvider">Object for web access, tests can swap this out for a playback class and avoid actual web access</param>
        /// </summary>
        public WebEnabledFastaImporter(WebSearchProvider webSearchProvider = null)
        {
            _webSearchProvider = webSearchProvider ?? new WebSearchProvider();
            _regexFasta = new List<RegexPair>();
            _hasWebAccess = null;  // Unknown as of yet
            _batchsize = new Dictionary<char, int>
            {
                {GENINFO_TAG, 1},  // search on Entrez, but don't mix with non-GI searches
                {ENTREZ_TAG, 1},
                {UNIPROTKB_TAG, 1}
            };
            const int ENTREZ_BATCHSIZE = 100; // they'd like 500, but it's really boggy at that size
            const int UNIPROTKB_BATCHSIZE = 200;  // Enforcing sequence length uniqueness helps efficiency, we can make this pretty large (was 400 before Sept 2019, but now they just close connection instead of issuing URL_TOO_LONG so start smaller)
            _maxBatchSize = new Dictionary<char, int>
            {
                {GENINFO_TAG, ENTREZ_BATCHSIZE},
                {ENTREZ_TAG, ENTREZ_BATCHSIZE},
                {UNIPROTKB_TAG, UNIPROTKB_BATCHSIZE}
            };
            _successCountAtThisBatchsize = new Dictionary<char, int>
            {
                {GENINFO_TAG, 0},
                {ENTREZ_TAG, 0},
                {UNIPROTKB_TAG, 0}
            };

            foreach (var regex in GetStandardRegexPairs())
            {
                // CONSIDER(bspratt): may need to make regex collection user extensible - and when we do, guard against
                // gibberish, though it's likely that this will always be general enough when combined with Entrez
                // and Uniprot search that we'll never have to expose users to regex at all
                var pair = new RegexPair
                {
                    RegexPattern =
                        new Regex(regex.Regex,
                            RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant |
                            RegexOptions.Compiled),
                    RegexReplacement = regex.WebSearchtermFormat
                };
                _regexFasta.Add(pair);
            }

        }

        public bool IsAccessFaked
        {
            get { return _webSearchProvider is FakeWebSearchProvider; }
        }

        /// <summary>
        /// Quick, cheap check for internet access (uniprot access, specifically)
        /// </summary>
        /// <returns>false if internet isn't available for any reason</returns>
        public bool HasWebAccess()
        {
            if (!_hasWebAccess.HasValue)
            {
                // First time anyone has asked - try a simple search to see if we have access
                var prot = ParseProteinLine(KNOWNGOOD_UNIPROT_SEARCH_TARGET);
                var protname = new ProteinSearchInfo(new DbProteinName(prot, new ProteinMetadata(KNOWNGOOD_UNIPROT_SEARCH_TARGET, string.Empty, null, null, null, null,
                    UNIPROTKB_TAG + KNOWNGOOD_UNIPROT_SEARCH_TARGET)), KNOWNGOOD_UNIPROT_SEARCH_TARGET_SEQLEN);
                _hasWebAccess = DoWebserviceLookup(new []{protname}, null, true).Any();
            }
            return _hasWebAccess.Value;
        }

        public IEnumerable<DbProtein> Import(TextReader reader)
        {
            string line;
            int lineNumber = 0;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (line.StartsWith(@">"))
                {
                    DbProtein protein = EndProtein();
                    if (protein != null)
                    {
                        yield return protein;
                    }
                    _curProtein = ParseProteinLine(line);
                    _curSequence = new StringBuilder();
                }
                else if (_curSequence == null)
                {
                    // break;
                }
                else
                {
                    string sequenceLine = ParseSequenceLine(line);
                    ValidateProteinSequence(sequenceLine, lineNumber);
                    _curSequence.Append(sequenceLine);
                }
            }
            DbProtein lastProtein = EndProtein();
            if (lastProtein != null)
            {
                yield return lastProtein;
            }
        }

        private DbProtein EndProtein()
        {
            if (_curProtein == null)
            {
                return null;
            }
            _curProtein.Sequence = _curSequence.ToString();
            _curSequence = new StringBuilder();
            DbProtein result = _curProtein;
            _curProtein = null;
            return result;
        }

        private DbProtein ParseProteinLine(String line)
        {
            String[] alternatives = line.Substring(1).Split((char) 1);
            var protein = new DbProtein();
            var proteinMetadata = ParseProteinMetaData(alternatives[0]);
            var dbName = new DbProteinName(null,proteinMetadata);
            protein.Names.Add(dbName);
            for (int i = 1; i < alternatives.Length; i++)
            {
                if (alternatives[i].Length > 0)
                {
                    var altProteinMetadata = ParseProteinMetaData(alternatives[i]);
                    var altName = new DbProteinName(protein,altProteinMetadata);
                    protein.Names.Add(altName);
                }
            }
            return protein;
        }

        private class RegexPair
        {
            public Regex RegexPattern { get; set; }
            public string RegexReplacement { get; set; }
        }

        public ProteinMetadata ParseProteinMetaData(ProteinMetadata proteinMetadata)
        {
            if (proteinMetadata.Name == null)
                return null;
            // watch for inputs like name="Library Peptides" which clearly isn't an actual protein name
            if (proteinMetadata.Name.Contains(@" "))
                return null;
            return ParseProteinMetaData(proteinMetadata.Name + @" " + proteinMetadata.Description);
        }

        /// <summary>
        /// Uses the known list of regexes to parse lineIn, keeping the
        /// result that fills in the most metdata.  In the event of a tie,
        /// first result wins - so regex list order matters.
        /// Populates the WebSearchInfo field but does not perform 
        /// the actual search - that's done elsewhere.
        /// </summary>
        /// <param name="lineIn">the text to be parsed</param>
        public ProteinMetadata ParseProteinMetaData(String lineIn)
        {
            if (lineIn.Length <= 0)
                return null;   

            var line = lineIn.Replace('\t', ' '); // regularize whitespace for simpler regexes

            // If there is a second >, then this is a custom name, and not
            // a real FASTA sequence.
            int start = (line.Length > 0 && line[0] == '>' ? 1 : 0);
            if (line.Length > 1 && line[1] == '>')
            {
                start++;
            }
            
            ProteinMetadata bestResult = null;
            var bestCount = 0;
            foreach (var r in _regexFasta)
            {
                Match match = r.RegexPattern.Match(line.Substring(start));
                if (match.Success)
                {
                    // a hit - now use the replacement expression to get the ProteinMetadata parts
                    string[] regexOutputs = r.RegexPattern.Replace(line.Substring(start), r.RegexReplacement).Split('\n');
                    var headerResult = new DbProteinName();
                    string searchterm = null; // assume no webservice lookup unless told otherwise
                    int dbColumnsFound = 0;
                    var failedParse = false;
                    for (var n = regexOutputs.Length; n-- > 0 && !failedParse;)
                    {
                        var split = regexOutputs[n].Split(new[] {':'}, 2); // split on first colon only
                        if (split.Length == 2)
                        {
                            var type = split[0].Trim();
                            var val = split[1].Trim();
                            if (val.Contains(@"${")) // failed match
                            {
                                val = String.Empty;
                            }
                            if (val.Length > 0)
                            {
                                dbColumnsFound++; // valid entry
                                switch (type)
                                {
                                    case @"name":
                                        headerResult.Name = val;
                                        break;
                                    case @"description":
                                        headerResult.Description = val;
                                        break;
                                    case @"accession":
                                        headerResult.Accession = val;
                                        break;
                                    case @"preferredname":
                                        headerResult.PreferredName = val;
                                        break;
                                    case @"gene":
                                        headerResult.Gene = val;
                                        break;
                                    case @"species":
                                        headerResult.Species = val;
                                        break;
                                    case @"searchterm":
                                        dbColumnsFound--; // not actually a db column
                                        searchterm = val;
                                        break;
                                    default:
                                        failedParse = true; // Unusual format, or this regex isn't quite the right one for this expression
                                        break;
                                }

                            }
                        }
                        else
                        {
                            failedParse = true; // Unusual format, or this regex isn't quite the right one for this expression
                        }
                    }
                    if (failedParse)
                    {
                        continue;  // Experience has shown no value in complaining to users about unusual formats, just move on
                    }
                    if (headerResult.GetProteinMetadata().HasMissingMetadata())
                    {
                        if (searchterm != null)
                        {
                            // shave off any alternatives (might look like "IPI:IPI00197700.1|SWISS-PROT:P04638|ENSEMBL:ENSRNOP00000004662|REFSEQ:NP_037244")
                            searchterm = searchterm.Split('|')[0];
                            // a reasonable accession value will have at least one digit in it, and won't have things like tabs and parens and braces that confuse web services
                            // ReSharper disable LocalizableElement
                            if ("0123456789".Any(searchterm.Contains) && !" \t()[]".Any(searchterm.Contains))
                            // ReSharper restore LocalizableElement
                                headerResult.SetWebSearchTerm(new WebSearchTerm(searchterm[0], searchterm.Substring(1))); // we'll need to hit the webservices to get this missing info
                        }
                    }
                    if (headerResult.GetProteinMetadata().WebSearchInfo.IsEmpty())
                        headerResult.SetWebSearchCompleted(); // no search possible
                    if (dbColumnsFound > bestCount)
                    {
                        bestCount = dbColumnsFound; // best match so far - tie goes to the first hit so order matters
                        bestResult = headerResult.GetProteinMetadata();
                    }
                }
            }
            return bestResult;
        }

        private String ParseSequenceLine(String line)
        {
            // ReSharper disable LocalizableElement
            line = line.Replace(" ", "").Trim();
            // ReSharper restore LocalizableElement
            if (line.EndsWith(@"*"))
            {
                line = line.Substring(0, line.Length - 1);
            }
            return line;
        }

        private readonly string[] _standardTypes = // these all have at least type|accession, will try them in Entrez
        {
            @"dbj", //  DDBJ  dbj|accession|locus
            @"emb", //  EMBL  emb|accession|locus
            @"gb", //  GenBank  gb|accession|locus
            @"gpp", //  genome pipeline 3  gpp|accession|name
            @"nat", //  named annotation track 3  nat|accession|name
            @"pir", //  PIR  pir|accession|name
            @"prf", //  PRF  prf|accession|name
            @"ref", //  RefSeq  ref|accession|name
            @"tpd", //  third-party DDBJ  tpd|accession|name
            @"tpe", //  third-party EMBL  tpe|accession|name
            @"tpg", //  third-party GenBank  tpg|accession|name
            @"bbm", //  GenInfo backbone moltype  bbm|integer
            @"bbs", //  GenInfo backbone seqid  bbs|integer
            @"gim", //  GenInfo import ID gim|integer
        };

        // basic Regex.Replace expression for returning the info we want - some regexes may want to augment
        // must represent name,description,accession,preferredname,gene,species,searchterm
        // where searchterm, if it exists, starts with "U" or "E"(or"G") to indicate UnitprotKB or Entrez preference
        // searchterm is used only when we need to find accession numbers etc
        public const char GENINFO_TAG = 'G'; // search on entrez, but seperate out GI number searches
        public const char ENTREZ_TAG = 'E';
        public const char UNIPROTKB_TAG = 'U';
        public const string UNIPROTKB_PREFIX_SGD = "S"; // formerly "SGD:S", then ""SGD_S", but Uniprot search behavior changes once in a while
        public const char SEARCHDONE_TAG = 'X'; // to note searches which have been completed

        private const string STANDARD_REGEX_OUTPUT_FORMAT = "name:${name}\ndescription:${description}\naccession:${accession}\npreferredname:${preferredname}\ngene:${gene}\nspecies:${species}\nsearchterm:";


        private const string MATCH_DESCRIPTION_WITH_OPTIONAL_OS_AND_GN = // match common uniprot OS=species (and maybe OX=speciesID) and GN=gene format
            @"(?<description>((.*?(((\sOS=(?<species>(.*?)))?)((\sOX=(?<speciesID>(.*?)))?)((\sGN=(?<gene>(.*?)))?)($|(\s\w\w\=)+).*)))|.*)";

        /// <summary>
        /// class for regex matching in FASTA header lines - contains a comment, a regex, and an output format to be used with Regex replace
        /// output format should be of the form found in STANDARD_REGEX_OUTPUT_FORMAT
        /// </summary>
        public class FastaRegExSearchtermPair
        {
            public string Comment { get; set; } // describes what the regex is supposed to match, for user's benefit
            public string Regex { get; set; } // the actual regular expression 
            public string WebSearchtermFormat { get; set; } // a regex replacement expression to yield 

            public FastaRegExSearchtermPair(string comment, string regex, string webSearchtermFormat)
            {
                Comment = comment; // this may be presented in the UI for user extensibility help
                Regex = regex;
                WebSearchtermFormat = webSearchtermFormat; // should be of the form found in STANDARD_REGEX_OUTPUT_FORMAT
            }
        }

        /// <summary>
        /// Class for retrieving data from the web.  Tests may use
        /// classes derived from this to spoof various aspects of
        /// web access and failure.
        /// </summary>
        public class WebSearchProvider 
        {
            /// <summary>
            /// Test overrides should return false to avoid slowing down the tests
            /// </summary>
            public virtual bool IsPolite
            {
                get { return true; }
            }

            public virtual XmlTextReader GetXmlTextReader(string url)
            {
                return new XmlTextReader(url);
            }

            public virtual Stream GetWebResponseStream(string url, int timeout)
            {
                HttpWebRequest httpRequest = (HttpWebRequest) WebRequest.Create(url);
                httpRequest.Timeout = timeout;
                httpRequest.UserAgent = @"Skyline";
                MemoryStream stream = new MemoryStream();
                using (HttpWebResponse webResponse = (HttpWebResponse)httpRequest.GetResponse())
                {
                    using (var webResponseStream = webResponse.GetResponseStream())
                    {
                        if (webResponseStream != null)
                        {
                            using (StreamReader reader = new StreamReader(webResponseStream))
                            {
                                var sb = new StringBuilder();
                                while (!reader.EndOfStream)
                                {
                                    sb.AppendLine(reader.ReadLine());
                                }
                                StreamWriter writer = new StreamWriter(stream);
                                writer.Write(sb.ToString());
                                writer.Flush();
                                stream.Position = 0;
                            }
                        }
                    }
                }
                return stream;
            }

            public virtual int WebRetryCount()
            {
                return 5;
            }

            public virtual string ConstructEntrezURL(IEnumerable<string> searches, bool summary)
            {
                // Yes, that's Brian's email address there - entrez wants a point of contact with the developer of the tool hitting their service
                return ConstructURL(searches,
                // ReSharper disable LocalizableElement
                "http://eutils.ncbi.nlm.nih.gov/entrez/eutils/efetch.fcgi?db=protein&id={0}&tool=%22skyline%22&email=%22bspratt@proteinms.net%22&retmode=xml" + (summary ? "&rettype=docsum" : string.Empty),
                // ReSharper restore LocalizableElement
                 @",");
            }

            public virtual List<string> ListSearchTerms(IEnumerable<ProteinSearchInfo>  proteins)
            {
                return proteins.Select(x => x.GetProteinMetadata().GetPendingSearchTerm()).ToList();
            }

            public virtual string ConstructUniprotURL(IEnumerable<string> searches)
            {
               return ConstructURL(searches,
                   @"https://rest.uniprot.org/uniprotkb/stream?query=({0})&format=tsv&columns=id,genes,organism,length,entry name,protein names,reviewed",
                   @"+OR+");
            }

            private static string ConstructURL(IEnumerable<string> searchesIn, string format, string separator)
            {
                var searches = searchesIn.Select(Uri.EscapeDataString).ToArray();
                var batchSearchTerms = string.Join(separator, searches);
                return string.Format(format, batchSearchTerms);
            }

            public virtual int GetTimeoutMsec(int searchTermCount)
            {
                return (1000) * (10 + (searchTermCount / 5)); // 10 secs + 1 more for every 5 search terms
            }
        }

        /// <summary>
        /// like the actual WebEnabledFastaImporter.WebSearchProvider,
        /// but just notes search terms instead of actually going to the web
        /// </summary>
        public class DelayedWebSearchProvider : WebSearchProvider
        {
            public override int WebRetryCount()
            {
                return 0; // don't even try once
            }
        }

        /// <summary>
        /// like the actual  WebEnabledFastaImporter.WebSearchProvider,
        /// but just claims success instead of actually going to the web
        /// </summary>
        public class FakeWebSearchProvider : WebSearchProvider
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
                return 0; // don't even try once
            }

            public override List<string> ListSearchTerms(IEnumerable<ProteinSearchInfo> searches)
            {
                // set them all as already searched
                var proteinSearchInfos = searches as ProteinSearchInfo[] ?? searches.ToArray();
                foreach (var searchInfo in proteinSearchInfos)
                {
                    searchInfo.Status = ProteinSearchInfo.SearchStatus.success;
                    searchInfo.ProteinDbInfo.SetWebSearchCompleted();
                }
                // then do the base class action - will be an empty list
                return base.ListSearchTerms(proteinSearchInfos);
            }
        }


        /// <summary>
        /// Returns a list of the default Regex values used to parse FASTA headers.
        /// The caller may wish to insert custom regex values before using
        /// In general the matching regex should produce at least two groups "name" and "description" to
        /// reproduce the original skyline parsing which just split the line at the first space.
        /// </summary>
        public IEnumerable<FastaRegExSearchtermPair> GetStandardRegexPairs()
        {
            return new[]
            {

                new FastaRegExSearchtermPair(@"matches the 'dbtype|accession|preferredname description' format for swissprot and trembl",
                    @"^((?<name>((?<dbtype>sp|tr" +
                    @")\|(?<accession>[^\s\|]*)(\|(?<preferredname>[^\s]+)?)))"+MATCH_DESCRIPTION_WITH_OPTIONAL_OS_AND_GN+@"?)",
                    STANDARD_REGEX_OUTPUT_FORMAT + UNIPROTKB_TAG + @"${accession}"), // will attempt to lookup on Uniprot

                new FastaRegExSearchtermPair(@"matches the 'gi|number|preferredname description' format",
                    @"^((?<name>((?<dbtype>gi"+
                    @")\|(?<ginumber>[^\s\|]*)(\|(?<preferredname>[^\s]+)?)))"+MATCH_DESCRIPTION_WITH_OPTIONAL_OS_AND_GN+@"?)",
                    STANDARD_REGEX_OUTPUT_FORMAT + GENINFO_TAG + @"${ginumber}"), // will attempt to lookup on Entrez, but seperate from non-GI searches

                new FastaRegExSearchtermPair(@"matches the 'dbtype|idnumber|preferredname description' format",
                    @"^((?<name>((?<dbtype>" + String.Join(@"|", _standardTypes) +
                    @")\|(?<idnumber>[^\s\|]*)(\|(?<preferredname>[^\s]+)?)))"+MATCH_DESCRIPTION_WITH_OPTIONAL_OS_AND_GN+@"?)",
                    STANDARD_REGEX_OUTPUT_FORMAT + ENTREZ_TAG + @"${idnumber}"), // will attempt to lookup on Entrez

                new FastaRegExSearchtermPair(
                    @"matches UniRefnnn_ form '>UniqueIdentifier ClusterName n=Members Tax=Taxon RepID=RepresentativeMember' like '>UniRef100_A5DI11 Elongation factor 2 n=1 Tax=Pichia guilliermondii RepID=EF2_PICGU'",
                    @"^(?<name>(((((?<typecode>UniRef)[0-9]+_(?<accession>((?<strippedAccession>[^\.\s]+)[^\s]*)))))[\s]*))\s(?<description>(?<preferredname>(.*([^=]\s)+))((.*(\sTax=(?<species>.*)?\sRepID=.*))))",
                    STANDARD_REGEX_OUTPUT_FORMAT + UNIPROTKB_TAG + @"${strippedAccession}"), // will attempt to lookup on Uniprot

                new FastaRegExSearchtermPair(
                    @"matches form '>AARS.IPI00027442 IPI:IPI00027442.4|SWISS-PROT:P49588|ENSEMBL:ENSP00000261772|REFSEQ:NP_001596|H-INV:HIT000035254|VEGA:OTTHUMP00000080084 Tax_Id=9606 Gene_Symbol=AARS Alanyl-tRNA synthetase, cytoplasmic'",
                    @"^(?<name>([^\s]*(?<ipi>(IPI0[0-9]+))[^\s]*))\s(?<description>.*)",
                    STANDARD_REGEX_OUTPUT_FORMAT + UNIPROTKB_TAG + @"${ipi}"), // will attempt to lookup on Uniprot after passing through our translation layer

                new FastaRegExSearchtermPair(
                    @"Pick out 'SGDID:S000028729' (or SGD:S...)) from >YAL019W-A YAL019W-A SGDID:S000028729, Chr I from 114250-114819, Genome Release 64-2-1, Dubious ORF, 'Dubious open reading frame; unlikely to encode a functional protein, based on available experimental and comparative sequence data; partially overlaps ORF ATS1/YAL020C'",
                    @"^(?<name>[^\s]+)(?<description>(.*?((SGD\:S|SGDID\:S)(?<sgd>([0-9]+))[^\s]*)).*)",
                    STANDARD_REGEX_OUTPUT_FORMAT + UNIPROTKB_TAG + UNIPROTKB_PREFIX_SGD + @"${sgd}"), // will attempt to lookup on Uniprot

                new FastaRegExSearchtermPair(@" and a fallback for everything else, like  '>name description'",
                    @"^(?<name>[^\s]+)"+MATCH_DESCRIPTION_WITH_OPTIONAL_OS_AND_GN+@"?",
                    STANDARD_REGEX_OUTPUT_FORMAT + UNIPROTKB_TAG + @"${name}") // will attempt to lookup on Uniprot

            };
        }

        /// <summary>
        /// Peruse the list of proteins, access webservices as needed to resolve protein metadata.
        /// note: best to use this in its own thread since it may block on web access
        /// </summary>
        /// <param name="proteinsToSearch">the proteins to populate</param>
        /// <param name="progressMonitor">For checking operation cancellation</param>
        /// <param name="singleBatch">if true, just do one batch and plan to come back later for more</param>
        /// <returns>IEnumerable of the proteins it actually populated (some don't need it, some have to wait for a decent web connection)</returns>
        public IEnumerable<ProteinSearchInfo> DoWebserviceLookup(IEnumerable<ProteinSearchInfo> proteinsToSearch, IProgressMonitor progressMonitor, bool singleBatch)
        {
            const int SINGLE_BATCH_SIZE = 500; // If caller has indicated that it wants to do a single batch and return for more later, stop after this many successes
            const int ENTREZ_RATELIMIT = 333; // 1/3 second between requests on Entrez
            const int UNIPROTKB_RATELIMIT = 10; 
            var searchOrder = new[]
            {
                GENINFO_TAG,
                ENTREZ_TAG,
                UNIPROTKB_TAG // this order matters - we may take entrez results into uniprot for further search
            };
            var minSearchTermLen = new Dictionary<char, int>
            {
                {GENINFO_TAG, 3}, // some gi numbers are quite small
                {ENTREZ_TAG, 6},
                {UNIPROTKB_TAG, 6} // if you feed uniprot a partial search term you get a huge response
            };
            var ratelimit = new Dictionary<char, int>
            {
                {GENINFO_TAG, ENTREZ_RATELIMIT}, // search on Entrez, but don't mix with non-GI searches
                {ENTREZ_TAG, ENTREZ_RATELIMIT},
                {UNIPROTKB_TAG, UNIPROTKB_RATELIMIT}
            };

            progressMonitor = progressMonitor ?? new SilentProgressMonitor();

            // sort out the various webservices so we can batch up
            var proteins = proteinsToSearch.ToArray();
            foreach (var prot in proteins)
            {
                // translate from IPI to Uniprot if needed
                var search = prot.GetProteinMetadata().GetPendingSearchTerm().ToUpperInvariant();
                if (search.StartsWith(@"IPI"))
                {
                    if (_ipiMapper == null)
                        _ipiMapper = new IpiToUniprotMap();
                    string mapped = _ipiMapper.MapToUniprot(search);
                    if (mapped == search) // no mapping from that IPI
                    {
                        prot.SetWebSearchCompleted(); // no resolution for that IPI value
                    }
                    else
                    {
                        prot.Accession = mapped; // go ahead and note the Uniprot accession, even though we haven't searched yet
                        prot.SetWebSearchTerm(new WebSearchTerm(UNIPROTKB_TAG,mapped)); 
                    }
                }
                else if (prot.GetProteinMetadata().GetSearchType() == UNIPROTKB_TAG)
                {
                    // Check for homegrown numbering schemes
                    long dummy;
                    if (Int64.TryParse(search, out dummy))
                    {
                        prot.SetWebSearchCompleted();  // All numbers, not a Uniprot ID
                    }
                    // Some uniprot records just don't have gene info - have we already parsed all there is to know from description?
                    else if (prot.GetProteinMetadata().HasMissingMetadata())
                    {
                        var test = prot.GetProteinMetadata().ChangeGene(@"test");
                        if (!test.HasMissingMetadata()) // We got everything except gene info
                        {
                            // Check to see if this is a standard uniprot formatted header, if so assume that GN was ommitted on purpose
                            if (prot.GetProteinMetadata().Description.Contains(string.Format(@" OS={0} ", (prot.GetProteinMetadata().Species))))
                                prot.SetWebSearchCompleted(); // There's not going to be any gene info
                        }
                    }
                }

                if (prot.SeqLength == 0)
                {
                    prot.SetWebSearchCompleted(); // Searching is too ambiguous without a sequence length, so we'd have to go one by one: don't attempt it
                }

                if (prot.GetProteinMetadata().NeedsSearch())
                {
                    // if you feed uniprot a partial search term you get a huge response                    
                    int minLen = minSearchTermLen[prot.GetProteinMetadata().GetSearchType()];
                    if (prot.GetProteinMetadata().GetPendingSearchTerm().Length < minLen)
                    {
                        prot.SetWebSearchCompleted(); // don't attempt it
                    }
                }

                if (!(prot.GetProteinMetadata().NeedsSearch()))
                {
                    yield return prot;  // doesn't need search, just pass it back unchanged
                }
            }
            _ipiMapper = null;  // Done with this now

            // CONSIDER(bspratt): Could this be simplified?
            var cancelled = false;
            var politeStopwatch = new Stopwatch();
            var totalSuccessesThisBatch = 0;
            politeStopwatch.Start();
            var furtherUniprotSearches = new List<ProteinSearchInfo>(); // Uniprot searches that result from intermediate searches
            foreach (var searchType in searchOrder)
            {
                var consecutiveFailures = 0;
                var failureCount = 0;
                var successCount = 0;
                cancelled |= progressMonitor.IsCanceled;
                if (cancelled)
                    break;
                var politenessIntervalMsec = ratelimit[searchType];
                var batchsizeIncreaseThreshold = 2;  // If you can do this many in a row at reduced batchsize, try increasing it
                while (true)
                {
                    var idealBatchsize = _maxBatchSize[searchType];
                    cancelled |= progressMonitor.IsCanceled;
                    if (cancelled)
                        break;
                    var type = searchType;
                    var furtherUniprotSearchesToProcess = Equals(searchType, UNIPROTKB_TAG)
                        ? furtherUniprotSearches.Where(s => (s.GetProteinMetadata().NeedsSearch())).ToList()
                        : null;
                    var searchlist = (furtherUniprotSearchesToProcess != null && furtherUniprotSearchesToProcess.Any()) 
                        ? furtherUniprotSearchesToProcess 
                        : proteins.Where(s => (s.GetProteinMetadata().NeedsSearch() &&
                                               s.GetProteinMetadata().GetSearchType() == type)).ToList();
                    if (!searchlist.Any())
                        break;
                    // try to batch up requests - reduce batch size if responses are ambiguous
                    var nextSearch = 0;
                    var searches = new List<ProteinSearchInfo>();
                    while ((nextSearch < searchlist.Count) && (searches.Count < _batchsize[searchType]))
                    {
                        // Unique sequence length helps a lot in disambiguation
                        if (searches.All(s => s.SeqLength != searchlist[nextSearch].SeqLength))
                            searches.Add(searchlist[nextSearch]);
                        nextSearch++;
                    }

                    // Be a good citizen - no more than three hits per second for Entrez
                    var snoozeMs = _webSearchProvider.IsPolite ?
                        politenessIntervalMsec - politeStopwatch.ElapsedMilliseconds : 0;
                    if (snoozeMs > 0)
                        Thread.Sleep((int)snoozeMs);
                    politeStopwatch.Restart();
                    cancelled |= progressMonitor.IsCanceled;
                    if (cancelled)
                        break;
                    var processed = DoWebserviceLookup(searches, searchType, progressMonitor);
                    if (processed < 0) // Returns negative on web access error
                    {
                        if (processed == URL_TOO_LONG)
                        {
                            // We're creating URLs that are too long
                            _maxBatchSize[searchType] = Math.Max(1, (int)(_maxBatchSize[searchType] * 0.75));
                        }
                        else
                        {
                            // Some error, we should just try again later so don't retry now
                            break;
                        }
                    }
                    var success = false;
                    foreach (var s in searches)
                    {
                        if (s.GetProteinMetadata().GetPendingSearchTerm().Length == 0)
                        {
                            yield return s; // We've processed it
                            if (s.Status == ProteinSearchInfo.SearchStatus.success)
                            {
                                success = true;
                                _successCountAtThisBatchsize[searchType]++;
                            }
                        }
                        else
                        {
                            // possibly an Entrez search we wish to further process in Uniprot
                            var newSearchType = s.GetProteinMetadata().GetSearchType();
                            if ((newSearchType != searchType) && (Equals(newSearchType, UNIPROTKB_TAG)))
                            {
                                success = true; // Entrez search worked, leads us to a UniprotKB search
                                s.Status = ProteinSearchInfo.SearchStatus.unsearched; // Not yet search as UniprotKB
                                furtherUniprotSearches.Add(s);
                                _successCountAtThisBatchsize[searchType]++;
                            }
                            else if (_batchsize[searchType] == 1)
                            {
                                // No ambiguity is possible at batchsize==1, this one just plain didn't work
                                s.SetWebSearchCompleted();
                                yield return s;
                            }
                        }
                    }

                    // Review the overall history - how is this working out?
                    foreach (var s in searchlist)
                    {
                        if (s.Status == ProteinSearchInfo.SearchStatus.success)
                        {
                            successCount++;
                            totalSuccessesThisBatch++;
                            consecutiveFailures = 0;
                        }
                        else if (s.Status == ProteinSearchInfo.SearchStatus.failure)
                        {
                            failureCount++;
                            consecutiveFailures++;
                        }
                        if ((consecutiveFailures > (MAX_CONSECUTIVE_PROTEIN_METATDATA_LOOKUP_FAILURES + successCount)) ||
                            ((failureCount + successCount) > 100 &&
                            failureCount / (double)(failureCount + successCount) > .5))
                        {
                            // We have failed a bunch in a row, or more than half overall.  Assume the rest are the same as this streak, and bail.
                            // That  "+ successCount" term above guards against the case where we're a few hundred successes in then 
                            // we hit a bad patch (though this is unlikely - FASTA files tend to be internally consistent).
                            foreach (var ss in searchlist)
                            {
                                if (ss.GetProteinMetadata().GetPendingSearchTerm().Length > 0)
                                {
                                    ss.NoteSearchFailure();
                                    ss.SetWebSearchCompleted(); // Just tag this as having been tried
                                    yield return ss; // And move on
                                }
                            }
                            break;
                        }
                    }

                    if (success)
                    {
                        if ((_successCountAtThisBatchsize[searchType] > batchsizeIncreaseThreshold) && (_batchsize[searchType] < idealBatchsize))
                        {
                            _batchsize[searchType] = Math.Min(idealBatchsize, _batchsize[searchType]*2);
                            _successCountAtThisBatchsize[searchType] = 0;
                        }
                        if (singleBatch && (totalSuccessesThisBatch >= SINGLE_BATCH_SIZE))
                        {  // Probably called from a background loader that's trying to be polite
                            break; // done with this search type for now, but go on to the next if any (especially for follow-on Uniprot searches)
                        }
                    }
                    else
                    {
                        _batchsize[searchType] = Math.Max(1, _batchsize[searchType] / 2);
                        _successCountAtThisBatchsize[searchType] = 0;
                        batchsizeIncreaseThreshold = Math.Max(batchsizeIncreaseThreshold, batchsizeIncreaseThreshold * 2); // Get increasingly pessimistic (watch for integer rollover)
                    }
                }
            }
        }

        private static string NullForEmpty(string strIn)
        {
            var str = (strIn == null)?null:strIn.Trim();
            return (String.IsNullOrEmpty(str) ? null : str);
        }

        private ProteinMetadata MergeSearchResult(ProteinMetadata searchResult, ProteinMetadata original)
        {
            // like a normal merge, use update to fill gaps in current, but 
            // then prefer the update name and description
            // we do this to make it easier for user to relate back to the original
            // fasta header line while still allowing for cases where there may not
            // possibly have been a proper original source for descrption or even name
            ProteinMetadata result = searchResult.Merge(original);
            if (!String.IsNullOrEmpty(original.Name))
                result = result.ChangeName(original.Name);
            if (!String.IsNullOrEmpty(original.Description))
                result = result.ChangeDescription(original.Description);
            return result;
        }

        public const string KNOWNGOOD_GENINFO_SEARCH_TARGET = "15834432";
        public const string KNOWNGOOD_ENTREZ_SEARCH_TARGET = "XP_915497";
        public const string KNOWNGOOD_UNIPROT_SEARCH_TARGET = "Q08641";
        public const int KNOWNGOOD_UNIPROT_SEARCH_TARGET_SEQLEN = 628;
        public const int MAX_CONSECUTIVE_PROTEIN_METATDATA_LOOKUP_FAILURES = 20; // If we fail on several in a row, assume all are doomed to fail.
        public const int URL_TOO_LONG = -2;

        private bool SimilarSearchTerms(string a, string b)
        {
            var searchA = a.ToUpperInvariant().Split('.')[0]; // xp_12345.6 -> XP_12345
            var searchB = b.ToUpperInvariant().Split('.')[0]; // xp_12345.6 -> XP_12345
            return Equals(searchA, searchB);
        }

        /// <summary>
        /// Handles web access for deriving missing protein metadata
        /// </summary>
        /// <param name="proteins">items to search</param>
        /// <param name="searchType">Uniprot or Entrez</param>
        /// <param name="progressMonitor">For detecting operation cancellation</param>
        /// <returns>negative value if we need to try again later, else number of proteins looked up</returns>
        /// 
        private int DoWebserviceLookup(IList<ProteinSearchInfo> proteins, char searchType, IProgressMonitor progressMonitor)
        {
            int lookupCount = _webSearchProvider is FakeWebSearchProvider ? proteins.Count : 0; // Fake websearch provider used in tests just claims victory, returns 0 for WebRetryCount
            var searchterms = _webSearchProvider.ListSearchTerms(proteins);
                
            if (searchterms.Count == 0)
                return 0; // no work, but not error either
            var responses = new List<ProteinSearchInfo>();
            for (var retries = _webSearchProvider.WebRetryCount();retries-->0;)  // be patient with the web
            {
                if (searchterms.Count == 0)
                    break;
                if (progressMonitor.IsCanceled)
                    break; // Cancelled
                var caught = false;
                try
                {
                    string urlString; // left at outer scope for exception debugging ease
                    if ((searchType == GENINFO_TAG) || (searchType == ENTREZ_TAG))
                    {
                        // first try to get enough summary information to redo this seach in uniprot

                        // throw in something we know will hit (Note: it's important that this particular value appear in the unit tests, so we can mimic web response)
                        string knowngood = (searchType == GENINFO_TAG) ? KNOWNGOOD_GENINFO_SEARCH_TARGET : KNOWNGOOD_ENTREZ_SEARCH_TARGET; 
                        bool addedKnowngood = false;
                        if (!searchterms.Any(searchterm => SimilarSearchTerms(searchterm,knowngood)))
                        {
                            searchterms.Insert(0, knowngood); // ensure at least one response if connection is good
                            addedKnowngood = true;
                        }

                        urlString = _webSearchProvider.ConstructEntrezURL(searchterms,true); // get in summary form

                        /*
                         * a search on XP_915497 and 15834432 yields something like this (but don't mix GI and non GI in practice):
                            <DocSum>
                            <Id>82891194</Id>
                            <Item Name="Caption" Type="String">XP_915497</Item>
                            <Item Name="Title" Type="String">
                            PREDICTED: similar to Syntaxin binding protein 3 (UNC-18 homolog 3) (UNC-18C) (MUNC-18-3) [Mus musculus]
                            </Item>
                            <Item Name="Extra" Type="String">gi|82891194|ref|XP_915497.1|[82891194]</Item>
                            <Item Name="Gi" Type="Integer">82891194</Item>
                            <Item Name="CreateDate" Type="String">2005/12/01</Item>
                            <Item Name="UpdateDate" Type="String">2005/12/01</Item>
                            <Item Name="Flags" Type="Integer">512</Item>
                            <Item Name="TaxId" Type="Integer">10090</Item>
                            <Item Name="Length" Type="Integer">566</Item>
                            <Item Name="Status" Type="String">replaced</Item>
                            <Item Name="ReplacedBy" Type="String">NP_035634</Item>   <-- useful for Uniprot search
                            <Item Name="Comment" Type="String">
                            <![CDATA[ This record was replaced or removed. ]]>
                            </Item>
                            </DocSum>
                            <DocSum>
                            <Id>15834432</Id>
                            <Item Name="Caption" Type="String">NP_313205</Item>    <-- useful for Uniprot search
                            <Item Name="Title" Type="String">
                            30S ribosomal protein S18 [Escherichia coli O157:H7 str. Sakai]
                            </Item>
                            <Item Name="Extra" Type="String">gi|15834432|ref|NP_313205.1|[15834432]</Item>
                            <Item Name="Gi" Type="Integer">15834432</Item>
                            <Item Name="CreateDate" Type="String">2001/03/07</Item>
                            <Item Name="UpdateDate" Type="String">2013/12/20</Item>
                            <Item Name="Flags" Type="Integer">512</Item>
                            <Item Name="TaxId" Type="Integer">386585</Item>
                            <Item Name="Length" Type="Integer">75</Item>
                            <Item Name="Status" Type="String">live</Item>
                            <Item Name="ReplacedBy" Type="String"/>
                            <Item Name="Comment" Type="String">
                            <![CDATA[ ]]>
                            </Item>
                            </DocSum>
                        */
                        using (var xmlTextReader = _webSearchProvider.GetXmlTextReader(urlString))
                        {
                            var elementName = String.Empty;
                            var response = new ProteinSearchInfo();
                            bool dummy = addedKnowngood;
                            string id = null;
                            string caption = null;
                            string replacedBy = null;
                            string attrName = null;
                            string length = null;
                            while (xmlTextReader.Read())
                            {
                                switch (xmlTextReader.NodeType)
                                {
                                    case XmlNodeType.Element: // The node is an element.
                                        elementName = xmlTextReader.Name;
                                        attrName = xmlTextReader.GetAttribute(@"Name");
                                        break;
                                    case XmlNodeType.Text: // text for current element
                                        if (@"Id" == elementName) // this will be the input GI number, or GI equivalent of input
                                        {
                                            id = NullForEmpty(xmlTextReader.Value);
                                        }
                                        else if (@"ERROR" == elementName)
                                        {
                                            // we made connection, but some trouble on their end
                                            throw new WebException(xmlTextReader.Value);
                                        }
                                        else if (@"Item" == elementName)
                                        {
                                            var value = NullForEmpty(xmlTextReader.Value);
                                            if (value != null)
                                            {
                                                switch (attrName)
                                                {
                                                    case @"ReplacedBy":
                                                        replacedBy = value; // a better read on name
                                                        break;
                                                    case @"Caption":
                                                        caption = value; // a better read on name
                                                        break;
                                                    case @"Length":
                                                        length = value; // Useful for disambiguation
                                                        break;
                                                }
                                            }
                                        }
                                        break;
                                    case XmlNodeType.EndElement:
                                        if (@"DocSum" == xmlTextReader.Name)
                                        {
                                            if (dummy)
                                            {
                                                dummy = false; // first returned is just the known-good seed, the rest are useful
                                            }
                                            else
                                            {
                                                // can we transfer this search to UniprotKB? Gets us the proper accession ID,
                                                // and avoids downloading sequence data we already have or just don't want
                                                string newSearchTerm = null;
                                                string intermediateName = null;
                                                if (replacedBy != null)
                                                {
                                                    newSearchTerm = replacedBy; //  Ref|XP_nnn -> GI -> NP_yyyy
                                                    intermediateName = caption;
                                                }
                                                else if (caption != null)
                                                {
                                                    newSearchTerm = caption; // GI -> NP_yyyy
                                                    intermediateName = id;
                                                }
                                                if (newSearchTerm != null)
                                                {
                                                    response.Accession = newSearchTerm;  // a decent accession if uniprot doesn't find it
                                                    response.Description = intermediateName; // stow this here to help make the connection between searches
                                                    response.SetWebSearchTerm(new WebSearchTerm(UNIPROTKB_TAG, newSearchTerm));
                                                    int intLength;
                                                    if (!int.TryParse(length, out intLength))
                                                        intLength = 0;
                                                    response.SeqLength = intLength; // Useful for disambiguation
                                                    responses.Add(response);
                                                    foreach (var value in new[] {id, caption})
                                                    {
                                                        // note as altname for association with the original search
                                                        if (response.Protein == null)
                                                            response.Protein = new DbProtein();
                                                        response.Protein.Names.Add(new DbProteinName(null, new ProteinMetadata(value, null)));
                                                        // and remove from consideration for the full-data Entrez search
                                                        var val = value;
                                                        var oldSearches = searchterms.Where(s => SimilarSearchTerms(s, val)).ToArray();
                                                        if (oldSearches.Any())
                                                        {
                                                            // conceivably same search term is in there twice, just replace the first
                                                            searchterms.Remove(oldSearches[0]); // don't do the more verbose Entrez search
                                                        }
                                                    }
                                                }
                                            }
                                            response = new ProteinSearchInfo(); // and start another
                                            id = caption = replacedBy = null;
                                        }
                                        break;
                                }
                            }
                            xmlTextReader.Close();
                        }

                        if (searchterms.Count > (addedKnowngood ? 1 : 0))
                        {
                            // now do full entrez search - unfortunately this pulls down sequence information so it's slow and we try to avoid it
                            urlString = _webSearchProvider.ConstructEntrezURL(searchterms, false); // not a summary

                            using (var xmlTextReader = _webSearchProvider.GetXmlTextReader(urlString))
                            {
                                var elementName = String.Empty;
                                var latestGbQualifierName = string.Empty;
                                var response = new ProteinSearchInfo(); // and start another
                                bool dummy = addedKnowngood;
                                while (xmlTextReader.Read())
                                {
                                    switch (xmlTextReader.NodeType)
                                    {
                                        case XmlNodeType.Element: // The node is an element.
                                            elementName = xmlTextReader.Name;
                                            break;
                                        case XmlNodeType.Text: // text for current element
                                            if (@"GBSeq_organism" == elementName)
                                            {
                                                response.Species = NullForEmpty(xmlTextReader.Value);
                                            }
                                            else if (@"GBSeq_locus" == elementName)
                                            {
                                                response.PreferredName = NullForEmpty(xmlTextReader.Value);
                                                    // a better read on name
                                            }
                                            else if (@"GBSeq_primary-accession" == elementName)
                                            {
                                                response.Accession = NullForEmpty(xmlTextReader.Value);
                                            }
                                            else if (@"GBSeq_definition" == elementName)
                                            {
                                                if (String.IsNullOrEmpty(response.Description))
                                                    response.Description = NullForEmpty(xmlTextReader.Value);
                                            }
                                            else if (@"GBQualifier_name" == elementName)
                                            {
                                                latestGbQualifierName = NullForEmpty(xmlTextReader.Value);
                                            }
                                            else if ((@"GBQualifier_value" == elementName) &&
                                                        (@"gene" == latestGbQualifierName))
                                            {
                                                response.Gene = NullForEmpty(xmlTextReader.Value);
                                            }
                                            else if (@"GBSeqid" == elementName)
                                            {
                                                // alternate name  
                                                // use this as a way to associate this result with a search -
                                                // accession may be completely unlike the search term in GI case
                                                if (response.Protein == null)
                                                    response.Protein = new DbProtein();
                                                response.Protein.Names.Add(new DbProteinName(null,
                                                    new ProteinMetadata(NullForEmpty(xmlTextReader.Value), null)));
                                            }
                                            else if (@"GBSeq_length" == elementName)
                                            {
                                                int length;
                                                if (!int.TryParse(xmlTextReader.Value, out length))
                                                    length = 0;
                                                response.SeqLength = length;
                                            }
                                            break;
                                        case XmlNodeType.EndElement:
                                            if (@"GBSeq" == xmlTextReader.Name)
                                            {
                                                if (dummy)
                                                {
                                                    dummy = false; // first returned is just the known-good seed, the rest are useful
                                                }
                                                else
                                                {
                                                    responses.Add(response);
                                                }
                                                response = new ProteinSearchInfo(); // and start another
                                            }
                                            break;
                                    }
                                }
                                xmlTextReader.Close();
                            }
                        } // end full entrez search
                    } // End if GENINFO or ENTREZ
                    else if (searchType == UNIPROTKB_TAG)
                    {
                        int timeout = _webSearchProvider.GetTimeoutMsec(searchterms.Count); // 10 secs + 1 more for every 5 search terms
                        urlString = _webSearchProvider.ConstructUniprotURL(searchterms);
                        using (var webResponseStream = _webSearchProvider.GetWebResponseStream(urlString, timeout))
                        {
                            if (webResponseStream != null)
                            {
                                using (var reader = new StreamReader(webResponseStream))
                                {
                                    if (!reader.EndOfStream)
                                    {
                                        var header = reader.ReadLine(); // eat the header
                                        var fieldNames = header.Split('\t').ToList();
                                        // Normally comes in as Entry\tEntry name\tStatus\tProtein names\tGene names\tOrganism\tLength, but could be any order or capitialization
                                        int colAccession = fieldNames.FindIndex(i => i.Equals(@"Entry", StringComparison.OrdinalIgnoreCase));
                                        int colPreferredName = fieldNames.FindIndex(i => i.Equals(@"Entry name", StringComparison.OrdinalIgnoreCase));
                                        int colDescription = fieldNames.FindIndex(i => i.Equals(@"Protein names", StringComparison.OrdinalIgnoreCase));
                                        int colGene = fieldNames.FindIndex(i => i.Equals(@"Gene names", StringComparison.OrdinalIgnoreCase));
                                        int colSpecies = fieldNames.FindIndex(i => i.Equals(@"Organism", StringComparison.OrdinalIgnoreCase));
                                        int colLength = fieldNames.FindIndex(i => i.Equals(@"Length", StringComparison.OrdinalIgnoreCase));
                                        int colStatus = fieldNames.FindIndex(i => i.Equals(@"Reviewed", StringComparison.OrdinalIgnoreCase)); // Formerly "Status"
                                        while (!reader.EndOfStream)
                                        {
                                            var line = reader.ReadLine();
                                            if (line != null)
                                            {
                                                string[] fields = line.Split('\t');
                                                int length = 0;
                                                if (colLength >= 0)
                                                    int.TryParse(fields[colLength], out length);
                                                var response = new ProteinSearchInfo
                                                {
                                                    ProteinDbInfo = new DbProteinName
                                                    {
                                                        Accession = NullForEmpty(fields[colAccession]),
                                                        PreferredName = NullForEmpty(fields[colPreferredName]),
                                                        Description = NullForEmpty(fields[colDescription]),
                                                        Gene = NullForEmpty(fields[colGene]),
                                                        Species = NullForEmpty(fields[colSpecies]),
                                                    },
                                                    SeqLength = length,
                                                    ReviewStatus = NullForEmpty(colStatus>=0 ? fields[colStatus] : null) // Reviewed or unreviewed
                                                };
                                                responses.Add(response);
                                            }
                                        }
                                    }
                                    reader.Close();
                                }
                                webResponseStream.Close();
                            }
                        }
                    } // End if Uniprot
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError)
                    {
                        switch (((HttpWebResponse)ex.Response).StatusCode)
                        {
                            case HttpStatusCode.BadRequest:
                            case HttpStatusCode.RequestUriTooLong:
                                // malformed search, stop trying
                                if (proteins.Count == 1)
                                {
                                    proteins[0].SetWebSearchCompleted(); // No more need for lookup
                                    return 1; // We resolved one
                                }
                                return URL_TOO_LONG; // Probably asked for too many at once, caller will go into batch reduction mode
                        }
                    }
                    else if (ex.Status == WebExceptionStatus.ConnectionClosed && searchType == UNIPROTKB_TAG) // Uniprot just drops the connection on too-large searches as of Sept 2019
                    {
                        return URL_TOO_LONG; // Probably asked for too many at once, caller will go into batch reduction mode
                    }
                    caught = true;
                }
                catch
                {
                    caught = true;
                }
                if (caught)
                {
                    if (retries == 0)
                        return -1;  // just try again later
                    Thread.Sleep(1000);
                    continue;
                }

                if (responses.Count>0)
                {
                    // now see if responses are ambiguous or not
                    if (proteins.Count == 1)
                    {
                        // Any responses must belong to this protein - or this isn't a protein at all (user named it "peptide6" for example).
                        // Can get multiple results for single uniprot code, but we'll ignore those
                        // since we're not in the market for alternative proteins (in fact we're likely 
                        // resolving metadata for one here).
                        ProteinSearchInfo result = null;
                        // See if we can uniquely match by sequence length
                        int length = proteins[0].SeqLength;
                        if (length == 0)  
                        {
                            // From a peptide list, probably - sequence unknown
                            if (responses.Count(r => r.IsReviewed) == 1)
                            {
                                result = responses.First(r => r.IsReviewed);
                            }
                            else if (responses.Count(r => Equals(r.Accession, proteins[0].Accession)) == 1)
                            {
                                result = responses.First(r =>Equals(r.Accession, proteins[0].Accession));
                            }
                            else
                            {
                                if (responses.Count != 1)
                                {
                                    // Ambiguous - don't make uneducated guesses.  But if all responses share species or gene etc note that
                                    var common = ProteinSearchInfo.Intersection(responses);
                                    if (common != null)
                                    {
                                        var old = proteins[0].GetProteinMetadata();
                                        proteins[0].ChangeProteinMetadata(MergeSearchResult(common.GetProteinMetadata(), old));
                                    }
                                    proteins[0].SetWebSearchCompleted(); // We aren't going to get an answer
                                    proteins[0].NoteSearchFailure();
                                    break;
                                }
                                result = responses.First();  // We got an unambiguous response
                            }
                        }
                        else if (responses.Count(r => r.SeqLength == length) == 1)
                        {
                            result = responses.First(r =>r.SeqLength == length);
                        }
                        else if (responses.Count(r => r.SeqLength == length && r.IsReviewed) == 1) // Narrow it down to reviewed only
                        {
                            result = responses.First(r => r.SeqLength == length && r.IsReviewed);
                        }

                        if (result == null)
                        {
                            if ((length > 0) && (responses.Count(r => r.SeqLength == length) == 0)) // No plausible matches (nothing of the proper length)
                            {
                                proteins[0].SetWebSearchCompleted(); // We aren't going to get an answer
                                proteins[0].NoteSearchFailure();
                                break;
                            }
                            else if (responses.Count(r => r.IsReviewed) == 1)
                            {
                                result = responses.First(r => r.IsReviewed);
                            }
                            else
                            {
                                // Ambiguous - don't make uneducated guesses.  But if all responses share species or gene etc note that
                                var common = ProteinSearchInfo.Intersection(responses);
                                if (common != null)
                                {
                                    var old = proteins[0].GetProteinMetadata();
                                    proteins[0].ChangeProteinMetadata(MergeSearchResult(common.GetProteinMetadata(), old));
                                }
                                proteins[0].SetWebSearchCompleted(); // We aren't going to get an answer
                                proteins[0].NoteSearchFailure();
                                break;
                            }
                        }
                        // prefer the data we got from web search to anything we parsed.
                        var oldMetadata = proteins[0].GetProteinMetadata();
                        proteins[0].ChangeProteinMetadata(MergeSearchResult(result.GetProteinMetadata(), oldMetadata)); // use the first, if more than one, as the primary
                        proteins[0].Status = ProteinSearchInfo.SearchStatus.success;
                        lookupCount++; // Succcess!
                        if (Equals(searchType, proteins[0].GetProteinMetadata().GetSearchType())) // did we reassign search from Entrez to UniprotKB? If so don't mark as resolved yet.
                            proteins[0].SetWebSearchCompleted(); // no more need for lookup
                    }
                    else if ((searchType == ENTREZ_TAG) || (searchType == GENINFO_TAG))
                    {
                        // multiple proteins, but responses come in reliable order
                        if (proteins.Count == responses.Count)
                        {
                            int n = 0;
                            foreach (var response in responses)
                            {
                                // prefer the data we got from web search
                                var oldMetadata = proteins[n].GetProteinMetadata();
                                if (Equals(searchType, proteins[n].GetProteinMetadata().GetSearchType())) // did we reassign search from Entrez to UniprotKB?
                                    oldMetadata = oldMetadata.SetWebSearchCompleted();  // no more need for lookup
                                // use oldMetadata to fill in any holes in response, then take oldMetadata name and description
                                proteins[n].Status = ProteinSearchInfo.SearchStatus.success;
                                proteins[n++].ChangeProteinMetadata(MergeSearchResult(response.GetProteinMetadata(), oldMetadata));
                                lookupCount++; // Succcess!
                            }
                        }
                        else // but sometimes with gaps
                        {
                            int n = 0;
                            foreach (var response in responses)
                            {   // each response should correspond to a protein, but some proteins won't have a response
                                while (n < proteins.Count)
                                {
                                    var s = proteins[n].GetProteinMetadata().WebSearchInfo;
                                    bool hit = (s.MatchesPendingSearchTerm(response.Accession) ||
                                                s.MatchesPendingSearchTerm(response.PreferredName));
                                    if (!hit && (response.ProteinDbInfo != null))
                                    {
                                        // we have a list of alternative names from the search, try those
                                        foreach (var altName in response.Protein.Names)
                                        {
                                            hit = s.MatchesPendingSearchTerm(altName.Name);
                                            if (hit)
                                                break;
                                        }
                                    }
                                    if (hit)
                                    {
                                        // prefer the data we got from web search
                                        var oldMetadata = proteins[n].GetProteinMetadata();
                                        if (Equals(searchType, proteins[0].GetProteinMetadata().GetSearchType())) // did we reassign search from Entrez to UniprotKB?
                                            oldMetadata = oldMetadata.SetWebSearchCompleted();  // no more need for lookup
                                        // use oldMetadata to fill in any holes in response, then take oldMetadata name and description
                                        proteins[n].ChangeProteinMetadata(MergeSearchResult(response.GetProteinMetadata(), oldMetadata));
                                        proteins[n].Status = ProteinSearchInfo.SearchStatus.success;
                                        lookupCount++; // Succcess!
                                        break;
                                    }
                                    n++;
                                }
                            }
                        }
                    }
                    else // (searchType == UNIPROTKB_TAG)
                    {
                        // Multiple proteins, responses come back in no particular order, and 
                        // possibly with alternatives thrown in
                        foreach (var p in proteins)
                        {
                            var seqLength = p.SeqLength;
                            var uniqueProteinLength = proteins.Count(pr => (pr.SeqLength == seqLength)) == 1;
                            for (var reviewedOnly=0; reviewedOnly < 2; reviewedOnly++)
                            {
                                // Only look at responses with proper sequence length - narrowing to reviewed only if we have ambiguity
                                var likelyResponses = reviewedOnly == 0 ?
                                    (from r in responses where (r.SeqLength == seqLength) select r).ToArray() :
                                    (from r in responses where (r.SeqLength == seqLength && r.IsReviewed) select r).ToArray();

                                var results = (uniqueProteinLength && likelyResponses.Length == 1) ?
                                    likelyResponses : // Unambiguous - single response that matches this length, and this protein is the only one with this length
                                    (from r in likelyResponses where (p.GetProteinMetadata().WebSearchInfo.MatchesPendingSearchTerm(r.Accession)) select r).ToArray();
                                if (results.Length != 1)
                                {
                                    // See if the search term is found in exactly one result's description field
                                    var resultsDescription = (from r in likelyResponses
                                                              where ((!String.IsNullOrEmpty(r.Description) && r.Description.ToUpperInvariant().
                                                              Split(' ').Contains(p.GetProteinMetadata().GetPendingSearchTerm().ToUpperInvariant())))
                                        select r).ToArray();
                                    if (resultsDescription.Length == 1)
                                        results = resultsDescription;
                                }
                                if (results.Length != 1)
                                {
                                    // See if the search term is found in exactly one result's gene names field
                                    var resultsGene = (from r in likelyResponses
                                                       where ((!String.IsNullOrEmpty(r.Gene) && r.Gene.ToUpperInvariant().
                                                       Split(' ').Contains(p.GetProteinMetadata().GetPendingSearchTerm().ToUpperInvariant())))
                                        select r).ToArray();
                                    if (resultsGene.Length == 1)
                                        results = resultsGene;
                                }
                                if (results.Length != 1 && uniqueProteinLength)
                                {
                                    // Didn't find an obvious match, but this is the only protein of this length in the search
                                    results = likelyResponses;
                                }
                                // Make sure all matching responses have same accession, at a minimum
                                var common = ProteinSearchInfo.Intersection(results);
                                if (results.Any() && common.Accession != null)
                                {
                                    // prefer the data we got from web search
                                    var oldMetadata = p.GetProteinMetadata();
                                    oldMetadata = oldMetadata.SetWebSearchCompleted();  // no more need for lookup
                                    // use oldMetadata to fill in any holes in response, then take oldMetadata name and description
                                    p.ChangeProteinMetadata(MergeSearchResult(common.GetProteinMetadata(), oldMetadata));
                                    p.Status = ProteinSearchInfo.SearchStatus.success;
                                    lookupCount++; // Succcess!
                                    break;
                                }
                            }
                            if (p.GetProteinMetadata().NeedsSearch() && uniqueProteinLength)
                            {
                                p.SetWebSearchCompleted(); // No answer found, but we're done
                                p.NoteSearchFailure();
                                lookupCount++; // done with this one
                            }
                        }
                    }
                } // End if we got any respones
                else if (searchType == UNIPROTKB_TAG)
                {
                    // None of the searches hit - Uniprot is our last search so just set these as complete
                    foreach (var p in proteins.Where(p => p.GetProteinMetadata().NeedsSearch()))
                    {
                        p.SetWebSearchCompleted();  // No answer found, but we're done
                        p.NoteSearchFailure();
                        lookupCount++; // done with this one
                    }
                }
                else if (proteins.Count == 1)
                {
                    proteins[0].SetWebSearchCompleted(); // no response for a single protein - we aren't going to get an answer
                    proteins[0].NoteSearchFailure();
                    lookupCount++; // done with this one
                }

                break; // No need for retry
            }
            return lookupCount;
        }
        public static void ValidateProteinSequence(string sequence, int lineNumber)
        {
            for (int i = 0; i < sequence.Length; i++)
            {
                var ch = sequence[i];
                if (!IsValidProteinSequenceChar(ch))
                {
                    throw new InvalidDataException(string.Format(Resources.WebEnabledFastaImporter_ValidateProteinSequence_A_protein_sequence_cannot_contain_the_character___0___at_line__1_,
                        ch, lineNumber));
                }
            }
        }
        public static bool IsValidProteinSequenceChar(char ch)
        {
            if (ch >= 'A' && ch <= 'Z')
            {
                return true;
            }
            if (ch == '*' || ch == '-')
            {
                return true;
            }
            return false;
        }
    }
}
