/*
 * To change this license header, choose License Headers in Project Properties.
 * To change this template file, choose Tools | Templates
 * and open the template in the editor.
 */
package uk.ac.ebi.jmzml.model.mzml.utilities;

import uk.ac.ebi.jmzml.model.mzml.params.BinaryDataArrayCVParam;

/**
 *
 * @author SPerkins
 */
public class CommonCvParams {

    public static final BinaryDataArrayCVParam MZ_PARAM = initMZParam();
    public static final BinaryDataArrayCVParam INTENSITY_PARAM = initIntensityParam();
    public static final BinaryDataArrayCVParam ZLIB_PARAM = initZlibParam();
    public static final BinaryDataArrayCVParam NO_COMPRESSION_PARAM = initNoCompressionParam();   
    public static final BinaryDataArrayCVParam ENCODING_32_BIT_INT_PARAM = init32BitIntEncodingParam();
    public static final BinaryDataArrayCVParam MSNUMPRESS_LINEAR_COMPRESSION_PARAM = initMSNumpressLinearCompressionParam();
    public static final BinaryDataArrayCVParam MSNUMPRESS_SLOF_COMPRESSION_PARAM = initMSNumpressSlofCompressionParam();
    public static final BinaryDataArrayCVParam MSNUMPRESS_PIC_COMPRESSION_PARAM = initMSNumpressPicCompressionParam();
    public static final BinaryDataArrayCVParam TIME_PARAM = initTimeParam();

    /**
     * Private constructor to prevent instantiation.
     */
    private CommonCvParams() {

    }

    private static BinaryDataArrayCVParam initMZParam() {
        BinaryDataArrayCVParam param = new BinaryDataArrayCVParam();
        param.setUnitCvRef("MS");
        param.setUnitName("m/z");
        param.setUnitAccession("MS:1000040");
        param.setName("m/z array");
        param.setAccession("MS:1000514");
        param.setCvRef("MS");
        return param;
    }

    private static BinaryDataArrayCVParam initIntensityParam() {
        BinaryDataArrayCVParam param = new BinaryDataArrayCVParam();
        param.setUnitCvRef("MS");
        param.setUnitName("number of counts");
        param.setAccession("MS:1000131");
        param.setName("intensity array");
        param.setAccession("MS:1000515");
        param.setCvRef("MS");
        return param;
    }

    private static BinaryDataArrayCVParam initZlibParam() {
        BinaryDataArrayCVParam param = new BinaryDataArrayCVParam();
        param.setAccession("MS:1000574");
        param.setName("zlib compression");
        return param;
    }
    
    private static BinaryDataArrayCVParam initNoCompressionParam() {
        BinaryDataArrayCVParam param = new BinaryDataArrayCVParam();
        param.setAccession("MS:1000576");
        param.setName("no compression");
        return param;
    }   
    
    private static BinaryDataArrayCVParam init32BitIntEncodingParam() {
    	BinaryDataArrayCVParam param = new BinaryDataArrayCVParam();
    	param.setAccession("MS:1000519");
    	param.setName("32-bit integer");
    	return param;
    }
    
    private static BinaryDataArrayCVParam initMSNumpressLinearCompressionParam() {
        BinaryDataArrayCVParam param = new BinaryDataArrayCVParam();
        param.setAccession("MS:1002312");
        param.setName("MS-Numpress linear prediction compression");
        return param;
    }
    
    private static BinaryDataArrayCVParam initMSNumpressSlofCompressionParam() {
        BinaryDataArrayCVParam param = new BinaryDataArrayCVParam();
        param.setAccession("MS:1002314");
        param.setName("MS-Numpress short logged float compression");
        return param;
    }
    
    private static BinaryDataArrayCVParam initMSNumpressPicCompressionParam() {
        BinaryDataArrayCVParam param = new BinaryDataArrayCVParam();
        param.setAccession("MS:1002313");
        param.setName("MS-Numpress positive integer compression");
        return param;
    }
    
    private static BinaryDataArrayCVParam initTimeParam() {
        BinaryDataArrayCVParam param = new BinaryDataArrayCVParam();
        param.setAccession("MS:1000595");
        param.setName("time array");
        param.setCvRef("MS");
        param.setUnitCvRef("UO");
        param.setUnitAccession("UO:0000031");
        param.setUnitName("minute");
        return param;
    }
}
