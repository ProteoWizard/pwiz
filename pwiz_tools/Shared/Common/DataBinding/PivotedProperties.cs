using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding
{
    public class PivotedProperties : Immutable
    {
        public PivotedProperties(ItemProperties itemProperties) : this(itemProperties, ImmutableList.Empty<ImmutableList<Series>>())
        {
        }
        private PivotedProperties(ItemProperties itemProperties, IEnumerable<ImmutableList<Series>> seriesGroups)
        {
            ItemProperties = itemProperties;
            SeriesGroups = ImmutableList.ValueOf(seriesGroups);
        }

        public ItemProperties ItemProperties { get; private set; }

        public ImmutableList<ImmutableList<Series>> SeriesGroups { get; private set; }

        public Series SeriesFromPropertyIndex(int propertyIndex)
        {
            foreach (var group in SeriesGroups)
            {
                var firstPropertyIndex = group[0].PropertyIndexes[0];
                if (propertyIndex < firstPropertyIndex)
                {
                    return null;
                }

                int groupPropertyCount = group.Count * group[0].PivotKeys.Count;
                if (propertyIndex < firstPropertyIndex + groupPropertyCount)
                {
                    return group[(propertyIndex - firstPropertyIndex) % group.Count];
                }
            }

            return null;
        }
        public class Series
        {
            public Series(object seriesId, IColumnCaption seriesCaption, IEnumerable<object> pivotKeys,
                IEnumerable<IColumnCaption> pivotCaptions, IEnumerable<int> propertyIndexes, Type propertyType)
            {
                SeriesId = seriesId;
                SeriesCaption = seriesCaption;
                PivotKeys = ImmutableList.ValueOf(pivotKeys);
                PivotCaptions = ImmutableList.ValueOf(pivotCaptions);
                PropertyIndexes = ImmutableList.ValueOf(propertyIndexes);
                PropertyType = propertyType;
            }
            public object SeriesId { get; }
            public IColumnCaption SeriesCaption { get; }
            public ImmutableList<object> PivotKeys { get; }
            public ImmutableList<IColumnCaption> PivotCaptions { get; }
            public ImmutableList<int> PropertyIndexes { get; }
            public Type PropertyType { get; }

            public Series ReorderPivotKeys(IList<int> newOrder)
            {
                return new Series(SeriesId, SeriesCaption, newOrder.Select(i => PivotKeys[i]),
                    newOrder.Select(i => PivotCaptions[i]), newOrder.Select(i => PropertyIndexes[i]), PropertyType);
            }

            public Series RenumberProperties(IList<int> newNumbering)
            {
                return new Series(SeriesId, SeriesCaption, PivotKeys, PivotCaptions, PropertyIndexes.Select(i=>newNumbering[i]), PropertyType);
            }
        }

        public IEnumerable<ImmutableList<Series>> CreateSeriesGroups()
        {
            // Create a lookup from SeriesId to properties in that series
            var propertiesBySeriesId = Enumerable.Range(0, ItemProperties.Count)
                .Select(i => Tuple.Create(i, ItemProperties[i].PivotedColumnId))
                .Where(tuple => null != tuple.Item2)
                .ToLookup(tuple => Tuple.Create(tuple.Item2.SeriesId, ItemProperties[tuple.Item1].PropertyType));
            List<Series> seriesList = new List<Series>();
            foreach (var seriesTuples in propertiesBySeriesId)
            {
                if (seriesTuples.Count() <= 1)
                {
                    continue;
                }

                var firstProperty = ItemProperties[seriesTuples.First().Item1];
                var firstPivotColumnId = seriesTuples.First().Item2;
                var series = new Series(firstPivotColumnId.SeriesId, firstPivotColumnId.SeriesCaption,
                    seriesTuples.Select(tuple => tuple.Item2.PivotKey),
                    seriesTuples.Select(tuple => tuple.Item2.PivotKeyCaption),
                    seriesTuples.Select(tuple => tuple.Item1),
                    firstProperty.PropertyType);
                seriesList.Add(series);
            }

            return seriesList.ToLookup(series => series.PivotKeys).Select(ImmutableList.ValueOf);
        }

        public PivotedProperties ChangeSeriesGroups(IEnumerable<IEnumerable<Series>> newGroups)
        {
            return FromSeriesGroups(ItemProperties, newGroups.Select(group => group.ToList()).ToList());
        }

        public PivotedProperties ReorderPivots(IList<IList<int>> newPivotOrders)
        {
            if (newPivotOrders.Count != SeriesGroups.Count)
            {
                throw new ArgumentException();
            }

            var newGroups = new List<List<Series>>();
            for (int iGroup = 0; iGroup < newPivotOrders.Count; iGroup++)
            {
                var pivotOrder = newPivotOrders[iGroup];
                newGroups.Add(SeriesGroups[iGroup].Select(series=>series.ReorderPivotKeys(pivotOrder)).ToList());
            }
            return FromSeriesGroups(ItemProperties, newGroups);
        }

        /// <summary>
        /// Reorder the ItemProperties collection so that the ungrouped properties come first,
        /// followed by the grouped properties.
        /// If a group contains multiple series, the properties from those series are interleaved
        /// with each other.
        /// </summary>
        /// <returns></returns>
        private static PivotedProperties FromSeriesGroups(ItemProperties itemProperties, List<List<Series>> seriesGroups)
        {
            var groupedPropertyIndexes = new HashSet<int>();
            foreach (var group in seriesGroups)
            {
                for (int i = 0; i < group.Count; i++)
                {
                    var series = group[i];
                    if (i > 0)
                    {
                        var firstSeries = group[0];
                        if (!Equals(firstSeries.PivotKeys, series.PivotKeys))
                        {
                            throw new ArgumentException(@"Pivot Keys do not match");
                        }
                    }

                    foreach (var propertyIndex in series.PropertyIndexes)
                    {
                        if (propertyIndex < 0 || propertyIndex > itemProperties.Count)
                        {
                            throw new ArgumentOutOfRangeException();
                        }
                        if (!groupedPropertyIndexes.Add(propertyIndex))
                        {
                            throw new ArgumentException(@"Duplicate property index");
                        }
                    }
                }
            }
            var newOrder = new List<int>();
            newOrder.AddRange(Enumerable.Range(0, itemProperties.Count).Where(i=>!groupedPropertyIndexes.Contains(i)));
            foreach (var group in seriesGroups)
            {
                int pivotKeyCount = group[0].PivotKeys.Count;
                for (int i = 0; i < pivotKeyCount; i++)
                {
                    foreach (var series in group)
                    {
                        newOrder.Add(series.PropertyIndexes[i]);
                    }
                }
            }

            var newNumbering = new int[newOrder.Count];
            for (int i = 0; i < newOrder.Count; i++)
            {
                newNumbering[newOrder[i]] = i;
            }

            var newItemProperties = new ItemProperties(newOrder.Select(i => itemProperties[i]));
            var newGroups = new List<ImmutableList<Series>>();
            for (int iGroup = 0; iGroup < seriesGroups.Count; iGroup++)
            {
                newGroups.Add(ImmutableList.ValueOf(seriesGroups[iGroup].Select(series => series.RenumberProperties(newNumbering))));
            }
            var result = new PivotedProperties(newItemProperties, newGroups);
#if DEBUG
            Debug.Assert(itemProperties.ToHashSet().SetEquals(newItemProperties.ToHashSet()));
            Debug.Assert(seriesGroups.Count == result.SeriesGroups.Count);
            for (int iGroup = 0; iGroup < seriesGroups.Count; iGroup++)
            {
                Debug.Assert(seriesGroups[iGroup].Count == result.SeriesGroups[iGroup].Count);
                for (int iSeries = 0; iSeries < seriesGroups[iGroup].Count; iSeries++)
                {
                    var resultSeries = result.SeriesGroups[iGroup][iSeries];
                    Debug.Assert(resultSeries.PropertyIndexes.OrderBy(i => i).SequenceEqual(resultSeries.PropertyIndexes));

                    var series = seriesGroups[iGroup][iSeries];
                    Debug.Assert(series.PivotKeys.SequenceEqual(resultSeries.PivotKeys));
                    Debug.Assert(series.PropertyIndexes.Select(i => itemProperties[i])
                        .SequenceEqual(resultSeries.PropertyIndexes.Select(i => result.ItemProperties[i])));
                }
            }
#endif
            return result;
        }
    }
}