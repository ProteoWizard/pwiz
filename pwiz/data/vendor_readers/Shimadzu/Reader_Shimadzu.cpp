//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2014 Vanderbilt University - Nashville, TN 37232
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

#include "Reader_Shimadzu.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/String.hpp"
#include <boost/filesystem/detail/utf8_codecvt_facet.hpp>


PWIZ_API_DECL std::string pwiz::msdata::Reader_Shimadzu::identify(const std::string& filename, const std::string& head) const
{
    if (bal::iends_with(filename, ".lcd"))
         return getType();

    return "";
}


#ifdef PWIZ_READER_SHIMADZU
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "pwiz/data/msdata/Version.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz_aux/msrc/utility/vendor_api/Shimadzu/ShimadzuReader.hpp"
#include "SpectrumList_Shimadzu.hpp"
#include "ChromatogramList_Shimadzu.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace msdata {


using namespace pwiz::util;
using namespace pwiz::msdata::detail;
namespace Shimadzu = pwiz::vendor_api::Shimadzu;


//
// Reader_Shimadzu
//


namespace {

void initializeInstrumentConfigurationPtrs(MSData& msd,
                                           Shimadzu::ShimadzuReaderPtr rawfile,
                                           const SoftwarePtr& instrumentSoftware)
{
    // create instrument configuration templates based on the instrument model
    vector<InstrumentConfigurationPtr> configurations(1, InstrumentConfigurationPtr(new InstrumentConfiguration));

    for (size_t i=0; i < configurations.size(); ++i)
    {
        InstrumentConfigurationPtr ic = InstrumentConfigurationPtr(new InstrumentConfiguration);

        ic->id = (format("IC%d") % (i+1)).str();
        ic->softwarePtr = instrumentSoftware;
        ic->set(MS_Shimadzu_instrument_model);

        ic->componentList.emplace_back(Component(MS_ESI, 1));

        if (rawfile->getScanCount() == 0)
        {
            ic->componentList.emplace_back(Component(MS_quadrupole, 2));
            ic->componentList.emplace_back(Component(MS_quadrupole, 3));
            ic->componentList.emplace_back(Component(MS_quadrupole, 4));
            ic->componentList.emplace_back(Component(MS_conversion_dynode_electron_multiplier, 5));
            ic->componentList[4].set(MS_pulse_counting);
        }
        else
        {
            ic->componentList.emplace_back(Component(MS_quadrupole, 2));
            ic->componentList.emplace_back(Component(MS_quadrupole, 3));
            ic->componentList.emplace_back(Component(MS_TOF, 4));
            ic->componentList.emplace_back(Component(MS_microchannel_plate_detector, 5));
            ic->componentList[4].set(MS_pulse_counting);
        }

        msd.instrumentConfigurationPtrs.push_back(ic);
    }
}


void fillInMetadata(const string& rawpath, Shimadzu::ShimadzuReaderPtr rawfile, MSData& msd, const Reader::Config& config)
{
    msd.cvs = defaultCVList();

    if (rawfile->getScanCount() == 0)
        msd.fileDescription.fileContent.set(MS_SRM_chromatogram);
    else
    {
        if (rawfile->getMSLevels().count(1) > 0)
            msd.fileDescription.fileContent.set(MS_MS1_spectrum);
        if (rawfile->getMSLevels().count(2) > 0)
            msd.fileDescription.fileContent.set(MS_MSn_spectrum);
        if (msd.fileDescription.fileContent.empty())
            throw runtime_error("[Reader_Shimadzu::fillInMetadata] unexpected values from getMSLevels()");
    }

    boost::filesystem::detail::utf8_codecvt_facet utf8;
    bfs::path p(rawpath, utf8);

    SourceFilePtr sourceFile(new SourceFile);
    sourceFile->id = p.filename().string(utf8);
    sourceFile->name = p.filename().string(utf8);
    sourceFile->location = "file:///" + bfs::system_complete(p.branch_path()).string(utf8);
    sourceFile->set(MS_scan_number_only_nativeID_format);
    sourceFile->set(MS_mass_spectrometer_file_format);
    msd.fileDescription.sourceFilePtrs.push_back(sourceFile);

    msd.run.defaultSourceFilePtr = sourceFile;

    msd.id = p.filename().replace_extension("").string(utf8);

    SoftwarePtr softwareShimadzu(new Software);
    softwareShimadzu->id = "Shimadzu software";
    softwareShimadzu->set(MS_Shimadzu_Corporation_software);
    softwareShimadzu->version = "4.0";
    msd.softwarePtrs.push_back(softwareShimadzu);

    SoftwarePtr softwarePwiz(new Software);
    softwarePwiz->id = "pwiz";
    softwarePwiz->set(MS_pwiz);
    softwarePwiz->version = pwiz::msdata::Version::str();
    msd.softwarePtrs.push_back(softwarePwiz);

    DataProcessingPtr dpPwiz(new DataProcessing);
    dpPwiz->id = "pwiz_Reader_Shimadzu_conversion";
    dpPwiz->processingMethods.push_back(ProcessingMethod());
    dpPwiz->processingMethods.back().softwarePtr = softwarePwiz;
    dpPwiz->processingMethods.back().set(MS_Conversion_to_mzML);

    // give ownership of dpPwiz to the SpectrumList (and ChromatogramList)
    SpectrumList_Shimadzu* sl = dynamic_cast<SpectrumList_Shimadzu*>(msd.run.spectrumListPtr.get());
    ChromatogramList_Shimadzu* cl = dynamic_cast<ChromatogramList_Shimadzu*>(msd.run.chromatogramListPtr.get());
    if (sl) sl->setDataProcessingPtr(dpPwiz);
    if (cl) cl->setDataProcessingPtr(dpPwiz);

    initializeInstrumentConfigurationPtrs(msd, rawfile, softwareShimadzu);
    if (!msd.instrumentConfigurationPtrs.empty())
        msd.run.defaultInstrumentConfigurationPtr = msd.instrumentConfigurationPtrs[0];

    msd.run.id = msd.id;

    auto analysisDate = rawfile->getAnalysisDate(config.adjustUnknownTimeZonesToHostTimeZone);
    if (!analysisDate.is_not_a_date_time())
        msd.run.startTimeStamp = encode_xml_datetime(analysisDate);
}

} // namespace


PWIZ_API_DECL
void Reader_Shimadzu::read(const string& filename,
                         const string& head,
                         MSData& result,
                         int runIndex /* = 0 */,
                         const Config& config) const
{
    if (runIndex != 0)
        throw ReaderFail("[Reader_Shimadzu::read] multiple runs not supported");

    // instantiate RawFile, share ownership with SpectrumList_Shimadzu

    Shimadzu::ShimadzuReaderPtr dataReader(Shimadzu::ShimadzuReader::create(filename));

    shared_ptr<SpectrumList_Shimadzu> sl(new SpectrumList_Shimadzu(result, dataReader, config));
    shared_ptr<ChromatogramList_Shimadzu> cl(new ChromatogramList_Shimadzu(dataReader, config));
    result.run.spectrumListPtr = sl;
    result.run.chromatogramListPtr = cl;

    fillInMetadata(filename, dataReader, result, config);
}


} // namespace msdata
} // namespace pwiz


#else // PWIZ_READER_SHIMADZU /////////////////////////////////////////////////////////////////////////////

//
// non-MSVC implementation
//

#include "pwiz/utility/misc/String.hpp"

namespace pwiz {
namespace msdata {


PWIZ_API_DECL void Reader_Shimadzu::read(const string& filename, const string& head, MSData& result, int sampleIndex /* = 0 */, const Config& config) const
{
    throw ReaderFail("[Reader_Shimadzu::read()] Shimadzu reader not implemented: "
#ifdef _MSC_VER // should be possible, apparently somebody decided to skip it
        "support was explicitly disabled when program was built"
#elif defined(WIN32) // wrong compiler
        "program was built without COM support and cannot access Shimadzu DLLs - try building with MSVC instead of GCC"
#else // wrong platform
        "Shimadzu DLLs only work on Windows"
#endif
        );
}


} // namespace msdata
} // namespace pwiz

#endif // PWIZ_READER_SHIMADZU /////////////////////////////////////////////////////////////////////////////

