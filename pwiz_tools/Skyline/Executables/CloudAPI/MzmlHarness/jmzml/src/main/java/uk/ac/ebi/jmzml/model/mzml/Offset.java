
package uk.ac.ebi.jmzml.model.mzml;

import javax.xml.bind.annotation.*;
import java.io.Serializable;


/**
 * <p>Java class for OffsetType complex type.
 * 
 * <p>The following schema fragment specifies the expected content contained within this class.
 * 
 * <pre>
 * &lt;complexType name="OffsetType">
 *   &lt;simpleContent>
 *     &lt;extension base="&lt;http://www.w3.org/2001/XMLSchema>long">
 *       &lt;attribute name="idRef" use="required" type="{http://www.w3.org/2001/XMLSchema}string" />
 *       &lt;attribute name="spotID" type="{http://www.w3.org/2001/XMLSchema}string" />
 *       &lt;attribute name="scanTime" type="{http://www.w3.org/2001/XMLSchema}double" />
 *     &lt;/extension>
 *   &lt;/simpleContent>
 * &lt;/complexType>
 * </pre>
 * 
 * 
 */
@XmlAccessorType(XmlAccessType.FIELD)
@XmlType(name = "OffsetType", propOrder = {
    "value"
})
public class Offset
    extends MzMLObject
    implements Serializable
{

    private final static long serialVersionUID = 100L;
    @XmlValue
    protected long value;
    @XmlAttribute(required = true)
    protected String idRef;
    @XmlAttribute
    protected String spotID;
    @XmlAttribute
    protected Double scanTime;

    /**
     * Gets the value of the value property.
     * 
     */
    public long getValue() {
        return value;
    }

    /**
     * Sets the value of the value property.
     * 
     */
    public void setValue(long value) {
        this.value = value;
    }

    /**
     * Gets the value of the idRef property.
     * 
     * @return
     *     possible object is
     *     {@link String }
     *     
     */
    public String getIdRef() {
        return idRef;
    }

    /**
     * Sets the value of the idRef property.
     * 
     * @param value
     *     allowed object is
     *     {@link String }
     *     
     */
    public void setIdRef(String value) {
        this.idRef = value;
    }

    /**
     * Gets the value of the spotID property.
     * 
     * @return
     *     possible object is
     *     {@link String }
     *     
     */
    public String getSpotID() {
        return spotID;
    }

    /**
     * Sets the value of the spotID property.
     * 
     * @param value
     *     allowed object is
     *     {@link String }
     *     
     */
    public void setSpotID(String value) {
        this.spotID = value;
    }

    /**
     * Gets the value of the scanTime property.
     * 
     * @return
     *     possible object is
     *     {@link Double }
     *     
     */
    public Double getScanTime() {
        return scanTime;
    }

    /**
     * Sets the value of the scanTime property.
     * 
     * @param value
     *     allowed object is
     *     {@link Double }
     *     
     */
    public void setScanTime(Double value) {
        this.scanTime = value;
    }

}
