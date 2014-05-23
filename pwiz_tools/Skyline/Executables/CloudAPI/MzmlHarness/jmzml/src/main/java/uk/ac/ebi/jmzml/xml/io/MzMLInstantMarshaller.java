/*
 * Date: 13/12/2013
 * Author: sperkins
 * File: uk.ac.ebi.jmzml.xml.io.MzMLInstantMarshaller
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

package uk.ac.ebi.jmzml.xml.io;

import java.util.Map;
import java.util.Map.Entry;
import java.util.Set;

/**
 * InstantMarshaller class extends MzMLMarshaller (so it exists in its original
 * form) and adds methods for creating MzML start and end tags so that
 * MzML files can be written piecemeal instead of all at once, which can cause
 * problems with large MzML objects.
 * @author SPerkins
 */
public class MzMLInstantMarshaller extends MzMLMarshaller {
    /**
     * The MzML namespace, used in the MzML header.
     */
    private static final String MZML_NAMESPACE = "http://psi.hupo.org/ms/mzml";
    
    /**
     * The default MzML version, used if one is not passed in.
     */
    private static final String DEFAULT_VERSION = "1.1.0";
    
    /**
     * Creates an instance of MzMLInstantMarshaller.
     */
    public MzMLInstantMarshaller() {
        super();
    }
    
    /**
     * Creates the XML header and returns it.
     * @return XML header
     */
    public final String createXmlHeader() {
        return "<?xml version=\"1.0\" encoding=\"" + "UTF-8" + "\"?>";
    }
    
    /**
     * Creates the MzML start tag and returns it.
     * @param fileName MzML file name
     * @return MzML start tag.
     */
    public final String createMzMLStartTag(final String fileName) {
        return createMzMLStartTag(fileName, DEFAULT_VERSION);
    }
    
    /**
     * Creates the MzML start tag and returns it.
     * @param fileName MzML file name
     * @param version MzML version number
     * @return MzML start tag
     */
    public final String createMzMLStartTag(final String fileName, final String version) {
        return "<mzML xmlns=\""
                + MZML_NAMESPACE
                + "\" id=\""
                + fileName
                + "\" version=\""
                + version
                + "\">";
    }
    
    /**
     * Creates the MzML close tag and returns it.
     * @return MzML close tag
     */
    public final String createMzMLCloseTag() {
        return "</mzML>";
    }
    
    /**
     * Creates the Run start tag with attributes and returns it.
     * @param attributes Run attributes
     * @return Run start tag
     */
    public final String createRunStartTag(final Map<String, String> attributes) {
        return "<run " + getAttributesString(attributes) + ">";
    }
    
    /**
     * Creates the run close tag and returns it.
     * @return Run close tag
     */
    public final String createRunCloseTag() {
        return "</run>";
    }
    
    /**
     * Creates the SpectrumList start tag with attributes and returns it.
     * @param attributes SpectrumList attributes
     * @return SpectrumList start tag
     */
    public final String createSpecListStartTag(final Map<String, String> attributes) {
        return "<spectrumList " + getAttributesString(attributes) + ">";
    }
    
    /**
     * Creates the SpectrumList close tag and returns it.
     * @return SpectrumList close tag.
     */
    public final String createSpecListCloseTag() {
        return "</spectrumList>";
    }
    
    /**
     * Gets attribute pairs formatted as a string for insertion into an XML tag.
     * @param attributes XML attributes
     * @return Attributes string
     */
    private String getAttributesString(final Map<String, String> attributes) {
        StringBuilder buffer = new StringBuilder();
        Set<Entry<String, String>> entries = attributes.entrySet();
        for (Entry<String, String> entry : entries) {
            buffer.append(entry.getKey());
            buffer.append("=\"");
            buffer.append(entry.getValue());
            buffer.append("\" ");
        }
        
        return buffer.toString().trim();
    }    
}
