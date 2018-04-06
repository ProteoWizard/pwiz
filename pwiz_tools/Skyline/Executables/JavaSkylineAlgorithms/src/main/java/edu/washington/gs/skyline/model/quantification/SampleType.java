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

import java.awt.*;

public enum SampleType {
    unknown(Color.black),
    standard(Color.gray),
    qc(Color.green),
    solvent(new Color(255,226,43)),
    blank(Color.blue),
    double_blank(new Color(255, 230, 216));
    private Color color;
    private SampleType(Color color) {
        this.color = color;
    }
    public Color getColor() {
        return color;
    }
    public static SampleType fromName(String name) {
        if (name == null || 0 == name.length()) {
            return unknown;
        }
        try {
            return SampleType.valueOf(name);
        } catch (IllegalArgumentException iae) {
            return unknown;
        }
    }
}
