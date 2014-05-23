/*
 * Date: 22/7/2008
 * Author: rcote
 * File: uk.ac.ebi.jmzml.xml.jaxb.marshaller.listeners.ObjectClassListener
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

package uk.ac.ebi.jmzml.xml.jaxb.marshaller.listeners;

import org.apache.log4j.Logger;
import uk.ac.ebi.jmzml.model.mzml.ParamGroup;

import javax.xml.bind.Marshaller;

public class ObjectClassListener extends Marshaller.Listener {

    private static final Logger logger = Logger.getLogger(ObjectClassListener.class);

    public void beforeMarshal(Object source) {
        //this class will only be associated with a Marshaller when
        //the logging level is set to DEBUG 
        logger.debug("marshalling: " + source.getClass());
        if(source instanceof ParamGroup) {
            logger.debug("Calling ParamGroup specific 'beforeMarshalOperation'.");
            ((ParamGroup)source).beforeMarshalOperation();
        }
    }

    public void afterMarshal(Object source) {
        logger.debug("  marshalled: " + source.getClass());
        if(source instanceof ParamGroup) {
            logger.debug("Calling ParamGroup specific 'afterMarshalOperation'.");
            ((ParamGroup)source).afterMarshalOperation();
        }
    }
}