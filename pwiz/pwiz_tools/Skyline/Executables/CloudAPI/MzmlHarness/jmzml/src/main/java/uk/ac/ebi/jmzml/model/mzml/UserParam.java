package uk.ac.ebi.jmzml.model.mzml;

import uk.ac.ebi.jmzml.xml.jaxb.adapters.IdRefAdapter;

import javax.xml.bind.annotation.*;
import javax.xml.bind.annotation.adapters.XmlJavaTypeAdapter;
import java.io.Serializable;


/**
 * Uncontrolled user parameters (essentially allowing free text). Before using these, one should verify whether there is an appropriate CV term available, and if so, use the CV term instead
 * <p/>
 * <p>Java class for UserParamType complex type.
 * <p/>
 * <p>The following schema fragment specifies the expected content contained within this class.
 * <p/>
 * <pre>
 * &lt;complexType name="UserParamType">
 *   &lt;complexContent>
 *     &lt;restriction base="{http://www.w3.org/2001/XMLSchema}anyType">
 *       &lt;attribute name="name" use="required" type="{http://www.w3.org/2001/XMLSchema}string" />
 *       &lt;attribute name="type" type="{http://www.w3.org/2001/XMLSchema}string" />
 *       &lt;attribute name="value" type="{http://www.w3.org/2001/XMLSchema}string" />
 *       &lt;attribute name="unitAccession" type="{http://www.w3.org/2001/XMLSchema}string" />
 *       &lt;attribute name="unitName" type="{http://www.w3.org/2001/XMLSchema}string" />
 *       &lt;attribute name="unitCvRef" type="{http://www.w3.org/2001/XMLSchema}IDREF" />
 *     &lt;/restriction>
 *   &lt;/complexContent>
 * &lt;/complexType>
 * </pre>
 */
@XmlAccessorType(XmlAccessType.FIELD)
@XmlType(name = "UserParamType")
public class UserParam
        extends MzMLObject
        implements Serializable {

    private final static long serialVersionUID = 100L;
    @XmlAttribute(required = true)
    protected String name;
    @XmlAttribute
    protected String type;
    @XmlAttribute
    protected String value;
    @XmlAttribute
    protected String unitAccession;
    @XmlAttribute
    protected String unitName;
    @XmlAttribute
    @XmlJavaTypeAdapter(IdRefAdapter.class)
    @XmlSchemaType(name = "IDREF")
    protected String unitCvRef;
    @XmlTransient
    private CV unitCv;

    /**
     * Added a boolean to indicate whether this UserParam was inferred from
     * a referenceableParamGroupRef. If so, this cvParam should not be marshalled
     * out, as it will already be marshalled out in the referenceableParamGroupRef.
     * Also, caution is to be used when editing!
     */
    @XmlTransient
    private boolean isInferredFromReferenceableParamGroupRef = false;


    /**
     * Gets the value of the name property.
     *
     * @return possible object is
     *         {@link String }
     */
    public String getName() {
        return name;
    }

    /**
     * Sets the value of the name property.
     *
     * @param value allowed object is
     *              {@link String }
     */
    public void setName(String value) {
        this.name = value;
    }

    /**
     * Gets the value of the type property.
     *
     * @return possible object is
     *         {@link String }
     */
    public String getType() {
        return type;
    }

    /**
     * Sets the value of the type property.
     *
     * @param value allowed object is
     *              {@link String }
     */
    public void setType(String value) {
        this.type = value;
    }

    /**
     * Gets the value of the value property.
     *
     * @return possible object is
     *         {@link String }
     */
    public String getValue() {
        return value;
    }

    /**
     * Sets the value of the value property.
     *
     * @param value allowed object is
     *              {@link String }
     */
    public void setValue(String value) {
        this.value = value;
    }

    /**
     * Gets the value of the unitAccession property.
     *
     * @return possible object is
     *         {@link String }
     */
    public String getUnitAccession() {
        return unitAccession;
    }

    /**
     * Sets the value of the unitAccession property.
     *
     * @param value allowed object is
     *              {@link String }
     */
    public void setUnitAccession(String value) {
        this.unitAccession = value;
    }

    /**
     * Gets the value of the unitName property.
     *
     * @return possible object is
     *         {@link String }
     */
    public String getUnitName() {
        return unitName;
    }

    /**
     * Sets the value of the unitName property.
     *
     * @param value allowed object is
     *              {@link String }
     */
    public void setUnitName(String value) {
        this.unitName = value;
    }

    /**
     * Gets the value of the unitCvRef property.
     *
     * @return possible object is
     *         {@link String }
     */
    public String getUnitCvRef() {
        return unitCvRef;
    }

    /**
     * Sets the value of the unitCvRef property.
     *
     * @param value allowed object is
     *              {@link String }
     */
    public void setUnitCvRef(String value) {
        this.unitCvRef = value;
    }

    public CV getUnitCv() {
        return unitCv;
    }

    public void setUnitCv(CV unitCv) {
        this.unitCv = unitCv;
        if (unitCv != null) {
            this.unitCvRef = unitCv.getId();
        }
    }

    public String toString() {
        return "UserParam{" +
                "name='" + name + '\'' +
                ", type='" + type + '\'' +
                ", value='" + value + '\'' +
                ", unitAccession='" + unitAccession + '\'' +
                ", unitName='" + unitName + '\'' +
                ", unitCV=" + unitCv +
                '}';
    }

    /**
     * This boolean indicates whether this UserParam was inferred from
     * a referenceableParamGroupRef. If so, this cvParam should not be marshalled
     * out, as it will already be marshalled out in the referenceableParamGroupRef.
     * Also, caution is to be used when editing the UserParam!
     *
     * @return boolean that indicates whether this UserParam was inferred from
     *         a referenceableParamGroupRef.
     */
    public boolean isInferredFromReferenceableParamGroupRef() {
        return isInferredFromReferenceableParamGroupRef;
    }

    /**
     * You can set this boolean to indicate whether this UserParam was inferred from
     * a referenceableParamGroupRef. If so, this cvParam will not be marshalled
     * out, as it will already be marshalled out in the referenceableParamGroupRef.
     * Also, caution is to be used when editing the UserParam!
     *
     * @param inferredFromReferenceableParamGroupRef
     *         boolean to indicates whether
     *         this UserParam was inferred from
     *         a referenceableParamGroupRef.
     */
    public void setInferredFromReferenceableParamGroupRef(boolean inferredFromReferenceableParamGroupRef) {
        isInferredFromReferenceableParamGroupRef = inferredFromReferenceableParamGroupRef;
    }
}
