
package edu.uw.gs.skyline.cloudapi.chromatogramgenerator.chromatogramrequest;

import javax.xml.bind.annotation.XmlEnum;
import javax.xml.bind.annotation.XmlEnumValue;
import javax.xml.bind.annotation.XmlType;


/**
 * <p>Java class for PerPointScore.
 * 
 * <p>The following schema fragment specifies the expected content contained within this class.
 * <p>
 * <pre>
 * &lt;simpleType name="PerPointScore">
 *   &lt;restriction base="{http://www.w3.org/2001/XMLSchema}string">
 *     &lt;enumeration value="MassError"/>
 *   &lt;/restriction>
 * &lt;/simpleType>
 * </pre>
 * 
 */
@XmlType(name = "PerPointScore")
@XmlEnum
public enum PerPointScore {

    @XmlEnumValue("MassError")
    MASS_ERROR("MassError");
    private final String value;

    PerPointScore(String v) {
        value = v;
    }

    public String value() {
        return value;
    }

    public static PerPointScore fromValue(String v) {
        for (PerPointScore c: PerPointScore.values()) {
            if (c.value.equals(v)) {
                return c;
            }
        }
        throw new IllegalArgumentException(v);
    }

}
