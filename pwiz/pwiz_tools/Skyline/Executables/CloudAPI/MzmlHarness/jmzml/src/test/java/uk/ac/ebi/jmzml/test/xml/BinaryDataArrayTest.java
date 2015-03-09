package uk.ac.ebi.jmzml.test.xml;

import junit.framework.TestCase;
import org.apache.commons.codec.binary.Base64;
import uk.ac.ebi.jmzml.model.mzml.*;
import uk.ac.ebi.jmzml.model.mzml.params.BinaryDataArrayCVParam;
import uk.ac.ebi.jmzml.xml.io.MzMLMarshaller;
import uk.ac.ebi.jmzml.xml.io.MzMLUnmarshaller;
import uk.ac.ebi.jmzml.xml.io.MzMLUnmarshallerException;

import java.io.ByteArrayOutputStream;
import java.io.UnsupportedEncodingException;
import java.net.URL;
import java.util.Arrays;
import java.util.Iterator;
import java.util.List;

/**
 * @author Florian Reisinger
 *         Date: 11-Jun-2009
 * @since 0.5
 */
public class BinaryDataArrayTest extends TestCase {

    // ToDo: test binary data for integer and String cases

    private final boolean VERBOSE = false;

    private CV cv;
    private CVParam prec64bit;
    private CVParam prec32bit;
    private CVParam compressed;
    private CVParam uncompressed;

    ///// ///// ///// ///// ///// ///// ///// ///// ///// /////
    // test values (like the ones in the example XML file) in
    // different numeric types

    private double[] testData64bitFloat = {
            0.000, 0.001, 0.002, 0.003, 0.004, 0.005, 0.006, 0.007, 0.008, 0.009,
            0.010, 0.011, 0.012, 0.013, 0.014, 0.015, 0.016, 0.017, 0.018, 0.019,
            0.020, 0.021, 0.022, 0.023, 0.024, 0.025, 0.026, 0.027, 0.028, 0.029,
            0.030, 0.031, 0.032, 0.033, 0.034, 0.035, 0.036, 0.037, 0.038, 0.039,
            0.040, 0.041, 0.042, 0.043, 0.044, 0.045, 0.046, 0.047, 0.048, 0.049,
            0.050, 0.051, 0.052, 0.053, 0.054, 0.055, 0.056, 0.057, 0.058, 0.059,
            0.060, 0.061, 0.062, 0.063, 0.064, 0.065, 0.066, 0.067, 0.068, 0.069,
            0.070, 0.071, 0.072, 0.073, 0.074, 0.075, 0.076, 0.077, 0.078, 0.079,
            0.080, 0.081, 0.082, 0.083, 0.084, 0.085, 0.086, 0.087, 0.088, 0.089,
            0.090, 0.091, 0.092, 0.093, 0.094, 0.095, 0.096, 0.097, 0.098};

    private float[] testData32bitFloat = {
            00.0F, 01.0F, 02.0F, 03.0F, 04.0F, 05.0F, 06.0F, 07.0F, 08.0F, 09.0F,
            10.0F, 11.0F, 12.0F, 13.0F, 14.0F, 15.0F, 16.0F, 17.0F, 18.0F, 19.0F,
            20.0F, 21.0F, 22.0F, 23.0F, 24.0F, 25.0F, 26.0F, 27.0F, 28.0F, 29.0F,
            30.0F, 31.0F, 32.0F, 33.0F, 34.0F, 35.0F, 36.0F, 37.0F, 38.0F, 39.0F,
            40.0F, 41.0F, 42.0F, 43.0F, 44.0F, 45.0F, 46.0F, 47.0F, 48.0F, 49.0F,
            50.0F, 51.0F, 52.0F, 53.0F, 54.0F, 55.0F, 56.0F, 57.0F, 58.0F, 59.0F,
            60.0F, 61.0F, 62.0F, 63.0F, 64.0F, 65.0F, 66.0F, 67.0F, 68.0F, 69.0F,
            70.0F, 71.0F, 72.0F, 73.0F, 74.0F, 75.0F, 76.0F, 77.0F, 78.0F, 79.0F,
            80.0F, 81.0F, 82.0F, 83.0F, 84.0F, 85.0F, 86.0F, 87.0F, 88.0F, 89.0F,
            90.0F, 91.0F, 92.0F, 93.0F, 94.0F, 95.0F, 96.0F, 97.0F, 98.0F};

    private long[] testData64bitInt = {
            0L, 1L, 2L, 3L, 4L, 5L, 6L, 7L, 8L, 9L,
            10L, 11L, 12L, 13L, 14L, 15L, 16L, 17L, 18L, 19L,
            20L, 21L, 22L, 23L, 24L, 25L, 26L, 27L, 28L, 29L,
            30L, 31L, 32L, 33L, 34L, 35L, 36L, 37L, 38L, 39L,
            40L, 41L, 42L, 43L, 44L, 45L, 46L, 47L, 48L, 49L,
            50L, 51L, 52L, 53L, 54L, 55L, 56L, 57L, 58L, 59L,
            60L, 61L, 62L, 63L, 64L, 65L, 66L, 67L, 68L, 69L,
            70L, 71L, 72L, 73L, 74L, 75L, 76L, 77L, 78L, 79L,
            80L, 81L, 82L, 83L, 84L, 85L, 86L, 87L, 88L, 89L,
            90L, 91L, 92L, 93L, 94L, 95L, 96L, 97L, 98L};

    private int[] testData32bitInt = {
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
            10, 11, 12, 13, 14, 15, 16, 17, 18, 19,
            20, 21, 22, 23, 24, 25, 26, 27, 28, 29,
            30, 31, 32, 33, 34, 35, 36, 37, 38, 39,
            40, 41, 42, 43, 44, 45, 46, 47, 48, 49,
            50, 51, 52, 53, 54, 55, 56, 57, 58, 59,
            60, 61, 62, 63, 64, 65, 66, 67, 68, 69,
            70, 71, 72, 73, 74, 75, 76, 77, 78, 79,
            80, 81, 82, 83, 84, 85, 86, 87, 88, 89,
            90, 91, 92, 93, 94, 95, 96, 97, 98};


    ///// ///// ///// ///// ///// ///// ///// ///// ///// /////
    // some base64 encoded binary data strings to compare against
    // the binary data produced by the BinaryDataArray object

    // binary test data: compressed, base64 encoded, 64 bit precision
    // extracted from file: MzMLFile_7_compressed.mzML (line 74) "m/z array"
    private final String c64bit = "eJwtkWlIVFEAhWUQERGREHFDokQUEQmREIlDoE" +
         "gbaUVI9GOQfgwiIiYxmkRZuVRWluWSmrPoLI7jzJvtJSGhIlYQYYJESERIDCYiJ" +
         "ZEleN995/4Z3ptzz/K9mBj9/HdtL5+prYL+a8TujbrXzU9NfG7E9RRv5bfWZr43" +
         "oyWaeyTe08b/b6Ls69me/oZ26u5g5kSioaCzg/oulAaXrs2+vMd7D6Bm392oVh/" +
         "y/mMMLxz0NZl66fMEX/7Ki/TrQ2bRWk7crWf0fY5LV6SS/gMYGpSHOUP4/OHP+e" +
         "/jL5g3jPTYi0mFyghzR1GjC5k/htNHu48b18fYw4Kf1ZrSwj5W3K//8c+1bWUvG" +
         "wo6U0WEjf3seG+pCP/as7PnOLRWabET7DuBhFVb47EEB3s74NZik53s78RJTZbq" +
         "4g4XZO0sN/e4obX7eGiSuyaRf/nR3E6eh/s8eCtUGUVT3DkFU+9mG0q83OuFMBP" +
         "R09w9jQsCjpjK/T7YRbtVm48cfPi9t2KO5vrJw49yCcBPLn6IcVoQ+SjQ7RRyUl" +
         "AsT4C8Arj95p0gFCC3AD6J9aIL+QVxWAqD5BjEVTkgRJ4hzO/kicUhcg3jgG5Iv" +
         "mFIt6UwOUegjGw5Fk9FyDsCgwyOkLuKczoY8ldh1WJXVH6HV9gH57SBqA==";

    // binary test data: uncompressed, base64 encoded, 64 bit precision
    // extracted from file: MzMLFile_7_uncompressed.mzML (line 74) "m/z array"
    private final String u64bit = "AAAAAAAAAAD8qfHSTWJQP/yp8dJNYmA/+n5qvH" +
         "STaD/8qfHSTWJwP3sUrkfhenQ/+n5qvHSTeD956SYxCKx8P/yp8dJNYoA/O99Pj" +
         "Zdugj97FK5H4XqEP7pJDAIrh4Y/+n5qvHSTiD85tMh2vp+KP3npJjEIrIw/uB6F" +
         "61G4jj/8qfHSTWKQP5zEILByaJE/O99PjZdukj/b+X5qvHSTP3sUrkfhepQ/Gy/" +
         "dJAaBlT+6SQwCK4eWP1pkO99PjZc/+n5qvHSTmD+amZmZmZmZPzm0yHa+n5o/2c" +
         "73U+Olmz956SYxCKycPxkEVg4tsp0/uB6F61G4nj9YObTIdr6fP/yp8dJNYqA/T" +
         "DeJQWDloD+cxCCwcmihP+xRuB6F66E/O99PjZduoj+LbOf7qfGiP9v5fmq8dKM/" +
         "K4cW2c73oz97FK5H4XqkP8uhRbbz/aQ/Gy/dJAaBpT9qvHSTGASmP7pJDAIrh6Y" +
         "/CtejcD0Kpz9aZDvfT42nP6rx0k1iEKg/+n5qvHSTqD9KDAIrhxapP5qZmZmZma" +
         "k/6SYxCKwcqj85tMh2vp+qP4lBYOXQIqs/2c73U+Olqz8pXI/C9SisP3npJjEIr" +
         "Kw/yXa+nxovrT8ZBFYOLbKtP2iR7Xw/Na4/uB6F61G4rj8IrBxaZDuvP1g5tMh2" +
         "vq8/VOOlm8QgsD/8qfHSTWKwP6RwPQrXo7A/TDeJQWDlsD/0/dR46SaxP5zEILB" +
         "yaLE/RIts5/upsT/sUbgeheuxP5MYBFYOLbI/O99PjZdusj/jpZvEILCyP4ts5/" +
         "up8bI/MzMzMzMzsz/b+X5qvHSzP4PAyqFFtrM/K4cW2c73sz/TTWIQWDm0P3sUr" +
         "kfherQ/I9v5fmq8tD/LoUW28/20P3Noke18P7U/Gy/dJAaBtT/D9Shcj8K1P2q8" +
         "dJMYBLY/EoPAyqFFtj+6SQwCK4e2P2IQWDm0yLY/CtejcD0Ktz+yne+nxku3P1p" +
         "kO99Pjbc/AiuHFtnOtz+q8dJNYhC4P1K4HoXrUbg/+n5qvHSTuD+iRbbz/dS4P0" +
         "oMAiuHFrk/";

    // binary test data: compressed, base64 encoded, 32 bit precision
    // extracted from file: MzMLFile_7_compressed.mzML (line 80) "intensity array"
    private final String c32bit = "eJwVxCFIQ2EAhdE/GAyGhQXDwoLBYFgwGAa+jQ" +
         "WDYcFgMCwYDIYFw4LhITLGGGOIyBAZDxkyhsgQkSFDHrJgNC4uGo1Gj5fv3BD+F" +
         "++6SMQkpCwJpRAy5CkQUaVGnZgWPfokjJgwJeWTLxYs+eaHX0I5hBVWWSNDlnVy" +
         "5Nlgky0KbLNDkYgKe+xT5YBDjqhxzAmn1DmjwTkxF1zSpEWbDl16XHHNDX1uuWN" +
         "Awj1DHhgx5pEnJjzzwitT3pjxTsoH8/IfQP5IBA==";

    // binary test data: uncompressed, base64 encoded, 32 bit precision
    // extracted from file: MzMLFile_7_uncompressed.mzML (line 80) "intensity array"
    private final String u32bit = "AAAAAAAAgD8AAABAAABAQAAAgEAAAKBAAADAQA" +
         "AA4EAAAABBAAAQQQAAIEEAADBBAABAQQAAUEEAAGBBAABwQQAAgEEAAIhBAACQQ" +
         "QAAmEEAAKBBAACoQQAAsEEAALhBAADAQQAAyEEAANBBAADYQQAA4EEAAOhBAADw" +
         "QQAA+EEAAABCAAAEQgAACEIAAAxCAAAQQgAAFEIAABhCAAAcQgAAIEIAACRCAAA" +
         "oQgAALEIAADBCAAA0QgAAOEIAADxCAABAQgAAREIAAEhCAABMQgAAUEIAAFRCAA" +
         "BYQgAAXEIAAGBCAABkQgAAaEIAAGxCAABwQgAAdEIAAHhCAAB8QgAAgEIAAIJCA" +
         "ACEQgAAhkIAAIhCAACKQgAAjEIAAI5CAACQQgAAkkIAAJRCAACWQgAAmEIAAJpC" +
         "AACcQgAAnkIAAKBCAACiQgAApEIAAKZCAACoQgAAqkIAAKxCAACuQgAAsEIAALJ" +
         "CAAC0QgAAtkIAALhCAAC6QgAAvEIAAL5CAADAQgAAwkIAAMRC";

    public void setUp() {
        // define the reference CV to use for the CVParams
        cv = new CV();
        cv.setId("MS");
        cv.setFullName("Proteomics Standards Initiative Mass Spectrometry Ontology");
        cv.setVersion("2.1.0");
        cv.setURI("http://psidev.cvs.sourceforge.net/*checkout*/psidev/psi/psi-ms/mzML/controlledVocabulary/psi-ms.obo");


        // define a CVParam for 64 bit precision
        prec64bit = new BinaryDataArrayCVParam();
        prec64bit.setAccession(BinaryDataArray.MS_FLOAT64BIT_AC);
        prec64bit.setName(BinaryDataArray.MS_FLOAT64BIT_NAME);
        prec64bit.setCv(cv);

        // define a CVParam for 32 bit precision
        prec32bit = new BinaryDataArrayCVParam();
        prec32bit.setAccession(BinaryDataArray.MS_FLOAT32BIT_AC);
        prec32bit.setName(BinaryDataArray.MS_FLOAT32BIT_NAME);
        prec32bit.setCv(cv);

        // define a CVParam for compression
        compressed = new BinaryDataArrayCVParam();
        compressed.setAccession(BinaryDataArray.MS_COMPRESSED_AC);
        compressed.setName(BinaryDataArray.MS_COMPRESSED_NAME);
        compressed.setCv(cv);

        // define a CVParam for no compression
        uncompressed = new BinaryDataArrayCVParam();
        uncompressed.setAccession(BinaryDataArray.MS_UNCOMPRESSED_AC);
        uncompressed.setName(BinaryDataArray.MS_UNCOMPRESSED_NAME);
        uncompressed.setCv(cv);

    }

    public void testUnmarshallingUncompressed64Float() throws MzMLUnmarshallerException, UnsupportedEncodingException {
        // find the test XML file
        URL url = this.getClass().getClassLoader().getResource("MzMLFile_7_uncompressed.mzML");

        if (VERBOSE) System.out.println("\n----- testUnmarshallingUncompressed64 ------------------------");
        checkFile(url, BinaryDataArray.Precision.FLOAT64BIT);
        if (VERBOSE) System.out.println("----- testUnmarshallingUncompressed64 end --------------------");

    }

    public void testUnmarshallingCompressed64Float() throws MzMLUnmarshallerException, UnsupportedEncodingException {
        // find the test XML file
        URL url = this.getClass().getClassLoader().getResource("MzMLFile_7_compressed.mzML");

        if (VERBOSE) System.out.println("\n----- testUnmarshallingCompressed64 --------------------------");
        checkFile(url, BinaryDataArray.Precision.FLOAT64BIT);
        if (VERBOSE) System.out.println("----- testUnmarshallingCompressed64 end ----------------------");

    }

    public void testUnmarshallingUncompressed32Float() throws MzMLUnmarshallerException, UnsupportedEncodingException {
        // find the test XML file
        URL url = this.getClass().getClassLoader().getResource("MzMLFile_7_uncompressed.mzML");

        if (VERBOSE) System.out.println("\n----- testUnmarshallingUncompressed32 ------------------------");
        checkFile(url, BinaryDataArray.Precision.FLOAT32BIT);
        if (VERBOSE) System.out.println("----- testUnmarshallingUncompressed32 end --------------------");

    }

    public void testUnmarshallingCompressed32Float() throws MzMLUnmarshallerException, UnsupportedEncodingException {
        // find the test XML file
        URL url = this.getClass().getClassLoader().getResource("MzMLFile_7_compressed.mzML");

        if (VERBOSE) System.out.println("\n----- testUnmarshallingCompressed32 --------------------------");
        checkFile(url, BinaryDataArray.Precision.FLOAT32BIT);
        if (VERBOSE) System.out.println("----- testUnmarshallingCompressed32 end ----------------------");

    }

    private void checkFile(URL mzMLFileUrl, BinaryDataArray.Precision prec) throws UnsupportedEncodingException {
        String id = "index=0";
        Spectrum spectrum = getSpectrum(mzMLFileUrl, id);

        // check that we have binary data
        //BinaryDataArrayList bdal = spectrum.getBinaryDataArrayList();
        //assertNotNull(bdal);
        List<BinaryDataArray> dataArray = spectrum.getBinaryDataArrayList().getBinaryDataArray();
        Iterator<BinaryDataArray> iter = dataArray.iterator();
        if (prec == BinaryDataArray.Precision.FLOAT32BIT) { iter.next(); } // get the second BinaryDataArray
        BinaryDataArray data = iter.next(); // get the first BinaryDataArray
        assertNotNull(data);

        // check the data from the XML against the expected data
        Number[] xmlData = data.getBinaryDataAsNumberArray();
        assertNotNull(xmlData);

        compareToRefData(xmlData, prec);
    }

    private void compareToRefData(Number[] array, BinaryDataArray.Precision prec) {
        Arrays.sort(array); // make sure we have the expected (numeric) order of values

        if (VERBOSE) {
            // print to see what the data looks like
            System.out.println("Data as double array:");
            for (Number anArray : array) {
                System.out.println("data: " + anArray.doubleValue());
            }
            System.out.println("end of double array.");
        }

        switch (prec) {
            case FLOAT64BIT :   // double values
                                assertTrue(array.length == testData64bitFloat.length);
                                for (int i = 0; i < array.length; i++) {
                                    assertTrue(array[i].doubleValue() == testData64bitFloat[i]);
                                }
                                break;

            case FLOAT32BIT :   // float values
                                assertTrue(array.length == testData32bitFloat.length);
                                for (int i = 0; i < array.length; i++) {
                                    assertTrue(array[i].floatValue() == testData32bitFloat[i]);
                                }
                                break;

            case INT64BIT :     // long values
                                assertTrue(array.length == testData64bitInt.length);
                                for (int i = 0; i < array.length; i++) {
                                    assertTrue(array[i].longValue() == testData64bitInt[i]);
                                }
                                break;

            case INT32BIT :     // int values
                                assertTrue(array.length == testData32bitInt.length);
                                for (int i = 0; i < array.length; i++) {
                                    assertTrue(array[i].intValue() == testData32bitInt[i]);
                                }
                                break;

            default       :     throw new IllegalStateException("Not supported Precision while " +
                                                                "comparing data with reference data!");
        }

    }

    private Spectrum getSpectrum(URL mzMLFileUrl, String id) {
        assertNotNull(mzMLFileUrl);

        // create the marshaller and check that the file is indexed
        MzMLUnmarshaller um = new MzMLUnmarshaller(mzMLFileUrl);
        assertTrue( um.isIndexedmzML() );

        // the checksum is not correct, so we skip this test
        // assertTrue(um.isOkFileChecksum());

        MzML content = um.unmarshall();
        assertNotNull(content);

        // fine the spectrum with id "index=0"
        List<Spectrum> spectrumList = content.getRun().getSpectrumList().getSpectrum();
        Spectrum spectrum = null;
        for (Spectrum sp : spectrumList) {
            assertNotNull(sp);
            if ( sp.getId() != null && sp.getId().equalsIgnoreCase(id) ) {
                spectrum = sp;
            }
        }
        assertNotNull(spectrum);
        return spectrum;
    }



    public void test64BitFloatSetGet() throws UnsupportedEncodingException {
        checkSetGetBinary(BinaryDataArray.Precision.FLOAT64BIT, false); // no compression
        checkSetGetBinary(BinaryDataArray.Precision.FLOAT64BIT, true); // with compression
    }

    public void test32BitFloatSetGet() throws UnsupportedEncodingException {
        checkSetGetBinary(BinaryDataArray.Precision.FLOAT32BIT, false); // no compression
        checkSetGetBinary(BinaryDataArray.Precision.FLOAT32BIT, true); // with compression
    }

    public void test64BitIntSetGet() throws UnsupportedEncodingException {
        checkSetGetBinary(BinaryDataArray.Precision.INT64BIT, false); // no compression
        checkSetGetBinary(BinaryDataArray.Precision.INT64BIT, true); // with compression
    }

    public void test32BitIntSetGet() throws UnsupportedEncodingException {
        checkSetGetBinary(BinaryDataArray.Precision.INT32BIT, false); // no compression
        checkSetGetBinary(BinaryDataArray.Precision.INT32BIT, true); // with compression
    }

    private void checkSetGetBinary(BinaryDataArray.Precision p, boolean compression) {
        // create the BinaryDataArray object from a test data array
        // this will do all the conversions and store the data
        BinaryDataArray bda = new BinaryDataArray();

        // set the test data
        Number[] retrievedData;
        switch (p) {
            case FLOAT64BIT : // double
                              bda.set64BitFloatArrayAsBinaryData(testData64bitFloat, compression, cv);
                              retrievedData = bda.getBinaryDataAsNumberArray();
                              for (int i = 0; i < retrievedData.length; i++) {
                                  assertTrue(retrievedData[i].doubleValue() == testData64bitFloat[i]);
                              }
                              break;

            case FLOAT32BIT : // float
                              bda.set32BitFloatArrayAsBinaryData(testData32bitFloat, compression, cv);
                              retrievedData = bda.getBinaryDataAsNumberArray();
                              for (int i = 0; i < retrievedData.length; i++) {
                                  assertTrue(retrievedData[i].floatValue() == testData32bitFloat[i]);
                              }
                              break;

            case INT64BIT   : // long
                              bda.set64BitIntArrayAsBinaryData(testData64bitInt, compression, cv);
                              retrievedData = bda.getBinaryDataAsNumberArray();
                              for (int i = 0; i < retrievedData.length; i++) {
                                  assertTrue(retrievedData[i].longValue() == testData64bitInt[i]);
                              }
                              break;

            case INT32BIT   : // int
                              bda.set32BitIntArrayAsBinaryData(testData32bitInt, compression, cv);
                              retrievedData = bda.getBinaryDataAsNumberArray();
                              for (int i = 0; i < retrievedData.length; i++) {
                                  assertTrue(retrievedData[i].intValue() == testData32bitInt[i]);
                              }
                              break;

            default         : throw new IllegalStateException("Not supported Precision in BinaryDataArray: " + p);
                
        }

    }



    public void testDataFromFile() throws UnsupportedEncodingException {
        Number[] one = createBDAFromC64Bit().getBinaryDataAsNumberArray(); // 64 bit, compressed
        Number[] two = createBDAFromU64Bit().getBinaryDataAsNumberArray(); // 64 bit, uncompressed
        Number[] three = createBDAFromC32Bit().getBinaryDataAsNumberArray(); // 32 bit, compressed
        Number[] four = createBDAFromU32Bit().getBinaryDataAsNumberArray();  // 32 bit, uncompressed
        assertTrue(one.length == two.length);
        assertTrue(three.length == four.length);
        assertTrue(one.length == four.length);

        // no matter the processing, the information should stay the same
        for (int i = 0; i < two.length; i++) {
            assertTrue(one[i].doubleValue() == two[i].doubleValue()); // 64 bit (double), "m/z array"
            assertTrue(three[i].floatValue() == four[i].floatValue()); // 32 bit (float), "intensity array"

            // unfortunatley the values for "m/z array" and "intensity array" are not the same
            // assertTrue((float)one[i] == (float)four[i]);
        }
    }


    public void testMarshaller() throws UnsupportedEncodingException {

        BinaryDataArray bda = createBDAFromC64Bit(); // compressed, 64 bit precision

        MzMLMarshaller m = new MzMLMarshaller();

        String c64bitTest = m.marshall(bda);

        if (VERBOSE) {
            System.out.println("\n----- testMarshaller -----------------------------------------");
            m.marshall(bda, System.out);
            System.out.println("----- testMarshaller end -------------------------------------");
        }

        // the BinaryDataArray was created from the String in "c64bit"
        // therefore the marshalled output should contain the same string
        // (in its binary XML tag)
        assertTrue(c64bitTest.contains(c64bit));

    }


    private BinaryDataArray createBDAFromC64Bit() throws UnsupportedEncodingException {
        // manually construct a BinaryDataArray with the according CVParams
        BinaryDataArray bda = new BinaryDataArray();
        // set compressed, 64 bit precision data
        // need to decode the base64 encoded string, since the data is stored
        // un-encoded in the object (decoded from XML with JAXB)
        bda.setBinary( Base64.decodeBase64(c64bit.getBytes("ASCII")) );
        // set CVParam for 64 bit precision
        bda.getCvParam().add(prec64bit);
        // set CVParam for compressed data
        bda.getCvParam().add(compressed);

        return bda;
    }

    private BinaryDataArray createBDAFromU64Bit() throws UnsupportedEncodingException {

        // manually construct a BinaryDataArray with the according CVParams
        BinaryDataArray bda = new BinaryDataArray();
        // set uncompressed, 64 bit precision data
        // need to decode the base64 encoded string, since the data is stored
        // un-encoded in the object (decoded from XML with JAXB)
        bda.setBinary( Base64.decodeBase64(u64bit.getBytes("ASCII")) );
        // set CVParam for 64 bit precision
        bda.getCvParam().add(prec64bit);
        // set CVParam for not compressed data
        bda.getCvParam().add(uncompressed);

        return bda;
    }

    private BinaryDataArray createBDAFromC32Bit() throws UnsupportedEncodingException {

        // manually construct a BinaryDataArray with the according CVParams
        BinaryDataArray bda = new BinaryDataArray();
        // set uncompressed, 32 bit precision data
        // need to decode the base64 encoded string, since the data is stored
        // un-encoded in the object (decoded from XML with JAXB)
        bda.setBinary( Base64.decodeBase64(c32bit.getBytes("ASCII")) );
        // set CVParam for 32 bit precision
        bda.getCvParam().add(prec32bit);
        // set CVParam for compressed data
        bda.getCvParam().add(compressed);

        return bda;
    }

    private BinaryDataArray createBDAFromU32Bit() throws UnsupportedEncodingException {

        // manually construct a BinaryDataArray with the according CVParams
        BinaryDataArray bda = new BinaryDataArray();
        // set uncompressed, 32 bit precision data
        // need to decode the base64 encoded string, since the data is stored
        // un-encoded in the object (decoded from XML with JAXB)
        bda.setBinary( Base64.decodeBase64(u32bit.getBytes("ASCII")) );
        // set CVParam for 32 bit precision
        bda.getCvParam().add(prec32bit);
        // set CVParam for not compressed data
        bda.getCvParam().add(uncompressed);

        return bda;
    }


}
