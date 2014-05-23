package uk.ac.ebi.jmzml.test.xml;

import junit.framework.Assert;
import junit.framework.TestCase;
import uk.ac.ebi.jmzml.model.mzml.MzML;
import uk.ac.ebi.jmzml.model.mzml.Scan;
import uk.ac.ebi.jmzml.model.mzml.Spectrum;
import uk.ac.ebi.jmzml.xml.io.MzMLMarshaller;
import uk.ac.ebi.jmzml.xml.io.MzMLUnmarshaller;

import java.io.BufferedWriter;
import java.io.File;
import java.io.FileWriter;
import java.io.IOException;
import java.net.URISyntaxException;
import java.net.URL;
import java.util.List;

/**
 * This class
 *
 * @author martlenn
 * @version $Id$
 * Date: 18-Aug-2008
 * Time: 11:15:38
 */
public class ReferenceableParamGroupHandlingTest extends TestCase {

    public void testReadingOfreferenceableParamGroup() throws URISyntaxException {
        URL url = this.getClass().getClassLoader().getResource("sample_small.mzML");
        assertNotNull(url);

        File file = new File(url.toURI());
        MzMLUnmarshaller um = new MzMLUnmarshaller(file);

        MzML mz = um.unmarshall();
        assertNotNull(mz);

        // First pass.
        List<Spectrum> spectra = mz.getRun().getSpectrumList().getSpectrum();
        for (Spectrum spectrum : spectra) {
            if (spectrum.getId().equalsIgnoreCase("scan=21")) {
                // skip this spectrum, since it does not contain scan data
            } else {
                if (spectrum.getScanList() != null) {
                    Scan scan = spectrum.getScanList().getScan().get(0); // get first scan
                    if(scan != null) {
                        Assert.assertEquals(5, scan.getCvParam().size());
                        Assert.assertEquals(2, scan.getUserParam().size());
                    }
                }
            }
        }

        // Second pass. See if it keeps adding the referenceable ones.
        for (Spectrum spectrum : spectra) {
            if (spectrum.getId().equalsIgnoreCase("scan=21")) {
                // skip this spectrum, since it does not contain scan data
            } else {
                if (spectrum.getScanList() != null) {
                    Scan scan = spectrum.getScanList().getScan().get(0); // get first scan
                    if(scan != null) {
                        Assert.assertEquals(5, scan.getCvParam().size());
                        Assert.assertEquals(2, scan.getUserParam().size());
                    }
                }
            }
        }
    }

    public void testReadingOfreferenceableParamGroupWithoutSpectrumCaching() {
        URL url = this.getClass().getClassLoader().getResource("sample_small.mzML");
        assertNotNull(url);

        MzMLUnmarshaller um = new MzMLUnmarshaller(url, false);

        MzML mz = um.unmarshall();
        assertNotNull(mz);

        assertNotNull(mz.getRun());

        // First pass.
        List<Spectrum> spectra = mz.getRun().getSpectrumList().getSpectrum();
        for (Spectrum spectrum : spectra) {
            if (spectrum.getId().equalsIgnoreCase("scan=21")) {
                // skip this spectrum, since it does not contain scan data
            } else {
                if (spectrum.getScanList() != null) {
                    Scan scan = spectrum.getScanList().getScan().get(0); // get first scan
                    if(scan != null) {
                        Assert.assertEquals(5, scan.getCvParam().size());
                        Assert.assertEquals(2, scan.getUserParam().size());
                    }
                }
            }
        }

        // Second pass. See if it keeps adding the referenceable ones.
        spectra = mz.getRun().getSpectrumList().getSpectrum();
        for (Spectrum spectrum : spectra) {
            if (spectrum.getId().equalsIgnoreCase("scan=21")) {
                // skip this spectrum, since it does not contain scan data
            } else {
                if (spectrum.getScanList() != null) {
                    Scan scan = spectrum.getScanList().getScan().get(0); // get first scan
                    if(scan != null) {
                        Assert.assertEquals(5, scan.getCvParam().size());
                        Assert.assertEquals(2, scan.getUserParam().size());
                    }
                }
            }
        }
    }

    public void testReferenceableParamGroupMarshalling() {
        URL url = this.getClass().getClassLoader().getResource("sample_small.mzML");
        assertNotNull(url);

        MzMLUnmarshaller um = new MzMLUnmarshaller(url);

        MzML mz = um.unmarshall();

        MzMLMarshaller marshaller = new MzMLMarshaller();
        try {
            // Try a single pass.
            File output = File.createTempFile("tempTestReferenceableParamGroupMarshalling", ".mzML");
            output.deleteOnExit();
//            File output = new File("tempTestReferenceableParamGroupMarshalling.mzML");
            BufferedWriter bw = new BufferedWriter(new FileWriter(output));
            marshaller.marshall(mz, bw);
            bw.flush();
            bw.close();
            um = new MzMLUnmarshaller(output);
            mz = um.unmarshall();
            // First pass.
            List<Spectrum> spectra = mz.getRun().getSpectrumList().getSpectrum();
            for (Spectrum spectrum : spectra) {
                if (spectrum.getId().equalsIgnoreCase("scan=21")) {
                    // skip this spectrum, since it does not contain scan data
                } else {
                    if (spectrum.getScanList() != null) {
                        Scan scan = spectrum.getScanList().getScan().get(0); // get first scan
                        if(scan != null) {
                            Assert.assertEquals(5, scan.getCvParam().size());
                            Assert.assertEquals(2, scan.getUserParam().size());
                            Assert.assertEquals(2, scan.getReferenceableParamGroupRef().get(0).getReferenceableParamGroup().getCvParam().size());
                            Assert.assertEquals(1, scan.getReferenceableParamGroupRef().get(0).getReferenceableParamGroup().getUserParam().size());
                        }
                    }
                }
            }

            // Second pass. See if it keeps adding the referenceable ones.
            spectra = mz.getRun().getSpectrumList().getSpectrum();
            for (Spectrum spectrum : spectra) {
                if (spectrum.getId().equalsIgnoreCase("scan=21")) {
                    // skip this spectrum, since it does not contain scan data
                } else {
                    if (spectrum.getScanList() != null) {
                        Scan scan = spectrum.getScanList().getScan().get(0); // get first scan
                        if(scan != null) {
                            Assert.assertEquals(5, scan.getCvParam().size());
                            Assert.assertEquals(2, scan.getUserParam().size());
                            Assert.assertEquals(2, scan.getReferenceableParamGroupRef().get(0).getReferenceableParamGroup().getCvParam().size());
                            Assert.assertEquals(1, scan.getReferenceableParamGroupRef().get(0).getReferenceableParamGroup().getUserParam().size());
                        }
                    }
                }
            }

            // Try a double pass.
            output = File.createTempFile("tempTestReferenceableParamGroupMarshalling", "mzML");
            output.deleteOnExit();
            bw = new BufferedWriter(new FileWriter(output));
            marshaller.marshall(mz, bw);
            bw.flush();
            bw.close();

            File output2 = File.createTempFile("tempTestReferenceableParamGroupMarshalling2", "mzML");
            output2.deleteOnExit();
            bw = new BufferedWriter(new FileWriter(output2));
            marshaller.marshall(mz, bw);
            bw.flush();
            bw.close();

            // Check wether this one still makes sense.
            um = new MzMLUnmarshaller(output2);
            mz = um.unmarshall();
            // First pass.
            spectra = mz.getRun().getSpectrumList().getSpectrum();
            for (Spectrum spectrum : spectra) {
                if (spectrum.getId().equalsIgnoreCase("scan=21")) {
                    // skip this spectrum, since it does not contain scan data
                } else {
                    if (spectrum.getScanList() != null) {
                        Scan scan = spectrum.getScanList().getScan().get(0); // get first scan
                        if(scan != null) {
                            Assert.assertEquals(5, scan.getCvParam().size());
                            Assert.assertEquals(2, scan.getUserParam().size());
                            Assert.assertEquals(2, scan.getReferenceableParamGroupRef().get(0).getReferenceableParamGroup().getCvParam().size());
                            Assert.assertEquals(1, scan.getReferenceableParamGroupRef().get(0).getReferenceableParamGroup().getUserParam().size());
                        }
                    }
                }
            }

            // Second pass. See if it keeps adding the referenceable ones.
            spectra = mz.getRun().getSpectrumList().getSpectrum();
            for (Spectrum spectrum : spectra) {
                if (spectrum.getId().equalsIgnoreCase("scan=21")) {
                    // skip this spectrum, since it does not contain scan data
                } else {
                    if (spectrum.getScanList() != null) {
                        Scan scan = spectrum.getScanList().getScan().get(0); // get first scan
                        if(scan != null) {
                            Assert.assertEquals(5, scan.getCvParam().size());
                            Assert.assertEquals(2, scan.getUserParam().size());
                            Assert.assertEquals(2, scan.getReferenceableParamGroupRef().get(0).getReferenceableParamGroup().getCvParam().size());
                            Assert.assertEquals(1, scan.getReferenceableParamGroupRef().get(0).getReferenceableParamGroup().getUserParam().size());
                        }
                    }
                }
            }
        } catch (IOException ioe) {
            ioe.printStackTrace();
            fail("IOException when creating a temproray file to marshall to!\n" + ioe.getMessage());
        }
    }

    public void testReferenceableParamGroupMarshallingWithoutSpectrumCaching() {
        URL url = this.getClass().getClassLoader().getResource("sample_small.mzML");
        assertNotNull(url);

        MzMLUnmarshaller um = new MzMLUnmarshaller(url, false);

        MzML mz = um.unmarshall();

        MzMLMarshaller marshaller = new MzMLMarshaller();
        try {
            // Try a single pass.
            File output = File.createTempFile("testReferenceableParamGroupMarshallingWithoutSpectrumCaching", ".mzML");
            output.deleteOnExit();
            BufferedWriter bw = new BufferedWriter(new FileWriter(output));
            marshaller.marshall(mz, bw);
            bw.flush();
            bw.close();
            um = new MzMLUnmarshaller(output, false, null);
            mz = um.unmarshall();
            // First pass.
            List<Spectrum> spectra = mz.getRun().getSpectrumList().getSpectrum();
            for (Spectrum spectrum : spectra) {
                if (spectrum.getId().equalsIgnoreCase("scan=21")) {
                    // skip this spectrum, since it does not contain scan data
                } else {
                    if (spectrum.getScanList() != null) {
                        Scan scan = spectrum.getScanList().getScan().get(0); // get first scan
                        if(scan != null) {
                            Assert.assertEquals(5, scan.getCvParam().size());
                            Assert.assertEquals(2, scan.getUserParam().size());
                            Assert.assertEquals(2, scan.getReferenceableParamGroupRef().get(0).getReferenceableParamGroup().getCvParam().size());
                            Assert.assertEquals(1, scan.getReferenceableParamGroupRef().get(0).getReferenceableParamGroup().getUserParam().size());
                        }
                    }
                }
            }

            // Second pass. See if it keeps adding the referenceable ones.
            spectra = mz.getRun().getSpectrumList().getSpectrum();
            for (Spectrum spectrum : spectra) {
                if (spectrum.getId().equalsIgnoreCase("scan=21")) {
                    // skip this spectrum, since it does not contain scan data
                } else {
                    if (spectrum.getScanList() != null) {
                        Scan scan = spectrum.getScanList().getScan().get(0); // get first scan
                        if(scan != null) {
                            Assert.assertEquals(5, scan.getCvParam().size());
                            Assert.assertEquals(2, scan.getUserParam().size());
                            Assert.assertEquals(2, scan.getReferenceableParamGroupRef().get(0).getReferenceableParamGroup().getCvParam().size());
                            Assert.assertEquals(1, scan.getReferenceableParamGroupRef().get(0).getReferenceableParamGroup().getUserParam().size());
                        }
                    }
                }
            }

            // Try a double pass.
            output = File.createTempFile("tempTestReferenceableParamGroupMarshalling", "mzML");
            output.deleteOnExit();
            bw = new BufferedWriter(new FileWriter(output));
            marshaller.marshall(mz, bw);
            bw.flush();
            bw.close();

            File output2 = File.createTempFile("tempTestReferenceableParamGroupMarshalling2", "mzML");
            output2.deleteOnExit();
            bw = new BufferedWriter(new FileWriter(output2));
            marshaller.marshall(mz, bw);
            bw.flush();
            bw.close();

            // Check wether this one still makes sense.
            um = new MzMLUnmarshaller(output2, false, null);
            mz = um.unmarshall();
            // First pass.
            spectra = mz.getRun().getSpectrumList().getSpectrum();
            for (Spectrum spectrum : spectra) {
                if (spectrum.getId().equalsIgnoreCase("scan=21")) {
                    // skip this spectrum, since it does not contain scan data
                } else {
                    if (spectrum.getScanList() != null) {
                        Scan scan = spectrum.getScanList().getScan().get(0); // get first scan
                        if(scan != null) {
                            Assert.assertEquals(5, scan.getCvParam().size());
                            Assert.assertEquals(2, scan.getUserParam().size());
                            Assert.assertEquals(2, scan.getReferenceableParamGroupRef().get(0).getReferenceableParamGroup().getCvParam().size());
                            Assert.assertEquals(1, scan.getReferenceableParamGroupRef().get(0).getReferenceableParamGroup().getUserParam().size());
                        }
                    }
                }
            }

            // Second pass. See if it keeps adding the referenceable ones.
            spectra = mz.getRun().getSpectrumList().getSpectrum();
            for (Spectrum spectrum : spectra) {
                if (spectrum.getId().equalsIgnoreCase("scan=21")) {
                    // skip this spectrum, since it does not contain scan data
                } else {
                    if (spectrum.getScanList() != null) {
                        Scan scan = spectrum.getScanList().getScan().get(0); // get first scan
                        if(scan != null) {
                            Assert.assertEquals(5, scan.getCvParam().size());
                            Assert.assertEquals(2, scan.getUserParam().size());
                            Assert.assertEquals(2, scan.getReferenceableParamGroupRef().get(0).getReferenceableParamGroup().getCvParam().size());
                            Assert.assertEquals(1, scan.getReferenceableParamGroupRef().get(0).getReferenceableParamGroup().getUserParam().size());
                        }
                    }
                }
            }
        } catch (IOException ioe) {
            ioe.printStackTrace();
            fail("IOException when creating a temproray file to marshall to!\n" + ioe.getMessage());
        }
    }

}
