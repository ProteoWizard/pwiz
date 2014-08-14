/*
 * Date: 22/7/2008
 * Author: rcote
 * File: uk.ac.ebi.jmzml.xml.Constants
 *
 * jmzml is Copyright 2008 The European Bioinformatics Institute
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 *
 *
 */

package uk.ac.ebi.jmzml.xml;

import uk.ac.ebi.jmzml.MzMLElement;

import java.util.Collections;
import java.util.HashSet;
import java.util.Set;

public class Constants {
    // ToDo: ? move to ModelConstants ?

    public static final String JAXB_ENCODING_PROPERTY = "jaxb.encoding";
    public static final String JAXB_FORMATTING_PROPERTY = "jaxb.formatted.output";
    public static final String JAXB_SCHEMALOCATION_PROPERTY = "jaxb.schemaLocation";
    public static final String JAXB_FRAGMENT_PROPERTY = "jaxb.fragment";

    // ToDo: check if all necessary types are present
    public static enum ReferencedType {
        CV,
        DataProcessing,
        InstrumentConfiguration,
        ReferenceableParamGroup,
        Sample,
        ScanSettings,
        Software,
        SourceFile,
        Spectrum,
        Chromatogram
    }

    private static Set<String> xpathsToIndex = new HashSet<String>();

    static {
        for (MzMLElement element : MzMLElement.values()) {
            if (element.isIndexed()) {
                xpathsToIndex.add(element.getXpath());
                //need to include indexedmzML elements as well
                if (!element.getXpath().startsWith("/indexedmzML")) {

                    xpathsToIndex.add("/indexedmzML" + element.getXpath());
                }
            }
        }

        // add some additional xpath that are not mapped to a class and therefore not represented in the MzMLElement enumeration
        xpathsToIndex.add("/indexedmzML/fileChecksum");
        //this one is to be able to unmarshal th Params from the Run element
        xpathsToIndex.add("/mzML/run/ReferenceableParamGroupRef");
        xpathsToIndex.add("/mzML/run/cvParam");
        xpathsToIndex.add("/mzML/run/userParam");

        // finally make the set unmodifiable
        xpathsToIndex = Collections.unmodifiableSet(xpathsToIndex);
    }


    public static final Set<String> XML_INDEXED_XPATHS = xpathsToIndex;

}
