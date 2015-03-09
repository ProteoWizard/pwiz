package uk.ac.ebi.jmzml.model.mzml;

import javax.xml.bind.annotation.*;
import javax.xml.bind.annotation.adapters.CollapsedStringAdapter;
import javax.xml.bind.annotation.adapters.XmlJavaTypeAdapter;
import java.io.Serializable;


/**
 * Description of the acquisition settings of the instrument prior to the start of the run.
 * <p/>
 * <p>Java class for ScanSettingsType complex type.
 * <p/>
 * <p>The following schema fragment specifies the expected content contained within this class.
 * <p/>
 * <pre>
 * &lt;complexType name="ScanSettingsType">
 *   &lt;complexContent>
 *     &lt;extension base="{http://psi.hupo.org/ms/mzml}ParamGroupType">
 *       &lt;sequence>
 *         &lt;element name="sourceFileRefList" type="{http://psi.hupo.org/ms/mzml}SourceFileRefListType" minOccurs="0"/>
 *         &lt;element name="targetList" type="{http://psi.hupo.org/ms/mzml}TargetListType" minOccurs="0"/>
 *       &lt;/sequence>
 *       &lt;attribute name="id" use="required" type="{http://www.w3.org/2001/XMLSchema}ID" />
 *     &lt;/extension>
 *   &lt;/complexContent>
 * &lt;/complexType>
 * </pre>
 */
@XmlAccessorType(XmlAccessType.FIELD)
@XmlType(name = "ScanSettingsType", propOrder = {
        "sourceFileRefList",
        "targetList"
})
public class ScanSettings
        extends ParamGroup
        implements Serializable {

    private final static long serialVersionUID = 100L;

    protected SourceFileRefList sourceFileRefList;
    protected TargetList targetList;

    @XmlAttribute(required = true)
    @XmlJavaTypeAdapter(CollapsedStringAdapter.class)
    @XmlID
    @XmlSchemaType(name = "ID")
    protected String id;

    /**
     * Gets the value of the sourceFileRefList property.
     *
     * @return possible object is
     *         {@link SourceFileRefList }
     */
    public SourceFileRefList getSourceFileRefList() {
        return sourceFileRefList;
    }

    /**
     * Sets the value of the sourceFileRefList property.
     *
     * @param value allowed object is
     *              {@link SourceFileRefList }
     */
    public void setSourceFileRefList(SourceFileRefList value) {
        this.sourceFileRefList = value;
    }

    /**
     * Gets the value of the targetList property.
     *
     * @return
     *     possible object is
     *     {@link TargetList }
     *
     */
    public TargetList getTargetList() {
        return targetList;
    }

    /**
     * Sets the value of the targetList property.
     *
     * @param value
     *     allowed object is
     *     {@link TargetList }
     *
     */
    public void setTargetList(TargetList value) {
        this.targetList = value;
    }

    /**
     * Gets the value of the id property.
     *
     * @return possible object is
     *         {@link String }
     */
    public String getId() {
        return id;
    }

    /**
     * Sets the value of the id property.
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
    public void setId(String value) {
        this.id = value;
    }

}
