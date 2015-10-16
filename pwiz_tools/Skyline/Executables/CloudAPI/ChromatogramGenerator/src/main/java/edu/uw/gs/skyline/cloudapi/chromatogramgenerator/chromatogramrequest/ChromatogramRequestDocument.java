
package edu.uw.gs.skyline.cloudapi.chromatogramgenerator.chromatogramrequest;

import java.util.ArrayList;
import java.util.List;
import javax.xml.bind.annotation.XmlAccessType;
import javax.xml.bind.annotation.XmlAccessorType;
import javax.xml.bind.annotation.XmlAttribute;
import javax.xml.bind.annotation.XmlElement;
import javax.xml.bind.annotation.XmlRootElement;
import javax.xml.bind.annotation.XmlType;


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
 *         &lt;element name="IsolationScheme">
 *           &lt;complexType>
 *             &lt;complexContent>
 *               &lt;restriction base="{http://www.w3.org/2001/XMLSchema}anyType">
 *                 &lt;sequence>
 *                   &lt;element name="IsolationWindow" maxOccurs="unbounded" minOccurs="0">
 *                     &lt;complexType>
 *                       &lt;complexContent>
 *                         &lt;restriction base="{http://www.w3.org/2001/XMLSchema}anyType">
 *                           &lt;attribute name="Start" use="required" type="{http://www.w3.org/2001/XMLSchema}double" />
 *                           &lt;attribute name="End" use="required" type="{http://www.w3.org/2001/XMLSchema}double" />
 *                           &lt;attribute name="Target" type="{http://www.w3.org/2001/XMLSchema}double" />
 *                           &lt;attribute name="StartMargin" type="{http://www.w3.org/2001/XMLSchema}double" />
 *                           &lt;attribute name="EndMargin" type="{http://www.w3.org/2001/XMLSchema}double" />
 *                         &lt;/restriction>
 *                       &lt;/complexContent>
 *                     &lt;/complexType>
 *                   &lt;/element>
 *                 &lt;/sequence>
 *                 &lt;attribute name="PrecursorFilter" type="{http://www.w3.org/2001/XMLSchema}double" />
 *                 &lt;attribute name="PrecursorRightFilter" type="{http://www.w3.org/2001/XMLSchema}double" />
 *                 &lt;attribute name="SpecialHandling" type="{http://www.w3.org/2001/XMLSchema}string" />
 *                 &lt;attribute name="WindowsPerScan" type="{http://www.w3.org/2001/XMLSchema}int" />
 *               &lt;/restriction>
 *             &lt;/complexContent>
 *           &lt;/complexType>
 *         &lt;/element>
 *         &lt;element name="ChromatogramGroup" maxOccurs="unbounded" minOccurs="0">
 *           &lt;complexType>
 *             &lt;complexContent>
 *               &lt;restriction base="{http://www.w3.org/2001/XMLSchema}anyType">
 *                 &lt;sequence>
 *                   &lt;element name="Chromatogram" maxOccurs="unbounded">
 *                     &lt;complexType>
 *                       &lt;complexContent>
 *                         &lt;restriction base="{http://www.w3.org/2001/XMLSchema}anyType">
 *                           &lt;attribute name="ProductMz" type="{http://www.w3.org/2001/XMLSchema}double" default="0" />
 *                           &lt;attribute name="MzWindow" type="{http://www.w3.org/2001/XMLSchema}double" default="0" />
 *                         &lt;/restriction>
 *                       &lt;/complexContent>
 *                     &lt;/complexType>
 *                   &lt;/element>
 *                 &lt;/sequence>
 *                 &lt;attribute name="PrecursorMz" type="{http://www.w3.org/2001/XMLSchema}double" default="0" />
 *                 &lt;attribute name="ModifiedSequence" use="required" type="{http://www.w3.org/2001/XMLSchema}string" />
 *                 &lt;attribute name="MinTime" type="{http://www.w3.org/2001/XMLSchema}double" />
 *                 &lt;attribute name="MaxTime" type="{http://www.w3.org/2001/XMLSchema}double" />
 *                 &lt;attribute name="Extractor" use="required" type="{}ChromExtractor" />
 *                 &lt;attribute name="Source" use="required" type="{}ChromSource" />
 *                 &lt;attribute name="MassErrors" type="{http://www.w3.org/2001/XMLSchema}boolean" default="false" />
 *                 &lt;attribute name="DriftTime" type="{http://www.w3.org/2001/XMLSchema}double" />
 *                 &lt;attribute name="DriftTimeWindow" type="{http://www.w3.org/2001/XMLSchema}double" />
 *               &lt;/restriction>
 *             &lt;/complexContent>
 *           &lt;/complexType>
 *         &lt;/element>
 *       &lt;/sequence>
 *       &lt;attribute name="MinMz" use="required" type="{http://www.w3.org/2001/XMLSchema}int" />
 *       &lt;attribute name="MaxMz" use="required" type="{http://www.w3.org/2001/XMLSchema}int" />
 *       &lt;attribute name="MzMatchTolerance" use="required" type="{http://www.w3.org/2001/XMLSchema}double" />
 *       &lt;attribute name="Ms2FullScanAcquisitionMethod" type="{}Ms2FullScanAcquisitionMethod" default="None" />
 *       &lt;attribute name="MinTime" type="{http://www.w3.org/2001/XMLSchema}double" />
 *       &lt;attribute name="MaxTime" type="{http://www.w3.org/2001/XMLSchema}double" />
 *     &lt;/restriction>
 *   &lt;/complexContent>
 * &lt;/complexType>
 * </pre>
 * 
 * 
 */
@XmlAccessorType(XmlAccessType.FIELD)
@XmlType(name = "", propOrder = {
    "isolationScheme",
    "chromatogramGroup"
})
@XmlRootElement(name = "ChromatogramRequestDocument")
public class ChromatogramRequestDocument {

    @XmlElement(name = "IsolationScheme", required = true)
    protected ChromatogramRequestDocument.IsolationScheme isolationScheme;
    @XmlElement(name = "ChromatogramGroup")
    protected List<ChromatogramRequestDocument.ChromatogramGroup> chromatogramGroup;
    @XmlAttribute(name = "MinMz", required = true)
    protected int minMz;
    @XmlAttribute(name = "MaxMz", required = true)
    protected int maxMz;
    @XmlAttribute(name = "MzMatchTolerance", required = true)
    protected double mzMatchTolerance;
    @XmlAttribute(name = "Ms2FullScanAcquisitionMethod")
    protected Ms2FullScanAcquisitionMethod ms2FullScanAcquisitionMethod;
    @XmlAttribute(name = "MinTime")
    protected Double minTime;
    @XmlAttribute(name = "MaxTime")
    protected Double maxTime;

    /**
     * Gets the value of the isolationScheme property.
     * 
     * @return
     *     possible object is
     *     {@link ChromatogramRequestDocument.IsolationScheme }
     *     
     */
    public ChromatogramRequestDocument.IsolationScheme getIsolationScheme() {
        return isolationScheme;
    }

    /**
     * Sets the value of the isolationScheme property.
     * 
     * @param value
     *     allowed object is
     *     {@link ChromatogramRequestDocument.IsolationScheme }
     *     
     */
    public void setIsolationScheme(ChromatogramRequestDocument.IsolationScheme value) {
        this.isolationScheme = value;
    }

    /**
     * Gets the value of the chromatogramGroup property.
     * 
     * <p>
     * This accessor method returns a reference to the live list,
     * not a snapshot. Therefore any modification you make to the
     * returned list will be present inside the JAXB object.
     * This is why there is not a <CODE>set</CODE> method for the chromatogramGroup property.
     * 
     * <p>
     * For example, to add a new item, do as follows:
     * <pre>
     *    getChromatogramGroup().add(newItem);
     * </pre>
     * 
     * 
     * <p>
     * Objects of the following type(s) are allowed in the list
     * {@link ChromatogramRequestDocument.ChromatogramGroup }
     * 
     * 
     */
    public List<ChromatogramRequestDocument.ChromatogramGroup> getChromatogramGroup() {
        if (chromatogramGroup == null) {
            chromatogramGroup = new ArrayList<ChromatogramRequestDocument.ChromatogramGroup>();
        }
        return this.chromatogramGroup;
    }

    /**
     * Gets the value of the minMz property.
     * 
     */
    public int getMinMz() {
        return minMz;
    }

    /**
     * Sets the value of the minMz property.
     * 
     */
    public void setMinMz(int value) {
        this.minMz = value;
    }

    /**
     * Gets the value of the maxMz property.
     * 
     */
    public int getMaxMz() {
        return maxMz;
    }

    /**
     * Sets the value of the maxMz property.
     * 
     */
    public void setMaxMz(int value) {
        this.maxMz = value;
    }

    /**
     * Gets the value of the mzMatchTolerance property.
     * 
     */
    public double getMzMatchTolerance() {
        return mzMatchTolerance;
    }

    /**
     * Sets the value of the mzMatchTolerance property.
     * 
     */
    public void setMzMatchTolerance(double value) {
        this.mzMatchTolerance = value;
    }

    /**
     * Gets the value of the ms2FullScanAcquisitionMethod property.
     * 
     * @return
     *     possible object is
     *     {@link Ms2FullScanAcquisitionMethod }
     *     
     */
    public Ms2FullScanAcquisitionMethod getMs2FullScanAcquisitionMethod() {
        if (ms2FullScanAcquisitionMethod == null) {
            return Ms2FullScanAcquisitionMethod.NONE;
        } else {
            return ms2FullScanAcquisitionMethod;
        }
    }

    /**
     * Sets the value of the ms2FullScanAcquisitionMethod property.
     * 
     * @param value
     *     allowed object is
     *     {@link Ms2FullScanAcquisitionMethod }
     *     
     */
    public void setMs2FullScanAcquisitionMethod(Ms2FullScanAcquisitionMethod value) {
        this.ms2FullScanAcquisitionMethod = value;
    }

    /**
     * Gets the value of the minTime property.
     * 
     * @return
     *     possible object is
     *     {@link Double }
     *     
     */
    public Double getMinTime() {
        return minTime;
    }

    /**
     * Sets the value of the minTime property.
     * 
     * @param value
     *     allowed object is
     *     {@link Double }
     *     
     */
    public void setMinTime(Double value) {
        this.minTime = value;
    }

    /**
     * Gets the value of the maxTime property.
     * 
     * @return
     *     possible object is
     *     {@link Double }
     *     
     */
    public Double getMaxTime() {
        return maxTime;
    }

    /**
     * Sets the value of the maxTime property.
     * 
     * @param value
     *     allowed object is
     *     {@link Double }
     *     
     */
    public void setMaxTime(Double value) {
        this.maxTime = value;
    }


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
     *         &lt;element name="Chromatogram" maxOccurs="unbounded">
     *           &lt;complexType>
     *             &lt;complexContent>
     *               &lt;restriction base="{http://www.w3.org/2001/XMLSchema}anyType">
     *                 &lt;attribute name="ProductMz" type="{http://www.w3.org/2001/XMLSchema}double" default="0" />
     *                 &lt;attribute name="MzWindow" type="{http://www.w3.org/2001/XMLSchema}double" default="0" />
     *               &lt;/restriction>
     *             &lt;/complexContent>
     *           &lt;/complexType>
     *         &lt;/element>
     *       &lt;/sequence>
     *       &lt;attribute name="PrecursorMz" type="{http://www.w3.org/2001/XMLSchema}double" default="0" />
     *       &lt;attribute name="ModifiedSequence" use="required" type="{http://www.w3.org/2001/XMLSchema}string" />
     *       &lt;attribute name="MinTime" type="{http://www.w3.org/2001/XMLSchema}double" />
     *       &lt;attribute name="MaxTime" type="{http://www.w3.org/2001/XMLSchema}double" />
     *       &lt;attribute name="Extractor" use="required" type="{}ChromExtractor" />
     *       &lt;attribute name="Source" use="required" type="{}ChromSource" />
     *       &lt;attribute name="MassErrors" type="{http://www.w3.org/2001/XMLSchema}boolean" default="false" />
     *       &lt;attribute name="DriftTime" type="{http://www.w3.org/2001/XMLSchema}double" />
     *       &lt;attribute name="DriftTimeWindow" type="{http://www.w3.org/2001/XMLSchema}double" />
     *     &lt;/restriction>
     *   &lt;/complexContent>
     * &lt;/complexType>
     * </pre>
     * 
     * 
     */
    @XmlAccessorType(XmlAccessType.FIELD)
    @XmlType(name = "", propOrder = {
        "chromatogram"
    })
    public static class ChromatogramGroup {

        @XmlElement(name = "Chromatogram", required = true)
        protected List<ChromatogramRequestDocument.ChromatogramGroup.Chromatogram> chromatogram;
        @XmlAttribute(name = "PrecursorMz")
        protected Double precursorMz;
        @XmlAttribute(name = "ModifiedSequence", required = true)
        protected String modifiedSequence;
        @XmlAttribute(name = "MinTime")
        protected Double minTime;
        @XmlAttribute(name = "MaxTime")
        protected Double maxTime;
        @XmlAttribute(name = "Extractor", required = true)
        protected ChromExtractor extractor;
        @XmlAttribute(name = "Source", required = true)
        protected ChromSource source;
        @XmlAttribute(name = "MassErrors")
        protected Boolean massErrors;
        @XmlAttribute(name = "DriftTime")
        protected Double driftTime;
        @XmlAttribute(name = "DriftTimeWindow")
        protected Double driftTimeWindow;

        /**
         * Gets the value of the chromatogram property.
         * 
         * <p>
         * This accessor method returns a reference to the live list,
         * not a snapshot. Therefore any modification you make to the
         * returned list will be present inside the JAXB object.
         * This is why there is not a <CODE>set</CODE> method for the chromatogram property.
         * 
         * <p>
         * For example, to add a new item, do as follows:
         * <pre>
         *    getChromatogram().add(newItem);
         * </pre>
         * 
         * 
         * <p>
         * Objects of the following type(s) are allowed in the list
         * {@link ChromatogramRequestDocument.ChromatogramGroup.Chromatogram }
         * 
         * 
         */
        public List<ChromatogramRequestDocument.ChromatogramGroup.Chromatogram> getChromatogram() {
            if (chromatogram == null) {
                chromatogram = new ArrayList<ChromatogramRequestDocument.ChromatogramGroup.Chromatogram>();
            }
            return this.chromatogram;
        }

        /**
         * Gets the value of the precursorMz property.
         * 
         * @return
         *     possible object is
         *     {@link Double }
         *     
         */
        public double getPrecursorMz() {
            if (precursorMz == null) {
                return  0.0D;
            } else {
                return precursorMz;
            }
        }

        /**
         * Sets the value of the precursorMz property.
         * 
         * @param value
         *     allowed object is
         *     {@link Double }
         *     
         */
        public void setPrecursorMz(Double value) {
            this.precursorMz = value;
        }

        /**
         * Gets the value of the modifiedSequence property.
         * 
         * @return
         *     possible object is
         *     {@link String }
         *     
         */
        public String getModifiedSequence() {
            return modifiedSequence;
        }

        /**
         * Sets the value of the modifiedSequence property.
         * 
         * @param value
         *     allowed object is
         *     {@link String }
         *     
         */
        public void setModifiedSequence(String value) {
            this.modifiedSequence = value;
        }

        /**
         * Gets the value of the minTime property.
         * 
         * @return
         *     possible object is
         *     {@link Double }
         *     
         */
        public Double getMinTime() {
            return minTime;
        }

        /**
         * Sets the value of the minTime property.
         * 
         * @param value
         *     allowed object is
         *     {@link Double }
         *     
         */
        public void setMinTime(Double value) {
            this.minTime = value;
        }

        /**
         * Gets the value of the maxTime property.
         * 
         * @return
         *     possible object is
         *     {@link Double }
         *     
         */
        public Double getMaxTime() {
            return maxTime;
        }

        /**
         * Sets the value of the maxTime property.
         * 
         * @param value
         *     allowed object is
         *     {@link Double }
         *     
         */
        public void setMaxTime(Double value) {
            this.maxTime = value;
        }

        /**
         * Gets the value of the extractor property.
         * 
         * @return
         *     possible object is
         *     {@link ChromExtractor }
         *     
         */
        public ChromExtractor getExtractor() {
            return extractor;
        }

        /**
         * Sets the value of the extractor property.
         * 
         * @param value
         *     allowed object is
         *     {@link ChromExtractor }
         *     
         */
        public void setExtractor(ChromExtractor value) {
            this.extractor = value;
        }

        /**
         * Gets the value of the source property.
         * 
         * @return
         *     possible object is
         *     {@link ChromSource }
         *     
         */
        public ChromSource getSource() {
            return source;
        }

        /**
         * Sets the value of the source property.
         * 
         * @param value
         *     allowed object is
         *     {@link ChromSource }
         *     
         */
        public void setSource(ChromSource value) {
            this.source = value;
        }

        /**
         * Gets the value of the massErrors property.
         * 
         * @return
         *     possible object is
         *     {@link Boolean }
         *     
         */
        public boolean isMassErrors() {
            if (massErrors == null) {
                return false;
            } else {
                return massErrors;
            }
        }

        /**
         * Sets the value of the massErrors property.
         * 
         * @param value
         *     allowed object is
         *     {@link Boolean }
         *     
         */
        public void setMassErrors(Boolean value) {
            this.massErrors = value;
        }

        /**
         * Gets the value of the driftTime property.
         * 
         * @return
         *     possible object is
         *     {@link Double }
         *     
         */
        public Double getDriftTime() {
            return driftTime;
        }

        /**
         * Sets the value of the driftTime property.
         * 
         * @param value
         *     allowed object is
         *     {@link Double }
         *     
         */
        public void setDriftTime(Double value) {
            this.driftTime = value;
        }

        /**
         * Gets the value of the driftTimeWindow property.
         * 
         * @return
         *     possible object is
         *     {@link Double }
         *     
         */
        public Double getDriftTimeWindow() {
            return driftTimeWindow;
        }

        /**
         * Sets the value of the driftTimeWindow property.
         * 
         * @param value
         *     allowed object is
         *     {@link Double }
         *     
         */
        public void setDriftTimeWindow(Double value) {
            this.driftTimeWindow = value;
        }


        /**
         * <p>Java class for anonymous complex type.
         * 
         * <p>The following schema fragment specifies the expected content contained within this class.
         * 
         * <pre>
         * &lt;complexType>
         *   &lt;complexContent>
         *     &lt;restriction base="{http://www.w3.org/2001/XMLSchema}anyType">
         *       &lt;attribute name="ProductMz" type="{http://www.w3.org/2001/XMLSchema}double" default="0" />
         *       &lt;attribute name="MzWindow" type="{http://www.w3.org/2001/XMLSchema}double" default="0" />
         *     &lt;/restriction>
         *   &lt;/complexContent>
         * &lt;/complexType>
         * </pre>
         * 
         * 
         */
        @XmlAccessorType(XmlAccessType.FIELD)
        @XmlType(name = "")
        public static class Chromatogram {

            @XmlAttribute(name = "ProductMz")
            protected Double productMz;
            @XmlAttribute(name = "MzWindow")
            protected Double mzWindow;

            /**
             * Gets the value of the productMz property.
             * 
             * @return
             *     possible object is
             *     {@link Double }
             *     
             */
            public double getProductMz() {
                if (productMz == null) {
                    return  0.0D;
                } else {
                    return productMz;
                }
            }

            /**
             * Sets the value of the productMz property.
             * 
             * @param value
             *     allowed object is
             *     {@link Double }
             *     
             */
            public void setProductMz(Double value) {
                this.productMz = value;
            }

            /**
             * Gets the value of the mzWindow property.
             * 
             * @return
             *     possible object is
             *     {@link Double }
             *     
             */
            public double getMzWindow() {
                if (mzWindow == null) {
                    return  0.0D;
                } else {
                    return mzWindow;
                }
            }

            /**
             * Sets the value of the mzWindow property.
             * 
             * @param value
             *     allowed object is
             *     {@link Double }
             *     
             */
            public void setMzWindow(Double value) {
                this.mzWindow = value;
            }

        }

    }


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
     *         &lt;element name="IsolationWindow" maxOccurs="unbounded" minOccurs="0">
     *           &lt;complexType>
     *             &lt;complexContent>
     *               &lt;restriction base="{http://www.w3.org/2001/XMLSchema}anyType">
     *                 &lt;attribute name="Start" use="required" type="{http://www.w3.org/2001/XMLSchema}double" />
     *                 &lt;attribute name="End" use="required" type="{http://www.w3.org/2001/XMLSchema}double" />
     *                 &lt;attribute name="Target" type="{http://www.w3.org/2001/XMLSchema}double" />
     *                 &lt;attribute name="StartMargin" type="{http://www.w3.org/2001/XMLSchema}double" />
     *                 &lt;attribute name="EndMargin" type="{http://www.w3.org/2001/XMLSchema}double" />
     *               &lt;/restriction>
     *             &lt;/complexContent>
     *           &lt;/complexType>
     *         &lt;/element>
     *       &lt;/sequence>
     *       &lt;attribute name="PrecursorFilter" type="{http://www.w3.org/2001/XMLSchema}double" />
     *       &lt;attribute name="PrecursorRightFilter" type="{http://www.w3.org/2001/XMLSchema}double" />
     *       &lt;attribute name="SpecialHandling" type="{http://www.w3.org/2001/XMLSchema}string" />
     *       &lt;attribute name="WindowsPerScan" type="{http://www.w3.org/2001/XMLSchema}int" />
     *     &lt;/restriction>
     *   &lt;/complexContent>
     * &lt;/complexType>
     * </pre>
     * 
     * 
     */
    @XmlAccessorType(XmlAccessType.FIELD)
    @XmlType(name = "", propOrder = {
        "isolationWindow"
    })
    public static class IsolationScheme {

        @XmlElement(name = "IsolationWindow")
        protected List<ChromatogramRequestDocument.IsolationScheme.IsolationWindow> isolationWindow;
        @XmlAttribute(name = "PrecursorFilter")
        protected Double precursorFilter;
        @XmlAttribute(name = "PrecursorRightFilter")
        protected Double precursorRightFilter;
        @XmlAttribute(name = "SpecialHandling")
        protected String specialHandling;
        @XmlAttribute(name = "WindowsPerScan")
        protected Integer windowsPerScan;

        /**
         * Gets the value of the isolationWindow property.
         * 
         * <p>
         * This accessor method returns a reference to the live list,
         * not a snapshot. Therefore any modification you make to the
         * returned list will be present inside the JAXB object.
         * This is why there is not a <CODE>set</CODE> method for the isolationWindow property.
         * 
         * <p>
         * For example, to add a new item, do as follows:
         * <pre>
         *    getIsolationWindow().add(newItem);
         * </pre>
         * 
         * 
         * <p>
         * Objects of the following type(s) are allowed in the list
         * {@link ChromatogramRequestDocument.IsolationScheme.IsolationWindow }
         * 
         * 
         */
        public List<ChromatogramRequestDocument.IsolationScheme.IsolationWindow> getIsolationWindow() {
            if (isolationWindow == null) {
                isolationWindow = new ArrayList<ChromatogramRequestDocument.IsolationScheme.IsolationWindow>();
            }
            return this.isolationWindow;
        }

        /**
         * Gets the value of the precursorFilter property.
         * 
         * @return
         *     possible object is
         *     {@link Double }
         *     
         */
        public Double getPrecursorFilter() {
            return precursorFilter;
        }

        /**
         * Sets the value of the precursorFilter property.
         * 
         * @param value
         *     allowed object is
         *     {@link Double }
         *     
         */
        public void setPrecursorFilter(Double value) {
            this.precursorFilter = value;
        }

        /**
         * Gets the value of the precursorRightFilter property.
         * 
         * @return
         *     possible object is
         *     {@link Double }
         *     
         */
        public Double getPrecursorRightFilter() {
            return precursorRightFilter;
        }

        /**
         * Sets the value of the precursorRightFilter property.
         * 
         * @param value
         *     allowed object is
         *     {@link Double }
         *     
         */
        public void setPrecursorRightFilter(Double value) {
            this.precursorRightFilter = value;
        }

        /**
         * Gets the value of the specialHandling property.
         * 
         * @return
         *     possible object is
         *     {@link String }
         *     
         */
        public String getSpecialHandling() {
            return specialHandling;
        }

        /**
         * Sets the value of the specialHandling property.
         * 
         * @param value
         *     allowed object is
         *     {@link String }
         *     
         */
        public void setSpecialHandling(String value) {
            this.specialHandling = value;
        }

        /**
         * Gets the value of the windowsPerScan property.
         * 
         * @return
         *     possible object is
         *     {@link Integer }
         *     
         */
        public Integer getWindowsPerScan() {
            return windowsPerScan;
        }

        /**
         * Sets the value of the windowsPerScan property.
         * 
         * @param value
         *     allowed object is
         *     {@link Integer }
         *     
         */
        public void setWindowsPerScan(Integer value) {
            this.windowsPerScan = value;
        }


        /**
         * <p>Java class for anonymous complex type.
         * 
         * <p>The following schema fragment specifies the expected content contained within this class.
         * 
         * <pre>
         * &lt;complexType>
         *   &lt;complexContent>
         *     &lt;restriction base="{http://www.w3.org/2001/XMLSchema}anyType">
         *       &lt;attribute name="Start" use="required" type="{http://www.w3.org/2001/XMLSchema}double" />
         *       &lt;attribute name="End" use="required" type="{http://www.w3.org/2001/XMLSchema}double" />
         *       &lt;attribute name="Target" type="{http://www.w3.org/2001/XMLSchema}double" />
         *       &lt;attribute name="StartMargin" type="{http://www.w3.org/2001/XMLSchema}double" />
         *       &lt;attribute name="EndMargin" type="{http://www.w3.org/2001/XMLSchema}double" />
         *     &lt;/restriction>
         *   &lt;/complexContent>
         * &lt;/complexType>
         * </pre>
         * 
         * 
         */
        @XmlAccessorType(XmlAccessType.FIELD)
        @XmlType(name = "")
        public static class IsolationWindow {

            @XmlAttribute(name = "Start", required = true)
            protected double start;
            @XmlAttribute(name = "End", required = true)
            protected double end;
            @XmlAttribute(name = "Target")
            protected Double target;
            @XmlAttribute(name = "StartMargin")
            protected Double startMargin;
            @XmlAttribute(name = "EndMargin")
            protected Double endMargin;

            /**
             * Gets the value of the start property.
             * 
             */
            public double getStart() {
                return start;
            }

            /**
             * Sets the value of the start property.
             * 
             */
            public void setStart(double value) {
                this.start = value;
            }

            /**
             * Gets the value of the end property.
             * 
             */
            public double getEnd() {
                return end;
            }

            /**
             * Sets the value of the end property.
             * 
             */
            public void setEnd(double value) {
                this.end = value;
            }

            /**
             * Gets the value of the target property.
             * 
             * @return
             *     possible object is
             *     {@link Double }
             *     
             */
            public Double getTarget() {
                return target;
            }

            /**
             * Sets the value of the target property.
             * 
             * @param value
             *     allowed object is
             *     {@link Double }
             *     
             */
            public void setTarget(Double value) {
                this.target = value;
            }

            /**
             * Gets the value of the startMargin property.
             * 
             * @return
             *     possible object is
             *     {@link Double }
             *     
             */
            public Double getStartMargin() {
                return startMargin;
            }

            /**
             * Sets the value of the startMargin property.
             * 
             * @param value
             *     allowed object is
             *     {@link Double }
             *     
             */
            public void setStartMargin(Double value) {
                this.startMargin = value;
            }

            /**
             * Gets the value of the endMargin property.
             * 
             * @return
             *     possible object is
             *     {@link Double }
             *     
             */
            public Double getEndMargin() {
                return endMargin;
            }

            /**
             * Sets the value of the endMargin property.
             * 
             * @param value
             *     allowed object is
             *     {@link Double }
             *     
             */
            public void setEndMargin(Double value) {
                this.endMargin = value;
            }

        }

    }

}
