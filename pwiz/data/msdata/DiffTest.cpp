//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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

#include "Diff.hpp"
#include "TextWriter.hpp"
#include "examples.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::cv;
using namespace pwiz::data;
using namespace pwiz::data::diff_impl;
using namespace pwiz::msdata;


ostream* os_ = 0;


void testFileContent()
{
    if (os_) *os_ << "testFileContent()\n";

    FileContent a, b; 
    a.userParams.push_back(UserParam("common"));
    b.userParams.push_back(UserParam("common"));
  
    Diff<FileContent, DiffConfig> diff(a, b);
    unit_assert(!diff);

    a.userParams.push_back(UserParam("different", "1"));
    b.userParams.push_back(UserParam("different", "2"));

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);

    unit_assert(diff.a_b.userParams.size() == 1);
    unit_assert(diff.a_b.userParams[0] == UserParam("different","1"));
    unit_assert(diff.b_a.userParams.size() == 1);
    unit_assert(diff.b_a.userParams[0] == UserParam("different","2"));
}


void testSourceFile()
{
    if (os_) *os_ << "testSourceFile()\n";

    SourceFile a("id1","name1","location1"), b("id1","name1","location1"); 
    a.userParams.push_back(UserParam("common"));
    b.userParams.push_back(UserParam("common"));
  
    Diff<SourceFile, DiffConfig> diff(a, b);
    unit_assert(!diff);

    b.location = "location2";
    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


void testFileDescription()
{
    if (os_) *os_ << "testFileDescription()\n";

    FileDescription a, b;

    a.fileContent.userParams.push_back(UserParam("user param 1"));
    b.fileContent.userParams.push_back(UserParam("user param 1"));

    Contact contact1, contact2, contact3, contact4;
    contact1.cvParams.push_back(CVParam(MS_contact_name, "Darren"));
    contact2.cvParams.push_back(CVParam(MS_contact_name, "Laura Jane"));
    contact3.cvParams.push_back(CVParam(MS_contact_name, "Emma Lee"));
    contact4.cvParams.push_back(CVParam(MS_contact_name, "Isabelle Lynn"));

    // verify vector_diff_diff with differently ordered vectors
    a.contacts.push_back(contact2);
    a.contacts.push_back(contact1);
    b.contacts.push_back(contact1);
    b.contacts.push_back(contact2);

    SourceFilePtr source1(new SourceFile("id1"));
    SourceFilePtr source2a(new SourceFile("id2"));
    SourceFilePtr source2b(new SourceFile("id2"));
    source2a->cvParams.push_back(MS_Thermo_RAW_format);

    a.sourceFilePtrs.push_back(source1);
    b.sourceFilePtrs.push_back(source1);

    Diff<FileDescription, DiffConfig> diff(a, b);
    unit_assert(!diff);

    a.contacts.push_back(contact3);
    b.contacts.push_back(contact4);

    a.sourceFilePtrs.push_back(source2a);
    b.sourceFilePtrs.push_back(source2b);
    
    diff(a, b);
    if (os_) *os_ << diff << endl;

    unit_assert(diff);
    unit_assert(diff.a_b.contacts.size() == 1);
    unit_assert(diff.a_b.contacts[0].cvParam(MS_contact_name).value == "Emma Lee");
    unit_assert(diff.b_a.contacts.size() == 1);
    unit_assert(diff.b_a.contacts[0].cvParam(MS_contact_name).value == "Isabelle Lynn");

    unit_assert(diff.a_b.sourceFilePtrs.size() == 1);
    unit_assert(diff.a_b.sourceFilePtrs[0]->hasCVParam(MS_Thermo_RAW_format));
    unit_assert(diff.b_a.sourceFilePtrs.size() == 1);
    unit_assert(!diff.b_a.sourceFilePtrs[0]->hasCVParam(MS_Thermo_RAW_format));
}


void testSample()
{
    if (os_) *os_ << "testSample()\n";

    Sample a("id1","name1"), b("id1","name1"); 
    a.userParams.push_back(UserParam("common"));
    b.userParams.push_back(UserParam("common"));
  
    Diff<Sample, DiffConfig> diff(a, b);
    unit_assert(!diff);

    a.cvParams.push_back(MS_peak_intensity); 
    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


void testComponent()
{
    if (os_) *os_ << "testComponent()\n";

    Component a, b;
    a.userParams.push_back(UserParam("common"));
    b.userParams.push_back(UserParam("common"));
  
    Diff<Component, DiffConfig> diff(a, b);
    unit_assert(!diff);

    a.order = 420;
    b.order = 421;
    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


void testSource()
{
    if (os_) *os_ << "testSource()\n";

    Component a, b;
    a.userParams.push_back(UserParam("common"));
    b.userParams.push_back(UserParam("common"));
  
    Diff<Component, DiffConfig> diff(a, b);
    unit_assert(!diff);

    a.order = 420;
    b.order = 421;
    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


void testComponentList()
{
    if (os_) *os_ << "testComponentList()\n";

    ComponentList a, b;

    a.push_back(Component(ComponentType_Source, 1));
    b.push_back(Component(ComponentType_Source, 1));
    a.push_back(Component(ComponentType_Analyzer, 2));
    b.push_back(Component(ComponentType_Analyzer, 2));
    a.push_back(Component(ComponentType_Detector, 3));
    b.push_back(Component(ComponentType_Detector, 3));

    a[0].userParams.push_back(UserParam("common"));
    b[0].userParams.push_back(UserParam("common"));

    Diff<ComponentList, DiffConfig> diff(a, b);
    unit_assert(!diff);

    a[1].userParams.push_back(UserParam("common"));
    b[1].userParams.push_back(UserParam("common"));
    a[1].userParams.push_back(UserParam("a only"));
    b[1].userParams.push_back(UserParam("b only"));

    a[2].userParams.push_back(UserParam("a only"));
    b[2].userParams.push_back(UserParam("b only"));

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


void testSoftware()
{
    if (os_) *os_ << "testSoftware()\n";

    Software a, b;

    a.id = "msdata";
    a.version = "4.20";
    a.set(MS_ionization_type);
    b = a;

    Diff<Software, DiffConfig> diff(a, b);
    unit_assert(!diff);

    b.version = "4.21";

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


void testInstrumentConfiguration()
{
    InstrumentConfiguration a, b;

    a.id = "LCQ Deca";
    a.cvParams.push_back(MS_LCQ_Deca);
    a.cvParams.push_back(CVParam(MS_instrument_serial_number, 23433));
    a.componentList.push_back(Component(MS_nanoelectrospray, 1));
    a.componentList.push_back(Component(MS_quadrupole_ion_trap, 2));
    a.componentList.push_back(Component(MS_electron_multiplier, 3));

    b = a;

    a.softwarePtr = SoftwarePtr(new Software("XCalibur"));
    a.softwarePtr->version = "4.20";

    b.softwarePtr = SoftwarePtr(new Software("XCalibur"));
    b.softwarePtr->version = "4.20";

    Diff<InstrumentConfiguration, DiffConfig> diff(a, b);
    unit_assert(!diff);

    b.set(MS_reflectron_off);
    b.componentList.source(0).order = 2; 
    b.componentList.detector(0).order = 1; 

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


void testProcessingMethod()
{
    if (os_) *os_ << "testProcessingMethod()\n";

    ProcessingMethod a, b;
    a.userParams.push_back(UserParam("common"));
    b.userParams.push_back(UserParam("common"));
  
    Diff<ProcessingMethod, DiffConfig> diff(a, b);
    unit_assert(!diff);

    a.order = 420;
    b.order = 421;
    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);

    b.order = 420;
    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(!diff);

    a.softwarePtr = SoftwarePtr(new Software("pwiz"));
    b.softwarePtr = SoftwarePtr(new Software("pwiz2"));
    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


void testDataProcessing()
{
    if (os_) *os_ << "testDataProcessing()\n";

    DataProcessing a, b;
    a.id = "dp1";

    b = a;

    ProcessingMethod pm1, pm2, pm3;
    pm1.userParams.push_back(UserParam("abc"));
    pm2.userParams.push_back(UserParam("def"));
    pm3.userParams.push_back(UserParam("ghi"));

    pm1.softwarePtr = SoftwarePtr(new Software("msdata")); 
    pm1.softwarePtr->version = "4.20";

    pm2.softwarePtr = SoftwarePtr(new Software("msdata")); 
    pm2.softwarePtr->version = "4.20";

    a.processingMethods.push_back(pm1);
    a.processingMethods.push_back(pm2);
    b.processingMethods.push_back(pm2);
    b.processingMethods.push_back(pm1);
  
    Diff<DataProcessing, DiffConfig> diff(a, b);
    unit_assert(!diff);

    pm2.softwarePtr = SoftwarePtr(new Software("Xcalibur")); 
    a.processingMethods.push_back(pm3);
    
    diff(a, b);
        
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


void testScanSettings()
{
    if (os_) *os_ << "testScanSettings()\n";

    ScanSettings a, b;
    a.id = "as1";

    b = a;

    Diff<ScanSettings, DiffConfig> diff(a, b);
    unit_assert(!diff);

    b.sourceFilePtrs.push_back(SourceFilePtr(new SourceFile("source file")));
    a.targets.resize(2);
   
    diff(a, b);
        
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
    unit_assert(diff.a_b.sourceFilePtrs.empty());
    unit_assert(diff.b_a.sourceFilePtrs.size() == 1);
    unit_assert(diff.a_b.targets.size() == 2);
    unit_assert(diff.b_a.targets.empty());
}


void testPrecursor()
{
    if (os_) *os_ << "testPrecursor()\n";

    Precursor a, b;

    a.spectrumID = "1234"; 
    a.activation.cvParams.push_back(CVParam(MS_ionization_type, 420));
    a.selectedIons.resize(1);
    a.selectedIons[0].cvParams.push_back(MS_reflectron_on);
    a.cvParams.push_back(MS_reflectron_off);
    b = a; 

    Diff<Precursor, DiffConfig> diff(a, b);
    unit_assert(!diff);

    a.cvParams.push_back(MS_reflectron_on); 
    a.selectedIons[0].userParams.push_back(UserParam("aaaa"));
    b.activation.userParams.push_back(UserParam("bbbb"));
    b.isolationWindow.set(MS_m_z, 200);
    
    diff(a, b);
        
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
    unit_assert(!diff.a_b.selectedIons.empty());
    unit_assert(!diff.a_b.selectedIons[0].userParams.empty());
    unit_assert(!diff.b_a.selectedIons.empty());
    unit_assert(diff.b_a.isolationWindow.cvParam(MS_m_z).valueAs<int>() == 200);
}


void testProduct()
{
    if (os_) *os_ << "testProduct()\n";

    Product a, b;

    a.isolationWindow.set(MS_ionization_type, 420);
    b = a; 

    Diff<Product, DiffConfig> diff(a, b);
    unit_assert(!diff);

    b.isolationWindow.set(MS_m_z, 200);
    
    diff(a, b);
        
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
    unit_assert(diff.a_b.isolationWindow.cvParams.empty());
    unit_assert(diff.b_a.isolationWindow.cvParams.size() == 1);
}


void testScan()
{
    if (os_) *os_ << "testScan()\n";

    Scan a, b;

    InstrumentConfigurationPtr ip = InstrumentConfigurationPtr(new InstrumentConfiguration);
    ip->id = "LTQ FT";

    a.cvParams.push_back(CVParam(MS_ionization_type, 420));
    a.instrumentConfigurationPtr = ip;
    a.scanWindows.push_back(ScanWindow());
    b = a; 

    Diff<Scan, DiffConfig> diff(a, b);
    unit_assert(!diff);

    b.scanWindows.push_back(ScanWindow(250.0, 2000.0, MS_m_z));
    diff(a, b);
        
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
    unit_assert(diff.b_a.scanWindows.size() == 1);
}


void testScanList()
{
    if (os_) *os_ << "testScanList()\n";

    ScanList a, b;

    Scan a1;
    a1.set(MS_filter_string, "booger");
    a1.set(MS_scan_start_time, "4.20", UO_minute);

    Scan a2;
    a1.set(MS_filter_string, "goober");
    a1.set(MS_scan_start_time, "6.66", UO_minute);

    a.scans.push_back(a1);
    a.scans.push_back(a2);
    b.scans.push_back(a2);
    b.scans.push_back(a1);

    Diff<ScanList, DiffConfig> diff(a, b);
    unit_assert(!diff);

    a.cvParams.push_back(MS_reflectron_on); 
    
    diff(a, b);
        
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


void testBinaryDataArray()
{
    if (os_) *os_ << "testBinaryDataArray()\n";

    vector<double> data;
    for (int i=0; i<10; i++) data.push_back(i);

    BinaryDataArray a, b;
    a.data = data; 
    a.dataProcessingPtr = DataProcessingPtr(new DataProcessing("dp1"));
    b = a; 

    DiffConfig config;
    config.precision = 1e-4;

    a.data[9] = 1.00001e10;
    b.data[9] = 1.00000e10;

    // we want to verify relative precision diff (1e-5),
    // not absolute diff (1e5)

    Diff<BinaryDataArray, DiffConfig> diff(a, b, config);
    if (diff && os_) *os_ << diff << endl;
    unit_assert(!diff);

    b.data[9] = 1.0002e10;

    diff(a, b);
        
    if (diff && os_) *os_ << diff << endl;
    unit_assert(diff);
}


void testSpectrum()
{
    if (os_) *os_ << "testSpectrum()\n";

    Spectrum a, b;

    a.id = "goober";
    a.index = 1;
    a.dataProcessingPtr = DataProcessingPtr(new DataProcessing("msdata"));
    a.scanList.scans.push_back(Scan());
    a.scanList.scans.back().instrumentConfigurationPtr = InstrumentConfigurationPtr(new InstrumentConfiguration("LTQ FT"));    
    a.scanList.scans.back().paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("CommonMS1SpectrumParams")));
    a.scanList.scans.back().cvParams.push_back(CVParam(MS_scan_start_time, 5.890500, UO_minute));
    a.scanList.scans.back().cvParams.push_back(CVParam(MS_filter_string, "+ c NSI Full ms [ 400.00-1800.00]"));
    a.scanList.scans.back().scanWindows.push_back(ScanWindow(400.0, 1800.0, MS_m_z));

    b = a; 

    DiffConfig config;
    config.precision = 1e-6;
    Diff<Spectrum, DiffConfig> diff(a, b, config);
    if (diff) cout << diff;
    unit_assert(!diff);

    b.index = 4;
    b.defaultArrayLength = 22;
    a.dataProcessingPtr = DataProcessingPtr(new DataProcessing("msdata 2"));
    b.sourceFilePtr = SourceFilePtr(new SourceFile("test.raw"));
    a.precursors.push_back(Precursor());
    a.precursors.back().spectrumID = "666";
    b.products.push_back(Product());
    b.products.back().isolationWindow.set(MS_ionization_type, 420);
    a.binaryDataArrayPtrs.push_back(BinaryDataArrayPtr(new BinaryDataArray));
    a.binaryDataArrayPtrs.back()->data.resize(6);
    b.binaryDataArrayPtrs.push_back(BinaryDataArrayPtr(new BinaryDataArray));
    b.binaryDataArrayPtrs.back()->data.resize(7);
    b.binaryDataArrayPtrs.push_back(a.binaryDataArrayPtrs[0]);

    diff(a, b);
        
    if (os_) *os_ << diff << endl;
    unit_assert(diff);

    unit_assert(diff.a_b.index == 1);
    unit_assert(diff.a_b.id == "goober");
    unit_assert(diff.a_b.defaultArrayLength == 0);
    unit_assert(diff.a_b.dataProcessingPtr->id == "msdata 2");
    unit_assert(diff.a_b.precursors.size() == 1);
    unit_assert(diff.a_b.products.empty());
    unit_assert(diff.a_b.binaryDataArrayPtrs.empty());

    unit_assert(diff.b_a.index == 4);
    unit_assert(diff.b_a.id == "goober");
    unit_assert(diff.b_a.defaultArrayLength == 22);
    unit_assert(diff.b_a.dataProcessingPtr->id == "msdata");
    unit_assert(diff.b_a.precursors.empty());
    unit_assert(diff.b_a.products.size() == 1);
    unit_assert(diff.b_a.binaryDataArrayPtrs.empty());

    b = a;

    unit_assert(a.binaryDataArrayPtrs.size() == 1); 
    b.binaryDataArrayPtrs[0] = BinaryDataArrayPtr(new BinaryDataArray);
    b.binaryDataArrayPtrs[0]->data.resize(6);

    a.binaryDataArrayPtrs[0]->data[0] = 420;
    b.binaryDataArrayPtrs[0]->data[0] = 420 + 1e-12;

    diff(a,b);
    if (os_ && diff) *os_ << diff << endl;
    unit_assert(!diff);

    b.binaryDataArrayPtrs[0]->data[0] += 1e-3;
    diff(a,b);
    if (os_ && diff) *os_ << diff << endl;
    unit_assert(diff);
}


void testChromatogram()
{
    if (os_) *os_ << "testChromatogram()\n";

    Chromatogram a, b;

    a.id = "goober";
    a.index = 1;
    a.dataProcessingPtr = DataProcessingPtr(new DataProcessing("msdata"));
    a.binaryDataArrayPtrs.push_back(BinaryDataArrayPtr(new BinaryDataArray));
    a.binaryDataArrayPtrs.back()->data.resize(6);

    b = a; 

    DiffConfig config;
    config.precision = 1e-6;
    Diff<Chromatogram, DiffConfig> diff(a, b, config);
    if (diff) cout << diff;
    unit_assert(!diff);

    b.binaryDataArrayPtrs[0] = BinaryDataArrayPtr(new BinaryDataArray);
    b.binaryDataArrayPtrs[0]->data.resize(6);

    a.binaryDataArrayPtrs[0]->data[0] = 420;
    b.binaryDataArrayPtrs[0]->data[0] = 420 + 1e-12;

    diff(a,b);
    if (os_ && diff) *os_ << diff << endl;
    unit_assert(!diff);

    b.binaryDataArrayPtrs[0]->data[0] += 1e-3;
    diff(a,b);
    if (os_ && diff) *os_ << diff << endl;
    unit_assert(diff);
}


void testSpectrumList()
{
    if (os_) *os_ << "testSpectrumList()\n";

    SpectrumListSimple aSimple, bSimple;

    SpectrumPtr spectrum1a = SpectrumPtr(new Spectrum);
    spectrum1a->id = "420";

    SpectrumPtr spectrum1b = SpectrumPtr(new Spectrum);
    spectrum1b->id = "420";
   
    aSimple.spectra.push_back(spectrum1a); 
    bSimple.spectra.push_back(spectrum1b); 
    
    SpectrumList& a = aSimple;
    SpectrumList& b = bSimple;

    Diff<SpectrumList, DiffConfig, SpectrumListSimple> diff(a, b);
    unit_assert(!diff);

    // check: dataProcessingPtr

    aSimple.dp = DataProcessingPtr(new DataProcessing("dp"));
    diff(a, b);
    unit_assert(diff);

    DiffConfig config_ignore;
    config_ignore.ignoreDataProcessing = true;
    Diff<SpectrumList, DiffConfig, SpectrumListSimple> diff_ignore(a, b, config_ignore);
    unit_assert(!diff_ignore);

    aSimple.dp = DataProcessingPtr();
    diff(a, b);
    unit_assert(!diff);

    // check: different SpectrumList::size()
    
    SpectrumPtr spectrum2 = SpectrumPtr(new Spectrum);
    spectrum2->id = "421";
    aSimple.spectra.push_back(spectrum2);

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
    unit_assert(diff.a_b.spectra.size() == 1);
    unit_assert(diff.a_b.spectra[0]->userParams.size() == 1);

    // check: same SpectrumList::size(), different last scan number 

    SpectrumPtr spectrum3 = SpectrumPtr(new Spectrum);
    spectrum3->id = "422";
    bSimple.spectra.push_back(spectrum3);

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
    unit_assert(diff.a_b.spectra.size() == 1);
    unit_assert(diff.a_b.spectra[0]->id == "421");
    unit_assert(diff.b_a.spectra.size() == 1);
    unit_assert(diff.b_a.spectra[0]->id == "422");

    // check: scan numbers match, binary data slightly different
   
    spectrum3->id = "421";
    BinaryDataArrayPtr b1(new BinaryDataArray);
    BinaryDataArrayPtr b2(new BinaryDataArray);
    b1->data.resize(10);
    b2->data.resize(10);
    for (int i=0; i<10; i++)
        b1->data[i] = b2->data[i] = i;
    b2->data[2] += 1e-7;
    spectrum2->binaryDataArrayPtrs.push_back(b1);
    spectrum3->binaryDataArrayPtrs.push_back(b2);

    DiffConfig config;
    config.precision = 1e-6;

    Diff<SpectrumList, DiffConfig, SpectrumListSimple> diffWide(a, b, config);
    unit_assert(!diffWide);

    config.precision = 1e-12;
    Diff<SpectrumList, DiffConfig, SpectrumListSimple> diffNarrow(a, b, config);
    if (os_) *os_ << diffNarrow << endl;
    unit_assert(diffNarrow);
}


void testChromatogramList()
{
    if (os_) *os_ << "testChromatogramList()\n";

    ChromatogramListSimple aSimple, bSimple;

    ChromatogramPtr chromatogram1a = ChromatogramPtr(new Chromatogram);
    chromatogram1a->id = "420";

    ChromatogramPtr chromatogram1b = ChromatogramPtr(new Chromatogram);
    chromatogram1b->id = "420";
   
    aSimple.chromatograms.push_back(chromatogram1a); 
    bSimple.chromatograms.push_back(chromatogram1b); 
    
    ChromatogramList& a = aSimple;
    ChromatogramList& b = bSimple;
    
    Diff<ChromatogramList, DiffConfig, ChromatogramListSimple> diff(a, b);
    DiffConfig config_ignore;
    config_ignore.ignoreChromatograms = true;

    Diff<ChromatogramList, DiffConfig, ChromatogramListSimple> diffIgnore(a, b, config_ignore);
    unit_assert(!diff);
    unit_assert(!diffIgnore);

    // check: dataProcessingPtr

    aSimple.dp = DataProcessingPtr(new DataProcessing("dp"));
    diff(a, b);
    unit_assert(diff);

    DiffConfig config_ignore_dp;
    config_ignore_dp.ignoreDataProcessing = true;
    Diff<ChromatogramList, DiffConfig, ChromatogramListSimple> diff_ignore_dp(a, b, config_ignore_dp);
    unit_assert(!diff_ignore_dp);

    aSimple.dp = DataProcessingPtr();
    diff(a, b);
    unit_assert(!diff);

    // check: different ChromatogramList::size()
    
    ChromatogramPtr chromatogram2 = ChromatogramPtr(new Chromatogram);
    chromatogram2->id = "421";
    aSimple.chromatograms.push_back(chromatogram2);

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
    unit_assert(diff.a_b.chromatograms.size() == 1);
    unit_assert(diff.a_b.chromatograms[0]->userParams.size() == 1);

    diffIgnore(a,b);
    if (os_) *os_ << diffIgnore << endl;
    unit_assert(!diffIgnore);

    // check: same ChromatogramList::size(), different last scan number 

    ChromatogramPtr chromatogram3 = ChromatogramPtr(new Chromatogram);
    chromatogram3->id = "422";
    bSimple.chromatograms.push_back(chromatogram3);

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
    unit_assert(diff.a_b.chromatograms.size() == 1);
    unit_assert(diff.a_b.chromatograms[0]->id == "421");
    unit_assert(diff.b_a.chromatograms.size() == 1);
    unit_assert(diff.b_a.chromatograms[0]->id == "422");

    diffIgnore(a,b);
    unit_assert(!diffIgnore);

    // check: scan numbers match, binary data slightly different
   
    chromatogram3->id = "421";
    BinaryDataArrayPtr b1(new BinaryDataArray);
    BinaryDataArrayPtr b2(new BinaryDataArray);
    b1->data.resize(10);
    b2->data.resize(10);
    for (int i=0; i<10; i++)
        b1->data[i] = b2->data[i] = i;
    b2->data[2] += 1e-7;
    chromatogram2->binaryDataArrayPtrs.push_back(b1);
    chromatogram3->binaryDataArrayPtrs.push_back(b2);

    DiffConfig config;
    config.precision = 1e-6;

    Diff<ChromatogramList, DiffConfig, ChromatogramListSimple> diffWide(a, b, config);
    unit_assert(!diffWide);

    config.precision = 1e-12;
    Diff<ChromatogramList, DiffConfig, ChromatogramListSimple> diffNarrow(a, b, config);
    if (os_) *os_ << diffNarrow << endl;
    unit_assert(diffNarrow);

    diffIgnore(a,b);
    unit_assert(!diffIgnore);
}


void testRun()
{
    if (os_) *os_ << "testRun()\n";

    Run a, b;
   
    a.id = "goober";
    a.startTimeStamp = "20 April 2004 4:20pm";  
    b.id = "goober";
    b.startTimeStamp = "20 April 2004 4:20pm";  

    Diff<Run, DiffConfig> diff(a, b);
    unit_assert(!diff);

    b.id = "raisinet";        

    shared_ptr<SpectrumListSimple> spectrumList1(new SpectrumListSimple);
    spectrumList1->spectra.push_back(SpectrumPtr(new Spectrum));
    spectrumList1->spectra.back()->id = "spectrum1";
    a.spectrumListPtr = spectrumList1;

    shared_ptr<ChromatogramListSimple> chromatogramList1(new ChromatogramListSimple);
    chromatogramList1->chromatograms.push_back(ChromatogramPtr(new Chromatogram));
    chromatogramList1->chromatograms.back()->id = "chromatogram1";
    b.chromatogramListPtr = chromatogramList1;

    // same ref id
    a.defaultInstrumentConfigurationPtr = InstrumentConfigurationPtr(new InstrumentConfiguration("instrumentConfiguration"));
    b.defaultInstrumentConfigurationPtr = InstrumentConfigurationPtr(new InstrumentConfiguration("instrumentConfiguration"));

    b.samplePtr = SamplePtr(new Sample("sample"));
    a.defaultSourceFilePtr = SourceFilePtr(new SourceFile("source file"));
    
    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);

    unit_assert(diff.a_b.spectrumListPtr->size() == 1);
    unit_assert(diff.a_b.spectrumListPtr->spectrum(0)->userParams.size() == 1);

    unit_assert(diff.a_b.chromatogramListPtr.get());
    unit_assert(diff.a_b.chromatogramListPtr->size() == 1);
    unit_assert(diff.a_b.chromatogramListPtr->chromatogram(0)->userParams.size() == 1);

    unit_assert(!diff.a_b.defaultInstrumentConfigurationPtr.get());
    unit_assert(!diff.b_a.defaultInstrumentConfigurationPtr.get());

    unit_assert(!diff.a_b.samplePtr.get());
    unit_assert(!diff.b_a.samplePtr->empty());

    unit_assert(diff.a_b.defaultSourceFilePtr.get());
    unit_assert(!diff.b_a.defaultSourceFilePtr.get());

    unit_assert(diff.a_b.startTimeStamp.empty());
    unit_assert(diff.b_a.startTimeStamp.empty());
}


struct MSDataWithSettableVersion : public MSData {using MSData::version; void version(const string& v) {version_ = v;}};

void testMSData()
{
    if (os_) *os_ << "testMSData()\n";

    MSDataWithSettableVersion a, b;
   
    a.id = "goober";
    b.id = "goober";

    Diff<MSData, DiffConfig> diff(a, b);
    unit_assert(!diff);

    a.accession = "different";
    b.version("version");
    a.cvs.push_back(CV());
    b.fileDescription.fileContent.cvParams.push_back(MS_reflectron_on);
    a.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    b.samplePtrs.push_back(SamplePtr(new Sample("sample"))); 
    a.instrumentConfigurationPtrs.push_back(InstrumentConfigurationPtr(new InstrumentConfiguration("instrumentConfiguration")));
    b.softwarePtrs.push_back(SoftwarePtr(new Software("software")));
    a.dataProcessingPtrs.push_back(DataProcessingPtr(new DataProcessing("dataProcessing")));
    b.run.id = "run";
    b.scanSettingsPtrs.push_back(ScanSettingsPtr(new ScanSettings("scanSettings")));
   
    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);

    unit_assert(diff.a_b.accession == "different");
    unit_assert(diff.b_a.accession.empty());

    unit_assert(diff.a_b.id == (a.id + " (" + a.version() + ")"));
    unit_assert(diff.b_a.id == (b.id + " (" + b.version() + ")"));

    unit_assert(diff.a_b.cvs.size() == 1);
    unit_assert(diff.b_a.cvs.empty());

    unit_assert(diff.a_b.fileDescription.empty());
    unit_assert(!diff.b_a.fileDescription.empty());

    unit_assert(!diff.a_b.paramGroupPtrs.empty());
    unit_assert(diff.b_a.paramGroupPtrs.empty());

    unit_assert(diff.a_b.samplePtrs.empty());
    unit_assert(!diff.b_a.samplePtrs.empty());

    unit_assert(!diff.a_b.instrumentConfigurationPtrs.empty());
    unit_assert(diff.b_a.instrumentConfigurationPtrs.empty());

    unit_assert(diff.a_b.softwarePtrs.empty());
    unit_assert(!diff.b_a.softwarePtrs.empty());

    unit_assert(!diff.a_b.dataProcessingPtrs.empty());
    unit_assert(diff.b_a.dataProcessingPtrs.empty());

    unit_assert(diff.a_b.run.empty());
    unit_assert(!diff.b_a.run.empty());

    unit_assert(diff.a_b.scanSettingsPtrs.empty());
    unit_assert(!diff.b_a.scanSettingsPtrs.empty());
}


void testBinaryDataOnly()
{
    MSData tiny;
    examples::initializeTiny(tiny);

    MSData tinier;
    SpectrumListSimplePtr sl(new SpectrumListSimple);
    ChromatogramListSimplePtr cl(new ChromatogramListSimple);
    tinier.run.spectrumListPtr = sl; 
    tinier.run.chromatogramListPtr = cl; 

    for (unsigned int i=0; i<tiny.run.spectrumListPtr->size(); i++)
    {
        SpectrumPtr from = tiny.run.spectrumListPtr->spectrum(i, true);
        sl->spectra.push_back(SpectrumPtr(new Spectrum));
        SpectrumPtr& to = sl->spectra.back();   

        for (vector<BinaryDataArrayPtr>::const_iterator it=from->binaryDataArrayPtrs.begin();
             it!=from->binaryDataArrayPtrs.end(); ++it)
        {
            // copy BinaryDataArray::data from tiny to tinier
            to->binaryDataArrayPtrs.push_back(BinaryDataArrayPtr(new BinaryDataArray));
            to->binaryDataArrayPtrs.back()->data = (*it)->data;
        }

        // copy "important" scan metadata

        to->defaultArrayLength = from->defaultArrayLength;
        to->scanList = from->scanList;

        to->precursors.resize(from->precursors.size());
        for (size_t precursorIndex=0; precursorIndex<from->precursors.size(); ++precursorIndex)
        {
            Precursor& precursorTo = to->precursors[precursorIndex];
            Precursor& precursorFrom = from->precursors[precursorIndex];
            precursorTo.selectedIons = precursorFrom.selectedIons;
        }
    }

    for (unsigned int i=0; i<tiny.run.chromatogramListPtr->size(); i++)
    {
        ChromatogramPtr from = tiny.run.chromatogramListPtr->chromatogram(i, true);
        cl->chromatograms.push_back(ChromatogramPtr(new Chromatogram));
        ChromatogramPtr& to = cl->chromatograms.back();   

        for (vector<BinaryDataArrayPtr>::const_iterator it=from->binaryDataArrayPtrs.begin();
             it!=from->binaryDataArrayPtrs.end(); ++it)
        {
            // copy BinaryDataArray::data from tiny to tinier
            to->binaryDataArrayPtrs.push_back(BinaryDataArrayPtr(new BinaryDataArray));
            to->binaryDataArrayPtrs.back()->data = (*it)->data;
        }

        // copy "important" scan metadata

        to->defaultArrayLength = from->defaultArrayLength;
    }

    if (os_)
    {
        *os_ << "tinier::";
        TextWriter(*os_,0)(tinier);
    }

    Diff<MSData, DiffConfig> diff_full(tiny, tinier);
    unit_assert(diff_full);

    DiffConfig config;
    config.ignoreMetadata = true;
    config.ignoreIdentity = true;

    Diff<MSData, DiffConfig> diff_data(tiny, tinier, config);
    if (os_ && diff_data) *os_ << diff_data << endl;
    unit_assert(!diff_data); 
}


static const char* userParamName_MaxBinaryDataArrayDifference_ = "Maximum binary data array difference";

// gets value of MaxBinaryDataArrayDifference userParam if present, else 0
template <typename list_type>
double getMaxPrecisionDiff(const list_type& list)
{
    if (list.dp.get() &&
        !list.dp->processingMethods.empty() &&
        !list.dp->processingMethods.back().userParam(userParamName_MaxBinaryDataArrayDifference_).empty())
        return lexical_cast<double>(list.dp->processingMethods.back().userParam(userParamName_MaxBinaryDataArrayDifference_).value);
    return 0;
}


void testMaxPrecisionDiff()

{ 
  if (os_)
    {
      *os_ <<"testMaxPrecisionDiff()\n";
    }

  double epsilon = numeric_limits<double>::epsilon(); 

  BinaryDataArrayPtr a(new BinaryDataArray);
  BinaryDataArrayPtr b(new BinaryDataArray);
  BinaryDataArrayPtr c(new BinaryDataArray);
  BinaryDataArrayPtr d(new BinaryDataArray);
  BinaryDataArrayPtr e(new BinaryDataArray);
  BinaryDataArrayPtr f(new BinaryDataArray);

  std::vector<double> data1;
  std::vector<double> data2;
 
  data1.push_back(3.0);
  data2.push_back(3.0000001);
  
  e->data = data1;
  f->data = data2;

  DiffConfig config;
  config.precision=1e-6;

  Diff<BinaryDataArray, DiffConfig> diff_toosmall(*e,*f,config);
  
  //not diff for diff of 1e-7
  unit_assert(!diff_toosmall);

  data1.push_back(2.0);
  data2.push_back(2.0001);
  
  c->data = data1;
  d->data = data2;

  data1.push_back(1.0);
  data2.push_back(1.001);

  a->data = data1;
  b->data = data2;

  Diff<BinaryDataArray, DiffConfig> diff(*a,*b,config);
  
  //diff 
  unit_assert(diff);

  if(os_) *os_<<diff<<endl;


  Diff<BinaryDataArray, DiffConfig> diff2(*c,*d,config);

  //diff
  unit_assert(diff2);
  
  if(os_) *os_<<diff2<<endl;

  // BinaryDataArray UserParam is set
  unit_assert(!diff.a_b.userParams.empty());
  unit_assert(!diff.b_a.userParams.empty());

  // and correctly 
  double maxBin_a_b=boost::lexical_cast<double>(diff.a_b.userParam("Binary data array difference").value); 
  double maxBin_b_a=boost::lexical_cast<double>(diff.a_b.userParam("Binary data array difference").value); 

  unit_assert_equal(maxBin_a_b,.001,epsilon);
  unit_assert_equal(maxBin_b_a,.001,epsilon); 
  
  Run run_a, run_b;
 
  shared_ptr<SpectrumListSimple> sls_a(new SpectrumListSimple);
  shared_ptr<SpectrumListSimple> sls_b(new SpectrumListSimple);
 
  SpectrumPtr spa(new Spectrum);
  SpectrumPtr spb(new Spectrum);
  SpectrumPtr spc(new Spectrum);
  SpectrumPtr spd(new Spectrum);

  spa->binaryDataArrayPtrs.push_back(a);
  spb->binaryDataArrayPtrs.push_back(b);
  spc->binaryDataArrayPtrs.push_back(c);
  spd->binaryDataArrayPtrs.push_back(d);
  
  sls_a->spectra.push_back(spa);
  sls_a->spectra.push_back(spc);
  sls_b->spectra.push_back(spb);
  sls_b->spectra.push_back(spc);
  
  shared_ptr<ChromatogramListSimple> cls_a(new ChromatogramListSimple);
  shared_ptr<ChromatogramListSimple> cls_b(new ChromatogramListSimple);

  ChromatogramPtr cpa(new Chromatogram);
  ChromatogramPtr cpb(new Chromatogram);
  ChromatogramPtr cpc(new Chromatogram);
  ChromatogramPtr cpd(new Chromatogram);

  cpa->binaryDataArrayPtrs.push_back(a);
  cpb->binaryDataArrayPtrs.push_back(b);
  cpc->binaryDataArrayPtrs.push_back(c);
  cpd->binaryDataArrayPtrs.push_back(d);
  
  cls_a->chromatograms.push_back(cpa);
  cls_a->chromatograms.push_back(cpc);
  cls_b->chromatograms.push_back(cpb);
  cls_b->chromatograms.push_back(cpd);
  
  run_a.spectrumListPtr = sls_a;
  run_b.spectrumListPtr = sls_b;

  run_a.chromatogramListPtr = cls_a;
  run_b.chromatogramListPtr = cls_b;

  // Run user param is written for both Spectrum and Chromatogram binary data array difference user params, if present, with the correct value (max of the Spectrum and Chromatogram user params over the SpectrumList/ ChromatogramList respectively)

  Diff<Run, DiffConfig> diff_run(run_a,run_b,config);
  
  // diff
  
  unit_assert(diff_run); 


  // Run user params are set

  unit_assert(!diff_run.a_b.userParams.empty());
  unit_assert(!diff_run.b_a.userParams.empty()); 


  // and correctly
  
  double maxSpecList_a_b=boost::lexical_cast<double>(diff_run.a_b.userParam("Spectrum binary data array difference").value);
  double maxSpecList_b_a=boost::lexical_cast<double>(diff_run.b_a.userParam("Spectrum binary data array difference").value);
  
  double maxChrList_a_b=boost::lexical_cast<double>(diff_run.a_b.userParam("Chromatogram binary data array difference").value);
  double maxChrList_b_a=boost::lexical_cast<double>(diff_run.b_a.userParam("Chromatogram binary data array difference").value);
  
  unit_assert_equal(maxSpecList_a_b,.001,epsilon);
  unit_assert_equal(maxSpecList_b_a,.001,epsilon);
  unit_assert_equal(maxChrList_a_b,.001,epsilon);
  unit_assert_equal(maxChrList_b_a,.001,epsilon);

  // test that Spectrum UserParam is written upon finding a binary data diff, with the correct value

  // user params are set  
  unit_assert(!diff_run.a_b.spectrumListPtr->spectrum(0)->userParams.empty());
  unit_assert(!diff_run.b_a.spectrumListPtr->spectrum(0)->userParams.empty()); //user params are set
  
  // and correctly
  
  double maxSpec_a_b=boost::lexical_cast<double>(diff_run.a_b.spectrumListPtr->spectrum(0)->userParam("Binary data array difference").value);
  double maxSpec_b_a=boost::lexical_cast<double>(diff_run.b_a.spectrumListPtr->spectrum(0)->userParam("Binary data array difference").value);

  unit_assert_equal(maxSpec_a_b,.001,epsilon);
  unit_assert_equal(maxSpec_b_a,.001,epsilon); 


  // test that Chromatogram UserParam is written upon finding a binary data diff, with the correct value

  // user params are set
  unit_assert(!diff_run.a_b.chromatogramListPtr->chromatogram(0)->userParams.empty());
  unit_assert(!diff_run.b_a.chromatogramListPtr->chromatogram(0)->userParams.empty());

  // and correctly

  double maxChr_a_b=boost::lexical_cast<double>(diff_run.a_b.chromatogramListPtr->chromatogram(0)->userParam("Binary data array difference").value);
  double maxChr_b_a=boost::lexical_cast<double>(diff_run.b_a.chromatogramListPtr->chromatogram(0)->userParam("Binary data array difference").value);
 
  unit_assert_equal(maxChr_a_b,.001,epsilon);
  unit_assert_equal(maxChr_b_a,.001,epsilon);

  if(os_) *os_<<diff_run<<endl;



  // test that maxPrecisionDiff is being returned correctly for a zero diff within diff_impl::diff(SpectrumList, SpectrumList, SpectrumList, SpectrumList, DiffConfig)

  shared_ptr<SpectrumListSimple> sls_a_a(new SpectrumListSimple);
  shared_ptr<SpectrumListSimple> sls_A_A(new SpectrumListSimple);

  pwiz::data::diff_impl::diff(*sls_a, *sls_a,*sls_a_a,*sls_A_A,config);
  double maxPrecisionNonDiffSpec = getMaxPrecisionDiff(*sls_a_a);
  unit_assert_equal(maxPrecisionNonDiffSpec,0,epsilon);


  // test that maxPrecisionDiff is being returned correctly for a non-zero diff within diff_impl::diff(SpectrumList, SpectrumList, SpectrumList, SpectrumList, DiffConfig)
  
  shared_ptr<SpectrumListSimple> sls_a_b(new SpectrumListSimple);
  shared_ptr<SpectrumListSimple> sls_b_a(new SpectrumListSimple);

  pwiz::data::diff_impl::diff(*sls_a, *sls_b,*sls_a_b,*sls_b_a,config);
  double maxPrecisionDiffSpec = getMaxPrecisionDiff(*sls_a_b);
  unit_assert_equal(maxPrecisionDiffSpec,.001,epsilon);


  // test that maxPrecisionDiff is being returned correctly for a zero diff within diff_impl::diff(ChromatogramList, ChromatogramList, ChromatogramList, ChromatogramList, DiffConfig)

  shared_ptr<ChromatogramListSimple> cls_a_a(new ChromatogramListSimple);
  shared_ptr<ChromatogramListSimple> cls_A_A(new ChromatogramListSimple);

  pwiz::data::diff_impl::diff(*cls_a, *cls_a,*cls_a_a,*cls_A_A,config);
  double maxPrecisionNonDiffChr = getMaxPrecisionDiff(*cls_a_a);
  unit_assert_equal(maxPrecisionNonDiffChr,0,epsilon);

  // test that maxPrecisionDiff is being returned correctly for a non-zero diff within diff_impl::diff(ChromatogramList, ChromatogramList, ChromatogramList, ChromatogramList, DiffConfig)

  shared_ptr<ChromatogramListSimple> cls_a_b(new ChromatogramListSimple);
  shared_ptr<ChromatogramListSimple> cls_b_a(new ChromatogramListSimple);

  pwiz::data::diff_impl::diff(*cls_a,*cls_b,*cls_a_b,*cls_b_a,config);
  double maxPrecisionDiffChr = getMaxPrecisionDiff(*cls_a_b);
  unit_assert_equal(maxPrecisionDiffChr,.001,epsilon);

  
}


void testMSDiffUpdate()
{
  if(os_) *os_<<"testMSDiffUpdate()"<<endl;

  MSData tiny1;
  MSData tiny2;

  examples::initializeTiny(tiny1);
  examples::initializeTiny(tiny2);

  Diff<MSData, DiffConfig> diff_initial(tiny1,tiny2);
  unit_assert(!diff_initial);

  //inflict metadata differences

  tiny1.id="ego";
  tiny1.run.id="superego";
  
  //inflict spectral differences

  SpectrumPtr tiny1_s0 = tiny1.run.spectrumListPtr->spectrum(0);
  SpectrumPtr tiny2_s1 = tiny2.run.spectrumListPtr->spectrum(1);

  tiny1_s0->id = "tiny1";
  tiny2_s1->id = "tiny2";

  //inflict chromatogram differences

  ChromatogramPtr tiny1_c0=tiny1.run.chromatogramListPtr->chromatogram(0);

  tiny1_c0->id="zumas";

  //test metadata, spectral, chromatogram differences

  Diff<MSData, DiffConfig> diff_changed(tiny1,tiny2);
  unit_assert(diff_changed);

  if(os_) *os_<<diff_changed<<endl;

  tiny1.run.spectrumListPtr.reset();
  
  Diff<MSData, DiffConfig> diff_changed_changed(tiny1,tiny2);
  unit_assert(diff_changed_changed);

  if(os_) *os_<<diff_changed_changed<<endl;
}


void test()
{
    testFileContent();
    testSourceFile();
    testFileDescription();
    testSample();
    testComponent();
    testSource();
    testComponentList();
    testSoftware();
    testInstrumentConfiguration();
    testProcessingMethod();
    testDataProcessing();
    testScanSettings();
    testPrecursor();
    testProduct();
    testScan();
    testScanList();
    testBinaryDataArray();
    testSpectrum();
    testChromatogram();
    testSpectrumList();
    testChromatogramList();
    testRun();
    testMSData();
    testBinaryDataOnly();
    testMaxPrecisionDiff();
    testMSDiffUpdate();
}


int main(int argc, char* argv[])
{
    TEST_PROLOG_EX(argc, argv, "_MSData")

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}

