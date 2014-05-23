package uk.ac.ebi.jmzml.xml.jaxb.resolver;

import uk.ac.ebi.jmzml.MzMLElement;
import uk.ac.ebi.jmzml.model.mzml.DataProcessing;
import uk.ac.ebi.jmzml.model.mzml.SourceFile;
import uk.ac.ebi.jmzml.model.mzml.SourceFileRef;
import uk.ac.ebi.jmzml.xml.io.MzMLObjectCache;
import uk.ac.ebi.jmzml.xml.xxindex.MzMLIndexer;

/**
 * Created by IntelliJ IDEA.
 * User: dani
 * Date: 21-Feb-2011
 * Time: 13:59:51
 * To change this template use File | Settings | File Templates.
 */
public class SourceFileRefResolver extends AbstractReferenceResolver<SourceFileRef>{
    public SourceFileRefResolver(MzMLIndexer index, MzMLObjectCache cache) {
             super(index, cache);
         }

         @Override
         public void updateObject(SourceFileRef object) {
             // if we automatically resolve the references, then update the object with the referenced object
             if (MzMLElement.SourceFileRef.isAutoRefResolving()) {
                 // add objects for the refID
                 String ref = object.getRef();
                 if (ref != null) {
                     SourceFile refObject = this.unmarshal(ref, SourceFile.class);
                     object.setSourceFile(refObject);
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
         public void checkRefID(SourceFileRef object) {
             // if there is a referenced object and its ID does not correspond to the refID, then there is something wrong
             if ( object.getSourceFile()!= null && !object.getRef().equals(object.getSourceFile().getId()) ) {
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
             if (SourceFileRef.class.isInstance(target)) {
                 updateObject((SourceFileRef)target);
             } // else, not business of this resolver
         }

}
