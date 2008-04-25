//
// IOTest.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#include "IO.hpp"
#include "Diff.hpp"
#include "utility/misc/unit.hpp"
#include <iostream>
#include <iterator>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::minimxml;
using namespace pwiz::msdata;
using boost::shared_ptr;
using boost::iostreams::stream_offset;


ostream* os_ = 0;


template <typename object_type>
void testObject(const object_type& a)
{
    if (os_) *os_ << "testObject(): " << typeid(a).name() << endl;

    // write 'a' out to a stream

    ostringstream oss;
    XMLWriter writer(oss);
    IO::write(writer, a);
    if (os_) *os_ << oss.str() << endl;

    // read 'b' in from stream

    object_type b; 
    istringstream iss(oss.str());
    IO::read(iss, b);

    // compare 'a' and 'b'

    Diff<object_type> diff(a,b);
    if (diff && os_) *os_ << "diff:\n" << diff << endl;
    unit_assert(!diff);
}


void testCV()
{
    CV a;
    a.URI = "abcd";
    a.id = "efgh";
    a.fullName = "ijkl";
    a.version = "mnop";

    testObject(a);
}


void testUserParam()
{
    UserParam a;
    a.name = "abcd";
    a.value = "efgh";
    a.type = "ijkl";

    testObject(a);
}


void testCVParam()
{
    CVParam a(MS_m_z, "810.48", MS_mass_unit);

    testObject(a);
}


void testParamGroup()
{
    ParamGroup a("pg");
    a.userParams.push_back(UserParam("goober", "goo", "peanuts"));
    a.cvParams.push_back(CVParam(MS_ionization_type, "420"));
    a.cvParams.push_back(CVParam(MS_m_z, "666"));
    a.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pgp")));
    testObject(a);
}


template <typename object_type>
void testNamedParamContainer()
{
    object_type a;
    a.userParams.push_back(UserParam("goober", "goo", "peanuts"));
    a.cvParams.push_back(CVParam(MS_ionization_type, "420"));
    a.cvParams.push_back(CVParam(MS_m_z, "666"));
    a.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pgp")));
    testObject(a);
}


void testSourceFile()
{
    SourceFile a("id123", "name456", "location789");
    a.userParams.push_back(UserParam("goober", "goo", "peanuts"));
    a.cvParams.push_back(CVParam(MS_ionization_type, "420"));
    a.cvParams.push_back(CVParam(MS_m_z, "666"));
    a.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pgp")));
    testObject(a);
}


void testFileDescription()
{
    FileDescription a;
    a.fileContent.cvParams.push_back(MS_MSn_spectrum);
    
    SourceFilePtr sf(new SourceFile("1", "tiny1.RAW", "file://F:/data/Exp01"));
    sf->cvParams.push_back(MS_Xcalibur_RAW_file);
    sf->cvParams.push_back(MS_SHA_1);
    a.sourceFilePtrs.push_back(sf);

    Contact contact;
    contact.cvParams.push_back(CVParam(MS_contact_name, "Darren"));
    a.contacts.push_back(contact); 

    testObject(a); 
}


void testSample()
{
    Sample a("id123", "name456");
    a.userParams.push_back(UserParam("goober", "goo", "peanuts"));
    a.cvParams.push_back(CVParam(MS_ionization_type, "420"));
    a.cvParams.push_back(CVParam(MS_m_z, "666"));
    a.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pgp")));
    testObject(a);
}


void testSource()
{
    Source a;
    //Analyzer a;
    //Detector a;
    a.order = 1;
    a.userParams.push_back(UserParam("goober", "goo", "peanuts"));
    a.cvParams.push_back(CVParam(MS_ionization_type, "420"));
    a.cvParams.push_back(CVParam(MS_m_z, "666"));
    a.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pgp")));
    testObject(a);
}


void testComponentList()
{
    ComponentList a;
    a.source.order = 1;
    a.analyzer.order = 2;
    a.detector.order = 3;
    a.source.cvParams.push_back(MS_nanoelectrospray);
    a.analyzer.cvParams.push_back(MS_quadrupole_ion_trap);
    a.detector.cvParams.push_back(MS_electron_multiplier);
    testObject(a);
}


void testSoftware()
{
    Software a;
    a.id = "goober";
    a.softwareParam = MS_ionization_type;
    a.softwareParamVersion = "4.20";
    testObject(a);
}


void testInstrumentConfiguration()
{
    InstrumentConfiguration a;
    a.id = "LCQ Deca";
    a.cvParams.push_back(MS_LCQ_Deca);
    a.cvParams.push_back(CVParam(MS_instrument_serial_number, 23433));
    a.componentList.source.order = 1;
    a.componentList.source.cvParams.push_back(MS_nanoelectrospray);
    a.componentList.analyzer.order = 2;
    a.componentList.analyzer.cvParams.push_back(MS_quadrupole_ion_trap);
    a.componentList.detector.order = 3;
    a.componentList.detector.cvParams.push_back(MS_electron_multiplier);
    a.softwarePtr = SoftwarePtr(new Software("XCalibur"));
    testObject(a);
}


void testProcessingMethod()
{
    ProcessingMethod a;
    a.order = 420;
    a.cvParams.push_back(CVParam(MS_deisotoping, false)); 
    a.cvParams.push_back(CVParam(MS_charge_deconvolution, false)); 
    a.cvParams.push_back(CVParam(MS_peak_picking, true)); 
    testObject(a);
}


void testDataProcessing()
{
    DataProcessing a;

    a.id = "msdata processing";
    a.softwarePtr = SoftwarePtr(new Software("msdata"));

    ProcessingMethod pm1, pm2;

    pm1.order = 420;
    pm1.cvParams.push_back(CVParam(MS_deisotoping, false)); 
    pm1.cvParams.push_back(CVParam(MS_charge_deconvolution, false)); 
    pm1.cvParams.push_back(CVParam(MS_peak_picking, true)); 

    pm2.order = 421;
    pm2.userParams.push_back(UserParam("testing"));

    a.processingMethods.push_back(pm1);
    a.processingMethods.push_back(pm2);
    
    testObject(a);
}


void testAcquisitionSettings()
{
    AcquisitionSettings a;

    a.id = "as1";
    a.instrumentConfigurationPtr = InstrumentConfigurationPtr(new InstrumentConfiguration("msdata"));

    Target t1, t2;

    t1.set(MS_m_z, 200); 
    t2.userParams.push_back(UserParam("testing"));

    a.targets.push_back(t1);
    a.targets.push_back(t2);

    a.sourceFilePtrs.push_back(SourceFilePtr(new SourceFile("sf1")));
    a.sourceFilePtrs.push_back(SourceFilePtr(new SourceFile("sf2")));
    
    testObject(a);
}


void testAcquisition()
{
    Acquisition a;

    a.number = 420;
    a.sourceFilePtr = SourceFilePtr(new SourceFile("test.raw"));
    a.spectrumID = "1234";
    a.cvParams.push_back(MS_reflectron_on);
   
    testObject(a);
}


void testAcquisitionList()
{
    AcquisitionList a;

    Acquisition a1;
    a1.number = 420;
    a1.sourceFilePtr = SourceFilePtr(new SourceFile("test.raw"));
    a1.spectrumID = "1234";
    a1.cvParams.push_back(MS_reflectron_on);

    Acquisition a2;
    a2.number = 421;
    a2.sourceFilePtr = SourceFilePtr(new SourceFile("test.mzxml"));
    a2.spectrumID = "5678";
    a1.cvParams.push_back(MS_reflectron_off);

    a.acquisitions.push_back(a1);
    a.acquisitions.push_back(a2);
    a.cvParams.push_back(MS_m_z);
   
    testObject(a);
}


void testPrecursor()
{
    Precursor a;
    
    a.spectrumID = "19";
    a.isolationWindow.cvParams.push_back(CVParam(MS_m_z, 123450));
    a.selectedIons.resize(2);
    a.selectedIons[0].cvParams.push_back(CVParam(MS_m_z, 445.34));
    a.selectedIons[1].cvParams.push_back(CVParam(MS_charge_state, 2));
    a.activation.cvParams.push_back(MS_collision_induced_dissociation);
    a.activation.cvParams.push_back(CVParam(MS_collision_energy, 35.00));
  
    testObject(a);
}


void testScan()
{
    Scan a;

    a.instrumentConfigurationPtr = InstrumentConfigurationPtr(new InstrumentConfiguration("LTQ FT"));    
    a.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("CommonMS1SpectrumParams")));
    a.cvParams.push_back(CVParam(MS_scan_time, 5.890500, MS_minute));
    a.cvParams.push_back(CVParam(MS_filter_string, "+ c NSI Full ms [ 400.00-1800.00]"));
    a.scanWindows.push_back(ScanWindow(400.0, 1800.0));

    testObject(a);
}


void testSpectrumDescription()
{
    SpectrumDescription a;

    a.cvParams.push_back(MS_centroid_mass_spectrum);
    a.cvParams.push_back(CVParam(MS_lowest_m_z_value, 320.39));
    a.cvParams.push_back(CVParam(MS_highest_m_z_value, 1003.56));
    a.cvParams.push_back(CVParam(MS_base_peak_m_z, 456.347));
    a.cvParams.push_back(CVParam(MS_base_peak_intensity, 23433));
    a.cvParams.push_back(CVParam(MS_total_ion_current, 1.66755e7));

    a.precursors.push_back(Precursor());
    a.precursors.back().spectrumID = "19";
    a.precursors.back().selectedIons.resize(1);
    a.precursors.back().selectedIons[0].cvParams.push_back(CVParam(MS_m_z, 445.34));
    a.precursors.back().selectedIons[0].cvParams.push_back(CVParam(MS_charge_state, 2));
    a.precursors.back().activation.cvParams.push_back(MS_collision_induced_dissociation);
    a.precursors.back().activation.cvParams.push_back(CVParam(MS_collision_energy, 35.00, MS_electron_volt)); 
    
    a.scan.instrumentConfigurationPtr = InstrumentConfigurationPtr(new InstrumentConfiguration("LTQ FT"));    
    a.scan.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("CommonMS2SpectrumParams")));
    a.scan.cvParams.push_back(CVParam(MS_scan_time, 5.990500, MS_minute));
    a.scan.cvParams.push_back(CVParam(MS_filter_string, "+ c d Full ms2  445.35@cid35.00 [ 110.00-905.00]"));
    a.scan.scanWindows.push_back(ScanWindow(110.0, 905.0));

    a.acquisitionList.acquisitions.push_back(Acquisition());
    a.acquisitionList.acquisitions.back().number = 420;

    testObject(a);
}


void testBinaryDataArray(const BinaryDataEncoder::Config& config)
{
    if (os_) *os_ << "testBinaryDataArray():\n";

    BinaryDataArray a;
    for (int i=0; i<10; i++) a.data.push_back(i);
    a.dataProcessingPtr = DataProcessingPtr(new DataProcessing("msdata"));

    // write 'a' out to a stream

    ostringstream oss;
    XMLWriter writer(oss);
    IO::write(writer, a, config);
    if (os_) *os_ << oss.str() << endl;

    // read 'b' in from stream

    BinaryDataArray b; 
    istringstream iss(oss.str());
    IO::read(iss, b);

    // compare 'a' and 'b'

    Diff<BinaryDataArray> diff(a,b);
    if (diff && os_) *os_ << "diff:\n" << diff << endl;
    unit_assert(!diff);
}


void testBinaryDataArray()
{
    BinaryDataEncoder::Config config;

    config.precision = BinaryDataEncoder::Precision_32;
    config.byteOrder = BinaryDataEncoder::ByteOrder_LittleEndian;
    testBinaryDataArray(config);
    
    config.precision = BinaryDataEncoder::Precision_64;
    config.byteOrder = BinaryDataEncoder::ByteOrder_LittleEndian;
    testBinaryDataArray(config);

    //config.precision = BinaryDataEncoder::Precision_64;
    //config.compression = BinaryDataEncoder::Compression_Zlib;
    //testBinaryDataArray(config);
}


void testSpectrum()
{
    if (os_) *os_ << "testSpectrum():\n";

    Spectrum a;
    
    a.index = 123;
    a.id = "goo";
    a.nativeID = "420";
    a.defaultArrayLength = 666;
    a.dataProcessingPtr = DataProcessingPtr(new DataProcessing("dp"));
    a.sourceFilePtr = SourceFilePtr(new SourceFile("sf"));
    a.binaryDataArrayPtrs.push_back(BinaryDataArrayPtr(new BinaryDataArray));
    for (size_t i=0; i<a.defaultArrayLength; i++)
        a.binaryDataArrayPtrs.back()->data.push_back(i);
    a.binaryDataArrayPtrs.back()->set(MS_m_z_array);
    a.binaryDataArrayPtrs.push_back(BinaryDataArrayPtr(new BinaryDataArray));
    for (size_t i=0; i<a.defaultArrayLength; i++)
        a.binaryDataArrayPtrs.back()->data.push_back(i*2);
    a.binaryDataArrayPtrs.back()->set(MS_intensity_array);
    a.spectrumDescription.cvParams.push_back(MS_reflectron_on);
    a.cvParams.push_back(MS_MSn_spectrum);

    // write 'a' out to a stream

    ostringstream oss;
    XMLWriter writer(oss);
    IO::write(writer, a);
    if (os_) *os_ << oss.str() << endl;

    // read 'b' in from stream

    Spectrum b; 
    istringstream iss(oss.str());
    IO::read(iss, b, IO::ReadBinaryData);
    unit_assert(b.sourceFilePosition == 0); // not -1

    // compare 'a' and 'b'

    Diff<Spectrum> diff(a,b);
    if (diff && os_) *os_ << "diff:\n" << diff << endl;
    unit_assert(!diff);

    // test IgnoreBinaryData

    Spectrum c;
    iss.seekg(0);
    IO::read(iss, c); // default = IgnoreBinaryData
    unit_assert(c.binaryDataArrayPtrs.empty());
    unit_assert(c.sourceFilePosition == 0); // not -1

    a.binaryDataArrayPtrs.clear();
    diff(a, c);
    unit_assert(!diff);
}


void testChromatogram()
{
    if (os_) *os_ << "testChromatogram():\n";

    Chromatogram a;
    
    a.index = 123;
    a.id = "goo";
    a.nativeID = "420";
    a.defaultArrayLength = 666;
    a.dataProcessingPtr = DataProcessingPtr(new DataProcessing("dp"));
    a.binaryDataArrayPtrs.push_back(BinaryDataArrayPtr(new BinaryDataArray));
    for (size_t i=0; i<a.defaultArrayLength; i++)
        a.binaryDataArrayPtrs.back()->data.push_back(i);
    a.binaryDataArrayPtrs.back()->set(MS_time_array);
    a.binaryDataArrayPtrs.push_back(BinaryDataArrayPtr(new BinaryDataArray));
    for (size_t i=0; i<a.defaultArrayLength; i++)
        a.binaryDataArrayPtrs.back()->data.push_back(i*2);
    a.binaryDataArrayPtrs.back()->set(MS_intensity_array);
    a.cvParams.push_back(MS_total_ion_chromatogram__); // TODO: fix when CV has appropriate terms

    // write 'a' out to a stream

    ostringstream oss;
    XMLWriter writer(oss);
    IO::write(writer, a);
    if (os_) *os_ << oss.str() << endl;

    // read 'b' in from stream

    Chromatogram b; 
    istringstream iss(oss.str());
    IO::read(iss, b, IO::ReadBinaryData);
    unit_assert(b.sourceFilePosition == 0); // not -1

    // compare 'a' and 'b'

    Diff<Chromatogram> diff(a,b);
    if (diff && os_) *os_ << "diff:\n" << diff << endl;
    unit_assert(!diff);

    // test IgnoreBinaryData

    Chromatogram c;
    iss.seekg(0);
    IO::read(iss, c); // default = IgnoreBinaryData
    unit_assert(c.binaryDataArrayPtrs.empty());
    unit_assert(c.sourceFilePosition == 0); // not -1

    a.binaryDataArrayPtrs.clear();
    diff(a, c);
    unit_assert(!diff);
}


void testSpectrumList()
{
    SpectrumListSimple a;

    SpectrumPtr spectrum1(new Spectrum);
    spectrum1->id = "goober";
    spectrum1->index = 0;
    spectrum1->nativeID = "420";
    spectrum1->defaultArrayLength = 666;
    spectrum1->spectrumDescription.userParams.push_back(UserParam("description1"));

    SpectrumPtr spectrum2(new Spectrum);
    spectrum2->id = "raisinet";
    spectrum2->index = 1;
    spectrum2->nativeID = "421";
    spectrum2->defaultArrayLength = 667;
    spectrum2->spectrumDescription.userParams.push_back(UserParam("description2"));
    
    a.spectra.push_back(spectrum1);
    a.spectra.push_back(spectrum2);

    testObject(a);
}


void testSpectrumListWithPositions()
{
    if (os_) *os_ << "testSpectrumListWithPositions()\n  ";

    SpectrumListSimple a;

    SpectrumPtr spectrum1(new Spectrum);
    spectrum1->id = "goober";
    spectrum1->index = 0;
    spectrum1->nativeID = "420";
    spectrum1->defaultArrayLength = 666;
    spectrum1->spectrumDescription.userParams.push_back(UserParam("description1"));

    SpectrumPtr spectrum2(new Spectrum);
    spectrum2->id = "raisinet";
    spectrum2->index = 1;
    spectrum2->nativeID = "421";
    spectrum2->defaultArrayLength = 667;
    spectrum2->spectrumDescription.userParams.push_back(UserParam("description2"));
    
    a.spectra.push_back(spectrum1);
    a.spectra.push_back(spectrum2);

    ostringstream oss;
    XMLWriter writer(oss);
    vector<stream_offset> positions;
    IO::write(writer, a, BinaryDataEncoder::Config(), &positions);

    if (os_)
    {
        copy(positions.begin(), positions.end(), ostream_iterator<stream_offset>(*os_, " "));
        *os_ << endl << oss.str() << endl;
        *os_ << "\n\n";
    }

    unit_assert(positions.size() == 2);
    unit_assert(positions[0] == 27);
    unit_assert(positions[1] == 225);
}


void testChromatogramList()
{
    ChromatogramListSimple a;

    ChromatogramPtr chromatogram1(new Chromatogram);
    chromatogram1->id = "goober";
    chromatogram1->index = 0;
    chromatogram1->nativeID = "420";
    chromatogram1->defaultArrayLength = 666;

    ChromatogramPtr chromatogram2(new Chromatogram);
    chromatogram2->id = "raisinet";
    chromatogram2->index = 1;
    chromatogram2->nativeID = "421";
    chromatogram2->defaultArrayLength = 667;
    
    a.chromatograms.push_back(chromatogram1);
    a.chromatograms.push_back(chromatogram2);

    testObject(a);
}


void testChromatogramListWithPositions()
{
    if (os_) *os_ << "testChromatogramListWithPositions()\n  ";

    ChromatogramListSimple a;

    ChromatogramPtr chromatogram1(new Chromatogram);
    chromatogram1->id = "goober";
    chromatogram1->index = 0;
    chromatogram1->nativeID = "420";
    chromatogram1->defaultArrayLength = 666;

    ChromatogramPtr chromatogram2(new Chromatogram);
    chromatogram2->id = "raisinet";
    chromatogram2->index = 1;
    chromatogram2->nativeID = "421";
    chromatogram2->defaultArrayLength = 667;
    
    a.chromatograms.push_back(chromatogram1);
    a.chromatograms.push_back(chromatogram2);

    ostringstream oss;
    XMLWriter writer(oss);
    vector<stream_offset> positions;
    IO::write(writer, a, BinaryDataEncoder::Config(), &positions);

    if (os_)
    {
        copy(positions.begin(), positions.end(), ostream_iterator<stream_offset>(*os_, " "));
        *os_ << endl << oss.str() << endl;
        *os_ << "\n\n";
    }

    unit_assert(positions.size() == 2);
    unit_assert(positions[0] == 31);
    unit_assert(positions[1] == 128);
}


void testRun()
{
    if (os_) *os_ << "testRun():\n";

    Run a;
    
    a.id = "goober";
    a.instrumentConfigurationPtr = InstrumentConfigurationPtr(new InstrumentConfiguration("instrumentConfiguration"));
    a.samplePtr = SamplePtr(new Sample("sample"));
    a.startTimeStamp = "20 April 2004 4:20pm";  
    a.sourceFilePtrs.push_back(SourceFilePtr(new SourceFile("sf1")));
    a.sourceFilePtrs.push_back(SourceFilePtr(new SourceFile("sf2")));

    // spectrumList

    shared_ptr<SpectrumListSimple> spectrumListSimple(new SpectrumListSimple);

    SpectrumPtr spectrum1(new Spectrum);
    spectrum1->id = "goober";
    spectrum1->index = 0;
    spectrum1->nativeID = "420";
    spectrum1->defaultArrayLength = 666;
    spectrum1->spectrumDescription.userParams.push_back(UserParam("description1"));

    SpectrumPtr spectrum2(new Spectrum);
    spectrum2->id = "raisinet";
    spectrum2->index = 1;
    spectrum2->nativeID = "421";
    spectrum2->defaultArrayLength = 667;
    spectrum2->spectrumDescription.userParams.push_back(UserParam("description2"));
    
    spectrumListSimple->spectra.push_back(spectrum1);
    spectrumListSimple->spectra.push_back(spectrum2);

    a.spectrumListPtr = spectrumListSimple;

    // chromatogramList

    shared_ptr<ChromatogramListSimple> chromatogramListSimple(new ChromatogramListSimple);

    ChromatogramPtr chromatogram1(new Chromatogram);
    chromatogram1->id = "goober";
    chromatogram1->index = 0;
    chromatogram1->nativeID = "420";
    chromatogram1->defaultArrayLength = 666;

    ChromatogramPtr chromatogram2(new Chromatogram);
    chromatogram2->id = "raisinet";
    chromatogram2->index = 1;
    chromatogram2->nativeID = "421";
    chromatogram2->defaultArrayLength = 667;
    
    chromatogramListSimple->chromatograms.push_back(chromatogram1);
    chromatogramListSimple->chromatograms.push_back(chromatogram2);

    a.chromatogramListPtr = chromatogramListSimple;

    // write 'a' out to a stream

    ostringstream oss;
    XMLWriter writer(oss);
    IO::write(writer, a);
    if (os_) *os_ << oss.str() << endl;

    // read 'b' in from stream, ignoring SpectrumList (default)

    Run b;
    istringstream iss(oss.str());
    IO::read(iss, b, IO::IgnoreSpectrumList); // IO::IgnoreSpectrumList 

    // compare 'a' and 'b'

    Diff<Run> diff(a,b);
    if (diff && os_) *os_ << "diff:\n" << diff << endl;
    unit_assert(diff);
    unit_assert(diff.a_b.spectrumListPtr.get());
    unit_assert(diff.a_b.spectrumListPtr->size() == 1);
    unit_assert(diff.a_b.spectrumListPtr->spectrum(0)->userParams.size() == 1);

    // read 'c' in from stream, reading SpectrumList

    Run c; 
    iss.seekg(0);
    IO::read(iss, c, IO::ReadSpectrumList);

    // compare 'a' and 'c'

    diff(a,c);
    if (diff && os_) *os_ << "diff:\n" << diff << endl;
    unit_assert(!diff);

    // remove SpectrumList and ChromatogramList from a, and compare to b 

    a.spectrumListPtr.reset();
    a.chromatogramListPtr.reset();
    diff(a, b);
    unit_assert(!diff);
}


void initializeTestData(MSData& msd)
{
    msd.accession = "test accession";
    msd.id = "test id";
    msd.version = "test version";

    // cvList

    msd.cvs.resize(1);
    CV& cv = msd.cvs.front();
    cv.URI = "http://psidev.sourceforge.net/ms/xml/mzdata/psi-ms.2.0.2.obo"; 
    cv.id = "MS";
    cv.fullName = "Proteomics Standards Initiative Mass Spectrometry Ontology";
    cv.version = "2.0.2";

    // fileDescription

    FileContent& fc = msd.fileDescription.fileContent;
    fc.cvParams.push_back(MS_MSn_spectrum);
    fc.userParams.push_back(UserParam("number of cats", "4"));

    SourceFilePtr sfp(new SourceFile);
    sfp->id = "1";
    sfp->name = "tiny1.RAW";
    sfp->location = "file://F:/data/Exp01";
    sfp->cvParams.push_back(MS_Xcalibur_RAW_file);
    sfp->cvParams.push_back(CVParam(MS_SHA_1,"71be39fb2700ab2f3c8b2234b91274968b6899b1"));
    msd.fileDescription.sourceFilePtrs.push_back(sfp);

    msd.fileDescription.contacts.resize(1);
    Contact& contact = msd.fileDescription.contacts.front();
    contact.cvParams.push_back(CVParam(MS_contact_name, "William Pennington"));
    contact.cvParams.push_back(CVParam(MS_contact_address, 
                               "Higglesworth University, 12 Higglesworth Avenue, 12045, HI, USA"));
	contact.cvParams.push_back(CVParam(MS_contact_URL, "http://www.higglesworth.edu/"));
	contact.cvParams.push_back(CVParam(MS_contact_email, "wpennington@higglesworth.edu"));

    // paramGroupList

    ParamGroupPtr pg1(new ParamGroup);
    pg1->id = "CommonMS1SpectrumParams";
    pg1->cvParams.push_back(MS_positive_scan);
    pg1->cvParams.push_back(MS_full_scan);
    msd.paramGroupPtrs.push_back(pg1);

    ParamGroupPtr pg2(new ParamGroup);
    pg2->id = "CommonMS2SpectrumParams";
    pg2->cvParams.push_back(MS_positive_scan);
    pg2->cvParams.push_back(MS_full_scan);
    msd.paramGroupPtrs.push_back(pg2);

    // sampleList

    SamplePtr samplePtr(new Sample);
    samplePtr->id = "1";
    samplePtr->name = "Sample1";
    msd.samplePtrs.push_back(samplePtr);

    // instrumentConfigurationList

    InstrumentConfigurationPtr instrumentConfigurationPtr(new InstrumentConfiguration);
    instrumentConfigurationPtr->id = "LCQ Deca";
    instrumentConfigurationPtr->cvParams.push_back(MS_LCQ_Deca);
    instrumentConfigurationPtr->cvParams.push_back(CVParam(MS_instrument_serial_number,"23433"));
    Source& source = instrumentConfigurationPtr->componentList.source;
    source.order = 1;
    source.cvParams.push_back(MS_nanoelectrospray);
    Analyzer& analyzer = instrumentConfigurationPtr->componentList.analyzer;
    analyzer.order = 2;
    analyzer.cvParams.push_back(MS_quadrupole_ion_trap);
    Detector& detector = instrumentConfigurationPtr->componentList.detector;
    detector.order = 3;
    detector.cvParams.push_back(MS_electron_multiplier);

    SoftwarePtr softwareXcalibur(new Software);
    softwareXcalibur->id = "Xcalibur";
    softwareXcalibur->softwareParam = MS_Xcalibur;
    softwareXcalibur->softwareParamVersion = "2.0.5";
    instrumentConfigurationPtr->softwarePtr = softwareXcalibur;

    msd.instrumentConfigurationPtrs.push_back(instrumentConfigurationPtr);

    // softwareList

    SoftwarePtr softwareBioworks(new Software);
    softwareBioworks->id = "Bioworks";
    softwareBioworks->softwareParam = MS_Bioworks;
    softwareBioworks->softwareParamVersion = "3.3.1 sp1";
     
    SoftwarePtr software_pwiz(new Software);
    software_pwiz->id = "pwiz";
    software_pwiz->softwareParam = MS_pwiz;
    software_pwiz->softwareParamVersion = "1.0";

    msd.softwarePtrs.push_back(softwareBioworks);
    msd.softwarePtrs.push_back(software_pwiz);
    msd.softwarePtrs.push_back(softwareXcalibur);

    // dataProcessingList

    DataProcessingPtr dpXcalibur(new DataProcessing);
    dpXcalibur->id = "Xcalibur Processing";
    dpXcalibur->softwarePtr = softwareXcalibur;
    
    ProcessingMethod procXcal;
    procXcal.order = 1;
    procXcal.cvParams.push_back(CVParam(MS_deisotoping, false));
    procXcal.cvParams.push_back(CVParam(MS_charge_deconvolution, false));
    procXcal.cvParams.push_back(CVParam(MS_peak_picking, true));

    dpXcalibur->processingMethods.push_back(procXcal);

    DataProcessingPtr dp_msconvert(new DataProcessing);
    dp_msconvert->id = "pwiz conversion";
    dp_msconvert->softwarePtr = software_pwiz;

    ProcessingMethod proc_msconvert;
    proc_msconvert.order = 2;
    proc_msconvert.cvParams.push_back(MS_Conversion_to_mzML);

    dp_msconvert->processingMethods.push_back(proc_msconvert);
 
    msd.dataProcessingPtrs.push_back(dpXcalibur);
    msd.dataProcessingPtrs.push_back(dp_msconvert);

    AcquisitionSettingsPtr as1(new AcquisitionSettings("as1"));
    as1->instrumentConfigurationPtr = instrumentConfigurationPtr;
    as1->sourceFilePtrs.push_back(SourceFilePtr(new SourceFile("SF2", "parameters.par", "file:///C:/settings/")));
    Target t1;
    t1.set(MS_m_z, 1000);
    Target t2;
    t2.set(MS_m_z, 1200);
    as1->targets.push_back(t1);
    as1->targets.push_back(t2);
    msd.acquisitionSettingsPtrs.push_back(as1);

    // run

    msd.run.id = "Exp01";
    msd.run.instrumentConfigurationPtr = instrumentConfigurationPtr;
    msd.run.samplePtr = samplePtr;
    msd.run.startTimeStamp = "2007-06-27T15:23:45.00035";
    msd.run.sourceFilePtrs.push_back(sfp);

    shared_ptr<SpectrumListSimple> spectrumList(new SpectrumListSimple);
    msd.run.spectrumListPtr = spectrumList;

    spectrumList->spectra.push_back(SpectrumPtr(new Spectrum));
    spectrumList->spectra.push_back(SpectrumPtr(new Spectrum));

    Spectrum& s19 = *spectrumList->spectra[0];
    s19.id = "S19";
    s19.index = 0;
    s19.nativeID = "19";
    s19.defaultArrayLength = 10;
    s19.cvParams.push_back(MS_MSn_spectrum);
    s19.set(MS_ms_level, 1);
    s19.spectrumDescription.cvParams.push_back(MS_centroid_mass_spectrum);
    s19.spectrumDescription.cvParams.push_back(CVParam(MS_lowest_m_z_value, 400.39));
    s19.spectrumDescription.cvParams.push_back(CVParam(MS_highest_m_z_value, 1795.56));
    s19.spectrumDescription.cvParams.push_back(CVParam(MS_base_peak_m_z, 445.347));
    s19.spectrumDescription.cvParams.push_back(CVParam(MS_base_peak_intensity, 120053));
    s19.spectrumDescription.cvParams.push_back(CVParam(MS_total_ion_current, 1.66755e+007));
    s19.spectrumDescription.scan.instrumentConfigurationPtr = instrumentConfigurationPtr;
    s19.spectrumDescription.scan.paramGroupPtrs.push_back(pg1);
    s19.spectrumDescription.scan.cvParams.push_back(CVParam(MS_scan_time, 5.890500, MS_minute));
    s19.spectrumDescription.scan.cvParams.push_back(CVParam(MS_filter_string, "+ c NSI Full ms [ 400.00-1800.00]"));
    s19.spectrumDescription.scan.scanWindows.resize(1);
    ScanWindow& window = s19.spectrumDescription.scan.scanWindows.front();
    window.cvParams.push_back(CVParam(MS_scan_m_z_lower_limit, 400.000000));
    window.cvParams.push_back(CVParam(MS_scan_m_z_upper_limit, 1800.000000));

    BinaryDataArrayPtr s19_mz(new BinaryDataArray);
    s19_mz->dataProcessingPtr = dpXcalibur;
    s19_mz->cvParams.push_back(MS_m_z_array);
    s19_mz->data.resize(10);
    for (int i=0; i<10; i++)
        s19_mz->data[i] = i;

    BinaryDataArrayPtr s19_intensity(new BinaryDataArray);
    s19_intensity->dataProcessingPtr = dpXcalibur;
    s19_intensity->cvParams.push_back(MS_intensity_array);
    s19_intensity->data.resize(10);
    for (int i=0; i<10; i++)
        s19_intensity->data[i] = 10-i;

    s19.binaryDataArrayPtrs.push_back(s19_mz);
    s19.binaryDataArrayPtrs.push_back(s19_intensity);

    Spectrum& s20 = *spectrumList->spectra[1];
    s20.id = "S20";
    s20.index = 1;
    s20.nativeID = "20";
    s20.defaultArrayLength = 10;

    s20.cvParams.push_back(MS_MSn_spectrum);
    s20.set(MS_ms_level, 2);

    s20.spectrumDescription.cvParams.push_back(MS_centroid_mass_spectrum);
    s20.spectrumDescription.cvParams.push_back(CVParam(MS_lowest_m_z_value, 320.39));
    s20.spectrumDescription.cvParams.push_back(CVParam(MS_highest_m_z_value, 1003.56));
    s20.spectrumDescription.cvParams.push_back(CVParam(MS_base_peak_m_z, 456.347));
    s20.spectrumDescription.cvParams.push_back(CVParam(MS_base_peak_intensity, 23433));
    s20.spectrumDescription.cvParams.push_back(CVParam(MS_total_ion_current, 1.66755e+007));

    s20.spectrumDescription.precursors.resize(1);
    Precursor& precursor = s20.spectrumDescription.precursors.front();
    precursor.spectrumID= s19.id;
    precursor.selectedIons.resize(1);
    precursor.selectedIons[0].cvParams.push_back(CVParam(MS_m_z, 445.34));
    precursor.selectedIons[0].cvParams.push_back(CVParam(MS_charge_state, 2));
    precursor.activation.cvParams.push_back(MS_collision_induced_dissociation);
    precursor.activation.cvParams.push_back(CVParam(MS_collision_energy, 35.00, MS_electron_volt));

    s20.spectrumDescription.scan.instrumentConfigurationPtr = instrumentConfigurationPtr;
    s20.spectrumDescription.scan.paramGroupPtrs.push_back(pg2);
    s20.spectrumDescription.scan.cvParams.push_back(CVParam(MS_scan_time, 5.990500, MS_minute));
    s20.spectrumDescription.scan.cvParams.push_back(CVParam(MS_filter_string, "+ c d Full ms2  445.35@cid35.00 [ 110.00-905.00]"));
    s20.spectrumDescription.scan.scanWindows.resize(1);
    ScanWindow& window2 = s20.spectrumDescription.scan.scanWindows.front();
    window2.cvParams.push_back(CVParam(MS_scan_m_z_lower_limit, 110.000000));
    window2.cvParams.push_back(CVParam(MS_scan_m_z_upper_limit, 905.000000));

    BinaryDataArrayPtr s20_mz(new BinaryDataArray);
    s20_mz->dataProcessingPtr = dpXcalibur;
    s20_mz->cvParams.push_back(MS_m_z_array);
    s20_mz->data.resize(10);
    for (int i=0; i<10; i++)
        s20_mz->data[i] = i;

    BinaryDataArrayPtr s20_intensity(new BinaryDataArray);
    s20_intensity->dataProcessingPtr = dpXcalibur;
    s20_intensity->cvParams.push_back(MS_intensity_array);
    s20_intensity->data.resize(10);
    for (int i=0; i<10; i++)
        s20_intensity->data[i] = 10-i;

    s20.binaryDataArrayPtrs.push_back(s20_mz);
    s20.binaryDataArrayPtrs.push_back(s20_intensity);

    // chromatograms

    shared_ptr<ChromatogramListSimple> chromatogramList(new ChromatogramListSimple);
    msd.run.chromatogramListPtr = chromatogramList;

    chromatogramList->chromatograms.push_back(ChromatogramPtr(new Chromatogram));

    Chromatogram& tic = *chromatogramList->chromatograms[0];
    tic.id = "tic";
    tic.index = 0;
    tic.nativeID = "tic";
    tic.defaultArrayLength = 10;
    tic.cvParams.push_back(MS_total_ion_chromatogram__);

    BinaryDataArrayPtr tic_time(new BinaryDataArray);
    tic_time->dataProcessingPtr = dp_msconvert;
    tic_time->cvParams.push_back(MS_time_array);
    tic_time->data.resize(10);
    for (int i=0; i<10; i++)
        tic_time->data[i] = i;

    BinaryDataArrayPtr tic_intensity(new BinaryDataArray);
    tic_intensity->dataProcessingPtr = dp_msconvert;
    tic_intensity->cvParams.push_back(MS_intensity_array);
    tic_intensity->data.resize(10);
    for (int i=0; i<10; i++)
        tic_intensity->data[i] = 10-i;

    tic.binaryDataArrayPtrs.push_back(tic_time);
    tic.binaryDataArrayPtrs.push_back(tic_intensity);
}


void testMSData()
{
    if (os_) *os_ << "testMSData():\n";

    MSData a;
    initializeTestData(a);

    // write 'a' out to a stream

    ostringstream oss;
    XMLWriter writer(oss);
    IO::write(writer, a);
    if (os_) *os_ << oss.str() << endl;

    // read 'b' in from stream, ignoring SpectrumList (default)

    MSData b;
    istringstream iss(oss.str());
    IO::read(iss, b); // IO::IgnoreSpectrumList

    // compare 'a' and 'b'

    Diff<MSData> diff(a,b);
    if (diff && os_) *os_ << "diff:\n" << diff << endl;
    unit_assert(diff);
    unit_assert(diff.a_b.run.spectrumListPtr.get());
    unit_assert(diff.a_b.run.spectrumListPtr->size() == 1);
    unit_assert(diff.a_b.run.spectrumListPtr->spectrum(0)->userParams.size() == 1);

    // read 'c' in from stream, reading SpectrumList 

    MSData c; 
    iss.seekg(0);
    IO::read(iss, c, IO::ReadSpectrumList);

    // compare 'a' and 'c'

    diff(a,c);
    if (diff && os_) *os_ << "diff:\n" << diff << endl;
    unit_assert(!diff);

    // remove SpectrumList and ChromatogramList from a, and compare to b 

    a.run.spectrumListPtr.reset();
    a.run.chromatogramListPtr.reset();
    diff(a, b);
    unit_assert(!diff);
}


void test()
{
    testCV();
    testUserParam();
    testCVParam();
    testParamGroup();
    testNamedParamContainer<FileContent>();
    testSourceFile();
    testNamedParamContainer<Contact>();
    testFileDescription();
    testSample();
    testSource();
    testComponentList(); 
    testSoftware();
    testInstrumentConfiguration();
    testProcessingMethod();
    testDataProcessing();
    testNamedParamContainer<Target>();
    testAcquisitionSettings();
    testAcquisition();
    testAcquisitionList();
    testNamedParamContainer<IsolationWindow>();
    testNamedParamContainer<SelectedIon>();
    testNamedParamContainer<Activation>();
    testPrecursor();
    testNamedParamContainer<ScanWindow>();
    testScan();
    testSpectrumDescription();
    testBinaryDataArray();
    testSpectrum();
    testChromatogram();
    testSpectrumList();
    testSpectrumListWithPositions();
    testChromatogramList();
    testChromatogramListWithPositions();
    testRun();
    testMSData();
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        if (os_) *os_ << "ok\n";
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n"; 
    }
    
    return 1;
}

