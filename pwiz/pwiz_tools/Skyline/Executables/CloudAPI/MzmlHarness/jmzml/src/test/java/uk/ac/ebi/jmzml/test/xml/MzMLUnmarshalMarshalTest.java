package uk.ac.ebi.jmzml.test.xml;

import junit.framework.TestCase;
import org.apache.log4j.Logger;
import org.xml.sax.SAXException;
import uk.ac.ebi.jmzml.MzMLElement;
import uk.ac.ebi.jmzml.model.mzml.*;
import uk.ac.ebi.jmzml.xml.io.MzMLMarshaller;
import uk.ac.ebi.jmzml.xml.io.MzMLObjectIterator;
import uk.ac.ebi.jmzml.xml.io.MzMLUnmarshaller;
import uk.ac.ebi.jmzml.xml.io.MzMLUnmarshallerException;

import javax.xml.transform.Source;
import javax.xml.transform.stream.StreamSource;
import javax.xml.validation.Schema;
import javax.xml.validation.SchemaFactory;
import javax.xml.validation.Validator;
import java.io.File;
import java.io.FileWriter;
import java.io.IOException;
import java.net.URISyntaxException;
import java.net.URL;
import java.util.Iterator;
import java.util.List;

/**
 * @author Florian Reisinger
 * @since 0.4
 */
public class MzMLUnmarshalMarshalTest extends TestCase {

    private static final Logger logger = Logger.getLogger(MzMLUnmarshalMarshalTest.class);

    private File mzMLFile;
    private File indexedmzMLFile;

    @Override
    protected void setUp() throws Exception {
        URL aUrl = this.getClass().getClassLoader().getResource("tiny.pwiz.mzML");
        assertNotNull(aUrl);
        URL bUrl = this.getClass().getClassLoader().getResource("tiny.pwiz.idx.mzML");
        assertNotNull(bUrl);

        try {
            mzMLFile = new File(aUrl.toURI());
            indexedmzMLFile = new File(bUrl.toURI());
        } catch (URISyntaxException e) {
            e.printStackTrace();
            throw new IllegalStateException("Could not create file from URL: " + aUrl + " or " + bUrl);
        }
        assertTrue("mzML instance file '" + mzMLFile.getAbsolutePath() + "' does not exist.", mzMLFile.exists());
        assertTrue("IndexedmzML instance file '" + indexedmzMLFile.getAbsolutePath() + "' does not exist.", indexedmzMLFile.exists());
    }

    // ToDo: refactor into better structure (private methods for checks and test
    // ToDo: methods for actual example testing) so we can test multiple files

    public void testReadIndexedMzML() throws MzMLUnmarshallerException {
        assertTrue(isValidMzML(indexedmzMLFile));

        MzMLUnmarshaller um = new MzMLUnmarshaller(indexedmzMLFile);
        MzML mz = um.unmarshall();
        assertNotNull(mz);

        // same content checks as if it were a normal MzML
        checkMzMLContent(mz);

        // now check the index
        IndexList idxl = um.getMzMLIndex();
        assertNotNull(idxl);

        // check that the index count is correct and as expected
        assertEquals(idxl.getCount().intValue(), idxl.getIndex().size());
        assertEquals(2, idxl.getIndex().size());

        // and check some values
        // note: there is a whole test class to check that the index is handled correctly
        assertEquals("spectrum", idxl.getIndex().get(0).getName());
        assertEquals("chromatogram", idxl.getIndex().get(1).getName());

    }

    /**
     * Checking the instrument components, since they are special cases
     * (the Class name does not reflect the XML element name).
     *
     * @throws MzMLUnmarshallerException in case of problems with the unmarshalling of XML elements.
     */
    public void testComponents() throws MzMLUnmarshallerException {
        assertTrue(isValidMzML(mzMLFile));
        logger.info("zmML file is valid.");

        MzMLUnmarshaller um = new MzMLUnmarshaller(mzMLFile);
        assertNotNull(um);

        int sourceCnt = um.getObjectCountForXpath("/mzML/instrumentConfigurationList/instrumentConfiguration/componentList/source");
        assertEquals("Expected number of 'source' elements.", 1, sourceCnt);
        MzMLObjectIterator<SourceComponent> sourceIter = um.unmarshalCollectionFromXpath("/mzML/instrumentConfigurationList/instrumentConfiguration/componentList/source", SourceComponent.class);
        assertNotNull(sourceIter);
        assertNotNull(sourceIter.next());

        int analyzerCnt = um.getObjectCountForXpath("/mzML/instrumentConfigurationList/instrumentConfiguration/componentList/analyzer");
        assertEquals("Expected number of 'analyzer' elements.", 1, analyzerCnt);
        MzMLObjectIterator<AnalyzerComponent> analyzerIter = um.unmarshalCollectionFromXpath("/mzML/instrumentConfigurationList/instrumentConfiguration/componentList/analyzer", AnalyzerComponent.class);
        assertNotNull(analyzerIter);
        assertNotNull(analyzerIter.next());

        int detectorCnt = um.getObjectCountForXpath("/mzML/instrumentConfigurationList/instrumentConfiguration/componentList/detector");
        assertEquals("Expected number of 'detector' elements.", 1, detectorCnt);
        MzMLObjectIterator<DetectorComponent> detectorIter = um.unmarshalCollectionFromXpath("/mzML/instrumentConfigurationList/instrumentConfiguration/componentList/detector", DetectorComponent.class);
        assertNotNull(detectorIter);
        assertNotNull(detectorIter.next());



    }

    public void testReadWriteMzML() throws MzMLUnmarshallerException {

        ///// ///// ///// ///// FIRST READ ///// ///// ///// /////
        // check if the instance file is valid
        assertTrue(isValidMzML(mzMLFile));
        logger.info("mzmML file is valid.");

        MzMLUnmarshaller um_1 = new MzMLUnmarshaller(mzMLFile);
        MzML mz_1 = um_1.unmarshall();
        assertNotNull(mz_1);

        assertNotNull(mz_1.getRun());

        // check that there is no IndexList (since we only have a mzML and not an indexedmzML)
        System.out.println("An ERROR could be logged here, which is perfectly fine.");
        assertFalse(um_1.isIndexedmzML());

        // now check if the content is as expected
        checkMzMLContent(mz_1);
        logger.info("Unmarshalling valid XML is OK.");


        ///// ///// ///// ///// WRITE BACK ///// ///// ///// /////
        // now try to write it back to a temporary file
        FileWriter fw;
        File tmpFile;
        try {
            tmpFile = File.createTempFile("tmpMzML", ".xml");
            tmpFile.deleteOnExit();
            fw = new FileWriter(tmpFile);
        } catch (IOException e) {
            e.printStackTrace();
            throw new IllegalStateException("Could not create or write to temporary file for marshalling.");
        }
        MzMLMarshaller mm = new MzMLMarshaller();
        mm.marshall(mz_1, fw);
        logger.info("Marshalled back to XML.");

        // now check if the written mzML is valid
        assertTrue(isValidMzML(tmpFile));
        logger.info("Marshalling mzML is valid.");


        ///// ///// ///// ///// RE-READ WRITTEN mzML ///// ///// ///// /////
        MzMLUnmarshaller um_2 = new MzMLUnmarshaller(tmpFile);
        MzML mz_2 = um_2.unmarshall();
        assertNotNull(mz_2);

        // check that there is no IndexList (since we only have a mzML and not an indexedmzML)
        System.out.println("An ERROR could be logged here, which is perfectly fine.");
        assertFalse(um_2.isIndexedmzML());

        // now check if the content is as expected
        checkMzMLContent(mz_2);

        // and compare the two versions (they have to have the same values!)
        checkEqual(mz_1, mz_2);
        logger.info("Re-unmarshalling mzML is OK.");

    }

    public void testReadWriteIndexedmzML() {

        ///// ///// ///// ///// FIRST READ ///// ///// ///// /////
        // check if the instance file is valid
        assertTrue(isValidMzML(indexedmzMLFile));
        logger.info("zmML file is valid.");

        MzMLUnmarshaller um_1 = new MzMLUnmarshaller(indexedmzMLFile);
        MzMLObject mz_1 = um_1.unmarshall();
        assertNotNull(mz_1);

        // now check if the content is as expected
        checkMzMLContent(mz_1);
        logger.info("Unmarshalling valid XML is OK.");


        ///// ///// ///// ///// WRITE BACK ///// ///// ///// /////
        // now try to write it back to a temporary file
        FileWriter fw;
        File tmpFile;
        try {
//            tmpFile = new File("tmpMzML.xml");
            tmpFile = File.createTempFile("tmpMzML", "xml");
            tmpFile.deleteOnExit();
            fw = new FileWriter(tmpFile);
        } catch (IOException e) {
            e.printStackTrace();
            throw new IllegalStateException("Could not create or write to temporary file for marshalling.");
        }
        MzMLMarshaller mm = new MzMLMarshaller();
        mm.marshall(mz_1, fw);
        logger.info("Marshalled back to XML.");

        // now check if the written mzML is valid
        assertTrue(isValidMzML(tmpFile));
        logger.info("Marshalling mzML is valid.");


        ///// ///// ///// ///// RE-READ WRITTEN mzML ///// ///// ///// /////
        MzMLUnmarshaller um_2 = new MzMLUnmarshaller(tmpFile);
        MzML mz_2 = um_2.unmarshall();
        assertNotNull(mz_2);

        // now check if the content is as expected
        checkMzMLContent(mz_2);

        // and compare the two versions (they have to have the same values!)
        checkEqual(mz_1, mz_2);
        logger.info("Re-unmarshalling mzML is OK.");

    }

    public void testSpectrumIteration() {
        assertTrue(isValidMzML(mzMLFile));

        MzMLUnmarshaller um = new MzMLUnmarshaller(mzMLFile);

        Iterator<Spectrum> iterator = um.unmarshalCollectionFromXpath(MzMLElement.Spectrum.getXpath(), Spectrum.class);

        // count the number of spectra we are iterating over
        logger.debug("Iterating over all spectra...");
        int spectrumCnt = 0;
        while (iterator.hasNext()) {
            Spectrum spectrum = iterator.next();
            if (logger.isDebugEnabled()) {
                logger.debug("Spectrum: " + spectrum.getId());
            }
            spectrumCnt++;
        }

        // get the number of spectra entries in the index
        int spectrumNo = um.getObjectCountForXpath(MzMLElement.Spectrum.getXpath());
        // compare the two spectra counts
        assertEquals(spectrumNo, spectrumCnt);


        // additionally check one spectrum if we can detect binary arrays for  m/z values and intensities
        String spectrumId = null;
        try {
            spectrumId = um.getSpectrumIDs().iterator().next();
            Spectrum testSpectrum = um.getSpectrumById(spectrumId);
            boolean mzDataFound = false;
            boolean intensityDataFound = false;
            for (BinaryDataArray binaryDataArray : testSpectrum.getBinaryDataArrayList().getBinaryDataArray()) {
                BinaryDataArray.DataType type = binaryDataArray.getDataType();
                if (type.equals(BinaryDataArray.DataType.MZ_VALUES)) {
                    mzDataFound = true;
                }
                if (type.equals(BinaryDataArray.DataType.INTENSITY)) {
                    intensityDataFound = true;
                }
            }
            assertTrue("m/z values not found!", mzDataFound);
            assertTrue("intensity values not found!", intensityDataFound);
        } catch (MzMLUnmarshallerException e) {
            logger.error("Error reading binary data arrays for spectrum " + spectrumId, e);
        }
    }


    ///// ///// ///// ///// /////
    // private helper methods


    private boolean isValidMzML(File mzML) {
        boolean retval;

        // 1. Lookup a factory for the W3C XML Schema language
        SchemaFactory factory = SchemaFactory.newInstance("http://www.w3.org/2001/XMLSchema");

        // 2. Compile the schema.
        URL schemaLocation;
        // Note: not checking against external schema, because of performance and availability (internet connection) issues 
//        try {
//            if (indexed) {
        schemaLocation = this.getClass().getClassLoader().getResource("mzML1.1.1-idx.xsd");
//                schemaLocation = new URL("http://psidev.cvs.sourceforge.net/*checkout*/psidev/psi/psi-ms/mzML/schema/mzML1.1.0_idx.xsd");
//            } else {
//                schemaLocation = this.getClass().getClassLoader().getResource("mzML1.1.0.xsd");
//                schemaLocation = new URL("http://psidev.cvs.sourceforge.net/*checkout*/psidev/psi/psi-ms/mzML/schema/mzML1.1.0.xsd");
//            }
//        } catch (MalformedURLException e) {
//            throw new IllegalStateException("Could not load external schema location!", e);
//        }
        assertNotNull(schemaLocation);

        Schema schema;
        try {
            schema = factory.newSchema(schemaLocation);
        } catch (SAXException e) {
            e.printStackTrace();
            throw new IllegalStateException("Could not compile Schema for file: " + schemaLocation);
        }

        // 3. Get a validator from the schema.
        Validator validator = schema.newValidator();

        // 4. Parse the document you want to check.
        Source source = new StreamSource(mzML);

        // 5. Check the document (throws an Exception if not valid)
        try {
            validator.validate(source);
            retval = true;
        } catch (SAXException ex) {
            System.out.println(mzML.getName() + " is not valid because ");
            System.out.println(ex.getMessage());
            retval = false;
        } catch (IOException e) {
            e.printStackTrace();
            throw new IllegalStateException("Could not validate file because of file read problems for source: " + mzML.getAbsolutePath());
        }

        return retval;
    }

    private void checkMzMLContent(MzMLObject mo) {
        // check some values of the XML file to see if the object model was populated correctly

        MzML mz;
        if (mo instanceof MzML) {
            mz = (MzML) mo;
        } else if (mo instanceof IndexedmzML) {
            mz = ((IndexedmzML) mo).getMzML();
        } else {
            throw new IllegalStateException("Can not check the MzML content for objects other than MzML or IndexemzML!");
        }

        // the specified CVs
        assertEquals("MS", mz.getCvList().getCv().get(0).getId());
        assertEquals("UO", mz.getCvList().getCv().get(1).getId());

        // the run id
        assertEquals("Exp01", mz.getRun().getId());

        // the run has one source file reference
        String sf = mz.getRun().getDefaultSourceFileRef();
        assertEquals("sf1", sf);

        // check the default processing method references
        SpectrumList sList = mz.getRun().getSpectrumList();
        if (MzMLElement.SpectrumList.isAutoRefResolving() && sList.getDefaultDataProcessingRef() != null) {
            DataProcessing dp = sList.getDefaultDataProcessing();
            List<ProcessingMethod> pmList = dp.getProcessingMethod();
            assertEquals(1, pmList.size());
            ProcessingMethod pm = pmList.get(0);
            assertNotNull(pm);
            assertEquals("pwiz", pm.getSoftware().getId());
        } else {
            logger.warn("SpectrumList is not auto-resolving or does not contain a DefaultDataProcessing reference!");
        }

        // check the software id of the default data processing method

        // ToDo: maybe add more cases
    }

    private void checkEqual(MzMLObject mo_1, MzMLObject mo_2) {

        MzML mz_1;
        MzML mz_2;
        if (mo_1 instanceof MzML && mo_2 instanceof MzML) {
            mz_1 = (MzML) mo_1;
            mz_2 = (MzML) mo_2;
        } else if (mo_1 instanceof IndexedmzML && mo_2 instanceof IndexedmzML) {
            mz_1 = ((IndexedmzML) mo_1).getMzML();
            mz_2 = ((IndexedmzML) mo_2).getMzML();
            // ToDo: add index specific check
        } else {
            throw new IllegalStateException("Can not compare objects of different type. Only MzML or IndexedmzML types are supported.");
        }

        // check if the content of the two MzMLs is equal
        // (this is not an extensive test, only a few example values are checked)

        // number of specified CVs
        assertEquals(mz_1.getCvList().getCv().size(), mz_2.getCvList().getCv().size());

        // the run id
        assertEquals(mz_1.getRun().getId(), mz_2.getRun().getId());

        // check the software id of the default data processing for the spectra
        SpectrumList sList_1 = mz_1.getRun().getSpectrumList();
        SpectrumList sList_2 = mz_2.getRun().getSpectrumList();
        if (MzMLElement.SpectrumList.isAutoRefResolving() && sList_1.getDefaultDataProcessingRef() != null && sList_2.getDefaultDataProcessingRef() != null) {
            ProcessingMethod pm_1 = sList_1.getDefaultDataProcessing().getProcessingMethod().get(0);
            ProcessingMethod pm_2 = sList_2.getDefaultDataProcessing().getProcessingMethod().get(0);
            assertEquals(pm_1.getSoftware().getId(), pm_2.getSoftware().getId());

        } else {
            logger.warn("SpectrumList is not auto-resolving or does not contain a DefaultDataProcessing reference!");
        }

    }

}
