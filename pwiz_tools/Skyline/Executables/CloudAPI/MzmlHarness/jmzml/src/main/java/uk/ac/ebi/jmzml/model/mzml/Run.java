package uk.ac.ebi.jmzml.model.mzml;

import uk.ac.ebi.jmzml.xml.jaxb.adapters.CalendarAdapter;
import uk.ac.ebi.jmzml.xml.jaxb.adapters.IdRefAdapter;

import javax.xml.bind.annotation.*;
import javax.xml.bind.annotation.adapters.CollapsedStringAdapter;
import javax.xml.bind.annotation.adapters.XmlJavaTypeAdapter;
import java.io.Serializable;
import java.util.Calendar;


/**
 * A run in mzML should correspond to a single, consecutive and coherent set of scans on an instrument.
 * <p/>
 * <p>Java class for RunType complex type.
 * <p/>
 * <p>The following schema fragment specifies the expected content contained within this class.
 * <p/>
 * <pre>
 * &lt;complexType name="RunType">
 *   &lt;complexContent>
 *     &lt;extension base="{http://psi.hupo.org/ms/mzml}ParamGroupType">
 *       &lt;sequence>
 *         &lt;element name="spectrumList" type="{http://psi.hupo.org/ms/mzml}SpectrumListType" minOccurs="0"/>
 *         &lt;element name="chromatogramList" type="{http://psi.hupo.org/ms/mzml}ChromatogramListType" minOccurs="0"/>
 *       &lt;/sequence>
 *       &lt;attribute name="id" use="required" type="{http://www.w3.org/2001/XMLSchema}ID" />
 *       &lt;attribute name="defaultInstrumentConfigurationRef" use="required" type="{http://www.w3.org/2001/XMLSchema}IDREF" />
 *       &lt;attribute name="defaultSourceFileRef" type="{http://www.w3.org/2001/XMLSchema}IDREF" />
 *       &lt;attribute name="sampleRef" type="{http://www.w3.org/2001/XMLSchema}IDREF" />
 *       &lt;attribute name="startTimeStamp" type="{http://www.w3.org/2001/XMLSchema}dateTime" />
 *     &lt;/extension>
 *   &lt;/complexContent>
 * &lt;/complexType>
 * </pre>
 */
@XmlAccessorType(XmlAccessType.FIELD)
@XmlType(name = "RunType", propOrder = {
        "spectrumList",
        "chromatogramList"
})
public class Run
        extends ParamGroup
        implements Serializable {

    private final static long serialVersionUID = 100L;
    protected SpectrumList spectrumList;
    protected ChromatogramList chromatogramList;
    @XmlAttribute(required = true)
    @XmlJavaTypeAdapter(CollapsedStringAdapter.class)
    @XmlID
    @XmlSchemaType(name = "ID")
    protected String id;
    @XmlAttribute(required = true)
    @XmlJavaTypeAdapter(IdRefAdapter.class)
    @XmlSchemaType(name = "IDREF")
    protected String defaultInstrumentConfigurationRef;
    @XmlTransient
    private InstrumentConfiguration defaultInstrumentConfiguration;
    @XmlAttribute
    @XmlJavaTypeAdapter(IdRefAdapter.class)
    @XmlSchemaType(name = "IDREF")
    protected String defaultSourceFileRef;
    @XmlTransient
    private SourceFile defaultSourceFile;
    @XmlAttribute
    @XmlJavaTypeAdapter(IdRefAdapter.class)
    @XmlSchemaType(name = "IDREF")
    protected String sampleRef;
    @XmlTransient
    private Sample sample;
    @XmlAttribute
    @XmlJavaTypeAdapter(CalendarAdapter.class)
    @XmlSchemaType(name = "dateTime")
    protected Calendar startTimeStamp;

    /**
     * Gets the value of the spectrumList property.
     *
     * @return possible object is
     *         {@link SpectrumList }
     */
    public SpectrumList getSpectrumList() {
        return spectrumList;
    }

    /**
     * Sets the value of the spectrumList property.
     *
     * @param value allowed object is
     *              {@link SpectrumList }
     */
    public void setSpectrumList(SpectrumList value) {
        this.spectrumList = value;
    }

    /**
     * Gets the value of the chromatogramList property.
     *
     * @return possible object is
     *         {@link ChromatogramList }
     */
    public ChromatogramList getChromatogramList() {
        return chromatogramList;
    }

    /**
     * Sets the value of the chromatogramList property.
     *
     * @param value allowed object is
     *              {@link ChromatogramList }
     */
    public void setChromatogramList(ChromatogramList value) {
        this.chromatogramList = value;
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
     * Gets the value of the defaultInstrumentConfigurationRef property.
     *
     * @return possible object is
     *         {@link String }
     */
    public String getDefaultInstrumentConfigurationRef() {
        return defaultInstrumentConfigurationRef;
    }

    /**
     * Sets the value of the defaultInstrumentConfigurationRef property.
     *
     * @param value allowed object is
     *              {@link String }
     */
    public void setDefaultInstrumentConfigurationRef(String value) {
        this.defaultInstrumentConfigurationRef = value;
    }

    public InstrumentConfiguration getDefaultInstrumentConfiguration() {
        return defaultInstrumentConfiguration;
    }

    public void setDefaultInstrumentConfiguration(InstrumentConfiguration instrumentConfiguration) {
        this.defaultInstrumentConfiguration = instrumentConfiguration;
        if (instrumentConfiguration != null) {
            this.defaultInstrumentConfigurationRef = instrumentConfiguration.getId();
        }
    }

    /**
     * Gets the value of the defaultSourceFileRef property.
     *
     * @return possible object is
     *         {@link String }
     */
    public String getDefaultSourceFileRef() {
        return defaultSourceFileRef;
    }

    /**
     * Sets the value of the defaultSourceFileRef property.
     *
     * @param value allowed object is
     *              {@link String }
     */
    public void setDefaultSourceFileRef(String value) {
        this.defaultSourceFileRef = value;
    }

    public SourceFile getDefaultSourceFile(){
        return defaultSourceFile;
    }

    public void setDefaultSourceFile(SourceFile sourceFile){
        this.defaultSourceFile = sourceFile;
        if(sourceFile != null){
            this.defaultSourceFileRef = sourceFile.getId();
        }
    }

    /**
     * Gets the value of the sampleRef property.
     *
     * @return possible object is
     *         {@link String }
     */
    public String getSampleRef() {
        return sampleRef;
    }

    /**
     * Sets the value of the sampleRef property.
     *
     * @param value allowed object is
     *              {@link String }
     */
    public void setSampleRef(String value) {
        this.sampleRef = value;
    }

    public Sample getSample(){
        return sample;
    }

    public void setSample(Sample sample){
        this.sample = sample;
        if (sample != null){
            this.sampleRef = sample.getId();
        }
    }

    /**
     * Gets the value of the startTimeStamp property.
     *
     * @return possible object is
     *         {@link String }
     */
    public Calendar getStartTimeStamp() {
        return startTimeStamp;
    }

    /**
     * Sets the value of the startTimeStamp property.
     *
     * @param value allowed object is
     *              {@link String }
     */
    public void setStartTimeStamp(Calendar value) {
        this.startTimeStamp = value;
    }

}
