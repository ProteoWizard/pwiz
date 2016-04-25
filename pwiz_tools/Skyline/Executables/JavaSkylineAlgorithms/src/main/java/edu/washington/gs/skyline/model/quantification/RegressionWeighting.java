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

import java.util.Arrays;
import java.util.Collections;
import java.util.List;

public abstract class RegressionWeighting {
    public static final RegressionWeighting NONE = new RegressionWeighting("none", "None") {
        @Override
        public double getWeighting(double x, double y) {
            return 1;
        }
    };

    public static final RegressionWeighting ONE_OVER_X = new RegressionWeighting("1/x", "1 / x") {
        @Override
        public double getWeighting(double x, double y) {
            return 1/x;
        }
    };

    public static final RegressionWeighting ONE_OVER_X_SQUARED = new RegressionWeighting("1/(x*x)", "1 / (x * x)") {
        @Override
        public double getWeighting(double x, double y) {
            return 1/(x*x);
        }
    };

    public static final List<RegressionWeighting> ALL
            = Collections.unmodifiableList(Arrays.asList(NONE, ONE_OVER_X, ONE_OVER_X_SQUARED));


    private String name;
    private String label;
    private RegressionWeighting(String name, String label) {
        this.name = name;
        this.label = label;
    }

    public String getName() {
        return name;
    }

    public String getLabel() {
        return label;
    }

    public abstract double getWeighting(double x, double y);

    public static RegressionWeighting parse(String name)
    {
        for (RegressionWeighting regressionWeighting : ALL) {
            if (regressionWeighting.getName().equals(name)) {
                return regressionWeighting;
            }
        }
        return NONE;
    }

}
