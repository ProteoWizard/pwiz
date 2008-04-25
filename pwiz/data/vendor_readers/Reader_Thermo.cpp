//
// Reader_Thermo.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
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


#include "Reader_Thermo.hpp"

namespace {
// helper function used by both forms (real and stubbed) of Reader_Thermo
bool _hasRAWHeader(const std::string& head)
{
    const char rawHeader[] =
    {
        '\x01', '\xA1', 
        'F', '\0', 'i', '\0', 'n', '\0', 'n', '\0', 
        'i', '\0', 'g', '\0', 'a', '\0', 'n', '\0'
    };

    for (size_t i=0; i<sizeof(rawHeader); i++)
        if (head[i] != rawHeader[i]) 
            return false;

    return true;
}
} // namespace


#ifndef PWIZ_NO_READER_RAW
#include "data/msdata/CVTranslator.hpp"
#include "utility/vendor_api/thermo/RawFile.h"
#include "utility/misc/SHA1Calculator.hpp"
#include "boost/shared_ptr.hpp"
#include "boost/lexical_cast.hpp"
#include "boost/algorithm/string.hpp"
#include "boost/filesystem/path.hpp"
#include "Reader_Thermo_Detail.hpp"
#include "SpectrumList_Thermo.hpp"
#include "ChromatogramList_Thermo.hpp"
#include <iostream>
#include <stdexcept>


namespace pwiz {
namespace msdata {


using namespace std;
using boost::shared_ptr;
using boost::lexical_cast;
using boost::bad_lexical_cast;
using namespace pwiz::raw;
using namespace pwiz::util;
namespace bfs = boost::filesystem;
using namespace pwiz::msdata::detail;


//
// Reader_Thermo
//


bool Reader_Thermo::hasRAWHeader(const string& head)
{
    return _hasRAWHeader(head);
}

namespace {

auto_ptr<RawFileLibrary> rawFileLibrary_;

void fillInstrumentComponentMetadata(RawFile& rawfile, MSData& msd, InstrumentConfigurationPtr& instrumentConfiguration)
{
    auto_ptr<ScanInfo> firstScanInfo = rawfile.getScanInfo(1);

    instrumentConfiguration->componentList.source.order = 1;
    instrumentConfiguration->componentList.source.cvParams.push_back(translateAsIonizationType(firstScanInfo->ionizationType()));
    if (translateAsInletType(firstScanInfo->ionizationType()).cvid != CVID_Unknown)
        instrumentConfiguration->componentList.source.cvParams.push_back(translateAsInletType(firstScanInfo->ionizationType()));

    // due diligence to determine the mass analyzer(s) (TODO: also try to get a quantative resolution estimate)
    instrumentConfiguration->componentList.analyzer.order = 2;
    string model = boost::to_lower_copy( rawfile.value(InstName) + rawfile.value(InstModel) );
    if (model.find("ltq") != string::npos)
        instrumentConfiguration->componentList.analyzer.cvParams.push_back(CVParam(MS_ion_trap));
    if (model.find("ft") != string::npos)
        instrumentConfiguration->componentList.analyzer.cvParams.push_back(CVParam(MS_FT_ICR));
    if (model.find("orbitrap") != string::npos)
        instrumentConfiguration->componentList.analyzer.cvParams.push_back(CVParam(MS_orbitrap));
    if (model.find("tsq") != string::npos || model.find("quantum") != string::npos)
        instrumentConfiguration->componentList.analyzer.cvParams.push_back(CVParam(MS_quadrupole));
    if (model.find("tof") != string::npos)
        instrumentConfiguration->componentList.analyzer.cvParams.push_back(CVParam(MS_time_of_flight));
    if (model.find("sector") != string::npos)
        instrumentConfiguration->componentList.analyzer.cvParams.push_back(CVParam(MS_magnetic_sector));

    instrumentConfiguration->componentList.detector.order = 3;
    // TODO: verify that all Thermo instruments use EM
    instrumentConfiguration->componentList.detector.cvParams.push_back(CVParam(MS_electron_multiplier));
}

void fillInMetadata(const string& filename, RawFile& rawfile, MSData& msd)
{
    msd.cvs.resize(1);
    CV& cv = msd.cvs.front();
    cv.URI = "psi-ms.obo"; 
    cv.id = "MS";
    cv.fullName = "Proteomics Standards Initiative Mass Spectrometry Ontology";
    cv.version = "1.0";

    msd.fileDescription.fileContent.cvParams.push_back(translateAsSpectrumType(rawfile.getScanInfo(1)->scanType()));

    SourceFilePtr sourceFile(new SourceFile);
    bfs::path p(filename);
    sourceFile->id = "rawfile";
    sourceFile->name = p.leaf();
    sourceFile->location = p.branch_path().string();
    sourceFile->cvParams.push_back(MS_Xcalibur_RAW_file);
    string sha1 = SHA1Calculator::hashFile(filename);
    sourceFile->cvParams.push_back(CVParam(MS_SHA_1, sha1));
    msd.fileDescription.sourceFilePtrs.push_back(sourceFile);

    SoftwarePtr softwareXcalibur(new Software);
    softwareXcalibur->id = "Xcalibur";
    softwareXcalibur->softwareParam = MS_Xcalibur;
    softwareXcalibur->softwareParamVersion = rawfile.value(InstSoftwareVersion);
    msd.softwarePtrs.push_back(softwareXcalibur);

    SoftwarePtr softwarePwiz(new Software);
    softwarePwiz->id = "pwiz::msdata::Reader_Thermo";
    softwarePwiz->softwareParam = MS_pwiz;
    softwarePwiz->softwareParamVersion = "1.0"; 
    msd.softwarePtrs.push_back(softwarePwiz);

    DataProcessingPtr dpPwiz(new DataProcessing);
    dpPwiz->id = "pwiz::msdata::Reader_Thermo conversion";
    dpPwiz->softwarePtr = softwarePwiz;
    dpPwiz->processingMethods.push_back(ProcessingMethod());
    dpPwiz->processingMethods.back().cvParams.push_back(MS_Conversion_to_mzML);
    msd.dataProcessingPtrs.push_back(dpPwiz);

    CVTranslator cvTranslator;

    InstrumentConfigurationPtr instrumentConfiguration(new InstrumentConfiguration);
    string model = rawfile.value(InstModel);
    CVID cvidModel = cvTranslator.translate(model);
    if (cvidModel != CVID_Unknown) 
    {
        instrumentConfiguration->cvParams.push_back(cvidModel);
        instrumentConfiguration->id = cvinfo(cvidModel).name;
    }
    else
    {
        instrumentConfiguration->userParams.push_back(UserParam("instrument model", model));
        instrumentConfiguration->id = model;
    }
    instrumentConfiguration->cvParams.push_back(CVParam(MS_instrument_serial_number, 
                                           rawfile.value(InstSerialNumber)));
    instrumentConfiguration->softwarePtr = softwareXcalibur;
    fillInstrumentComponentMetadata(rawfile, msd, instrumentConfiguration);
    msd.instrumentConfigurationPtrs.push_back(instrumentConfiguration);

    msd.run.id = filename;
    //msd.run.startTimeStamp = rawfile.getCreationDate(); // TODO format: 2007-06-27T15:23:45.00035
    msd.run.instrumentConfigurationPtr = instrumentConfiguration;
}

} // namespace


bool Reader_Thermo::accept(const string& filename, const string& head) const
{
    return hasRAWHeader(head);
}


void Reader_Thermo::read(const string& filename, 
                      const string& head,
                      MSData& result) const
{
    // initialize RawFileLibrary

	if (!rawFileLibrary_.get()) {
		rawFileLibrary_.reset(new RawFileLibrary());
	}

    // instantiate RawFile, share ownership with SpectrumList_Thermo

    shared_ptr<RawFile> rawfile(RawFile::create(filename).release());
    rawfile->setCurrentController(Controller_MS, 1);

    SpectrumList_Thermo* sl = new SpectrumList_Thermo(result, rawfile);
    result.run.spectrumListPtr = SpectrumListPtr(sl);
    result.run.chromatogramListPtr = sl->Chromatograms();

    fillInMetadata(filename, *rawfile, result);
}


} // namespace msdata
} // namespace pwiz


#else // PWIZ_NO_READER_RAW /////////////////////////////////////////////////////////////////////////////

//
// non-MSVC implementation
//

#include "Reader_Thermo.hpp"
#include <stdexcept>

namespace pwiz {
namespace msdata {

using namespace std;

bool Reader_Thermo::accept(const string& filename, const string& head) const
{
    return false;
}

void Reader_Thermo::read(const string& filename, const string& head, MSData& result) const
{
    throw runtime_error("[Reader_Thermo::read()] Not implemented."); 
}

bool Reader_Thermo::hasRAWHeader(const string& head)
{   
    return _hasRAWHeader(head);
}

} // namespace msdata
} // namespace pwiz

#endif // PWIZ_NO_READER_RAW /////////////////////////////////////////////////////////////////////////////

