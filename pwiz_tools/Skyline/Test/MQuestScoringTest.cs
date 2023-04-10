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
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.ChromLib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Tests of MQuest scoring from OpenSWATH project in OpenMS
    /// </summary>
    [TestClass]
    public class MQuestScoringTest : AbstractUnitTest
    {
        private static readonly double[] DATA1 = {0, 1, 3, 5, 2, 0};
        private static readonly double[] DATA2 = {1, 3, 5, 2, 0, 0};

        private static void AreCloseEnough(double v1, double v2)
        {
            if (double.IsNaN(v1) || double.IsNaN(v2))
            {
                Assert.IsTrue(double.IsNaN(v1), string.Format("Expected NaN but v1 = {0}", v1));
                Assert.IsTrue(double.IsNaN(v2), string.Format("Expected NaN but v2 = {0}", v2));
            }
            else
            {
                Assert.AreEqual(v2, v1, 1E-5);
            }
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

        private static readonly double[] INTENS3 =
            {
                10, 20, 50, 100, 150, 150, 100, 50, 20, 10, 5
            };

        private static readonly double[] INTENS0 =
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            };

        /// <summary>
        /// A test for MQuest cross-correlation matrix
        /// </summary>
        [TestMethod]
        public void MQuestCrossCorrelationTest()
        {
            var peakData1 = new MockTranPeakData<IDetailedPeakData>(INTENS1);
            var peakData2 = new MockTranPeakData<IDetailedPeakData>(INTENS2);
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

            var xcorrMatrixAuto = new MQuestCrossCorrelation(peakData1, new MockTranPeakData<IDetailedPeakData>(INTENS1), true);
            var xcorrDictAuto = xcorrMatrixAuto.XcorrDict;
            AreCloseEnough(xcorrDictAuto[0], 1);
            AreCloseEnough(xcorrDictAuto[1], -0.227352707759245);
            AreCloseEnough(xcorrDictAuto[-1], -0.227352707759245);
            AreCloseEnough(xcorrDictAuto[2], -0.07501116);
            AreCloseEnough(xcorrDictAuto[-2], -0.07501116);
        }

        /// <summary>
        /// A test for MQuest and next-generation summary scores
        /// </summary>
        /// 
        [TestMethod]
        public void MQuestSummaryScoreTest()
        {
            var peakData1 = new MockTranPeakData<ISummaryPeakData>(INTENS1, IonType.a, null, 2, 0.5, 20);
            var peakData2 = new MockTranPeakData<ISummaryPeakData>(INTENS2, IonType.a, null, 2, -1.0, 300);
            var peakData2a = new MockTranPeakData<ISummaryPeakData>(INTENS2, IonType.a, null, 2, -1.0, 300);
            var peakData3 = new MockTranPeakData<ISummaryPeakData>(INTENS3, IonType.precursor, null, 2, 5.0, 1000000, 0.8);
            var peakData4 = new MockTranPeakData<ISummaryPeakData>(INTENS3, IonType.precursor, null, 2, 5.0, 1000000, 0.2);
            var peakData4a = new MockTranPeakData<ISummaryPeakData>(INTENS3, IonType.precursor, null, 2, 5.0, 1000000, 0.2);
            var peptidePeakData = new MockPeptidePeakData<ISummaryPeakData>(new[] { peakData1, peakData2 });
            var peptidePeakDataFull = new MockPeptidePeakData<ISummaryPeakData>(new[] { peakData1, peakData2, peakData2a });
            var peptidePeakDataMs1 = new MockPeptidePeakData<ISummaryPeakData>(new[] { peakData1, peakData2, peakData3, peakData4 });
            var peptidePeakDataMs1Full = new MockPeptidePeakData<ISummaryPeakData>(new[] { peakData1, peakData2, peakData2a, peakData3, peakData4, peakData4a });
            var calcPrecursorMassError = new NextGenPrecursorMassErrorCalc();
            var calcProductMassError = new NextGenProductMassErrorCalc();
            var calcIntensity = new MQuestIntensityCalc();
            var calcCrossCorr = new MQuestIntensityCorrelationCalc();
            var calcIdotp = new NextGenIsotopeDotProductCalc();

            // These scores are the same with or without an extra MS1 transition
            foreach (var peptideData in new[] { peptidePeakData, peptidePeakDataMs1 })
            {
                MQuestScoreEquals(calcProductMassError, 0.99112, peptideData);
                MQuestScoreEquals(calcIntensity, 2.84732, peptideData);
                MQuestScoreEquals(calcCrossCorr, double.NaN, peptideData);  // Not enough fragment ions
            }
            foreach (var peptideData in new[] {peptidePeakDataFull, peptidePeakDataMs1Full})
            {
                MQuestScoreEquals(calcCrossCorr, 0.945381, peptideData);
            }
            // The precursor mass error score differs when an MS1 transition is added
            MQuestScoreEquals(calcPrecursorMassError, double.NaN, peptidePeakData);
            MQuestScoreEquals(calcPrecursorMassError, 5.0, peptidePeakDataMs1);
            MQuestScoreEquals(calcIdotp, double.NaN, peptidePeakData);
            MQuestScoreEquals(calcIdotp, double.NaN, peptidePeakDataMs1);   // Not enough MS1 transitions
            MQuestScoreEquals(calcIdotp, 0.783653, peptidePeakDataMs1Full);

            var peakData1NoArea = new MockTranPeakData<ISummaryPeakData>(INTENS0, IonType.a, null, 2, 0.5);
            var peakData2NoArea = new MockTranPeakData<ISummaryPeakData>(INTENS0, IonType.a, null, 2, -1.0);
            MQuestScoreEquals(calcProductMassError, 0.75,
                new MockPeptidePeakData<ISummaryPeakData>(new[] {peakData1NoArea, peakData2NoArea}));

            // TODO: Figure out OpenSWATH weights
            // var calcWeighted = new MQuestWeightedShapeCalc();
            // AreCloseEnough(calcWeighted.Calculate(new PeakScoringContext(), peptidePeakData), 0.6984916);
        }

        /// <summary>
        /// A test for MQuest and next-generation non-reference-based detail scores
        /// </summary>
        [TestMethod]
        public void MQuestDetailScoreTest()
        {
            var peakData1 = new MockTranPeakData<IDetailedPeakData>(INTENS1, IonType.a, null, 2, 0.5);
            var peakData2 = new MockTranPeakData<IDetailedPeakData>(INTENS2, IonType.a, null, 2, -1.0);
            var peakData3 = new MockTranPeakData<IDetailedPeakData>(INTENS3, IonType.precursor, null, 2, 5.0);
            var peptidePeakData = new MockPeptidePeakData<IDetailedPeakData>(new[] { peakData1, peakData2 });
            var peptidePeakDataMs1 = new MockPeptidePeakData<IDetailedPeakData>(new[] { peakData1, peakData2, peakData3 });
            var calcShape = new MQuestShapeCalc();
            var calcCoelution = new MQuestCoElutionCalc();
            var calcWeightedShape = new MQuestWeightedShapeCalc();
            var calcWeightedCoelution = new MQuestWeightedCoElutionCalc();
            var calcWeightedCrossShape = new NextGenCrossWeightedShapeCalc();
            var calcSignalNoise = new NextGenSignalNoiseCalc();

            var peptidePeakDatas = new List<MockPeptidePeakData<IDetailedPeakData>> { peptidePeakData, peptidePeakDataMs1 };
            // These scores are the same with or without an extra MS1 transition
            foreach (var peptideData in peptidePeakDatas)
            {
                MQuestScoreEquals(calcShape, 0.3969832, peptideData);
                MQuestScoreEquals(calcCoelution, 3.0, peptideData);
                MQuestScoreEquals(calcWeightedShape, 0.3969832, peptideData);
                MQuestScoreEquals(calcWeightedCoelution, 3.0, peptideData);
                MQuestScoreEquals(calcSignalNoise, 1.3086293, peptideData);
            }
            // The MS1-MS2 cross score differs when an MS1 transition is added
            MQuestScoreEquals(calcWeightedCrossShape, 0.94669, peptidePeakDataMs1);

            // TODO: Figure out OpenSWATH weights
            // var calcWeighted = new MQuestWeightedShapeCalc();
            // AreCloseEnough(calcWeighted.Calculate(new PeakScoringContext(), peptidePeakData), 0.6984916);
        }

        /// <summary>
        /// A test for MQuest reference-based detail scores
        /// </summary>
        [TestMethod]
        public void MQuestReferenceSummaryScoreTest()
        {
            var peakData1 = new MockTranPeakData<ISummaryPeakData>(INTENS1, IonType.a, IsotopeLabelType.light);
            var peakData2 = new MockTranPeakData<ISummaryPeakData>(INTENS2, IonType.a, IsotopeLabelType.heavy);
            var peakData3 = new MockTranPeakData<ISummaryPeakData>(INTENS3, IonType.a, IsotopeLabelType.light);
            var peakData4 = new MockTranPeakData<ISummaryPeakData>(INTENS3, IonType.a, IsotopeLabelType.heavy);
            var peakData5 = new MockTranPeakData<ISummaryPeakData>(INTENS3, IonType.precursor, IsotopeLabelType.light);
            var peakData6 = new MockTranPeakData<ISummaryPeakData>(INTENS3, IonType.precursor, IsotopeLabelType.heavy);
            var tranGroupData1 = new MockTranGroupPeakData<ISummaryPeakData>(new[] { peakData1, peakData3 });
            var tranGroupData2 = new MockTranGroupPeakData<ISummaryPeakData>(new[] { peakData2, peakData4 }, true);
            var tranGroupData3 = new MockTranGroupPeakData<ISummaryPeakData>(new[] { peakData1, peakData3, peakData5 });
            var tranGroupData4 = new MockTranGroupPeakData<ISummaryPeakData>(new[] { peakData2, peakData4, peakData6 }, true);
            var peptidePeakData = new MockPeptidePeakData<ISummaryPeakData>(new[] { tranGroupData1, tranGroupData2 });
            var peptidePeakDataMs1 = new MockPeptidePeakData<ISummaryPeakData>(new[] { tranGroupData3, tranGroupData4 });
            var peptidePeakDatas = new List<MockPeptidePeakData<ISummaryPeakData>> { peptidePeakData, peptidePeakDataMs1 };
            var calcReferenceCorr = new MQuestReferenceCorrelationCalc();
            // These scores are the same with or without an extra MS1 transition
            foreach (var peptideData in peptidePeakDatas)
            {
                MQuestScoreEquals(calcReferenceCorr, 0.570172, peptideData);
            }
            // TODO: Figure out OpenSWATH weights
            // var calcWeighted = new MQuestWeightedShapeCalc();
            // AreCloseEnough(calcWeighted.Calculate(new PeakScoringContext(), peptidePeakData), 0.6984916);
        }

        /// <summary>
        /// A test for MQuest reference-based detail scores
        /// </summary>
        [TestMethod]
        public void MQuestReferenceDetailScoreTest()
        {
            var peakData1 = new MockTranPeakData<IDetailedPeakData>(INTENS1, IonType.a, IsotopeLabelType.light);
            var peakData2 = new MockTranPeakData<IDetailedPeakData>(INTENS2, IonType.a, IsotopeLabelType.heavy);
            var peakData3 = new MockTranPeakData<IDetailedPeakData>(INTENS3, IonType.precursor, IsotopeLabelType.light);
            var peakData4 = new MockTranPeakData<IDetailedPeakData>(INTENS3, IonType.precursor, IsotopeLabelType.heavy);
            var tranGroupData1 = new MockTranGroupPeakData<IDetailedPeakData>(new[] { peakData1 });
            var tranGroupData2 = new MockTranGroupPeakData<IDetailedPeakData>(new[] { peakData2 }, true);
            var tranGroupData3 = new MockTranGroupPeakData<IDetailedPeakData>(new[] { peakData1, peakData3 });
            var tranGroupData4 = new MockTranGroupPeakData<IDetailedPeakData>(new[] { peakData2, peakData4 }, true);
            var peptidePeakData = new MockPeptidePeakData<IDetailedPeakData>(new[] { tranGroupData1, tranGroupData2 });
            var peptidePeakDataMs1 = new MockPeptidePeakData<IDetailedPeakData>(new[] { tranGroupData3, tranGroupData4 });
            var peptidePeakDatas = new List<MockPeptidePeakData<IDetailedPeakData>> { peptidePeakData, peptidePeakDataMs1 };
            var calcShape = new MQuestReferenceShapeCalc();
            var calcCoelution = new MQuestReferenceCoElutionCalc();
            var calcWeightedShape = new MQuestWeightedReferenceShapeCalc();
            var calcWeightedCoelution = new MQuestWeightedReferenceCoElutionCalc();
            // These scores are the same with or without an extra MS1 transition
            foreach (var peptideData in peptidePeakDatas)
            {
                MQuestScoreEquals(calcShape, 0.3969832, peptideData);
                MQuestScoreEquals(calcCoelution, 3.0, peptideData);
                MQuestScoreEquals(calcWeightedShape, 0.3969832, peptideData);
                MQuestScoreEquals(calcWeightedCoelution, 3.0, peptideData);
            }
            // TODO: Figure out OpenSWATH weights
            // var calcWeighted = new MQuestWeightedShapeCalc();
            // AreCloseEnough(calcWeighted.Calculate(new PeakScoringContext(), peptidePeakData), 0.6984916);
        }

        public void MQuestScoreEquals(IPeakFeatureCalculator calc, double score, IPeptidePeakData peptidePeakData)
        {
            AreCloseEnough(calc.Calculate(new PeakScoringContext(null), peptidePeakData), score);
        }

        private class MockPeptidePeakData<TPeak> : IPeptidePeakData<TPeak> where TPeak : class
        {
            public MockPeptidePeakData(IList<ITransitionPeakData<TPeak>> transitionPeakData)
            {
                TransitionGroupPeakData = new[] { new MockTranGroupPeakData<TPeak>(transitionPeakData), };
                AnalyteGroupPeakData = TransitionGroupPeakData;
                StandardGroupPeakData = new ITransitionGroupPeakData<TPeak>[0];
            }

            public MockPeptidePeakData(IList<ITransitionGroupPeakData<TPeak>> transitionGroupPeakData)
            {
                TransitionGroupPeakData = transitionGroupPeakData;
                AnalyteGroupPeakData = TransitionGroupPeakData.Where(t => !t.IsStandard).ToArray();
                StandardGroupPeakData = TransitionGroupPeakData.Where(t => t.IsStandard).ToArray();
            }

            public PeptideDocNode NodePep { get { return null; } }

            public ChromFileInfo FileInfo { get { return null; } }

            public IList<ITransitionGroupPeakData<TPeak>> TransitionGroupPeakData { get; private set; }

            public IList<ITransitionGroupPeakData<TPeak>> AnalyteGroupPeakData { get; private set; }

            public IList<ITransitionGroupPeakData<TPeak>> StandardGroupPeakData { get; private set; }

            public IList<ITransitionGroupPeakData<TPeak>> BestAvailableGroupPeakData
            {
                get { return StandardGroupPeakData.Count > 0 ? StandardGroupPeakData : AnalyteGroupPeakData; }
            }
        }

        private class MockTranGroupPeakData<TPeak> : ITransitionGroupPeakData<TPeak> where TPeak : class
        {
            public MockTranGroupPeakData(IList<ITransitionPeakData<TPeak>> transitionPeakData,
                                         bool isStandard = false,
                                         int? charge = null,
                                         IsotopeLabelType labelType = null)
            {
                TransitionPeakData = transitionPeakData;
                Ms1TranstionPeakData = TransitionPeakData.Where(t => t.NodeTran != null && t.NodeTran.IsMs1).ToArray();
                Ms2TranstionPeakData = Ms2TranstionDotpData =
                    TransitionPeakData.Where(t => t.NodeTran != null && !t.NodeTran.IsMs1).ToArray();

                IsStandard = isStandard;
                var libInfo = new ChromLibSpectrumHeaderInfo("", 0, null);
                var peptide = new Peptide(null, "AVVAVVA", null, null, 0);
                NodeGroup = new TransitionGroupDocNode(new TransitionGroup(peptide, Adduct.FromChargeProtonated(charge ?? 2), labelType), null, null,
                   null, libInfo, ExplicitTransitionGroupValues.EMPTY, null, new TransitionDocNode[0], true);
            }

            public TransitionGroupDocNode NodeGroup { get; private set; }
            public bool IsStandard { get; private set; }
            public IList<ITransitionPeakData<TPeak>> TransitionPeakData { get; private set; }
            public IList<ITransitionPeakData<TPeak>> Ms1TranstionPeakData { get; private set; }
            public IList<ITransitionPeakData<TPeak>> Ms2TranstionPeakData { get; private set; }
            public IList<ITransitionPeakData<TPeak>> Ms2TranstionDotpData { get; private set; }

            public IList<ITransitionPeakData<TPeak>> DefaultTranstionPeakData
            {
                get { return Ms2TranstionPeakData.Count > 0 ? Ms2TranstionPeakData : Ms1TranstionPeakData; }
            }
        }

        private class MockTranPeakData<TPeak> : ITransitionPeakData<TPeak> where TPeak : class
        {
            public MockTranPeakData(double[] data, 
                                    IonType ionType = IonType.a,
                                    IsotopeLabelType labelType = null,
                                    int? charge = null,
                                    double? massError = null,
                                    double libIntensity = 0,
                                    double? isotopeProportion = null)
            {
                if (labelType == null)
                    labelType = IsotopeLabelType.light;
                PeakData = new MockPeakData(data, massError) as TPeak;
                var peptide = new Peptide(null, "AVVAVVA", null, null, 0);
                charge = charge ?? 2;
                var tranGroup = new TransitionGroup(peptide, Adduct.FromChargeProtonated(charge), labelType);
                int offset = ionType == IonType.precursor ? 6 : 0;
                var isotopeInfo = isotopeProportion == null ? null : new TransitionIsotopeDistInfo(1, (float)isotopeProportion);
                NodeTran = new TransitionDocNode(new Transition(tranGroup, ionType, offset, 0, Adduct.FromChargeProtonated(charge), null),
                                                 null, TypedMass.ZERO_MONO_MASSH, new TransitionDocNode.TransitionQuantInfo(isotopeInfo, new TransitionLibInfo(1, (float)libIntensity), true), ExplicitTransitionValues.EMPTY);
            }

            public TransitionDocNode NodeTran { get; private set; }

            public TPeak PeakData { get; private set; }

            private class MockPeakData : IDetailedPeakData
            {
                private readonly float[] _data;

                public MockPeakData(double[] data, double? massError = null)
                {
                    MassError = (float?)massError;
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
                            Height = (float)intensity;
                            RetentionTime = StartTime + i;
                        }
                        if (lastIntensity.HasValue)
                        {
                            // Calculate trapezoidal area with time units equal to 1
                            area += Math.Min(intensity, lastIntensity.Value) +
                                    Math.Abs(intensity - lastIntensity.Value) / 2;
                        }
                        lastIntensity = intensity;
                        _data[i] = (float)data[i];
                    }
                    int last = data.Length - 1;
                    // Background is simple rectangular area with minimum edge
                    BackgroundArea = (float)Math.Min(data[0], data[last]) * (last);
                    Area = (float)(area - BackgroundArea);
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
                public int TimeIndex { get { return _data.Length / 2; } }
                public int EndIndex { get { return _data.Length - 1; } }
                public int StartIndex { get { return 0; } }
                public int Length { get { return _data.Length; } }
                public IList<float> Times { get { throw new InvalidOperationException(); } }
                public IList<float> Intensities { get { return _data; } }
                public float? MassError { get; private set; }
            }
        }
    }
}