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

namespace pwiz.ProteomeDatabase.Fasta
{

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


        /// <summary>
        /// Class for reading in a Fasta file, possibly noting need for later webservice access to get extra information like accession, gene etc
        /// <param name="webSearchProvider">Object for web access, tests can swap this out for a playback class and avoid actual web access</param>
        /// </summary>
        public WebEnabledFastaImporter(WebSearchProvider webSearchProvider = null)
        {
            _webSearchProvider = webSearchProvider ?? new WebSearchProvider();
            _regexFasta = new List<RegexPair>();
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

        /// <summary>
        /// Quick, cheap check for internet access (uniprot access, specifically)
        /// </summary>
        /// <returns>false if internet isn't available for any reason</returns>
        public bool HasWebAccess()
        {
            var prot = ParseProteinLine(KNOWNGOOD_UNIPROT_SEARCH_TARGET);
            var protname = new DbProteinName(prot, new ProteinMetadata(KNOWNGOOD_UNIPROT_SEARCH_TARGET, string.Empty, null, null, null, null,
                UNIPROTKB_TAG+KNOWNGOOD_UNIPROT_SEARCH_TARGET));
            return DoWebserviceLookup(new []{protname}, null, true).Any();
        }

        public IEnumerable<DbProtein> Import(TextReader reader)
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith(">")) // Not L10N
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
                    _curSequence.Append(ParseSequenceLine(line));
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
            if (proteinMetadata.Name.Contains(" ")) // Not L10N 
                return null;
            return ParseProteinMetaData(proteinMetadata.Name + " " + proteinMetadata.Description); // Not L10N
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
                    for (var n = regexOutputs.Length; n-- > 0;)
                    {
                        var split = regexOutputs[n].Split(new[] {':'}, 2); // split on first colon only
                        if (split.Length == 2)
                        {
                            var type = split[0].Trim();
                            var val = split[1].Trim();
                            if (val.Contains("${")) // failed match // Not L10N
                            {
                                val = String.Empty;
                            }
                            if (val.Length > 0)
                            {
                                dbColumnsFound++; // valid entry
                                switch (type)
                                {
                                    case "name": // Not L10N
                                        headerResult.Name = val;
                                        break;
                                    case "description": // Not L10N
                                        headerResult.Description = val;
                                        break;
                                    case "accession": // Not L10N
                                        headerResult.Accession = val;
                                        break;
                                    case "preferredname": // Not L10N
                                        headerResult.PreferredName = val;
                                        break;
                                    case "gene": // Not L10N
                                        headerResult.Gene = val;
                                        break;
                                    case "species": // Not L10N
                                        headerResult.Species = val;
                                        break;
                                    case "searchterm": // Not L10N
                                        dbColumnsFound--; // not actually a db column
                                        searchterm = val;
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException(
                                            String.Format("Unknown Fasta RegEx output formatter type \'{0}\'",    // Not L10N
                                                regexOutputs[n]));
                                }

                            }
                        }
                        else
                        {
                            throw new ArgumentOutOfRangeException(
                                String.Format("Unknown Fasta RegEx output formatter type \'{0}\'",  // Not L10N
                                    regexOutputs[n]));
                        }
                    }
                    if (headerResult.GetProteinMetadata().HasMissingMetadata())
                    {
                        if (searchterm != null)
                        {
                            // shave off any alternatives (might look like "IPI:IPI00197700.1|SWISS-PROT:P04638|ENSEMBL:ENSRNOP00000004662|REFSEQ:NP_037244")
                            searchterm = searchterm.Split('|')[0];
                            // a reasonable accession value will have at least one digit in it
                            if ("0123456789".Any(searchterm.Contains) && !" \t".Any(searchterm.Contains))  // Not L10N
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
            line = line.Replace(" ", "").Trim(); // Not L10N
            if (line.EndsWith("*")) // Not L10N
            {
                line = line.Substring(0, line.Length - 1);
            }
            return line;
        }

        private readonly string[] _standardTypes = // these all have at least type|accession, will try them in Entrez
        {
            "dbj", //  DDBJ  dbj|accession|locus // Not L10N 
            "emb", //  EMBL  emb|accession|locus // Not L10N
            "gb", //  GenBank  gb|accession|locus // Not L10N
            "gpp", //  genome pipeline 3  gpp|accession|name // Not L10N
            "nat", //  named annotation track 3  nat|accession|name // Not L10N
            "pir", //  PIR  pir|accession|name // Not L10N
            "prf", //  PRF  prf|accession|name // Not L10N
            "ref", //  RefSeq  ref|accession|name // Not L10N
            "tpd", //  third-party DDBJ  tpd|accession|name // Not L10N
            "tpe", //  third-party EMBL  tpe|accession|name // Not L10N
            "tpg", //  third-party GenBank  tpg|accession|name // Not L10N
            "bbm", //  GenInfo backbone moltype	bbm|integer	// Not L10N
            "bbs", //  GenInfo backbone seqid	bbs|integer	// Not L10N
            "gim", //  GenInfo import ID	gim|integer	// Not L10N
        };

        // basic Regex.Replace expression for returning the info we want - some regexes may want to augment
        // must represent name,description,accession,preferredname,gene,species,searchterm
        // where searchterm, if it exists, starts with "U" or "E"(or"G") to indicate UnitprotKB or Entrez preference
        // searchterm is used only when we need to find accession numbers etc
        public const char GENINFO_TAG = 'G'; // search on entrez, but seperate out GI number searches
        public const char ENTREZ_TAG = 'E';
        public const char UNIPROTKB_TAG = 'U';
        public const char SEARCHDONE_TAG = 'X'; // to note searches which have been completed

        private const string STANDARD_REGEX_OUTPUT_FORMAT = "name:${name}\ndescription:${description}\naccession:${accession}\npreferredname:${preferredname}\ngene:${gene}\nspecies:${species}\nsearchterm:"; // Not L10N


        private const string MATCH_DESCRIPTION_WITH_OPTIONAL_OS_AND_GN = // match common uniprot OS and GN format
            @"(?<description>((.*?(((\sOS=(?<species>(.*?)))?)((\sGN=(?<gene>(.*?)))?)(\s\w\w\=)+).*))|.*)"; // Not L10N

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

            public virtual XmlTextReader GetXmlTextReader(string url)
            {
                return new XmlTextReader(url);
            }

            public virtual Stream GetWebResponseStream(string url, int timeout)
            {
                HttpWebRequest httpRequest = (HttpWebRequest) WebRequest.Create(url);
                httpRequest.Timeout = timeout;
                httpRequest.UserAgent = "Skyline"; // Not L10N
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
                var batchSearchTerms = String.Join(",", searches.Select(Uri.EscapeDataString));    // Not L10N
                const string server = "http://eutils.ncbi.nlm.nih.gov"; // Not L10N

                // Yes, that's Brian's email address there - entrez wants a point of contact with the developer of the tool hitting their service
                string urlString = server +
                            string.Format(
                                "/entrez/eutils/efetch.fcgi?db=protein&id={0}&tool=%22skyline%22&email=%22bspratt@proteinms.net%22&retmode=xml", // Not L10N
                                batchSearchTerms) + (summary ? "&rettype=docsum" : String.Empty); // Not L10N
                return urlString;
            }

            public virtual List<string> ListSearchTerms(IEnumerable<DbProteinName> proteins)
            {
                return proteins.Select(x => x.GetProteinMetadata().GetPendingSearchTerm()).ToList();
            }

            public virtual string ConstructUniprotURL(IEnumerable<string> searchesIn, bool reviewedOnly)
            {
                var searches = searchesIn.Select(Uri.EscapeDataString).ToArray();
                var batchSearchTerms = String.Join("+OR+", searches);    // Not L10N
                if (batchSearchTerms.Length==0)
                    return null;

                // For searches of more than one, accept reviewed only
                // For single searches, we are presumably desperate so take non-reviewed (Trembl) as well
                string urlString =string.Format(
                    "http://www.uniprot.org/uniprot/?query={0}({1})&format=tab", // Not L10N
                    reviewedOnly ? "reviewed:yes+AND+" : String.Empty, // Not L10N
                    batchSearchTerms);
                return urlString;
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
            public override int GetTimeoutMsec(int searchTermCount)
            {
                return 10 * (10 + (searchTermCount / 5));
            }

            public override int WebRetryCount()
            {
                return 0; // don't even try once
            }

            public override List<string> ListSearchTerms(IEnumerable<DbProteinName> proteins)
            {
                // set them all as already searched
                var dbProteinNames = proteins as DbProteinName[] ?? proteins.ToArray();
                foreach (var proteinName in dbProteinNames)
                {
                    proteinName.SetWebSearchCompleted();
                }
                // then do the base class action - will be an empty list
                return base.ListSearchTerms(dbProteinNames);
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

                new FastaRegExSearchtermPair(@"matches the 'dbtype|accession|preferredname description' format for swissprot and trembl", // Not L10N
                    @"^((?<name>((?<dbtype>sp|tr" + // Not L10N
                    @")\|(?<accession>[^\s\|]*)(\|(?<preferredname>[^\s]+)?)))"+MATCH_DESCRIPTION_WITH_OPTIONAL_OS_AND_GN+"?)", // Not L10N
                    STANDARD_REGEX_OUTPUT_FORMAT + UNIPROTKB_TAG + "${accession}"), // will attempt to lookup on Uniprot // Not L10N

                new FastaRegExSearchtermPair(@"matches the 'gi|number|preferredname description' format", // Not L10N
                    @"^((?<name>((?<dbtype>gi"+ // Not L10N
                    @")\|(?<ginumber>[^\s\|]*)(\|(?<preferredname>[^\s]+)?)))"+MATCH_DESCRIPTION_WITH_OPTIONAL_OS_AND_GN+"?)", // Not L10N
                    STANDARD_REGEX_OUTPUT_FORMAT + GENINFO_TAG + "${ginumber}"), // will attempt to lookup on Entrez, but seperate from non-GI searches // Not L10N

                new FastaRegExSearchtermPair(@"matches the 'dbtype|idnumber|preferredname description' format", // Not L10N
                    @"^((?<name>((?<dbtype>" + String.Join("|", _standardTypes) + // Not L10N
                    @")\|(?<idnumber>[^\s\|]*)(\|(?<preferredname>[^\s]+)?)))"+MATCH_DESCRIPTION_WITH_OPTIONAL_OS_AND_GN+"?)", // Not L10N
                    STANDARD_REGEX_OUTPUT_FORMAT + ENTREZ_TAG + "${idnumber}"), // will attempt to lookup on Entrez // Not L10N

                new FastaRegExSearchtermPair(
                    @"matches UniRefnnn_ form '>UniqueIdentifier ClusterName n=Members Tax=Taxon RepID=RepresentativeMember' like '>UniRef100_A5DI11 Elongation factor 2 n=1 Tax=Pichia guilliermondii RepID=EF2_PICGU'", // Not L10N
                    @"^(?<name>(((((?<typecode>UniRef)[0-9]+_(?<accession>((?<strippedAccession>[^\.\s]+)[^\s]*)))))[\s]*))\s(?<description>(?<preferredname>(.*([^=]\s)+))((.*(\sTax=(?<species>.*)?\sRepID=.*))))", // Not L10N
                    STANDARD_REGEX_OUTPUT_FORMAT + UNIPROTKB_TAG + "${strippedAccession}"), // will attempt to lookup on Uniprot // Not L10N

                new FastaRegExSearchtermPair(
                    @"matches form '>AARS.IPI00027442 IPI:IPI00027442.4|SWISS-PROT:P49588|ENSEMBL:ENSP00000261772|REFSEQ:NP_001596|H-INV:HIT000035254|VEGA:OTTHUMP00000080084 Tax_Id=9606 Gene_Symbol=AARS Alanyl-tRNA synthetase, cytoplasmic'", // Not L10N
                    @"^(?<name>([^\s]*(?<ipi>(IPI0[0-9]+))[^\s]*))\s(?<description>.*)", // Not L10N
                    STANDARD_REGEX_OUTPUT_FORMAT + UNIPROTKB_TAG + "${ipi}"), // will attempt to lookup on Uniprot after passing through our translation layer // Not L10N

                new FastaRegExSearchtermPair(@" and a fallback for everything else, like  '>name description'", // Not L10N
                    @"^(?<name>[^\s]+)"+MATCH_DESCRIPTION_WITH_OPTIONAL_OS_AND_GN+"?",  // Not L10N
                    STANDARD_REGEX_OUTPUT_FORMAT + UNIPROTKB_TAG + "${name}") // will attempt to lookup on Uniprot // Not L10N

            };
        }

        /// <summary>
        /// Peruse the list of proteins, access webservices as needed to resolve protein metadata.
        /// note: best to use this in its own thread since it may block on web access
        /// </summary>
        /// <param name="proteinsToSearch">the proteins to populate - we use DbProteinName as a convenient 
        /// container for the immutable ProteinMetadata</param>
        /// <param name="progressMonitor">For checking operation cancellation</param>
        /// <param name="singleBatch">if true, just do one batch and plan to come back later for more</param>
        /// <returns>IEnumerable of the proteins it actually populated (some don't need it, some have to wait for a decent web connection)</returns>
        public IEnumerable<DbProteinName> DoWebserviceLookup(IEnumerable<DbProteinName> proteinsToSearch, IProgressMonitor progressMonitor, bool singleBatch)
        {
            const int SINGLE_BATCH_SIZE = 500; // If caller has indicated that it wants to do a single batch and return for more later, stop after this many successes
            const int ENTREZ_BATCHSIZE = 100; // they'd like 500, but it's really boggy at that size
            const int UNIPROTKB_BATCHSIZE = 50; 
            const int ENTREZ_RATELIMIT = 333; // 1/3 second between requests on Entrez
            const int UNIPROTKB_RATELIMIT = 10; 
            var searchOrder = new[]
            {
                GENINFO_TAG,
                ENTREZ_TAG,
                UNIPROTKB_TAG // this order matters - we may take entrez results into uniprot for further search
            };
            var batchSize = new Dictionary<char, int>
            {
                {GENINFO_TAG, ENTREZ_BATCHSIZE}, // search on Entrez, but don't mix with non-GI searches
                {ENTREZ_TAG, ENTREZ_BATCHSIZE},
                {UNIPROTKB_TAG, UNIPROTKB_BATCHSIZE}
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

            // sort out the various webservices so we can batch up
            var proteins = proteinsToSearch.ToArray();
            foreach (var prot in proteins)
            {
                // translate from IPI to Uniprot if needed
                var search = prot.GetProteinMetadata().GetPendingSearchTerm().ToUpperInvariant();
                if (search.StartsWith("IPI")) // Not L10N
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
            bool cancelled = false;
            var politeStopwatch = new Stopwatch();
            politeStopwatch.Start();
            int consecutiveFailures = 0; // Guard against getting mired in some user-defined format that can't succeed
            foreach (var searchType in searchOrder)
            {
                cancelled |= ((progressMonitor != null) && progressMonitor.IsCanceled);
                if (cancelled)
                    break;
                int politenessIntervalMsec = ratelimit[searchType];
                int idealBatchsize = batchSize[searchType];
                int batchsize = 1; // Managing for ambiguous responses: start with pessimistic batch size, then grow it with success.
                int batchsizeIncreaseThreshold = 2;  // if you can do this many in a row at reduced batchsize, try increasing it
                int successCountAtThisBatchsize = 0;  
                int successCount = 0;
                bool completedSearchType = false;
                while (!completedSearchType)
                {
                    cancelled |= ((progressMonitor != null) && progressMonitor.IsCanceled);
                    if (cancelled)
                        break;
                    char type = searchType;
                    var searchlist =
                        proteins.Where(s => (s.GetProteinMetadata().NeedsSearch() &&
                                               s.GetProteinMetadata().GetSearchType() == type)).ToList();
                    completedSearchType = !searchlist.Any();
                    for (int searchListIndex = 0; !completedSearchType && (searchListIndex < searchlist.Count); )
                    {
                        cancelled |= ((progressMonitor != null) && progressMonitor.IsCanceled);
                        if (cancelled)
                            break;
                        // try to batch up requests - reduce batch size if responses are ambiguous
                        int nextSearch = searchListIndex;
                        var searches = new List<DbProteinName>();
                        while ((nextSearch < searchlist.Count) && (searches.Count < batchsize))
                        {
                            searches.Add(searchlist[nextSearch++]);
                        }
                        if (searches.Count > 0)
                        {
                            // Be a good citizen - no more than three hits per second for Entrez
                            var snoozeMs = politenessIntervalMsec - politeStopwatch.ElapsedMilliseconds;
                            if (snoozeMs > 0)
                                Thread.Sleep((int)snoozeMs);
                            politeStopwatch.Restart();
                            cancelled |= ((progressMonitor != null) && progressMonitor.IsCanceled);
                            if (cancelled)
                                break;
                            int lookupCount = DoWebserviceLookup(searches, searchType, progressMonitor);
                            if (lookupCount < 0) // Returns negative on web access error
                            {
                                // Some error, we should just try again later so don't retry now
                                completedSearchType = true; // Done with this search type
                                consecutiveFailures = 0; // Reset - failure isn't due to input
                                break;
                            }
                            bool success = false;
                            foreach (var s in searches)
                            {
                                if (s.GetProteinMetadata().GetPendingSearchTerm().Length == 0)
                                {
                                    yield return s; // We've processed it
                                    if (lookupCount > 0)
                                    {
                                        success = true;
                                        successCount++;
                                        successCountAtThisBatchsize++;
                                        consecutiveFailures = 0; // Reset
                                    }
                                    else
                                    {
                                        consecutiveFailures++;
                                    }
                                }
                                else
                                {
                                    // possibly an Entrez search we wish to further process in Uniprot
                                    var newSearchType = s.GetProteinMetadata().GetSearchType();
                                    if ((newSearchType != searchType) && (Equals(newSearchType, UNIPROTKB_TAG)))
                                    {
                                        success = true; // Entrez search worked, leads us to a UniprotKB search
                                        successCount++;
                                        successCountAtThisBatchsize++;
                                        consecutiveFailures = 0; // Reset
                                    }
                                }
                            }
                            if (success)
                            {
                                if (singleBatch && (successCount >= SINGLE_BATCH_SIZE))
                                {  // Probably called from a background loader that's trying to be polite
                                    completedSearchType = true; // Done with this search type for the moment
                                    break;
                                }
                            }
                            else
                            {
                                if (batchsize == 1)
                                {
                                    // No ambiguity is possible at batchsize==1, this one just plain didn't work
                                    searchListIndex++; // For better or worse, it's processed
                                    if (consecutiveFailures > (MAX_CONSECUTIVE_PROTEIN_METATDATA_LOOKUP_FAILURES + successCount))
                                    {
                                        // We have failed a bunch in a row, assume the rest are the same as this streak, and bail.
                                        // That  "+ successCount" term above guards against the case where we're a few hundred successes in then 
                                        // we hit a bad patch (though this is unlikely - FASTA files tend to be internally consistent).
                                        while (searchListIndex < searchlist.Count)
                                        {
                                            searchlist[searchListIndex].SetWebSearchCompleted(); // Just tag this as having been tried
                                            yield return searchlist[searchListIndex++]; // And move on
                                        }
                                    }
                                }
                                int oldBatchsize = batchsize;
                                batchsize = Math.Max(1, batchsize / 2);
                                if (oldBatchsize != batchsize)
                                {
                                    consecutiveFailures = 0; // Perhaps we will do better at smaller batch size
                                }
                                successCountAtThisBatchsize = 0;
                                batchsizeIncreaseThreshold *= 2; // Get increasingly pessimistic
                                nextSearch = searchListIndex; // Try again at lower batch size
                            }
                        }
                        else
                        {
                            break; // Go reevaluate search list
                        }
                        searchListIndex = nextSearch;
                        if ((successCountAtThisBatchsize > batchsizeIncreaseThreshold) && (batchsize < idealBatchsize))
                        {
                            batchsize *= 2; // Past the ambiguity, it seems
                            successCountAtThisBatchsize = 0;
                        }
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

        public const string KNOWNGOOD_GENINFO_SEARCH_TARGET = "15834432"; // Not L10N
        public const string KNOWNGOOD_ENTREZ_SEARCH_TARGET = "XP_915497"; // Not L10N
        public const string KNOWNGOOD_UNIPROT_SEARCH_TARGET = "Q08641"; // Not L10N
        public const int MAX_CONSECUTIVE_PROTEIN_METATDATA_LOOKUP_FAILURES = 20; // If we fail on several in a row, assume all are doomed to fail.

        private bool SimilarSearchTerms(string a, string b)
        {
            var searchA = a.ToUpperInvariant().Split('.')[0]; // xp_12345.6 -> XP_12345
            var searchB = b.ToUpperInvariant().Split('.')[0]; // xp_12345.6 -> XP_12345
            return Equals(searchA, searchB);
        }

        /// <summary>
        /// Handles web access for deriving missing protein metadata
        /// </summary>
        /// <param name="proteins">items to search - we use DbProteinName as a convenient 
        /// container for the immutable ProteinMetadata</param>
        /// <param name="searchType">Uniprot or Entrez</param>
        /// <param name="progressMonitor">For detecting operation cancellation</param>
        /// <returns>negative value if we need to try again later, else number of proteins looked up</returns>
        /// 
        private int DoWebserviceLookup(IList<DbProteinName> proteins, char searchType, IProgressMonitor progressMonitor)
        {
            int lookupCount = 0;
            var searchterms = _webSearchProvider.ListSearchTerms(proteins);
                
            if (searchterms.Count == 0)
                return 0; // no work, but not error either
            var responses = new List<DbProteinName>();
            for (var retries = _webSearchProvider.WebRetryCount();retries-->0;)  // be patient with the web
            {
                if ((progressMonitor != null) && progressMonitor.IsCanceled)
                    break;

                try
                {
                    string urlString; // left at outer scope for exception debugging ease
                    if ((searchType == GENINFO_TAG) || (searchType == ENTREZ_TAG))
                    {
                        // first try to get enough summary information to redo this seach in uniprot

                        // throw in something we know will hit (Note: it's important that this particular value appear in the unit tests, so we can mimic web response)
                        string knowngood = (searchType == GENINFO_TAG) ? KNOWNGOOD_GENINFO_SEARCH_TARGET : KNOWNGOOD_ENTREZ_SEARCH_TARGET; //  Not L10N
                        bool addedKnowngood = false;
                        if (!searchterms.Any(searchterm => SimilarSearchTerms(searchterm,knowngood)))
                        {
                            searchterms.Insert(0, knowngood); // ensure at least one response if connection is good
                            addedKnowngood = true;
                        }
                        if (searchterms.Count == 0)
                            break;

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
                        try
                        {
                            using (var xmlTextReader = _webSearchProvider.GetXmlTextReader(urlString))
                            {
                                var elementName = String.Empty;
                                var response = new DbProteinName();
                                bool dummy = addedKnowngood;
                                string id=null;
                                string caption=null;
                                string replacedBy=null;
                                string attrName = null;
                                while (xmlTextReader.Read())
                                {
                                    switch (xmlTextReader.NodeType)
                                    {
                                        case XmlNodeType.Element: // The node is an element.
                                            elementName = xmlTextReader.Name;
                                            attrName = xmlTextReader.GetAttribute("Name"); // Not L10N
                                            break;
                                        case XmlNodeType.Text: // text for current element
                                            if ("Id" == elementName) // this will be the input GI number, or GI equivalent of input // Not L10N
                                            {
                                                id = NullForEmpty(xmlTextReader.Value);
                                            }
                                            else if ("ERROR" == elementName) // Not L10N
                                            {
                                                // we made connection, but some trouble on their end
                                                throw new WebException(xmlTextReader.Value);
                                            }
                                            else if ("Item" == elementName) // Not L10N
                                            {
                                                var value = NullForEmpty(xmlTextReader.Value);
                                                if (value != null)
                                                {
                                                    switch (attrName)
                                                    {
                                                        case "ReplacedBy": // Not L10N
                                                            replacedBy = value; // a better read on name
                                                            break;
                                                        case "Caption": // Not L10N
                                                            caption = value; // a better read on name
                                                            break;
                                                    }
                                                }
                                            }
                                            break;
                                        case XmlNodeType.EndElement:
                                            if ("DocSum" == xmlTextReader.Name) // Not L10N
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
                                                        newSearchTerm = replacedBy;  //  Ref|XP_nnn -> GI -> NP_yyyy
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
                                                        responses.Add(response);
                                                        foreach (var value in new[] { id, caption })
                                                        {
                                                            // note as altname for association with the original search
                                                            if (response.Protein == null)
                                                                response.Protein = new DbProtein();
                                                            response.Protein.Names.Add(new DbProteinName(null, new ProteinMetadata(value, null)));
                                                            // and remove from consideration for the full-data Entrez search
                                                            var val = value;
                                                            var oldSearches = searchterms.Where(s => SimilarSearchTerms(s, val)).ToArray();
                                                            if (oldSearches.Any()) // conceivably same search term is in there twice, just replace the first
                                                                searchterms.Remove(oldSearches[0]); // don't do the more verbose Entrez search
                                                        }
                                                    }
                                                }
                                                response = new DbProteinName(); // and start another
                                                id = caption = replacedBy = null;
                                            }
                                            break;
                                    }
                                }
                                xmlTextReader.Close();
                            }
                        }
                        catch (Exception)
                        {
                            if (retries == 0)
                                return -1; // just try again later
                            Thread.Sleep(1000);
                            continue;
                        }

                        if (searchterms.Count > (addedKnowngood ? 1 : 0))
                        {

                            // now do full entrez search - unfortunately this pulls down sequence information so it's slow and we try to avoid it
                            urlString = _webSearchProvider.ConstructEntrezURL(searchterms, false); // not a summary

                            try
                            {
                                using (var xmlTextReader = _webSearchProvider.GetXmlTextReader(urlString))
                                {
                                    var elementName = String.Empty;
                                    var latestGbQualifierName = string.Empty;
                                    var response = new DbProteinName(); // and start another
                                    bool dummy = addedKnowngood;
                                    while (xmlTextReader.Read())
                                    {
                                        switch (xmlTextReader.NodeType)
                                        {
                                            case XmlNodeType.Element: // The node is an element.
                                                elementName = xmlTextReader.Name;
                                                break;
                                            case XmlNodeType.Text: // text for current element
                                                if ("GBSeq_organism" == elementName) // Not L10N
                                                {
                                                    response.Species = NullForEmpty(xmlTextReader.Value);
                                                }
                                                else if ("GBSeq_locus" == elementName) // Not L10N
                                                {
                                                    response.PreferredName = NullForEmpty(xmlTextReader.Value); // a better read on name
                                                }
                                                else if ("GBSeq_primary-accession" == elementName) // Not L10N
                                                {
                                                    response.Accession = NullForEmpty(xmlTextReader.Value);
                                                }
                                                else if ("GBSeq_definition" == elementName) // Not L10N
                                                {
                                                    if (String.IsNullOrEmpty(response.Description))
                                                        response.Description = NullForEmpty(xmlTextReader.Value);
                                                }
                                                else if ("GBQualifier_name" == elementName) // Not L10N
                                                {
                                                    latestGbQualifierName = NullForEmpty(xmlTextReader.Value);
                                                }
                                                else if (("GBQualifier_value" == elementName) && // Not L10N
                                                         ("gene" == latestGbQualifierName)) // Not L10N
                                                {
                                                    response.Gene = NullForEmpty(xmlTextReader.Value);
                                                }
                                                else if ("GBSeqid" == elementName)  // Not L10N
                                                {
                                                    // alternate name  
                                                    // use this as a way to associate this result with a search -
                                                    // accession may be completely unlike the search term in GI case
                                                    if (response.Protein == null)
                                                        response.Protein = new DbProtein();
                                                    response.Protein.Names.Add(new DbProteinName(null,new ProteinMetadata(NullForEmpty(xmlTextReader.Value),null)));
                                                }
                                                break;
                                            case XmlNodeType.EndElement:
                                                if ("GBSeq" == xmlTextReader.Name) // Not L10N
                                                {
                                                    if (dummy)
                                                    {
                                                        dummy = false; // first returned is just the known-good seed, the rest are useful
                                                    }
                                                    else 
                                                    {
                                                        responses.Add(response);
                                                    }
                                                    response = new DbProteinName(); // and start another
                                                }
                                                break;
                                        }
                                    }
                                    xmlTextReader.Close();
                                }
                            }
                            catch (Exception)
                            {
                                if (retries==0)
                                    return -1; // just try again later
                                Thread.Sleep(1000);
                                continue;
                            }
                        } // end full entrez search
                    }
                    else if (searchType == UNIPROTKB_TAG)
                    {
                        if (searchterms.Count==0)
                            break;


                        int timeout = _webSearchProvider.GetTimeoutMsec(searchterms.Count); // 10 secs + 1 more for every 5 search terms
                        try
                        {
                            bool reviewedOnly = (searchterms.Count > 1); // Unless we're desperate, reduce size of result by asking for reviewed proteins only
                            while (true)
                            {
                                urlString = _webSearchProvider.ConstructUniprotURL(searchterms, reviewedOnly);
                                using (var webResponseStream = _webSearchProvider.GetWebResponseStream(urlString, timeout))
                                {
                                    if (webResponseStream != null)
                                    {
                                        using (StreamReader reader = new StreamReader(webResponseStream))
                                        {
                                            if (!reader.EndOfStream)
                                            {
                                                var header = reader.ReadLine(); // eat the header
                                                string[] fieldNames = header.Split('\t'); // Not L10N
                                                // normally comes in as Entry\tEntry name\tStatus\tProtein names\tGene names\tOrganism\tLength
                                                int colAccession = Array.IndexOf(fieldNames, "Entry");  // Not L10N
                                                int colPreferredName = Array.IndexOf(fieldNames, "Entry name");  // Not L10N
                                                int colDescription = Array.IndexOf(fieldNames, "Protein names");  // Not L10N
                                                int colGene = Array.IndexOf(fieldNames, "Gene names"); // Not L10N
                                                int colSpecies = Array.IndexOf(fieldNames, "Organism"); // Not L10N
                                                while (!reader.EndOfStream)
                                                {
                                                    var line = reader.ReadLine();
                                                    if (line != null)
                                                    {
                                                        string[] fields = line.Split('\t'); // Not L10N
                                                        var response = new DbProteinName
                                                        {
                                                            Accession = NullForEmpty(fields[colAccession]),
                                                            PreferredName = NullForEmpty(fields[colPreferredName]),
                                                            Description = NullForEmpty(fields[colDescription]),
                                                            Gene = NullForEmpty(fields[colGene]),
                                                            Species = NullForEmpty(fields[colSpecies])
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
                                // If no responses, try again without the reviewedOnly filter.
                                if (responses.Any() || !reviewedOnly)
                                    break;
                                reviewedOnly = false;
                            }
                        }
                        catch (WebException)
                        {
                            if (retries == 0)
                                return -1;  // just try again later
                            Thread.Sleep(1000);
                            continue;
                        }
                        catch (Exception)
                        {
                            return -1;  // just try again later
                        }
                    }
                }
                catch (WebException)
                {
                    if (retries==0)
                        return -1;  // just try again later
                    Thread.Sleep(1000);
                    continue;
                }
                catch
                {
                    return -1;  // just try again later
                }

                if (responses.Count>0)
                {
                    // now see if responses are ambiguous or not
                    if (proteins.Count() == 1)
                    {
                        // any responses must belong to this protein.
                        // can get multiple results for single uniprot code, but we'll ignore those
                        // since we're not in the market for alternative proteins (in fact we're likely 
                        // resolving metadtat for one here).

                        // prefer the data we got from web search to anything we parsed.
                        var oldMetadata = proteins[0].GetProteinMetadata();
                        proteins[0].ChangeProteinMetadata(MergeSearchResult(responses[0].GetProteinMetadata(), oldMetadata)); // use the first, if more than one, as the primary
                        lookupCount++; // Succcess!
                        if (Equals(searchType, proteins[0].GetProteinMetadata().GetSearchType())) // did we reassign search from Entrez to UniprotKB? If so don't mark as resolved yet.
                            proteins[0].SetWebSearchCompleted(); // no more need for lookup
                    }
                    else if ((searchType == ENTREZ_TAG) || (searchType == GENINFO_TAG))
                    {
                        // multiple proteins, but responses come in reliable order
                        if (proteins.Count() == responses.Count())
                        {
                            int n = 0;
                            foreach (var response in responses)
                            {
                                // prefer the data we got from web search
                                var oldMetadata = proteins[n].GetProteinMetadata();
                                if (Equals(searchType, proteins[n].GetProteinMetadata().GetSearchType())) // did we reassign search from Entrez to UniprotKB?
                                    oldMetadata = oldMetadata.SetWebSearchCompleted();  // no more need for lookup
                                // use oldMetadata to fill in any holes in response, then take oldMetadata name and description
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
                                    if (!hit && (response.Protein != null))
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
                        foreach (var protein in proteins)
                        {
                            // Any responses have data that matches our search term?
                            var p = protein;
                            var results = (from r in responses
                                        where (p.GetProteinMetadata().WebSearchInfo.MatchesPendingSearchTerm(r.Accession))
                                        select r).ToArray();
                            if (!results.Any())
                            {
                                // See if the search term is found in exactly one result's description field
                                results = (from r in responses
                                           where ((!String.IsNullOrEmpty(r.Description)&& r.Description.ToUpperInvariant().Split(' ').Contains(p.GetProteinMetadata().GetPendingSearchTerm().ToUpperInvariant())))
                                           select r).ToArray();
                            }
                            if (!results.Any())
                            {
                                // See if the search term is found in exactly one result's gene names field
                                results = (from r in responses
                                           where ((!String.IsNullOrEmpty(r.Gene) && r.Gene.ToUpperInvariant().Split(' ').Contains(p.GetProteinMetadata().GetPendingSearchTerm().ToUpperInvariant())))
                                           select r).ToArray();
                            }
                            // If more than one result match for this protein make sure it's non-ambiguous
                            if (results.Select(m => m.Accession).Distinct().Count() == 1)
                            {
                                // prefer the data we got from web search
                                var oldMetadata = p.GetProteinMetadata();
                                oldMetadata = oldMetadata.SetWebSearchCompleted();  // no more need for lookup
                                // use oldMetadata to fill in any holes in response, then take oldMetadata name and description
                                p.ChangeProteinMetadata(MergeSearchResult(results.First().GetProteinMetadata(), oldMetadata));
                                lookupCount++; // Succcess!
                            }
                        }
                    }
                }
                else if (proteins.Count() == 1)
                {
                    proteins[0].SetWebSearchCompleted(); // no response for a single protein - we aren't going to get an answer
                }

                break; // No need for retry
            }
            return lookupCount; // No problems encountered
        }
    }
}
