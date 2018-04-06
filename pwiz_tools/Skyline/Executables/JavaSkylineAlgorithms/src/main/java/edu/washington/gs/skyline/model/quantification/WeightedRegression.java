/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
package edu.washington.gs.skyline.model.quantification;

import org.apache.commons.math3.linear.CholeskyDecomposition;
import org.apache.commons.math3.linear.MatrixUtils;
import org.apache.commons.math3.linear.RealMatrix;
import org.apache.commons.math3.linear.RealVector;

class WeightedRegression {
    public static double[] weighted(double[][] x, double[]y, double[] weights, boolean intercept) {
        RealMatrix predictor;
        if (intercept) {
            int nRows = x.length;
            int nCols = x[0].length + 1;
            predictor = MatrixUtils.createRealMatrix(nRows, nCols);
            for (int iRow = 0; iRow < nRows; iRow++) {
                predictor.setEntry(iRow, 0, 1.0);
                for (int iCol = 1; iCol < nCols; iCol++) {
                    predictor.setEntry(iRow, iCol, x[iRow][iCol - 1]);
                }
            }
        } else {
            predictor = MatrixUtils.createRealMatrix(x);
        }
        RealVector responseVector = MatrixUtils.createRealVector(y);
        RealMatrix weightsMatrix = MatrixUtils.createRealDiagonalMatrix(weights);
        RealMatrix predictorTransposed = predictor.transpose();
        RealMatrix predictorTransposedTimesWeights = predictorTransposed.multiply(weightsMatrix.multiply(predictor));
        CholeskyDecomposition choleskyDecomposition = new CholeskyDecomposition(predictorTransposedTimesWeights);
        RealVector vectorToSolve = predictorTransposed.operate(weightsMatrix.operate(responseVector));
        RealVector result = choleskyDecomposition.getSolver().solve(vectorToSolve);
        return result.toArray();
    }
}
