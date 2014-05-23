package uk.ac.ebi.jmzml.xml.jaxb.resolver;

import uk.ac.ebi.jmzml.MzMLElement;
import uk.ac.ebi.jmzml.model.mzml.BinaryDataArray;
import uk.ac.ebi.jmzml.model.mzml.ChromatogramList;
import uk.ac.ebi.jmzml.model.mzml.DataProcessing;
import uk.ac.ebi.jmzml.xml.io.MzMLObjectCache;
import uk.ac.ebi.jmzml.xml.xxindex.MzMLIndexer;

/**
 * Created by IntelliJ IDEA.
 * User: dani
 * Date: 21-Feb-2011
 * Time: 10:47:41
 * To change this template use File | Settings | File Templates.
 */
public class ChromatogramListRefResolver extends AbstractReferenceResolver<ChromatogramList>{
    public ChromatogramListRefResolver(MzMLIndexer index, MzMLObjectCache cache) {
             super(index, cache);
         }

         @Override
         public void updateObject(ChromatogramList object) {
             // if we automatically resolve the references, then update the object with the referenced object
             if (MzMLElement.ChromatogramList.isAutoRefResolving()) {
                 // add objects for the refID
                 String ref = object.getDefaultDataProcessingRef();
                 if (ref != null) {
                     DataProcessing refObject = this.unmarshal(ref, DataProcessing.class);
                     object.setDefaultDataProcessing(refObject);
                 }
             }
         }

         /**
          * A method to be called before the marshall process.
          * Whenever a referenced object is set, its refID should be updated
          * automatically, so that the refID and the ID of the object are
          * always in sync. Here we check that this is the case.
          *
          * @param object The Object to check for reference ID integrity.
          */
         @Override
         public void checkRefID(ChromatogramList object) {
             // if there is a referenced object and its ID does not correspond to the refID, then there is something wrong
             if ( object.getDefaultDataProcessing()!= null && !object.getDefaultDataProcessingRef().equals(object.getDefaultDataProcessing().getId()) ) {
                 throw new IllegalStateException("Reference ID and referenced object ID do not match!");
             }
         }

         /**
          * Method to perform the afterUnmarshal operation if the resolver
          * applies to the specified object.
          *
          * @param target the object to modify after unmarshalling.
          * @param parent object referencing the target. Null if target is root element.
          */
         @Override
         public void afterUnmarshal(Object target, Object parent) {
             if (ChromatogramList.class.isInstance(target)) {
                 updateObject((ChromatogramList)target);
             } // else, not business of this resolver
         }

}
