/*
 * Date: 13/12/2013 
 * File: uk.ac.ebi.jmzml.model.mzml.BinaryDataArray
 *
 * jmzml is Copyright 2008 The European Bioinformatics Institute
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 *
 *
 */

package uk.ac.ebi.jmzml.model.mzml;

import uk.ac.ebi.jmzml.model.mzml.params.BinaryDataArrayCVParam;
import uk.ac.ebi.jmzml.xml.jaxb.adapters.IdRefAdapter;

import javax.xml.bind.annotation.*;
import javax.xml.bind.annotation.adapters.XmlJavaTypeAdapter;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.Serializable;
import java.io.UnsupportedEncodingException;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.util.ArrayList;
import java.util.List;
import java.util.zip.DataFormatException;
import java.util.zip.Deflater;
import java.util.zip.Inflater;
import uk.ac.ebi.jmzml.model.mzml.utilities.CommonCvParams;
import uk.ac.ebi.jmzml.model.mzml.utilities.MSNumpressCodec;


/**
 * The structure into which encoded binary data goes. Byte ordering is always little endian (Intel style). Computers using a different endian style must convert to/from little endian when writing/reading mzML
 * <p/>
 * <p>Java class for BinaryDataArrayType complex type.
 * <p/>
 * <p>The following schema fragment specifies the expected content contained within this class.
 * <p/>
 * <pre>
 * &lt;complexType name="BinaryDataArrayType">
 *   &lt;complexContent>
 *     &lt;extension base="{http://psi.hupo.org/ms/mzml}ParamGroupType">
 *       &lt;sequence>
 *         &lt;element name="binary" type="{http://www.w3.org/2001/XMLSchema}base64Binary"/>
 *       &lt;/sequence>
 *       &lt;attribute name="arrayLength" type="{http://www.w3.org/2001/XMLSchema}nonNegativeInteger" />
 *       &lt;attribute name="dataProcessingRef" type="{http://www.w3.org/2001/XMLSchema}IDREF" />
 *       &lt;attribute name="encodedLength" use="required" type="{http://www.w3.org/2001/XMLSchema}nonNegativeInteger" />
 *     &lt;/extension>
 *   &lt;/complexContent>
 * &lt;/complexType>
 * </pre>
 */
@XmlAccessorType(XmlAccessType.FIELD)
@XmlType(name = "BinaryDataArrayType", propOrder = {
        "binary"
})
public class BinaryDataArray
        extends ParamGroup
        implements Serializable {

    /**
     * Defines the number of bytes required in an UNENCODED byte array to hold
     * a single double value.
     */
    public static final int BYTES_64_PRECISION = 8;

    /**
     * Defines the number of bytes required in an UNENCODED byte array to hold
     * a single float value.
     */
    public static final int BYTES_32_PRECISION = 4;

    // PSI-MS controlled vocabulary terms and accessions
    public static final String MS_INTENSITY_AC = "MS:1000515";
    public static final String MS_INTENSITY_NAME = "intensity array";
    public static final String MS_MZ_VALUE_AC = "MS:1000514";
    public static final String MS_MZ_VALUE_NAME = "m/z array";
    public static final String MS_COMPRESSED_AC = "MS:1000574";
    public static final String MS_COMPRESSED_NAME = "zlib compression";
    public static final String MS_UNCOMPRESSED_AC = "MS:1000576";
    public static final String MS_UNCOMPRESSED_NAME = "no compression";    
    public static final String MS_FLOAT32BIT_AC = "MS:1000521";
    public static final String MS_FLOAT32BIT_NAME = "32-bit float";
    public static final String MS_FLOAT64BIT_AC = "MS:1000523";
    public static final String MS_FLOAT64BIT_NAME = "64-bit float";
    public static final String MS_INT32BIT_AC = "MS:1000519";
    public static final String MS_INT32BIT_NAME = "32-bit integer";
    public static final String MS_INT64BIT_AC = "MS:1000522";
    public static final String MS_INT64BIT_NAME = "64-bit integer";
    public static final String MS_NTSTRING_AC = "MS:1001479";
    public static final String MS_NTSTRING_NAME = "null-terminated ASCII string";

    /**
     * Enumeration defining the allowed precision cases for the binary data
     * as defined in the mzML specifications and the PSI-MS ontology.
     */
    public enum Precision {
        /**
         * Corresponds to the PSI-MS ontology term "MS:1000521" / "32-bit float"
         * and binary data will be represented in the Java primitive: float
         */
        FLOAT32BIT,

        /**
         * Corresponds to the PSI-MS ontology term "MS:1000523" / "64-bit float"
         * and binary data will be represented in the Java primitive: double
         */
        FLOAT64BIT,

        /**
         * Corresponds to the PSI-MS ontology term "MS:1000519" / "32-bit integer"
         * and binary data will be represented in the Java primitive: int
         */
        INT32BIT,

        /**
         * Corresponds to the PSI-MS ontology term "MS:1000522" / "64-bit integer"
         * and binary data will be represented in the Java primitive: long
         */
        INT64BIT,

        /**
         * Corresponds to the PSI-MS ontology term "MS:1001479" / "null-terminated ASCII string"
         * and binary data will be represented in the Java type: String
         */
        NTSTRING
    }

    /**
     * Enumeration defining the data types that can/should be present in
     * the binary data arrays.
     */
    public enum DataType {
        /**
         * Based on the constant MS_INTENSITY_AC to define the intensities related to the m/z values of a spectrum.
         */
        INTENSITY,

        /**
         * Based on the constant MS_MZ_VALUE_AC to define the m/z values of a spectrum.
         */
        MZ_VALUES,

        /**
         * Used if no other DataType could be determined.
         * Possible reasons: other data encoded, other vocabulary terms used, data not present, ...
         */
        UNKNOWN

    }

    private final static long serialVersionUID = 100L;
    @XmlElement(required = true)
    protected byte[] binary;
    @XmlAttribute
    @XmlSchemaType(name = "nonNegativeInteger")
    protected Integer arrayLength;
    @XmlAttribute
    @XmlJavaTypeAdapter(IdRefAdapter.class)
    @XmlSchemaType(name = "IDREF")
    protected String dataProcessingRef;

    @XmlTransient
    private DataProcessing dataProcessing;

    @XmlAttribute(required = true)
    @XmlSchemaType(name = "nonNegativeInteger")
    protected Integer encodedLength;


    /**
     * Gets the value of the binary property.
     *
     * @return possible object is
     *         byte[]
     */
    public byte[] getBinary() {
        return binary;
    }

    /**
     * Sets the value of the binary property.
     *
     * @param value allowed object is
     *              byte[]
     */
    public void setBinary(byte[] value) {
        this.binary = ((byte[]) value);
    }

    /**
     * Gets the value of the arrayLength property.
     *
     * @return possible object is
     *         {@link Integer }
     */
    public Integer getArrayLength() {
        return arrayLength;
    }

    /**
     * Sets the value of the arrayLength property.
     *
     * @param value allowed object is
     *              {@link Integer }
     */
    public void setArrayLength(Integer value) {
        this.arrayLength = value;
    }

    /**
     * Gets the value of the dataProcessingRef property.
     *
     * @return possible object is
     *         {@link String }
     */
    public String getDataProcessingRef() {
        return dataProcessingRef;
    }

    /**
     * Sets the value of the dataProcessingRef property.
     *
     * @param value allowed object is
     *              {@link String }
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


    ///// ///// ///// ///// ///// ///// ///// ///// ///// /////
    // adjusted Getter/Setter and public convenience methods

    /**
     * Retrieve the binary data as array of numeric values.
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


    /**
     * Gets the value of the encodedLength property.
     *
     * @return possible object is
     *         {@link Integer }
     */
    public Integer getEncodedLength() {
        return encodedLength;
    }

    /**
     * Sets the value of the encodedLength property.
     *
     * @param value allowed object is
     *              {@link Integer }
     */
    public void setEncodedLength(Integer value) {
        this.encodedLength = value;
    }

    ///// ///// ///// ///// ///// ///// ///// ///// ///// /////
    // adjusted Getter/Setter and public convenience methods

    /**
     * Retrieve the binary data as array of numeric values.
     * The type (double, float, long, int) of the values is
     * determined by the according CVParam of this BinaryDataArray.
     * The CVParams will also be used to determine if the
     * data first needs uncompressing.
     *
     * @return a Number array representation of the binary data.
     */
    public Number[] getBinaryDataAsNumberArray() {

        // 1. decode the base64 encoded data (data is assumed to always be base64 encoded in the XML)
        // already done, by JAXB unmarshaller

        // 2. Decompression of the data (if required)
        byte[] data;
        if (needsUncompressing()) { // if CVParam states the data is compressed
            data = decompress(binary);
        } else {
            data = binary;
        }
        
        Number[] dataArray = null;
        
        // 3a. if data has been numpress compressed then do the decompression...
        String numpressAccession = null;
        if ((numpressAccession = MSNumpressCodec.getMSNumpressEncodingAccession(this.cvParam)) != null) {
            dataArray = MSNumpressCodec.decode(numpressAccession, data);
            this.arrayLength = dataArray.length;            
            return dataArray;
        }

        // 3b. ...if not then apply the specified precision when converting into numeric values        
        switch (getPrecision()) {
            case FLOAT64BIT:
                dataArray = convertData(data, Precision.FLOAT64BIT);
                break;
            case FLOAT32BIT:
                dataArray = convertData(data, Precision.FLOAT32BIT);
                break;
            case INT64BIT:
                dataArray = convertData(data, Precision.INT64BIT);
                break;
            case INT32BIT:
                dataArray = convertData(data, Precision.INT32BIT);
                break;
            case NTSTRING:
                throw new IllegalArgumentException("Precision " + Precision.NTSTRING + " is not supported in this method!");
            default:
                throw new IllegalStateException("Not supported Precision in BinaryDataArray: " + getPrecision());
        }

        // return the result
        return dataArray;
    }

    /**
     * Converts the binary data representing the "null-terminated ASCII string"
     * into a Java String.
     * The method to use if the attached CVParam defines the binary data
     * as "null-terminated ASCII string".
     *
     * @return the String constructed from the binary data.
     * @throws UnsupportedEncodingException if the expected encoding (ASCII) is not supported.
     * @throws IllegalStateException        if the method is used on binary data and the accompanying
     *                                      CVParams state that the data does not represent a "null-terminated ASCII string".
     * @see #getPrecision()
     */
    public String getBinaryDataAsString() throws UnsupportedEncodingException {
        // check if we have the right binary data
        if (getPrecision() != Precision.NTSTRING) {
            throw new IllegalStateException("This method has to be used with data " +
                    "according to Precision " + Precision.NTSTRING + "!");
        }

        // 1. decode the base64 encoded data (data is assumed to always be base64 encoded in the XML)
        // already done, by JAXB unmarshaller

        // 2. Decompression of the data (if required)
        byte[] data;
        if (needsUncompressing()) { // if CVParam states the data is compressed
            data = decompress(binary);
        } else {
            data = binary;
        }

        // 3. convert the binary data into a String
        // since we are dealing with a "null terminated string" as defined
        // in the mzML specification, we have to first get rid of the null
        // byte before we can convert the data into a Java String.
        byte[] stringData = new byte[data.length - 1]; // one byte less than data
        System.arraycopy(data, 0, stringData, 0, stringData.length);

        return new String(stringData, "ASCII");
    }

    /**
     * Sets the value of the binary property for data in double values.
     * Note that double values imply a precision of 64 bit.
     *
     * @param value    the data as double array.
     * @param compress flag whether or not the data should be compressed.
     * @param cv       The CV that will be used as reference CV for the generated
     *                 compression and precision CVParams.
     * @return an int value specifying the length of the byte[] that was stored as binary data.
     */
    public int set64BitFloatArrayAsBinaryData(double[] value, boolean compress, CV cv) {
        int dataLength;
        ByteBuffer buffer = ByteBuffer.allocate(value.length * BYTES_64_PRECISION);
        buffer.order(ByteOrder.LITTLE_ENDIAN);
        for (double aDoubleArray : value) {
            buffer.putDouble(aDoubleArray);
        }
        byte[] data = buffer.array();
        dataLength = data.length;
        setBinaryData(data, compress, cv);

        // add a cv parameter stating that the data uses 64 bit float (double) precision
        CVParam cvParam = new BinaryDataArrayCVParam();
        cvParam.setAccession(MS_FLOAT64BIT_AC);
        cvParam.setName(MS_FLOAT64BIT_NAME);
        cvParam.setCv(cv);
        this.getCvParam().add(cvParam);

        return dataLength;
    }
    
    /**
     * Sets the value of the binary property for data in double values.
     * Note that double values imply a precision of 64 bit.
     * @param value the data as double array.
     * @param compress flag whether or not the data should be compressed (zlib).
     * @param cv The CV that will be used as reference CV for the generated compression and CVParams.
     * @param numpressParam The CVParam detailing the MSNumpress compression algorithm to use.
     * @return an int value specifying the length of the byte[] that was stored as binary data.
     */
    public int set64BitFloatArrayAsBinaryData(double[] value, boolean compress, CV cv, CVParam numpressParam) {
        byte[] numpressEncodedBytes = MSNumpressCodec.encode(value, numpressParam.getAccession());
        int dataLength = numpressEncodedBytes.length;
        setBinaryData(numpressEncodedBytes, compress, cv);
        numpressParam.setCv(cv);
        this.getCvParam().add(numpressParam);
        
        CVParam cvParam = new BinaryDataArrayCVParam();
        cvParam.setAccession(MS_FLOAT64BIT_AC);
        cvParam.setName(MS_FLOAT64BIT_NAME);
        cvParam.setCv(cv);
        this.getCvParam().add(cvParam);
        
        this.arrayLength = value.length;
        
        return dataLength;
    }

    /**
     * Sets the value of the binary property for data in float values.
     * Note that float values imply a precision of 32 bit.
     *
     * @param value    the data as float array.
     * @param compress flag whether or not the data should be compressed.
     * @param cv       The CV that will be used as reference CV for the generated
     *                 compression and precision CVParams.
     * @return an int value specifying the length of the byte[] that was stored as binary data.
     */
    public int set32BitFloatArrayAsBinaryData(float[] value, boolean compress, CV cv) {
        int dataLength;
        ByteBuffer buffer = ByteBuffer.allocate(value.length * BYTES_32_PRECISION);
        buffer.order(ByteOrder.LITTLE_ENDIAN);
        for (float aFloatArray : value) {
            buffer.putFloat(aFloatArray);
        }
        byte[] data = buffer.array();
        dataLength = data.length;
        setBinaryData(data, compress, cv);

        // add a cv parameter stating that the data uses 32-bit float precision
        CVParam cvParam = new BinaryDataArrayCVParam();
        cvParam.setAccession(MS_FLOAT32BIT_AC);
        cvParam.setName(MS_FLOAT32BIT_NAME);
        cvParam.setCv(cv);
        this.getCvParam().add(cvParam);

        return dataLength;
    }
    
    /**
     * Sets the value of the binary property for data in float values.
     * Note that float values imply a precision of 32 bit, the data stored will be 64 bit doubles.
     * @param value the data as float array.
     * @param compress flag whether or not the data should be compressed (zlib).
     * @param cv The CV that will be used as reference CV for the generated compression and CVParams.
     * @param numpressParam The CVParam detailing the MSNumpress compression algorithm to use.
     * @return an int value specifying the length of the byte[] that was stored as binary data.
     */
    public int set32BitFloatArrayAsBinaryData(float[] value, boolean compress, CV cv, CVParam numpressParam) {
        return set64BitFloatArrayAsBinaryData(convertFloatToDoubleArray(value), compress, cv, numpressParam);
    }

    /**
     * Sets the value of the binary property for data in int values.
     * Note that int values imply a precision of 32 bit.
     *
     * @param array    the data as int array.
     * @param compress flag whether or not the data should be compressed.
     * @param cv       The CV that will be used as reference CV for the generated
     *                 compression and precision CVParams.
     * @return an int value specifying the length of the byte[] that was stored as binary data.
     */
    public int set32BitIntArrayAsBinaryData(int[] array, boolean compress, CV cv) {
        int dataLength;
        ByteBuffer buffer = ByteBuffer.allocate(array.length * BYTES_32_PRECISION);
        buffer.order(ByteOrder.LITTLE_ENDIAN);
        for (int aIntValue : array) {
            buffer.putInt(aIntValue);
        }
        byte[] data = buffer.array();
        dataLength = data.length;
        setBinaryData(data, compress, cv);

        // add a cv parameter stating that the data uses 32-bit integer precision
        CVParam cvParam = new BinaryDataArrayCVParam();
        cvParam.setAccession(MS_INT32BIT_AC);
        cvParam.setName(MS_INT32BIT_NAME);
        cvParam.setCv(cv);
        this.getCvParam().add(cvParam);

        return dataLength;
    }

    /**
     * Sets the value of the binary property for data in int values.
     * Note that int values imply a precision of 32 bit, the data will stored will be 64 bit doubles.
     * @param value the data as int array.
     * @param compress flag whether or not the data should be compressed (zlib).
     * @param cv The CV that will be used as reference CV for the generated compression and CVParams.
     * @param numpressParam The CVParam detailing the MSNumpress compression algorithm to use.
     * @return an int value specifying the length of the byte[] that was stored as binary data.
     */
    public int set32BitIntArrayAsBinaryData(int[] array, boolean compress, CV cv, CVParam numpressParam) {
        return set64BitFloatArrayAsBinaryData(convertIntToDoubleArray(array), compress, cv, numpressParam);
    }
    
    /**
     * Sets the value of the binary property for data in long values.
     * Note that long values imply a precision of 64 bit.
     *
     * @param array    the data as long array.
     * @param compress flag whether or not the data should be compressed.
     * @param cv       The CV that will be used as reference CV for the generated
     *                 compression and precision CVParams.
     * @return an int value specifying the length of the byte[] that was stored as binary data.
     */
    public int set64BitIntArrayAsBinaryData(long[] array, boolean compress, CV cv) {
        int dataLength;
        ByteBuffer buffer = ByteBuffer.allocate(array.length * BYTES_64_PRECISION);
        buffer.order(ByteOrder.LITTLE_ENDIAN);
        for (long aIntValue : array) {
            buffer.putLong(aIntValue);
        }
        byte[] data = buffer.array();
        dataLength = data.length;
        setBinaryData(data, compress, cv);

        // add a cv parameter stating that the data uses 64-bit integer precision
        CVParam cvParam = new BinaryDataArrayCVParam();
        cvParam.setAccession(MS_INT64BIT_AC);
        cvParam.setName(MS_INT64BIT_NAME);
        cvParam.setCv(cv);
        this.getCvParam().add(cvParam);

        return dataLength;
    }

    /**
     * Sets the value of the binary property for data in long values.
     * Note that long values imply a precision of 64 bit, the data will stored will be 64 bit doubles.
     * @param value the data as long array.
     * @param compress flag whether or not the data should be compressed (zlib).
     * @param cv The CV that will be used as reference CV for the generated compression and CVParams.
     * @param numpressParam The CVParam detailing the MSNumpress compression algorithm to use.
     * @return an int value specifying the length of the byte[] that was stored as binary data.
     */
    public int set64BitIntArrayAsBinaryData(long[] array, boolean compress, CV cv, CVParam numpressParam) {
        return set64BitFloatArrayAsBinaryData(convertLongToDoubleArray(array), compress, cv, numpressParam);
    }
    
    /**
     * Sets the value of the binary property for data represented as String.
     * Note: since Java does not have the concept of "null terminated strings",
     * this will add a null byte to comply with the mzML specifications.
     *
     * @param value    the String value of the binary data.
     * @param compress flag whether or not the data should be compressed.
     * @param cv       The CV that will be used as reference CV for the generated
     *                 compression and precision CVParams.
     * @return an int value specifying the length of the byte[] that was stored as binary data.
     * @throws UnsupportedEncodingException if the encoding (ASCII) used
     *                                      by the String methods is not supported.
     */
    public int setStringAsBinaryData(String value, boolean compress, CV cv) throws UnsupportedEncodingException {
        int dataLength;
        // get the byte array of the String and add a null byte
        byte[] tmp = value.getBytes("ASCII");
        byte[] data = new byte[tmp.length + 1];
        System.arraycopy(tmp, 0, data, 0, tmp.length);
        data[data.length - 1] = 0; // add a null byte as last byte
        dataLength = data.length;
        setBinaryData(data, compress, cv);

        // add a cv parameter stating that the data uses a string representation
        CVParam cvParam = new BinaryDataArrayCVParam();
        cvParam.setAccession(MS_NTSTRING_AC);
        cvParam.setName(MS_NTSTRING_NAME);
        cvParam.setCv(cv);
        this.getCvParam().add(cvParam);

        return dataLength;
    }

    /**
     * Sets the value of the binary property for data in Number values.
     * Since Number can hold all other number data, the precision has
     * to be specified.
     *
     * @param array    the Number array holding the data.
     * @param p        the Precision defining the data format.
     * @param compress flag whether or not the data should be compressed.
     * @param cv       The CV that will be used as reference CV for the generated
     *                 compression and precision CVParams.
     * @return an int value specifying the length of the byte[] that was stored as binary data.
     * @see #set32BitFloatArrayAsBinaryData(float[], boolean, CV)
     * @see #set64BitFloatArrayAsBinaryData(double[], boolean, CV)
     * @see #set32BitIntArrayAsBinaryData(int[], boolean, CV)
     * @see #set64BitIntArrayAsBinaryData(long[], boolean, CV)
     */
    public int setNumberArrayAsBinaryData(Number[] array, Precision p, boolean compress, CV cv) {
        int size;
        switch (p) {
            case FLOAT32BIT:
                size = set32BitFloatArrayAsBinaryData(convertNumberToFloatArray(array), compress, cv);
                break;
            case FLOAT64BIT:
                size = set64BitFloatArrayAsBinaryData(convertNumberToDoubleArray(array), compress, cv);
                break;
            case INT32BIT:
                size = set32BitIntArrayAsBinaryData(convertNumberToIntArray(array), compress, cv);
                break;
            case INT64BIT:
                size = set64BitIntArrayAsBinaryData(convertNumberToLongArray(array), compress, cv);
                break;
            case NTSTRING:
                throw new IllegalArgumentException("Precision " + Precision.NTSTRING + " is not supported in this method!");
            default:
                throw new IllegalStateException("Not supported Precision in BinaryDataArray: " + p);
        }
        return size;
    }

    /**
     * Retrieve the precision from the CVParams of this BinaryDataArray
     * and report the found precision as BinaryDataArray.Precision.
     *
     * @return the Precision defined by the CVParams accompanying this BinaryDataArray.
     */
    public Precision getPrecision() {
        Precision p;

        // first get all registered CV parameters
        List<String> cvs2 = new ArrayList<String>();
        for (CVParam param : this.getCvParam()) {
            cvs2.add(param.getAccession());
        }

        // then check if we have 64 or 32 bit precision
        if (cvs2.contains(MS_FLOAT64BIT_AC)) {
            p = Precision.FLOAT64BIT;
        } else if (cvs2.contains(MS_FLOAT32BIT_AC)) {
            p = Precision.FLOAT32BIT;
        } else if (cvs2.contains(MS_INT64BIT_AC)) {
            p = Precision.INT64BIT;
        } else if (cvs2.contains(MS_INT32BIT_AC)) {
            p = Precision.INT32BIT;
        } else if (cvs2.contains(MS_NTSTRING_AC)) {
            p = Precision.NTSTRING;
        } else {
            throw new IllegalStateException("Required precision CV parameter ('" + MS_FLOAT64BIT_NAME
                    + "' or '" + MS_FLOAT32BIT_NAME + "' or '" + MS_INT64BIT_NAME + "' or '"
                    + MS_INT32BIT_NAME + "' or '" + MS_NTSTRING_NAME + "') not found in BinaryDataArray!");
        }

        return p;
    }

    /**
     * Reads the CVParams of this BinaryDataArray object and reports true if
     * the according CVParam was found stating that the binary data is compressed.
     *
     * @return true if the attached CVParams contain a parameter stating
     *         that the data is compressed. False is returned if the
     *         CVParam for uncompressed data is found.
     * @throws IllegalStateException if none of the expected CVParams were found.
     */
    public boolean needsUncompressing() {
        boolean uncompress;

        // first get all registered CV parameters
        List<String> cvs = new ArrayList<String>();
        for (CVParam param : this.getCvParam()) {
            cvs.add(param.getAccession());
        }

        // now check if we have compressed or uncompressed data
        if (cvs.contains(MS_COMPRESSED_AC)) {
            uncompress = true;
        } else if (cvs.contains(MS_UNCOMPRESSED_AC)) {
            uncompress = false;
        } else if (cvs.contains(CommonCvParams.MSNUMPRESS_LINEAR_COMPRESSION_PARAM.getAccession())) {    
            uncompress = false;
        } else if (cvs.contains(CommonCvParams.MSNUMPRESS_SLOF_COMPRESSION_PARAM.getAccession())) {
            uncompress = false;
        } else if (cvs.contains(CommonCvParams.MSNUMPRESS_PIC_COMPRESSION_PARAM.getAccession())) {
            uncompress = false;        
        } else {
            throw new IllegalStateException("Required compression CV parameter not found in BinaryDataArray!");
        }

        return uncompress;
    }

    /**
     * Reads the List of CVParams of this BinaryDataArray and tries to determine the type of data encoded within it.
     * @return the DataType that corresponds to the data in the binary data array.
     */
    public DataType getDataType() {
        // for all registered CV parameters
        for (CVParam param : this.getCvParam()) {
            // find the param that defines what the data represents
            if (param.getAccession().equals(MS_MZ_VALUE_AC)) {
                return DataType.MZ_VALUES;
            } else if (param.getAccession().equals(MS_INTENSITY_AC)) {
                return DataType.INTENSITY;
            } // else go on to the next cv param
        }
        // if we haven't found a recognised DataType yet, we can't say what the data represents
        return DataType.UNKNOWN;
    }

    ///// ///// ///// ///// ///// ///// ///// ///// ///// /////
    // private helper methods

    private Number[] convertData(byte[] data, Precision prec) {
        int step;
        switch (prec) {
            case FLOAT64BIT: // fall through
            case INT64BIT:
                step = 8;
                break;
            case FLOAT32BIT: // fall through
            case INT32BIT:
                step = 4;
                break;
            default:
                step = -1;
        }
        // create a Number array of sufficient size
        Number[] resultArray = new Number[data.length / step];
        // create a buffer around the data array for easier retrieval
        ByteBuffer bb = ByteBuffer.wrap(data);
        bb.order(ByteOrder.LITTLE_ENDIAN); // the order is always LITTLE_ENDIAN
        // progress in steps of 4/8 bytes according to the set step
        for (int indexOut = 0; indexOut < data.length; indexOut += step) {
            // Note that the 'getFloat(index)' and getInt(index) methods read the next 4 bytes
            // and the 'getDouble(index)' and getLong(index) methods read the next 8 bytes.
            Number num;
            switch (prec) {
                case FLOAT64BIT:
                    num = bb.getDouble(indexOut);
                    break;
                case INT64BIT:
                    num = bb.getLong(indexOut);
                    break;
                case FLOAT32BIT:
                    num = bb.getFloat(indexOut);
                    break;
                case INT32BIT:
                    num = bb.getInt(indexOut);
                    break;
                default:
                    num = null;
            }
            resultArray[indexOut / step] = num;
        }
        return resultArray;
    }

    private byte[] decompress(byte[] compressedData) {
        byte[] decompressedData;

        // using a ByteArrayOutputStream to not having to define the result array size beforehand
        Inflater decompressor = new Inflater();

        decompressor.setInput(compressedData);
        // Create an expandable byte array to hold the decompressed data
        ByteArrayOutputStream bos = new ByteArrayOutputStream(compressedData.length);
        byte[] buf = new byte[1024];
        while (!decompressor.finished()) {
            try {
                int count = decompressor.inflate(buf);
                if (count == 0 && decompressor.needsInput()) {
                    break;
                }
                bos.write(buf, 0, count);
            } catch (DataFormatException e) {
                throw new IllegalStateException("Encountered wrong data format " +
                        "while trying to decompress binary data!", e);
            }
        }
        try {
            bos.close();
        } catch (IOException e) {
            // ToDo: add logging
            e.printStackTrace();
        }
        // Get the decompressed data
        decompressedData = bos.toByteArray();

        if (decompressedData == null) {
            throw new IllegalStateException("Decompression of binary data produced no result (null)!");
        }
        return decompressedData;
    }

    /**
     * Compressed source data using the Deflate algorithm.
     * @param uncompressedData Data to be compressed
     * @return Compressed data
     */
    private byte[] compress(byte[] uncompressedData) {
        byte[] data = null; // Decompress the data

        // create a temporary byte array big enough to hold the compressed data
        // with the worst compression (the length of the initial (uncompressed) data)
        // EDIT: if it turns out this byte array was not big enough, then double its size and try again.
        byte[] temp = new byte[uncompressedData.length / 2];
        int compressedBytes = temp.length;
        while (compressedBytes == temp.length) {
            // compress
            temp = new byte[temp.length * 2];
            Deflater compresser = new Deflater();
            compresser.setInput(uncompressedData);
            compresser.finish();
            compressedBytes = compresser.deflate(temp);
        }      
        
        // create a new array with the size of the compressed data (compressedBytes)        
        data = new byte[compressedBytes];
        System.arraycopy(temp, 0, data, 0, compressedBytes);

        return data;
    }

    private double[] convertNumberToDoubleArray(Number[] array) {
        double[] result = new double[array.length];
        for (int i = 0; i < array.length; i++) {
            result[i] = array[i].doubleValue();
        }
        return result;
    }

    private float[] convertNumberToFloatArray(Number[] array) {
        float[] result = new float[array.length];
        for (int i = 0; i < array.length; i++) {
            result[i] = array[i].floatValue();
        }
        return result;
    }

    private int[] convertNumberToIntArray(Number[] array) {
        int[] result = new int[array.length];
        for (int i = 0; i < array.length; i++) {
            result[i] = array[i].intValue();
        }
        return result;
    }

    private long[] convertNumberToLongArray(Number[] array) {
        long[] result = new long[array.length];
        for (int i = 0; i < array.length; i++) {
            result[i] = array[i].longValue();
        }
        return result;
    }
    
    private double[] convertFloatToDoubleArray(final float[] array) {
        double[] result = new double[array.length];
        for (int i = 0; i < array.length; i++) {
            result[i] = array[i];
        }
        return result;
    }
    
    private double[] convertIntToDoubleArray(final int[] array) {
        double[] result = new double[array.length];
        for (int i = 0; i < array.length; i++) {
            result[i] = array[i];
        }
        return result;
    }
    
    private double[] convertLongToDoubleArray(final long[] array) {
        double[] result = new double[array.length];
        for (int i = 0; i < array.length; i++) {
            result[i] = array[i];
        }
        return result;
    }

    private void setBinaryData(byte[] input, boolean compress, CV cv) {
        byte[] output;
        if (compress) {
            // data needs compressing
            output = compress(input);
            // add a cv parameter stating that the data was compressed
            CVParam cvParam = new BinaryDataArrayCVParam();
            cvParam.setAccession(MS_COMPRESSED_AC);
            cvParam.setName(MS_COMPRESSED_NAME);
            cvParam.setCv(cv);
            this.getCvParam().add(cvParam);
        } else {
            // the data will not be compressed
            output = input;
            // add a cv parameter stating that the data was not compressed
            CVParam cvParam = new BinaryDataArrayCVParam();
            cvParam.setAccession(MS_UNCOMPRESSED_AC);
            cvParam.setName(MS_UNCOMPRESSED_NAME);
            cvParam.setCv(cv);
            this.getCvParam().add(cvParam);
        }

        // store the binary data
        // Note: no base64 encoding needed, since the
        // JAXB marshaller will take care of that
        setBinary(output);
    }
}

