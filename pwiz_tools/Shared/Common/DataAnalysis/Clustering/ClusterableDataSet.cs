using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding.Clustering;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataAnalysis.Clustering
{
    public class ClusterDataSet : Immutable
    {
        public static ClusterDataSet FromDataFrames(IEnumerable<string> rowLabels, IEnumerable<DataFrame> dataFrames)
        {
            return new ClusterDataSet(rowLabels, dataFrames.ToLookup(frame => frame.ColumnHeaders)
                .Select(ImmutableList.ValueOf));
        }

        public ClusterDataSet(IEnumerable<string> rowLabels, IEnumerable<ImmutableList<DataFrame>> dataFrameGroups)
        {
            RowLabels = ImmutableList.ValueOf(rowLabels);
            DataFrameGroups = ImmutableList.ValueOf(dataFrameGroups.Where(group=>group.Count > 0));
            foreach (var group in DataFrameGroups)
            {
                var firstGroup = group[0];
                if (firstGroup.RowCount != RowLabels.Count)
                {
                    throw new ArgumentException(@"Wrong number of rows", nameof(dataFrameGroups));
                }
            }
        }

        public ImmutableList<string> RowLabels { get; private set; }

        public int RowCount
        {
            get { return RowLabels.Count; }
        }

        public int ColumnCount => DataFrames.Sum(frame => frame.ColumnHeaders.Count);

        public ImmutableList<ImmutableList<DataFrame>> DataFrameGroups { get; private set; }

        public IEnumerable<DataFrame> DataFrames
        {
            get { return DataFrameGroups.SelectMany(group => group); }
        }

        public ClusterDataSet ReorderRows(IList<int> newOrdering)
        {
            return new ClusterDataSet(Reorder(RowLabels, newOrdering),
                DataFrameGroups.Select(group =>
                    ImmutableList.ValueOf(group.Select(frame => frame.ReorderRows(newOrdering)))));
        }

        public class DataFrame
        {
            public DataFrame(ImmutableList<string> columnHeaders, IEnumerable<ImmutableList<double?>> dataColumns)
            {
                ColumnHeaders = columnHeaders;
                if (ColumnHeaders.Count == 0)
                {
                    throw new ArgumentException(nameof(columnHeaders));
                }
                DataColumns = ImmutableList.ValueOf(dataColumns);
                if (ColumnHeaders.Count != DataColumns.Count)
                {
                    throw new ArgumentException(@"Wrong number of data columns", nameof(dataColumns));
                }

                if (DataColumns.Select(col => col.Count).Distinct().Count() > 1)
                {
                    throw new ArgumentException(@"All data columns must have the same number of rows", nameof(dataColumns));
                }
            }

            public IEnumerable<double?> GetZScores(int iRow)
            {
                var values = DataColumns.Select(column => column[iRow]).Where(IsValidValue).ToList();
                if (values.Count <= 1)
                {
                    return DataColumns.Select(column => IsValidValue(column[iRow]) ? (double?) 0 : null);
                }

                var mean = values.Mean();
                var stdDev = values.StandardDeviation();
                return DataColumns.Select(col =>
                {
                    var value = col[iRow];
                    if (IsValidValue(value))
                    {
                        return (double?) (value.Value - mean) / stdDev;
                    }

                    return null;
                });
            }

            private bool IsValidValue(double? value)
            {
                return value.HasValue && !double.IsInfinity(value.Value) && !double.IsNaN(value.Value);
            }
            public ImmutableList<string> ColumnHeaders { get; private set; }
            public ImmutableList<ImmutableList<double?>> DataColumns { get; private set; }
            public int RowCount
            {
                get { return DataColumns[0].Count; }
            }

            public DataFrame ReorderRows(IList<int> newOrdering)
            {
                return new DataFrame(ColumnHeaders, DataColumns.Select(col=>ImmutableList.ValueOf(Reorder(col, newOrdering))));
            }
        }

        public ClusterResults PerformClustering()
        {
            int columnCount = DataFrames.Sum(frame => frame.ColumnHeaders.Count);
            var rowDataSet = new double[RowLabels.Count, columnCount];
            for (int iRow = 0; iRow < RowLabels.Count; iRow++)
            {
                int iCol = 0;
                foreach (var dataFrame in DataFrames)
                {
                    foreach (var zScore in dataFrame.GetZScores(iRow))
                    {
                        if (zScore.HasValue)
                        {
                            rowDataSet[iRow, iCol] = zScore.Value;
                        }

                        iCol++;
                    }
                }
            }
            alglib.clusterizercreate(out alglib.clusterizerstate s);
            alglib.clusterizersetpoints(s, rowDataSet, 2);
            alglib.clusterizerrunahc(s, out alglib.ahcreport rep);
            return new ClusterResults(ReorderRows(rep.p), new DendrogramData(rep.pz, rep.mergedist), null);
        }

        public static IEnumerable<T> Reorder<T>(IList<T> list, IList<int> newOrder)
        {
            return Enumerable.Range(0, list.Count).OrderBy(i => newOrder[i]).Select(i => list[i]);
        }
    }
}
