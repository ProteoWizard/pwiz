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


#include "References.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"


using namespace pwiz::util;
using namespace pwiz::cv;
using namespace pwiz::msdata;


ostream* os_ = 0;


void testParamContainer()
{
    if (os_) *os_ << "testParamContainer()\n"; 

    ParamContainer pc;
    pc.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg1")));
    pc.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg2")));

    MSData msd;
    msd.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg2")));
    msd.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg1")));
    msd.paramGroupPtrs[0]->cvParams.push_back(MS_reflectron_on);
    msd.paramGroupPtrs[1]->cvParams.push_back(MS_reflectron_off);

    unit_assert(pc.paramGroupPtrs[0]->cvParams.empty());
    unit_assert(pc.paramGroupPtrs[1]->cvParams.empty());

    References::resolve(pc, msd);

    unit_assert(pc.paramGroupPtrs[0]->cvParams.size() == 1);
    unit_assert(pc.paramGroupPtrs[0]->cvParams[0] == MS_reflectron_off);
    unit_assert(pc.paramGroupPtrs[1]->cvParams.size() == 1);
    unit_assert(pc.paramGroupPtrs[1]->cvParams[0] == MS_reflectron_on);
}


void testFileDescription()
{
    if (os_) *os_ << "testFileDescription()\n"; 

    FileDescription fd;
    fd.fileContent.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg1")));
    fd.sourceFilePtrs.push_back(SourceFilePtr(new SourceFile));
    fd.sourceFilePtrs.back()->paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg2")));
    fd.sourceFilePtrs.push_back(SourceFilePtr(new SourceFile));
    fd.sourceFilePtrs.back()->paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg3")));
    fd.contacts.push_back(Contact());
    fd.contacts.back().paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg4")));
    fd.contacts.push_back(Contact());
    fd.contacts.back().paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg5")));

    MSData msd;
    msd.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg5")));
    msd.paramGroupPtrs.back()->userParams.push_back(UserParam("user5"));
    msd.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg4")));
    msd.paramGroupPtrs.back()->userParams.push_back(UserParam("user4"));
    msd.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg3")));
    msd.paramGroupPtrs.back()->userParams.push_back(UserParam("user3"));
    msd.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg2")));
    msd.paramGroupPtrs.back()->userParams.push_back(UserParam("user2"));
    msd.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg1")));
    msd.paramGroupPtrs.back()->userParams.push_back(UserParam("user1"));

    References::resolve(fd, msd);

    unit_assert(!fd.fileContent.paramGroupPtrs[0]->userParams.empty() &&
                fd.fileContent.paramGroupPtrs[0]->userParams[0].name == "user1");

    unit_assert(!fd.sourceFilePtrs[0]->paramGroupPtrs[0]->userParams.empty() &&
                fd.sourceFilePtrs[0]->paramGroupPtrs[0]->userParams[0].name == "user2");

    unit_assert(!fd.sourceFilePtrs[1]->paramGroupPtrs[0]->userParams.empty() &&
                fd.sourceFilePtrs[1]->paramGroupPtrs[0]->userParams[0].name == "user3");

    unit_assert(!fd.contacts[0].paramGroupPtrs[0]->userParams.empty() &&
                fd.contacts[0].paramGroupPtrs[0]->userParams[0].name == "user4");

    unit_assert(!fd.contacts[1].paramGroupPtrs[0]->userParams.empty() &&
                fd.contacts[1].paramGroupPtrs[0]->userParams[0].name == "user5");
}


void testComponentList()
{
    if (os_) *os_ << "testComponentList()\n"; 

    ComponentList componentList;
    componentList.push_back(Component(ComponentType_Source, 1));
    componentList.push_back(Component(ComponentType_Analyzer, 2));
    componentList.push_back(Component(ComponentType_Detector, 3));
    componentList.source(0).paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    componentList.analyzer(0).paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    componentList.detector(0).paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));

    MSData msd;
    msd.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    msd.paramGroupPtrs.back()->userParams.push_back(UserParam("user"));
 
    References::resolve(componentList, msd);

    unit_assert(!componentList.source(0).paramGroupPtrs[0]->userParams.empty());
    unit_assert(!componentList.analyzer(0).paramGroupPtrs[0]->userParams.empty());
    unit_assert(!componentList.detector(0).paramGroupPtrs[0]->userParams.empty());
}


void testInstrumentConfiguration()
{
    if (os_) *os_ << "testInstrumentConfiguration()\n"; 

    InstrumentConfiguration instrumentConfiguration;
    instrumentConfiguration.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    instrumentConfiguration.componentList.push_back(Component(ComponentType_Source, 1));
    instrumentConfiguration.componentList.source(0).paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    instrumentConfiguration.softwarePtr = SoftwarePtr(new Software("msdata"));

    MSData msd;
    msd.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    msd.paramGroupPtrs.back()->userParams.push_back(UserParam("user"));
    msd.softwarePtrs.push_back(SoftwarePtr(new Software("booger")));
    msd.softwarePtrs.push_back(SoftwarePtr(new Software("msdata")));
    msd.softwarePtrs[1]->version = "4.20";

    unit_assert(instrumentConfiguration.softwarePtr->version.empty());
    unit_assert(instrumentConfiguration.paramGroupPtrs[0]->userParams.empty());

    References::resolve(instrumentConfiguration, msd);

    unit_assert(!instrumentConfiguration.paramGroupPtrs[0]->userParams.empty());
    unit_assert(!instrumentConfiguration.componentList.source(0).paramGroupPtrs[0]->userParams.empty());
    unit_assert(instrumentConfiguration.softwarePtr->version == "4.20");
}


void testDataProcessing()
{
    if (os_) *os_ << "testDataProcessing()\n"; 

    DataProcessing dataProcessing;
    dataProcessing.processingMethods.push_back(ProcessingMethod());
    dataProcessing.processingMethods.back().paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    dataProcessing.processingMethods.back().softwarePtr = SoftwarePtr(new Software("msdata"));

    MSData msd;
    msd.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    msd.paramGroupPtrs.back()->userParams.push_back(UserParam("user"));
    msd.softwarePtrs.push_back(SoftwarePtr(new Software("booger")));
    msd.softwarePtrs.push_back(SoftwarePtr(new Software("msdata")));
    msd.softwarePtrs[1]->version = "4.20";

    unit_assert(dataProcessing.processingMethods.back().softwarePtr->version.empty());
    unit_assert(dataProcessing.processingMethods.back().paramGroupPtrs[0]->userParams.empty());

    References::resolve(dataProcessing, msd);

    unit_assert(!dataProcessing.processingMethods.back().paramGroupPtrs[0]->userParams.empty());
    unit_assert(dataProcessing.processingMethods.back().softwarePtr->version == "4.20");
}


void testScanSettings()
{
    if (os_) *os_ << "testScanSettings()\n"; 

    ScanSettings scanSettings;
    scanSettings.targets.push_back(Target());
    scanSettings.targets.back().paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    scanSettings.sourceFilePtrs.push_back(SourceFilePtr(new SourceFile("sf2")));
    scanSettings.sourceFilePtrs.push_back(SourceFilePtr(new SourceFile("sf1")));

    MSData msd;
    msd.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    msd.paramGroupPtrs.back()->userParams.push_back(UserParam("user"));
    msd.fileDescription.sourceFilePtrs.push_back(SourceFilePtr(new SourceFile("sf1"))); 
    msd.fileDescription.sourceFilePtrs.back()->name = "goo1.raw";
    msd.fileDescription.sourceFilePtrs.push_back(SourceFilePtr(new SourceFile("sf2"))); 
    msd.fileDescription.sourceFilePtrs.back()->name = "goo2.raw";

    unit_assert(scanSettings.targets.back().paramGroupPtrs[0]->userParams.empty());
    unit_assert(scanSettings.sourceFilePtrs[0]->name.empty());
    unit_assert(scanSettings.sourceFilePtrs[1]->name.empty());

    References::resolve(scanSettings, msd);

    unit_assert(!scanSettings.targets.back().paramGroupPtrs.empty());
    unit_assert(!scanSettings.targets.back().paramGroupPtrs[0]->userParams.empty());
    unit_assert(scanSettings.sourceFilePtrs[0]->name == "goo2.raw");
    unit_assert(scanSettings.sourceFilePtrs[1]->name == "goo1.raw");
}


void testPrecursor()
{
    if (os_) *os_ << "testPrecursor()\n"; 

    Precursor precursor;
    precursor.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    precursor.selectedIons.resize(1);
    precursor.selectedIons[0].paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    precursor.activation.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    precursor.isolationWindow.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));

    MSData msd;
    msd.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    msd.paramGroupPtrs.back()->userParams.push_back(UserParam("user"));
    
    unit_assert(precursor.paramGroupPtrs[0]->userParams.empty());
    unit_assert(precursor.selectedIons[0].paramGroupPtrs[0]->userParams.empty());
    unit_assert(precursor.activation.paramGroupPtrs[0]->userParams.empty());
    unit_assert(precursor.isolationWindow.paramGroupPtrs[0]->userParams.empty());

    References::resolve(precursor, msd);

    unit_assert(!precursor.paramGroupPtrs[0]->userParams.empty());
    unit_assert(!precursor.selectedIons[0].paramGroupPtrs[0]->userParams.empty());
    unit_assert(!precursor.activation.paramGroupPtrs[0]->userParams.empty());
    unit_assert(!precursor.isolationWindow.paramGroupPtrs[0]->userParams.empty());
}


void testProduct()
{
    if (os_) *os_ << "testProduct()\n"; 

    Product product;
    product.isolationWindow.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));

    MSData msd;
    msd.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    msd.paramGroupPtrs.back()->userParams.push_back(UserParam("user"));
    
    unit_assert(product.isolationWindow.paramGroupPtrs[0]->userParams.empty());

    References::resolve(product, msd);

    unit_assert(!product.isolationWindow.paramGroupPtrs[0]->userParams.empty());
}


void testScan()
{
    if (os_) *os_ << "testScan()\n"; 

    Scan scan;
    scan.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    scan.instrumentConfigurationPtr = InstrumentConfigurationPtr(new InstrumentConfiguration("instrumentConfiguration"));
    scan.scanWindows.push_back(ScanWindow());
    scan.scanWindows.back().paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));

    MSData msd;
    msd.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    msd.paramGroupPtrs.back()->userParams.push_back(UserParam("user"));
    msd.instrumentConfigurationPtrs.push_back(InstrumentConfigurationPtr(new InstrumentConfiguration("instrumentConfiguration")));
    msd.instrumentConfigurationPtrs.back()->userParams.push_back(UserParam("user"));
    
    unit_assert(scan.paramGroupPtrs[0]->userParams.empty());
    unit_assert(scan.instrumentConfigurationPtr->userParams.empty());
    unit_assert(scan.scanWindows.back().paramGroupPtrs.back()->userParams.empty());

    References::resolve(scan, msd);

    unit_assert(!scan.paramGroupPtrs[0]->userParams.empty());
    unit_assert(!scan.instrumentConfigurationPtr->userParams.empty());
    unit_assert(!scan.scanWindows.back().paramGroupPtrs.back()->userParams.empty());
}


void testScanList()
{
    if (os_) *os_ << "testScanList()\n"; 

    ScanList scanList;
    scanList.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));

    scanList.scans.push_back(Scan());
    Scan& scan = scanList.scans.back();
    scan.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));

    MSData msd;
    msd.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    msd.paramGroupPtrs.back()->userParams.push_back(UserParam("user"));
    
    unit_assert(scanList.paramGroupPtrs[0]->userParams.empty());
    unit_assert(scan.paramGroupPtrs[0]->userParams.empty());

    References::resolve(scanList, msd);

    unit_assert(!scanList.paramGroupPtrs[0]->userParams.empty());
    unit_assert(!scan.paramGroupPtrs[0]->userParams.empty());
}


void testBinaryDataArray()
{
    if (os_) *os_ << "testBinaryDataArray()\n"; 

    BinaryDataArray binaryDataArray;
    binaryDataArray.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    binaryDataArray.dataProcessingPtr = DataProcessingPtr(new DataProcessing("msdata"));

    MSData msd;
    msd.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    msd.paramGroupPtrs.back()->userParams.push_back(UserParam("user"));
    msd.dataProcessingPtrs.push_back(DataProcessingPtr(new DataProcessing("msdata")));
    msd.dataProcessingPtrs.back()->processingMethods.push_back(ProcessingMethod());
    msd.dataProcessingPtrs.back()->processingMethods.back().softwarePtr = SoftwarePtr(new Software("software"));
    
    unit_assert(binaryDataArray.paramGroupPtrs.back()->userParams.empty());
    unit_assert(binaryDataArray.dataProcessingPtr->processingMethods.empty());

    References::resolve(binaryDataArray, msd);

    unit_assert(!binaryDataArray.paramGroupPtrs.back()->userParams.empty());
    unit_assert(binaryDataArray.dataProcessingPtr->processingMethods.size() == 1);
    unit_assert(binaryDataArray.dataProcessingPtr->processingMethods.back().softwarePtr.get());
}


void testSpectrum()
{
    if (os_) *os_ << "testSpectrum()\n"; 

    Spectrum spectrum;
    spectrum.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    spectrum.dataProcessingPtr = DataProcessingPtr(new DataProcessing("dp"));
    spectrum.sourceFilePtr = SourceFilePtr(new SourceFile("sf"));
    spectrum.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));

    spectrum.scanList.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    spectrum.precursors.push_back(Precursor());
    spectrum.precursors.back().paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    spectrum.products.push_back(Product());
    spectrum.products.back().isolationWindow.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));

    spectrum.binaryDataArrayPtrs.push_back(BinaryDataArrayPtr(new BinaryDataArray));
    spectrum.binaryDataArrayPtrs.back()->paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));

 
    MSData msd;
    msd.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    msd.paramGroupPtrs.back()->userParams.push_back(UserParam("user"));
    msd.dataProcessingPtrs.push_back(DataProcessingPtr(new DataProcessing("dp")));
    msd.dataProcessingPtrs.back()->processingMethods.push_back(ProcessingMethod());
    msd.dataProcessingPtrs.back()->processingMethods.back().softwarePtr = SoftwarePtr(new Software("software"));
    msd.fileDescription.sourceFilePtrs.push_back(SourceFilePtr(new SourceFile("sf"))); 
    msd.fileDescription.sourceFilePtrs.back()->name = "goo.raw";
    
    unit_assert(spectrum.paramGroupPtrs.back()->userParams.empty());
    unit_assert(spectrum.dataProcessingPtr->processingMethods.empty());
    unit_assert(spectrum.sourceFilePtr->name.empty());
    unit_assert(spectrum.paramGroupPtrs.back()->userParams.empty());
    unit_assert(spectrum.scanList.paramGroupPtrs.back()->userParams.empty());
    unit_assert(spectrum.precursors.back().paramGroupPtrs.back()->userParams.empty());
    unit_assert(spectrum.products.back().isolationWindow.paramGroupPtrs.back()->userParams.empty());
    unit_assert(spectrum.binaryDataArrayPtrs.back()->paramGroupPtrs.back()->userParams.empty());

    References::resolve(spectrum, msd);

    unit_assert(!spectrum.paramGroupPtrs.back()->userParams.empty());
    unit_assert(spectrum.dataProcessingPtr->processingMethods.size() == 1);
    unit_assert(spectrum.dataProcessingPtr->processingMethods.back().softwarePtr.get());
    unit_assert(spectrum.sourceFilePtr->name == "goo.raw");
    unit_assert(!spectrum.paramGroupPtrs.back()->userParams.empty());
    unit_assert(!spectrum.scanList.paramGroupPtrs.back()->userParams.empty());
    unit_assert(!spectrum.precursors.back().paramGroupPtrs.back()->userParams.empty());
    unit_assert(!spectrum.products.back().isolationWindow.paramGroupPtrs.back()->userParams.empty());
    unit_assert(!spectrum.binaryDataArrayPtrs.back()->paramGroupPtrs.back()->userParams.empty());
}


void testChromatogram()
{
    if (os_) *os_ << "testChromatogram()\n"; 

    Chromatogram chromatogram;
    chromatogram.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    chromatogram.dataProcessingPtr = DataProcessingPtr(new DataProcessing("dp"));
    chromatogram.binaryDataArrayPtrs.push_back(BinaryDataArrayPtr(new BinaryDataArray));
    chromatogram.binaryDataArrayPtrs.back()->paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));

    MSData msd;
    msd.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    msd.paramGroupPtrs.back()->userParams.push_back(UserParam("user"));
    msd.dataProcessingPtrs.push_back(DataProcessingPtr(new DataProcessing("dp")));
    msd.dataProcessingPtrs.back()->processingMethods.push_back(ProcessingMethod());
    msd.dataProcessingPtrs.back()->processingMethods.back().softwarePtr = SoftwarePtr(new Software("software"));
    
    unit_assert(chromatogram.paramGroupPtrs.back()->userParams.empty());
    unit_assert(chromatogram.dataProcessingPtr->processingMethods.empty());
    unit_assert(chromatogram.binaryDataArrayPtrs.back()->paramGroupPtrs.back()->userParams.empty());

    References::resolve(chromatogram, msd);

    unit_assert(!chromatogram.paramGroupPtrs.back()->userParams.empty());
    unit_assert(chromatogram.dataProcessingPtr->processingMethods.size() == 1);
    unit_assert(chromatogram.dataProcessingPtr->processingMethods.back().softwarePtr.get());
    unit_assert(!chromatogram.binaryDataArrayPtrs.back()->paramGroupPtrs.back()->userParams.empty());
}


void testRun()
{
    if (os_) *os_ << "testRun()\n"; 

    Run run;
    run.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    run.defaultInstrumentConfigurationPtr = InstrumentConfigurationPtr(new InstrumentConfiguration("instrumentConfiguration"));
    run.samplePtr = SamplePtr(new Sample("sample"));
    run.defaultSourceFilePtr = SourceFilePtr(new SourceFile("sf2"));

    MSData msd;
    msd.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    msd.paramGroupPtrs.back()->userParams.push_back(UserParam("user"));
    msd.instrumentConfigurationPtrs.push_back(InstrumentConfigurationPtr(new InstrumentConfiguration("instrumentConfiguration")));
    msd.instrumentConfigurationPtrs.back()->userParams.push_back(UserParam("user"));
    msd.samplePtrs.push_back(SamplePtr(new Sample("sample")));
    msd.samplePtrs.back()->name = "sample name";
    msd.fileDescription.sourceFilePtrs.push_back(SourceFilePtr(new SourceFile("sf1"))); 
    msd.fileDescription.sourceFilePtrs.back()->name = "goo1.raw";
    msd.fileDescription.sourceFilePtrs.push_back(SourceFilePtr(new SourceFile("sf2"))); 
    msd.fileDescription.sourceFilePtrs.back()->name = "goo2.raw";

    unit_assert(run.paramGroupPtrs.back()->userParams.empty());
    unit_assert(run.defaultInstrumentConfigurationPtr->userParams.empty());
    unit_assert(run.samplePtr->name.empty());
    unit_assert(run.defaultSourceFilePtr->name.empty());

    References::resolve(run, msd);

    unit_assert(!run.paramGroupPtrs.back()->userParams.empty());
    unit_assert(!run.defaultInstrumentConfigurationPtr->userParams.empty());
    unit_assert(run.samplePtr->name == "sample name");
    unit_assert(run.defaultSourceFilePtr->name == "goo2.raw");
}


void testMSData()
{
    if (os_) *os_ << "testMSData()\n"; 

    MSData msd;

    msd.fileDescription.fileContent.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    msd.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    msd.paramGroupPtrs.back()->userParams.push_back(UserParam("user"));
    msd.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg1")));
    msd.paramGroupPtrs.back()->paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    msd.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg2")));
    msd.paramGroupPtrs.back()->paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    msd.samplePtrs.push_back(SamplePtr(new Sample("sample")));
    msd.samplePtrs.back()->paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    msd.instrumentConfigurationPtrs.push_back(InstrumentConfigurationPtr(new InstrumentConfiguration("instrumentConfiguration")));
    msd.instrumentConfigurationPtrs.back()->paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    msd.dataProcessingPtrs.push_back(DataProcessingPtr(new DataProcessing("dp")));
    msd.dataProcessingPtrs.back()->processingMethods.push_back(ProcessingMethod());
    msd.dataProcessingPtrs.back()->processingMethods.back().paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));
    msd.run.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pg")));

    unit_assert(msd.paramGroupPtrs[1]->paramGroupPtrs.back()->userParams.empty());
    unit_assert(msd.paramGroupPtrs[2]->paramGroupPtrs.back()->userParams.empty());
    unit_assert(msd.samplePtrs.back()->paramGroupPtrs.back()->userParams.empty());
    unit_assert(msd.instrumentConfigurationPtrs.back()->paramGroupPtrs.back()->userParams.empty());
    unit_assert(msd.dataProcessingPtrs.back()->processingMethods.back().paramGroupPtrs.back()->userParams.empty());
    unit_assert(msd.run.paramGroupPtrs.back()->userParams.empty());

    References::resolve(msd);

    unit_assert(!msd.paramGroupPtrs[1]->paramGroupPtrs.back()->userParams.empty());
    unit_assert(!msd.paramGroupPtrs[2]->paramGroupPtrs.back()->userParams.empty());
    unit_assert(!msd.samplePtrs.back()->paramGroupPtrs.back()->userParams.empty());
    unit_assert(!msd.instrumentConfigurationPtrs.back()->paramGroupPtrs.back()->userParams.empty());
    unit_assert(!msd.dataProcessingPtrs.back()->processingMethods.back().paramGroupPtrs.back()->userParams.empty());
    unit_assert(!msd.run.paramGroupPtrs.back()->userParams.empty());
}


void test()
{
    testParamContainer();
    testFileDescription();
    testComponentList();
    testInstrumentConfiguration();
    testDataProcessing();
    testScanSettings();
    testPrecursor();
    testProduct();
    testScan();
    testScanList();
    testBinaryDataArray();
    testSpectrum();
    testChromatogram();
    testRun();
    testMSData();
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

