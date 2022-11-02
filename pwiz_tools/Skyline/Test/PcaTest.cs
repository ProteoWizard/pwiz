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
using pwiz.SkylineTestUtil;
// ReSharper disable RedundantExplicitArraySize

namespace pwiz.SkylineTest
{
    [TestClass]
    public class PcaTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestPca()
        {
            var X = new double[4, 3]
            {
                {1, 2, 4},
                {1, 2, 5},
                {1, 2, 6},
                {1, 2, 7}
            };
            var averageVector = Enumerable.Range(0, 3)
                .Select(c => Enumerable.Range(0, X.GetLength(0)).Average(i => X[i, c])).ToList();
            var normalizedVector = averageVector.Select(y => y / Math.Sqrt(averageVector.Sum(x => x * x))).ToList();
            alglib.pcabuildbasis(X, 4, 3, out int info, out double[] s2, out double[,] v);
            Assert.AreEqual(1, info);
            Assert.AreEqual(3, v.GetLength(0));
            Assert.AreEqual(3, v.GetLength(1));
            for (int iVector = 0; iVector < X.GetLength(1); iVector++)
            {
                var resultVector = new double[3];
                for (int i = 0; i < 3; i++)
                {
                    var dotProduct = 0.0;
                    for (int c = 0; c < 3; c++)
                    {
                        dotProduct += X[iVector, c] * v[c, i];
                    }

                    for (int c = 0; c < 3; c++)
                    {
                        resultVector[c] += v[c, i] * dotProduct;
                    }
                }

                var differenceVector = Enumerable.Range(0, 3).Select(c => X[iVector, c] - resultVector[c]).ToList();
                Assert.AreEqual(3, differenceVector.Count);
            }
            alglib.pcatruncatedsubspace(X, 4, 3, 1, .0001, 0, out double[] s2_trunc_1, out double[,] v_trunc_1);
            Assert.AreEqual(1, s2_trunc_1.Length);
            Assert.AreEqual(3, v_trunc_1.GetLength(0));
            Assert.AreEqual(1, v_trunc_1.GetLength(1));

            alglib.pcatruncatedsubspace(X, 4, 3, 2, .0001, 0, out double[] s2_trunc_2, out double[,] v_trunc_2);
            Assert.AreEqual(2, s2_trunc_2.Length);
            Assert.AreEqual(3, v_trunc_2.GetLength(0));
            Assert.AreEqual(2, v_trunc_2.GetLength(1));

            double dot_product_2 = 0;
            for (int i = 0; i < 3; i++)
            {
                dot_product_2 += v_trunc_2[i, 0] * v_trunc_2[i, 1];
            }
            Assert.AreEqual(0, dot_product_2, 1e-15);
            var decomposedVectors = new List<double[]>();
            for (int iVector = 0; iVector < X.GetLength(0); iVector++)
            {
                var decomposedVector = new double[2];
                for (int iComponent = 0; iComponent < 2; iComponent++)
                {
                    var dotProduct = 0.0;
                    for (int iCoordinate = 0; iCoordinate < 3; iCoordinate++)
                    {
                        dotProduct += X[iVector, iCoordinate] * v_trunc_2[iCoordinate,iComponent];
                    }

                    decomposedVector[iComponent] = dotProduct;
                }
                decomposedVectors.Add(decomposedVector);
            }
            Assert.AreEqual(4, decomposedVectors.Count);
        }
    }
}
