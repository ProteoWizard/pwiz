package uk.ac.ebi.jmzml.model.mzml;

import uk.ac.ebi.jmzml.xml.jaxb.adapters.IdRefAdapter;

import javax.xml.bind.annotation.*;
import javax.xml.bind.annotation.adapters.XmlJavaTypeAdapter;
import java.io.Serializable;
import java.util.List;


/**
 * This element holds additional data or annotation. Only controlled values are allowed here.
 * <p/>
 * <p>Java class for CVParamType complex type.
 * <p/>
 * <p>The following schema fragment specifies the expected content contained within this class.
 * <p/>
 * <pre>
 * &lt;complexType name="CVParamType">
 *   &lt;complexContent>
 *     &lt;restriction base="{http://www.w3.org/2001/XMLSchema}anyType">
 *       &lt;attribute name="cvRef" use="required" type="{http://www.w3.org/2001/XMLSchema}IDREF" />
 *       &lt;attribute name="accession" use="required" type="{http://www.w3.org/2001/XMLSchema}string" />
 *       &lt;attribute name="value" type="{http://www.w3.org/2001/XMLSchema}string" />
 *       &lt;attribute name="name" use="required" type="{http://www.w3.org/2001/XMLSchema}string" />
 *       &lt;attribute name="unitAccession" type="{http://www.w3.org/2001/XMLSchema}string" />
 *       &lt;attribute name="unitName" type="{http://www.w3.org/2001/XMLSchema}string" />
 *       &lt;attribute name="unitCvRef" type="{http://www.w3.org/2001/XMLSchema}IDREF" />
 *     &lt;/restriction>
 *   &lt;/complexContent>
 * &lt;/complexType>
 * </pre>
 */
@XmlAccessorType(XmlAccessType.FIELD)
@XmlType(name = "CVParamType")
public class CVParam
        extends MzMLObject
        implements Serializable {

    private final static long serialVersionUID = 100L;
    @XmlAttribute(required = true)
    @XmlJavaTypeAdapter(IdRefAdapter.class)
    @XmlSchemaType(name = "IDREF")
    protected String cvRef;
    @XmlTransient
    private CV cv;

    @XmlAttribute(required = true)
    protected String accession;
    @XmlAttribute
    protected String value;
    @XmlAttribute(required = true)
    protected String name;
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
     * Added a boolean to indicate whether this CVParam was inferred from
     * a referenceableParamGroupRef. If so, this cvParam should not be marshalled
     * out, as it will already be marshalled out in the referenceableParamGroupRef.
     * Also, caution is to be used when editing!
     */
    @XmlTransient
    private boolean isInferredFromReferenceableParamGroupRef = false;

    /**
     * Gets the value of the cvRef property.
     *
     * @return possible object is
     *         {@link String }
     */
    public String getCvRef() {
        return cvRef;
    }

    /**
     * Sets the value of the cvRef property.
     *
     * Note: the id attribute is restricted by the XML schema data type 'ID'.
     * Valid values follow the NCName definition.
     * See also:
     *  http://www.w3.org/TR/2000/CR-xmlschema-2-20001024/#ID
     *  http://www.w3.org/TR/REC-xml-names/#NT-NCName
     *
     * @param value allowed object is
     *              {@link String }
     */
    public void setCvRef(String value) {
        this.cvRef = value;
    }

    public CV getCv() {
        return cv;
    }

    public void setCv(CV cv) {
        this.cv = cv;
        if (cv != null) {
            this.cvRef = cv.getId();
        }
    }

    /**
     * Gets the value of the accession property.
     *
     * @return possible object is
     *         {@link String }
     */
    public String getAccession() {
        return accession;
    }

    /**
     * Sets the value of the accession property.
     *
     * @param value allowed object is
     *              {@link String }
     */
    public void setAccession(String value) {
        this.accession = value;
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
        return "CVParam{" +
                "cv=" + cv +
                ", accession='" + accession + '\'' +
                ", value='" + value + '\'' +
                ", name='" + name + '\'' +
                ", unitAccession='" + unitAccession + '\'' +
                ", unitName='" + unitName + '\'' +
                ", unitCV=" + unitCv +
                '}';
    }

    /**
     * This boolean indicates whether this CVParam was inferred from
     * a referenceableParamGroupRef. If so, this cvParam should not be marshalled
     * out, as it will already be marshalled out in the referenceableParamGroupRef.
     * Also, caution is to be used when editing the CVParam!
     *
     * @return boolean that indicates whether this CVParam was inferred from
     *         a referenceableParamGroupRef.
     */
    public boolean isInferredFromReferenceableParamGroupRef() {
        return isInferredFromReferenceableParamGroupRef;
    }
    
    /**
     * Checks whether this CVParam is contained by the supplied list.
     * @param paramList List of CVParam objects.
     * @return Whether CVParam is contained by list.
     */
    public boolean isContainedBy(List<CVParam> paramList) {
        for (CVParam param : paramList) {
            if (this.getAccession().equals(param.getAccession())) {
                return true;
            }
        }
        
        return false;
    }

    /**
     * You can set this boolean to indicate whether this CVParam was inferred from
     * a referenceableParamGroupRef. If so, this cvParam will not be marshalled
     * out, as it will already be marshalled out in the referenceableParamGroupRef.
     * Also, caution is to be used when editing the CVParam!
     *
     * @param inferredFromReferenceableParamGroupRef
     *         boolean to indicates whether
     *         this CVParam was inferred from
     *         a referenceableParamGroupRef.
     */
    public void setInferredFromReferenceableParamGroupRef(boolean inferredFromReferenceableParamGroupRef) {
        isInferredFromReferenceableParamGroupRef = inferredFromReferenceableParamGroupRef;
    }
}
