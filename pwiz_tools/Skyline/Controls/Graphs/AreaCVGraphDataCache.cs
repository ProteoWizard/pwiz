/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Threading;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class AreaCVGraphData
    {
        public class AreaCVGraphDataCache : IDisposable
        {
            private readonly StackWorker<GraphDataProperties> _producerConsumer;
            private readonly List<AreaCVGraphData> _data;
            private SrmDocument _document;
            private AreaCVGraphSettings _settings;
            private CancellationTokenSource _tokenSource;

            private readonly object _requestLock = new object();
            private GraphDataProperties _requested;
            private Action<AreaCVGraphData> _callback;

            private static readonly int MAX_THREADS = Math.Max(1, Environment.ProcessorCount / 2);

            public AreaCVGraphDataCache()
            {
                _producerConsumer = new StackWorker<GraphDataProperties>(null, CacheData);
                _producerConsumer.RunAsync(MAX_THREADS, "AreaCVGraphDataCache"); // Not L10N
                _data = new List<AreaCVGraphData>();
                _tokenSource = new CancellationTokenSource();
            }

            public bool TryGet(SrmDocument document, AreaCVGraphSettings settings,
                GraphDataProperties properties, Action<AreaCVGraphData> callback, out AreaCVGraphData result)
            {
                if (!IsValidFor(document, settings))
                {
                    Cancel();

                    lock (_data)
                    {
                        _data.Clear();
                        _document = document;
                        _settings = settings;
                    }

                    // Get a list of all properties that we want to cache data for, except for the data that just got requested
                    var propertyList = new List<GraphDataProperties>(GetPropertyVariants().Except(new[] {properties}));
                    _producerConsumer.Add(propertyList, false, false);
                }


                result = Get(properties);
                if (result != null)
                    return true;
                
                lock (_requestLock)
                {
                    if (!properties.Equals((object) _requested))
                    {
                        _producerConsumer.Add(properties);

                        _requested = properties;
                        _callback = callback;
                    }

                    return false;
                }
            }

            private AreaCVGraphData CreateOrGet(GraphDataProperties properties)
            {
                var result = Get(properties);
                if (result == null)
                {
                    var factor = AreaGraphController.GetAreaCVFactorToDecimal();
                    result = new AreaCVGraphData(_document,
                        new AreaCVGraphSettings(AreaGraphController.GraphType,
                            properties.NormalizationMethod,
                            properties.RatioIndex,
                            AreaGraphController.GroupByGroup,
                            properties.Annotation,
                            AreaGraphController.PointsType,
                            Settings.Default.AreaCVQValueCutoff,
                            Settings.Default.AreaCVCVCutoff / factor,
                            properties.MinimumDetections,
                            Settings.Default.AreaCVHistogramBinWidth / factor));

                    lock (_data)
                    {
                        _data.Add(result);
                    }
                }

                return result;
            }

            public AreaCVGraphData Get(GraphDataProperties properties)
            {
                return Get(properties.Group, properties.Annotation, properties.MinimumDetections,
                    properties.NormalizationMethod, properties.RatioIndex);
            }

            public AreaCVGraphData Get(string group, string annotation, int minimumDetections,
                AreaCVNormalizationMethod normalizationMethod, int ratioIndex)
            {
                lock (_data)
                {
                    // Linear search, but very short list
                    return _data.FirstOrDefault(d => d._graphSettings.Group == group &&
                                                     d._graphSettings.Annotation == annotation &&
                                                     d._graphSettings.MinimumDetections == minimumDetections &&
                                                     d._graphSettings.NormalizationMethod == normalizationMethod &&
                                                     d._graphSettings.RatioIndex == ratioIndex);
                }
            }

            public bool IsValidFor(SrmDocument document, AreaCVGraphSettings settings)
            {
                return _document != null && _settings != null &&
                       ReferenceEquals(_document.Children, document.Children) &&
                       AreaCVGraphSettings.CacheEqual(_settings, settings);
            }

            private static int GetMinDetectionsForAnnotation(SrmDocument document, string annotationValue)
            {
                return document.Settings.PeptideSettings.Integration.PeakScoringModel.IsTrained && !double.IsNaN(Settings.Default.AreaCVQValueCutoff)
                    ? AnnotationHelper.GetReplicateIndices(document.Settings, AreaGraphController.GroupByGroup, annotationValue).Length
                    : 2;
            }

            private IEnumerable<GraphDataProperties> GetPropertyVariants()
            {
                var annotationsArray = AnnotationHelper.GetPossibleAnnotations(_document.Settings,
                    AreaGraphController.GroupByGroup, AnnotationDef.AnnotationTarget.replicate);

                // Add an entry for All
                var annotations = annotationsArray.Concat(new string[] { null }).ToList();

                var normalizationMethods = new List<AreaCVNormalizationMethod> { AreaCVNormalizationMethod.none, AreaCVNormalizationMethod.medians, AreaCVNormalizationMethod.ratio };
                if (_document.Settings.HasGlobalStandardArea)
                    normalizationMethods.Add(AreaCVNormalizationMethod.global_standards);

                // First cache for current normalization method
                if (normalizationMethods.Remove(AreaGraphController.NormalizationMethod))
                    normalizationMethods.Insert(0, AreaGraphController.NormalizationMethod);

                // First cache the histograms for the current annotation
                if (annotations.Remove(AreaGraphController.GroupByAnnotation))
                    annotations.Insert(0, AreaGraphController.GroupByAnnotation);

                foreach (var n in normalizationMethods)
                {
                    bool isRatio = n == AreaCVNormalizationMethod.ratio;
                    // There can be RatioInternalStandardTypes even though HasHeavyModifications is false
                    if (isRatio && !_document.Settings.PeptideSettings.Modifications.HasHeavyModifications)
                        continue;

                    var ratioIndices = isRatio
                        ? Enumerable.Range(0, _document.Settings.PeptideSettings.Modifications.RatioInternalStandardTypes.Count).ToList()
                        : new List<int> { -1 };

                    if (AreaGraphController.AreaCVRatioIndex != -1)
                    {
                        if (ratioIndices.Remove(AreaGraphController.AreaCVRatioIndex))
                            ratioIndices.Insert(0, AreaGraphController.AreaCVRatioIndex);
                    }

                    foreach (var r in ratioIndices)
                    {
                        foreach (var a in annotations)
                        {
                            var minDetections = GetMinDetectionsForAnnotation(_document, a);

                            for (var i = 2; i <= minDetections; ++i)
                            {
                                yield return new GraphDataProperties(AreaGraphController.GroupByGroup, n, r, a, i);
                            }
                        }
                    }
                }
            }

            private void CacheData(GraphDataProperties properties, int index)
            {
                var data = CreateOrGet(properties);

                lock (_requestLock)
                {
                    if (properties.Equals((object) _requested))
                    {
                        _callback(data);
                        _requested = null;
                        _callback = null;
                    }
                }
            }

            public void Cancel()
            {
                _tokenSource.Cancel();
                _producerConsumer.Wait();
                _tokenSource.Dispose();
                _tokenSource = new CancellationTokenSource();
            }

            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                // Will only be called from UI thread, so it's safe to not have a lock
                if (!IsDisposed)
                {
                    _tokenSource.Cancel();
                    _producerConsumer.Abort(true);
                    _tokenSource.Dispose();
                    _data.Clear();
                    IsDisposed = true;
                }
            }

            public class GraphDataProperties
            {
                public bool Equals(GraphDataProperties other)
                {
                    return string.Equals(Group, other.Group) && NormalizationMethod == other.NormalizationMethod && RatioIndex == other.RatioIndex && string.Equals(Annotation, other.Annotation) && MinimumDetections == other.MinimumDetections;
                }

                public override bool Equals(object obj)
                {
                    if (ReferenceEquals(null, obj)) return false;
                    return obj is GraphDataProperties && Equals((GraphDataProperties)obj);
                }

                public override int GetHashCode()
                {
                    unchecked
                    {
                        var hashCode = (Group != null ? Group.GetHashCode() : 0);
                        hashCode = (hashCode * 397) ^ (int)NormalizationMethod;
                        hashCode = (hashCode * 397) ^ RatioIndex;
                        hashCode = (hashCode * 397) ^ (Annotation != null ? Annotation.GetHashCode() : 0);
                        hashCode = (hashCode * 397) ^ MinimumDetections;
                        return hashCode;
                    }
                }

                public GraphDataProperties(AreaCVGraphSettings settings)
                {
                    Group = settings.Group;
                    NormalizationMethod = settings.NormalizationMethod;
                    RatioIndex = settings.RatioIndex;
                    Annotation = settings.Annotation;
                    MinimumDetections = settings.MinimumDetections;
                }

                public GraphDataProperties(string group, AreaCVNormalizationMethod normalizationMethod, int ratioIndex, string annotation, int minimumDetections)
                {
                    Group = group;
                    NormalizationMethod = normalizationMethod;
                    RatioIndex = ratioIndex;
                    Annotation = annotation;
                    MinimumDetections = minimumDetections;
                }

                public string Group { get; private set; }
                public AreaCVNormalizationMethod NormalizationMethod { get; private set; }
                public int RatioIndex { get; private set; }
                public string Annotation { get; private set; }
                public int MinimumDetections { get; private set; }
            }

            #region Functional test support

            public int DataCount { get { return _data.Count; } }

            #endregion
        }
    }
}