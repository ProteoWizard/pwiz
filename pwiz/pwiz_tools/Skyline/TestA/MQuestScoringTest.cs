/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    /// <summary>
    /// Tests of MQuest scoring from OpenSWATH project in OpenMS
    /// </summary>
    [TestClass]
    public class MQuestScoringTest : AbstractUnitTest
    {
        private static readonly double[] DATA1 = new double[] {0, 1, 3, 5, 2, 0};
        private static readonly double[] DATA2 = new double[] {1, 3, 5, 2, 0, 0};

        private static void AreCloseEnough(double v1, double v2)
        {
            Assert.IsFalse(double.IsNaN(v1));
            Assert.AreEqual(v2, v1, 1E-5);
        }

        [TestMethod]
        public void StandardizeTest()
        {
            var data1 = new Statistics(DATA1).Standardize();
            var data2 = new Statistics(DATA2).Standardize();

            AreCloseEnough(data1[0], -1.03479296);
            AreCloseEnough(data1[1], -0.47036043);
            AreCloseEnough(data1[2], 0.65850461);
            AreCloseEnough(data1[3], 1.78736965);
            AreCloseEnough(data1[4], 0.09407209);
            AreCloseEnough(data1[5], -1.03479296);

            AreCloseEnough(data2[0], -0.47036043);
            AreCloseEnough(data2[1], 0.65850461);
            AreCloseEnough(data2[2], 1.78736965);
            AreCloseEnough(data2[3], 0.09407209);
            AreCloseEnough(data2[4], -1.03479296);
            AreCloseEnough(data2[5], -1.03479296);
        }

        /// <summary>
        /// A test for cross-correlation function with normalization
        /// </summary>
        [TestMethod]
        public void CrossCorrelationNormalizedTest()
        {
            var stat1 = new Statistics(DATA1);
            var stat2 = new Statistics(DATA2);

            var result = stat1.CrossCorrelation(stat2, true);

            ValidateXCorr(result);
        }

        /// <summary>
        /// A test for cross-correlation function with normalization
        /// </summary>
        [TestMethod]
        public void CrossCorrelationTest()
        {
            var stat1 = new Statistics(new Statistics(DATA1).Standardize());
            var stat2 = new Statistics(new Statistics(DATA2).Standardize());

            var result = stat1.CrossCorrelation(stat2, false);
            foreach (var pair in result.ToArray())
                result[pair.Key] = pair.Value / 6.0;

            ValidateXCorr(result);
        }

        private static void ValidateXCorr(Dictionary<int, double> result)
        {
            AreCloseEnough(result[2], -0.7374631);
            AreCloseEnough(result[1], -0.567846);
            AreCloseEnough(result[0], 0.4159292);
            AreCloseEnough(result[-1], 0.8215339);
            AreCloseEnough(result[-2], 0.15634218);
        }

        private static readonly double[] INTENS1 =
            {
                5.97543668746948, 4.2749171257019, 3.3301842212677, 4.08597040176392,
                5.50307035446167, 5.24326848983765, 8.40812492370605, 2.83419919013977,
                6.94378805160522, 7.69957494735718, 4.08597040176392
            };

        private static readonly double[] INTENS2 =
            {
                15.8951349258423, 41.5446395874023, 76.0746307373047, 109.069435119629,
                111.90364074707, 169.79216003418, 121.043930053711, 63.0136985778809,
                44.6150207519531, 21.4926776885986, 7.93575811386108
            };

        /// <summary>
        /// A test for MQuest cross-correlation matrix
        /// </summary>
        [TestMethod]
        public void MQuestCrossCorrelationTest()
        {
            var peakData1 = new MockTranPeakData(INTENS1);
            var peakData2 = new MockTranPeakData(INTENS2);
            var xcorrMatrix = new MQuestCrossCorrelation(peakData1, peakData2, true);

            Assert.AreEqual(xcorrMatrix.XcorrDict.Count, 23);

            var xcorrDict = xcorrMatrix.XcorrDict;
            AreCloseEnough(xcorrDict[2], -0.31165141);
            AreCloseEnough(xcorrDict[1], -0.35036919);
            AreCloseEnough(xcorrDict[0], 0.03129565);
            AreCloseEnough(xcorrDict[-1], 0.30204049);
            AreCloseEnough(xcorrDict[-2], 0.13012441);
            AreCloseEnough(xcorrDict[-3], 0.39698322);
            AreCloseEnough(xcorrDict[-4], 0.16608774);

            var xcorrMatrixAuto = new MQuestCrossCorrelation(peakData1, new MockTranPeakData(INTENS1), true);
            var xcorrDictAuto = xcorrMatrixAuto.XcorrDict;
            AreCloseEnough(xcorrDictAuto[0], 1);
            AreCloseEnough(xcorrDictAuto[1], -0.227352707759245);
            AreCloseEnough(xcorrDictAuto[-1], -0.227352707759245);
            AreCloseEnough(xcorrDictAuto[2], -0.07501116);
            AreCloseEnough(xcorrDictAuto[-2], -0.07501116);
        }

        /// <summary>
        /// A test for MQuest co-elution score calculator
        /// </summary>
        [TestMethod]
        public void MQuestCoelutionScoreTest()
        {
            var peakData1 = new MockTranPeakData(INTENS1);
            var peakData2 = new MockTranPeakData(INTENS2);
            var peptidePeakData = new MockPeptidePeakData(new[] {peakData1, peakData2});
            var calc = new MQuestCoElutionCalc();
            AreCloseEnough(calc.Calculate(new PeakScoringContext(null), peptidePeakData), 3.0);
//            AreCloseEnough(calc.Calculate(new PeakScoringContext(), peptidePeakData), 1 + Math.Sqrt(3.0));

            // TODO: Figure out OpenSWATH weights
//            var calcWeighted = new MQuestWeightedCoElutionCalc();
//            AreCloseEnough(calcWeighted.Calculate(new PeakScoringContext(), peptidePeakData), 1.5);
        }

        /// <summary>
        /// A test for MQuest co-elution score calculator
        /// </summary>
        [TestMethod]
        public void MQuestShapeScoreTest()
        {
            var peakData1 = new MockTranPeakData(INTENS1);
            var peakData2 = new MockTranPeakData(INTENS2);
            var peptidePeakData = new MockPeptidePeakData(new[] { peakData1, peakData2 });
            var calc = new MQuestShapeCalc();
            AreCloseEnough(calc.Calculate(new PeakScoringContext(null), peptidePeakData), 0.3969832);
//            AreCloseEnough(calc.Calculate(new PeakScoringContext(), peptidePeakData), (1 + 0.3969832 + 1) / 3.0);

            // TODO: Figure out OpenSWATH weights
//            var calcWeighted = new MQuestWeightedShapeCalc();
//            AreCloseEnough(calcWeighted.Calculate(new PeakScoringContext(), peptidePeakData), 0.6984916);
        }

        private class MockPeptidePeakData : IPeptidePeakData<IDetailedPeakData>
        {
            public MockPeptidePeakData(IList<ITransitionPeakData<IDetailedPeakData>> transitionPeakData)
            {
                TransitionGroupPeakData = new[] { new MockTranGroupPeakData(transitionPeakData), };
            }

            public PeptideDocNode NodePep { get { return null; } }

            public ChromFileInfo FileInfo { get { return null; } }

            public IList<ITransitionGroupPeakData<IDetailedPeakData>> TransitionGroupPeakData { get; private set; }
        }

        private class MockTranGroupPeakData : ITransitionGroupPeakData<IDetailedPeakData>
        {
            public MockTranGroupPeakData(IList<ITransitionPeakData<IDetailedPeakData>> transtionPeakData)
            {
                TranstionPeakData = transtionPeakData;
            }

            public TransitionGroupDocNode NodeGroup { get { return null; } }
            public bool IsStandard { get { return false; } }
            public IList<ITransitionPeakData<IDetailedPeakData>> TranstionPeakData { get; private set; }
        }

        private class MockTranPeakData : ITransitionPeakData<IDetailedPeakData>
        {
            public MockTranPeakData(double[] data)
            {
                PeakData = new MockPeakData(data);
            }

            public TransitionDocNode NodeTran { get { return null; } }

            public IDetailedPeakData PeakData { get; private set; }

            private class MockPeakData : IDetailedPeakData
            {
                private readonly float[] _data;

                public MockPeakData(double[] data)
                {
                    StartTime = 5;
                    EndTime = StartTime + data.Length;

                    _data = new float[data.Length];
                    double? lastIntensity = null;
                    double area = 0;
                    for (int i = 0; i < data.Length; i++)
                    {
                        double intensity = data[i];
                        if (intensity > Height)
                        {
                            Height = (float) intensity;
                            RetentionTime = StartTime + i;
                        }
                        if (lastIntensity.HasValue)
                        {
                            // Calculate trapezoidal area with time units equal to 1
                            area += Math.Min(intensity, lastIntensity.Value) +
                                    Math.Abs(intensity - lastIntensity.Value)/2;
                        }
                        lastIntensity = intensity;
                        _data[i] = (float)data[i];
                    }
                    int last = data.Length - 1;
                    // Background is simple rectangular area with minimum edge
                    BackgroundArea = (float) Math.Min(data[0], data[last])*(last);
                    Area = (float) (area - BackgroundArea);
                }

                public float RetentionTime { get; private set; }
                public float StartTime { get; private set; }
                public float EndTime { get; private set; }
                public float Area { get; private set; }
                public float BackgroundArea { get; private set; }
                public float Height { get; private set; }
                public float Fwhm { get { throw new InvalidOperationException(); } }
                public bool IsEmpty { get { return false; } }
                public bool IsFwhmDegenerate { get { return false; } }
                public bool IsForcedIntegration { get { return false; } }
                public PeakIdentification Identified { get { return PeakIdentification.FALSE; } }
                public bool? IsTruncated { get { return null; } }
                public int TimeIndex { get { return _data.Length/2; } }
                public int EndIndex { get { return _data.Length - 1; } }
                public int StartIndex { get { return 0; } }
                public int Length { get { return _data.Length; } }
                public float[] Times { get { throw new InvalidOperationException(); } }
                public float[] Intensities { get { return _data; } }
            }
        }
    }
}