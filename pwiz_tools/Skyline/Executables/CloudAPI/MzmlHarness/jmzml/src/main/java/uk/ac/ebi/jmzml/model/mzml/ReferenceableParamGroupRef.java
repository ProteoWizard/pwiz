
package uk.ac.ebi.jmzml.model.mzml;

import uk.ac.ebi.jmzml.xml.jaxb.adapters.IdRefAdapter;

import javax.xml.bind.annotation.*;
import javax.xml.bind.annotation.adapters.XmlJavaTypeAdapter;
import java.io.Serializable;


/**
 * A reference to a previously defined ParamGroup, which is a reusable container of one or more cvParams.
 * 
 * <p>Java class for ReferenceableParamGroupRefType complex type.
 * 
 * <p>The following schema fragment specifies the expected content contained within this class.
 * 
 * <pre>
 * &lt;complexType name="ReferenceableParamGroupRefType">
 *   &lt;complexContent>
 *     &lt;restriction base="{http://www.w3.org/2001/XMLSchema}anyType">
 *       &lt;attribute name="ref" use="required" type="{http://www.w3.org/2001/XMLSchema}IDREF" />
 *     &lt;/restriction>
 *   &lt;/complexContent>
 * &lt;/complexType>
 * </pre>
 * 
 * 
 */
@XmlAccessorType(XmlAccessType.FIELD)
@XmlType(name = "ReferenceableParamGroupRefType")
public class ReferenceableParamGroupRef
    extends MzMLObject
    implements Serializable
{

    private final static long serialVersionUID = 100L;
    @XmlAttribute(required = true)
    @XmlJavaTypeAdapter(IdRefAdapter.class)
    @XmlSchemaType(name = "IDREF")
    protected String ref;

    @XmlTransient
    private ReferenceableParamGroup referenceableParamGroup;



    /**
     * Gets the value of the ref property.
     * 
     * @return
     *     possible object is
     *     {@link String }
     *     
     */
    public String getRef() {
        return ref;
    }

    /**
     * Sets the value of the ref property.
     * 
     * @param value
     *     allowed object is
     *     {@link String }
     *     
     */
    public void setRef(String value) {
        this.ref = value;
    }

    public ReferenceableParamGroup getReferenceableParamGroup() {
        return referenceableParamGroup;
    }

    public void setReferenceableParamGroup(ReferenceableParamGroup referenceableParamGroup) {
        this.referenceableParamGroup = referenceableParamGroup;
        if (referenceableParamGroup != null) {
            this.ref = referenceableParamGroup.getId();
        }
    }

}
