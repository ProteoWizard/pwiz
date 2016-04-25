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

public class NormalizationMethod {
    public static final NormalizationMethod NONE = new NormalizationMethod("none", "None");
    public static final NormalizationMethod EQUALIZE_MEDIANS = new NormalizationMethod("equalize_medians", "Equalize Medians");
    public static final NormalizationMethod QUANTILE = new NormalizationMethod("quantile", "Quantile");
    public static final NormalizationMethod GLOBAL_STANDARDS = new NormalizationMethod("global_standards", "Ratio to Global Standards");

    private static final String ratio_prefix = "ratio_to_";

    public static NormalizationMethod fromName(String name) {
        if (name == null || name.length() == 0) {
            return NONE;
        }
        if (name.startsWith(ratio_prefix)) {
            return forIsotopeLabelType(name.substring(ratio_prefix.length()));
        }
        for (NormalizationMethod normalizationMethod : new NormalizationMethod[]{EQUALIZE_MEDIANS, QUANTILE, GLOBAL_STANDARDS}) {
            if (name.equals(normalizationMethod.getName())) {
                return normalizationMethod;
            }
        }
        return NONE;
    }

    public static NormalizationMethod forIsotopeLabelType(String isotopeLabelTypeName) {
        String title = isotopeLabelTypeName;
        if (isotopeLabelTypeName.length() > 0 && !Character.isUpperCase(isotopeLabelTypeName.charAt(0))) {
            title = Character.toUpperCase(isotopeLabelTypeName.charAt(0)) + isotopeLabelTypeName.substring(1);
        }
        NormalizationMethod normalizationMethod = new NormalizationMethod(ratio_prefix + isotopeLabelTypeName, "Ratio to " + title);
        normalizationMethod.isotopeLabelTypeName = isotopeLabelTypeName;
        return normalizationMethod;
    }

    private String name;
    private String title;
    private String isotopeLabelTypeName;

    private NormalizationMethod(String name, String title) {
        this.name = name;
        this.title = title;
    }

    public String getName() {
        return name;
    }

    public String getTitle() {
        return title;
    }

    public String getIsotopeLabelTypeName() {
        return isotopeLabelTypeName;
    }

    public boolean isAllowTruncated() {
        return null != getIsotopeLabelTypeName();
    }

    public boolean isAllowMissingTransitions() {
        return null != getIsotopeLabelTypeName();
    }
}
