
package uk.ac.ebi.jmzml.model.mzml;

import javax.xml.bind.annotation.*;
import java.io.Serializable;
import java.util.ArrayList;
import java.util.List;


/**
 * Container for a list of referenceableParamGroups
 * 
 * <p>Java class for ReferenceableParamGroupListType complex type.
 * 
 * <p>The following schema fragment specifies the expected content contained within this class.
 * 
 * <pre>
 * &lt;complexType name="ReferenceableParamGroupListType">
 *   &lt;complexContent>
 *     &lt;restriction base="{http://www.w3.org/2001/XMLSchema}anyType">
 *       &lt;sequence>
 *         &lt;element name="referenceableParamGroup" type="{http://psi.hupo.org/ms/mzml}ReferenceableParamGroupType" maxOccurs="unbounded"/>
 *       &lt;/sequence>
 *       &lt;attribute name="count" use="required" type="{http://www.w3.org/2001/XMLSchema}nonNegativeInteger" />
 *     &lt;/restriction>
 *   &lt;/complexContent>
 * &lt;/complexType>
 * </pre>
 * 
 * 
 */
@XmlAccessorType(XmlAccessType.FIELD)
@XmlType(name = "ReferenceableParamGroupListType", propOrder = {
    "referenceableParamGroup"
})
public class ReferenceableParamGroupList
    extends MzMLObject
    implements Serializable
{

    private final static long serialVersionUID = 100L;
    @XmlElement(required = true)
    protected List<ReferenceableParamGroup> referenceableParamGroup;
    @XmlAttribute(required = true)
    @XmlSchemaType(name = "nonNegativeInteger")
    protected Integer count;

    /**
     * Gets the value of the referenceableParamGroup property.
     * 
     * <p>
     * This accessor method returns a reference to the live list,
     * not a snapshot. Therefore any modification you make to the
     * returned list will be present inside the JAXB object.
     * This is why there is not a <CODE>set</CODE> method for the referenceableParamGroup property.
     * 
     * <p>
     * For example, to add a new item, do as follows:
     * <pre>
     *    getReferenceableParamGroup().add(newItem);
     * </pre>
     * 
     * 
     * <p>
     * Objects of the following type(s) are allowed in the list
     * {@link ReferenceableParamGroup }
     * 
     * 
     */
    public List<ReferenceableParamGroup> getReferenceableParamGroup() {
        if (referenceableParamGroup == null) {
            referenceableParamGroup = new ArrayList<ReferenceableParamGroup>();
        }
        return this.referenceableParamGroup;
    }

    /**
     * Gets the value of the count property.
     * 
     * @return
     *     possible object is
     *     {@link Integer }
     *     
     */
    public Integer getCount() {
        return count;
    }

    /**
     * Sets the value of the count property.
     * 
     * @param value
     *     allowed object is
     *     {@link Integer }
     *     
     */
    public void setCount(Integer value) {
        this.count = value;
    }

}
