//
// DiffTest.cpp
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


#include "Diff.hpp"
#include "examples.hpp"
#include "utility/misc/unit.hpp"
#include <iostream>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::msdata;
using boost::shared_ptr;


ostream* os_ = 0;


void testString()
{
    if (os_) *os_ << "testString()\n";

    Diff<string> diff("goober", "goober");
    unit_assert(diff.a_b.empty() && diff.b_a.empty());
    unit_assert(!diff);

    diff("goober", "goo");
    unit_assert(diff);
    if (os_) *os_ << diff << endl;
}


void testCV()
{
    if (os_) *os_ << "testCV()\n";

    CV a, b;
    a.URI = "uri";
    a.id = "cvLabel";
    a.fullName = "fullName";
    a.version = "version";
    b = a;

    Diff<CV> diff;
    diff(a,b);

    unit_assert(diff.a_b.empty());
    unit_assert(diff.b_a.empty());
    unit_assert(!diff);

    a.version = "version_changed";

    diff(a,b); 
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
    unit_assert(diff.a_b.URI.empty() && diff.b_a.URI.empty());
    unit_assert(diff.a_b.id.empty() && diff.b_a.id.empty());
    unit_assert(diff.a_b.fullName.empty() && diff.b_a.fullName.empty());
    unit_assert(diff.a_b.version == "version_changed");
    unit_assert(diff.b_a.version == "version");
}


void testUserParam()
{
    if (os_) *os_ << "testUserParam()\n";

    UserParam a, b;
    a.name = "name";
    a.value = "value";
    a.type = "type";
    a.units = MS_minute;
    b = a;

    Diff<UserParam> diff(a, b);
    unit_assert(!diff);
    unit_assert(diff.a_b.empty());
    unit_assert(diff.b_a.empty());

    b.value = "value_changed";
    a.units = MS_second;
    unit_assert(diff(a,b));
    if (os_) *os_ << diff << endl;
    unit_assert(diff.a_b.name == "name");
    unit_assert(diff.b_a.name == "name");
    unit_assert(diff.a_b.value == "value");
    unit_assert(diff.b_a.value == "value_changed");
    unit_assert(diff.a_b.type.empty() && diff.b_a.type.empty());
    unit_assert(diff.a_b.units == MS_second);
    unit_assert(diff.b_a.units == MS_minute);
}


void testCVParam()
{
    if (os_) *os_ << "testCVParam()\n";

    CVParam a, b;
    a.cvid = MS_ionization_type; 
    a.value = "420";
    b = a;

    Diff<CVParam> diff(a, b);
    unit_assert(!diff);
    unit_assert(diff.a_b.empty());
    unit_assert(diff.b_a.empty());

    b.value = "value_changed";
    diff(a,b);
    unit_assert(diff);
    if (os_) *os_ << diff << endl;
    unit_assert(diff.a_b.cvid == MS_ionization_type);
    unit_assert(diff.b_a.cvid == MS_ionization_type);
    unit_assert(diff.a_b.value == "420");
    unit_assert(diff.b_a.value == "value_changed");
}


void testParamContainer()
{
    if (os_) *os_ << "testParamContainer()\n";

    ParamGroupPtr pgp1(new ParamGroup("pg1"));
    ParamGroupPtr pgp2(new ParamGroup("pg2"));
    ParamGroupPtr pgp3(new ParamGroup("pg3"));
 
    ParamContainer a, b;
    a.userParams.push_back(UserParam("common"));
    b.userParams.push_back(UserParam("common"));
    a.cvParams.push_back(MS_m_z);
    b.cvParams.push_back(MS_m_z);
    a.paramGroupPtrs.push_back(pgp1);
    b.paramGroupPtrs.push_back(pgp1);
   
    Diff<ParamContainer> diff(a, b);
    unit_assert(!diff);

    a.userParams.push_back(UserParam("different", "1"));
    b.userParams.push_back(UserParam("different", "2"));
    a.cvParams.push_back(MS_charge_state);
    b.cvParams.push_back(MS_intensity);
    a.paramGroupPtrs.push_back(pgp2);
    b.paramGroupPtrs.push_back(pgp3);

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);

    unit_assert(diff.a_b.userParams.size() == 1);
    unit_assert(diff.a_b.userParams[0] == UserParam("different","1"));
    unit_assert(diff.b_a.userParams.size() == 1);
    unit_assert(diff.b_a.userParams[0] == UserParam("different","2"));

    unit_assert(diff.a_b.cvParams.size() == 1);
    unit_assert(diff.a_b.cvParams[0] == MS_charge_state); 
    unit_assert(diff.b_a.cvParams.size() == 1);
    unit_assert(diff.b_a.cvParams[0] == MS_intensity); 

    unit_assert(diff.a_b.paramGroupPtrs.size() == 1);
    unit_assert(diff.a_b.paramGroupPtrs[0]->id == "pg2"); 
    unit_assert(diff.b_a.paramGroupPtrs.size() == 1);
    unit_assert(diff.b_a.paramGroupPtrs[0]->id == "pg3"); 
}


void testParamGroup()
{
    if (os_) *os_ << "testParamGroup()\n";

    ParamGroup a("pg"), b("pg");
    a.userParams.push_back(UserParam("common"));
    b.userParams.push_back(UserParam("common"));
  
    Diff<ParamGroup> diff(a, b);
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


void testFileContent()
{
    if (os_) *os_ << "testFileContent()\n";

    FileContent a, b; 
    a.userParams.push_back(UserParam("common"));
    b.userParams.push_back(UserParam("common"));
  
    Diff<FileContent> diff(a, b);
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
  
    Diff<SourceFile> diff(a, b);
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
    source2a->cvParams.push_back(MS_Xcalibur_RAW_file);

    a.sourceFilePtrs.push_back(source1);
    b.sourceFilePtrs.push_back(source1);

    Diff<FileDescription> diff(a, b);
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
    unit_assert(diff.a_b.sourceFilePtrs[0]->hasCVParam(MS_Xcalibur_RAW_file));
    unit_assert(diff.b_a.sourceFilePtrs.size() == 1);
    unit_assert(!diff.b_a.sourceFilePtrs[0]->hasCVParam(MS_Xcalibur_RAW_file));
}


void testSample()
{
    if (os_) *os_ << "testSample()\n";

    Sample a("id1","name1"), b("id1","name1"); 
    a.userParams.push_back(UserParam("common"));
    b.userParams.push_back(UserParam("common"));
  
    Diff<Sample> diff(a, b);
    unit_assert(!diff);

    a.cvParams.push_back(MS_intensity); 
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
  
    Diff<Component> diff(a, b);
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
  
    Diff<Component> diff(a, b);
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

    Diff<ComponentList> diff(a, b);
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
    a.softwareParam = MS_ionization_type;
    a.softwareParamVersion = "4.20";
    b = a;

    Diff<Software> diff(a, b);
    unit_assert(!diff);

    b.softwareParamVersion = "4.21";

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
    a.softwarePtr->softwareParamVersion = "4.20";

    b.softwarePtr = SoftwarePtr(new Software("XCalibur"));
    b.softwarePtr->softwareParamVersion = "4.20";

    Diff<InstrumentConfiguration> diff(a, b);
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
  
    Diff<ProcessingMethod> diff(a, b);
    unit_assert(!diff);

    a.order = 420;
    b.order = 421;
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

    a.softwarePtr = SoftwarePtr(new Software("msdata")); 
    a.softwarePtr->softwareParamVersion = "4.20";

    b.softwarePtr = SoftwarePtr(new Software("msdata")); 
    b.softwarePtr->softwareParamVersion = "4.20";

    ProcessingMethod pm1, pm2, pm3;
    pm1.userParams.push_back(UserParam("abc"));
    pm2.userParams.push_back(UserParam("def"));
    pm3.userParams.push_back(UserParam("ghi"));

    a.processingMethods.push_back(pm1);
    a.processingMethods.push_back(pm2);
    b.processingMethods.push_back(pm2);
    b.processingMethods.push_back(pm1);
  
    Diff<DataProcessing> diff(a, b);
    unit_assert(!diff);

    b.softwarePtr = SoftwarePtr(new Software("Xcalibur")); 
    a.processingMethods.push_back(pm3);
    
    diff(a, b);
        
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


void testAcquisitionSettings()
{
    if (os_) *os_ << "testAcquisitionSettings()\n";

    AcquisitionSettings a, b;
    a.id = "as1";

    b = a;

    Diff<AcquisitionSettings> diff(a, b);
    unit_assert(!diff);

    a.instrumentConfigurationPtr = InstrumentConfigurationPtr(new InstrumentConfiguration("instrumentConfiguration")); 
    b.sourceFilePtrs.push_back(SourceFilePtr(new SourceFile("source file")));
    a.targets.resize(2);
   
    diff(a, b);
        
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
    unit_assert(diff.a_b.instrumentConfigurationPtr.get());
    unit_assert(!diff.b_a.instrumentConfigurationPtr.get());
    unit_assert(diff.a_b.sourceFilePtrs.empty());
    unit_assert(diff.b_a.sourceFilePtrs.size() == 1);
    unit_assert(diff.a_b.targets.size() == 2);
    unit_assert(diff.b_a.targets.empty());
}


void testAcquisition()
{
    if (os_) *os_ << "testAcquisition()\n";

    Acquisition a, b;
    a.number = 420;
    a.sourceFilePtr = SourceFilePtr(new SourceFile("test.raw"));
    a.spectrumID = "1234";
    b = a;

    Diff<Acquisition> diff(a, b);
    unit_assert(!diff);

    a.sourceFilePtr = SourceFilePtr(new SourceFile("test.mzxml"));
    
    diff(a, b);
        
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


void testAcquisitionList()
{
    if (os_) *os_ << "testAcquisitionList()\n";

    AcquisitionList a, b;

    Acquisition a1;
    a1.number = 420;
    a1.sourceFilePtr = SourceFilePtr(new SourceFile("test.raw"));
    a1.spectrumID = "1234";

    Acquisition a2;
    a2.number = 421;
    a2.sourceFilePtr = SourceFilePtr(new SourceFile("test.mzxml"));
    a2.spectrumID = "5678";

    a.acquisitions.push_back(a1);
    a.acquisitions.push_back(a2);
    b.acquisitions.push_back(a2);
    b.acquisitions.push_back(a1);

    Diff<AcquisitionList> diff(a, b);
    unit_assert(!diff);

    a.cvParams.push_back(MS_reflectron_on); 
    
    diff(a, b);
        
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
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

    Diff<Precursor> diff(a, b);
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

    Diff<Scan> diff(a, b);
    unit_assert(!diff);

    b.scanWindows.push_back(ScanWindow(250.0, 2000.0));
    diff(a, b);
        
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
    unit_assert(diff.b_a.scanWindows.size() == 1);
}


void testSpectrumDescription()
{
    if (os_) *os_ << "testSpectrumDescription()\n";

    SpectrumDescription a, b;

    a.scan.instrumentConfigurationPtr = InstrumentConfigurationPtr(new InstrumentConfiguration("LTQ FT"));    
    a.scan.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("CommonMS1SpectrumParams")));
    a.scan.cvParams.push_back(CVParam(MS_scan_time, 5.890500, MS_minute));
    a.scan.cvParams.push_back(CVParam(MS_filter_string, "+ c NSI Full ms [ 400.00-1800.00]"));
    a.scan.scanWindows.push_back(ScanWindow(400.0, 1800.0));

    b = a; 

    Diff<SpectrumDescription> diff(a, b);
    unit_assert(!diff);

    a.acquisitionList.acquisitions.push_back(Acquisition());
    a.acquisitionList.acquisitions.back().number = 420;
    a.scan.cvParams.push_back(MS_m_z);
    b.precursors.push_back(Precursor());
    b.precursors.back().cvParams.push_back(MS_reflectron_on);

    diff(a, b);
        
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
    unit_assert(diff.a_b.acquisitionList.acquisitions.size() == 1);
    unit_assert(diff.a_b.scan.cvParams.size() == 1);
    unit_assert(diff.b_a.precursors.size() == 1);
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
    config.precision = 1e-10;

    b.data[9] += 1e-12;

    Diff<BinaryDataArray> diff(a, b, config);
    if (diff && os_) *os_ << diff << endl;
    unit_assert(!diff);

    b.data[9] += 1e-9;

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
    a.spectrumDescription.scan.instrumentConfigurationPtr = InstrumentConfigurationPtr(new InstrumentConfiguration("LTQ FT"));    
    a.spectrumDescription.scan.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("CommonMS1SpectrumParams")));
    a.spectrumDescription.scan.cvParams.push_back(CVParam(MS_scan_time, 5.890500, MS_minute));
    a.spectrumDescription.scan.cvParams.push_back(CVParam(MS_filter_string, "+ c NSI Full ms [ 400.00-1800.00]"));
    a.spectrumDescription.scan.scanWindows.push_back(ScanWindow(400.0, 1800.0));

    b = a; 

    DiffConfig config;
    config.precision = 1e-6;
    Diff<Spectrum> diff(a, b, config);
    if (diff) cout << diff;
    unit_assert(!diff);

    b.index = 4;
    a.nativeID = "420";
    b.defaultArrayLength = 22;
    a.dataProcessingPtr = DataProcessingPtr(new DataProcessing("msdata 2"));
    b.sourceFilePtr = SourceFilePtr(new SourceFile("test.raw"));
    a.spectrumDescription.precursors.push_back(Precursor());
    a.spectrumDescription.precursors.back().spectrumID = "666";
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
    unit_assert(diff.a_b.nativeID == "420");
    unit_assert(diff.a_b.defaultArrayLength == 0);
    unit_assert(diff.a_b.dataProcessingPtr->id == "msdata 2");
    unit_assert(diff.a_b.spectrumDescription.precursors.size() == 1);
    unit_assert(diff.a_b.binaryDataArrayPtrs.empty());

    unit_assert(diff.b_a.index == 4);
    unit_assert(diff.b_a.id == "goober");
    unit_assert(diff.b_a.nativeID.empty());
    unit_assert(diff.b_a.defaultArrayLength == 22);
    unit_assert(diff.b_a.dataProcessingPtr->id == "msdata");
    unit_assert(diff.b_a.spectrumDescription.precursors.empty());
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
    Diff<Chromatogram> diff(a, b, config);
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
    SpectrumListSimple aSimple, bSimple;

    SpectrumPtr spectrum1a = SpectrumPtr(new Spectrum);
    spectrum1a->nativeID = "420";

    SpectrumPtr spectrum1b = SpectrumPtr(new Spectrum);
    spectrum1b->nativeID = "420";
   
    aSimple.spectra.push_back(spectrum1a); 
    bSimple.spectra.push_back(spectrum1b); 
    
    SpectrumList& a = aSimple;
    SpectrumList& b = bSimple;

    Diff<SpectrumList> diff(a, b);
    unit_assert(!diff);

    // check: different SpectrumList::size()
    
    SpectrumPtr spectrum2 = SpectrumPtr(new Spectrum);
    spectrum2->nativeID = "421";
    aSimple.spectra.push_back(spectrum2);

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
    unit_assert(diff.a_b.spectra.size() == 1);
    unit_assert(diff.a_b.spectra[0]->userParams.size() == 1);

    // check: same SpectrumList::size(), different last scan number 

    SpectrumPtr spectrum3 = SpectrumPtr(new Spectrum);
    spectrum3->nativeID = "422";
    bSimple.spectra.push_back(spectrum3);

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
    unit_assert(diff.a_b.spectra.size() == 1);
    unit_assert(diff.a_b.spectra[0]->nativeID == "421");
    unit_assert(diff.b_a.spectra.size() == 1);
    unit_assert(diff.b_a.spectra[0]->nativeID == "422");

    // check: scan numbers match, binary data slightly different
   
    spectrum3->nativeID = "421";
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

    Diff<SpectrumList> diffWide(a, b, config);
    unit_assert(!diffWide);

    config.precision = 1e-12;
    Diff<SpectrumList> diffNarrow(a, b, config);
    if (os_) *os_ << diffNarrow << endl;
    unit_assert(diffNarrow);
}


void testChromatogramList()
{
    ChromatogramListSimple aSimple, bSimple;

    ChromatogramPtr chromatogram1a = ChromatogramPtr(new Chromatogram);
    chromatogram1a->nativeID = "420";

    ChromatogramPtr chromatogram1b = ChromatogramPtr(new Chromatogram);
    chromatogram1b->nativeID = "420";
   
    aSimple.chromatograms.push_back(chromatogram1a); 
    bSimple.chromatograms.push_back(chromatogram1b); 
    
    ChromatogramList& a = aSimple;
    ChromatogramList& b = bSimple;

    Diff<ChromatogramList> diff(a, b);
    DiffConfig config_ignore;
    config_ignore.ignoreChromatograms = true;
    Diff<ChromatogramList> diffIgnore(a, b, config_ignore);
    unit_assert(!diff);
    unit_assert(!diffIgnore);

    // check: different ChromatogramList::size()
    
    ChromatogramPtr chromatogram2 = ChromatogramPtr(new Chromatogram);
    chromatogram2->nativeID = "421";
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
    chromatogram3->nativeID = "422";
    bSimple.chromatograms.push_back(chromatogram3);

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
    unit_assert(diff.a_b.chromatograms.size() == 1);
    unit_assert(diff.a_b.chromatograms[0]->nativeID == "421");
    unit_assert(diff.b_a.chromatograms.size() == 1);
    unit_assert(diff.b_a.chromatograms[0]->nativeID == "422");

    diffIgnore(a,b);
    unit_assert(!diffIgnore);

    // check: scan numbers match, binary data slightly different
   
    chromatogram3->nativeID = "421";
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

    Diff<ChromatogramList> diffWide(a, b, config);
    unit_assert(!diffWide);

    config.precision = 1e-12;
    Diff<ChromatogramList> diffNarrow(a, b, config);
    if (os_) *os_ << diffNarrow << endl;
    unit_assert(diffNarrow);

    diffIgnore(a,b);
    unit_assert(!diffIgnore);
}


void testRun()
{
    Run a, b;
   
    a.id = "goober";
    a.startTimeStamp = "20 April 2004 4:20pm";  
    b.id = "goober";
    b.startTimeStamp = "20 April 2004 4:20pm";  

    Diff<Run> diff(a, b);
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
    a.sourceFilePtrs.push_back(SourceFilePtr(new SourceFile("source file")));
    
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

    unit_assert(!diff.a_b.sourceFilePtrs.empty());
    unit_assert(diff.b_a.sourceFilePtrs.empty());

    unit_assert(diff.a_b.startTimeStamp.empty());
    unit_assert(diff.b_a.startTimeStamp.empty());
}


void testMSData()
{
    MSData a, b;
   
    a.id = "goober";
    b.id = "goober";

    Diff<MSData> diff(a, b);
    unit_assert(!diff);

    a.accession = "different";
    b.version = "version";
    a.cvs.push_back(CV());
    b.fileDescription.fileContent.cvParams.push_back(MS_reflectron_on);
    a.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    b.samplePtrs.push_back(SamplePtr(new Sample("sample"))); 
    a.instrumentConfigurationPtrs.push_back(InstrumentConfigurationPtr(new InstrumentConfiguration("instrumentConfiguration")));
    b.softwarePtrs.push_back(SoftwarePtr(new Software("software")));
    a.dataProcessingPtrs.push_back(DataProcessingPtr(new DataProcessing("dataProcessing")));
    b.run.id = "run";
    b.acquisitionSettingsPtrs.push_back(AcquisitionSettingsPtr(new AcquisitionSettings("acquisitionSettings")));
   
    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);

    unit_assert(diff.a_b.accession == "different");
    unit_assert(diff.b_a.accession.empty());

    unit_assert(diff.a_b.version.empty());
    unit_assert(diff.b_a.version == "version");

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

    unit_assert(diff.a_b.acquisitionSettingsPtrs.empty());
    unit_assert(!diff.b_a.acquisitionSettingsPtrs.empty());
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

        to->index = from->index;
        to->nativeID = from->nativeID;
        to->defaultArrayLength = from->defaultArrayLength;

        to->spectrumDescription.precursors.resize(from->spectrumDescription.precursors.size());
        for (size_t precursorIndex=0; precursorIndex<from->spectrumDescription.precursors.size(); ++precursorIndex)
        {
            Precursor& precursorTo = to->spectrumDescription.precursors[precursorIndex];
            Precursor& precursorFrom = from->spectrumDescription.precursors[precursorIndex];
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

        to->index = from->index;
        to->nativeID = from->nativeID;
        to->defaultArrayLength = from->defaultArrayLength;
    }

    if (os_)
    {
        *os_ << "tinier::";
        TextWriter(*os_,0)(tinier);
    }

    Diff<MSData> diff_full(tiny, tinier);
    unit_assert(diff_full);

    DiffConfig config;
    config.ignoreMetadata = true;

    Diff<MSData> diff_data(tiny, tinier, config);
    if (os_ && diff_data) *os_ << diff_data << endl;
    unit_assert(!diff_data);
}


void test()
{
    testString();
    testCV();
    testUserParam();
    testCVParam();
    testParamContainer();
    testParamGroup();
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
    testAcquisitionSettings();
    testAcquisition();
    testAcquisitionList();
    testPrecursor();
    testScan();
    testSpectrumDescription();
    testBinaryDataArray();
    testSpectrum();
    testChromatogram();
    testSpectrumList();
    testChromatogramList();
    testRun();
    testMSData();
    testBinaryDataOnly();
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
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

