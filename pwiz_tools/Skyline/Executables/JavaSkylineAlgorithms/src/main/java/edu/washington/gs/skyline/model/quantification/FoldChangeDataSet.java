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

import org.apache.commons.lang3.ArrayUtils;
import org.apache.commons.lang3.StringUtils;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collection;
import java.util.List;
import java.util.stream.IntStream;

/**
 * Set of data used to calculate fold changes.
 * This class holds the data necessary to calculate the fold change between two groups, having stripped away all
 * extra information about replicates, etc. other than whether they are cases or controls.
 */
class FoldChangeDataSet {
    private final double[] abundances;
    private final int[] features;
    private final int[] runs;
    private final int[] subjects;
    private final boolean[] subjectControls;
    private final int featureCount;
    private final int subjectCount;
    private final int runCount;

    /**
     * Construct a new FoldChangeDataSet.  The first four collections must all have the same number of elements.
     * The "subjectControls" must have the same number of elements as the number of unique values of "subjects".
     * @param abundances log2 intensity
     * @param features identifiers of the transition that the abundance was measured for.  Note that Skyline always sums
     *                 the transition intensities before coming in here, the feature values should all be zero.
     * @param runs integers representing which replicate the value came from
     * @param subjects identifiers used for combining biological replicates.
     * @param subjectControls specifies which subject values belong to the control group.
     */
    public FoldChangeDataSet(Collection<Double> abundances,
                             Collection<Integer> features,
                             Collection<Integer> runs,
                             Collection<Integer> subjects,
                             Collection<Boolean> subjectControls) {
        if (abundances.size() != features.size() || abundances.size() != subjects.size() || abundances.size() != runs.size()) {
            throw new IllegalArgumentException("Wrong number of rows");
        }
        this.abundances = abundances.stream().mapToDouble(Double::doubleValue).toArray();
        this.features = features.stream().mapToInt(Integer::intValue).toArray();
        this.runs = runs.stream().mapToInt(Integer::intValue).toArray();
        this.subjects = subjects.stream().mapToInt(Integer::intValue).toArray();
        this.subjectControls = ArrayUtils.toPrimitive(subjectControls.toArray(new Boolean[subjectControls.size()]));
        if (this.abundances.length == 0) {
            featureCount = 0;
            subjectCount = 0;
            runCount = 0;
        } else {
            if (Arrays.stream(this.features).min().getAsInt() < 0 ||
                    Arrays.stream(this.runs).min().getAsInt() < 0 ||
                    Arrays.stream(this.subjects).min().getAsInt() < 0) {
                throw new IllegalArgumentException("Cannot be negative");
            }
            featureCount = Arrays.stream(this.features).max().getAsInt() + 1;
            subjectCount = Arrays.stream(this.subjects).max().getAsInt() + 1;
            runCount = Arrays.stream(this.runs).max().getAsInt() + 1;
        }
        if (this.subjectControls.length != subjectCount) {
            throw new IllegalArgumentException("Wrong number of subjects");
        }
    }

    public double getAbundance(int iRow) {
        return abundances[iRow];
    }

    public int getFeature(int iRow) {
        return features[iRow];
    }

    public int getRun(int iRow) {
        return runs[iRow];
    }

    public int getSubject(int iRow) {
        return subjects[iRow];
    }

    public int getRowCount() {
        return abundances.length;
    }

    public int getFeatureCount() {
        return featureCount;
    }

    public int getSubjectCount() {
        return subjectCount;
    }

    public int getControlGroupCount() {
        return (int) IntStream.range(0, subjectControls.length).filter(this::isSubjectInControlGroup).count();
    }

    public int getRunCount() {
        return runCount;
    }

    public boolean isSubjectInControlGroup(int subjectId) {
        return subjectControls[subjectId];
    }

    public boolean isRowInControlGroup(int rowIndex) {
        return isSubjectInControlGroup(subjects[rowIndex]);
    }

    public String toString() {
        List<String> lines = new ArrayList<>();
        lines.add("Subject,Run,Feature,Abundance,Control");
        for (int i = 0; i < getRowCount(); i++) {
            lines.add(StringUtils.join(new Object[]{getSubject(i), getRun(i), getFeature(i), getAbundance(i),isRowInControlGroup(i)}, ','));
        }
        return String.join("\n", lines);
    }
}
