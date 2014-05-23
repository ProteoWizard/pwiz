package uk.ac.ebi.jmzml.model.mzml;

import javax.xml.bind.annotation.XmlTransient;

/**
 * Created by IntelliJ IDEA.
 * User: dani
 * Date: 18-Feb-2011
 * Time: 11:12:45
 * To change this template use File | Settings | File Templates.
 */
@javax.xml.bind.annotation.XmlTransient
public abstract class MzMLObject {

    @XmlTransient
    protected long hid;

    public long getHid(){
        return hid;
    }
}
