using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using MathNet.Numerics.Statistics;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataAnalysis.Clustering
{
    public class ClusterDataSet<TRow, TColumn> : Immutable
    {
        public static ClusterDataSet<TRow, TColumn> FromDataFrames(IEnumerable<TRow> rowLabels, IEnumerable<DataFrame> dataFrames)
        {
            return new ClusterDataSet<TRow, TColumn>(rowLabels, dataFrames.ToLookup(frame => frame.ColumnHeaders)
                .Select(ImmutableList.ValueOf));
        }

        public ClusterDataSet(IEnumerable<TRow> rowLabels, IEnumerable<ImmutableList<DataFrame>> dataFrameGroups)
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

        public ImmutableList<TRow> RowLabels { get; private set; }

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

        public ClusterDataSet<TRow, TColumn> ReorderRows(IList<int> newOrdering)
        {
            return new ClusterDataSet<TRow, TColumn>(Reorder(RowLabels, newOrdering),
                DataFrameGroups.Select(group =>
                    ImmutableList.ValueOf(group.Select(frame => frame.ReorderRows(newOrdering)))));
        }

        public class DataFrame
        {
            public DataFrame(IEnumerable<TColumn> columnHeaders, IEnumerable<ImmutableList<double?>> dataColumns)
            {
                ColumnHeaders = ImmutableList.ValueOf(columnHeaders);
                DataColumns = ImmutableList.ValueOf(dataColumns);
                if (ColumnHeaders.Count == 0)
                {
                    throw new ArgumentException(nameof(columnHeaders));
                }
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
                return ZScores.CalculateZScores(DataColumns.Select(col => col[iRow]));
            }
            public ImmutableList<TColumn> ColumnHeaders { get; private set; }
            public ImmutableList<ImmutableList<double?>> DataColumns { get; private set; }
            public int RowCount
            {
                get { return DataColumns[0].Count; }
            }

            public DataFrame ReorderRows(IList<int> newOrdering)
            {
                return new DataFrame(ColumnHeaders, DataColumns.Select(col=>ImmutableList.ValueOf(Reorder(col, newOrdering))));
            }

            public DataFrame ReorderColumns(IList<int> newOrdering)
            {
                return new DataFrame(ImmutableList.ValueOf(Reorder(ColumnHeaders, newOrdering)),
                    Reorder(DataColumns, newOrdering));
            }
        }

        public Results ClusterRows()
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
            return new Results(ReorderRows(rep.p), new DendrogramData(rep.pz, rep.mergedist), null);
        }

        private Tuple<ImmutableList<DataFrame>, DendrogramData> ClusterDataFrameGroup(ImmutableList<DataFrame> group)
        {
            var points = new double[group[0].ColumnHeaders.Count,
                RowCount * group.Count];
            for (int iRow = 0; iRow < RowCount; iRow++)
            {
                for (int iFrame = 0; iFrame < group.Count; iFrame++)
                {
                    var frame = group[iFrame];
                    var zScores = frame.GetZScores(iRow).ToList();
                    int y = iRow * group.Count + iFrame;
                    for (int x = 0; x < zScores.Count; x++)
                    {
                        var zScore = zScores[x];
                        if (zScore.HasValue)
                        {
                            points[x, y] = zScore.Value;
                        }
                    }
                }
            }
            alglib.clusterizercreate(out alglib.clusterizerstate s);
            alglib.clusterizersetpoints(s, points, 2);
            alglib.clusterizerrunahc(s, out alglib.ahcreport rep);
            var newGroup = ImmutableList.ValueOf(group.Select(frame => frame.ReorderColumns(rep.p)));
            var dendrogramData = new DendrogramData(rep.pz, rep.mergedist);
            return Tuple.Create(newGroup, dendrogramData);
        }

        public Results ClusterColumns()
        {
            var clusteredGroups = DataFrameGroups.Select(ClusterDataFrameGroup).ToList();
            var newDataSet = new ClusterDataSet<TRow, TColumn>(RowLabels, clusteredGroups.Select(tuple=>tuple.Item1));
            return new Results(newDataSet, null, ImmutableList.ValueOf(clusteredGroups.Select(tuple=>tuple.Item2)));
        }

        public Results PerformClustering(bool clusterRows, bool clusterColumns)
        {
            Results rowResults;
            if (clusterRows)
            {
                rowResults = ClusterRows();
            }
            else
            {
                rowResults = new Results(this, null, null);
            }
            Results columnResults = rowResults.DataSet.ClusterColumns();
            return new Results(columnResults.DataSet, rowResults.RowDendrogram, columnResults.ColumnGroupDendrograms);
        }

        public static IEnumerable<T> Reorder<T>(IList<T> list, IList<int> newOrder)
        {
            return Enumerable.Range(0, list.Count).OrderBy(i => newOrder[i]).Select(i => list[i]);
        }

        public class Results
        {
            public Results(ClusterDataSet<TRow, TColumn> dataSet, DendrogramData rowDendrogram,
                ImmutableList<DendrogramData> columnDendrograms)
            {
                DataSet = dataSet;
                RowDendrogram = rowDendrogram;
                ColumnGroupDendrograms = columnDendrograms;
            }
            public ClusterDataSet<TRow, TColumn> DataSet { get; }
            public DendrogramData RowDendrogram { get; private set; }
            public ImmutableList<DendrogramData> ColumnGroupDendrograms { get; private set; }

            public Results ReverseRows()
            {
                List<int> newOrdering = Enumerable.Range(0, DataSet.RowCount).ToList();
                newOrdering.Reverse();
                
                return new Results(DataSet.ReorderRows(newOrdering), RowDendrogram?.Reverse(), ColumnGroupDendrograms);
            }
        }
    }
}
