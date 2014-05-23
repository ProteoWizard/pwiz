package uk.ac.ebi.jmzml.model.mzml;


import javax.xml.bind.annotation.*;
import java.io.Serializable;


/**
 * This is the root element for the Proteomics Standards Initiative (PSI) mzML schema, which is intended to capture the use of a mass spectrometer, the data generated, and the initial processing of that data (to the level of the peak list).
 * <p/>
 * <p>Java class for mzMLType complex type.
 * <p/>
 * <p>The following schema fragment specifies the expected content contained within this class.
 * <p/>
 * <pre>
 * &lt;complexType name="mzMLType">
 *   &lt;complexContent>
 *     &lt;restriction base="{http://www.w3.org/2001/XMLSchema}anyType">
 *       &lt;sequence>
 *         &lt;element name="cvList" type="{http://psi.hupo.org/ms/mzml}CVListType"/>
 *         &lt;element name="fileDescription" type="{http://psi.hupo.org/ms/mzml}FileDescriptionType"/>
 *         &lt;element name="referenceableParamGroupList" type="{http://psi.hupo.org/ms/mzml}ReferenceableParamGroupListType" minOccurs="0"/>
 *         &lt;element name="sampleList" type="{http://psi.hupo.org/ms/mzml}SampleListType" minOccurs="0"/>
 *         &lt;element name="softwareList" type="{http://psi.hupo.org/ms/mzml}SoftwareListType"/>
 *         &lt;element name="scanSettingsList" type="{http://psi.hupo.org/ms/mzml}ScanSettingsListType" minOccurs="0"/>
 *         &lt;element name="instrumentConfigurationList" type="{http://psi.hupo.org/ms/mzml}InstrumentConfigurationListType"/>
 *         &lt;element name="dataProcessingList" type="{http://psi.hupo.org/ms/mzml}DataProcessingListType"/>
 *         &lt;element name="run" type="{http://psi.hupo.org/ms/mzml}RunType"/>
 *       &lt;/sequence>
 *       &lt;attribute name="accession" type="{http://www.w3.org/2001/XMLSchema}string" />
 *       &lt;attribute name="version" use="required" type="{http://www.w3.org/2001/XMLSchema}string" />
 *       &lt;attribute name="id" type="{http://www.w3.org/2001/XMLSchema}string" />
 *     &lt;/restriction>
 *   &lt;/complexContent>
 * &lt;/complexType>
 * </pre>
 */
@XmlAccessorType(XmlAccessType.FIELD)
@XmlType(name = "mzMLType", propOrder = {
        "cvList",
        "fileDescription",
        "referenceableParamGroupList",
        "sampleList",
        "softwareList",
        "scanSettingsList",
        "instrumentConfigurationList",
        "dataProcessingList",
        "run"
})
public class MzML
        extends MzMLObject
        implements Serializable {

    private final static long serialVersionUID = 100L;

    @XmlElement(required = true)
    protected CVList cvList;

    @XmlElement(required = true)
    protected FileDescription fileDescription;

    protected ReferenceableParamGroupList referenceableParamGroupList;

    protected SampleList sampleList;

    @XmlElement(required = true)
    protected SoftwareList softwareList;
    protected ScanSettingsList scanSettingsList;
    @XmlElement(required = true)
    protected InstrumentConfigurationList instrumentConfigurationList;
    @XmlElement(required = true)
    protected DataProcessingList dataProcessingList;
    @XmlElement(required = true)
    protected Run run;
    @XmlAttribute
    protected String accession;
    @XmlAttribute(required = true)
    protected String version;
    @XmlAttribute
    protected String id;

    /**
     * Gets the value of the cvList property.
     *
     * @return
     *     possible object is
     *     {@link CVList }
     *
     */
    public CVList getCvList() {
        return cvList;
    }

    /**
     * Sets the value of the cvList property.
     *
     * @param value
     *     allowed object is
     *     {@link CVList }
     *
     */
    public void setCvList(CVList value) {
        this.cvList = value;
    }

    /**
     * Gets the value of the fileDescription property.
     *
     * @return
     *     possible object is
     *     {@link FileDescription }
     *
     */
    public FileDescription getFileDescription() {
        return fileDescription;
    }

    /**
     * Sets the value of the fileDescription property.
     *
     * @param value
     *     allowed object is
     *     {@link FileDescription }
     *
     */
    public void setFileDescription(FileDescription value) {
        this.fileDescription = value;
    }

    /**
     * Gets the value of the referenceableParamGroupList property.
     *
     * @return
     *     possible object is
     *     {@link ReferenceableParamGroupList }
     *
     */
    public ReferenceableParamGroupList getReferenceableParamGroupList() {
        return referenceableParamGroupList;
    }

    /**
     * Sets the value of the referenceableParamGroupList property.
     *
     * @param value
     *     allowed object is
     *     {@link ReferenceableParamGroupList }
     *
     */
    public void setReferenceableParamGroupList(ReferenceableParamGroupList value) {
        this.referenceableParamGroupList = value;
    }

    /**
     * Gets the value of the sampleList property.
     *
     * @return
     *     possible object is
     *     {@link SampleList }
     *
     */
    public SampleList getSampleList() {
        return sampleList;
    }

    /**
     * Sets the value of the sampleList property.
     *
     * @param value
     *     allowed object is
     *     {@link SampleList }
     *
     */
    public void setSampleList(SampleList value) {
        this.sampleList = value;
    }

    /**
     * Gets the value of the softwareList property.
     *
     * @return
     *     possible object is
     *     {@link SoftwareList }
     *
     */
    public SoftwareList getSoftwareList() {
        return softwareList;
    }

    /**
     * Sets the value of the softwareList property.
     *
     * @param value
     *     allowed object is
     *     {@link SoftwareList }
     *
     */
    public void setSoftwareList(SoftwareList value) {
        this.softwareList = value;
    }

    /**
     * Gets the value of the scanSettingsList property.
     *
     * @return
     *     possible object is
     *     {@link ScanSettingsList }
     *
     */
    public ScanSettingsList getScanSettingsList() {
        return scanSettingsList;
    }

    /**
     * Sets the value of the scanSettingsList property.
     *
     * @param value
     *     allowed object is
     *     {@link ScanSettingsList }
     *
     */
    public void setScanSettingsList(ScanSettingsList value) {
        this.scanSettingsList = value;
    }

    /**
     * Gets the value of the instrumentConfigurationList property.
     *
     * @return
     *     possible object is
     *     {@link InstrumentConfigurationList }
     *
     */
    public InstrumentConfigurationList getInstrumentConfigurationList() {
        return instrumentConfigurationList;
    }

    /**
     * Sets the value of the instrumentConfigurationList property.
     *
     * @param value
     *     allowed object is
     *     {@link InstrumentConfigurationList }
     *
     */
    public void setInstrumentConfigurationList(InstrumentConfigurationList value) {
        this.instrumentConfigurationList = value;
    }

    /**
     * Gets the value of the dataProcessingList property.
     *
     * @return
     *     possible object is
     *     {@link DataProcessingList }
     *
     */
    public DataProcessingList getDataProcessingList() {
        return dataProcessingList;
    }

    /**
     * Sets the value of the dataProcessingList property.
     *
     * @param value
     *     allowed object is
     *     {@link DataProcessingList }
     *
     */
    public void setDataProcessingList(DataProcessingList value) {
        this.dataProcessingList = value;
    }

    /**
     * Gets the value of the run property.
     *
     * @return
     *     possible object is
     *     {@link Run }
     *
     */
    public Run getRun() {
        return run;
    }

    /**
     * Sets the value of the run property.
     *
     * @param value
     *     allowed object is
     *     {@link Run }
     *
     */
    public void setRun(Run value) {
        this.run = value;
    }

    /**
     * Gets the value of the accession property.
     *
     * @return
     *     possible object is
     *     {@link String }
     *
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
     * Gets the value of the version property.
     *
     * @return possible object is
     *         {@link String }
     */
    public String getVersion() {
        return version;
    }

    /**
     * Sets the value of the version property.
     *
     * @param value allowed object is
     *              {@link String }
     */
    public void setVersion(String value) {
        this.version = value;
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
     * @param value allowed object is
     *              {@link String }
     */
    public void setId(String value) {
        this.id = value;
    }

}
