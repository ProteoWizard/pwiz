/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
// This code was taken from:
// http://code.google.com/p/nelder-mead-simplex/
// And is licensed under the MIT license:
// http://www.opensource.org/licenses/mit-license.php
using System;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace pwiz.Common.DataAnalysis
{
    /// <summary>
    /// Algorithm for narrowing in on the set of parameter values that optimize 
    /// (i.e. yields the lowest value for) some function.
    /// This method is also called "downhill simplex method".  It is a heuristic
    /// search that works well if there is only one local minimum.
    /// </summary>
    public sealed class NelderMeadSimplex
    {
        private const double JITTER = 1e-10d; // a small value used to protect against floating point noise

        public static RegressionResult Regress(SimplexConstant[] simplexConstants, double convergenceTolerance, int maxEvaluations, 
                                        ObjectiveFunctionDelegate objectiveFunction)
        {
            // confirm that we are in a position to commence
            if (objectiveFunction == null)
                throw new InvalidOperationException("ObjectiveFunction must be set to a valid ObjectiveFunctionDelegate"); // Not L10N

            if (simplexConstants == null)
                throw new InvalidOperationException("SimplexConstants must be initialized"); // Not L10N

            // create the initial simplex
            int numDimensions = simplexConstants.Length;
            int numVertices = numDimensions + 1;
            Vector<double>[] vertices = InitializeVertices(simplexConstants);

            int evaluationCount = 0;
            TerminationReason terminationReason;
            ErrorProfile errorProfile;

            double[] errorValues = InitializeErrorValues(vertices, objectiveFunction);

            // iterate until we converge, or complete our permitted number of iterations
            while (true)
            {
               errorProfile = EvaluateSimplex(errorValues);

                // see if the range in point heights is small enough to exit
                if (HasConverged(convergenceTolerance, errorProfile, errorValues))
                {
                    terminationReason = TerminationReason.Converged;
                    break;
                }

                // attempt a reflection of the simplex
                double reflectionPointValue = TryToScaleSimplex(-1.0, ref errorProfile, vertices, errorValues, objectiveFunction);
                ++evaluationCount;
                if (reflectionPointValue <= errorValues[errorProfile.LowestIndex])
                {
                    // it's better than the best point, so attempt an expansion of the simplex
                    TryToScaleSimplex(2.0, ref errorProfile, vertices, errorValues, objectiveFunction);
                    ++evaluationCount;
                }
                else if (reflectionPointValue >= errorValues[errorProfile.NextHighestIndex])
                {
                    // it would be worse than the second best point, so attempt a contraction to look
                    // for an intermediate point
                    double currentWorst = errorValues[errorProfile.HighestIndex];
                    double contractionPointValue = TryToScaleSimplex(0.5, ref errorProfile, vertices, errorValues, objectiveFunction);
                    ++evaluationCount;
                    if (contractionPointValue >= currentWorst)
                    {
                        // that would be even worse, so let's try to contract uniformly towards the low point; 
                        // don't bother to update the error profile, we'll do it at the start of the
                        // next iteration
                        ShrinkSimplex(errorProfile, vertices, errorValues, objectiveFunction);
                        evaluationCount += numVertices; // that required one function evaluation for each vertex; keep track
                    }
                }
                // check to see if we have exceeded our alloted number of evaluations
                if (evaluationCount >= maxEvaluations)
                {
                    terminationReason = TerminationReason.MaxFunctionEvaluations;
                    break;
                }
            }
            RegressionResult regressionResult = new RegressionResult(terminationReason,
                                vertices[errorProfile.LowestIndex].ToArray(), errorValues[errorProfile.LowestIndex], evaluationCount);
            return regressionResult;
        }

        /// <summary>
        /// Evaluate the objective function at each vertex to create a corresponding
        /// list of error values for each vertex
        /// </summary>
        private static double[] InitializeErrorValues(Vector<double>[] vertices, ObjectiveFunctionDelegate objectiveFunction)
        {
            double[] errorValues = new double[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                errorValues[i] = objectiveFunction(vertices[i].ToArray());
            }
            return errorValues;
        }

        /// <summary>
        /// Check whether the points in the error profile have so little range that we
        /// consider ourselves to have converged
        /// </summary>
        private static bool HasConverged(double convergenceTolerance, ErrorProfile errorProfile, double[] errorValues)
        {
            double range = 2 * Math.Abs(errorValues[errorProfile.HighestIndex] - errorValues[errorProfile.LowestIndex]) /
                (Math.Abs(errorValues[errorProfile.HighestIndex]) + Math.Abs(errorValues[errorProfile.LowestIndex]) + JITTER);

            if (range < convergenceTolerance)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Examine all error values to determine the ErrorProfile
        /// </summary>
        private static ErrorProfile EvaluateSimplex(double[] errorValues)
        {
            ErrorProfile errorProfile = new ErrorProfile();
            if (errorValues[0] > errorValues[1])
            {
                errorProfile.HighestIndex = 0;
                errorProfile.NextHighestIndex = 1;
            }
            else
            {
                errorProfile.HighestIndex = 1;
                errorProfile.NextHighestIndex = 0;
            }

            for (int index = 0; index < errorValues.Length; index++)
            {
                double errorValue = errorValues[index];
                if (errorValue <= errorValues[errorProfile.LowestIndex])
                {
                    errorProfile.LowestIndex = index;
                }
                if (errorValue > errorValues[errorProfile.HighestIndex])
                {
                    errorProfile.NextHighestIndex = errorProfile.HighestIndex; // downgrade the current highest to next highest
                    errorProfile.HighestIndex = index;
                }
                else if (errorValue > errorValues[errorProfile.NextHighestIndex] && index != errorProfile.HighestIndex)
                {
                    errorProfile.NextHighestIndex = index;
                }
            }

            return errorProfile;
        }

        /// <summary>
        /// Construct an initial simplex, given starting guesses for the constants, and
        /// initial step sizes for each dimension
        /// </summary>
        private static Vector<double>[] InitializeVertices(SimplexConstant[] simplexConstants)
        {
            int numDimensions = simplexConstants.Length;
            Vector<double>[] vertices = new Vector<double>[numDimensions + 1];

            // define one point of the simplex as the given initial guesses
            Vector<double> p0 = new DenseVector(numDimensions);
            for (int i = 0; i < numDimensions; i++)
            {
                p0[i] = simplexConstants[i].Value;
            }

            // now fill in the vertices, creating the additional points as:
            // P(i) = P(0) + Scale(i) * UnitVector(i)
            vertices[0] = p0;
            for (int i = 0; i < numDimensions; i++)
            {
                double scale = simplexConstants[i].InitialPerturbation;
                Vector unitVector = new DenseVector(numDimensions);
                unitVector[i] = 1;
                vertices[i + 1] = p0.Add(unitVector.Multiply(scale)); 
            }
            return vertices;
        }

        /// <summary>
        /// Test a scaling operation of the high point, and replace it if it is an improvement
        /// </summary>
        private static double TryToScaleSimplex(double scaleFactor, ref ErrorProfile errorProfile, Vector<double>[] vertices, 
                                          double[] errorValues, ObjectiveFunctionDelegate objectiveFunction)
        {
            // find the centroid through which we will reflect
            Vector centroid = ComputeCentroid(vertices, errorProfile);

            // define the vector from the centroid to the high point
            Vector centroidToHighPoint = (Vector) vertices[errorProfile.HighestIndex].Subtract(centroid);

            // scale and position the vector to determine the new trial point
            Vector newPoint = (Vector) centroidToHighPoint.Multiply(scaleFactor).Add(centroid);

            // evaluate the new point
            double newErrorValue = objectiveFunction(newPoint.ToArray());

            // if it's better, replace the old high point
            if (newErrorValue < errorValues[errorProfile.HighestIndex])
            {
                vertices[errorProfile.HighestIndex] = newPoint;
                errorValues[errorProfile.HighestIndex] = newErrorValue;
            }

            return newErrorValue;
        }

        /// <summary>
        /// Contract the simplex uniformly around the lowest point
        /// </summary>
        private static void ShrinkSimplex(ErrorProfile errorProfile, Vector<double>[] vertices, double[] errorValues, 
                                      ObjectiveFunctionDelegate objectiveFunction)
        {
            Vector<double> lowestVertex = vertices[errorProfile.LowestIndex];
            for (int i = 0; i < vertices.Length; i++)
            {
                if (i != errorProfile.LowestIndex)
                {
                    vertices[i] = (vertices[i].Add(lowestVertex)).Multiply(0.5);
                    errorValues[i] = objectiveFunction(vertices[i].ToArray());
                }
            }
        }

        /// <summary>
        /// Compute the centroid of all points except the worst
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="errorProfile"></param>
        /// <returns></returns>
        private static Vector ComputeCentroid(Vector<double>[] vertices, ErrorProfile errorProfile)
        {
            int numVertices = vertices.Length;
            // find the centroid of all points except the worst one
            Vector centroid = new DenseVector(numVertices - 1);
            for (int i = 0; i < numVertices; i++)
            {
                if (i != errorProfile.HighestIndex)
                {
                    centroid = (Vector) centroid.Add(vertices[i]);
                }
            }
            return (Vector) centroid.Multiply(1.0d / (numVertices - 1));
        }

        private sealed class ErrorProfile
        {
            public int HighestIndex { get; set; }

            public int NextHighestIndex { get; set; }

            public int LowestIndex { get; set; }
        }
        public delegate double ObjectiveFunctionDelegate(double[] constants);

        // ReSharper disable InconsistentNaming
        public enum TerminationReason
        {
            MaxFunctionEvaluations,
            Converged,
            Unspecified
        }
        // ReSharper restore InconsistentNaming
        public sealed class RegressionResult
        {
            private readonly TerminationReason _terminationReason;
            private readonly double[] _constants;
            private readonly double _errorValue;
            private readonly int _evaluationCount;

            public RegressionResult(TerminationReason terminationReason, double[] constants, double errorValue, int evaluationCount)
            {
                _terminationReason = terminationReason;
                _constants = constants;
                _errorValue = errorValue;
                _evaluationCount = evaluationCount;
            }

            public TerminationReason TerminationReason
            {
                get { return _terminationReason; }
            }

            public double[] Constants
            {
                get { return _constants; }
            }

            public double ErrorValue
            {
                get { return _errorValue; }
            }

            public int EvaluationCount
            {
                get { return _evaluationCount; }
            }
        }
        public sealed class SimplexConstant
        {
            public SimplexConstant(double value, double initialPerturbation)
            {
                Value = value;
                InitialPerturbation = initialPerturbation;
            }

            /// <summary>
            /// The value of the constant
            /// </summary>
            public double Value { get; set; }

            // The size of the initial perturbation
            public double InitialPerturbation { get; set; }
        }
    }
}
