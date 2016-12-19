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
import java.util.Map;
import java.util.stream.Collectors;
import java.util.stream.IntStream;

public class GroupComparisonDataSet {
    private NormalizationMethod normalizationMethod = NormalizationMethod.NONE;
    private List<Replicate> replicates = new ArrayList<>();

    public Replicate addReplicate(boolean control, Object bioReplicate) {
        Replicate replicate = new Replicate(control, bioReplicate);
        replicates.add(replicate);
        return replicate;
    }

    public NormalizationMethod getNormalizationMethod() {
        return normalizationMethod;
    }

    public void setNormalizationMethod(NormalizationMethod normalizationMethod) {
        if (normalizationMethod != null) {
            this.normalizationMethod = normalizationMethod;
        }
    }

    public LinearFitResult calculateFoldChange(String label) {
        List<Replicate> replicates = removeIncompleteReplicates(label, this.replicates);
        if (replicates.size() == 0) {
            return null;
        }
        List<Replicate> summarizedRows;
        if (replicates.stream().anyMatch(row -> null != row.getBioReplicate())) {
            Map<Pair<Boolean, Object>, List<Replicate>> groupedByBioReplicate =
                    replicates.stream().collect(Collectors.groupingBy(
                            replicate->Pair.of(replicate.isControl(), replicate.bioReplicate)));
            summarizedRows = new ArrayList<>();
            for (Map.Entry<Pair<Boolean, Object>, List<Replicate>> entry : groupedByBioReplicate.entrySet()) {
                Double log2Abundance = calculateMean(entry.getValue().stream()
                        .map(replicateData->replicateData.getLog2Abundance(label))
                        .collect(Collectors.toList()));
                if (log2Abundance == null) {
                    continue;
                }
                Replicate combinedReplicate = new Replicate(entry.getKey().getLeft(), entry.getKey().getValue());
                ResultFileData resultFileData = combinedReplicate.ensureResultFileData();
                resultFileData.setTransitionAreas(label,
                        TransitionAreas.fromMap(Collections.singletonMap("", Math.pow(2.0, log2Abundance))));
                if (getNormalizationMethod() instanceof NormalizationMethod.RatioToLabel) {
                    TransitionAreas denominator = TransitionAreas.fromMap(Collections.singletonMap("", 1.0));
                    resultFileData.setTransitionAreas(((NormalizationMethod.RatioToLabel) getNormalizationMethod()).getIsotopeLabelTypeName(), denominator);
                }
                summarizedRows.add(combinedReplicate);
            }
        } else {
            summarizedRows = replicates;
        }

        List<Double> abundances = summarizedRows.stream()
                .map(replicateData->replicateData.getLog2Abundance(label))
                .collect(Collectors.toList());
        List<Integer> features = Collections.nCopies(summarizedRows.size(), 0);
        List<Integer> runs = IntStream.range(0, summarizedRows.size()).boxed().collect(Collectors.toList());
        List<Integer> subjects = IntStream.range(0, summarizedRows.size()).boxed().collect(Collectors.toList());
        List<Boolean> subjectControls = summarizedRows.stream().map(Replicate::isControl).collect(Collectors.toList());
        FoldChangeDataSet foldChangeDataSet = new FoldChangeDataSet(abundances, features, runs, subjects, subjectControls);
        DesignMatrix designMatrix = DesignMatrix.getDesignMatrix(foldChangeDataSet, false);
        LinearFitResult linearFitResult = designMatrix.performLinearFit().get(0);
        return linearFitResult;
    }

    List<Replicate> removeIncompleteReplicates(String label, List<Replicate> replicates) {
        TransitionKeys requiredTransitions = null;
        if (!(getNormalizationMethod() instanceof NormalizationMethod.RatioToLabel)) {
            requiredTransitions = TransitionKeys.EMPTY;
            for (Replicate replicate : replicates) {
                TransitionAreas transitionAreas = replicate.getTransitionAreas(label);
                if (transitionAreas != null) {
                    requiredTransitions = requiredTransitions.union(transitionAreas.getKeys());
                }
            }
        }

        List<Replicate> completeReplicates = new ArrayList<>();
        for (Replicate replicateData : replicates) {
            TransitionAreas transitionAreas = replicateData.getTransitionAreas(label);
            if (transitionAreas == null || transitionAreas.getKeys().size() == 0) {
                continue;
            }
            if (requiredTransitions != null && !transitionAreas.getKeys().containsAll(requiredTransitions)) {
                continue;
            }
            if (null == replicateData.getNormalizedArea(getNormalizationMethod(), label, requiredTransitions)) {
                continue;
            }
            completeReplicates.add(replicateData);
        }
        return completeReplicates;
    }

    private Double calculateMean(Iterable<Double> values) {
        int count = 0;
        double sum = 0;
        for (Double value : values) {
            if (value == null) {
                continue;
            }
            sum += value;
            count++;
        }
        if (count == 0) {
            return null;
        }
        return sum / count;
    }

    public class Replicate extends ReplicateData {
        private boolean control;
        private Object bioReplicate;

        public Replicate(boolean control, Object bioReplicate) {
            this.control = control;
            this.bioReplicate = bioReplicate;
        }

        public boolean isControl() {
            return control;
        }

        public Object getBioReplicate() {
            return bioReplicate;
        }
        public Double getLog2Abundance(String label) {
            Double normalizedIntensity = getNormalizedArea(getNormalizationMethod(), label, null);
            if (null == normalizedIntensity) {
                return null;
            }
            return log2(normalizedIntensity);
        }

        @Override
        public String toString() {
            return "Replicate{" +
                    "control=" + control +
                    ", bioReplicate=" + bioReplicate +
                    ", super=" + super.toString() +
                    '}';
        }
    }

    protected double log2(double value) {
        return Math.log(value) / Math.log(2.0);
    }

    @Override
    public String toString() {
        return "GroupComparisonDataSet{" +
                "normalizationMethod=" + normalizationMethod +
                ", replicates=" + replicates +
                '}';
    }
}
