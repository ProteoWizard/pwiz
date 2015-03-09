package uk.ac.ebi.jmzml.model.mzml;

import uk.ac.ebi.jmzml.xml.jaxb.adapters.IdRefAdapter;
import uk.ac.ebi.jmzml.xml.jaxb.adapters.SoftwareRefAdapter;

import javax.xml.bind.annotation.*;
import javax.xml.bind.annotation.adapters.CollapsedStringAdapter;
import javax.xml.bind.annotation.adapters.XmlJavaTypeAdapter;
import java.io.Serializable;


/**
 * Description of a particular hardware configuration of a mass spectrometer. Each configuration must have one (and only one) of the three different components used for an analysis. For hybrid instruments, such as an LTQ-FT, there must be one configuration for each permutation of the components that is used in the document. For software configuration, use a ReferenceableParamGroup element.
 * <p/>
 * <p>Java class for InstrumentConfigurationType complex type.
 * <p/>
 * <p>The following schema fragment specifies the expected content contained within this class.
 * <p/>
 * <pre>
 * &lt;complexType name="InstrumentConfigurationType">
 *   &lt;complexContent>
 *     &lt;extension base="{http://psi.hupo.org/ms/mzml}ParamGroupType">
 *       &lt;sequence>
 *         &lt;element name="componentList" type="{http://psi.hupo.org/ms/mzml}ComponentListType" minOccurs="0"/>
 *         &lt;element name="softwareRef" type="{http://psi.hupo.org/ms/mzml}SoftwareRefType" minOccurs="0"/>
 *       &lt;/sequence>
 *       &lt;attribute name="id" use="required" type="{http://www.w3.org/2001/XMLSchema}ID" />
 *       &lt;attribute name="scanSettingsRef" type="{http://www.w3.org/2001/XMLSchema}IDREF" />
 *     &lt;/extension>
 *   &lt;/complexContent>
 * &lt;/complexType>
 * </pre>
 */
@XmlAccessorType(XmlAccessType.FIELD)
@XmlType(name = "InstrumentConfigurationType", propOrder = {
        "componentList",
        "softwareRef"
})
public class InstrumentConfiguration
        extends ParamGroup
        implements Serializable {

    private final static long serialVersionUID = 100L;
    protected ComponentList componentList;
    @XmlJavaTypeAdapter(SoftwareRefAdapter.class)
    protected String softwareRef;

    @XmlTransient
    private Software software;

    @XmlAttribute(required = true)
    @XmlJavaTypeAdapter(CollapsedStringAdapter.class)
    @XmlID
    @XmlSchemaType(name = "ID")
    protected String id;
    @XmlAttribute
    @XmlJavaTypeAdapter(IdRefAdapter.class)
    @XmlSchemaType(name = "IDREF")
    protected String scanSettingsRef;

    @XmlTransient
    private ScanSettings scanSettings;

    /**
     * Gets the value of the componentList property.
     *
     * @return possible object is
     *         {@link ComponentList }
     */
    public ComponentList getComponentList() {
        return componentList;
    }

    /**
     * Sets the value of the componentList property.
     *
     * @param value allowed object is
     *              {@link ComponentList }
     */
    public void setComponentList(ComponentList value) {
        this.componentList = value;
    }

    /**
     * Gets the value of the softwareRef property.
     *
     * @return possible object is
     *         {@link SoftwareRef }
     */
    public String getSoftwareRef() {
        return softwareRef;
    }

    /**
     * Sets the value of the softwareRef property.
     *
     * @param value allowed object is
     *              {@link SoftwareRef }
     */
    public void setSoftwareRef(String value) {
        this.softwareRef = value;
    }


    public void setSoftware(Software soft){
        this.software = soft;
        if (soft != null){
            this.softwareRef = soft.getId();
        }
    }

    public Software getSoftware(){
        return software;
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

    /**
     * Gets the value of the scanSettingsRef property.
     *
     * @return possible object is
     *         {@link String }
     */
    public String getScanSettingsRef() {
        return scanSettingsRef;
    }

    /**
     * Sets the value of the scanSettingsRef property.
     *
     * @param value allowed object is
     *              {@link String }
     */
    public void setScanSettingsRef(String value) {
        this.scanSettingsRef = value;
    }

    public ScanSettings getScanSettings() {
        return scanSettings;
    }


    public void setScanSettings(ScanSettings scanSettings) {
        this.scanSettings = scanSettings;
        if (scanSettings != null) {
            this.scanSettingsRef = scanSettings.getId();
        }
    }
}
