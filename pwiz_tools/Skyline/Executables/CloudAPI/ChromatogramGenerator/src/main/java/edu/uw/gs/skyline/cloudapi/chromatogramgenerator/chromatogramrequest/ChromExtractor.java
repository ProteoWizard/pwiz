
package edu.uw.gs.skyline.cloudapi.chromatogramgenerator.chromatogramrequest;

import javax.xml.bind.annotation.XmlEnum;
import javax.xml.bind.annotation.XmlEnumValue;
import javax.xml.bind.annotation.XmlType;


/**
 * <p>Java class for ChromExtractor.
 * 
 * <p>The following schema fragment specifies the expected content contained within this class.
 * <p>
 * <pre>
 * &lt;simpleType name="ChromExtractor">
 *   &lt;restriction base="{http://www.w3.org/2001/XMLSchema}string">
 *     &lt;enumeration value="Summed"/>
 *     &lt;enumeration value="BasePeak"/>
 *   &lt;/restriction>
 * &lt;/simpleType>
 * </pre>
 * 
 */
@XmlType(name = "ChromExtractor")
@XmlEnum
public enum ChromExtractor {

    @XmlEnumValue("Summed")
    SUMMED("Summed"),
    @XmlEnumValue("BasePeak")
    BASE_PEAK("BasePeak");
    private final String value;

    ChromExtractor(String v) {
        value = v;
    }

    public String value() {
        return value;
    }

    public static ChromExtractor fromValue(String v) {
        for (ChromExtractor c: ChromExtractor.values()) {
            if (c.value.equals(v)) {
                return c;
            }
        }
        throw new IllegalArgumentException(v);
    }

}
