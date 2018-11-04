/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataAnalysis;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest.MSstats
{
    [TestClass]
    public class MedianPolishTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestMedianPolishSimpleMatrix()
        {
            var matrix = new double?[,]{{1,3},{2,5}};
            MedianPolish medianPolish = MedianPolish.GetMedianPolish(matrix, 0.01, 10);
            Assert.AreEqual(2.75, medianPolish.OverallConstant);
            CollectionAssert.AreEqual(new[]{-.75,.75}, medianPolish.RowEffects);
            CollectionAssert.AreEqual(new[]{-1.25, 1.25}, medianPolish.ColumnEffects);
        }

        [TestMethod]
        public void TestMedianPolishSimpleMissingValues()
        {
            var matrix = new double?[,] {{null, 1, 2}, {3, 4, 5}, {6, null, 7}};
            MedianPolish medianPolish = MedianPolish.GetMedianPolish(matrix, 0, 100);
            Assert.AreEqual(4, medianPolish.OverallConstant);
            AssertDoublesEqual(new[]{ -3.000000 , 0.000000 , 2.333333}, medianPolish.RowEffects, .0001);
            AssertDoublesEqual(new[]{-6.666667e-01, -6.223015e-61, 1.000000e+00}, medianPolish.ColumnEffects, .0001);
        }

        [TestMethod]
        public void TestMedianPolishExamples()
        {
            var matrix = new double?[,]
            {
                {
                    1677120562, 1057244128, 1573838418, 1610727572, 1700424094, 1337004885, 2096688822, 556694100,
                    1758444441
                },
                {822328116, 698575767, 557625726, 1867460568, 1497452757, 1286780009, 2109397777, 63595003, 579931872},
                {2021080567, 1939792651, 998333669, 309807657, 766650568, 1525104204, 98432661, 1273392816, 423985426},
                {310985865, 1892567676, 834287094, 1778432993, 99139496, 2020064656, 1859647279, 1208352316, 673041733}
            };
            var expectedResult = new MedianPolish()
                {
                    OverallConstant = 1250365665,
                    RowEffects = new double[]
                        { 367804634, -369087286, -130763091, 130763091},
                    ColumnEffects = new double[] {0, 164368154, -222460779, 194930755, -135349106, 405501630, 478518523, -495229908, -498481827},
                    Residuals = new double?[,]
                    {
                        {58950263, -725294325, 178128898, -202373482, 217602900, -686667045, 0, -566246291, 638755969},
                        { -58950263, -347070766, -101191874,   791251435,   751523484,          0,   749600876, -322453468,  197135321},
                        { 901477993,  655821923,  101191874, -1004725672,  -217602900,          0, -1499688436,  649020150, -197135321},
                        {-1070142891,  347070766, -324380884,   202373482, -1146640155,  233434270,           0,  322453468, -209605196}
                    }
                };
            MedianPolish medianPolish = MedianPolish.GetMedianPolish(matrix, 0.01, 10);
            Assert.AreEqual(expectedResult.OverallConstant, medianPolish.OverallConstant);
            AssertDoublesEqual(expectedResult.RowEffects, medianPolish.RowEffects);
            AssertDoublesEqual(expectedResult.ColumnEffects, medianPolish.ColumnEffects);
            AssertMatricesEqual(expectedResult.Residuals, medianPolish.Residuals);
        }

        [TestMethod]
        public void TestMedianPolishMissingValues()
        {
            /* matrix(c(NA, 1057244128, 1573838418, 1610727572, 1700424094, 1337004885, 2096688822, 556694100,1758444441, 
                     822328116, NA, 557625726, 1867460568, 1497452757, 1286780009, 2109397777,63595003, 579931872, 
                     2021080567, 1939792651, 998333669, 309807657, 766650568, 1525104204, 98432661, 1273392816, 423985426, 
                     310985865, 1892567676, 834287094, 1778432993, 99139496, 2020064656, 1859647279, 1208352316, 673041733),nrow = 4, byrow = TRUE) */

            var matrix = new double?[,]
            {
                {null, 1057244128, 1573838418, 1610727572, 1700424094, 1337004885, 2096688822, 556694100, 1758444441},
                {822328116, null, 557625726, 1867460568, 1497452757, 1286780009, 2109397777, 63595003, 579931872},
                {2021080567, 1939792651, 998333669, 309807657, 766650568, 1525104204, 98432661, 1273392816, 423985426},
                {310985865, 1892567676, 834287094, 1778432993, 99139496, 2020064656, 1859647279, 1208352316, 673041733}
            };
            var expectedResult =
                new MedianPolish()
                {
                    OverallConstant = 1162563201,
                    RowEffects = new double[] {119681337, -129549460, 16173465, -16173465},
                    ColumnEffects =
                        new double[]
                        {
                            -210685625, 746177940, -246252819, 480263146, 3046729, 300066903, 763850914, -331793929,
                            -463214936
                        },
                    Residuals = new double?[,]
                    {
                        {
                            null, -971178350, 537846699, -151780112, 415132827, -245306556, 50593371, -393756509, 939414839
                        },
                        {0, null, -229135195, 354183682, 461392287, -46300635, 312533123, -637624809, 10133067},
                        {
                            1053029526, 14878045, 65849823, -1349192154, -415132827, 46300635, -1844154918, 426450079,
                            -291536304
                        },
                        {-624718246, 0, -65849823, 151780112, -1050296969, 573608017, -50593371, 393756509, -10133067}
                    }
                };
            MedianPolish medianPolish = MedianPolish.GetMedianPolish(matrix, 0.01, 10);
            Assert.AreEqual(expectedResult.OverallConstant, medianPolish.OverallConstant, 1);
            AssertDoublesEqual(expectedResult.RowEffects, medianPolish.RowEffects);
            AssertDoublesEqual(expectedResult.ColumnEffects, medianPolish.ColumnEffects);
            AssertMatricesEqual(expectedResult.Residuals, medianPolish.Residuals);
        }

        private void AssertDoublesEqual(IList<double> expected, IList<double> actual, double epsilon = 1.0)
        {
            Assert.AreEqual(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i], actual[i], epsilon);
            }
        }

        private void AssertMatricesEqual(double?[,] expected, double?[,] actual)
        {
            Assert.AreEqual(expected.GetLength(0), actual.GetLength(0));
            Assert.AreEqual(expected.GetLength(1), actual.GetLength(1));
            for (int iRow = 0; iRow < expected.GetLength(0); iRow++)
            {
                for (int iCol = 0; iCol < expected.GetLength(1); iCol++)
                {
                    var expectedValue = expected[iRow, iCol];
                    var actualValue = actual[iRow, iCol];
                    Assert.AreEqual(expectedValue.HasValue, actualValue.HasValue);
                    Assert.AreEqual(expectedValue.GetValueOrDefault(), actualValue.GetValueOrDefault(), 1.0);
                }
            }
        }
    }
}
