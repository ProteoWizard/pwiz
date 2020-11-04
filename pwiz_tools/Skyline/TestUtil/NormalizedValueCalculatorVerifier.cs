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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.SkylineTestUtil
{
    public static class NormalizedValueCalculatorVerifier
    {
        /// <summary>
        /// Verifies that the ratios stored on the TransitionChromInfo and TransitionGroupChromInfo's have
        /// the same values that the class <see cref="NormalizedValueCalculator"/> would get.
        /// </summary>
        /// <param name="doc"></param>
        public static void VerifyRatioCalculations(SrmDocument doc)
        {
            var calculator = new NormalizedValueCalculator(doc);
            bool hasGlobalStandardArea = doc.Settings.HasGlobalStandardArea;
            foreach (var molecule in doc.Molecules)
            {
                var adductsAndLabels = new HashSet<Tuple<Adduct, IsotopeLabelType>>();
                foreach (var transitionGroupDocNode in molecule.TransitionGroups)
                {
                    // PeptideChromInfoCalculator.CalcTransitionGroupRatio gets the wrong answer if there is more than one precursor with the same adduct and label
                    bool duplicatePrecursor = !adductsAndLabels.Add(Tuple.Create(
                        transitionGroupDocNode.PrecursorAdduct.Unlabeled, transitionGroupDocNode.LabelType));
                    if (transitionGroupDocNode.Results != null)
                    {
                        foreach (var transitionGroupChromInfo in transitionGroupDocNode.Results.SelectMany(r => r))
                        {
                            AssertEx.AreEqual(transitionGroupChromInfo.Area, calculator.GetTransitionGroupValue(NormalizationMethod.NONE, molecule, transitionGroupDocNode, transitionGroupChromInfo));
                            if (!duplicatePrecursor)
                            {
                                for (int iRatio = 0; iRatio < calculator.RatioInternalStandardTypes.Count; iRatio++)
                                {
                                    float? expected = transitionGroupChromInfo.Ratios[iRatio]?.Ratio;
                                    double? actual = (float?)calculator.GetTransitionGroupValue(
                                        new NormalizationMethod.RatioToLabel(
                                            calculator.RatioInternalStandardTypes[iRatio]), molecule,
                                        transitionGroupDocNode, transitionGroupChromInfo);

                                    AssertNumbersSame(expected, actual);
                                }
                            }

                            if (hasGlobalStandardArea)
                            {
                                AssertEx.AreEqual(calculator.RatioInternalStandardTypes.Count + 1, transitionGroupChromInfo.Ratios.Count);
                                AssertNumbersSame(transitionGroupChromInfo.Ratios.Last()?.Ratio,
                                    calculator.GetTransitionGroupValue(NormalizationMethod.GLOBAL_STANDARDS,
                                        molecule, transitionGroupDocNode, transitionGroupChromInfo));
                            }
                            else
                            {
                                AssertEx.AreEqual(calculator.RatioInternalStandardTypes.Count, transitionGroupChromInfo.Ratios.Count);
                            }
                        }
                    }

                    foreach (var transitionDocNode in transitionGroupDocNode.Transitions)
                    {
                        if (transitionDocNode.Results != null)
                        {
                            foreach (var transitionChromInfo in transitionDocNode.Results.SelectMany(results => results)
                            )
                            {
                                if (!duplicatePrecursor && transitionChromInfo.OptimizationStep == 0)
                                {
                                    for (int iRatio = 0; iRatio < calculator.RatioInternalStandardTypes.Count; iRatio++)
                                    {
                                        float? expected = transitionChromInfo.Ratios[iRatio];
                                        float? actual = (float?)calculator.GetTransitionValue(
                                            new NormalizationMethod.RatioToLabel(
                                                calculator.RatioInternalStandardTypes[iRatio]), molecule,
                                            transitionGroupDocNode, transitionDocNode, transitionChromInfo);

                                        AssertNumbersSame(expected, actual);
                                    }
                                }
                                if (hasGlobalStandardArea)
                                {
                                    Assert.AreEqual(calculator.RatioInternalStandardTypes.Count + 1, transitionChromInfo.Ratios.Count);
                                    AssertNumbersSame(transitionChromInfo.Ratios.Last(),
                                        calculator.GetTransitionValue(NormalizationMethod.GLOBAL_STANDARDS,
                                            molecule, transitionGroupDocNode, transitionDocNode, transitionChromInfo));
                                }
                                else
                                {
                                    Assert.AreEqual(calculator.RatioInternalStandardTypes.Count, transitionChromInfo.Ratios.Count);
                                }

                            }
                        }
                    }
                }
            }
        }

        public static void AssertNumbersSame(double? value1, double? value2)
        {
            if (Equals(value1, value2))
            {
                return;
            }
            AssertEx.AreEqual(value1.HasValue, value2.HasValue);
            if (!value1.HasValue || !value2.HasValue)
            {
                return;
            }

            var delta = Math.Min(Math.Abs(value1.Value), Math.Abs(value2.Value)) / 1e6;
            AssertEx.AreEqual(value1.Value, value2.Value, delta);
        }
    }
}
