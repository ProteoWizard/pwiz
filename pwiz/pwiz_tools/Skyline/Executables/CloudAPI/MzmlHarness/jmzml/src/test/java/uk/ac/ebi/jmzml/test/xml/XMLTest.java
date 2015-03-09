/*
 * Date: 22/7/2008
 * Author: rcote
 * File: uk.ac.ebi.jmzml.test.xml.XMLTest
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

package uk.ac.ebi.jmzml.test.xml;

import junit.framework.TestCase;
import org.apache.log4j.Logger;
import uk.ac.ebi.jmzml.model.mzml.*;
import uk.ac.ebi.jmzml.xml.io.MzMLMarshaller;
import uk.ac.ebi.jmzml.xml.io.MzMLObjectIterator;
import uk.ac.ebi.jmzml.xml.io.MzMLUnmarshaller;

import java.net.URL;

public class XMLTest extends TestCase {

    /**
     * todo - write toString & equals & hashcode
     * todo - make class diagram
     * todo - map to database layer
     */

    Logger logger = Logger.getLogger(XMLTest.class);

    public void testXMLIndex() throws Exception {

        URL url = this.getClass().getClassLoader().getResource("tiny.pwiz.mzML");
        assertNotNull(url);

        MzMLUnmarshaller um = new MzMLUnmarshaller(url);

        DataProcessing dh = um.unmarshalFromXpath("/dataProcessingList/dataProcessing", DataProcessing.class);
        assertNotNull(dh);

        MzML mz = um.unmarshall();
        assertNotNull(mz);
        FileDescription fd = um.unmarshalFromXpath("/fileDescription", FileDescription.class);
        assertNotNull(fd);


        MzMLMarshaller mm = new MzMLMarshaller();
        String outFD = mm.marshall(fd);
        assertNotNull(outFD);
        String mzml = mm.marshall(mz);
        assertNotNull(mzml);

        int chromatogramCount = 0;
        MzMLObjectIterator<Chromatogram> iter = um.unmarshalCollectionFromXpath("/run/chromatogramList/chromatogram", Chromatogram.class);
        while (iter.hasNext()) {
            iter.next();
            chromatogramCount++;
        }
        Run run = um.unmarshalFromXpath("/run", Run.class);
        ChromatogramList chl = run.getChromatogramList(); // can not unmarshal directly because not in xxindex inclusion list

        assertEquals("Chromatogram count not equal!", chromatogramCount, chl.getCount().intValue());
        assertEquals("Chromatogram count not equal!", chromatogramCount, um.getObjectCountForXpath("/run/chromatogramList/chromatogram"));

    }

    public void testXMLUnmarshalling() throws Exception {
        URL url = this.getClass().getClassLoader().getResource("tiny.pwiz.mzML");
        assertNotNull(url);

        MzMLUnmarshaller um = new MzMLUnmarshaller(url);
        MzML mz = um.unmarshall();
        assertNotNull(mz);

        // the test file has:

        // accession="PRIDE:12345" id="urn:lsid:psidev.info:mzML.instanceDocuments.tiny.pwiz" version="1.0"
        String ac = um.getMzMLAccession();
        assertEquals("The MzML Accession is correct", "PRIDE:12345", ac);
        String version = um.getMzMLVersion();
        assertEquals("The MzML Version is correct", "1.0", version);
        String id = um.getMzMLId();
        assertEquals("The MzML ID is correct", "urn:lsid:psidev.info:mzML.instanceDocuments.tiny.pwiz", id);

        // two CVs
        //assertEquals("The cvList does not have the same number of entries as stated in its counter attribute!", mz.getCvList().getCount().intValue(), mz.getCvList().getCv().size());
        //assertEquals("The cvList doen not have as many entries as expected.", 2, mz.getCvList().getCount().intValue());
        assertEquals("The cvList doen not have as many entries as expected.", 2, mz.getCvList().getCv().size());

        // two source files
        //assertEquals("SourceFileList count does not equal real number of entries.", mz.getFileDescription().getSourceFileList().getCount().intValue(), mz.getFileDescription().getSourceFileList().getSourceFile().size());
        //assertEquals("", 2, mz.getFileDescription().getSourceFileList().getCount().intValue());
        assertEquals("", 2, mz.getFileDescription().getSourceFileList().getSourceFile().size());

        // one contact
        assertEquals("Not expected number of contacts.", 1, mz.getFileDescription().getContact().size());

        // two referencable param groups
        //assertEquals("ReferencableParamGroupList count does not equal real number of entries.", mz.getReferenceableParamGroupList().getCount().intValue(), mz.getReferenceableParamGroupList().getReferenceableParamGroup().size());
        //assertEquals("Not expected number of referencable param groups.", 2, mz.getReferenceableParamGroupList().getCount().intValue());
        assertEquals("Not expected number of referencable param groups.", 2, mz.getReferenceableParamGroupList().getReferenceableParamGroup().size());

        // one sample
        //assertEquals("SampleList count does not equal real number of entries.", mz.getSampleList().getCount().intValue(), mz.getSampleList().getSample().size());
        //assertEquals("Not expected number of samples.", 1, mz.getSampleList().getCount().intValue());
        assertEquals("Not expected number of samples.", 1, mz.getSampleList().getSample().size());

        // three software entries
        //assertEquals("SoftwareList count does not equal real number of entries.", mz.getSoftwareList().getCount().intValue(), mz.getSoftwareList().getSoftware().size());
        //assertEquals("Not expected number of softwares.", 3, mz.getSoftwareList().getCount().intValue());
        assertEquals("Not expected number of softwares.", 3, mz.getSoftwareList().getSoftware().size());

        // one scanSetting
        //assertEquals("ScanSettingList count does not equal real number of entries.", mz.getScanSettingsList().getCount().intValue(), mz.getScanSettingsList().getScanSettings().size());
        //assertEquals("Not expected number of scanSettings.", 1, mz.getScanSettingsList().getCount().intValue());
        assertEquals("Not expected number of scanSettings.", 1, mz.getScanSettingsList().getScanSettings().size());

        // one instrumentConfiguration
        //assertEquals("InstrumentConfigurationList count does not equal real number of entries.", mz.getInstrumentConfigurationList().getCount().intValue(), mz.getInstrumentConfigurationList().getInstrumentConfiguration().size());
        //assertEquals("Not expected number of InstrumentConfigurations.", 1, mz.getInstrumentConfigurationList().getCount().intValue());
        assertEquals("Not expected number of InstrumentConfigurations.", 1, mz.getInstrumentConfigurationList().getInstrumentConfiguration().size());

        // two dataProcessing entries
        //assertEquals("DataProcessingList count does not equal real number of entries.", mz.getDataProcessingList().getCount().intValue(), mz.getDataProcessingList().getDataProcessing().size());
        //assertEquals("Not expected number of data processing entries.", 2, mz.getDataProcessingList().getCount().intValue());
        assertEquals("Not expected number of data processing entries.", 2, mz.getDataProcessingList().getDataProcessing().size());

        // one instrumentConfiguration
        //assertEquals("InstrumentConfigurationList count does not equal real number of entries.", mz.getInstrumentConfigurationList().getCount().intValue(), mz.getInstrumentConfigurationList().getInstrumentConfiguration().size());
        //assertEquals("Not expected number of instrument configuration entries.", 1, mz.getInstrumentConfigurationList().getCount().intValue());
        assertEquals("Not expected number of instrument configuration entries.", 1, mz.getInstrumentConfigurationList().getInstrumentConfiguration().size());
        // note: here we test the manual modification of the JAXB generated ComponentList class to allow direct retrieval of source/analyzer/detector
        //ComponentList cpl = mz.getInstrumentConfigurationList().getInstrumentConfiguration().iterator().next().getComponentList();
        //ComponentList cpl = mz.getInstrumentConfigurationList().get(0).getComponentList();
        ComponentList cpl = mz.getInstrumentConfigurationList().getInstrumentConfiguration().get(0).getComponentList();
        assertNotNull(cpl);
        assertEquals("Not expected number of component elements!", 3, cpl.getComponents().size());
        assertEquals("Not expected number of component elements!", cpl.getCount().intValue(), cpl.getComponents().size());
        assertEquals("Not expected number of source elements!", 1, cpl.getSource().size());
        assertEquals("Not expected number of analyzer elements!", 1, cpl.getAnalyzer().size());
        assertEquals("Not expected number of detector elements!", 1, cpl.getDetector().size());


        // now check the run element in more detail
        Run run = mz.getRun();
        
        // the run has 4 spectra
        assertEquals("SpectrumList count does not equal real number of entries.", run.getSpectrumList().getCount().intValue(), run.getSpectrumList().getSpectrum().size());
        assertEquals("Not expected number of specrta.", 4, run.getSpectrumList().getCount().intValue());

        // the run has 2 chromatograms
        assertEquals("ChromatogramList count does not equal real number of entries.", run.getChromatogramList().getCount().intValue(), run.getChromatogramList().getChromatogram().size());
        assertEquals("Not expected number of chromatograms.", 2, run.getChromatogramList().getCount().intValue());

        // the default instrument configuration for this run (test if the references are correctly resolved)
        //assertEquals("The instrument configuration software does not match.", "Xcalibur", run.getDefaultInstrumentConfiguration().getSoftwareRef().getSoftware().getCvParam().get(0).getName());

        // ToDo: check the IDREF referenced elements

    }

}
