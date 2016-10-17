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

import org.apache.commons.math3.fitting.WeightedObservedPoint;
import org.apache.commons.math3.stat.descriptive.DescriptiveStatistics;
import org.apache.commons.math3.stat.descriptive.SummaryStatistics;

import java.util.Arrays;
import java.util.List;

public abstract class RegressionFit {
    public static final RegressionFit NONE = new RegressionFit("none", "None") {
        @Override
        protected CalibrationCurve performFit(List<WeightedObservedPoint> points) {
            CalibrationCurve curve = new CalibrationCurve();
            curve.setPointCount(0);
            curve.setSlope(1.0);
            return curve;
        }
    };

    public static final RegressionFit LINEAR = new RegressionFit("linear", "Linear") {
        @Override
        protected CalibrationCurve performFit(List<WeightedObservedPoint> points) {
            CalibrationCurve curve = new CalibrationCurve();
            curve.setPointCount(points.size());
            double[][] x = new double[points.size()][];
            double[] y = new double[points.size()];
            double[] weights = new double[points.size()];
            for (int i = 0; i < points.size(); i++) {
                x[i] = new double[]{points.get(i).getX()};
                y[i] = points.get(i).getY();
                weights[i] = points.get(i).getWeight();
            }
            double[] result = WeightedRegression.weighted(x, y, weights, true);
            curve.setIntercept(result[0]);
            curve.setSlope(result[1]);
            return curve;
        }
    };

    public static final RegressionFit LINEAR_THROUGH_ZERO = new RegressionFit("linear_through_zero", "Linear through zero") {
        @Override
        protected CalibrationCurve performFit(List<WeightedObservedPoint> points) {
            CalibrationCurve curve = new CalibrationCurve();
            curve.setPointCount(points.size());
            double[][] x = new double[points.size()][];
            double[] y = new double[points.size()];
            double[] weights = new double[points.size()];
            for (int i = 0; i < points.size(); i++) {
                x[i] = new double[]{points.get(i).getX()};
                y[i] = points.get(i).getY();
                weights[i] = points.get(i).getWeight();
            }
            double[] result = WeightedRegression.weighted(x, y, weights, true);
            curve.setSlope(result[0]);
            return curve;
        }
    };

    public static final RegressionFit QUADRATIC = new RegressionFit("quadratic", "Quadratic") {
        @Override
        protected CalibrationCurve performFit(List<WeightedObservedPoint> points) {
            CalibrationCurve curve = new CalibrationCurve();
            curve.setPointCount(points.size());
            double[][] x = new double[points.size()][];
            double[] y = new double[points.size()];
            double[] weights = new double[points.size()];
            for (int i = 0; i < points.size(); i++) {
                double xValue = points.get(i).getX();
                x[i] = new double[]{xValue, xValue*xValue};
                y[i] = points.get(i).getY();
                weights[i] = points.get(i).getWeight();
            }
            double[] result = WeightedRegression.weighted(x, y, weights, true);
            curve.setIntercept(result[0]);
            curve.setSlope(result[1]);
            curve.setQuadraticCoefficient(result[2]);
            return curve;
        }
    };




    private final String name;
    private final String label;

    public RegressionFit(String name, String label) {
        this.name = name;
        this.label = label;
    }

    public String getName() {
        return name;
    }

    public String getLabel() {
        return label;
    }

    public CalibrationCurve fit(List<WeightedObservedPoint> points) {
        try {
            CalibrationCurve curve = performFit(points);
            if (curve != null) {
                curve.setRSquared(computeRSquared(curve, points));
            }
            return curve;
        } catch (Exception e) {
            CalibrationCurve curve = new CalibrationCurve();
            curve.setErrorMessage(e.toString());
            return curve;
        }
    }

    protected abstract CalibrationCurve performFit(List<WeightedObservedPoint> points);

    public Double computeRSquared(CalibrationCurve curve, List<WeightedObservedPoint> points) {
        SummaryStatistics yValues = new SummaryStatistics();
        SummaryStatistics residuals = new SummaryStatistics();
        for (WeightedObservedPoint point : points) {
            Double yFitted = curve.getY(point.getX());
            if (yFitted == null) {
                continue;
            }
            yValues.addValue(point.getY());
            residuals.addValue(point.getY() - yFitted);
        }
        if (0 == residuals.getN()) {
            return null;
        }
        double yMean = yValues.getMean();
        double totalSumOfSquares = points.stream()
                .mapToDouble(p->(p.getY() - yMean) * (p.getY() - yMean))
                .sum();
        double sumOfSquaresOfResiduals = residuals.getSumsq();
        double rSquared = 1 - sumOfSquaresOfResiduals / totalSumOfSquares;
        return rSquared;
    }

    public static List<RegressionFit> listAll() {
        return Arrays.asList(NONE, LINEAR_THROUGH_ZERO, LINEAR, QUADRATIC);
    }

    public static RegressionFit parse(String name) {
        if (name == null) {
            return null;
        }
        return listAll().stream()
                .filter(regressionFit->regressionFit.getName().equals(name)).findFirst()
                .orElse(NONE);
    }
}
