/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.RetentionTimes
{
    public enum RegressionMethodRT { linear, kde, log, loess }

    /// <summary>
    /// Contains all the retention time alignments that are relevant for a <see cref="SrmDocument"/>
    /// </summary>
    [XmlRoot("doc_rt_alignments")]
    public class DocumentRetentionTimes : IXmlSerializable
    {
        public static readonly DocumentRetentionTimes EMPTY =
            new DocumentRetentionTimes(Array.Empty<RetentionTimeSource>(), Array.Empty<FileRetentionTimeAlignments>());
        public const double REFINEMENT_THRESHOLD = .99;
        public DocumentRetentionTimes(IEnumerable<RetentionTimeSource> sources, IEnumerable<FileRetentionTimeAlignments> fileAlignments)
            : this()
        {
            RetentionTimeSources = ResultNameMap.FromNamedElements(sources);
            FileAlignments = ResultNameMap.FromNamedElements(fileAlignments);
        }
        public DocumentRetentionTimes(SrmDocument document)
            : this()
        {
            RetentionTimeSources = ListAvailableRetentionTimeSources(document.Settings);
            FileAlignments = ResultNameMap<FileRetentionTimeAlignments>.EMPTY;
        }

        public bool IsEmpty
        {
            get { return RetentionTimeSources.IsEmpty && FileAlignments.IsEmpty; }
        }

        public static string IsNotLoadedExplained(SrmSettings srmSettings)
        {
            if (!srmSettings.PeptideSettings.Libraries.IsLoaded)
            {
                return null;
            }
            var documentRetentionTimes = srmSettings.DocumentRetentionTimes;
            var availableSources = ListAvailableRetentionTimeSources(srmSettings);
            var resultSources = ListSourcesForResults(srmSettings.MeasuredResults, availableSources);
            if (!Equals(resultSources.Keys, documentRetentionTimes.FileAlignments.Keys))
            {
                return @"DocumentRetentionTimes: !Equals(resultSources.Keys, documentRetentionTimes.FileAlignments.Keys)";
            }
            if (documentRetentionTimes.FileAlignments.IsEmpty)
            {
                return null;
            }
            if (!Equals(availableSources, documentRetentionTimes.RetentionTimeSources))
            {
                return @"DocumentRetentionTimes: !Equals(availableSources, documentRetentionTimes.RetentionTimeSources)";
            }
            return null;
        }

        public static string IsNotLoadedExplained(SrmDocument document)
        {
            return IsNotLoadedExplained(document.Settings);
        }

        public static bool IsLoaded(SrmDocument document)
        {
            return IsNotLoadedExplained(document) == null;
        }

        public static SrmDocument RecalculateAlignments(SrmDocument document, IProgressMonitor progressMonitor)
        {
            var newSources = ListAvailableRetentionTimeSources(document.Settings);
            var newResultsSources = ListSourcesForResults(document.Settings.MeasuredResults, newSources);
            var allLibraryRetentionTimes = ReadAllRetentionTimes(document, newSources);
            var newFileAlignments = new List<KeyValuePair<int, FileRetentionTimeAlignments>>();
            var pairsToAlign = GetPairsToAlign(newResultsSources, document.MeasuredResults, newSources);
            using var cancellationTokenSource = new PollingCancellationToken(() => progressMonitor.IsCanceled);
            IProgressStatus progressStatus = new ProgressStatus(RetentionTimesResources.DocumentRetentionTimes_RecalculateAlignments_Aligning_retention_times);
            ParallelEx.ForEach(newResultsSources.Values.Select(Tuple.Create<RetentionTimeSource, int>), tuple =>
            {
                {
                    if (progressMonitor.IsCanceled)
                    {
                        return;
                    }
                    try
                    {
                        var retentionTimeSource = tuple.Item1;
                        var fileAlignments = CalculateFileRetentionTimeAlignments(retentionTimeSource.Name,
                            allLibraryRetentionTimes, pairsToAlign, cancellationTokenSource.Token);
                        lock (newFileAlignments)
                        {
                            newFileAlignments.Add(new KeyValuePair<int, FileRetentionTimeAlignments>(tuple.Item2, fileAlignments));
                            progressStatus =
                                progressStatus.ChangePercentComplete(100 * newFileAlignments.Count / newResultsSources.Count);
                            progressMonitor.UpdateProgress(progressStatus);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // ignore
                    }
                }
            });
            if (progressMonitor.IsCanceled)
            {
                return null;
            }

            var newDocRt = new DocumentRetentionTimes(newSources.Values,
                newFileAlignments.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value));
            var newDocument = document.ChangeSettings(document.Settings.ChangeDocumentRetentionTimes(newDocRt));
            Debug.Assert(IsLoaded(newDocument));
            progressMonitor.UpdateProgress(progressStatus.Complete());
            return newDocument;
        }

        private static FileRetentionTimeAlignments CalculateFileRetentionTimeAlignments(
            string dataFileName, ResultNameMap<IDictionary<Target, MeasuredRetentionTime>> libraryRetentionTimes, 
            HashSet<Tuple<string, string>> pairsToAlign,
            CancellationToken cancellationToken)
        {
            var targetTimes = libraryRetentionTimes.Find(dataFileName);
            if (targetTimes == null)
            {
                return null;
            }
            var alignments = new List<RetentionTimeAlignment>();
            foreach (var entry in libraryRetentionTimes)
            {
                if (dataFileName == entry.Key)
                {
                    continue;
                }

                if (!pairsToAlign.Contains(Tuple.Create(dataFileName, entry.Key)) &&
                    !pairsToAlign.Contains(Tuple.Create(entry.Key, dataFileName)))
                {
                    continue;
                }
                var alignedFile = AlignedRetentionTimes.AlignLibraryRetentionTimes(targetTimes, entry.Value,
                    REFINEMENT_THRESHOLD, RegressionMethodRT.linear, cancellationToken);
                if (alignedFile == null || alignedFile.RegressionRefinedStatistics == null ||
                    !RetentionTimeRegression.IsAboveThreshold(alignedFile.RegressionRefinedStatistics.R, REFINEMENT_THRESHOLD))
                {
                    continue;
                }
                var regressionLine = alignedFile.RegressionRefined.Conversion as RegressionLineElement;
                if (regressionLine != null)
                    alignments.Add(new RetentionTimeAlignment(entry.Key, regressionLine));
            }
            return new FileRetentionTimeAlignments(dataFileName, alignments);
        }

        public ResultNameMap<FileRetentionTimeAlignments> FileAlignments { get; private set; }
        public ResultNameMap<RetentionTimeSource> RetentionTimeSources { get; private set; }

        #region Object Overrides
        public bool Equals(DocumentRetentionTimes other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.RetentionTimeSources, RetentionTimeSources) &&
                   Equals(other.FileAlignments, FileAlignments);

        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(DocumentRetentionTimes)) return false;
            return Equals((DocumentRetentionTimes)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = FileAlignments.GetHashCode();
                result = (result*397) ^ RetentionTimeSources.GetHashCode();
                return result;
            }
        }
        #endregion

        #region Implementation of IXmlSerializable
        /// <summary>
        /// For serialization
        /// </summary>
        private DocumentRetentionTimes()
        {
        }

        public static DocumentRetentionTimes Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new DocumentRetentionTimes());
        }
        public void ReadXml(XmlReader reader)
        {
            if (RetentionTimeSources != null || FileAlignments != null)
            {
                throw new InvalidOperationException();
            }
            var sources = new List<RetentionTimeSource>();
            var fileAlignments = new List<FileRetentionTimeAlignments>();
            if (reader.IsEmptyElement)
            {
                reader.Read();
            }
            else
            {
                reader.Read();
                reader.ReadElements(sources);
                reader.ReadElements(fileAlignments);
                reader.ReadEndElement();
            }
            RetentionTimeSources = ResultNameMap.FromNamedElements(sources);
            FileAlignments = ResultNameMap.FromNamedElements(fileAlignments);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteElements(RetentionTimeSources.Values);
            writer.WriteElements(FileAlignments.Values);
        }
        public XmlSchema GetSchema()
        {
            return null;
        }
        #endregion

        public static ResultNameMap<RetentionTimeSource> ListSourcesForResults(MeasuredResults results, ResultNameMap<RetentionTimeSource> availableSources)
        {
            if (results == null)
            {
                return ResultNameMap<RetentionTimeSource>.EMPTY;
            }
            var sourcesForResults = results.Chromatograms
                .SelectMany(chromatogramSet => chromatogramSet.MSDataFileInfos)
                .Select(availableSources.Find);
            return ResultNameMap.FromNamedElements(sourcesForResults.Where(source => null != source));
        }

        public static ResultNameMap<RetentionTimeSource> ListAvailableRetentionTimeSources(SrmSettings settings)
        {
            if (!settings.TransitionSettings.FullScan.IsEnabled)
            {
                return ResultNameMap<RetentionTimeSource>.EMPTY;
            }
            IEnumerable<RetentionTimeSource> sources = Array.Empty<RetentionTimeSource>();
            foreach (var library in settings.PeptideSettings.Libraries.Libraries)
            {
                if (library == null || !library.IsLoaded)
                {
                    continue;
                }
                sources = sources.Concat(library.ListRetentionTimeSources());
            }
            return ResultNameMap.FromNamedElements(sources);
        }
        public static ResultNameMap<IDictionary<Target, MeasuredRetentionTime>> ReadAllRetentionTimes(SrmDocument document, ResultNameMap<RetentionTimeSource> sources)
        {
            var allRetentionTimes = new Dictionary<string, IDictionary<Target, MeasuredRetentionTime>>();
            foreach (var source in sources)
            {
                var library = document.Settings.PeptideSettings.Libraries.GetLibrary(source.Value.Library);
                if (null == library)
                {
                    continue;
                }
                LibraryRetentionTimes libraryRetentionTimes;
                if (!library.TryGetRetentionTimes(MsDataFileUri.Parse(source.Value.Name), out libraryRetentionTimes))
                {
                    continue;
                }

                allRetentionTimes.Add(source.Key,
                    ConvertToMeasuredRetentionTimes(libraryRetentionTimes.GetFirstRetentionTimes()));
            }
            return ResultNameMap.FromDictionary(allRetentionTimes);
        }

        public static Dictionary<Target, MeasuredRetentionTime> ConvertToMeasuredRetentionTimes(IEnumerable<KeyValuePair<Target, double>> retentionTimes)
        {
            var dictionary = new Dictionary<Target, MeasuredRetentionTime>();
            foreach (var entry in retentionTimes)
            {
                try
                {
                    var measuredRetentionTime = new MeasuredRetentionTime(entry.Key, entry.Value);
                    dictionary.Add(entry.Key, measuredRetentionTime);
                }
                catch (Exception)
                {
                    // ignore
                }
            }

            return dictionary;
        }

        /// <summary>
        /// Returns the set of things that should be aligned against each other.
        /// This function figures out the "Primary Replicate" which is the first replicate in
        /// the document when ordered by <see cref="_alignmentPriorities"/>.
        /// (That is, the first internal standard, or, if there are no internal standards then the first
        /// ordinary replicate).
        /// All retention times are aligned against the primary replicate.
        /// In addition, within each <see cref="ChromatogramSet.BatchName"/>, all retention times within that
        /// batch are aligned against the primary replicate of that batch.
        /// </summary>
        public static HashSet<Tuple<string, string>> GetPairsToAlign(ResultNameMap<RetentionTimeSource> sourcesInDocument,
            MeasuredResults measuredResults, ResultNameMap<RetentionTimeSource> allSources)
        {
            var alignmentPairs = new HashSet<Tuple<string, string>>();
            if (measuredResults == null)
            {
                return alignmentPairs;
            }
            var replicateRetentionTimeSources = measuredResults.Chromatograms.ToDictionary(
                chromatogramSet => chromatogramSet,
                chromatogramSet => chromatogramSet.MSDataFileInfos.Select(sourcesInDocument.Find)
                    .Where(source => null != source)
                    .ToList());
            foreach (var tuple in GetReplicateAlignmentPairs(measuredResults.Chromatograms))
            {
                if (!replicateRetentionTimeSources.TryGetValue(tuple.Item1, out var sources1))
                {
                    continue;
                }

                if (!replicateRetentionTimeSources.TryGetValue(tuple.Item2, out var sources2))
                {
                    continue;
                }
                alignmentPairs.UnionWith(sources1.SelectMany(source1=>sources2.Select(source2=>Tuple.Create(source1.Name, source2.Name))));
            }

            // Also, align against the first replicate the things that are not in the document
            var primaryReplicate = measuredResults.Chromatograms
                .OrderBy(c => _alignmentPriorities[c.SampleType]).FirstOrDefault();
            if (primaryReplicate != null)
            {
                alignmentPairs.UnionWith(replicateRetentionTimeSources[primaryReplicate].SelectMany(source1 =>
                    allSources.Select(source2 => Tuple.Create(source1.Name, source2.Key))));
            }

            return alignmentPairs;
        }

        public static IEnumerable<Tuple<ChromatogramSet, ChromatogramSet>> GetReplicateAlignmentPairs(
            IEnumerable<ChromatogramSet> chromatogramSets)
        {
            List<ChromatogramSet> batchLeaders = new List<ChromatogramSet>();
            foreach (var batch in chromatogramSets.GroupBy(chromatogramSet => chromatogramSet.BatchName))
            {
                ChromatogramSet batchLeader = null;
                foreach (var chromatogramSet in batch.OrderBy(chromatogramSet => _alignmentPriorities[chromatogramSet.SampleType]))
                {
                    if (batchLeader == null)
                    {
                        batchLeader = chromatogramSet;
                        foreach (var otherLeader in batchLeaders)
                        {
                            yield return Tuple.Create(otherLeader, batchLeader);
                        }
                        batchLeaders.Add(batchLeader);
                    }
                    else
                    {
                        yield return Tuple.Create(batchLeader, chromatogramSet);
                    }
                }
            }
        }

        private static readonly Dictionary<SampleType, int> _alignmentPriorities = new Dictionary<SampleType, int>
        {
            {SampleType.STANDARD, 1},
            {SampleType.UNKNOWN, 2},
            {SampleType.QC, 2},
            {SampleType.BLANK, 3},
            {SampleType.DOUBLE_BLANK, 4},
            {SampleType.SOLVENT, 4}
        };

        public AlignmentFunction GetMappingFunction(string alignTo, string alignFrom, int maxStopovers)
        {
            var queue = new Queue<ImmutableList<KeyValuePair<string, RetentionTimeAlignment>>>();
            queue.Enqueue(ImmutableList<KeyValuePair<string, RetentionTimeAlignment>>.EMPTY);
            while (queue.Count > 0)
            {
                var list = queue.Dequeue();
                var name = list.LastOrDefault().Key ?? alignTo;
                var fileAlignment = FileAlignments.Find(name);
                if (fileAlignment == null)
                {
                    continue;
                }

                var endAlignment = fileAlignment.RetentionTimeAlignments.Find(alignFrom);
                if (endAlignment != null)
                {
                    return MakeAlignmentFunc(list.Select(tuple => tuple.Value.RegressionLine).Prepend(endAlignment.RegressionLine));
                }

                if (list.Count < maxStopovers)
                {
                    var excludeNames = list.Select(tuple => tuple.Key).ToHashSet();
                    foreach (var availableAlignment in fileAlignment.RetentionTimeAlignments)
                    {
                        if (!excludeNames.Contains(availableAlignment.Key))
                        {
                            queue.Enqueue(ImmutableList.ValueOf(list.Prepend(availableAlignment)));
                        }
                    }
                }
            }

            return null;
        }

        public static AlignmentFunction MakeAlignmentFunc(IEnumerable<RegressionLine> regressionLines)
        {

            return AlignmentFunction.FromParts(regressionLines.Select(line =>
                AlignmentFunction.Define(line.GetY, line.GetX)));
        }
    }
}
