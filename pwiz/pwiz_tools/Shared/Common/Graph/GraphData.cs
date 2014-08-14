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
using System.Collections.Generic;
using System.IO;
using ZedGraph;
using pwiz.Common.Collections;

namespace pwiz.Common.Graph
{
    public class GraphData
    {
        public static GraphData GetGraphData(MasterPane masterPane)
        {
            var paneDatas = new List<GraphPaneData>();
            foreach (var graphPane in masterPane.PaneList)
            {
                var graphPaneData = GraphPaneData.GetGraphPaneData(graphPane);
                if (graphPaneData != null)
                {
                    paneDatas.Add(graphPaneData);
                }
            }
            return new GraphData(null, paneDatas);
        }

        public GraphData(string name, IEnumerable<GraphPaneData> panes)
        {
            Name = name;
            Panes = ImmutableList.ValueOf(panes);
        }

        public string Name { get; private set; }
        public IList<GraphPaneData> Panes { get; private set; }
        public override string ToString()
        {
            var stringWriter = new StringWriter();
            Write(stringWriter, "\t"); // Not L10N
            return stringWriter.ToString();
        }
        public void Write(TextWriter writer, string separator)
        {
            foreach (var pane in Panes)
            {
                if (!string.IsNullOrEmpty(pane.Title))
                {
                    writer.WriteLine(pane.Title);
                }
                foreach (var dataFrame in pane.DataFrames)
                {
                    dataFrame.Write(writer, separator);
                }
            }
        }
    }
}
