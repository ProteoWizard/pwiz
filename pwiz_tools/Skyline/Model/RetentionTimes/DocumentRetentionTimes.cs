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
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.RetentionTimes
{
    public enum RegressionMethodRT { linear, kde, log, loess }

    /// <summary>
    /// Contains all of the retention time alignments that are relevant for a <see cref="SrmDocument"/>
    /// </summary>
    [XmlRoot("doc_rt_alignments")]
    public class DocumentRetentionTimes : IXmlSerializable
    {
        public static readonly DocumentRetentionTimes EMPTY = new DocumentRetentionTimes(new RetentionTimeSource[0], new FileRetentionTimeAlignments[0]);
        public const double REFINEMENT_THRESHHOLD = .99;
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
            var newFileAlignments = new List<FileRetentionTimeAlignments>();
            IProgressStatus progressStatus = new ProgressStatus(@"Aligning retention times"); // CONSIDER: localize?  Will users see this?
            foreach (var retentionTimeSource in newResultsSources.Values)
            {
                progressStatus = progressStatus.ChangePercentComplete(100*newFileAlignments.Count/newResultsSources.Count);
                progressMonitor.UpdateProgress(progressStatus);
                try
                {
                    var fileAlignments = CalculateFileRetentionTimeAlignments(retentionTimeSource.Name, allLibraryRetentionTimes, progressMonitor);
                    newFileAlignments.Add(fileAlignments);
                }
                catch (OperationCanceledException)
                {
                    progressMonitor.UpdateProgress(progressStatus.Cancel());
                    return null;
                }
            }
            var newDocRt = new DocumentRetentionTimes(newSources.Values, newFileAlignments);
            var newDocument = document.ChangeSettings(document.Settings.ChangeDocumentRetentionTimes(newDocRt));
            Debug.Assert(IsLoaded(newDocument));
            progressMonitor.UpdateProgress(progressStatus.Complete());
            return newDocument;
        }

        private static FileRetentionTimeAlignments CalculateFileRetentionTimeAlignments(
            string dataFileName, ResultNameMap<IDictionary<Target, double>> libraryRetentionTimes, IProgressMonitor progressMonitor)
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

                using (var tokenSource = new PollingCancellationToken(() => progressMonitor.IsCanceled))
                {
                    var alignedFile = AlignedRetentionTimes.AlignLibraryRetentionTimes(targetTimes, entry.Value,
                        REFINEMENT_THRESHHOLD, RegressionMethodRT.linear, tokenSource.Token);
                    if (alignedFile == null || alignedFile.RegressionRefinedStatistics == null ||
                        !RetentionTimeRegression.IsAboveThreshold(alignedFile.RegressionRefinedStatistics.R, REFINEMENT_THRESHHOLD))
                    {
                        continue;
                    }
                    var regressionLine = alignedFile.RegressionRefined.Conversion as RegressionLineElement;
                    if (regressionLine != null)
                        alignments.Add(new RetentionTimeAlignment(entry.Key, regressionLine));
                }
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
            IEnumerable<RetentionTimeSource> sources = new RetentionTimeSource[0];
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
        public static ResultNameMap<IDictionary<Target, double>> ReadAllRetentionTimes(SrmDocument document, ResultNameMap<RetentionTimeSource> sources)
        {
            var allRetentionTimes = new Dictionary<string, IDictionary<Target, double>>();
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
                allRetentionTimes.Add(source.Key, libraryRetentionTimes.GetFirstRetentionTimes());
            }
            return ResultNameMap.FromDictionary(allRetentionTimes);
        }
    }
}
