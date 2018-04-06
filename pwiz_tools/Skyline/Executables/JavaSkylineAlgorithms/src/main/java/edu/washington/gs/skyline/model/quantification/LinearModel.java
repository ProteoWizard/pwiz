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

import org.apache.commons.math3.distribution.TDistribution;
import org.apache.commons.math3.linear.ArrayRealVector;
import org.apache.commons.math3.linear.LUDecomposition;
import org.apache.commons.math3.linear.MatrixUtils;
import org.apache.commons.math3.linear.QRDecomposition;
import org.apache.commons.math3.linear.RealMatrix;
import org.apache.commons.math3.linear.RealVector;

import java.util.ArrayList;
import java.util.List;
import java.util.stream.IntStream;

class LinearModel {
    private RealMatrix designMatrix;
    private RealMatrix contrastValues;
    private QRDecomposition qrDecomposition;
    private RealMatrix matrixCrossproductInverse;
    private int[] independentColumnIndices;
    public LinearModel(RealMatrix designMatrix, RealMatrix contrastValues) {
        this.designMatrix = designMatrix;
        this.contrastValues = contrastValues;
        qrDecomposition = new QRDecomposition(designMatrix);
        independentColumnIndices = IntStream.range(0, designMatrix.getColumnDimension()).toArray();
        RealMatrix matrixCrossproduct = computeMatrixCrossproduct(
                designMatrix, independentColumnIndices);
        matrixCrossproductInverse = new LUDecomposition(matrixCrossproduct).getSolver().getInverse();
    }

    public List<LinearFitResult> fit(double[] observations) {
        if (observations.length != designMatrix.getRowDimension()) {
            throw new IllegalArgumentException("Wrong number of rows");
        }
        RealVector coefficients = qrDecomposition.getSolver().solve(new ArrayRealVector(observations));
        RealVector fittedValues = new ArrayRealVector(observations.length);
        RealVector residuals = new ArrayRealVector(observations.length);
        for (int iRow = 0; iRow < observations.length; iRow++) {
            RealVector designRow = designMatrix.getRowVector(iRow);
            fittedValues.setEntry(iRow, designRow.dotProduct(coefficients));
            residuals.setEntry(iRow, observations[iRow] - fittedValues.getEntry(iRow));
        }
        double rss = residuals.dotProduct(residuals);
        int degreesOfFreedom = observations.length - qrDecomposition.getR().getColumnDimension();
        double resVar = rss / degreesOfFreedom;
        double sigma = Math.sqrt(resVar);
        RealMatrix covarianceUnscaled = matrixCrossproductInverse;
        RealMatrix scaledCovariance = covarianceUnscaled.scalarMultiply(sigma * sigma);
        List<LinearFitResult> results = new ArrayList<>();
        for (int iContrastRow = 0; iContrastRow < contrastValues.getRowDimension(); iContrastRow++) {
            RealVector contrastRow = contrastValues.getRowVector(iContrastRow);
            double standardError = 0;
            for (int iRow = 0; iRow < independentColumnIndices.length; iRow++) {
                for (int iCol = 0; iCol < independentColumnIndices.length; iCol++) {
                    standardError = contrastRow.getEntry(independentColumnIndices[iRow]) * scaledCovariance.getEntry(iRow, iCol) * contrastRow.getEntry(independentColumnIndices[iCol]);
                }
            }
            standardError = Math.sqrt(standardError);
            double foldChange = coefficients.dotProduct(contrastRow);
            LinearFitResult linearFitResult = new LinearFitResult(foldChange);
            double tValue = foldChange / standardError;
            linearFitResult.setTValue(tValue);
            linearFitResult.setStandardError(standardError);
            linearFitResult.setDegreesOfFreedom(degreesOfFreedom);
            if (0 == degreesOfFreedom) {
                linearFitResult.setPValue(1.0);
            } else {
                TDistribution tDistribution = new TDistribution(degreesOfFreedom);
                double pValue = (1-tDistribution.cumulativeProbability(Math.abs(tValue))) * 2;
                linearFitResult.setPValue(pValue);
            }
            results.add(linearFitResult);
        }
        return results;
    }

    private static RealMatrix computeMatrixCrossproduct(RealMatrix matrix, int[] columnIndexes) {
        RealMatrix result = MatrixUtils.createRealMatrix(columnIndexes.length, columnIndexes.length);
        for (int iRow = 0; iRow < columnIndexes.length; iRow++) {
            for (int iCol = 0; iCol < columnIndexes.length; iCol++) {
                double dotProduct = matrix.getColumnVector(iRow).dotProduct(matrix.getColumnVector(iCol));
                result.setEntry(iRow, iCol, dotProduct);
            }
        }
        return result;
    }
}
