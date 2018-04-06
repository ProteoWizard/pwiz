package edu.washington.gs.skyline.model.quantification;

import junit.framework.TestCase;
import org.apache.commons.math3.fitting.WeightedObservedPoint;
import org.junit.Assert;

import java.util.ArrayList;
import java.util.List;

/**
 * Created by nicksh on 11/15/2016.
 */
public class RegressionFitTest extends TestCase {
    /**
     * Confirms that the calculated R-squared value is the same as was calculated in Excel.
     */
    public void testRSquared() {
        double[] xValues = {25, 25, 500, 500, 2000, 2000 };
        double[] yValues = {0.075571454, 0.082758175, 1.543044753, 1.562160311, 5.187147747, 5.462728205};
        List<WeightedObservedPoint> points = new ArrayList<>();
        for (int i = 0; i < xValues.length; i++) {
            points.add(new WeightedObservedPoint(1.0, xValues[i], yValues[i]));
        }
        CalibrationCurve curve = RegressionFit.LINEAR.fit(points);
        double rSquared = RegressionFit.LINEAR.computeRSquared(curve, points);
        Assert.assertEquals(0.99682572, rSquared, .00001);
    }
}