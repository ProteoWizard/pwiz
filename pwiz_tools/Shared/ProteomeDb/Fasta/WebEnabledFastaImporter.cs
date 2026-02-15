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
            FailureReason = WebSearchFailureReason.none;
        }

        public ProteinSearchInfo()
        {
            ProteinDbInfo = new DbProteinName();
            SeqLength = 0;
            Status = SearchStatus.unsearched;
            FailureReason = WebSearchFailureReason.none;
        }

        public enum SearchStatus { unsearched, success, failure };

        public DbProteinName ProteinDbInfo { get; set; }
        public int SeqLength { get; set; }  // Useful for disambiguation of multiple responses
        public string ReviewStatus { get; set; } // Status reviewed or unreviewed in Uniprot
        public bool IsReviewed => string.Equals(ReviewStatus,@"reviewed", StringComparison.OrdinalIgnoreCase); // Uniprot reviewed status
        public SearchStatus Status { get; set; }
        public WebSearchFailureReason FailureReason { get; private set; }
        public Exception FailureException { get; private set; }
        public string FailureDetail { get; private set; }
        public int? TaxonomyId { get; set; }
        private readonly Queue<WebSearchTerm> _alternateSearchTerms = new Queue<WebSearchTerm>();
        private readonly List<string> _searchUrlHistory = new List<string>();
        public IReadOnlyList<string> SearchUrlHistory => _searchUrlHistory;
        public IEnumerable<WebSearchTerm> AlternateSearchTerms => _alternateSearchTerms.ToArray();

        public void NoteSearchFailure(WebSearchFailureReason reason = WebSearchFailureReason.none,
            Exception exception = null, string detail = null, bool markFailed = true)
        {
            if (markFailed && Status == SearchStatus.unsearched)
                Status = SearchStatus.failure;
            if (reason != WebSearchFailureReason.none || FailureReason == WebSearchFailureReason.none)
                FailureReason = reason;
            if (exception != null)
                FailureException = exception;
            if (detail != null)
            {
                FailureDetail = detail;
            }
            else if (exception != null && string.IsNullOrEmpty(FailureDetail))
            {
                FailureDetail = exception.Message;
            }
        }

        public void AddAlternateSearchTerm(WebSearchTerm searchTerm)
        {
            if (searchTerm == null || string.IsNullOrEmpty(searchTerm.Query))
                return;
            _alternateSearchTerms.Enqueue(searchTerm);
        }

        public void AppendAlternateSearchTerms(IEnumerable<WebSearchTerm> searchTerms)
        {
            if (searchTerms == null)
                return;
            foreach (var searchTerm in searchTerms)
            {
                AddAlternateSearchTerm(searchTerm);
            }
        }

        public IEnumerable<WebSearchTerm> GetAlternateSearchTerms()
        {
            return _alternateSearchTerms.ToArray();
        }

        public bool TryActivateNextSearchTerm()
        {
            // Don't activate alternate search terms if this protein is already marked as complete
            if (!GetProteinMetadata().NeedsSearch())
                return false;
            while (_alternateSearchTerms.Count > 0)
            {
                var next = _alternateSearchTerms.Dequeue();
                if (next == null || string.IsNullOrEmpty(next.Query))
                    continue;
                ProteinDbInfo.SetWebSearchTerm(next);
                ResetFailureState();
                Status = SearchStatus.unsearched;
                return true;
            }
            return false;
        }

        private void ResetFailureState()
        {
            FailureReason = WebSearchFailureReason.none;
            FailureException = null;
            FailureDetail = null;
        }

        public void MarkLastSearchUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return;
            if (_searchUrlHistory.Count > 0 && string.Equals(_searchUrlHistory[_searchUrlHistory.Count - 1], url, StringComparison.Ordinal))
                return; // Avoid duplicate consecutive entries
            _searchUrlHistory.Add(url);
        }

        public void AppendSearchHistory(IEnumerable<string> urls)
        {
            if (urls == null)
                return;
            foreach (var url in urls)
            {
                MarkLastSearchUrl(url);
            }
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

        public override string ToString()
        {
            // Prefer Accession, fall back to PreferredName if Accession is not available
            // Accession gets used in request URLs more than PreferredName
            var name = Accession ?? ProteinDbInfo?.Accession ??
                PreferredName ?? ProteinDbInfo?.PreferredName ?? string.Empty;
            var description = Description ?? ProteinDbInfo?.Description ?? string.Empty;
            if (Status != SearchStatus.failure)
                return $@"{Status} - {name}: {description}";

            var reasonText = FailureReason == WebSearchFailureReason.none
                ? @"unspecified"
                : FailureReason.ToString();
            var exceptionText = FailureException?.GetType().Name;
            var detailText = FailureDetail;
            var message = !string.IsNullOrEmpty(detailText)
                ? detailText
                : exceptionText;
            if (string.IsNullOrEmpty(message))
                return $@"{Status} ({reasonText}) - {name}: {description}";
            return $@"{Status} ({reasonText}) - {name}: {description} [{message}]";
        }
    }

    public enum WebSearchFailureReason
    {
        none,
        no_response,
        ambiguous_response,
        sequence_mismatch,
        failure_threshold,
        http_not_found,
        http_error,
        invalid_request,
        url_too_long,
        dns_failure,
        connection_failed,
        connection_lost,
        no_network,
        timeout,
        cancelled,
        unknown_error
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
        private List<ProteinSearchInfo> _activeUniprotRequeueList;


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
                {GENINFO_TAG, 1},  // Search on Entrez, but don't mix with non-GI searches
                {ENTREZ_TAG, 1},
                {UNIPROTKB_TAG, 1}
            };
            const int ENTREZ_BATCHSIZE = 100; // They'd like 500, but it's really boggy at that size
            const int UNIPROTKB_BATCHSIZE = 200;  // Enforcing sequence length uniqueness helps efficiency, we can make this pretty large (was 400 before Sept 2019, but now they just close the connection on overly large requests so start smaller)
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
        /// <param name="lineIn">The text to be parsed</param>
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
                    // A hit - now use the replacement expression to get the ProteinMetadata parts
                    string[] regexOutputs = r.RegexPattern.Replace(line.Substring(start), r.RegexReplacement).Split('\n');
                    var headerResult = new DbProteinName();
                    string searchterm = null; // Assume no webservice lookup unless told otherwise
                    int dbColumnsFound = 0;
                    var failedParse = false;
                    for (var n = regexOutputs.Length; n-- > 0 && !failedParse;)
                    {
                        var split = regexOutputs[n].Split(new[] {':'}, 2); // Split on first colon only
                        if (split.Length == 2)
                        {
                            var type = split[0].Trim();
                            var val = split[1].Trim();
                            if (val.Contains(@"${")) // Failed match
                            {
                                val = String.Empty;
                            }
                            if (val.Length > 0)
                            {
                                dbColumnsFound++; // Valid entry
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
                                        dbColumnsFound--; // Not actually a db column
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
                            // Shave off any alternatives (might look like "IPI:IPI00197700.1|SWISS-PROT:P04638|ENSEMBL:ENSRNOP00000004662|REFSEQ:NP_037244")
                            searchterm = searchterm.Split('|')[0];
                            // A reasonable accession value will have at least one digit in it, and won't have things like tabs and parens and braces that confuse web services
                            // ReSharper disable LocalizableElement
                            if ("0123456789".Any(searchterm.Contains) && !" \t()[]".Any(searchterm.Contains))
                                headerResult.SetWebSearchTerm(new WebSearchTerm(searchterm[0], searchterm.Substring(1))); // we'll need to hit the webservices to get this missing info
                            // ReSharper restore LocalizableElement
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
            line = line.Replace(@" ", string.Empty).Trim();
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
        public const char GENINFO_TAG = 'G'; // Search on entrez, but separate out GI number searches
        public const char ENTREZ_TAG = 'E';
        public const char UNIPROTKB_TAG = 'U';
        public const string UNIPROTKB_PREFIX_SGD = "S"; // Formerly "SGD:S", then ""SGD_S", but Uniprot search behavior changes once in a while
        public const char SEARCHDONE_TAG = 'X'; // To note searches which have been completed

        private const string STANDARD_REGEX_OUTPUT_FORMAT = "name:${name}\ndescription:${description}\naccession:${accession}\npreferredname:${preferredname}\ngene:${gene}\nspecies:${species}\nsearchterm:";


        private const string MATCH_DESCRIPTION_WITH_OPTIONAL_OS_AND_GN = // match common uniprot OS=species (and maybe OX=speciesID) and GN=gene format
            @"(?<description>((.*?(((\sOS=(?<species>(.*?)))?)((\sOX=(?<speciesID>(.*?)))?)((\sGN=(?<gene>(.*?)))?)($|(\s\w\w\=)+).*)))|.*)";

        /// <summary>
        /// Class for regex matching in FASTA header lines - contains a comment, a regex, and an output format to be used with Regex replace
        /// output format should be of the form found in STANDARD_REGEX_OUTPUT_FORMAT
        /// </summary>
        public class FastaRegExSearchtermPair
        {
            public string Comment { get; set; } // Describes what the regex is supposed to match, for user's benefit
            public string Regex { get; set; } // The actual regular expression 
            public string WebSearchtermFormat { get; set; } // A regex replacement expression to yield 

            public FastaRegExSearchtermPair(string comment, string regex, string webSearchtermFormat)
            {
                Comment = comment; // This may be presented in the UI for user extensibility help
                Regex = regex;
                WebSearchtermFormat = webSearchtermFormat; // Should be of the form found in STANDARD_REGEX_OUTPUT_FORMAT
            }
        }

        /// <summary>
        /// Class for retrieving data from the web.  Tests may use
        /// classes derived from this to spoof various aspects of
        /// web access and failure.
        /// </summary>
        public class WebSearchProvider 
        {
            private const string SKYLINE_USER_AGENT = @"Skyline";

            protected virtual HttpClientWithProgress CreateHttpClient(IProgressMonitor progressMonitor, int timeout)
            {
                // Progress status for this request is ignored. The progress monitor is passed only
                // to support cancellation. Progress for the larger operation that is making the
                // HTTP requests gets reported based on completed chunks.
                var httpClient = new HttpClientWithProgress(new CancelOnlyProgressMonitor(progressMonitor), new ProgressStatus());
                if (timeout > 0)
                    httpClient.RequestTimeout = TimeSpan.FromMilliseconds(timeout);
                httpClient.ShowTransferSize = false;
                httpClient.AddHeader(@"User-Agent", SKYLINE_USER_AGENT);
                return httpClient;
            }

            /// <summary>
            /// Test overrides should return false to avoid slowing down the tests
            /// </summary>
            public virtual bool IsPolite
            {
                get { return true; }
            }

            public virtual XmlTextReader GetXmlTextReader(string url, IProgressMonitor progressMonitor, int timeout)
            {
                using var httpClient = CreateHttpClient(progressMonitor, timeout);
                var xmlContent = httpClient.DownloadString(new Uri(url));
                return new XmlTextReader(new StringReader(xmlContent));
            }

            public virtual Stream GetWebResponseStream(string url, IProgressMonitor progressMonitor, int timeout)
            {
                using var httpClient = CreateHttpClient(progressMonitor, timeout);
                var responseData = httpClient.DownloadData(new Uri(url));
                return new MemoryStream(responseData, writable: false);
            }

            public virtual int WebRetryCount()
            {
                return 5;
            }

            public virtual string ConstructEntrezURL(IEnumerable<string> searches, bool summary)
            {
                // Yes, that's Brian's email address there - entrez wants a point of contact with the developer of the tool hitting their service
                // ReSharper disable LocalizableElement
                return ConstructURL(searches,
                    "http://eutils.ncbi.nlm.nih.gov/entrez/eutils/efetch.fcgi?db=protein&id={0}&tool=%22skyline%22&email=%22bspratt@proteinms.net%22&retmode=xml" +
                    (summary ? "&rettype=docsum" : string.Empty),
                    @",");
                // ReSharper restore LocalizableElement
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

        private class CancelOnlyProgressMonitor : IProgressMonitorWithCancellationToken
        {
            private IProgressMonitor _wrappedProgressMonitor;

            public CancelOnlyProgressMonitor(IProgressMonitor wrappedProgressMonitor)
            {
                _wrappedProgressMonitor = wrappedProgressMonitor;
            }

            public bool IsCanceled => _wrappedProgressMonitor.IsCanceled;

            public UpdateProgressResponse UpdateProgress(IProgressStatus status)
            {
                // Do nothing. This monitor is only for cancellation.
                return UpdateProgressResponse.normal;
            }

            public bool HasUI => false;

            public CancellationToken CancellationToken =>
                (_wrappedProgressMonitor as IProgressMonitorWithCancellationToken)?.CancellationToken ?? CancellationToken.None;
        }

        /// <summary>
        /// Like the actual <see cref="WebEnabledFastaImporter.WebSearchProvider"/>,
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
        /// Like the actual <see cref="WebEnabledFastaImporter.WebSearchProvider"/>,
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
        /// The caller may wish to insert custom regex values before using.
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
        /// <param name="proteinsToSearch">The proteins to populate</param>
        /// <param name="progressMonitor">For checking operation cancellation</param>
        /// <param name="singleBatch">If true, just do one batch and plan to come back later for more</param>
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
                UNIPROTKB_TAG // This order matters - we may take entrez results into uniprot for further search
            };
            var minSearchTermLen = new Dictionary<char, int>
            {
                {GENINFO_TAG, 3}, // Some gi numbers are quite small
                {ENTREZ_TAG, 6},
                {UNIPROTKB_TAG, 6} // If you feed uniprot a partial search term you get a huge response
            };
            var ratelimit = new Dictionary<char, int>
            {
                {GENINFO_TAG, ENTREZ_RATELIMIT}, // Search on Entrez, but don't mix with non-GI searches
                {ENTREZ_TAG, ENTREZ_RATELIMIT},
                {UNIPROTKB_TAG, UNIPROTKB_RATELIMIT}
            };

            progressMonitor = progressMonitor ?? new SilentProgressMonitor();

            // Sort out the various webservices so we can batch up
            var proteins = proteinsToSearch.ToArray();
            foreach (var prot in proteins)
            {
                // Translate from IPI to Uniprot if needed
                var search = prot.GetProteinMetadata().GetPendingSearchTerm().ToUpperInvariant();
                if (search.StartsWith(@"IPI"))
                {
                    if (_ipiMapper == null)
                        _ipiMapper = new IpiToUniprotMap();
                    string mapped = _ipiMapper.MapToUniprot(search);
                    if (mapped == search) // No mapping from that IPI
                    {
                        prot.SetWebSearchCompleted(); // No resolution for that IPI value
                    }
                    else
                    {
                        prot.Accession = mapped; // Go ahead and note the Uniprot accession, even though we haven't searched yet
                        prot.SetWebSearchTerm(new WebSearchTerm(UNIPROTKB_TAG,mapped)); 
                    }
                }
                else if (prot.GetProteinMetadata().GetSearchType() == UNIPROTKB_TAG)
                {
                    // Check for homegrown numbering schemes
                    if (Int64.TryParse(search, out _))
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
                    // If you feed uniprot a partial search term you get a huge response                    
                    int minLen = minSearchTermLen[prot.GetProteinMetadata().GetSearchType()];
                    if (prot.GetProteinMetadata().GetPendingSearchTerm().Length < minLen)
                    {
                        prot.SetWebSearchCompleted(); // Don't attempt it
                    }
                }

                if (!(prot.GetProteinMetadata().NeedsSearch()))
                {
                    yield return prot;  // Doesn't need search, just pass it back unchanged
                }
            }
            _ipiMapper = null;  // Done with this now

            // CONSIDER(bspratt): Could this be simplified?
            var cancelled = false;
            var abort = false;
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
                    // Try to batch up requests - reduce batch size if responses are ambiguous
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
                    WebserviceLookupOutcome lookupResult;
                    try
                    {
                        _activeUniprotRequeueList = Equals(searchType, UNIPROTKB_TAG) ? furtherUniprotSearches : null;
                        lookupResult = DoWebserviceLookup(searches, searchType, progressMonitor);
                    }
                    finally
                    {
                        _activeUniprotRequeueList = null;
                    }
                    if (lookupResult == WebserviceLookupOutcome.url_too_long)
                    {
                        // We're creating URLs that are too long
                        _maxBatchSize[searchType] = Math.Max(1, (int)(_maxBatchSize[searchType] * 0.75));
                    }
                    else if (lookupResult == WebserviceLookupOutcome.retry_later)
                    {
                        // Some error, we should just try again later so don't retry now
                        abort = true;
                        break;
                    }
                    else if (lookupResult == WebserviceLookupOutcome.cancelled)
                    {
                        cancelled = true;
                        break;
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
                            // Possibly an Entrez search we wish to further process in Uniprot
                            var newSearchType = s.GetProteinMetadata().GetSearchType();
                            if ((newSearchType != searchType) && (Equals(newSearchType, UNIPROTKB_TAG)))
                            {
                                success = true; // Entrez search worked, leads us to a UniprotKB search
                                s.Status = ProteinSearchInfo.SearchStatus.unsearched; // Not yet search as UniprotKB
                                furtherUniprotSearches.Add(s);
                                _successCountAtThisBatchsize[searchType]++;
                            }
                            else if (Equals(searchType, UNIPROTKB_TAG) && s.GetProteinMetadata().NeedsSearch())
                            {
                                if (!furtherUniprotSearches.Contains(s))
                                    furtherUniprotSearches.Add(s);
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
                                    ss.NoteSearchFailure(
                                        WebSearchFailureReason.failure_threshold,
                                        detail: @"Exceeded consecutive failure threshold.");
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

                // Failure in the inner loop should end the outer loop also
                if (abort)
                    break;
            }
        }

        private static string NullForEmpty(string strIn)
        {
            var str = (strIn == null)?null:strIn.Trim();
            return (String.IsNullOrEmpty(str) ? null : str);
        }

        private void QueryEntrez(List<string> searchterms, char searchType, IProgressMonitor progressMonitor,
            List<ProteinSearchInfo> responses, IList<ProteinSearchInfo> proteins)
        {
            var addedKnowngood = EnsureEntrezKnownGood(searchterms, searchType);
            ReadEntrezSummary(searchterms, progressMonitor, responses, addedKnowngood, proteins);
            if (searchterms.Count > (addedKnowngood ? 1 : 0))
            {
                ReadEntrezFull(searchterms, progressMonitor, responses, addedKnowngood, proteins);
            }
        }

        private bool EnsureEntrezKnownGood(IList<string> searchterms, char searchType)
        {
            // Throw in something we know will hit (Note: it's important that this particular value appear in the unit tests, so we can mimic web response)
            string knowngood = (searchType == GENINFO_TAG) ? KNOWNGOOD_GENINFO_SEARCH_TARGET : KNOWNGOOD_ENTREZ_SEARCH_TARGET;
            if (searchterms.Any(searchterm => SimilarSearchTerms(searchterm, knowngood)))
                return false;
            searchterms.Insert(0, knowngood); // Ensure at least one response if connection is good
            return true;
        }

        private void ReadEntrezSummary(List<string> searchterms, IProgressMonitor progressMonitor,
            List<ProteinSearchInfo> responses, bool addedKnowngood, IList<ProteinSearchInfo> proteins)
        {
            var urlString = _webSearchProvider.ConstructEntrezURL(searchterms, true); // Get in summary form
            int timeout = _webSearchProvider.GetTimeoutMsec(searchterms.Count);
            StampRequestUrl(proteins, urlString);

            /*
             * A search on XP_915497 and 15834432 yields something like this (but don't mix GI and non GI in practice):
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
            using var xmlTextReader = _webSearchProvider.GetXmlTextReader(urlString, progressMonitor, timeout);
            var elementName = String.Empty;
            var response = new ProteinSearchInfo();
            bool dummy = addedKnowngood;
            string id = null;
            string caption = null;
            string replacedBy = null;
            string attrName = null;
            string length = null;
            string title = null;
            string taxId = null;
            while (xmlTextReader.Read())
            {
                switch (xmlTextReader.NodeType)
                {
                    case XmlNodeType.Element: // The node is an element.
                        elementName = xmlTextReader.Name;
                        attrName = xmlTextReader.GetAttribute(@"Name");
                        break;
                    case XmlNodeType.Text: // Text for current element
                        if (@"Id" == elementName) // This will be the input GI number, or GI equivalent of input
                        {
                            id = NullForEmpty(xmlTextReader.Value);
                        }
                        else if (@"ERROR" == elementName)
                        {
                            // We made connection, but some trouble on their end
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
                                        replacedBy = value; // A better read on name
                                        break;
                                    case @"Caption":
                                        caption = value; // A better read on name
                                        break;
                                    case @"Length":
                                        length = value; // Useful for disambiguation
                                        break;
                                    case @"Title":
                                        title = value;
                                        break;
                                    case @"TaxId":
                                        taxId = value;
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
                                dummy = false; // First returned is just the known-good seed, the rest are useful
                            }
                            else
                            {
                                ProcessEntrezSummaryResult(searchterms, responses, ref response, id, caption, replacedBy, title,
                                    taxId, length, urlString);
                            }
                            response = new ProteinSearchInfo(); // And start another
                            id = caption = replacedBy = title = taxId = null;
                            length = null;
                        }
                        break;
                }
            }
        }

        private static string ExtractSpeciesFromTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
                return null;
            // Entrez titles can have format: "description [status] - species name" or "description [species name]"
            // Look for bracketed text, but skip known status words
            var start = title.LastIndexOf('[');
            var end = title.LastIndexOf(']');
            if (start >= 0 && end > start)
            {
                var bracketed = title.Substring(start + 1, end - start - 1).Trim();
                // Skip known status words that appear before the species
                if (bracketed.Equals(@"validated", StringComparison.OrdinalIgnoreCase) ||
                    bracketed.Equals(@"imported", StringComparison.OrdinalIgnoreCase))
                {
                    // Species appears after a dash separator
                    var dashIndex = title.IndexOf(@" - ", start, StringComparison.Ordinal);
                    if (dashIndex > 0 && dashIndex < title.Length - 3)
                    {
                        // Extract everything after " - " as the species
                        var speciesCandidate = title.Substring(dashIndex + 3).Trim();
                        // Remove any trailing bracketed annotations if present
                        var trailingBracket = speciesCandidate.IndexOf('[');
                        if (trailingBracket > 0)
                            speciesCandidate = speciesCandidate.Substring(0, trailingBracket).Trim();
                        return speciesCandidate;
                    }
                    return null;
                }
                return bracketed;
            }
            return null;
        }

        private static string BuildUniprotSearchTerm(string accession, string taxId)
        {
            // If accession is null or empty, cannot build a search term - return null to indicate no search possible
            if (string.IsNullOrEmpty(accession))
                return null;
            // If taxId is not available, return just the accession without organism filter
            if (string.IsNullOrEmpty(taxId))
                return accession;
            // Build search term with organism filter for more precise results
            return $@"{accession} AND (organism_id:{taxId})";
        }

        private void ProcessEntrezSummaryResult(List<string> searchterms, List<ProteinSearchInfo> responses,
            ref ProteinSearchInfo response, string id, string caption, string replacedBy, string title,
            string taxId, string length, string urlString)
        {
            // Can we transfer this search to UniprotKB? Gets us the proper accession ID,
            // and avoids downloading sequence data we already have or just don't want.
            // Note: UniProt API moves around with some frequency, so we're cautious about changes here.
            string newSearchTerm = null;
            string intermediateName = null;
            string fallbackSearchTerm = null;
            var species = ExtractSpeciesFromTitle(title);
            if (!string.IsNullOrEmpty(replacedBy))
            {
                newSearchTerm = replacedBy; //  Ref|XP_nnn -> GI -> NP_yyyy
                intermediateName = caption;
                if (!string.IsNullOrEmpty(caption) &&
                    !string.Equals(replacedBy, caption, StringComparison.OrdinalIgnoreCase))
                {
                    fallbackSearchTerm = caption;
                }
            }
            else if (!string.IsNullOrEmpty(caption))
            {
                newSearchTerm = caption; // GI -> NP_yyyy
                intermediateName = id;
            }
            if (newSearchTerm != null)
            {
                response.Accession = newSearchTerm;  // A decent accession if uniprot doesn't find it
                response.Description = intermediateName; // Stow this here to help make the connection between searches
                var primarySearch = BuildUniprotSearchTerm(newSearchTerm, taxId);
                response.SetWebSearchTerm(new WebSearchTerm(UNIPROTKB_TAG, primarySearch));
                if (!string.Equals(primarySearch, newSearchTerm, StringComparison.Ordinal))
                {
                    response.AddAlternateSearchTerm(new WebSearchTerm(UNIPROTKB_TAG, newSearchTerm));
                }
                if (!string.IsNullOrEmpty(species))
                    response.Species = species;
                if (int.TryParse(taxId, out var parsedTaxId))
                    response.TaxonomyId = parsedTaxId;
                if (!string.IsNullOrEmpty(fallbackSearchTerm))
                    response.AddAlternateSearchTerm(new WebSearchTerm(UNIPROTKB_TAG, fallbackSearchTerm));
                int intLength;
                if (!int.TryParse(length, out intLength))
                    intLength = 0;
                response.SeqLength = intLength; // Useful for disambiguation
                response.MarkLastSearchUrl(urlString);
                responses.Add(response);
                foreach (var value in new[] { id, caption })
                {
                    // Note as altname for association with the original search
                    if (response.Protein == null)
                        response.Protein = new DbProtein();
                    response.Protein.Names.Add(new DbProteinName(null, new ProteinMetadata(value, null)));
                    // and remove from consideration for the full-data Entrez search
                    var val = value;
                    var oldSearches = searchterms.Where(s => SimilarSearchTerms(s, val)).ToArray();
                    if (oldSearches.Any())
                    {
                        // Conceivably same search term is in there twice, just replace the first
                        searchterms.Remove(oldSearches[0]); // don't do the more verbose Entrez search
                    }
                }
            }
        }

        private void ReadEntrezFull(List<string> searchterms, IProgressMonitor progressMonitor,
            List<ProteinSearchInfo> responses, bool addedKnowngood, IList<ProteinSearchInfo> proteins)
        {
            var urlString = _webSearchProvider.ConstructEntrezURL(searchterms, false); // Not a summary
            int timeout = _webSearchProvider.GetTimeoutMsec(searchterms.Count);
            StampRequestUrl(proteins, urlString);
            using var xmlTextReader = _webSearchProvider.GetXmlTextReader(urlString, progressMonitor, timeout);
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
                    case XmlNodeType.Text: // Text for current element
                        MapEntrezFullElement(xmlTextReader, ref response, ref latestGbQualifierName);
                        break;
                    case XmlNodeType.EndElement:
                        if (@"GBSeq" == xmlTextReader.Name)
                        {
                            if (dummy)
                            {
                                dummy = false; // First returned is just the known-good seed, the rest are useful
                            }
                            else
                            {
                                response.MarkLastSearchUrl(urlString);
                                responses.Add(response);
                            }
                            response = new ProteinSearchInfo(); // and start another
                        }
                        break;
                }
            }
        }

        private void MapEntrezFullElement(XmlTextReader xmlTextReader, ref ProteinSearchInfo response, ref string latestGbQualifierName)
        {
            var elementName = xmlTextReader.Name;
            if (@"GBSeq_organism" == elementName)
            {
                response.Species = NullForEmpty(xmlTextReader.Value);
            }
            else if (@"GBSeq_locus" == elementName)
            {
                response.PreferredName = NullForEmpty(xmlTextReader.Value);
                // A better read on name
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
                // Alternate name  
                // Use this as a way to associate this result with a search -
                // Accession may be completely unlike the search term in GI case
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
        }

        private void QueryUniprot(List<string> searchTerms, IProgressMonitor progressMonitor,
            List<ProteinSearchInfo> responses, IList<ProteinSearchInfo> proteins)
        {
            int timeout = _webSearchProvider.GetTimeoutMsec(searchTerms.Count); // 10 secs + 1 more for every 5 search terms
            var urlString = _webSearchProvider.ConstructUniprotURL(searchTerms);
            StampRequestUrl(proteins, urlString);
            using var webResponseStream = _webSearchProvider.GetWebResponseStream(urlString, progressMonitor, timeout);
            if (webResponseStream == null)
                return;

            using var reader = new StreamReader(webResponseStream);
            if (reader.EndOfStream)
                return;

            var header = reader.ReadLine(); // eat the header
            if (header == null)
                return;

            var fieldNames = header.Split('\t').ToList();
            // Normally comes in as Entry\tEntry name\tStatus\tProtein names\tGene names\tOrganism\tLength, but could be any order or capitalization
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
                        ReviewStatus = NullForEmpty(colStatus >= 0 ? fields[colStatus] : null) // Reviewed or unreviewed
                    };
                    response.MarkLastSearchUrl(urlString);
                    responses.Add(response);
                }
            }
        }

        private ProteinMetadata MergeSearchResult(ProteinMetadata searchResult, ProteinMetadata original)
        {
            // Like a normal merge, use update to fill gaps in current, but 
            // then prefer the update name and description
            // We do this to make it easier for user to relate back to the original
            // FASTA header line while still allowing for cases where there may not
            // possibly have been a proper original source for description or even name
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
        private bool SimilarSearchTerms(string a, string b)
        {
            var searchA = a.ToUpperInvariant().Split('.')[0]; // xp_12345.6 -> XP_12345
            var searchB = b.ToUpperInvariant().Split('.')[0]; // xp_12345.6 -> XP_12345
            return Equals(searchA, searchB);
        }

        private enum WebserviceLookupOutcome
        {
            completed,
            url_too_long,
            retry_later,
            cancelled
        }

        /// <summary>
        /// Handles web access for deriving missing protein metadata
        /// </summary>
        /// <param name="proteins">Items to search</param>
        /// <param name="searchType">Uniprot or Entrez</param>
        /// <param name="progressMonitor">For detecting operation cancellation</param>
        /// <returns>Outcome indicating success, a too-long request, or that the caller should retry later</returns>
        private WebserviceLookupOutcome DoWebserviceLookup(IList<ProteinSearchInfo> proteins, char searchType, IProgressMonitor progressMonitor)
        {
            var searchTerms = _webSearchProvider.ListSearchTerms(proteins);

            if (searchTerms.Count == 0)
                return WebserviceLookupOutcome.completed; // No work, but not error either

            if (IsAccessFaked)
                return WebserviceLookupOutcome.completed;
            var responses = new List<ProteinSearchInfo>();
            for (var retries = _webSearchProvider.WebRetryCount(); retries-- > 0;)  // Be patient with the web
            {
                if (searchTerms.Count == 0)
                    break;
                if (progressMonitor.IsCanceled)
                    break; // Cancelled

                var iterationOutcome = ExecuteLookupIteration(proteins, searchType, searchTerms, responses, progressMonitor);
                if (iterationOutcome == WebserviceLookupOutcome.retry_later)
                {
                    if (retries == 0)
                        return WebserviceLookupOutcome.retry_later;  // Just try again later
                    Thread.Sleep(1000);
                    continue;
                }
                if (iterationOutcome == WebserviceLookupOutcome.url_too_long)
                {
                    return WebserviceLookupOutcome.url_too_long;
                }
                if (iterationOutcome == WebserviceLookupOutcome.cancelled)
                {
                    return WebserviceLookupOutcome.cancelled;
                }

                break; // Success
            }
            return WebserviceLookupOutcome.completed;
        }

        private WebserviceLookupOutcome ExecuteLookupIteration(IList<ProteinSearchInfo> proteins,
            char searchType, List<string> searchTerms, List<ProteinSearchInfo> responses,
            IProgressMonitor progressMonitor)
        {
            var failureReason = WebSearchFailureReason.none;
            Exception failureException = null;
            string failureDetail = null;

            try
            {
                if (searchType == GENINFO_TAG || searchType == ENTREZ_TAG)
                {
                    QueryEntrez(searchTerms, searchType, progressMonitor, responses, proteins);
                }
                else if (searchType == UNIPROTKB_TAG)
                {
                    QueryUniprot(searchTerms, progressMonitor, responses, proteins);
                }
            }
            catch (NetworkRequestException ex)
            {
                failureReason = MapNetworkFailureReason(ex);

                if (ex.StatusCode == HttpStatusCode.BadRequest || ex.StatusCode == HttpStatusCode.RequestUriTooLong)
                {
                    if (proteins.Count == 1)
                    {
                        if (failureReason == WebSearchFailureReason.none)
                            failureReason = WebSearchFailureReason.invalid_request;
                        RecordFailure(proteins, failureReason, ex, markFailed: true);
                        proteins[0].SetWebSearchCompleted(); // No more need for lookup
                        return WebserviceLookupOutcome.completed; // We resolved one
                    }
                    return WebserviceLookupOutcome.url_too_long; // Probably asked for too many at once, caller will go into batch reduction mode
                }

                if (ex.FailureType == NetworkFailureType.ConnectionLost && searchType == UNIPROTKB_TAG)
                {
                    return WebserviceLookupOutcome.url_too_long; // UniProt drops the connection on too-large searches
                }

                if (ex.StatusCode != HttpStatusCode.NotFound)
                {
                    RecordFailure(proteins, failureReason, ex);
                    return WebserviceLookupOutcome.retry_later;
                }

                failureException = ex;
                failureDetail = ex.Message;
            }
            catch (OperationCanceledException ex)
            {
                RecordFailure(proteins, WebSearchFailureReason.cancelled, ex);
                return WebserviceLookupOutcome.cancelled;
            }
            catch (TimeoutException ex)
            {
                RecordFailure(proteins, WebSearchFailureReason.timeout, ex);
                return WebserviceLookupOutcome.retry_later;
            }
            catch (Exception ex)
            {
                // CONSIDER: Move ExceptionUtil to CommonUtil.SystemUtil so that IsProgrammingDefect can be used here
                RecordFailure(proteins, WebSearchFailureReason.unknown_error, ex);
                return WebserviceLookupOutcome.retry_later;
            }

            if (responses.Count > 0)
            {
                ProcessResponsesBySearchType(proteins, responses, searchType);
            }
            else
            {
                HandleNoResponses(proteins, searchType, failureReason, failureException, failureDetail);
            }

            return WebserviceLookupOutcome.completed;
        }

        private static void RecordFailure(IEnumerable<ProteinSearchInfo> proteins,
            WebSearchFailureReason reason, Exception exception,
            string detail = null, bool markFailed = false)
        {
            if (reason == WebSearchFailureReason.none && exception == null && string.IsNullOrEmpty(detail))
                return;

            foreach (var protein in proteins)
            {
                if (!protein.GetProteinMetadata().NeedsSearch())
                    continue;
                protein.NoteSearchFailure(reason, exception, detail ?? exception?.Message, markFailed);
            }
        }

        private static WebSearchFailureReason MapNetworkFailureReason(NetworkRequestException ex)
        {
            if (ex == null)
                return WebSearchFailureReason.unknown_error;
            if (ex.StatusCode.HasValue)
            {
                switch (ex.StatusCode.Value)
                {
                    case HttpStatusCode.NotFound:
                        return WebSearchFailureReason.http_not_found;
                    case HttpStatusCode.RequestUriTooLong:
                        return WebSearchFailureReason.url_too_long;
                    case HttpStatusCode.BadRequest:
                        return WebSearchFailureReason.invalid_request;
                    default:
                        return WebSearchFailureReason.http_error;
                }
            }

            return ex.FailureType switch
            {
                NetworkFailureType.HttpError => WebSearchFailureReason.http_error,
                NetworkFailureType.NoConnection => WebSearchFailureReason.no_network,
                NetworkFailureType.ConnectionFailed => WebSearchFailureReason.connection_failed,
                NetworkFailureType.ConnectionLost => WebSearchFailureReason.connection_lost,
                NetworkFailureType.Timeout => WebSearchFailureReason.timeout,
                NetworkFailureType.DnsResolution => WebSearchFailureReason.dns_failure,
                _ => WebSearchFailureReason.unknown_error
            };
        }

        private static void StampRequestUrl(IEnumerable<ProteinSearchInfo> proteins, string url)
        {
            if (string.IsNullOrEmpty(url) || proteins == null)
                return;
            foreach (var protein in proteins)
            {
                protein?.MarkLastSearchUrl(url);
            }
        }

        private bool TryScheduleAlternateUniprotSearch(ProteinSearchInfo protein)
        {
            if (protein == null)
                return false;
            if (!protein.TryActivateNextSearchTerm())
                return false;
            if (_activeUniprotRequeueList != null && !_activeUniprotRequeueList.Contains(protein))
            {
                _activeUniprotRequeueList.Add(protein);
            }
            return true;
        }

        private void ProcessResponsesBySearchType(IList<ProteinSearchInfo> proteins, IList<ProteinSearchInfo> responses,
            char searchType)
        {
            if (proteins.Count == 1)
            {
                HandleSingleProteinResponse(proteins[0], responses, searchType);
                return;
            }

            if (searchType == ENTREZ_TAG || searchType == GENINFO_TAG)
            {
                HandleEntrezResponses(proteins, responses, searchType);
                return;
            }

            HandleUniprotResponses(proteins, responses);
        }

        private void HandleNoResponses(IList<ProteinSearchInfo> proteins, char searchType,
            WebSearchFailureReason reason,
            Exception exception, string detail)
        {
            var resolvedReason = reason != WebSearchFailureReason.none
                ? reason
                : WebSearchFailureReason.no_response;

            if (searchType == UNIPROTKB_TAG)
            {
                // None of the searches hit - Uniprot is our last search so just set these as complete
                foreach (var protein in proteins.Where(p => p.GetProteinMetadata().NeedsSearch()))
                {
                    if (TryScheduleAlternateUniprotSearch(protein))
                    {
                        continue;
                    }
                    protein.NoteSearchFailure(resolvedReason, exception, detail);
                    protein.SetWebSearchCompleted();  // No answer found, but we're done
                }
                return;
            }

            if (proteins.Count == 1)
            {
                if (proteins[0].GetProteinMetadata().NeedsSearch())
                    proteins[0].NoteSearchFailure(resolvedReason, exception, detail);
                proteins[0].SetWebSearchCompleted(); // No response for a single protein - we aren't going to get an answer
            }
        }

        private void HandleSingleProteinResponse(ProteinSearchInfo protein, IList<ProteinSearchInfo> responses,
            char searchType)
        {
            // Any responses must belong to this protein - or this isn't a protein at all (user named it "peptide6" for example).
            // Can get multiple results for single uniprot code, but we'll ignore those
            // since we're not in the market for alternative proteins (in fact we're likely 
            // resolving metadata for one here).
            ProteinSearchInfo result = null;
            // See if we can uniquely match by sequence length
            int length = protein.SeqLength;
            if (length == 0)
            {
                // From a peptide list, probably - sequence unknown
                if (responses.Count(r => r.IsReviewed) == 1)
                {
                    result = responses.First(r => r.IsReviewed);
                }
                else if (responses.Count(r => Equals(r.Accession, protein.Accession)) == 1)
                {
                    result = responses.First(r => Equals(r.Accession, protein.Accession));
                }
                else
                {
                    if (responses.Count != 1)
                    {
                        // Ambiguous - don't make uneducated guesses.  But if all responses share species or gene etc note that
                        var common = ProteinSearchInfo.Intersection(responses);
                        if (common != null)
                        {
                            var old = protein.GetProteinMetadata();
                            protein.ChangeProteinMetadata(MergeSearchResult(common.GetProteinMetadata(), old));
                        }
                        if (TryScheduleAlternateUniprotSearch(protein))
                            return;
                        protein.SetWebSearchCompleted(); // We aren't going to get an answer
                        protein.NoteSearchFailure(
                            WebSearchFailureReason.ambiguous_response,
                            detail: $@"Ambiguous response count: {responses.Count}");
                        return;
                    }
                    result = responses.First();  // We got an unambiguous response
                }
            }
            else if (responses.Count(r => r.SeqLength == length) == 1)
            {
                result = responses.First(r => r.SeqLength == length);
            }
            else if (responses.Count(r => r.SeqLength == length && r.IsReviewed) == 1) // Narrow it down to reviewed only
            {
                result = responses.First(r => r.SeqLength == length && r.IsReviewed);
            }

            if (result == null)
            {
                if ((length > 0) && (responses.Count(r => r.SeqLength == length) == 0)) // No plausible matches (nothing of the proper length)
                {
                    protein.SetWebSearchCompleted(); // We aren't going to get an answer
                    protein.NoteSearchFailure(
                        WebSearchFailureReason.sequence_mismatch,
                        detail: $@"No responses with sequence length {length}");
                    return;
                }

                if (responses.Count(r => r.IsReviewed) == 1)
                {
                    result = responses.First(r => r.IsReviewed);
                }
                else
                {
                    // Ambiguous - don't make uneducated guesses.  But if all responses share species or gene etc note that
                    var common = ProteinSearchInfo.Intersection(responses);
                    if (common != null)
                    {
                        var old = protein.GetProteinMetadata();
                        protein.ChangeProteinMetadata(MergeSearchResult(common.GetProteinMetadata(), old));
                    }
                    if (TryScheduleAlternateUniprotSearch(protein))
                        return;
                    protein.SetWebSearchCompleted(); // We aren't going to get an answer
                    protein.NoteSearchFailure(
                        WebSearchFailureReason.ambiguous_response,
                        detail: $@"Ambiguous response count: {responses.Count}");
                    return;
                }
            }
            // Prefer the data we got from web search to anything we parsed.
            var oldMetadata = protein.GetProteinMetadata();
            protein.ChangeProteinMetadata(MergeSearchResult(result.GetProteinMetadata(), oldMetadata)); // use the first, if more than one, as the primary
            protein.Status = ProteinSearchInfo.SearchStatus.success;
            protein.AppendAlternateSearchTerms(result.GetAlternateSearchTerms());
            if (result.SearchUrlHistory.Any())
                protein.AppendSearchHistory(result.SearchUrlHistory);
            if (Equals(searchType, protein.GetProteinMetadata().GetSearchType())) // did we reassign search from Entrez to UniprotKB? If so don't mark as resolved yet.
                protein.SetWebSearchCompleted(); // no more need for lookup
        }

        private void HandleEntrezResponses(IList<ProteinSearchInfo> proteins, IList<ProteinSearchInfo> responses,
            char searchType)
        {
            // Multiple proteins, but responses come in reliable order
            if (proteins.Count == responses.Count)
            {
                int index = 0;
                foreach (var response in responses)
                {
                    // Prefer the data we got from web search
                    var oldMetadata = proteins[index].GetProteinMetadata();
                    if (Equals(searchType, proteins[index].GetProteinMetadata().GetSearchType())) // did we reassign search from Entrez to UniprotKB?
                        oldMetadata = oldMetadata.SetWebSearchCompleted();  // no more need for lookup
                    // Use oldMetadata to fill in any holes in response, then take oldMetadata name and description
                    proteins[index].Status = ProteinSearchInfo.SearchStatus.success;
                    proteins[index].ChangeProteinMetadata(MergeSearchResult(response.GetProteinMetadata(), oldMetadata));
                    if (response.TaxonomyId.HasValue)
                        proteins[index].TaxonomyId = response.TaxonomyId;
                    proteins[index].AppendAlternateSearchTerms(response.GetAlternateSearchTerms());
                    if (response.SearchUrlHistory.Any())
                        proteins[index].AppendSearchHistory(response.SearchUrlHistory);
                    index++;
                }
                return;
            }

            int proteinIndex = 0;
            foreach (var response in responses)
            {
                // Each response should correspond to a protein, but some proteins won't have a response
                while (proteinIndex < proteins.Count)
                {
                    var searchInfo = proteins[proteinIndex].GetProteinMetadata().WebSearchInfo;
                    bool hit = (searchInfo.MatchesPendingSearchTerm(response.Accession) ||
                                searchInfo.MatchesPendingSearchTerm(response.PreferredName));
                    if (!hit && (response.ProteinDbInfo != null))
                    {
                        // We have a list of alternative names from the search, try those
                        foreach (var altName in response.Protein.Names)
                        {
                            hit = searchInfo.MatchesPendingSearchTerm(altName.Name);
                            if (hit)
                                break;
                        }
                    }
                    if (hit)
                    {
                        // Prefer the data we got from web search
                        var oldMetadata = proteins[proteinIndex].GetProteinMetadata();
                        if (Equals(searchType, proteins[0].GetProteinMetadata().GetSearchType())) // did we reassign search from Entrez to UniprotKB?
                            oldMetadata = oldMetadata.SetWebSearchCompleted();  // No more need for lookup
                        // Use oldMetadata to fill in any holes in response, then take oldMetadata name and description
                        proteins[proteinIndex].ChangeProteinMetadata(MergeSearchResult(response.GetProteinMetadata(), oldMetadata));
                        proteins[proteinIndex].Status = ProteinSearchInfo.SearchStatus.success;
                        if (response.TaxonomyId.HasValue)
                            proteins[proteinIndex].TaxonomyId = response.TaxonomyId;
                        proteins[proteinIndex].AppendAlternateSearchTerms(response.GetAlternateSearchTerms());
                        if (response.SearchUrlHistory.Any())
                            proteins[proteinIndex].AppendSearchHistory(response.SearchUrlHistory);
                        break;
                    }
                    proteinIndex++;
                }
            }
        }

        private void HandleUniprotResponses(IList<ProteinSearchInfo> proteins, IList<ProteinSearchInfo> responses)
        {
            // Multiple proteins, responses come back in no particular order, and 
            // possibly with alternatives thrown in
            foreach (var protein in proteins)
            {
                var seqLength = protein.SeqLength;
                var uniqueProteinLength = proteins.Count(pr => (pr.SeqLength == seqLength)) == 1;
                for (var reviewedOnly = 0; reviewedOnly < 2; reviewedOnly++)
                {
                    // Only look at responses with proper sequence length - narrowing to reviewed only if we have ambiguity
                    var likelyResponses = reviewedOnly == 0 ?
                        responses.Where(r => r.SeqLength == seqLength).ToArray() :
                        responses.Where(r => r.SeqLength == seqLength && r.IsReviewed).ToArray();

                    var results = (uniqueProteinLength && likelyResponses.Length == 1) ?
                        likelyResponses : // Unambiguous - single response that matches this length, and this protein is the only one with this length
                        likelyResponses.Where(r => protein.GetProteinMetadata().WebSearchInfo.MatchesPendingSearchTerm(r.Accession)).ToArray();
                    if (results.Length != 1)
                    {
                        var normalizedSpecies = NormalizeSpecies(protein.Species);
                        if (!string.IsNullOrEmpty(normalizedSpecies))
                        {
                            var speciesMatches = likelyResponses
                                .Where(r => !string.IsNullOrEmpty(r.Species) &&
                                            NormalizeSpecies(r.Species) == normalizedSpecies)
                                .ToArray();
                            if (speciesMatches.Length == 1)
                                results = speciesMatches;
                            else if (speciesMatches.Length > 1)
                                results = speciesMatches;
                        }
                    }
                    if (results.Length != 1)
                    {
                        // See if the search term is found in exactly one result's description field
                        var searchTerm = protein.GetProteinMetadata().GetPendingSearchTerm().ToUpperInvariant();
                        var resultsDescription = likelyResponses
                            .Where(r => !String.IsNullOrEmpty(r.Description) &&
                                        r.Description.ToUpperInvariant().Split(' ').Contains(searchTerm))
                            .ToArray();
                        if (resultsDescription.Length == 1)
                            results = resultsDescription;
                    }
                    if (results.Length != 1)
                    {
                        // See if the search term is found in exactly one result's gene names field
                        var searchTerm = protein.GetProteinMetadata().GetPendingSearchTerm().ToUpperInvariant();
                        var resultsGene = likelyResponses
                            .Where(r => !String.IsNullOrEmpty(r.Gene) &&
                                        r.Gene.ToUpperInvariant().Split(' ').Contains(searchTerm))
                            .ToArray();
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
                        // Prefer the data we got from web search
                        var oldMetadata = protein.GetProteinMetadata();
                        oldMetadata = oldMetadata.SetWebSearchCompleted();  // No more need for lookup
                        // Use oldMetadata to fill in any holes in response, then take oldMetadata name and description
                        protein.ChangeProteinMetadata(MergeSearchResult(common.GetProteinMetadata(), oldMetadata));
                        protein.Status = ProteinSearchInfo.SearchStatus.success;
                        foreach (var match in results)
                        {
                            if (match.SearchUrlHistory.Any())
                                protein.AppendSearchHistory(match.SearchUrlHistory);
                        }
                        break;
                    }
                }
                if (protein.GetProteinMetadata().NeedsSearch())
                {
                    if (TryScheduleAlternateUniprotSearch(protein))
                        continue;
                }
                if (protein.GetProteinMetadata().NeedsSearch() && uniqueProteinLength)
                {
                    protein.SetWebSearchCompleted(); // No answer found, but we're done
                    protein.NoteSearchFailure(WebSearchFailureReason.no_response);
                }
            }
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

        private static string NormalizeSpecies(string species)
        {
            if (string.IsNullOrEmpty(species))
                return null;
            var builder = new StringBuilder();
            foreach (var ch in species.ToUpperInvariant())
            {
                if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
                    builder.Append(ch);
                else if (ch == '/' || ch == '-' || ch == '_')
                    builder.Append(' ');
            }
            var tokens = builder.ToString()
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t != @"STR" && t != @"STRAIN")
                .ToArray();
            return CommonTextUtil.SpaceSeparate(tokens);
        }
    }
}
