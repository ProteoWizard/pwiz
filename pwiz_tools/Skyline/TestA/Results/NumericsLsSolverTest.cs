/*
 * Original author: Jarrett Egertson <jegertso .at .u.washington.edu>,
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

using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA.Results
{
    [TestClass]
    public class NumericsLsSolverTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestNumericsLsSolver()
        {
            TestWeightedMatrixPrepare();
            TestNonNegSolver();
        }

        /// <summary>
        /// Test the WeightedConditioner class which is responsible for "conditioning"
        /// the A and b matrices in the equation Ax=b when solving for x.  b contains
        /// transition intensity values to be deconvolved, A encodes the precursor isolation
        /// window scheme used.  The conditioner weights the entries in the center rows of each
        /// matrix higher than those on the edges.
        /// </summary>
        private static void TestWeightedMatrixPrepare()
        {
            // If the matrices have less than 5 rows, the conditioner should have
            // no effect.
            DeconvBlock dbSmall = new DeconvBlock(5,5,4);
            double[] mask = {1.0,1.0,1.0,1.0, 0.0};
            double[] data = {2.0,2.0,2.0,2.0};
            for (int i = 0; i<4; ++i)
            {
                dbSmall.Add(mask, data);
            }
            var conditioner = new WeightedConditioner();
            conditioner.Condition(dbSmall);
            var conditionedMasks = dbSmall.Masks;
            var conditionedData = dbSmall.BinnedData;
            for (int i = 0; i<4; ++i)
            {
                var conditionedMask = conditionedMasks.Matrix.Row(i, 0, 5).ToArray();
                var condData = conditionedData.Matrix.Row(i, 0, 4).ToArray();
                AssertEx.AreEqualDeep(mask, conditionedMask);
                AssertEx.AreEqualDeep(data, condData);
            }

            // If the matrices have more than 5 rows, check that the weighting
            // is applied correctly.
            DeconvBlock dbLarge = new DeconvBlock(5, 11, 4);
            for (int i = 0; i<11; ++i)
            {
                dbLarge.Add(mask, data);
            }
            conditioner.Condition(dbLarge);
            conditionedMasks = dbLarge.Masks;
            conditionedData = dbLarge.BinnedData;
            var expectedMask = new[] {-0.086, -0.086, -0.086, -0.086, 0.0};
            var expectedData = new[] {2*-0.086, 2*-0.086, 2*-0.086, 2*-0.086};
            for (int i = 0; i<2; ++i)
            {
                var conditionedMask = conditionedMasks.Matrix.Row(i, 0, 5).ToArray();
                var condData = conditionedData.Matrix.Row(i, 0, 4).ToArray();
                AssertEx.AreEqualDeep(expectedMask, conditionedMask);
                AssertEx.AreEqualDeep(expectedData, condData);
            }
            expectedMask = new[] {0.343, 0.343, 0.343, 0.343, 0.0};
            expectedData = new[] {2*0.343, 2*0.343, 2*0.343, 2*0.343};
            for (int i = 2; i<4; ++i)
            {
                var conditionedMask = conditionedMasks.Matrix.Row(i, 0, 5).ToArray();
                var condData = conditionedData.Matrix.Row(i, 0, 4).ToArray();
                AssertEx.AreEqualDeep(expectedMask, conditionedMask);
                AssertEx.AreEqualDeep(expectedData, condData);
            }

            expectedMask = new[] {0.486, 0.486, 0.486, 0.486, 0.0};
            expectedData = new[] {2*0.486, 2*0.486, 2*0.486, 2*0.486};
            for (int i = 4; i<6; ++i)
            {
                var conditionedMask = conditionedMasks.Matrix.Row(i, 0, 5).ToArray();
                var condData = conditionedData.Matrix.Row(i, 0, 4).ToArray();
                AssertEx.AreEqualDeep(expectedMask, conditionedMask);
                AssertEx.AreEqualDeep(expectedData, condData);
            }

            expectedMask = new[] {0.343, 0.343, 0.343, 0.343, 0.0};
            expectedData = new[] {2*0.343, 2*0.343, 2*0.343, 2*0.343};
            for (int i = 6; i<8; ++i)
            {
                var conditionedMask = conditionedMasks.Matrix.Row(i, 0, 5).ToArray();
                var condData = conditionedData.Matrix.Row(i, 0, 4).ToArray();
                AssertEx.AreEqualDeep(expectedMask, conditionedMask);
                AssertEx.AreEqualDeep(expectedData, condData);
            }

            expectedMask = new[] {-0.086, -0.086, -0.086, -0.086, 0.0};
            expectedData = new[] {2*-0.086, 2*-0.086, 2*-0.086, 2*-0.086};
            for (int i = 8; i<11; ++i)
            {
                var conditionedMask = conditionedMasks.Matrix.Row(i, 0, 5).ToArray();
                var condData = conditionedData.Matrix.Row(i, 0, 4).ToArray();
                AssertEx.AreEqualDeep(expectedMask, conditionedMask);
                AssertEx.AreEqualDeep(expectedData, condData);
            }
        }

        /// <summary>
        /// Checks the non-negative least squares solver by comparing the output of a simple 
        /// non-negative least squares optimization solving for x in the equation Ax=b.  The expected
        /// values were generated using the lsqnonneg function in Matlab.
        /// </summary>
        private static void TestNonNegSolver()
        {
            var matrixA = DenseMatrix.OfArray(new[,]
                                              {
                                                  {0.0372, 0.2869}, {0.6861, 0.7071}, {0.6233, 0.6245},
                                                  {0.6344, 0.6170}, {0, -200000}
                                              });
            var colB = new DenseVector(new[]{0.8587,0.1781,0.0747,0.8405,0.0});
            var initialize = new DenseVector(new[] {0.0, 0.0});
            var matrixAt = matrixA.Transpose();
            var vectorAx = matrixA.Multiply(initialize);
            var vectorW = matrixAt*(colB - vectorAx);
            var solver = new NonNegLsSolver(2, 5, 1);
            solver.SetTolerance(2.2205E-9);
            solver.SetMaxIter(6);
            Vector<double> solution = new DenseVector(5);
            Assert.IsTrue(solver.SolveColumnTest(matrixA, colB, initialize, matrixAt, vectorAx, vectorW, ref solution));
            Assert.AreEqual(0.0, solution[0], 0.000001);
            Assert.AreEqual(2.344E-11, solution[1], 0.0001);
        }
    }
}
