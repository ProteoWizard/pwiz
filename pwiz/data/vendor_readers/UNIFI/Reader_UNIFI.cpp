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

#include "Reader_UNIFI.hpp"
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/data/msdata/Version.hpp"
#include <boost/foreach_field.hpp>


PWIZ_API_DECL std::string pwiz::msdata::Reader_UNIFI::identify(const std::string& filename, const std::string& head) const
{
    std::string result;
    if ((bal::istarts_with(filename, "http://") || bal::istarts_with(filename, "https://")) && bal::icontains(filename, "/sampleresults"))
        result = getType();
    return result;
}


#ifdef PWIZ_READER_UNIFI
#include "pwiz_aux/msrc/utility/vendor_api/UNIFI/UnifiData.hpp"
#include "SpectrumList_UNIFI.hpp"
//#include "ChromatogramList_UNIFI.hpp"
//#include "Reader_UNIFI_Detail.hpp"


namespace pwiz {
namespace msdata {


using namespace pwiz::util;
using namespace pwiz::msdata::detail;
//using namespace pwiz::msdata::detail::UNIFI;


//
// Reader_UNIFI
//

namespace {

void fillInMetadata(const string& sampleResultUrl, MSData& msd, const UnifiDataPtr& unifiData, const Reader::Config& config)
{
    msd.cvs = defaultCVList();

    /*BOOST_FOREACH_FIELD((boost::fusion::ignore)(const ExperimentPtr& msExperiment), experimentsMap)
    {
        if (msExperiment->getExperimentType() != MRM)
            msd.fileDescription.fileContent.set(translateAsSpectrumType(msExperiment->getExperimentType()));
        else
            msd.fileDescription.fileContent.set(MS_SRM_chromatogram);
    }*/

    SourceFilePtr sourceFile(new SourceFile);
    bfs::path p(sampleResultUrl);
    sourceFile->id = "UNIFI";
    sourceFile->name = p.filename().string();
    sourceFile->location = p.parent_path().string();
    //sourceFile->set(MS_WIFF_nativeID_format);
    //sourceFile->set(MS_UNIFI_WIFF_format);
    msd.fileDescription.sourceFilePtrs.push_back(sourceFile);

    msd.run.defaultSourceFilePtr = sourceFile;

    string sampleName = unifiData->getSampleName();
    msd.id = sampleName.empty() ? sourceFile->name : sampleName;
    if (!unifiData->getWellPosition().empty())
    {
        msd.id += "_" + unifiData->getWellPosition();
        sourceFile->userParams.emplace_back("well position", unifiData->getWellPosition(), "xsd:string");
    }
    msd.id += "_" + lexical_cast<string>(unifiData->getReplicateNumber());
    sourceFile->userParams.emplace_back("replicate number", lexical_cast<string>(unifiData->getReplicateNumber()), "xsd:positiveInteger");

    SoftwarePtr acquisitionSoftware(new Software);
    acquisitionSoftware->id = "UNIFI";
    acquisitionSoftware->set(MS_UNIFY);
    acquisitionSoftware->version = "1.0";
    msd.softwarePtrs.push_back(acquisitionSoftware);

    SoftwarePtr softwarePwiz(new Software);
    softwarePwiz->id = "pwiz_Reader_UNIFI";
    softwarePwiz->set(MS_pwiz);
    softwarePwiz->version = pwiz::msdata::Version::str();
    msd.softwarePtrs.push_back(softwarePwiz);

    DataProcessingPtr dpPwiz(new DataProcessing);
    dpPwiz->id = "pwiz_Reader_UNIFI_conversion";
    dpPwiz->processingMethods.push_back(ProcessingMethod());
    dpPwiz->processingMethods.back().softwarePtr = softwarePwiz;
    dpPwiz->processingMethods.back().set(MS_Conversion_to_mzML);

    // give ownership of dpPwiz to the SpectrumList (and ChromatogramList)
    SpectrumList_UNIFI* sl = dynamic_cast<SpectrumList_UNIFI*>(msd.run.spectrumListPtr.get());
    //ChromatogramList_UNIFI* cl = dynamic_cast<ChromatogramList_UNIFI*>(msd.run.chromatogramListPtr.get());
    if (sl) sl->setDataProcessingPtr(dpPwiz);
    //if (cl) cl->setDataProcessingPtr(dpPwiz);

    InstrumentConfigurationPtr ic(new InstrumentConfiguration("IC1"));
    ic->set(MS_Waters_instrument_model);
    ic->softwarePtr = acquisitionSoftware;
    msd.instrumentConfigurationPtrs.push_back(ic);
    msd.run.defaultInstrumentConfigurationPtr = ic;

    msd.run.id = msd.id;
    msd.run.startTimeStamp = encode_xml_datetime(unifiData->getAcquisitionStartTime());
}

} // namespace


PWIZ_API_DECL
void Reader_UNIFI::read(const string& sampleResultUrl,
                        const string& head,
                        MSData& result,
                        int runIndex,
                        const Config& config) const
{
    try
    {
        UnifiDataPtr unifiData(new UnifiData(sampleResultUrl, config.combineIonMobilitySpectra));

        SpectrumList_UNIFI* sl = new SpectrumList_UNIFI(result, unifiData, config);
        //ChromatogramList_UNIFI* cl = new ChromatogramList_UNIFI(result, wifffile, experimentsMap, runIndex);
        result.run.spectrumListPtr = SpectrumListPtr(sl);
        //result.run.chromatogramListPtr = ChromatogramListPtr(cl);

        fillInMetadata(sampleResultUrl, result, unifiData, config);
    }
    catch (std::exception& e)
    {
        throw std::runtime_error(e.what());
    }
    catch (...)
    {
        throw runtime_error("[Reader_UNIFI::read()] unhandled exception");
    }
}


PWIZ_API_DECL
void Reader_UNIFI::read(const string& sampleResultUrl,
                        const string& head,
                        vector<MSDataPtr>& results,
                        const Config& config) const
{
    results.push_back(MSDataPtr(new MSData));
    read(sampleResultUrl, head, *results.back(), 0, config);
}


PWIZ_API_DECL
void Reader_UNIFI::readIds(const string& sampleResultUrl,
                           const string& head,
                           vector<string>& results,
                           const Config& config) const
{
    try
    {
        UnifiDataPtr unifiData(new UnifiData(sampleResultUrl, config.combineIonMobilitySpectra));
        results.push_back(unifiData->getSampleName());
    }
    catch (std::exception& e)
    {
        throw std::runtime_error(e.what());
    }
    catch (...)
    {
        throw runtime_error("[Reader_UNIFI::readIds()] unhandled exception");
    }
}

} // namespace msdata
} // namespace pwiz


#else // PWIZ_READER_UNIFI

//
// non-MSVC implementation
//

#include "Reader_UNIFI.hpp"
#include "pwiz/utility/misc/String.hpp"

namespace pwiz {
namespace msdata {

PWIZ_API_DECL void Reader_UNIFI::read(const string& filename, const string& head, MSData& result, int runIndex, const Config& config) const
{
    throw ReaderFail("[Reader_UNIFI::read()] Waters UNIFI reader not implemented: "
#ifdef _MSC_VER // should be possible, apparently somebody decided to skip it
        "support was explicitly disabled when program was built"
#elif defined(WIN32) // wrong compiler
        "program was built without COM support and cannot access DLLs - try building with MSVC instead of GCC"
#else // wrong platform
        "requires .NET DLLs which only work on Windows"
#endif
    );
}

PWIZ_API_DECL void Reader_UNIFI::read(const string& filename, const string& head, vector<MSDataPtr>& results, const Config& config) const
{
    throw ReaderFail("[Reader_UNIFI::read()] Waters UNIFI reader not implemented: "
#ifdef _MSC_VER // should be possible, apparently somebody decided to skip it
        "support was explicitly disabled when program was built"
#elif defined(WIN32) // wrong compiler
        "program was built without COM support and cannot access DLLs - try building with MSVC instead of GCC"
#else // wrong platform
        "requires .NET DLLs which only work on Windows"
#endif
    );
}

PWIZ_API_DECL void Reader_UNIFI::readIds(const std::string& filename, const std::string& head, std::vector<std::string>& results, const Config& config) const
{
    throw ReaderFail("[Reader_UNIFI::read()] Waters UNIFI reader not implemented: "
#ifdef _MSC_VER // should be possible, apparently somebody decided to skip it
        "support was explicitly disabled when program was built"
#elif defined(WIN32) // wrong compiler
        "program was built without COM support and cannot access DLLs - try building with MSVC instead of GCC"
#else // wrong platform
        "requires .NET DLLs which only work on Windows"
#endif
    );
}

} // namespace msdata
} // namespace pwiz

#endif // PWIZ_READER_UNIFI
