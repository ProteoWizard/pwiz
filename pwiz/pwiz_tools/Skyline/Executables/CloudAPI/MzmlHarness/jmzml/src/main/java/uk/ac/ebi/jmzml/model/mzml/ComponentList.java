package uk.ac.ebi.jmzml.model.mzml;

import javax.xml.bind.annotation.*;
import java.io.Serializable;
import java.util.ArrayList;
import java.util.List;


/**
 * List with the different components used in the mass spectrometer. At least one source, one mass analyzer and one detector need to be specified.
 * <p/>
 * <p>Java class for ComponentListType complex type.
 * <p/>
 * <p>The following schema fragment specifies the expected content contained within this class.
 * <p/>
 * <pre>
 * &lt;complexType name="ComponentListType">
 *   &lt;complexContent>
 *     &lt;restriction base="{http://www.w3.org/2001/XMLSchema}anyType">
 *       &lt;sequence>
 *         &lt;element name="source" type="{http://psi.hupo.org/ms/mzml}SourceComponentType" maxOccurs="unbounded"/>
 *         &lt;element name="analyzer" type="{http://psi.hupo.org/ms/mzml}AnalyzerComponentType" maxOccurs="unbounded"/>
 *         &lt;element name="detector" type="{http://psi.hupo.org/ms/mzml}DetectorComponentType" maxOccurs="unbounded"/>
 *       &lt;/sequence>
 *       &lt;attribute name="count" use="required" type="{http://www.w3.org/2001/XMLSchema}nonNegativeInteger" />
 *     &lt;/restriction>
 *   &lt;/complexContent>
 * &lt;/complexType>
 * </pre>
 */
@XmlAccessorType(XmlAccessType.FIELD)
@XmlType(name = "ComponentListType", propOrder = {
        "components"
})
public class ComponentList
        extends MzMLObject
        implements Serializable {

    private final static long serialVersionUID = 100L;
    @XmlElements({
            @XmlElement(name = "detector", required = true, type = DetectorComponent.class),
            @XmlElement(name = "analyzer", required = true, type = AnalyzerComponent.class),
            @XmlElement(name = "source", required = true, type = SourceComponent.class)
    })
    protected List<Component> components;
    @XmlAttribute(required = true)
    @XmlSchemaType(name = "nonNegativeInteger")
    protected Integer count;

    /**
     * Gets the value of the components property.
     * <p/>
     * <p/>
     * This accessor method returns a reference to the live list,
     * not a snapshot. Therefore any modification you make to the
     * returned list will be present inside the JAXB object.
     * This is why there is not a <CODE>set</CODE> method for the components property.
     * <p/>
     * <p/>
     * For example, to add a new item, do as follows:
     * <pre>
     *    getComponents().add(newItem);
     * </pre>
     * <p/>
     * <p/>
     * <p/>
     * Objects of the following type(s) are allowed in the list
     * {@link DetectorComponent }
     * {@link AnalyzerComponent }
     * {@link SourceComponent }
     */
    public List<Component> getComponents() {
        if (components == null) {
            components = new ArrayList<Component>();
        }
        return this.components;
    }

    /**
     * Gets the value of the count property.
     *
     * @return possible object is
     *         {@link Integer }
     */
    public Integer getCount() {
        return count;
    }

    /**
     * Sets the value of the count property.
     *
     * @param value allowed object is
     *              {@link Integer }
     */
    public void setCount(Integer value) {
        this.count = value;
    }

    ///// ///// ///// ///// ///// ///// ///// ///// ///// /////
    // manual additions to allow direct access to source/analyzer/detector components

    public List<SourceComponent> getSource() {
        List<SourceComponent> sources = new ArrayList<SourceComponent>();
        for (Component component : getComponents()) {
            if (component instanceof SourceComponent) {
                sources.add((SourceComponent) component);
            }
        }
        return sources;
    }

    public List<AnalyzerComponent> getAnalyzer() {
        List<AnalyzerComponent> analyzers = new ArrayList<AnalyzerComponent>();
        for (Component component : getComponents()) {
            if (component instanceof AnalyzerComponent) {
                analyzers.add((AnalyzerComponent) component);
            }
        }
        return analyzers;
    }

    public List<DetectorComponent> getDetector() {
        List<DetectorComponent> detectors = new ArrayList<DetectorComponent>();
        for (Component component : getComponents()) {
            if (component instanceof DetectorComponent) {
                detectors.add((DetectorComponent) component);
            }
        }
        return detectors;
    }

}
