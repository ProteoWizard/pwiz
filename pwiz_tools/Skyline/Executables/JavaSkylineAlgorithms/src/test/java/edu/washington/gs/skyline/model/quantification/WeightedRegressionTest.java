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

import junit.framework.TestCase;
import org.apache.commons.math3.stat.regression.SimpleRegression;

import java.util.Date;
import java.util.Random;

public class WeightedRegressionTest extends TestCase {
    public void testWeighted() {
        Random random = new Random((int) new Date().getTime());
        SimpleRegression simpleRegressionWithIntercept = new SimpleRegression(true);
        SimpleRegression simpleRegressionWithoutIntercept = new SimpleRegression(false);
        final int nPoints = 10;
        double[][] xValues = new double[nPoints][];
        double[] yValues = new double[nPoints];
        double[] weights = new double[nPoints];
        for (int i = 0; i < nPoints; i++)
        {
            int weight = random.nextInt(10) + 1;
            weights[i] = weight;
            double x = random.nextDouble();
            double y = random.nextDouble();
            xValues[i] = new double[]{x};
            yValues[i] = y;
            for (int w = 0; w < weight; w++) {
                simpleRegressionWithIntercept.addData(x, y);
                simpleRegressionWithoutIntercept.addData(x, y);
            }
        }
        final double epsilon = 1E-12;
        double repeatedIntercept = simpleRegressionWithIntercept.getIntercept();
        double repeatedSlope = simpleRegressionWithIntercept.getSlope();

        double[] weightedRegression = WeightedRegression.weighted(xValues, yValues, weights, true);
        assertEquals(repeatedIntercept, weightedRegression[0], epsilon);
        assertEquals(repeatedSlope, weightedRegression[1], epsilon);
        double[] weightedRegressionWithoutIntercept = WeightedRegression.weighted(xValues, yValues, weights, false);

        double repeatedSlopeWithoutIntercept = simpleRegressionWithoutIntercept.getSlope();
        assertEquals(repeatedSlopeWithoutIntercept, weightedRegressionWithoutIntercept[0], epsilon);

    }
}
