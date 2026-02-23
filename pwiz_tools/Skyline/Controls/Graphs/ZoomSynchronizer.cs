/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using pwiz.Skyline.Properties;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public class ZoomSynchronizer
    {
        public static readonly ZoomSynchronizer INSTANCE = new ZoomSynchronizer();
        private List<GraphHelper> _graphs = new List<GraphHelper>();

        public void RegisterGraph(GraphHelper graph)
        {
            if (_graphs.Contains(graph))
            {
                throw new ArgumentException(@"Graph already registered");
            }
            _graphs.Add(graph);
        }

        public void UnregisterGraph(GraphHelper graph)
        {
            if (!_graphs.Remove(graph))
            {
                throw new ArgumentException(@"Graph not registered");
            }
        }

        public void OnZoom(GraphHelper sender)
        {
            if (!Settings.Default.AutoZoomAllChromatograms)
            {
                SynchronizedZoomState = null;
                return;
            }

            SynchronizedZoomState = sender.GetZoomStates();
            foreach (var graphHelper in _graphs.ToList())
            {
                if (!ReferenceEquals(graphHelper, sender))
                {
                    graphHelper.OnSynchronizedZoom();
                }
            }
        }

        public Dictionary<PaneKey, ZoomStateStack> SynchronizedZoomState { get; private set; }
    }
}
