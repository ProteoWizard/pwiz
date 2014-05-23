
package edu.uw.gs.skyline.cloudapi.chromatogramgenerator.chromatogramrequest;

import javax.xml.bind.annotation.XmlEnum;
import javax.xml.bind.annotation.XmlEnumValue;
import javax.xml.bind.annotation.XmlType;


/**
 * <p>Java class for ChromSource.
 * 
 * <p>The following schema fragment specifies the expected content contained within this class.
 * <p>
 * <pre>
 * &lt;simpleType name="ChromSource">
 *   &lt;restriction base="{http://www.w3.org/2001/XMLSchema}string">
 *     &lt;enumeration value="Ms1"/>
 *     &lt;enumeration value="Ms2"/>
 *     &lt;enumeration value="Sim"/>
 *   &lt;/restriction>
 * &lt;/simpleType>
 * </pre>
 * 
 */
@XmlType(name = "ChromSource")
@XmlEnum
public enum ChromSource {

    @XmlEnumValue("Ms1")
    MS_1("Ms1"),
    @XmlEnumValue("Ms2")
    MS_2("Ms2"),
    @XmlEnumValue("Sim")
    SIM("Sim");
    private final String value;

    ChromSource(String v) {
        value = v;
    }

    public String value() {
        return value;
    }

    public static ChromSource fromValue(String v) {
        for (ChromSource c: ChromSource.values()) {
            if (c.value.equals(v)) {
                return c;
            }
        }
        throw new IllegalArgumentException(v);
    }

}
