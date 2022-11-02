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
using System.Drawing;
using pwiz.Common.Collections;
using pwiz.Common.DataAnalysis.Clustering;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;

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

        public class Point : Immutable
        {
            public Point(int rowIndex, int columnIndex, Color? color)
            {
                RowIndex = rowIndex;
                ColumnIndex = columnIndex;
                Color = color;
            }
            public int ColumnIndex { get; set; }
            public int RowIndex { get; set; }
            public Color? Color { get; private set; }
            public IdentityPath IdentityPath { get; private set; }

            public Point ChangeIdentityPath(IdentityPath identityPath)
            {
                return ChangeProp(ImClone(this), im => im.IdentityPath = identityPath);
            }
            public string ReplicateName { get; private set; }

            public Point ChangeReplicateName(string replicateName)
            {
                return ChangeProp(ImClone(this), im => im.ReplicateName = replicateName);
            }
        }
    }
}
