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

public class CalibrationCurve {
    private Double slope;
    private Double intercept;
    private Integer pointCount;
    private Double quadraticCoefficient;
    private Double rSquared;
    private String errorMessage;

    public double getSlope() {
        return slope == null ? 0 : slope;
    }

    public boolean hasSlope() {
        return slope != null;
    }

    public void setSlope(Double slope) {
        this.slope = slope;
    }

    public double getIntercept() {
        return intercept == null ? 0 : intercept;
    }

    public boolean hasIntercept() {
        return intercept != null;
    }

    public void setIntercept(Double intercept) {
        this.intercept = intercept;
    }

    public int getPointCount() {
        return pointCount == null ? 0 : pointCount;
    }

    public boolean hasPointCount() {
        return pointCount != null;
    }

    public void setPointCount(Integer pointCount) {
        this.pointCount = pointCount;
    }

    public double getQuadraticCoefficient() {
        return quadraticCoefficient == null ? 0 : quadraticCoefficient;
    }

    public boolean hasQuadraticCoefficient() {
        return quadraticCoefficient != null;
    }

    public void setQuadraticCoefficient(Double quadraticCoefficient) {
        this.quadraticCoefficient = quadraticCoefficient;
    }

    public Double getRSquared() {
        return rSquared;
    }

    public void setRSquared(Double rSquared) {
        this.rSquared = rSquared;
    }

    public String getErrorMessage() {
        return errorMessage;
    }

    public void setErrorMessage(String errorMessage) {
        this.errorMessage = errorMessage;
    }

    public Double getY(double x)
    {
        if (hasQuadraticCoefficient())
        {
            return x*x*getQuadraticCoefficient() + x*getSlope() + getIntercept();
        }
        return x*getSlope() + getIntercept();
    }

    public Double getX(double y)
    {
        if (hasQuadraticCoefficient())
        {
            double discriminant = getSlope()*getSlope()- 4*getQuadraticCoefficient()*(getIntercept() - y);
            if (discriminant < 0)
            {
                return Double.NaN;
            }
            double sqrtDiscriminant = Math.sqrt(discriminant);
            return (-getSlope() + sqrtDiscriminant)/2/getQuadraticCoefficient();
        }
        return (y - getIntercept())/getSlope();
    }

    public static CalibrationCurve forNoExternalStandards() {
        CalibrationCurve calibrationCurve = new CalibrationCurve();
        calibrationCurve.setPointCount(0);
        calibrationCurve.setSlope(1.0);
        return calibrationCurve;
    }
}
