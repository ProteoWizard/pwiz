package uk.ac.ebi.jmzml.xml.io;

import uk.ac.ebi.jmzml.model.mzml.MzMLObject;

import java.util.List;

/**
 * @author Florian Reisinger
 *         Date: 11-Nov-2010
 * @since 1.0
 */
public interface MzMLObjectCache {

    // ToDo: change to only handle MzMLObjects
    // ToDo: that would also mean we can not cache CvParams or UserParams, etc
    // that way we make sure that the objects have an ID which identifies them!

    public void putInCache(String id, MzMLObject object);

    public void putInCache(MzMLObject element);

    public <T extends MzMLObject> T getCachedObject(String id, Class<T> cls);

    public <T extends MzMLObject> boolean hasEntry(Class<T> clazz);

    public <T extends MzMLObject> List<T> getEntries(Class<T> clazz);

}
