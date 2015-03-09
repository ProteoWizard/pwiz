
package uk.ac.ebi.jmzml.model.mzml;

import javax.xml.bind.annotation.XmlAccessType;
import javax.xml.bind.annotation.XmlAccessorType;
import javax.xml.bind.annotation.XmlType;
import java.io.Serializable;


/**
 * This element must be used to describe a Detector Component Type. This is a PRIDE3-specific
 *                 modification of the core MzML schema that does not have any impact on the base schema validation.
 *             
 * 
 * <p>Java class for DetectorComponentType complex type.
 * 
 * <p>The following schema fragment specifies the expected content contained within this class.
 * 
 * <pre>
 * &lt;complexType name="DetectorComponentType">
 *   &lt;complexContent>
 *     &lt;extension base="{http://psi.hupo.org/ms/mzml}ComponentType">
 *     &lt;/extension>
 *   &lt;/complexContent>
 * &lt;/complexType>
 * </pre>
 * 
 * 
 */
@XmlAccessorType(XmlAccessType.FIELD)
@XmlType(name = "DetectorComponentType")
public class DetectorComponent
    extends Component
    implements Serializable
{

    private final static long serialVersionUID = 100L;

}
