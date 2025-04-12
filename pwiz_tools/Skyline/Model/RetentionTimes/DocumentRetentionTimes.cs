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
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.Serialization;
using JetBrains.Annotations;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.RetentionTimes
{
    public enum RegressionMethodRT { linear, kde, log, loess }

    /// <summary>
    /// Contains all the retention time alignments that are relevant for a <see cref="SrmDocument"/>
    /// </summary>
    [XmlRoot("doc_rt_alignments")]
    public class DocumentRetentionTimes : Immutable, IXmlSerializable
    {
        public static readonly DocumentRetentionTimes EMPTY =
            new DocumentRetentionTimes(Array.Empty<RetentionTimeSource>(), Array.Empty<FileRetentionTimeAlignments>())
            {
                _libraryAlignments = new Dictionary<LibraryAlignmentKey, LibraryAlignmentValue>(),
                RetentionTimeSources = ResultNameMap<RetentionTimeSource>.EMPTY,
                FileAlignments = ResultNameMap<FileRetentionTimeAlignments>.EMPTY
            };
        public const double REFINEMENT_THRESHOLD = .99;
        public DocumentRetentionTimes(IEnumerable<RetentionTimeSource> sources, IEnumerable<FileRetentionTimeAlignments> fileAlignments)
            : this()
        {
            RetentionTimeSources = ResultNameMap.FromNamedElements(sources);
            FileAlignments = ResultNameMap.FromNamedElements(fileAlignments);
        }
        public bool IsEmpty
        {
            get { return RetentionTimeSources.IsEmpty && FileAlignments.IsEmpty && _libraryAlignments.Count == 0; }
        }

        public static string IsNotLoadedExplained(SrmSettings srmSettings)
        {
            if (!AlignmentTarget.TryGetAlignmentTarget(srmSettings, out _))
            {
                return null;
            }
            if (!srmSettings.PeptideSettings.Libraries.IsLoaded)
            {
                return null;
            }

            if (srmSettings.DocumentRetentionTimes.UpdateFromLoadedSettings(srmSettings) != null)
            {
                return nameof(DocumentRetentionTimes) + " need to update from loaded settings";
            }
            var unloadedLibraries = srmSettings.DocumentRetentionTimes.GetMissingAlignments(srmSettings).Select(param => param.Key.ToString()).ToList();
            if (unloadedLibraries.Count == 0)
            {
                
                return null;
            }

            return nameof(DocumentRetentionTimes) + @": " + TextUtil.SpaceSeparate(unloadedLibraries);
        }

        public static string IsNotLoadedExplained(SrmDocument document)
        {
            return IsNotLoadedExplained(document.Settings);
        }

        public static bool IsLoaded(SrmDocument document)
        {
            return IsNotLoadedExplained(document) == null;
        }


        public ResultNameMap<FileRetentionTimeAlignments> FileAlignments { get; private set; }
        public ResultNameMap<RetentionTimeSource> RetentionTimeSources { get; private set; }

        #region Object Overrides
        public bool Equals(DocumentRetentionTimes other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.RetentionTimeSources, RetentionTimeSources) &&
                   Equals(other.FileAlignments, FileAlignments)
                   && CollectionUtil.EqualsDeep(_libraryAlignments, other._libraryAlignments);

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
                result = (result * 397) ^ CollectionUtil.GetHashCodeDeep(_libraryAlignments);
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

        private enum EL
        {
            alignments,
            alignment,
            x,
            y
        }

        private enum ATTR
        {
            library,
            file,
            batch
        }



        public static DocumentRetentionTimes Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new DocumentRetentionTimes());
        }
        public void ReadXml(XmlReader reader)
        {
            if (_libraryAlignments != null)
            {
                throw new InvalidOperationException();
            }
            var xElement = (XElement) XNode.ReadFrom(reader);
            var libraryAlignments = new Dictionary<LibraryAlignmentKey, LibraryAlignmentValue>();
            foreach (var elLibrary in xElement.Elements(EL.alignments))
            {
                var key = new LibraryAlignmentKey(elLibrary.Attribute(ATTR.library)?.Value, elLibrary.Attribute(ATTR.batch)?.Value);
                var alignments = new Dictionary<string, PiecewiseLinearMap>();
                foreach (var elAlignment in elLibrary.Elements(EL.alignment))
                {
                    var xValues = PrimitiveArrays.FromBytes<double>(Convert.FromBase64String(elAlignment.Elements(EL.x).First().Value));
                    var yValues = PrimitiveArrays.FromBytes<double>(Convert.FromBase64String(elAlignment.Elements(EL.y).First().Value));
                    alignments.Add(elAlignment.Attribute(ATTR.file).Value, PiecewiseLinearMap.FromValues(xValues, yValues));
                }
                libraryAlignments.Add(key, new LibraryAlignmentValue(null, new LibraryAlignments(alignments)));
            }
            _libraryAlignments = libraryAlignments;
            FileAlignments = ResultNameMap<FileRetentionTimeAlignments>.EMPTY;
            RetentionTimeSources = ResultNameMap<RetentionTimeSource>.EMPTY;
        }

        public void WriteXml(XmlWriter writer)
        {
            foreach (var entry in _libraryAlignments)
            {
                writer.WriteStartElement(EL.alignments);
                writer.WriteAttributeIfString(ATTR.library, entry.Key.LibraryName);
                writer.WriteAttributeIfString(ATTR.batch, entry.Key.BatchName);
                foreach (var alignment in entry.Value.LibraryAlignments.GetAllAlignmentFunctions().OrderBy(kvp=>kvp.Key))
                {
                    writer.WriteStartElement(EL.alignment);
                    writer.WriteAttribute(ATTR.file, alignment.Key);
                    var piecewiseLinearMap = alignment.Value;
                    writer.WriteStartElement(EL.x);
                    writer.WriteValue(Convert.ToBase64String(PrimitiveArrays.ToBytes(piecewiseLinearMap.XValues.ToArray())));
                    writer.WriteEndElement();
                    writer.WriteStartElement(EL.y);
                    writer.WriteValue(Convert.ToBase64String(PrimitiveArrays.ToBytes(piecewiseLinearMap.YValues.ToArray())));
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
        }
        public XmlSchema GetSchema()
        {
            return null;
        }
        #endregion

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

        public Dictionary<string, AlignmentFunction> GetAllMappingFunctions(FileRetentionTimeAlignments alignTo, int maxStopovers)
        {
            Dictionary<string, AlignmentFunction> alignmentFunctions = new Dictionary<string, AlignmentFunction>();
            foreach (var source in RetentionTimeSources.Values)
            {
                if (alignTo.Name == source.Name)
                {
                    continue;
                }

                var alignmentFunction = GetMappingFunction(alignTo.Name, source.Name, maxStopovers);
                if (alignmentFunction != null)
                {
                    alignmentFunctions[source.Name] = alignmentFunction;
                }
            }

            return alignmentFunctions;
        }

        public RetentionTimeAlignmentIndexes GetRetentionTimeAlignmentIndexes(string name)
        {
            var file = FileAlignments.Find(name);
            if (file == null)
            {
                return null;
            }

            return new RetentionTimeAlignmentIndexes(GetAllMappingFunctions(file, 3)
                .Select(kvp => new RetentionTimeAlignmentIndex(kvp.Key, kvp.Value)));
        }

        public static AlignmentFunction MakeAlignmentFunc(IEnumerable<RegressionLine> regressionLines)
        {

            return AlignmentFunction.FromParts(regressionLines.Select(line =>
                AlignmentFunction.Define(line.GetY, line.GetX)));
        }

        private Dictionary<LibraryAlignmentKey, LibraryAlignmentValue> _libraryAlignments;

        public LibraryAlignments GetLibraryAlignments(LibraryAlignmentKey key)
        {
            _libraryAlignments.TryGetValue(key, out var libraryAlignments);
            return libraryAlignments?.LibraryAlignments;
        }

        public DocumentRetentionTimes ChangeLibraryAlignments(LibraryAlignmentParam alignmentParam, LibraryAlignments alignments)
        {
            var newEntries = _libraryAlignments.Where(kvp => !alignmentParam.Key.Equals(kvp.Key));
            if (alignments != null)
            {
                newEntries = newEntries.Append(new KeyValuePair<LibraryAlignmentKey, LibraryAlignmentValue>(
                    alignmentParam.Key, new LibraryAlignmentValue(alignmentParam, alignments)));
            }

            return ChangeProp(ImClone(this),
                im => im._libraryAlignments = newEntries.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        }

        public DocumentRetentionTimes DumpStaleAlignments(SrmSettings settings)
        {
            if (_libraryAlignments.Count == 0)
            {
                return this;
            }

            var newParams = GetAlignmentParams(settings);
            var newAlignments = _libraryAlignments.Where(kvp =>
                    newParams.TryGetValue(kvp.Key, out var paramValue) && false != paramValue?.Equals(kvp.Value.Param))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            if (newAlignments.Count == _libraryAlignments.Count)
            {
                return this;
            }

            return ChangeProp(ImClone(this), im => im._libraryAlignments = newAlignments);
        }

        public IEnumerable<LibraryAlignmentParam> GetMissingAlignments(SrmSettings settings)
        {
            var dictParams = GetAlignmentParams(settings);
            if (dictParams == null)
            {
                yield break;
            }

            foreach (var kvp in dictParams)
            {
                if (kvp.Value != null && !_libraryAlignments.ContainsKey(kvp.Key))
                {
                    yield return kvp.Value;
                }
            }
        }

        public Dictionary<LibraryAlignmentKey, LibraryAlignmentParam> GetAlignmentParams(SrmSettings settings)
        {
            if (!AlignmentTarget.TryGetAlignmentTarget(settings, out var alignmentTarget))
            {
                return null;
            }
            var dict = new Dictionary<LibraryAlignmentKey, LibraryAlignmentParam>();
            if (!settings.HasResults || alignmentTarget == null)
            {
                return dict;
            }

            var peptideLibraries = settings.PeptideSettings.Libraries;
            if (peptideLibraries.Libraries.Count == 0)
            {
                return dict;
            }
            foreach (var grouping in settings.MeasuredResults.Chromatograms.GroupBy(chrom => chrom.BatchName))
            {
                for (int iLibrary = 0; iLibrary < peptideLibraries.Libraries.Count; iLibrary++)
                {
                    var library = peptideLibraries.Libraries[iLibrary];
                    var libraryName = library?.Name ?? peptideLibraries.LibrarySpecs[iLibrary]?.Name;
                    if (libraryName == null)
                    {
                        continue;
                    }
                    var libraryKey = new LibraryAlignmentKey(libraryName, grouping.Key);
                    if (true != library?.IsLoaded)
                    {
                        dict.Add(libraryKey, null);
                        continue;
                    }
                    if (string.IsNullOrEmpty(grouping.Key))
                    {
                        dict.Add(libraryKey, new LibraryAlignmentParam(alignmentTarget, libraryKey, library, null));
                    }
                    else
                    {
                        var spectrumSourceFiles = grouping.SelectMany(chrom => chrom.MSDataFilePaths)
                            .Select(library.LibraryFiles.FindIndexOf).Where(i => i >= 0).OrderBy(i => i)
                            .Select(i => library.LibraryFiles.FilePaths[i]).ToImmutable();
                        dict.Add(libraryKey, new LibraryAlignmentParam(alignmentTarget, libraryKey, library, spectrumSourceFiles));
                    }
                }
            }

            return dict;
        }

        public Dictionary<LibraryAlignmentKey, LibraryAlignmentParam> GetAlignmentParams(LibrarySpec librarySpec, Library library, MeasuredResults measuredResults)
        {
            var dict = new Dictionary<LibraryAlignmentKey, LibraryAlignmentParam>();
            return dict;
        }

        public class LibraryAlignmentKey
        {
            public LibraryAlignmentKey(string libraryName, string batchName)
            {
                LibraryName = libraryName;
                BatchName = string.IsNullOrEmpty(batchName) ? null : batchName;
            }
            public string LibraryName { get; private set; }
            public string BatchName { get; private set; }

            protected bool Equals(LibraryAlignmentKey other)
            {
                return LibraryName == other.LibraryName && BatchName == other.BatchName;
            }

            public override bool Equals(object obj)
            {
                if (obj is null) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((LibraryAlignmentKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((LibraryName != null ? LibraryName.GetHashCode() : 0) * 397) ^ (BatchName != null ? BatchName.GetHashCode() : 0);
                }
            }

            public override string ToString()
            {
                if (BatchName == null)
                {
                    return LibraryName;
                }

                return LibraryName + " (" + BatchName + ")";
            }
        }

        public class LibraryAlignmentParam
        {
            public LibraryAlignmentParam(AlignmentTarget alignmentTarget, LibraryAlignmentKey key, Library library, ImmutableList<string> spectrumSourceFileSubset)
            {
                AlignmentTarget = alignmentTarget;
                Key = key;
                Library = library;
                Assume.AreEqual(Library.Name, Key.LibraryName);
                SpectrumSourceFileSubset = spectrumSourceFileSubset;

            }
            public AlignmentTarget AlignmentTarget { get; }
            public LibraryAlignmentKey Key { get; private set; }
            public Library Library { get; private set; }
            [CanBeNull]
            public ImmutableList<string> SpectrumSourceFileSubset { get; private set; }

            protected bool Equals(LibraryAlignmentParam other)
            {
                return Equals(AlignmentTarget, other.AlignmentTarget) && Equals(Key, other.Key) && Equals(Library, other.Library) && Equals(SpectrumSourceFileSubset, other.SpectrumSourceFileSubset);
            }

            public override bool Equals(object obj)
            {
                if (obj is null) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((LibraryAlignmentParam)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = (AlignmentTarget != null ? AlignmentTarget.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (Key != null ? Key.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (Library != null ? Library.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (SpectrumSourceFileSubset != null ? SpectrumSourceFileSubset.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }

        private class LibraryAlignmentValue
        {
            public LibraryAlignmentValue(LibraryAlignmentParam alignmentParam, LibraryAlignments libraryAlignments)
            {
                Param = alignmentParam;
                LibraryAlignments = libraryAlignments;
            }

            public LibraryAlignmentParam Param { get; }
            public LibraryAlignments LibraryAlignments { get; }
        }

        public static LibraryAlignments PerformAlignment(ILoadMonitor loadMonitor, ref IProgressStatus progressStatus, LibraryAlignmentParam alignmentParam)
        {
            var library = alignmentParam.Library;
            var spectrumSourceFiles = alignmentParam.SpectrumSourceFileSubset ?? library.LibraryFiles.FilePaths;
            var allRetentionTimes = library.GetAllRetentionTimes(alignmentParam.SpectrumSourceFileSubset);
            if (allRetentionTimes == null)
            {
                return LibraryAlignments.EMPTY;
            }

            using var pollingCancellationToken = new PollingCancellationToken(() => loadMonitor.IsCanceled) { PollingInterval = 1000 };
            var alignmentFunctions = new KeyValuePair<string, PiecewiseLinearMap>[allRetentionTimes.Length];
            int completedCount = 0;
            IProgressStatus localProgressStatus = progressStatus;
            ParallelEx.For(0, alignmentFunctions.Length, iFile =>
            {
                var alignment = alignmentParam.AlignmentTarget.PerformAlignment(allRetentionTimes[iFile], pollingCancellationToken.Token);
                if (loadMonitor.IsCanceled)
                {
                    return;
                }
                lock (alignmentFunctions)
                {
                    alignmentFunctions[iFile] = new KeyValuePair<string, PiecewiseLinearMap>(spectrumSourceFiles[iFile], alignment);
                    loadMonitor.UpdateProgress(localProgressStatus =
                        localProgressStatus.ChangePercentComplete(100 * completedCount++ / alignmentFunctions.Length));
                }
            });
            progressStatus = localProgressStatus;
            return new LibraryAlignments(alignmentFunctions);
        }

        public DocumentRetentionTimes UpdateFromLoadedSettings(SrmSettings settings)
        {
            if (_libraryAlignments.Count == 0)
            {
                return null;
            }
            var newParams = GetAlignmentParams(settings);
            if (newParams == null)
            {
                return null;
            }
            var newLibraries = new Dictionary<LibraryAlignmentKey, LibraryAlignmentValue>();
            bool anyChanges = false;
            foreach (var entry in _libraryAlignments)
            {
                var key = entry.Key;
                if (!newParams.TryGetValue(key, out var param))
                {
                    anyChanges = true;
                    continue;
                }

                if (Equals(entry.Value.Param, param))
                {
                    newLibraries.Add(key, entry.Value);
                    continue;
                }

                anyChanges = true;
                if (entry.Value.Param == null)
                {
                    newLibraries.Add(key, new LibraryAlignmentValue(param, entry.Value.LibraryAlignments));
                }
            }

            if (anyChanges)
            {
                return ChangeProp(ImClone(this), im => im._libraryAlignments = newLibraries);
            }

            return null;
        }

        public AlignmentFunction GetAlignmentFunction(PeptideLibraries peptideLibraries, MsDataFileUri filePath, string batchName, bool forward)
        {
            if (string.IsNullOrEmpty(batchName))
            {
                batchName = null;
            }

            foreach (var library in peptideLibraries.Libraries.Where(lib => true == lib?.IsLoaded))
            {
                var key = new LibraryAlignmentKey(library.Name, batchName);
                if (!_libraryAlignments.TryGetValue(key, out var value))
                {
                    continue;
                }

                return value.LibraryAlignments.GetAlignmentFunction(filePath, forward);
            }
            return null;
        }
    }
}
