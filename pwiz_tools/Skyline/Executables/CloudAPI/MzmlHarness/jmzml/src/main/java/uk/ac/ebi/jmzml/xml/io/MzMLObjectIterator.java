/*
 * Date: 22/7/2008
 * Author: rcote
 * File: uk.ac.ebi.jmzml.xml.io.MzMLObjectIterator
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

import org.apache.log4j.Logger;
import org.xml.sax.InputSource;
import uk.ac.ebi.jmzml.model.mzml.MzMLObject;
import uk.ac.ebi.jmzml.xml.jaxb.unmarshaller.UnmarshallerFactory;
import uk.ac.ebi.jmzml.xml.jaxb.unmarshaller.filters.MzMLNamespaceFilter;
import uk.ac.ebi.jmzml.xml.util.EscapingXMLUtilities;
import uk.ac.ebi.jmzml.xml.xxindex.MzMLIndexer;

import javax.xml.bind.JAXBElement;
import javax.xml.bind.JAXBException;
import javax.xml.bind.Unmarshaller;
import javax.xml.transform.sax.SAXSource;
import java.io.StringReader;
import java.util.Iterator;

public class MzMLObjectIterator<X extends MzMLObject> implements Iterator<X> {

    private static Logger logger = Logger.getLogger(MzMLObjectIterator.class);

    private MzMLIndexer index;

    private Iterator<String> innerXpathIterator;
    private String xpath;
    private Class cls;
    private MzMLObjectCache cache;
    private boolean useSpectrumCache = true;

    //package level constructor!
    MzMLObjectIterator(String xpath, Class cls, MzMLIndexer index, MzMLObjectCache cache) {
        this(xpath, cls, index, cache, true);
    }

    MzMLObjectIterator(String xpath, Class cls, MzMLIndexer index, MzMLObjectCache cache, boolean aUseSpectrumCache) {
        innerXpathIterator = index.getXmlStringIterator(xpath);
        this.xpath = xpath;
        this.cls = cls;
        this.index = index;
        this.cache = cache;
        this.useSpectrumCache = aUseSpectrumCache;
    }


    public boolean hasNext() {
        return innerXpathIterator.hasNext();
    }

    public X next() {

        try {
            String xmlSt = innerXpathIterator.next();

            //need to clean up XML to ensure that there are no weird control characters
            String cleanXML = EscapingXMLUtilities.escapeCharacters(xmlSt);

            if (logger.isDebugEnabled()) {
                logger.trace("XML to unmarshal: " + cleanXML);
            }

            //required for the addition of namespaces to top-level objects
            MzMLNamespaceFilter xmlFilter = new MzMLNamespaceFilter();
            //initializeUnmarshaller will assign the proper reader to the xmlFilter
            Unmarshaller unmarshaller = UnmarshallerFactory.getInstance().initializeUnmarshaller(index, xmlFilter, cache, useSpectrumCache);
            //unmarshall the desired object
            JAXBElement<X> holder = unmarshaller.unmarshal(new SAXSource(xmlFilter, new InputSource(new StringReader(cleanXML))), cls);

            X retval = holder.getValue();

            if (logger.isDebugEnabled()) {
                logger.debug("unmarshalled object = " + retval);
            }

            return retval;
        } catch (JAXBException e) {
            logger.error("MzMLObjectIterator.next", e);
            throw new IllegalStateException("Could not unmarshal object at xpath:" + xpath);
        }

    }

    public void remove() {
        throw new UnsupportedOperationException(MzMLObjectIterator.class.getName() + " can't be used to remove objects while iterating");
    }

}
