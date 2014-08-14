
package uk.ac.ebi.jmzml.model.mzml;

import org.apache.log4j.Logger;

import javax.xml.bind.annotation.*;
import java.io.Serializable;
import java.util.ArrayList;
import java.util.List;


/**
 * Structure allowing the use of a controlled (cvParam) or uncontrolled vocabulary (userParam), or a reference to a predefined set of these in this mzML file (paramGroupRef).
 * 
 * <p>Java class for ParamGroupType complex type.
 * 
 * <p>The following schema fragment specifies the expected content contained within this class.
 * 
 * <pre>
 * &lt;complexType name="ParamGroupType">
 *   &lt;complexContent>
 *     &lt;restriction base="{http://www.w3.org/2001/XMLSchema}anyType">
 *       &lt;sequence>
 *         &lt;element name="referenceableParamGroupRef" type="{http://psi.hupo.org/ms/mzml}ReferenceableParamGroupRefType" maxOccurs="unbounded" minOccurs="0"/>
 *         &lt;element name="cvParam" type="{http://psi.hupo.org/ms/mzml}CVParamType" maxOccurs="unbounded" minOccurs="0"/>
 *         &lt;element name="userParam" type="{http://psi.hupo.org/ms/mzml}UserParamType" maxOccurs="unbounded" minOccurs="0"/>
 *       &lt;/sequence>
 *     &lt;/restriction>
 *   &lt;/complexContent>
 * &lt;/complexType>
 * </pre>
 * 
 * 
 */
@XmlAccessorType(XmlAccessType.FIELD)
@XmlType(name = "ParamGroupType", propOrder = {
    "referenceableParamGroupRef",
    "cvParam",
    "userParam"
})
@XmlSeeAlso({
    Scan.class,
    ProcessingMethod.class,
    Run.class,
    Software.class,
    SourceFile.class,
    InstrumentConfiguration.class,
    BinaryDataArray.class,
    Sample.class,
    Spectrum.class,
    Component.class,
    ScanSettings.class,
    Chromatogram.class,
    ScanList.class
})
public class ParamGroup
    extends MzMLObject
    implements Serializable
{

    private static final Logger logger = Logger.getLogger(ParamGroup.class);

    private final static long serialVersionUID = 100L;
    protected List<ReferenceableParamGroupRef> referenceableParamGroupRef;
    protected List<CVParam> cvParam;
    protected List<UserParam> userParam;

    @XmlTransient
    private List<CVParam> cvParamsSkippedDuringMarshalling = new ArrayList<CVParam>();
    @XmlTransient
    private List<UserParam> userParamsSkippedDuringMarshalling = new ArrayList<UserParam>();


    /**
     * Gets the value of the referenceableParamGroupRef property.
     * 
     * <p>
     * This accessor method returns a reference to the live list,
     * not a snapshot. Therefore any modification you make to the
     * returned list will be present inside the JAXB object.
     * This is why there is not a <CODE>set</CODE> method for the referenceableParamGroupRef property.
     * 
     * <p>
     * For example, to add a new item, do as follows:
     * <pre>
     *    getReferenceableParamGroupRef().add(newItem);
     * </pre>
     * 
     * 
     * <p>
     * Objects of the following type(s) are allowed in the list
     * {@link ReferenceableParamGroupRef }
     * 
     * 
     */
    public List<ReferenceableParamGroupRef> getReferenceableParamGroupRef() {
        if (referenceableParamGroupRef == null) {
            referenceableParamGroupRef = new ArrayList<ReferenceableParamGroupRef>();
        }
        return this.referenceableParamGroupRef;
    }

    /**
     * Gets the value of the cvParam property.
     * 
     * <p>
     * This accessor method returns a reference to the live list,
     * not a snapshot. Therefore any modification you make to the
     * returned list will be present inside the JAXB object.
     * This is why there is not a <CODE>set</CODE> method for the cvParam property.
     * 
     * <p>
     * For example, to add a new item, do as follows:
     * <pre>
     *    getCvParam().add(newItem);
     * </pre>
     * 
     * 
     * <p>
     * Objects of the following type(s) are allowed in the list
     * {@link CVParam }
     * 
     * 
     */
    public List<CVParam> getCvParam() {
        if (cvParam == null) {
            cvParam = new ArrayList<CVParam>();
        }
        return this.cvParam;
    }

    /**
     * Gets the value of the userParam property.
     * 
     * <p>
     * This accessor method returns a reference to the live list,
     * not a snapshot. Therefore any modification you make to the
     * returned list will be present inside the JAXB object.
     * This is why there is not a <CODE>set</CODE> method for the userParam property.
     * 
     * <p>
     * For example, to add a new item, do as follows:
     * <pre>
     *    getUserParam().add(newItem);
     * </pre>
     * 
     * 
     * <p>
     * Objects of the following type(s) are allowed in the list
     * {@link UserParam }
     * 
     * 
     */
    public List<UserParam> getUserParam() {
        if (userParam == null) {
            userParam = new ArrayList<UserParam>();
        }
        return this.userParam;
    }

    public boolean beforeMarshalOperation() {
        List<CVParam> cvParams = this.getCvParam();
        List<CVParam> tempCV = new ArrayList<CVParam>();
        for (CVParam cvParam : cvParams) {
            if (cvParam.isInferredFromReferenceableParamGroupRef()) {
                cvParamsSkippedDuringMarshalling.add(cvParam);
                logger.debug("Skipping cvParam " + cvParam);
            } else {
                tempCV.add(cvParam);
            }
        }

        // Replace original list of cvParams with only those that were not inferred
        // (i.e., our temp list).
        this.cvParam = tempCV;

        List<UserParam> userParams = this.getUserParam();
        List<UserParam> tempUser = new ArrayList<UserParam>();
        for (UserParam userParam : userParams) {
            if (userParam.isInferredFromReferenceableParamGroupRef()) {
                userParamsSkippedDuringMarshalling.add(userParam);
                logger.debug("Skipping cvParam " + userParam);
            } else {
                tempUser.add(userParam);
            }
        }

        // Replace original list of cvParams with only those that were not inferred
        // (i.e., our temp list).
        this.userParam = tempUser;

        return true;
    }

    public void afterMarshalOperation() {
        // Reset the thing in its original state.
        cvParam.addAll(cvParamsSkippedDuringMarshalling);
        logger.debug("Re-inserting " + cvParamsSkippedDuringMarshalling.size() + " referenceable CV params into the main cv param list after marshalling.");
        userParam.addAll(userParamsSkippedDuringMarshalling);
        logger.debug("Re-inserting " + userParamsSkippedDuringMarshalling.size() + " referenceable user params into the main user param list after marshalling.");
        // Reset the skipped stuff, as it should now be empty again.
        cvParamsSkippedDuringMarshalling.clear();
        userParamsSkippedDuringMarshalling.clear();
    }

}
