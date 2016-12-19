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

import java.util.LinkedHashMap;
import java.util.Map;

public class ReplicateData
{
    private final LinkedHashMap<String, ResultFileData> resultFileDatas = new LinkedHashMap<>();
    public ReplicateData() {
    }

    public ResultFileData getResultFileData(String name) {
        return resultFileDatas.get(name);
    }

    public Iterable<Map.Entry<String, ResultFileData>> getResultFileDatas() {
        return resultFileDatas.entrySet();
    }

    public ResultFileData ensureResultFileData() {
        return ensureResultFileData("");
    }

    public ResultFileData ensureResultFileData(String name) {
        ResultFileData resultFileData = getResultFileData(name);
        if (resultFileData == null) {
            resultFileData = new ResultFileData();
            resultFileDatas.put(name, resultFileData);
        }
        return resultFileData;
    }

    public Double getNormalizedArea(NormalizationMethod normalizationMethod, String label, TransitionKeys transitionKeys) {
        TransitionAreas numerator = TransitionAreas.EMPTY;
        TransitionAreas denominator = null;
        String isotopeLabelName = null;
        if (normalizationMethod instanceof NormalizationMethod.RatioToLabel) {
            denominator = TransitionAreas.EMPTY;
            isotopeLabelName = ((NormalizationMethod.RatioToLabel) normalizationMethod).getIsotopeLabelTypeName();
        }
        for (ResultFileData resultFileData : resultFileDatas.values()) {
            numerator = numerator.merge(resultFileData.getTransitionAreas(label));
            if (denominator != null) {
                denominator = denominator.merge(resultFileData.getTransitionAreas(isotopeLabelName));
            }
        }
        if (transitionKeys != null) {
            numerator = numerator.filter(transitionKeys);
            if (denominator != null) {
                denominator = denominator.filter(transitionKeys);
            }
        }
        if (denominator == null) {
            if (numerator.isEmpty()) {
                return null;
            }
            return numerator.sum();
        }
        return numerator.ratioTo(denominator);
    }

    public TransitionAreas getTransitionAreas(String label) {
        TransitionAreas areas = TransitionAreas.EMPTY;
        for (ResultFileData resultFileData : resultFileDatas.values()) {
            areas = areas.merge(resultFileData.getTransitionAreas(label));
        }
        return areas;
    }

    @Override
    public String toString() {
        return "ReplicateData{" +
                "resultFileDatas=" + resultFileDatas +
                '}';
    }
}
