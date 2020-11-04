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
    public static class RatioCalculatorVerifier
    {
        public static void VerifyRatioCalculations(SrmDocument doc)
        {
            var ratioCalculator = new RatioCalculator(doc) {BugCompatibility = true};
            bool hasGlobalStandardArea = doc.Settings.HasGlobalStandardArea;
            foreach (var molecule in doc.Molecules)
            {
                var adductsAndLabels = new HashSet<Tuple<Adduct, IsotopeLabelType>>();
                foreach (var transitionGroupDocNode in molecule.TransitionGroups)
                {
                    // TODO: PeptideChromInfoCalculator.CalcTransitionGroupRatio gets the wrong answer if there is more than one precursor with the same adduct and label
                    bool duplicatePrecursor = !adductsAndLabels.Add(Tuple.Create(
                        transitionGroupDocNode.PrecursorAdduct.Unlabeled, transitionGroupDocNode.LabelType));
                    if (transitionGroupDocNode.Results != null)
                    {
                        foreach (var transitionGroupChromInfo in transitionGroupDocNode.Results.SelectMany(r => r))
                        {
                            AssertEx.AreEqual(transitionGroupChromInfo.Area, ratioCalculator.GetTransitionGroupValue(NormalizationMethod.NONE, molecule, transitionGroupDocNode, transitionGroupChromInfo));
                            if (!duplicatePrecursor)
                            {
                                for (int iRatio = 0; iRatio < ratioCalculator.RatioInternalStandardTypes.Count; iRatio++)
                                {
                                    float? expected = transitionGroupChromInfo.Ratios[iRatio]?.Ratio;
                                    double? actual = (float?)ratioCalculator.GetTransitionGroupValue(
                                        new NormalizationMethod.RatioToLabel(
                                            ratioCalculator.RatioInternalStandardTypes[iRatio]), molecule,
                                        transitionGroupDocNode, transitionGroupChromInfo);

                                    AssertNumbersSame(expected, actual);
                                }
                            }

                            if (hasGlobalStandardArea)
                            {
                                AssertEx.AreEqual(ratioCalculator.RatioInternalStandardTypes.Count + 1, transitionGroupChromInfo.Ratios.Count);
                                AssertNumbersSame(transitionGroupChromInfo.Ratios.Last()?.Ratio,
                                    ratioCalculator.GetTransitionGroupValue(NormalizationMethod.GLOBAL_STANDARDS,
                                        molecule, transitionGroupDocNode, transitionGroupChromInfo));
                            }
                            else
                            {
                                AssertEx.AreEqual(ratioCalculator.RatioInternalStandardTypes.Count, transitionGroupChromInfo.Ratios.Count);
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
                                    for (int iRatio = 0; iRatio < ratioCalculator.RatioInternalStandardTypes.Count; iRatio++)
                                    {
                                        float? expected = transitionChromInfo.Ratios[iRatio];
                                        float? actual = (float?)ratioCalculator.GetTransitionValue(
                                            new NormalizationMethod.RatioToLabel(
                                                ratioCalculator.RatioInternalStandardTypes[iRatio]), molecule,
                                            transitionGroupDocNode, transitionDocNode, transitionChromInfo);

                                        AssertNumbersSame(expected, actual);
                                    }
                                }
                                if (hasGlobalStandardArea)
                                {
                                    Assert.AreEqual(ratioCalculator.RatioInternalStandardTypes.Count + 1, transitionChromInfo.Ratios.Count);
                                    AssertNumbersSame(transitionChromInfo.Ratios.Last(),
                                        ratioCalculator.GetTransitionValue(NormalizationMethod.GLOBAL_STANDARDS,
                                            molecule, transitionGroupDocNode, transitionDocNode, transitionChromInfo));
                                }
                                else
                                {
                                    Assert.AreEqual(ratioCalculator.RatioInternalStandardTypes.Count, transitionChromInfo.Ratios.Count);
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
