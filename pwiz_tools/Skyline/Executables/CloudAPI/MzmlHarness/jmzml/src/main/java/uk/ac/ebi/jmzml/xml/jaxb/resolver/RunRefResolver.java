package uk.ac.ebi.jmzml.xml.jaxb.resolver;

import uk.ac.ebi.jmzml.MzMLElement;
import uk.ac.ebi.jmzml.model.mzml.InstrumentConfiguration;
import uk.ac.ebi.jmzml.model.mzml.Run;
import uk.ac.ebi.jmzml.model.mzml.Sample;
import uk.ac.ebi.jmzml.model.mzml.SourceFile;
import uk.ac.ebi.jmzml.xml.io.MzMLObjectCache;
import uk.ac.ebi.jmzml.xml.xxindex.MzMLIndexer;

/**
 * Created by IntelliJ IDEA.
 * User: dani
 * Date: 21-Feb-2011
 * Time: 11:56:39
 * To change this template use File | Settings | File Templates.
 */
public class RunRefResolver extends AbstractReferenceResolver<Run> {
    public RunRefResolver(MzMLIndexer index, MzMLObjectCache cache) {
        super(index, cache);
    }

    @Override
    public void updateObject(Run object) {
        // if we automatically resolve the references, then update the object with the referenced object
        if (MzMLElement.Run.isAutoRefResolving()) {
            // add objects for the refID
            String ref = object.getDefaultInstrumentConfigurationRef();
            if (ref != null) {
                InstrumentConfiguration refObject = this.unmarshal(ref, InstrumentConfiguration.class);
                object.setDefaultInstrumentConfiguration(refObject);
            }
            String refSource = object.getDefaultSourceFileRef();
            if (refSource != null){
                SourceFile refObject = this.unmarshal(refSource, SourceFile.class);
                object.setDefaultSourceFile(refObject);
            }
            String refSample = object.getSampleRef();
            if (refSample != null){
                Sample refObject = this.unmarshal(refSample, Sample.class);
                object.setSample(refObject);
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
    public void checkRefID(Run object) {
        // if there is a referenced object and its ID does not correspond to the refID, then there is something wrong
        if (object.getDefaultInstrumentConfiguration() != null && !object.getDefaultInstrumentConfigurationRef().equals(object.getDefaultInstrumentConfiguration().getId())) {
            throw new IllegalStateException("Reference ID and referenced object ID do not match!");
        }
        if (object.getDefaultSourceFile() != null && !object.getDefaultSourceFileRef().equals(object.getDefaultSourceFile().getId())) {
            throw new IllegalStateException("Reference ID and referenced object ID do not match!");
        }
        if (object.getSample() != null && !object.getSampleRef().equals(object.getSample().getId())) {
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
        if (Run.class.isInstance(target)) {
            updateObject((Run) target);
        } // else, not business of this resolver
    }

}
