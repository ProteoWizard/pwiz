package uk.ac.ebi.jmzml.model.mzml.utilities;

/**
 * @author Florian Reisinger
 *         Date: 19/12/11
 * @since $version
 */
public class MzMLElementConfig {

    private String tagName;
    private boolean indexed;
    private String xpath;
    private boolean idMapped;
    private Class clazz;
    private boolean autoRefResolving;
    private Class refResolverClass;

    // additional values that are used in the jmzidentml API
    //    private boolean cached;
    //    private Class cvParamClass;
    //    private Class userParamClass;


    public String getTagName() {
        return tagName;
    }

    public void setTagName(String tagName) {
        this.tagName = tagName;
    }

    public boolean isIndexed() {
        return indexed;
    }

    public void setIndexed(boolean indexed) {
        this.indexed = indexed;
    }

    public String getXpath() {
        return xpath;
    }

    public void setXpath(String xpath) {
        this.xpath = xpath;
    }

    public boolean isIdMapped() {
        return idMapped;
    }

    public void setIdMapped(boolean idMapped) {
        this.idMapped = idMapped;
    }

    public Class getClazz() {
        return clazz;
    }

    public void setClazz(Class clazz) {
        this.clazz = clazz;
    }

    public boolean isAutoRefResolving() {
        return autoRefResolving;
    }

    public void setAutoRefResolving(boolean autoRefResolving) {
        this.autoRefResolving = autoRefResolving;
    }

    public Class getRefResolverClass() {
        return refResolverClass;
    }

    public void setRefResolverClass(Class refResolverClass) {
        this.refResolverClass = refResolverClass;
    }
}
