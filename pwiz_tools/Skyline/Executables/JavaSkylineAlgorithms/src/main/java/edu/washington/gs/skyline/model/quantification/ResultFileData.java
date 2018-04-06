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
import java.util.Set;

public class ResultFileData {
    private final Map<String, TransitionAreas> transitionAreasByLabel = new HashMap<>();
    public Set<String> getIsotopeLabelTypes() {
        return transitionAreasByLabel.keySet();
    }

    public TransitionAreas getTransitionAreas(String isotopeLabelType) {
        TransitionAreas transitionAreas = transitionAreasByLabel.get(isotopeLabelType);
        if (transitionAreas == null) {
            return TransitionAreas.EMPTY;
        }
        return transitionAreas;
    }

    public void setTransitionAreas(String isotopeLabelType, TransitionAreas transitionAreas) {
        transitionAreasByLabel.put(isotopeLabelType, transitionAreas);
    }

    @Override
    public String toString() {
        return "ResultFileData{" +
                "transitionAreasByLabel=" + transitionAreasByLabel +
                '}';
    }
}
