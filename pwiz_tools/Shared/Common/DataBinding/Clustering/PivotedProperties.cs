/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
#if DEBUG
using System.Diagnostics;
#endif
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding.Clustering
{
    public class PivotedProperties
    {
        public PivotedProperties(ItemProperties itemProperties) : this(itemProperties, ImmutableList.Empty<SeriesGroup>())
        {
        }
        private PivotedProperties(ItemProperties itemProperties, IEnumerable<SeriesGroup> seriesGroups)
        {
            ItemProperties = itemProperties;
            SeriesGroups = ImmutableList.ValueOf(seriesGroups);
            if (!SeriesGroups.SelectMany(group => group.SeriesList)
                .All(series => ReferenceEquals(series.ItemProperties, itemProperties)))
            {
                throw new ArgumentException(@"Wrong set of properties", nameof(seriesGroups));
            }
        }

        public ItemProperties ItemProperties { get; private set; }

        public ImmutableList<SeriesGroup> SeriesGroups { get; private set; }

        public IEnumerable<DataPropertyDescriptor> UngroupedProperties
        {
            get
            {
                var groupedPropertyIndexes = SeriesGroups
                    .SelectMany(group => group.SeriesList.SelectMany(series => series.PropertyIndexes)).ToHashSet();
                return Enumerable.Range(0, ItemProperties.Count).Where(i => !groupedPropertyIndexes.Contains(i))
                    .Select(i => ItemProperties[i]);
            }
        }

        public class SeriesGroup
        {
            public SeriesGroup(IEnumerable<object> pivotKeys, IEnumerable<IColumnCaption> pivotCaptions, IEnumerable<Series> series)
            {
                PivotKeys = ImmutableList.ValueOf(pivotKeys);
                PivotCaptions = ImmutableList.ValueOf(pivotCaptions);
                SeriesList = ImmutableList.ValueOf(series);
            }
            public ImmutableList<object> PivotKeys { get; }
            public ImmutableList<IColumnCaption> PivotCaptions { get; }
            public ImmutableList<Series> SeriesList { get; private set; }
            public SeriesGroup ReorderPivotKeys(IList<int> newOrder)
            {
                return new SeriesGroup(newOrder.Select(i=>PivotKeys[i]),
                    newOrder.Select(i=>PivotCaptions[i]),
                    SeriesList.Select(series=>series.ReorderProperties(newOrder)));
            }

            public SeriesGroup RenumberProperties(ItemProperties newItemProperties, IList<int> newNumbering)
            {
                return new SeriesGroup(PivotKeys, PivotCaptions, SeriesList.Select(series=>series.RenumberProperties(newItemProperties, newNumbering)));
            }
        }

        public class Series
        {
            public Series(ItemProperties itemProperties, object seriesId, IColumnCaption seriesCaption, IEnumerable<int> propertyIndexes, Type propertyType)
            {
                ItemProperties = itemProperties;
                SeriesId = seriesId;
                SeriesCaption = seriesCaption;
                PropertyIndexes = ImmutableList.ValueOf(propertyIndexes);
                PropertyType = propertyType;
            }

            internal ItemProperties ItemProperties { get; }
            public object SeriesId { get; }
            public IColumnCaption SeriesCaption { get; }
            public ImmutableList<int> PropertyIndexes { get; }

            public IList<DataPropertyDescriptor> PropertyDescriptors
            {
                get
                {
                    return ReadOnlyList.Create(PropertyIndexes.Count, i => ItemProperties[PropertyIndexes[i]]);
                }
            }

            public Type PropertyType { get; }

            public Series ReorderProperties(IList<int> newOrder)
            {
                return new Series(ItemProperties, SeriesId, SeriesCaption, newOrder.Select(i => PropertyIndexes[i]), PropertyType);
            }

            public Series RenumberProperties(ItemProperties newItemProperties, IList<int> newNumbering)
            {
                return new Series(newItemProperties, SeriesId, SeriesCaption, PropertyIndexes.Select(i=>newNumbering[i]), PropertyType);
            }
        }

        public IEnumerable<SeriesGroup> CreateSeriesGroups()
        {
            // Create a lookup from SeriesId to properties in that series
            var propertiesBySeriesId = Enumerable.Range(0, ItemProperties.Count)
                .Select(i => Tuple.Create(i, ItemProperties[i].PivotedColumnId))
                .Where(tuple => null != tuple.Item2)
                .ToLookup(tuple => Tuple.Create(tuple.Item2.SeriesId, ItemProperties[tuple.Item1].PropertyType));
            var seriesList = new List<Tuple<Series, ImmutableList<object>, ImmutableList<IColumnCaption>>>();
            foreach (var seriesTuples in propertiesBySeriesId)
            {
                if (seriesTuples.Count() <= 1)
                {
                    continue;
                }

                var firstProperty = ItemProperties[seriesTuples.First().Item1];
                var firstPivotColumnId = seriesTuples.First().Item2;
                var series = new Series(ItemProperties, firstPivotColumnId.SeriesId, firstPivotColumnId.SeriesCaption,
                    seriesTuples.Select(tuple => tuple.Item1),
                    firstProperty.PropertyType);

                seriesList.Add(Tuple.Create(series,
                    ImmutableList.ValueOf(seriesTuples.Select(tuple => tuple.Item2.PivotKey)),
                    ImmutableList.ValueOf(seriesTuples.Select(tuple => tuple.Item2.PivotKeyCaption))));
            }

            return seriesList.ToLookup(tuple => tuple.Item2).Select(grouping =>
                new SeriesGroup(grouping.First().Item2, grouping.First().Item3, grouping.Select(tuple => tuple.Item1)));
        }

        public PivotedProperties ChangeSeriesGroups(IEnumerable<SeriesGroup> newGroups)
        {
            return new PivotedProperties(ItemProperties, newGroups);
        }

        public PivotedProperties ReorderPivots(IList<IList<int>> newPivotOrders)
        {
            if (newPivotOrders.Count != SeriesGroups.Count)
            {
                throw new ArgumentException();
            }

            var newGroups = new List<SeriesGroup>();
            for (int iGroup = 0; iGroup < newPivotOrders.Count; iGroup++)
            {
                var pivotOrder = newPivotOrders[iGroup];
                var newGroup = SeriesGroups[iGroup];
                if (pivotOrder != null)
                {
                    newGroup = newGroup.ReorderPivotKeys(pivotOrder);
                }
                newGroups.Add(newGroup);
            }
            return new PivotedProperties(ItemProperties, newGroups);
        }

        /// <summary>
        /// Reorder the ItemProperties collection so that the ungrouped properties come first,
        /// followed by the grouped properties.
        /// If a group contains multiple series, the properties from those series are interleaved
        /// with each other.
        /// </summary>
        /// <returns></returns>
        public PivotedProperties ReorderItemProperties()
        {
            var groupedPropertyIndexes = SeriesGroups
                .SelectMany(group => group.SeriesList.SelectMany(series => series.PropertyIndexes)).ToHashSet();
            var newOrder = new List<int>();
            newOrder.AddRange(Enumerable.Range(0, ItemProperties.Count).Where(i=>!groupedPropertyIndexes.Contains(i)));
            newOrder.AddRange(SeriesGroups.SelectMany(group =>
                Enumerable.Range(0, group.PivotKeys.Count)
                    .SelectMany(i => group.SeriesList.Select(series => series.PropertyIndexes[i]))));

            var newNumbering = new int[newOrder.Count];
            for (int i = 0; i < newOrder.Count; i++)
            {
                newNumbering[newOrder[i]] = i;
            }

            var newItemProperties = new ItemProperties(newOrder.Select(i => ItemProperties[i]));
            var result = new PivotedProperties(newItemProperties, SeriesGroups.Select(group=>group.RenumberProperties(newItemProperties, newNumbering)));
#if DEBUG
            Debug.Assert(ItemProperties.ToHashSet().SetEquals(result.ItemProperties.ToHashSet()));
            Debug.Assert(SeriesGroups.Count == result.SeriesGroups.Count);
            for (int iGroup = 0; iGroup < SeriesGroups.Count; iGroup++)
            {
                Debug.Assert(SeriesGroups[iGroup].SeriesList.Count == result.SeriesGroups[iGroup].SeriesList.Count);
                Debug.Assert(SeriesGroups[iGroup].PivotKeys.SequenceEqual(result.SeriesGroups[iGroup].PivotKeys));
                for (int iSeries = 0; iSeries < SeriesGroups[iGroup].SeriesList.Count; iSeries++)
                {
                    var resultSeries = result.SeriesGroups[iGroup].SeriesList[iSeries];
                    Debug.Assert(resultSeries.PropertyIndexes.OrderBy(i => i).SequenceEqual(resultSeries.PropertyIndexes));

                    var series = SeriesGroups[iGroup].SeriesList[iSeries];
                    Debug.Assert(series.PropertyIndexes.Select(i => ItemProperties[i])
                        .SequenceEqual(resultSeries.PropertyIndexes.Select(i => result.ItemProperties[i])));
                }
            }
#endif
            return result;
        }
    }
}