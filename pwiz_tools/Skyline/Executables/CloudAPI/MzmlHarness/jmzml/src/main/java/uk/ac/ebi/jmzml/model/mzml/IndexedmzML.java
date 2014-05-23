
package uk.ac.ebi.jmzml.model.mzml;

import javax.xml.bind.annotation.*;
import java.io.Serializable;


/**
 * <p>Java class for anonymous complex type.
 * 
 * <p>The following schema fragment specifies the expected content contained within this class.
 * 
 * <pre>
 * &lt;complexType>
 *   &lt;complexContent>
 *     &lt;restriction base="{http://www.w3.org/2001/XMLSchema}anyType">
 *       &lt;sequence>
 *         &lt;element ref="{http://psi.hupo.org/ms/mzml}mzML"/>
 *         &lt;element name="indexList" type="{http://psi.hupo.org/ms/mzml}IndexListType"/>
 *         &lt;element name="indexListOffset" type="{http://www.w3.org/2001/XMLSchema}long"/>
 *         &lt;element name="fileChecksum" type="{http://www.w3.org/2001/XMLSchema}string"/>
 *       &lt;/sequence>
 *     &lt;/restriction>
 *   &lt;/complexContent>
 * &lt;/complexType>
 * </pre>
 * 
 * 
 */
@XmlAccessorType(XmlAccessType.FIELD)
@XmlType(name = "", propOrder = {
    "mzML",
    "indexList",
    "indexListOffset",
    "fileChecksum"
})
@XmlRootElement(name = "indexedmzML")
public class IndexedmzML
    extends MzMLObject
    implements Serializable
{

    private final static long serialVersionUID = 100L;

    @XmlElement(required = true)
    protected MzML mzML;

    @XmlElement(required = true)
    protected IndexList indexList;

    @XmlElement(required = true, type = Long.class, nillable = true)
    protected Long indexListOffset;

    @XmlElement(required = true)
    protected String fileChecksum;

    /**
     * Gets the value of the mzML property.
     * 
     * @return
     *     possible object is
     *     {@link MzML }
     *     
     */
    public MzML getMzML() {
        return mzML;
    }

    /**
     * Sets the value of the mzML property.
     * 
     * @param value
     *     allowed object is
     *     {@link MzML }
     *     
     */
    public void setMzML(MzML value) {
        this.mzML = value;
    }

    /**
     * Gets the value of the indexList property.
     *
     * @return
     *     possible object is
     *     {@link IndexList }
     *
     */
    public IndexList getIndexList() {
        return indexList;
    }

    /**
     * Sets the value of the indexList property.
     *
     * @param value
     *     allowed object is
     *     {@link IndexList }
     *
     */
    public void setIndexList(IndexList value) {
        this.indexList = value;
    }

    /**
     * Gets the value of the indexListOffset property.
     * 
     * @return
     *     possible object is
     *     {@link Long }
     *     
     */
    public Long getIndexListOffset() {
        return indexListOffset;
    }

    /**
     * Sets the value of the indexListOffset property.
     * 
     * @param value
     *     allowed object is
     *     {@link Long }
     *     
     */
    public void setIndexListOffset(Long value) {
        this.indexListOffset = value;
    }

    /**
     * Gets the value of the fileChecksum property.
     * 
     * @return
     *     possible object is
     *     {@link String }
     *     
     */
    public String getFileChecksum() {
        return fileChecksum;
    }

    /**
     * Sets the value of the fileChecksum property.
     * 
     * @param value
     *     allowed object is
     *     {@link String }
     *     
     */
    public void setFileChecksum(String value) {
        this.fileChecksum = value;
    }

}
