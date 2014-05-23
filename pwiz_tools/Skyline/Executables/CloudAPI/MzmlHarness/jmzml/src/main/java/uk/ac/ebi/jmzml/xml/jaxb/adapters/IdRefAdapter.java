package uk.ac.ebi.jmzml.xml.jaxb.adapters;

import javax.xml.bind.annotation.adapters.XmlAdapter;

/**
 * Created by IntelliJ IDEA.
 * User: dani
 * Date: 18-Feb-2011
 * Time: 11:25:21
 * To change this template use File | Settings | File Templates.
 */
public class IdRefAdapter extends XmlAdapter<String, String> {
    public String unmarshal(String value) {
        return value;
    }

    public String marshal(String value) {
        return value;
    }

}
