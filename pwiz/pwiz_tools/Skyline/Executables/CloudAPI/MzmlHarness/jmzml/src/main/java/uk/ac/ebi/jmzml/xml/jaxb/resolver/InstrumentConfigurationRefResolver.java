package uk.ac.ebi.jmzml.xml.jaxb.resolver;

import uk.ac.ebi.jmzml.MzMLElement;
import uk.ac.ebi.jmzml.model.mzml.InstrumentConfiguration;
import uk.ac.ebi.jmzml.model.mzml.ScanSettings;
import uk.ac.ebi.jmzml.model.mzml.Software;
import uk.ac.ebi.jmzml.xml.io.MzMLObjectCache;
import uk.ac.ebi.jmzml.xml.xxindex.MzMLIndexer;

/**
 * Created by IntelliJ IDEA.
 * User: dani
 * Date: 21-Feb-2011
 * Time: 11:05:22
 * To change this template use File | Settings | File Templates.
 */
public class InstrumentConfigurationRefResolver extends AbstractReferenceResolver<InstrumentConfiguration> {
    public InstrumentConfigurationRefResolver(MzMLIndexer index, MzMLObjectCache cache) {
        super(index, cache);
    }

    @Override
    public void updateObject(InstrumentConfiguration object) {
        // if we automatically resolve the references, then update the object with the referenced object
        if (MzMLElement.InstrumentConfiguration.isAutoRefResolving()) {
            // add objects for the refID
            String ref = object.getScanSettingsRef();
            if (ref != null) {
                ScanSettings refObject = this.unmarshal(ref, ScanSettings.class);
                object.setScanSettings(refObject);
            }
            String refSoftware = object.getSoftwareRef();
            if (refSoftware != null){
                Software softwareObject = this.unmarshal(refSoftware, Software.class);
                object.setSoftware(softwareObject);
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
    public void checkRefID(InstrumentConfiguration object) {
        // if there is a referenced object and its ID does not correspond to the refID, then there is something wrong
        if (object.getScanSettings() != null && !object.getScanSettingsRef().equals(object.getScanSettings().getId()) && object.getSoftware() != null && !object.getSoftwareRef().equals(object.getSoftware().getId())) {
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
        if (InstrumentConfiguration.class.isInstance(target)) {
            updateObject((InstrumentConfiguration) target);
        } // else, not business of this resolver
    }

}
