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

import org.apache.commons.lang3.tuple.Pair;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.stream.DoubleStream;

public class PValues {
    public static double[] adjustPValues(double[] pValues) {
        List<Pair<Double, Integer>> entries = new ArrayList<>();
        DoubleStream.of(pValues).forEach(value->entries.add(Pair.of(value, entries.size())));
        Collections.sort(entries);
        double currentMin = 1.0;
        double[] result = new double[entries.size()];
        for (int i = entries.size() - 1; i >= 0; i--) {
            double value = entries.get(i).getLeft() * entries.size() / (i + 1);
            currentMin = Math.min(value, currentMin);
            result[entries.get(i).getRight()] = currentMin;
        }
        return result;
    }
}
