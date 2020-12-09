using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using ZedGraph;

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
            DistanceMetric = ClusterMetricType.EUCLIDEAN;
            RowLabels = ImmutableList.ValueOf(rowLabels);
            DataFrameGroups = ImmutableList.ValueOf(dataFrameGroups);
            foreach (var group in DataFrameGroups.Where(group=>group.Count > 0))
            {
                var firstGroup = group[0];
                if (firstGroup.RowCount != RowLabels.Count)
                {
                    throw new ArgumentException(@"Wrong number of rows", nameof(dataFrameGroups));
                }
            }
        }

        public ImmutableList<TRow> RowLabels { get; private set; }

        public ClusterMetricType DistanceMetric { get; private set; }

        public ClusterDataSet<TRow, TColumn> ChangeDistanceMetric(ClusterMetricType distanceMetric)
        {
            return ChangeProp(ImClone(this), im => im.DistanceMetric = distanceMetric);
        }

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

        public ClusterDataSet<TNewRow, TNewColumn> ChangeLabels<TNewRow, TNewColumn>(IEnumerable<TNewRow> newRowLabels,
            IEnumerable<IEnumerable<TNewColumn>> newColumnLabels)
        {
            var newDataFrameGroups = new List<ImmutableList<ClusterDataSet<TNewRow, TNewColumn>.DataFrame>>();
            foreach (var newLabels in newColumnLabels)
            {
                var newLabelList = ImmutableList.ValueOf(newLabels);
                newDataFrameGroups.Add(ImmutableList.ValueOf(DataFrameGroups[newDataFrameGroups.Count].Select(frame =>
                    new ClusterDataSet<TNewRow, TNewColumn>.DataFrame(newLabelList, frame.DataColumns))));
            }

            return new ClusterDataSet<TNewRow, TNewColumn>(newRowLabels, newDataFrameGroups);
        }

        public class DataFrame
        {
            public DataFrame(IEnumerable<TColumn> columnHeaders, IEnumerable<ImmutableList<double>> dataColumns)
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

            public ClusterRole.Transform Transform { get; private set; }

            public ImmutableList<TColumn> ColumnHeaders { get; private set; }
            public ImmutableList<ImmutableList<double>> DataColumns { get; private set; }
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
                    foreach (var dataColumn in dataFrame.DataColumns)
                    {
                        rowDataSet[iRow, iCol] = dataColumn[iRow];
                        iCol++;
                    }
                }
            }
            alglib.clusterizercreate(out alglib.clusterizerstate s);
            alglib.clusterizersetpoints(s, rowDataSet, DistanceMetric.AlgLibValue);
            alglib.clusterizerrunahc(s, out alglib.ahcreport rep);
            if (rep.p.Length == 0)
            {
                return new Results(this, null, null);
            }
            return new Results(ReorderRows(rep.p), new DendrogramData(rep.pz, rep.mergedist), null);
        }

        private Tuple<ImmutableList<DataFrame>, DendrogramData> ClusterDataFrameGroup(ImmutableList<DataFrame> group)
        {
            if (group.Count == 0 || group[0].ColumnHeaders.Count <= 1)
            {
                return Tuple.Create(group, (DendrogramData) null);
            }
            var points = new double[group[0].ColumnHeaders.Count,
                RowCount * group.Count];
            for (int iRow = 0; iRow < RowCount; iRow++)
            {
                for (int iFrame = 0; iFrame < group.Count; iFrame++)
                {
                    var frame = group[iFrame];
                    int y = iRow * group.Count + iFrame;
                    for (int x = 0; x < frame.DataColumns.Count; x++)
                    {
                        points[x, y] = frame.DataColumns[x][iRow];
                    }
                }
            }
            alglib.clusterizercreate(out alglib.clusterizerstate s);
            alglib.clusterizersetpoints(s, points, DistanceMetric.AlgLibValue);
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

        public Results PerformClustering(bool clusterRows)
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

        public IEnumerable<PcaResults<TColumn>> PerformPcaOnColumnGroups(int maxLevels)
        {
            return DataFrameGroups.Select(group => PerformPca(group, maxLevels));
        }

        public PcaResults<TRow> PerformPcaOnRows(int maxLevels)
        {
            int nPoints = RowCount;
            int nVars = DataFrames.Sum(frame => frame.ColumnHeaders.Count);
            var matrix = new double[nPoints, nVars];
            int iCol = 0;
            foreach (var dataFrame in DataFrames)
            {
                foreach (var col in dataFrame.DataColumns)
                {

                    for (int iRow = 0; iRow < nPoints; iRow++)
                    {
                        matrix[iRow, iCol] = col[iRow];
                    }

                    iCol++;
                }
            }
            alglib.pcatruncatedsubspace(matrix, nPoints, nVars, maxLevels, .0001, 0, out double[] s2, out double[,] vectors);
            var decomposedVectors = new List<ImmutableList<double>>();
            for (int iRow = 0; iRow < nPoints; iRow++)
            {
                var decomposedVector = new List<double>();
                for (int iComponent = 0; iComponent < vectors.GetLength(1); iComponent++)
                {
                    double dotProduct = 0;
                    for (int iCoordinate = 0; iCoordinate < nVars; iCoordinate++)
                    {
                        dotProduct += matrix[iRow, iCoordinate] * vectors[iCoordinate, iComponent];
                    }
                    decomposedVector.Add(dotProduct);
                }
                decomposedVectors.Add(ImmutableList.ValueOf(decomposedVector));
            }

            return new PcaResults<TRow>(RowLabels, decomposedVectors);

        }

        private PcaResults<TColumn> PerformPca(ImmutableList<DataFrame> dataFrames, int maxLevels)
        {
            ImmutableList<TColumn> columnHeaders = null;
            foreach (var dataFrame in dataFrames)
            {
                if (columnHeaders == null)
                {
                    columnHeaders = dataFrame.ColumnHeaders;
                }
                else
                {
                    if (!Equals(dataFrame.ColumnHeaders, columnHeaders))
                    {
                        throw new ArgumentException(@"Column headers do not match", nameof(dataFrames));
                    }
                }

                if (dataFrame.RowCount != RowCount)
                {
                    throw new ArgumentException(@"Wrong number of rows", nameof(dataFrame));
                }
            }

            if (columnHeaders == null)
            {
                throw new ArgumentException(@"No data", nameof(dataFrames));
            }

            int nPoints = columnHeaders.Count;
            int nVars = RowCount * dataFrames.Count;
            var matrix = new double[nPoints, nVars];
            for (int iColumn = 0; iColumn < columnHeaders.Count; iColumn++)
            {
                for (int iRow = 0; iRow < RowCount; iRow++)
                {
                    for (int iDataFrame = 0; iDataFrame < dataFrames.Count; iDataFrame++)
                    {
                        matrix[iColumn, iRow * dataFrames.Count + iDataFrame] =
                            dataFrames[iDataFrame].DataColumns[iColumn][iRow];
                    }
                }
            }
            alglib.pcatruncatedsubspace(matrix, nPoints, nVars, maxLevels, .0001, 0, out double[] s2, out double[,] vectors);
            var decomposedVectors = new List<ImmutableList<double>>();
            for (int iColumn = 0; iColumn < columnHeaders.Count; iColumn++)
            {
                var decomposedVector = new List<double>();
                for (int iComponent = 0; iComponent < vectors.GetLength(1); iComponent++)
                {
                    double dotProduct = 0;
                    for (int iCoordinate = 0; iCoordinate < nVars; iCoordinate++)
                    {
                        dotProduct += matrix[iColumn, iCoordinate] * vectors[iCoordinate, iComponent];
                    }
                    decomposedVector.Add(dotProduct);
                }
                decomposedVectors.Add(ImmutableList.ValueOf(decomposedVector));
            }

            return new PcaResults<TColumn>(columnHeaders, decomposedVectors);
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

            public ClusterDataSet<TNewRow, TNewColumn>.Results ChangeLabels<TNewRow, TNewColumn>(
                IEnumerable<TNewRow> newRowLabels, IEnumerable<IEnumerable<TNewColumn>> newColumnLabels)
            {
                return new ClusterDataSet<TNewRow, TNewColumn>.Results(DataSet.ChangeLabels(newRowLabels, newColumnLabels), RowDendrogram, ColumnGroupDendrograms);
            }
        }
    }
}
