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

import edu.washington.gs.skyline.model.NameValueCollection;

import java.util.Collections;
import java.util.Locale;

public abstract class NormalizationMethod {
    public static final NormalizationMethod NONE = new SingletonNormalizationMethod("none", "None");
    public static final NormalizationMethod EQUALIZE_MEDIANS = new SingletonNormalizationMethod("equalize_medians", "Equalize Medians");
    public static final NormalizationMethod QUANTILE = new SingletonNormalizationMethod("quantile", "Quantile");
    public static final NormalizationMethod GLOBAL_STANDARDS = new SingletonNormalizationMethod("global_standards", "Ratio to Global Standards");

    private static final String ratio_prefix = "ratio_to_";
    private static final String surrogate_prefix = "surrogate_";


    public static NormalizationMethod fromName(String name) {
        if (name == null || name.length() == 0) {
            return null;
        }
        if (name.startsWith(ratio_prefix)) {
            return new RatioToLabel(name.substring(ratio_prefix.length()));
        }
        RatioToSurrogate ratioToSurrogate = RatioToSurrogate.parseRatioToSurrogate(name);
        if (ratioToSurrogate != null)
        {
            return ratioToSurrogate;
        }

        for (NormalizationMethod normalizationMethod : new NormalizationMethod[]{EQUALIZE_MEDIANS, QUANTILE, GLOBAL_STANDARDS}) {
            if (name.equals(normalizationMethod.getName())) {
                return normalizationMethod;
            }
        }
        return NONE;
    }

    private final String name;

    private NormalizationMethod(String name) {
        this.name = name;
    }

    public String getName() {
        return name;
    }

    public abstract String getTitle();

    @Override
    public String toString() {
        return getTitle();
    }

    public boolean isAllowTruncatedTransitions() {
        return false;
    }

    public static class RatioToLabel extends NormalizationMethod {
        private final String isotopeLabelName;
        public RatioToLabel(String isotopeLabelName) {
            super(ratio_prefix + isotopeLabelName);
            this.isotopeLabelName = isotopeLabelName;
        }

        @Override
        public String getTitle() {
            return "Ratio to " + getIsotopeLabelTitle();
        }

        public String getIsotopeLabelTypeName() {
            return isotopeLabelName;
        }

        public String getIsotopeLabelTitle() {
            if (isotopeLabelName == null || isotopeLabelName.length() == 0) {
                return isotopeLabelName;
            }
            return isotopeLabelName.substring(0, 1).toUpperCase(Locale.US) + isotopeLabelName.substring(1);
        }

        @Override
        public boolean isAllowTruncatedTransitions() {
            return true;
        }
    }

    public static class RatioToSurrogate extends NormalizationMethod {
        private final String isotopeLabelName;
        private final String surrogateName;
        private static final String LABEL_ARG = "label";

        public RatioToSurrogate(String surrogateName, String isotopeLabelTypeName) {
            super(surrogate_prefix + NameValueCollection.encode(surrogateName) + '?'
                            + new NameValueCollection(Collections.singletonMap(LABEL_ARG, isotopeLabelTypeName).entrySet()));
            this.surrogateName = surrogateName;
            this.isotopeLabelName = isotopeLabelTypeName;
        }

        public RatioToSurrogate(String surrogateName) {
            super(surrogate_prefix + NameValueCollection.encode(surrogateName));
            this.surrogateName = surrogateName;
            isotopeLabelName = null;
        }

        @Override
        public String getTitle() {
            if (null != isotopeLabelName) {
                return "Ratio to surrogate " + surrogateName + " (" + isotopeLabelName + ")";
            }
            return "Ratio to surrogate " + surrogateName;
        }

        public String getSurrogateName() {
            return surrogateName;
        }

        public String getIsotopeLabelName() {
            return isotopeLabelName;
        }

        public static RatioToSurrogate parseRatioToSurrogate(String name) {
            if (!name.startsWith(surrogate_prefix)) {
                return null;
            }
            name = name.substring(surrogate_prefix.length());
            NameValueCollection arguments = NameValueCollection.EMPTY;
            String surrogateName;
            int ichQuestion = name.indexOf('?');
            if (ichQuestion >= 0) {
                surrogateName = NameValueCollection.decode(name.substring(0, ichQuestion));
                arguments = NameValueCollection.parseQueryString(name.substring(ichQuestion + 1));
            } else {
                surrogateName = NameValueCollection.decode(name);
            }

            String labelName = arguments.getFirstValue(LABEL_ARG);
            if (labelName == null) {
                return new RatioToSurrogate(surrogateName);
            }
            return new RatioToSurrogate(surrogateName, labelName);
        }
    }

    private static class SingletonNormalizationMethod extends NormalizationMethod {
        private final String label;
        public SingletonNormalizationMethod(String name, String label) {
            super(name);
            this.label = label;
        }

        @Override
        public String getTitle() {
            return label;
        }
    }
}
