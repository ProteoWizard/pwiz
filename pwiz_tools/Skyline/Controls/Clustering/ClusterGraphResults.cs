using System;
using System.Collections.Generic;
using System.Drawing;
using pwiz.Common.Collections;
using pwiz.Common.DataAnalysis.Clustering;
using ZedGraph;

namespace pwiz.Skyline.Controls.Clustering
{
    public class ClusterGraphResults
    {
        public ClusterGraphResults(DendrogramData rowDendrogramData, 
            IEnumerable<Header> rowHeaders,
            IEnumerable<ColumnGroup> columnGroups,
            IEnumerable<Point> points)
        {
            RowDendrogramData = rowDendrogramData;
            RowHeaders = ImmutableList.ValueOf(rowHeaders);
            ColumnGroups = ImmutableList.ValueOf(columnGroups);
            Points = ImmutableList.ValueOf(points);
        }

        public DendrogramData RowDendrogramData { get; private set; }
        public ImmutableList<Header> RowHeaders { get; private set; }
        public ImmutableList<ColumnGroup> ColumnGroups { get; private set; }
        public ImmutableList<Point> Points { get; private set; }


        public int RowCount
        {
            get { return RowHeaders.Count; }
        }
        public class Header
        {
            public Header(string caption, IEnumerable<Color> colors)
            {
                Caption = caption;
                Colors = ImmutableList.ValueOf(colors);
            }
            public string Caption { get; private set; }
            public ImmutableList<Color> Colors { get; private set; }
        }

        public class ColumnGroup
        {
            public ColumnGroup(DendrogramData dendrogramData, IEnumerable<Header> headers)
            {
                DendrogramData = dendrogramData;
                Headers = ImmutableList.ValueOf(headers);
                if (DendrogramData.LeafCount != Headers.Count)
                {
                    throw new ArgumentException(@"Wrong number of headers", nameof(headers));
                }
            }
            public DendrogramData DendrogramData { get; private set; }
            public ImmutableList<Header> Headers { get; private set; }
        }

        public class Point
        {
            public Point(int rowIndex, int columnIndex, Color color)
            {
                RowIndex = rowIndex;
                ColumnIndex = columnIndex;
                Color = color;
            }
            public int ColumnIndex { get; set; }
            public int RowIndex { get; set; }
            public Color Color { get; private set; }
        }
    }
}
