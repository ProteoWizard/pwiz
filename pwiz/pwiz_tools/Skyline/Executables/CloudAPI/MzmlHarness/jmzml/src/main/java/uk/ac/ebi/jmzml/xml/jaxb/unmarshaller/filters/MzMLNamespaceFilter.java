/*
 * Date: 22/7/2008
 * Author: rcote
 * File: uk.ac.ebi.jmzml.xml.jaxb.unmarshaller.filters.MzMLNamespaceFilter
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

package uk.ac.ebi.jmzml.xml.jaxb.unmarshaller.filters;

import org.apache.log4j.Logger;
import org.xml.sax.Attributes;
import org.xml.sax.SAXException;
import org.xml.sax.XMLReader;
import org.xml.sax.helpers.XMLFilterImpl;
import uk.ac.ebi.jmzml.model.mzml.utilities.ModelConstants;


public class MzMLNamespaceFilter extends XMLFilterImpl {

    /*
        note that if "elementFormDefault" is set to "qualified", there should be
        a namespace associated with the startElement (so the uri should not be
        empty). If this is not the case (e.g. we only have a XML snippet which
        then does not have the ns definitions of the root element), this filter
        will replace the empty uri with the namespace defined for the mzML schema.
     */

    private static final Logger logger = Logger.getLogger(MzMLNamespaceFilter.class);

    public MzMLNamespaceFilter() {
        logger.debug("MzMLNamespaceFilter created. Remember to call setParent(XMLReader)");
    }

    public MzMLNamespaceFilter(XMLReader reader) {
        super(reader);
    }

    @Override
    public void startElement(String uri, String localName, String qName, Attributes atts) throws SAXException {
        // the elements are defined by a qualified schema, but we rip them out of context with the xxindex
        // so the namespace information is lost and we have to add it again here manually
        logger.trace("Changing namespace. uri: " + uri + " \tlocalName: " + localName + " \tqName: " + qName + " \tatts: " + atts);
        if ( uri.length()== 0 ) super.startElement(ModelConstants.MZML_NS, localName, qName, atts);
        else super.startElement(uri, localName, qName, atts);
    }
}
