
package uk.ac.ebi.jmzml.model.mzml;

import java.io.Serializable;
import javax.xml.bind.annotation.XmlAccessType;
import javax.xml.bind.annotation.XmlAccessorType;
import javax.xml.bind.annotation.XmlType;


/**
 * The method of product ion selection and activation in a precursor ion scan
 * 
 * <p>Java class for ProductType complex type.
 * 
 * <p>The following schema fragment specifies the expected content contained within this class.
 * 
 * <pre>
 * &lt;complexType name="ProductType">
 *   &lt;complexContent>
 *     &lt;restriction base="{http://www.w3.org/2001/XMLSchema}anyType">
 *       &lt;sequence>
 *         &lt;element name="isolationWindow" type="{http://psi.hupo.org/ms/mzml}ParamGroupType" minOccurs="0"/>
 *       &lt;/sequence>
 *     &lt;/restriction>
 *   &lt;/complexContent>
 * &lt;/complexType>
 * </pre>
 * 
 * 
 */
@XmlAccessorType(XmlAccessType.FIELD)
@XmlType(name = "ProductType", propOrder = {
    "isolationWindow"
})
public class Product
    extends MzMLObject
    implements Serializable
{

    private final static long serialVersionUID = 100L;
    protected ParamGroup isolationWindow;

    /**
     * Gets the value of the isolationWindow property.
     * 
     * @return
     *     possible object is
     *     {@link ParamGroup }
     *     
     */
    public ParamGroup getIsolationWindow() {
        return isolationWindow;
    }

    /**
     * Sets the value of the isolationWindow property.
     * 
     * @param value
     *     allowed object is
     *     {@link ParamGroup }
     *     
     */
    public void setIsolationWindow(ParamGroup value) {
        this.isolationWindow = value;
    }

}
