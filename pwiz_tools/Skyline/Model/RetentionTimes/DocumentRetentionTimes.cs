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
            new DocumentRetentionTimes()
            {
                _libraryAlignments = new Dictionary<string, LibraryAlignmentValue>(),
            };
        public const double REFINEMENT_THRESHOLD = .99;
        public bool IsEmpty
        {
            get { return _libraryAlignments.Count == 0 && ResultFileAlignments.IsEmpty;  }
        }

        private static string IsNotLoadedExplained(SrmSettings srmSettings)
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
                return nameof(DocumentRetentionTimes) + @" need to update from loaded settings";
            }
            var unloadedLibraries = srmSettings.DocumentRetentionTimes.GetMissingAlignments(srmSettings).Select(param => param.LibraryName).ToList();
            if (unloadedLibraries.Count == 0)
            {
                
                return null;
            }

            return TextUtil.ColonSeparate(nameof(DocumentRetentionTimes), TextUtil.SpaceSeparate(unloadedLibraries));
        }

        public static string IsNotLoadedExplained(SrmDocument document)
        {
            if (null == AlignmentTarget.GetAlignmentTarget(document))
            {
                return null;
            }
            var notLoaded = IsNotLoadedExplained(document.Settings);
            if (notLoaded != null)
            {
                return notLoaded;
            }
            var documentRetentionTimes = document.Settings.DocumentRetentionTimes;
            if (!documentRetentionTimes.ResultFileAlignments.IsUpToDate(document))
            {
                return nameof(ResultFileAlignments);
            }

            return null;
        }

        public static bool IsReadyForReintegration(SrmDocument document)
        {
            var documentRetentionTimes = document.Settings.DocumentRetentionTimes;
            return documentRetentionTimes.ResultFileAlignments.IsUpToDate(document);
        }

        public static bool IsReadyForChromatogramExtraction(SrmDocument document)
        {
            return null == IsNotLoadedExplained(document.Settings);
        }

        public static bool IsLoaded(SrmDocument document)
        {
            return IsNotLoadedExplained(document) == null;
        }

        public ResultFileAlignments ResultFileAlignments { get; private set; } = ResultFileAlignments.EMPTY;

        #region Object Overrides
        public bool Equals(DocumentRetentionTimes other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return CollectionUtil.EqualsDeep(_libraryAlignments, other._libraryAlignments)
                   && Equals(ResultFileAlignments, other.ResultFileAlignments);

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
                int result = ResultFileAlignments.GetHashCode();
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
            measured,
            aligned
        }

        private enum ATTR
        {
            library,
            file,
            length
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
            var libraryAlignments = new Dictionary<string, LibraryAlignmentValue>();
            foreach (var elLibrary in xElement.Elements(EL.alignments))
            {
                var alignments = new Dictionary<string, PiecewiseLinearMap>();
                foreach (var elAlignment in elLibrary.Elements(EL.alignment))
                {
                    var elMeasured = elAlignment.Elements(EL.measured).FirstOrDefault();
                    var length = elAlignment.GetNullableInt(ATTR.length);
                    if (elMeasured != null)
                    {
                        var yValues = BytesToDoubles(length, Convert.FromBase64String(elMeasured.Value));
                        var xValues = BytesToDoubles(length, Convert.FromBase64String(elAlignment.Elements(EL.aligned).First().Value));
                        alignments.Add(elAlignment.Attribute(ATTR.file).Value, PiecewiseLinearMap.FromValues(xValues, yValues));
                    }
                }
                var libraryName = elLibrary.Attribute(ATTR.library)?.Value;
                if (libraryName == null)
                {
                    _deserializedAlignmentFunctions = alignments.ToDictionary(kvp => MsDataFileUri.Parse(kvp.Key), kvp => kvp.Value);
                }
                else
                {
                    libraryAlignments.Add(libraryName, new LibraryAlignmentValue(null, new Alignments(null, alignments)));
                }
            }
            _libraryAlignments = libraryAlignments;
        }

        private double[] BytesToDoubles(int? length, byte[] bytes)
        {
            if (!length.HasValue || bytes.Length == length * sizeof(double))
            {
                return PrimitiveArrays.FromBytes<double>(bytes);
            }

            if (bytes.Length != length * sizeof(int))
            {
                throw new ArgumentException();
            }

            return PrimitiveArrays.FromBytes<float>(bytes).Select(f => (double)f).ToArray();
        }

        public void WriteXml(XmlWriter writer)
        {
            WriteAlignments(writer, null,
                ResultFileAlignments.GetAlignmentFunctions().Select(kvp =>
                    new KeyValuePair<string, PiecewiseLinearMap>(kvp.Key.ToString(), kvp.Value)));
            foreach (var entry in _libraryAlignments)
            {
                WriteAlignments(writer, entry.Key, entry.Value.Alignments.GetAllAlignmentFunctions());
            }
        }

        private void WriteAlignments(XmlWriter writer, string libraryName,
            IEnumerable<KeyValuePair<string, PiecewiseLinearMap>> alignments)
        {
            var orderedAlignments = alignments.OrderBy(kvp => kvp.Key).Where(kvp=>kvp.Value != null).ToList();
            if (orderedAlignments.Count == 0)
            {
                return;
            }
            writer.WriteStartElement(EL.alignments);
            writer.WriteAttributeIfString(ATTR.library, libraryName);
            foreach (var alignment in orderedAlignments)
            {
                WriteAlignment(writer, alignment.Key, alignment.Value);
            }
            writer.WriteEndElement();
        }

        private void WriteAlignment(XmlWriter writer, string key, PiecewiseLinearMap piecewiseLinearMap)
        {
            writer.WriteStartElement(EL.alignment);
            writer.WriteAttribute(ATTR.file, key);
            writer.WriteAttribute(ATTR.length, piecewiseLinearMap.Count);
            writer.WriteStartElement(EL.measured);
            writer.WriteValue(Convert.ToBase64String(PrimitiveArrays.ToBytes(piecewiseLinearMap.YValues.Select(y=>(float) y).ToArray())));
            writer.WriteEndElement();
            writer.WriteStartElement(EL.aligned);
            writer.WriteValue(Convert.ToBase64String(PrimitiveArrays.ToBytes(piecewiseLinearMap.XValues.Select(x => (float) x).ToArray())));
            writer.WriteEndElement();
            writer.WriteEndElement();
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

        public static AlignmentFunction MakeAlignmentFunc(IEnumerable<RegressionLine> regressionLines)
        {

            return AlignmentFunction.FromParts(regressionLines.Select(line =>
                AlignmentFunction.Define(line.GetY, line.GetX)));
        }

        private Dictionary<string, LibraryAlignmentValue> _libraryAlignments;

        public LibraryAlignment GetLibraryAlignment(string libraryName)
        {
            _libraryAlignments.TryGetValue(libraryName, out var alignmentValue);
            return alignmentValue?.LibraryAlignment;
        }

        public DocumentRetentionTimes ChangeLibraryAlignments(LibraryAlignmentParam alignmentParam, Alignments alignments)
        {
            var newEntries = _libraryAlignments.Where(kvp => !alignmentParam.LibraryName.Equals(kvp.Key));
            if (alignments != null)
            {
                newEntries = newEntries.Append(new KeyValuePair<string, LibraryAlignmentValue>(
                    alignmentParam.LibraryName, new LibraryAlignmentValue(alignmentParam, alignments)));
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

        public Dictionary<string, LibraryAlignmentParam> GetAlignmentParams(SrmSettings settings)
        {
            if (!AlignmentTarget.TryGetAlignmentTarget(settings, out var alignmentTarget))
            {
                return null;
            }
            var dict = new Dictionary<string, LibraryAlignmentParam>();
            if (!settings.HasResults || alignmentTarget == null)
            {
                return dict;
            }

            var peptideLibraries = settings.PeptideSettings.Libraries;
            if (peptideLibraries.Libraries.Count == 0)
            {
                return dict;
            }
            for (int iLibrary = 0; iLibrary < peptideLibraries.Libraries.Count; iLibrary++)
            {
                var library = peptideLibraries.Libraries[iLibrary];
                var libraryName = library?.Name ?? peptideLibraries.LibrarySpecs[iLibrary]?.Name;
                if (libraryName == null)
                {
                    continue;
                }
                if (true != library?.IsLoaded)
                {
                    dict.Add(libraryName, null);
                    continue;
                }
                dict.Add(libraryName, new LibraryAlignmentParam(alignmentTarget, library, null));
            }

            return dict;
        }

        public class LibraryAlignmentParam : Immutable
        {
            public LibraryAlignmentParam(AlignmentTarget alignmentTarget, Library library, ImmutableList<string> spectrumSourceFileSubset)
            {
                AlignmentTarget = alignmentTarget;
                Library = library;
                SpectrumSourceFileSubset = spectrumSourceFileSubset;

            }
            public AlignmentTarget AlignmentTarget { get; }
            public string LibraryName
            {
                get { return Library.Name; }
            }
            public Library Library { get; private set; }

            public LibraryAlignmentParam ChangeLibrary(Library library)
            {
                return ChangeProp(ImClone(this), im => im.Library = library);
            }
            [CanBeNull]
            public ImmutableList<string> SpectrumSourceFileSubset { get; private set; }

            protected bool Equals(LibraryAlignmentParam other)
            {
                return Equals(AlignmentTarget, other.AlignmentTarget) && Equals(Library, other.Library) && Equals(SpectrumSourceFileSubset, other.SpectrumSourceFileSubset);
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
                    hashCode = (hashCode * 397) ^ (Library != null ? Library.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (SpectrumSourceFileSubset != null ? SpectrumSourceFileSubset.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }

        private class LibraryAlignmentValue
        {
            public LibraryAlignmentValue(LibraryAlignmentParam alignmentParam, Alignments alignments)
            {
                Param = alignmentParam;
                Alignments = alignments;
            }

            public LibraryAlignmentParam Param { get; }
            public Alignments Alignments { get; }

            public LibraryAlignment LibraryAlignment
            {
                get
                {
                    return Param == null ? null : new LibraryAlignment(Param.Library, Alignments);
                }
            }

            protected bool Equals(LibraryAlignmentValue other)
            {
                return Equals(Param, other.Param) && Equals(Alignments, other.Alignments);
            }

            public override bool Equals(object obj)
            {
                if (obj is null) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((LibraryAlignmentValue)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Param != null ? Param.GetHashCode() : 0) * 397) ^ (Alignments != null ? Alignments.GetHashCode() : 0);
                }
            }
        }

        public static Alignments PerformAlignment(ILoadMonitor loadMonitor, ref IProgressStatus progressStatus, LibraryAlignmentParam alignmentParam)
        {
            var library = alignmentParam.Library;
            var spectrumSourceFiles = alignmentParam.SpectrumSourceFileSubset ?? library.LibraryFiles.FilePaths;
            var allRetentionTimes = library.GetAllRetentionTimes(alignmentParam.SpectrumSourceFileSubset);
            if (allRetentionTimes == null)
            {
                return Alignments.EMPTY;
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
            }, threadName: nameof(PerformAlignment));
            progressStatus = localProgressStatus;
            return new Alignments(null, alignmentFunctions);
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
            var newLibraries = new Dictionary<string, LibraryAlignmentValue>();
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
                    newLibraries.Add(key, new LibraryAlignmentValue(param, entry.Value.Alignments));
                }
            }

            if (anyChanges)
            {
                return ChangeProp(ImClone(this), im => im._libraryAlignments = newLibraries);
            }

            return null;
        }

        public DocumentRetentionTimes UpdateFromDeserializedDocument(SrmDocument document)
        {
            if (_deserializedAlignmentFunctions == null)
            {
                return this;
            }
            return ChangeProp(ImClone(this), im =>
            {
                im.ResultFileAlignments = new ResultFileAlignments(document, _deserializedAlignmentFunctions, GetDataFilesWithoutLibraryAlignments(document.MeasuredResults).ToHashSet());
                im._deserializedAlignmentFunctions = null;
            });
        }

        public AlignmentFunction GetAlignmentFunction(PeptideLibraries peptideLibraries, MsDataFileUri filePath, bool forward)
        {
            foreach (var library in peptideLibraries.Libraries.Where(lib => true == lib?.IsLoaded))
            {
                if (!_libraryAlignments.TryGetValue(library.Name, out var value))
                {
                    continue;
                }

                var alignmentFunction = value.Alignments.GetAlignmentFunction(filePath, forward);
                if (alignmentFunction != null)
                {
                    return alignmentFunction;
                }
            }

            return ResultFileAlignments.GetAlignmentFunction(filePath)?.ToAlignmentFunction(forward);
        }

        public IEnumerable<MsDataFileUri> GetDataFilesWithoutLibraryAlignments(MeasuredResults measuredResults)
        {
            if (measuredResults == null)
            {
                yield break;
            }

            foreach (var msDataFileUri in measuredResults.MSDataFilePaths.Distinct())
            {
                if (_libraryAlignments.Values.All(file => file.Alignments.GetAlignmentFunction(msDataFileUri, true) == null))
                {
                    yield return msDataFileUri;
                }
            }
        }

        public DocumentRetentionTimes UpdateResultFileAlignments(ILoadMonitor loadMonitor,
            ref IProgressStatus progressStatus, SrmDocument document)
        {
            if (ResultFileAlignments.IsUpToDate(document))
            {
                return this;
            }
            var newAlignments = ResultFileAlignments.ChangeDocument(AlignmentTarget.GetAlignmentTarget(document),
                document, GetDataFilesWithoutLibraryAlignments(document.MeasuredResults).ToHashSet(), loadMonitor,
                ref progressStatus);
            return ChangeProp(ImClone(this), im =>
            {
                im.ResultFileAlignments = newAlignments;
            });
        }

        /// <summary>
        /// Replace the entries for a library with a new library. This is used when we know that a library has been
        /// renamed but is otherwise unchanged.
        /// </summary>
        public DocumentRetentionTimes ChangeLibrary(Library oldLibrary, Library newLibrary)
        {
            if (!_libraryAlignments.TryGetValue(oldLibrary.Name, out var oldEntry))
            {
                return this;
            }


            var newLibraryAlignments = _libraryAlignments.Where(entry => entry.Key != oldLibrary.Name)
                .ToDictionary(entry => entry.Key, entry => entry.Value);
            newLibraryAlignments[newLibrary.Name] =
                new LibraryAlignmentValue(oldEntry.Param.ChangeLibrary(newLibrary), oldEntry.Alignments);
            return ChangeProp(ImClone(this), im => im._libraryAlignments = newLibraryAlignments);
        }

        private Dictionary<MsDataFileUri, PiecewiseLinearMap> _deserializedAlignmentFunctions;
    }
}
