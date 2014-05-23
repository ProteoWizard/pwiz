package uk.ac.ebi.jmzml.xml.jaxb.resolver;

import uk.ac.ebi.jmzml.MzMLElement;
import uk.ac.ebi.jmzml.model.mzml.CV;
import uk.ac.ebi.jmzml.model.mzml.DataProcessing;
import uk.ac.ebi.jmzml.model.mzml.UserParam;
import uk.ac.ebi.jmzml.xml.io.MzMLObjectCache;
import uk.ac.ebi.jmzml.xml.xxindex.MzMLIndexer;

/**
 * Created by IntelliJ IDEA.
 * User: dani
 * Date: 21-Feb-2011
 * Time: 14:09:56
 * To change this template use File | Settings | File Templates.
 */
public class UserParamRefResolver extends AbstractReferenceResolver<UserParam>{
    public UserParamRefResolver(MzMLIndexer index, MzMLObjectCache cache) {
             super(index, cache);
         }

         @Override
         public void updateObject(UserParam object) {
             // if we automatically resolve the references, then update the object with the referenced object
             if (MzMLElement.UserParam.isAutoRefResolving()) {
                 // add objects for the refID
                 String ref = object.getUnitCvRef();
                 if (ref != null) {
                     CV refObject = this.unmarshal(ref, CV.class);
                     object.setUnitCv(refObject);
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
         public void checkRefID(UserParam object) {
             // if there is a referenced object and its ID does not correspond to the refID, then there is something wrong
             if ( object.getUnitCv()!= null && !object.getUnitCvRef().equals(object.getUnitCv().getId()) ) {
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
             if (UserParam.class.isInstance(target)) {
                 updateObject((UserParam)target);
             } // else, not business of this resolver
         }

}
