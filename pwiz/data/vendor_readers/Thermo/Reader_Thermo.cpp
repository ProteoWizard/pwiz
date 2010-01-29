//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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


#define PWIZ_SOURCE

#include "Reader_Thermo.hpp"
#include "pwiz/utility/misc/String.hpp"


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


#ifdef PWIZ_READER_THERMO
#include "pwiz/utility/vendor_api/thermo/RawFile.h"
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "pwiz/data/msdata/Version.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "Reader_Thermo_Detail.hpp"
#include "SpectrumList_Thermo.hpp"
#include "ChromatogramList_Thermo.hpp"
#include "boost/shared_ptr.hpp"
#include <iostream>
#include <iomanip>
#include <stdexcept>
#include <numeric>


namespace pwiz {
namespace msdata {


using namespace std;
using boost::shared_ptr;
using namespace pwiz::vendor_api::Thermo;
using namespace pwiz::util;
using namespace pwiz::msdata::detail;


//
// Reader_Thermo
//


PWIZ_API_DECL bool Reader_Thermo::hasRAWHeader(const string& head)
{
    return _hasRAWHeader(head);
}


namespace {

void initializeInstrumentConfigurationPtrs(MSData& msd,
                                           RawFile& rawfile,
                                           const SoftwarePtr& instrumentSoftware)
{
    CVID cvidModel = translateAsInstrumentModel(rawfile.getInstrumentModel());

    // set common instrument parameters
    ParamGroupPtr commonInstrumentParams(new ParamGroup);
    commonInstrumentParams->id = "CommonInstrumentParams";
    msd.paramGroupPtrs.push_back(commonInstrumentParams);

    if (cvidModel == MS_Thermo_Electron_instrument_model)
        commonInstrumentParams->userParams.push_back(UserParam("instrument model", rawfile.value(InstModel)));
    commonInstrumentParams->set(cvidModel);
    commonInstrumentParams->set(MS_instrument_serial_number, rawfile.value(InstSerialNumber));

    // create instrument configuration templates based on the instrument model
    vector<InstrumentConfiguration> configurations = createInstrumentConfigurations(rawfile);

    for (size_t i=0; i < configurations.size(); ++i)
    {
        InstrumentConfigurationPtr ic = InstrumentConfigurationPtr(new InstrumentConfiguration(configurations[i]));

        ic->id = (format("IC%d") % (i+1)).str();
        ic->paramGroupPtrs.push_back(commonInstrumentParams);
        ic->softwarePtr = instrumentSoftware;

        msd.instrumentConfigurationPtrs.push_back(ic);
    }
}


void fillInMetadata(const string& filename, RawFile& rawfile, MSData& msd)
{
    msd.cvs = defaultCVList();

    SourceFilePtr sourceFile(new SourceFile);
    bfs::path p(filename);
    sourceFile->id = "RAW1";
    sourceFile->name = p.leaf();
    string location = bfs::complete(p.branch_path()).string();
    if (location.empty()) location = ".";
    sourceFile->location = string("file://") + location;
    sourceFile->set(MS_Thermo_nativeID_format);
    sourceFile->set(MS_Thermo_RAW_file);
    msd.fileDescription.sourceFilePtrs.push_back(sourceFile);

    msd.run.defaultSourceFilePtr = sourceFile;

    msd.id = bfs::basename(p);

    SoftwarePtr softwareXcalibur(new Software);
    softwareXcalibur->id = "Xcalibur";
    softwareXcalibur->set(MS_Xcalibur);
    softwareXcalibur->version = rawfile.value(InstSoftwareVersion);
    msd.softwarePtrs.push_back(softwareXcalibur);

    SoftwarePtr softwarePwiz(new Software);
    softwarePwiz->id = "pwiz";
    softwarePwiz->set(MS_pwiz);
    softwarePwiz->version = pwiz::msdata::Version::str();
    msd.softwarePtrs.push_back(softwarePwiz);

    DataProcessingPtr dpPwiz(new DataProcessing);
    dpPwiz->id = "pwiz_Reader_Thermo_conversion";
    dpPwiz->processingMethods.push_back(ProcessingMethod());
    dpPwiz->processingMethods.back().softwarePtr = softwarePwiz;
    dpPwiz->processingMethods.back().set(MS_Conversion_to_mzML);

    // give ownership of dpPwiz to the SpectrumList (and ChromatogramList)
    SpectrumList_Thermo* sl = dynamic_cast<SpectrumList_Thermo*>(msd.run.spectrumListPtr.get());
    ChromatogramList_Thermo* cl = dynamic_cast<ChromatogramList_Thermo*>(msd.run.chromatogramListPtr.get());
    if (sl) sl->setDataProcessingPtr(dpPwiz);
    if (cl) cl->setDataProcessingPtr(dpPwiz);

    // add file content metadata

    // the +3 offset is because MSOrder_NeutralLoss == -3
    if (sl->spectraByMSOrder[MSOrder_NeutralLoss+3] > 0)
        msd.fileDescription.fileContent.set(MS_constant_neutral_loss_spectrum);
    if (sl->spectraByMSOrder[MSOrder_NeutralGain+3] > 0)
        msd.fileDescription.fileContent.set(MS_constant_neutral_gain_spectrum);
    if (sl->spectraByMSOrder[MSOrder_ParentScan+3] > 0)
        msd.fileDescription.fileContent.set(MS_precursor_ion_spectrum);

    if (sl->spectraByScanType[ScanType_Full] > 0)
    {
        int simScanCount = sl->spectraByScanType[ScanType_SIM]; // MS1
        int srmScanCount = sl->spectraByScanType[ScanType_SRM]; // MS2

        // MS can be either Full scans or SIM scans so we compare against the SIM scan count
        if (sl->spectraByMSOrder[MSOrder_MS+3] > simScanCount)
            msd.fileDescription.fileContent.set(MS_MS1_spectrum);

        // MS2 can be either Full or SRM scans so we compare against the SRM scan count
        if (sl->spectraByMSOrder[MSOrder_MS2+3] > srmScanCount)
            msd.fileDescription.fileContent.set(MS_MSn_spectrum);
        
        // MS3+ scans are definitely MSn
        if (std::accumulate(sl->spectraByMSOrder.begin() + MSOrder_MS3 + 3,
                            sl->spectraByMSOrder.end(), 0) > 0)
            msd.fileDescription.fileContent.set(MS_MSn_spectrum);
    }

    // these scan types should be represented as chromatograms
    if (sl->spectraByScanType[ScanType_SIM] > 0)
        msd.fileDescription.fileContent.set(MS_SIM_chromatogram);
    if (sl->spectraByScanType[ScanType_SRM] > 0)
        msd.fileDescription.fileContent.set(MS_SRM_chromatogram);

    // add instrument configuration metadata
    initializeInstrumentConfigurationPtrs(msd, rawfile, softwareXcalibur);
    if (!msd.instrumentConfigurationPtrs.empty())
        msd.run.defaultInstrumentConfigurationPtr = msd.instrumentConfigurationPtrs[0];

    msd.run.id = msd.id;
    msd.run.startTimeStamp = encode_xml_datetime(rawfile.getCreationDate());
}

} // namespace


PWIZ_API_DECL std::string Reader_Thermo::identify(const string& filename, const string& head) const
{
	return std::string(hasRAWHeader(head)?getType():"");
}


PWIZ_API_DECL
void Reader_Thermo::read(const string& filename,
                         const string& head,
                         MSData& result,
                         int sampleIndex /* = 0 */) const
{
    if (sampleIndex != 0)
        throw ReaderFail("[Reader_Thermo::read] multiple samples not supported");

    // instantiate RawFile, share ownership with SpectrumList_Thermo

    RawFilePtr rawfile = RawFile::create(filename);
    rawfile->setCurrentController(Controller_MS, 1);

    shared_ptr<SpectrumList_Thermo> sl(new SpectrumList_Thermo(result, rawfile));
    shared_ptr<ChromatogramList_Thermo> cl(new ChromatogramList_Thermo(result, rawfile));
    result.run.spectrumListPtr = sl;
    result.run.chromatogramListPtr = cl;

    fillInMetadata(filename, *rawfile, result);
}


} // namespace msdata
} // namespace pwiz


#else // PWIZ_READER_THERMO

//
// non-MSVC implementation
//

#include <stdexcept>

namespace pwiz {
namespace msdata {

using namespace std;

PWIZ_API_DECL std::string Reader_Thermo::identify(const string& filename, const string& head) const
{
   // we know what this is, but we'll throw an exception on read
	return std::string(hasRAWHeader(head)?getType():"");
}

PWIZ_API_DECL void Reader_Thermo::read(const string& filename, const string& head, MSData& result, int sampleIndex /* = 0 */) const
{
	throw ReaderFail("[Reader_Thermo::read()] Thermo RAW reader not implemented: "
#ifdef _MSC_VER // should be possible, apparently somebody decided to skip it
		"support was explicitly disabled when program was built"
#elif defined(WIN32) // wrong compiler
		"program was built without COM support and cannot access Thermo DLLs - try building with MSVC instead of GCC"
#else // wrong platform
		"Thermo DLLs only work on Windows"
#endif
		);
}

PWIZ_API_DECL bool Reader_Thermo::hasRAWHeader(const string& head)
{
    return _hasRAWHeader(head);
}

} // namespace msdata
} // namespace pwiz

#endif // PWIZ_READER_THERMO

