//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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

// CompassXtractMS DLL usage is msvc only - mingw doesn't provide com support
#if (!defined(_MSC_VER) && defined(PWIZ_READER_BRUKER))
#undef PWIZ_READER_BRUKER
#endif


#include "Reader_Bruker.hpp"
#include "Reader_Bruker_Detail.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/data/msdata/Version.hpp"
#include <stdexcept>


// A Bruker Analysis source (representing a "run") is actually a directory
// It contains several files related to a single acquisition, e.g.:
// fid, acqu, acqus, Analysis.FAMethod, AnalysisParameter.xml, sptype

PWIZ_API_DECL
std::string pwiz::msdata::Reader_Bruker::identify(const std::string& filename,
                                                  const std::string& head) const
{
    using namespace pwiz::msdata::detail::Bruker;

    switch (pwiz::msdata::detail::Bruker::format(filename))
    {
        case Reader_Bruker_Format_FID: return "Bruker FID";
        case Reader_Bruker_Format_YEP: return "Bruker YEP";
        case Reader_Bruker_Format_BAF: return "Bruker BAF";
        case Reader_Bruker_Format_U2: return "Bruker U2";
        case Reader_Bruker_Format_BAF_and_U2: return "Bruker BAF/U2";
        case Reader_Bruker_Format_TDF: return "Bruker TDF";

        case Reader_Bruker_Format_Unknown:
        default:
            return "";
    }
}


#ifdef PWIZ_READER_BRUKER
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "SpectrumList_Bruker.hpp"
#include "ChromatogramList_Bruker.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace msdata {


using namespace pwiz::util;
using namespace pwiz::msdata::detail;
using namespace pwiz::msdata::detail::Bruker;
using namespace pwiz::vendor_api::Bruker;


//
// Reader_Bruker
//

namespace {

void initializeInstrumentConfigurationPtrs(MSData& msd, CompassDataPtr rawfile, const SoftwarePtr& instrumentSoftware)
{
    CVID cvidSeries = translateAsInstrumentSeries(rawfile);

    // set common instrument parameters
    ParamGroupPtr commonInstrumentParams(new ParamGroup);
    commonInstrumentParams->id = "CommonInstrumentParams";
    msd.paramGroupPtrs.push_back(commonInstrumentParams);

    if (!rawfile->getInstrumentDescription().empty())
        commonInstrumentParams->userParams.push_back(UserParam("instrument model", rawfile->getInstrumentDescription()));
    commonInstrumentParams->set(cvidSeries);

    // create instrument configuration templates based on the instrument model
    vector<InstrumentConfiguration> configurations = createInstrumentConfigurations(rawfile);
    if (configurations.empty())
        configurations.resize(1); // provide at least one configuration

    for (size_t i = 0; i < configurations.size(); ++i)
    {
        InstrumentConfigurationPtr ic = InstrumentConfigurationPtr(new InstrumentConfiguration(configurations[i]));

        ic->id = (boost::format("IC%d") % (i + 1)).str();
        ic->paramGroupPtrs.push_back(commonInstrumentParams);
        ic->softwarePtr = instrumentSoftware;

        msd.instrumentConfigurationPtrs.push_back(ic);
    }
}

void fillInMetadata(const bfs::path& rootpath, MSData& msd, Reader_Bruker_Format format, CompassDataPtr compassDataPtr)
{
    msd.cvs = defaultCVList();

    msd.id = bfs::basename(rootpath);

    SoftwarePtr apiSoftware(new Software);
    msd.softwarePtrs.push_back(apiSoftware);
    switch (format)
    {
        case Reader_Bruker_Format_BAF:
        case Reader_Bruker_Format_BAF_and_U2:
            apiSoftware->id = "BAF2SQL";
            apiSoftware->set(MS_Bruker_software);
            apiSoftware->userParams.emplace_back("software name", "BAF2SQL");
            apiSoftware->version = "2.7.300.20-112";
            break;

        case Reader_Bruker_Format_TDF:
            apiSoftware->id = "TIMS_SDK";
            apiSoftware->set(MS_Bruker_software);
            apiSoftware->userParams.emplace_back("software name", "TIMS SDK");
            apiSoftware->version = "2.3.101.131-791";
            break;

        default:
            apiSoftware->id = "CompassXtract";
            apiSoftware->set(MS_CompassXtract);
            apiSoftware->version = "3.1.7";
            break;
    }

    SoftwarePtr acquisitionSoftware(new Software);
    CVID acquisitionSoftwareCvid = translateAsAcquisitionSoftware(compassDataPtr);
    acquisitionSoftware->id = cvTermInfo(acquisitionSoftwareCvid).shortName();
    acquisitionSoftware->set(acquisitionSoftwareCvid);
    acquisitionSoftware->version = compassDataPtr->getAcquisitionSoftwareVersion();
    msd.softwarePtrs.push_back(acquisitionSoftware);

    SoftwarePtr softwarePwiz(new Software);
    softwarePwiz->id = "pwiz_Reader_Bruker";
    softwarePwiz->set(MS_pwiz);
    softwarePwiz->version = pwiz::msdata::Version::str();
    msd.softwarePtrs.push_back(softwarePwiz);

    DataProcessingPtr dpPwiz(new DataProcessing);
    dpPwiz->id = "pwiz_Reader_Bruker_conversion";
    dpPwiz->processingMethods.push_back(ProcessingMethod());
    dpPwiz->processingMethods.back().softwarePtr = softwarePwiz;
    dpPwiz->processingMethods.back().set(MS_Conversion_to_mzML);

    // give ownership of dpPwiz to the SpectrumList (and ChromatogramList)
    SpectrumList_Bruker* sl = dynamic_cast<SpectrumList_Bruker*>(msd.run.spectrumListPtr.get());
    ChromatogramList_Bruker* cl = dynamic_cast<ChromatogramList_Bruker*>(msd.run.chromatogramListPtr.get());
    if (sl) sl->setDataProcessingPtr(dpPwiz);
    if (cl) cl->setDataProcessingPtr(dpPwiz);

    bool hasMS1 = false;
    bool hasMSn = false;
    size_t scan=1, end=compassDataPtr->getMSSpectrumCount();
    if (format == Reader_Bruker_Format_FID) --scan, --end;
    for (; scan <= end && (!hasMS1 || !hasMSn); ++scan)
    {
        int msLevel = sl->getMSSpectrumPtr(scan, vendor_api::Bruker::DetailLevel_InstantMetadata)->getMSMSStage();
        if (!hasMS1 && msLevel == 1)
        {
            hasMS1 = true;
            msd.fileDescription.fileContent.set(MS_MS1_spectrum);
        }
        else if (!hasMSn && msLevel > 1)
        {
            hasMSn = true;
            msd.fileDescription.fileContent.set(MS_MSn_spectrum);
        }
    }

    initializeInstrumentConfigurationPtrs(msd, compassDataPtr, acquisitionSoftware);
    if (!msd.instrumentConfigurationPtrs.empty())
        msd.run.defaultInstrumentConfigurationPtr = msd.instrumentConfigurationPtrs[0];

    msd.run.id = msd.id;
    msd.run.startTimeStamp = encode_xml_datetime(compassDataPtr->getAnalysisDateTime());
}

} // namespace


PWIZ_API_DECL
void Reader_Bruker::read(const string& filename,
                         const string& head,
                         MSData& result,
                         int runIndex,
                         const Config& config) const
{
    if (runIndex != 0)
        throw ReaderFail("[Reader_Bruker::read] multiple runs not supported");

    string::const_iterator unicodeCharItr = std::find_if(filename.begin(), filename.end(), [](char ch) { return !isprint(ch) || static_cast<int>(ch) < 0; });
    if (unicodeCharItr != filename.end())
    {
        auto utf8CharAsString = [](string::const_iterator ch, string::const_iterator end) { string utf8; while (ch != end && *ch < 0) { utf8 += *ch; ++ch; }; return utf8; };
        throw ReaderFail(string("[Reader_Bruker::read()] Bruker API does not support Unicode in filepaths ('") + utf8CharAsString(unicodeCharItr, filename.end()) + "')");
    }

    Reader_Bruker_Format format = Bruker::format(filename);
    if (format == Reader_Bruker_Format_Unknown)
        throw ReaderFail("[Reader_Bruker::read] Path given is not a recognized Bruker format");


    // trim filename from end of source path if necessary (it's not valid to pass to CompassXtract)
    bfs::path rootpath = filename;
    if (bfs::is_regular_file(rootpath))
        rootpath = rootpath.branch_path();

    CompassDataPtr compassDataPtr(CompassData::create(rootpath.string(), config.combineIonMobilitySpectra, format, config.preferOnlyMsLevel));

    SpectrumList_Bruker* sl = new SpectrumList_Bruker(result, rootpath.string(), format, compassDataPtr, config);
    ChromatogramList_Bruker* cl = new ChromatogramList_Bruker(result, rootpath.string(), format, compassDataPtr);
    result.run.spectrumListPtr = SpectrumListPtr(sl);
    result.run.chromatogramListPtr = ChromatogramListPtr(cl);

    fillInMetadata(rootpath, result, format, compassDataPtr);
}


} // namespace msdata
} // namespace pwiz


#else // PWIZ_READER_BRUKER

//
// non-MSVC implementation
//

namespace pwiz {
namespace msdata {

PWIZ_API_DECL void Reader_Bruker::read(const string& filename, const string& head, MSData& result, int sampleIndex /* = 0 */, const Config& config) const
{
    throw ReaderFail("[Reader_Bruker::read()] Bruker Analysis reader not implemented: "
#ifdef _MSC_VER // should be possible, apparently somebody decided to skip it
        "support was explicitly disabled when program was built"
#elif defined(WIN32) // wrong compiler
        "program was built without COM support and cannot access CompassXtract DLLs - try building with MSVC instead of GCC"
#else // wrong platform
        "requires CompassXtract which only works on Windows"
#endif
        );
}

} // namespace msdata
} // namespace pwiz

#endif // PWIZ_READER_BRUKER
