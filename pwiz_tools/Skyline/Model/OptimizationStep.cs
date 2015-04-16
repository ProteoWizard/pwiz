/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.Diagnostics;
using System.Linq;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model
{
    public sealed class OptimizationStep<TReg>
            where TReg : OptimizableRegression
    {
        private OptimizationStep(TReg regression, int step)
        {
            Regression = regression;
            Step = step;
        }

        private TReg Regression { get; set; }
        private int Step { get; set; }
        private double TotalArea { get; set; }

        private void AddArea(double area)
        {
            TotalArea += area;
        }

        public delegate double GetRegressionValue(SrmSettings settings, PeptideDocNode nodePep,
                                                  TransitionGroupDocNode nodeGroup, TReg regression, int step);

        public static double? FindOptimizedValueFromResults(SrmSettings settings,
                                                           PeptideDocNode nodePep,
                                                           TransitionGroupDocNode nodeGroup,
                                                           TransitionDocNode nodeTran,
                                                           OptimizedMethodType methodType,
                                                           GetRegressionValue getRegressionValue)
        {
            // Collect peak area for 
            var dictOptTotals = new Dictionary<TReg, Dictionary<int, OptimizationStep<TReg>>>();
            if (settings.HasResults)
            {
                var chromatograms = settings.MeasuredResults.Chromatograms;
                for (int i = 0; i < chromatograms.Count; i++)
                {
                    var chromSet = chromatograms[i];
                    var regression = chromSet.OptimizationFunction as TReg;
                    if (regression == null)
                        continue;

                    Dictionary<int, OptimizationStep<TReg>> stepAreas;
                    if (!dictOptTotals.TryGetValue(regression, out stepAreas))
                        dictOptTotals.Add(regression, stepAreas = new Dictionary<int, OptimizationStep<TReg>>());

                    if (methodType == OptimizedMethodType.Precursor)
                    {
                        TransitionGroupDocNode[] listGroups = FindCandidateGroups(nodePep, nodeGroup);
                        foreach (var nodeGroupCandidate in listGroups)
                            AddOptimizationStepAreas(nodeGroupCandidate, i, regression, stepAreas);
                    }
                    else if (methodType == OptimizedMethodType.Transition)
                    {
                        IEnumerable<TransitionDocNode> listTransitions = FindCandidateTransitions(nodePep, nodeGroup, nodeTran);
                        foreach (var nodeTranCandidate in listTransitions)
                            AddOptimizationStepAreas(nodeTranCandidate, i, regression, stepAreas);
                    }
                }
            }
            // If no candidate values were found, use the document regressor.
            if (dictOptTotals.Count == 0)
                return null;

            // Get the CE value with the maximum total peak area
            double maxArea = 0;
            double bestValue = 0;
            foreach (var optTotals in dictOptTotals.Values)
            {
                foreach (var optStep in optTotals.Values)
                {
                    if (maxArea < optStep.TotalArea)
                    {
                        maxArea = optStep.TotalArea;
                        bestValue = getRegressionValue(settings, nodePep, nodeGroup, optStep.Regression, optStep.Step);
                    }
                }
            }
            // Use value for candidate with the largest area
            return bestValue;
        }

        public static double FindOptimizedValue(SrmSettings settings,
                                             PeptideDocNode nodePep,
                                             TransitionGroupDocNode nodeGroup,
                                             TransitionDocNode nodeTran,
                                             OptimizedMethodType methodType,
                                             TReg regressionDocument,
                                             GetRegressionValue getRegressionValue)
        {
            double? optimizedValue = FindOptimizedValueFromResults(settings, nodePep, nodeGroup, nodeTran, methodType, getRegressionValue);
            return optimizedValue.HasValue ? optimizedValue.Value : getRegressionValue(settings, nodePep, nodeGroup, regressionDocument, 0);
        }

        // ReSharper disable SuggestBaseTypeForParameter
        private static TransitionGroupDocNode[] FindCandidateGroups(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup)
        // ReSharper restore SuggestBaseTypeForParameter
        {
            if (nodePep.Children.Count == 1)
                return new[] { nodeGroup };
            // Add all precursors with the same charge as the one passed in
            var listCandidates = new List<TransitionGroupDocNode> { nodeGroup };
            foreach (TransitionGroupDocNode nodeGroupCandidate in nodePep.Children)
            {
                if (nodeGroup.TransitionGroup.PrecursorCharge == nodeGroupCandidate.TransitionGroup.PrecursorCharge &&
                        !ReferenceEquals(nodeGroup, nodeGroupCandidate))
                    listCandidates.Add(nodeGroupCandidate);
            }
            return listCandidates.ToArray();
        }

        private static IEnumerable<TransitionDocNode> FindCandidateTransitions(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran)
        {
            var candidateGroups = FindCandidateGroups(nodePep, nodeGroup);
            if (candidateGroups.Length < 2)
                return new[] { nodeTran };
            Debug.Assert(ReferenceEquals(nodeGroup, candidateGroups[0]));
            var listCandidates = new List<TransitionDocNode> { nodeTran };
            var transition = nodeTran.Transition;
            for (int i = 1; i < candidateGroups.Length; i++)
            {
                foreach (TransitionDocNode nodeTranCandidate in candidateGroups[i].Children)
                {
                    var transitionCandidate = nodeTranCandidate.Transition;
                    if (transition.Charge == transitionCandidate.Charge &&
                        transition.Ordinal == transitionCandidate.Ordinal &&
                        transition.IonType == transitionCandidate.IonType)
                    {
                        listCandidates.Add(nodeTranCandidate);
                        break;
                    }
                }
            }
            return listCandidates.ToArray();
        }

        private static void AddOptimizationStepAreas(TransitionGroupDocNode nodeGroup, int iResult, TReg regression,
            IDictionary<int, OptimizationStep<TReg>> optTotals)
        {
            var results = (nodeGroup.HasResults ? nodeGroup.Results[iResult] : null);
            if (results == null)
                return;
            foreach (var chromInfo in results)
            {
                if (!chromInfo.Area.HasValue)
                    continue;
                int step = chromInfo.OptimizationStep;
                OptimizationStep<TReg> optStep;
                if (!optTotals.TryGetValue(step, out optStep))
                    optTotals.Add(step, optStep = new OptimizationStep<TReg>(regression, step));
                optStep.AddArea(chromInfo.Area.Value);
            }
        }

        private static void AddOptimizationStepAreas(TransitionDocNode nodeTran, int iResult, TReg regression,
            IDictionary<int, OptimizationStep<TReg>> optTotals)
        {
            var results = (nodeTran.HasResults ? nodeTran.Results[iResult] : null);
            // Skip the result set if it only has step 0, the predicted value. This happens
            // when someone mistakenly sets "Optimizing" on a data set that does not contain
            // optimization steps.
            if (results == null || results.All(c => c.OptimizationStep == 0 || c.IsEmpty))
                return;
            foreach (var chromInfo in results)
            {
                if (chromInfo.Area == 0)
                    continue;
                int step = chromInfo.OptimizationStep;
                OptimizationStep<TReg> optStep;
                if (!optTotals.TryGetValue(step, out optStep))
                    optTotals.Add(step, optStep = new OptimizationStep<TReg>(regression, step));
                optStep.AddArea(chromInfo.Area);
            }
        }
    }
}
