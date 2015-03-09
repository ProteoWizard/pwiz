/*
 * Date: 22/7/2008
 * Author: rcote
 * File: uk.ac.ebi.jmzml.xml.jaxb.marshaller.MarshallerFactory
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


package uk.ac.ebi.jmzml.xml.jaxb.marshaller;

import org.apache.log4j.Logger;
import uk.ac.ebi.jmzml.model.mzml.utilities.ModelConstants;
import uk.ac.ebi.jmzml.xml.Constants;
import uk.ac.ebi.jmzml.xml.jaxb.marshaller.listeners.ObjectClassListener;

import javax.xml.bind.JAXBContext;
import javax.xml.bind.JAXBException;
import javax.xml.bind.Marshaller;

public class MarshallerFactory {

    private static final Logger logger = Logger.getLogger(MarshallerFactory.class);
    private static MarshallerFactory instance = new MarshallerFactory();
    private static JAXBContext jc = null;

    public static MarshallerFactory getInstance() {
        return instance;
    }

    private MarshallerFactory() {
    }

    public Marshaller initializeMarshaller() {
        logger.debug("Initializing Marshaller for mzML.");
        try {
            // Lazy caching of JAXB context.
            if(jc == null) {
                jc = JAXBContext.newInstance(ModelConstants.PACKAGE);
            }
            //create marshaller and set basic properties
            Marshaller marshaller = jc.createMarshaller();
            marshaller.setProperty(Constants.JAXB_ENCODING_PROPERTY, "UTF-8");
            marshaller.setProperty(Constants.JAXB_FORMATTING_PROPERTY, true);

            // Register a listener that calls before/afterMarshalOperation on ParamAlternative/-List objects.
            // See: ParamAlternative.beforeMarshalOperation and ParamAlternativeList.beforeMarshalOperation
            marshaller.setListener(new ObjectClassListener());

            logger.info("Marshaller initialized");

            return marshaller;

        } catch (JAXBException e) {
            logger.error("MarshallerFactory.initializeMarshaller", e);
            throw new IllegalStateException("Can't initialize marshaller: " + e.getMessage());
        }
    }

}
