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

import org.apache.commons.math3.linear.MatrixUtils;
import org.apache.commons.math3.linear.RealMatrix;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.stream.Collectors;

class DesignMatrix {
    private final FoldChangeDataSet dataSet;
    private final double[][] matrixColumns;
    private final double[][] contrastValues;
    private final String[] columnNames;

    private DesignMatrix(FoldChangeDataSet foldChangeDataSet, double[][] matrixColumns, double[][] contrastValues, String[] columnNames) {
        this.dataSet = foldChangeDataSet;
        this.matrixColumns = matrixColumns;
        this.contrastValues = contrastValues;
        this.columnNames = columnNames;
    }

    public static DesignMatrix getDesignMatrix(FoldChangeDataSet dataSet, boolean includeFeatureInteraction) {
        List<String> columnNames = new ArrayList<>();
        List<double[]> matrixColumns = new ArrayList<>();
        List<Double> contrastValues = new ArrayList<>();
        double[] interceptColumn = new double[dataSet.getRowCount()];
        Arrays.fill(interceptColumn, 1);
        columnNames.add("Intercept");
        matrixColumns.add(interceptColumn);
        contrastValues.add(0.0);
        for (int iFeature = 1; iFeature < dataSet.getFeatureCount(); iFeature++) {
            columnNames.add("Feature" + iFeature);
            double[] featureColumn = new double[dataSet.getRowCount()];
            for (int iRow = 0; iRow < featureColumn.length; iRow++) {
                featureColumn[iRow] = iFeature == dataSet.getFeature(iRow) ? 1 : 0;
            }
            matrixColumns.add(featureColumn);
            contrastValues.add(0.0);
        }
        if (dataSet.getRowCount() != dataSet.getSubjectCount()) {
            int controlGroupCount = dataSet.getControlGroupCount();
            for (int iSubject = 1; iSubject < dataSet.getSubjectCount(); iSubject++) {
                columnNames.add("Subject" + iSubject);
                double[] subjectColumn = new double[dataSet.getRowCount()];
                for (int iRow = 0; iRow < subjectColumn.length; iRow++) {
                    subjectColumn[iRow] = iSubject == dataSet.getSubject(iRow) ? 1 : 0;
                }
                matrixColumns.add(subjectColumn);
                if (dataSet.isSubjectInControlGroup(iSubject)) {
                    contrastValues.add(-1.0/controlGroupCount);
                } else {
                    contrastValues.add(1.0 / (dataSet.getSubjectCount() - controlGroupCount));
                }
            }
        }
        columnNames.add("Control");
        double[] controlColumn = new double[dataSet.getRowCount()];
        for (int iRow = 0; iRow < controlColumn.length; iRow++) {
            controlColumn[iRow] = dataSet.isSubjectInControlGroup(iRow) ? 1.0 : 0.0;
        }
        matrixColumns.add(controlColumn);
        contrastValues.add(-1.0);
        if (includeFeatureInteraction) {
            for (int iFeature = 1; iFeature < dataSet.getFeatureCount(); iFeature++) {
                columnNames.add("Feature" + iFeature + "Interaction");
                double[] featureInteractionColumn = new double[dataSet.getRowCount()];
                for (int iRow = 0; iRow < featureInteractionColumn.length; iRow++) {
                    if (iFeature == dataSet.getFeature(iRow) && dataSet.isRowInControlGroup(iRow)) {
                        featureInteractionColumn[iRow] = 1.0;
                    } else {
                        featureInteractionColumn[iRow] = 0;
                    }
                }
                matrixColumns.add(featureInteractionColumn);
                contrastValues.add(-1.0/dataSet.getFeatureCount());
            }
        }
        double[][] contrastValueMatrix = new double[contrastValues.size()][];
        for (int i = 0; i < contrastValueMatrix.length; i++) {
            contrastValueMatrix[i] = new double[] {contrastValues.get(i)};
        }

        return new DesignMatrix(dataSet, matrixColumns.toArray(new double[matrixColumns.size()][]),
                contrastValueMatrix, columnNames.toArray(new String[columnNames.size()]));
    }

    private static RealMatrix matrixFromColumnVectors(double[][] columnVectors) {
        RealMatrix realMatrix = MatrixUtils.createRealMatrix(columnVectors[0].length, columnVectors.length);
        for (int iRow = 0; iRow < realMatrix.getRowDimension(); iRow++) {
            for (int iCol = 0; iCol < realMatrix.getColumnDimension(); iCol++) {
                realMatrix.setEntry(iRow, iCol, columnVectors[iCol][iRow]);
            }
        }
        return realMatrix;
    }

    public List<LinearFitResult> performLinearFit() {
        LinearModel linearModel = new LinearModel(matrixFromColumnVectors(this.matrixColumns),
                matrixFromColumnVectors(contrastValues));
        double[] abundances = new double[dataSet.getRowCount()];
        for (int i = 0; i < abundances.length; i++) {
            abundances[i] = dataSet.getAbundance(i);
        }
        return linearModel.fit(abundances);
    }

    @Override
    public String toString() {
        if (columnNames.length == 0) {
            return "";
        }
        List<String> lines = new ArrayList<>();
        lines.add(String.join("\t", columnNames));
        for (int row = 0; row < matrixColumns[0].length; row++) {
            final int rowNumber = row;
            lines.add(String.join("\t", Arrays.stream(matrixColumns)
                    .map(col->Double.toString(col[rowNumber]))
                    .collect(Collectors.toList())));
        }

        return String.join("\n", lines);
    }
}
