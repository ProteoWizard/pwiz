
/*
 * Original author: Max Horowitz-Gelb <maxhg .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Scoring.Tric
{
    /// <summary>
    /// Minimum spanning tree used by the TRIC algorithm.
    /// The vertices are replicates
    /// The edges are retention time alignments between replicates
    /// 
    /// We calculate all pairwise linear regression between runs.  The weight of each edge is 1-(RSquared of the retention
    /// time alignment).
    /// The tree is reduced to the set of edges that have the lowest weights.
    /// The remaining edges have a non-linear (currently loess) alignment done between them.
    /// </summary>
    public sealed class TricMst : TricTree
    {
        public TricMst(IEnumerable<PeptideFileFeatureSet>  peptides,
                        IDictionary<int,string> fileNames,
                        IList<int> fileIndexes,
                        double anchorCutoff,
                        RegressionMethodRT regressionMethod,
                        IProgressMonitor progressMonitor,
                        ref IProgressStatus status, bool verbose = false)
            : base(peptides, fileNames, fileIndexes, anchorCutoff, regressionMethod, progressMonitor, ref status, verbose)
        {
            //Never add code here
        }

        private void FindMst(IProgressMonitor pm, IProgressStatus status, int percentRange)
        {
            int startPercent = status.PercentComplete;

            _tree = new List<Edge>();
            int edgesAdded = 0;
            //Kruskal algorithm
            foreach (var edge in _edges.OrderBy(e => e.Weight))
            {
                if (!Vertex.InSameSet(edge.AVertex, edge.BVertex))
                {
                    Vertex.Union(edge.AVertex, edge.BVertex);
                    _tree.Add(edge);
                    edgesAdded++;
                    if (pm != null)
                    {
                        pm.UpdateProgress(
                            status = status.ChangePercentComplete(startPercent + percentRange*edgesAdded/_fileIndexes.Count));
                    }
                    if (edgesAdded >= _fileIndexes.Count - 1)
                    {
                        break;
                    }
                }
            }
            if (edgesAdded < _fileIndexes.Count - 1)
            {
                throw new Exception(
                        Resources.TricMst_FindMst_Insufficient_high_quality_identifications_to_make_a_quality_alignment);
                    // Not L10N
            }
            foreach (var edge in _tree)
            {
                edge.AVertex.NeighborRuns.Add(edge.BVertex.FileIndex);
                edge.BVertex.NeighborRuns.Add(edge.AVertex.FileIndex);
                edge.AVertex.Connections.Add(edge);
                edge.BVertex.Connections.Add(edge);
            }
        }

        protected override void LearnTree(IProgressMonitor progressMonitor, ref IProgressStatus status, int percentRange)
        {
            FindMst(progressMonitor, status, percentRange);
        }

        protected override void CalculateEdgeWeights(IProgressMonitor pm, ref IProgressStatus status, int percentRange)
        {
            int startPercent = status.PercentComplete;

           

            // O(n^2) algorithm for calculating weights of edges between all runs and all other runs
            // The weigths are 1 - r^2 of a simple linear regression, because it is too
            // slow to calculate correlation with non-linear regressions, which may be used
            // for the actual alignment.
            _edges = new List<Edge>(_fileIndexes.Count*(_fileIndexes.Count - 1));
            var depList = new List<double>();
            var indList = new List<double>();
            int regressionsCounter = 0;
            foreach(var fileIndexFrom in _fileIndexes)
            {
                foreach (var fileIndexTo in _fileIndexes)
                {
                    if(fileIndexTo == fileIndexFrom)
                        continue;
                    GetAnchorPoints(fileIndexFrom, fileIndexTo, indList, depList);
                    var depStats = new Statistics(depList);
                    var indStats = new Statistics(indList);
                    double R = double.NaN;
                    if (depList.Count > 3)
                        R = Statistics.R(indStats, depStats);
                    if (!double.IsNaN(R))
                    {
                        var edge = new Edge(_vertices[fileIndexFrom], _vertices[fileIndexTo], 1 - R*R);
                        _edges.Add(edge);
                    }
                }
                regressionsCounter++;
                if (pm != null)
                    pm.UpdateProgress(status = status.ChangePercentComplete(startPercent + regressionsCounter*percentRange/_fileIndexes.Count));
            }
        }

        protected override void ScoreFiles()
        {
            //Do nothing
        }
    }
}