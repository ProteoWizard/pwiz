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

import java.util.HashMap;
import java.util.Map;
import java.util.stream.DoubleStream;

public class TransitionAreas {
    public static final TransitionAreas EMPTY = new TransitionAreas(TransitionKeys.EMPTY, new double[0]);

    private final TransitionKeys keys;
    private final double[] areas;

    public static TransitionAreas fromMap(Map<String, Double> map) {
        TransitionKeys keys = TransitionKeys.of(map.keySet());
        double[] areas = new double[keys.size()];
        for (int i = 0; i < keys.size(); i++) {
            areas[i] = map.get(keys.get(i));
        }
        return new TransitionAreas(keys, areas);
    }

    private TransitionAreas(TransitionKeys keys, double[] areas) {
        if (keys == null || areas == null) {
            throw new IllegalArgumentException();
        }
        if (keys.size() != areas.length) {
            throw new IllegalArgumentException();
        }
        this.keys = keys;
        this.areas = areas;
    }

    public TransitionKeys getKeys() {
        return keys;
    }

    public Double getArea(String key) {
        int index = keys.indexOf(key);
        if (index < 0) {
            return null;
        }
        return areas[index];
    }

    private HashMap<String, Double> toMap() {
        HashMap<String, Double> map = new HashMap<>();
        for (int i = 0; i < keys.size(); i++) {
            map.put(keys.get(i), areas[i]);
        }
        return map;
    }

    public boolean isEmpty() {
        return keys.size() == 0;
    }

    public TransitionAreas merge(TransitionAreas that) {
        if (isEmpty()) {
            return that;
        }
        if (that.isEmpty()) {
            return this;
        }
        HashMap<String, Double> map = that.toMap();
        map.putAll(toMap());
        return fromMap(map);
    }

    public Double ratioTo(TransitionAreas denominator) {
        double totalNumerator = 0;
        double totalDenominator = 0;
        int count = 0;
        for (int i = 0; i < keys.size(); i++) {
            Double denominatorValue = denominator.getArea(keys.get(i));
            if (denominatorValue == null) {
                continue;
            }
            totalDenominator += denominatorValue;
            totalNumerator += areas[i];
            count ++;
        }
        if (count == 0) {
            return null;
        }
        return totalNumerator / totalDenominator;
    }

    public TransitionAreas scale(double factor) {
        if (factor == 1.0) {
            return this;
        }
        return new TransitionAreas(keys, DoubleStream.of(areas).map(area->area * factor).toArray());
    }

    public double sum() {
        return DoubleStream.of(areas).sum();
    }

    public Double totalArea(TransitionKeys keys) {
        if (!getKeys().containsAll(keys)) {
            return null;
        }
        return filter(keys).sum();
    }

    public TransitionAreas filter(TransitionKeys keys) {
        TransitionKeys newKeys = getKeys().intersect(keys);
        double[] newAreas = new double[newKeys.size()];
        for (int i = 0; i < newKeys.size(); i++) {
            newAreas[i] = getArea(newKeys.get(i));
        }
        return new TransitionAreas(newKeys, newAreas);
    }

    public TransitionAreas setArea(String key, double value) {
        HashMap<String, Double> map = toMap();
        map.put(key, value);
        return fromMap(map);
    }
}
