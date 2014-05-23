
package uk.ac.ebi.jmzml.model.mzml;

import uk.ac.ebi.jmzml.xml.jaxb.adapters.IdRefAdapter;

import javax.xml.bind.annotation.*;
import javax.xml.bind.annotation.adapters.XmlJavaTypeAdapter;
import java.io.Serializable;


/**
 * A single chromatogram.
 * 
 * <p>Java class for ChromatogramType complex type.
 * 
 * <p>The following schema fragment specifies the expected content contained within this class.
 * 
 * <pre>
 * &lt;complexType name="ChromatogramType">
 *   &lt;complexContent>
 *     &lt;extension base="{http://psi.hupo.org/ms/mzml}ParamGroupType">
 *       &lt;sequence>
 *         &lt;element name="precursor" type="{http://psi.hupo.org/ms/mzml}PrecursorType" minOccurs="0"/>
 *         &lt;element name="product" type="{http://psi.hupo.org/ms/mzml}ProductType" minOccurs="0"/>
 *         &lt;element name="binaryDataArrayList" type="{http://psi.hupo.org/ms/mzml}BinaryDataArrayListType"/>
 *       &lt;/sequence>
 *       &lt;attribute name="id" use="required" type="{http://www.w3.org/2001/XMLSchema}string" />
 *       &lt;attribute name="index" use="required" type="{http://www.w3.org/2001/XMLSchema}nonNegativeInteger" />
 *       &lt;attribute name="defaultArrayLength" use="required" type="{http://www.w3.org/2001/XMLSchema}int" />
 *       &lt;attribute name="dataProcessingRef" type="{http://www.w3.org/2001/XMLSchema}IDREF" />
 *     &lt;/extension>
 *   &lt;/complexContent>
 * &lt;/complexType>
 * </pre>
 * 
 * 
 */
@XmlAccessorType(XmlAccessType.FIELD)
@XmlType(name = "ChromatogramType", propOrder = {
    "precursor",
    "product",
    "binaryDataArrayList"
})
public class Chromatogram
    extends ParamGroup
    implements Serializable
{

    private final static long serialVersionUID = 100L;
    protected Precursor precursor;

    protected Product product;

    @XmlElement(required = true)
    protected BinaryDataArrayList binaryDataArrayList;

    @XmlAttribute(required = true)
    protected String id;

    @XmlAttribute(required = true)
    @XmlSchemaType(name = "nonNegativeInteger")
    protected Integer index;

    @XmlAttribute(required = true)
    protected int defaultArrayLength;

    @XmlAttribute
    @XmlJavaTypeAdapter(IdRefAdapter.class)
    @XmlSchemaType(name = "IDREF")
    protected String dataProcessingRef;

    @XmlTransient
    private DataProcessing dataProcessing;

    /**
     * Gets the value of the precursor property.
     * 
     * @return
     *     possible object is
     *     {@link Precursor }
     *     
     */
    public Precursor getPrecursor() {
        return precursor;
    }

    /**
     * Sets the value of the precursor property.
     * 
     * @param value
     *     allowed object is
     *     {@link Precursor }
     *     
     */
    public void setPrecursor(Precursor value) {
        this.precursor = value;
    }

    /**
     * Gets the value of the product property.
     * 
     * @return
     *     possible object is
     *     {@link Product }
     *     
     */
    public Product getProduct() {
        return product;
    }

    /**
     * Sets the value of the product property.
     * 
     * @param value
     *     allowed object is
     *     {@link Product }
     *     
     */
    public void setProduct(Product value) {
        this.product = value;
    }

    /**
     * Gets the value of the binaryDataArrayList property.
     * 
     * @return
     *     possible object is
     *     {@link BinaryDataArrayList }
     *     
     */
    public BinaryDataArrayList getBinaryDataArrayList() {
        return binaryDataArrayList;
    }

    /**
     * Sets the value of the binaryDataArrayList property.
     *
     * @param value
     *     allowed object is
     *     {@link BinaryDataArrayList }
     *
     */
    public void setBinaryDataArrayList(BinaryDataArrayList value) {
        this.binaryDataArrayList = value;
    }

    /**
     * Gets the value of the id property.
     * 
     * @return
     *     possible object is
     *     {@link String }
     *     
     */
    public String getId() {
        return id;
    }

    /**
     * Sets the value of the id property.
     * 
     * @param value
     *     allowed object is
     *     {@link String }
     *     
     */
    public void setId(String value) {
        this.id = value;
    }

    /**
     * Gets the value of the index property.
     * 
     * @return
     *     possible object is
     *     {@link Integer }
     *     
     */
    public Integer getIndex() {
        return index;
    }

    /**
     * Sets the value of the index property.
     * 
     * @param value
     *     allowed object is
     *     {@link Integer }
     *     
     */
    public void setIndex(Integer value) {
        this.index = value;
    }

    /**
     * Gets the value of the defaultArrayLength property.
     * 
     */
    public int getDefaultArrayLength() {
        return defaultArrayLength;
    }

    /**
     * Sets the value of the defaultArrayLength property.
     * 
     */
    public void setDefaultArrayLength(int value) {
        this.defaultArrayLength = value;
    }

    /**
     * Gets the value of the dataProcessingRef property.
     * 
     * @return
     *     possible object is
     *     {@link String }
     *     
     */
    public String getDataProcessingRef() {
        return dataProcessingRef;
    }

    /**
     * Sets the value of the dataProcessingRef property.
     * 
     * @param value
     *     allowed object is
     *     {@link String }
     *     
     */
    public void setDataProcessingRef(String value) {
        this.dataProcessingRef = value;
    }

/**
     * Gets the value of the dataProcessing property.
     * Note: this property may be populated automatically at unmarshal
     * time with the Object referenced with the dataProcessingRef property.
     *
     * @return Valid values are DataProcessing objects.
     * @see uk.ac.ebi.jmzml.MzMLElement#isAutoRefResolving()
     */
    public DataProcessing getDataProcessing() {
        return dataProcessing;
    }


    /**
     * Sets a DataProcessing reference. Setting a DataProcessing object will update
     * the dataProcessingRef element with the id from the new DataProcessing object.
     *
     * @param dataProcessing the DataProcessing to reference from this BinaryDataArray.
     * @see #dataProcessingRef
     */
    public void setDataProcessing(DataProcessing dataProcessing) {
        this.dataProcessing = dataProcessing;
        if (dataProcessing != null) {
            this.dataProcessingRef = dataProcessing.getId();
        }
    }


}
