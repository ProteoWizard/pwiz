package uk.ac.ebi.jmzml.test.xml;

import junit.framework.TestCase;
import uk.ac.ebi.jmzml.model.mzml.Chromatogram;
import uk.ac.ebi.jmzml.model.mzml.IndexList;
import uk.ac.ebi.jmzml.model.mzml.Spectrum;
import uk.ac.ebi.jmzml.xml.io.MzMLUnmarshaller;
import uk.ac.ebi.jmzml.xml.io.MzMLUnmarshallerException;

import java.net.URL;

/**
 * @author Florian Reisinger
 * @since 0.4
 */
public class MzMLIndexTest extends TestCase {

    public void testIndexedmzML() throws MzMLUnmarshallerException {
        URL url = this.getClass().getClassLoader().getResource("tiny.pwiz.err.idx.mzML");
        assertNotNull(url);

        MzMLUnmarshaller um = new MzMLUnmarshaller(url);

        IndexList index = um.getMzMLIndex();
        assertNotNull(index);

        // check that we have as many index entries as we expect
        // we expect 2 entries: 'chromatogram' and 'spectrum'
        assertEquals(2, index.getCount().intValue());
        assertEquals(index.getCount().intValue(), index.getIndex().size());

        Spectrum spectrum = um.getSpectrumByRefId("scan=19");
        assertNotNull(spectrum);

        spectrum = um.getSpectrumByRefId("scan=21");
        assertNotNull(spectrum);

        spectrum = um.getSpectrumByRefId("scan=22");
        assertNotNull(spectrum);

        spectrum = um.getSpectrumBySpotId("A1,42x42,4242x4242");
        assertNotNull(spectrum);


        Chromatogram chr = um.getChromatogramByRefId("tic");
        assertNotNull(chr);

        chr = um.getChromatogramByRefId("sic");
        assertNotNull(chr);

        ///// ///// ///// ///// ///// ///// ///// ///// ///// /////
        // negative testing
        // (tests that are not supposed to return useful results)

        // here we check the bahaviour if we search for a id that is not in the index
        spectrum = um.getSpectrumByRefId("nonexist");
        assertNull(spectrum);

        // here we try to retrieve a spectrum by scanTime, but no according entry exists in the index
        spectrum = um.getSpectrumByScanTime(12345);
        assertNull(spectrum);

        // here we introduced a offset mismatch in the mzML index of the test file to
        // test the behaviour of the unmarshaller in case of a index offset mismatch
        Exception expected = null; // we expect an exception
        try {
            um.getSpectrumByRefId("scan=20");
        } catch (MzMLUnmarshallerException mue) {
            expected = mue;
        }
        assertNotNull(expected);

    }

}
