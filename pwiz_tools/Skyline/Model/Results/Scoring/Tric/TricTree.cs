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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Results.Scoring.Tric
{
    public abstract class TricTree
    {
        protected IDictionary<int, string> _fileNames;
        protected IList<int> _fileIndexes;
        protected List<Edge> _edges;
        protected Dictionary<int, Vertex> _vertices;
        protected List<Edge> _tree;
        protected double _anchorCutoff;
        protected IEnumerable<PeptideFileFeatureSet> _peptides;
        private readonly RegressionMethodRT _regressionMethod;

        protected TricTree(IEnumerable<PeptideFileFeatureSet> peptides, IDictionary<int,string> fileNames, IList<int> fileIndexes, double anchorCutoff,
            RegressionMethodRT regressionMethod, IProgressMonitor progressMonitor, ref IProgressStatus status, bool verbose = false)
        {
            _fileNames = fileNames;
            _fileIndexes = fileIndexes;
            _peptides = peptides;
            _anchorCutoff = anchorCutoff;
            _regressionMethod = regressionMethod;
            _vertices = new Dictionary<int, Vertex>();

            Initialize(progressMonitor, ref status, verbose);
        }

        private void Initialize(IProgressMonitor progressMonitor, ref IProgressStatus status, bool verbose = false)
        {
            if (progressMonitor != null)
                status = status.ChangeMessage(Resources.TricTree_TricTree_Calculating_retention_time_alignment_edges);
            foreach (var fileIndex in _fileIndexes)
            {
                //Runs are not zero based
                _vertices.Add(fileIndex, new Vertex(fileIndex));
            }
            CalculateEdgeWeights(progressMonitor, ref status, Tric.PERCENT_EDGE_WEIGHTS);
            ScoreFiles();
            // ReSharper disable once VirtualMemberCallInConstructor
            LearnTree(progressMonitor, ref status, Tric.PERCENT_LEARN_TREE);
            TrainAligners(progressMonitor, ref status, Tric.PERCENT_TRAIN_ALINGERS, verbose);
        }

        protected abstract void LearnTree(IProgressMonitor progressMonitor, ref IProgressStatus status, int percentRange);

        protected abstract void CalculateEdgeWeights(IProgressMonitor pm, ref IProgressStatus status, int percentRange);

        //For now we will not use a progress monitor since it's unlikely that scoring a vertice would take
        //much time
        protected abstract void ScoreFiles();
       
        protected void GetAnchorPoints(int indFileIndex, int depFileIndex, List<double> indList, List<double> depList)
        {
            indList.Clear();
            depList.Clear();

            foreach (var peptideFileFeatures in _peptides)
            {
                TricFileFeatureStatistics indStat, depStat;
                var fileToFeatures = peptideFileFeatures.FileFeatures;
                if (!fileToFeatures.TryGetValue(indFileIndex, out indStat) || !fileToFeatures.TryGetValue(depFileIndex, out depStat))
                    continue;
                if(indStat.Features.IsDecoy)
                    continue;
                
                int indAnchors = 0;
                int depAnchors = 0;
                foreach (var score in indStat.FeatureStats.MprophetScores)
                {
                    if (score > _anchorCutoff)
                    {
                        indAnchors++;
                    }
                }
                if (indAnchors == 0 || indAnchors >= 2)
                {
                    continue;
                }
                foreach (var score in depStat.FeatureStats.MprophetScores)
                {
                    if (score > _anchorCutoff)
                    {
                        depAnchors++;
                    }
                }
                if (depAnchors == 0 || depAnchors >= 2)
                {
//                    continue;  // redundant
                }
                else
                {
                    indList.Add(indStat.Features.PeakGroupFeatures[indStat.FeatureStats.BestScoreIndex].MedianRetentionTime);
                    depList.Add(depStat.Features.PeakGroupFeatures[depStat.FeatureStats.BestScoreIndex].MedianRetentionTime);
                }
            }
        }

        private void TrainAligners(IProgressMonitor progressMonitor, ref IProgressStatus status, int percentRange, bool verbose = false)
        {
            int startPercent = status.PercentComplete;
            if (progressMonitor != null)
                progressMonitor.UpdateProgress(status = status.ChangeMessage(Resources.TricTree_TrainAligners_Calculating_retention_time_alignment_functions));

            int edgesCompleted = 0;
            var aList = new List<double>();
            var bList = new List<double>();
            foreach (var edge in _tree)
            {
                GetAnchorPoints(edge.AVertex.FileIndex, edge.BVertex.FileIndex, aList, bList);
                edge.Aligner = GetAligner(edge.AVertex.FileIndex, edge.BVertex.FileIndex);
                edge.Aligner.Train(aList.ToArray(), bList.ToArray());
                edgesCompleted++;

                if (verbose)
                {
                    var nameA = _fileNames[edge.AVertex.FileIndex];
                    var nameB = _fileNames[edge.BVertex.FileIndex];
                    Console.WriteLine(Resources.TricTree_TrainAligners__0______1__RMSD__2_, nameA, nameB,
                        edge.Aligner.GetRmsd() * 60);
                }

                if (progressMonitor != null)
                {
                    int percentComplete = startPercent + edgesCompleted*percentRange / _tree.Count;
                    progressMonitor.UpdateProgress(status = status.ChangePercentComplete(percentComplete));
                }
            }
        }

        private Aligner GetAligner(int aFileIndex, int bFileIndex)
        {
            switch (_regressionMethod)
            {
                case RegressionMethodRT.linear:
                    return new LinearAligner(aFileIndex, bFileIndex);
                case RegressionMethodRT.kde:
                    return new KdeAligner(aFileIndex, bFileIndex);
                case RegressionMethodRT.loess:
                    return new LoessAligner(aFileIndex, bFileIndex, 0.1, 3);
                default:
                    throw new NotSupportedException();
            }
        }

        public List<DirectionalEdge> Traverse(int startFileIndex,IDictionary<int, string> fileNames, bool verbose = false)
        {
            var traversal = new List<DirectionalEdge>();
            var visited = new HashSet<int>();
            var q = new Queue<Vertex>();
            q.Enqueue(_vertices[startFileIndex]);
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                visited.Add(cur.FileIndex);
                for (int i = 0; i < cur.NeighborRuns.Count; i ++)
                {
                    var fileIndex = cur.NeighborRuns[i];
                    if (!visited.Contains(fileIndex))
                    {
                        q.Enqueue(_vertices[fileIndex]);
                        traversal.Add(new DirectionalEdge(cur.FileIndex, fileIndex, cur.Connections[i]));
                    }
                }
            }
            if (verbose)
            {
                foreach (var edge in traversal)
                {
                    var nameA = fileNames[edge.SourceFileIndex];
                    var nameB = fileNames[edge.TargetFileIndex];
                    var rmsd = edge.Rmsd * 60;
                    Console.WriteLine(Resources.TricTree_Traverse__0______1____RMSD__2_,nameA, nameB,rmsd);    
                }
            }
            return traversal;
        }

        public class DirectionalEdge
        {
            private readonly Edge _edge;
            private readonly int _sourceFileIndex;
            private readonly int _targetFileIndex;

            public DirectionalEdge(int sourceFileIndex, int targetFileIndex, Edge edge)
            {
                _sourceFileIndex = sourceFileIndex;
                _targetFileIndex = targetFileIndex;
                _edge = edge;
                Rmsd = edge.Aligner.GetRmsd();
            }
            public int SourceFileIndex { get { return _sourceFileIndex; } }
            public int TargetFileIndex { get { return _targetFileIndex; } }
            public double Rmsd { get; private set; }

            public double Transform(double sourceRT)
            {
                return _edge.Aligner.GetValueFor(sourceRT, _sourceFileIndex);
            }
        }

        public class Edge
        {
            public Edge(Vertex a, Vertex b, double weight)
            {
                AVertex = a;
                BVertex = b;
                Weight = weight;
            }
            public Aligner Aligner { get; set; }
            public Vertex AVertex { get; private set; }
            public Vertex BVertex { get; private set; }
            public double Weight { get; private set; }
        }

        public class Vertex
        {
            private int _rank;
            private Vertex _parent;
            public Vertex(int fileIndex)
            {
                FileIndex = fileIndex;
                NeighborRuns = new List<int>();
                Connections = new List<Edge>();
                Score = 0;
            }

            public int FileIndex { get; private set; }
            public IList<int> NeighborRuns { get; private set; }
            public IList<Edge> Connections { get; private set; }
            
            //Score for a vertice. Used for tric star tree
            public double Score { get; internal set; }

            public Vertex FindRoot()
            {
                return FindRootOfVertex(this);
            }

            private static Vertex FindRootOfVertex(Vertex cur)
            {
                if (cur._parent == null)
                {
                    return cur;
                }
                else
                {
                    cur._parent = FindRootOfVertex(cur._parent);
                    return cur._parent;
                }
            }

            public static Vertex Union(Vertex a, Vertex b)
            {
                var pA = a.FindRoot();
                var pB = b.FindRoot();

                if (pA._rank > pB._rank)
                {
                    pB._parent = pA;
                    return pA;
                }
                else if (pA._rank < pB._rank)
                {
                    pA._parent = pB;
                    return pB;
                }
                else
                {
                    pB._parent = pA;
                    pA._rank++;
                    return pA;
                }
            }

            public static bool InSameSet(Vertex a, Vertex b)
            {
                return a.FindRoot() == b.FindRoot();
            }
        }
    }
}
