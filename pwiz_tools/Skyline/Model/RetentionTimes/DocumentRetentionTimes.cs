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
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.Skyline.Model.DocSettings;
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
            new DocumentRetentionTimes
            {
                _libraryAlignments = new Dictionary<string, LibraryAlignment>(),
            };
        public const double REFINEMENT_THRESHOLD = .99;

        public bool AnyAlignments()
        {
            return AnyLibraryAlignments() || ResultFileAlignments.GetAlignmentFunctions().Any(kvp => null == kvp.Value);
        }
        public bool AnyLibraryAlignments()
        {
            return _libraryAlignments.Values.Any(libraryAlignmentValue=>libraryAlignmentValue.Alignments.GetAllAlignmentFunctions().Any());
        }

        public bool AnyLibraryAlignmentsForFiles(IEnumerable<MsDataFileUri> dataFileUris)
        {
            if (!AnyLibraryAlignments() || dataFileUris == null)
            {
                return false;
            }

            return dataFileUris.Any(file => _libraryAlignments.Values.Any(libraryAlignmentValue =>
                libraryAlignmentValue.Alignments.GetAlignmentFunction(file, true) != null));
        }

        public bool HasUnalignedTimes()
        {
            foreach (var libraryAlignment in _libraryAlignments.Values)
            {
                var alignments = libraryAlignment.Alignments;
                var library = libraryAlignment.Library;
                if (true == library?.IsLoaded && alignments.LibraryFiles.Count != library.LibraryFiles.Count && library.ListRetentionTimeSources().Any())
                {
                    return true;
                }
            }

            return false;
        }

        public AlignmentTarget.MedianDocumentRetentionTimes MedianDocumentRetentionTimes { get; private set; }

        public AlignmentTarget AlignmentTarget { get; private set; }

        private static string IsNotLoadedExplained(AlignmentTarget alignmentTarget, SrmSettings srmSettings)
        {
            if (!srmSettings.PeptideSettings.Libraries.IsLoaded)
            {
                return null;
            }

            if (srmSettings.DocumentRetentionTimes.UpdateFromLoadedSettings(alignmentTarget, srmSettings) != null)
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
            var targetSpec = document.Settings.GetAlignmentTargetSpec();
            if (!targetSpec.TryGetAlignmentTarget(document, out var target))
            {
                return null;
            }
            var notLoaded = IsNotLoadedExplained(target, document.Settings);
            if (notLoaded != null)
            {
                return notLoaded;
            }
            var documentRetentionTimes = document.Settings.DocumentRetentionTimes;
            if (target == null)
            {
                if (documentRetentionTimes.AlignmentTarget == null)
                {
                    return null;
                }

                if (Equals(ResultFileAlignments.EMPTY, documentRetentionTimes.ResultFileAlignments))
                {
                    return null;
                }

                return nameof(documentRetentionTimes.ResultFileAlignments);
            }
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
            return null == IsNotLoadedExplained(document);
        }

        public static bool IsLoaded(SrmDocument document)
        {
            return IsNotLoadedExplained(document) == null;
        }

        public ResultFileAlignments ResultFileAlignments { get; private set; } = ResultFileAlignments.EMPTY;

        public bool Equivalent(DocumentRetentionTimes other)
        {
            if (Equals(other))
            {
                return true;
            }

            var thisUnloaded = UnloadLibraries();
            var otherUnloaded = other.UnloadLibraries();
            if (Equals(thisUnloaded, otherUnloaded))
            {
                return true;
            }

            return false;
        }

        private DocumentRetentionTimes UnloadLibraries()
        {
            return ChangeProp(ImClone(this), im =>
            {
                im._libraryAlignments = _libraryAlignments.ToDictionary(kvp => kvp.Key,
                    kvp => new LibraryAlignment(null, kvp.Value.Alignments));
                im._deserializedAlignmentFunctions ??= CollectionUtil.SafeToDictionary(im.ResultFileAlignments.GetAlignmentFunctions());
                im.ResultFileAlignments = ResultFileAlignments.EMPTY;
            });
        }
        #region Object Overrides
        public bool Equals(DocumentRetentionTimes other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return CollectionUtil.EqualsDeep(_libraryAlignments, other._libraryAlignments)
                   && Equals(ResultFileAlignments, other.ResultFileAlignments)
                   && Equals(MedianDocumentRetentionTimes, other.MedianDocumentRetentionTimes)
                   && CollectionUtil.EqualsDeep(_deserializedAlignmentFunctions, other._deserializedAlignmentFunctions);

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
                result = (result * 397) ^ (MedianDocumentRetentionTimes?.GetHashCode() ?? 0);
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
            var libraryAlignments = new Dictionary<string, LibraryAlignment>();
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
                    libraryAlignments.Add(libraryName, new LibraryAlignment(null, new Alignments(null, alignments)));
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

        private Dictionary<string, LibraryAlignment> _libraryAlignments;

        public LibraryAlignment GetLibraryAlignment(string libraryName)
        {
            if (_libraryAlignments.TryGetValue(libraryName, out var libraryAlignment) &&
                true == libraryAlignment.Library?.IsLoaded)
            {
                return libraryAlignment;
            }
            return null;
        }

        public DocumentRetentionTimes ChangeLibraryAlignments(LibraryAlignmentParam alignmentParam, Alignments alignments)
        {
            var newEntries = _libraryAlignments.Where(kvp => !alignmentParam.LibraryName.Equals(kvp.Key));
            var newAlignmentTarget = AlignmentTarget;
            if (alignments != null && (AlignmentTarget == null || Equals(alignmentParam.AlignmentTarget, AlignmentTarget)))
            {
                newAlignmentTarget ??= alignmentParam.AlignmentTarget;
                newEntries = newEntries.Append(new KeyValuePair<string, LibraryAlignment>(
                    alignmentParam.LibraryName, new LibraryAlignment(alignmentParam.Library, alignments)));
            }

            return ChangeProp(ImClone(this),
                im =>
                {
                    im._libraryAlignments = newEntries.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    im.AlignmentTarget = newAlignmentTarget;
                });
        }

        public IEnumerable<LibraryAlignmentParam> GetMissingAlignments(SrmSettings settings)
        {
            var dictParams = GetAlignmentParams(null, settings);
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

        public Dictionary<string, LibraryAlignmentParam> GetAlignmentParams(AlignmentTarget alignmentTarget, SrmSettings settings)
        {
            if (!TryGetAlignmentTarget(settings, ref alignmentTarget))
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
                if (libraryName == null || dict.ContainsKey(libraryName))
                {
                    continue;
                }
                if (true != library?.IsLoaded)
                {
                    dict.Add(libraryName, null);
                    continue;
                }
                dict.Add(libraryName, new LibraryAlignmentParam(alignmentTarget, library));
            }

            return dict;
        }

        private bool TryGetAlignmentTarget(SrmSettings settings, ref AlignmentTarget alignmentTarget)
        {
            if (alignmentTarget != null)
            {
                return true;
            }
            var targetSpec = settings.GetAlignmentTargetSpec();
            if (targetSpec.IsChromatogramPeaks)
            {
                alignmentTarget = MedianDocumentRetentionTimes;
                return alignmentTarget != null;
            }

            return targetSpec.TryGetAlignmentTarget(settings, out alignmentTarget);
        }

        public class LibraryAlignmentParam : Immutable
        {
            public LibraryAlignmentParam(AlignmentTarget alignmentTarget, Library library)
            {
                AlignmentTarget = alignmentTarget;
                Library = library;

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

            protected bool Equals(LibraryAlignmentParam other)
            {
                return Equals(AlignmentTarget, other.AlignmentTarget) && Equals(Library, other.Library);
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
                    return hashCode;
                }
            }
        }

        public static Alignments PerformAlignment(ILoadMonitor loadMonitor, ref IProgressStatus progressStatus, LibraryAlignmentParam alignmentParam)
        {
            var library = alignmentParam.Library;
            var spectrumSourceFiles = library.LibraryFiles.FilePaths;
            var allRetentionTimes = library.GetAllRetentionTimes(null);
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

        /// <summary>
        /// After Libraries have been loaded, updates <see cref="_libraryAlignments"/>
        /// so that they have the actual Library object instead of null.
        /// </summary>
        public DocumentRetentionTimes UpdateFromLoadedSettings(AlignmentTarget alignmentTarget, SrmSettings settings)
        {
            if (_libraryAlignments.Count == 0 && ResultFileAlignments.IsEmpty)
            {
                // Nothing to remember
                return null;
            }
            var newMedianDocumentRetentionTimes = alignmentTarget as AlignmentTarget.MedianDocumentRetentionTimes;
            if (Equals(newMedianDocumentRetentionTimes, MedianDocumentRetentionTimes))
            {
                newMedianDocumentRetentionTimes = MedianDocumentRetentionTimes;
                alignmentTarget = newMedianDocumentRetentionTimes ?? alignmentTarget;
            }

            if (AlignmentTarget != null && !Equals(AlignmentTarget, alignmentTarget))
            {
                return EMPTY;
            }
            bool anyChanges = !ReferenceEquals(newMedianDocumentRetentionTimes, MedianDocumentRetentionTimes) || !Equals(alignmentTarget, AlignmentTarget);
            if (_libraryAlignments.Count == 0 && !anyChanges)
            {
                return null;
            }
            var newParams = GetAlignmentParams(alignmentTarget, settings);
            if (newParams == null)
            {
                return null;
            }
            var newLibraries = new Dictionary<string, LibraryAlignment>();
            foreach (var entry in _libraryAlignments)
            {
                var key = entry.Key;
                if (!newParams.TryGetValue(key, out var param))
                {
                    anyChanges = true;
                    continue;
                }

                if (param == null || Equals(new LibraryAlignmentParam(AlignmentTarget, entry.Value.Library), param))
                {
                    newLibraries.Add(key, entry.Value);
                    continue;
                }

                anyChanges = true;
                if (entry.Value.Library == null)
                {
                    newLibraries.Add(key, new LibraryAlignment(param.Library, entry.Value.Alignments));
                }
            }

            if (anyChanges)
            {
                return ChangeProp(ImClone(this), im =>
                {
                    im.AlignmentTarget = alignmentTarget;
                    im.MedianDocumentRetentionTimes = newMedianDocumentRetentionTimes;
                    im._libraryAlignments = newLibraries;
                });
            }

            return null;
        }

        /// <summary>
        /// Called right after the SrmDocument has been deserialized from XML.
        /// Updates all of the <see cref="ResultFileAlignments.AlignmentSource"/> with the current peak boundaries in the document so
        /// that if any of those peak boundaries change later, it knows that the alignment needs to be performed again.
        /// </summary>
        public DocumentRetentionTimes UpdateFromDeserializedDocument(SrmDocument document)
        {
            if (_deserializedAlignmentFunctions == null)
            {
                return this;
            }
            return ChangeProp(ImClone(this), im =>
            {
                if (document.Settings.HasResults)
                {
                    im.ResultFileAlignments = new ResultFileAlignments(document, _deserializedAlignmentFunctions, _deserializedAlignmentFunctions.Keys);
                }
                else
                {
                    im.ResultFileAlignments = ResultFileAlignments.EMPTY;
                }
                im._deserializedAlignmentFunctions = null;
            });
        }

        /// <summary>
        /// Returns an alignment function which only uses ID times from spectral libraries, and does not use chromatogram peak information in the document.
        /// </summary>
        public AlignmentFunction GetLibraryAlignmentFunction(PeptideLibraries peptideLibraries, MsDataFileUri filePath, bool forward)
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

            return null;
        }

        /// <summary>
        /// Returns an alignment function which either uses ID times from spectral libraries or chromatogram peak information.
        /// </summary>
        public AlignmentFunction GetRunToRunAlignmentFunction(PeptideLibraries peptideLibraries, MsDataFileUri filePath, bool forward)
        {
            var libraryAlignmentFunction = GetLibraryAlignmentFunction(peptideLibraries, filePath, forward);
            if (libraryAlignmentFunction != null)
            {
                return libraryAlignmentFunction;
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

        public DocumentRetentionTimes UpdateResultFileAlignments(AlignmentTarget newAlignmentTarget, ILoadMonitor loadMonitor,
            ref IProgressStatus progressStatus, SrmDocument document)
        {
            if (newAlignmentTarget == null)
            {
                if (Equals(ResultFileAlignments, ResultFileAlignments.EMPTY))
                {
                    return this;
                }

                return ChangeProp(ImClone(this), im => im.ResultFileAlignments = ResultFileAlignments.EMPTY);
            }
            if (ResultFileAlignments.IsUpToDate(document))
            {
                return this;
            }
            var newAlignments = ResultFileAlignments.ChangeDocument(newAlignmentTarget,
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
                new LibraryAlignment(newLibrary, oldEntry.Alignments);
            return ChangeProp(ImClone(this), im => im._libraryAlignments = newLibraryAlignments);
        }

        private Dictionary<MsDataFileUri, PiecewiseLinearMap> _deserializedAlignmentFunctions;

        #region Methods for testing
        public Alignments GetDeserializedAlignmentsForLibrary(string libraryName)
        {
            _libraryAlignments.TryGetValue(libraryName, out var libraryAlignment);
            return libraryAlignment?.Alignments;
        }
        #endregion
    }
}
