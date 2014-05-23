
package edu.uw.gs.skyline.cloudapi.chromatogramgenerator.chromatogramrequest;

import javax.xml.bind.annotation.XmlEnum;
import javax.xml.bind.annotation.XmlEnumValue;
import javax.xml.bind.annotation.XmlType;


/**
 * <p>Java class for Ms2FullScanAcquisitionMethod.
 * 
 * <p>The following schema fragment specifies the expected content contained within this class.
 * <p>
 * <pre>
 * &lt;simpleType name="Ms2FullScanAcquisitionMethod">
 *   &lt;restriction base="{http://www.w3.org/2001/XMLSchema}string">
 *     &lt;enumeration value="None"/>
 *     &lt;enumeration value="Targeted"/>
 *     &lt;enumeration value="DIA"/>
 *   &lt;/restriction>
 * &lt;/simpleType>
 * </pre>
 * 
 */
@XmlType(name = "Ms2FullScanAcquisitionMethod")
@XmlEnum
public enum Ms2FullScanAcquisitionMethod {

    @XmlEnumValue("None")
    NONE("None"),
    @XmlEnumValue("Targeted")
    TARGETED("Targeted"),
    DIA("DIA");
    private final String value;

    Ms2FullScanAcquisitionMethod(String v) {
        value = v;
    }

    public String value() {
        return value;
    }

    public static Ms2FullScanAcquisitionMethod fromValue(String v) {
        for (Ms2FullScanAcquisitionMethod c: Ms2FullScanAcquisitionMethod.values()) {
            if (c.value.equals(v)) {
                return c;
            }
        }
        throw new IllegalArgumentException(v);
    }

}
